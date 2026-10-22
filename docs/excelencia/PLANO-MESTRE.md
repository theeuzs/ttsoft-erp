# Plano Mestre — Programa de Excelência TTSoft

> Produto: ConstruTTor ERP
> Criado em: 2026-10-22 · Versão: 1.1 · Fase atual: **EXC-F0 concluída; EXC-A0 em execução**
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
- **Status:** Em execução. Relatórios recebidos e reconciliados em 2026-10-22: 22 pendências
  herdadas encerradas com evidência, 14 convertidas em registros, 9 adiadas para auditorias
  específicas (INVENTARIO §4).
- **Escopo:** relatórios anteriores, commits, documentos versionados; confronto com o código atual.
- **Evidências necessárias:** respostas às confirmações 2 a 9 da seção 7; relatórios
  faltantes, se existirem (INVENTARIO §2.3).
- **Critérios de aprovação:** cada item do INVENTARIO com estado *Confirmado*, *Divergente*
  ou *Sem relatório — não verificável*; pendências herdadas registradas sem duplicação.
- **Encerramento:** inventário revisado pelo responsável; nenhuma pendência dos relatórios
  antigos sem destino.

#### EXC-A1 — Isolamento entre clientes e acesso a dados
- **Status:** Não iniciada.
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
| Patrocinador / decisor | Aprova fichas, prioriza e decide sobre riscos aceitos | Matheus (a confirmar) |
| Auditor técnico | Executa auditorias das áreas 4–7, 9, 10 | Claude Code, sob revisão |
| Responsáveis por área | Fornecem evidências e executam ações | A definir por área |

O histórico de commits tem um único autor. Se não houver outros responsáveis, isso deve
ser considerado no planejamento (capacidade e revisão por pares).

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

---

## 7. Confirmações pendentes (para fechar a EXC-A0)

| # | Pergunta | Registro relacionado |
|---|---|---|
| 1 | ~~Onde estão os relatórios das auditorias anteriores?~~ Respondida em 2026-10-22 (35 relatórios entregues; faltam os do INVENTARIO §2.3) | EXC-DAD-001 |
| 2 | Qual login SQL o WPF usa em cada loja e quais permissões ele tem? | EXC-SEG-001 |
| 3 | Os segredos removidos do repositório em jun/2026 foram trocados? | EXC-SEG-003 |
| 4 | Quantas lojas estão em produção, com qual versão do WPF, e o banco é compartilhado? A Vila Verde já usa o banco na Azure? (em 13/08 usava versão antiga com banco local) | EXC-SEG-001, EXC-OPS-004 |
| 5 | Estado do interruptor de recuperação fiscal por tenant em produção | EXC-FIS-001, EXC-FIS-007 |
| 6 | Política de backup e último teste de restauração do Azure SQL | EXC-OPS-003 |
| 7 | Onde estão termos de uso, política de privacidade e contrato de licença? | EXC-FIS-002 |
| 8 | O app Mobile segue no escopo do produto? | EXC-PRD-001 |
| 9 | Quem são os responsáveis por área? | §5 |
| 10 | Onde está o código da API de licenças? | EXC-OPS-005 |
| 11 | Quais custos mensais (Azure, Focus) e preços praticados existem hoje? | EXC-ADM-001, EXC-VEN-001 |

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

1. Obter as respostas da seção 7, em especial 2, 4 e 5, que podem elevar a gravidade de
   EXC-SEG-001 e EXC-FIS-007 (fecha EXC-A0).
2. Revisar e aprovar a ficha da EXC-A1.
3. Executar a EXC-A1.

---

## Histórico do documento

| Data | Versão | Alteração |
|---|---|---|
| 2026-10-22 | 1.0 | Criação na fase EXC-F0 |
| 2026-10-22 | 1.1 | Relatórios anteriores recebidos; EXC-A0 atualizada; confirmações 10 e 11 |
