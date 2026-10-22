# Inventário de Auditorias e Trabalhos Anteriores

> Programa de Excelência TTSoft · Auditoria EXC-A0 (linha de base)
> Criado em: 2026-10-22 · Versão: 1.1 · Base de verificação: commit `ec6d177` (main, 2026-10-21)
> Documentos relacionados: [PLANO-MESTRE](PLANO-MESTRE.md) · [CRITERIOS-DE-EXCELENCIA](CRITERIOS-DE-EXCELENCIA.md) · [REGISTRO-CENTRAL](REGISTRO-CENTRAL.md)

---

## 1. Como este inventário foi montado

- **Versão 1.0:** reconstruído a partir das mensagens dos 225 commits, sem acesso aos relatórios.
- **Versão 1.1:** os relatórios foram entregues em 2026-10-22, no arquivo
  `analises_e_auditorias_construTTor.zip` (Área de Trabalho\ConstruTTor, fora do repositório):
  36 arquivos, 35 distintos (`construttor_analise_critica_2026.md` e `-2.md` são idênticos).
  Os relatórios **não** foram copiados para o repositório.
- **Regra de leitura:** um relatório descreve o código **na data dele**. Toda pendência herdada
  foi conferida no código atual (§4) antes de virar registro. Itens já corrigidos são
  encerrados aqui, com evidência, e **não** entram no registro central.
- A coluna **Estado verificado** usa:
  - **Confirmado** — conferido no código atual e/ou por teste executado em 2026-10-22.
  - **Parcial** — parte conferida; o restante depende de ambiente ou evidência externa.
  - **Não verificado** — ainda não conferido.
  - **Divergente** — o código atual contradiz o que o trabalho/documento afirma.

---

## 2. Relatórios localizados

### 2.1 Profundidade de leitura nesta fase

| Profundidade | Documentos |
|---|---|
| Lidos integralmente | DOC-30, DOC-31, DOC-33, DOC-34, DOC-35 |
| Lidas as seções de pendências/verificação | DOC-24 (tabela S12 e achados residuais), DOC-25 (riscos), DOC-27 (verificação da S14), DOC-32 (seções A e E) |
| Apenas cabeçalho e estrutura | Demais |

A série de segurança é encadeada: cada auditoria verifica item a item a anterior
(a 8ª declara não repetir itens fechados nas auditorias 1 a 7; a 13ª confirma os 4 itens da 12ª;
a 15ª lista os 6 itens da 14ª). Por isso, as pendências dos relatórios não lidos
integralmente foram consideradas **cobertas pelos relatórios posteriores**. Isso é uma
premissa, a confirmar na leitura completa prevista na EXC-A2.

### 2.2 Lista

