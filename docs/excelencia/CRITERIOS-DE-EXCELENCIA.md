# Critérios de Excelência — ConstruTTor ERP

> Programa de Excelência TTSoft · Documento de referência
> Criado em: 2026-10-22 · Versão: 1.0
> Documentos relacionados: [PLANO-MESTRE](PLANO-MESTRE.md) · [INVENTARIO-AUDITORIAS](INVENTARIO-AUDITORIAS.md) · [REGISTRO-CENTRAL](REGISTRO-CENTRAL.md)

Este documento define **como** as descobertas são classificadas e **quando** uma área ou
descoberta pode ser considerada resolvida. Ele vale para todas as auditorias do programa.

---

## 1. Regras de evidência

1. **Ausência de evidência não é prova de defeito.** Se algo não foi encontrado, registra-se
   como *Lacuna*, não como *Fato*.
2. **Toda afirmação precisa de evidência rastreável**: `arquivo:linha`, hash de commit, nome
   de teste com resultado e data, log, captura de tela ou documento externo identificado.
3. **Evidência tem data.** O código muda; uma verificação vale para o commit em que foi feita.
   Registre sempre o commit ou a data da verificação.
4. **Teste em InMemory ou SQLite não comprova comportamento no SQL Server/Azure SQL**
   (SQL bruto, filtros globais, transações, concorrência, collation). Para afirmar
   comportamento em produção, a evidência deve vir de SQL Server real ou do próprio ambiente.
5. **Documento antigo não é evidência do estado atual.** Relatórios, checklists e commits
   anteriores indicam onde olhar; a confirmação vem do código e do ambiente atuais.
6. **Não se inventam números.** Indicadores comerciais, financeiros ou de uso só entram com
   fonte. Sem dados, registra-se a lacuna.

## 2. Natureza da descoberta

| Natureza | Quando usar | Exige |
|---|---|---|
| **Fato** | Comportamento ou estado verificado diretamente | Evidência reproduzível |
| **Hipótese** | Risco plausível ainda não confirmado | Descrição de como confirmar ou descartar |
| **Lacuna** | Evidência necessária que não está disponível | Quem pode fornecê-la |
| **Recomendação** | Melhoria proposta sem defeito associado | Justificativa e benefício esperado |

Uma Hipótese só vira Fato com evidência. Uma Hipótese descartada é encerrada como
`Descartado`, com a evidência do descarte.

## 3. Escala de gravidade

A gravidade mede o **impacto real se o problema ocorrer**, considerando o contexto atual
(clientes em produção, dados envolvidos). A probabilidade é tratada em *Impacto* e
*Confiança*, não inflando a gravidade.

| Gravidade | Critério | Exemplos típicos |
|---|---|---|
| **Crítica** | Vazamento de dados entre clientes (tenants); perda ou corrupção de dados financeiros, fiscais ou de estoque; nota fiscal incorreta autorizada na SEFAZ; exploração remota sem autenticação; parada total da operação da loja | Consulta sem filtro de tenant em produção; venda gravada sem conta a receber |
| **Alta** | Falha em fluxo crítico sem contorno razoável; escalada de privilégio dentro de um tenant; exposição de credencial ativa; risco fiscal com multa provável | Operador sem permissão consegue cancelar nota; segredo ativo exposto |
| **Média** | Falha com contorno; risco que exige condições específicas; ausência de controle importante sem exploração conhecida | Relatório com total divergente corrigível manualmente |
| **Baixa** | Inconsistência sem impacto direto em dados, segurança ou fiscal | Documentação desatualizada; avisos de compilação |
| **Informativa** | Observação útil sem necessidade de ação | Padrão de código divergente sem efeito |
| **A classificar** | Impacto depende de evidência ainda ausente | Usar junto com uma Hipótese ou Lacuna |

Regra: quando a gravidade depender de uma confirmação, registre **"A classificar"** e
descreva no campo *Impacto* qual seria a gravidade em cada cenário.

## 4. Escala de confiança

