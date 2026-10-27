# EXC-A2 — Segurança de aplicação e qualidade (primeira passada)

> Programa de Excelência TTSoft · Produto: ConstruTTor ERP
> Data: 2026-10-26 · Base: commit `1de867b` · Auditor: Claude Code, sob revisão de Matheus
> Classificação conforme [CRITERIOS-DE-EXCELENCIA](CRITERIOS-DE-EXCELENCIA.md) · Plano: [PLANO-MESTRE](PLANO-MESTRE.md) · Registro: [REGISTRO-CENTRAL](REGISTRO-CENTRAL.md)

---

## 1. Escopo e método

**Examinado (leitura de código):** autenticação e emissão de tokens da API (`AuthController`,
`ChatTokenController`, `Program.cs`), middlewares (tenant, troca obrigatória de senha, limites de
requisição), autorização por endpoint (189 ações em 37 controllers, levantadas por script),
filtros de tenant e usos de `IgnoreQueryFilters` na API/Infrastructure, cadastro público,
webhooks e OAuth de marketplace, canal de e-commerce, SignalR, storage de imagens, Portal
(armazenamento do token e HTML gerado), proteção de segredos (`TokenProtector`), atualizador do
WPF, configuração (`appsettings*.json`) e dependências.

**Executado:** `dotnet list ERP.sln package --vulnerable --include-transitive` em 2026-10-26.

**Não executado:**
- Testes de reprodução dos achados. Esta passada não produziu testes que exercitem os cenários
  de abuso; a coluna *Validação* da matriz diz o que falta para cada item. Os critérios de
  validação (seção 5) descrevem **testes de regressão** que comprovam o comportamento seguro
  depois da correção.
- Nenhum acesso ao Azure, à Focus, ao banco de produção ou a dados de clientes.

**Fora do escopo:** o que já está registrado e planejado (EXC-SEG-001, 006, 009 — acesso direto
do WPF ao SQL e firewall), regras fiscais (A4), integridade transacional (A3).

**Regra de leitura:** *Fato (código)* = comportamento lido no código, sem execução.
Pela CRITERIOS §1.4, nada aqui comprova comportamento no SQL Server/Azure.

---

## 2. Matriz de riscos

Ordenada por gravidade e, no empate, pela prioridade da CRITERIOS §5.

