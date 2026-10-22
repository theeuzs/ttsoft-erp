# Registro Central de Descobertas

> Programa de Excelência TTSoft
> Criado em: 2026-10-22 · Última atualização: 2026-10-22 (v1.1) · Base: commit `ec6d177`
> Classificação conforme [CRITERIOS-DE-EXCELENCIA](CRITERIOS-DE-EXCELENCIA.md) · Plano: [PLANO-MESTRE](PLANO-MESTRE.md) · Origem dos itens: [INVENTARIO-AUDITORIAS](INVENTARIO-AUDITORIAS.md)

---

## Como usar

- Cada descoberta tem um ID `EXC-<ÁREA>-NNN`, nunca reutilizado.
- O **índice** resume; o **detalhe** é a fonte. Ao mudar status ou gravidade, atualize os dois.
- Antes de criar um registro, procure no índice e no INVENTARIO §4 por um item igual ou
  relacionado. Duplicatas são encerradas como `Duplicado de EXC-…`.
- Pendências de relatórios antigos só entram aqui depois de conferidas no código atual.
  As já corrigidas estão no INVENTARIO §4.1 e **não** são registradas.
- Os registros são **diagnóstico, não tarefas**: nenhum foi aprovado para execução.
- `DOC-NN` refere-se aos relatórios listados no INVENTARIO §2.

### Modelo de registro

```
### EXC-<ÁREA>-NNN — <título curto>

| Campo | Valor |
|---|---|
| Área | |
| Natureza | Fato / Hipótese / Lacuna / Recomendação |
| Descrição | |
| Evidência | arquivo:linha, commit, teste (com resultado e data), DOC-NN |
| Gravidade | Crítica / Alta / Média / Baixa / Informativa / A classificar |
| Impacto | o que acontece, com quem, em que condição |
| Confiança | Alta / Média / Baixa |
| Dependências | |
| Responsável | |
| Ação proposta | |
| Status | ver CRITERIOS §6 |
| Critério de validação | |
| Origem | EXC-A#, INV-NNN, DOC-NN |
| Relacionados | |
| Datas | Registrado em / atualizado em |
```

---

## Índice

| ID | Área | Título | Natureza | Gravidade | Confiança | Status | Responsável |
|---|---|---|---|---|---|---|---|
| EXC-SEG-001 | SEG | WPF acessa o banco com uma credencial única, igual em todas as lojas, que lê e grava dados de todos os tenants | Fato | Alta | Alta | Confirmado | A definir |
| EXC-SEG-002 | SEG | Dependência com vulnerabilidade conhecida de gravidade alta (`System.Security.Cryptography.Xml` 8.0.3) | Fato | A classificar | Alta (aviso) | Confirmado | A definir |
| EXC-SEG-003 | SEG | Troca dos segredos removidos do repositório não confirmada | Lacuna | A classificar | Baixa | Lacuna | Matheus |
| EXC-SEG-004 | SEG | Tokens OAuth do Mercado Livre gravados em texto plano | Fato | Média | Alta | Confirmado | A definir |
| EXC-SEG-005 | SEG | JWT sem refresh token | Fato | Baixa | Alta | Confirmado | A definir |
| EXC-SEG-006 | SEG | Senha do login SQL de produção compartilhada fora de um cofre de segredos | Fato | Alta | Alta | Confirmado | Matheus |
| EXC-SEG-007 | SEG | Conexão do WPF ao SQL não valida o certificado do servidor (`TrustServerCertificate=True`) | Fato | Média | Alta | Confirmado | A definir |
| EXC-ENG-001 | ENG | Testes não comprovam comportamento no SQL Server (SQL bruto e `IgnoreQueryFilters`) | Fato | Média | Alta | Confirmado | A definir |
| EXC-ENG-002 | ENG | README desatualizado em relação à arquitetura atual | Fato | Baixa | Alta | Confirmado | A definir |
| EXC-ENG-003 | ENG | 155 avisos de compilação | Fato | Baixa | Alta | Confirmado | A definir |
| EXC-ENG-004 | ENG | `ChangeTracker.Clear()` após todo `SaveChanges` | Fato | Média | Alta | Confirmado | A definir |
| EXC-OPS-001 | OPS | Checklist de deploy e `versao.json` versionados desatualizados | Fato | Média | Alta | Confirmado | A definir |
| EXC-OPS-002 | OPS | Deploy automático da `main` em produção; pipeline de staging nunca dispara | Fato | A classificar | Alta | Confirmado | A definir |
| EXC-OPS-003 | OPS | Política de backup e teste de restauração sem evidência | Lacuna | A classificar | — | Lacuna | Matheus |
| EXC-OPS-004 | OPS | Versão do produto sem fonte única; versões e bancos em campo desconhecidos | Fato + Lacuna | Baixa | Alta | Confirmado | A definir |
| EXC-OPS-005 | OPS | Licenciamento depende de API externa fora deste repositório | Fato + Lacuna | A classificar | Alta | Confirmado | Matheus |
| EXC-OPS-006 | OPS | Rate limit e cache em memória impedem escalar a API para mais de uma instância | Fato | Baixa | Média | Confirmado | A definir |
| EXC-OPS-007 | OPS | Produção usa o banco `ERPTTSoftStaging`, que também recebe testes, num servidor compartilhado com o OutfiTT | Fato | Média | Média | Confirmado | Matheus |
| EXC-FIS-001 | FIS | Estado da recuperação fiscal (interruptor por tenant) em produção desconhecido | Lacuna | A classificar | — | Lacuna | Matheus |
| EXC-FIS-002 | FIS | Termos de uso, privacidade, contrato, DPA e limitação de responsabilidade não localizados | Lacuna | A classificar | — | Lacuna | Matheus |
| EXC-FIS-003 | FIS | Serviços locais de emissão fiscal seguem registrados no WPF junto ao serviço via API | Hipótese | A classificar | Baixa | Hipótese | A definir |
| EXC-FIS-004 | FIS | WPF lê a configuração fiscal (token Focus) de arquivo local; API lê do banco | Fato | Média | Alta | Confirmado | A definir |
| EXC-FIS-005 | FIS | Nenhum tratamento da Reforma Tributária (IBS/CBS) no código | Fato + Lacuna | A classificar | Alta | Confirmado | A definir |
| EXC-FIS-006 | FIS | SPED EFD ICMS/IPI gerado sem Bloco E | Fato | Baixa | Alta | Confirmado | A definir |
| EXC-FIS-007 | FIS | Caminho antigo de contingência reenvia NFC-e com `DataEmissao` congelada | Fato | A classificar | Média | Confirmado | A definir |
| EXC-PRD-001 | PRD | Roadmap exibido no Portal não corresponde ao produto atual | Fato | Baixa | Alta | Confirmado | A definir |
| EXC-DAD-001 | DAD | Parte dos relatórios anteriores não foi localizada | Lacuna | Baixa | Alta | Lacuna | Matheus |
| EXC-DAD-002 | DAD | Sem concorrência otimista; créditos de Haver por leitura-e-gravação | Fato + Hipótese | Média | Média | Confirmado | A definir |
| EXC-ADM-001 | ADM | Custo de operação por cliente desconhecido | Lacuna | A classificar | — | Lacuna | Matheus |
| EXC-VEN-001 | VEN | Preços e planos sem validação com clientes | Lacuna | A classificar | — | Lacuna | Matheus |

