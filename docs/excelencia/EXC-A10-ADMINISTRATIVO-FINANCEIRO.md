# EXC-A10 (parte ADM) — Avaliação administrativa e financeira

> Programa de Excelência TTSoft · Área ADM (com insumos de VEN)
> Criado em: 2026-10-26 · Versão: 1.0 · Base de código: commit `624abef` (main)
> Documentos relacionados: [PLANO-MESTRE](PLANO-MESTRE.md) · [CRITERIOS-DE-EXCELENCIA](CRITERIOS-DE-EXCELENCIA.md) · [REGISTRO-CENTRAL](REGISTRO-CENTRAL.md) · modelo: [EXC-ADM-modelo-cenarios.py](EXC-ADM-modelo-cenarios.py)

Esta é a primeira passada sobre a área ADM. Ela **não** substitui a conferência de faturas
(EXC-ADM-001). Ela organiza o que se sabe, separa o que é dado do que é premissa, mostra quais
números mexem no resultado e lista o que precisa ser coletado.

---

## 1. Resumo

1. **Hoje a operação dá prejuízo mensal declarado de R$ 64,00** (receita R$ 119,90, custo
   R$ 183,90), antes de impostos e do tempo do responsável. São números declarados em
   2026-10-22, sem fatura. Dos R$ 119,90, R$ 30,00 são do OutfiTT (outro produto).
2. **O resultado do ConstruTTor por cliente depende mais do volume fiscal (notas por mês) do que
   da infraestrutura.** Pela tabela pública da Focus NFe, a nota fiscal é o custo variável que
   pode passar do preço da mensalidade. Esse volume por loja **não está medido** em nenhum
   documento do programa. É o dado mais valioso a coletar (seção 8, item 1).
3. **Se a TTSoft pagar as notas dos clientes**, com 500 NFC-e/mês por loja o modelo fecha com
   margem positiva a R$ 89,90. Com 1.500/mês fecha **negativo em qualquer escala testada**
   (10 a 300 clientes): o preço de equilíbrio sobe para cerca de R$ 108 a R$ 134, conforme o
   imposto. Se o contrato repassar as notas excedentes ao cliente, esse risco some. O repasse é
   uma **decisão comercial pendente**, não técnica.
4. **Há lacunas estruturais**, não só de números: não se sabe a forma jurídica e o regime
   tributário da TTSoft, não há contrato com clientes (EXC-FIS-002), não se sabe como a
   mensalidade é cobrada, e não há custo por cliente medido (o banco é compartilhado).
5. **Nenhum custo, faturamento ou margem real foi inventado.** Os cenários usam só três tipos
   de valor, sempre rotulados: *declarado*, *tabela pública* e *premissa* (seção 2).

---

## 2. Método e regra de rotulagem

| Rótulo | Significado | Pode ser tratado como real? |
|---|---|---|
| **DECLARADO** | Dito por Matheus em 2026-10-22; sem fatura | Sim, com confiança Média |
| **TABELA PÚBLICA** | Preço de lista do fornecedor, consultado em 2026-10-26 | Não: é referência, não o contrato da TTSoft |
| **PREMISSA** | Valor de cenário para ilustrar | **Não.** Existe para ser trocado por dado real |
| **LACUNA** | Dado necessário e ausente | — |

