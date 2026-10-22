# Registro Central de Descobertas

> Programa de Excelência TTSoft
> Criado em: 2026-10-22 · Última atualização: 2026-10-22 · Base: commit `ec6d177`
> Classificação conforme [CRITERIOS-DE-EXCELENCIA](CRITERIOS-DE-EXCELENCIA.md) · Plano: [PLANO-MESTRE](PLANO-MESTRE.md) · Origem dos itens: [INVENTARIO-AUDITORIAS](INVENTARIO-AUDITORIAS.md)

---

## Como usar

- Cada descoberta tem um ID `EXC-<ÁREA>-NNN`, nunca reutilizado.
- O **índice** resume; o **detalhe** é a fonte. Ao mudar status ou gravidade, atualize os dois.
- Antes de criar um registro, procure no índice por um item igual ou relacionado.
  Duplicatas são encerradas como `Duplicado de EXC-…`.
- Os registros iniciais vêm da fase EXC-F0 e são **diagnóstico, não tarefas**: nenhum deles
  foi aprovado para execução.

### Modelo de registro

```
### EXC-<ÁREA>-NNN — <título curto>

| Campo | Valor |
|---|---|
| Área | |
| Natureza | Fato / Hipótese / Lacuna / Recomendação |
| Descrição | |
| Evidência | arquivo:linha, commit, teste (com resultado e data) |
| Gravidade | Crítica / Alta / Média / Baixa / Informativa / A classificar |
| Impacto | o que acontece, com quem, em que condição |
| Confiança | Alta / Média / Baixa |
| Dependências | |
| Responsável | |
| Ação proposta | |
| Status | ver CRITERIOS §6 |
| Critério de validação | |
| Origem | EXC-A#, INV-NNN |
| Relacionados | |
| Datas | Registrado em / atualizado em |
```

---

## Índice

| ID | Área | Título | Natureza | Gravidade | Confiança | Status | Responsável |
|---|---|---|---|---|---|---|---|
| EXC-SEG-001 | SEG | WPF acessa o banco diretamente com credencial armazenada na máquina da loja | Fato + Hipótese | A classificar | Média | Hipótese | A definir |
| EXC-SEG-002 | SEG | Dependência com vulnerabilidade conhecida de gravidade alta (`System.Security.Cryptography.Xml` 8.0.3) | Fato | A classificar | Alta (aviso) | Confirmado | A definir |
| EXC-SEG-003 | SEG | Troca dos segredos removidos do repositório não confirmada | Lacuna | A classificar | Baixa | Lacuna | Matheus |
| EXC-ENG-001 | ENG | Testes não comprovam comportamento no SQL Server (SQL bruto e `IgnoreQueryFilters`) | Fato | Média | Alta | Confirmado | A definir |
| EXC-ENG-002 | ENG | README desatualizado em relação à arquitetura atual | Fato | Baixa | Alta | Confirmado | A definir |
| EXC-ENG-003 | ENG | 155 avisos de compilação | Fato | Baixa | Alta | Confirmado | A definir |
| EXC-OPS-001 | OPS | Checklist de deploy e `versao.json` versionados desatualizados | Fato | Média | Alta | Confirmado | A definir |
| EXC-OPS-002 | OPS | Deploy automático da `main` em produção; pipeline de staging nunca dispara | Fato | A classificar | Alta | Confirmado | A definir |
| EXC-OPS-003 | OPS | Política de backup e teste de restauração sem evidência | Lacuna | A classificar | — | Lacuna | Matheus |
| EXC-OPS-004 | OPS | Versão do produto sem fonte única; versões instaladas desconhecidas | Fato + Lacuna | Baixa | Alta | Confirmado | A definir |
| EXC-FIS-001 | FIS | Estado da recuperação fiscal (interruptor por tenant) em produção desconhecido | Lacuna | A classificar | — | Lacuna | Matheus |
| EXC-FIS-002 | FIS | Termos de uso, política de privacidade e contrato de licença não localizados | Lacuna | A classificar | — | Lacuna | Matheus |
| EXC-FIS-003 | FIS | Serviços locais de emissão fiscal seguem registrados no WPF junto ao serviço via API | Hipótese | A classificar | Baixa | Hipótese | A definir |
| EXC-PRD-001 | PRD | Roadmap exibido no Portal não corresponde ao produto atual | Fato | Baixa | Alta | Confirmado | A definir |
| EXC-DAD-001 | DAD | Relatórios das auditorias anteriores não estão disponíveis | Lacuna | Média | Alta | Lacuna | Matheus |

