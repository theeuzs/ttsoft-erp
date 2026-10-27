# EXC-A10 (parte VEN) — Processo comercial, demonstração e funil

> Programa de Excelência TTSoft · Área VEN (com insumos de MKT, ADM, SUP e FIS)
> Criado em: 2026-10-26 · Versão: 1.0 · Base de código: commit `49c0ba5` (main)
> Documentos relacionados: [PLANO-MESTRE](PLANO-MESTRE.md) · [CRITERIOS-DE-EXCELENCIA](CRITERIOS-DE-EXCELENCIA.md) · [REGISTRO-CENTRAL](REGISTRO-CENTRAL.md) · [EXC-A10-MARKETING](EXC-A10-MARKETING.md) · [EXC-A10-ADMINISTRATIVO-FINANCEIRO](EXC-A10-ADMINISTRATIVO-FINANCEIRO.md)

Esta é a primeira passada sobre a área VEN. Ela desenha o caminho de uma loja desde o primeiro
contato até a renovação, diz o que já existe (no código e fora dele) e o que precisa ser criado,
e modela o funil com definições e fórmulas. **Nenhuma taxa de conversão, ciclo de venda ou meta
foi atribuída à TTSoft**: não há histórico comercial para isso (EXC-MKT-004). Perfil de cliente,
mensagens e canais estão na parte MKT e não são repetidos aqui; custos e margem estão na parte ADM.

---

## 1. Resumo

1. **Não existe processo comercial documentado** (CRITERIOS VEN-1). Houve uma venda (Vila
   Verde, com condição especial) e ao menos uma proposta (Eldorado, DOC-29, situação
   desconhecida). Nenhuma etapa tem critério de passagem, registro ou dono além do responsável.
2. **O código tem mais para a implantação do que os documentos diziam:** um importador CSV de
   produtos e de clientes **com saldo de fiado e haver**, com prévia antes de gravar
   (`ImportacaoService`, commit `1980593`), importação de XML de compra, e licença com
   vencimento, aviso e carência de 3 dias. Isso fecha em parte a lacuna D7 da parte MKT.
3. **Há um cadastro público "Criar conta gratuita" no Portal** que cria uma loja no banco de
   produção, sem plano, prazo, contrato nem aceite de termos (`Login.razor:45-47`,
   `CadastroService`). Na prática é um período de teste sem fim e sem regra, aberto a qualquer
   CNPJ ativo. Registrado como **EXC-VEN-002**.
4. **A implantação de uma loja nova depende de passos técnicos manuais** que só o responsável
   sabe fazer: editar o `licenca.json` em cada PC, digitar a credencial do banco no primeiro uso,
   cadastrar a licença numa API cujo código só está no PC dele, configurar o fiscal. Critério
   VEN-3 não atendido. Registrado como **EXC-VEN-003**.
5. **Recomendação de desenho:** venda consultiva e presencial, em 11 etapas (seção 4), com
   **demonstração guiada por dor** (seção 6), **plano único** com franquia de notas (seção 9) e
   **piloto assistido de 30 dias** em vez de teste gratuito livre (seção 10). Tudo isso só vira
   venda real depois das pré-condições da parte MKT §9 (login restrito, CNPJ do emitente por
   loja, contrato).
6. Entregas: processo (seção 4), roteiro de demonstração (seção 6), respostas a objeções
   (seção 7), estrutura de proposta (seção 8), funil com fórmulas (seção 13) e painel de
   indicadores (seção 14).

---

## 2. Método e rotulagem

Mesmos rótulos das partes ADM e MKT:

| Rótulo | Significado |
|---|---|
| **FATO-CÓDIGO** | Verificado no repositório em 2026-10-26 (arquivo:linha ou commit) |
| **DECLARADO** | Dito por Matheus em 2026-10-22, sem documento |
| **RELATÓRIO ANTERIOR** | Afirmação de DOC-NN, não reconferida aqui |
| **HIPÓTESE** | Inferência a testar |
| **RECOMENDAÇÃO** | Proposta de desenho; vira processo só com aprovação de Matheus |
| **LACUNA** | Dado necessário e ausente |

Fontes: código do repositório (cadastro, licença, importação, permissões, checklist de deploy),
documentos do programa, DOC-29 (proposta Eldorado, citada, não relida) e DOC-32 (GTM, com preços
marcados como estimativa). Não houve conversa com clientes nem acesso a e-mails, WhatsApp ou
propostas enviadas.

---

## 3. O que existe hoje

### 3.1 No código

| Recurso | O que faz | Evidência | Uso comercial | Limite |
|---|---|---|---|---|
| Cadastro público de loja | Cria tenant, 4 cargos, permissões e usuário `admin`; valida CNPJ ativo na Receita (BrasilAPI); se o e-mail diverge do da Receita, cria inativo e confirma pelo e-mail oficial | `ERP.Api/Controllers/CadastroController.cs:29-80`; `ERP.Infrastructure/Services/CadastroService.cs:39-211` | Base de um *onboarding* sem intervenção | Sem plano, prazo, termos nem contrato (EXC-VEN-002); só cria o acesso ao Portal, não instala o PDV |
| Link "Criar conta gratuita" | Leva ao cadastro a partir do login do Portal | `ERP.Portal/Pages/Login.razor:43-48` | Promessa comercial | "Gratuita" sem condição definida |
| E-mail de boas-vindas | Envia endereço do Portal, CNPJ e usuário | `CadastroService.cs` (assunto "Seu acesso ao TTSoft ERP está pronto!") | Primeiro contato pós-cadastro | Marca "TTSoft ERP", diferente do PDV (MKT §6.3); nenhum próximo passo comercial |
| Licença do PDV | Consulta CNPJ + identificador da máquina numa API externa; guarda cache; avisa vencimento; carência de 3 dias sem internet | `ERP.WPF/Security/LicenseManager.cs:15,21-90` | **Mecanismo de renovação e de corte por inadimplência** | API fora do repositório (EXC-OPS-005); regra por máquina não verificável |
| CNPJ da loja no PC | Lido do `licenca.json` ao lado do executável | `ERP.WPF/Services/TenantService.cs:28-40` | Define a loja de cada PC | Editado à mão em cada PC (`Scripts/CHECKLIST_DEPLOY.md` §2) |
| Configuração do primeiro uso | Tela pede servidor, banco, usuário e senha do SQL | `ERP.WPF/ViewModels/SetupViewModel.cs:18-45,131-135` | Instalação | Exige a credencial do banco (EXC-SEG-001) |
| Importação de produtos e clientes (CSV) | Detecta colunas por sinônimos, mostra prévia, grava; cliente com saldo de fiado vira **uma** conta a prazo "Saldo migrado do sistema anterior"; haver vira saldo direto | `ERP.Infrastructure/Services/ImportacaoService.cs:41-62,347-352`; `ERP.WPF/ViewModels/ImportacaoViewModel.cs` | Reduz a principal barreira de troca (MKT D7) | Fiado entra **sem os vencimentos originais**; nenhum teste automatizado encontrado |
| Importação de histórico de vendas | Lê arquivos `vendas*` de texto de um sistema anterior específico | `ERP.Application/Services/LegacyImportService.cs:23-130` | Histórico para relatórios | Todo item vai para o **primeiro produto cadastrado**; formato de um único sistema |
| Importação de XML de compra | Cria produto e estoque a partir da NF-e do fornecedor | `NfeImportView`, `NfesRecebidas` | Cadastro inicial rápido | — |
| Módulos por loja | Liga/desliga Metas de Vendas e Pontos de Fidelidade | `ERP.Domain/Entities/TenantFeatureFlags.cs:19-20` | Poderia diferenciar planos | Só 2 módulos; não é controle de plano |
| Atualização automática do PDV | Baixa nova versão do Blob | `Scripts/versao.json`; `CHECKLIST_DEPLOY.md` §3-4 | Cliente recebe melhorias sem visita | — |

