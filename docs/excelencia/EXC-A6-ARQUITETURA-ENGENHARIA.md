# EXC-A6 — Arquitetura e engenharia de software

> Programa de Excelência TTSoft · Área ENG (com achados de SEG, DAD e FIS)
> Criado em: 2026-10-26 · Versão: 1.0 · Base de código: commit `8801d9c` (main)
> Documentos relacionados: [PLANO-MESTRE](PLANO-MESTRE.md) · [CRITERIOS-DE-EXCELENCIA](CRITERIOS-DE-EXCELENCIA.md) · [REGISTRO-CENTRAL](REGISTRO-CENTRAL.md) · [INVENTARIO-AUDITORIAS](INVENTARIO-AUDITORIAS.md)

Esta é a primeira passada da EXC-A6. Ela foi executada **antes** do encerramento da EXC-A1
(isolamento), da EXC-A2 (segurança de aplicação) e da EXC-A3 (integridade transacional), a
pedido do responsável. Por isso: o isolamento entre tenants não é reauditado aqui (fica na A1);
os problemas transacionais encontrados são registrados e **entregues à A3**, que fará o mapa
completo; e o que é de segurança vai para a A2. Nenhuma linha de código foi alterada.

---

## 1. Resumo

1. **A arquitetura em camadas existe e, no servidor, é seguida em boa parte.** Domain não
   depende de nada; Application não conhece EF Core; a API é a raiz de composição; o Portal
   fala só HTTP. Não há demonstração de que uma reescrita seja necessária (PLANO-MESTRE,
   princípio 5). Os problemas estão em **fronteiras**: entre o WPF e o banco, entre o cliente e
   o servidor, e entre o Portal e a API.
2. **O WPF é o centro da dívida.** Ele é ao mesmo tempo cliente da API (7 serviços) e aplicação
   com acesso direto ao banco (cerca de 70 serviços locais registrados; 25 arquivos usam
   `AppDbContext` ou `IUnitOfWork`). Esse duplo caminho é a causa de EXC-SEG-001 e de dois dos
   defeitos abaixo. A migração WPF→API em andamento é o caminho certo e tem boa prática própria
   (troca por registro de DI, com linha de reversão documentada); falta ordem por risco.
3. **Três defeitos de gravidade Alta, comprovados no código:**
   - **EXC-ENG-005** — cancelar uma venda em dinheiro pelo histórico do WPF **estorna o caixa
     duas vezes**: o servidor lança o estorno (`CancelamentoVenda`) e o WPF lança uma sangria de
     estorno logo depois. Acontece desde 11/09/2026 (`ae2adde`). Cancelamentos reais ocorrem
     na Vila Verde (comentário de 17/09 em `SaleService.cs:385-397`).
   - **EXC-ENG-006** — **editar produto ou cliente pelo Portal apaga os campos que o formulário
     não envia**: NCM, CEST, CFOP, CSOSN, categoria, marca, fornecedor, produto pai, preços de
     atacado; no cliente, IE, UF, bairro e complemento. Também grava o estoque do momento em que
     o formulário abriu. O uso do Portal para isso em produção não é conhecido (lacuna).
   - **EXC-SEG-010** — o *seeder* que roda a cada abertura do WPF **promove a Administrador
     qualquer usuário cujo nome contenha "matheus" ou "administrador"** (ou login `admin` /
     `mahenrique`) e, num tenant sem cargo Administrador, **cria um usuário `admin` com hash de
     senha fixo no código**. Os tenants de 1 usuário encontrados na EXC-A0 (EXC-OPS-007) são
     compatíveis com esse comportamento (hipótese).
4. **Outros defeitos (Média):** produto fracionado (produto pai/filho) devolve o estoque ao
   produto errado no cancelamento e na devolução (EXC-ENG-007); cancelamento e devolução gravam
   em várias etapas sem transação (EXC-ENG-008); o WPF compartilha um único `AppDbContext`
   entre telas por erro de tempo de vida na injeção de dependência (EXC-ENG-009).
5. **O que está bem feito e deve ser preservado:** baixa de estoque e débito de Haver por
   `UPDATE` atômico; idempotência financeira por `SalePaymentId` com índice único; venda online
   em transação com estratégia de repetição; fila offline com o mesmo `Id` da venda; tratamento
   global de exceções na API com `correlationId`; 1337 testes aprovados.
6. **Por que os defeitos passaram pelos testes:** cada parte é testada isoladamente (o estorno do
   servidor, o estorno do cliente, o PUT do produto), mas **nenhum teste exercita o fluxo
   composto** cliente + servidor, e nenhum roda contra SQL Server (EXC-ENG-001).
7. **Plano (seção 9):** cinco ondas, sem reescrita, começando por corrigir os três defeitos Altos
   com teste de fluxo composto, e terminando com o WPF sem credencial de banco.

Registros criados: EXC-ENG-005 a EXC-ENG-013 e EXC-SEG-010. Registros atualizados: EXC-ENG-001,
EXC-ENG-003 e EXC-ENG-004.

---

## 2. Método, escopo e rotulagem

| Rótulo | Significado |
|---|---|
| **FATO-CÓDIGO** | Verificado no repositório em 2026-10-26, commit `8801d9c` (arquivo:linha ou commit) |
| **FATO-EXECUÇÃO** | Resultado de comando executado em 2026-10-26 (suíte de testes, build) |
| **HIPÓTESE** | Inferência a confirmar; a seção 10 traz a consulta que confirma ou descarta |
| **RECOMENDAÇÃO** | Proposta; só vira tarefa com decisão na triagem (PLANO-MESTRE §6) |
| **LACUNA** | Evidência necessária e indisponível |

