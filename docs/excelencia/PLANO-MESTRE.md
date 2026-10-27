# Plano Mestre — Programa de Excelência TTSoft

> Produto: ConstruTTor ERP
> Criado em: 2026-10-22 · Versão: 1.8 · Fase atual: **EXC-A0 concluída; EXC-A1 a iniciar**
> Documentos relacionados: [CRITERIOS-DE-EXCELENCIA](CRITERIOS-DE-EXCELENCIA.md) · [INVENTARIO-AUDITORIAS](INVENTARIO-AUDITORIAS.md) · [REGISTRO-CENTRAL](REGISTRO-CENTRAL.md)

---

## 1. Objetivo

Construir um produto confiável, seguro, fiscalmente consistente, fácil de usar, sustentável e
preparado para crescer comercialmente. O objetivo **não** é lançar rapidamente.

### Princípios

1. Evidência antes de conclusão (ver CRITERIOS §1).
2. Fato, hipótese, lacuna e recomendação são registrados separadamente.
3. Nada é registrado duas vezes; problemas já corrigidos não são reabertos sem nova evidência.
4. Gravidade pelo impacto real, sem exagero.
5. Nenhuma reescrita completa é recomendada sem demonstração de necessidade.
6. Descoberta não é tarefa: a decisão de agir é explícita (§6).
7. Prioridade: integridade dos dados, isolamento entre clientes, segurança, confiabilidade
   fiscal e fluxos críticos.

### Fora do escopo do programa

- Indicadores comerciais ou financeiros sem fonte.
- Alterações de código durante as auditorias (as correções seguem o fluxo do §6).

### Convenção de nomes

Para não colidir com nomes já usados no projeto (por exemplo, os testes `FASE0` e `F11`
de maio/2026 e as fases A/B/C da migração WPF→API), tudo deste programa usa o prefixo **EXC**:

| Item | Formato | Exemplo |
|---|---|---|
| Fase do programa | `EXC-F#` | EXC-F0 |
| Auditoria | `EXC-A#` | EXC-A1 |
| Descoberta | `EXC-<ÁREA>-NNN` | EXC-SEG-001 |
| Trabalho anterior inventariado | `INV-NNN` | INV-007 |

Códigos de área: `ADM`, `MKT`, `VEN`, `ENG`, `PRD`, `SEG`, `OPS`, `SUP`, `FIS`, `DAD`.

---

## 2. Áreas do programa e tipo de evidência

| # | Área | Código | Tipo de evidência | Auditável no código? |
|---|---|---|---|---|
| 1 | Administração e finanças | ADM | Contratos, faturas, custos, dados financeiros | Não |
| 2 | Marketing | MKT | Materiais, canais, entrevistas | Não |
| 3 | Vendas e crescimento comercial | VEN | Processo comercial, clientes, preços | Não |
| 4 | Engenharia de software e arquitetura | ENG | Código, testes, CI | **Sim** |
| 5 | Produto, funcionalidades e UX | PRD | Código + observação de usuários reais | Parcial |
| 6 | Segurança e qualidade | SEG | Código, dependências, configuração + ambiente | **Sim** (maior parte) |
| 7 | Infraestrutura, implantação e operações | OPS | Workflows no repositório + Azure | Parcial |
| 8 | Suporte e sucesso do cliente | SUP | Chamados, relatos, implantações | Não |
| 9 | Fiscal, jurídico e conformidade | FIS | Código fiscal + contador + documentos legais | Parcial |
| 10 | Dados, processos e governança | DAD | Modelo de dados, migrations + processos | Parcial |

Áreas sem evidência disponível registram **Lacunas**, não indicadores estimados.

---

## 3. Estado da Fase EXC-F0 (preparação) — 2026-10-22

### Examinado

- Estrutura do repositório: 10 projetos (.NET 8: Domain, Application, Infrastructure,
  Persistence, Api, WPF, Portal Blazor, Mobile MAUI, Updater, Tests); 88 migrations.
- Os 225 commits de 2026-05-19 a 2026-10-21 (ponto de partida: commit `ec6d177`).
- Documentação versionada: `README.md`, `Scripts/CHECKLIST_DEPLOY.md`,
  `CHECKLIST_REGRESSAO_MERCADO_LIVRE.md`, `ERP.Portal/Pages/Roadmap.razor`.