Fontes consultadas: [Focus NFe, planos](https://focusnfe.com.br/precos/) e [Asaas, taxas](https://www.asaas.com/precos-e-taxas)
(ambas em 2026-10-26); código do repositório; documentos do programa. **Não foi possível obter
preço do Azure** (calculadora dinâmica, sem fonte confiável para a região Brazil South). Por isso
a infraestrutura Azure entra como premissa e **todas as conclusões abaixo foram escritas para
não depender dela** (ver orçamento de infra por cliente, seção 6.3).

Reprodutibilidade: `python -I docs/excelencia/EXC-ADM-modelo-cenarios.py` imprime todas as
tabelas deste documento. As premissas ficam em dicionários no topo do script.

---

## 3. Situação atual (DECLARADO, 2026-10-22)

| Item | Valor | Observação |
|---|---:|---|
| Custo mensal declarado | R$ 183,90 | Azure (2 bancos) + Focus (plano para até 3 CNPJs). Não foi dito se inclui App Service e armazenamento |
| Receita Vila Verde (ConstruTTor) | R$ 89,90 | Condição especial: a loja recebe antes as atualizações. Em agosto citava-se R$ 99,90 + R$ 2.000 de implantação (DOC-34) |
| Receita cliente OutfiTT | R$ 30,00 | Outro produto; condição de primeiro cliente; divide a mesma infraestrutura |
| Receita total | R$ 119,90 | |
| **Resultado mensal** | **−R$ 64,00** | Antes de impostos, de ferramentas e do tempo do responsável |

**Hipótese a conferir (não é fato):** o plano *Start* da Focus (até 3 CNPJs) custa R$ 119,90 na
tabela pública. Se for o plano contratado, sobrariam exatamente R$ 64,00 dos R$ 183,90 para o
Azure inteiro. A coincidência com o déficit é só aritmética (183,90 − 119,90); mas mostra que
**o Azure pode estar subestimado**, porque o código indica mais recursos que "2 bancos" (seção 4).

Pessoas: uma (EXC-ADM-002). Clientes pagantes do ConstruTTor: **um**. Tenants de teste: 4.

---

## 4. Estrutura de custos

### 4.1 Inventário do que gera ou pode gerar custo

"Evidência" é o que o código mostra; **ele não prova que o recurso existe nem quanto custa.**

| # | Item | Tipo | Evidência | Valor conhecido? | Como obter |
|---|---|---|---|---|---|
| 1 | Azure SQL (servidor `erp-ttsoft-server`, bancos do ConstruTTor e do OutfiTT) | Fixo, cresce por camada | `Scripts/CHECKLIST_DEPLOY.md`; EXC-OPS-007 | Parcial (dentro dos R$ 183,90) | Azure Cost Management → exportar custo por recurso, últimos 3 meses |
| 2 | App Service da API do ERP (`erp-ttsoft-api`) | Fixo, cresce por camada | `ERP.WPF/App.xaml.cs:534`; `.github/workflows/ci-cd.yml` (deploy-api) | **Não** (pode não estar nos R$ 183,90) | Cost Management |
| 3 | *Slot* de staging da API | Fixo | `staging.yml`: `slot-name: staging` no app `erp-ttsoft-api`. **Hipótese:** *slots* exigem camada Standard ou superior no App Service; se o *slot* existe, o plano não é o mais barato | **Não** | Portal Azure → App Service → Slots; plano de preços |
| 4 | App Service da API de licenças (outro app) | Fixo | `ERP.WPF/Security/LicenseManager.cs:13`; EXC-OPS-005 | **Não** | Cost Management |
| 5 | Azure Static Web Apps (Portal) | Fixo (há camada gratuita) | `ci-cd.yml` (deploy-portal) | **Não** | Cost Management |
| 6 | Armazenamento Blob: atualizações (`ttsoftupdates`), imagens de produto, fotos de entrega | Variável (GB + saída de rede) | `Scripts/versao.json`; `appsettings.json` (`AzureStorage`); `StorageController.cs:23` (limite de 5 MB por arquivo) | **Não** | Cost Management; tamanho do `ERP.WPF.exe` publicado; contagem de arquivos |
| 7 | Application Insights | Variável (ingestão em GB) | `Program.cs:303-307`: **amostragem desligada** ("queremos 100% dos dados"). Custo cresce linear com requisições, e cada PC sincroniza com a API | **Não** | Application Insights → Uso e custos estimados |
| 8 | Azure SignalR | Variável (conexões) | `Program.cs:440-444`: usado **somente** se `AzureSignalRConnectionString` estiver preenchida. Em `appsettings.json` está vazia; valor em produção desconhecido | **Não** | App Service → Configuração |
| 9 | Focus NFe (emissão fiscal) | Fixo por CNPJ + **variável por nota** | `TenantFiscalConfiguration` (token por tenant); EXC-FIS-004 | Parcial (plano para até 3 CNPJs; valor exato e franquia de notas não confirmados) | Contrato e fatura da Focus; painel (contagem de notas) |
| 10 | Asaas (boleto) | Variável (por boleto recebido) | `AsaasService.cs`; `Program.cs:121`; `appsettings.json`: ambiente `sandbox` | **Não** (uso real em produção desconhecido) | Painel Asaas; configuração do App Service |
| 11 | Certificado digital A1 do emitente | Por CNPJ do cliente | `appsettings.json` (`Sefaz`); `TenantFiscalConfiguration` | **Não** (quem paga, TTSoft ou loja?) | Definir no contrato |
| 12 | Domínio(s) (`ttsofts.com.br` aparece nas origens CORS) | Fixo anual | `appsettings.json` (`Cors`) | **Não** | Registro.br / registrador |
| 13 | GitHub (repositório, Actions) | Fixo/variável | `.github/workflows/*` (jobs em `windows-latest`) | **Não** (provavelmente na franquia gratuita) | Faturamento do GitHub |
| 14 | Assinatura de código do instalador | Fixo anual, se existir | Nada no repositório | **Lacuna** | Confirmar se o `.exe` é assinado |
| 15 | Ferramentas de desenvolvimento, IA, equipamentos, internet | Fixo | Fora do repositório | **Não** | Levantar recibos |
| 16 | Contador / abertura e manutenção da empresa | Fixo | Fora do repositório | **Lacuna** | Contrato com o contador |
| 17 | Tempo do responsável (suporte, implantação, desenvolvimento) | O maior custo real | EXC-ADM-002 | **Não** | Decidir o valor-hora ou a retirada-alvo (seção 7) |

### 4.2 O que o código diz sobre como o custo escala

- **Banco compartilhado entre clientes** (EXC-OPS-007, EXC-SEG-001): o custo de banco **não se
  divide por cliente de forma observável**. Alocação só por *proxy*. Há um disponível:
  o App Insights já marca requisições com `TenantId` (`TenantTelemetryInitializer.cs:43,51`).
- **A API só roda em uma instância** (EXC-OPS-006: rate limit e cache em memória). Crescer
  significa subir de camada (custo em degraus), não somar instâncias baratas.
- **Telemetria sem amostragem** (item 7) e **WPF acessando o SQL direto** (EXC-SEG-001): ambos
  fazem o consumo crescer com o número de PCs, não só de lojas. A migração WPF→API muda esse
  perfil de custo (mais carga na API, menos conexões diretas no SQL).
- **Licenças de bibliotecas**: o `.csproj` usa só pacotes de código aberto, com duas condições
  a vigiar, **a confirmar nos termos dos autores**: QuestPDF está em `LicenseType.Community`
  (`ERP.WPF/App.xaml.cs:51`), que, segundo o autor, é gratuita abaixo de um teto de
  faturamento anual da *empresa*; AutoMapper está na versão 14.0.0 e versões posteriores
  passaram a ter licença comercial. Custo atual: zero. Risco: surpresa ao atualizar ou crescer.

### 4.3 Custos por cliente adicional (o que muda quando entra uma loja)

| Custo | Natureza | Observação |
|---|---|---|
| CNPJ extra na Focus | Degrau | No plano *Start* (tabela pública), cada CNPJ acima de 3 custa R$ 39,90 |
| Notas acima da franquia | Variável | R$ 0,055 a R$ 0,16 por nota, conforme plano e tipo |
| Boleto da mensalidade | Variável | R$ 1,99 por boleto recebido (tabela Asaas), **se** a cobrança for por Asaas. A forma de cobrança é lacuna |
| Banco, API, telemetria | Degrau/variável | Sem medição; ver premissa e orçamento na seção 6 |
| Implantação e treinamento | Tempo do responsável | Horas por implantação: lacuna |
| Suporte | Tempo do responsável | Horas por cliente/mês: lacuna |
| Inadimplência / cancelamento | Perda de receita | Sem histórico: lacuna |

---

## 5. Obrigações empresariais, tributos e contratos

Nada disto pode ser verificado no código. **Registrado como lacuna; a resposta é do contador.**

| Tema | O que se sabe | Lacuna / pergunta ao contador |
|---|---|---|
| Forma jurídica | Não consta em nenhum documento do programa | A TTSoft tem CNPJ? Qual natureza (MEI, ME, outra)? O produto OutfiTT é da mesma empresa? |
| Regime tributário | Desconhecido | Se Simples Nacional: a atividade cai no Anexo III ou V? Depende do **fator R** (folha ÷ receita ≥ 28%). Alíquotas nominais da 1ª faixa (LC 123/2006): 6,0% (III) e 15,5% (V). **Confirmar** |
| Teto de receita | — | Se for MEI, o teto anual é de R$ 81.000 (confirmar). Com R$ 89,90/mês, **mais de ~75 clientes** passam esse valor. Na tabela da seção 6.4 |
| ISS e emissão de nota de serviço | Nenhuma NFS-e da TTSoft aparece no repositório (a NFS-e do código é a dos clientes) | A mensalidade é serviço (SaaS) ou licença? Emite NFS-e para o cliente? |
| Reforma Tributária (IBS/CBS) | EXC-FIS-005 | Impacto sobre a carga da TTSoft e sobre o que o cliente precisa emitir |
| Contrato com clientes | **Não existe** (confirmação 7, EXC-FIS-002) | Sem contrato não há reajuste, multa, prazo, nem direito claro de suspender por inadimplência. Critério ADM-3 não atendido |
| LGPD | EXC-FIS-002 | A TTSoft é operadora dos dados dos clientes das lojas; precisa de contrato com DPA |
| Cobrança da mensalidade | Desconhecido (o `LicenseManager` verifica vencimento, `LicenseManager.cs:29-39`, mas não cobra) | Como é cobrado e conciliado? Há controle de inadimplência? |
| Fornecedores críticos (ADM-4) | Azure, Focus, GitHub, Asaas (se usado), Mercado Livre (integração) | Sem plano de contingência documentado. Focus é fornecedor único da emissão; Azure é uma região (Brazil South) |

---

## 6. Precificação, margem e cenários

### 6.1 O que já existe sobre preço

- Praticado: R$ 89,90 (Vila Verde) e R$ 30,00 (OutfiTT). Anterior citado: R$ 99,90 + R$ 2.000 de
  implantação (agosto). **Nenhum validado com cliente sem condição especial** (EXC-VEN-001).
- Há um interruptor por tenant (`TenantFeatureFlags`: Metas de Vendas, Pontos de Fidelidade),
  mas não foi avaliado como controle de plano. **Não existe** no código entidade de plano,
  assinatura ou cobrança recorrente (busca por plano/assinatura/faturamento só encontrou o
  Asaas de boleto e termos de contas a receber das lojas).
- Estratégia de preços do DOC-32 está marcada pelo próprio documento como **[estimativa]** e não
  foi usada aqui.

### 6.2 Premissas dos cenários (todas trocáveis)

| Premissa | Valor | Rótulo | Por quê | Como substituir |
|---|---|---|---|---|
| Clientes pagantes | 10, 50, 100, 300 | Pedido do responsável | — | — |
| Preço mensal por cliente | R$ 89,90 e R$ 99,90 | Já praticados/declarados | Não inventar preço novo | Resultado de conversas de preço (VEN-2) |
| Imposto | 6,0% e 15,5% sobre a receita | Premissa (1ª faixa Simples, III e V) | Cobre o intervalo plausível | Alíquota efetiva do contador |
| NFC-e por cliente/mês | 100, 500, 1.500 | **Premissa** | Alinhadas aos limites dos planos da Focus (100 e 500 incluídas) | Consulta SQL da seção 8 |
| NF-e por cliente/mês | 10 | Premissa | Uniforme | Mesma consulta |
| CNPJs por cliente | 1 | Premissa | Lojas com filial de CNPJ próprio elevam | Cadastro de filiais |
| Boletos recebidos por cliente/mês | 1 a R$ 1,99 | Tabela pública | Forma de cobrança é lacuna | Forma real (Pix zera) |
| Plano da Focus | O de menor custo entre Start, Growth, Retail, Retail+ | Tabela pública | Mostra o melhor caso que a tabela permite | Contrato real; programa de revenda da Focus (lacuna) |
| Infra Azure | R$ 64,00 × 1 (≤10), ×2 (≤50), ×3 (≤100), ×5 (≤300) | **Chute explícito** | Base inferida da hipótese da seção 3; fatores sem medição | Cost Management + medição de uso. Veja 6.3 |
| Custos fixos não levantados | 0 | Lacuna | Itens 11 a 17 da seção 4.1 | Levantamento |

**Quem paga as notas dos clientes é a premissa mais forte do modelo:** os cenários assumem que
a **TTSoft** paga a Focus por todas as lojas, como no custo declarado hoje. Se o contrato
repassar a franquia da Focus ao cliente, a linha "Focus" sai do cálculo e a margem sobe.

### 6.3 Resultados: preço R$ 89,90, Simples 6,0% (Anexo III)

Margem = receita − impostos − boletos − Focus − infra. **É um teto:** não inclui os custos
fixos não levantados nem o tempo do responsável.

| Clientes | NFC-e/cliente | Receita | Focus (plano de menor custo) | Margem total | Margem/cliente | Margem % |
|---:|---|---:|---:|---:|---:|---:|
| 10 | 100 | R$ 899,00 | R$ 410,20 (Start) | R$ 350,96 | R$ 35,10 | 39% |
| 10 | 500 | R$ 899,00 | R$ 629,00 (Retail) | R$ 132,16 | R$ 13,22 | 15% |
| 10 | 1.500 | R$ 899,00 | R$ 1.047,90 (Retail+) | −R$ 286,74 | −R$ 28,67 | −32% |
| 50 | 100 | R$ 4.495,00 | R$ 657,90 (Retail+) | R$ 3.339,90 | R$ 66,80 | 74% |
| 50 | 500 | R$ 4.495,00 | R$ 1.697,90 (Retail+) | R$ 2.299,90 | R$ 46,00 | 51% |
| 50 | 1.500 | R$ 4.495,00 | R$ 4.947,90 (Retail+) | −R$ 950,10 | −R$ 19,00 | −21% |
| 100 | 100 | R$ 8.990,00 | R$ 722,90 (Retail+) | R$ 7.336,70 | R$ 73,37 | 82% |
| 100 | 500 | R$ 8.990,00 | R$ 3.322,90 (Retail+) | R$ 4.736,70 | R$ 47,37 | 53% |
| 100 | 1.500 | R$ 8.990,00 | R$ 9.822,90 (Retail+) | −R$ 1.763,30 | −R$ 17,63 | −20% |
| 300 | 100 | R$ 26.970,00 | R$ 2.342,90 (Retail+) | R$ 22.091,90 | R$ 73,64 | 82% |
| 300 | 500 | R$ 26.970,00 | R$ 10.142,90 (Retail+) | R$ 14.291,90 | R$ 47,64 | 53% |
| 300 | 1.500 | R$ 26.970,00 | R$ 29.642,90 (Retail+) | −R$ 5.208,10 | −R$ 17,36 | −19% |

As combinações com R$ 99,90 e com imposto de 15,5% (Anexo V) estão no script. Resumo do efeito:
passar de R$ 89,90 para R$ 99,90 soma ~R$ 9,40 por cliente de margem (já descontado o imposto
de 6%); passar do Anexo III para o V reduz a margem em ~R$ 8,50 por cliente a R$ 89,90.
**Nenhuma dessas mudanças inverte o sinal no cenário de 1.500 NFC-e/cliente.**

### 6.4 Leituras que não dependem do chute de infra

**a) Preço de equilíbrio por cliente** (margem zero, só custos modelados; R$/mês):