**Contagem:** 30 registros — 0 Crítica, 2 Alta, 8 Média, 8 Baixa, 12 A classificar.
Nenhum registro é Crítico até agora; EXC-SEG-001 pode subir com o resultado do
[roteiro](ROTEIRO-CONFIRMACOES-A0.md), passo 1.1 e 2.5 (acesso a outros bancos do servidor).

---

## Detalhe — Segurança (SEG)

### EXC-SEG-001 — WPF acessa o banco com uma credencial única, que lê e grava dados de todos os tenants

| Campo | Valor |
|---|---|
| Área | SEG (também ENG, DAD) |
| Natureza | Fato |
| Descrição | O WPF guarda a string de conexão em `conexao.dat`, ao lado do executável, cifrada com DPAPI de máquina e entropia fixa no binário; quem usa a máquina consegue decifrá-la. Segundo o responsável (2026-10-22), toda instalação usa o **mesmo login SQL** (`sa_ttsoft`) no banco `ERPTTSoftStaging`, que contém todos os tenants. Não há segurança por linha no banco: nenhuma política nem uso de `SESSION_CONTEXT` no código, que é o que permitiria ao banco distinguir lojas. Como o WPF lê e grava os próprios dados com esse login, o login necessariamente lê e grava os dados de **todas** as lojas. O isolamento existe só no filtro executado no cliente. Ainda passam pelo banco direto: Auth, Usuários, Funções, Devolução, Orçamento, Haver, Motor Financeiro, SPED, Dashboard, emissão/cancelamento/contingência de NF-e e outros. |
| Evidência | `ERP.WPF/Helpers/SecureConfigService.cs:14-20,29,40`; `ERP.WPF/ViewModels/SetupViewModel.cs:100-101`; `ERP.WPF/App.xaml.cs:132`, `:298-303`, `:361-502`; busca por `SECURITY POLICY`/`SESSION_CONTEXT` sem resultados (2026-10-22); declaração do responsável (2026-10-22); DOC-31, DOC-32, DOC-33, DOC-34. |
| Gravidade | **Alta.** Credencial com acesso a todas as lojas distribuída em máquinas de loja e extraível por quem tem acesso a elas. Não é Crítica hoje porque não há indício de exploração e a operação real é de uma loja. **Sobe para Crítica** se o roteiro mostrar que `sa_ttsoft` é administrador do servidor ou abre outros bancos (o do OutfiTT está no mesmo servidor), ou quando houver mais lojas em produção. |
| Impacto | Quem obtiver a credencial a partir de um PC de loja (malware, funcionário, técnico) lê e altera dados de todas as lojas, inclusive os tokens de EXC-SEG-004, e possivelmente de outros bancos do servidor. |
| Confiança | Alta (acesso a todos os tenants); Baixa (privilégios além do banco, até o passo 1.1/2.5) |
| Dependências | [ROTEIRO-CONFIRMACOES-A0](ROTEIRO-CONFIRMACOES-A0.md), passos 1.1 e 2 |
| Responsável | A definir |
| Ação proposta | Concluir o roteiro (passos 1.1 e 2). Na EXC-A1: definir a mitigação de curto prazo (login próprio do ConstruTTor com o mínimo de permissões, sem acesso a outros bancos) e a de longo prazo (continuar a migração WPF→API até o WPF não precisar de credencial do banco). Não implementar antes da conclusão da EXC-A1. |
| Status | Confirmado |
| Critério de validação | Nenhuma máquina de loja com credencial capaz de ler dados de outra loja; ou risco aceito formalmente com prazo. |
| Origem | EXC-F0; INV-020; DOC-31, DOC-33; confirmação 2 |
| Relacionados | EXC-ENG-001, EXC-FIS-003, EXC-SEG-004, EXC-SEG-006, EXC-SEG-007, EXC-OPS-007 |
| Datas | Registrado em 2026-10-22; atualizado em 2026-10-22 (relatórios anteriores; declaração do login e do banco) |

### EXC-SEG-002 — Dependência com vulnerabilidade conhecida de gravidade alta

| Campo | Valor |
|---|---|
| Área | SEG |
| Natureza | Fato (o aviso existe); impacto real ainda não avaliado |
| Descrição | O restore reporta `NU1903` para `System.Security.Cryptography.Xml` 8.0.3 no projeto `ERP.Application`. O pacote é provavelmente usado na assinatura/validação de XML fiscal. Nenhum relatório anterior registra este aviso. |
| Evidência | Build de 2026-10-22, `ec6d177`: `ERP.Application.csproj : warning NU1903: Package 'System.Security.Cryptography.Xml' 8.0.3 has a known high severity vulnerability`. |
| Gravidade | **A classificar** — depende do advisory e do uso da funcionalidade afetada. |
| Impacto | A determinar na EXC-A2. |
| Confiança | Alta para o aviso; Baixa para o impacto |
| Dependências | — |
| Responsável | A definir |
| Ação proposta | Na EXC-A2: identificar o advisory, onde o pacote é usado (direto ou transitivo) e a versão corrigida; rodar `dotnet list package --vulnerable --include-transitive` em todos os projetos. |
| Status | Confirmado |
| Critério de validação | Nenhuma vulnerabilidade alta/crítica, ou risco aceito com justificativa. |
| Origem | EXC-F0 |
| Relacionados | INV-007 |
| Datas | Registrado em 2026-10-22 |

### EXC-SEG-003 — Troca dos segredos removidos do repositório não confirmada

| Campo | Valor |
|---|---|
| Área | SEG |
| Natureza | Lacuna |
| Descrição | Commits de junho/2026 removeram do versionamento segredos (senha do banco, chave JWT, credenciais GCP, `config_recibo`, credenciais SEFAZ). O histórico do Git mantém o conteúdo anterior, salvo reescrita (sem indício de reescrita). Não se sabe se os segredos foram trocados. Os valores antigos **não** foram abertos. DOC-30 e DOC-31 confirmam que o código não tem segredos, mas não tratam da troca. |
| Evidência | Commits `587a0ba`, `ee29179`, `48d7748`, `6b8ac31`; `appsettings` atuais só com placeholders (2026-10-22). |
| Gravidade | **A classificar.** Alta se algum segredo antigo continuar válido; Informativa se todos foram trocados. |
| Impacto | Acesso indevido a banco, emissão de tokens ou serviços externos com credencial antiga. |
| Confiança | Baixa |
| Dependências | PLANO-MESTRE §7, confirmação 3 |
| Responsável | Matheus |
| Ação proposta | Listar os segredos que já estiveram no histórico e confirmar a troca de cada um. |
| Status | Lacuna |
| Critério de validação | Lista de segredos com data de troca, ou confirmação de que nunca foram válidos. |
| Origem | EXC-F0; INV-004 |
| Relacionados | EXC-SEG-001 |
| Datas | Registrado em 2026-10-22 |