**Não existe no código:** entidade de plano, assinatura ou cobrança recorrente da TTSoft
(parte ADM §6.1); painel da TTSoft para ver lojas, planos e vencimentos; registro de aceite de
termos; exportação completa dos dados de uma loja (para cancelamento ou portabilidade); tenant
de demonstração com dados de exemplo. Buscas em 2026-10-26 por plano, assinatura, *admin* de
plataforma, listagem de tenants e exportação de dados não acharam nada.

### 3.2 Fora do código

| Item | Situação | Rótulo |
|---|---|---|
| Clientes pagantes do ConstruTTor | 1 (Vila Verde, R$ 89,90/mês, condição especial) | DECLARADO |
| Preço anterior citado | R$ 99,90/mês + R$ 2.000 de implantação (agosto) | RELATÓRIO ANTERIOR (DOC-34) |
| Propostas enviadas | Eldorado (DOC-29); situação desconhecida | LACUNA |
| Contrato, termos, política de privacidade | Não existem | DECLARADO (EXC-FIS-002) |
| Roteiro de demonstração, apresentação, material | Não constam | LACUNA |
| Registro de contatos e funil | Não existe | LACUNA (EXC-MKT-004) |
| Forma de cobrança e inadimplência | Desconhecidas | LACUNA (EXC-ADM-004) |
| Horas gastas na implantação da Vila Verde | Não registradas | LACUNA (EXC-ADM-002) |
| Canal de suporte e registro de chamados | Não verificado nesta passada | LACUNA (CRITERIOS SUP-1) |

---

## 4. Processo comercial ponta a ponta (RECOMENDAÇÃO)

Desenhado para **uma pessoa**, venda presencial e local, ciclo curto com o dono da loja. Cada
etapa tem um **critério de passagem** objetivo: é ele que define o funil da seção 13.

```
 E1 Contato ─► E2 Qualificação ─► E3 Diagnóstico ─► E4 Demonstração ─► E5 Proposta
                                                                           │
 E11 Renovação ◄─ E10 Acompanhamento ◄─ E9 Implantação ◄─ E8 Contrato ◄─ E6 Piloto (opcional)
                                                                           │
                                                              E7 Decisão ◄─┘
 Qualquer etapa ─► Perdido (com motivo)       E10/E11 ─► Cancelado (com motivo)
```

| Etapa | Entra quando | Sai (passa) quando | Artefato | Existe hoje? | Falta criar |
|---|---|---|---|---|---|
| **E1 Contato** | Primeiro contato registrado (indicação, visita, contador, WhatsApp, cadastro no Portal) | Conversa marcada ou respondida | Linha na planilha de funil | Não | Planilha (seção 13.3) |
| **E2 Qualificação** | Houve conversa | Loja atende os critérios de entrada da seção 5 | Campos "no perfil? por quê" | Não | Perguntas de qualificação (seção 5) |
| **E3 Diagnóstico** | Qualificada | Dor principal, sistema atual, preço atual e decisor anotados; dono aceita ver uma demonstração | Ficha de diagnóstico (seção 5.2) | Não | Ficha; "Diagnóstico do fiado" (MKT §11) |
| **E4 Demonstração** | Data marcada com o decisor presente | Demonstração feita e reação registrada | Roteiro (seção 6) + registro | Não | Tenant de demonstração; roteiro |
| **E5 Proposta** | Demonstrou e o dono pediu preço | Proposta escrita entregue | Proposta (seção 8) | Não | Modelo de proposta; preço decidido |
| **E6 Piloto** (opcional) | Dono quer usar antes de decidir | 30 dias concluídos com os critérios de sucesso medidos | Termo de piloto (seção 10) | Não (só o cadastro livre) | Termo, critérios, regra de saída |
| **E7 Decisão** | Proposta (ou piloto) concluída | Sim ou não registrado, com motivo | Registro | Não | — |
| **E8 Contrato** | Disse sim | Contrato assinado e primeira cobrança emitida | Contrato + termos + DPA | **Não** (EXC-FIS-002) | Contrato com assessoria jurídica |
| **E9 Implantação** | Contrato assinado | Critérios de "implantação concluída" atendidos (seção 11.3) | Ficha de implantação | Parcial (checklist de deploy, desatualizado e da Vila Verde) | Roteiro de implantação (seção 11) |
| **E10 Acompanhamento** | Implantação concluída | Contínuo; revisões em D+30, D+60, D+90 | Ficha de saúde (seção 12) | Não | Ficha; consultas de uso |
| **E11 Renovação** | Aviso de vencimento ou fim do período contratado | Renovado (ou cancelado, com motivo e exportação de dados) | Registro + licença atualizada | Parcial (licença com vencimento) | Regra de reajuste, aviso, exportação |

**Regras do processo:**