**Examinado:** os 10 projetos da solução; registro de dependências do WPF (`App.xaml.cs`) e da API
(`Program.cs`); `AppDbContext`, `UnitOfWork` e repositórios; serviços de venda, caixa, motor
financeiro, Haver, devolução, produto e cliente; controllers de venda e produto; telas do WPF
de PDV, finalização, histórico de vendas e produtos; páginas do Portal de produtos e clientes;
*seeder*; testes; workflow de CI.

**Fluxos rastreados de ponta a ponta:** venda no PDV (online e offline), cancelamento de venda,
devolução, edição de produto e de cliente, abertura do WPF.

**Executado:** `dotnet test` em 2026-10-26, commit `8801d9c`: **1337 aprovados, 0 falhas,
0 ignorados** (1 min 31 s). O build emitiu 155 avisos (classificados em EXC-ENG-003).

**Fora do escopo:** isolamento entre tenants (EXC-A1); segredos e dependências (EXC-A2); mapa
completo de leitura-e-gravação monetária (EXC-A3); regras fiscais (EXC-A4); desempenho. Mobile
(254 linhas, pausado) e Updater (69 linhas) foram só dimensionados.

**Limites:** não houve acesso ao banco nem ao Azure; nada foi executado contra SQL Server. Onde a
gravidade depende do que existe em produção, a seção 10 traz a consulta somente leitura.

---

## 3. Diagnóstico arquitetural

### 3.1 Componentes

Linhas de código C#/XAML/Razor sem `bin`, `obj` e migrations (FATO-EXECUÇÃO, contagem em 2026-10-26).

| Projeto | Papel | Arquivos | Linhas | Depende de |
|---|---|---|---|---|
| ERP.Domain | Entidades, enums, interfaces de repositório | 88 | 2 707 | — |
| ERP.Application | Serviços de aplicação, DTOs, validadores, fiscal (Focus, recuperação) | 157 | 10 152 | Domain |
| ERP.Persistence | `AppDbContext`, configurações, *seeder*, 88 migrations | 5 | 1 420 | Domain, **Application** |
| ERP.Infrastructure | Repositórios, UoW, 41 serviços (fiscal, relatórios, marketplace, Haver…) | 60 | 10 657 | Domain, Application, Persistence |
| ERP.Api | Controllers, middlewares, *workers* fiscais, e-commerce, SignalR | 66 | 7 577 | Application, Infrastructure, Persistence |
| ERP.WPF | PDV e retaguarda desktop | 302 | 37 106 | Application, Infrastructure, **Persistence** |
| ERP.Portal | Blazor WebAssembly | 53 | 14 026 | — (só HTTP) |
| ERP.Mobile | MAUI (pausado) | 12 | 254 | — |
| ERP.Updater | Atualizador do WPF | 1 | 69 | — |
| ERP.Tests | xUnit, InMemory, SQLite, `WebApplicationFactory` | 84 | 19 518 | todos, inclusive WPF e Api |

### 3.2 Direção das dependências

```
                 ┌──────────── ERP.Portal (HTTP) ───────────┐
                 │                                          ▼
ERP.WPF ──HTTP (7 serviços)──────────────────────────► ERP.Api
   │                                                        │
   │  acesso direto (≈70 serviços, credencial de admin)     │
   ▼                                                        ▼
ERP.Infrastructure ──► ERP.Persistence ──► SQL Server ◄── (mesmo banco)
   │        │                │
   ▼        ▼                ▼
ERP.Application ──────► ERP.Domain
```

| Regra esperada | Situação | Evidência |
|---|---|---|
| Domain sem dependências | Cumprida | `ERP.Domain.csproj` |
| Application sem EF Core | Cumprida (as menções a `AppDbContext` são comentários) | busca em `ERP.Application` |
| Persistence depende só de Domain | **Desvio leve**: depende de Application para `IRequestTenant` e um *helper* | `ERP.Persistence.csproj`; `using ERP.Application.Interfaces` |
| Clientes falam com o servidor | **Desvio estrutural**: o WPF referencia Persistence e Infrastructure e abre conexão com o banco | `ERP.WPF.csproj`; `App.xaml.cs:298-303` |
| Contratos compartilhados entre clientes e API | **Ausente no Portal**: monta objetos anônimos; o WPF reaproveita os DTOs de Application | `Produtos.razor:662-679`; `Clientes.razor:260-271` |
| Estado por requisição injetado | **Desvio**: tenant e usuário também vêm de estáticos (`AppDbContext`, `ERP.Domain.CurrentUser`, `AppSession`) | §3.5 |

### 3.3 Onde está a regra de negócio

| Local | Exemplos | Observação |
|---|---|---|
| `ERP.Application/Services` (33) | `SaleService`, `CaixaService`, `MotorFinanceiroService`, `DevolucaoService` | Lugar esperado; usa `IUnitOfWork` |
| `ERP.Infrastructure/Services` (41) | `HaverService`, `ContaReceberService`, `OrcamentoService`, `FiscalService`, relatórios | **30 dos 41** usam `AppDbContext` direto, sem UoW |
| `ERP.Api` | `EcommercePedidoService` (cria venda com `AppDbContext`), lógica em controllers | `SalesController.Create` orquestra venda + financeiro |
| `ERP.WPF/ViewModels` | cancelamento de venda (`SaleViewModel`), ajuste de estoque, contas a pagar | Orquestração de negócio na tela, com `MessageBox` no meio |