| ID | Achado | Natureza | Gravidade | Confiança | Validação |
|---|---|---|---|---|---|
| EXC-SEG-011 | Cadastro público decide se a loja já existe pela presença de um usuário `admin`; com CNPJ de loja existente sem esse usuário, cria cargos e administrador **dentro do tenant existente** | Fato (código) + Lacuna (dados) | **A classificar** — Crítica se algum tenant ativo não tiver `admin` não excluído; Alta (latente) caso contrário | Alta (código) | Consulta C-A2-1; teste de regressão R1 |
| EXC-SEG-012 | Tokens auxiliares (chat, redefinição de senha) são aceitos como token de sessão pela API inteira e não passam pela checagem de revogação; o token de chat emite outro token de chat | Fato (código) | **Alta** | Alta (código) | Testes R2, R3 |
| EXC-SEG-015 | XSS armazenado nos relatórios do Portal (nomes de cliente, vendedor e produto inseridos como HTML), com o token de sessão guardado em `localStorage` | Fato (código) + Hipótese (exploração) | **Alta** | Alta (código) | Teste R8 |
| EXC-SEG-017 | Tokens da Focus NFe cifrados com chave fixa no código-fonte e no binário do WPF | Fato | **Alta** (com EXC-SEG-001; Média isolado) | Alta | Critério V7 |
| EXC-SEG-002 *(atualizado)* | Dependências com avisos High: `System.Security.Cryptography.Xml` 8.0.3 (9 avisos; usado em `SignedXml` na importação de XML de fornecedor), `System.Security.Cryptography.Pkcs` 8.0.1, `AutoMapper` 14.0.0, `Microsoft.AspNetCore.SignalR.Common` 8.0.0, `System.Text.Json` 8.0.0, `SQLitePCLRaw.lib.e_sqlite3` 2.1.6 | Fato | **Alta** (Cryptography.Xml processa XML externo); demais a classificar | Alta (varredura) | Varredura sem High/Critical |
| EXC-SEG-013 | Redefinição de senha não revoga sessões abertas; troca de e-mail do perfil não pede a senha atual | Fato (código) | Média | Alta (código) | Testes R4, R5 |
| EXC-SEG-014 | Token de autorizador (sangria/suprimento): checagem de revogação provavelmente inoperante; o limite aplicado é o do solicitante, e `0` significa "sem limite" | Fato (código) + Hipótese (mapeamento da claim) | Média | Média | Testes R6, R7 |
| EXC-SEG-016 | Autorização por recurso incompleta: ~70 leituras exigem só login (contas a receber/pagar, SPED, clientes, notas); permissões `*.view` liberam escrita (cancelar nota, baixa, pagar, configuração) | Fato | Média | Alta | Matriz de permissões aprovada + testes negativos |
| EXC-SEG-018 | Limites por IP usam cabeçalhos enviados pelo cliente (`X-Real-IP`, `X-Forwarded-For` de qualquer origem); o limite de login por CNPJ (público) permite bloquear o login de uma loja pela API | Fato | Média | Alta (código; teste existente S11 confirma o particionamento por `X-Forwarded-For`) | Teste R9 |
| EXC-SEG-021 | Atualizador do WPF confere SHA-256 publicado no mesmo local do executável: integridade, sem autenticidade (critério SEG-5) | Fato | Média | Alta | Critério V9 |
| EXC-SEG-020 | Nome do arquivo da foto de entrega entra sem validação no nome do blob (`{tenant}/{entrega}/{fileName}`) | Fato (código) + Hipótese (alcance fora do prefixo do tenant) | **A classificar** — Média se o SDK normalizar o caminho; Baixa se não | Média | Teste R10 |
| EXC-SEG-019 | Webhook da Shopee aceita chave vazia ou o *placeholder*; a integração não existe | Fato (código) | Baixa (a confirmação do pedido falha hoje) | Alta (código) | Rota desativada ou guarda de configuração |
| EXC-SEG-022 | Configuração sem guarda contra *placeholders* (`Jwt:Key`, Shopee); Swagger público se o ambiente for `Staging`; CORS de produção com origens locais | Fato + Lacuna (ambiente do App Service) | Baixa | Média | Consulta C-A2-2 |
| EXC-SEG-023 | OAuth do Mercado Livre usa o Id do canal como `state`, fixo e reutilizável | Fato | Baixa | Alta | Teste R11 |
| EXC-SEG-024 | Tokens em *query string* (SignalR `access_token`, `chatToken`) podem ser gravados pela telemetria de requisições; e-mail completo em log de falha de envio | Hipótese | Baixa | Baixa | Consulta C-A2-3 |

**Verificado e sem achado novo:** canal de e-commerce (chave ≥ 32 caracteres, comparação em
tempo constante, tenant fixo na configuração, falha fechado); `ERPHub` (grupo pelo tenant da
claim); webhook do Mercado Livre (não confia no corpo, busca o pedido de novo com o token da
loja); troca obrigatória de senha; bloqueio de conta após tentativas (`LockoutPolicy`); token de
sessão com revogação por `TokenVersion`; *exception handler* sem detalhes ao cliente; ausência
de segredos ativos no código (busca por senhas, chaves e *hashes* em `.cs`/`.json`).

---

## 3. Detalhe dos achados

### EXC-SEG-011 — Cadastro público e tenant existente
- **Evidência:** `ERP.Infrastructure/Services/CadastroService.cs:114` (existência = usuário
  `admin` não excluído no tenant derivado do CNPJ); `:122-162` (cria cargos, permissões e
  usuário `admin` com o `TenantId` derivado); o TenantId é função pública do CNPJ
  (`ERP.Api/Models/TenantHelper.cs`); não há índice único em `Users(TenantId, Username)`, nem em
  `Roles`/`Permissions` por tenant (`AppDbContext.cs`, busca por `IsUnique`). A verificação na
  Receita (`:55-105`) confere situação e e-mail, que são dados públicos, e falha aberta enquanto o
  circuito da BrasilAPI está fechado (`BrasilApiService.cs:124-155`).
