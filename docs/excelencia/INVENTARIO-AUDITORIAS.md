# Inventário de Auditorias e Trabalhos Anteriores

> Programa de Excelência TTSoft · Auditoria EXC-A0 (linha de base)
> Criado em: 2026-10-22 · Base de verificação: commit `ec6d177` (main, 2026-10-21)
> Documentos relacionados: [PLANO-MESTRE](PLANO-MESTRE.md) · [CRITERIOS-DE-EXCELENCIA](CRITERIOS-DE-EXCELENCIA.md) · [REGISTRO-CENTRAL](REGISTRO-CENTRAL.md)

---

## 1. Como este inventário foi montado

- **Os relatórios das auditorias anteriores não estão no repositório** nem no arquivo
  `C:\ERP\ERP_Projeto_Atual.zip` (que contém os mesmos três `.md` versionados). Ver
  EXC-DAD-001.
- O inventário foi **reconstruído a partir das mensagens dos 225 commits**. A mensagem de
  um commit indica a intenção; não prova que o problema foi resolvido nem que continua
  resolvido.
- A coluna **Estado verificado** usa:
  - **Confirmado** — conferido no código atual e/ou por teste executado em 2026-10-22.
  - **Parcial** — parte conferida; o restante depende de ambiente ou evidência externa.
  - **Não verificado** — ainda não conferido nesta fase.
  - **Divergente** — o código atual contradiz o que o trabalho/documento afirma.
- Itens *Não verificado* **não** são considerados defeitos. Serão conferidos na auditoria
  indicada.

---

## 2. Trabalhos anteriores