### EXC-SEG-004 — Tokens OAuth do Mercado Livre gravados em texto plano

| Campo | Valor |
|---|---|
| Área | SEG |
| Natureza | Fato |
| Descrição | `SalesChannel.AccessToken` e `RefreshToken` são gravados sem proteção. O projeto já tem `TokenProtector`, usado apenas para o token da Focus (`DatabaseFiscalConfigurationProvider`). |
| Evidência | `ERP.Domain/Entities/SalesChannel.cs:24-25`; gravação em `ERP.Infrastructure/Services/MercadoLivreAuthService.cs:117-118` e `ERP.Infrastructure/Repositories/OrderSyncRepository.cs:45-46`; `TokenProtector` só referenciado por `DatabaseFiscalConfigurationProvider.cs` (2026-10-22). Apontado em DOC-31 e DOC-33 (P1). |
| Gravidade | Média. Sobe se EXC-SEG-001 for confirmado (qualquer máquina de loja leria os tokens de todos os tenants). |
| Impacto | Quem obtém leitura do banco (dump, backup, credencial do WPF) pode operar a conta do Mercado Livre de qualquer loja conectada. |
| Confiança | Alta |
| Dependências | — |
| Responsável | A definir |
| Ação proposta | Na EXC-A2: avaliar a proteção dos tokens com o mecanismo existente e a migração dos registros atuais. |
| Status | Confirmado |
| Critério de validação | Nenhum token OAuth legível em texto no banco; teste que garanta a proteção na gravação. |
| Origem | INV-011; DOC-31, DOC-33 |
| Relacionados | EXC-SEG-001 |
| Datas | Registrado em 2026-10-22 |

### EXC-SEG-005 — JWT sem refresh token

| Campo | Valor |
|---|---|
| Área | SEG (também PRD) |
| Natureza | Fato |
| Descrição | O JWT da API expira em 12 h (`Jwt:ExpirationHours`) e não há fluxo de renovação. A revogação, apontada como ausente em DOC-31/DOC-33, já existe (`TokenVersion`). O commit `b0dd317` cita 14 h; o `appsettings.json` atual diz 12. |
| Evidência | `ERP.Api/appsettings.json:10`; `ERP.Api/Program.cs:387-390`; nenhum endpoint de refresh em `AuthController` (2026-10-22). |
| Gravidade | Baixa |
| Impacto | Sessão expira durante o expediente longo; tokens longos aumentam a janela de uso de um token vazado (mitigado pela revogação). |
| Confiança | Alta |
| Dependências | — |
| Responsável | A definir |
| Ação proposta | Na EXC-A2: confirmar o valor efetivo em produção e decidir entre manter (risco aceito) ou criar renovação. |
| Status | Confirmado |
| Critério de validação | Decisão registrada; se houver renovação, testes de expiração e renovação. |
| Origem | DOC-31, DOC-33 |
| Relacionados | — |
| Datas | Registrado em 2026-10-22 |

### EXC-SEG-006 — Senha do login SQL de produção compartilhada fora de um cofre de segredos

| Campo | Valor |
|---|---|
| Área | SEG |
| Natureza | Fato |
| Descrição | Em 2026-10-22 a senha do login usado pelo WPF foi enviada a esta conversa, numa captura de tela de uma anotação de instalação. A mesma senha está em todas as máquinas de loja (EXC-SEG-001). O valor **não** foi reproduzido em nenhum documento. |
| Evidência | Conversa de 2026-10-22; cópia da imagem na pasta temporária da sessão do Claude Code no PC do responsável. |
| Gravidade | Alta (credencial ativa de produção exposta fora de um cofre; mesma credencial em todas as lojas) |
| Impacto | Quem tiver acesso à anotação, à imagem ou ao histórico da conversa pode acessar o banco de produção com os privilégios de EXC-SEG-001. |
| Confiança | Alta |
| Dependências | Trocar a senha exige reconfigurar o `conexao.dat` dos 3 PCs da Vila Verde (e a API, se usar o mesmo login — roteiro 1.3). |
| Responsável | Matheus |
| Ação proposta | Planejar a troca da senha junto com a mitigação de EXC-SEG-001 (criar o login restrito já com senha nova evita trocar duas vezes); apagar a imagem temporária; guardar a credencial num cofre (gerenciador de senhas ou Key Vault), não em anotação. |
| Status | Confirmado |
| Critério de validação | Senha antiga invalidada; nova senha guardada só em cofre; nenhuma cópia em anotação ou imagem. |
| Origem | Confirmação 2 (2026-10-22) |
| Relacionados | EXC-SEG-001, EXC-SEG-003 |
| Datas | Registrado em 2026-10-22 |

### EXC-SEG-007 — Conexão do WPF ao SQL não valida o certificado do servidor

| Campo | Valor |
|---|---|
| Área | SEG |
| Natureza | Fato |
| Descrição | O assistente de instalação monta a connection string com `TrustServerCertificate=True`. A conexão é cifrada, mas o certificado do servidor não é verificado: um intermediário na rede da loja poderia se passar pelo servidor e capturar a credencial. O Azure SQL apresenta certificado válido, então a opção não é necessária para o servidor atual. |
| Evidência | `ERP.WPF/ViewModels/SetupViewModel.cs:101` (2026-10-22) |
| Gravidade | Média (exige posição na rede da loja; o ganho do atacante é a credencial de EXC-SEG-001) |
| Impacto | Captura da credencial do banco em rede comprometida (por exemplo, Wi-Fi da loja). |
| Confiança | Alta (código); Média (exploração, depende da rede) |
| Dependências | — |
| Responsável | A definir |
| Ação proposta | Na EXC-A2: avaliar remover a opção para conexões ao Azure SQL; o efeito só chega às lojas quando o `conexao.dat` for recriado. |
| Status | Confirmado |
| Critério de validação | Conexões do WPF validam o certificado do servidor. |
| Origem | Confirmação 2 (2026-10-22) |
| Relacionados | EXC-SEG-001 |
| Datas | Registrado em 2026-10-22 |

---

## Detalhe — Engenharia (ENG)

### EXC-ENG-001 — Testes não comprovam comportamento no SQL Server