1. Toda passagem de etapa recebe data na planilha. Sem data, o funil não se calcula.
2. Toda perda recebe **um** motivo principal, de uma lista fechada (seção 13.4).
3. Contatos vindos do cadastro público do Portal entram em E1 como qualquer outro, e recebem
   ligação ou mensagem em até 2 dias úteis (enquanto o cadastro existir, seção 10).
4. Limite de capacidade (parte MKT, regra 3): **no máximo 2 pilotos ou implantações ao mesmo
   tempo**. Acima disso, novas propostas ficam com data de início futura.
5. Nada de etapa E6 a E9 com loja nova antes das pré-condições da parte MKT §9.

---

## 5. Qualificação e diagnóstico

### 5.1 Critérios de entrada (E2)

Derivados do perfil de cliente ideal da parte MKT §4 (HIPÓTESE). Uma loja **passa** se cumprir
todos os obrigatórios.

| Critério | Obrigatório? | Pergunta | Desqualifica se |
|---|---|---|---|
| Porte | Sim | "Quantas lojas e quantos caixas?" | Mais de 3 lojas, ou comitê de compra |
| Raio de atendimento | Sim | "Onde fica a loja?" | Fora do raio presencial (decisão pendente, MKT §15) |
| Regime fiscal | Sim | "A loja é do Simples? Quem é o contador?" | Precisa do SPED completo pelo sistema (EXC-FIS-006) |
| Pagamento com cartão | Sim | "Precisa que a maquininha seja integrada ao caixa?" | Exige TEF integrado (não existe) |
| Venda por celular | Sim | "Vendedor tira pedido no celular?" | Depende de app (EXC-PRD-001) |
| PCs | Sim | "O caixa é um PC com Windows?" | Sem PC Windows |
| Crediário/fiado | Não (pontua) | "Vende fiado? Quanto tem a receber hoje?" | — (sem fiado, o diferencial principal some) |
| Volume de notas | Não (pontua) | "Quantas notas por mês, mais ou menos?" | — (alto volume pede a regra de excedente, seção 9) |
| Decisor | Não (pontua) | "Quem decide a troca de sistema?" | — (marcar a demonstração com ele) |

### 5.2 Ficha de diagnóstico (E3)

Uma página, preenchida em conversa de 20 a 30 minutos, **antes** de qualquer demonstração.
Serve também como entrevista X1 (parte MKT §12).

1. Sistema atual (nome), há quanto tempo, quanto paga, o que mais incomoda.
2. Como controla o fiado hoje; quanto tem a receber; o que perdeu no último ano.
3. O que acontece quando a internet cai.
4. Problemas com nota fiscal no último mês.
5. Que número o dono olha toda semana.
6. Vende pela internet? Onde?
7. Quem decide, e o que faria trocar de sistema agora (ou o que impede).
8. Quando o contrato atual vence ou quando seria um bom momento para trocar.
9. Dor principal, nas palavras do dono (copiar literalmente).

A demonstração (E4) é montada a partir dos itens 2 a 6. Se a dor principal não está entre as
que o produto resolve (MKT §5), **não demonstrar**: registrar como perda "fora do perfil" e o
pedido como insumo de produto.

---

## 6. Roteiro de demonstração (E4)

### 6.1 Pré-requisitos (antes da primeira demonstração com loja nova)

| # | Pré-requisito | Por quê | Situação |
|---|---|---|---|
| P1 | Nome da Vila Verde fora da interface | Loja nova veria o nome de outra | Corrigido no código, falta publicar (EXC-MKT-002) |
| P2 | **Tenant de demonstração** com CNPJ fictício que não seja de nenhuma empresa real, com produtos, clientes, fiado e vendas de exemplo | Nunca mostrar dados reais de cliente; lição do EXC-FIS-008 | **Não existe**; criar |
| P3 | Fiscal desligado ou só em homologação no tenant de demonstração | Uma nota de demonstração autorizada em produção é documento fiscal real; e hoje toda nota sai com o CNPJ da Vila Verde (EXC-FIS-013) | **Não emitir nota em demonstração** até EXC-FIS-013 estar corrigido |
| P4 | Notebook com o PDV instalado apontando para o tenant de demonstração, e um *plano B* sem internet (vídeo gravado das telas) | A demonstração não pode depender da rede da loja | Criar |
| P5 | Lista "pode dizer / não pode dizer" impressa | Parte MKT §6.1 e §6.2 | Existe no documento MKT |

### 6.2 Roteiro (45 minutos, guiado pela dor do diagnóstico)

Ordem padrão abaixo; **começar pelo bloco da dor principal** anotada em E3 e cortar os blocos
que não tocam nenhuma dor do dono.

| Bloco | Tempo | O que mostrar | Frase permitida (MKT §6.1) | Pergunta de checagem |
|---|---|---|---|---|
| 0. Abertura | 3 min | Repetir a dor nas palavras do dono; dizer o que vai mostrar e quanto tempo leva | — | "Era isso mesmo?" |
| 1. Venda no balcão | 7 min | Venda rápida com leitor, desconto dentro do limite do cargo, várias formas de pagamento, cupom | — | "É parecido com o que o seu caixa faz hoje? O que falta?" |
| 2. Fiado sob controle | 8 min | Cliente com limite; venda a prazo; **venda bloqueada ao passar do limite**; contas a receber; haver | "O sistema não deixa vender acima do limite do cliente" | "Quanto do seu fiado hoje passaria do limite?" |
| 3. Caixa sem internet | 5 min | Desligar a rede do notebook, vender, religar, mostrar a venda sincronizada | "A venda continua e sincroniza depois" (**nunca** "a nota sai normal sem internet") | "Quantas vezes por mês a internet cai aí?" |
| 4. Entrada de mercadoria | 5 min | Importar um XML de compra (de preferência do fornecedor do próprio lojista) | "Importe a nota de compra e o estoque se monta" | "Quantas notas de compra chegam por semana?" |
| 5. Números do dono | 5 min | Fechamento de caixa, DRE, margem, curva ABC; painel no Portal | — | "Que número você queria ver toda segunda?" |
| 6. Extras conforme o perfil | 0 a 5 min | Calculadora de materiais, Mercado Livre, orçamento por WhatsApp, entregas | Só "existe e funciona"; sem números de resultado | — |
| 7. Troca de sistema | 4 min | Prévia da importação de um CSV de clientes com saldo de fiado; explicar o que vem e o que não vem | "Seus produtos e clientes, com o saldo do fiado, entram por planilha" (o saldo entra como um valor por cliente, sem os vencimentos originais) | "Seu sistema atual exporta planilha?" |
| 8. Fechamento | 3 min | Como é a implantação, quanto tempo leva (quando houver dado), próximo passo | — | "Faz sentido eu mandar uma proposta?" |