| Confiança | Significado |
|---|---|
| **Alta** | Verificado diretamente (teste executado, código lido, ambiente consultado) |
| **Média** | Inferido de evidência indireta consistente (código lido sem execução, configuração) |
| **Baixa** | Indício isolado, relato ou documento antigo não confirmado |

## 5. Prioridade de atenção

Em empate de gravidade, prioriza-se nesta ordem:

1. Integridade dos dados (financeiro, estoque, vendas).
2. Isolamento entre clientes.
3. Segurança (autenticação, autorização, segredos).
4. Confiabilidade fiscal.
5. Funcionamento dos fluxos críticos (seção 7).
6. Demais áreas.

## 6. Fluxo de status de uma descoberta

```
Hipótese / Lacuna ──► Confirmado ──► Triado ──► Planejado ──► Em andamento ──► Em validação ──► Encerrado
        │                  │            │
        ├─► Descartado     ├─► Duplicado de EXC-…    └─► Risco aceito
```

| Status | Significado |
|---|---|
| Hipótese / Lacuna | Registrado, ainda sem confirmação |
| Confirmado | Evidência suficiente; ainda sem decisão de ação |
| Triado | Gravidade, responsável e ação proposta definidos |
| Planejado | Entrou em um plano de execução com data ou etapa |
| Em andamento | Correção ou trabalho em execução |
| Em validação | Trabalho concluído, aguardando o critério de validação |
| Encerrado | Critério de validação atendido e evidência anexada |
| Descartado | Hipótese refutada, com evidência |
| Duplicado de | Mesmo problema de outro registro (indicar o ID) |
| Risco aceito | Decisão consciente de não agir, com justificativa, responsável e data de revisão |

**Nem toda descoberta vira tarefa imediatamente.** A passagem de *Confirmado* para
*Planejado* é uma decisão explícita, tomada na revisão do registro (ver PLANO-MESTRE §6).

## 7. Fluxos críticos

Fluxos cuja falha afeta diretamente a operação, o caixa ou a situação fiscal da loja.
Descobertas que os atingem recebem prioridade dentro da mesma gravidade.

1. Login, sessão e permissões (WPF, API, Portal).
2. Venda no PDV (online e offline), incluindo pagamentos divididos e desconto.
3. Abertura, movimentação e fechamento de caixa (sangria, suprimento).
4. Emissão, cancelamento, contingência e recuperação de NFC-e / NF-e; devolução.
5. Contas a receber (a prazo/crediário, haver) e contas a pagar.
6. Movimentação de estoque (venda, devolução, ajuste, compra, transferência).
7. Sincronização offline → servidor (outbox, idempotência).
8. Integrações que geram venda (Mercado Livre, e-commerce).
9. Atualização do aplicativo WPF.

## 8. Definição de pronto para encerrar uma descoberta

Uma descoberta só passa a **Encerrado** quando:

1. O critério de validação definido no registro foi atendido.
2. A evidência do atendimento está anexada (commit, teste com nome e resultado, consulta,
   captura), com data.
3. Para defeitos: existe um teste automatizado de regressão **ou** a justificativa de por
   que não é viável (e qual verificação manual substitui).
4. Para itens de dados/tenant/fiscal: a verificação foi feita no ambiente relevante
   (SQL Server real ou staging), não apenas em InMemory.
5. Documentação afetada foi atualizada.

## 9. Critérios de excelência por área

Cada critério é verificável. Uma área é considerada **excelente** quando todos os seus
critérios estão atendidos com evidência, ou formalmente aceitos como risco.
Os critérios serão refinados na ficha de cada auditoria; esta é a linha de base.

### 1. Administração e finanças (evidência externa)
- ADM-1 Custos de infraestrutura e serviços (Azure, Focus NFe, certificados, domínios) conhecidos e revisados mensalmente.
- ADM-2 Receita recorrente por cliente registrada com fonte (contratos/faturas).
- ADM-3 Contratos com clientes vigentes e arquivados.
- ADM-4 Fornecedores críticos identificados, com plano de contingência.