| ID | Arquivo | Data | Tipo | Observação |
|---|---|---|---|---|
| DOC-01 | `avaliacao-ttsoft-erp.docx` | 2026-03-05 | Avaliação técnica | Primeira avaliação (desktop) |
| DOC-02 | `roadmap-diferenciais-ttsoft.docx` | 2026-03-05 | Roadmap/produto | |
| DOC-03 | `avaliacao-atualizada-pos-fase1.docx` | 2026-03-06 | Avaliação técnica | 130 arquivos, 9.228 linhas |
| DOC-04 | `TTSoft_ERP_Dossie_Completo.docx` | 2026-03-21 | Estratégico/comercial | Arquitetura, precificação, pitch |
| DOC-05 | `TTSoft_Guia_MultiTenancy_AutoUpdate.docx` | 2026-03-26 | Guia de implementação | Multi-tenancy e atualização automática |
| DOC-06 | `analise-v4-staff.docx` | 2026-03-28 | Análise técnica | |
| DOC-07 | `analise-v6-definitiva.docx` | 2026-04-01 | Análise técnica + competitiva | vs VHSYS |
| DOC-08 | `analise-multiplataforma-TTSOFT.docx` | 2026-04-10 | Análise técnica | Desktop, API, Portal, Mobile |
| DOC-09 | `analise-definitiva-v11.docx` | 2026-04-14 | Análise competitiva | vs Construshow, CISS, Citel, Santri |
| DOC-10 | `auditoria-construtor.docx` | 2026-04-24 | Auditoria de produção | |
| DOC-11 | `auditoria-seguranca.docx` | 2026-04-25 | Auditoria de segurança | AppSec, observabilidade, UX de balcão |
| DOC-12 | `auditoria-final-construtor.docx` | 2026-04-26 | Auditoria + roadmap | |
| DOC-13 | `analise-critica-construttor.md` | 2026-05-25 | Análise crítica (produto, técnica, GTM) | Contesta uma auditoria anterior que dava nota 9,5 (relatório não localizado) |
| DOC-14 | `adendo-codigo-completo.md` | 2026-05-25 | Adendo técnico | Define a "Fase 0" de mai/2026 |
| DOC-15 | `reavaliacao-pos-fase0-fase1.md` | 2026-05-26 | 3ª avaliação | Verifica Fase 0 + Fase 1 |
| DOC-16 | `AUDITORIA SEGURANÇA 10-06.md` | 2026-06-10 | 4ª avaliação (segurança) | Cabeçalho diz "Maio/2026" |
| DOC-17 | `quinta-auditoria.md` | 2026-06-12 | 5ª auditoria | Verifica Fase 1.6 |
| DOC-18 | `oitava-auditoria.md` | 2026-06-23 | 8ª auditoria (segurança) | 18 achados |
| DOC-19 | `nona-auditoria.md` | 2026-06-23 | 9ª auditoria | 13 de 18 itens da 8ª corrigidos |
| DOC-20 | `contexto-nova-conversa.md` | 2026-06-23 | Contexto de sessão | Inclui OutfiTT ERP (outro produto) |
| DOC-21 | `decima-auditoria.md` | 2026-06-27 | 10ª auditoria | Os 8 itens da 9ª corrigidos |
| DOC-22 | `decima-primeira-auditoria.md` | 2026-06-30 | 11ª auditoria | |
| DOC-23 | `decima-segunda-auditoria.md` | 2026-06-30 | 12ª auditoria | |
| DOC-24 | `decima-terceira-auditoria.md` | 2026-07-01 | 13ª auditoria | 4 itens da 12ª corrigidos; 3 residuais baixos |
| DOC-25 | `construttor_analise_critica_2026.md` (+ cópia `-2`) | 2026-07-06 | Análise crítica (produto, preço, vendas, marketing, riscos) | Feita sem o código-fonte, segundo o próprio documento |
| DOC-26 | `Auditoria_Parte_02_PDV.md` | 2026-07-07 | Auditoria do PDV (parte 2) | Parte 1 não localizada |
| DOC-27 | `decima-quinta-auditoria-e-roadmap.md` | 2026-07-07 | 15ª auditoria + roadmap competitivo | Os 6 itens da 14ª inalterados naquela data |
| DOC-28 | `auditoria-estrategica-completa.md` | 2026-07-10 | Auditoria estratégica | Módulos, papéis, 14 concorrentes |
| DOC-29 | `Roadmap_Tecnico_ConstruTTor.md` | 2026-07-18 | Roadmap de execução | Proposta ao cliente Eldorado; contém ADR-001 |
| DOC-30 | `construttor_auditoria_codigo_2026.md` | 2026-07-26 | Auditoria de código | Bugs de `GetGlobalTenantId()` |
| DOC-31 | `AUDITORIA_TECNICA_CONSTRUTTOR.md` | 2026-07-27 | Due diligence de código | |
| DOC-32 | `ESTRATEGIA_PRODUTO_GTM_CONSTRUTTOR.md` | 2026-07-27 | Estratégia de produto e GTM | Preços marcados como **[estimativa]** |
| DOC-33 | `AUDITORIA_TECNICA_ERP_13-08-2026.md` | 2026-08-13 | Auditoria técnica completa | Relatório técnico mais recente |
| DOC-34 | `CONTEXTO_HANDOFF_ConstruTTor.md` | 2026-08-13 | Contexto de handoff | Estado da Fase B e do roadmap |
| DOC-35 | `etapa3_fechamento_auditoria.md` | 2026-09-26 | Fechamento de diagnóstico fiscal (3A/3B) | Define a Etapa 3C e a Etapa 4 |

### 2.3 Relatórios citados e não localizados