**Contagem:** 15 registros — 0 Crítica, 0 Alta, 3 Média, 4 Baixa, 8 A classificar.
Nenhum registro foi classificado como Crítico ou Alto nesta fase porque nenhum risco grave
foi **confirmado**; vários aguardam evidência que pode elevá-los.

---

## Detalhe

### EXC-SEG-001 — WPF acessa o banco diretamente com credencial armazenada na máquina da loja

| Campo | Valor |
|---|---|
| Área | SEG (também ENG, DAD) |
| Natureza | **Fato:** o WPF abre conexão SQL direta. **Hipótese:** a credencial permite acesso a dados de outros tenants. |
| Descrição | O WPF carrega uma string de conexão cifrada com DPAPI (`DataProtectionScope.LocalMachine`) e registra um `AppDbContext` com SQL Server. Venda, Produto, Cliente, Caixa, Contas a Receber/Pagar e Fiscal já usam a API, mas Auth, Usuários, Funções, Devolução, Orçamento, Haver, Motor Financeiro, SPED, Dashboard, emissão/cancelamento/contingência de NF-e e outros seguem acessando o banco diretamente. O isolamento entre tenants nesses caminhos depende do filtro de consulta executado **no cliente**. |
| Evidência | `ERP.WPF/App.xaml.cs:132` (`SecureConfigService.Carregar()`), `ERP.WPF/App.xaml.cs:298-303` (`AddDbContext`/`UseSqlServer`), `ERP.WPF/App.xaml.cs:361-502` (serviços locais), `ERP.WPF/Helpers/SecureConfigService.cs:29,40` (DPAPI LocalMachine). Verificado em 2026-10-22, `ec6d177`. |
| Gravidade | **A classificar.** Crítica se o login SQL distribuído às lojas tiver acesso de leitura/escrita aos dados de todos os tenants (DPAPI LocalMachine protege contra cópia do arquivo para outra máquina, não contra um usuário com acesso à própria máquina). Média se o login for restrito por tenant no próprio banco. |
| Impacto | Potencial exposição ou alteração de dados de outras lojas por quem tiver acesso administrativo a uma máquina de loja. Não há indício de exploração. |
| Confiança | Média (código lido; permissões do login não verificadas) |
| Dependências | Confirmação nº 2 e nº 4 do PLANO-MESTRE §7; acesso ao Azure SQL |
| Responsável | A definir |
| Ação proposta | Na EXC-A1: identificar o login e as permissões; mapear cada serviço ainda local; avaliar o caminho de mitigação (continuação da migração WPF→API ou restrição do login). Não implementar antes da conclusão da EXC-A1. |
| Status | Hipótese |
| Critério de validação | Permissões do login documentadas; classificação definitiva da gravidade com evidência. |
| Origem | EXC-F0; INV-020 |
| Relacionados | EXC-ENG-001, EXC-FIS-003 |
| Datas | Registrado em 2026-10-22 |

### EXC-SEG-002 — Dependência com vulnerabilidade conhecida de gravidade alta

| Campo | Valor |
|---|---|
| Área | SEG |
| Natureza | Fato (o aviso existe); impacto real ainda não avaliado |
| Descrição | O restore reporta `NU1903` para `System.Security.Cryptography.Xml` 8.0.3 no projeto `ERP.Application`. O pacote é provavelmente usado na assinatura/validação de XML fiscal. |
| Evidência | Saída do build de 2026-10-22, `ec6d177`: `ERP.Application.csproj : warning NU1903: Package 'System.Security.Cryptography.Xml' 8.0.3 has a known high severity vulnerability`. |
| Gravidade | **A classificar** — depende de qual advisory se aplica e se o código usa a funcionalidade afetada. |
| Impacto | A determinar na EXC-A2. |
| Confiança | Alta para a existência do aviso; Baixa para o impacto |
| Dependências | — |
| Responsável | A definir |
| Ação proposta | Na EXC-A2: identificar o advisory, onde o pacote é usado (direto ou transitivo) e a versão corrigida. Verificar demais projetos com `dotnet list package --vulnerable --include-transitive`. |
| Status | Confirmado |
| Critério de validação | `dotnet list package --vulnerable --include-transitive` sem vulnerabilidades altas/críticas, ou risco aceito com justificativa. |
| Origem | EXC-F0 |
| Relacionados | INV-007 |
| Datas | Registrado em 2026-10-22 |