### 2. Marketing (evidência externa)
- MKT-1 Público-alvo e proposta de valor documentados, validados com clientes reais.
- MKT-2 Materiais de divulgação coerentes com as funcionalidades efetivamente entregues.
- MKT-3 Canais de aquisição medidos com dados reais (sem números estimados apresentados como fatos).

### 3. Vendas e crescimento comercial (evidência externa)
- VEN-1 Processo comercial documentado (prospecção, demonstração, implantação, cobrança).
- VEN-2 Política de preço e planos definida.
- VEN-3 Onboarding de nova loja executável sem intervenção técnica não documentada.

### 4. Engenharia de software e arquitetura (auditável no código)
- ENG-1 Todo acesso a dados de clientes passa por camada de servidor autenticada e autorizada.
- ENG-2 Build sem avisos de segurança; avisos restantes conhecidos e justificados.
- ENG-3 Testes cobrem os fluxos críticos, incluindo execução contra SQL Server real para SQL bruto, filtros globais e transações.
- ENG-4 Documentação de arquitetura reflete o estado atual (revisada a cada mudança estrutural).
- ENG-5 Decisões arquiteturais relevantes registradas (motivo, alternativas, data).

### 5. Produto, funcionalidades e experiência do usuário (código + usuários)
- PRD-1 Fluxos críticos testados com usuários reais de loja, com problemas registrados.
- PRD-2 Roadmap e comunicação ao cliente coerentes com o que está entregue.
- PRD-3 Mensagens de erro compreensíveis pelo operador e com ação clara.
- PRD-4 Escopo de cada aplicativo (WPF, Portal, Mobile) definido.

### 6. Segurança e qualidade (auditável no código + ambiente)
- SEG-1 Nenhum segredo ativo em repositório ou em clientes desktop sem necessidade; segredos já expostos foram trocados.
- SEG-2 Isolamento entre tenants comprovado por testes contra SQL Server real e por revisão de todos os pontos que ignoram filtros.
- SEG-3 Dependências sem vulnerabilidades conhecidas de gravidade alta ou crítica.
- SEG-4 Autenticação, autorização (RBAC) e limites de requisição cobertos por testes negativos.
- SEG-5 Atualizações do aplicativo têm integridade e autenticidade verificadas.

### 7. Infraestrutura, implantação e operações (CI no código + Azure)
- OPS-1 Implantação em produção passa por staging funcional e tem aprovação explícita.
- OPS-2 Backup com restauração testada e registrada nos últimos 90 dias.
- OPS-3 Monitoramento com alertas para falhas da API, da emissão fiscal e da sincronização.
- OPS-4 Procedimentos de deploy e rollback documentados e atualizados.
- OPS-5 Fonte única de versão por componente.

### 8. Suporte e sucesso do cliente (evidência externa)
- SUP-1 Canal de suporte definido, com registro de chamados.
- SUP-2 Base de conhecimento para os problemas recorrentes.
- SUP-3 Acompanhamento de implantação de cada cliente.

### 9. Fiscal, jurídico e conformidade (código + contador + documentos)
- FIS-1 Regras tributárias implementadas validadas por contador, com casos documentados.
- FIS-2 Emissão, cancelamento, contingência e recuperação testados em homologação, com evidência.
- FIS-3 Separação garantida entre ambiente de homologação e produção.
- FIS-4 Termos de uso, política de privacidade (LGPD) e contrato de licença publicados e vigentes.
- FIS-5 Inventário de dados pessoais tratados, com base legal e retenção.

### 10. Dados, processos e governança (código + processo)
- DAD-1 Migrations reproduzíveis e aplicadas de forma controlada.
- DAD-2 Política de retenção de dados e logs definida e implementada.
- DAD-3 Registro das auditorias e decisões mantido neste programa (este conjunto de documentos).
- DAD-4 Processo de release (versão, notas, comunicação) definido.
