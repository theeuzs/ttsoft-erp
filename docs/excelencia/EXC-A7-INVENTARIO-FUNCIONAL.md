# EXC-A7 — Inventário funcional do produto

> Programa de Excelência TTSoft · Área PRD (com achados de FIS, OPS e ENG)
> Criado em: 2026-10-26 · Versão: 1.0 · Base de código: commit `47a4b0a` (main)
> Documentos relacionados: [PLANO-MESTRE](PLANO-MESTRE.md) · [CRITERIOS-DE-EXCELENCIA](CRITERIOS-DE-EXCELENCIA.md) · [REGISTRO-CENTRAL](REGISTRO-CENTRAL.md) · [EXC-A6](EXC-A6-ARQUITETURA-ENGENHARIA.md) · [EXC-A10-VENDAS](EXC-A10-VENDAS.md) · [EXC-A10-MARKETING](EXC-A10-MARKETING.md)

Esta é a primeira passada da EXC-A7, limitada ao **inventário funcional feito no código**. A
EXC-A7 completa depende de observar usuários reais da loja piloto (CRITERIOS PRD-1), o que não
foi feito aqui. Por isso a coluna "Operação" da matriz é, na maior parte, **Desconhecida**: o
código mostra o que existe, não o que a loja usa. A seção 11 traz a consulta somente leitura que
preenche essa coluna. Nenhuma linha de código foi alterada.

---

## 1. Resumo

1. **O ConstruTTor é largo.** São 13 módulos e 128 funcionalidades inventariadas,
   distribuídas entre o WPF (43 entradas de menu), o Portal (36 páginas) e a API (37
   controllers). A cobertura de um ERP de varejo está quase toda presente no código: cadastro,
   compras por XML, estoque, PDV com pagamento dividido, fiado com limite, caixa, contas a
   receber e a pagar, conciliação, NFC-e, NF-e, NFS-e, relatórios, permissões e integrações.
2. **A profundidade é desigual.** O núcleo (venda no PDV, financeiro da venda, NFC-e) tem
   testes de unidade e uso diário comprovado. Fora dele, a maioria das funcionalidades tem
   **implementação sem teste de fluxo e sem evidência de uso**: 29 dos 48 serviços examinados
   têm no máximo um teste de fumaça (autorização e listagem) ou nenhum.
3. **Só seis funcionalidades estão validadas em operação com evidência:** venda no PDV
   (vendas diárias na Vila Verde, confirmação 4.3), emissão de NFC-e (EXC-FIS-009),
   cancelamento de venda (relato em `SaleService.cs:385-397`), login de usuários nos 3 PCs,
   licenciamento (o PDV só abre com licença válida) e cadastro de filiais (consultado na EXC-A0).
   Abertura e fechamento de caixa e cadastro de produtos são consequência necessária da venda
   diária (rótulo "Inferida"). Todo o resto é desconhecido.
4. **Quatro funcionalidades parecem completas na interface, mas não fazem o que o nome
   promete:**
   - **Multi-filial** existe só como cadastro: venda, caixa, usuário, nota fiscal e saldo de
     estoque não têm filial; a transferência move um saldo por filial que nenhuma outra tela
     usa (EXC-PRD-002).
   - **Importar XML de compra** não impede importar a mesma nota duas vezes: estoque e contas a
     pagar duplicam (EXC-PRD-003).
   - **Backup automático** do WPF usa `BACKUP DATABASE ... TO DISK`, que o Azure SQL Database
     não aceita, e roda em modo silencioso (EXC-OPS-008).
   - **Fiscal** atende só o Simples Nacional (CSOSN); uma loja de Lucro Presumido ou Real não
     pode ser implantada (EXC-FIS-014).
5. **Regras de negócio que uma loja espera e não encontra:** fechamento de caixa sem contagem
   nem diferença (EXC-PRD-005); devolução sem cliente identificado não registra o reembolso,
   e devolução nunca sai em dinheiro (EXC-PRD-006); venda a prazo gera uma conta única com
   vencimento fixo em 30 dias, sem parcelas, juros ou multa (EXC-PRD-008); custo pelo último
   preço de compra, sem custo médio nem histórico de movimentação de estoque (EXC-PRD-004);
   fornecedor só com nome (EXC-PRD-007).
6. **Ausências típicas de loja de materiais de construção:** etiquetas de preço, TEF,
   retirada posterior com reserva de estoque, lote e validade, inutilização de numeração
   fiscal, kits. Nenhuma delas impede a operação atual; algumas aparecem em quase toda
   negociação (TEF e etiquetas, ver EXC-A10-VENDAS O2).
7. **Para vender a lojas independentes, o bloqueio não é de funcionalidade.** Os bloqueadores
   são correção (EXC-ENG-005, EXC-ENG-006, EXC-FIS-013), segurança (EXC-SEG-001), contrato
   (EXC-FIS-002) e implantação (EXC-VEN-003). Do lado funcional, os bloqueadores são poucos e
   pequenos (seção 8.1). A recomendação é **parar de alargar o produto** e aprofundar o núcleo
   (seção 10).

Registros criados: EXC-PRD-002 a EXC-PRD-011, EXC-FIS-014 e EXC-OPS-008.

---

## 2. Método, escopo e rotulagem

### 2.1 As cinco condições, mantidas separadas

Cada funcionalidade recebe três classificações independentes. Uma não implica a outra: há
funcionalidade implementada sem teste, testada sem uso e usada sem teste.

| Coluna | Valores | Significado |
|---|---|---|
| **Implementação** | Ausente / Parcial / Implementada | *Implementada*: caminho completo da tela à gravação, sem lacuna conhecida no próprio fluxo. *Parcial*: existe, mas falta parte do fluxo ou há defeito registrado que o compromete. *Ausente*: não encontrada no código (busca registrada). |
| **Teste** | Nenhum / Fumaça / Unidade / Fluxo | *Fumaça*: só autorização, listagem ou "não retorna 500". *Unidade*: regras do serviço testadas isoladamente (InMemory, SQLite ou *mock*). *Fluxo*: o caminho composto é exercido (cliente + servidor, ou venda → financeiro → caixa). Nenhum teste roda contra SQL Server (EXC-ENG-001). |
| **Operação** | Validada / Inferida / Indício / Declarada / Desconhecida | *Validada*: uso real comprovado por consulta ao banco ou registro do programa. *Inferida*: consequência necessária de algo validado. *Indício*: correção de defeito de campo no histórico, sem consulta. *Declarada*: informada pelo responsável. *Desconhecida*: sem evidência. |

"Testada **e** validada em operação" exige as duas colunas preenchidas com evidência própria.

### 2.2 Rótulos de evidência

Os mesmos da EXC-A6: **FATO-CÓDIGO** (repositório em 2026-10-26, commit `47a4b0a`),
**FATO-EXECUÇÃO** (comando executado em 2026-10-26), **HIPÓTESE**, **RECOMENDAÇÃO**, **LACUNA**.

### 2.3 Examinado

Entidades do domínio (`ERP.Domain/Entities`), enums, serviços de Application e Infrastructure,
controllers da API, menu e navegação do WPF (`MainWindow.xaml`, `MainWindow.xaml.cs:286-411`),
páginas e menu do Portal (`ERP.Portal/Pages`, `Layout/MainLayout.razor`), permissões do
*seeder* (`DbSeeder.cs:69-104, 219-250`), os 84 arquivos de teste (busca por serviço), e os
registros do programa (para não duplicar).

**Fora do escopo:** usabilidade e mensagens de erro (PRD-3, depende de observação); regras
tributárias em detalhe (EXC-A4); isolamento (EXC-A1); integridade transacional (EXC-A3);
Mobile (pausado, EXC-PRD-001).