- Workflows `.github/workflows/ci-cd.yml` e `staging.yml`.
- Suíte de testes executada localmente em 2026-10-22, commit `ec6d177`:
  **1322 aprovados, 0 falhas, 0 ignorados** (~2 min). Filtros do CI `FASE0` (4 testes) e
  `F11` (9 testes) selecionam e aprovam testes.
- Verificações pontuais registradas no [REGISTRO-CENTRAL](REGISTRO-CENTRAL.md).

- 35 relatórios anteriores, entregues em 2026-10-22 (fora do repositório), inventariados e
  reconciliados com o código atual (INVENTARIO §2 e §4).

### Não acessível nesta fase

- Relatórios citados e não localizados (INVENTARIO §2.3).
- Histórico de execuções do CI no GitHub.
- Ambiente Azure (bancos, App Service, Application Insights, backups, Blob de atualizações).
- Conta Focus NFe, SEFAZ, certificados digitais.
- Build piloto `C:\ERP\ConstruTTor_Piloto_x86` (não inspecionado).
- Dados de clientes, chamados de suporte, finanças, contratos, documentos legais.

---

## 4. Sequência de auditorias

| ID | Auditoria | Áreas | Depende de | Pode rodar em paralelo com | Justificativa |
|---|---|---|---|---|---|
| EXC-A0 | Linha de base e reconciliação | DAD (todas) | — | — | Evita reabrir o que já foi corrigido |
| EXC-A1 | Isolamento entre clientes e acesso a dados | SEG, ENG, DAD | A0 | A5 | Maior impacto potencial; acesso direto do WPF ao SQL (EXC-SEG-001) |
| EXC-A2 | Segurança de aplicação, segredos e dependências | SEG | A0; parte de A1 | A5 | Credenciais e segredos se cruzam com A1 |
| EXC-A3 | Integridade transacional (venda, caixa, financeiro, estoque, offline) | ENG, DAD | A1 | A5 | O fiscal depende de venda consistente |
| EXC-A4 | Fiscal técnico | FIS | A3; contador; Focus | A5 | Módulo em mudança (Etapa 4); auditar sem atrapalhar |
| EXC-A5 | Infraestrutura, implantação e operações | OPS | A0; acesso ao Azure | A1–A4 | Deploy direto da `main`, staging, backup |
| EXC-A6 | Arquitetura e engenharia | ENG | A1–A3 | — | Consolida achados; reescrita só com demonstração |
| EXC-A7 | Produto e UX dos fluxos críticos | PRD | A3; usuários do piloto | A8 | Exige observação real |
| EXC-A8 | Dados e governança | DAD | A5 | A7 | Migrations, retenção, LGPD, release |
| EXC-A9 | Fiscal-jurídico e conformidade | FIS | A8; documentos externos | — | Termos, privacidade, contratos |
| EXC-A10 | Suporte, administração, marketing e vendas | SUP, ADM, MKT, VEN | Dados da empresa | qualquer | Sem dados, o resultado é lacuna |

### Fichas das auditorias

Cada auditoria só começa com a ficha aprovada. Campos obrigatórios:

```
### EXC-A# — <nome>
Status: Não iniciada | Ficha em revisão | Em execução | Concluída
Escopo:                 o que será examinado (módulos, fluxos, ambientes)
Fora do escopo:         o que explicitamente não será examinado
Evidências necessárias: o que precisa estar acessível, e de quem
Método:                 leitura de código, testes, consultas, entrevistas
Critérios de aprovação: quais critérios da CRITERIOS §9 serão verificados
Condições de encerramento: o que precisa existir para declarar a auditoria concluída
Saídas:                 registros EXC-… criados/atualizados
```

#### EXC-A0 — Linha de base e reconciliação
- **Status:** **Concluída em 2026-10-22**, após a primeira triagem (§6.5).
  Relatórios reconciliados (22 pendências herdadas encerradas com evidência, 14 convertidas em
  registros, 9 adiadas para auditorias específicas — INVENTARIO §4). As 11 confirmações da
  seção 7 foram respondidas em 2026-10-22.
- **Escopo:** relatórios anteriores, commits, documentos versionados; confronto com o código atual.
- **Evidências necessárias:** atendidas. Relatórios faltantes (INVENTARIO §2.3) seguem em
  EXC-DAD-001, sem bloquear o encerramento.