| Clientes | 100 NFC-e | 500 NFC-e | 1.500 NFC-e |
|---:|---:|---:|---:|
| 10 | R$ 52,56 – R$ 58,47 | R$ 75,84 – R$ 84,37 | R$ 120,40 – R$ 133,94 |
| 50 | R$ 18,84 – R$ 20,96 | R$ 40,97 – R$ 45,57 | R$ 110,11 – R$ 122,49 |
| 100 | R$ 11,85 – R$ 13,18 | R$ 39,51 – R$ 43,95 | R$ 108,66 – R$ 120,87 |
| 300 | R$ 11,56 – R$ 12,86 | R$ 39,22 – R$ 43,63 | R$ 108,37 – R$ 120,55 |

(faixa = imposto de 6,0% a 15,5%.) Acima de ~100 clientes o preço de equilíbrio praticamente
estabiliza: **depende da nota, não da escala.** Escala dilui o custo fixo, não o variável.

**b) Orçamento de infra por cliente que o preço comporta** (R$ 89,90, 6,0%): com 100 NFC-e,
R$ 41,50 (10 clientes) a ~R$ 75 (100 a 300 clientes); com 500 NFC-e, R$ 19,62 (10) a ~R$ 49
(100 a 300); com 1.500, **negativo** (a nota sozinha consome o preço). Compare com o custo
real de Azure por cliente quando a medição existir. Se o Azure total sair de R$ 64,00 para algo
perto de R$ 320 com 300 clientes (o chute), o custo é ~R$ 1,07 por cliente: pequeno diante da
nota fiscal. **A hipótese de trabalho é que a nota fiscal e o tempo do responsável pesam mais
que a nuvem.** Ela precisa ser testada com a fatura do Azure.