- **Impacto:** uma loja que renomeou ou excluiu o `admin` (ou que nunca o teve, como tenants
  criados pelo *seeder* do WPF) fica com um segundo conjunto de cargos e um administrador
  criado por terceiros, com acesso pela API aos dados e às operações fiscais da loja.
  Relaciona-se a EXC-VEN-002 (cadastro cria loja no banco de produção).
- **Ação proposta:** desligar o cadastro público por configuração até a correção; verificar
  existência pelo tenant (qualquer usuário, cargo ou filial com o `TenantId`, incluindo
  excluídos); índice único `(TenantId, Username)`; ativação de loja nova só com aprovação
  manual enquanto não houver contrato (EXC-FIS-002).

### EXC-SEG-012 — Separação entre tipos de token
- **Evidência:** todos os tokens usam a mesma chave, emissor e audiência:
  sessão (`AuthController.cs:365-407`), redefinição (`:162-185`), chat
  (`ChatTokenController.cs:44-79`). O *bearer* não distingue o tipo e só checa revogação se
  houver `token_version` (`Program.cs:372`); os tokens de chat e de redefinição não têm essa
  claim. O endpoint `chat-token` exige apenas `[Authorize]` (`ChatTokenController.cs:44-45`),
  logo aceita o próprio token de chat.
- **Impacto:** um token de 5 minutos ou de redefinição dá acesso às ~70 leituras e às escritas
  que exigem só login (ver EXC-SEG-016), e a revogação (logout, desativação, troca de senha) não
  o alcança. Escalada de privilégio não ocorre (esses tokens não têm permissões), mas a
  revogação deixa de ser garantia.
- **Ação proposta:** audiência (ou claim `typ`) distinta por tipo de token; o *bearer* da API
  aceita só tokens de sessão e exige `token_version`; `chat-token` emitido só a partir de token
  de sessão; validar o token de chat também contra `TokenVersion`.

### EXC-SEG-015 — XSS armazenado no Portal
- **Evidência:** `ERP.Portal/Pages/Relatorios.razor:103` renderiza `MarkupString` montado com
  `CustomerName`, `SellerName` e `p.Nome` sem codificação (`:158-159`, `:286`; nenhuma
  chamada a `HtmlEncode` no arquivo). Token em `localStorage` (`Services/ApiClient.cs:23,160`).
- **Impacto:** quem pode editar nomes de cliente (cargo Vendedor tem `customers.edit`) ou de
  produto (inclusive por XML de compra importado) injeta conteúdo executado no navegador de
  quem abre o relatório; o token de sessão desse usuário fica ao alcance do script.
  Escalada de privilégio dentro do tenant (CRITERIOS §3, Alta).
- **Ação proposta:** codificar todo valor interpolado (`WebUtility.HtmlEncode`) ou trocar o
  `MarkupString` por componentes Razor; avaliar CSP no Static Web App; a médio prazo, token fora
  do `localStorage`.

### EXC-SEG-017 — Chave fixa do `TokenProtector`
- **Evidência:** `ERP.Application/Helpers/TokenProtector.cs:20-21` (chave derivada de texto fixo);
  `Desproteger` devolve a entrada em caso de erro (`:61-64`).
- **Impacto:** quem lê o banco (hoje, qualquer PC de loja — EXC-SEG-001) e tem o binário decifra
  os tokens da Focus de todas as lojas e pode emitir ou cancelar documentos fiscais em nome
  delas. A ação proposta em EXC-SEG-004 (usar o `TokenProtector` nos tokens do Mercado Livre)
  herdaria o mesmo problema.
- **Ação proposta:** chave fora do código (Key Vault ou configuração do App Service), com
  rotação e recifragem dos registros; o WPF deixa de decifrar tokens fiscais (emissão só pela
  API — EXC-FIS-004); revisar EXC-SEG-004 para usar o mecanismo novo.