- **Critérios de aprovação:** cada item do INVENTARIO com estado *Confirmado*, *Divergente*
  ou *Sem relatório — não verificável*; pendências herdadas registradas sem duplicação.
- **Encerramento:** inventário revisado pelo responsável; nenhuma pendência dos relatórios
  antigos sem destino.

#### EXC-A1 — Isolamento entre clientes e acesso a dados
- **Status:** Em execução. Desenho do login restrito aprovado (execução aguarda pré-verificações P1 a P4):
  [EXC-A1-DESENHO-LOGIN-RESTRITO](EXC-A1-DESENHO-LOGIN-RESTRITO.md) (2026-10-22).
  Já há evidência coletada na EXC-A0: permissões do login atual, inventário de tenants e
  registros sem `TenantId` (ROTEIRO-CONFIRMACOES-A0).
- **Escopo:** filtros globais de tenant (`HasQueryFilter`), todos os usos de
  `IgnoreQueryFilters` e de SQL bruto fora dos testes, resolução de tenant na API,
  acesso direto do WPF ao banco (credencial, permissões do login SQL), armazenamento de
  arquivos, webhooks e integrações, Portal.
- **Fora do escopo:** regras fiscais; desempenho.
- **Evidências necessárias:** acesso de leitura ao Azure SQL (usuários e permissões),
  confirmação de como a string de conexão é distribuída às lojas.
- **Critérios de aprovação:** SEG-2, ENG-1 (diagnóstico, não correção).
- **Encerramento:** cada ponto de acesso classificado (seguro / risco / defeito) com
  evidência; EXC-SEG-001 e EXC-ENG-001 confirmados ou descartados.

As fichas de EXC-A2 a EXC-A10 serão preenchidas antes de cada auditoria e aprovadas pelo
responsável.

---

## 5. Papéis

| Papel | Responsabilidade | Atribuído a |
|---|---|---|
| Patrocinador / decisor | Aprova fichas, prioriza e decide sobre riscos aceitos | Matheus |
| Auditor técnico | Executa auditorias das áreas 4–7, 9, 10 | Claude Code, sob revisão de Matheus |
| Responsáveis por área | Fornecem evidências e executam ações | Matheus (todas as áreas) |
| Apoio externo | Validação fiscal e contábil; orientação jurídica | Contador da loja piloto (fiscal); assessoria jurídica a contratar (EXC-FIS-002) |

Confirmado em 2026-10-22: a empresa tem uma pessoa (EXC-ADM-002). Consequências para o
programa:
- a capacidade é o principal limite: as auditorias seguem em sequência, sem paralelismo;
- não há revisão por outra pessoa, então cada correção derivada do programa precisa de
  critério de validação objetivo (teste, consulta ou evidência) antes de chegar à produção;
- decisões fiscais e jurídicas dependem de apoio externo, e por isso aparecem como
  dependência nos registros.

---

## 6. Ritos

1. **Revisão do registro** a cada auditoria concluída (ou quinzenal): triagem dos itens
   *Confirmado*, decisão sobre o que passa a *Planejado*, revisão de *Risco aceito* vencido.
2. **Das descobertas às tarefas:** só itens *Triado* com ação aprovada entram em plano de
   execução. Correções no módulo fiscal em andamento (Etapa 4) são coordenadas com o plano
   daquele trabalho.
3. **Mudanças em produção:** enquanto EXC-OPS-002 estiver aberto (deploy automático da
   `main`), correções derivadas do programa devem ser validadas antes do merge na `main`.
4. **Versionamento destes documentos:** alterações via commit, com nota no histórico abaixo.

### 6.5 Decisões de triagem

#### Triagem 1 — 2026-10-22 (encerramento da EXC-A0)

Itens Críticos e Altos. Decididos por Matheus, que aceitou as recomendações.