**c) Receita anual bruta** (R$ 89,90 × 12 × clientes): 10 → R$ 10.788; 50 → R$ 53.940;
100 → R$ 107.880; 300 → R$ 323.640. Isso importa para o enquadramento (seção 5) e para as
licenças condicionadas ao faturamento (seção 4.2).

### 6.5 Capacidade de uma pessoa (insumo de ponto de equilíbrio)

Há um limite que o modelo financeiro não mostra: o tempo. Com uma pessoa (EXC-ADM-002) e
**sem dado de horas por cliente**, não dá para afirmar quantos clientes ela atende. Fórmula
para quando houver dado:

```
clientes_suportáveis = horas_produtivas_por_mês ÷ (horas_suporte_por_cliente_mês + horas_implantação_por_cliente ÷ meses_de_vida_média)
```

Sem as horas, o resultado de qualquer cenário com 50 ou mais clientes deve ser lido como
**"financeiramente possível, operacionalmente não demonstrado".** Contratar apoio é decisão de
negócio (EXC-ADM-002); o custo dessa pessoa é lacuna.

---

## 7. Ponto de equilíbrio

Três definições, com o dado que falta para cada uma:

| Equilíbrio | Fórmula | Hoje, com os dados disponíveis |
|---|---|---|
| **Operacional** (cobrir custo declarado) | custos ÷ margem de contribuição por cliente | Custo R$ 183,90 contra R$ 119,90 de receita: **não cobre**. Falta fatura para saber quanto custa o Azure por cliente adicional |
| **Com impostos** | idem, com alíquota efetiva | Depende do regime (seção 5) |
| **Com retirada** (pagar o responsável) | (custos fixos + retirada-alvo) ÷ margem de contribuição unitária | **Impossível sem a retirada-alvo**, que é decisão de Matheus. Inserir no script e ler a primeira linha da tabela com margem total acima do valor |