| Campo | Valor |
|---|---|
| Área | ENG (também SEG) |
| Natureza | Fato (limite da cobertura) |
| Descrição | A suíte (1322 testes aprovados) usa EF Core InMemory e SQLite. Há 40 chamadas de SQL bruto e 51 usos de `IgnoreQueryFilters` fora dos testes. O comportamento desses pontos no SQL Server, inclusive o isolamento por tenant, não é coberto. Não indica defeito; indica ausência de prova. O próprio defeito do `HasQueryFilter` (DOC-34) só apareceu ao trocar InMemory por SQLite. |
| Evidência | `ERP.Tests.csproj` (InMemory e Sqlite); contagem por busca e execução da suíte em 2026-10-22. DOC-30 propõe um teste genérico contra registros com `TenantId` vazio. |
| Gravidade | Média |
| Impacto | Regressões em SQL bruto ou filtros podem passar no CI e só aparecer em produção. |
| Confiança | Alta |
| Dependências | — |
| Responsável | A definir |
| Ação proposta | Na EXC-A1: revisar cada uso de `IgnoreQueryFilters` e SQL bruto; avaliar testes contra SQL Server para os pontos críticos. |
| Status | Confirmado |
| Critério de validação | Usos revisados e classificados; pontos críticos cobertos por teste contra SQL Server ou risco aceito. |
| Origem | EXC-F0; INV-001, INV-015; DOC-30, DOC-34 |
| Relacionados | EXC-SEG-001 |
| Datas | Registrado em 2026-10-22 |

### EXC-ENG-002 — README desatualizado

| Campo | Valor |
|---|---|
| Área | ENG |
| Natureza | Fato |
| Descrição | O `README.md` descreve 5 projetos (existem 10), instrui editar a conexão em `App.xaml.cs` (hoje vem de `SecureConfigService`) e lista como "Próximos passos" funcionalidades já entregues. DOC-31 (E) recomendava um `ARCHITECTURE.md` de uma página para reduzir o bus factor. |
| Evidência | `README.md` (2026-04-10); `ERP.sln`; `ERP.WPF/App.xaml.cs:132`; DOC-31 |
| Gravidade | Baixa |
| Impacto | Dificulta a entrada de novas pessoas e induz configuração errada. |
| Confiança | Alta |
| Dependências | EXC-A6 |
| Responsável | A definir |
| Ação proposta | Reescrever o README e criar a descrição de arquitetura após a EXC-A6. |
| Status | Confirmado |
| Critério de validação | README descreve os 10 projetos, a configuração atual e o processo de build/teste. |
| Origem | EXC-F0; DOC-31 |
| Relacionados | — |
| Datas | Registrado em 2026-10-22 |

### EXC-ENG-003 — 155 avisos de compilação

| Campo | Valor |
|---|---|
| Área | ENG |
| Natureza | Fato |
| Descrição | O build gera 155 avisos, majoritariamente de anulabilidade (CS8618, CS8604, CS8601, CS8603, CS8602), além de `NU1701`, `CS0618` e `NU1903` (EXC-SEG-002). DOC-34 citava 16 avisos em 13/08, possivelmente com outro escopo de build. |
| Evidência | Log do build de 2026-10-22, `ec6d177` |
| Gravidade | Baixa |
| Impacto | Avisos relevantes ficam escondidos no volume. |
| Confiança | Alta |
| Dependências | — |
| Responsável | A definir |
| Ação proposta | Na EXC-A6: separar avisos com risco real dos cosméticos. |
| Status | Confirmado |
| Critério de validação | Avisos restantes conhecidos e justificados (CRITERIOS ENG-2). |
| Origem | EXC-F0 |
| Relacionados | EXC-SEG-002 |
| Datas | Registrado em 2026-10-22 |

### EXC-ENG-004 — `ChangeTracker.Clear()` após todo `SaveChanges`

| Campo | Valor |
|---|---|
| Área | ENG (também DAD) |
| Natureza | Fato |
| Descrição | O `AppDbContext` limpa o rastreamento de entidades depois de todo `SaveChanges`. Código que salva e continua alterando a mesma entidade perde as alterações seguintes sem erro. Combinado com `NoTracking` global, já causou defeitos reais na integração com o Mercado Livre. |
| Evidência | `ERP.Persistence/Context/AppDbContext.cs:806` e `:816` (2026-10-22); commits `2d62508`, `4a9642d`, `3baceab`; DOC-31 (A), DOC-33 (§13, §18) |
| Gravidade | Média |
| Impacto | Gravações perdidas silenciosamente em código novo ou alterado. |
| Confiança | Alta |
| Dependências | EXC-A6 |
| Responsável | A definir |
| Ação proposta | Na EXC-A6: mapear métodos que salvam e continuam mutando; decidir entre manter o padrão com salvaguardas (teste, análise estática, documentação) ou mudá-lo. Não mudar sem esse mapa. |
| Status | Confirmado |
| Critério de validação | Decisão registrada com mapa de impacto; salvaguarda implementada se o padrão for mantido. |
| Origem | DOC-31, DOC-33 |
| Relacionados | EXC-DAD-002 |
| Datas | Registrado em 2026-10-22 |

---

## Detalhe — Infraestrutura e operações (OPS)

### EXC-OPS-001 — Checklist de deploy e `versao.json` versionados desatualizados

| Campo | Valor |
|---|---|
| Área | OPS |
| Natureza | Fato |
| Descrição | `Scripts/CHECKLIST_DEPLOY.md` descreve a v1.1.0 (março/2026), com publicação em arquivo único e Blob anônimo. O `Scripts/versao.json` versionado não tem `Sha256`, exigido pelo atualizador desde `2448e7c`. **Inferência:** seguir o checklist e publicar o `versao.json` versionado resultaria em atualização recusada. DOC-31 (E) já apontava que a distribuição do WPF é "conhecimento tribal". |
| Evidência | `Scripts/CHECKLIST_DEPLOY.md`; `Scripts/versao.json`; `ERP.WPF/Services/UpdateService.cs:88`; DOC-31 |
| Gravidade | Média |
| Impacto | Publicação de versão com procedimento errado; lojas sem atualização. |
| Confiança | Alta (documentos e código); Média (efeito, inferido) |
| Dependências | EXC-A5 |
| Responsável | A definir |
| Ação proposta | Documentar o procedimento real de publicação na EXC-A5 e substituir o checklist. |
| Status | Confirmado |
| Critério de validação | Checklist reproduz uma publicação real com sucesso (CRITERIOS OPS-4). |
| Origem | EXC-F0; INV-018 |
| Relacionados | EXC-OPS-004 |
| Datas | Registrado em 2026-10-22 |

### EXC-OPS-002 — Deploy automático da `main` em produção; pipeline de staging nunca dispara