**Executado:** `dotnet test` em 2026-10-26, commit `47a4b0a`: **1337 aprovados, 0 falhas,
0 ignorados** (1 min 32 s). FATO-EXECUÇÃO. A suíte verde não muda nenhuma linha da matriz:
ela confirma os testes que existem, não os que faltam.

**Limites:** sem acesso ao banco; nenhuma tela foi executada. "Implementada" significa que o
código do fluxo existe e foi lido, não que a tela foi operada.

---

## 3. Mapa do produto: onde cada módulo vive

| Módulo | WPF (loja) | Portal (navegador) | API | Observação |
|---|---|---|---|---|
| Cadastro (produtos, clientes) | Sim | Sim | Sim | Edição pelo Portal apaga campos (EXC-ENG-006) |
| Compras e fornecedores | Sim | Sim (compras, sugestão) | Sim | Importação de XML só no WPF |
| Estoque | Sim (ajuste, inventário, ABC) | Sim (estoque, inventário, transferências) | Sim | Transferência só no Portal |
| Vendas fora do PDV | Orçamentos, histórico, gerenciador | Orçamentos, CRM, entregas, comissões, metas, tintométrico, calculadora | Sim | Entregas, tintométrico, metas e CRM **só no Portal** |
| PDV | Sim (principal, com fila offline) | Sim (`PDV.razor`, 1 793 linhas, sem offline) | Sim | Dois PDVs com regras diferentes |
| Caixa | Sim | Sim | Sim | |
| Financeiro | Sim (completo) | Sim (receber, pagar, haver, conciliação) | Sim | DRE e fluxo de caixa só no WPF |
| Fiscal | Sim (NFC-e, NF-e, avulsa, MD-e, NFS-e, SPED, XML) | Notas fiscais, SPED | Sim | |
| Relatórios | Sim | Sim (relatórios, BI, painel de TV) | Sim | |
| Usuários e permissões | Sim | Cargos, perfil, senha | Sim | |
| Filiais | Cadastro | Transferências | Sim | Ver EXC-PRD-002 |
| Integrações | Mercado Livre, SKU, chat | Catálogo público, autoatendimento, motorista | E-commerce, *webhooks* | |
| Implantação | Primeiro uso, licença, importações | Cadastro público | Cadastro | Ver EXC-VEN-002, EXC-VEN-003 |

FATO-CÓDIGO: `ERP.WPF/MainWindow.xaml` (43 entradas com `Tag`), `MainWindow.xaml.cs:361-411`;
`ERP.Portal/Layout/MainLayout.razor` (36 rotas); `ERP.Api/Controllers` (37 controllers).

Não há documento que defina o papel de cada aplicativo (CRITERIOS PRD-4). Na prática, o WPF é
a loja e o Portal é uma segunda retaguarda que repete parte do WPF e acrescenta módulos que o
WPF não tem. Funções repetidas nos dois têm regras diferentes, e foi por isso que a edição pelo
Portal apaga campos (EXC-ENG-006). Registro: EXC-PRD-009.

---

## 4. Matriz funcional

Legenda das colunas: ver §2.1. "App": W = WPF, P = Portal. Evidência sem arquivo indicado está
na seção correspondente do texto ou no registro citado.

### 4.1 Cadastro

| Funcionalidade | App | Implementação | Teste | Operação | Evidência / observação |
|---|---|---|---|---|---|
| Produto: código de barras, SKU, categoria, marca, unidade, preço, custo | W, P | Implementada (P: Parcial, EXC-ENG-006) | Unidade | Inferida | `Product.cs:10-30`; `ProductServiceTests` |
| Produto: dados fiscais (NCM, CEST, CSOSN, CFOP, origem, MVA) | W | Implementada | Unidade (via fiscal) | Inferida (NFC-e emitidas) | `Product.cs:65-79` |
| Preço por grupo de cliente (A, B, C) e atacado por quantidade | W, P | Implementada | Unidade | Desconhecida | `Product.cs:34-44, 80-81`; `SaleServiceAtacadoTests` |
| Unidade de venda diferente da de estoque (fator de conversão) | W | Implementada | Nenhum | Desconhecida | `Product.cs:85-94`; nenhum teste cita `FatorConversao` |
| Produto filho (fracionado: metro, quilo de um rolo ou saco) | W | Parcial | Nenhum | Desconhecida | EXC-ENG-007 |
| Produtos complementares (sugestão no PDV) | W | Implementada | Nenhum | Desconhecida | `ProdutoAgregado.cs` |
| Custo com IPI e ICMS, margem desejada e real | W | Implementada | Nenhum | Desconhecida | `Product.cs:49-56`; custo = última entrada (EXC-PRD-004) |
| Histórico de alteração de preço (quem, quando) | W, API | Parcial | Nenhum | Desconhecida | `Product.cs:107-110`; autor vazio na API (EXC-ENG-011) |
| Cliente PF/PJ com IE, endereço, grupo de preço, limite de crédito | W, P | Implementada (P: Parcial, EXC-ENG-006) | Unidade | Desconhecida | `Customer.cs:8-41`; `CustomerServiceTests` |
| Fornecedor | W | **Parcial** (só nome e ativo) | Nenhum | Desconhecida | `Supplier.cs:8-9`; EXC-PRD-007 |
| Categorias e marcas | W | Implementada | Nenhum | Desconhecida | `SupplierCategoryBrandService` |
| Importação de produtos e clientes por CSV, com saldo de fiado e haver | W | Implementada | Nenhum | Desconhecida | `ImportacaoService.cs`; EXC-A10-VENDAS §3.1 |
| Importação de histórico de vendas do sistema antigo | W | Parcial (todos os itens no primeiro produto; um único formato) | Nenhum | Desconhecida | `LegacyImportService.cs:23-130` |
| Etiquetas de preço / gôndola / código de barras | — | **Ausente** | — | — | Busca por `Etiqueta` sem resultado |
| Kits e composições (vende o kit, baixa os componentes) | — | **Ausente** | — | — | `ProdutoAgregado` é sugestão, não composição |

### 4.2 Compras e fornecedores

| Funcionalidade | App | Implementação | Teste | Operação | Evidência / observação |
|---|---|---|---|---|---|
| Pedido de compra: rascunho → enviado → recebido (soma estoque, atualiza custo) | W, P | Implementada | Unidade (3) | Desconhecida | `PedidoCompraService.cs:36-157`; `NovosTestes.cs:231` |
| Recebimento parcial do pedido | — | **Ausente** (recebe todos os itens) | — | — | `PedidoCompra.Receber()`; busca sem resultado |
| Importar XML da NF-e de compra: valida assinatura e SEFAZ, cria produto, soma estoque, atualiza custo, gera contas a pagar pelas duplicatas | W | **Parcial** (sem trava de duplicidade) | Nenhum | Desconhecida | `NfeImportService.cs:298-410`; EXC-PRD-003 |
| Conversão da unidade do fornecedor (caixa → unidade) na entrada | W | Parcial (quantidade conferida digitada à mão) | Nenhum | Desconhecida | `NfeImportService.cs:336-341` |
| Notas emitidas contra o CNPJ da loja (MD-e, via Focus) | W | Implementada | Nenhum | Desconhecida | `NfeRecebidaService` |
| Sugestão de compras por estoque mínimo/ideal | W, P | Implementada | Fumaça | Desconhecida | `SugestaoComprasService` |
| Histórico de compras por produto | W | Implementada | Nenhum | Desconhecida | `PedidoCompraService.cs:202` |
| Custo médio ponderado | — | **Ausente** (última entrada substitui) | — | — | EXC-PRD-004 |
| Devolução de compra ao fornecedor | W | Parcial (NF-e avulsa; baixa de estoque não verificada) | Unidade (nota avulsa) | Desconhecida | `NotaFiscalAvulsaService` |

