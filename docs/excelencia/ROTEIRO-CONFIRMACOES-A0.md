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

## Passo 5 — Consultas de acompanhamento (após os resultados de 2026-10-22)

Conectado a `ERPTTSoftStaging`, somente `SELECT`.

```sql
-- E1 (EXC-FIS-008) Notas do tenant de teste e da Vila Verde, por ambiente e status
SELECT TenantId, Tipo, Ambiente, Status, COUNT(*) AS qtd,
       MIN(DataEmissao) AS primeira, MAX(DataEmissao) AS ultima
FROM NotasFiscais
WHERE TenantId IN ('8FB9FC74-BBF7-8418-C6D6-48B7F1EB5466', 'B7F9F866-0695-EF92-E546-52C20EFE57D6')
GROUP BY TenantId, Tipo, Ambiente, Status
ORDER BY TenantId, Tipo, Ambiente, Status;

SELECT TenantId, NfceAmbiente, NfceStatusFocus, COUNT(*) AS qtd,
       MIN(NfceEmitidaEm) AS primeira, MAX(NfceEmitidaEm) AS ultima
FROM Sales
WHERE TenantId = '8FB9FC74-BBF7-8418-C6D6-48B7F1EB5466' AND NfceChave IS NOT NULL
GROUP BY TenantId, NfceAmbiente, NfceStatusFocus;

-- E2 (EXC-FIS-007) A pendência ativa, sem o payload
SELECT Id, VendaId, TipoNota, Referencia, DataFalha, Tentativas, Estado,
       ProximaTentativaEm, UltimaDecisao, LEFT(UltimaMensagemErro, 300) AS ultima_mensagem
FROM NfePendentes
WHERE Estado = 'Ativa';

-- E3 (EXC-SEG-001) Repetir conectado ao banco master, apenas para documentar
SELECT name, HAS_DBACCESS(name) AS tem_acesso FROM sys.databases ORDER BY name;

-- E4 (EXC-DAD-003) Eventos de auditoria sem tenant
SELECT EntityType, Action, MachineName, [Timestamp]
FROM AuditLogs
WHERE TenantId = '00000000-0000-0000-0000-000000000000';

-- E5 (EXC-SEG-001) Resultado da 2.4 não informado
SELECT name, is_enabled FROM sys.security_policies;
```

### Rodada 2 (após E1 e E2)

Mesma conexão e mesmo banco (`ERPTTSoftStaging`), somente `SELECT`. Na tela de resultados,
copie como texto ou tire um print.

```sql
-- E6 (EXC-FIS-009) Itens da venda presa: procure nomes com aspas ou "EMB"
SELECT ProductName, Quantity, UnitPrice
FROM SaleItems
WHERE SaleId = '1A7F2760-01DD-4A47-A3FF-BC18B75BAE33';

-- E7 (EXC-FIS-009) Trecho do payload guardado em volta de "EMB" (só o trecho, não o payload inteiro)
SELECT SUBSTRING(PayloadJson, CHARINDEX('EMB', PayloadJson) - 120, 200) AS trecho
FROM NfePendentes
WHERE Id = '342ED764-1F86-4AFD-B6AB-3310394993D8';

-- E8 (EXC-FIS-008) Notas de produção do tenant de teste com o CNPJ emitente tirado da chave de acesso
--   (posições 7 a 20 da chave de 44 dígitos = CNPJ do emitente)
SELECT Tipo, Status, Finalidade, Numero, Serie, DataEmissao,
       SUBSTRING(Chave, 7, 14) AS cnpj_emitente, VendaId
FROM NotasFiscais
WHERE TenantId = '8FB9FC74-BBF7-8418-C6D6-48B7F1EB5466' AND Ambiente = 'Produção'
ORDER BY DataEmissao;

-- E9 (EXC-FIS-010) Vendas da Vila Verde nos últimos 30 dias por status
--   (0 = sem nota, 1 = nota emitida, 2 = cancelada) e por existência de chave de NFC-e
SELECT Status,
       CASE WHEN NfceChave IS NULL OR NfceChave = '' THEN 'sem chave' ELSE 'com chave' END AS chave_nfce,
       COUNT(*) AS vendas
FROM Sales
WHERE TenantId = 'B7F9F866-0695-EF92-E546-52C20EFE57D6'
  AND CreatedAt >= DATEADD(day, -30, SYSUTCDATETIME())
GROUP BY Status, CASE WHEN NfceChave IS NULL OR NfceChave = '' THEN 'sem chave' ELSE 'com chave' END
ORDER BY Status;
```

Na Focus (painel web, somente consulta): procure a referência
`1a7f2760-01dd-4a47-a3ff-bc18b75bae33` (EXC-FIS-009) e as notas listadas pela E8 (EXC-FIS-008).

### Rodada 3 (após E6 a E9)

```sql
-- E10 (EXC-FIS-011) Produtos ativos da Vila Verde com aspas no nome
SELECT COUNT(*) AS produtos_com_aspas
FROM Products
WHERE TenantId = 'B7F9F866-0695-EF92-E546-52C20EFE57D6'
  AND IsDeleted = 0
  AND Name LIKE '%"%';

-- E11 (EXC-FIS-011) Vendas dos últimos 90 dias com itens que têm aspas, por status da venda
--   (0 = sem nota, 1 = nota emitida, 2 = cancelada)
SELECT s.Status, COUNT(DISTINCT s.Id) AS vendas
FROM Sales s
JOIN SaleItems i ON i.SaleId = s.Id
WHERE s.TenantId = 'B7F9F866-0695-EF92-E546-52C20EFE57D6'
  AND s.CreatedAt >= DATEADD(day, -90, SYSUTCDATETIME())
  AND i.ProductName LIKE '%"%'
GROUP BY s.Status;
```