Referência rápida: na faixa de 500 NFC-e por cliente, a margem total (R$ 89,90, 6%) é de
R$ 2.300 (50 clientes), R$ 4.737 (100) e R$ 14.292 (300), antes de custos fixos e do tempo. Isso é
tudo que o modelo permite dizer, e depende das premissas das seções 6.2 e 6.4.

---

## 8. Dados a coletar (em ordem de valor)

| # | Dado | De onde | Fecha qual dúvida | Registro |
|---|---|---|---|---|
| 1 | **Notas emitidas por mês por loja**, por tipo, nos últimos 3 a 6 meses (Vila Verde) | Consulta abaixo + painel Focus | Define o volume fiscal real; é a premissa que mais pesa | EXC-ADM-003 |
| 2 | **Fatura mensal Azure por recurso**, 3 meses (inclui App Service, slot, Static Web App, Blob, App Insights, SignalR) | Azure Cost Management → Exportar | Custo real de infra; confirma ou derruba a hipótese de R$ 64,00 | EXC-ADM-001 |
| 3 | **Contrato e fatura da Focus** (plano, franquia, CNPJs, preço do excedente, programa de revenda) | Painel/e-mail da Focus | Se o plano é o *Start*; custo real por nota | EXC-ADM-001, EXC-ADM-003 |
| 4 | **Forma jurídica, CNPJ e regime tributário da TTSoft; alíquota efetiva** | Contador | Impostos reais e teto de receita | EXC-ADM-004 |
| 5 | **Como a mensalidade é cobrada e recebida**, e se há inadimplência | Extrato bancário / Asaas | Custo de cobrança e perda de receita | EXC-ADM-004 |
| 6 | Se o **Asaas** está em produção e quem recebe os boletos | App Service → Configuração (`Asaas__Environment`); painel Asaas | Risco financeiro/jurídico da seção 9 | EXC-ADM-005 |
| 7 | Requisições e GB de telemetria **por tenant** | Application Insights (consulta abaixo) | Proxy para alocar custo de infra por cliente | EXC-ADM-003 |
| 8 | Horas gastas por semana em suporte, implantação, desenvolvimento (anotar por 4 semanas) | Responsável | Capacidade e custo real do tempo | EXC-ADM-002 |
| 9 | Itens fixos 11 a 16 da seção 4.1 (domínio, assinatura de código, ferramentas, contador) | Recibos | Custos fixos além de nuvem | EXC-ADM-004 |
| 10 | Preço testado com cliente **sem** condição especial | Conversas comerciais (EXC-A10, VEN) | Preço de mercado | EXC-VEN-001 |

