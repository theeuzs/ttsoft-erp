# Roteiro — Confirmações 2, 4 e 5 da EXC-A0

> Programa de Excelência TTSoft · Apoio à auditoria EXC-A0
> Criado em: 2026-10-22 · Base: commit `ec6d177`
> Relacionados: [PLANO-MESTRE §7](PLANO-MESTRE.md) · [REGISTRO-CENTRAL](REGISTRO-CENTRAL.md)

Tudo aqui é **somente leitura**: nenhum passo altera dados, configuração ou produção.
Não copie senhas, tokens ou connection strings completas para este documento nem para a
conversa. Registre apenas o resultado (sim/não, nomes de papéis, contagens, nomes de banco).

---

## 0. O que já está comprovado (sem acesso ao ambiente)

| Item | Evidência |
|---|---|
| O WPF guarda a connection string em `conexao.dat`, na pasta do executável, cifrada com DPAPI de máquina e entropia fixa | `ERP.WPF/Helpers/SecureConfigService.cs:14-20` |
| A connection string é montada no assistente de instalação com usuário e senha SQL digitados e `TrustServerCertificate=True` | `ERP.WPF/ViewModels/SetupViewModel.cs:100-101` |
| Não existe tabela de lojas: o `TenantId` é derivado do CNPJ (SHA-256, 16 primeiros bytes) | `Scripts/CalcularTenantId.csx`; `ERP.Infrastructure/Services/CadastroService.cs:225-232` |
| TenantId esperado da Vila Verde (CNPJ do próprio script): `b7f9f866-0695-ef92-e546-52c20efe57d6` | Calculado com o algoritmo acima em 2026-10-22; **confirmar no banco** (passo 4.2) |
| O WPF não informa sua versão a nenhum serviço, e a versão do assembly é fixa em 1.1.0 | `ERP.WPF/ERP.WPF.csproj:3-5`; nenhuma telemetria de versão encontrada |
| O WPF grava o nome da máquina em cada registro de auditoria | `ERP.Persistence/Context/AppDbContext.cs:752` |
| O interruptor de recuperação fiscal é a configuração `FiscalRecovery:TenantsHabilitados` (no App Service: `FiscalRecovery__TenantsHabilitados`); ausente ou vazia = nenhum tenant habilitado; qualquer item inválido desabilita todos | `ERP.Application/Fiscal/Recovery/FiscalRecoverySwitch.cs:20-37`; registro em `ERP.Api/Extensions/FiscalRecoveryServiceCollectionExtensions.cs:48-49` |
| A chave não existe em nenhum `appsettings` versionado | Busca em `ERP.Api/appsettings*.json` (2026-10-22) |

## Declarado pelo responsável em 2026-10-22

- A instalação do WPF usa o login SQL `sa_ttsoft` no servidor `erp-ttsoft-server`, o mesmo em todas as lojas.
- O banco do ConstruTTor em uso é `ERPTTSoftStaging`. O banco `ERPTTSoft`, no mesmo servidor, é usado pelo OutfiTT (outro produto).
- A Vila Verde usa 3 PCs com a versão do último artefato WPF publicado pelo GitHub Actions.

---

## Passo 1 — Portal do Azure (mais fácil, sem SQL)

### 1.1 O login do WPF é o administrador do servidor? (confirmação 2)
Portal do Azure → **SQL servers** → `erp-ttsoft-server` → **Visão geral** → campo
**Administrador do servidor** (*Server admin*).

- Se for `sa_ttsoft`: o WPF de cada loja tem controle total sobre **todos** os bancos do
  servidor, inclusive o do OutfiTT. Registrar e pular para o passo 3.
- Se for outro nome: seguir para o passo 2 para medir as permissões.

Anote também, na mesma tela, **Microsoft Entra admin** (se houver) e, em **Rede**, se o
acesso público está aberto e quais regras de firewall existem (só os nomes e faixas, não é segredo).

### 1.2 Interruptor da recuperação fiscal (confirmação 5)
Portal do Azure → **App Services** → `erp-ttsoft-api` → **Configuração** (ou *Environment variables*)
→ **Configurações de aplicativo**. Procure `FiscalRecovery__TenantsHabilitados`.