| Documento | Onde é citado |
|---|---|
| 6ª e 7ª auditorias | Numeração da série (DOC-17 → DOC-18) |
| 14ª auditoria (S14) | DOC-27 (os itens são conhecidos pela verificação da 15ª) |
| Auditoria com nota 9,5/10 | DOC-13 |
| Auditoria do PDV, parte 1 | DOC-26 |
| "Análise Crítica v2" e auditoria de 10/08/2026 | DOC-34 |
| ADRs além do ADR-001 | DOC-29 |

Ver EXC-DAD-001.

---

## 3. Trabalhos anteriores (pelo histórico de commits)

| ID | Trabalho | Período | Commits principais | Relatório | O que alega ter feito | Estado verificado | Auditoria que reconfere |
|---|---|---|---|---|---|---|---|
| INV-001 | "fase0 + fase1" (isolamento multi-tenant inicial) | 2026-05-26 | `13006c8`, `e12719f`, `84d9e8f` | DOC-13, DOC-14 (plano), DOC-15 (verificação) | Correção multi-tenant, transação na venda, IDOR, rate limit, App Insights; testes dual-tenant F1.1; gate no CI | **Parcial** — testes `FASE0` (4) e `F11` (9) aprovados; rodam em InMemory/SQLite (EXC-ENG-001) | EXC-A1 |
| INV-002 | "fase2", "fase3" (funcionalidades) | 2026-05-27 | `1339116`, `66a30b8` | — | Caixa web, comissões, tintométrico | Não verificado | EXC-A3 / A7 |
| INV-003 | Correções de CI e isolamento em testes | 2026-05-28 a 2026-06-10 | `9b5a75c`, `371a4bd`, `79ba2a3`, `921fa70`, `332c9c9` | — | AsyncLocal no `HasQueryFilter`, 172/172 testes | **Confirmado** — suíte atual 1322/1322 | — |
| INV-004 | Remoção de segredos do repositório | 2026-06-10 a 2026-06-26 | `587a0ba`, `ee29179`, `48d7748`, `6b8ac31` | DOC-16 a DOC-18 | Segredos retirados de `appsettings.json`, `config_recibo`, GCP e SEFAZ | **Parcial** — `appsettings` atuais só com placeholders; troca dos segredos não verificada (EXC-SEG-003) | EXC-A2 |
| INV-005 | Série de segurança 1.6.x–1.9 | 2026-06-10 a 2026-06-22 | `51b5357` … `527371f` | DOC-16, DOC-17 | Login cross-tenant, HMAC, rate limit por CNPJ, storage, RBAC, MustChangePassword, SAS, uploads/EXIF, AND TenantId em UPDATEs | **Parcial** — os 37 controllers têm atributo de autorização explícito; demais não verificados | EXC-A1 / A2 |
| INV-006 | S9 (etapas 2–8) | 2026-06-24 | `217be11` … `f1f2466` | DOC-18, DOC-19 | Desconto por função, TOCTOU, Swagger fora de produção, XSD da NF-e | **Parcial** — Swagger só em Development/Staging (`ERP.Api/Program.cs:630`) | EXC-A2 / A3 |
| INV-007 | NF-e Fases 2+3 (assinatura ICP-Brasil, consulta SEFAZ) | 2026-06-26 | `c7b8259`, `56498ea`, `6b8ac31` | DOC-21 | Assinatura e consulta; cStat=100 na SEFAZ-PR | Não verificado (depende de SEFAZ) | EXC-A4 |
| INV-008 | Onboarding self-service | 2026-06-26 a 2026-06-27 | `7e4486f`, `28cf78f`, `f7371c7` | DOC-21 a DOC-24 | `POST /api/cadastro` e página `/cadastro` | **Parcial** — limpeza de cadastros expirados existe (`ERP.Api/Services/CleanupCadastrosExpiradosService.cs`) | EXC-A2 / A7 |
| INV-009 | S10 a S13 | 2026-06-27 a 2026-07-01 | `ccb374b` … `e817533` | DOC-21 a DOC-24 | Críticos N1/N2, cadastro, senha, rate limit, enumeração, tenant squatting, PasswordPolicy, token | **Parcial** — DOC-24 confirma os itens da S12; verificação atual em §4 | EXC-A2 |
| INV-010 | S14/S15 e controllers para camada de serviço | 2026-07-07 a 2026-07-09 | `b4e54b5` | DOC-27 | 5 controllers refatorados; correção cross-tenant no Tintométrico; itens da S14 | **Parcial** — itens da S14 em §4 | EXC-A1 / A6 |
| INV-011 | Integração Mercado Livre | 2026-07-21 a 2026-07-24 | `16daf9a` … `eff9b68` | DOC-31, DOC-33; `CHECKLIST_REGRESSAO_MERCADO_LIVRE.md` | OAuth, pedidos, idempotência, estoque, SKU mapping | **Parcial** — tokens OAuth em texto plano (EXC-SEG-004) | EXC-A3 |
| INV-012 | P0 financeiro (atomicidade) | 2026-07-26 a 2026-07-27 | `704dcd0`, `4574704`, `521c829` | DOC-31 (achados), DOC-33 | Baixa de conta a receber atômica, limite de crédito, Haver atômico, retry do EF | **Parcial** — ver §4; créditos de Haver ainda leem-e-gravam (EXC-DAD-002) | EXC-A3 |
| INV-013 | Fiscal — etapas 2 e 4 (config. por tenant, marketplace) e itens 5 a 9 | 2026-07-28 a 2026-08-06 | `3f5ec04` … `65dc8f8` | DOC-33 | Config. fiscal por tenant, cancelamento, CC-e, devolução, XML, nota avulsa, MD-e | **Parcial** — WPF ainda lê a configuração fiscal de arquivo (EXC-FIS-004) | EXC-A4 |
| INV-014 | Nota avulsa — rodadas 1 a 3 | 2026-08-06 | `d6a3f4a` … `e39b791` | — | Correções, CNPJ automático, CFOP, PDF espelho, autosave | Não verificado; existem testes (`ERP.Tests/Infrastructure/NotaFiscalAvulsa*Tests.cs`) | EXC-A4 / A7 |
| INV-015 | "Fix auditoria" (agosto) | 2026-08-06 a 2026-08-12 | `6ec58b5`, `023d584`, `45fcd20` | DOC-31 (ICMS, 12 entidades), DOC-34 (filtro P0) | ICMS interestadual, 12 entidades sem filtro, isolamento no filtro | **Parcial** — 63 `HasQueryFilter` para 57 `DbSet`; teste de alíquota interestadual em `ERP.Tests/Application/Fase2Fase3Tests.cs` | EXC-A1 / A4 |
| INV-016 | Offline-first — fases 1 a 3 | 2026-08-07 a 2026-08-10 | `da0d27a` … `72bd41e` | DOC-31 (antes), DOC-33 (depois) | Outbox, idempotência, motor de sincronização, indicador | **Parcial** — `OfflineSyncService` hoje tem consumidores no WPF (PDV, finalizar venda, sync); DOC-31 o dava como código morto | EXC-A3 |
| INV-017 | Fase A — testes de venda e idempotência | 2026-08-11 | `1c2e89f` | DOC-34 | Testes de venda e idempotência | **Parcial** — na suíte aprovada; InMemory | EXC-A3 |
| INV-018 | S18/S19, S27 (fiscal e atualizador) | 2026-08-17 a 2026-08-20 | `2448e7c`, `cd1a762` | DOC-33 (pendência do atualizador) | Valor unitário, notas referenciadas, atacado, TEF, atualizador | **Confirmado** para o atualizador: SHA-256 exigido em `ERP.WPF/Services/UpdateService.cs:88` e `ERP.Updater/Program.cs:35-38` | EXC-A2 / A4 |
| INV-019 | Correções do Portal e PDV web | 2026-09-10 a 2026-09-15 | `8f859ab` … `1061a67` | — | Diversas | Não verificado | EXC-A7 |
| INV-020 | Migração WPF→API (Fases B e C, módulos 1 a 4) | 2026-08 a 2026-09-23 | `375f2ca`, `1fd4286`, `95bc71b`, `eadba17` | DOC-33, DOC-34 | Venda, Cliente, Produto, Caixa, Contas a Receber/Pagar via API | **Confirmado** — `ERP.WPF/App.xaml.cs:360-436`. Em 13/08 só Venda passava pela API | EXC-A1 / A6 |
| INV-021 | Módulo 5 (fiscal) — etapas 1A/1B, 2, Rota A | 2026-09-23 a 2026-09-26 | `2e5a340` … `188c7df` | DOC-35 | Emissão e devolução via API, contingência movida para a API | **Parcial** — `HttpFiscalService` registrado; serviços locais também (`App.xaml.cs:441-446`) | EXC-A4 |
| INV-022 | Fiscal — Etapa 4 (recuperação de NFC-e, trava de ambiente) | 2026-10-07 a 2026-10-10 | `6251c91` … `f33a098` | DOC-35 (diagnóstico de origem) | Recuperação por estado, inerte, interruptor por tenant, trava de ambiente | **Parcial** — testes aprovados; estado em produção desconhecido (EXC-FIS-001, EXC-FIS-007) | EXC-A4 |
| INV-023 | E-commerce e S28 | 2026-10-14 a 2026-10-21 | `a27776a` … `ec6d177` | — | Catálogo, vendas do site, NF-e única, desconto na NFC-e | Não verificado | EXC-A3 / A4 |