### EXC-SEG-003 — Troca dos segredos removidos do repositório não confirmada

| Campo | Valor |
|---|---|
| Área | SEG |
| Natureza | Lacuna |
| Descrição | Commits de junho/2026 removeram do versionamento segredos (senha do banco, chave JWT, credenciais GCP, `config_recibo`, credenciais SEFAZ). O histórico do Git mantém o conteúdo dos commits anteriores, salvo reescrita do histórico (não há indício de reescrita). Não se sabe se esses segredos foram trocados. Os valores antigos **não** foram abertos nesta fase. |
| Evidência | Commits `587a0ba`, `ee29179`, `48d7748`, `6b8ac31`. `ERP.Api/appsettings.json` e `appsettings.Staging.json` atuais só contêm placeholders (verificado em 2026-10-22). |
| Gravidade | **A classificar.** Alta se algum segredo antigo continuar válido (especialmente se o repositório for ou tiver sido acessível a terceiros); Informativa se todos foram trocados. |
| Impacto | Acesso indevido a banco, emissão de tokens ou serviços externos com credencial antiga. |
| Confiança | Baixa |
| Dependências | Confirmação nº 3 do PLANO-MESTRE §7 |
| Responsável | Matheus |
| Ação proposta | Listar os segredos que já estiveram no histórico e confirmar a troca de cada um. |
| Status | Lacuna |
| Critério de validação | Lista de segredos com data de troca registrada, ou confirmação de que nunca foram válidos. |
| Origem | EXC-F0; INV-004 |
| Relacionados | EXC-SEG-001 |
| Datas | Registrado em 2026-10-22 |

### EXC-ENG-001 — Testes não comprovam comportamento no SQL Server

| Campo | Valor |
|---|---|
| Área | ENG (também SEG) |
| Natureza | Fato (limite da cobertura) |
| Descrição | A suíte (1322 testes aprovados) usa EF Core InMemory e SQLite. Há 40 chamadas de SQL bruto (`ExecuteSqlRaw`, `FromSqlRaw` etc.) e 51 usos de `IgnoreQueryFilters` fora dos testes. O comportamento desses pontos — inclusive o isolamento por tenant — no SQL Server não é coberto pela suíte. Isso **não** indica defeito; indica ausência de prova. |
| Evidência | `ERP.Tests.csproj` (pacotes `EntityFrameworkCore.InMemory` e `Sqlite`); contagem por busca em 2026-10-22, `ec6d177`; execução da suíte em 2026-10-22. |
| Gravidade | Média (lacuna de verificação em controles de isolamento e integridade) |
| Impacto | Regressões em SQL bruto ou filtros podem passar no CI e só aparecer em produção. |
| Confiança | Alta |
| Dependências | — |
| Responsável | A definir |
| Ação proposta | Na EXC-A1: revisar cada uso de `IgnoreQueryFilters` e SQL bruto; avaliar uma camada de testes contra SQL Server (ex.: container) para os pontos críticos. |
| Status | Confirmado |
| Critério de validação | Todos os usos revisados e classificados; pontos críticos cobertos por teste contra SQL Server ou risco aceito. |
| Origem | EXC-F0; INV-001, INV-015 |
| Relacionados | EXC-SEG-001 |
| Datas | Registrado em 2026-10-22 |

### EXC-ENG-002 — README desatualizado

| Campo | Valor |
|---|---|
| Área | ENG |
| Natureza | Fato |
| Descrição | O `README.md` descreve 5 projetos (existem 10), instrui editar a conexão em `App.xaml.cs` (hoje vem de `SecureConfigService`) e lista como "Próximos passos" funcionalidades já entregues. |
| Evidência | `README.md` (alterado em 2026-04-10); `ERP.sln`; `ERP.WPF/App.xaml.cs:132` |
| Gravidade | Baixa |
| Impacto | Dificulta a entrada de novas pessoas e induz configuração errada. |
| Confiança | Alta |
| Dependências | EXC-A6 (para documentar a arquitetura real) |
| Responsável | A definir |
| Ação proposta | Reescrever o README após a EXC-A6, apontando para a documentação do programa. |
| Status | Confirmado |
| Critério de validação | README descreve os 10 projetos, a configuração atual e o processo de build/teste. |
| Origem | EXC-F0 |
| Relacionados | — |
| Datas | Registrado em 2026-10-22 |