### 4.3 Estoque

| Funcionalidade | App | Implementação | Teste | Operação | Evidência / observação |
|---|---|---|---|---|---|
| Saldo único por produto, baixa atômica na venda | W, P | Implementada | Unidade | Inferida | EXC-A6 §1 item 5 |
| Ajuste manual com permissão `stock.adjust` | W | Implementada | Nenhum | Desconhecida | `AjusteEstoqueView` |
| Inventário: contagem, divergência, aplicar ajuste | W, P | Implementada | Fumaça | Desconhecida | `InventarioController.cs:82-130` |
| Estoque negativo permitido por produto | W | Implementada | Nenhum | Desconhecida | `Product.cs:61` |
| Estoque mínimo, alertas e notificações | W, P | Implementada | Fumaça | Desconhecida | `NotificacoesView`, `/api/stock/low` |
| Curva ABC | W | Implementada | Nenhum | Desconhecida | `EstoqueAbcView` |
| Localização no depósito | W | Implementada (campo) | — | Desconhecida | `Product.cs:62` |
| Histórico de movimentação por produto (kardex) | — | **Ausente** (só a auditoria genérica) | — | — | Busca por `Kardex`, `MovimentoEstoque` sem resultado; EXC-PRD-004 |
| Lote e validade (cimento, argamassa, tinta) | — | **Ausente** | — | — | Busca sem resultado no domínio |
| Reserva para retirada posterior ou entrega | — | **Ausente** no PDV (existe só para pedidos de marketplace) | — | — | `ShadowStockReservation.cs:8` |
| Saldo por filial e transferência | P | **Parcial** | Fumaça | Desconhecida | EXC-PRD-002 |

### 4.4 Vendas fora do PDV

| Funcionalidade | App | Implementação | Teste | Operação | Evidência / observação |
|---|---|---|---|---|---|
| Orçamento com validade (7 dias), impressão, conversão, *follow-up*, taxa de conversão | W, P | Implementada | Fumaça | Desconhecida | `Orcamento.cs:79`; `OrcamentoService.cs:26-207` |
| Venda suspensa (estacionar e retomar) | W | Implementada | Nenhum | Desconhecida | `VendaSuspensaService` |
| Cancelamento de venda | W | **Parcial** (estorno duplo em dinheiro; sem transação) | Unidade (partes) | **Validada** | EXC-ENG-005, EXC-ENG-008; `SaleService.cs:385-397` |
| Devolução parcial com crédito em haver e NF-e de devolução | W, P | **Parcial** | Unidade (fiscal) | Desconhecida | EXC-PRD-006, EXC-ENG-007, EXC-ENG-008 |
| Troca (devolve e leva outro na mesma operação) | — | Ausente como fluxo; feita por devolução + haver + venda nova | — | Desconhecida | — |
| Entregas: agenda, janela, motorista, veículo, assinatura, tela do motorista | P | Implementada | Fumaça | Desconhecida | `Entrega.cs:10-54`; `Motorista.razor` |
| Comissões de vendedor | P | Implementada (relatório) | Fumaça | Desconhecida | `ComissaoRelatorioService` |
| Metas de vendas e painel de TV | P | Implementada (módulo opcional) | Fumaça | Desconhecida | `TenantFeatureFlags.cs:19-20` |
| Fidelidade (pontos) | W, P | Implementada (módulo opcional) | Unidade | Desconhecida | `RelatorioServicesTests` |
| CRM (contatos e *follow-up* de orçamento) | P | Implementada | Nenhum | Desconhecida | `CRM.razor` |
| Tintométrico (fórmulas de cor) | P | Implementada | Fumaça | Desconhecida | `TintometricoService` |
| Calculadora de materiais (interna e pública) | P | Implementada | Fumaça | Desconhecida | `CalculadoraService` |
| Catálogo público e autoatendimento | P | Implementada | Fumaça | Desconhecida | `CatalogoPublico.razor`, `AutoAtendimento.razor` |
| Pedido por obra (cliente construtora com várias obras) | — | Ausente (endereço só na entrega) | — | — | — |

### 4.5 PDV

| Funcionalidade | App | Implementação | Teste | Operação | Evidência / observação |
|---|---|---|---|---|---|
| Venda de balcão: busca por código ou nome, leitor de código de barras | W | Implementada | Unidade + cliente HTTP | **Validada** | Confirmação 4.3 (2026-10-22) |
| Pagamento dividido: dinheiro, débito, crédito, Pix, a prazo, haver | W, P | Implementada | Unidade + Fluxo de API | Validada para a venda; **por forma, Desconhecida** | `Enums.cs:10-18`; `MotorFinanceiroServiceTests`; "POST /api/sales cria venda e processa financeiro" |
| Idempotência da venda (mesmo `Id` duas vezes não duplica) | W, P | Implementada | Fluxo de API | Desconhecida | `ControllersIntegrationTests` |
| Desconto com autorização de gerente por senha | W | Implementada | Unidade | Desconhecida | `SenhaGerenteView`, `AlertaDescontoView` |
| Fiado com limite de crédito | W | Implementada | Unidade | Desconhecida | `SaleService.cs:226-231`; EXC-A10-MARKETING §6.1 |
| Pix com QR e confirmação automática | W | Parcial (depende de conta em provedor; senão manual) | Unidade | Desconhecida | `PixPollingServiceTests` |
| Venda sem internet (fila local, sincroniza a cada minuto) | W | Implementada | Unidade (fila, motor, *fallback*) | **Indício** | Correção `f195b8b` (venda offline presa em *loop*) |
| Balança | W | Implementada | Nenhum | Desconhecida | `BalancaService` |
| Cupom ou recibo impresso | W | Implementada | Nenhum | Inferida | `config_recibo.json` |
| PDV no navegador | P | Implementada (sem fila offline) | Fumaça | Desconhecida | `PDV.razor:1742` |
| TEF / maquininha integrada | — | **Ausente** | — | — | EXC-A10-MARKETING §6.2 |
| Pré-venda (vendedor monta, caixa recebe) | — | Não identificada como fluxo; venda suspensa é o mais próximo (não verificado) | — | — | — |

### 4.6 Caixa

| Funcionalidade | App | Implementação | Teste | Operação | Evidência / observação |
|---|---|---|---|---|---|
| Abertura com fundo de troco | W, P | Implementada | Unidade | Inferida | `CaixaServiceTests`; venda de PDV exige caixa aberto |
| Sangria e suprimento com autorização | W, P | Implementada | Fluxo de API (autorizador) | Desconhecida | 7 testes "Fase C — Sangria" |
| Recebimento de conta e pagamento de despesa pelo caixa | W | Implementada | Unidade | Desconhecida | `CaixaEnums.cs:17-18` |
| Resumo por forma de pagamento | W, P | Implementada | Unidade | Inferida | `CaixaServiceObterResumoAsyncTests` |
| Fechamento com valor contado e diferença (quebra) | W | **Parcial** (fecha sem contagem) | Nenhum | Inferida (fecha) | `Caixa.cs:11-22`; EXC-PRD-005 |
| Reabertura de caixa fechado | — | Ausente por decisão ("não poderá ser reaberto") | — | — | `ResumoCaixaViewModel.cs:242` |

### 4.7 Financeiro