**Consulta 1: volume fiscal por mês** (somente leitura; ajustar o nome da tabela se divergir):

```sql
SELECT TenantId, FORMAT(DataEmissao, 'yyyy-MM') AS mes, Tipo, Ambiente, Status, COUNT(*) AS qtd
FROM NotasFiscais
GROUP BY TenantId, FORMAT(DataEmissao, 'yyyy-MM'), Tipo, Ambiente, Status
ORDER BY TenantId, mes DESC, Tipo;
```

Lembrete: o tenant de teste com o CNPJ da Vila Verde emitiu notas de teste em produção
(EXC-FIS-008). Separar `Ambiente` e tenant antes de ler o resultado como volume real.

**Consulta 2: requisições por tenant** (Application Insights → Logs):

```kusto
requests
| where timestamp > ago(30d)
| summarize requisicoes = count() by tenant = tostring(customDimensions.TenantId)
| order by requisicoes desc
```

---

## 9. Riscos e achados de administração

| Tema | Natureza | Descrição curta |
|---|---|---|
| Margem dominada pelo custo da nota fiscal | Hipótese | Calculado com tabela pública e premissas de volume. Vira Fato com o volume real e o contrato da Focus |
| **Asaas com uma única chave para toda a aplicação** | Hipótese | `AsaasService` lê `Asaas:ApiKey` da configuração (`AsaasService.cs:23-24`), e não de um dado por tenant. Se estiver em produção, os boletos de clientes dos lojistas seriam emitidos na conta Asaas da TTSoft, com a TTSoft como recebedora do dinheiro. Isso tem efeito fiscal, financeiro e de intermediação. O padrão `appsettings.json` é `sandbox`; produção não verificada. **Não é afirmação de que está ocorrendo** |
| Lacunas jurídicas e tributárias | Lacuna | Seção 5 |
| Dependência de uma pessoa | Fato (EXC-ADM-002) | Capacidade e continuidade |
| Fornecedor único de emissão fiscal | Fato | Focus; sem alternativa documentada (ADM-4) |