### Branches locais

`feature/theuzs-ecommerce`, `instrumentacao-focus` e `trava-ambiente` não têm commits fora
da `main` (2026-10-22). Não existe branch `develop`, da qual depende o workflow de staging
(EXC-OPS-002).

---

## 4. Reconciliação das pendências herdadas

Pendências abertas nos relatórios mais recentes, conferidas no código em 2026-10-22
(commit `ec6d177`). **Encerradas** não entram no registro central.

### 4.1 Encerradas com evidência

| Pendência herdada | Origem | Evidência atual |
|---|---|---|
| Portal chama `emitir-da-venda` sem rota (404) | DOC-33 §5 | Rota `[HttpPost("{tipo}/emitir-da-venda/{vendaId:guid}")]` em `ERP.Api/Controllers/NotasFiscaisController.cs:110` |
| Atualizador sem verificação de integridade | DOC-31, DOC-33 | `ERP.WPF/Services/UpdateService.cs:88` recusa sem `Sha256`; `ERP.Updater/Program.cs:35-38` recalcula o hash. O hash vem pelo mesmo canal do executável: autenticidade (critério SEG-5) a avaliar na EXC-A2 |
| Endpoints fiscais lendo `FocusNfe:Token` direto da configuração | DOC-33 §5 | `NotasFiscaisController` e `FiscalController` recebem `IFiscalConfigurationProvider`; nenhuma leitura de `FocusNfe:Token` na API |
| Worker de contingência só no WPF, com `catch { }` | DOC-33 §5 | `NfeContingencyHostedService` registrado na API (`ERP.Api/Program.cs:272`), com log de erro (linhas 59-65) |
| `AuditLogs` sem retenção | DOC-31, DOC-33 | `AuditLogRetentionService` registrado (`ERP.Api/Program.cs:214`). Política de retenção a confirmar na EXC-A8 |
| Código morto: `TEFService`, `AuditInterceptor`, `OfflineSyncService` na API, `WpfEstoqueSyncService` no DI | DOC-31, DOC-33 | Sem arquivos/registros de TEF e `AuditInterceptor`; `ERP.Api/Program.cs:222` e `ERP.WPF/App.xaml.cs:384-388` documentam as remoções |
| Offline-first inexistente (`OfflineSyncService` sem consumidores) | DOC-31, DOC-32 | Hoje consumido por `SyncEngineService`, `PdvViewModel`, `FinalizarVendaViewModel`; comportamento real a validar na EXC-A3 |
| Sem testes de `OrderProcessingService`, `ContaReceberService`, `NotaFiscalAvulsaService`, worker de contingência | DOC-31, DOC-33 | Existem `OrderProcessingServiceTests.cs`, `ContaReceberServiceTests.cs`, `NotaFiscalAvulsaServiceTests.cs`, `NfeContingencyHostedService*Tests.cs`; suíte aprovada |
| Limite de crédito só como aviso | DOC-31, DOC-32 | `LimiteCreditoExcedidoException` lançada em `ERP.Application/Services/SaleService.cs:211` |
| Baixa de conta a receber com lost-update | DOC-31 | UPDATE relativo em `ERP.Infrastructure/Services/ContaReceberService.cs:105-116` |
| Débito de Haver read-modify-write | DOC-31, DOC-33 | UPDATE relativo em `ERP.Infrastructure/Repositories/SpecificRepositories.cs:165`. Créditos seguem em aberto (EXC-DAD-002) |
| `EnableRetryOnFailure` ausente | DOC-31 | Presente no WPF (`App.xaml.cs:302`); confirmado na API por DOC-33 |
| ~29 `catch { }` no WPF | DOC-31 | Nenhum `catch` vazio restante fora de cancelamentos (busca por padrão em 2026-10-22) |
| `GetGlobalTenantId()` em `RoleRepository`, `TransferenciaService`, `NfseEmissionService` | DOC-30 | Nenhuma chamada a `GetGlobalTenantId()` em Infrastructure, Application ou Api |
| `BaixarEstoqueAtomicoAsync` sem `AND TenantId` | DOC-30 | `UPDATE ... AND TenantId = {tenantId}` em `ERP.Infrastructure/Repositories/SpecificRepositories.cs:42` e `:49` |
| Relatório de divergências de inventário carrega o catálogo inteiro | DOC-30 | `InventarioController.cs:124` documenta a correção |
| S14: `_falhasConsecutivas++` sem atomicidade | DOC-27 | Tratado e comentado em `ERP.Infrastructure/Services/BrasilApiService.cs:60-66` |
| S14: IP fixo no Mobile | DOC-27 | `ERP.Mobile/MauiProgram.cs:27` (S15 FIX) |
| S14: entrada duplicada no `.gitignore` | DOC-27 | Uma única ocorrência de `ERP.WPF/appsettings.json` |
| S13: `ConfirmacaoToken` sem expiração | DOC-24 | `CleanupCadastrosExpiradosService` remove tokens após 48h |
| JWT sem revogação | DOC-31, DOC-33 | Revogação por `TokenVersion` (`ERP.Api/Program.cs:387-390`); falta de refresh token segue em EXC-SEG-005 |
| 12 entidades sem `HasQueryFilter` | DOC-31 | Corrigido em `023d584`; 63 filtros para 57 `DbSet` (contagem; cobertura semântica na EXC-A1) |