Se E11 mostrar vendas com status 1 (nota emitida) contendo aspas, a hipótese de que a Focus
sempre recusa aspas fica enfraquecida e o caso precisa de outra explicação.

Na Focus (somente consulta): Requisições → empresa Vila Verde → ambiente produção → períodos
17/08, 23 a 26/09 e 20/10, para identificar as notas do tenant de teste sem chave (EXC-FIS-008).

---

## Registro dos resultados

| Passo | Resultado | Data | Quem verificou |
|---|---|---|---|
| 1.1 Administrador do servidor | `sa_ttsoft` (mesmo login das lojas); Entra admin, Auditoria e Defender "Não configurado"; 2 bancos no servidor: `ERPTTSoftStaging` e `ERPTTSoft` (Básico) | 2026-10-22 | Matheus (captura do portal) |
| 1.2 `FiscalRecovery__TenantsHabilitados` (produção / slot) | Produção: **não existe** → nenhum tenant habilitado, todas as lojas no caminho antigo. Slot `staging`: não verificado | 2026-10-22 | Matheus |
| 1.3 Banco e login da API | Banco `ERPTTSoftStaging` (declarado); login da API não informado | 2026-10-22 | Matheus |
| 1.4 Log da partida | Pendente | | |
| 2.1–2.3 Permissões no `ERPTTSoftStaging` | `dbo`; `db_owner`, `db_datareader`, `db_datawriter`, `db_ddladmin` = 1; inclui `CONTROL`, `ALTER ANY USER`, `BACKUP DATABASE`, `UNMASK`, `CREATE DATABASE` | 2026-10-22 | Matheus |
| 2.4 Políticas de segurança por linha | Não informado (E5) | | |
| 2.5 Bancos acessíveis | Executada no contexto do `ERPTTSoftStaging` (lista só ele e `master`); inconclusiva, repetir no `master` (E3). O papel de administrador do servidor já implica acesso ao `ERPTTSoft` | 2026-10-22 | Matheus |
| 3 Builds instaladas (por PC) | Os 3 PCs atualizados com o artefato WPF do último push (declarado; data e hash por PC não verificados) | 2026-10-22 | Matheus |
| 4.1 Tenants | 5: Vila Verde, teste `11222333000181`, CNPJ zerado, TTSoft `65183796000100`, `FDF3CD72…` não identificado | 2026-10-22 | Matheus |
| 4.2 Filiais | Vila Verde com 2 matrizes ativas; teste com 1; nenhuma com CNPJ | 2026-10-22 | Matheus |
| 4.3 Vendas 30 dias | Vila Verde em operação diária; teste 22 | 2026-10-22 | Matheus |
| 4.4 Máquinas | Vila Verde: `DESKTOP-JQB0BIQ`, `SERVIDOR`, `COMPUTADOR03` (+ hosts da API); teste e CNPJ zerado: `DESKTOP-3P3C5VH`; 2 eventos com TenantId vazio | 2026-10-22 | Matheus |
| 4.5 Configuração fiscal | Vila Verde e **tenant de teste** com CNPJ da Vila Verde e produção | 2026-10-22 | Matheus |
| 4.6 Pendências fiscais | 1 NFC-e da Vila Verde, `Ativa`, desde 2026-10-16 09:12, 10.653 tentativas | 2026-10-22 | Matheus |
| E1 Notas do tenant de teste | Produção: 7 NFC-e e 4 NF-e autorizadas, 2 NFC-e em "Contingência"; homologação: 2 autorizadas, 1 rejeitada; 5 vendas com chave de produção | 2026-10-22 | Matheus |
| E2 Pendência ativa | Venda `1A7F2760…`, 10.661 tentativas; Focus responde `UnsupportedMediaType` / `formato_invalido` (JSON inválido na coluna 1473, perto de "EMB") | 2026-10-22 | Matheus |
| E3, E5 | Pendentes (opcionais) | | |
| E6 Itens da venda presa | 4 itens; `TRINCHA MEDIA TIGRE 695 4" EMB 12 PC` tem aspas | 2026-10-22 | Matheus |
| E7 Trecho do payload | Aspas guardadas escapadas (`4\" EMB`), JSON válido no banco | 2026-10-22 | Matheus |
| Focus — referência da venda presa | Nenhuma requisição registrada (empresa Vila Verde, 10/09 a 23/10) | 2026-10-22 | Matheus (captura do painel) |
| E8 Emitente das notas do tenant de teste | 6 com CNPJ da Vila Verde (NFC-e 3303, 3315, 3318, 3319, 3322; NF-e devolução 314); 5 autorizadas sem chave gravada | 2026-10-22 | Matheus |
| E9 Vendas da Vila Verde por status (30 dias) | Executada; tratada como política do cliente (EXC-FIS-010, encerrado). Observação de dados registrada em EXC-DAD-003 | 2026-10-22 | Matheus |
| E10 Produtos com aspas | 3 produtos ativos | 2026-10-22 | Matheus |
| E11 Vendas com esses produtos (90 dias) | 4 vendas, todas "sem nota" | 2026-10-22 | Matheus |