**Não mostrar em demonstração:** emissão de nota em produção (P3); qualquer tela com dados da
Vila Verde; funções que não funcionarem no ensaio do dia; o app Mobile; promessas de recursos
futuros com data.

**Registro depois de cada demonstração:** data, quem estava, blocos mostrados, reação a cada
bloco (interessou / indiferente / objeção), objeções literais, próximo passo combinado.

---

## 7. Objeções e respostas

Respostas **honestas para o estado de 2026-10-26**. Onde a resposta é fraca, a coluna "o que
falta" diz o que a tornaria forte. Nenhuma resposta usa as afirmações proibidas da parte MKT §6.2.

| # | Objeção | Resposta possível hoje | O que falta para responder melhor | Registro |
|---|---|---|---|---|
| O1 | "E se você sumir? É uma pessoa só." | Atendimento direto com quem fez o sistema; seus dados podem ser exportados (só se for verdade no dia, ver "o que falta") | Contrato com aviso prévio de encerramento, **exportação completa dos dados** (não existe), procedimento documentado de continuidade | EXC-ADM-002, EXC-OPS-005, EXC-VEN-003 |
| O2 | "Tem TEF / maquininha integrada?" | Não. O pagamento em cartão é lançado no caixa; a maquininha é separada | Integração com TEF (decisão de produto) | Anotar como motivo de perda "TEF" |
| O3 | "Tem app no celular?" | Não hoje. Há Portal no navegador para o dono acompanhar | Decisão sobre o Mobile (EXC-PRD-001) | Motivo "mobile" |
| O4 | "É seguro? Meus dados ficam onde?" | Na nuvem da Microsoft (Azure), no Brasil. **Não** dizer "seguro" ou "testado" | Login restrito e troca de senha concluídos (E1 a E6); política de privacidade (EXC-FIS-002) | EXC-SEG-001, EXC-MKT-003 |
| O5 | "Está caro." / "O meu custa menos." | Comparar pelo que resolve (fiado, caixa sem internet, implantação presencial), não pelo preço; perguntar o que o atual não faz | Preço testado (X5); dado de quanto as lojas pagam hoje | EXC-VEN-001 |
| O6 | "Vou perder meus cadastros e o fiado." | Produtos e clientes com saldo de fiado entram por planilha, com prévia antes de gravar | Teste do importador com a exportação real de 2 ou 3 sistemas comuns; trazer vencimentos do fiado; teste automatizado | Seção 3.1 |
| O7 | "E se a internet cair?" | A venda continua e sincroniza quando a rede volta | Teste em campo (MKT X8); contingência fiscal corrigida (EXC-FIS-007, EXC-FIS-009) | — |
| O8 | "Preciso falar com o meu contador." | Ótimo: oferecer conversa com o contador; mostrar o que o sistema entrega a ele | Lista do que o contador recebe (XML, relatórios); SPED com Bloco E (EXC-FIS-006) | Canal C2 da parte MKT |
| O9 | "Agora não é hora." / "Meu contrato vence em X." | Registrar a data e combinar retorno 30 dias antes | — | Etapa "adiado", com data de retorno |
| O10 | "Quanto tempo para implantar? Vou parar a loja?" | A implantação é feita fora do horário e acompanhada pessoalmente | Horas e dias medidos em implantações reais (seção 11) | EXC-ADM-002 |
| O11 | "Quem mais usa?" | Uma loja em operação, citada **só com autorização escrita** | Autorização da Vila Verde; segundo cliente | MKT §14, item 4 |
| O12 | "Tem contrato? Posso cancelar?" | **Hoje não há contrato.** Não fechar venda nova sem ele | Contrato, termos e DPA (EXC-FIS-002) | Bloqueia E8 |
| O13 | "Vocês emitem a nota da minha loja certinho?" | Emissão de NFC-e e NF-e via Focus NFe | EXC-FIS-013 corrigido (hoje toda nota sairia com o CNPJ da Vila Verde) | Bloqueia E9 |

---

## 8. Proposta comercial (E5)

Proposta escrita de uma ou duas páginas, enviada por WhatsApp em PDF e explicada em conversa.
**Não pode ser enviada com valores e condições que o contrato não sustente** (EXC-FIS-002).

### 8.1 Estrutura mínima

1. **Situação da loja** — a dor principal do diagnóstico, nas palavras do dono.
2. **O que o ConstruTTor resolve para ela** — no máximo 4 itens, só os da coluna "pode usar
   hoje" da parte MKT §6.1.
3. **O que está incluído** — módulos, número de PCs/caixas, usuários, Portal, atualizações.
4. **O que não está incluído** — TEF, app, SPED completo, certificado digital (definir quem paga,
   parte ADM §4.1 item 11), hardware.
5. **Implantação** — o que será feito, em quantas visitas, quem prepara a planilha de
   importação, quando o caixa começa a usar, valor (ou isenção) da implantação.
6. **Preço** — mensalidade, franquia de notas, preço do excedente, forma e dia de cobrança.
7. **Prazo e saída** — vigência, reajuste (índice e data), aviso de cancelamento, o que acontece
   com os dados no cancelamento (exportação, prazo de guarda e exclusão).
8. **Suporte** — canal, horário, tempo de resposta **que uma pessoa consegue cumprir**.
9. **Validade da proposta** — data.

### 8.2 O que depende de decisão antes da primeira proposta

| Decisão | Dono | Ligada a |
|---|---|---|
| Preço fixo a apresentar | Matheus | MKT X5, EXC-VEN-001 |
| Notas excedentes: TTSoft paga ou repassa | Matheus | EXC-ADM-003 |
| Cobra implantação? Quanto? | Matheus | Seção 9 |
| Quem paga o certificado digital A1 | Matheus | ADM §4.1 |
| Tempo de resposta do suporte | Matheus | EXC-ADM-002, SUP-1 |
| Texto do contrato e dos termos | Matheus + assessoria jurídica | EXC-FIS-002 |