| Registro | Gravidade | Decisão | Tipo | Quando |
|---|---|---|---|---|
| EXC-FIS-008 | Crítica | Parar de usar o tenant de teste com o CNPJ da Vila Verde; levar ao contador a lista das notas de teste (6 confirmadas e 5 a identificar no painel da Focus) | Ação imediata, sem código | Agora |
| EXC-SEG-001 + EXC-SEG-006 | Crítica + Alta | Login exclusivo do ConstruTTor, restrito ao `ERPTTSoftStaging` e ao mínimo que o WPF usa, com senha nova; reconfigurar os 3 PCs fora do horário da loja | Mudança de produção planejada | Desenho no início da EXC-A1; execução em janela combinada |
| EXC-FIS-009 | Alta | Decidir com o contador a regularização da venda `1A7F2760…`; correção da classificação de erros na EXC-A4 | Ação imediata sem código + correção futura | Agora / EXC-A4 |
| EXC-OPS-003 | Alta | Ver a retenção no portal e restaurar uma cópia para um banco novo, sem tocar no de produção | Verificação antecipada da EXC-A5 | Antes da EXC-A1 terminar |

Os demais registros seguem para as auditorias indicadas em cada um.

#### Triagem 2 — 2026-10-22 (pré-verificações da EXC-A1)

Decidida por Matheus.

| Registro | Gravidade | Decisão | Tipo | Quando |
|---|---|---|---|---|
| EXC-SEG-009 | Crítica | (1) Rodar a consulta corrigida de sessões em horário de uso da loja e do cliente do OutfiTT e anotar o IP atual de cada um; (2) criar ou ajustar as regras específicas que faltarem; (3) remover a regra `Liberar` fora do horário das lojas, pronta para ser recriada se alguém perder acesso; (4) revisar as regras sem efeito ou de pessoas sem acesso previsto | Mudança de produção prioritária | Antes da etapa E1 do login restrito |

**Revisão da triagem 2 (2026-10-22):** Matheus decidiu manter a regra aberta, porque os IPs
públicos da região mudam cerca de 4 vezes por semana. EXC-SEG-009 passou a **Risco aceito**,
com revisão ao final da E6 e as condições registradas no próprio registro. Consequência: com o
servidor aberto, a senha é a única barreira, e o login restrito e a troca de senha passam a ser
a mitigação principal.

Ordem resultante das ações de produção: login restrito do WPF (E1 a E4) → usuários próprios da
API e do OutfiTT (E5) → troca da senha de `sa_ttsoft` (E6) → revisão do EXC-SEG-009.
Avaliar a auditoria do servidor (EXC-SEG-008) em paralelo, como controle compensatório.

---

## 7. Confirmações da EXC-A0 (todas respondidas em 2026-10-22)

| # | Pergunta | Registro relacionado |
|---|---|---|
| 1 | ~~Onde estão os relatórios das auditorias anteriores?~~ Respondida em 2026-10-22 (35 relatórios entregues; faltam os do INVENTARIO §2.3) | EXC-DAD-001 |
| 2 | Qual login SQL o WPF usa em cada loja e quais permissões ele tem? **Concluída (2026-10-22):** o login das lojas é o administrador do servidor SQL (`dbo`/`db_owner` no banco, acesso a todos os bancos do servidor) | EXC-SEG-001, EXC-SEG-008 |
| 3 | Os segredos removidos do repositório em jun/2026 foram trocados? **Respondida (2026-10-22):** sim | EXC-SEG-003 (encerrado) |
| 4 | Quantas lojas estão em produção, com qual versão do WPF, e o banco é compartilhado? A Vila Verde já usa o banco na Azure? **Concluída (2026-10-22):** uma loja real (Vila Verde, 3 PCs confirmados no banco) e 4 tenants de teste/sem uso no mesmo banco; API e WPF no `ERPTTSoftStaging`; PCs atualizados com o artefato do último push (declarado; hash por PC não verificado) | EXC-SEG-001, EXC-OPS-004, EXC-OPS-007, EXC-FIS-008 |
| 5 | Estado do interruptor de recuperação fiscal por tenant em produção. **Concluída (2026-10-22):** `FiscalRecovery__TenantsHabilitados` não existe no App Service de produção; nenhuma loja usa a recuperação nova; 1 NFC-e da Vila Verde presa no caminho antigo | EXC-FIS-001 (encerrado), EXC-FIS-007 |
| 6 | Política de backup e último teste de restauração do Azure SQL. **Respondida (2026-10-22):** backup provavelmente existe; restauração nunca testada | EXC-OPS-003 |
| 7 | Onde estão termos de uso, política de privacidade e contrato de licença? **Respondida (2026-10-22):** não existem | EXC-FIS-002 |
| 8 | O app Mobile segue no escopo do produto? **Respondida (2026-10-22):** sim, pausado (iOS exige Mac) | EXC-PRD-001 |
| 9 | Quem são os responsáveis por área? **Respondida (2026-10-22):** só o responsável | §5, EXC-ADM-002 |
| 10 | Onde está o código da API de licenças? **Respondida (2026-10-22):** só no PC do responsável | EXC-OPS-005 |
| 11 | Quais custos mensais (Azure, Focus) e preços praticados existem hoje? **Respondida (2026-10-22)** | EXC-ADM-001, EXC-VEN-001 |