| Funcionalidade | App | Implementação | Teste | Operação | Evidência / observação |
|---|---|---|---|---|---|
| Contas a receber: baixa parcial, eventos, parcelamento manual | W, P | Implementada | Unidade (7 arquivos) | Desconhecida | `ContaReceberService.cs:320-335` |
| Venda a prazo gera a conta a receber | W | **Parcial** (uma conta, 30 dias fixos) | Unidade | Desconhecida | `ContaReceberService.cs:62`; EXC-PRD-008 |
| Juros e multa por atraso | — | **Ausente** | — | — | Busca por `Juros`, `Multa` sem resultado |
| Inadimplentes | W, P | Implementada | Fumaça | Desconhecida | `/api/contas-receber/inadimplentes/count` |
| Boleto (Asaas) | API, P | Parcial (chave única da aplicação) | Fumaça | Desconhecida | EXC-ADM-005 |
| Haver (crédito do cliente) | W, P | Implementada | Unidade | Desconhecida | EXC-DAD-002 (leitura-e-gravação) |
| Contas a pagar, inclusive das duplicatas do XML | W, P | Implementada | Fumaça + cliente HTTP | Desconhecida | `HttpContaPagarServiceTests` |
| Contas bancárias e extrato | W | Implementada | Unidade | Desconhecida | `ExtratoFinanceiroServiceTests` |
| Conciliação bancária por OFX | W, P | Implementada | Fumaça (parser sem teste) | Desconhecida | `OfxParser.cs` |
| Operadoras de cartão e recebíveis (taxa e prazo) | W | Implementada | Unidade | Desconhecida | `MotorFinanceiroServiceTests` |
| Fluxo de caixa e DRE | W | Implementada | Nenhum | Desconhecida | `FluxoCaixaView`, `DreView` |
| Plano de contas / centro de custo | — | **Ausente** | — | — | Busca sem resultado |

### 4.8 Fiscal

| Funcionalidade | App | Implementação | Teste | Operação | Evidência / observação |
|---|---|---|---|---|---|
| NFC-e da venda (Focus NFe) | W | **Parcial** (CNPJ do emitente fixo; aspas na descrição) | Unidade (extensa) | **Validada** | EXC-FIS-013, EXC-FIS-011, EXC-FIS-009 |
| NF-e modelo 55 da venda (cliente PJ) | W, P | Implementada | Unidade | Desconhecida na Vila Verde | `FiscalService.cs:57`; 4 NF-e do tenant de teste em produção (EXC-FIS-008) |
| Nota avulsa e NF-e única de várias vendas | W | Implementada | Unidade | Desconhecida | `NotaFiscalAvulsaService.cs:37-924` |
| Cancelamento de nota | W, P | Implementada | Unidade | Desconhecida | `NfeCancellationService` |
| Carta de correção | W | Implementada | Nenhum | Desconhecida | `NfeCorrecaoService` |
| Inutilização de numeração | — | **Ausente** | — | — | Busca por `Inutiliz` sem resultado |
| Contingência e recuperação | W, API | Parcial | Unidade (extensa) | **Validada com falha** (nota presa) | EXC-FIS-007, EXC-FIS-009 |
| NF-e de devolução de venda | W, P | Implementada (melhor esforço) | Unidade | Desconhecida | `FiscalService.cs:326` |
| NFS-e | W, P | Implementada | Fumaça | Desconhecida | `NfseEmissionService` |
| Regime tributário | — | **Só Simples Nacional** (CSOSN) | Unidade | — | `MotorFiscalBrasileiro.cs:51-87`; EXC-FIS-014 |
| Substituição tributária (CSOSN 201-203, MVA) | W | Parcial | Unidade | Desconhecida | `FiscalService.cs:821-822` |
| IBS/CBS (Reforma Tributária) | — | **Ausente** | — | — | EXC-FIS-005 |
| SPED EFD ICMS/IPI | W, P | Parcial | Unidade (3) | Desconhecida | EXC-FIS-006, EXC-FIS-012 |
| SPED Contribuições | W | Implementada | Nenhum | Desconhecida | `SpedContribuicoesGenerator` |
| Exportar XML para o contador | W | Implementada | Nenhum | Declarada (o responsável baixa da Focus) | `ExportarNotasView`; EXC-FIS-008 |

### 4.9 Relatórios

| Funcionalidade | App | Implementação | Teste | Operação | Evidência / observação |
|---|---|---|---|---|---|
| Dashboard e BI | W, P | Implementada | Nenhum (BI, Dashboard) | Desconhecida | `BIService`, `DashboardService` |
| Vendas, histórico, vendas × custo, margem | W, P | Implementada | Unidade parcial | Desconhecida | Custo = última entrada (EXC-PRD-004) |
| Comissões, entregas, divergências de inventário | P | Implementada | Fumaça | Desconhecida | — |
| Painel de TV | P | Implementada | Nenhum | Desconhecida | `DashboardTv.razor` |
| Auditoria (quem alterou o quê) | W, P | Implementada | Fumaça + autorização | Desconhecida | `AuditLogs` |

### 4.10 Usuários e permissões

| Funcionalidade | App | Implementação | Teste | Operação | Evidência / observação |
|---|---|---|---|---|---|
| Login por usuário no WPF, no Portal e na API | W, P | Implementada | Unidade + integração | **Validada** (3 PCs) | Confirmação 4 |
| Cargos com 28 permissões editáveis | W, P | Implementada | Integração ("alteração de permissões persiste") | Desconhecida | `DbSeeder.cs:69-104, 249-250` |
| Autorização pontual por senha de gerente | W | Implementada | Fluxo de API (sangria) | Desconhecida | `SenhaGerenteView` |
| Encerramento por inatividade | W | Implementada | Nenhum | Desconhecida | `MainWindow.xaml.cs:140` |
| Recuperação e troca de senha | P | Implementada | Nenhum | Desconhecida | `RecuperarSenha.razor`, `NovaSenha.razor` |
| Permissão por filial | — | Ausente | — | — | EXC-PRD-002 |
| Promoção indevida a Administrador pelo *seeder* | W | Defeito | — | Hipótese | EXC-SEG-010 |

### 4.11 Filiais

| Funcionalidade | App | Implementação | Teste | Operação | Evidência / observação |
|---|---|---|---|---|---|
| Cadastro de filiais (CNPJ, matriz, ativa) | W | Implementada | Fumaça | Validada (registros existem; matriz duplicada, EXC-DAD-003) | `Branch.cs:11-16` |
| Venda, caixa, usuário e nota por filial | — | **Ausente** | — | — | EXC-PRD-002 |
| Estoque por filial e transferência | P | **Parcial** | Fumaça | Desconhecida | EXC-PRD-002 |
| Configuração fiscal (emitente) por filial | — | Ausente (por tenant) | — | — | EXC-FIS-013 |
| Catálogo público por filial | P | Implementada | Nenhum | Desconhecida | `Branch.cs:24-28` |

### 4.12 Integrações

| Funcionalidade | Implementação | Teste | Operação | Evidência / observação |
|---|---|---|---|---|
| Focus NFe (emissão, consulta, cancelamento) | Implementada | Unidade (extensa) | **Validada** | Fiscal |
| Mercado Livre (pedidos, SKU, estoque, política de preço) | Implementada | Unidade | Relatório anterior (DOC-32); uso atual desconhecido | INV-011; EXC-SEG-004 |
| Shopee | **Ausente** (só o valor no enum) | — | — | `MarketplaceEnums.cs:7`; só existe `MercadoLivreDispatcher` |
| API de e-commerce (catálogo e pedidos com chave) | Implementada | Nenhum | Desconhecida | `ERP.Api/Ecommerce` |
| Asaas (boleto) | Parcial | Fumaça | Desconhecida | EXC-ADM-005 |
| Provedores de Pix (OpenPix, EFÍ, PagBank) | Parcial (opcional) | Unidade | Desconhecida | `PixPollingService` |
| BrasilAPI (CNPJ) e consulta SEFAZ do XML | Implementada | Unidade (cadastro) | Desconhecida | `CadastroServiceTests` |
| Chat interno | Implementada | Fluxo de API (token) | Desconhecida | `ChatHub` |
| Atualização automática do WPF | Implementada | Unidade | Declarada | `UpdateServiceTests`; confirmação 4 |
| Licenciamento | Implementada (API externa) | Nenhum | Validada (PDV em uso) | EXC-OPS-005 |
| Backup local noturno | **Parcial** (incompatível com Azure SQL Database) | Nenhum | Hipótese: falha silenciosa | EXC-OPS-008 |
| TEF | **Ausente** | — | — | — |

