# EXC-A1 — Desenho do login restrito do WPF

> Programa de Excelência TTSoft · Auditoria EXC-A1 (isolamento entre clientes e acesso a dados)
> Criado em: 2026-10-22 · Base: commit `8b46b6b`/`1ec7840` · **Status: desenho aprovado em 2026-10-22 — aguardando pré-verificações P1 a P4; nada executado**
> Registros: EXC-SEG-001 (Crítica), EXC-SEG-006 (Alta), EXC-SEG-004 · Decisão de origem: [PLANO-MESTRE §6.5](PLANO-MESTRE.md)

Este documento propõe **como** trocar a credencial que vai nos PCs das lojas. Nenhum comando
aqui foi executado. A execução só começa depois da revisão e aprovação do responsável.

---

## 1. Objetivo e limite

**Objetivo:** os PCs das lojas deixam de usar o administrador do servidor (`sa_ttsoft`) e
passam a usar um usuário que:

- só existe dentro do banco `ERPTTSoftStaging` (não abre o `ERPTTSoft` do OutfiTT nem o `master`);
- só lê e grava dados (sem criar ou alterar tabelas, usuários, permissões, backups);
- não enxerga as tabelas que guardam tokens de terceiros (Mercado Livre e Focus);
- tem senha nova, guardada só num gerenciador de senhas (resolve EXC-SEG-006).

**O que este passo NÃO resolve:** o novo usuário continua lendo e gravando dados de **todas
as lojas** do ConstruTTor, porque o banco não separa tenants e o WPF precisa ler os próprios
dados. Isso só acaba quando o WPF deixar de acessar o banco diretamente (migração WPF→API)
ou com segurança por linha no banco. Por isso, depois da execução, EXC-SEG-001 cai de
**Crítica** para **Alta**, não é encerrado.

| | Hoje (`sa_ttsoft`) | Depois (`construttor_wpf`) |
|---|---|---|
| Bancos acessíveis | Todos do servidor (ConstruTTor e OutfiTT) | Só `ERPTTSoftStaging` |
| Ler/gravar dados de todas as lojas do ConstruTTor | Sim | Sim (limite deste passo) |
| Criar/alterar tabelas, usuários, permissões | Sim | Não |
| Ler tokens do Mercado Livre e da Focus | Sim | Não |
| Criar bancos, logins, backups | Sim | Não |
| Senha exposta na conversa de 2026-10-22 | Sim | Não (senha nova) |

---

## 2. O que o WPF faz no banco (levantado no código em 2026-10-22)

| Verificação | Resultado | Evidência |
|---|---|---|
| Aplica migrations ao iniciar? | **Não.** Nenhum `Migrate`/`EnsureCreated` no WPF nem na API; migrations rodam pelo `dotnet ef` no PC do desenvolvedor | busca em `ERP.WPF`, `ERP.Api`, `ERP.Persistence`, `ERP.Infrastructure`, `ERP.Application` |
| Executa DDL ou comandos administrativos? | Só o `BACKUP DATABASE` do `BackupService`, que **não roda no Azure** (`IsAzureSql()` retorna antes) | `ERP.WPF/Helpers/BackupService.cs:62-71`, `:110` |
| Usa procedures, sequences, catálogo `sys.*`? | Não encontrado | busca por `EXEC`, `sp_`, `NEXT VALUE FOR`, `sys.`, `INFORMATION_SCHEMA` |
| Operações de dados | `SELECT`, `INSERT`, `UPDATE`, `DELETE` (EF Core e SQL parametrizado) em várias tabelas, incluindo `Users` (login local) e `AuditLogs` (auditoria no `SaveChanges`) | `ERP.WPF/App.xaml.cs:361-502` (serviços locais) |
| Toca `SalesChannels` (tokens do ML)? | **Não.** A tela de Integrações usa a API (`/api/saleschannels`); o repositório que lê a tabela só é registrado na API | `ERP.WPF/ViewModels/IntegracoesViewModel.cs:77-78`; `OrderSyncRepository` ausente do DI do WPF |
| Toca `TenantFiscalConfigurations` (token Focus)? | **Não.** O WPF usa `JsonFiscalConfigurationProvider` (arquivo local); só `DatabaseFiscalConfigurationProvider`, registrado na API, lê a tabela | `ERP.WPF/App.xaml.cs:390-394` |

Conclusão: o WPF precisa de **leitura e escrita de dados** no esquema `dbo` e de mais nada.

---

## 3. Pré-verificações (somente leitura, antes de qualquer mudança)