| ID | Trabalho | Período | Commits principais | Relatório original | O que alega ter feito | Estado verificado | Auditoria que reconfere |
|---|---|---|---|---|---|---|---|
| INV-001 | "fase0 + fase1" (isolamento multi-tenant inicial) | 2026-05-26 | `13006c8`, `e12719f`, `84d9e8f` | Não localizado | Correção multi-tenant, transação na venda, IDOR, rate limit, App Insights; testes dual-tenant F1.1 para 8 controllers; gate no CI | **Parcial** — testes `FASE0` (4) e `F11` (9) executados e aprovados; rodam em InMemory/SQLite (EXC-ENG-001) | EXC-A1 |
| INV-002 | "fase2", "fase3" (funcionalidades) | 2026-05-27 | `1339116`, `66a30b8` | Não localizado | Caixa web, comissões, tintométrico | Não verificado | EXC-A3 / A7 |
| INV-003 | Correções de CI e isolamento em testes | 2026-05-28 a 2026-06-10 | `9b5a75c`, `371a4bd`, `79ba2a3`, `921fa70`, `332c9c9` | Não localizado | AsyncLocal no `HasQueryFilter`, 172/172 testes | **Confirmado** — suíte atual: 1322/1322 aprovados | — |
| INV-004 | Remoção de segredos do repositório | 2026-06-10 a 2026-06-26 | `587a0ba`, `ee29179`, `48d7748`, `6b8ac31` | Não localizado | Segredos retirados de `appsettings.json`, `config_recibo`, credenciais GCP e SEFAZ | **Parcial** — `ERP.Api/appsettings.json` e `appsettings.Staging.json` atuais contêm apenas placeholders; troca (rotação) dos segredos não verificada (EXC-SEG-003) | EXC-A2 |
| INV-005 | Série de segurança 1.6.x–1.9 | 2026-06-10 a 2026-06-22 | `51b5357`, `420fcc3`, `1fbb4c0`, `41ae140`, `71bb64f`, `ac91def`, `3536d4e`, `f5af80f`, `bd224ce`, `2de9015`, `61980ad`, `89278b9`, `21c37fc`, `263c3ab`, `527371f` | Não localizado | Login cross-tenant, HMAC em webhooks, rate limit por CNPJ, isolamento no storage, RBAC por permissão em 21 controllers, MustChangePassword, SAS de 5 min, validação de uploads e remoção de EXIF, AND TenantId em UPDATEs brutos | **Parcial** — os 37 controllers têm `[Authorize]`, `[HasPermission]` ou `[AllowAnonymous]` explícito (presença do atributo, não adequação); demais itens não verificados | EXC-A1 / A2 |
| INV-006 | S9 (etapas 2–8) | 2026-06-24 | `217be11`, `0c8c2e4`, `2faed35`, `afc258f`, `40201e4`, `5c42731`, `f1f2466` | Não localizado | Desconto limitado pela função, TOCTOU em fidelidade e caixa, Swagger bloqueado em produção, XSD da NF-e | **Parcial** — Swagger só em Development/Staging confirmado em `ERP.Api/Program.cs:630`; demais não verificados | EXC-A2 / A3 |
| INV-007 | NF-e Fases 2+3 (assinatura ICP-Brasil, consulta SEFAZ) | 2026-06-26 | `c7b8259`, `56498ea`, `6b8ac31` | Não localizado | Assinatura e consulta; cStat=100 confirmado na SEFAZ-PR | Não verificado (depende de SEFAZ) | EXC-A4 |
| INV-008 | Onboarding self-service | 2026-06-26 a 2026-06-27 | `7e4486f`, `28cf78f`, `f7371c7` | Não localizado | `POST /api/cadastro` e página `/cadastro` no Portal | Não verificado | EXC-A2 / A7 |
| INV-009 | S10 a S13 (com commits "pós-auditoria") | 2026-06-27 a 2026-07-01 | `ccb374b`, `9f882a6`, `477fc5f`, `b8f541d`, `89fd1ea`, `44c7c46`, `2492832`, `56ec36c`, `b50bb94`, `2a73858`, `175feba`, `05338c0`, `b7af754`, `e817533` | **Existiu, não localizado** (commits citam "pós-auditoria" e "pós-revisão") | Correções críticas N1/N2, hardening do cadastro, senha de 12 caracteres, rate limit por IP, oráculo de enumeração, tenant squatting (CNPJ via BrasilAPI), recuperação de senha, PasswordPolicy, expiração de token, circuit breaker | Não verificado | EXC-A2 |
| INV-010 | S15 — controllers para camada de serviço | 2026-07-09 | `b4e54b5` | Não localizado | 5 controllers refatorados; correção cross-tenant no Tintométrico | Não verificado | EXC-A1 / A6 |
| INV-011 | Integração Mercado Livre | 2026-07-21 a 2026-07-24 | `16daf9a` … `eff9b68` | `CHECKLIST_REGRESSAO_MERCADO_LIVRE.md` (roteiro manual, não relatório) | OAuth, ingestão de pedidos, idempotência, sincronização de estoque, SKU mapping | Não verificado; checklist manual existe | EXC-A3 |
| INV-012 | P0 financeiro (atomicidade) | 2026-07-26 a 2026-07-27 | `704dcd0`, `4574704`, `521c829` | Não localizado | Baixa/desconto/cancelamento de conta a receber atômicos; limite de crédito ao vivo; Haver atômico; retry do EF | Não verificado | EXC-A3 |
| INV-013 | Fiscal — etapas 2 e 4 (configuração por tenant, marketplace) e itens 5 a 9 | 2026-07-28 a 2026-08-06 | `3f5ec04`, `1712e0c`, `1676c19`, `d907c3f`, `532bb4f`, `500c6eb`, `e4d4cb0`, `65dc8f8` | Não localizado | Configuração fiscal por tenant com token protegido, cancelamento NF-e, CC-e, devolução, exportação XML, nota avulsa, MD-e | Não verificado | EXC-A4 |
| INV-014 | Nota avulsa — rodadas 1 a 3 | 2026-08-06 | `d6a3f4a` … `e39b791` | Não localizado | 6 bugs reais, CNPJ automático, presets de CFOP, PDF espelho, badge de ambiente, autosave | Não verificado | EXC-A4 / A7 |
| INV-015 | "Fix auditoria" (agosto) | 2026-08-06 a 2026-08-12 | `6ec58b5`, `023d584`, `45fcd20` | **Existiu, não localizado** (commits citam "auditoria") | ICMS interestadual invertido corrigido; 12 entidades sem `HasQueryFilter`; correção P0 do isolamento no filtro | **Parcial** — `AppDbContext` tem 63 `HasQueryFilter` para 57 `DbSet` (contagem, não análise de cobertura) | EXC-A1 / A4 |
| INV-016 | Offline-first — fases 1 a 3 | 2026-08-07 a 2026-08-10 | `da0d27a`, `5400b3d`, `29dd027`, `82400e5`, `c015d20`, `72bd41e` | Não localizado | Outbox, idempotência por GUID, motor de sincronização, indicador no PDV; idempotência financeira "confirmada contra dado real de produção" | Não verificado | EXC-A3 |
| INV-017 | Fase A — testes de venda e idempotência | 2026-08-11 | `1c2e89f` | Não localizado | Testes de venda e idempotência (InMemory) | **Parcial** — incluídos na suíte aprovada; InMemory | EXC-A3 |
| INV-018 | S18/S19, S27 (fiscal e atualizador) | 2026-08-17 a 2026-08-20 | `2448e7c`, `cd1a762` | Não localizado | Precisão do valor unitário, notas referenciadas, atacado, TEF, atualizador, status Processando | **Parcial** — atualizador exige SHA-256 (`ERP.WPF/Services/UpdateService.cs:88`, introduzido em `2448e7c`) | EXC-A2 / A4 |
| INV-019 | Correções do Portal e PDV web | 2026-09-10 a 2026-09-15 | `8f859ab` … `1061a67` | Não localizado | Diversas correções e funcionalidades no Portal/PDV | Não verificado | EXC-A7 |
| INV-020 | Migração WPF→API (Fases B e C, módulos 1 a 4) | 2026-08 a 2026-09-23 | `375f2ca`, `1fd4286`, `95bc71b`, `eadba17` | Não localizado | Venda, Cliente, Produto, Caixa, Contas a Receber/Pagar via API | **Confirmado** — `ERP.WPF/App.xaml.cs:360-436` registra `Http*Service` para esses módulos. Demais serviços seguem com acesso direto ao banco (EXC-SEG-001) | EXC-A1 / A6 |
| INV-021 | Módulo 5 (fiscal) — etapas 1A/1B, 2, Rota A | 2026-09-23 a 2026-09-26 | `2e5a340`, `1a62fee`, `338d8df`, `beae59d`, `35ca27c`, `188c7df` | Não localizado | Emissão e devolução via API, chave/número salvos, item fiscal, reconciliação, contingência movida para a API | **Parcial** — `HttpFiscalService` registrado no WPF; serviços de emissão/cancelamento/contingência locais também seguem registrados (`App.xaml.cs:441-446`) | EXC-A4 |
| INV-022 | Fiscal — Etapa 4 (recuperação de NFC-e, trava de ambiente) | 2026-10-07 a 2026-10-10 | `6251c91` … `f33a098` | Não localizado (há planejamento detalhado nas mensagens) | Recuperação por estado, orquestrador e worker **inertes**, interruptor por tenant, trava de ambiente, migrations `AddRecuperacaoToNfePendentes` e `AddCriadaEmProducaoToNfePendentes` | **Parcial** — testes de `Fiscal/Recovery` aprovados; estado em produção desconhecido (EXC-FIS-001) | EXC-A4 |
| INV-023 | E-commerce e S28 | 2026-10-14 a 2026-10-21 | `a27776a` … `ec6d177` | Não localizado | Endpoints de catálogo, lançamento de vendas do site, NF-e única de vendas, desconto na NFC-e | Não verificado (trabalho mais recente) | EXC-A3 / A4 |