Há **três** implementações que criam venda (`SaleService.CreateAsync`,
`EcommercePedidoService`, `LegacyImportService`) e elas divergem na unidade de
`SaleItem.Quantity` (o próprio código registra a divergência em `EcommercePedidoService.cs:33-34`).
Status de documentos são texto livre com grafias diferentes para o mesmo estado (`"Cancelado"` em
`SaleService.cs:423`; `"Cancelada"` em `SaleViewModel.cs:193`).

### 3.4 Estado da migração WPF→API

| Já via API (`Http*Service`) | Ainda direto no banco (amostra) |
|---|---|
| Venda (`ISaleService`), Cliente, Produto, Caixa, Contas a Pagar, Contas a Receber, emissão fiscal da venda e da devolução | Auth, Usuários, Cargos, Devolução, Orçamento, Haver, Motor Financeiro (Conta Bancária, Recebíveis), NF-e avulsa, cancelamento/contingência de NF-e, SPED, relatórios, Importação, Transferência, Fidelidade, *seeder* |

Indicadores de acoplamento no WPF (FATO-EXECUÇÃO, busca em 2026-10-26): `App.Services`
(localizador de serviços) 142 ocorrências em 48 arquivos; `AppSession.` estático 99 em 30;
`MessageBox.Show` 345 em 72; `async void` 21 em 11; `catch` vazio ou silencioso 34 em 27.

A prática usada na migração é boa e deve continuar: cada troca é uma linha de DI com comentário
de pré-requisito ("REQUER a API com … publicada ANTES") e de reversão (`App.xaml.cs:349-372`).
O que falta é **ordem por risco**: os módulos que movem dinheiro (devolução, Haver, motor
financeiro) e segurança (auth, usuários, cargos) ficaram para trás de módulos de leitura.

### 3.5 Multi-tenant e estado global (resumo; auditoria na EXC-A1)

O tenant vem de três fontes com ordens de prioridade diferentes: `AsyncLocal` estático, campo
estático global (WPF) e `IRequestTenant` (API). O filtro de consulta usa
`CurrentFilterTenantId` (AsyncLocal → global, `AppDbContext.cs:219-220`); a gravação usa
`GetTenantId()` (IRequestTenant → AsyncLocal → global, `:101-107`). `IRequestTenant` tem
*setters* públicos e é alterado fora do middleware (e-commerce). Funciona hoje, mas a correção
depende de convenções que o compilador não verifica; o histórico de correções no próprio arquivo
(`:83-100`, `:203-218`) mostra que já falhou duas vezes. Consequência concreta e menor:
`ProductService` grava o autor da mudança de preço a partir de `ERP.Domain.CurrentUser`
(estático, `ProductService.cs:140`), que só o WPF preenche (`AppSession.cs:62-63`); na API, que
edita produtos desde a Fase C, o campo fica vazio (EXC-ENG-011).

---

## 4. Fluxos rastreados

### 4.1 Venda no PDV — online e offline

```
FinalizarVendaViewModel ─► HttpSaleService.CreateAsync ─POST /api/sales─► SalesController.Create
  (Id da venda e de cada                                                     │
   pagamento gerados no PC)                                                  ├─► SaleService.CreateAsync
                                                                             │     estratégia de repetição + transação:
                                                                             │     baixa atômica de estoque, limite de crédito,
                                                                             │     débito atômico de Haver, grava Sale → COMMIT
                                                                             └─► MotorFinanceiro.ProcessarRecebimentoVendaAsync
                                                                                   (fora da transação; idempotente por SalePaymentId)
  ◄── 201 ─┘
  └─► SalvarNoCaixaEContasAReceberAsync (WPF) ─► mesmo motor, de novo, pelo cliente → sai sem gravar (já existe)
```

| Aspecto | Avaliação | Evidência |
|---|---|---|
| Atomicidade da venda | Boa: estoque, Haver e venda na mesma transação | `SaleService.cs:114-264` |
| Venda × financeiro | Consistência **eventual**: o financeiro roda depois do commit. Se falhar, a API devolve 500, o WPF trata como falta de conexão, grava na fila offline e reenvia com o mesmo `Id`; o servidor devolve a venda existente e completa o financeiro | `SalesController.cs:105-129`; `HttpSaleService` (5xx → `HttpRequestException`); `FinalizarVendaViewModel.cs:748-778`; `ConnectivityExceptionClassifier.cs:77` |
| Idempotência | Boa: venda por `Id`; financeiro por `SalePaymentId` com índice único filtrado | `SaleService.cs:65-70`; `AppDbContext.cs:640-652` |
| Orquestração duplicada | O motor financeiro roda no servidor **e** no cliente; não causa dano porque o cliente encontra os lançamentos e sai, mas é trabalho redundante que depende da idempotência | `FinalizarVendaViewModel.cs:640-644`; `MotorFinanceiroService.cs:156,180` |

**Conclusão:** o fluxo de venda é o mais sólido do sistema. A recomendação é tirar a segunda
orquestração do cliente (EXC-ENG-010), não mudar o desenho.

### 4.2 Cancelamento de venda pelo histórico do WPF — **defeito EXC-ENG-005**