### 4.2 Ainda abertas (viraram registros)

| Pendência herdada | Origem | Registro |
|---|---|---|
| WPF acessa o banco com credencial de todos os tenants | DOC-31, DOC-32, DOC-33, DOC-34 | EXC-SEG-001 |
| Tokens OAuth do Mercado Livre em texto plano | DOC-31, DOC-33 | EXC-SEG-004 |
| JWT sem refresh token | DOC-31, DOC-33 | EXC-SEG-005 |
| `ChangeTracker.Clear()` após todo `SaveChanges` | DOC-31, DOC-33 | EXC-ENG-004 |
| Ausência de concorrência otimista; créditos de Haver read-modify-write | DOC-31, DOC-33 | EXC-DAD-002 |
| WPF com configuração fiscal em arquivo local | DOC-33, DOC-34 | EXC-FIS-004 |
| Reforma Tributária (IBS/CBS) sem tratamento | DOC-25, DOC-27, DOC-30 | EXC-FIS-005 |
| SPED EFD ICMS/IPI sem Bloco E | DOC-31 | EXC-FIS-006 |
| NFC-e reenviada com `DataEmissao` congelada (rejeição 704) | DOC-35 | EXC-FIS-007 |
| API de licenças em outro repositório | DOC-31 | EXC-OPS-005 |
| Rate limit e cache em memória impedem mais de uma instância | DOC-31 | EXC-OPS-006 |
| Banco da Vila Verde na Azure; versões instaladas | DOC-33, DOC-34 | EXC-OPS-004 |
| Preço e planos sem validação; custo por cliente desconhecido | DOC-25, DOC-32 | EXC-VEN-001, EXC-ADM-001 |
| DPA, limitação de responsabilidade, estrutura jurídica | DOC-25 | EXC-FIS-002 |