### EXC-SEG-002 — Dependências (atualização)
- **Evidência:** varredura de 2026-10-26 (saída na pasta temporária da sessão; repetir com
  `dotnet list ERP.sln package --vulnerable --include-transitive`). Nenhum aviso *Critical*.
  `System.Security.Cryptography.Xml` é referência direta em `ERP.Application.csproj:9` e é usado
  em `NfeImportService.cs:228` (`SignedXml` sobre XML recebido de fornecedor).
  `ERP.Domain` e `ERP.Updater` sem avisos.
- **Ação proposta:** atualizar para os patches 8.0.x correntes (`Cryptography.Xml`, `Pkcs`,
  `System.Text.Json`, `SignalR.Common`) e o `SQLitePCLRaw`; avaliar a versão corrigida do
  AutoMapper (mudança de licença nas versões novas — decisão do responsável); falhar o CI com
  `NU1903/NU1904`.

### EXC-SEG-013 — Redefinição de senha e troca de e-mail
- **Evidência:** `AuthController.cs:262` grava a senha nova sem `RevokeSessionsAsync`
  (a troca autenticada revoga: `AuthService.cs`, `ChangePasswordAsync`);
  `AuthController.cs:342-363` troca o e-mail sem pedir a senha atual.
- **Impacto:** depois de uma redefinição feita justamente por suspeita de acesso indevido, as
  sessões antigas continuam válidas por até 12 h; o e-mail de recuperação pode ser trocado por
  quem tem só uma sessão (ou um token de EXC-SEG-012).
- **Ação proposta:** revogar sessões na redefinição; exigir senha atual para trocar e-mail e
  avisar o e-mail anterior.

### EXC-SEG-014 — Autorização de sangria por token de outro usuário
- **Evidência:** `CaixaController.cs:254` procura a claim `sub`, que o `JwtSecurityTokenHandler`
  remapeia por padrão para `NameIdentifier` (o próprio projeto contorna isso em
  `AuthController.cs:215`); se a claim não é encontrada, a checagem de revogação é pulada.
  O limite aplicado é o do solicitante (`:118`), e `SangriaPolicy.cs:32` trata `0` como "sem
  limite" — o valor que o *seeder* dá ao Vendedor (`DbSeeder.cs:214`), cuja intenção aparente é
  "sem sangria".
- **Impacto:** gerente desligado continua autorizando até o token expirar; um autorizador com
  limite menor libera sangria acima do próprio limite.
- **Ação proposta:** limpar o mapeamento de claims nessa validação; aplicar o limite do
  autorizador; separar "sem limite" de "zero" (ex.: `null` = sem limite).

### EXC-SEG-016 — Autorização por recurso
- **Evidência:** levantamento de atributos das 189 ações: leituras com só `[Authorize]` em
  `ContasReceberController`, `ContasPagarController`, `FiscalController` (SPED),
  `CustomersController`, `NotasFiscaisController`, `SalesController.GetReport` e outras;
  `HasPermission(...View)` em escritas, ex. `NotasFiscaisController` (emitir/cancelar),
  `ContasReceberController` (baixa/cancelar/desconto), `ConfigController.cs:36-38` (PUT),
  `SalesChannelsController`. Não há política *fallback* (`AddAuthorization` em
  `Program.cs:396-403`), então ação sem atributo fica anônima.
- **Impacto:** qualquer cargo lê dados financeiros e fiscais da loja; não dá para dar acesso de
  consulta sem dar acesso de alteração.
- **Ação proposta:** matriz cargo × recurso × operação aprovada pelo responsável; permissões de
  leitura e escrita separadas; `FallbackPolicy = RequireAuthenticatedUser`; teste que falha se
  uma ação nova não tiver atributo de autorização.