```
SaleViewModel.CancelarVenda
 1. MotorFinanceiro (local) .VerificarPodeCancelarVendaAsync     — banco direto
 2. HttpSaleService.CancelAsync ─POST /api/sales/{id}/cancel─► SaleService.CancelAsync (servidor)
       a. estoque += quantidade (leitura e gravação, sem UPDATE atômico)        :345-357
       b. Haver do cliente += valor; HaverService grava MovimentoHaver e
          chama SaveChanges → grava (a) e (b) e limpa o rastreamento          :359-378
       c. para CADA pagamento não-Haver: CaixaMovimento Tipo=CancelamentoVenda,
          Valor = −pagamento.Amount                                         :398-418
       d. contas a receber pendentes → "Cancelado"; venda → Cancelada; SaveChanges  :420-439
 3. WPF: contas a receber "Pendente" → "Cancelada" (banco direto)              :186-197
 4. MotorFinanceiro (local) .EstornarVendaAsync                                 :212-213
       Dinheiro → HttpCaixaService.RegistrarMovimentoAsync(Tipo=Sangria,
                  Valor = valor − troco, "ESTORNO VENDA …")                    MotorFinanceiroService.cs:99-117
       PIX      → ContaBancária: Saída (banco direto)
       Cartão   → cancela recebíveis pendentes (banco direto)
```

**Efeito para pagamento em dinheiro (FATO-CÓDIGO):**

| Lançamento | Origem | Efeito no saldo em dinheiro (`GetSaldoDinheiroAsync`) | Efeito no resumo do caixa |
|---|---|---|---|
| Venda original: `+(valor − troco)` | servidor, na venda | soma | `VendasDinheiro +=` |
| `CancelamentoVenda`: `−valor` | servidor, passo 2c | soma valor negativo → **reduz** | `VendasDinheiro +=` (negativo) |
| `Sangria` "ESTORNO VENDA": `valor − troco` | WPF, passo 4 | Sangria → **reduz de novo** | `VendasDinheiro −=` |

Fontes: `CaixaRepository.cs:106-116`; `CaixaService.cs:235-275`. Resultado: o caixa termina com
o valor da venda **a menos** (mais o troco, que o estorno do servidor também não desconta:
`SaleService.cs:409` usa `pagamento.Amount`, enquanto o lançamento original foi `valor − troco`,
`MotorFinanceiroService.cs:158`). Além disso, a sangria do passo 4 passa pela política de
sangria (saldo e limite do cargo, `CaixaService.cs:113-119`): se falhar, o operador vê "Erro ao
tentar cancelar a venda" **depois** de a venda já ter sido cancelada no servidor.

**Linha do tempo (FATO-CÓDIGO, `git blame`):** o estorno pelo cliente existe desde 21/07/2026
(`16daf9a`); o estorno no servidor foi acrescentado em 11/09/2026 (`ae2adde`) sem remover o do
cliente. Para PIX e cartão o estorno do servidor fica no caixa e o do cliente em outra conta
(conta bancária, recebíveis), então não há dupla contagem no mesmo saldo.

**Por que os testes não pegaram:** `MotorFinanceiroServiceTests` testa o estorno do cliente com
dublês; `SaleServiceTests` testa o cancelamento do servidor; nenhum teste executa os dois em
sequência sobre o mesmo caixa.

### 4.3 Devolução — **defeitos EXC-ENG-007 e EXC-ENG-008**

`DevolucaoService.DevolverItensAsync` (WPF, banco direto):
1. devolve o estoque por `UPDATE` atômico, **em autocommit, fora de transação** (`:57-62`);
2. registra as devoluções e chama `HaverService.RegistrarMovimentoVendaAsync`, que faz
   `SaveChanges` (`RelatorioServices.cs:420-432`) — primeira gravação;
3. lê o cliente e soma o Haver em memória (leitura e gravação, já em EXC-DAD-002);
4. `CommitAsync` — segunda gravação (`:114`).

Uma falha entre 1 e 2 deixa o estoque devolvido sem registro de devolução; como a trava contra
devolução repetida conta os registros de devolução (`:42-55`), **uma nova tentativa devolve o
estoque outra vez**. O cancelamento tem o mesmo padrão (§4.2, passos a–d em duas gravações).

O item devolvido volta ao estoque de `item.ProductId` (`:60`). Para produto filho (produto
fracionado, `ParentProductId`), a venda baixou o estoque **do pai** e gravou a quantidade
**na unidade do pai** (`SaleService.cs:144-159,204-214`); a devolução e o cancelamento
(`SaleService.cs:345-357`) devolvem essa quantidade **ao filho**. Exemplo: venda de 2 barras de
6 m de um rolo de 100 m baixa 12 do rolo; o cancelamento soma 12 às barras. A NFC-e da mesma
venda sai com quantidade 12 e descrição da barra (`FiscalService.cs:791-805`).

### 4.4 Edição de produto e de cliente — **defeito EXC-ENG-006**

```
Portal Produtos.razor ─PUT /api/Products/{id} { 14 campos }─► ProductsController.Update
                                                               └─► ProductService.UpdateAsync
                                                                     _mapper.Map(dto, product)   ← copia TODOS os 40 campos do DTO
```

`UpdateProductDto` herda os 40 campos de `CreateProductDto` (`Dtos.cs:58-104`); o mapeamento
`UpdateProductDto → Product` não tem condição para ignorar campos ausentes
(`MappingProfile.cs:44-48`); o Portal envia 14 (`Produtos.razor:662-679`). Os campos não
enviados chegam com o valor padrão e **sobrescrevem** o cadastro: `NCM`, `CEST`, `CFOPPadrao`,
`CSOSN`, `CategoryId`, `BrandId`, `SupplierId`, `IpiPercent`, `IcmsPercent`,
`WholesalePrice`, `ParentProductId`, `ConversionFactor`, `UnidadeVenda`, `AllowNegativeStock`,
`DescricaoDetalhada`, `EmCampanha` e outros. Sem NCM e CSOSN, a NFC-e sai com os valores de
reserva `00000000` e `102` (`FiscalService.cs:804-805`). Com cliente é igual: o Portal envia 10
de 14 campos (`Clientes.razor:260-271`) e `CustomerService.cs:107` apaga **IE, UF, bairro e
complemento**, necessários na NF-e.