### 4.3 Não reconferidas nesta fase (vão para a auditoria indicada)

| Pendência herdada | Origem | Auditoria |
|---|---|---|
| Estado estático de tenant (`SetQueryTenantId` público) | DOC-31, DOC-33 | EXC-A1 |
| S13: BrasilAPI fail-open; S14: log com e-mail da RFB em texto | DOC-24, DOC-27 | EXC-A2 |
| `IntegracoesViewModel` e fluxo OAuth completo | DOC-33 | EXC-A2 |
| Motor ICMS-ST não alimenta a emissão | DOC-31 | EXC-A4 |
| Datas financeiras misturando `DateTime.Now` e `UtcNow` | DOC-31 | EXC-A3 |
| Status de conta como texto livre | DOC-31 | EXC-A6 |
| Etapas fiscais 5 (concorrência multi-instância) e 6 (contingência de devoluções) | DOC-35 | EXC-A4 |
| Kit de implantação, importador de dados (CSV existe desde `1980593`) | DOC-33, DOC-34 | EXC-A7 / A10 |
| Lacunas de produto por papel e por concorrente | DOC-27, DOC-28 | EXC-A7 / A10 |

---

## 5. Documentos versionados e seu estado

| Documento | Última alteração | Estado | Divergência | Registro |
|---|---|---|---|---|
| `README.md` | 2026-04-10 | **Divergente** | 5 projetos descritos (existem 10); conexão em `App.xaml.cs` (hoje `SecureConfigService`); "Próximos passos" já entregues | EXC-ENG-002 |
| `Scripts/CHECKLIST_DEPLOY.md` | 2026-03-27 | **Divergente** | v1.1.0; arquivo único; `versao.json` sem `Sha256` | EXC-OPS-001 |
| `Scripts/versao.json` | 2026-03-27 | **Divergente** | 1.1.0, sem `Sha256` | EXC-OPS-001, EXC-OPS-004 |
| `CHECKLIST_REGRESSAO_MERCADO_LIVRE.md` | 2026-07-24 | Não verificado | Código do módulo mudou depois | — (EXC-A3) |
| `ERP.Portal/Pages/Roadmap.razor` | — | **Divergente** | Versão 2.4.1; itens entregues como "planejados"; Mobile "em desenvolvimento" | EXC-PRD-001, EXC-OPS-004 |
| `.github/workflows/ci-cd.yml` | — | Confirmado | Deploy de API e Portal em todo push na `main` | EXC-OPS-002 |
| `.github/workflows/staging.yml` | — | **Divergente** | Dispara em `develop`, branch inexistente | EXC-OPS-002 |