---

## 9. Planos e preços

### 9.1 O que se sabe

- Preços praticados ou citados: R$ 89,90 (Vila Verde, condição especial), R$ 99,90 + R$ 2.000 de
  implantação (agosto, DOC-34). **Nenhum testado sem condição especial** (EXC-VEN-001).
- O custo variável dominante é a **nota fiscal** (parte ADM §6): com a TTSoft pagando as notas,
  1.500 NFC-e por loja/mês tornam a margem negativa em qualquer escala.
- O custo dominante real, provavelmente, é o **tempo** de implantação e suporte (ADM §6.5), que
  não está medido.
- A licença é consultada por CNPJ **e máquina** (`LicenseManager.cs:23-29`). Se a API de licenças
  limita máquinas, já existe um meio técnico de cobrar por PC (HIPÓTESE: o código da API não está
  no repositório).

### 9.2 Estrutura recomendada (RECOMENDAÇÃO, a testar em X5)

**Plano único** no início, com quatro variáveis explícitas:

| Componente | Forma | Por que assim |
|---|---|---|
| Mensalidade | Valor fixo por loja (CNPJ), com até N PCs incluídos | Simples de explicar e de cobrar por uma pessoa; N a definir pela Vila Verde (3 PCs hoje) |
| PC adicional | Valor fixo por PC acima de N | Segue o custo de suporte, que cresce com PCs (e com o consumo, ADM §4.2) |
| Franquia de notas | Quantidade de notas incluída por mês | Protege a margem do risco EXC-ADM-003 |
| Excedente de notas | Preço por nota acima da franquia, **repassado** ao cliente | Se a decisão for repassar; senão o plano precisa de preço maior para lojas de alto volume |
| Implantação | Valor único, ou isenção com fidelidade mínima | Cobre as horas de implantação (que ainda não estão medidas) |

**Por que não vários planos agora:** só há 2 módulos ligáveis (`TenantFeatureFlags`), não há
entidade de plano no código, e cada plano a mais é uma regra a mais para explicar, cobrar e
suportar. Planos por módulo só fazem sentido quando houver dado de quais módulos as lojas usam
(consulta da parte MKT §14) e de quais justificam preço maior.

**Valores:** não são propostos aqui. O teste X5 usa os pontos já praticados (R$ 89,90 e
R$ 99,90) como ponto de partida; a franquia e o excedente devem partir do contrato real da Focus
(ADM §8, item 3) e do volume real da Vila Verde (ADM §8, item 1). O preço mínimo por cenário de
notas está na tabela de equilíbrio da ADM §6.4a.

### 9.3 Descontos e condições especiais