| Situação | Significado |
|---|---|
| Não existe ou está vazia | Nenhuma loja habilitada: **todas** usam o caminho antigo (EXC-FIS-007 ativo) |
| Lista de GUIDs | Só essas lojas usam a recuperação nova; compare com os TenantIds do passo 4.2 |

Repita em **Slots de implantação** → `staging`, se existir.
Não altere o valor. Não clique em "Salvar" nessa tela.

### 1.3 Qual banco a API usa (confirmação 4)
Na mesma tela, em **Cadeias de conexão**, clique em mostrar **apenas** para ler os campos
`Server` / `Database` / `User Id` de `DefaultConnection`. Não copie a senha.
Registre: nome do banco e se o login é o mesmo do WPF.

### 1.4 Confirmação independente pelo log (confirmação 5)
App Service → **Log stream**, ou Application Insights → **Logs**, logo após um reinício já
ocorrido (não reinicie só para isso). Mensagens emitidas na partida:

- `NfeRecoveryHostedService iniciado (ciclo de ... s, tenants habilitados: N)`
- `Recuperacao fiscal: habilitada para N tenant(s).`
- `Recuperacao fiscal: a configuracao FiscalRecovery:TenantsHabilitados esta INVALIDA` (se houver erro)

Consulta no Application Insights (somente leitura):

```kusto
traces
| where timestamp > ago(30d)
| where message has "tenants habilitados" or message has "Recuperacao fiscal"
| project timestamp, message
| order by timestamp desc
| take 20
```

---

## Passo 2 — Permissões do login (confirmação 2)

Ferramenta: SSMS, Azure Data Studio ou *Query editor* do portal, conectado **com o mesmo
login do WPF**, no banco `ERPTTSoftStaging`. Todas as consultas são `SELECT`.

```sql
-- 2.1 Quem sou eu neste banco
SELECT SUSER_SNAME() AS login_atual,
       USER_NAME()   AS usuario_no_banco,   -- 'dbo' indica administrador do servidor ou dono do banco
       DB_NAME()     AS banco_atual;

-- 2.2 Papéis no banco
SELECT IS_ROLEMEMBER('db_owner')      AS db_owner,
       IS_ROLEMEMBER('db_datareader') AS db_datareader,
       IS_ROLEMEMBER('db_datawriter') AS db_datawriter,
       IS_ROLEMEMBER('db_ddladmin')   AS db_ddladmin;

-- 2.3 Permissões efetivas no banco
SELECT permission_name
FROM fn_my_permissions(NULL, 'DATABASE')
ORDER BY permission_name;

-- 2.4 Existe segurança por linha (RLS) que isole lojas no próprio banco?
SELECT name, is_enabled FROM sys.security_policies;
```

Depois, conectado ao banco **`master`** com o mesmo login:

```sql
-- 2.5 Quais bancos do servidor este login consegue abrir
SELECT name, HAS_DBACCESS(name) AS tem_acesso
FROM sys.databases
ORDER BY name;
```

Como interpretar:

| Resultado | Conclusão |
|---|---|
| 2.1 `usuario_no_banco = dbo` **ou** 2.2 `db_owner = 1` | Controle total do banco: leitura e escrita de todas as lojas, alteração de estrutura |
| `db_datareader = 1` e `db_datawriter = 1`, sem `db_owner` | Leitura e escrita de todas as lojas |
| 2.4 sem linhas | Nenhum isolamento no banco: o isolamento depende só do filtro no WPF |
| 2.5 `tem_acesso = 1` em `ERPTTSoft` | A credencial das lojas do ConstruTTor também abre o banco do OutfiTT |

---

## Passo 3 — Instalações nas lojas (confirmação 4)

Em cada PC da loja (ou por acesso remoto), **sem abrir** o `conexao.dat`:

1. Caminho da pasta do ERP e data de modificação de `ERP.WPF.exe`.
2. Hash do executável (PowerShell, somente leitura):
   `Get-FileHash .\ERP.WPF.exe -Algorithm SHA256`