### 4.13 Implantação e ciclo de vida do cliente

| Funcionalidade | Implementação | Observação |
|---|---|---|
| Cadastro público de loja | Implementada | Sem plano, prazo nem termos (EXC-VEN-002) |
| Primeiro uso do WPF | Implementada | Pede a credencial do SQL (EXC-SEG-001) |
| Exportação completa dos dados da loja | **Ausente** | EXC-PRD-011 |
| Loja de demonstração com dados de exemplo | Ausente | EXC-A10-VENDAS §3.1 |
| Planos, assinatura, painel da TTSoft | Ausente | EXC-A10-ADM §6.1 |

### 4.14 Totais

Contagem das 128 linhas das tabelas 4.1 a 4.13 pela coluna "Implementação" (linhas com
defeito registrado contam como Parcial; a classificação de 3 ou 4 linhas é discutível, por
isso os números são aproximados):

| Condição | Quantidade |
|---|---|
| Implementada | ~79 |
| Parcial | ~26 |
| Ausente | ~23 |
| Com teste de fluxo | 5 (criação de venda com financeiro, idempotência, sangria com autorizador, cargos, isolamento) |
| Validada em operação | 6 (venda no PDV, NFC-e, cancelamento, login, licenciamento, cadastro de filial) |

---

## 5. Jornadas ponta a ponta

Cada jornada é o que a loja faz num dia normal. "Ruptura" é onde o fluxo para, duplica ou
perde informação.

### J1 — Comprar e receber mercadoria

Pedido de compra (opcional) → chega a nota → importar o XML → conferir quantidades e custos →
estoque somado, custo atualizado, contas a pagar criadas.

| Situação | Estado |
|---|---|
| Caminho principal | Implementado, sem teste, uso desconhecido |
| Ruptura 1 | Mesmo XML importado duas vezes duplica estoque e contas a pagar (EXC-PRD-003) |
| Ruptura 2 | Receber o pedido de compra **e** importar o XML da mesma mercadoria soma o estoque duas vezes: os dois fluxos não se conhecem (`PedidoCompraService.cs:142`, `NfeImportService.cs:338`) |
| Ruptura 3 | Fornecedor localizado pelo nome exato; variação de grafia cria outro fornecedor (EXC-PRD-007) |
| Exceção não tratada | Mercadoria chega em parte: não há recebimento parcial |
| Exceção não tratada | Custo de uma compra promocional vira o custo do produto inteiro (EXC-PRD-004) |

### J2 — Vender no balcão, à vista

Abrir caixa → buscar produtos → desconto (com senha, se preciso) → pagamento dividido → NFC-e →
cupom.

| Situação | Estado |
|---|---|
| Caminho principal | **Validado em operação**; testes de unidade e de fluxo na API |
| Ruptura | NFC-e com aspas na descrição é recusada (EXC-FIS-011); nota em contingência pode ficar presa (EXC-FIS-009) |
| Exceção tratada | Sem internet, a venda entra na fila local (Indício de uso) |
| Exceção tratada | Estoque insuficiente bloqueia, salvo produto com estoque negativo permitido |

### J3 — Vender fiado e receber depois

Cliente com limite → forma "a prazo" → conta a receber → cliente volta e paga no caixa → baixa.

| Situação | Estado |
|---|---|
| Caminho principal | Implementado; limite testado; uso desconhecido |
| Ruptura | Conta única com vencimento fixo em 30 dias; o combinado com o cliente (parcelas, dia do mês) não é registrado na venda (EXC-PRD-008) |
| Exceção não tratada | Atraso: sem juros nem multa; cobrança só por lista de inadimplentes |
| Exceção parcial | Boleto pelo Asaas depende de chave única da aplicação (EXC-ADM-005) |

### J4 — Orçamento, venda e entrega

Orçamento para o cliente da obra → *follow-up* → cliente aprova → venda → entrega agendada →
motorista confirma.

| Situação | Estado |
|---|---|
| Caminho principal | Implementado em partes espalhadas: orçamento no WPF e no Portal, entrega e motorista **só no Portal** |
| Ruptura | Estoque sai na venda, não na entrega; não há reserva nem "retirar depois" para mercadoria paga e não levada |
| Exceção não tratada | Entrega parcial (parte hoje, parte amanhã) |
| Exceção não tratada | Preço do orçamento vencido: validade de 7 dias fixa (`Orcamento.cs:79`) |

### J5 — Devolução e troca

Cliente traz o produto → localizar a venda → devolver itens → crédito → (troca) nova venda.

| Situação | Estado |
|---|---|
| Caminho principal | Implementado com cliente identificado: estoque volta, haver creditado, NF-e de devolução se a venda tinha NF-e |
| Ruptura 1 | Venda sem cliente (consumidor final, o caso mais comum no balcão): o estoque volta, mas **nada registra o dinheiro devolvido** (EXC-PRD-006) |
| Ruptura 2 | Produto filho devolve o estoque ao produto errado (EXC-ENG-007); gravações sem transação (EXC-ENG-008) |
| Exceção não tratada | Devolver em dinheiro, Pix ou estorno de cartão |

### J6 — Cancelar uma venda

| Situação | Estado |
|---|---|
| Caminho principal | **Validado em operação** |
| Ruptura | Em dinheiro, pelo histórico do WPF, o caixa é estornado duas vezes (EXC-ENG-005) |

### J7 — Fechar o caixa do dia

Sangrias ao longo do dia → resumo por forma → contar a gaveta → fechar → conferir com o dono.

| Situação | Estado |
|---|---|
| Caminho principal | Fecha com o resumo do sistema |
| Ruptura | Não há onde informar o valor contado; a diferença não é registrada nem fica no histórico (EXC-PRD-005). Somado a EXC-ENG-005, o "esperado" pode estar errado e ninguém percebe |

### J8 — Inventário

| Situação | Estado |
|---|---|
| Caminho principal | Contagem, relatório de divergência e ajuste com permissão |
| Ruptura | Sem kardex, a divergência não tem explicação (qual entrada, venda ou ajuste a causou) (EXC-PRD-004) |
| Exceção não tratada | Vendas durante a contagem: o ajuste usa o saldo do momento da contagem (`InventarioController.cs:88`); hipótese de distorção, a confirmar na EXC-A3 |

### J9 — Vender para construtora com NF-e

| Situação | Estado |
|---|---|
| Caminho principal | Cliente PJ com IE → venda → NF-e modelo 55 |
| Ruptura | Emitente fixo (EXC-FIS-013) bloqueia qualquer loja além da Vila Verde |
| Exceção não tratada | Várias obras do mesmo cliente; faturamento mensal de várias entregas (há "NF-e única de várias vendas", sem vínculo com obra) |

### J10 — Implantar uma loja nova

Coberta na [EXC-A10-VENDAS](EXC-A10-VENDAS.md) §11 e em EXC-VEN-003. Acréscimos desta
passada: a loja precisa ser do Simples Nacional (EXC-FIS-014); não há como devolver os dados
se ela sair (EXC-PRD-011).

### J11 — Operar sem internet

Só a venda tem fila local. Consultas de cliente, fiado, haver, cadastros e relatórios dependem
de rede (EXC-A10-MARKETING §6.2). Hoje o "offline" é do PDV, não da loja.

