# EXC-A10 (parte MKT) — Posicionamento e plano de aquisição

> Programa de Excelência TTSoft · Área MKT (com insumos de VEN, ADM e PRD)
> Criado em: 2026-10-26 · Versão: 1.1 · Base de código: commit `39e811c` (main)
> Documentos relacionados: [PLANO-MESTRE](PLANO-MESTRE.md) · [CRITERIOS-DE-EXCELENCIA](CRITERIOS-DE-EXCELENCIA.md) · [REGISTRO-CENTRAL](REGISTRO-CENTRAL.md) · [EXC-A10-ADMINISTRATIVO-FINANCEIRO](EXC-A10-ADMINISTRATIVO-FINANCEIRO.md)

Esta é a primeira passada sobre a área MKT. Ela **não** valida o público-alvo (isso exige
conversas com lojistas, CRITERIOS MKT-1). Ela organiza o que se sabe, separa o que o código
prova do que é hipótese de mercado, define o que pode e o que não pode ser dito em material
de divulgação e propõe experimentos baratos com critério de sucesso e de interrupção.

---

## 1. Resumo

1. **Nada sobre o cliente ideal está validado.** Há uma loja pagante (Vila Verde), com condição
   especial, e nenhuma entrevista estruturada registrada. O perfil de cliente ideal da seção 4 é
   **hipótese**, construída a partir das restrições do próprio produto e da empresa, não de dados
   de mercado.
2. **O produto tem diferenciais que o código comprova** e que concorrentes de preço baixo não
   mostram nas páginas públicas: crediário com limite que bloqueia a venda, venda que continua
   quando a API não responde, integração com o Mercado Livre e calculadora de materiais para o
   cliente da loja (seção 6). Comprovado no código **não é o mesmo que** comprovado em campo:
   falta medir o que a Vila Verde usa de fato.
3. **Algumas afirmações recomendadas nos relatórios anteriores não podem ser usadas hoje.**
   Em especial "transparência técnica / sistema testado em segurança" (DOC-25): com
   EXC-SEG-001 e EXC-SEG-009 abertos como Críticos, isso é a afirmação mais arriscada possível.
   A seção 6.2 lista as afirmações proibidas e o motivo de cada uma.
4. **A aquisição deve começar como validação, não como campanha.** Com uma pessoa, um cliente,
   margem negativa declarada (EXC-ADM-001), sem contrato (EXC-FIS-002) e banco compartilhado sem
   login restrito (EXC-SEG-001), trazer clientes novos antes das pré-condições da seção 9
   aumenta risco e prejuízo. Entrevistas e demonstrações podem começar já; anúncio pago não.
5. **Os canais propostos são baratos e locais** (indicação, visita, contador, associação
   regional, WhatsApp e Instagram), cada um com um experimento de 4 a 8 semanas, custo em reais e
   em horas, métrica e regra de parada (seção 12). Nenhum número de conversão foi estimado: os
   limiares são **regras de decisão**, não previsões.
6. Achados registrados: o nome "Vila Verde" aparece fixo em mensagens do WPF que qualquer loja
   veria (EXC-MKT-002), e, de passagem, o SPED gerado pelo Portal usa razão social e CNPJ fixos
   (EXC-FIS-012).

---

## 2. Método e regra de rotulagem

| Rótulo | Significado | Pode ir para material de divulgação? |
|---|---|---|
| **FATO-CÓDIGO** | Verificado no repositório em 2026-10-26 (arquivo:linha) | Só se também estiver em uso real ou testado (ver 6.1) |
| **DECLARADO** | Dito por Matheus em 2026-10-22, sem documento | Não, até ter evidência |
| **FONTE PÚBLICA** | Página ou pesquisa pública consultada em 2026-10-26, com link | Sim, citando a fonte |
| **RELATÓRIO ANTERIOR** | Afirmação de DOC-NN, **não** reconferida aqui | Não |
| **HIPÓTESE** | Inferência para ser testada | **Não** |
| **LACUNA** | Dado necessário e ausente | — |