3. Comparar com o artefato `ERP-WPF-<commit>` baixado do GitHub Actions
   (job *Publish WPF Installer*). Como a versão é sempre 1.1.0, **o hash ou a data são a
   única forma de saber qual build está instalado**.
4. Existência de `conexao.dat` e `config_recibo.json` na pasta (só se existem; não abrir).

Registre por PC: loja, nome da máquina, data do exe, hash (primeiros 12 caracteres basta),
commit correspondente.

---

## Passo 4 — Lojas e atividade no banco (confirmações 4 e 5)

Conectado a `ERPTTSoftStaging`. Somente `SELECT`; as consultas retornam contagens e nomes
de loja, não dados de clientes finais.

```sql
-- 4.1 Tenants existentes (pelos usuários cadastrados)
SELECT TenantId,
       COUNT(*)                                   AS usuarios,
       SUM(CASE WHEN IsActive = 1 THEN 1 ELSE 0 END) AS usuarios_ativos
FROM Users
GROUP BY TenantId
ORDER BY usuarios DESC;

-- 4.2 Nome e CNPJ por tenant (filiais cadastradas)
SELECT TenantId, Name, CNPJ, IsMatriz, IsActive
FROM Branches
ORDER BY TenantId, IsMatriz DESC;

-- 4.3 Atividade de vendas nos últimos 30 dias (CreatedAt em UTC)
SELECT TenantId, COUNT(*) AS vendas_30d, MAX(CreatedAt) AS ultima_venda_utc
FROM Sales
WHERE CreatedAt >= DATEADD(day, -30, SYSUTCDATETIME())
GROUP BY TenantId
ORDER BY vendas_30d DESC;

-- 4.4 Máquinas que gravaram no banco nos últimos 30 dias (Timestamp em horário de Brasília)
SELECT TenantId, MachineName, COUNT(*) AS eventos, MAX([Timestamp]) AS ultimo_evento
FROM AuditLogs
WHERE [Timestamp] >= DATEADD(day, -30, GETDATE())
GROUP BY TenantId, MachineName
ORDER BY TenantId, ultimo_evento DESC;

-- 4.5 Configuração fiscal por tenant (sem os tokens)
SELECT TenantId, Cnpj, UsarAmbienteProducao
FROM TenantFiscalConfigurations;

-- 4.6 Pendências fiscais por tenant e estado (confirmação 5 / EXC-FIS-007)
SELECT TenantId, TipoNota, Estado, CriadaEmProducao,
       COUNT(*)        AS quantidade,
       MIN(DataFalha)  AS falha_mais_antiga,
       MAX(Tentativas) AS max_tentativas
FROM NfePendentes
GROUP BY TenantId, TipoNota, Estado, CriadaEmProducao
ORDER BY TenantId, TipoNota, Estado;
```

Como interpretar:

- 4.1 a 4.3 mostram quantas lojas existem e quais estão ativas. Tenants de teste (por
  exemplo, o do CNPJ `11222333000181`, usado no checklist do Mercado Livre) aparecerão aqui,
  porque o banco de testes e o de produção são o mesmo (EXC-OPS-007).
- 4.4 mostra os PCs que gravam diretamente no banco; deve bater com os 3 PCs da Vila Verde.
  Registros com o nome do servidor do App Service vêm da API.
- 4.6 cruzado com o passo 1.2: NFC-e pendentes em tenant **fora** da lista do interruptor
  seguem pelo caminho antigo e estão expostas ao EXC-FIS-007.

---

## Registro dos resultados

Preencha e devolva; o registro central será atualizado a partir daqui.

| Passo | Resultado | Data | Quem verificou |
|---|---|---|---|
| 1.1 Administrador do servidor | | | |
| 1.2 `FiscalRecovery__TenantsHabilitados` (produção / slot) | | | |
| 1.3 Banco e login da API | | | |
| 1.4 Log da partida | | | |
| 2.1–2.4 Permissões no `ERPTTSoftStaging` | | | |
| 2.5 Bancos acessíveis | | | |
| 3 Builds instaladas (por PC) | | | |
| 4.1–4.5 Lojas e atividade | | | |
| 4.6 Pendências fiscais | | | |