### EXC-ENG-003 — 155 avisos de compilação

| Campo | Valor |
|---|---|
| Área | ENG |
| Natureza | Fato |
| Descrição | O build da solução de testes gera 155 avisos, majoritariamente de anulabilidade (CS8618, CS8604, CS8601, CS8603, CS8602), além de `NU1701` (compatibilidade de pacote), `CS0618` (API obsoleta) e `NU1903` (tratado em EXC-SEG-002). |
| Evidência | Log do build de 2026-10-22, `ec6d177` |
| Gravidade | Baixa |
| Impacto | Avisos relevantes ficam escondidos no volume. |
| Confiança | Alta |
| Dependências | — |
| Responsável | A definir |
| Ação proposta | Na EXC-A6: separar os avisos com risco real (obsoletos, compatibilidade) dos cosméticos. |
| Status | Confirmado |
| Critério de validação | Avisos restantes conhecidos e justificados (CRITERIOS ENG-2). |
| Origem | EXC-F0 |
| Relacionados | EXC-SEG-002 |
| Datas | Registrado em 2026-10-22 |

### EXC-OPS-001 — Checklist de deploy e `versao.json` versionados desatualizados

| Campo | Valor |
|---|---|
| Área | OPS |
| Natureza | Fato |
| Descrição | `Scripts/CHECKLIST_DEPLOY.md` descreve a v1.1.0 (março/2026), com publicação em arquivo único e Blob anônimo. O `Scripts/versao.json` versionado não tem o campo `Sha256`, que o atualizador exige desde `2448e7c`; uma atualização sem hash é recusada. **Inferência:** seguir o checklist e publicar o `versao.json` versionado como está resultaria em atualização recusada. A cópia publicada no Blob não foi vista. |
| Evidência | `Scripts/CHECKLIST_DEPLOY.md`; `Scripts/versao.json`; `ERP.WPF/Services/UpdateService.cs:88` |
| Gravidade | Média |
| Impacto | Publicação de versão com procedimento errado; lojas sem atualização. |
| Confiança | Alta (documentos e código); Média (efeito, por ser inferido) |
| Dependências | EXC-A5 |
| Responsável | A definir |
| Ação proposta | Documentar o procedimento real de publicação na EXC-A5 e substituir o checklist. |
| Status | Confirmado |
| Critério de validação | Checklist reproduz uma publicação real com sucesso (CRITERIOS OPS-4). |
| Origem | EXC-F0; INV-018 |
| Relacionados | EXC-OPS-004 |
| Datas | Registrado em 2026-10-22 |

### EXC-OPS-002 — Deploy automático da `main` em produção; pipeline de staging nunca dispara

| Campo | Valor |
|---|---|
| Área | OPS |
| Natureza | Fato |
| Descrição | `ci-cd.yml` publica API (App Service `erp-ttsoft-api`) e Portal (Static Web Apps) em todo push na `main`, após os testes, sem etapa de aprovação. `staging.yml` dispara apenas em `develop`, branch que não existe local nem remotamente. Logo, não há passagem automática por staging antes de produção. |
| Evidência | `.github/workflows/ci-cd.yml` (jobs `deploy-portal`, `deploy-api`, condição `github.ref == 'refs/heads/main'`); `.github/workflows/staging.yml` (`branches: [ develop ]`); `git branch -a` em 2026-10-22. |
| Gravidade | **A classificar** — depende de proteção de branch e ambientes configurados no GitHub (não visíveis no repositório). |
| Impacto | Mudanças (inclusive fiscais) chegam à produção no mesmo push, sem validação em staging. |
| Confiança | Alta para a configuração do repositório |
| Dependências | Acesso às configurações do GitHub (proteção de branch, environments) |
| Responsável | A definir |
| Ação proposta | Na EXC-A5: confirmar as proteções existentes e definir o fluxo de promoção para produção. |
| Status | Confirmado |
| Critério de validação | CRITERIOS OPS-1 atendido ou risco aceito. |
| Origem | EXC-F0 |
| Relacionados | PLANO-MESTRE §6.3 |
| Datas | Registrado em 2026-10-22 |

### EXC-OPS-003 — Política de backup e teste de restauração sem evidência