---

## 8. Riscos de conduzir o programa sem organização

- Reabrir problemas já corrigidos por falta dos relatórios antigos.
- Tratar a suíte verde (InMemory/SQLite) como prova de isolamento ou de comportamento no SQL Server.
- Gravidade inflada e hipóteses tratadas como fatos, levando a reescritas desnecessárias.
- Correções pulverizadas indo direto para produção pelo deploy automático da `main`,
  inclusive no fiscal em mudança.
- Indicadores comerciais inventados para preencher as áreas 1–3.

---

## 9. Próximas ações

1. Executar as ações imediatas da triagem 1 (§6.5), que não envolvem código: tenant de teste,
   contador (notas de teste e venda presa) e teste de restauração.
2. Revisar e aprovar a ficha da EXC-A1 e começar pelo desenho do login restrito.
3. Executar a EXC-A1.

---

## Histórico do documento

| Data | Versão | Alteração |
|---|---|---|
| 2026-10-22 | 1.0 | Criação na fase EXC-F0 |
| 2026-10-22 | 1.1 | Relatórios anteriores recebidos; EXC-A0 atualizada; confirmações 10 e 11 |
| 2026-10-22 | 1.2 | Confirmações 2, 4 e 5 parcialmente respondidas; roteiro de verificação somente leitura |
| 2026-10-22 | 1.3 | Confirmação 2 concluída; resultados do banco registrados (EXC-SEG-001 Crítica; EXC-FIS-007 e EXC-FIS-008 Altas) |
| 2026-10-22 | 1.4 | Confirmação 5 concluída (interruptor ausente em produção); EXC-FIS-001 encerrado |
| 2026-10-22 | 1.5 | Consultas E1 e E2: EXC-FIS-008 Crítica; novos EXC-FIS-009 e EXC-FIS-010; EXC-FIS-007 corrigido e rebaixado a Média |
| 2026-10-22 | 1.6 | Consultas E6 a E9 e painel da Focus: emitente das notas de teste confirmado; causa da NFC-e presa (EXC-FIS-011) |
| 2026-10-22 | 1.7 | Confirmações 3 e 6 a 11 respondidas; papéis definidos; EXC-A0 aguardando a primeira triagem |
| 2026-10-22 | 1.8 | Triagem 1 registrada (§6.5); EXC-A0 concluída; EXC-A1 é a próxima |
| 2026-10-22 | 1.9 | Triagem 2: remoção da regra de firewall aberta (EXC-SEG-009) como ação prioritária |
| 2026-10-22 | 1.10 | Triagem 2 revista: EXC-SEG-009 em risco aceito (IPs dinâmicos); login restrito e troca de senha passam a ser a mitigação principal |
| 2026-10-26 | 1.11 | Avaliação administrativa e financeira (EXC-A10, parte ADM): relatório, modelo de cenários e registros EXC-ADM-003 a 005 |
| 2026-10-26 | 1.12 | Avaliação de marketing (EXC-A10, parte MKT): posicionamento, perfil de cliente, experimentos e registros EXC-MKT-001 a 004 e EXC-FIS-012 |
| 2026-10-26 | 1.13 | EXC-MKT-002 corrigido no código e em validação (teste `TextosInterfaceTests`); novo EXC-FIS-013 (Alta): CNPJ do emitente fixo no envio à Focus |
| 2026-10-26 | 1.14 | Avaliação de vendas (EXC-A10, parte VEN): processo comercial, roteiro de demonstração, funil com fórmulas e registros EXC-VEN-002 a 004 |