### EXC-SEG-018 — Limites de requisição
- **Evidência:** `Program.cs:188` (`RealIpHeader = "X-Real-IP"`), `:580-585`
  (`KnownNetworks`/`KnownProxies` vazios); teste existente
  `ControllersIntegrationTests.cs:2884-2930` mostra baldes separados por `X-Forwarded-For`.
  `TenantRateLimitMiddleware.cs:36,62` limita login a 5/min pelo CNPJ do cabeçalho.
- **Impacto:** os limites de recuperação de senha e de cadastro não seguram um cliente que varie
  os cabeçalhos; o limite por CNPJ, que vale também para quem não está autenticado, pode impedir
  o login legítimo da loja pela API.
- **Ação proposta:** confiar só no proxy do App Service; remover `X-Real-IP`; limite de login
  por (CNPJ, usuário) combinado com IP real; o bloqueio por conta (`LockoutPolicy`) já cobre
  força bruta por usuário.

### EXC-SEG-021 — Autenticidade do atualizador
- **Evidência:** `ERP.WPF/Services/UpdateService.cs:31` (manifesto no mesmo container),
  `:88`, `:138-139` (só SHA-256 do manifesto).
- **Ação proposta:** assinar o executável (Authenticode) e verificar a assinatura antes de
  instalar, ou assinar o manifesto com chave assimétrica cuja pública vai no WPF; restringir a
  escrita no container de *releases*.