| # | O quê | Onde | Por quê |
|---|---|---|---|
| P1 | Qual `User Id` a API usa em `DefaultConnection` | Portal → App Service `erp-ttsoft-api` → Configuração → Cadeias de conexão (ler só `User Id`) | Se for `sa_ttsoft`, trocar a senha derruba a API. Ela precisa de um usuário próprio antes da troca |
| P2 | Quais instalações do **OutfiTT** usam `sa_ttsoft` | Conhecimento do responsável | Pelo que foi declarado, o OutfiTT também instala com `sa_ttsoft` (no banco `ERPTTSoft`). Trocar a senha derruba essas instalações também |
| P3 | Regras de firewall do servidor | Portal → SQL server → Rede | Se houver regra aberta para qualquer IP, a credencial vazada funciona de qualquer lugar. Muda a urgência da troca de senha |
| P4 | Outros lugares com `sa_ttsoft` | Conhecimento do responsável: user secrets do `dotnet ef`, scripts, SSMS, ferramentas | Tudo que usa a senha atual precisa ser atualizado na troca |

---

## 4. Proposta técnica

### 4.1 Usuário contido no banco (não login de servidor)

No Azure SQL, um **usuário contido** é criado dentro do próprio banco, com senha, sem login
no `master`. Ele só consegue abrir o banco onde foi criado, o que garante por construção que
não alcança o OutfiTT. A connection string do WPF já informa o banco, então o assistente de
instalação funciona sem mudança de código (`ERP.WPF/ViewModels/SetupViewModel.cs:101`).

### 4.2 Script proposto (NÃO executado)

A ser executado conectado ao `ERPTTSoftStaging` como administrador, na fase de execução.
A senha é gerada e guardada no gerenciador de senhas; **nunca** escrita aqui, em chat ou em
arquivo do repositório.

```sql
-- Papel com o mínimo que o WPF usa
CREATE ROLE construttor_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::dbo TO construttor_app;

-- Tabelas com tokens de terceiros: o WPF não usa (seção 2)
DENY SELECT, INSERT, UPDATE, DELETE ON OBJECT::dbo.SalesChannels              TO construttor_app;
DENY SELECT, INSERT, UPDATE, DELETE ON OBJECT::dbo.TenantFiscalConfigurations TO construttor_app;

-- Usuário contido do WPF
CREATE USER construttor_wpf WITH PASSWORD = '<senha do gerenciador de senhas>';
ALTER ROLE construttor_app ADD MEMBER construttor_wpf;
```

Notas:
- `GRANT ... ON SCHEMA::dbo` cobre tabelas criadas por migrations futuras, sem manutenção.
- `DENY` prevalece sobre `GRANT`.
- Criar o papel e o usuário é **aditivo**: ninguém passa a usá-los até um PC ser reconfigurado.

### 4.3 Verificação do usuário (somente leitura, logo após criar)

```sql
EXECUTE AS USER = 'construttor_wpf';

SELECT 'Sales INSERT'              AS teste, HAS_PERMS_BY_NAME('dbo.Sales', 'OBJECT', 'INSERT') AS tem -- espera 1
UNION ALL SELECT 'Users SELECT',              HAS_PERMS_BY_NAME('dbo.Users', 'OBJECT', 'SELECT')       -- espera 1
UNION ALL SELECT 'AuditLogs INSERT',          HAS_PERMS_BY_NAME('dbo.AuditLogs', 'OBJECT', 'INSERT')   -- espera 1
UNION ALL SELECT 'SalesChannels SELECT',      HAS_PERMS_BY_NAME('dbo.SalesChannels', 'OBJECT', 'SELECT')              -- espera 0
UNION ALL SELECT 'TenantFiscalConfig SELECT', HAS_PERMS_BY_NAME('dbo.TenantFiscalConfigurations', 'OBJECT', 'SELECT') -- espera 0
UNION ALL SELECT 'CREATE TABLE',              HAS_PERMS_BY_NAME(DB_NAME(), 'DATABASE', 'CREATE TABLE')  -- espera 0
UNION ALL SELECT 'ALTER ANY USER',            HAS_PERMS_BY_NAME(DB_NAME(), 'DATABASE', 'ALTER ANY USER') -- espera 0
UNION ALL SELECT 'CONTROL',                   HAS_PERMS_BY_NAME(DB_NAME(), 'DATABASE', 'CONTROL');      -- espera 0

REVERT;
```

---

## 5. Execução proposta (em etapas, cada uma reversível)