O estoque é sobrescrito com o valor do formulário em ambos os clientes. O WPF reduz a janela
relendo o estoque antes de salvar quando o usuário não o alterou (`ProductViewModel.cs:361-366`);
o Portal não faz isso.

**Causa arquitetural:** o Portal não compartilha contratos com a API (objetos anônimos) e o PUT
é tratado como substituição total. Não há teste de contrato entre Portal e API.

### 4.5 Abertura do WPF — **defeito EXC-SEG-010** e tempo de vida (EXC-ENG-009)

`App.OnStartup` (`App.xaml.cs:80-230`): lê `conexao.dat` → fixa o tenant global pelo
`licenca.json` → monta a DI → **executa `DbSeeder.Seed` contra o banco de produção** (`:170-174`)
→ verifica atualização → login.

O *seeder* (`DbSeeder.cs`, tenant = `GetGlobalTenantId()`, `:13`):
- se o tenant não tem cargos e permissões, cria (`:16-108`);
- se existe o cargo Administrador, **troca para Administrador todo usuário do tenant** cujo login
  seja `admin` ou `mahenrique`, ou cujo nome contenha `matheus` ou `administrador`
  (`:157-180`). Isso roda a cada abertura de cada PC e desfaz qualquer rebaixamento manual;
- se não existe, **cria o cargo e um usuário `admin` com hash bcrypt fixo no código-fonte**, sem
  `MustChangePassword` (`:181-198`).

Qualquer PC com um CNPJ novo no `licenca.json` (inclusive o padrão `00000000000000`) cria, ao
abrir, um tenant com cargos, permissões e esse administrador no banco de produção.

**Tempo de vida:** `PdvViewModel` é *singleton* (`App.xaml.cs:537`) e recebe serviços *Scoped*
locais (`IOrcamentoService`, `IVendaSuspensaService`, `IMotorFiscalService`,
`IProdutoAgregadoService`); o `MainWindow` resolve `IUnitOfWork` e as ViewModels pela raiz
(`MainWindow.xaml.cs:71-75,448`). Resolvidos pela raiz, todos recebem **o mesmo**
`AppDbContext`, que vive até o programa fechar. O erro "A second operation was started on this
context" já ocorreu (comentário de 15/09 em `PdvViewModel.cs:331-339`); o semáforo criado só
protege as tarefas de inicialização do PDV, não as outras telas que usam o mesmo contexto.

---

## 5. Avaliação por tema

| Tema | Situação | Pontos fortes | Problemas | Registros |
|---|---|---|---|---|
| Separação de camadas | Parcial | Domain e Application limpos; API como raiz de composição | WPF com banco direto; 30 serviços de Infrastructure com `AppDbContext` direto; Persistence → Application | EXC-ENG-010, EXC-ENG-012, EXC-SEG-001 |
| Padrões de implementação | Inconsistentes | Repository/UoW, DTOs, validadores, troca de DI documentada | Três caminhos de criação de venda; status em texto livre; localizador de serviços no WPF; dois estilos de erro (exceções e `FluentResults`, este em 3 arquivos) | EXC-ENG-012 |
| Tratamento de erros | Bom na API, fraco no WPF | Handler global com `correlationId`; regra de negócio → 400 por convenção (`ErroNegocioVendaException`) | WPF: 34 `catch` silenciosos, 21 `async void`, 2 chamadas assíncronas não aguardadas (CS4014, `FinalizarVendaViewModel.cs:137`); `throw new Exception("Ei! …")` (`SaleService.cs:371`) | EXC-ENG-003 |
| Concorrência | Boa nos pontos críticos de venda | `UPDATE` atômico de estoque e de Haver; índices únicos de idempotência; índice único de caixa aberto | Sem `RowVersion`; estoque por leitura e gravação no cancelamento, compra e importação de NF-e; contexto compartilhado no WPF | EXC-DAD-002, EXC-ENG-009 |
| Transações | Boa na venda, ausente no resto | `ExecuteInTransactionAsync` + transação explícita na venda | Cancelamento e devolução em várias gravações; `HaverService` grava por conta própria no meio de operações maiores; `ChangeTracker.Clear()` após todo `SaveChanges` | EXC-ENG-008, EXC-ENG-004 |
| Consistência dos dados | Em risco em fluxos de reversão | Idempotência por `SalePaymentId` | Estorno duplicado; campos apagados pelo Portal; estoque no produto errado | EXC-ENG-005, -006, -007 |
| Testabilidade | Boa no servidor, difícil no WPF | 1337 testes; integração com `WebApplicationFactory` (182 em `ControllersIntegrationTests`); `Http*Service` testados com *handler* falso; métodos extraídos para teste | Zero testes contra SQL Server; nenhum teste de fluxo composto cliente+servidor; ViewModels com `MessageBox` e `App.Services` | EXC-ENG-001 |
| Complexidade | Concentrada | — | `PdvViewModel` 1830 linhas; `PDV.razor` 1794; `NotaAvulsaViewModel` 1187; `NotaFiscalAvulsaService` 941; `FiscalService` 932; `AppDbContext` 821 | §7, item D9 |
| Manutenção | Dependente de uma pessoa | Comentários explicam o porquê das correções | 262 marcadores de histórico em comentários (`S17 FIX`, `Achado (dd/mm)`), que envelhecem; código órfão mantido de propósito (`NfeContingencyWorker`, `WpfEstoqueSyncService`, `FiscalService` no WPF); README desatualizado | EXC-ENG-002, EXC-ENG-013 |