| Campo | Valor |
|---|---|
| Área | OPS |
| Natureza | Fato |
| Descrição | `ci-cd.yml` publica API e Portal em todo push na `main`, após os testes, sem aprovação. `staging.yml` dispara apenas em `develop`, branch inexistente. Não há passagem automática por staging. DOC-31 (E) cita também um "duplo caminho de deploy" (pipeline com publish profile e Deployment Center do Azure) e DOC-34 um App Service que às vezes mantém código antigo até reinício manual. |
| Evidência | `.github/workflows/ci-cd.yml`; `.github/workflows/staging.yml`; `git branch -a` (2026-10-22); DOC-31, DOC-34 |
| Gravidade | **A classificar** — depende de proteções de branch e ambientes no GitHub (não visíveis no repositório). |
| Impacto | Mudanças, inclusive fiscais, chegam à produção no mesmo push, sem validação em staging; possível divergência entre o código publicado e o que roda. |
| Confiança | Alta para a configuração do repositório; Baixa para o duplo caminho (relato) |
| Dependências | Acesso às configurações do GitHub e do Azure |
| Responsável | A definir |
| Ação proposta | Na EXC-A5: confirmar proteções e caminhos de deploy existentes e definir o fluxo de promoção. |
| Status | Confirmado |
| Critério de validação | CRITERIOS OPS-1 atendido ou risco aceito. |
| Origem | EXC-F0; DOC-31, DOC-34 |
| Relacionados | PLANO-MESTRE §6.3 |
| Datas | Registrado em 2026-10-22 |

### EXC-OPS-003 — Política de backup e teste de restauração sem evidência

| Campo | Valor |
|---|---|
| Área | OPS (também DAD) |
| Natureza | Lacuna |
| Descrição | Não há documentação nem evidência acessível sobre retenção de backup do Azure SQL ou teste de restauração. O WPF tem um `BackupService` ligado ao fechamento, logout e comando manual (DOC-31); o conteúdo e a utilidade desse backup não foram auditados. |
| Evidência | Ausência no repositório; Azure não acessível; DOC-31 (B.1) |
| Gravidade | **A classificar** — Alta se não houver restauração testada com dados de clientes em produção. |
| Impacto | Perda de dados sem recuperação. |
| Confiança | — |
| Dependências | PLANO-MESTRE §7, confirmação 6 |
| Responsável | Matheus |
| Ação proposta | Na EXC-A5: levantar a configuração e executar um teste de restauração documentado. |
| Status | Lacuna |
| Critério de validação | CRITERIOS OPS-2. |
| Origem | EXC-F0; DOC-31 |
| Relacionados | — |
| Datas | Registrado em 2026-10-22 |

### EXC-OPS-004 — Versão do produto sem fonte única; versões e bancos em campo desconhecidos

| Campo | Valor |
|---|---|
| Área | OPS (também DAD) |
| Natureza | Fato (divergência) + Lacuna (situação em campo) |
| Descrição | `ERP.WPF.csproj` e `Scripts/versao.json` indicam 1.1.0; o Portal exibe "2.4.1" fixo no código. Em 13/08 a Vila Verde ainda rodava a versão antiga com banco local (DOC-34). Segundo o responsável (2026-10-22), hoje ela usa 3 PCs com o último artefato WPF publicado pelo GitHub Actions, ligados ao `ERPTTSoftStaging`. Como a versão do assembly é sempre 1.1.0 e o WPF não reporta versão a nenhum serviço, **não há como saber pelo sistema qual build está em cada PC**: só pela data ou hash do executável comparados com o artefato. A distribuição pelo artefato do GitHub também indica que o atualizador automático (Blob + `versao.json`) pode não estar em uso. |
| Evidência | `ERP.WPF/ERP.WPF.csproj:3-5`; `Scripts/versao.json`; `ERP.Portal/Pages/Roadmap.razor:17`; nenhuma telemetria de versão no WPF; DOC-33, DOC-34; declaração do responsável (2026-10-22) |
| Gravidade | Baixa (a divergência de versão) |
| Impacto | Sem saber a build instalada, não dá para dizer se uma correção já chegou à loja. |
| Confiança | Alta (versões no código); Média (situação em campo, declarada e não verificada) |
| Dependências | PLANO-MESTRE §7, confirmação 4 |
| Responsável | A definir |
| Ação proposta | Roteiro, passos 3 e 4 (build por PC e lojas ativas no banco); definir a fonte de versão e o canal oficial de distribuição na EXC-A8. |
| Status | Confirmado |
| Critério de validação | CRITERIOS OPS-5 e DAD-4; inventário de instalações registrado. |
| Origem | EXC-F0; DOC-33, DOC-34 |
| Relacionados | EXC-OPS-001, EXC-PRD-001 |
| Datas | Registrado em 2026-10-22; atualizado em 2026-10-22 (relatórios anteriores) |

### EXC-OPS-005 — Licenciamento depende de API externa fora deste repositório

| Campo | Valor |
|---|---|
| Área | OPS (também ADM) |
| Natureza | Fato (dependência) + Lacuna (código e operação desconhecidos) |
| Descrição | O `LicenseManager` do WPF consulta um App Service diferente da API do ERP. O código dessa API não está neste repositório nem documentado aqui. |
| Evidência | `ERP.WPF/Security/LicenseManager.cs:13` (2026-10-22); DOC-31 (C, E) |
| Gravidade | **A classificar** — depende do comportamento do WPF se a API de licenças estiver fora do ar e de onde está o código. |
| Impacto | Possível bloqueio das lojas por falha de um serviço não versionado aqui; bus factor. |
| Confiança | Alta (dependência); — (comportamento) |
| Dependências | Acesso ao código da API de licenças |
| Responsável | Matheus |
| Ação proposta | Na EXC-A5: localizar o código, documentar o serviço e testar o comportamento do WPF com a API indisponível. |
| Status | Confirmado |
| Critério de validação | Serviço documentado e comportamento em falha conhecido e aceito. |
| Origem | DOC-31 |
| Relacionados | — |
| Datas | Registrado em 2026-10-22 |

### EXC-OPS-006 — Rate limit e cache em memória impedem escalar para mais de uma instância

| Campo | Valor |
|---|---|
| Área | OPS (também ENG) |
| Natureza | Fato |
| Descrição | A API usa `AddMemoryCache` e o limitador de requisições do ASP.NET Core, ambos por processo. Com duas ou mais instâncias, os limites e o cache (inclusive ETag) deixam de ser consistentes. |
| Evidência | `ERP.Api/Program.cs:181` e `:513` (2026-10-22); DOC-31 (C) |
| Gravidade | Baixa (hoje, presumindo uma instância — a confirmar) |
| Impacto | Limites de login e cadastro contornáveis em escala horizontal. |
| Confiança | Média (número de instâncias não verificado) |
| Dependências | EXC-A5 |
| Responsável | A definir |
| Ação proposta | Registrar como restrição de escala na EXC-A5; tratar quando houver plano de mais de uma instância. |
| Status | Confirmado |
| Critério de validação | Restrição documentada; solução antes de habilitar escala horizontal. |
| Origem | DOC-31 |
| Relacionados | — |
| Datas | Registrado em 2026-10-22 |