### Branches locais

`feature/theuzs-ecommerce`, `instrumentacao-focus` e `trava-ambiente` não têm commits fora
da `main` (verificado em 2026-10-22). Não existe branch `develop` (local ou remota), da qual
depende o workflow de staging (EXC-OPS-002).

---

## 3. Documentos versionados e seu estado

| Documento | Última alteração | Estado | Divergência | Registro |
|---|---|---|---|---|
| `README.md` | 2026-04-10 | **Divergente** | Descreve 5 projetos (existem 10); diz que a conexão está em `App.xaml.cs` (hoje vem de `SecureConfigService`, DPAPI); "Próximos passos" já entregues | EXC-ENG-002 |
| `Scripts/CHECKLIST_DEPLOY.md` | 2026-03-27 | **Divergente** | Versão 1.1.0; publicação em arquivo único; o `versao.json` versionado não tem `Sha256`, que o atualizador exige desde `2448e7c` | EXC-OPS-001 |
| `Scripts/versao.json` | 2026-03-27 | **Divergente** | Versão 1.1.0, sem `Sha256`; a cópia publicada no Blob não foi vista | EXC-OPS-001, EXC-OPS-004 |
| `CHECKLIST_REGRESSAO_MERCADO_LIVRE.md` | 2026-07-24 | Não verificado | Roteiro manual; o código do módulo mudou depois (ex.: `59117ca`, `c920f44`) | — (EXC-A3) |
| `ERP.Portal/Pages/Roadmap.razor` | — | **Divergente** | Mostra versão 2.4.1; lista como "planejado/futuro" itens já existentes no código; Mobile "em desenvolvimento" | EXC-PRD-001, EXC-OPS-004 |
| `.github/workflows/ci-cd.yml` | — | Confirmado | Build, gates `FASE0`/`F11`, suíte completa; deploy de API e Portal em todo push na `main` | EXC-OPS-002 |
| `.github/workflows/staging.yml` | — | **Divergente** | Dispara em `develop`, branch inexistente | EXC-OPS-002 |