| Campo | Valor |
|---|---|
| Área | OPS (também DAD) |
| Natureza | Lacuna |
| Descrição | Não há documentação nem evidência acessível sobre retenção de backup do Azure SQL ou teste de restauração. Existe um `BackupService` no WPF (timeout ajustado em `05adfbd`), cuja finalidade e alcance não foram examinados. |
| Evidência | Ausência no repositório; Azure não acessível |
| Gravidade | **A classificar** — Alta se não houver restauração testada com dados de clientes em produção. |
| Impacto | Perda de dados sem recuperação. |
| Confiança | — |
| Dependências | Confirmação nº 6 do PLANO-MESTRE §7 |
| Responsável | Matheus |
| Ação proposta | Na EXC-A5: levantar a configuração e executar um teste de restauração documentado. |
| Status | Lacuna |
| Critério de validação | CRITERIOS OPS-2. |
| Origem | EXC-F0 |
| Relacionados | — |
| Datas | Registrado em 2026-10-22 |

### EXC-OPS-004 — Versão do produto sem fonte única; versões instaladas desconhecidas

| Campo | Valor |
|---|---|
| Área | OPS (também DAD) |
| Natureza | Fato (divergência) + Lacuna (versões em campo) |
| Descrição | `ERP.WPF.csproj` e `Scripts/versao.json` indicam 1.1.0; o Portal exibe "2.4.1" fixo no código. Não se sabe quais versões estão instaladas nas lojas. |
| Evidência | `ERP.WPF/ERP.WPF.csproj:3-5`; `Scripts/versao.json`; `ERP.Portal/Pages/Roadmap.razor:17` |
| Gravidade | Baixa |
| Impacto | Dificulta suporte, diagnóstico e comunicação de mudanças. |
| Confiança | Alta |
| Dependências | Confirmação nº 4 do PLANO-MESTRE §7 |
| Responsável | A definir |
| Ação proposta | Definir a fonte de versão por componente na EXC-A8 (processo de release). |
| Status | Confirmado |
| Critério de validação | CRITERIOS OPS-5 e DAD-4. |
| Origem | EXC-F0 |
| Relacionados | EXC-OPS-001, EXC-PRD-001 |
| Datas | Registrado em 2026-10-22 |

### EXC-FIS-001 — Estado da recuperação fiscal em produção desconhecido

| Campo | Valor |
|---|---|
| Área | FIS |
| Natureza | Lacuna |
| Descrição | A Etapa 4 do fiscal introduziu recuperação de NFC-e pendentes com orquestrador e worker descritos como "inertes", um interruptor por tenant e o corte do worker antigo. Não se sabe quais tenants estão em qual modo em produção. |
| Evidência | Commits `3a01d52`, `c387dab`, `f33a098`; testes em `ERP.Tests/Fiscal/Recovery` aprovados em 2026-10-22 |
| Gravidade | **A classificar** — notas pendentes sem tratamento seriam risco fiscal Alto; se o caminho antigo está ativo e funcional, Informativa. |
| Impacto | Notas pendentes não recuperadas ou recuperadas por caminho não previsto. |
| Confiança | — |
| Dependências | Confirmação nº 5 do PLANO-MESTRE §7 |
| Responsável | Matheus |
| Ação proposta | Na EXC-A4: levantar o estado por tenant e a fila de pendências. |
| Status | Lacuna |
| Critério de validação | Estado de cada tenant documentado e coerente com o plano da Etapa 4. |
| Origem | EXC-F0; INV-022 |
| Relacionados | EXC-FIS-003 |
| Datas | Registrado em 2026-10-22 |

### EXC-FIS-002 — Termos de uso, política de privacidade e contrato de licença não localizados

| Campo | Valor |
|---|---|
| Área | FIS (jurídico/LGPD) |
| Natureza | Lacuna — **não** significa que não existam; podem estar fora do repositório. |
| Descrição | Não foram encontrados no repositório termos de uso, política de privacidade ou contrato de licença. O Portal possui cadastro público self-service (`/cadastro`). |
| Evidência | Busca em 2026-10-22 por "política de privacidade", "termos de uso", "termo de aceite" em `.razor`, `.xaml`, `.cs`, `.html`: nenhuma página encontrada. |
| Gravidade | **A classificar** |
| Impacto | Possível descumprimento de obrigações da LGPD e ausência de base contratual no cadastro self-service. |
| Confiança | — |
| Dependências | Confirmação nº 7 do PLANO-MESTRE §7 |
| Responsável | Matheus |
| Ação proposta | Localizar os documentos; na EXC-A9, avaliar com assessoria jurídica. |
| Status | Lacuna |
| Critério de validação | CRITERIOS FIS-4. |
| Origem | EXC-F0; INV-008 |
| Relacionados | — |
| Datas | Registrado em 2026-10-22 |