### EXC-OPS-007 — Produção usa o banco `ERPTTSoftStaging`, que também recebe testes, num servidor compartilhado com o OutfiTT

| Campo | Valor |
|---|---|
| Área | OPS (também DAD, SEG) |
| Natureza | Fato |
| Descrição | O banco do ConstruTTor em produção se chama `ERPTTSoftStaging` (declaração do responsável e comentário em `AppDbContextFactory`). O checklist do Mercado Livre usa um tenant de teste nesse mesmo banco, então dados de teste e de clientes convivem. O servidor `erp-ttsoft-server` também hospeda o `ERPTTSoft`, usado pelo OutfiTT. Não existe banco de staging separado. |
| Evidência | Declaração do responsável (2026-10-22); `ERP.Persistence/Context/AppDbContextFactory.cs:11-14`; `CHECKLIST_REGRESSAO_MERCADO_LIVRE.md` (seção 0) |
| Gravidade | Média |
| Impacto | Testes podem alterar ou poluir dados de produção; o nome induz a operações erradas (achar que é ambiente de teste); problemas de capacidade ou de acesso em um produto afetam o outro. |
| Confiança | Média (banco declarado; uso de testes documentado no checklist) |
| Dependências | Roteiro, passos 1.3 e 4.1 |
| Responsável | Matheus |
| Ação proposta | Na EXC-A5: inventariar tenants de teste no banco de produção e definir ambientes separados (nomes, bancos e logins por produto e por ambiente). |
| Status | Confirmado |
| Critério de validação | Produção e testes em bancos distintos, com nomes que não induzam a erro; cada produto com login próprio. |
| Origem | Confirmação 4 (2026-10-22) |
| Relacionados | EXC-SEG-001, EXC-OPS-002 |
| Datas | Registrado em 2026-10-22 |

---

## Detalhe — Fiscal, jurídico e conformidade (FIS)

### EXC-FIS-001 — Estado da recuperação fiscal em produção desconhecido

| Campo | Valor |
|---|---|
| Área | FIS |
| Natureza | Lacuna |
| Descrição | A Etapa 4 introduziu a recuperação de NFC-e pendentes e um interruptor por tenant. O interruptor é a configuração `FiscalRecovery:TenantsHabilitados` (App Setting `FiscalRecovery__TenantsHabilitados` no App Service), uma lista de TenantIds separados por vírgula. Ausente ou vazia, nenhum tenant é habilitado; qualquer item inválido desabilita todos. A chave não está em nenhum `appsettings` versionado, então **o padrão é o caminho antigo para todas as lojas**, a menos que tenha sido definida no App Service. DOC-35 lista as etapas 5 (concorrência multi-instância) e 6 (contingência de devoluções) como pendentes naquela data. |
| Evidência | `ERP.Application/Fiscal/Recovery/FiscalRecoverySwitch.cs`; `ERP.Api/Extensions/FiscalRecoveryServiceCollectionExtensions.cs:48-49`; `ERP.Api/BackgroundServices/NfeContingencyHostedService.cs:141-146`; `ERP.Api/BackgroundServices/NfeRecoveryHostedService.cs:56-58` (log da partida); busca em `ERP.Api/appsettings*.json`; commits `3a01d52`, `c387dab`, `f33a098`; DOC-35 |
| Gravidade | **A classificar** |
| Impacto | Define se EXC-FIS-007 está ativo em produção. |
| Confiança | — |
| Dependências | [ROTEIRO-CONFIRMACOES-A0](ROTEIRO-CONFIRMACOES-A0.md), passos 1.2, 1.4 e 4.6 |
| Responsável | Matheus |
| Ação proposta | Executar os passos do roteiro; na EXC-A4, levantar a situação das etapas 5 e 6. |
| Status | Lacuna |
| Critério de validação | Estado de cada tenant documentado e coerente com o plano da Etapa 4. |
| Origem | EXC-F0; INV-022; DOC-35 |
| Relacionados | EXC-FIS-003, EXC-FIS-007 |
| Datas | Registrado em 2026-10-22; atualizado em 2026-10-22 (relatórios anteriores) |

### EXC-FIS-002 — Termos de uso, privacidade, contrato, DPA e limitação de responsabilidade não localizados

| Campo | Valor |
|---|---|
| Área | FIS (jurídico/LGPD) |
| Natureza | Lacuna — **não** significa que não existam; podem estar fora do repositório. |
| Descrição | Não foram encontrados no repositório termos de uso, política de privacidade ou contrato de licença. O Portal tem cadastro público self-service. DOC-25 recomenda contrato de tratamento de dados (DPA) com cada cliente, cláusula de limitação de responsabilidade fiscal revisada por advogado e avaliação da estrutura jurídica da operação. |
| Evidência | Busca em 2026-10-22 em `.razor`, `.xaml`, `.cs`, `.html`; DOC-25 (F, "Risco jurídico") |
| Gravidade | **A classificar** |
| Impacto | Possível descumprimento da LGPD (dados de crédito de pessoas físicas de vários lojistas no mesmo sistema) e ausência de base contratual. |
| Confiança | — |
| Dependências | PLANO-MESTRE §7, confirmação 7 |
| Responsável | Matheus |
| Ação proposta | Localizar os documentos; na EXC-A9, avaliar com assessoria jurídica. |
| Status | Lacuna |
| Critério de validação | CRITERIOS FIS-4 e FIS-5. |
| Origem | EXC-F0; INV-008; DOC-25 |
| Relacionados | EXC-SEG-001 |
| Datas | Registrado em 2026-10-22; atualizado em 2026-10-22 (relatórios anteriores) |

### EXC-FIS-003 — Serviços locais de emissão fiscal seguem registrados no WPF

| Campo | Valor |
|---|---|
| Área | FIS (também ENG) |
| Natureza | Hipótese |
| Descrição | O WPF registra `HttpFiscalService` (emissão via API) e, ao mesmo tempo, `NfceEmissionService`, `NfeEmissionService`, `NfeCancellationService`, `NfeStatusService` e `NfeContingencyService` locais. O comentário em `App.xaml.cs:400-404` diz que o `FiscalService` local fica "órfão, até a Etapa 1C confirmar que nada mais precisa dele". Registrado não significa usado. |
| Evidência | `ERP.WPF/App.xaml.cs:400-406` e `:441-446` |
| Gravidade | **A classificar** |
| Impacto | Se os dois caminhos forem usados, o comportamento fiscal pode variar conforme a tela. |
| Confiança | Baixa |
| Dependências | EXC-A1 (mapa dos serviços locais) |
| Responsável | A definir |
| Ação proposta | Na EXC-A4: rastrear os consumidores de cada serviço e confirmar ou descartar. |
| Status | Hipótese |
| Critério de validação | Mapa de uso documentado; hipótese confirmada ou descartada. |
| Origem | EXC-F0; INV-021 |
| Relacionados | EXC-SEG-001, EXC-FIS-004 |
| Datas | Registrado em 2026-10-22 |

