# EXC-A5 — Infraestrutura, implantação e operações (primeira passada)

> Programa de Excelência TTSoft · Produto: ConstruTTor ERP
> Data: 2026-10-26 · Base: commit `297e3a7` · Auditor: Claude Code, sob revisão de Matheus
> Classificação conforme [CRITERIOS-DE-EXCELENCIA](CRITERIOS-DE-EXCELENCIA.md) · Plano: [PLANO-MESTRE](PLANO-MESTRE.md) · Registro: [REGISTRO-CENTRAL](REGISTRO-CENTRAL.md)

---

## 0. Conclusões

1. **O repositório do produto é público no GitHub desde 2026-04-10** (EXC-SEG-025, Alta). Já
   estão publicados o código, que contém a chave fixa do `TokenProtector` e o *hash* do
   administrador padrão, e os documentos do programa até `0139cd7` (nome do servidor SQL, nome
   do login usado nas lojas, forma de guardar a conexão nos PCs). Os 13 commits locais ainda
   não enviados incluem o relatório da EXC-A2, com a matriz de vulnerabilidades e o arquivo:linha
   de cada uma. **Este relatório foi commitado só localmente. Nenhum push deve ser feito enquanto
   o repositório for público.**
2. **A loja depende de três serviços Azure para abrir o sistema**: o Azure SQL (acesso direto do
   WPF e *seed* na abertura), a API de licenças (que bloqueia o login quando responde com erro
   HTTP, EXC-OPS-009) e a API do ERP (necessária para vender online; sem ela a venda cai no modo
   offline). Uma vez logado, o PDV continua vendendo offline. Abrir o sistema e trocar de turno
   não funcionam sem SQL e sem a API de licenças.
3. **Não há alerta configurado no repositório, e a telemetria pode estar desligada sem que
   ninguém perceba** (EXC-OPS-010). A NFC-e presa desde 16/10 (EXC-FIS-009) só apareceu nas
   consultas da auditoria. O critério OPS-3 não é atendido.
4. **A proteção dos dados é a retenção automática do Azure SQL Basic: no máximo 7 dias, no mesmo
   servidor lógico** (EXC-OPS-013). Não há cópia fora desse servidor, e a restauração nunca foi
   testada (EXC-OPS-003). Se o servidor lógico for excluído, os bancos dos dois produtos se perdem
   sem recuperação, a menos que exista retenção de longo prazo (LTR).
5. **Não há rollback documentado.** As migrations são aplicadas à mão, do PC do desenvolvedor, e
   o pacote da API publicado não fica guardado (EXC-OPS-011). Desfazer um deploy exige `revert` e
   novo push. Desfazer uma migration exige restauração ou script manual.
6. **A capacidade não pode ser estimada sem medição.** O plano Basic tem limites rígidos (2 GB,
   5 DTU, 30 *workers*) e o uso atual não é conhecido (EXC-OPS-012). Se o banco chegar a 2 GB, as
   vendas passam silenciosamente para o modo offline. Seção 9: consultas que dão a resposta.
7. **Custos:** pelo preço de tabela público (2026-10-26), os dois bancos Basic custam
   ~R$ 61,86/mês. Esse valor é quase igual aos ~R$ 64 de Azure inferidos na avaliação
   administrativa. Isso sugere que o custo declarado **não inclui** App Service, armazenamento nem
   telemetria (seção 10).

---

## 1. Escopo e método

**Examinado (leitura de código e configuração):** workflows `.github/workflows/ci-cd.yml` e
`staging.yml`; `ERP.Api/Program.cs` (banco, *retry*, *health checks*, telemetria, SignalR,
limitadores, serviços em segundo plano); `appsettings*.json` da API e do Portal; transações e
estratégia de execução (`UnitOfWork`); abertura e login do WPF (`App.xaml.cs`,
`LoginViewModel.cs`); verificação de licença (`LicenseManager.cs`); modo offline do PDV
(`FinalizarVendaViewModel`, `ConnectivityExceptionClassifier`, `SyncEngineService`, temporizadores
em `MainWindow.xaml.cs`); atualizador (`UpdateService.cs`); `Scripts/`; registros EXC-OPS-001 a 008.