---

## 6. Defeitos comprovados × oportunidades de refatoração

### 6.1 Defeitos (comportamento errado verificável)

| Registro | Defeito | Gravidade | Confiança | Ocorrência em produção |
|---|---|---|---|---|
| EXC-ENG-005 | Cancelar venda em dinheiro pelo WPF estorna o caixa duas vezes | **Alta** | Alta (código) | Provável; confirmar com a consulta C1 |
| EXC-ENG-006 | PUT de produto/cliente pelo Portal apaga campos e sobrescreve estoque | **Alta** | Alta (código) | Lacuna: depende do uso do Portal; consulta C2 |
| EXC-SEG-010 | *Seeder* promove usuários a Administrador pelo nome e cria admin com senha fixa | **Alta** | Alta (código) | Hipótese para os tenants de 1 usuário; consulta C4 |
| EXC-ENG-007 | Produto filho: estoque devolvido ao produto errado; NFC-e com quantidade do pai | Média | Alta (código) | Lacuna: depende de haver produto filho vendido; consulta C3 |
| EXC-ENG-008 | Cancelamento e devolução sem transação; nova tentativa repete estoque e Haver | Média | Alta (código) | Exige falha no meio da operação |
| EXC-ENG-009 | `AppDbContext` único compartilhado entre telas do WPF | Média | Alta (código); Baixa (frequência) | Já ocorreu na inicialização do PDV (comentário de 15/09) |
| EXC-ENG-011 (parte) | Autor da mudança de preço vazio quando a edição vem da API | Baixa | Alta (código) | Toda edição de preço desde a Fase C |

### 6.2 Oportunidades de refatoração (sem defeito associado)

| Registro | Oportunidade | Benefício | Risco de não fazer |
|---|---|---|---|
| EXC-ENG-010 | Concluir a migração WPF→API por ordem de risco | Elimina a credencial de banco nos PCs (EXC-SEG-001) e a regra dividida entre cliente e servidor | Novos defeitos do tipo EXC-ENG-005 a cada módulo migrado pela metade |
| EXC-ENG-011 | Fonte única e imutável de tenant/usuário por requisição | Menos convenções frágeis no isolamento | Regressão de isolamento (já houve duas) |
| EXC-ENG-012 | Um serviço por operação de negócio; status como enum; UoW em vez de `AppDbContext` direto | Uma regra em um lugar | Divergências como a de `SaleItem.Quantity` |
| EXC-ENG-013 | URL da API em configuração; limpeza de arquivos e pacotes órfãos | WPF apontável para staging sem recompilar | Baixo |
| EXC-ENG-004 | Decidir sobre `ChangeTracker.Clear()` com o mapa da §4 | Previsibilidade de gravações | Gravações perdidas em código novo |
| EXC-ENG-003 | Tratar CS4014, CS0618 e NU1701; depois a anulabilidade | Avisos com risco deixam de ficar escondidos | Baixo |

---

## 7. Dívida técnica priorizada

Ordem pela CRITERIOS §5 (integridade de dados → isolamento → segurança → fiscal → fluxos críticos).
Esforço relativo (P ≤ 1 dia; M ≤ 1 semana; G > 1 semana) é **estimativa do auditor**, não medida.

| # | Item | Registro | Por que nesta posição | Esforço | Risco da mudança |
|---|---|---|---|---|---|
| D1 | Remover o estorno duplicado do cancelamento e corrigir o troco | EXC-ENG-005 | Dinheiro, fluxo crítico, ocorre hoje | P–M | Médio (versões do WPF em campo; §8) |
| D2 | Fechar a edição parcial do Portal (contrato próprio, estoque fora do PUT) | EXC-ENG-006 | Cadastro fiscal apagado sem aviso | M | Médio (WPF usa o mesmo PUT para estoque) |
| D3 | Tirar do *seeder* a promoção por nome e o admin com senha fixa; não rodar o *seeder* no WPF | EXC-SEG-010 | Escalada de privilégio a cada abertura | P | Baixo (definir antes o procedimento de recuperação de acesso) |
| D4 | Cancelamento e devolução em uma transação; `HaverService` sem `SaveChanges` próprio | EXC-ENG-008, EXC-DAD-002 | Integridade de estoque e Haver | M | Médio (estratégia de repetição reexecuta o bloco; exige SQL Server nos testes) |
| D5 | Produto filho: unidade de `SaleItem.Quantity` e estoque de origem gravados no item | EXC-ENG-007 | Estoque e fiscal; depende de uso (C3) | M–G | Alto (dados históricos, devolução fiscal referencia a nota original) |
| D6 | Testes de fluxo composto e testes contra SQL Server para venda, cancelamento e devolução | EXC-ENG-001 | Pré-requisito de D1, D4, D5 | M | Baixo |
| D7 | Escopo de DI por tela no WPF; `ValidateScopes` em desenvolvimento | EXC-ENG-009 | Erros intermitentes no PDV | M | Médio (o carrinho do PDV deve continuar *singleton*) |
| D8 | Migração WPF→API por risco: Haver/devolução/motor financeiro → auth/usuários/cargos → fiscal restante → relatórios | EXC-ENG-010 | Leva ao fim de EXC-SEG-001 | G | Médio por módulo (o padrão de troca com reversão já existe) |
| D9 | Quebrar `PdvViewModel` e `PDV.razor` em componentes; retirar `MessageBox` da lógica | EXC-ENG-012 | Manutenção | G | Médio (PDV é o fluxo mais usado) |
| D10 | Tenant/usuário: fonte única imutável; remover `ERP.Domain.CurrentUser` | EXC-ENG-011 | Isolamento; só depois da EXC-A1 | M | **Alto** (toca o filtro de tenant) |
| D11 | URL da API configurável; remover `ERP.WPF_1qyhnkib_wpftmp.csproj`, pasta `{ERP.Domain…` e Dapper sem uso; `UnitOfWork.Dispose` sem descartar o contexto da DI | EXC-ENG-013 | Higiene | P | Baixo |
| D12 | Avisos: CS4014 e CS0618 já; NU1701 (LiveCharts rc, OpenTK) na próxima atualização de gráficos; anulabilidade aos poucos | EXC-ENG-003 | Baixo impacto | P–M | Baixo |
| D13 | Descrição de arquitetura e registro de decisões (ADR) a partir da §3 | EXC-ENG-002 | Bus factor (EXC-ADM-002) | P | Nenhum |