### EXC-FIS-004 — WPF lê a configuração fiscal de arquivo local; API lê do banco

| Campo | Valor |
|---|---|
| Área | FIS (também OPS) |
| Natureza | Fato |
| Descrição | A API usa `DatabaseFiscalConfigurationProvider` (configuração por tenant, token protegido). O WPF continua com `JsonFiscalConfigurationProvider`, que lê o token da Focus de arquivo local. DOC-33 e DOC-34 registravam a migração do token do piloto para o banco como pendente. |
| Evidência | `ERP.WPF/App.xaml.cs:390-394` (2026-10-22); DOC-33 §9, DOC-34 |
| Gravidade | Média |
| Impacto | Configuração fiscal em dois lugares por cliente; tokens podem divergir entre caminhos; token em arquivo na máquina da loja. |
| Confiança | Alta |
| Dependências | EXC-FIS-003 (se o caminho local deixar de ser usado, o arquivo pode deixar de ser necessário) |
| Responsável | A definir |
| Ação proposta | Na EXC-A4: verificar quais caminhos ainda leem o arquivo e o estado da migração dos dados. |
| Status | Confirmado |
| Critério de validação | Uma única fonte de configuração fiscal por tenant, ou divergência justificada. |
| Origem | INV-013; DOC-33, DOC-34 |
| Relacionados | EXC-FIS-003 |
| Datas | Registrado em 2026-10-22 |

### EXC-FIS-005 — Nenhum tratamento da Reforma Tributária (IBS/CBS) no código

| Campo | Valor |
|---|---|
| Área | FIS |
| Natureza | Fato (ausência no código) + Lacuna (obrigações e prazos aplicáveis ao produto) |
| Descrição | Nenhuma referência a IBS, CBS ou Reforma Tributária no código. DOC-25, DOC-27 e DOC-30 já apontavam o tema como prioridade. Os prazos e campos exigidos para NF-e/NFC-e dos clientes do produto precisam ser confirmados com contador e com o emissor (Focus NFe). |
| Evidência | Busca por `IBS`, `CBS` e "reforma tribut" nos `.cs` (2026-10-22); DOC-25, DOC-27, DOC-30 |
| Gravidade | **A classificar** — depende das obrigações vigentes para os clientes e de quanto o emissor absorve. |
| Impacto | Rejeição de documentos fiscais ou descumprimento quando as novas regras forem exigidas. |
| Confiança | Alta (ausência no código) |
| Dependências | Contador; documentação da Focus NFe |
| Responsável | A definir |
| Ação proposta | Na EXC-A4/A9: levantar obrigações e cronograma aplicáveis e o que muda no payload enviado à Focus. |
| Status | Confirmado |
| Critério de validação | Plano de adequação aprovado por contador, com datas. |
| Origem | DOC-25, DOC-27, DOC-30 |
| Relacionados | — |
| Datas | Registrado em 2026-10-22 |

### EXC-FIS-006 — SPED EFD ICMS/IPI gerado sem Bloco E

| Campo | Valor |
|---|---|
| Área | FIS |
| Natureza | Fato |
| Descrição | O gerador de EFD ICMS/IPI produz blocos 0, C, H e 9, sem o Bloco E (apuração). DOC-31 observa que um arquivo assim é rejeitado pelo validador se algum cliente precisar entregá-lo, e que optantes do Simples Nacional costumam ser dispensados. |
| Evidência | `ERP.Infrastructure/Services/SpedEfdGenerator.cs` (nenhum registro `E001`, 2026-10-22); DOC-31 (B.1) |
| Gravidade | Baixa (enquanto os clientes forem do Simples Nacional — a confirmar com contador) |
| Impacto | Arquivo inválido para clientes obrigados à EFD ICMS/IPI. |
| Confiança | Alta (ausência do bloco); Média (consequência) |
| Dependências | Contador |
| Responsável | A definir |
| Ação proposta | Na EXC-A4: confirmar o regime dos clientes; decidir entre completar o arquivo ou restringir/avisar na tela. |
| Status | Confirmado |
| Critério de validação | Arquivo aceito pelo validador oficial para o perfil de cliente atendido, ou restrição documentada. |
| Origem | DOC-31 |
| Relacionados | — |
| Datas | Registrado em 2026-10-22 |

### EXC-FIS-007 — Caminho antigo de contingência reenvia NFC-e com `DataEmissao` congelada

| Campo | Valor |
|---|---|
| Área | FIS |
| Natureza | Fato (no código); exposição em produção depende de EXC-FIS-001 |
| Descrição | DOC-35 (3B) diagnosticou que a pendência fiscal guarda o payload com `DataEmissao` do momento da falha e o reenvio não a atualiza. Para NFC-e, a SEFAZ rejeita (704) quando a diferença passa de ~5 minutos, segundo a fonte citada no documento. A Etapa 4 corrige isso no caminho novo (`4A-5a`), que é inerte e ligado por tenant. Para tenants sem o interruptor ligado, o caminho antigo (`NfeContingencyHostedService` → `NfeContingencyService`) continua reenviando o payload original. |
| Evidência | `ERP.Application/Services/NfeContingencyService.cs:50` (payload gravado, sem regeneração da data); `ERP.Api/BackgroundServices/NfeContingencyHostedService.cs:141-146` (decisão por interruptor); commit `f312a77` (correção no caminho novo); DOC-35 |
| Gravidade | **A classificar** — Alta se algum tenant em produção estiver no caminho antigo com NFC-e pendentes (vendas sem documento fiscal válido); Baixa se todos estiverem no caminho novo. |
| Impacto | NFC-e rejeitadas no reenvio; vendas sem documento fiscal autorizado. |
| Confiança | Média (comportamento lido no código e no diagnóstico; não reproduzido) |
| Dependências | EXC-FIS-001 |
| Responsável | A definir |
| Ação proposta | Na EXC-A4: confirmar o modo de cada tenant e as pendências existentes; não alterar o caminho antigo sem o plano da Etapa 4. |
| Status | Confirmado |
| Critério de validação | Nenhum tenant em produção exposto, ou correção validada em homologação. |
| Origem | INV-022; DOC-35 |
| Relacionados | EXC-FIS-001 |
| Datas | Registrado em 2026-10-22 |

---

## Detalhe — Produto (PRD)

### EXC-PRD-001 — Roadmap exibido no Portal não corresponde ao produto atual