---

## 6. Regras de negócio e casos excepcionais

| Caso | Tratado? | Evidência |
|---|---|---|
| Venda acima do limite de crédito | Sim, bloqueia | `SaleService.cs:226-231` |
| Desconto acima do permitido ao operador | Sim, senha de gerente | `AlertaDescontoView` |
| Devolver mais do que foi vendido | Sim, inclusive somando devoluções anteriores | `DevolucaoService.cs:39-55` |
| Valor da devolução informado pelo cliente da API | Sim, recalculado no servidor | `DevolucaoService.cs:67-72` |
| Venda repetida (mesmo `Id`) | Sim, idempotente | Teste de fluxo da API |
| Venda de produto excluído durante o offline | Sim (correção recente) | `f195b8b` |
| Estoque negativo | Por produto | `Product.cs:61` |
| Sangria sem permissão | Sim, autorizador | 7 testes |
| Importar a mesma NF-e duas vezes | **Não** | EXC-PRD-003 |
| Receber pedido e importar o XML da mesma compra | **Não** | §5 J1 |
| Diferença no fechamento do caixa | **Não** | EXC-PRD-005 |
| Devolução de consumidor não identificado | **Não** | EXC-PRD-006 |
| Atraso no fiado (juros, multa) | **Não** | EXC-PRD-008 |
| Mercadoria paga e não retirada | **Não** | §4.3 |
| Lacuna na numeração fiscal (inutilização) | **Não** | §4.8 |
| Loja fora do Simples Nacional | **Não** | EXC-FIS-014 |
| Produto com aspas na descrição | **Não** | EXC-FIS-011 |
| Cancelamento em dinheiro | Com defeito | EXC-ENG-005 |

---

## 7. Necessidades de uma loja de materiais de construção

Necessidades típicas do segmento, comparadas ao produto. As necessidades são uma HIPÓTESE
de mercado (EXC-MKT-001: perfil de cliente não validado); a coluna "Situação" é FATO-CÓDIGO.

| Necessidade | Por que importa no segmento | Situação |
|---|---|---|
| Fiado com limite e cobrança | Clientela recorrente de pedreiros e pequenos empreiteiros | Parcial: limite sim; parcelas, vencimento combinado, juros e multa não (EXC-PRD-008) |
| Venda fracionada e conversão de unidade | Fio por metro, areia por metro cúbico, piso por caixa e por m² | Implementada (conversão) e Parcial (produto filho, EXC-ENG-007); sem teste |
| Preço por tipo de cliente e atacado | Profissional paga diferente do consumidor | Implementada |
| Orçamento com *follow-up* | Obra decide em dias ou semanas | Implementada |
| Entrega agendada | Material pesado sai por caminhão | Implementada, só no Portal |
| Retirada posterior e entrega parcial | Cliente paga a obra toda e retira por etapa | **Ausente** |
| NF-e para construtora | Cliente PJ exige nota modelo 55 | Implementada; bloqueada para outras lojas por EXC-FIS-013 |
| Compras pelo XML do fornecedor | Muitos itens por nota | Implementada; duplicidade não bloqueada (EXC-PRD-003) |
| Custo confiável e margem | Margens apertadas, preços de fornecedor oscilam | Parcial: último custo, sem custo médio (EXC-PRD-004) |
| Etiquetas de preço | Gôndola e prateleira | **Ausente** |
| Maquininha integrada (TEF) | Objeção frequente (EXC-A10-VENDAS O2) | **Ausente** |
| Tintométrico | Lojas que vendem tinta | Implementada (Portal) |
| Calculadora de materiais | Ajuda o cliente leigo | Implementada |
| Lote e validade | Cimento, argamassa, rejunte, tinta | **Ausente** |
| Fechamento de caixa conferido | Controle de perda por funcionário | Parcial (EXC-PRD-005) |
| Simples Nacional | Maioria das lojas pequenas | Implementada |
| Lucro Presumido | Lojas médias | **Ausente** (EXC-FIS-014) |
| Várias lojas | Pequenas redes regionais | Parcial: só cadastro (EXC-PRD-002) |
| Sistema funcionando sem internet | Internet instável em cidades menores | Parcial: só a venda (§5 J11) |
| Migração do sistema antigo | Principal barreira de troca (EXC-A10-MARKETING D7) | Implementada para produtos, clientes e saldo de fiado; sem teste |

---

## 8. Lacunas de produto para vender a lojas independentes

Classificação: **B** = bloqueia vender ou implantar; **C** = necessária para competir (aparece
em quase toda negociação); **D** = diferencial ou nicho. Os bloqueadores não funcionais
(segurança, contrato, implantação) já estão registrados e só são citados.

### 8.1 Bloqueadores (B)

| # | Lacuna | Registro | Por que bloqueia |
|---|---|---|---|
| B1 | Emitente fixo na Focus | EXC-FIS-013 | Nenhuma segunda loja emite nota correta |
| B2 | Estorno duplo no cancelamento em dinheiro | EXC-ENG-005 | O caixa de toda loja nova fica errado desde o primeiro cancelamento |
| B3 | Edição pelo Portal apaga campos fiscais | EXC-ENG-006 | O dono que edita pelo Portal estraga o cadastro fiscal |
| B4 | Regime só Simples Nacional | EXC-FIS-014 | Vender para loja fora do Simples é vender nota errada; a qualificação precisa filtrar |
| B5 | Sem exportação dos dados da loja | EXC-PRD-011 | Contrato e LGPD exigem devolver os dados no cancelamento (EXC-A10-VENDAS O1, O12) |
| B6 | Importação duplicada de XML | EXC-PRD-003 | Corrompe estoque e contas a pagar com um clique repetido; correção pequena |
| — | Credencial do SQL no PC, contrato, implantação manual | EXC-SEG-001, EXC-FIS-002, EXC-VEN-003 | Já registrados |

### 8.2 Necessárias para competir (C)

| # | Lacuna | Registro |
|---|---|---|
| C1 | Fechamento de caixa com contagem e diferença | EXC-PRD-005 |
| C2 | Devolução com reembolso em dinheiro/Pix e registro no caixa | EXC-PRD-006 |
| C3 | Fiado com parcelas, vencimento combinado, juros e multa | EXC-PRD-008 |
| C4 | Custo médio e histórico de movimentação de estoque | EXC-PRD-004 |
| C5 | Etiquetas de preço | (sem registro; roadmap) |
| C6 | Inutilização de numeração fiscal | (sem registro; levar à EXC-A4) |
| C7 | Retirada posterior / entrega parcial com reserva | (sem registro; roadmap) |
| C8 | Fornecedor com CNPJ e contato | EXC-PRD-007 |
| C9 | TEF | (decisão de produto; motivo de perda a medir, EXC-A10-VENDAS §13.4) |

### 8.3 Diferenciais e nicho (D)

Multi-filial real (EXC-PRD-002), Lucro Presumido, lote e validade, kits, pedido por obra,
Shopee, Mobile. Recomendação: **não** investir antes de haver cliente pedindo, medido no funil
(EXC-A10-VENDAS §13.4, motivos de perda).

### 8.4 O que já existe e é diferencial

Fiado com limite, calculadora de materiais, tintométrico, entregas com motorista, importação
de XML, Mercado Livre, venda sem internet. São diferenciais **no código**; para virarem
argumento comercial precisam de teste e uso registrados (CRITERIOS MKT-2).

---

## 9. Jornadas críticas e como validá-las

### 9.1 Prioridade

Ordem pelos CRITERIOS §5 e §7 e pelo que a loja faz todo dia:

| Prioridade | Jornada | Implementação | Teste | Operação |
|---|---|---|---|---|
| 1 | J2 Vender à vista | Implementada | Unidade + fluxo de API | Validada |
| 2 | J7 Fechar o caixa | Parcial | Unidade | Inferida |
| 3 | J6 Cancelar | Parcial (defeito Alto) | Partes | Validada |
| 4 | J3 Fiado | Parcial | Unidade | Desconhecida |
| 5 | J5 Devolução e troca | Parcial | Unidade (fiscal) | Desconhecida |
| 6 | J1 Compras | Parcial | Nenhum | Desconhecida |
| 7 | J9 NF-e para PJ | Parcial (bloqueada fora da Vila Verde) | Unidade | Desconhecida |
| 8 | J4 Orçamento e entrega | Implementada, espalhada | Fumaça | Desconhecida |
| 9 | J8 Inventário | Implementada | Fumaça | Desconhecida |
| 10 | J11 Sem internet | Parcial | Unidade | Indício |

### 9.2 Roteiro de observação na Vila Verde (para PRD-1)

RECOMENDAÇÃO. Duas visitas de meio período, uma num dia de movimento e uma no fechamento do mês,
sem intervir.

1. Antes: rodar a consulta da seção 11 para saber quais módulos a loja usa.
2. Observar e anotar, para as jornadas 1 a 6: quem faz, quanto tempo leva, onde hesita, que
   mensagem aparece, o que faz fora do sistema (caderno, planilha, calculadora, maquininha).
3. Perguntar ao fim: o que você faz no papel que deveria estar no sistema? O que do sistema
   você nunca usa?
4. Registrar cada problema como `EXC-PRD-NNN` com a jornada, a frequência observada e a
   evidência (foto da tela ou anotação datada).
5. Repetir a consulta da seção 11 após 30 dias.

---

## 10. Roadmap orientado ao cliente

RECOMENDAÇÃO, a decidir na triagem (PLANO-MESTRE §6). Sem datas: a capacidade é de uma pessoa
(EXC-ADM-002), e a ordem importa mais do que o prazo. Cada etapa diz o que o **cliente** ganha.

### R0 — "A loja atual não perde dinheiro nem nota"

| Item | Registro | Resultado para a loja |
|---|---|---|
| Corrigir o estorno duplo | EXC-ENG-005 | Caixa confere após cancelamentos |
| Corrigir ou desligar a edição pelo Portal | EXC-ENG-006 | Cadastro fiscal não se perde |
| Bloquear XML de compra repetido | EXC-PRD-003 | Estoque e contas a pagar não duplicam |
| Destravar a contingência | EXC-FIS-009 | Nenhuma nota presa |
| Corrigir o *seeder* | EXC-SEG-010 | Ninguém vira administrador sem querer |
| Medir o uso por módulo | EXC-PRD-010 | Saber o que a loja usa antes de mexer |

Critério de saída: cada item com teste de regressão do fluxo composto (EXC-A6 §9) e a
consulta da seção 11 executada.

### R1 — "Uma segunda loja do Simples Nacional, com uma filial, pode ser vendida e implantada"

| Item | Registro | Resultado para o cliente |
|---|---|---|
| Emitente por loja | EXC-FIS-013 | Nota sai com o CNPJ certo |
| Login restrito, sem credencial de administrador no PC | EXC-SEG-001 | Dados de uma loja não ficam ao alcance de outra |
| Contrato, termos, privacidade | EXC-FIS-002 | Venda com regra clara |
| Exportação completa dos dados | EXC-PRD-011 | O cliente pode sair com os dados dele |
| Roteiro de implantação | EXC-VEN-003 | Implantação repetível |
| Papel de cada aplicativo definido | EXC-PRD-009 | O dono sabe onde fazer cada coisa; menos regras duplicadas |
| Fechamento de caixa com contagem | EXC-PRD-005 | Controle de quebra de caixa |
| Devolução com reembolso registrado | EXC-PRD-006 | Caixa fecha também em dia de devolução |
| Fiado em parcelas com vencimento combinado | EXC-PRD-008 (parte) | Cobrança como a loja já combina com o cliente |
| Qualificação comercial filtra o regime tributário | EXC-FIS-014 | Ninguém compra o que não pode usar |
| Ocultar ou rotular o que é Parcial (multi-filial, Shopee, backup local) | EXC-PRD-002, EXC-OPS-008 | Nada na tela promete o que não faz |

Critério de saída: CRITERIOS VEN-3 (implantação só com o roteiro) e as jornadas J1 a J7 com
teste de fluxo e observadas em uso real (PRD-1).

### R2 — "Competitivo para a loja típica do segmento"

Etiquetas de preço; custo médio e kardex (EXC-PRD-004); juros e multa (EXC-PRD-008);
retirada posterior com reserva; inutilização (EXC-A4); fornecedor completo (EXC-PRD-007);
Reforma Tributária (EXC-FIS-005, calendário legal a confirmar com o contador na EXC-A4);
decisão sobre TEF a partir dos motivos de perda medidos.

Critério de saída: os motivos de perda "falta funcionalidade" do funil (EXC-A10-VENDAS §13.4)
deixam de aparecer para os itens entregues.

### R3 — "Crescer além da loja única"

Multi-filial real (EXC-PRD-002) ou retirada da oferta; Lucro Presumido (EXC-FIS-014); lote e
validade; pedido por obra; Shopee; Mobile (EXC-PRD-001). Só com cliente pagante pedindo.

### Congelar

Até o fim de R1, **não ampliar** módulos já largos e sem uso medido: CRM, metas, painel de TV,
fidelidade, chat, catálogo público, autoatendimento, BI. Manter funcionando, sem novas frentes.
Motivo: cada módulo novo sem teste aumenta a área que uma pessoa precisa manter (EXC-ADM-002)
e o risco de prometer o que não está validado (EXC-MKT-003).

---

## 11. Consulta de confirmação (uso real por módulo)

Somente `SELECT`, no `ERPTTSoftStaging`, como no [roteiro da EXC-A0](ROTEIRO-CONFIRMACOES-A0.md).
Retorna contagens por tabela para um tenant, sem dados de clientes finais. Substituir
`@tenant` pelo `TenantId` da Vila Verde. Os nomes das tabelas seguem os `DbSet` do
`AppDbContext`; se algum não existir, conferir com `SELECT name FROM sys.tables`.