---

## 8. Riscos de cada mudança

| Mudança | O que pode quebrar | Como reduzir o risco |
|---|---|---|
| **D1** — o servidor passa a ser o único dono do estorno | Os 3 PCs da Vila Verde continuam com a versão que faz o estorno pelo cliente até serem atualizados. Se o WPF for atualizado antes da API, PIX e cartão deixam de ser estornados até a API chegar | **Ordem:** 1) publicar a API com o estorno completo no servidor (PIX e cartão idempotentes por `SalePaymentId`, troco corrigido usando o valor do lançamento original); 2) atualizar os 3 PCs com o WPF sem o passo 4. Durante a janela o dinheiro continua duplicado como hoje, sem piora. Teste de integração: abrir caixa → vender em dinheiro com troco → cancelar → saldo igual ao da abertura. Correção dos caixas já afetados: decisão do responsável com base na consulta C1 |
| **D2** — contrato próprio para o Portal e estoque fora do PUT | O WPF ajusta estoque pela tela de produto (`_stockEditado`); tirar `Stock` do PUT muda esse fluxo | Etapa 1: o Portal passa a ler o produto e reenviar o objeto completo (correção mínima, só no Portal). Etapa 2: endpoint de edição com só os campos editáveis e estoque apenas por ajuste/movimento, com o WPF migrado junto. Teste: PUT com corpo parcial não altera NCM |
| **D3** — *seeder* | Perder o "acesso de emergência" que a promoção por nome garante hoje | Escrever antes o procedimento de recuperação (script com senha aleatória e `MustChangePassword`); rodar a consulta C4 para saber quem depende da promoção |
| **D4** — transação no cancelamento e na devolução | A estratégia de repetição do EF reexecuta o bloco inteiro; com `ChangeTracker.Clear()` e `SaveChanges` internos, um bloco que não seja idempotente pode duplicar | Mesmo padrão da venda (`SaleService.cs:114-118`): tudo dentro do bloco, `HaverService` recebendo a unidade de trabalho; testes contra SQL Server (D6) antes do merge |
| **D5** — produto filho | Vendas antigas estão na unidade do pai; uma regra nova aplicada a elas devolveria o estoque errado ao contrário | Colunas novas anuláveis no item (produto de estoque e fator); linhas antigas tratadas pela regra antiga. Se a consulta C3 mostrar que ninguém usa produto filho, alternativa mais barata: bloquear cancelamento/devolução automática desses itens até a correção |
| **D7** — escopo por tela | Estado do PDV (carrinho, vendas suspensas) mantido pelo *singleton* | Manter a ViewModel *singleton* e trocar só a obtenção dos serviços (fábrica de escopo por operação) |
| **D8** — migração | Cada módulo migrado pela metade gera regra dividida (foi o caso de EXC-ENG-005) | Critério de pronto por módulo: toda escrita do módulo no servidor; nenhuma orquestração no cliente; teste de fluxo composto |
| **D10** — tenant | Regressão de isolamento | Só depois da EXC-A1, com testes contra SQL Server de dois tenants (critério SEG-2) |

Enquanto EXC-OPS-002 estiver aberto (deploy automático da `main`), toda mudança acima precisa
ser validada antes do merge (PLANO-MESTRE §6.3).

---

## 9. Plano de evolução incremental

Ondas em sequência (capacidade de uma pessoa, EXC-ADM-002). Nenhuma onda reescreve camada.
Nomes em "onda" para não colidir com as etapas E1–E6 do login restrito.