---

## 4. Evidências executadas nesta fase

| Evidência | Data | Commit | Resultado |
|---|---|---|---|
| `dotnet test ERP.Tests` (suíte completa, local, SDK 9.0.305, alvo net8.0-windows) | 2026-10-22 | `ec6d177` | 1322 aprovados, 0 falhas, 0 ignorados, ~1 min 57 s |
| `dotnet test --filter FASE0` | 2026-10-22 | `ec6d177` | 4 aprovados |
| `dotnet test --filter F11` | 2026-10-22 | `ec6d177` | 9 aprovados |
| Avisos de compilação | 2026-10-22 | `ec6d177` | 155 avisos; inclui `NU1903` (EXC-SEG-002) |

---

## 5. Lacunas de evidência

| Lacuna | Quem pode fornecer | Registro |
|---|---|---|
| Relatórios das auditorias anteriores | Matheus | EXC-DAD-001 |
| Histórico de execuções do CI no GitHub | Acesso ao repositório `theeuzs/ttsoft-erp` | — |
| Permissões do login SQL usado pelo WPF | Azure SQL | EXC-SEG-001 |
| Rotação dos segredos removidos | Matheus / Azure / provedores | EXC-SEG-003 |
| Interruptor de recuperação fiscal em produção | Azure SQL / configuração | EXC-FIS-001 |
| Backup e restauração | Azure | EXC-OPS-003 |
| Termos de uso, privacidade, contrato | Matheus | EXC-FIS-002 |
| Lojas em produção e versões instaladas | Matheus / telemetria | EXC-OPS-004 |
| Build piloto `C:\ERP\ConstruTTor_Piloto_x86` | Não inspecionado | — |
| Dados comerciais, financeiros, suporte | Matheus | (áreas ADM, MKT, VEN, SUP) |

---

## Histórico do documento

| Data | Versão | Alteração |
|---|---|---|
| 2026-10-22 | 1.0 | Criação na fase EXC-F0 |