Fontes lidas: relatórios DOC-25 (análise crítica, jul/2026, feita sem o código), DOC-28
(auditoria estratégica, 14 concorrentes) e DOC-32 (estratégia de produto e GTM, preços marcados
como estimativa); código do repositório; registros do programa. Pesquisa pública em 2026-10-26:
[Bling, planos](https://www.bling.com.br/planos-e-precos), [GestãoClick, planos](https://gestaoclick.com.br/planos),
reportagem sobre a pesquisa do Instituto Anamaco ([Estado de Minas, 19/02/2026](https://www.em.com.br/mundo-corporativo/2026/02/7358033-vendas-digitais-avancam-no-varejo-da-construcao.html)),
[B2B Stack: Viasoft Construshow](https://www.b2bstack.com.br/product/viasoft-construshow),
[Una ERP](https://www.unasistemas.com.br/erp-para-materiais-de-construcao/),
[TOTVS Home Center](https://www.totvs.com/varejo/home-center/).
A busca por preços de sistemas verticais de material de construção **não achou preço público**
de nenhum deles; a maioria vende "sob consulta".

---

## 3. O que se sabe do mercado

| Dado | Valor | Rótulo | Observação |
|---|---|---|---|
| Lojas de material de construção no Brasil | ~160 mil (160.627 em 2025, base RAIS; o Sistema Anamaco diz representar 159 mil) | FONTE PÚBLICA (via reportagem) | Confirmar no relatório "Cenário do Varejo de Material de Construção" do Instituto Anamaco antes de citar o número exato |
| Lojas que usam WhatsApp para vender e se relacionar | ~97% | FONTE PÚBLICA (pesquisa Anamaco, ~2 mil lojistas, dados de 2025) | Canal de contato e de demonstração óbvio |
| Instagram entre pequenas e médias | ~95% (o texto também cita 92%) | FONTE PÚBLICA | LinkedIn, TikTok e YouTube ~1% a 3%: **pouco útil para alcançar o dono da loja** |
| Faturamento em loja física | ~67% | FONTE PÚBLICA | O balcão segue sendo o centro; reforça PDV e crediário |
| E-commerce entre pequenas e médias | ~20% | FONTE PÚBLICA | A integração com marketplace é diferencial para uma minoria |
| Quantas dessas lojas têm 1 a 3 caixas, crediário, Simples Nacional, no Paraná | **Desconhecido** | LACUNA | É o tamanho real do público-alvo. Fonte possível: RAIS/CNAE 4744 por município + Acomac-PR |
| Que sistema as lojas pequenas da região usam hoje | **Desconhecido** | LACUNA | O concorrente real. Perguntar em toda entrevista (seção 12, X1) |
| Quanto pagam hoje | **Desconhecido** | LACUNA | EXC-VEN-001 |
| Quem decide a compra | Dono, em geral; filho mais digital influencia | RELATÓRIO ANTERIOR (DOC-25) | Verificar nas entrevistas |

Observação do código: o município padrão do SPED é `4106902` (Curitiba),
`ERP.Api/Controllers/FiscalController.cs:105`, e os relatórios anteriores situam a operação em
Curitiba e São José dos Pinhais. A localização da Vila Verde não consta nos documentos do
programa (lacuna pequena, fácil de fechar).

---

## 4. Perfil do cliente ideal (HIPÓTESE)

Construído a partir das **restrições conhecidas** do produto e da empresa, porque não há dado de
mercado. Cada critério diz de onde vem.

| Critério | Hipótese de cliente ideal | De onde vem |
|---|---|---|
| Porte | Loja independente, 1 loja (até 3 filiais), 1 a 3 caixas, até ~10 usuários | Vila Verde tem 3 PCs (confirmação 4); o suporte é de uma pessoa (EXC-ADM-002) |
| Venda | Balcão, com **crediário/fiado** relevante no faturamento | O diferencial mais forte do código (seção 6) |
| Fiscal | Simples Nacional, já emite NFC-e | SPED EFD sem Bloco E (EXC-FIS-006) não serve a quem precisa entregar a EFD completa |
| Volume fiscal | Até ~500 NFC-e/mês, **ou** aceita pagar o excedente de notas | Com 1.500 NFC-e/mês a margem por cliente é negativa se a TTSoft pagar a Focus (EXC-ADM-003) |
| Infraestrutura | PCs Windows no caixa; internet que às vezes cai | PDV é WPF; a venda continua sem API, mas precisa sincronizar depois |
| Geografia | Raio em que o responsável faz implantação e suporte presencial (Curitiba e região, a confirmar) | Uma pessoa; troca de sistema exige presença |
| Situação atual | Sistema local antigo, planilha ou caderno; incômodo com fiado, NFC-e ou falta de relatório | Hipótese de dor (seção 5) |
| Decisor | Dono acessível, que decide sem comitê | Ciclo curto; sem processo de compra formal |

**Fora do cliente ideal agora (anti-perfil):**

- Redes e home centers (mais de 3 lojas, comitê de compra, exigência de integração com ERP corporativo).
- Lojas que exigem **TEF integrado** (não há integração no código; busca por `TEF`/`PayGo` em 2026-10-26 não encontrou nada).
- Lojas que dependem de **app no celular** para vender (Mobile pausado, EXC-PRD-001).
- Lojas no Lucro Presumido/Real que precisam do SPED completo pelo sistema (EXC-FIS-006).
- Alto volume de notas sem aceitar repasse (EXC-ADM-003).
- Fora do raio de atendimento presencial.

O perfil vira **Fato** quando pelo menos 5 lojas com essas características tiverem confirmado,
em entrevista, a dor principal e a disposição de pagar (critério MKT-1).

---

## 5. Problemas que o produto resolve (HIPÓTESES de dor)

| # | Dor do lojista (hipótese) | O que o produto faz (FATO-CÓDIGO) | Como confirmar |
|---|---|---|---|
| D1 | Fiado sai do controle: cliente compra acima do que deveria, ninguém sabe quem deve quanto | Crediário com contas a receber, eventos e limite de crédito que **bloqueia** a venda (`ERP.Application/Services/SaleService.cs:230`, `LimiteCreditoExcedidoException`); haver do cliente (`MovimentosHaver`) | Entrevista: "como controla o fiado hoje? quanto tem a receber? quanto perdeu no último ano?" |
| D2 | Internet cai e o caixa para | Venda gravada em fila local quando a API não responde e sincronizada automaticamente a cada minuto (`ERP.WPF/ViewModels/FinalizarVendaViewModel.cs:637-675`, `ERP.WPF/MainWindow.xaml.cs:189-219`) | Entrevista: "o que acontece quando a internet cai?" + teste em campo (seção 6.1) |
| D3 | NFC-e e notas dão trabalho e medo de erro | Emissão via Focus NFe; importação do XML de compra cria produto e estoque (`NfeImportView`, `NfesRecebidas`) | Entrevista; contagem de notas rejeitadas na Vila Verde |
| D4 | Dono não sabe se a loja dá lucro | DRE, fluxo de caixa, margem, curva ABC no WPF; BI e dashboard no Portal | Entrevista: "que número você olha toda semana?" |
| D5 | Balconista perde tempo calculando material para o pedreiro | Calculadora de materiais pública, sem cadastro, que gera orçamento (`ERP.Portal/Pages/CalculadoraPublica.razor`, `CalculadoraController` com `gerar-orcamento`) | Entrevista + uso real da calculadora (logs) |
| D6 | Vender no Mercado Livre sem planilha e sem vender o que não tem | Integração com pedidos, estoque reservado e mapeamento de SKU (INV-011) | Só para os ~20% com e-commerce (seção 3) |
| D7 | Trocar de sistema dá medo de perder cadastro e fiado | Importação legada por pasta (`ILegacyImportService.ImportFromFolderAsync`); importação de XML de compra | **Não verificado** o que a importação legada aceita, nem se traz saldos de fiado: lacuna antes de prometer |

Dores **não** atendidas que podem aparecer nas entrevistas e devem ser registradas, não
prometidas: TEF, app de vendedor, SPED completo, multiempresa com contabilidade.

---

## 6. Diferenciais comprováveis

### 6.1 O que pode ser afirmado, e com que cuidado

Uma afirmação só vai para material de divulgação quando tem **código** e **uso ou teste real**
(CRITERIOS MKT-2). Hoje quase tudo está só no primeiro degrau.

| Afirmação candidata | Código | Uso/teste real | Pode usar hoje? |
|---|---|---|---|
| "O fiado tem limite: o sistema não deixa vender acima do limite do cliente" | Sim (`SaleService.cs:230`) | Lacuna (a Vila Verde usa limite?) | **Sim, em demonstração**; em material depois de confirmar uso |
| "Se a internet cair, o caixa continua vendendo e sincroniza depois" | Sim (fila local + sincronização a cada minuto) | **Lacuna**: não há teste em campo registrado; a NFC-e em contingência tem defeitos abertos (EXC-FIS-007, EXC-FIS-009) | **Só "a venda continua"**, nunca "a nota sai normalmente sem internet". Fazer um teste de queda na Vila Verde antes de usar em material |
| "Integração com o Mercado Livre" | Sim (INV-011) | RELATÓRIO ANTERIOR (DOC-32 cita compra real validada) | Sim, em demonstração; confirmar se algum cliente usa |
| "Calculadora de materiais para o cliente da sua loja" | Sim | Lacuna | Sim, em demonstração |
| "Importe as notas de compra e o estoque se monta" | Sim | Lacuna | Sim, em demonstração, com XML do próprio lojista |
| "Pix com confirmação automática" | Parcial: polling opcional (OpenPix, EFÍ, PagBank) se houver chave; senão confirmação manual (`ERP.WPF/Helpers/PixPollingService.cs`) | Lacuna | Só como "Pix integrado com os provedores X, Y, Z", se a loja tiver conta neles |
| "Usado na loja Vila Verde desde <data>" | — | Fato, 1 loja | **Só com autorização escrita da loja** |
| Catálogo público, autoatendimento do cliente, entregas com motorista, metas e painel de TV, multi-filial, tintométrico | Sim (páginas e controllers existem) | Lacuna | Em demonstração, se funcionar no dia; não em material |

### 6.2 O que não pode ser afirmado (e por quê)

| Afirmação | Motivo | Até quando |
|---|---|---|
| "Seguro", "seus dados protegidos", "testado em segurança" (sugestão de DOC-25) | EXC-SEG-001 e EXC-SEG-009 Críticos: o WPF usa credencial de administrador e o servidor aceita conexão de qualquer IP | Até o login restrito e a troca de senha (E1 a E6) estarem concluídos |
| "Em conformidade com a LGPD" | Não há termos, política de privacidade nem contrato (EXC-FIS-002) | Até FIS-4 atendido |
| "TEF integrado" / "maquininha integrada" | Não existe no código | — |
| "Funciona 100% offline" | Só a venda tem fila local; consultas e cadastros dependem de rede; contingência fiscal com defeitos | Teste em campo + EXC-FIS-009 corrigido |
| "Gera o SPED" | Bloco E ausente (EXC-FIS-006); SPED do Portal com empresa fixa (EXC-FIS-012) | EXC-A4 |
| "App para celular" | Mobile pausado (EXC-PRD-001) | — |
| "Migração completa do sistema antigo, inclusive o fiado" | Escopo da importação legada não verificado | Verificação da importação |
| Números de resultado ("reduz perdas em X%") | Nenhuma medição existe | Medição antes/depois em um cliente |
| Comparações nominais com concorrentes ("melhor que o Bling") | Risco jurídico e de credibilidade; os dados públicos são parciais | Não usar; comparar por necessidade ("tem fiado com limite?"), não por marca |

Base legal da cautela: o Código de Defesa do Consumidor proíbe publicidade enganosa (art. 37).
Para um fornecedor de uma pessoa, uma promessa quebrada também corre a cidade (DOC-32).

### 6.3 Identidade do produto

Hoje o produto aparece com três nomes: **ConstruTTor ERP** (login do WPF,
`ERP.WPF/Views/LoginView.xaml:4`), **TTSoft ERP — Portal Gerencial** (`ERP.Portal/wwwroot/index.html:6`,
`manifest.json`) e **Vila Verde** em 8 títulos de mensagem e uma dica de tela do WPF que
qualquer loja veria (EXC-MKT-002; corrigido no código em 2026-10-26, com mais 4 ocorrências em
documentos que chegam ao cliente final, aguardando publicação). Antes de mostrar o sistema para uma segunda loja, o nome da
loja piloto precisa sair da interface. A escolha da marca de produto (ConstruTTor) e da marca
da empresa (TTSoft) é decisão de Matheus; não foi verificado se alguma delas tem registro no INPI
(lacuna).

---

## 7. Concorrentes

Concorrente real é o que o lojista do perfil usa hoje, e isso é **lacuna** (seção 3). A tabela
mostra referências públicas, não participação de mercado.

| Concorrente | Foco | Preço público (2026-10-26) | Crediário/fiado | Sem internet | Fonte |
|---|---|---|---|---|---|
| Bling | PME horizontal, e-commerce e marketplace | R$ 60 a R$ 1.250+/mês (página exibia desconto promocional) | Não aparece na página de planos | **Declara não ter contingência nem modo offline** (FAQ) | [bling.com.br](https://www.bling.com.br/planos-e-precos) |
| GestãoClick | PME horizontal; cita material de construção entre segmentos | R$ 183 a R$ 583/mês mensal; R$ 119 a R$ 379/mês no anual; NF no plano de entrada só como adicional | Não aparece na página | Não informado | [gestaoclick.com.br](https://gestaoclick.com.br/planos) |
| Viasoft Construshow | Vertical construção, desktop, desde 1990 | Sob consulta | Não verificado aqui | Não verificado | [B2B Stack](https://www.b2bstack.com.br/product/viasoft-construshow) |
| Una ERP | Vertical material de construção | Sob consulta | Cita crédito do cliente e limite | Não informado | [unasistemas.com.br](https://www.unasistemas.com.br/erp-para-materiais-de-construcao/) |
| TOTVS Varejo Home Center | Médio e grande porte | Sob consulta | Não verificado | Não verificado | [totvs.com](https://www.totvs.com/varejo/home-center/) |
| CISS, Sankhya, Alterdata, Omie, Conta Azul e outros | Ver DOC-28 (14 concorrentes) | — | — | — | RELATÓRIO ANTERIOR, não reconferido |
| Sistema local antigo / planilha / caderno | Provável concorrente real da loja pequena | — | Caderno é o "crediário" | Funciona sem internet | HIPÓTESE |

Leitura (HIPÓTESE): o espaço do ConstruTTor está entre os horizontais baratos, que não mostram
crediário de balcão nem operação sem internet, e os verticais de venda consultiva, que não
publicam preço e miram portes maiores. O argumento não é "moderno contra antigo" (DOC-32 já
corrigiu isso): é **"feito para o balcão com fiado da loja pequena"**. Isso só se sustenta se
as entrevistas mostrarem que o fiado é dor de verdade.

**Lacuna de concorrência:** preço e recursos de 3 a 5 sistemas que as lojas da região usam.
Coletar nas entrevistas (pergunta direta) e, quando possível, pedindo proposta como lojista.

---

## 8. Posicionamento proposto (HIPÓTESE a testar)

> **Para** donos de lojas de material de construção de 1 a 3 caixas que vendem fiado no balcão,
> **o ConstruTTor é** o sistema de loja que **controla o crediário com limite e não para o caixa
> quando a internet cai**, com NFC-e e Mercado Livre no mesmo lugar, **implantado e atendido
> pessoalmente** por quem desenvolve o sistema.

Mensagens de apoio, na ordem de teste: (1) fiado sob controle; (2) caixa que não para;
(3) atendimento direto com quem fez o sistema; (4) nota fiscal sem dor de cabeça.

Variantes a testar nas entrevistas (X1) e no conteúdo (X4), para descobrir qual dor puxa
conversa: **A** fiado; **B** internet; **C** atendimento próximo; **D** lucro da loja.

Sobre o "feito por quem atende o pedreiro às 7h da manhã" (DOC-25): depende de ser verdade.
**Não consta nos documentos do programa** que o responsável trabalhe no balcão. Se for verdade,
é o argumento mais difícil de copiar; se não for, não usar.

O atendimento por uma pessoa é ao mesmo tempo vantagem (proximidade) e objeção (continuidade,
EXC-ADM-002). A resposta honesta a "e se você sumir?" precisa existir antes da primeira venda
nova: contrato com prazo de aviso, exportação dos dados do cliente e procedimento documentado
(ligado a EXC-FIS-002 e EXC-OPS-005).

---

## 9. Pré-condições para trazer clientes novos

Separar o que pode começar agora do que deve esperar:

| Atividade | Pode começar já? | Depende de |
|---|---|---|
| Entrevistas de problema (sem vender) | **Sim** | Nada |
| Demonstrações com dados de exemplo | **Sim**, depois de tirar "Vila Verde" das mensagens (EXC-MKT-002) e preparar um tenant de demonstração **sem CNPJ real** (lição de EXC-FIS-008) | EXC-MKT-002 |
| Proposta de preço | Sim, como teste (X5) | Decisão sobre repasse de notas (EXC-ADM-003) |
| Implantar uma segunda loja | **Não** | Login restrito do WPF (EXC-SEG-001, E1 a E4), CNPJ do emitente por tenant (EXC-FIS-013), contrato e termos (EXC-FIS-002), preço decidido |
| Anúncio pago, material impresso, feira | **Não** | Tudo acima + pelo menos 1 canal orgânico com resultado medido |

Motivo: cada loja nova entra no mesmo banco onde hoje qualquer PC tem acesso de administrador a
todos os clientes. Uma segunda loja muda o risco de "dados de uma loja" para "dados de uma loja
expostos a outra".

---

## 10. Canais de aquisição

Ordenados por custo e por adequação a uma pessoa. Todos são hipóteses.

| # | Canal | Por que (hipótese) | Custo | Quando |
|---|---|---|---|---|
| C1 | **Indicação e visita local**, com a Vila Verde como referência | Confiança pessoal pesa na decisão (DOC-25); loja vizinha vê a outra usando | Horas + deslocamento | Já (entrevista), demo depois de EXC-MKT-002 |
| C2 | **Contadores** que atendem lojas de material de construção | Veem o problema fiscal e o de crediário de várias lojas; um contador alcança várias lojas | Horas | Já |
| C3 | **Associação regional** (Acomac da região, não a Anamaco nacional) | Proximidade com lojistas; acessível a fornecedor pequeno (DOC-25) | Baixo a médio (associação, evento) | Depois de C1/C2 darem sinal |
| C4 | **WhatsApp e Instagram** com conteúdo de dor | 97% das lojas no WhatsApp e ~95% das PMEs no Instagram (seção 3) | Horas | Já, com as regras da seção 11 |
| C5 | Busca paga local (Google) | Quem procura "sistema para loja de material de construção" está comprando | R$ (teto definido em X6) | Só depois da seção 9 |
| C6 | Revendas de equipamento de PDV (leitor, impressora, balança) | Relação de confiança já existe com o lojista | Comissão | Depois de 3 clientes |
| — | Não fazer agora: feira nacional, Anamaco nacional, LinkedIn/TikTok/YouTube como canal principal, anúncio amplo em redes | Custo alto ou público errado (LinkedIn/TikTok ~3%, YouTube ~1%) | — | — |

---

## 11. Estratégia de conteúdo

**Princípio:** conteúdo de dor e de rotina de balcão, não lista de funcionalidades. Toda
afirmação sobre o produto precisa estar na coluna "Pode usar hoje? Sim" da seção 6.1.

| Pilar | Exemplos de tema | Mensagem do produto (só se autorizada em 6.1) |
|---|---|---|
| Fiado sob controle | "Como saber quanto a loja tem a receber"; "limite de crédito por cliente sem constrangimento" | Limite que bloqueia a venda |
| Caixa que não para | "O que fazer quando a internet cai no meio da venda" | "A venda continua" (sem prometer a nota) |
| Nota fiscal sem susto | "Os erros de NFC-e que mais travam a venda" (ex.: aspas na descrição, EXC-FIS-011) | Importação de XML |
| Balcão rápido | "Calcular material para o pedreiro em 1 minuto" | Calculadora |
| Bastidores honestos | Como é a implantação, quanto tempo leva, o que muda na rotina | Atendimento direto |

**Formato e cadência sustentáveis para uma pessoa:** 2 publicações por semana no Instagram,
1 vídeo curto por semana gravado no balcão (com autorização da loja e **sem dados de clientes na
tela**: nomes, CPF, valores de fiado), lista de transmissão no WhatsApp para os contatos que
pedirem. Teto de **3 horas por semana** para conteúdo; se passar, cortar formato, não horas de
suporte.

**Isca para gerar contato (lead):** "Diagnóstico do fiado", uma conversa de 20 minutos ou
uma planilha simples para o lojista ver quanto tem a receber e há quanto tempo. Entregue por
WhatsApp. Serve a dois fins: gera conversa qualificada e coleta dado de dor para X1.

---

## 12. Experimentos de baixo custo

Regras comuns: cada experimento tem dono (Matheus), data de início e fim, e é registrado na
planilha de funil (seção 13). Os limiares são **regras de decisão escolhidas antes de começar**,
não previsões. Ajustar os limiares depois de ver o resultado invalida o experimento.

| ID | Hipótese | Ação | Custo | Duração | Métrica principal | Sucesso | Interromper / mudar se |
|---|---|---|---|---|---|---|---|
| **X1** | Fiado é a dor principal do perfil (seção 4) | 10 entrevistas de problema, 30 min, roteiro fixo (dor, sistema atual, quanto paga, quem decide, o que faria trocar). **Sem demonstração** | R$ 0 + ~15 h | 4 semanas | Nº de lojas que citam a mesma dor sem ser estimuladas | ≥ 6 de 10 citam a mesma dor | < 3 de 10 citam fiado → testar variantes B, C, D como mensagem principal; < 5 entrevistas marcadas em 4 semanas → o problema é acesso, mudar para C2 |
| **X2** | A referência da Vila Verde abre portas | Visitar 15 lojas do perfil no raio de atendimento, citando a Vila Verde (com autorização) | Deslocamento (registrar R$) + ~12 h | 4 semanas | Demonstrações agendadas | ≥ 4 demos | < 2 demos após 15 visitas |
| **X3** | Contadores indicam lojas | Conversar com 8 escritórios que atendem material de construção; oferecer o "diagnóstico do fiado" para os clientes deles | R$ 0 + ~8 h | 6 semanas | Indicações que estão no perfil | ≥ 2 indicações qualificadas | 0 indicações após 8 conversas |
| **X4** | Conteúdo de dor gera conversa no WhatsApp | Seção 11, com uma variante de mensagem por quinzena (A, B, C) | R$ 0 + ≤ 3 h/semana | 6 semanas | Conversas iniciadas por lojista do perfil | ≥ 5 conversas qualificadas | < 2 qualificadas em 6 semanas, **ou** passou de 3 h/semana por 2 semanas seguidas |
| **X5** | O preço comporta R$ 89,90 a R$ 99,90 (ou mais) sem condição especial | Em cada demo, apresentar um preço fixo definido antes (e a regra de notas excedentes) e registrar a reação: aceita / negocia / recusa por preço | R$ 0 | Junto com X2/X3 | % de propostas recusadas **por preço** | Decisão de preço com ≥ 8 reações registradas | > 60% recusas por preço → rever preço ou perfil; < 10% → testar preço mais alto nas próximas |
| **X6** | Busca paga local traz lojista do perfil a custo aceitável | **Só após a seção 9.** Google Ads de busca, região de atendimento, termos de compra ("sistema para loja de material de construção" + cidade) | Teto **R$ 300** em 30 dias | 30 dias | Custo por lead qualificado | Custo por cliente pagante ≤ teto provisório (abaixo) | R$ 150 gastos sem nenhum lead qualificado → parar; > 70% dos leads fora do perfil → parar |
| **X7** | Associação regional dá acesso a vários donos de uma vez | 1 apresentação ou participação em reunião da Acomac | Registrar custo de associação/evento | 1 evento | Contatos do perfil por evento | ≥ 3 contatos qualificados | Custo > R$ 500 por contato qualificado, ou 0 contatos |
| **X8** | "A venda continua sem internet" é verdade em campo | Na Vila Verde, fora do horário: desligar a rede, vender, religar, conferir sincronização e nota | R$ 0 + ~2 h | 1 dia | Vendas sincronizadas sem intervenção | 100% sincronizadas, nenhuma duplicada | Qualquer venda perdida ou duplicada → afirmação continua proibida e vira registro ENG/FIS |

**Teto provisório de custo de aquisição (premissa, trocar quando houver dado):** não gastar
mais de **3 mensalidades (~R$ 270)** em mídia e deslocamento por cliente pagante. É um limite de
prudência, não um cálculo: a margem por cliente não é conhecida (EXC-ADM-003), e com margem
negativa qualquer custo de aquisição aumenta o prejuízo. Recalcular com o modelo da parte ADM
quando o volume de notas e a fatura do Azure existirem.

**Regras gerais de interrupção (valem para qualquer campanha):**

1. Mais de 70% dos contatos fora do perfil por 2 semanas → a mensagem ou o canal está errado.
2. Mais de **4 horas por lead qualificado** somando todos os canais → o canal não cabe na
   capacidade de uma pessoa.
3. Mais de **2 pilotos ou implantações ao mesmo tempo** → pausar aquisição até um terminar.
4. Qualquer incidente com dados ou fiscal em cliente → pausar aquisição até a causa ser tratada.
5. Nenhum cliente pagante novo depois de X1 a X5 completos (cerca de 3 meses) → rever
   posicionamento e perfil antes de gastar dinheiro (X6 não começa).

---

## 13. Indicadores e registro do funil

Não há funil nem canal medido hoje (EXC-MKT-004). Uma planilha simples resolve; não é tarefa de
código.

**Campos por contato:** data, loja, cidade, nº de caixas, regime (se souber), sistema atual,
canal de origem (C1 a C7), está no perfil? (s/n e por quê), dor citada, etapa, motivo de perda,
preço apresentado e reação, horas gastas.

**Etapas:** contato → lead qualificado (no perfil) → entrevista ou diagnóstico → demonstração →
proposta → piloto → cliente pagante → (perdido, com motivo).

| Indicador | Definição | Frequência | Para quê |
|---|---|---|---|
| Leads qualificados por canal | Contatos no perfil, por origem | Semanal | Escolher canal (MKT-3) |
| Conversão por etapa | % que passa de uma etapa para a próxima, por canal | Mensal | Achar onde o funil quebra |
| Horas por lead qualificado e por cliente | Soma das horas registradas | Mensal | Regra de interrupção 2; capacidade (EXC-ADM-002) |
| Custo por cliente pagante | R$ de mídia, deslocamento e eventos ÷ clientes novos | Por cliente | Teto provisório |
| Motivos de perda | Contagem por motivo (preço, TEF, mobile, confiança, timing) | Mensal | Produto e preço |
| Dor citada | Contagem por tema (A, B, C, D, outra) | Ao fim de X1 | Validar o posicionamento (MKT-1) |
| Tempo até o "sim" e até a implantação | Dias | Por cliente | Planejar capacidade |

Nenhuma meta numérica foi definida além dos limiares dos experimentos: meta sem base histórica
seria número inventado (PLANO-MESTRE §8).

---

## 14. Dados a coletar (em ordem de valor)

| # | Dado | De onde | Fecha qual dúvida | Registro |
|---|---|---|---|---|
| 1 | Por que a Vila Verde escolheu o ConstruTTor, o que usava antes, o que mais usa e o que falta | Conversa de 30 min com o dono da loja | Primeira evidência de dor e de diferencial | EXC-MKT-001 |
| 2 | Que módulos a Vila Verde usa de fato (consulta abaixo) | Banco, somente leitura | Quais diferenciais estão em uso real (seção 6.1) | EXC-MKT-001 |
| 3 | Entrevistas X1 | Lojistas | Perfil e dor | EXC-MKT-001 |
| 4 | Autorização da Vila Verde para ser citada e filmada | Dono da loja, por escrito | Uso como referência | — |
| 5 | Se o responsável trabalha no balcão, e onde fica a Vila Verde | Matheus | Argumento da seção 8; raio de atendimento | — |
| 6 | Existe site, perfil de Instagram ou material da TTSoft/ConstruTTor? | Matheus | Ponto de partida de C4; MKT-2 | EXC-MKT-004 |
| 7 | Registro de marca (INPI) de "ConstruTTor" e "TTSoft" | Busca no INPI | Risco de ter de trocar de nome depois de investir | — |
| 8 | Situação da proposta à Eldorado (DOC-29) e de outros contatos | Matheus | Funil existente | EXC-VEN-001 |
| 9 | O que a importação legada aceita e se traz saldos de fiado | Teste com uma pasta de exemplo | Afirmação de migração (D7) | — |

**Consulta de uso por módulo, últimos 90 dias** (somente leitura; filtrar o tenant da Vila
Verde e excluir os tenants de teste, conforme ROTEIRO-CONFIRMACOES-A0):

```sql
DECLARE @desde datetime2 = DATEADD(day, -90, SYSUTCDATETIME());
SELECT 'Vendas' AS modulo, TenantId, COUNT(*) AS qtd FROM Sales WHERE CreatedAt >= @desde GROUP BY TenantId
UNION ALL SELECT 'Pagamento a prazo', s.TenantId, COUNT(*) FROM SalePayment p JOIN Sales s ON s.Id = p.SaleId
          WHERE p.PaymentMethod = 4 AND s.CreatedAt >= @desde GROUP BY s.TenantId
UNION ALL SELECT 'Pagamento com haver', s.TenantId, COUNT(*) FROM SalePayment p JOIN Sales s ON s.Id = p.SaleId
          WHERE p.PaymentMethod = 5 AND s.CreatedAt >= @desde GROUP BY s.TenantId
UNION ALL SELECT 'Pagamento Pix', s.TenantId, COUNT(*) FROM SalePayment p JOIN Sales s ON s.Id = p.SaleId
          WHERE p.PaymentMethod = 3 AND s.CreatedAt >= @desde GROUP BY s.TenantId
UNION ALL SELECT 'Clientes com limite', TenantId, COUNT(*) FROM Customers WHERE LimiteCredito > 0 GROUP BY TenantId
UNION ALL SELECT 'Orçamentos', TenantId, COUNT(*) FROM Orcamentos WHERE CreatedAt >= @desde GROUP BY TenantId
UNION ALL SELECT 'Entregas', TenantId, COUNT(*) FROM Entregas WHERE CreatedAt >= @desde GROUP BY TenantId
UNION ALL SELECT 'Pedidos marketplace', TenantId, COUNT(*) FROM ExternalOrders WHERE CreatedAt >= @desde GROUP BY TenantId
UNION ALL SELECT 'NF-e de compra importadas', TenantId, COUNT(*) FROM NfesRecebidas WHERE CreatedAt >= @desde GROUP BY TenantId
UNION ALL SELECT 'Fórmulas tintométricas', TenantId, COUNT(*) FROM FormulasTintometricas GROUP BY TenantId
UNION ALL SELECT 'Transferências', TenantId, COUNT(*) FROM Transferencias WHERE CreatedAt >= @desde GROUP BY TenantId
ORDER BY TenantId, modulo;
```

Os nomes de coluna seguem as entidades (`BaseEntity.CreatedAt`, `PaymentMethod` 3 = Pix,
4 = A prazo, 5 = Haver em `ERP.Domain/Enums/Enums.cs`; `Customer.LimiteCredito`, em que
0 significa sem limite). Ajustar se algum nome de tabela divergir no banco.

---

## 15. Registros propostos e próximos passos

Registros novos no [REGISTRO-CENTRAL](REGISTRO-CENTRAL.md) (diagnóstico, não tarefa):

| ID | Título | Natureza | Gravidade |
|---|---|---|---|
| EXC-MKT-001 | Público-alvo e proposta de valor sem validação com clientes | Lacuna | A classificar |
| EXC-MKT-002 | Nome da loja piloto fixo na interface do WPF | Fato | Baixa |
| EXC-MKT-003 | Relatórios anteriores recomendam afirmações de divulgação que o estado atual não sustenta | Fato | Média |
| EXC-MKT-004 | Nenhum canal de aquisição ou funil medido | Lacuna | A classificar |
| EXC-FIS-012 | SPED gerado pelo Portal usa razão social e CNPJ fixos | Fato | Média |

Decisões que são de Matheus (não técnicas):

1. Aprovar ou ajustar o **perfil de cliente ideal** e o **anti-perfil** (seção 4) como hipótese
   de trabalho para X1.
2. Definir o **raio de atendimento presencial**.
3. Escolher o **preço fixo** a apresentar em X5 e a regra de notas excedentes (ligada a
   EXC-ADM-003).
4. Confirmar a **marca** de produto e de empresa a usar em material (seção 6.3).
5. Aceitar as **regras de interrupção** da seção 12 antes de começar qualquer experimento.

Ordem sugerida: dados 1 e 2 da seção 14 → X8 (teste de queda) → X1 e X3 em paralelo →
corrigir EXC-MKT-002 e preparar o tenant de demonstração → X2 e X5 → X4 → avaliação ao fim de
~3 meses (regra 5) → X6 e X7 só se as pré-condições da seção 9 estiverem atendidas.

Fora do escopo desta passada: entrevistas, testes de preço, verificação de recursos dos
concorrentes além das páginas públicas, e qualquer projeção de clientes ou receita. O que não
está nas tabelas acima **não foi estimado**.

---

## Histórico do documento

| Data | Versão | Alteração |
|---|---|---|
| 2026-10-26 | 1.0 | Criação: mercado, perfil de cliente ideal, diferenciais comprováveis e proibidos, concorrentes, posicionamento, canais, conteúdo, experimentos X1 a X8 e indicadores |
| 2026-10-26 | 1.1 | EXC-MKT-002 corrigido no código (em validação); EXC-FIS-013 (CNPJ do emitente fixo) passa a ser pré-condição para uma segunda loja (seção 9) |