---

## 6. Evidências executadas

| Evidência | Data | Commit | Resultado |
|---|---|---|---|
| `dotnet test ERP.Tests` (suíte completa, local, SDK 9.0.305, alvo net8.0-windows) | 2026-10-22 | `ec6d177` | 1322 aprovados, 0 falhas, 0 ignorados, ~1 min 57 s |
| `dotnet test --filter FASE0` | 2026-10-22 | `ec6d177` | 4 aprovados |
| `dotnet test --filter F11` | 2026-10-22 | `ec6d177` | 9 aprovados |
| Avisos de compilação | 2026-10-22 | `ec6d177` | 155 avisos; inclui `NU1903` (EXC-SEG-002) |
| Conferência das pendências herdadas (§4) | 2026-10-22 | `ec6d177` | 22 encerradas, 14 abertas, 9 adiadas para auditorias |

---

## 7. Lacunas de evidência

| Lacuna | Quem pode fornecer | Registro |
|---|---|---|
| Relatórios citados e não localizados (§2.3) | Matheus | EXC-DAD-001 |
| Histórico de execuções do CI no GitHub | Acesso ao repositório `theeuzs/ttsoft-erp` | — |
| Permissões do login SQL usado pelo WPF | Azure SQL | EXC-SEG-001 |
| Troca dos segredos removidos | Matheus / Azure / provedores | EXC-SEG-003 |
| Interruptor de recuperação fiscal em produção | Azure SQL / configuração | EXC-FIS-001 |
| Backup e restauração | Azure | EXC-OPS-003 |
| Termos de uso, privacidade, contrato, DPA | Matheus | EXC-FIS-002 |
| Lojas em produção e versões instaladas | Matheus / telemetria | EXC-OPS-004 |
| Código da API de licenças | Matheus | EXC-OPS-005 |
| Build piloto `C:\ERP\ConstruTTor_Piloto_x86` | Não inspecionado | — |
| Custos, receita, contratos, chamados | Matheus | EXC-ADM-001, EXC-VEN-001 |

---

## Histórico do documento

| Data | Versão | Alteração |
|---|---|---|
| 2026-10-22 | 1.0 | Criação na fase EXC-F0 |
| 2026-10-22 | 1.1 | Inclusão dos 35 relatórios entregues; reconciliação das pendências herdadas (§4) |