| Campo | Valor |
|---|---|
| Área | PRD |
| Natureza | Fato |
| Descrição | `Roadmap.razor` lista como "planejado" ou "futuro" itens que já existem no código (entrada de NF-e e MD-e, transferências, Pix automático, fidelidade, marketplace, e-commerce, dashboard de TV) e o Mobile como "em desenvolvimento", embora o projeto tenha uma página, sem alterações desde maio/2026, e seja removido do build do CI. DOC-33 recomendava não investir no Mobile agora. DOC-32 lista o que não deve ser prometido em demonstração (offline, TEF, app mobile); offline mudou desde então (INVENTARIO §4.1), TEF foi removido. |
| Evidência | `ERP.Portal/Pages/Roadmap.razor:170-296`; `ERP.Mobile/`; `.github/workflows/ci-cd.yml`; DOC-32, DOC-33 |
| Gravidade | Baixa |
| Impacto | Comunicação ao cliente inconsistente com o produto. |
| Confiança | Alta |
| Dependências | Decisão sobre o escopo do Mobile (confirmação 8) |
| Responsável | A definir |
| Ação proposta | Na EXC-A7: definir o escopo de cada aplicativo e a fonte do roadmap. |
| Status | Confirmado |
| Critério de validação | CRITERIOS PRD-2 e PRD-4. |
| Origem | EXC-F0; DOC-32, DOC-33 |
| Relacionados | EXC-OPS-004 |
| Datas | Registrado em 2026-10-22 |

---

## Detalhe — Dados e governança (DAD)

### EXC-DAD-001 — Parte dos relatórios anteriores não foi localizada

| Campo | Valor |
|---|---|
| Área | DAD (governança) |
| Natureza | Lacuna |
| Descrição | Os 35 relatórios entregues em 2026-10-22 cobrem a maior parte da história. Faltam os citados no INVENTARIO §2.3 (6ª, 7ª e 14ª auditorias, a auditoria com nota 9,5, a parte 1 da auditoria do PDV, a "Análise Crítica v2" e a auditoria de 10/08). Como a série de segurança é encadeada, o risco de pendência perdida é baixo. Os relatórios continuam fora do repositório. |
| Evidência | INVENTARIO §2 |
| Gravidade | Baixa (era Média antes da entrega dos relatórios) |
| Impacto | Pequeno risco de pendência antiga sem destino. |
| Confiança | Alta |
| Dependências | — |
| Responsável | Matheus |
| Ação proposta | Localizar os relatórios faltantes, se existirem; decidir onde os relatórios devem ser guardados (repositório, pasta compartilhada). |
| Status | Lacuna |
| Critério de validação | Relatórios faltantes localizados ou declarados inexistentes; local de guarda definido. |
| Origem | EXC-F0 |
| Relacionados | — |
| Datas | Registrado em 2026-10-22; atualizado em 2026-10-22 (entrega dos relatórios) |

### EXC-DAD-002 — Sem concorrência otimista; créditos de Haver por leitura-e-gravação

| Campo | Valor |
|---|---|
| Área | DAD (também ENG) |
| Natureza | Fato (padrão no código) + Hipótese (perda em uso real) |
| Descrição | Nenhuma entidade tem controle de concorrência otimista (`RowVersion`/`IsConcurrencyToken`). O débito de Haver e a baixa de contas a receber já usam UPDATE relativo. Os créditos de Haver no cancelamento de venda e na devolução ainda leem o saldo, somam em memória e gravam. Duas operações simultâneas sobre o mesmo cliente podem perder uma atualização. |
| Evidência | Nenhum `RowVersion` em Domain/Persistence (2026-10-22); `ERP.Application/Services/SaleService.cs:338` (`CancelAsync`); `ERP.Application/Services/DevolucaoService.cs:107` (`DevolverItensAsync`); débito atômico em `ERP.Infrastructure/Repositories/SpecificRepositories.cs:165`; DOC-31 (D), DOC-33 §8 |
| Gravidade | Média (dinheiro; exige operações simultâneas no mesmo cliente) |
| Impacto | Saldo de Haver incorreto em concorrência. |
| Confiança | Alta (código); Baixa (frequência real com poucos terminais) |
| Dependências | — |
| Responsável | A definir |
| Ação proposta | Na EXC-A3: mapear todos os read-modify-write de valores monetários e de estoque; priorizar pelo risco. |
| Status | Confirmado |
| Critério de validação | Operações monetárias atômicas ou protegidas por concorrência, com teste de concorrência. |
| Origem | INV-012; DOC-31, DOC-33 |
| Relacionados | EXC-ENG-004 |
| Datas | Registrado em 2026-10-22 |

---

## Detalhe — Administração (ADM) e Vendas (VEN)

### EXC-ADM-001 — Custo de operação por cliente desconhecido

| Campo | Valor |
|---|---|
| Área | ADM |
| Natureza | Lacuna |
| Descrição | Não há dado sobre custo de infraestrutura (Azure), emissão fiscal (a Focus cobra por documento, segundo DOC-32) e suporte por cliente. DOC-25 recomenda modelar esse custo antes de fixar preço para muitos clientes. DOC-33 registra que o plano F1 do App Service já esgotou CPU com um cliente. |
| Evidência | DOC-25 (F), DOC-32 (E), DOC-33 §15 |
| Gravidade | **A classificar** |
| Impacto | Preço abaixo do custo em clientes de alto volume. |
| Confiança | — |
| Dependências | Faturas do Azure e da Focus |
| Responsável | Matheus |
| Ação proposta | Na EXC-A10: levantar custos reais dos últimos meses. |
| Status | Lacuna |
| Critério de validação | CRITERIOS ADM-1. |
| Origem | DOC-25, DOC-32, DOC-33 |
| Relacionados | EXC-VEN-001 |
| Datas | Registrado em 2026-10-22 |

### EXC-VEN-001 — Preços e planos sem validação com clientes

| Campo | Valor |
|---|---|
| Área | VEN |
| Natureza | Lacuna |
| Descrição | DOC-32 propõe planos e faixas de preço, todas marcadas pelo próprio documento como **[estimativa]**, e recomenda validá-las nas primeiras conversas com clientes. O único preço praticado documentado é o do piloto (R$ 2.000 de implantação + R$ 99,90/mês, segundo DOC-34), muito abaixo das faixas propostas. Existe um mecanismo de módulos por tenant (`TenantFeatureFlags`), mas não foi avaliado como controle de plano. |
| Evidência | DOC-32 (E), DOC-34; `ERP.Domain/Entities/TenantFeatureFlags.cs` |
| Gravidade | **A classificar** |
| Impacto | Sem preço validado, não há base para projeção comercial. |
| Confiança | — |
| Dependências | Conversas com clientes; EXC-ADM-001 |
| Responsável | Matheus |
| Ação proposta | Na EXC-A10: registrar preços praticados e propostas enviadas (ex.: Eldorado, DOC-29) e definir como validar. |
| Status | Lacuna |
| Critério de validação | CRITERIOS VEN-2. |
| Origem | DOC-32, DOC-34 |
| Relacionados | EXC-ADM-001 |
| Datas | Registrado em 2026-10-22 |