```sql
DECLARE @tenant uniqueidentifier = '<TenantId da Vila Verde>';
DECLARE @desde  datetime2 = DATEADD(day, -90, SYSUTCDATETIME());

SELECT 'Sales' AS tabela, COUNT(*) AS total, SUM(CASE WHEN CreatedAt >= @desde THEN 1 ELSE 0 END) AS ultimos_90d FROM Sales WHERE TenantId = @tenant
UNION ALL SELECT 'Orcamentos',              COUNT(*), SUM(CASE WHEN CreatedAt >= @desde THEN 1 ELSE 0 END) FROM Orcamentos              WHERE TenantId = @tenant
UNION ALL SELECT 'VendasSuspensas',         COUNT(*), SUM(CASE WHEN CreatedAt >= @desde THEN 1 ELSE 0 END) FROM VendasSuspensas         WHERE TenantId = @tenant
UNION ALL SELECT 'SaleItemDevolucoes',      COUNT(*), SUM(CASE WHEN CreatedAt >= @desde THEN 1 ELSE 0 END) FROM SaleItemDevolucoes      WHERE TenantId = @tenant
UNION ALL SELECT 'Entregas',                COUNT(*), SUM(CASE WHEN CreatedAt >= @desde THEN 1 ELSE 0 END) FROM Entregas                WHERE TenantId = @tenant
UNION ALL SELECT 'Caixas',                  COUNT(*), SUM(CASE WHEN CreatedAt >= @desde THEN 1 ELSE 0 END) FROM Caixas                  WHERE TenantId = @tenant
UNION ALL SELECT 'CaixaMovimentos',         COUNT(*), SUM(CASE WHEN CreatedAt >= @desde THEN 1 ELSE 0 END) FROM CaixaMovimentos         WHERE TenantId = @tenant
UNION ALL SELECT 'ContasReceber',           COUNT(*), SUM(CASE WHEN CreatedAt >= @desde THEN 1 ELSE 0 END) FROM ContasReceber           WHERE TenantId = @tenant
UNION ALL SELECT 'ContasPagar',             COUNT(*), SUM(CASE WHEN CreatedAt >= @desde THEN 1 ELSE 0 END) FROM ContasPagar             WHERE TenantId = @tenant
UNION ALL SELECT 'MovimentosHaver',         COUNT(*), SUM(CASE WHEN CreatedAt >= @desde THEN 1 ELSE 0 END) FROM MovimentosHaver         WHERE TenantId = @tenant
UNION ALL SELECT 'MovimentosContaBancaria', COUNT(*), SUM(CASE WHEN CreatedAt >= @desde THEN 1 ELSE 0 END) FROM MovimentosContaBancaria WHERE TenantId = @tenant
UNION ALL SELECT 'RecebiveisOperadora',     COUNT(*), SUM(CASE WHEN CreatedAt >= @desde THEN 1 ELSE 0 END) FROM RecebiveisOperadora     WHERE TenantId = @tenant
UNION ALL SELECT 'PedidosCompra',           COUNT(*), SUM(CASE WHEN CreatedAt >= @desde THEN 1 ELSE 0 END) FROM PedidosCompra           WHERE TenantId = @tenant
UNION ALL SELECT 'NfesRecebidas',           COUNT(*), SUM(CASE WHEN CreatedAt >= @desde THEN 1 ELSE 0 END) FROM NfesRecebidas           WHERE TenantId = @tenant
UNION ALL SELECT 'Suppliers',               COUNT(*), SUM(CASE WHEN CreatedAt >= @desde THEN 1 ELSE 0 END) FROM Suppliers               WHERE TenantId = @tenant
UNION ALL SELECT 'Transferencias',          COUNT(*), SUM(CASE WHEN CreatedAt >= @desde THEN 1 ELSE 0 END) FROM Transferencias          WHERE TenantId = @tenant
UNION ALL SELECT 'NotasFiscais',            COUNT(*), SUM(CASE WHEN CreatedAt >= @desde THEN 1 ELSE 0 END) FROM NotasFiscais            WHERE TenantId = @tenant
UNION ALL SELECT 'NfseEmitidas',            COUNT(*), SUM(CASE WHEN CreatedAt >= @desde THEN 1 ELSE 0 END) FROM NfseEmitidas            WHERE TenantId = @tenant
UNION ALL SELECT 'PontosFidelidade',        COUNT(*), SUM(CASE WHEN CreatedAt >= @desde THEN 1 ELSE 0 END) FROM PontosFidelidade        WHERE TenantId = @tenant
UNION ALL SELECT 'MetasVendas',             COUNT(*), SUM(CASE WHEN CreatedAt >= @desde THEN 1 ELSE 0 END) FROM MetasVendas             WHERE TenantId = @tenant
UNION ALL SELECT 'FormulasTintometricas',   COUNT(*), SUM(CASE WHEN CreatedAt >= @desde THEN 1 ELSE 0 END) FROM FormulasTintometricas   WHERE TenantId = @tenant
UNION ALL SELECT 'ExternalOrders',          COUNT(*), SUM(CASE WHEN CreatedAt >= @desde THEN 1 ELSE 0 END) FROM ExternalOrders          WHERE TenantId = @tenant
ORDER BY tabela;

-- Formas de pagamento usadas (90 dias): 0 Dinheiro, 1 Débito, 2 Crédito, 3 Pix, 4 A prazo, 5 Haver
SELECT p.PaymentMethod AS forma, COUNT(*) AS pagamentos
FROM SalePayment p JOIN Sales s ON s.Id = p.SaleId
WHERE s.TenantId = @tenant AND s.CreatedAt >= @desde
GROUP BY p.Method;

-- Importações de XML repetidas (EXC-PRD-003): pedidos criados do mesmo XML
SELECT Observacoes, COUNT(*) AS vezes
FROM PedidosCompra
WHERE TenantId = @tenant AND Observacoes LIKE 'Importado automaticamente do XML%'
GROUP BY Observacoes
HAVING COUNT(*) > 1;
```

A tabela de pagamentos se chama `SalePayment`, no singular (não há `DbSet` próprio; nome
confirmado nas migrations), e a coluna é `PaymentMethod` (`SalePayment.cs:13`). Resultado esperado: a coluna "Operação" da matriz passa de
Desconhecida para "Validada" ou "Sem uso" em cada linha com tabela própria, e EXC-PRD-003
recebe a contagem de duplicidades já ocorridas.

---

## 12. Situação dos critérios de produto

| Critério | Situação em 2026-10-26 |
|---|---|
| PRD-1 Fluxos críticos testados com usuários reais | **Não atendido**: nenhuma observação registrada; roteiro em §9.2 |
| PRD-2 Roadmap coerente com o entregue | **Não atendido**: EXC-PRD-001; este documento fornece a base (matriz) |
| PRD-3 Mensagens de erro compreensíveis | Não avaliado (exige observação) |
| PRD-4 Escopo de cada aplicativo definido | **Não atendido**: EXC-PRD-009 |

---

## 13. Registros e próximos passos

### 13.1 Registros criados

| ID | Título | Gravidade |
|---|---|---|
| EXC-PRD-002 | Multi-filial existe só no cadastro | Média |
| EXC-PRD-003 | Importar o mesmo XML de compra duas vezes duplica estoque e contas a pagar | Média |
| EXC-PRD-004 | Custo pela última entrada, sem custo médio nem histórico de movimentação de estoque | Média |
| EXC-PRD-005 | Fechamento de caixa sem valor contado nem diferença | Média |
| EXC-PRD-006 | Devolução sem cliente não registra o reembolso; devolução nunca sai em dinheiro | Média |
| EXC-PRD-007 | Fornecedor só tem nome | Baixa |
| EXC-PRD-008 | Venda a prazo: conta única com vencimento fixo em 30 dias, sem juros nem multa | Baixa |
| EXC-PRD-009 | Papel de cada aplicativo não definido; funções repetidas no WPF e no Portal | Média |
| EXC-PRD-010 | Uso real de cada módulo não medido | A classificar |
| EXC-PRD-011 | Não existe exportação completa dos dados de uma loja | Média |
| EXC-FIS-014 | Motor fiscal atende só o Simples Nacional | A classificar |
| EXC-OPS-008 | Backup local noturno incompatível com o Azure SQL Database | Baixa |

### 13.2 Próximos passos

1. Rodar a consulta da seção 11 e atualizar a coluna "Operação" (fecha parte de EXC-PRD-010).
2. Triagem dos registros novos junto com os Altos da EXC-A6; decidir R0.
3. Decidir o papel de cada aplicativo (EXC-PRD-009) antes de corrigir EXC-ENG-006, porque a
   decisão muda a correção (corrigir a edição no Portal ou retirá-la).
4. Observação na Vila Verde (§9.2) para a segunda passada da EXC-A7 (PRD-1 e PRD-3).
5. Atualizar o roadmap do Portal com base na matriz (EXC-PRD-001).

---

## Histórico do documento

| Data | Versão | Alteração |
|---|---|---|
| 2026-10-26 | 1.0 | Primeira passada da EXC-A7 (inventário funcional pelo código), commit `47a4b0a` |