### EXC-SEG-020, 019, 022, 023, 024 — Itens de menor gravidade
- **020:** `StorageService.cs:162`; validar `fileName` (só nome simples, sem `/`, `\`, `..`).
- **019:** `WebhookSignatureValidator.cs:83-117` não recusa chave vazia (o do Mercado Livre
  recusa); `MarketplaceController.cs` rota Shopee anônima. Desativar a rota até existir a
  integração; recusar chave vazia ou *placeholder*.
- **022:** `appsettings.json` (placeholders `CONFIGURADO_VIA_AZURE_APP_SERVICE` em `Jwt:Key` e
  Shopee; CORS com `localhost` e `192.168.0.111`); `Program.cs:630` (Swagger em `Staging`).
  Validar a configuração na partida e abortar com *placeholder*; CORS de produção só com
  domínios de produção.
- **023:** `MercadoLivreAuthService.cs:44`; `state` aleatório de uso único, guardado no início da
  autorização.
- **024:** `Program.cs:340-348`, `ERPHub.cs` (`chatToken` em *query*);
  `AuthController.cs:310` (e-mail em log). Filtrar a *query string* na telemetria; mascarar
  e-mail como já faz `CadastroService.MascararEmail`.

---

## 4. Qualidade e testes

- A suíte (1322 testes em 2026-10-22) cobre bem a revogação de sessão (`TokenVersion`), a troca
  obrigatória de senha, o limite de login por CNPJ e várias regressões S8–S15.
- **Lacunas de teste de segurança:** nenhum teste negativo por tipo de token, nenhum de
  cadastro com CNPJ de tenant existente, nenhum de renderização do Portal, nenhum teste que
  enumere as ações sem permissão específica. As fábricas de teste geram tokens sem
  `token_version` (`ControllersIntegrationTests.cs:261-300`), justamente o caminho que pula a
  revogação — por isso esse comportamento nunca aparece como falha.
- Testes rodam em SQLite (EXC-ENG-001): índices únicos e *collation* do SQL Server não estão
  cobertos, o que pesa no EXC-SEG-011.
- Fronteiras: WPF → banco direto (EXC-SEG-001) continua sendo a fronteira mais fraca; API →
  banco depende só do filtro global e de `IgnoreQueryFilters` pontuais (52 usos; os da API e
  Infrastructure têm condição explícita de tenant, exceto o OAuth do Mercado Livre — EXC-SEG-023
  — e os serviços de limpeza, que são intencionalmente globais); Portal → API via token em
  `localStorage` (EXC-SEG-015).

---

## 5. Plano de mitigação

Cada correção segue o PLANO-MESTRE §6: triagem, validação antes do merge na `main`
(EXC-OPS-002), e teste de regressão que comprova o comportamento **seguro**.

| Onda | Itens | Ação | Validação |
|---|---|---|---|
| **0 — sem código, agora** | 011, 022, 024 | Consultas C-A2-1 a C-A2-3 (somente leitura); se C-A2-1 encontrar tenant sem `admin`, tratar como incidente | Resultado registrado |
| **1 — correções pequenas, alto impacto** | 011, 012, 013, 014, 015 | Existência do tenant por qualquer registro + índice único; tipos de token separados e *bearer* só de sessão; revogação na redefinição; senha atual na troca de e-mail; mapeamento de claims e limite do autorizador; codificação HTML nos relatórios | R1–R8 |
| **2 — dependências e bordas** | 002, 018, 019, 020, 022 | Patches de pacotes; proxy confiável e chaves de limite; desativar rota Shopee; validar nome de arquivo; guarda de configuração na partida | Varredura limpa; R9, R10 |
| **3 — estruturais** | 016, 017, 021, 023 | Matriz de permissões leitura/escrita e *fallback policy*; chave do `TokenProtector` fora do código com recifragem; assinatura do atualizador; `state` OAuth de uso único | V7, V9, R11, teste de enumeração de ações |

### Testes de regressão a escrever junto com cada correção (comportamento esperado)
- **R1** Cadastro com CNPJ cujo tenant já tem qualquer registro é recusado sem criar nada.
- **R2** API responde 401 a token de chat e a token de redefinição.
- **R3** `chat-token` recusa token de chat; token de chat de usuário revogado não conecta ao hub.
- **R4** Após redefinição de senha, token de sessão anterior recebe 401.
- **R5** Troca de e-mail sem a senha atual é recusada.
- **R6** Autorizador com sessão revogada não autoriza sangria.
- **R7** Sangria autorizada acima do limite do autorizador é recusada; cargo com limite zero não faz sangria.
- **R8** Relatório renderiza nome com marcação como texto (teste de componente ou da função que monta o HTML).
- **R9** Variar `X-Forwarded-For`/`X-Real-IP` não abre balde novo de limite.
- **R10** Nome de arquivo com separadores ou `..` é recusado com 400.
- **R11** Callback OAuth com `state` desconhecido ou já usado é recusado.
- **V7** Nenhuma chave de cifragem em código; registros recifrados. **V9** WPF recusa executável sem assinatura válida.

### Consultas somente leitura (para o responsável)
- **C-A2-1:** por tenant ativo, existe usuário `Username = 'admin'` e `IsDeleted = 0`? (contagem por `TenantId`, sem listar dados pessoais).
- **C-A2-2:** valor de `ASPNETCORE_ENVIRONMENT` e presença das chaves `Jwt__Key`, `Marketplace__Shopee__*` no App Service (só se existem, não os valores).
- **C-A2-3:** no Application Insights, se a coluna `url` das requisições a `/hubs/` guarda a *query string*.

---

## 6. Critérios SEG (CRITERIOS §9.6) após esta passada

| Critério | Situação |
|---|---|
| SEG-1 Segredos | Não atendido — EXC-SEG-001/006 (planejados), EXC-SEG-017 |
| SEG-2 Isolamento | Não atendido — EXC-SEG-001; EXC-SEG-011 a confirmar; testes só em SQLite |
| SEG-3 Dependências | Não atendido — EXC-SEG-002 (6 pacotes com avisos High) |
| SEG-4 Autenticação/autorização com testes negativos | Parcial — revogação e troca de senha cobertas; EXC-SEG-012 a 016 sem cobertura |
| SEG-5 Atualizações com autenticidade | Não atendido — EXC-SEG-021 |

## 7. Próximos passos

1. Triagem dos itens Alta e "A classificar" (011, 012, 015, 017, 002) e execução das consultas C-A2-1 a C-A2-3.
2. Incorporar os registros EXC-SEG-011 a 024 e a atualização do EXC-SEG-002 ao REGISTRO-CENTRAL.
3. Segunda passada: executar as validações pendentes e cobrir o WPF além do atualizador (sessão local, `PermissionChecker`, modo offline).