Regra recomendada: toda condição especial é **escrita, com prazo e motivo** (ex.: "loja de
referência, R$ X até dd/mm/aaaa, em troca de autorização para ser citada"). A condição da Vila
Verde hoje não tem prazo conhecido, e isso torna difícil reajustar sem conflito.

---

## 10. Período de teste

### 10.1 Situação atual

- **Portal:** qualquer CNPJ ativo cria uma loja pelo link "Criar conta gratuita"
  (`Login.razor:45-47`). A conta não tem prazo, plano, limite nem aceite de termos, e é criada no
  mesmo banco das lojas reais (`CadastroService.cs:118-165`; banco compartilhado, EXC-OPS-007).
  O cadastro tem boas proteções contra uso do CNPJ de outra empresa (confirmação pelo e-mail da
  Receita, limite de 3 tentativas por hora por IP, mesma resposta para CNPJ novo ou existente).
- **PDV:** não roda sem licença válida da API externa e sem o `licenca.json` com o CNPJ. Um teste
  no PDV exige o responsável cadastrar a licença com um vencimento (HIPÓTESE: é assim que se
  daria um teste por tempo, mas o código da API não está no repositório).
- Em 2026-10-22 o banco tinha 1 loja real e 4 tenants de teste/sem uso (confirmação 4): **não há
  indício de cadastros espontâneos até essa data**. Situação atual não consultada.

### 10.2 Recomendação: piloto assistido, não teste livre

Para o perfil (loja pequena, dono pouco digital, troca de sistema com medo de perder dados), um
teste livre no Portal mostra pouco: o valor está no caixa e no fiado, que exigem instalação,
importação e treinamento. E cada teste custa horas de uma pessoa. Por isso:

| Opção | Prós | Contras | Recomendação |
|---|---|---|---|
| A. Teste gratuito livre (como hoje) | Sem esforço de venda | Não mostra o PDV; contas sem dono no banco de produção; promessa "gratuita" sem regra; suporte não planejado | **Retirar o link** ou trocá-lo por "Fale conosco" até a decisão (EXC-VEN-002) |
| B. Teste gratuito de 14 a 30 dias, assistido | Baixa barreira para o lojista | Implantação inteira sem receita; com 2 implantações simultâneas no máximo, trava o funil | Só para lojas muito alinhadas ao perfil, com limite de 1 por vez |
| C. **Piloto de 30 dias, pago, com saída sem multa** | Compromisso dos dois lados; cobre parte das horas; critérios de sucesso escritos | Barreira maior | **Recomendado** como padrão |

**Termo de piloto (C), mínimo:** data de início e fim; o que será implantado; **3 critérios de
sucesso** escolhidos com o dono antes de começar (ex.: "todo fiado novo lançado no sistema",
"fechamento de caixa feito pelo operador sem ajuda", "nenhuma venda perdida em queda de
internet"); valor do piloto e se ele é abatido da implantação; o que acontece com os dados se a
loja desistir (exportação e exclusão em prazo definido). No fim dos 30 dias: reunião de decisão
(E7) com os critérios medidos.

**Pré-condições iguais às de qualquer loja nova** (parte MKT §9): login restrito do PDV
(EXC-SEG-001), CNPJ do emitente por loja (EXC-FIS-013) e contrato/termos (EXC-FIS-002). Um piloto
é operação real: vende, emite nota, guarda fiado.

---

## 11. Implantação (E9)

### 11.1 Passos de hoje (reconstruídos do código e do checklist)

| # | Passo | Quem faz | Documentado? | Evidência |
|---|---|---|---|---|
| 1 | Criar o tenant (pelo cadastro do Portal ou à mão) | Responsável ou cliente | Parcial (cadastro existe; o caminho manual não) | `CadastroService`; `Scripts/CalcularTenantId.csx` |
| 2 | Cadastrar a licença (CNPJ, vencimento) na API de licenças | Responsável | **Não** | EXC-OPS-005 |
| 3 | Configurar o fiscal: token Focus, certificado, dados do emitente | Responsável | **Não** | `TenantFiscalConfiguration`; EXC-FIS-004, EXC-FIS-013 |
| 4 | Instalar o PDV em cada PC | Responsável, presencial | Parcial | `CHECKLIST_DEPLOY.md` §4 (desatualizado, EXC-OPS-001) |
| 5 | Editar `licenca.json` com o CNPJ em cada PC | Responsável | Sim, mas com o CNPJ da Vila Verde como exemplo | `CHECKLIST_DEPLOY.md` §2 |
| 6 | Digitar servidor, banco, usuário e senha do SQL no primeiro uso | Responsável | **Não** | `SetupViewModel.cs`; EXC-SEG-001 |
| 7 | Configurar o recibo (`config_recibo.json`: nome fantasia, endereço) | Responsável | **Não** | EXC-MKT-002 |
| 8 | Importar produtos e clientes (CSV), XML de compra | Responsável com o lojista | Não (a tela tem prévia) | `ImportacaoService` |
| 9 | Criar usuários e cargos, limites de desconto e sangria | Responsável com o dono | Não | `CadastroService.cs:124-127` (4 cargos padrão) |
| 10 | Treinar operadores e dono | Responsável | Não | — |

Observação (HIPÓTESE, não verificada em execução): o cadastro do Portal cria 26 permissões e o
*seeder* do PDV conhece 28; as duas que faltam no cadastro são `importacao.view` e
`legacyimport.view` (`DbSeeder.cs:249-250`).
O *seeder* roda na abertura do PDV (`App.xaml.cs:169-173`) e parece completar o que falta para o
cargo Administrador. Se não completar, uma loja criada pelo Portal não veria a tela de
importação. Conferir na primeira implantação.

### 11.2 Roteiro de implantação recomendado

| Quando | Atividade | Saída |
|---|---|---|
| D−7 | Pedir ao lojista a exportação do sistema atual (produtos, clientes, saldo de fiado) ou montar a planilha com ele; levantar PCs, impressoras, leitor, internet; certificado digital | Planilhas validadas na prévia do importador; lista de equipamentos |
| D−3 | Criar tenant, licença, fiscal em homologação; importar no ambiente da loja; conferir **total do fiado importado contra o total do sistema antigo** com o dono | Ata de conferência do fiado assinada pelo dono |
| D−1 (fora do horário) | Instalar nos PCs, configurar recibo, usuários e cargos; venda de teste sem nota | PCs prontos |
| D0 | Primeiro dia acompanhado no caixa; fiscal em produção; primeira NFC-e autorizada | Primeira nota real |
| D+1 a D+5 | Suporte diário rápido; registrar cada dúvida (vira base de conhecimento, SUP-2) | Lista de dúvidas |
| D+7 | Revisão com o dono: critérios da seção 11.3 | Implantação concluída ou plano de correção |

**Registrar horas por atividade** em toda implantação: é o dado que falta para preço de
implantação, capacidade e CAC (ADM §6.5).

### 11.3 Critérios de "implantação concluída"

1. Todos os PCs vendendo com a versão atual.
2. Fiado migrado conferido pelo dono (total e amostra de 10 clientes).
3. 5 dias seguidos de vendas sem intervenção do responsável.
4. Pelo menos uma NFC-e autorizada por PC, sem rejeição pendente.
5. Fechamento de caixa feito pelo operador.
6. Dono sabe abrir o Portal e ver o resumo do dia.

### 11.4 O que falta para atender VEN-3

Implantação "executável sem intervenção técnica não documentada" exige, no mínimo: instalador
que não peça credencial de banco (depende do login restrito e da migração para a API); licença
cadastrada por procedimento escrito (ou por painel, com o código da API versionado, EXC-OPS-005);
configuração fiscal por tela ou por procedimento escrito; checklist de implantação atualizado e
genérico (sem dados da Vila Verde). Registrado como **EXC-VEN-003**.

---

## 12. Acompanhamento e renovação (E10, E11)

### 12.1 Ritmo

| Quando | O quê | Formato |
|---|---|---|
| D+30 | Revisão de uso: o que usa, o que não usa, dúvidas, pedidos | Visita ou ligação, 20 min |
| D+60 | Revisão de valor: a dor do diagnóstico melhorou? (medir, ex.: total a receber vencido) | Ligação |
| D+90 | Revisão de relacionamento: satisfação, indicação, autorização para ser citada | Visita |
| Trimestral depois | Revisão curta + novidades da versão | Ligação ou WhatsApp |
| 30 dias antes do vencimento do período contratado | Aviso de renovação e reajuste, por escrito | WhatsApp + e-mail |

### 12.2 Sinais de saúde (medíveis no banco, somente leitura)

| Sinal | Como medir | Alerta (regra, não previsão) |
|---|---|---|
| Uso diário | Vendas por dia útil por loja | Queda forte contra a média das 4 semanas anteriores |
| Uso do diferencial | Vendas a prazo, clientes com limite (consulta da parte MKT §14) | Fiado zerado depois de ter sido usado |
| Problemas fiscais | Notas rejeitadas ou em contingência por semana | Qualquer nota presa por mais de 24 h (EXC-FIS-009) |
| Sincronização | Vendas na fila local há mais de 1 hora | Qualquer ocorrência |
| Suporte | Chamados por mês, por loja | Aumento contínuo por 2 meses |
| Licença | Dias até o vencimento (API de licenças) | Menos de 15 dias sem renovação combinada |

### 12.3 Renovação e cancelamento

- **Mecanismo técnico existe:** a licença tem vencimento, o PDV avisa antes e bloqueia após a
  carência (`LicenseManager.cs:15,43-53`). A renovação é feita pelo responsável na API externa.
- **Falta:** regra de reajuste (sem contrato não há, ADM §5); data de aniversário por cliente;
  aviso escrito; e, para o cancelamento, **exportação dos dados da loja** (não existe) e prazo de
  exclusão (LGPD, EXC-FIS-002). Todo cancelamento recebe motivo da lista da seção 13.4 e uma
  conversa de saída.

---

## 13. Funil: definições e fórmulas

Sem dados, o funil é **um modelo de medição**, não uma previsão. Nenhuma taxa foi preenchida.

### 13.1 Etapas e evento que marca a entrada

| Código | Etapa | Evento de entrada (data registrada) |
|---|---|---|
| N₁ | Contatos | Primeiro contato registrado |
| N₂ | Qualificados | Passou nos critérios obrigatórios da seção 5.1 |
| N₃ | Diagnosticados | Ficha de diagnóstico completa |
| N₄ | Demonstrados | Demonstração feita com o decisor |
| N₅ | Com proposta | Proposta escrita entregue |
| N₆ | Em piloto | Termo de piloto assinado (opcional) |
| N₇ | Clientes | Contrato assinado e primeira cobrança paga |
| N₈ | Implantados | Critérios da seção 11.3 atendidos |
| N₉ | Retidos em t | Ativos t meses depois de N₇ |

### 13.2 Fórmulas

**Conversão entre etapas (por coorte).** Coorte = contatos que entraram em E1 num mesmo período
(mês ou trimestre). Para uma coorte *c*:

```
taxa(k→k+1, c) = N(k+1, c) / N(k, c)
```

contando em N(k+1, c) só quem veio de N(k, c). Calcular **só para coortes fechadas** (todas as
oportunidades já ganhas ou perdidas, ou com mais de 90 dias), senão a taxa sai subestimada.

**Conversão total da coorte:** `taxa(1→7, c) = N₇(c) / N₁(c)` = produto das taxas das etapas.

**Taxa de ganho (*win rate*):** `ganhos / (ganhos + perdidos)` entre as propostas decididas.
Não conta propostas em aberto.

**Tempo de ciclo:** mediana, em dias, de `data(E7 = sim) − data(E1)`, por coorte. Usar mediana,
não média (poucos casos e valores extremos).

**Tempo por etapa:** mediana de `data(entrada k+1) − data(entrada k)`. Mostra onde o funil para.

**Regra de pequenos números:** com menos de 10 casos numa etapa, **reportar contagem**
("3 de 7"), não porcentagem. Para decidir com poucos casos, usar o intervalo de Wilson a 90%:

```
p̂ = x/n,  z = 1,645
centro = (p̂ + z²/2n) / (1 + z²/n)
meia_largura = z · √(p̂(1−p̂)/n + z²/4n²) / (1 + z²/n)
```

Exemplo só de cálculo, **não é dado da TTSoft**: 2 de 6 dá p̂ = 33% e intervalo de ~12% a 65%.
Um intervalo desse tamanho quer dizer que ainda não dá para comparar canais.

**Planejamento reverso** (para quando houver taxas medidas):

```
contatos_necessários = clientes_desejados / taxa(1→7)
horas_necessárias    = Σ_k ( N_k × horas_médias_por_oportunidade_na_etapa_k )
```

Se `horas_necessárias` passa das horas disponíveis para vendas no mês, a meta é inviável para uma
pessoa, independentemente do canal.

**Receita recorrente e retenção:**

| Indicador | Fórmula |
|---|---|
| MRR | Σ mensalidades ativas no último dia do mês (sem implantação e sem excedente de notas, que entram à parte) |
| MRR novo | Σ mensalidades dos clientes que entraram no mês |
| MRR perdido | Σ mensalidades dos clientes que cancelaram no mês |
| MRR de expansão / redução | Σ das variações de mensalidade de clientes existentes (PC adicional, reajuste, desconto) |
| Cancelamento de clientes (mês) | `clientes que cancelaram no mês / clientes ativos no início do mês` |
| Retenção líquida de receita (12 meses) | `(MRR hoje dos clientes ativos há 12 meses) / (MRR deles há 12 meses)` |
| Ticket médio (ARPA) | `MRR / clientes ativos` |
| Inadimplência | `valor vencido há mais de 30 dias / faturamento do mês` |

**Aquisição e retorno:**

| Indicador | Fórmula | Observação |
|---|---|---|
| CAC em dinheiro | `(mídia + deslocamento + eventos + brindes) / clientes novos` no período | Teto provisório da parte MKT §12 |
| CAC total | `CAC em dinheiro + (horas de venda e piloto × valor-hora) / clientes novos` | Valor-hora é decisão de Matheus (ADM §7) |
| Margem de contribuição por cliente | `ARPA − impostos − custo de notas − cobrança − infra alocada` | Modelo da parte ADM |
| Prazo de retorno (*payback*), meses | `(CAC total + custo da implantação − valor de implantação cobrado) / margem de contribuição mensal` | Só com margem positiva |
| Valor do cliente no tempo (LTV) | `margem de contribuição mensal / taxa mensal de cancelamento` | **Não calcular** antes de ~10 clientes e 12 meses de histórico: com 1 cliente e nenhum cancelamento a fórmula dá infinito |
| Capacidade de novos clientes/mês | `horas livres para implantação no mês / horas por implantação` | Liga com a fórmula de capacidade da ADM §6.5 |

### 13.3 Planilha de funil (campos)

Estende os campos da parte MKT §13 com os de venda:

| Grupo | Campos |
|---|---|
| Identificação | id, loja, CNPJ (só após o contato concordar), cidade, contato, telefone |
| Origem | canal (C1 a C7), quem indicou, experimento (X1 a X8) |
| Qualificação | nº de lojas, nº de caixas, regime, sistema atual, preço atual, usa fiado (s/n), volume de notas aproximado, decisor, **no perfil? (s/n) e motivo** |
| Datas | uma coluna por etapa E1 a E11 (data de entrada) |
| Demonstração | blocos mostrados, reação, objeções literais |
| Proposta | mensalidade, implantação, franquia, excedente, condição especial (com prazo) |
| Resultado | situação (aberto / ganho / perdido / adiado / cancelado), motivo principal, data de retorno |
| Esforço | horas por etapa, custos em R$ |

Dados pessoais de contato ficam só na planilha, que deve ficar fora do repositório e com acesso
restrito.

### 13.4 Motivos de perda e cancelamento (lista fechada)

Fora do perfil · Preço · Falta TEF · Falta app · Falta SPED completo · Outro recurso (qual) ·
Confiança / empresa pequena · Medo da troca / migração · Já contratou outro · Não é o momento
(com data de retorno) · Sem resposta · Problema na implantação · Problema fiscal · Suporte ·
Fechou a loja.

---

## 14. Indicadores comerciais (painel mensal)

| Indicador | Fórmula (seção 13) | Frequência | Faz sentido a partir de | Serve para |
|---|---|---|---|---|
| Contatos e qualificados por canal | N₁, N₂ por canal | Semanal | Já | Escolher canal (MKT-3) |
| Conversão por etapa (por coorte) | taxa(k→k+1, c) | Mensal | Coorte fechada; contagem se n < 10 | Achar o gargalo |
| Taxa de ganho | ganhos / decididas | Mensal | ~10 propostas decididas | Proposta e preço |
| Motivos de perda | contagem por motivo | Mensal | Já | Produto e preço |
| Tempo de ciclo e por etapa | medianas | Trimestral | ~5 ganhos | Planejar capacidade |
| Horas por cliente novo | Σ horas / N₇ | Por cliente | 1º cliente novo | Capacidade, CAC total |
| Horas por implantação | Σ horas E9 | Por cliente | 1ª implantação nova | Preço de implantação |
| MRR, novo, perdido, expansão | seção 13.2 | Mensal | Já (MRR atual declarado: R$ 89,90 do ConstruTTor) | Crescimento |
| Cancelamento mensal | seção 13.2 | Mensal | ~10 clientes | Retenção |
| Inadimplência | seção 13.2 | Mensal | Forma de cobrança definida (EXC-ADM-004) | Caixa |
| CAC em dinheiro e total | seção 13.2 | Por cliente | 1º cliente novo | Teto de aquisição |
| *Payback* | seção 13.2 | Trimestral | Margem medida (EXC-ADM-003) | Decidir se acelera aquisição |
| Saúde da base | sinais da seção 12.2 | Mensal | Já (Vila Verde) | Evitar cancelamento |

**Metas:** nenhuma. Metas entram depois de 2 coortes fechadas, a partir das taxas medidas,
e conferidas contra a capacidade (planejamento reverso, seção 13.2).

---

## 15. O que existe e o que precisa ser criado

| Item | Existe? | Criar | Depende de | Ordem |
|---|---|---|---|---|
| Planilha de funil | Não | Seção 13.3 | Nada | 1 |
| Perguntas de qualificação e ficha de diagnóstico | Não | Seções 5.1 e 5.2 | Nada | 1 |
| Decisão sobre o link "Criar conta gratuita" | Link ativo no código | Retirar ou trocar por "Fale conosco" | Decisão de Matheus | 1 |
| Tenant de demonstração com dados de exemplo | Não | Seção 6.1 (P2, P3) | EXC-MKT-002 publicado | 2 |
| Roteiro de demonstração ensaiado | Não | Seção 6.2 | Tenant de demonstração | 2 |
| Teste do importador com exportações reais | Importador existe, sem teste | 2 ou 3 sistemas comuns na região | Planilhas de lojistas | 2 |
| Preço, franquia, excedente, implantação | Valores soltos | Seção 9.2 | EXC-ADM-003, X5 | 3 |
| Modelo de proposta | Não | Seção 8 | Preço decidido | 3 |
| Contrato, termos, DPA | Não | Com assessoria jurídica | EXC-FIS-002 | 3 (bloqueia E8) |
| Termo de piloto | Não | Seção 10.2 | Contrato | 4 |
| Roteiro de implantação genérico | Checklist da Vila Verde, desatualizado | Seção 11.2 | EXC-OPS-001 | 4 |
| Instalação sem credencial de banco | Não | Login restrito, depois API | EXC-SEG-001 | 4 (bloqueia E9 com loja nova) |
| CNPJ do emitente por loja | Não | Correção de código | EXC-FIS-013 | 4 (bloqueia E9) |
| Exportação completa dos dados da loja | Não | Função ou procedimento | Decisão de produto | 5 (antes do primeiro cancelamento) |
| Ficha de saúde e consultas de uso | Consulta de uso por módulo pronta (MKT §14) | Seção 12.2 | Nada | 5 |
| Painel da TTSoft (lojas, licenças, vencimentos) | Não (licença na API externa) | Planilha basta até ~10 clientes | EXC-OPS-005 | Depois |

---

## 16. Registros propostos e próximos passos

Registros novos no [REGISTRO-CENTRAL](REGISTRO-CENTRAL.md) (diagnóstico, não tarefa):

| ID | Título | Natureza | Gravidade |
|---|---|---|---|
| EXC-VEN-002 | Cadastro público "Criar conta gratuita" cria loja no banco de produção sem plano, prazo, termos nem contrato | Fato (código) | Média |
| EXC-VEN-003 | Implantação de loja nova depende de passos técnicos manuais e não documentados | Fato | Média |
| EXC-VEN-004 | Não existe processo comercial documentado nem registro de propostas | Lacuna | A classificar |

Atualização de registro existente: EXC-VEN-001 ganha a estrutura de plano recomendada
(seção 9.2) como hipótese a testar.

Decisões que são de Matheus:

1. O que fazer com o link **"Criar conta gratuita"** agora (retirar, trocar por contato, ou
   manter com regras).
2. **Piloto pago, gratuito ou sem piloto** (seção 10.2), e se o valor é abatido da implantação.
3. **Cobrar implantação** e quanto; quem paga o certificado digital.
4. **Número de PCs incluídos** no plano e preço do PC adicional.
5. Tempo de resposta do suporte que pode ser prometido.
6. Aprovar o processo da seção 4 como padrão (com as regras 1 a 5).

Ordem sugerida: planilha de funil e ficha de diagnóstico → decisão sobre o cadastro gratuito →
diagnósticos (junto com X1) → tenant e roteiro de demonstração → demonstrações (com X5) →
preço e proposta → contrato → piloto ou implantação só com as pré-condições da parte MKT §9.

Fora do escopo desta passada: conversas com clientes, reconstituição da proposta da Eldorado,
redação do contrato, e qualquer taxa de conversão, ciclo ou receita projetada. O que não está nas
tabelas acima **não foi estimado**.

---

## Histórico do documento

| Data | Versão | Alteração |
|---|---|---|
| 2026-10-26 | 1.0 | Criação: inventário do que existe, processo E1 a E11, qualificação, roteiro de demonstração, objeções, proposta, planos, piloto, implantação, acompanhamento, funil com fórmulas e painel; registros EXC-VEN-002 a 004 |