---

## 10. Registros propostos e próximos passos

Registros novos no [REGISTRO-CENTRAL](REGISTRO-CENTRAL.md) (diagnóstico, não tarefa):

| ID | Título | Natureza | Gravidade |
|---|---|---|---|
| EXC-ADM-003 | Margem por cliente depende do volume de notas fiscais, que não está medido | Hipótese | A classificar |
| EXC-ADM-004 | Dados administrativos e tributários da TTSoft ausentes | Lacuna | A classificar |
| EXC-ADM-005 | Asaas usa uma única chave da aplicação; uso em produção desconhecido | Hipótese | A classificar |

Decisões que são de Matheus (não técnicas):

1. As notas excedentes da Focus serão **da TTSoft ou repassadas ao cliente**? Define se o modelo
   fecha em escala.
2. Qual **retirada-alvo** mensal torna o negócio sustentável? Sem ela, o ponto de equilíbrio com
   retirada não existe.
3. A forma de **cobrança** da mensalidade (boleto, Pix, cartão) e o prazo de suspensão por atraso.
4. Se haverá **pessoa de apoio** e em que número de clientes.

Fora do escopo desta passada: faturas reais, validação com contador e testes de preço com clientes.
O que não está nas tabelas acima **não foi estimado**.

---

## Histórico do documento

| Data | Versão | Alteração |
|---|---|---|
| 2026-10-26 | 1.0 | Criação: inventário de custos, premissas, cenários 10/50/100/300 e lista de coleta |