**Consultado (fontes públicas, 2026-10-26):**
- Microsoft Learn: [limites DTU de banco único](https://learn.microsoft.com/azure/azure-sql/database/resource-limits-dtu-single-databases)
  e [backups automáticos](https://learn.microsoft.com/azure/azure-sql/database/automated-backups-overview).
- API pública de preços da Azure (`prices.azure.com/api/retail/prices`, moeda BRL, região
  `brazilsouth`): Azure SQL Basic/Standard, App Service, Log Analytics.
- API pública do GitHub: visibilidade do repositório `theeuzs/ttsoft-erp`. Também foi
  comparado o `origin/main` com a `main` local.

**Não acessado:** Portal do Azure (planos, instâncias, *Always On*, configurações do App Service,
retenção e redundância dos backups, alertas, custos), configurações do GitHub (proteção de
branch, ambientes, histórico do CI), PCs da loja. Tudo o que depende deles está como **Lacuna**,
com a verificação correspondente na seção 12.

**Regra de leitura:** *Fato (código)* = comportamento lido no código, sem execução; *Fato
(documentação)* = limite publicado pela Microsoft para o plano, que ainda precisa ser confirmado
na configuração real; *Lacuna* = depende do portal. Nenhum número de uso ou de capacidade deste
relatório foi medido.

---

## 2. Inventário da infraestrutura

| # | Componente | Função | Evidência | Configuração conhecida | Lacuna |
|---|---|---|---|---|---|
| 1 | Servidor lógico `erp-ttsoft-server` (Azure SQL) | Hospeda os bancos dos dois produtos | EXC-OPS-007; `Scripts/CHECKLIST_DEPLOY.md` | Firewall aberto (EXC-SEG-009, risco aceito); login administrador usado nas lojas (EXC-SEG-001) | *Resource lock*, auditoria (EXC-SEG-008) |
| 2 | Banco `ERPTTSoftStaging` | **Produção** do ConstruTTor (todos os tenants) | EXC-OPS-007 | Plano Basic (captura de 2026-10-22) | Tamanho, uso de DTU, retenção, redundância, LTR, *elastic pool* |
| 3 | Banco `ERPTTSoft` | Produção do OutfiTT | EXC-OPS-007 | Plano Basic | Idem |
| 4 | App Service `erp-ttsoft-api` (Linux) | API do ERP, SignalR e serviços em segundo plano | `ci-cd.yml:131-158` (`linux-x64`) | Deploy por *publish profile* em todo push na `main` | Plano, instâncias, *Always On*, *health check*, slot `staging` |
| 5 | App Service da API de licenças | Autoriza o login de cada PC | `LicenseManager.cs:13` | Código só no PC do responsável (EXC-OPS-005) | Plano, sistema operacional, monitoramento |
| 6 | Azure Static Web Apps | Portal Blazor | `ci-cd.yml:112-128` | Mesmo token para produção e staging | Plano (há camada gratuita) |
| 7 | Storage `ttsoftupdates` + containers de imagens e fotos | Atualizador do WPF, imagens de produto, fotos de entrega | `UpdateService.cs:31`; `appsettings.json` (`AzureStorage`) | Container `releases` com leitura anônima (checklist) | Redundância, uso real do atualizador (EXC-OPS-004) |
| 8 | Application Insights | Telemetria da API | `Program.cs:303-313` | Amostragem desligada; valor padrão é um *placeholder* zerado | Se a *connection string* real chega ao código (EXC-OPS-010) |
| 9 | Azure SignalR (opcional) | *Backplane* do SignalR | `Program.cs:440-444` | Usado só se `AzureSignalRConnectionString` estiver preenchida | Configuração do App Service |
| 10 | GitHub (repositório e Actions) | Código, CI/CD, artefato do WPF | `ci-cd.yml` | **Repositório público** (EXC-SEG-025); artefato do WPF guardado por 30 dias | Proteção de branch, ambientes, histórico |
| 11 | Focus NFe, BrasilAPI, Mercado Livre, Asaas | Serviços externos | `Program.cs:205-286` | Fora do escopo desta auditoria | — |
| 12 | PCs da loja (3) | WPF + SQLite local do modo offline | EXC-OPS-004 | Conexão direta ao SQL; `licenca.json`; `conexao.dat` | Build instalada em cada PC |

Não existe infraestrutura como código no repositório (nenhum Bicep, Terraform ou ARM). Recriar
qualquer recurso depende de lembrar como ele foi criado no portal.

---

## 3. Dependências por fluxo crítico e pontos únicos de falha

### 3.1 O que cada fluxo precisa

| Fluxo (CRITERIOS §7) | Azure SQL | API do ERP | API de licenças | Focus | Internet da loja | Evidência |
|---|---|---|---|---|---|---|
| Abrir o WPF | **Obrigatório** (*seed* antes do login; falha → "Falha Fatal" e o app fecha) | — | — | — | Obrigatória | `App.xaml.cs:170-174`, `:225-228` |
| Login / troca de turno | **Obrigatório** (login lido do banco direto) | Desejável (JWT; sem ele, vendas e histórico avisam e falham) | **Obrigatório** se responder erro HTTP; com *timeout* ou DNS, usa o cache por até 3 dias | — | Obrigatória | `LoginViewModel.cs:110-127`, `:184-192`; `LicenseManager.cs:32`, `:59-80`, `:83-89` |
| Venda no PDV (já logado) | Indireto (via API) | Desejável: sem ela, a venda vai para a fila offline (SQLite) | — | — | Desejável | `FinalizarVendaViewModel.cs:761`; `ConnectivityExceptionClassifier.cs:70`, `:77` |
| Sincronizar vendas offline | Via API | **Obrigatório** (fila reenviada a cada minuto) | — | — | Obrigatória | `MainWindow.xaml.cs:189-221` |
| Caixa, haver, contas, devolução | **Obrigatório** (caminho direto ao banco, EXC-ENG-010) | Parcial | — | — | Obrigatória | EXC-ENG-010 |
| NFC-e / contingência | Obrigatório | **Obrigatório** (serviços em segundo plano rodam dentro da API) | — | **Obrigatório** | — | `Program.cs:272-273` |
| Portal | Via API | **Obrigatório** | — | — | — | `ERP.Portal/wwwroot/appsettings.json` |
| Atualizar o WPF | — | — | — | — | Desejável (falha é ignorada) | `UpdateService.cs:41`, `:67` |

### 3.2 Pontos únicos de falha

Ordenados pelo efeito na loja. "Redundância" = existe outro caminho automático hoje.

| # | Ponto | Efeito se cair | Redundância | Recuperação hoje | Registro |
|---|---|---|---|---|---|
| P1 | **Banco `ERPTTSoftStaging`** (instância única, Basic) | Nenhum PC abre o sistema; PCs abertos vendem offline até a fila ser reenviada; caixa e financeiro param | Alta disponibilidade interna da plataforma (fora do controle da TTSoft) | Esperar a plataforma; restauração nunca testada | EXC-OPS-003, 012, 013 |
| P2 | **API de licenças** | Login bloqueado em todos os PCs de todas as lojas quando responde 4xx/5xx; com *timeout*, 3 dias de carência | Cache local só para falha de rede | Republicar o serviço a partir do PC do responsável | **EXC-OPS-009**, EXC-OPS-005 |
| P3 | **Login SQL administrador compartilhado** | Trocar a senha (por incidente) derruba ao mesmo tempo os 3 PCs, a API, o OutfiTT e o `dotnet ef` | Nenhuma | Reconfigurar cada PC à mão | EXC-SEG-001, 006; desenho da EXC-A1 |
| P4 | **App Service da API do ERP** (presumida uma instância, EXC-OPS-006) | Vendas offline; Portal fora; contingência fiscal parada | Nenhuma | Reinício, ou `revert` + push (EXC-OPS-011) | EXC-OPS-002, 006, 011 |
| P5 | **Servidor lógico** | Exclusão = perda definitiva dos bancos dos dois produtos sem LTR | Nenhuma conhecida | Nenhuma sem LTR | **EXC-OPS-013** |
| P6 | **Conta Azure / conta GitHub do responsável** | Sem acesso a nenhum recurso, deploy ou código | Nenhuma (uma pessoa, EXC-ADM-002) | Recuperação de conta pelos provedores | EXC-ADM-002 |
| P7 | **PC do responsável** | Perde o código da API de licenças, *user secrets* e o caminho das migrations | Nenhuma | — | EXC-OPS-005, 011 |
| P8 | Região Brazil South | Tudo fora | *Geo-restore* só se a redundância do backup for geográfica (padrão; não verificado) | Recriar tudo à mão em outra região (sem IaC) | EXC-OPS-013 |
| P9 | Focus NFe | Notas em contingência | Mecanismos de contingência (EXC-A4) | — | Fora do escopo |

**Dependência compartilhada entre os produtos (correção de EXC-OPS-007):** bancos únicos
(*single databases*) no mesmo servidor lógico **não** dividem DTU. Cada um tem seus 5 DTU, a menos
que estejam num *elastic pool* (verificação V9). Os dois produtos dividem o **servidor lógico**:
firewall, login administrador, nome DNS, exclusão do servidor e região. Portanto, problema de
desempenho de um produto não afeta o outro. Problema de acesso, credencial ou exclusão afeta os dois.

---

## 4. Avaliação por tema

| Tema | Situação | Natureza | Detalhe |
|---|---|---|---|
| **Disponibilidade** | Sem meta definida; nenhuma redundância própria | Fato + Lacuna | O SLA publicado pela plataforma vale por recurso; a disponibilidade da loja é o produto de SQL × API de licenças × API do ERP (seção 3.1). Nenhuma medição de disponibilidade existe (sem teste de disponibilidade no App Insights ou monitor externo). |
| **Limites de recursos** | Limites do plano Basic conhecidos; uso desconhecido | Fato (documentação) + Lacuna | Basic: 5 DTU, **2 GB de tamanho máximo**, 30 *workers*, 30 logins simultâneos, 300 sessões, armazenamento em HDD. A documentação recomenda S3 ou superior para restaurações pesadas. EXC-OPS-012. |
| **Conexões** | API com *retry* correto; WPF direto ao SQL com o mesmo *retry* | Fato (código) | API: `EnableRetryOnFailure(3, 5 s)` (`Program.cs:52-53`). Transações manuais estão dentro da estratégia de execução (`UnitOfWork.cs:86-90`, `SaleService.cs:118`, `EcommercePedidoService.cs:144`, `:295`): **sem achado**. WPF: mesmo *retry* (`App.xaml.cs:300-302`) e MARS ligado; cada PC mantém um *pool* próprio de conexões. Número de sessões por origem: consulta C-A5-3. |
| **Latência** | Não medida | Lacuna | WPF fala com o SQL pela internet; o banco Basic usa HDD. A telemetria, se estiver ativa, já tem a medida: consulta C-A5-5 (KQL). |
| **Backups** | Automáticos da plataforma; retenção máxima de 7 dias no Basic; sem cópia fora | Fato (documentação) + Lacuna | Completo semanal, diferencial a cada 12–24 h, log a cada ~10 min (documentação). O backup local do WPF não funciona com Azure SQL (EXC-OPS-008). EXC-OPS-013. |
| **Restauração** | Nunca testada | Fato | EXC-OPS-003 (planejado). A restauração cria um banco novo no mesmo servidor e não sobrescreve o original. Teste T1. |
| **Monitoramento** | Telemetria e *health checks* no código; efetividade desconhecida | Fato + Hipótese | `/health`, `/health/ready` (SQL), `/health/live`. A versão no `/health` é fixa (`"1.0.0"`, `Program.cs:679`), então ela não informa qual build está no ar. Telemetria: EXC-OPS-010. |
| **Alertas** | Nenhum definido no repositório | Fato + Lacuna | Os "alertas" do código são linhas de log (`BruteForceAlertMiddleware`, avisos dos serviços fiscais). Ninguém é notificado. EXC-OPS-010. |
| **Deploy** | Automático em todo push na `main`, sem aprovação; staging nunca dispara | Fato | EXC-OPS-002. A API vai para produção no mesmo push em que os testes passam. |
| **Rollback** | Inexistente como procedimento | Fato | EXC-OPS-011. O workflow não tem `workflow_dispatch` e só guarda o artefato do WPF (30 dias, `ci-cd.yml:109`). O da API não é guardado. |
| **Contingência** | Modo offline do PDV implementado; abertura e login sem contingência | Fato (código) | Seção 3.1. A venda offline guarda o mesmo `Id` e é reenviada com idempotência. |
| **Recuperação de falhas** | Sem plano nem objetivos (RPO/RTO) | Fato | Proposta na seção 7. |
| **Segundo plano** | Contingência fiscal, reconciliação, limpeza e retenção rodam dentro da API | Fato (código) + Lacuna | Se o App Service descarrega a aplicação por inatividade (*Always On* desligado ou indisponível no plano), esses ciclos param até a próxima requisição, em geral a abertura da loja. EXC-OPS-014. |
| **Escala horizontal** | Impedida | Fato | EXC-OPS-006, agora também pelos serviços em segundo plano (seção 6). |

---

## 5. Matriz de achados desta passada

| ID | Achado | Natureza | Gravidade | Confiança | Validação |
|---|---|---|---|---|---|
| EXC-SEG-025 | Repositório público no GitHub desde 2026-04-10, com o código (chave fixa do `TokenProtector`, *hash* do administrador padrão, URLs de produção) e os documentos do programa até `0139cd7` | Fato | **Alta** | Alta (API do GitHub) | Repositório privado; varredura do histórico; push dos documentos só depois |
| EXC-OPS-009 | WPF bloqueia o login quando a API de licenças responde com erro HTTP: o cache offline só é usado em exceção de rede | Fato (código) | **Alta** | Alta (código) | Teste T4 |
| EXC-OPS-010 | Nenhum alerta configurado no repositório; telemetria possivelmente desligada pelo *placeholder* do `appsettings.json` | Fato + Hipótese | Média | Média | Verificações V5, V6; teste T7 |
| EXC-OPS-011 | Sem procedimento de rollback; migrations aplicadas à mão, do PC do desenvolvedor; pacote da API não guardado; artefato do WPF expira em 30 dias | Fato | Média | Alta | Procedimento escrito e ensaiado (T6) |
| EXC-OPS-013 | Proteção dos dados limitada a ≤ 7 dias no mesmo servidor lógico; sem LTR conhecida, sem cópia fora, sem trava contra exclusão | Fato (documentação) + Lacuna | Média | Média | V7, V8; testes T2, T3 |
| EXC-OPS-012 | Limites do Azure SQL Basic (2 GB, 5 DTU, 30 *workers*) sem medição; com o banco cheio, as vendas vão silenciosamente para a fila offline | Fato (documentação + código) + Lacuna | **A classificar** | Alta (limites); — (uso) | C-A5-1, C-A5-2 |
| EXC-OPS-014 | Serviços fiscais em segundo plano dependem de a API estar carregada; *Always On* não verificado | Fato (código) + Lacuna | **A classificar** | Alta (código) | V2 |

**Atualizados:** EXC-OPS-002 (sem `workflow_dispatch`, pacote da API não guardado),
EXC-OPS-003 (limites de retenção e cuidados do teste), EXC-OPS-006 (serviços em segundo plano
duplicados com mais de uma instância), EXC-OPS-007 (o que os produtos realmente compartilham).

**Verificado e sem achado novo:** estratégia de execução com transações manuais na API; *retry*
de conexão na API e no WPF; idempotência da venda offline (mesmo `Id`); *timeout* do atualizador,
cuja falha não impede a abertura.

---

## 6. Detalhe dos achados

### EXC-SEG-025 — Repositório público
- **Evidência:** `GET api.github.com/repos/theeuzs/ttsoft-erp` em 2026-10-26: `visibility:
  public`, criado em 2026-04-10, 0 *forks*. `origin/main` = `f195b8b`; `docs/excelencia` publicado
  até `0139cd7` (fase F0, reconciliação e confirmações 2, 4 e 5). A `main` local está **13 commits
  à frente**, entre eles a EXC-A2, a EXC-A6, a EXC-A7, as avaliações da EXC-A10 e a triagem 2
  (firewall aberto).
- **O que já está público:** código-fonte inteiro, incluindo a chave do `TokenProtector`
  (EXC-SEG-017), o *hash* bcrypt do administrador criado pelo *seeder* (EXC-SEG-010), as URLs da
  API e da API de licenças, o histórico com os segredos removidos em junho (EXC-SEG-003, trocados
  segundo o responsável) e o REGISTRO-CENTRAL de 22/10 (nome do servidor, login `sa_ttsoft`,
  `conexao.dat` com DPAPI de máquina e entropia fixa).
- **Impacto:** reduz o esforço para explorar EXC-SEG-010 (CNPJ é público, o TenantId é derivado
  dele e o *hash* padrão está no código), EXC-SEG-017 (quem ler o banco decifra os tokens da Focus)
  e EXC-SEG-001/009 (servidor aberto e nome do login conhecidos; a senha é a única barreira). O
  próximo push publicaria a matriz da EXC-A2.
- **Gravidade:** Alta (segredo ativo exposto: a chave do `TokenProtector` protege tokens em uso).
  **Sobe para Crítica** se a consulta C4 da EXC-A6 encontrar algum tenant com o administrador do
  *seeder* e a senha padrão, porque isso permite entrar pela API sem credencial.
- **Ação proposta (decisão do responsável):** (1) tornar o repositório privado nas configurações
  do GitHub; (2) não fazer push até isso; (3) tratar como exposto tudo o que estava no código
  público: a chave do `TokenProtector` (trocar a chave e recifrar os tokens na correção do
  EXC-SEG-017; avaliar trocar os tokens da Focus) e o *hash* padrão; (4) rodar uma varredura de
  segredos no histórico inteiro (por exemplo, `gitleaks`) e registrar o resultado. Tornar o
  repositório privado não desfaz cópias já feitas.

### EXC-OPS-009 — Login bloqueado quando a API de licenças responde com erro
- **Evidência:** `LicenseManager.cs:32` trata só `IsSuccessStatusCode`; qualquer outra resposta
  cai no `else` (`:59-80`), mostra "SISTEMA NÃO AUTORIZADO" e retorna inválido. O cache offline
  (`VerificarCacheOffline`, carência de 3 dias, `:15`) só é usado no `catch` de exceção (`:83-89`),
  ou seja, falha de rede, DNS ou *timeout* de 30 s. `LoginViewModel.cs:110-127` aborta o login.
- **Quando acontece:** App Service parado ou com erro de inicialização (respostas 5xx), cota
  excedida em plano gratuito (o App Service responde com erro HTTP), falha num deploy do serviço,
  erro 500 do próprio serviço. Em todos esses casos o PC **tem** cache válido e mesmo assim é
  bloqueado.
- **Impacto:** nenhum PC de nenhuma loja consegue entrar até o serviço voltar. Os PCs já logados
  seguem funcionando (a licença só é verificada no login). Se acontecer na abertura da loja ou
  numa troca de turno, a operação para. O código do serviço está só no PC do responsável
  (EXC-OPS-005).
- **Ação proposta:** tratar 5xx e 408/429 como indisponibilidade (usar o cache) e reservar o
  bloqueio para uma resposta explícita de licença inválida; registrar o caso com teste (T4).

### EXC-OPS-010 — Sem alertas; telemetria possivelmente inativa
- **Evidência (fato):** nenhuma regra de alerta, *action group*, teste de disponibilidade ou IaC
  no repositório. Os avisos do código vão só para log: `BruteForceAlertMiddleware.cs:65`
  (`LogWarning`) e os avisos de 30 em 30 minutos dos serviços fiscais
  (`NfeContingencyHostedService.cs:40`, `NfeRecoveryHostedService.cs:25`). Indício de efeito: a
  NFC-e presa desde 16/10 (EXC-FIS-009) só foi vista em 22/10, pelas consultas da auditoria.
- **Hipótese (telemetria):** `appsettings.json:93` traz uma *connection string* com chave zerada,
  e `Program.cs:305` a atribui explicitamente às opções do Application Insights. Se o App Service
  definir só `APPLICATIONINSIGHTS_CONNECTION_STRING` (a variável que a integração do portal cria)
  e não `ApplicationInsights__ConnectionString`, a atribuição explícita deve prevalecer e a
  telemetria vai para uma chave inexistente, sem erro visível. Confirmação: V5 e V6.
- **Ação proposta:** alertas mínimos (seção 7.3), com notificação no celular do responsável;
  confirmar que a telemetria chega; corrigir a versão fixa no `/health`.

### EXC-OPS-011 — Sem rollback; migrations à mão
- **Evidência:** `ci-cd.yml` dispara só em `push` e `pull_request` (sem `workflow_dispatch`); o job
  `deploy-api` publica direto (`:158`) e não guarda o pacote como artefato; o artefato do WPF expira
  em 30 dias (`:109`). Nenhum `Migrate` no código: as migrations rodam por `dotnet ef` no PC do
  desenvolvedor (EXC-A1, desenho, linha 43; `README.md:29-30`).
- **Impacto:** (1) um deploy ruim da API só é desfeito com `revert` + push, passando de novo pela
  suíte completa no CI; (2) a migration e o deploy não andam juntos: existe uma janela em que a
  API nova roda com o esquema antigo, ou o contrário, e os PCs da loja, ligados direto ao banco,
  também são afetados; (3) uma migration destrutiva só é desfeita por restauração, com perda do
  que foi gravado depois, ou por script manual; (4) passados 30 dias sem push, não há mais pacote
  para reinstalar o WPF num PC novo sem recompilar o commit.
- **Ação proposta:** procedimento escrito (seção 7.2) e um ensaio (T6) quando houver ambiente
  separado (EXC-OPS-007); guardar o pacote da API como artefato; `workflow_dispatch` com o SHA a
  publicar; script idempotente das migrations (`dotnet ef migrations script --idempotent`),
  revisado antes de aplicar.

### EXC-OPS-012 — Limites do plano Basic sem medição
- **Evidência (documentação Microsoft, 2026-10-26):** Basic = 5 DTU, 2 GB de tamanho máximo,
  30 *workers*, 30 logins simultâneos, 300 sessões, armazenamento em HDD. **Código:** o
  `AuditLogs` é mantido por 180 dias (`appsettings.json:59`); o classificador do PDV trata qualquer
  `SqlException` e qualquer erro HTTP como falha de conectividade
  (`ConnectivityExceptionClassifier.cs:70`, `:77`).
- **Impacto por cenário:** com o banco no limite de 2 GB, as gravações falham; a API devolve erro,
  e o PDV grava a venda na fila offline sem avisar que o problema não é a internet. A fila fica
  tentando a cada minuto sem sucesso: a loja vende "offline" sem NFC-e e sem financeiro até alguém
  perceber. Gravidade **Crítica** se o banco estiver acima de ~80% de 2 GB ou crescendo para isso
  em poucos meses; **Média** entre 50% e 80%; **Baixa** abaixo. Consultas C-A5-1 e C-A5-2.

### EXC-OPS-013 — Retenção curta e sem cópia fora do servidor
- **Evidência (documentação Microsoft):** restauração a um ponto no tempo (PITR) no Basic é
  configurável **de 1 a 7 dias**; a restauração é sempre para um banco novo no mesmo servidor;
  **excluir o servidor lógico exclui todos os bancos sem recuperação**, salvo backups de LTR, que
  não vêm ligados por padrão; o *geo-restore* só existe se a redundância do backup for geográfica.
- **Impacto:** um dado corrompido percebido depois de 7 dias (por exemplo, a sobrescrita de
  estoque do EXC-ENG-006 ou o estorno duplicado do EXC-ENG-005) não tem mais ponto de restauração.
  Um erro ou comprometimento da conta Azure que exclua o servidor perde os dois produtos.
- **Ação proposta:** ver a retenção, a redundância e a LTR (V7); ligar LTR mensal ou exportar
  BACPAC mensal para fora do servidor (T3); trava `CanNotDelete` no servidor (V8). Avaliar mudar a
  produção para S0, que permite até 35 dias de PITR (seção 10).

### EXC-OPS-014 — Processamento fiscal em segundo plano depende de a API estar carregada
- **Evidência:** contingência (a cada 2 min), reconciliação (1 min), limpeza de cadastros (6 h) e
  retenção de auditoria rodam como `HostedService` dentro do processo da API
  (`Program.cs:212-214`, `:272-273`). Não há outro executor.
- **Impacto por cenário:** com *Always On* ligado, nenhum. Desligado (ou plano sem *Always On*), o
  App Service descarrega a aplicação depois de um período sem requisições, e os ciclos param até a
  próxima chamada, em geral a abertura da loja no dia seguinte. A nota que entrou em contingência
  no fim do dia só é reenviada quando alguém usar o sistema. Gravidade **Média** nesse caso;
  **Informativa** com *Always On*. Verificação V2.

---

## 7. Plano de recuperação

### 7.1 Objetivos propostos (decisão do responsável)

Valores de partida, para aprovar ou ajustar. Não são medições.

| Objetivo | Proposta | Base |
|---|---|---|
| RPO (perda máxima de dados) | ≤ 15 minutos para falha da plataforma; ≤ 7 dias de janela para erro humano descoberto tarde | Backup de log a cada ~10 min (documentação); retenção máxima do Basic |
| RTO do banco (restauração) | A definir **depois** do teste T1 | Duração real da restauração ainda não medida |
| RTO da API | ≤ 1 hora | Tempo de `revert` + CI + deploy, a medir no T6 |
| RTO da API de licenças | ≤ carência do cache (3 dias) **após** corrigir o EXC-OPS-009 | `LicenseManager.cs:15` |

### 7.2 Procedimentos por cenário

| # | Cenário | Como se percebe hoje | Recuperação | Bloqueios |
|---|---|---|---|---|
| R1 | Dado errado gravado (defeito ou erro humano) | Reclamação da loja | PITR para banco **novo** no ponto anterior; comparar; copiar de volta só as linhas afetadas (nunca trocar o banco inteiro sem decidir o que se perde) | Restauração nunca testada (T1); janela de 7 dias |
| R2 | Banco excluído por engano | Loja não abre o sistema | Restaurar banco excluído (mesmo servidor, dentro da retenção); renomear | Nunca ensaiado (T2) |
| R3 | Servidor lógico excluído | Os dois produtos fora | Só com LTR ou BACPAC externo; sem isso, **não há recuperação** | EXC-OPS-013 |
| R4 | Senha do login SQL comprometida | Não percebe (sem auditoria, EXC-SEG-008) | Trocar a senha → reconfigurar 3 PCs, API, OutfiTT e `dotnet ef`, nessa ordem | Login compartilhado (P3); a EXC-A1 resolve |
| R5 | Deploy ruim da API | Loja liga ou Portal falha | `git revert` do commit + push; acompanhar CI; conferir `/health`; se houver migration nova, avaliar se a versão anterior funciona com o esquema novo **antes** do revert | EXC-OPS-011 |
| R6 | Migration ruim | Erros na API e no WPF | Script reverso revisado, ou R1 | EXC-OPS-011 |
| R7 | API de licenças fora | Lojas não entram | Republicar do PC do responsável | EXC-OPS-005, 009 |
| R8 | API do ERP fora (plataforma) | Vendas ficam "offline" | Reiniciar; ver *Resource Health*; vendas da fila sobem sozinhas ao voltar | Sem alerta (EXC-OPS-010) |
| R9 | Internet da loja fora | Loja avisa | PCs abertos vendem offline; **não fechar o WPF** (não reabre sem SQL) | Seção 3.1 |
| R10 | PC da loja perdido | Loja avisa | Reinstalar com o artefato do último push (se tiver menos de 30 dias); `licenca.json`; conexão; liberar a máquina na API de licenças. Vendas offline não sincronizadas daquele PC se perdem | EXC-OPS-011; EXC-VEN-003 |
| R11 | PC do responsável perdido | — | Recuperar a partir do GitHub e do Azure; o código da API de licenças se perde | EXC-OPS-005 |
| R12 | Região Brazil South fora | Tudo fora | *Geo-restore* (se a redundância for geográfica) + recriar App Services à mão em outra região + trocar URLs fixas no WPF (EXC-ENG-013) | Sem IaC; URLs fixas no binário |

### 7.3 Alertas mínimos propostos

Todos com notificação por e-mail **e** pelo aplicativo Azure no celular do responsável:
1. Teste de disponibilidade do Application Insights em `/health/ready` da API (a cada 5 min, 2 locais).
2. Mesmo teste na API de licenças (endpoint a definir; o código não está no repositório).
3. Taxa de respostas 5xx da API acima de um limiar (a definir depois de C-A5-5).
4. Banco: uso de armazenamento > 70% do máximo; DTU > 80% por 15 min.
5. *Service Health* para Azure SQL e App Service em Brazil South.
6. Fiscal: busca no log por "nota em contingência há mais de N horas" (depende do texto do log
   atual; definir na EXC-A4).
7. Orçamento (*budget*) mensal do Azure com aviso em 80% e 100%.

---

## 8. Testes de recuperação propostos

Só os que têm evidência suficiente para serem desenhados. **Cuidado comum a todos:** uma cópia
do banco de produção contém os tokens da Focus (cifrados com a chave pública do EXC-SEG-025) e as
notas pendentes. **Nenhuma API, WPF ou serviço pode ser apontado para uma cópia restaurada**,
porque o serviço de contingência reenviaria notas reais à Focus. Consultas de conferência apenas,
por SSMS, e exclusão da cópia ao final.

| # | Teste | Pré-condição | Execução | Aprovação | Registrar |
|---|---|---|---|---|---|
| T1 | Restauração a um ponto no tempo (EXC-OPS-003, já planejado) | V7 feito | Portal → banco → *Restore* → ponto de 1 h atrás → nome `ERPTTSoftStaging_restore_AAAAMMDD` | Contagens por tenant das tabelas principais iguais às da produção no instante escolhido, salvo o gravado depois; última venda da Vila Verde antes do ponto presente | Data, ponto escolhido, **duração**, tamanho, resultado; excluir a cópia |
| T2 | Restaurar banco excluído | — | Criar banco descartável pequeno, excluí-lo, restaurá-lo pelo portal | Banco volta com os dados | Duração; passo a passo |
| T3 | Cópia fora do servidor (BACPAC) | T1 feito | Exportar a **cópia do T1** (não a produção, cuja exportação não é consistente durante gravações) para BACPAC; importar num SQL Server local | Importação sem erro; contagens iguais às do T1 | Tamanho do arquivo, duração, onde fica guardado |
| T4 | API de licenças indisponível (EXC-OPS-009) | PC de teste fora da loja, com cache válido | (a) apontar o host da API de licenças para um endereço inexistente no arquivo `hosts` → esperado: entra pelo cache; (b) apontar para um servidor local que responde 503 → esperado pelo código: **bloqueia** | (a) entra; (b) depois da correção, entra | Resultado de cada caso |
| T5 | Venda offline e sincronização | **Bloqueado** até existir banco e API de teste separados (EXC-OPS-007): sincronizar cria venda real e pode emitir NFC-e | — | — | — |
| T6 | Rollback da API | **Bloqueado** até existir ambiente de teste separado | `revert` + push num ambiente que não é produção; medir tempo até `/health` responder com a versão anterior | Tempo dentro do RTO aprovado | Duração |
| T7 | Alertas | Alertas da seção 7.3 criados | Parar o slot ou app de teste (ou usar o "testar *action group*") | Notificação chega no celular em ≤ 10 min | Horários |

---

## 9. Capacidade

**Nenhuma estimativa de capacidade é feita nesta passada:** não há uma única medição de uso do
banco ou da API. Dizer "aguenta N lojas" agora seria inventar o número. As consultas abaixo são
**somente leitura** e respondem à pergunta. O critério de decisão está ao lado de cada uma.

| # | Pergunta | Consulta (somente leitura, no banco `ERPTTSoftStaging`) | Decisão |
|---|---|---|---|
| C-A5-1 | Quanto do limite de 2 GB está usado e o que ocupa | `SELECT SUM(CAST(FILEPROPERTY(name,'SpaceUsed') AS bigint))*8/1024 AS usado_mb, CAST(DATABASEPROPERTYEX(DB_NAME(),'MaxSizeInBytes') AS bigint)/1048576 AS max_mb FROM sys.database_files WHERE type_desc='ROWS';` e, por tabela: `SELECT TOP 15 OBJECT_NAME(object_id) AS tabela, SUM(reserved_page_count)*8/1024 AS mb, SUM(CASE WHEN index_id<2 THEN row_count END) AS linhas FROM sys.dm_db_partition_stats GROUP BY object_id ORDER BY mb DESC;` | Classifica EXC-OPS-012; repetir mensalmente dá a taxa de crescimento |
| C-A5-2 | Quanto dos 5 DTU é usado | Última hora (intervalos de 15 s): `SELECT MAX(avg_cpu_percent) cpu_max, AVG(avg_cpu_percent) cpu_med, MAX(avg_data_io_percent) io_max, MAX(avg_log_write_percent) log_max, MAX(max_worker_percent) workers_max, MAX(max_session_percent) sessoes_max FROM sys.dm_db_resource_stats;` Rodar no horário de pico da loja. Histórico de 14 dias: `sys.resource_stats` no banco `master` | Pico > 80% em uso normal → considerar S0 |
| C-A5-3 | Quem abre conexões | `SELECT host_name, program_name, login_name, COUNT(*) sessoes FROM sys.dm_exec_sessions WHERE is_user_process=1 GROUP BY host_name, program_name, login_name ORDER BY sessoes DESC;` | Base para o limite de 300 sessões e para a EXC-A1 |
| C-A5-4 | Ritmo de crescimento | C-A5-1 repetida no dia 1 de cada mês, anotada aqui | Meses até 80% de 2 GB |
| C-A5-5 | Latência e erros da API (se a telemetria estiver ativa) | KQL no App Insights, 7 dias: `requests \| summarize n=count(), p50=percentile(duration,50), p95=percentile(duration,95), falhas=countif(success==false) by name \| order by n desc` e `dependencies \| where type=="SQL" \| summarize p95=percentile(duration,95), n=count() by bin(timestamp,1h)` | Limiar do alerta 3; mostra se o banco Basic pesa na latência |

**Restrições de escala já conhecidas (sem números):** a API não escala para mais de uma instância
(EXC-OPS-006, agravado pelos serviços em segundo plano); cada PC novo soma conexões diretas ao
banco (EXC-SEG-001); o banco é compartilhado entre lojas, então o uso de uma loja grande afeta as
outras.

---

## 10. Projeção de custos

**Fonte:** API pública de preços da Azure, BRL, região `brazilsouth`, consultada em 2026-10-26.
Preço de tabela, **sem impostos, sem descontos e sem conversão de fatura**. Mês = 730 h ou 30,42
dias. Confirmar sempre no *Cost Management* (verificação V10).

### 10.1 Preços unitários

| Recurso | Preço de tabela | Mensal |
|---|---|---|
| Azure SQL Basic (5 DTU) | R$ 1,0167/dia | **R$ 30,93** por banco |
| Azure SQL S0 (10 DTU, 250 GB, PITR até 35 dias) | R$ 3,0560/dia | R$ 92,96 |
| Azure SQL S1 (20 DTU) | R$ 6,1119/dia | R$ 185,92 |
| Azure SQL S2 (50 DTU) | R$ 15,3450/dia | R$ 466,79 |
| Azure SQL S3 (100 DTU) | R$ 30,6932/dia | R$ 933,69 |
| App Service Linux B1 | R$ 0,1044/h | R$ 76,21 |
| App Service Linux S1 (permite slots) | R$ 0,6055/h | R$ 442,02 |
| App Service Linux P0v3 | R$ 0,5794/h | R$ 422,96 |
| App Service Windows B1 | R$ 0,4776/h | R$ 348,65 |
| Ingestão de logs (Log Analytics / App Insights) | 5 GB/mês grátis; depois R$ 24,01/GB | depende do volume |

### 10.2 O que a conta atual provavelmente cobre

- Dois bancos Basic = **R$ 61,86/mês**. A avaliação administrativa inferiu ~R$ 64 de Azure no custo
  declarado (R$ 183,90 − R$ 119,90 da Focus). A coincidência sugere que o valor declarado cobre
  **só os bancos** (hipótese, não fato).
- Portanto, App Service da API, App Service de licenças, armazenamento e telemetria **não estão no
  custo declarado**, ou estão em camadas gratuitas. Se a API estiver em plano gratuito, não há
  *Always On* e há cota diária de CPU (EXC-OPS-014). Se existir o slot `staging`, o plano é
  Standard ou superior (≥ R$ 442/mês só a API), o que contradiz o custo declarado. V1 e V10 resolvem.

### 10.3 Cenários

Valores mensais adicionais ao que existe hoje. **Não incluem aumento por crescimento de uso**
(seção 9: sem medição).

| Cenário | Mudança | Custo adicional/mês | Justificativa (registro) |
|---|---|---|---|
| A — Ambiente de teste separado | +1 banco Basic `ConstruTTor_Teste` | + R$ 30,93 | EXC-OPS-007, desbloqueia T5 e T6 |
| B — Retenção maior na produção | Produção Basic → S0 | + R$ 62,03 | PITR até 35 dias, 250 GB, 60 *workers* (EXC-OPS-012, 013) |
| C — Cópia de longo prazo | LTR mensal ou BACPAC em Blob | Centavos a poucos reais (banco ≤ 2 GB; cobrança por GB armazenado) | EXC-OPS-013 |
| D — API com *Always On* | Se hoje estiver no gratuito: → B1 Linux | + R$ 76,21 | EXC-OPS-014 |
| E — Deploy com slot de staging | API B1 → S1 Linux | + R$ 365,81 | EXC-OPS-002; só se o fluxo de promoção for por slot. Alternativa sem custo de plano: segundo App Service B1 (+R$ 76,21) para teste |
| F — Alertas e testes de disponibilidade | Regras de alerta de métrica e testes padrão | Baixo; confirmar na página de preços do Azure Monitor (não consultada) | EXC-OPS-010 |

**Combinação mínima recomendada para atender OPS-1 a OPS-3 sem slot:** A + B + C + (D, se
aplicável) + F ≈ **R$ 93 a R$ 170/mês** acima do atual, mais o que a V10 revelar.
O prejuízo mensal declarado já era de R$ 64 (EXC-ADM-001), então isso entra na decisão de preço
(EXC-VEN-001).

---

## 11. Situação dos critérios OPS

| Critério | Situação | Registros |
|---|---|---|
| OPS-1 Produção passa por staging funcional, com aprovação | **Não atendido** — deploy direto da `main`; staging nunca dispara; não há banco de teste | EXC-OPS-002, 007 |
| OPS-2 Restauração testada nos últimos 90 dias | **Não atendido** — nunca testada | EXC-OPS-003, 013 |
| OPS-3 Alertas para API, emissão fiscal e sincronização | **Não atendido** — nenhum alerta; telemetria a confirmar | EXC-OPS-010, 014 |
| OPS-4 Deploy e rollback documentados | **Não atendido** — checklist desatualizado; sem rollback | EXC-OPS-001, 011 |
| OPS-5 Fonte única de versão | **Não atendido** — versões divergentes; `/health` com versão fixa | EXC-OPS-004 |

---

## 12. Verificações no portal (somente leitura)

| # | Onde | O que anotar | Registro |
|---|---|---|---|
| V1 | App Service `erp-ttsoft-api` → Plano do App Service; Escalar horizontalmente; Slots de implantação | Camada, nº de instâncias, se o slot `staging` existe | EXC-OPS-002, 006 |
| V2 | `erp-ttsoft-api` → Configuração → Configurações gerais | *Always On*; *health check* (caminho) | EXC-OPS-014 |
| V3 | `erp-ttsoft-api` → Centro de Implantação | Se há uma segunda origem de deploy além do GitHub Actions ("duplo caminho", DOC-31) | EXC-OPS-002 |
| V4 | App Service da API de licenças → Plano; Configuração | Camada, *Always On*, se há monitoramento | EXC-OPS-005, 009 |
| V5 | `erp-ttsoft-api` → Variáveis de ambiente (**só os nomes**, não os valores) | Se existem `ApplicationInsights__ConnectionString`, `APPLICATIONINSIGHTS_CONNECTION_STRING`, `AzureSignalRConnectionString` | EXC-OPS-010 |
| V6 | Application Insights → Pesquisa de transações, últimas 24 h | Se há requisições chegando | EXC-OPS-010 |
| V7 | Banco `ERPTTSoftStaging` → Backups → Políticas de retenção | Retenção PITR (dias), redundância (LRS/ZRS/GRS), LTR | EXC-OPS-003, 013 |
| V8 | Servidor `erp-ttsoft-server` → Bloqueios; Monitor → Alertas | Se há trava `CanNotDelete`; regras de alerta existentes | EXC-OPS-010, 013 |
| V9 | Servidor → Pools elásticos | Se os dois bancos estão num *pool* | EXC-OPS-007 |
| V10 | Gerenciamento de Custos → Análise de custo, 3 meses, agrupado por recurso | Custo por recurso | EXC-ADM-001; seção 10 |
| V11 | GitHub → Settings → Branches / Environments | Proteção da `main`, ambientes com aprovação | EXC-OPS-002 |

---

## 13. Encerramento desta passada e próximos passos

**Para encerrar a EXC-A5:** V1 a V11 e C-A5-1 a C-A5-3 registradas; T1 executado (já planejado);
triagem de EXC-SEG-025, EXC-OPS-009 e dos dois itens "A classificar"; objetivos da seção 7.1
aprovados; procedimento de publicação e rollback escrito (substitui o checklist do EXC-OPS-001).

**Ordem sugerida (decisão do responsável):**
1. EXC-SEG-025: tornar o repositório privado, **antes de qualquer push**.
2. V1, V2, V5, V7 e C-A5-1: cinco minutos de portal e uma consulta, que classificam três registros.
3. T1 (restauração), já planejado na triagem 1.
4. Alertas 1, 4 e 5 da seção 7.3 (sem código).
5. Correção do EXC-OPS-009 (pequena, no WPF), com o teste T4.