| Etapa | O quê | Reversão | Critério para seguir |
|---|---|---|---|
| E0 | Pré-verificações P1 a P4 | — | Respostas registradas |
| E1 | Executar 4.2 e 4.3 no `ERPTTSoftStaging` | `DROP USER construttor_wpf; DROP ROLE construttor_app;` | 4.3 com todos os valores esperados |
| E2 | **Piloto em 1 PC** fora do horário da loja: copiar `conexao.dat` para uma pasta de backup no próprio PC; renomear o original; abrir o ERP; no assistente, informar servidor, banco, `construttor_wpf` e a senha nova; testar o roteiro da seção 6 | Restaurar o `conexao.dat` copiado (a cifra é presa à máquina, então a cópia só vale no mesmo PC) | Roteiro sem erro de permissão |
| E3 | Observar o PC piloto por 1 dia útil de operação real | Igual à E2 | Nenhum erro "permission was denied" no log do WPF (`Logs/log-*.json`); sessões do PC aparecem com `construttor_wpf` (consulta 7.1) |
| E4 | Repetir E2 nos outros 2 PCs | Igual à E2 | Consulta 7.1 sem nenhuma sessão de PC de loja com `sa_ttsoft` |
| E5 | Resolver P1 e P2: se a API ou o OutfiTT usam `sa_ttsoft`, criar usuários contidos próprios para eles (desenho à parte, mesmo padrão) | Por usuário | Consulta 7.1 mostra só o responsável usando `sa_ttsoft` |
| E6 | **Trocar a senha de `sa_ttsoft`** no portal (SQL server → Repor palavra-passe) e atualizar P4 | Não há volta para a senha antiga (de propósito) | Só depois de E4 e E5 estáveis por pelo menos 1 semana |

A E6 é o único passo sem volta. Por isso fica por último, com todas as dependências resolvidas.

---

## 6. Roteiro de teste no PC piloto (E2)

Pela arquitetura atual, parte das telas usa a API (já com usuário próprio do servidor) e
parte usa o banco direto com o novo usuário. O roteiro cobre as que usam o banco direto:

1. Login de operador e de gerente; troca de senha.
2. Abrir o PDV e buscar produto e cliente; fazer uma venda real normal da loja.
3. Orçamento: criar e converter em venda.
4. Devolução de um item (se houver uma devolução real a fazer).
5. Haver e contas do cliente: consultar.
6. Cadastros: usuários e cargos (consultar).
7. Relatórios: vendas, gerenciador de vendas, extrato financeiro.
8. Auditoria: abrir a tela de logs.
9. Notas fiscais: consultar a lista (sem emitir só para testar).
10. Sincronização: abrir a tela de diagnóstico e confirmar o indicador verde.

Qualquer erro "permission was denied" ou "SELECT/INSERT/UPDATE/DELETE permission" indica
uma tabela que o desenho deixou de fora. Reverter o PC (E2) e registrar a tabela.

---

## 7. Monitoramento (somente leitura)

### 7.1 Quem está conectado com qual usuário

No `ERPTTSoftStaging`, como administrador:

```sql
SELECT login_name, host_name, program_name, COUNT(*) AS sessoes, MAX(login_time) AS ultimo_login
FROM sys.dm_exec_sessions
WHERE is_user_process = 1
GROUP BY login_name, host_name, program_name
ORDER BY login_name, host_name;
```

Esperado ao final: PCs da loja (`DESKTOP-JQB0BIQ`, `SERVIDOR`, `COMPUTADOR03`) com
`construttor_wpf`; API com seu próprio usuário; `sa_ttsoft` só a partir do PC do responsável.

---

## 8. Riscos do próprio plano

| Risco | Mitigação |
|---|---|
| Alguma tela usa uma tabela de forma não prevista (ex.: navegação do EF para `SalesChannels`) | Piloto em 1 PC com reversão imediata; roteiro da seção 6 |
| Esquecer um uso de `sa_ttsoft` e derrubar algo na troca de senha | P1, P2, P4 e consulta 7.1 antes da E6 |
| Operador sem acesso durante a troca | Executar fora do horário; reversão em minutos pelo backup do `conexao.dat` |
| Nova senha vazar do mesmo jeito | Digitar direto no assistente de cada PC; guardar só no gerenciador de senhas |

---

## 9. Decisões pedidas ao responsável

1. Aprovar o usuário contido `construttor_wpf` com leitura/escrita em `dbo` e bloqueio das
   duas tabelas de tokens.
2. Responder P1 a P4.
3. Escolher o PC piloto e a janela (fora do horário da loja).
4. Confirmar que a troca de senha de `sa_ttsoft` (E6) fica para depois da estabilização,
   incluindo API e OutfiTT.

---

## Histórico do documento

| Data | Versão | Alteração |
|---|---|---|
| 2026-10-22 | 1.0 | Desenho inicial para revisão |
| 2026-10-22 | 1.1 | Desenho aprovado pelo responsável; execução aguarda P1 a P4 |