| Onda | Objetivo | Itens | Pré-condição | Critério de saída |
|---|---|---|---|---|
| **O1 — Estancar** | Parar a geração de dados errados | C1–C4 (seção 10); D1; D3; D2 etapa 1 (ou retirar a edição do Portal até corrigir) | Triagem dos 3 registros Altos | Consultas executadas e registradas; teste de cancelamento com saldo de caixa correto; *seeder* sem promoção por nome; PUT parcial não apaga campos |
| **O2 — Provar** | Testes que pegariam os defeitos desta auditoria | D6 (SQL Server em contêiner ou banco de teste dedicado no CI); testes de fluxo composto de venda, cancelamento e devolução | Ambiente de teste fora do banco de produção (EXC-OPS-007) | CRITERIOS ENG-3 atendido para os fluxos críticos 2, 3, 5 e 6 |
| **O3 — Atomizar** | Reversões atômicas | D4; D5 conforme C3; decisão de EXC-ENG-004 | O2 | Cancelamento e devolução em uma transação, com teste de falha no meio |
| **O4 — Migrar** | WPF sem banco direto | D8 na ordem Haver/devolução/motor financeiro → auth/usuários/cargos → fiscal restante → relatórios; D7 no caminho | O3 para os módulos de dinheiro | WPF sem `conexao.dat`; EXC-SEG-001 encerrado (CRITERIOS ENG-1) |
| **O5 — Simplificar** | Manutenção | D9, D10 (após EXC-A1), D11, D12, D13 | O4 em andamento | ENG-2, ENG-4 e ENG-5 atendidos |

---

## 10. Consultas de confirmação (somente leitura)

Para rodar no `ERPTTSoftStaging` pelo responsável; resultados vão para os registros. Enums
gravados como inteiros: `TipoMovimentoCaixa` Sangria = 4, CancelamentoVenda = 8;
`PaymentMethod` Dinheiro = 0.

**C1 — estornos duplicados (EXC-ENG-005).** A sangria do WPF não grava `VendaId` (o
`HttpCaixaService` descarta o campo), por isso a ligação é pela descrição.

```sql
SELECT s.TenantId, s.SaleNumber, s.SaleDate,
       cv.Valor AS EstornoServidor, sg.Valor AS SangriaCliente, sg.DataHora, cv.CaixaId
FROM Sales s
JOIN CaixaMovimentos cv ON cv.VendaId = s.Id AND cv.Tipo = 8 AND cv.FormaPagamento = 0
JOIN CaixaMovimentos sg ON sg.Tipo = 4 AND sg.Descricao = CONCAT('ESTORNO VENDA ', s.SaleNumber)
ORDER BY s.SaleDate;
```

**C2 — campos apagados por edição (EXC-ENG-006).**

```sql
SELECT TenantId, EntityType, EntityId, Timestamp, UserName, MachineName
FROM AuditLogs
WHERE Action = 'UPDATE'
  AND ((EntityType = 'Product'  AND JSON_VALUE(OldValues, '$.NCM') IS NOT NULL
                                AND JSON_VALUE(NewValues, '$.NCM') IS NULL)
    OR (EntityType = 'Customer' AND JSON_VALUE(OldValues, '$.State') IS NOT NULL
                                AND JSON_VALUE(NewValues, '$.State') IS NULL))
ORDER BY Timestamp;
```

**C3 — produto filho em uso (EXC-ENG-007).**

```sql
SELECT f.TenantId, COUNT(DISTINCT f.Id) AS ProdutosFilho, COUNT(si.Id) AS ItensVendidos
FROM Products f
LEFT JOIN SaleItems si ON si.ProductId = f.Id
WHERE f.ParentProductId IS NOT NULL AND f.ConversionFactor > 0
GROUP BY f.TenantId;
```

**C4 — usuários alcançados pelo *seeder* (EXC-SEG-010).**

```sql
SELECT u.TenantId, u.Username, u.Name, r.Name AS Cargo, u.IsActive, u.MustChangePassword
FROM Users u LEFT JOIN Roles r ON r.Id = u.RoleId
WHERE LOWER(u.Username) IN ('admin', 'mahenrique')
   OR LOWER(u.Name) LIKE '%matheus%' OR LOWER(u.Name) LIKE '%administrador%'
ORDER BY u.TenantId;
```

---

## 11. Critérios de excelência ENG (CRITERIOS §9.4)

| Critério | Situação em 2026-10-26 | Registros |
|---|---|---|
| ENG-1 Todo acesso a dados de clientes passa por servidor autenticado | **Não atendido** — WPF com banco direto | EXC-SEG-001, EXC-ENG-010 |
| ENG-2 Build sem avisos de segurança; restantes justificados | **Não atendido** — NU1903 ativo; demais classificados | EXC-SEG-002, EXC-ENG-003 |
| ENG-3 Testes cobrem fluxos críticos, com SQL Server | **Não atendido** — sem SQL Server e sem fluxo composto | EXC-ENG-001 |
| ENG-4 Documentação de arquitetura atual | **Parcial** — §3 deste relatório serve de base; README desatualizado | EXC-ENG-002 |
| ENG-5 Decisões arquiteturais registradas | **Não atendido** — decisões vivem em comentários de código | EXC-ENG-002 |

---

## 12. Lacunas e próximos passos

| Lacuna | Quem fornece | Desbloqueia |
|---|---|---|
| Resultado das consultas C1–C4 | Matheus (banco) | Gravidade final de EXC-ENG-005, -006, -007 e EXC-SEG-010 |
| Se o Portal é usado para editar produtos e clientes na Vila Verde | Matheus | Urgência de EXC-ENG-006 |
| Logs do WPF dos 3 PCs (pasta `Logs/`) com "second operation" | Matheus | Frequência de EXC-ENG-009 |

Próximos passos: triagem dos três registros Altos; execução das consultas; a EXC-A3 assume o mapa
completo de leitura-e-gravação e de gravações em várias etapas a partir de EXC-ENG-008.

---

## Histórico do documento

| Data | Versão | Alteração |
|---|---|---|
| 2026-10-26 | 1.0 | Primeira passada da EXC-A6 sobre o commit `8801d9c` |