### EXC-FIS-003 — Serviços locais de emissão fiscal seguem registrados no WPF

| Campo | Valor |
|---|---|
| Área | FIS (também ENG) |
| Natureza | Hipótese |
| Descrição | O WPF registra `HttpFiscalService` (emissão via API) e, ao mesmo tempo, `NfceEmissionService`, `NfeEmissionService`, `NfeCancellationService`, `NfeStatusService` e `NfeContingencyService` locais. Estar registrado não significa estar em uso; é preciso verificar quais telas usam qual caminho. Dois caminhos de emissão podem divergir em regras ou numeração. |
| Evidência | `ERP.WPF/App.xaml.cs:400-406` e `ERP.WPF/App.xaml.cs:441-446` |
| Gravidade | **A classificar** |
| Impacto | Se houver uso dos dois caminhos: comportamento fiscal diferente conforme a tela. |
| Confiança | Baixa |
| Dependências | EXC-A1 (mapa dos serviços locais) |
| Responsável | A definir |
| Ação proposta | Na EXC-A4: rastrear os consumidores de cada serviço e confirmar ou descartar. |
| Status | Hipótese |
| Critério de validação | Mapa de uso documentado; hipótese confirmada ou descartada com evidência. |
| Origem | EXC-F0; INV-021 |
| Relacionados | EXC-SEG-001, EXC-FIS-001 |
| Datas | Registrado em 2026-10-22 |

### EXC-PRD-001 — Roadmap exibido no Portal não corresponde ao produto atual

| Campo | Valor |
|---|---|
| Área | PRD |
| Natureza | Fato |
| Descrição | `Roadmap.razor` lista como "planejado" ou "futuro" itens que já existem no código (entrada de NF-e e MD-e, transferências, Pix automático, fidelidade, marketplace, e-commerce, dashboard de TV) e o app Mobile como "em desenvolvimento", embora o projeto Mobile tenha uma página, sem alterações desde maio/2026, e seja removido do build do CI. |
| Evidência | `ERP.Portal/Pages/Roadmap.razor:170-296`; controllers e telas correspondentes; `ERP.Mobile/` (10 arquivos de código); `.github/workflows/ci-cd.yml` (passo que remove o Mobile da solução) |
| Gravidade | Baixa |
| Impacto | Comunicação ao cliente inconsistente com o produto. |
| Confiança | Alta |
| Dependências | Decisão sobre o escopo do Mobile (confirmação nº 8) |
| Responsável | A definir |
| Ação proposta | Na EXC-A7: definir o escopo de cada aplicativo e a fonte do roadmap. |
| Status | Confirmado |
| Critério de validação | CRITERIOS PRD-2 e PRD-4. |
| Origem | EXC-F0 |
| Relacionados | EXC-OPS-004 |
| Datas | Registrado em 2026-10-22 |

### EXC-DAD-001 — Relatórios das auditorias anteriores não estão disponíveis

| Campo | Valor |
|---|---|
| Área | DAD (governança) |
| Natureza | Lacuna |
| Descrição | Commits citam "pós-auditoria", "pós-revisão" e "Fix auditoria" (S12, S13, ago/2026), mas nenhum relatório está no repositório. Sem eles, não é possível saber se restaram pendências abertas nem evitar reabrir itens corrigidos. |
| Evidência | Commits `175feba`, `05338c0`, `e817533`, `6ec58b5`, `023d584`; repositório e `ERP_Projeto_Atual.zip` sem os relatórios |
| Gravidade | Média (afeta a confiabilidade da linha de base inteira) |
| Impacto | Risco de duplicação de trabalho e de pendências antigas esquecidas. |
| Confiança | Alta |
| Dependências | Confirmação nº 1 do PLANO-MESTRE §7 |
| Responsável | Matheus |
| Ação proposta | Localizar os relatórios e anexar o resumo de cada um ao INVENTARIO. |
| Status | Lacuna |
| Critério de validação | Cada relatório localizado está referenciado no INVENTARIO e suas pendências têm destino no registro. |
| Origem | EXC-F0 |
| Relacionados | INV-009, INV-015 |
| Datas | Registrado em 2026-10-22 |
