# Registro Central de Descobertas

> Programa de Excelência TTSoft
> Criado em: 2026-10-22 · Última atualização: 2026-10-26 (v1.5) · Base: commit `ec6d177` (EXC-A6: `8801d9c`)
> Classificação conforme [CRITERIOS-DE-EXCELENCIA](CRITERIOS-DE-EXCELENCIA.md) · Plano: [PLANO-MESTRE](PLANO-MESTRE.md) · Origem dos itens: [INVENTARIO-AUDITORIAS](INVENTARIO-AUDITORIAS.md)

---

## Como usar

- Cada descoberta tem um ID `EXC-<ÁREA>-NNN`, nunca reutilizado.
- O **índice** resume; o **detalhe** é a fonte. Ao mudar status ou gravidade, atualize os dois.
- Antes de criar um registro, procure no índice e no INVENTARIO §4 por um item igual ou
  relacionado. Duplicatas são encerradas como `Duplicado de EXC-…`.
- Pendências de relatórios antigos só entram aqui depois de conferidas no código atual.
  As já corrigidas estão no INVENTARIO §4.1 e **não** são registradas.
- Os registros são **diagnóstico, não tarefas**: nenhum foi aprovado para execução.
- `DOC-NN` refere-se aos relatórios listados no INVENTARIO §2.

### Modelo de registro

```
### EXC-<ÁREA>-NNN — <título curto>

| Campo | Valor |
|---|---|
| Área | |
| Natureza | Fato / Hipótese / Lacuna / Recomendação |
| Descrição | |
| Evidência | arquivo:linha, commit, teste (com resultado e data), DOC-NN |
| Gravidade | Crítica / Alta / Média / Baixa / Informativa / A classificar |
| Impacto | o que acontece, com quem, em que condição |
| Confiança | Alta / Média / Baixa |
| Dependências | |
| Responsável | |
| Ação proposta | |
| Status | ver CRITERIOS §6 |
| Critério de validação | |
| Origem | EXC-A#, INV-NNN, DOC-NN |
| Relacionados | |
| Datas | Registrado em / atualizado em |
```

---

## Índice

| ID | Área | Título | Natureza | Gravidade | Confiança | Status | Responsável |
|---|---|---|---|---|---|---|---|
| EXC-SEG-001 | SEG | WPF acessa o banco com a credencial de administrador do servidor SQL, igual em todas as lojas | Fato | **Crítica** | Alta | **Planejado** | A definir |
| EXC-SEG-002 | SEG | Dependência com vulnerabilidade conhecida de gravidade alta (`System.Security.Cryptography.Xml` 8.0.3) | Fato | A classificar | Alta (aviso) | Confirmado | A definir |
| EXC-SEG-003 | SEG | Troca dos segredos removidos do repositório | Lacuna | Informativa | Média | **Encerrado** (troca declarada) | Matheus |
| EXC-SEG-004 | SEG | Tokens OAuth do Mercado Livre gravados em texto plano | Fato | Média | Alta | Confirmado | A definir |
| EXC-SEG-005 | SEG | JWT sem refresh token | Fato | Baixa | Alta | Confirmado | A definir |
| EXC-SEG-006 | SEG | Senha do login SQL de produção compartilhada fora de um cofre de segredos | Fato | Alta | Alta | **Planejado** | Matheus |
| EXC-SEG-007 | SEG | Conexão do WPF ao SQL não valida o certificado do servidor (`TrustServerCertificate=True`) | Fato | Média | Alta | Confirmado | A definir |
| EXC-SEG-008 | SEG | Servidor SQL sem auditoria, sem Defender e sem administrador do Entra | Fato | Média | Alta | Confirmado | Matheus |
| EXC-SEG-009 | SEG | Firewall do servidor SQL aberto para qualquer IP da internet | Fato | **Crítica** | Alta | **Risco aceito** (revisão: fim da E6) | Matheus |
| EXC-SEG-010 | SEG | *Seeder* do WPF promove usuários a Administrador pelo nome a cada abertura e cria administrador com senha fixa no código | Fato + Hipótese | **Alta** | Alta (código) | Confirmado | A definir |
| EXC-ENG-001 | ENG | Testes não comprovam comportamento no SQL Server (SQL bruto e `IgnoreQueryFilters`) | Fato | Média | Alta | Confirmado | A definir |
| EXC-ENG-002 | ENG | README desatualizado em relação à arquitetura atual | Fato | Baixa | Alta | Confirmado | A definir |
| EXC-ENG-003 | ENG | 155 avisos de compilação | Fato | Baixa | Alta | Confirmado (classificados na EXC-A6) | A definir |
| EXC-ENG-004 | ENG | `ChangeTracker.Clear()` após todo `SaveChanges` | Fato | Média | Alta | Confirmado | A definir |
| EXC-ENG-005 | ENG | Cancelar venda em dinheiro pelo histórico do WPF estorna o caixa duas vezes (servidor e cliente) | Fato + Lacuna | **Alta** | Alta (código) | Confirmado | A definir |
| EXC-ENG-006 | ENG | Edição de produto e de cliente pelo Portal apaga os campos que o formulário não envia (NCM, CSOSN, IE, UF…) e sobrescreve o estoque | Fato + Lacuna | **Alta** | Alta (código) | Confirmado | A definir |
| EXC-ENG-007 | ENG | Produto filho (fracionado): cancelamento e devolução devolvem o estoque ao produto errado; NFC-e sai com a quantidade do pai | Fato + Lacuna | Média | Alta (código) | Confirmado | A definir |
| EXC-ENG-008 | ENG | Cancelamento e devolução gravam em várias etapas sem transação; nova tentativa repete estoque e Haver | Fato | Média | Alta (código) | Confirmado | A definir |
| EXC-ENG-009 | ENG | WPF compartilha um único `AppDbContext` entre telas (serviços *Scoped* resolvidos pela raiz; PDV *singleton*) | Fato + Hipótese | Média | Alta (código) | Confirmado | A definir |
| EXC-ENG-010 | ENG | WPF opera em dois caminhos (API e banco direto) e divide regras de negócio entre cliente e servidor | Fato | Média | Alta | Confirmado | A definir |
| EXC-ENG-011 | ENG | Tenant e usuário resolvidos por estado estático mutável; autor da mudança de preço fica vazio na API | Fato | Baixa | Alta | Confirmado | A definir |
| EXC-ENG-012 | ENG | Regras de negócio espalhadas e duplicadas (três criações de venda, 30 serviços com `AppDbContext` direto, status em texto livre) | Recomendação | Baixa | Alta | Confirmado | A definir |
| EXC-ENG-013 | ENG | URL da API fixa no binário do WPF; arquivo temporário de build, pasta residual e pacote sem uso no repositório | Fato | Baixa | Alta | Confirmado | A definir |
| EXC-OPS-001 | OPS | Checklist de deploy e `versao.json` versionados desatualizados | Fato | Média | Alta | Confirmado | A definir |
| EXC-OPS-002 | OPS | Deploy automático da `main` em produção; pipeline de staging nunca dispara | Fato | A classificar | Alta | Confirmado | A definir |
| EXC-OPS-003 | OPS | Restauração do banco de produção nunca testada | Fato | **Alta** | Alta | **Planejado** | Matheus |
| EXC-OPS-004 | OPS | Versão do produto sem fonte única; versões e bancos em campo desconhecidos | Fato + Lacuna | Baixa | Alta | Confirmado | A definir |
| EXC-OPS-005 | OPS | Licenciamento depende de uma API cujo código está só no PC do responsável | Fato | Média | Alta | Confirmado | Matheus |
| EXC-OPS-006 | OPS | Rate limit e cache em memória impedem escalar a API para mais de uma instância | Fato | Baixa | Média | Confirmado | A definir |
| EXC-OPS-007 | OPS | Produção usa o banco `ERPTTSoftStaging`, que também recebe testes, num servidor compartilhado com o OutfiTT | Fato | Média | Média | Confirmado | Matheus |
| EXC-FIS-001 | FIS | Estado da recuperação fiscal (interruptor por tenant) em produção desconhecido | Lacuna | A classificar | Alta | **Encerrado** (nenhum tenant habilitado; consequência em EXC-FIS-007) | Matheus |
| EXC-FIS-002 | FIS | Não existem termos de uso, política de privacidade nem contrato com os clientes | Fato | Média | Alta | Confirmado | Matheus |
| EXC-FIS-003 | FIS | Serviços locais de emissão fiscal seguem registrados no WPF junto ao serviço via API | Hipótese | A classificar | Baixa | Hipótese | A definir |
| EXC-FIS-004 | FIS | WPF lê a configuração fiscal (token Focus) de arquivo local; API lê do banco | Fato | Média | Alta | Confirmado | A definir |
| EXC-FIS-005 | FIS | Nenhum tratamento da Reforma Tributária (IBS/CBS) no código | Fato + Lacuna | A classificar | Alta | Confirmado | A definir |
| EXC-FIS-006 | FIS | SPED EFD ICMS/IPI gerado sem Bloco E | Fato | Baixa | Alta | Confirmado | A definir |
| EXC-FIS-007 | FIS | Caminho antigo de contingência reenvia NFC-e com `DataEmissao` congelada (todas as lojas estão nele) | Fato | Média | Alta | Confirmado | A definir |
| EXC-FIS-008 | FIS | Tenant de teste emitiu 11 documentos fiscais autorizados em produção, configurado com o CNPJ da Vila Verde | Fato | **Crítica** | Média | **Planejado** | Matheus |
| EXC-FIS-009 | FIS | Erros definitivos da Focus tratados como falha de comunicação: nota fica em contingência para sempre (NFC-e da Vila Verde presa desde 16/10) | Fato | **Alta** | Alta | **Planejado** | A definir |
| EXC-FIS-010 | FIS | Política de emissão fiscal da loja piloto | Lacuna | — | — | **Encerrado** (decisão do cliente, a tratar com o contador) | Matheus |
| EXC-FIS-011 | FIS | Descrição de produto com aspas (polegada) faz a Focus recusar a NFC-e | Fato + Hipótese | Média | Média | Confirmado | A definir |
| EXC-FIS-012 | FIS | SPED gerado pelo Portal usa razão social e CNPJ fixos (`Vila Verde Materiais`, CNPJ fictício) | Fato | Média | Alta | Confirmado | A definir |
| EXC-FIS-013 | FIS | CNPJ do emitente fixo no envio de notas à Focus: toda nota sai com o CNPJ da Vila Verde | Fato + Hipótese | **Alta** | Alta (código) | Confirmado | A definir |
| EXC-PRD-001 | PRD | Roadmap exibido no Portal não corresponde ao produto atual | Fato | Baixa | Alta | Confirmado | A definir |
| EXC-DAD-001 | DAD | Parte dos relatórios anteriores não foi localizada | Lacuna | Baixa | Alta | Lacuna | Matheus |
| EXC-DAD-002 | DAD | Sem concorrência otimista; créditos de Haver por leitura-e-gravação | Fato + Hipótese | Média | Média | Confirmado | A definir |
| EXC-DAD-003 | DAD | Registros com `TenantId` vazio e filial matriz duplicada na Vila Verde | Fato | Baixa | Alta | Confirmado | A definir |
| EXC-ADM-001 | ADM | Receita mensal declarada não cobre os custos de infraestrutura declarados | Fato | Média | Média | Confirmado | Matheus |
| EXC-ADM-002 | ADM | Todo o conhecimento e a operação concentrados em uma pessoa | Fato | Média | Alta | Confirmado | Matheus |
| EXC-ADM-003 | ADM | Margem por cliente depende do volume de notas fiscais, que não está medido | Hipótese | A classificar | Baixa | Hipótese | Matheus |
| EXC-ADM-004 | ADM | Dados administrativos e tributários da TTSoft ausentes (forma jurídica, regime, cobrança, custos fixos) | Lacuna | A classificar | — | Lacuna | Matheus |
| EXC-ADM-005 | ADM | Asaas usa uma única chave da aplicação; uso em produção desconhecido | Hipótese | A classificar | Baixa | Hipótese | Matheus |
| EXC-VEN-001 | VEN | Preços e planos sem validação com clientes | Lacuna | A classificar | — | Lacuna | Matheus |
| EXC-VEN-002 | VEN | Cadastro público "Criar conta gratuita" cria loja no banco de produção sem plano, prazo, termos nem contrato | Fato | Média | Alta (código) | Confirmado | Matheus |
| EXC-VEN-003 | VEN | Implantação de loja nova depende de passos técnicos manuais e não documentados | Fato | Média | Alta | Confirmado | Matheus |
| EXC-VEN-004 | VEN | Não existe processo comercial documentado nem registro de propostas | Lacuna | A classificar | — | Lacuna | Matheus |
| EXC-MKT-001 | MKT | Público-alvo e proposta de valor sem validação com clientes | Lacuna | A classificar | — | Lacuna | Matheus |
| EXC-MKT-002 | MKT | Nome da loja piloto fixo na interface do WPF | Fato | Baixa | Alta | **Em validação** (corrigido no código; falta publicar) | Matheus |
| EXC-MKT-003 | MKT | Relatórios anteriores recomendam afirmações de divulgação que o estado atual não sustenta | Fato | Média | Alta | Confirmado | Matheus |
| EXC-MKT-004 | MKT | Nenhum canal de aquisição ou funil medido | Lacuna | A classificar | — | Lacuna | Matheus |

**Contagem:** 60 registros (57 abertos, um deles em risco aceito; 3 encerrados) — abertos: 3 Crítica, 7 Alta, 23 Média, 13 Baixa, 11 A classificar.
Em 2026-10-26 (EXC-A6): acrescentados EXC-ENG-005 a 013 e EXC-SEG-010 pela [avaliação de arquitetura e engenharia](EXC-A6-ARQUITETURA-ENGENHARIA.md); EXC-ENG-001, 003 e 004 atualizados.
Em 2026-10-26: acrescentados EXC-ADM-003 a 005 pela [avaliação administrativa e financeira](EXC-A10-ADMINISTRATIVO-FINANCEIRO.md), EXC-MKT-001 a 004 e EXC-FIS-012 pela [avaliação de marketing](EXC-A10-MARKETING.md), e EXC-VEN-002 a 004 pela [avaliação de vendas](EXC-A10-VENDAS.md); EXC-FIS-013 encontrado na correção do EXC-MKT-002, que passou a *Em validação*.
Atualizado em 2026-10-22 com os resultados do [roteiro](ROTEIRO-CONFIRMACOES-A0.md)
(passos 1.1, 1.2, 2, 4 e E1 a E9). Prioridade imediata: EXC-FIS-008, EXC-SEG-001 e EXC-FIS-009.

---

## Detalhe — Segurança (SEG)

### EXC-SEG-001 — WPF acessa o banco com a credencial de administrador do servidor SQL, igual em todas as lojas

| Campo | Valor |
|---|---|
| Área | SEG (também ENG, DAD) |
| Natureza | Fato |
| Descrição | O WPF guarda a string de conexão em `conexao.dat`, ao lado do executável, cifrada com DPAPI de máquina e entropia fixa no binário; quem usa a máquina consegue decifrá-la. Segundo o responsável (2026-10-22), toda instalação usa o **mesmo login SQL** (`sa_ttsoft`) no banco `ERPTTSoftStaging`, que contém todos os tenants. Não há segurança por linha no banco: nenhuma política nem uso de `SESSION_CONTEXT` no código, que é o que permitiria ao banco distinguir lojas. Como o WPF lê e grava os próprios dados com esse login, o login necessariamente lê e grava os dados de **todas** as lojas. O isolamento existe só no filtro executado no cliente. Ainda passam pelo banco direto: Auth, Usuários, Funções, Devolução, Orçamento, Haver, Motor Financeiro, SPED, Dashboard, emissão/cancelamento/contingência de NF-e e outros. |
| Evidência | `ERP.WPF/Helpers/SecureConfigService.cs:14-20,29,40`; `ERP.WPF/ViewModels/SetupViewModel.cs:100-101`; `ERP.WPF/App.xaml.cs:132`, `:298-303`, `:361-502`; busca por `SECURITY POLICY`/`SESSION_CONTEXT` sem resultados; declaração do responsável. **Verificado no ambiente em 2026-10-22:** Portal do Azure mostra `sa_ttsoft` como *Administrador do servidor* de `erp-ttsoft-server`; no `ERPTTSoftStaging` o login é `dbo`, membro de `db_owner` e tem `CONTROL`, `ALTER ANY USER`, `BACKUP DATABASE`, `UNMASK` e `CREATE DATABASE` (roteiro 2.1–2.3). Os PCs `DESKTOP-JQB0BIQ`, `SERVIDOR` e `COMPUTADOR03` da Vila Verde gravaram diretamente no banco em 22/10 (roteiro 4.4). DOC-31, DOC-32, DOC-33, DOC-34. |
| Gravidade | **Crítica.** O administrador do servidor lógico tem controle total de todos os bancos do servidor, inclusive o `ERPTTSoft` do OutfiTT, e pode criar logins e bancos. Essa credencial está em cada PC de loja, decifrável por quem usa a máquina. |
| Impacto | Quem obtiver a credencial a partir de um PC de loja (malware, funcionário, técnico) lê, altera ou apaga dados de todas as lojas dos dois produtos, inclusive os tokens de EXC-SEG-004, e pode criar acessos persistentes. Sem auditoria no servidor (EXC-SEG-008), o uso indevido não deixaria rastro. Não há indício de exploração. |
| Confiança | Alta. O acesso ao `ERPTTSoft` decorre do papel de administrador do servidor; a consulta 2.5 foi executada no contexto do `ERPTTSoftStaging` (que só lista o próprio banco e o `master`) e pode ser repetida no `master` apenas para documentar. |
| Dependências | — |
| Responsável | A definir |
| Ação proposta | Mitigação de curto prazo, a desenhar na EXC-A1 antes de qualquer mudança: login próprio do ConstruTTor, restrito ao `ERPTTSoftStaging` e ao mínimo de permissões que o WPF usa (sem `db_owner`), com senha nova (resolve também EXC-SEG-006), reconfigurando os 3 PCs; o administrador do servidor deixa de sair da máquina do responsável. Longo prazo: continuar a migração WPF→API até o WPF não precisar de credencial do banco. |
| Status | **Planejado** (triagem de 2026-10-22): desenhar no início da EXC-A1 um login exclusivo do ConstruTTor, restrito ao `ERPTTSoftStaging` e às permissões que o WPF usa, com senha nova; executar em janela fora do horário da loja, reconfigurando os 3 PCs. Resolve também EXC-SEG-006. |
| Critério de validação | Nenhuma máquina de loja com credencial capaz de ler dados de outra loja; ou risco aceito formalmente com prazo. |
| Origem | EXC-F0; INV-020; DOC-31, DOC-33; confirmação 2 |
| Relacionados | EXC-ENG-001, EXC-FIS-003, EXC-SEG-004, EXC-SEG-006, EXC-SEG-007, EXC-OPS-007 |
| Datas | Registrado em 2026-10-22; atualizado em 2026-10-22 (relatórios anteriores; declaração do login; verificação no Azure e no banco) |

### EXC-SEG-002 — Dependência com vulnerabilidade conhecida de gravidade alta

| Campo | Valor |
|---|---|
| Área | SEG |
| Natureza | Fato (o aviso existe); impacto real ainda não avaliado |
| Descrição | O restore reporta `NU1903` para `System.Security.Cryptography.Xml` 8.0.3 no projeto `ERP.Application`. O pacote é provavelmente usado na assinatura/validação de XML fiscal. Nenhum relatório anterior registra este aviso. |
| Evidência | Build de 2026-10-22, `ec6d177`: `ERP.Application.csproj : warning NU1903: Package 'System.Security.Cryptography.Xml' 8.0.3 has a known high severity vulnerability`. |
| Gravidade | **A classificar** — depende do advisory e do uso da funcionalidade afetada. |
| Impacto | A determinar na EXC-A2. |
| Confiança | Alta para o aviso; Baixa para o impacto |
| Dependências | — |
| Responsável | A definir |
| Ação proposta | Na EXC-A2: identificar o advisory, onde o pacote é usado (direto ou transitivo) e a versão corrigida; rodar `dotnet list package --vulnerable --include-transitive` em todos os projetos. |
| Status | Confirmado |
| Critério de validação | Nenhuma vulnerabilidade alta/crítica, ou risco aceito com justificativa. |
| Origem | EXC-F0 |
| Relacionados | INV-007 |
| Datas | Registrado em 2026-10-22 |

### EXC-SEG-003 — Troca dos segredos removidos do repositório não confirmada

| Campo | Valor |
|---|---|
| Área | SEG |
| Natureza | Lacuna |
| Descrição | Commits de junho/2026 removeram do versionamento segredos (senha do banco, chave JWT, credenciais GCP, `config_recibo`, credenciais SEFAZ). O histórico do Git mantém o conteúdo anterior, salvo reescrita (sem indício de reescrita). Não se sabe se os segredos foram trocados. Os valores antigos **não** foram abertos. DOC-30 e DOC-31 confirmam que o código não tem segredos, mas não tratam da troca. |
| Evidência | Commits `587a0ba`, `ee29179`, `48d7748`, `6b8ac31`; `appsettings` atuais só com placeholders (2026-10-22). |
| Gravidade | Informativa (todos trocados, segundo o responsável). |
| Impacto | Acesso indevido a banco, emissão de tokens ou serviços externos com credencial antiga. |
| Confiança | Média (declaração do responsável, sem lista de datas) |
| Dependências | — |
| Responsável | Matheus |
| Ação proposta | Nenhuma. A senha atual do banco tem registro próprio (EXC-SEG-006). |
| Status | **Encerrado** em 2026-10-22: o responsável declarou que os segredos removidos em junho foram trocados. |
| Critério de validação | Lista de segredos com data de troca, ou confirmação de que nunca foram válidos. |
| Origem | EXC-F0; INV-004 |
| Relacionados | EXC-SEG-001 |
| Datas | Registrado em 2026-10-22 |

### EXC-SEG-004 — Tokens OAuth do Mercado Livre gravados em texto plano

| Campo | Valor |
|---|---|
| Área | SEG |
| Natureza | Fato |
| Descrição | `SalesChannel.AccessToken` e `RefreshToken` são gravados sem proteção. O projeto já tem `TokenProtector`, usado apenas para o token da Focus (`DatabaseFiscalConfigurationProvider`). |
| Evidência | `ERP.Domain/Entities/SalesChannel.cs:24-25`; gravação em `ERP.Infrastructure/Services/MercadoLivreAuthService.cs:117-118` e `ERP.Infrastructure/Repositories/OrderSyncRepository.cs:45-46`; `TokenProtector` só referenciado por `DatabaseFiscalConfigurationProvider.cs` (2026-10-22). Apontado em DOC-31 e DOC-33 (P1). |
| Gravidade | Média. Sobe se EXC-SEG-001 for confirmado (qualquer máquina de loja leria os tokens de todos os tenants). |
| Impacto | Quem obtém leitura do banco (dump, backup, credencial do WPF) pode operar a conta do Mercado Livre de qualquer loja conectada. |
| Confiança | Alta |
| Dependências | — |
| Responsável | A definir |
| Ação proposta | Na EXC-A2: avaliar a proteção dos tokens com o mecanismo existente e a migração dos registros atuais. |
| Status | Confirmado |
| Critério de validação | Nenhum token OAuth legível em texto no banco; teste que garanta a proteção na gravação. |
| Origem | INV-011; DOC-31, DOC-33 |
| Relacionados | EXC-SEG-001 |
| Datas | Registrado em 2026-10-22 |

### EXC-SEG-005 — JWT sem refresh token

| Campo | Valor |
|---|---|
| Área | SEG (também PRD) |
| Natureza | Fato |
| Descrição | O JWT da API expira em 12 h (`Jwt:ExpirationHours`) e não há fluxo de renovação. A revogação, apontada como ausente em DOC-31/DOC-33, já existe (`TokenVersion`). O commit `b0dd317` cita 14 h; o `appsettings.json` atual diz 12. |
| Evidência | `ERP.Api/appsettings.json:10`; `ERP.Api/Program.cs:387-390`; nenhum endpoint de refresh em `AuthController` (2026-10-22). |
| Gravidade | Baixa |
| Impacto | Sessão expira durante o expediente longo; tokens longos aumentam a janela de uso de um token vazado (mitigado pela revogação). |
| Confiança | Alta |
| Dependências | — |
| Responsável | A definir |
| Ação proposta | Na EXC-A2: confirmar o valor efetivo em produção e decidir entre manter (risco aceito) ou criar renovação. |
| Status | Confirmado |
| Critério de validação | Decisão registrada; se houver renovação, testes de expiração e renovação. |
| Origem | DOC-31, DOC-33 |
| Relacionados | — |
| Datas | Registrado em 2026-10-22 |

### EXC-SEG-006 — Senha do login SQL de produção compartilhada fora de um cofre de segredos

| Campo | Valor |
|---|---|
| Área | SEG |
| Natureza | Fato |
| Descrição | Em 2026-10-22 a senha do login usado pelo WPF foi enviada a esta conversa, numa captura de tela de uma anotação de instalação. A mesma senha está em todas as máquinas de loja (EXC-SEG-001). O valor **não** foi reproduzido em nenhum documento. |
| Evidência | Conversa de 2026-10-22; cópia da imagem na pasta temporária da sessão do Claude Code no PC do responsável. |
| Gravidade | Alta (credencial ativa de produção exposta fora de um cofre; mesma credencial em todas as lojas) |
| Impacto | Quem tiver acesso à anotação, à imagem ou ao histórico da conversa pode acessar o banco de produção com os privilégios de EXC-SEG-001. |
| Confiança | Alta |
| Dependências | Trocar a senha exige reconfigurar o `conexao.dat` dos 3 PCs da Vila Verde (e a API, se usar o mesmo login — roteiro 1.3). |
| Responsável | Matheus |
| Ação proposta | Planejar a troca da senha junto com a mitigação de EXC-SEG-001 (criar o login restrito já com senha nova evita trocar duas vezes); apagar a imagem temporária; guardar a credencial num cofre (gerenciador de senhas ou Key Vault), não em anotação. |
| Status | **Planejado** (triagem de 2026-10-22): resolvido junto com EXC-SEG-001 (login novo com senha nova; a senha atual deixa de ser usada pelas lojas). |
| Critério de validação | Senha antiga invalidada; nova senha guardada só em cofre; nenhuma cópia em anotação ou imagem. |
| Origem | Confirmação 2 (2026-10-22) |
| Relacionados | EXC-SEG-001, EXC-SEG-003 |
| Datas | Registrado em 2026-10-22 |

### EXC-SEG-007 — Conexão do WPF ao SQL não valida o certificado do servidor

| Campo | Valor |
|---|---|
| Área | SEG |
| Natureza | Fato |
| Descrição | O assistente de instalação monta a connection string com `TrustServerCertificate=True`. A conexão é cifrada, mas o certificado do servidor não é verificado: um intermediário na rede da loja poderia se passar pelo servidor e capturar a credencial. O Azure SQL apresenta certificado válido, então a opção não é necessária para o servidor atual. |
| Evidência | `ERP.WPF/ViewModels/SetupViewModel.cs:101` (2026-10-22) |
| Gravidade | Média (exige posição na rede da loja; o ganho do atacante é a credencial de EXC-SEG-001) |
| Impacto | Captura da credencial do banco em rede comprometida (por exemplo, Wi-Fi da loja). |
| Confiança | Alta (código); Média (exploração, depende da rede) |
| Dependências | — |
| Responsável | A definir |
| Ação proposta | Na EXC-A2: avaliar remover a opção para conexões ao Azure SQL; o efeito só chega às lojas quando o `conexao.dat` for recriado. |
| Status | Confirmado |
| Critério de validação | Conexões do WPF validam o certificado do servidor. |
| Origem | Confirmação 2 (2026-10-22) |
| Relacionados | EXC-SEG-001 |
| Datas | Registrado em 2026-10-22 |

### EXC-SEG-008 — Servidor SQL sem auditoria, sem Defender e sem administrador do Entra

| Campo | Valor |
|---|---|
| Área | SEG (também OPS) |
| Natureza | Fato |
| Descrição | Na visão geral de `erp-ttsoft-server`, as funcionalidades *Auditoria*, *Microsoft Defender para SQL* e *Administrador do Microsoft Entra* aparecem como "Não configurado". O único acesso administrativo é o login SQL `sa_ttsoft`, o mesmo das lojas. |
| Evidência | Captura do Portal do Azure enviada pelo responsável (2026-10-22) |
| Gravidade | Média. Com a aceitação do EXC-SEG-009 (servidor aberto para a internet), passa a ser controle compensatório prioritário. |
| Impacto | Um acesso indevido com a credencial de EXC-SEG-001 não deixa registro nem dispara alerta; não há como investigar depois. |
| Confiança | Alta |
| Dependências | EXC-SEG-001 (a separação de logins vem antes) |
| Responsável | Matheus |
| Ação proposta | Na EXC-A2/A5: avaliar custo e configuração da auditoria do servidor e de um administrador do Entra para uso humano, separado do login das aplicações. |
| Status | Confirmado |
| Critério de validação | Auditoria ativa com retenção definida; acesso administrativo humano distinto do usado pelas aplicações. |
| Origem | Confirmação 2 (2026-10-22) |
| Relacionados | EXC-SEG-001, EXC-OPS-003 |
| Datas | Registrado em 2026-10-22 |

### EXC-SEG-009 — Firewall do servidor SQL aberto para qualquer IP da internet

| Campo | Valor |
|---|---|
| Área | SEG (também OPS) |
| Natureza | Fato |
| Descrição | O servidor `erp-ttsoft-server` tem a regra de firewall `Liberar`, de `0.0.0.0` a `255.255.255.255`: aceita conexões de qualquer IP da internet. Assim, a única barreira até os dois bancos (ConstruTTor e OutfiTT) é a senha do administrador, que está em todos os PCs das lojas (EXC-SEG-001) e foi exposta na conversa de 2026-10-22 (EXC-SEG-006). Há também a regra `AllowAllWindowsAzureIps` (serviços do Azure, necessária enquanto a API usar o endpoint público), regras por IP da loja e de clientes do OutfiTT, IPs adicionados pelo portal, duas regras com o nome de uma terceira pessoa e uma regra com IP de rede privada (`192.168.x.x`), que não tem efeito no Azure. **Confirmado em 2026-10-22 (consulta de sessões com IP):** os 3 PCs da Vila Verde conectam de um IP diferente do da regra `Loja`, e o PC do responsável de um IP que não corresponde a nenhuma regra. Os dois só funcionam hoje por causa da regra `Liberar`. O IP público da loja já mudou pelo menos uma vez, então é provavelmente dinâmico. O IP atual do cliente do OutfiTT não foi visto (não estava conectado). |
| Evidência | `sys.firewall_rules` consultada pelo responsável em 2026-10-22 (IPs não reproduzidos neste registro) |
| Gravidade | **Crítica.** Junto com EXC-SEG-001 e EXC-SEG-006, qualquer pessoa na internet que tenha a senha controla todos os bancos do servidor. |
| Impacto | Leitura, alteração ou destruição dos dados das duas operações a partir de qualquer lugar; sem auditoria (EXC-SEG-008), sem rastro. |
| Confiança | Alta |
| Dependências | IP atual do cliente do OutfiTT; os da loja e do responsável já foram identificados (EXC-A1-DESENHO-LOGIN-RESTRITO, seção 3) |
| Limitação conhecida | Com IP dinâmico na loja, cada troca de IP pelo provedor corta o acesso do WPF até a regra ser atualizada. A solução definitiva é o WPF não conectar direto no banco (migração WPF→API); alternativas intermediárias (IP fixo no provedor, rede privada) ficam para a EXC-A5. |
| Responsável | Matheus |
| Ação proposta | Enquanto o risco estiver aceito: remover só as regras sem efeito ou de pessoas que não devem ter acesso (confirmar quem é "Bruno"), sem impacto para as lojas. Na revisão: escolher entre as alternativas registradas no campo Status. |
| Status | **Risco aceito** em 2026-10-22 por Matheus, revendo a triagem 2. **Justificativa:** os IPs públicos da região mudam cerca de 4 vezes por semana, o que torna inviável manter regras por IP no Azure. **Condições da aceitação:** (1) a execução do login restrito (E1 a E4) e a troca da senha de `sa_ttsoft` (E6) passam a ser a mitigação principal, porque com o servidor aberto a senha é a única barreira; (2) a auditoria e o Defender do servidor (EXC-SEG-008) passam a ser controles compensatórios a avaliar com prioridade. **Revisão:** ao final da E6, ou antes se houver indício de acesso indevido. **Alternativas para a revisão:** IP fixo no provedor da loja, rede privada (VPN) até o Azure, ou o WPF deixar de conectar direto no banco (migração WPF→API), que elimina a necessidade de expor o banco às lojas. |
| Critério de validação | Nenhuma regra ampla; cada regra com dono identificado; clientes conectando normalmente após a mudança. |
| Origem | EXC-A1, pré-verificação P3 (2026-10-22) |
| Relacionados | EXC-SEG-001, EXC-SEG-006, EXC-SEG-008 |
| Datas | Registrado em 2026-10-22 |

### EXC-SEG-010 — *Seeder* do WPF promove usuários a Administrador pelo nome e cria administrador com senha fixa

| Campo | Valor |
|---|---|
| Área | SEG (também ENG, DAD) |
| Natureza | Fato (código) + Hipótese (efeito em produção) |
| Descrição | `DbSeeder.Seed` roda **a cada abertura do WPF**, contra o banco de produção, no tenant do `licenca.json` do PC. (1) Se o tenant tem o cargo Administrador, troca para esse cargo todo usuário cujo login seja `admin` ou `mahenrique`, ou cujo nome contenha `matheus` ou `administrador`, desfazendo qualquer rebaixamento manual. (2) Se não tem, cria o cargo e um usuário `admin` com **hash bcrypt fixo no código-fonte**, sem `MustChangePassword`. Também cria cargos e permissões para qualquer tenant novo. **Hipótese:** os tenants de 1 usuário de EXC-OPS-007 (CNPJ `00000000000000`, CNPJ da TTSoft e `FDF3CD72…`) foram criados por esse caminho ao abrir um WPF com outro `licenca.json`. |
| Evidência | `ERP.WPF/App.xaml.cs:170-174`; `ERP.Persistence/Context/DbSeeder.cs:13`, `:16-108`, `:157-180`, `:181-198`; `ERP.Domain/Entities/User.cs:12` (2026-10-26, `8801d9c`). [EXC-A6 §4.5](EXC-A6-ARQUITETURA-ENGENHARIA.md) |
| Gravidade | **Alta** — escalada de privilégio dentro do tenant (CRITERIOS §3), reaplicada a cada abertura; credencial padrão conhecida por quem tem o código. |
| Impacto | Um operador de loja cujo nome contenha "Matheus" vira administrador (cancelar vendas, sangria ilimitada, usuários e cargos) sem ninguém decidir. Em tenant criado pelo caminho (2), quem conhece a senha do hash entra como administrador. Isolamento entre tenants não é afetado. |
| Confiança | Alta (código); Baixa (quais usuários e tenants foram afetados) |
| Dependências | Consulta C4 da EXC-A6 |
| Responsável | A definir |
| Ação proposta | Remover a promoção por nome; criar o administrador inicial só por procedimento explícito, com senha aleatória e `MustChangePassword`; tirar o *seeder* da abertura do WPF (cargos e permissões criados pela API na criação do tenant). Antes, escrever o procedimento de recuperação de acesso. Encaminhar à EXC-A2. |
| Status | Confirmado |
| Critério de validação | Nenhum código que altere cargo por nome; nenhuma senha fixa no código; consulta C4 sem usuário promovido indevidamente; teste que garante que a abertura do WPF não altera cargos. |
| Origem | EXC-A6 (2026-10-26) |
| Relacionados | EXC-SEG-001, EXC-OPS-007, EXC-VEN-003 |
| Datas | Registrado em 2026-10-26 |

---

## Detalhe — Engenharia (ENG)

### EXC-ENG-001 — Testes não comprovam comportamento no SQL Server

| Campo | Valor |
|---|---|
| Área | ENG (também SEG) |
| Natureza | Fato (limite da cobertura) |
| Descrição | A suíte (1322 testes aprovados) usa EF Core InMemory e SQLite. Há 40 chamadas de SQL bruto e 51 usos de `IgnoreQueryFilters` fora dos testes. O comportamento desses pontos no SQL Server, inclusive o isolamento por tenant, não é coberto. Não indica defeito; indica ausência de prova. O próprio defeito do `HasQueryFilter` (DOC-34) só apareceu ao trocar InMemory por SQLite. |
| Evidência | `ERP.Tests.csproj` (InMemory e Sqlite); contagem por busca e execução da suíte em 2026-10-22. DOC-30 propõe um teste genérico contra registros com `TenantId` vazio. |
| Gravidade | Média |
| Impacto | Regressões em SQL bruto ou filtros podem passar no CI e só aparecer em produção. |
| Confiança | Alta |
| Dependências | — |
| Responsável | A definir |
| Ação proposta | Na EXC-A1: revisar cada uso de `IgnoreQueryFilters` e SQL bruto; avaliar testes contra SQL Server para os pontos críticos. |
| Atualização (2026-10-26, EXC-A6) | Reconferido no commit `8801d9c`: 1337 testes aprovados; 7 configurações InMemory e 9 SQLite, nenhuma SQL Server; 53 usos de `IgnoreQueryFilters` em 17 arquivos e 39 de SQL bruto em 15 arquivos do código de servidor. Lacuna adicional: **nenhum teste exercita um fluxo composto cliente + servidor** — foi o que deixou passar EXC-ENG-005. A EXC-A6 propõe a onda O2 (testes contra SQL Server e de fluxo composto para venda, cancelamento e devolução). |
| Status | Confirmado |
| Critério de validação | Usos revisados e classificados; pontos críticos cobertos por teste contra SQL Server ou risco aceito. |
| Origem | EXC-F0; INV-001, INV-015; DOC-30, DOC-34 |
| Relacionados | EXC-SEG-001, EXC-ENG-005 |
| Datas | Registrado em 2026-10-22; atualizado em 2026-10-26 (EXC-A6) |

### EXC-ENG-002 — README desatualizado

| Campo | Valor |
|---|---|
| Área | ENG |
| Natureza | Fato |
| Descrição | O `README.md` descreve 5 projetos (existem 10), instrui editar a conexão em `App.xaml.cs` (hoje vem de `SecureConfigService`) e lista como "Próximos passos" funcionalidades já entregues. DOC-31 (E) recomendava um `ARCHITECTURE.md` de uma página para reduzir o bus factor. |
| Evidência | `README.md` (2026-04-10); `ERP.sln`; `ERP.WPF/App.xaml.cs:132`; DOC-31 |
| Gravidade | Baixa |
| Impacto | Dificulta a entrada de novas pessoas e induz configuração errada. |
| Confiança | Alta |
| Dependências | EXC-A6 |
| Responsável | A definir |
| Ação proposta | Reescrever o README e criar a descrição de arquitetura após a EXC-A6. |
| Status | Confirmado |
| Critério de validação | README descreve os 10 projetos, a configuração atual e o processo de build/teste. |
| Origem | EXC-F0; DOC-31 |
| Relacionados | — |
| Datas | Registrado em 2026-10-22 |

### EXC-ENG-003 — 155 avisos de compilação

| Campo | Valor |
|---|---|
| Área | ENG |
| Natureza | Fato |
| Descrição | O build gera 155 avisos, majoritariamente de anulabilidade (CS8618, CS8604, CS8601, CS8603, CS8602), além de `NU1701`, `CS0618` e `NU1903` (EXC-SEG-002). DOC-34 citava 16 avisos em 13/08, possivelmente com outro escopo de build. |
| Evidência | Log do build de 2026-10-22, `ec6d177` |
| Gravidade | Baixa |
| Impacto | Avisos relevantes ficam escondidos no volume. |
| Confiança | Alta |
| Dependências | — |
| Responsável | A definir |
| Ação proposta | Na EXC-A6: separar avisos com risco real dos cosméticos. |
| Atualização (2026-10-26, EXC-A6) | Classificados no build de `8801d9c` (155 linhas de aviso): **com risco** — NU1903 ×10 (EXC-SEG-002); CS4014 ×2 (chamada assíncrona não aguardada em `FinalizarVendaViewModel.cs:137`, exceção perdida); CS0618 ×8 (`System.Data.SqlClient` obsoleto em `SetupViewModel.cs:113`; API `Canvas` do QuestPDF obsoleta em `CarnePdfReport.cs`); NU1701 ×20 (LiveCharts `2.0.0-rc6.1`, OpenTK e `SkiaSharp.Views.WPF` restaurados para .NET Framework). **Cosméticos** — anulabilidade (CS86xx) ≈115; CS1998 ×4; CS0108 ×2; SYSLIB0050 ×1 (teste). Proposta: tratar CS4014 e CS0618 já; NU1701 na próxima atualização de gráficos; anulabilidade por arquivo tocado. |
| Status | Confirmado |
| Critério de validação | Avisos restantes conhecidos e justificados (CRITERIOS ENG-2). |
| Origem | EXC-F0 |
| Relacionados | EXC-SEG-002 |
| Datas | Registrado em 2026-10-22; atualizado em 2026-10-26 (EXC-A6) |

### EXC-ENG-004 — `ChangeTracker.Clear()` após todo `SaveChanges`

| Campo | Valor |
|---|---|
| Área | ENG (também DAD) |
| Natureza | Fato |
| Descrição | O `AppDbContext` limpa o rastreamento de entidades depois de todo `SaveChanges`. Código que salva e continua alterando a mesma entidade perde as alterações seguintes sem erro. Combinado com `NoTracking` global, já causou defeitos reais na integração com o Mercado Livre. |
| Evidência | `ERP.Persistence/Context/AppDbContext.cs:806` e `:816` (2026-10-22); commits `2d62508`, `4a9642d`, `3baceab`; DOC-31 (A), DOC-33 (§13, §18) |
| Gravidade | Média |
| Impacto | Gravações perdidas silenciosamente em código novo ou alterado. |
| Confiança | Alta |
| Dependências | EXC-A6 |
| Responsável | A definir |
| Ação proposta | Na EXC-A6: mapear métodos que salvam e continuam mutando; decidir entre manter o padrão com salvaguardas (teste, análise estática, documentação) ou mudá-lo. Não mudar sem esse mapa. |
| Atualização (2026-10-26, EXC-A6) | Mapa parcial: o padrão perigoso hoje não é só "salvar e continuar mutando", mas **um serviço chamado no meio de uma operação maior gravar por conta própria**. `HaverService.RegistrarMovimentoVendaAsync` (`RelatorioServices.cs:420-432`) chama `SaveChanges`, que grava tudo o que estava pendente no contexto compartilhado e limpa o rastreamento; é chamado no meio de `SaleService.CancelAsync` e de `DevolucaoService.DevolverItensAsync`, dividindo cada operação em duas gravações (EXC-ENG-008). Não foi encontrada gravação perdida nesses dois métodos (as entidades seguintes são reanexadas por `Update`/`Add`). O mapa completo fica com a EXC-A3. Decisão sobre o padrão continua pendente. |
| Status | Confirmado |
| Critério de validação | Decisão registrada com mapa de impacto; salvaguarda implementada se o padrão for mantido. |
| Origem | DOC-31, DOC-33 |
| Relacionados | EXC-DAD-002, EXC-ENG-008 |
| Datas | Registrado em 2026-10-22; atualizado em 2026-10-26 (EXC-A6) |

### EXC-ENG-005 — Cancelar venda em dinheiro pelo histórico do WPF estorna o caixa duas vezes

| Campo | Valor |
|---|---|
| Área | ENG (também DAD) |
| Natureza | Fato (código) + Lacuna (quantas vendas foram afetadas) |
| Descrição | Ao cancelar uma venda pelo histórico, o WPF chama a API, cujo `SaleService.CancelAsync` cria para cada pagamento não-Haver um movimento `CancelamentoVenda` com valor `−pagamento.Amount`. Em seguida o próprio WPF chama `MotorFinanceiroService.EstornarVendaAsync`, que para dinheiro registra uma **sangria** "ESTORNO VENDA …" de `valor − troco`. Os dois reduzem o saldo em dinheiro e o resumo do caixa. Além disso, o estorno do servidor não desconta o troco (o lançamento original foi `valor − troco`). Se a sangria for recusada pela política (saldo ou limite do cargo), o operador vê erro de cancelamento depois de a venda já estar cancelada. Estorno pelo cliente desde 21/07 (`16daf9a`); estorno no servidor desde 11/09 (`ae2adde`), sem remover o do cliente. PIX e cartão não duplicam (o cliente estorna em conta bancária e recebíveis, não no caixa). |
| Evidência | `ERP.Application/Services/SaleService.cs:398-418` (`:409`); `ERP.WPF/ViewModels/SaleViewModel.cs:174-213`; `ERP.Application/Services/MotorFinanceiroService.cs:99-117`, `:158`; `ERP.WPF/Services/HttpCaixaService.cs:64-105`; `ERP.Infrastructure/Repositories/CaixaRepository.cs:106-116`; `ERP.Application/Services/CaixaService.cs:113-119`, `:235-275`; `git blame` (2026-10-26). [EXC-A6 §4.2](EXC-A6-ARQUITETURA-ENGENHARIA.md) |
| Gravidade | **Alta** — dado financeiro do caixa incorreto em fluxo crítico (CRITERIOS §7, itens 2 e 3), sem aviso ao operador; correção manual possível. Subiria para Crítica se o fechamento de caixa alimentar conciliação ou contabilidade. |
| Impacto | Cada cancelamento de venda em dinheiro deixa o caixa com o valor da venda (mais o troco) a menos; o fechamento mostra falta que não existe. Cancelamentos reais ocorrem na Vila Verde (comentário de 17/09 em `SaleService.cs:385-397`). |
| Confiança | Alta (código); ocorrências em produção a confirmar (consulta C1) |
| Dependências | Consulta C1 da EXC-A6 |
| Responsável | A definir |
| Ação proposta | O servidor passa a ser o único dono do estorno (dinheiro, PIX e cartão, idempotentes por `SalePaymentId`; valor do lançamento original, com troco); o WPF deixa de estornar. Ordem: API primeiro, WPF depois (EXC-A6 §8). Teste de integração do fluxo composto. Correção dos caixas afetados decidida pelo responsável a partir da C1. |
| Status | Confirmado |
| Critério de validação | Teste automatizado: abrir caixa → vender em dinheiro com troco → cancelar → saldo em dinheiro igual ao da abertura; consulta C1 sem ocorrências novas após a publicação. |
| Origem | EXC-A6 (2026-10-26) |
| Relacionados | EXC-ENG-008, EXC-ENG-010, EXC-ENG-001 |
| Datas | Registrado em 2026-10-26 |

### EXC-ENG-006 — Edição de produto e de cliente pelo Portal apaga campos e sobrescreve o estoque

| Campo | Valor |
|---|---|
| Área | ENG (também FIS, DAD) |
| Natureza | Fato (código) + Lacuna (uso do Portal para editar) |
| Descrição | O Portal edita produto com um objeto anônimo de 14 campos; a API recebe `UpdateProductDto` (40 campos herdados de `CreateProductDto`) e copia **todos** para a entidade com AutoMapper, sem ignorar ausentes. Os campos não enviados viram nulo, zero ou padrão: NCM, CEST, CFOP, CSOSN, categoria, marca, fornecedor, IPI, ICMS, atacado, produto pai e fator, unidade de venda, `AllowNegativeStock`, descrição, campanha. Sem NCM/CSOSN a NFC-e sai com `00000000`/`102`. Cliente: o Portal envia 10 de 14 campos e a API apaga IE, UF, bairro e complemento. Nos dois clientes o estoque é gravado com o valor do formulário; o WPF relê antes de salvar quando o estoque não foi alterado, o Portal não. Causa: o Portal não compartilha contratos com a API e o PUT é substituição total. |
| Evidência | `ERP.Portal/Pages/Produtos.razor:662-685`; `ERP.Portal/Pages/Clientes.razor:260-278`; `ERP.Application/DTOs/Dtos.cs:58-104`; `ERP.Application/Mappings/MappingProfile.cs:44-48`, `:72`; `ERP.Application/Services/ProductService.cs:128-150`; `ERP.Application/Services/CustomerService.cs:107`; `ERP.Infrastructure/Services/FiscalService.cs:804-805`; `ERP.WPF/ViewModels/ProductViewModel.cs:361-366` (2026-10-26). [EXC-A6 §4.4](EXC-A6-ARQUITETURA-ENGENHARIA.md) |
| Gravidade | **Alta** — cadastro fiscal apagado sem aviso, levando a notas rejeitadas ou com dados errados (CRITERIOS §3: risco fiscal); estoque sobrescrito. Cai para Baixa se o Portal não for usado para editar. |
| Impacto | Uma edição de preço pelo Portal tira o NCM do produto e a IE/UF do cliente; a próxima nota sai errada ou é rejeitada; vendas feitas enquanto o formulário estava aberto somem do estoque. |
| Confiança | Alta (código); uso em produção desconhecido |
| Dependências | Consulta C2 da EXC-A6; confirmação de uso do Portal pela loja |
| Responsável | A definir |
| Ação proposta | Etapa 1 (só Portal): ler o registro e reenviar o objeto completo, ou retirar a edição do Portal até a etapa 2. Etapa 2: contrato de edição com só os campos editáveis; estoque alterado apenas por ajuste/movimento, com o WPF migrado junto. Teste de contrato: PUT parcial não altera NCM nem IE. |
| Status | Confirmado |
| Critério de validação | Teste automatizado de PUT parcial; consulta C2 sem ocorrências novas; registros afetados corrigidos. |
| Origem | EXC-A6 (2026-10-26) |
| Relacionados | EXC-FIS-011, EXC-ENG-012 |
| Datas | Registrado em 2026-10-26 |

### EXC-ENG-007 — Produto filho: estoque devolvido ao produto errado e NFC-e com a quantidade do pai

| Campo | Valor |
|---|---|
| Área | ENG (também DAD, FIS) |
| Natureza | Fato (código) + Lacuna (uso de produto filho) |
| Descrição | Na venda de produto filho (`ParentProductId` + `ConversionFactor`), `SaleService.CreateAsync` baixa o estoque do **pai** e grava em `SaleItem` o `ProductId` do **filho** com a quantidade **na unidade do pai**. O cancelamento (`CancelAsync`) e a devolução (`DevolverItensAsync`) devolvem essa quantidade ao **filho**. A NFC-e usa `SaleItem.Quantity`, saindo com a quantidade do pai e a descrição do filho (total correto, quantidade e unitário errados). O e-commerce grava na unidade vendida e documenta a divergência. |
| Evidência | `ERP.Application/Services/SaleService.cs:144-159`, `:204-214`, `:345-357`; `ERP.Application/Services/DevolucaoService.cs:57-62`; `ERP.Infrastructure/Services/FiscalService.cs:791-805`; `ERP.Api/Ecommerce/EcommercePedidoService.cs:33-34`; teste só da baixa (`ProductServiceTests.cs:168-175`) (2026-10-26) |
| Gravidade | Média — estoque de dois produtos errado após cancelamento/devolução e quantidade fiscal incorreta; só ocorre com produto filho vendido. |
| Impacto | Rolo fica com estoque a menos e a barra com estoque a mais a cada cancelamento; devolução fiscal referencia quantidades na unidade errada. |
| Confiança | Alta (código); uso desconhecido |
| Dependências | Consulta C3 da EXC-A6; EXC-A4 para o efeito fiscal |
| Responsável | A definir |
| Ação proposta | Se C3 mostrar uso: gravar no item o produto de estoque e o fator (colunas novas anuláveis; vendas antigas pela regra antiga) e corrigir cancelamento, devolução e NFC-e. Se não houver uso: bloquear cancelamento/devolução automática desses itens até a correção. |
| Status | Confirmado |
| Critério de validação | Teste: vender filho → cancelar → estoques do pai e do filho iguais aos de antes; NFC-e com a quantidade vendida. |
| Origem | EXC-A6 (2026-10-26) |
| Relacionados | EXC-ENG-008, EXC-ENG-012 |
| Datas | Registrado em 2026-10-26 |

### EXC-ENG-008 — Cancelamento e devolução gravam em várias etapas sem transação

| Campo | Valor |
|---|---|
| Área | ENG (também DAD) |
| Natureza | Fato |
| Descrição | Diferente da criação da venda, o cancelamento e a devolução não usam transação. `CancelAsync` grava em duas etapas: o `SaveChanges` interno do `HaverService` (estoque e Haver) e o `CommitAsync` final (estorno do caixa, contas a receber, status da venda). `DevolverItensAsync` grava em três: estoque por `UPDATE` em autocommit, devoluções + movimento de Haver, e saldo do cliente. Falha entre as etapas deixa estado parcial; como a trava contra repetição conta registros gravados depois, uma nova tentativa devolve estoque e Haver outra vez. O estoque no cancelamento é por leitura e gravação (`product.Stock +=`), não pelo `UPDATE` atômico usado na venda. |
| Evidência | `ERP.Application/Services/SaleService.cs:337-440`; `ERP.Application/Services/DevolucaoService.cs:42-114`; `ERP.Infrastructure/Services/RelatorioServices.cs:420-432`; `ERP.Persistence/Context/AppDbContext.cs:810-818`; padrão correto em `SaleService.cs:114-118` (2026-10-26) |
| Gravidade | Média — dinheiro e estoque; exige falha no meio da operação. |
| Impacto | Venda com estoque devolvido e status ainda ativa; cliente com Haver creditado duas vezes após nova tentativa. |
| Confiança | Alta (código); frequência desconhecida |
| Dependências | EXC-ENG-001 (testes contra SQL Server) |
| Responsável | A definir |
| Ação proposta | Na EXC-A3: aplicar o padrão da venda (estratégia de repetição + transação explícita); `HaverService` recebendo a unidade de trabalho em vez de gravar sozinho; estoque por `UPDATE` atômico. |
| Status | Confirmado |
| Critério de validação | Teste contra SQL Server com falha injetada entre as etapas: nada gravado; nova tentativa não duplica. |
| Origem | EXC-A6 (2026-10-26) |
| Relacionados | EXC-DAD-002, EXC-ENG-004, EXC-ENG-005 |
| Datas | Registrado em 2026-10-26 |

### EXC-ENG-009 — WPF compartilha um único `AppDbContext` entre telas

| Campo | Valor |
|---|---|
| Área | ENG |
| Natureza | Fato (configuração) + Hipótese (frequência do erro) |
| Descrição | O WPF monta o contêiner sem escopos por tela. `PdvViewModel` é *singleton* e recebe serviços *Scoped* locais; o `MainWindow` resolve `IUnitOfWork` e as ViewModels pela raiz. Resolvidos pela raiz, todos recebem o mesmo `AppDbContext`, que dura até o programa fechar e não aceita operações simultâneas. O erro "A second operation was started on this context" já ocorreu (15/09); o semáforo criado protege só as tarefas de inicialização do PDV. |
| Evidência | `ERP.WPF/App.xaml.cs:298-303`, `:537`; `ERP.WPF/ViewModels/PdvViewModel.cs:331-348`; `ERP.WPF/MainWindow.xaml.cs:71-75`, `:448` (2026-10-26) |
| Gravidade | Média — operações falham de forma intermitente; há contorno (repetir). |
| Impacto | Erros esporádicos em telas que acessam o banco enquanto o PDV ou outra tela também acessa. |
| Confiança | Alta (configuração); Baixa (frequência) |
| Dependências | Logs dos PCs da loja |
| Responsável | A definir |
| Ação proposta | Fábrica de escopo por operação/tela; ViewModel do PDV continua *singleton* sem guardar serviços de banco; `ValidateScopes` em desenvolvimento. Perde relevância à medida que a migração WPF→API avança (EXC-ENG-010). |
| Status | Confirmado |
| Critério de validação | Contêiner com `ValidateScopes` sem erro; nenhum serviço *Scoped* resolvido pela raiz. |
| Origem | EXC-A6 (2026-10-26) |
| Relacionados | EXC-ENG-010 |
| Datas | Registrado em 2026-10-26 |

### EXC-ENG-010 — WPF em dois caminhos (API e banco direto), com regras divididas entre cliente e servidor

| Campo | Valor |
|---|---|
| Área | ENG (também SEG) |
| Natureza | Fato |
| Descrição | Sete serviços do WPF já usam a API (venda, cliente, produto, caixa, contas a pagar e a receber, emissão fiscal); cerca de 70 continuam locais, e 25 arquivos usam `AppDbContext`/`IUnitOfWork` diretamente. Fluxos que cruzam os dois caminhos ficam com a regra dividida: a venda roda o motor financeiro no servidor e de novo no cliente; o cancelamento é orquestrado pela tela. A prática de troca por DI com reversão documentada é boa; falta ordem por risco (dinheiro e segurança ficaram para trás). |
| Evidência | `ERP.WPF/App.xaml.cs:327-526`; `ERP.WPF/ViewModels/SaleViewModel.cs:169-219`; `ERP.WPF/ViewModels/FinalizarVendaViewModel.cs:640-644`; contagens de 2026-10-26 em [EXC-A6 §3.4](EXC-A6-ARQUITETURA-ENGENHARIA.md) |
| Gravidade | Média — causa estrutural de EXC-SEG-001 e EXC-ENG-005. |
| Impacto | Cada módulo migrado pela metade pode gerar regra duplicada ou conflitante. |
| Confiança | Alta |
| Dependências | — |
| Responsável | A definir |
| Ação proposta | Onda O4 da EXC-A6: Haver/devolução/motor financeiro → auth/usuários/cargos → fiscal restante → relatórios. Critério de pronto por módulo: toda escrita no servidor, nenhuma orquestração no cliente, teste de fluxo composto. |
| Status | Confirmado |
| Critério de validação | WPF sem `conexao.dat` (CRITERIOS ENG-1). |
| Origem | EXC-A6 (2026-10-26) |
| Relacionados | EXC-SEG-001, EXC-ENG-005, EXC-ENG-009 |
| Datas | Registrado em 2026-10-26 |

### EXC-ENG-011 — Tenant e usuário resolvidos por estado estático mutável

| Campo | Valor |
|---|---|
| Área | ENG (também SEG) |
| Natureza | Fato |
| Descrição | O tenant vem de três fontes (`AsyncLocal` estático, campo estático do WPF, `IRequestTenant` com *setters* públicos) com ordens diferentes para filtro (`CurrentFilterTenantId`) e gravação (`GetTenantId`). O usuário vem também de `ERP.Domain.CurrentUser` (estático), que só o WPF preenche; `ProductService` o usa para gravar quem mudou o preço, e na API, que edita produtos desde a Fase C, o campo fica vazio. |
| Evidência | `ERP.Persistence/Context/AppDbContext.cs:19-28`, `:43-55`, `:101-107`, `:219-220`; `ERP.Api/Services/RequestTenant.cs`; `ERP.Domain/CurrentUser.cs:6`; `ERP.Application/Services/ProductService.cs:140`; `ERP.WPF/State/AppSession.cs:62-63` (2026-10-26) |
| Gravidade | Baixa (o defeito concreto é o autor vazio); o risco ao isolamento é avaliado na EXC-A1 |
| Impacto | Trilha de alteração de preço sem autor; correção do isolamento dependente de convenções não verificadas pelo compilador. |
| Confiança | Alta |
| Dependências | EXC-A1 |
| Responsável | A definir |
| Ação proposta | Usar `IRequestTenant.UserName` em `ProductService` (correção pequena). Depois da EXC-A1: fonte única e imutável de tenant/usuário por requisição. |
| Status | Confirmado |
| Critério de validação | Alteração de preço pela API grava o autor; nenhum estático de tenant/usuário no código de servidor. |
| Origem | EXC-A6 (2026-10-26) |
| Relacionados | EXC-SEG-001, EXC-ENG-001 |
| Datas | Registrado em 2026-10-26 |

### EXC-ENG-012 — Regras de negócio espalhadas e duplicadas

| Campo | Valor |
|---|---|
| Área | ENG |
| Natureza | Recomendação |
| Descrição | Regras estão em Application, em Infrastructure (30 de 41 serviços com `AppDbContext` direto), na API (e-commerce cria venda direto no contexto) e em ViewModels do WPF. Há três implementações de criação de venda, com unidades diferentes em `SaleItem.Quantity`. Status são texto livre com grafias diferentes para o mesmo estado (`"Cancelado"`/`"Cancelada"`). Arquivos de alta complexidade: `PdvViewModel` (1830 linhas), `PDV.razor` (1794), `NotaAvulsaViewModel` (1187). |
| Evidência | Contagens e arquivos em [EXC-A6 §3.3 e §5](EXC-A6-ARQUITETURA-ENGENHARIA.md); `ERP.Application/Services/SaleService.cs:423`; `ERP.WPF/ViewModels/SaleViewModel.cs:193` (2026-10-26) |
| Gravidade | Baixa |
| Impacto | Correção feita em um caminho não chega aos outros (como em EXC-ENG-007). |
| Confiança | Alta |
| Dependências | — |
| Responsável | A definir |
| Ação proposta | Onda O5: um serviço por operação de negócio; status como enum; quebrar PDV em componentes. Fazer ao tocar cada módulo, sem projeto dedicado. |
| Status | Confirmado |
| Critério de validação | Cada operação de negócio com um único ponto de implementação. |
| Origem | EXC-A6 (2026-10-26) |
| Relacionados | EXC-ENG-006, EXC-ENG-007, EXC-ENG-010 |
| Datas | Registrado em 2026-10-26 |

### EXC-ENG-013 — URL da API fixa no WPF; arquivos e pacotes órfãos

| Campo | Valor |
|---|---|
| Área | ENG (também OPS) |
| Natureza | Fato |
| Descrição | A URL da API de produção está fixa em dois pontos do `App.xaml.cs`, então um WPF não pode apontar para staging sem recompilar. O arquivo temporário de build `ERP.WPF/ERP.WPF_1qyhnkib_wpftmp.csproj` está versionado (com versão de QuestPDF diferente da do projeto). Existe na raiz uma pasta residual `{ERP.Domain\{Entities,…` (não versionada). `ERP.Application` referencia Dapper sem usar. `UnitOfWork.Dispose` descarta o `AppDbContext` que pertence ao contêiner. Classes órfãs mantidas de propósito (`NfeContingencyWorker`, `WpfEstoqueSyncService`, `FiscalService` no WPF). |
| Evidência | `ERP.WPF/App.xaml.cs:534`, `:553`; `git ls-files`; `ERP.Application/ERP.Application.csproj`; `ERP.Infrastructure/UnitOfWork/UnitOfWork.cs:92` (2026-10-26) |
| Gravidade | Baixa |
| Impacto | Testes do WPF contra staging exigem build especial; ruído no repositório. |
| Confiança | Alta |
| Dependências | EXC-OPS-002, EXC-OPS-007 (ambiente de staging) |
| Responsável | A definir |
| Ação proposta | URL em `appsettings.json` do WPF; remover arquivos e pacote órfãos; decidir prazo para apagar classes órfãs. |
| Status | Confirmado |
| Critério de validação | WPF configurável por arquivo; repositório sem arquivos temporários de build. |
| Origem | EXC-A6 (2026-10-26) |
| Relacionados | EXC-OPS-002 |
| Datas | Registrado em 2026-10-26 |

---

## Detalhe — Infraestrutura e operações (OPS)

### EXC-OPS-001 — Checklist de deploy e `versao.json` versionados desatualizados

| Campo | Valor |
|---|---|
| Área | OPS |
| Natureza | Fato |
| Descrição | `Scripts/CHECKLIST_DEPLOY.md` descreve a v1.1.0 (março/2026), com publicação em arquivo único e Blob anônimo. O `Scripts/versao.json` versionado não tem `Sha256`, exigido pelo atualizador desde `2448e7c`. **Inferência:** seguir o checklist e publicar o `versao.json` versionado resultaria em atualização recusada. DOC-31 (E) já apontava que a distribuição do WPF é "conhecimento tribal". |
| Evidência | `Scripts/CHECKLIST_DEPLOY.md`; `Scripts/versao.json`; `ERP.WPF/Services/UpdateService.cs:88`; DOC-31 |
| Gravidade | Média |
| Impacto | Publicação de versão com procedimento errado; lojas sem atualização. |
| Confiança | Alta (documentos e código); Média (efeito, inferido) |
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
| Descrição | `ci-cd.yml` publica API e Portal em todo push na `main`, após os testes, sem aprovação. `staging.yml` dispara apenas em `develop`, branch inexistente. Não há passagem automática por staging. DOC-31 (E) cita também um "duplo caminho de deploy" (pipeline com publish profile e Deployment Center do Azure) e DOC-34 um App Service que às vezes mantém código antigo até reinício manual. |
| Evidência | `.github/workflows/ci-cd.yml`; `.github/workflows/staging.yml`; `git branch -a` (2026-10-22); DOC-31, DOC-34 |
| Gravidade | **A classificar** — depende de proteções de branch e ambientes no GitHub (não visíveis no repositório). |
| Impacto | Mudanças, inclusive fiscais, chegam à produção no mesmo push, sem validação em staging; possível divergência entre o código publicado e o que roda. |
| Confiança | Alta para a configuração do repositório; Baixa para o duplo caminho (relato) |
| Dependências | Acesso às configurações do GitHub e do Azure |
| Responsável | A definir |
| Ação proposta | Na EXC-A5: confirmar proteções e caminhos de deploy existentes e definir o fluxo de promoção. |
| Status | Confirmado |
| Critério de validação | CRITERIOS OPS-1 atendido ou risco aceito. |
| Origem | EXC-F0; DOC-31, DOC-34 |
| Relacionados | PLANO-MESTRE §6.3 |
| Datas | Registrado em 2026-10-22 |

### EXC-OPS-003 — Restauração do banco de produção nunca testada

| Campo | Valor |
|---|---|
| Área | OPS (também DAD) |
| Natureza | Fato (restauração nunca testada) + Lacuna (configuração de backup) |
| Descrição | O responsável acredita que existe backup, mas nunca testou uma restauração (2026-10-22). O Azure SQL faz backups automáticos, com retenção que depende do plano; os dois bancos estão no plano Básico, que tem a menor retenção. A configuração real não foi vista. O WPF tem um `BackupService` local (DOC-31), cujo conteúdo e utilidade não foram auditados. O `ERPTTSoftStaging` guarda os dados reais da loja piloto. |
| Evidência | Declaração do responsável (2026-10-22); captura do Portal do Azure (plano Básico); DOC-31 (B.1) |
| Gravidade | **Alta.** Sem restauração testada não há garantia de recuperar os dados reais de produção. |
| Impacto | Perda de dados de vendas, financeiro e fiscal sem recuperação comprovada. |
| Confiança | Alta (restauração nunca testada); Baixa (configuração de backup) |
| Dependências | — |
| Responsável | Matheus |
| Ação proposta | Na EXC-A5 (ou antes, se a triagem decidir): ver no portal a retenção configurada e fazer uma restauração de teste para um banco **novo**, sem tocar no de produção, conferindo os dados. Registrar data, duração e resultado. |
| Status | **Planejado** (triagem de 2026-10-22): antecipado da EXC-A5 — ver a retenção configurada no portal e restaurar uma cópia para um banco novo, sem tocar no de produção, registrando data, duração e resultado. |
| Critério de validação | CRITERIOS OPS-2. |
| Origem | EXC-F0; DOC-31 |
| Relacionados | — |
| Datas | Registrado em 2026-10-22 |

### EXC-OPS-004 — Versão do produto sem fonte única; versões e bancos em campo desconhecidos

| Campo | Valor |
|---|---|
| Área | OPS (também DAD) |
| Natureza | Fato (divergência) + Lacuna (situação em campo) |
| Descrição | `ERP.WPF.csproj` e `Scripts/versao.json` indicam 1.1.0; o Portal exibe "2.4.1" fixo no código. Em 13/08 a Vila Verde ainda rodava a versão antiga com banco local (DOC-34). Segundo o responsável (2026-10-22), hoje ela usa 3 PCs com o último artefato WPF publicado pelo GitHub Actions, ligados ao `ERPTTSoftStaging`; os registros de auditoria confirmam 3 máquinas ativas em 22/10 (`DESKTOP-JQB0BIQ`, `SERVIDOR`, `COMPUTADOR03`) e vendas diárias. Como a versão do assembly é sempre 1.1.0 e o WPF não reporta versão a nenhum serviço, **não há como saber pelo sistema qual build está em cada PC**: só pela data ou hash do executável comparados com o artefato. A distribuição pelo artefato do GitHub também indica que o atualizador automático (Blob + `versao.json`) pode não estar em uso. |
| Evidência | `ERP.WPF/ERP.WPF.csproj:3-5`; `Scripts/versao.json`; `ERP.Portal/Pages/Roadmap.razor:17`; nenhuma telemetria de versão no WPF; DOC-33, DOC-34; declaração do responsável (2026-10-22) |
| Gravidade | Baixa (a divergência de versão) |
| Impacto | Sem saber a build instalada, não dá para dizer se uma correção já chegou à loja. |
| Atualização (2026-10-22) | O responsável declarou que os 3 PCs foram atualizados com o artefato WPF do último push e que a API usa o `ERPTTSoftStaging`. A atualização é manual, pelo artefato do GitHub Actions, e não pelo atualizador automático. |
| Confiança | Alta (versões no código); Média (situação em campo, declarada e não verificada por hash) |
| Dependências | PLANO-MESTRE §7, confirmação 4 |
| Responsável | A definir |
| Ação proposta | Roteiro, passos 3 e 4 (build por PC e lojas ativas no banco); definir a fonte de versão e o canal oficial de distribuição na EXC-A8. |
| Status | Confirmado |
| Critério de validação | CRITERIOS OPS-5 e DAD-4; inventário de instalações registrado. |
| Origem | EXC-F0; DOC-33, DOC-34 |
| Relacionados | EXC-OPS-001, EXC-PRD-001 |
| Datas | Registrado em 2026-10-22; atualizado em 2026-10-22 (relatórios anteriores) |

### EXC-OPS-005 — Licenciamento depende de uma API cujo código está só no PC do responsável

| Campo | Valor |
|---|---|
| Área | OPS (também ADM) |
| Natureza | Fato |
| Descrição | O `LicenseManager` do WPF consulta um App Service diferente da API do ERP. Segundo o responsável (2026-10-22), o código dessa API está apenas no PC dele, fora de um repositório remoto. |
| Evidência | `ERP.WPF/Security/LicenseManager.cs:13`; DOC-31 (C, E); declaração do responsável (2026-10-22) |
| Gravidade | Média. O serviço publicado continua funcionando, mas a perda do PC impediria mantê-lo ou corrigi-lo. Pode subir se o WPF bloquear as lojas quando a API de licenças estiver fora do ar (não verificado). |
| Impacto | Sem como corrigir ou republicar o serviço de licenças se o PC for perdido; bus factor (EXC-ADM-002). |
| Confiança | Alta (localização do código, declarada); — (comportamento em falha) |
| Dependências | — |
| Responsável | Matheus |
| Ação proposta | Versionar o código num repositório privado (decisão simples, sem mudança de produção). Na EXC-A5: documentar o serviço e testar o comportamento do WPF com a API indisponível. |
| Status | Confirmado |
| Critério de validação | Serviço documentado e comportamento em falha conhecido e aceito. |
| Origem | DOC-31 |
| Relacionados | — |
| Datas | Registrado em 2026-10-22 |

### EXC-OPS-006 — Rate limit e cache em memória impedem escalar para mais de uma instância

| Campo | Valor |
|---|---|
| Área | OPS (também ENG) |
| Natureza | Fato |
| Descrição | A API usa `AddMemoryCache` e o limitador de requisições do ASP.NET Core, ambos por processo. Com duas ou mais instâncias, os limites e o cache (inclusive ETag) deixam de ser consistentes. |
| Evidência | `ERP.Api/Program.cs:181` e `:513` (2026-10-22); DOC-31 (C) |
| Gravidade | Baixa (hoje, presumindo uma instância — a confirmar) |
| Impacto | Limites de login e cadastro contornáveis em escala horizontal. |
| Confiança | Média (número de instâncias não verificado) |
| Dependências | EXC-A5 |
| Responsável | A definir |
| Ação proposta | Registrar como restrição de escala na EXC-A5; tratar quando houver plano de mais de uma instância. |
| Status | Confirmado |
| Critério de validação | Restrição documentada; solução antes de habilitar escala horizontal. |
| Origem | DOC-31 |
| Relacionados | — |
| Datas | Registrado em 2026-10-22 |

### EXC-OPS-007 — Produção usa o banco `ERPTTSoftStaging`, que também recebe testes, num servidor compartilhado com o OutfiTT

| Campo | Valor |
|---|---|
| Área | OPS (também DAD, SEG) |
| Natureza | Fato |
| Descrição | O banco do ConstruTTor em produção se chama `ERPTTSoftStaging` (declaração do responsável e comentário em `AppDbContextFactory`). O checklist do Mercado Livre usa um tenant de teste nesse mesmo banco, então dados de teste e de clientes convivem. O servidor `erp-ttsoft-server` também hospeda o `ERPTTSoft`, usado pelo OutfiTT (ambos no plano Básico). Não existe banco de staging separado. Tenants encontrados no banco em 2026-10-22 (TenantId calculado a partir do CNPJ): **Vila Verde** (`B7F9F866…`, 4 usuários, em operação diária); **teste** CNPJ `11222333000181` (`8FB9FC74…`, 3 usuários, 22 vendas em 30 dias — ver EXC-FIS-008); CNPJ **`00000000000000`**, o valor padrão do `licenca.json` (`A3156E2E…`, 1 usuário, gravações da máquina `DESKTOP-3P3C5VH` em 26/09); CNPJ **`65183796000100`**, da própria TTSoft (`5AA4DA8B…`, 1 usuário); e `FDF3CD72…`, sem CNPJ identificado (1 usuário). |
| Evidência | Declaração do responsável (2026-10-22); `ERP.Persistence/Context/AppDbContextFactory.cs:11-14`; `CHECKLIST_REGRESSAO_MERCADO_LIVRE.md` (seção 0); captura do Portal do Azure; roteiro 4.1, 4.3, 4.4 (2026-10-22) |
| Gravidade | Média (a consequência fiscal mais grave está separada em EXC-FIS-008) |
| Impacto | Testes alteram e poluem dados de produção; o nome induz a operações erradas (achar que é ambiente de teste); problemas de capacidade ou de acesso em um produto afetam o outro. |
| Confiança | Alta (tenants de teste em produção verificados no banco) |
| Dependências | Roteiro, passo 1.3 (banco usado pela API) |
| Responsável | Matheus |
| Ação proposta | Na EXC-A5: inventariar tenants de teste no banco de produção e definir ambientes separados (nomes, bancos e logins por produto e por ambiente). |
| Status | Confirmado |
| Critério de validação | Produção e testes em bancos distintos, com nomes que não induzam a erro; cada produto com login próprio. |
| Origem | Confirmação 4 (2026-10-22) |
| Relacionados | EXC-SEG-001, EXC-OPS-002 |
| Datas | Registrado em 2026-10-22 |

---

## Detalhe — Fiscal, jurídico e conformidade (FIS)

### EXC-FIS-001 — Estado da recuperação fiscal em produção desconhecido

| Campo | Valor |
|---|---|
| Área | FIS |
| Natureza | Lacuna |
| Descrição | A Etapa 4 introduziu a recuperação de NFC-e pendentes e um interruptor por tenant. O interruptor é a configuração `FiscalRecovery:TenantsHabilitados` (App Setting `FiscalRecovery__TenantsHabilitados` no App Service), uma lista de TenantIds separados por vírgula. Ausente ou vazia, nenhum tenant é habilitado; qualquer item inválido desabilita todos. A chave não está em nenhum `appsettings` versionado, então **o padrão é o caminho antigo para todas as lojas**, a menos que tenha sido definida no App Service. DOC-35 lista as etapas 5 (concorrência multi-instância) e 6 (contingência de devoluções) como pendentes naquela data. |
| Evidência | `ERP.Application/Fiscal/Recovery/FiscalRecoverySwitch.cs`; `ERP.Api/Extensions/FiscalRecoveryServiceCollectionExtensions.cs:48-49`; `ERP.Api/BackgroundServices/NfeContingencyHostedService.cs:141-146`; `ERP.Api/BackgroundServices/NfeRecoveryHostedService.cs:56-58` (log da partida); busca em `ERP.Api/appsettings*.json`; commits `3a01d52`, `c387dab`, `f33a098`; DOC-35 |
| Gravidade | **A classificar** |
| Impacto | Define se EXC-FIS-007 está ativo em produção. |
| Confiança | — |
| Dependências | — |
| Responsável | Matheus |
| Ação proposta | Nenhuma neste registro. A decisão de habilitar a recuperação nova e a situação das etapas 5 e 6 seguem em EXC-FIS-007. |
| Status | **Encerrado** em 2026-10-22. O responsável verificou nas configurações de aplicativo do App Service `erp-ttsoft-api` (slot de produção) que `FiscalRecovery__TenantsHabilitados` não existe. Pela regra do código (ausente = nenhum tenant), **nenhuma loja usa a recuperação nova; todas usam o caminho antigo**. Coerente com a NFC-e presa com 10.653 tentativas (roteiro 4.6). O slot `staging`, se existir, não foi verificado. |
| Critério de validação | Estado de cada tenant documentado e coerente com o plano da Etapa 4 — atendido: a recuperação está desligada para todos, como previsto ("inerte") até decisão de habilitar. |
| Origem | EXC-F0; INV-022; DOC-35 |
| Relacionados | EXC-FIS-003, EXC-FIS-007 |
| Datas | Registrado em 2026-10-22; atualizado em 2026-10-22 (relatórios anteriores) |

### EXC-FIS-002 — Não existem termos de uso, política de privacidade nem contrato com os clientes

| Campo | Valor |
|---|---|
| Área | FIS (jurídico/LGPD) |
| Natureza | Fato |
| Descrição | O responsável confirmou (2026-10-22) que não existe nenhum termo: nem termos de uso, nem política de privacidade, nem contrato com os clientes. O sistema guarda dados pessoais dos clientes das lojas (inclusive crediário). O Portal tem cadastro público self-service. DOC-25 recomenda contrato de tratamento de dados (DPA) com cada cliente, cláusula de limitação de responsabilidade fiscal revisada por advogado e avaliação da estrutura jurídica da operação. |
| Evidência | Declaração do responsável (2026-10-22); busca no código (2026-10-22); DOC-25 (F) |
| Gravidade | Média com os 2 clientes atuais. **Sobe para Alta** antes de qualquer novo cliente ou do uso do cadastro público. |
| Impacto | Sem base contratual para responsabilidade (inclusive fiscal), pagamento e tratamento de dados pessoais; obrigações da LGPD não formalizadas. |
| Confiança | Alta |
| Dependências | Assessoria jurídica |
| Responsável | Matheus |
| Ação proposta | Na EXC-A9: contratar orientação jurídica para termos de uso, política de privacidade, contrato de licença com DPA e limitação de responsabilidade. Antes disso, não abrir o cadastro público a novos clientes. |
| Status | Confirmado |
| Critério de validação | CRITERIOS FIS-4 e FIS-5. |
| Origem | EXC-F0; INV-008; DOC-25 |
| Relacionados | EXC-SEG-001 |
| Datas | Registrado em 2026-10-22; atualizado em 2026-10-22 (relatórios anteriores) |

### EXC-FIS-003 — Serviços locais de emissão fiscal seguem registrados no WPF

| Campo | Valor |
|---|---|
| Área | FIS (também ENG) |
| Natureza | Hipótese |
| Descrição | O WPF registra `HttpFiscalService` (emissão via API) e, ao mesmo tempo, `NfceEmissionService`, `NfeEmissionService`, `NfeCancellationService`, `NfeStatusService` e `NfeContingencyService` locais. O comentário em `App.xaml.cs:400-404` diz que o `FiscalService` local fica "órfão, até a Etapa 1C confirmar que nada mais precisa dele". Registrado não significa usado. |
| Evidência | `ERP.WPF/App.xaml.cs:400-406` e `:441-446` |
| Gravidade | **A classificar** |
| Impacto | Se os dois caminhos forem usados, o comportamento fiscal pode variar conforme a tela. |
| Confiança | Baixa |
| Dependências | EXC-A1 (mapa dos serviços locais) |
| Responsável | A definir |
| Ação proposta | Na EXC-A4: rastrear os consumidores de cada serviço e confirmar ou descartar. |
| Status | Hipótese |
| Critério de validação | Mapa de uso documentado; hipótese confirmada ou descartada. |
| Origem | EXC-F0; INV-021 |
| Relacionados | EXC-SEG-001, EXC-FIS-004 |
| Datas | Registrado em 2026-10-22 |

### EXC-FIS-004 — WPF lê a configuração fiscal de arquivo local; API lê do banco

| Campo | Valor |
|---|---|
| Área | FIS (também OPS) |
| Natureza | Fato |
| Descrição | A API usa `DatabaseFiscalConfigurationProvider` (configuração por tenant, token protegido). O WPF continua com `JsonFiscalConfigurationProvider`, que lê o token da Focus de arquivo local. DOC-33 e DOC-34 registravam a migração do token do piloto para o banco como pendente. |
| Evidência | `ERP.WPF/App.xaml.cs:390-394` (2026-10-22); DOC-33 §9, DOC-34 |
| Gravidade | Média |
| Impacto | Configuração fiscal em dois lugares por cliente; tokens podem divergir entre caminhos; token em arquivo na máquina da loja. |
| Confiança | Alta |
| Dependências | EXC-FIS-003 (se o caminho local deixar de ser usado, o arquivo pode deixar de ser necessário) |
| Responsável | A definir |
| Ação proposta | Na EXC-A4: verificar quais caminhos ainda leem o arquivo e o estado da migração dos dados. |
| Status | Confirmado |
| Critério de validação | Uma única fonte de configuração fiscal por tenant, ou divergência justificada. |
| Origem | INV-013; DOC-33, DOC-34 |
| Relacionados | EXC-FIS-003 |
| Datas | Registrado em 2026-10-22 |

### EXC-FIS-005 — Nenhum tratamento da Reforma Tributária (IBS/CBS) no código

| Campo | Valor |
|---|---|
| Área | FIS |
| Natureza | Fato (ausência no código) + Lacuna (obrigações e prazos aplicáveis ao produto) |
| Descrição | Nenhuma referência a IBS, CBS ou Reforma Tributária no código. DOC-25, DOC-27 e DOC-30 já apontavam o tema como prioridade. Os prazos e campos exigidos para NF-e/NFC-e dos clientes do produto precisam ser confirmados com contador e com o emissor (Focus NFe). |
| Evidência | Busca por `IBS`, `CBS` e "reforma tribut" nos `.cs` (2026-10-22); DOC-25, DOC-27, DOC-30 |
| Gravidade | **A classificar** — depende das obrigações vigentes para os clientes e de quanto o emissor absorve. |
| Impacto | Rejeição de documentos fiscais ou descumprimento quando as novas regras forem exigidas. |
| Confiança | Alta (ausência no código) |
| Dependências | Contador; documentação da Focus NFe |
| Responsável | A definir |
| Ação proposta | Na EXC-A4/A9: levantar obrigações e cronograma aplicáveis e o que muda no payload enviado à Focus. |
| Status | Confirmado |
| Critério de validação | Plano de adequação aprovado por contador, com datas. |
| Origem | DOC-25, DOC-27, DOC-30 |
| Relacionados | — |
| Datas | Registrado em 2026-10-22 |

### EXC-FIS-006 — SPED EFD ICMS/IPI gerado sem Bloco E

| Campo | Valor |
|---|---|
| Área | FIS |
| Natureza | Fato |
| Descrição | O gerador de EFD ICMS/IPI produz blocos 0, C, H e 9, sem o Bloco E (apuração). DOC-31 observa que um arquivo assim é rejeitado pelo validador se algum cliente precisar entregá-lo, e que optantes do Simples Nacional costumam ser dispensados. |
| Evidência | `ERP.Infrastructure/Services/SpedEfdGenerator.cs` (nenhum registro `E001`, 2026-10-22); DOC-31 (B.1) |
| Gravidade | Baixa (enquanto os clientes forem do Simples Nacional — a confirmar com contador) |
| Impacto | Arquivo inválido para clientes obrigados à EFD ICMS/IPI. |
| Confiança | Alta (ausência do bloco); Média (consequência) |
| Dependências | Contador |
| Responsável | A definir |
| Ação proposta | Na EXC-A4: confirmar o regime dos clientes; decidir entre completar o arquivo ou restringir/avisar na tela. |
| Status | Confirmado |
| Critério de validação | Arquivo aceito pelo validador oficial para o perfil de cliente atendido, ou restrição documentada. |
| Origem | DOC-31 |
| Relacionados | — |
| Datas | Registrado em 2026-10-22 |

### EXC-FIS-007 — Caminho antigo de contingência reenvia NFC-e com `DataEmissao` congelada

| Campo | Valor |
|---|---|
| Área | FIS |
| Natureza | Fato (no código); todas as lojas expostas |
| Situação em produção (2026-10-22) | `FiscalRecovery__TenantsHabilitados` não existe no App Service: todas as lojas estão no caminho antigo (EXC-FIS-001). **Correção:** a NFC-e da Vila Verde presa desde 16/10 tinha sido atribuída a este registro; a consulta E2 mostrou que ela falha por outro motivo (JSON recusado pela Focus) e foi movida para EXC-FIS-009. Não há, até agora, ocorrência confirmada da rejeição por data. |
| Descrição | DOC-35 (3B) diagnosticou que a pendência fiscal guarda o payload com `DataEmissao` do momento da falha e o reenvio não a atualiza. Para NFC-e, a SEFAZ rejeita (704) quando a diferença passa de ~5 minutos, segundo a fonte citada no documento. A Etapa 4 corrige isso no caminho novo (`4A-5a`), que é inerte e ligado por tenant. Para tenants sem o interruptor ligado, o caminho antigo (`NfeContingencyHostedService` → `NfeContingencyService`) continua reenviando o payload original. |
| Evidência | `ERP.Application/Services/NfeContingencyService.cs:50` (payload gravado, sem regeneração da data); `ERP.Api/BackgroundServices/NfeContingencyHostedService.cs:141-146` (decisão por interruptor); commit `f312a77` (correção no caminho novo); DOC-35 |
| Gravidade | Média (rebaixada de Alta em 2026-10-22, após a correção acima). Exposição confirmada para todas as lojas, mas só ocorre quando a Focus fica inalcançável por alguns minutos, e a nota rejeitada pode ser reemitida manualmente. |
| Impacto | Após uma falha real de comunicação, a NFC-e reenviada com a data original tende a ser rejeitada e a venda fica sem documento fiscal até ação manual. |
| Confiança | Alta (código e configuração); Média (efeito, baseado no diagnóstico de DOC-35, não reproduzido) |
| Dependências | — |
| Responsável | A definir |
| Ação proposta | Na EXC-A4: decidir se e quando habilitar a recuperação nova (criar a configuração no App Service é mudança de produção, a planejar e validar antes) e levantar a situação das etapas 5 e 6 de DOC-35. Tratar junto com EXC-FIS-009, que afeta o mesmo caminho. |
| Status | Confirmado |
| Critério de validação | Nenhum tenant em produção exposto, ou correção validada em homologação. |
| Origem | INV-022; DOC-35 |
| Relacionados | EXC-FIS-001, EXC-FIS-009 |
| Datas | Registrado em 2026-10-22; corrigido em 2026-10-22 (consulta E2) |

### EXC-FIS-008 — Tenant de teste emitiu 11 documentos fiscais autorizados em produção, configurado com o CNPJ da Vila Verde

| Campo | Valor |
|---|---|
| Área | FIS (também OPS, SEG) |
| Natureza | Fato (notas autorizadas em produção com o CNPJ da Vila Verde). A confirmar: o emitente das 5 notas sem chave gravada e se alguma corresponde a operação real. |
| Emitente (consulta E8, 2026-10-22) | **6 documentos confirmados com o CNPJ da Vila Verde (12820608000141)** pela chave de acesso: NFC-e nº 3303, 3315, 3318, 3319 e 3322 (24 a 26/09) e NF-e de devolução nº 314 (26/09, referente à venda da NFC-e 3319). As NFC-e usam a **numeração real da loja**. Outras 5 autorizadas não têm chave gravada (17/08 e 23/09, antes da correção de `1a62fee`; e 2 NF-e de 20/10 sem venda vinculada, data dos commits S28 de NF-e única/avulsa); o emitente delas não foi confirmado. As vendas `547696AA…` e `5D5E1AC1…` são citadas em DOC-35 como registros do diagnóstico fiscal, o que indica testes deliberados em produção durante o desenvolvimento. |
| Situação em produção (consulta E1, 2026-10-22) | O tenant de teste tem, em **ambiente de produção**: **7 NFC-e autorizadas** (2026-08-17 a 2026-09-26), **4 NF-e autorizadas** (2026-09-23 a 2026-10-20) e 2 NFC-e em "Contingência" (2026-09-26) que não aparecem em `NfePendentes` (estado divergente: nem autorizadas nem na fila). Em homologação: 1 NFC-e e 1 NF-e autorizadas, 1 NF-e rejeitada. Nas vendas do tenant, só 5 têm chave de NFC-e de produção (contra 7 notas), e `NfceEmitidaEm` está vazio em todas. Nenhuma nota aparece como cancelada no sistema. |
| Descrição | O TenantId `8FB9FC74-BBF7-8418-C6D6-48B7F1EB5466` corresponde ao CNPJ de teste `11222333000181` (o do checklist do Mercado Livre). Em `TenantFiscalConfigurations`, esse tenant tem `Cnpj = 12820608000141` (o da Vila Verde) e `UsarAmbienteProducao = 1`, igual ao tenant real da Vila Verde. Ele registrou **22 vendas nos últimos 30 dias** (a última em 2026-10-20), feitas pela API e pela máquina `DESKTOP-3P3C5VH`. Se essas vendas emitiram NFC-e, foram documentos fiscais **reais**, de produção, em nome da Vila Verde, para vendas que não aconteceram. Não se sabe qual token da Focus essa configuração usa. |
| Evidência | Roteiro 4.1, 4.3, 4.4, 4.5 e E1 (2026-10-22); TenantId calculado a partir do CNPJ com o algoritmo de `CadastroService.CnpjParaTenantId`; `CHECKLIST_REGRESSAO_MERCADO_LIVRE.md` |
| Gravidade | **Crítica.** Documentos fiscais autorizados em produção a partir de vendas de teste, com configuração que aponta para o CNPJ de um cliente. Só deixa de ser Crítica se a consulta E8 e o painel da Focus mostrarem que nenhuma dessas notas saiu com o CNPJ da Vila Verde, ou se o responsável documentar que correspondem a operações reais. |
| Impacto | Notas e obrigações tributárias indevidas para a Vila Verde (ou para outro CNPJ emitente); os prazos normais de cancelamento das notas mais antigas provavelmente já passaram (a confirmar com o contador). |
| Confiança | Alta (6 documentos com o CNPJ da Vila Verde); Média (5 sem chave) |
| Fluxo contábil (declarado em 2026-10-22) | O responsável baixa na Focus todos os XML do CNPJ e os envia ao contador, que não distingue notas de teste das demais. Ou seja, os documentos de teste entram na escrituração da loja como operações reais. O responsável considera aceitável continuar emitindo em produção nos testes. Isso **não** foi registrado como *Risco aceito*: aceitar exige avaliação do contador, porque são documentos fiscais sem operação real, e a decisão afeta a escrituração da loja. |
| Dependências | Painel da Focus NFe; contador |
| Responsável | Matheus |
| Ação proposta | (1) No painel da Focus, listar as requisições da empresa Vila Verde em produção nas datas da E8 (17/08, 23 a 26/09 e 20/10) para identificar as 5 notas sem chave e verificar se alguma foi cancelada direto na Focus. (2) Levar ao contador a lista completa (número, data, valor, situação) para decidir o tratamento. (3) Até isso, não usar o tenant de teste para vendas nem para emissão. (4) Na EXC-A4/A5: isolar testes da produção fiscal (homologação, outro CNPJ ou outro banco — ver EXC-OPS-007). Não alterar dados nem configuração antes da decisão. |
| Status | **Planejado** (triagem de 2026-10-22): ação imediata, sem código — parar de usar o tenant de teste com o CNPJ da Vila Verde e levar ao contador a lista das notas de teste (6 confirmadas e 5 a identificar no painel da Focus). |
| Critério de validação | Nenhum tenant de teste com CNPJ de cliente ou ambiente de produção; notas indevidas, se houver, tratadas e documentadas. |
| Origem | Confirmação 4 (2026-10-22) |
| Relacionados | EXC-OPS-007, EXC-FIS-001, EXC-FIS-004 |
| Datas | Registrado em 2026-10-22; atualizado em 2026-10-22 (consultas E1 e E8) |

### EXC-FIS-009 — Erros definitivos da Focus tratados como falha de comunicação: a nota fica em contingência para sempre

| Campo | Valor |
|---|---|
| Área | FIS (também ENG) |
| Natureza | Fato (no código e em produção) |
| Descrição | `FocusNfeHttpClient.PostAsync` transforma **qualquer** resposta HTTP sem sucesso em falha, e `NfceEmissionService` prefixa a mensagem com "Erro de Comunicação". `FiscalService` e o worker antigo só reconhecem como erro definitivo as mensagens com `UnprocessableEntity` ou `erro_validacao_schema`; todo o resto (400, 401, 403, 415, 5xx de regra) é tratado como falha de comunicação. A venda vai para contingência, o operador vê "a nota será transmitida automaticamente", e o worker reenvia sem limite de tentativas. |
| Situação em produção (consulta E2, 2026-10-22) | NFC-e da Vila Verde, venda `1A7F2760-01DD-4A47-A3FF-BC18B75BAE33`, falha desde 2026-10-16 09:12, **10.661 tentativas** (10.653 algumas horas antes: a fila continua tentando). Última resposta da Focus: `UnsupportedMediaType` com `formato_invalido` — "Erro no JSON … expected ',' or '}' after object value, got: 'EMB' at line 1 column 1473". A Focus está recusando o conteúdo; reenviar o mesmo conteúdo nunca vai funcionar. |
| Causa do JSON inválido | Separada em **EXC-FIS-011**: o item `TRINCHA MEDIA TIGRE 695 4" EMB 12 PC` tem aspas de polegada (E6, E7). |
| Situação na Focus (2026-10-22) | Painel da Focus, empresa Vila Verde, referência `1a7f2760-01dd-4a47-a3ff-bc18b75bae33`, período 10/09 a 23/10: **nenhuma requisição registrada**. A nota nunca foi recebida pela Focus: a venda está sem NFC-e e não há risco de duplicidade num envio futuro. |
| Evidência | `ERP.Infrastructure/HttpClients/FocusNfeHttpClient.cs:38-48`; `ERP.Application/Services/NfceEmissionService.cs:31-32`; `ERP.Infrastructure/Services/FiscalService.cs:113-123`; `ERP.Api/BackgroundServices/NfeContingencyHostedService.cs:320-368`; roteiro 4.6 e E2 |
| Gravidade | **Alta.** Venda real sem NFC-e há 6 dias, sem aviso efetivo ao operador; qualquer outro erro definitivo da Focus tem o mesmo destino. |
| Impacto | Vendas sem documento fiscal; o operador acredita que a nota sairá sozinha; milhares de chamadas inúteis à Focus. |
| Confiança | Alta |
| Dependências | Contador (emissão tardia da venda `1A7F2760…`) |
| Responsável | A definir |
| Ação proposta | (1) Decidir com o contador como regularizar a venda `1A7F2760…` (emitida em 16/10, sem NFC-e). (2) Só então retirar a pendência da fila; a mesma pendência nunca será aceita, porque o payload guardado contém o texto que a Focus recusa. (3) Na EXC-A4: classificar corretamente os erros definitivos, limitar tentativas e avisar o operador. |
| Status | **Planejado** (triagem de 2026-10-22): ação imediata, sem código — decidir com o contador a regularização da venda `1A7F2760…`; a correção da classificação de erros fica para a EXC-A4. |
| Critério de validação | Erros definitivos saem da fila com aviso; tentativas limitadas; venda `1A7F2760…` com situação fiscal resolvida e documentada. |
| Origem | Confirmação 5 (consulta E2, 2026-10-22) |
| Relacionados | EXC-FIS-007, EXC-FIS-001, EXC-FIS-011 |
| Datas | Registrado em 2026-10-22; atualizado em 2026-10-22 (E6, E7, painel da Focus) |

### EXC-FIS-010 — Política de emissão fiscal da loja piloto

| Campo | Valor |
|---|---|
| Área | FIS |
| Natureza | Lacuna |
| Descrição | A consulta E9 levantou a distribuição das vendas da loja piloto por status fiscal. O responsável esclareceu a questão em 2026-10-22. A política de emissão fiscal da loja é do cliente, fora do escopo técnico do produto, e deve ser tratada pelo cliente com o contador. |
| Evidência | Consulta E9 e esclarecimento do responsável (2026-10-22) |
| Gravidade | — (fora do escopo do produto) |
| Impacto | — |
| Confiança | — |
| Dependências | — |
| Responsável | Matheus |
| Ação proposta | Nenhuma no programa. A observação de dados que surgiu nesta consulta (chave da NFC-e ausente na venda) foi movida para EXC-DAD-003. |
| Status | **Encerrado** em 2026-10-22 |
| Critério de validação | Lacuna respondida. |
| Origem | Confirmação 4/5 (2026-10-22) |
| Relacionados | EXC-FIS-008 |
| Datas | Registrado em 2026-10-22; encerrado em 2026-10-22 |

### EXC-FIS-011 — Descrição de produto com aspas (polegada) faz a Focus recusar a NFC-e

| Campo | Valor |
|---|---|
| Área | FIS (também ENG, PRD) |
| Natureza | **Fato:** uma NFC-e real foi recusada exatamente no ponto das aspas. **Hipótese:** o mecanismo é a Focus interpretar mal o escape `"`. |
| Descrição | O item `TRINCHA MEDIA TIGRE 695 4" EMB 12 PC` contém aspas para indicar polegadas. O payload guardado (Newtonsoft) escapa as aspas como `\"`; o envio é feito com `System.Text.Json`, que as escapa como `"`. As duas formas são JSON válido. Mesmo assim, a Focus responde "expected ',' or '}' after object value, got: 'EMB'", isto é, termina o texto nas aspas, o que é compatível com o escape `"` sendo convertido em aspas antes da leitura do JSON no lado da Focus. Acentos também saem escapados (`Ç`); não foi verificado se notas com acentos na descrição foram autorizadas (E11 ajuda a medir). Medidas em polegadas são comuns em material de construção. |
| Evidência | E6 e E7 (2026-10-22); mensagem da Focus em E2; execução local em 2026-10-22: `System.Text.Json` serializa `4" EMB` como `4" EMB`; `ERP.Infrastructure/HttpClients/FocusNfeHttpClient.cs:42`; `ERP.Infrastructure/Services/FiscalService.cs:603` (descrição vem do nome do produto na venda) |
| Alcance (E10, E11, 2026-10-22) | **3 produtos ativos** da Vila Verde têm aspas no nome. Nos últimos 90 dias, **4 vendas** tiveram esses produtos, todas com status "sem nota"; nenhuma com nota emitida. Não há contraexemplo à hipótese, mas também não é prova: as outras 3 vendas podem não ter tentado emitir (ver EXC-FIS-010). |
| Gravidade | Média. Frequência baixa (4 vendas em 90 dias), mas cada ocorrência com tentativa de emissão deixa a venda sem NFC-e e presa em contingência (EXC-FIS-009). |
| Impacto | Vendas com esses 3 produtos não recebem NFC-e. |
| Confiança | Alta (ocorrência e alcance); Média (mecanismo, depende do comportamento da Focus) |
| Dependências | Confirmação com o suporte da Focus |
| Responsável | A definir |
| Ação proposta | Confirmar o comportamento com o suporte da Focus. Na EXC-A4: decidir a correção (forma do escape no envio ou tratamento do caractere na descrição) com teste de regressão usando esse produto. Até lá, renomear os 3 produtos (ex.: `4 POL`) é decisão operacional do responsável: evitaria novas ocorrências, mas não resolve a venda já presa. |
| Status | Confirmado |
| Critério de validação | NFC-e com descrição contendo aspas autorizada em homologação; teste automatizado cobrindo o caso. |
| Origem | Consultas E2, E6, E7, E10, E11 (2026-10-22) |
| Relacionados | EXC-FIS-009, EXC-FIS-010 |
| Datas | Registrado em 2026-10-22; atualizado em 2026-10-22 (E10, E11) |

### EXC-FIS-012 — SPED gerado pelo Portal usa razão social e CNPJ fixos

| Campo | Valor |
|---|---|
| Área | FIS |
| Natureza | Fato |
| Descrição | A tela de relatórios do Portal monta a chamada ao gerador de SPED com `razaoSocial=Vila Verde Materiais` e `cnpj=11222333000181` escritos no código, para qualquer tenant. O endpoint aceita os dois valores da requisição sem conferir com o cadastro do tenant e gera os blocos C e H vazios (`Array.Empty`). O arquivo baixado pelo Portal identifica sempre a mesma empresa, com um CNPJ que não é o da loja. Encontrado de passagem na avaliação de marketing. |
| Evidência | `ERP.Portal/Pages/Relatorios.razor:344`; `ERP.Api/Controllers/FiscalController.cs:99-125` (2026-10-26) |
| Gravidade | Média (arquivo fiscal com identificação errada; o validador oficial deve rejeitar, mas um usuário pode entregá-lo ao contador como se fosse da loja) |
| Impacto | Qualquer loja que gere o SPED pelo Portal recebe um arquivo de outra empresa e sem movimento. |
| Confiança | Alta |
| Dependências | Contador (se algum arquivo já foi entregue) |
| Responsável | A definir |
| Ação proposta | Na EXC-A4, junto com EXC-FIS-006: ler razão social, CNPJ, IE e município do cadastro do tenant no servidor e decidir se o SPED do Portal fica disponível enquanto os blocos estiverem vazios. Perguntar ao contador da Vila Verde se algum arquivo do Portal foi usado. |
| Status | Confirmado |
| Critério de validação | Arquivo gerado pelo Portal com os dados do tenant autenticado, sem parâmetro de empresa vindo do navegador; teste automatizado cobrindo o caso. |
| Origem | EXC-A10 (parte MKT) |
| Relacionados | EXC-FIS-006, EXC-MKT-002 |
| Datas | Registrado em 2026-10-26 |

### EXC-FIS-013 — CNPJ do emitente fixo no envio de notas à Focus: toda nota sai com o CNPJ da Vila Verde

| Campo | Valor |
|---|---|
| Área | FIS (também SEG) |
| Natureza | Fato (código) + Hipótese (efeito na Focus) |
| Descrição | `FocusNfceRequest.CnpjEmitente` tem como valor padrão `12820608000141`, o CNPJ da Vila Verde, e nenhum dos três montadores de requisição do `FiscalService` (NFC-e, NF-e A4 e NF-e de devolução) o substitui pelo CNPJ do tenant. Toda nota enviada à Focus leva `cnpj_emitente` da Vila Verde, seja qual for a loja. Hoje só a Vila Verde emite, então não há dano conhecido. Com uma segunda loja, há dois efeitos possíveis, não verificados: a Focus recusa a nota (token de outra empresa) ou, se o token der acesso a mais de um CNPJ da conta da TTSoft, a nota é autorizada **em nome da Vila Verde**. **Hipótese a conferir:** pode ser a causa, ou parte da causa, das notas do tenant de teste emitidas com o CNPJ da Vila Verde (EXC-FIS-008). Encontrado de passagem na correção do EXC-MKT-002. |
| Evidência | `ERP.Application/DTOs/FocusNfe/FocusNfceRequest.cs:10`; `ERP.Infrastructure/Services/FiscalService.cs:626`, `:868` e `MontarRequestNfeA4` (busca por `CnpjEmitente` sem atribuição em 2026-10-26) |
| Gravidade | Alta (impede ou desvia a emissão fiscal de qualquer loja além da Vila Verde) |
| Impacto | Bloqueia a implantação de uma segunda loja; no pior caso, notas de uma loja emitidas com o CNPJ de outra. |
| Confiança | Alta (código); Baixa (comportamento da Focus) |
| Dependências | Documentação da Focus sobre `cnpj_emitente` e tokens por empresa; configuração da conta Focus da TTSoft |
| Responsável | A definir |
| Ação proposta | Na EXC-A4, antes de qualquer segunda loja emitir: preencher `CnpjEmitente` com o CNPJ da configuração fiscal do tenant (`TenantFiscalConfiguration`) e tirar o valor padrão; conferir no painel da Focus se as notas do tenant de teste usaram este padrão. |
| Status | Confirmado |
| Critério de validação | Teste automatizado: requisição montada para um tenant com CNPJ diferente leva o CNPJ desse tenant; nenhum CNPJ fixo em `FocusNfceRequest`. |
| Origem | Correção do EXC-MKT-002 (2026-10-26) |
| Relacionados | EXC-FIS-008, EXC-FIS-012, EXC-FIS-004 |
| Datas | Registrado em 2026-10-26 |

---

## Detalhe — Produto (PRD)

### EXC-PRD-001 — Roadmap exibido no Portal não corresponde ao produto atual

| Campo | Valor |
|---|---|
| Área | PRD |
| Natureza | Fato |
| Descrição | `Roadmap.razor` lista como "planejado" ou "futuro" itens que já existem no código (entrada de NF-e e MD-e, transferências, Pix automático, fidelidade, marketplace, e-commerce, dashboard de TV) e o Mobile como "em desenvolvimento", embora o projeto tenha uma página, sem alterações desde maio/2026, e seja removido do build do CI. DOC-33 recomendava não investir no Mobile agora. DOC-32 lista o que não deve ser prometido em demonstração (offline, TEF, app mobile); offline mudou desde então (INVENTARIO §4.1), TEF foi removido. |
| Evidência | `ERP.Portal/Pages/Roadmap.razor:170-296`; `ERP.Mobile/`; `.github/workflows/ci-cd.yml`; DOC-32, DOC-33 |
| Gravidade | Baixa |
| Impacto | Comunicação ao cliente inconsistente com o produto. |
| Confiança | Alta |
| Escopo do Mobile (2026-10-22) | O responsável decidiu pausar o app Mobile: está nos planos, mas o desenvolvimento para iOS exige um Mac, que ele não tem. |
| Dependências | — |
| Responsável | Matheus |
| Ação proposta | Na EXC-A7: atualizar o roadmap do Portal para refletir o que está entregue e o Mobile como pausado, e definir a fonte do roadmap. |
| Status | Confirmado |
| Critério de validação | CRITERIOS PRD-2 e PRD-4. |
| Origem | EXC-F0; DOC-32, DOC-33 |
| Relacionados | EXC-OPS-004 |
| Datas | Registrado em 2026-10-22 |

---

## Detalhe — Dados e governança (DAD)

### EXC-DAD-001 — Parte dos relatórios anteriores não foi localizada

| Campo | Valor |
|---|---|
| Área | DAD (governança) |
| Natureza | Lacuna |
| Descrição | Os 35 relatórios entregues em 2026-10-22 cobrem a maior parte da história. Faltam os citados no INVENTARIO §2.3 (6ª, 7ª e 14ª auditorias, a auditoria com nota 9,5, a parte 1 da auditoria do PDV, a "Análise Crítica v2" e a auditoria de 10/08). Como a série de segurança é encadeada, o risco de pendência perdida é baixo. Os relatórios continuam fora do repositório. |
| Evidência | INVENTARIO §2 |
| Gravidade | Baixa (era Média antes da entrega dos relatórios) |
| Impacto | Pequeno risco de pendência antiga sem destino. |
| Confiança | Alta |
| Dependências | — |
| Responsável | Matheus |
| Ação proposta | Localizar os relatórios faltantes, se existirem; decidir onde os relatórios devem ser guardados (repositório, pasta compartilhada). |
| Status | Lacuna |
| Critério de validação | Relatórios faltantes localizados ou declarados inexistentes; local de guarda definido. |
| Origem | EXC-F0 |
| Relacionados | — |
| Datas | Registrado em 2026-10-22; atualizado em 2026-10-22 (entrega dos relatórios) |

### EXC-DAD-002 — Sem concorrência otimista; créditos de Haver por leitura-e-gravação

| Campo | Valor |
|---|---|
| Área | DAD (também ENG) |
| Natureza | Fato (padrão no código) + Hipótese (perda em uso real) |
| Descrição | Nenhuma entidade tem controle de concorrência otimista (`RowVersion`/`IsConcurrencyToken`). O débito de Haver e a baixa de contas a receber já usam UPDATE relativo. Os créditos de Haver no cancelamento de venda e na devolução ainda leem o saldo, somam em memória e gravam. Duas operações simultâneas sobre o mesmo cliente podem perder uma atualização. |
| Evidência | Nenhum `RowVersion` em Domain/Persistence (2026-10-22); `ERP.Application/Services/SaleService.cs:338` (`CancelAsync`); `ERP.Application/Services/DevolucaoService.cs:107` (`DevolverItensAsync`); débito atômico em `ERP.Infrastructure/Repositories/SpecificRepositories.cs:165`; DOC-31 (D), DOC-33 §8 |
| Gravidade | Média (dinheiro; exige operações simultâneas no mesmo cliente) |
| Impacto | Saldo de Haver incorreto em concorrência. |
| Confiança | Alta (código); Baixa (frequência real com poucos terminais) |
| Dependências | — |
| Responsável | A definir |
| Ação proposta | Na EXC-A3: mapear todos os read-modify-write de valores monetários e de estoque; priorizar pelo risco. |
| Status | Confirmado |
| Critério de validação | Operações monetárias atômicas ou protegidas por concorrência, com teste de concorrência. |
| Origem | INV-012; DOC-31, DOC-33 |
| Relacionados | EXC-ENG-004 |
| Datas | Registrado em 2026-10-22 |

### EXC-DAD-003 — Registros com `TenantId` vazio e filial matriz duplicada na Vila Verde

| Campo | Valor |
|---|---|
| Área | DAD (também SEG) |
| Natureza | Fato |
| Descrição | (1) `AuditLogs` tem 2 eventos com `TenantId` `00000000-0000-0000-0000-000000000000`, gravados pela API (host `dd6460df80f2`) em 2026-09-23. É o tipo de defeito que DOC-30 encontrou (gravação sem tenant); a entidade afetada não foi identificada. (2) A Vila Verde tem duas filiais "Matriz (Loja Principal)" ativas, ambas sem CNPJ. (3) Quase todas as vendas com nota emitida não têm `NfceChave` gravada na própria venda; a chave existe só em `NotasFiscais` (consultas E8 e E9). |
| Evidência | Roteiro 4.2 e 4.4 (2026-10-22) |
| Gravidade | Baixa (2 eventos antigos; duplicidade sem efeito conhecido) |
| Impacto | Registros sem dono de tenant ficam invisíveis para todos; filial duplicada pode dividir estoque por filial ou relatórios. |
| Confiança | Alta |
| Dependências | Consultas E4 e E5 |
| Responsável | A definir |
| Ação proposta | Na EXC-A1: identificar a entidade dos eventos sem tenant e procurar outras tabelas com `TenantId` vazio. Na EXC-A3: verificar se a matriz duplicada afeta estoque por filial e decidir se `Sale.NfceChave` continua existindo ou se `NotasFiscais` é a fonte única. |
| Status | Confirmado |
| Critério de validação | Origem dos registros sem tenant explicada; nenhum registro novo sem tenant; uma matriz por loja. |
| Origem | Confirmação 4 (2026-10-22) |
| Relacionados | EXC-ENG-001 |
| Datas | Registrado em 2026-10-22 |

---

## Detalhe — Administração (ADM) e Vendas (VEN)

### EXC-ADM-001 — Receita mensal declarada não cobre os custos de infraestrutura declarados

| Campo | Valor |
|---|---|
| Área | ADM |
| Natureza | Fato (números declarados pelo responsável em 2026-10-22) |
| Descrição | **Custos:** Azure (2 bancos) e Focus NFe (plano para até 3 CNPJs), R$ 183,90/mês no total declarado; não foi detalhado se inclui o App Service e o armazenamento. **Receita:** R$ 89,90/mês da Vila Verde e R$ 30,00/mês do cliente do OutfiTT, que é outro produto e divide a mesma infraestrutura. Total R$ 119,90/mês, **R$ 64,00/mês abaixo dos custos declarados**, sem contar o tempo do responsável. Os dois preços são condições especiais (ver EXC-VEN-001). DOC-32 observa que a Focus cobra por documento acima do plano. |
| Evidência | Declaração do responsável (2026-10-22); DOC-25 (F), DOC-32 (E), DOC-33 §15 |
| Gravidade | Média (sustentabilidade financeira do produto; não afeta dados nem segurança) |
| Impacto | A operação depende de aporte do responsável; crescer sem rever preços aumenta o déficit. |
| Confiança | Média (valores declarados, sem faturas) |
| Dependências | Faturas do Azure e da Focus |
| Responsável | Matheus |
| Ação proposta | Na EXC-A10: conferir as faturas (inclusive App Service), separar o custo por produto (ConstruTTor e OutfiTT) e estimar o custo por cliente adicional. Não é tarefa técnica. |
| Status | Confirmado |
| Critério de validação | CRITERIOS ADM-1. |
| Origem | DOC-25, DOC-32, DOC-33; confirmação 11 |
| Relacionados | EXC-VEN-001, EXC-ADM-002 |
| Datas | Registrado em 2026-10-22; atualizado em 2026-10-22 (confirmação 11) |

### EXC-ADM-002 — Todo o conhecimento e a operação concentrados em uma pessoa

| Campo | Valor |
|---|---|
| Área | ADM (também OPS, SUP) |
| Natureza | Fato |
| Descrição | O responsável é a única pessoa da empresa (2026-10-22): desenvolvimento, implantação, suporte, infraestrutura, fiscal e comercial. Parte do conhecimento não está versionada (código da API de licenças só no PC, procedimento real de publicação, configurações do Azure). DOC-25 e DOC-31 já apontavam bus factor 1. |
| Evidência | Declaração do responsável (2026-10-22); EXC-OPS-005, EXC-OPS-001; DOC-25 (F), DOC-31 (E) |
| Gravidade | Média |
| Impacto | Uma ausência prolongada do responsável interrompe suporte, correções e a operação fiscal dos clientes; sem revisão por outra pessoa, erros passam direto para produção (EXC-OPS-002). |
| Confiança | Alta |
| Dependências | — |
| Responsável | Matheus |
| Ação proposta | Reduzir o que depende só da memória do responsável: versionar o que está só no PC, documentar publicação e recuperação (EXC-A5). A decisão de ter outra pessoa de apoio é de negócio (EXC-A10). |
| Status | Confirmado |
| Critério de validação | Código, configuração e procedimentos críticos versionados e documentados de forma que outra pessoa consiga operar. |
| Origem | Confirmação 9 (2026-10-22) |
| Relacionados | EXC-OPS-005, EXC-OPS-001, EXC-OPS-002, EXC-ADM-001 |
| Datas | Registrado em 2026-10-22 |

### EXC-VEN-001 — Preços e planos sem validação com clientes

| Campo | Valor |
|---|---|
| Área | VEN |
| Natureza | Lacuna |
| Descrição | DOC-32 propõe planos e faixas de preço, todas marcadas pelo próprio documento como **[estimativa]**, e recomenda validá-las nas primeiras conversas com clientes. Preços praticados hoje (declarados em 2026-10-22): Vila Verde **R$ 89,90/mês**, preço reduzido porque a loja recebe primeiro todas as atualizações do ConstruTTor (DOC-34, de agosto, citava R$ 99,90/mês e R$ 2.000 de implantação); o cliente do OutfiTT paga R$ 30,00/mês em condição especial de primeiro cliente. Nenhum preço foi testado com um cliente sem condição especial. Existe um mecanismo de módulos por tenant (`TenantFeatureFlags`), mas não foi avaliado como controle de plano. |
| Evidência | DOC-32 (E), DOC-34; `ERP.Domain/Entities/TenantFeatureFlags.cs`; declaração do responsável (2026-10-22) |
| Gravidade | **A classificar** |
| Impacto | Sem preço validado, não há base para projeção comercial. |
| Confiança | — |
| Dependências | Conversas com clientes; EXC-ADM-001 |
| Responsável | Matheus |
| Ação proposta | Na EXC-A10: registrar as propostas enviadas (ex.: Eldorado, DOC-29), definir como validar preço com clientes sem condição especial e cruzar com o custo (EXC-ADM-001). |
| Status | Lacuna |
| Critério de validação | CRITERIOS VEN-2. |
| Origem | DOC-32, DOC-34; confirmação 11 |
| Relacionados | EXC-ADM-001, EXC-ADM-003, EXC-VEN-004; estrutura de plano recomendada (plano único com franquia e excedente de notas, PC adicional e implantação) em [EXC-A10-VENDAS](EXC-A10-VENDAS.md) §9.2, como hipótese a testar no experimento X5 |
| Datas | Registrado em 2026-10-22; atualizado em 2026-10-22 (confirmação 11) e em 2026-10-26 (avaliação de vendas) |

### EXC-ADM-003 — Margem por cliente depende do volume de notas fiscais, que não está medido

| Campo | Valor |
|---|---|
| Área | ADM (também FIS) |
| Natureza | **Hipótese** (cálculo sobre tabela pública e premissas de volume; nenhum dado real de volume) |
| Descrição | Pela tabela pública da Focus NFe (2026-10-26), o custo por nota e por CNPJ é o principal custo variável por cliente. Com premissas de 100, 500 e 1.500 NFC-e por loja/mês e preço de R$ 89,90, a margem por cliente é positiva com 100 e 500, e **negativa com 1.500 em todas as escalas de 10 a 300 clientes** (preço de equilíbrio de R$ 108 a R$ 134 conforme o imposto). Vale **se** a TTSoft pagar a Focus pelas notas dos clientes, como no custo declarado hoje. O volume real da Vila Verde não consta em nenhum documento do programa. |
| Evidência | [EXC-A10-ADMINISTRATIVO-FINANCEIRO](EXC-A10-ADMINISTRATIVO-FINANCEIRO.md) §6; `EXC-ADM-modelo-cenarios.py` (execução em 2026-10-26); tabela pública https://focusnfe.com.br/precos/ (consulta em 2026-10-26); declaração do responsável (2026-10-22) |
| Gravidade | A classificar. Se o volume real for alto e o contrato não repassar o excedente: Média a Alta (cada loja nova aumenta o prejuízo). Se o volume for baixo ou o excedente for repassado: Baixa |
| Impacto | Crescer sem rever preço ou repasse pode piorar o resultado em vez de melhorá-lo. |
| Confiança | Baixa (premissas de volume e contrato Focus não verificados) |
| Dependências | Contagem de notas por loja (consulta da §8 do relatório); contrato e fatura da Focus |
| Responsável | Matheus |
| Ação proposta | Rodar a consulta de volume fiscal e conferir o contrato da Focus (franquia, excedente, revenda). Depois, decidir se o excedente é repassado. Não é tarefa técnica. |
| Status | Hipótese |
| Critério de validação | CRITERIOS ADM-1, ADM-2 e VEN-2: volume real por loja e custo real por nota documentados, e o modelo refeito com eles. |
| Origem | EXC-A10 (parte ADM) |
| Relacionados | EXC-ADM-001, EXC-VEN-001, EXC-FIS-008 (separar notas de teste do volume real) |
| Datas | Registrado em 2026-10-26 |

### EXC-ADM-004 — Dados administrativos e tributários da TTSoft ausentes

| Campo | Valor |
|---|---|
| Área | ADM (também FIS) |
| Natureza | Lacuna |
| Descrição | Não constam em nenhum documento do programa: forma jurídica e CNPJ da TTSoft, regime tributário e alíquota efetiva, forma de cobrança da mensalidade e histórico de inadimplência, custos fixos fora da nuvem (domínio, assinatura de código, ferramentas, contador), horas gastas por tipo de trabalho e fornecedores críticos com plano de contingência. O código não tem entidade de plano, assinatura ou cobrança recorrente. |
| Evidência | Busca no repositório e nos documentos do programa (2026-10-26); [EXC-A10-ADMINISTRATIVO-FINANCEIRO](EXC-A10-ADMINISTRATIVO-FINANCEIRO.md) §4.1, §5 e §8 |
| Gravidade | A classificar. Depende do que o contador e as faturas mostrarem (ex.: teto de receita do enquadramento, que pode ser ultrapassado a partir de ~75 clientes a R$ 89,90 se o regime for MEI) |
| Impacto | Sem esses dados não há custo por cliente, ponto de equilíbrio nem previsão de tributo confiáveis. |
| Confiança | — |
| Dependências | Contador; faturas; extrato de recebimentos |
| Responsável | Matheus |
| Ação proposta | Seguir a lista de coleta da §8 do relatório (itens 4, 5, 8 e 9). |
| Status | Lacuna |
| Critério de validação | CRITERIOS ADM-1 a ADM-4. |
| Origem | EXC-A10 (parte ADM) |
| Relacionados | EXC-ADM-001, EXC-ADM-002, EXC-FIS-002, EXC-FIS-005 |
| Datas | Registrado em 2026-10-26 |

### EXC-ADM-005 — Asaas usa uma única chave da aplicação; uso em produção desconhecido

| Campo | Valor |
|---|---|
| Área | ADM (também FIS, SEG) |
| Natureza | **Hipótese** |
| Descrição | `AsaasService` lê `Asaas:ApiKey` e `Asaas:Environment` da configuração da aplicação, não de um dado por tenant. Se `Asaas__Environment=production` estiver no App Service e algum cliente gerar boleto pela API, o boleto sairia na conta Asaas da TTSoft, com a TTSoft como recebedora do valor devido à loja. O padrão do código é `sandbox`. Não foi verificado se há uso em produção. |
| Evidência | `ERP.Infrastructure/Services/AsaasService.cs:23-24`; `ERP.Api/Program.cs:121`; `ERP.Api/Controllers/ContasReceberController.cs:154-172`; `ERP.Api/appsettings.json` (`Asaas.Environment` = `sandbox`) |
| Gravidade | A classificar. Sem uso em produção: Informativa. Com uso: pode ser Alta (dinheiro de terceiros na conta da TTSoft, obrigação tributária e de intermediação) |
| Impacto | Efeito financeiro, fiscal e contratual sobre recebimentos das lojas. |
| Confiança | Baixa (só leitura de código) |
| Dependências | Configuração do App Service; painel do Asaas; contador |
| Responsável | Matheus |
| Ação proposta | Conferir a configuração de produção e o histórico de cobranças no painel do Asaas. Se houver uso, tratar com o contador antes de qualquer mudança de código. |
| Status | Hipótese |
| Critério de validação | Configuração de produção registrada; e, se houver uso, decisão documentada sobre quem é o recebedor. |
| Origem | EXC-A10 (parte ADM) |
| Relacionados | EXC-ADM-004, EXC-FIS-002 |
| Datas | Registrado em 2026-10-26 |

### EXC-VEN-002 — Cadastro público "Criar conta gratuita" cria loja no banco de produção sem plano, prazo, termos nem contrato

| Campo | Valor |
|---|---|
| Área | VEN (também FIS, SEG) |
| Natureza | Fato (código); presença em produção não verificada |
| Descrição | A tela de login do Portal oferece "Criar conta gratuita". O cadastro cria tenant, cargos, permissões e usuário `admin` no banco compartilhado, para qualquer CNPJ ativo na Receita. Não há plano, prazo de teste, limite de uso, aceite de termos nem contrato; o e-mail de boas-vindas não indica próximo passo comercial. As proteções contra uso do CNPJ de outra empresa são boas (confirmação pelo e-mail da Receita quando o e-mail diverge, 3 tentativas por hora por IP, resposta igual para CNPJ novo e existente). O PDV continua exigindo licença da API externa, então a conta gratuita dá acesso ao Portal, não ao caixa. Em 2026-10-22 não havia indício de cadastros espontâneos (1 loja real e 4 tenants de teste). |
| Evidência | `ERP.Portal/Pages/Login.razor:43-48`; `ERP.Api/Controllers/CadastroController.cs:29-80`; `ERP.Infrastructure/Services/CadastroService.cs:39-211`; confirmação 4 (2026-10-22) |
| Gravidade | Média (promessa comercial sem regra; dados de terceiros entram no banco de produção sem termos nem política de privacidade, EXC-FIS-002; suporte não planejado para uma pessoa) |
| Impacto | Quem se cadastra espera um teste gratuito sem condições conhecidas; a TTSoft passa a tratar dados de uma empresa sem contrato; contas sem dono se acumulam no banco compartilhado. |
| Confiança | Alta (código). Não verificado se a página está publicada em `app.ttsofts.com.br/cadastro` nem se houve cadastros depois de 2026-10-22 |
| Dependências | Decisão sobre o período de teste ([EXC-A10-VENDAS](EXC-A10-VENDAS.md) §10.2) |
| Responsável | Matheus |
| Ação proposta | Decidir entre retirar o link, trocá-lo por "Fale conosco" ou manter com regras (prazo, aceite de termos, aviso ao responsável). Enquanto existir, ligar para cada cadastro em até 2 dias úteis. Consultar se há tenants criados pelo cadastro depois de 2026-10-22. |
| Status | Confirmado |
| Critério de validação | Decisão registrada; se mantido, termos aceitos no cadastro e prazo do teste aplicado; consulta de tenants criados pelo cadastro registrada. |
| Origem | EXC-A10 (parte VEN) |
| Relacionados | EXC-FIS-002, EXC-OPS-007, EXC-VEN-003, EXC-MKT-002 (marca "TTSoft ERP" no cadastro e no e-mail) |
| Datas | Registrado em 2026-10-26 |

### EXC-VEN-003 — Implantação de loja nova depende de passos técnicos manuais e não documentados

| Campo | Valor |
|---|---|
| Área | VEN (também OPS, SUP) |
| Natureza | Fato |
| Descrição | Implantar uma loja exige passos que só o responsável sabe fazer e que não estão escritos: cadastrar a licença na API de licenças (código só no PC dele), configurar o fiscal do tenant, editar o `licenca.json` com o CNPJ em cada PC, digitar servidor, banco, usuário e senha do SQL na tela de primeiro uso, configurar o `config_recibo.json`. O único roteiro versionado é o checklist de deploy, desatualizado e com dados da Vila Verde. Há bom apoio para os dados: importador CSV de produtos e clientes com saldo de fiado e haver (o fiado entra como uma conta por cliente, sem os vencimentos originais; nenhum teste automatizado encontrado). Hipótese a conferir: o cadastro do Portal cria 26 permissões e o *seeder* do PDV conhece 28 (faltam `importacao.view` e `legacyimport.view`); o *seeder* parece completar ao abrir o PDV. |
| Evidência | `ERP.WPF/ViewModels/SetupViewModel.cs:18-45,131-135`; `ERP.WPF/Services/TenantService.cs:28-40`; `ERP.WPF/Security/LicenseManager.cs:13-29`; `Scripts/CHECKLIST_DEPLOY.md` §1-2; `ERP.Infrastructure/Services/ImportacaoService.cs:41-62,347-352` (commit `1980593`); `ERP.Persistence/Context/DbSeeder.cs:249-250` |
| Gravidade | Média (não afeta dados; bloqueia crescimento e concentra a operação em uma pessoa) |
| Impacto | Cada implantação depende da presença e da memória do responsável; não há como medir nem delegar; CRITERIOS VEN-3 não atendido. |
| Confiança | Alta |
| Dependências | EXC-SEG-001 (credencial no primeiro uso), EXC-OPS-005 (API de licenças), EXC-OPS-001 (checklist), EXC-FIS-013 (emitente por loja) |
| Responsável | Matheus |
| Ação proposta | Escrever o roteiro de implantação genérico ([EXC-A10-VENDAS](EXC-A10-VENDAS.md) §11.2) e registrar horas por atividade na próxima implantação; os passos técnicos somem com as correções das dependências. |
| Status | Confirmado |
| Critério de validação | CRITERIOS VEN-3: uma implantação feita seguindo só o roteiro escrito, com os critérios de §11.3 atendidos e as horas registradas. |
| Origem | EXC-A10 (parte VEN) |
| Relacionados | EXC-ADM-002, EXC-SEG-001, EXC-OPS-001, EXC-OPS-005, EXC-FIS-013 |
| Datas | Registrado em 2026-10-26 |

### EXC-VEN-004 — Não existe processo comercial documentado nem registro de propostas

| Campo | Valor |
|---|---|
| Área | VEN |
| Natureza | Lacuna |
| Descrição | Não há etapas, critérios de passagem, roteiro de demonstração, modelo de proposta, termo de piloto, ritmo de acompanhamento nem regra de renovação. A situação da proposta à Eldorado (DOC-29) é desconhecida. A licença do PDV tem vencimento e aviso, mas a renovação não tem regra comercial (reajuste, aviso, exportação de dados no cancelamento). |
| Evidência | Busca nos documentos do programa e no repositório (2026-10-26); [EXC-A10-VENDAS](EXC-A10-VENDAS.md) §3 |
| Gravidade | A classificar |
| Impacto | Sem processo e registro, não há funil, taxa de ganho nem motivo de perda para decidir preço, canal e produto. |
| Confiança | — |
| Dependências | EXC-FIS-002 (contrato), EXC-VEN-001 (preço) |
| Responsável | Matheus |
| Ação proposta | Aprovar o processo E1 a E11 ([EXC-A10-VENDAS](EXC-A10-VENDAS.md) §4) e começar a planilha de funil (§13.3) com os contatos existentes, incluindo a Eldorado. |
| Status | Lacuna |
| Critério de validação | CRITERIOS VEN-1: processo aprovado e pelo menos um ciclo completo (do contato à decisão) registrado com datas por etapa. |
| Origem | EXC-A10 (parte VEN) |
| Relacionados | EXC-VEN-001, EXC-MKT-004, EXC-FIS-002 |
| Datas | Registrado em 2026-10-26 |

---

## Detalhe — Marketing (MKT)

### EXC-MKT-001 — Público-alvo e proposta de valor sem validação com clientes

| Campo | Valor |
|---|---|
| Área | MKT (também VEN) |
| Natureza | Lacuna |
| Descrição | Não há entrevista estruturada com lojistas registrada, nem dado de quais módulos a Vila Verde usa. O perfil de cliente ideal, as dores e o posicionamento da avaliação de marketing foram construídos a partir das restrições do produto e da empresa e de pesquisa pública, e estão marcados como hipótese. DOC-25 e DOC-32 propunham posicionamento e canais sem validação. |
| Evidência | [EXC-A10-MARKETING](EXC-A10-MARKETING.md) §4, §5 e §8; busca nos documentos do programa (2026-10-26); DOC-25 (E), DOC-32 (E) |
| Gravidade | A classificar |
| Impacto | Esforço de aquisição pode ir para o público ou a mensagem errados; com uma pessoa, o custo de errar é o tempo de suporte e desenvolvimento. |
| Confiança | — |
| Dependências | Conversa com a Vila Verde; entrevistas X1 |
| Responsável | Matheus |
| Ação proposta | Coletar os dados 1 a 3 da §14 do relatório (conversa com a Vila Verde, consulta de uso por módulo, 10 entrevistas). |
| Status | Lacuna |
| Critério de validação | CRITERIOS MKT-1: pelo menos 5 lojas do perfil confirmando a dor principal e a disposição de pagar, registradas. |
| Origem | EXC-A10 (parte MKT) |
| Relacionados | EXC-VEN-001, EXC-MKT-004 |
| Datas | Registrado em 2026-10-26 |

### EXC-MKT-002 — Nome da loja piloto fixo na interface do WPF

| Campo | Valor |
|---|---|
| Área | MKT (também PRD) |
| Natureza | Fato |
| Descrição | O texto "Vila Verde" aparecia em 8 títulos de mensagem e em uma dica de tela do WPF que qualquer loja veria, incluindo o aviso de modo offline e o fechamento de caixa. Na correção, o teste automatizado achou mais 4 ocorrências em maiúsculas, que chegam ao **cliente final** da loja: o cabeçalho do comprovante de pagamento, o texto de venda e o de orçamento enviados por WhatsApp, e a tela `ReciboPreviewView` (sem uso no código), que tinha também o CNPJ da Vila Verde fixo. O produto também aparece com dois outros nomes: "ConstruTTor ERP" no login do WPF e "TTSoft ERP — Portal Gerencial" no Portal (não alterado: depende da decisão de marca). |
| Evidência | `ERP.WPF/ViewModels/FinalizarVendaViewModel.cs:64,418,449,671,681`; `ERP.WPF/ViewModels/ResumoCaixaViewModel.cs:242`; `ERP.WPF/ViewModels/ContaPagarViewModel.cs:112`; `ERP.WPF/ViewModels/UserViewModel.cs:87`; `ERP.WPF/Views/NfeImportView.xaml:100`; `ERP.WPF/Views/LoginView.xaml:4`; `ERP.Portal/wwwroot/index.html:6` (2026-10-26) |
| Gravidade | Baixa (não afeta dados; afeta a credibilidade numa demonstração ou numa segunda loja) |
| Impacto | Uma segunda loja vê o nome de outra loja no próprio caixa. |
| Confiança | Alta |
| Dependências | Decisão sobre a marca do produto |
| Responsável | A definir |
| Ação proposta | **Feita em 2026-10-26:** títulos de mensagem e dica de tela passaram a "ConstruTTor"; comprovante e textos de WhatsApp usam o `NomeFantasia` do `config_recibo.json` da loja, como os recibos já usavam; `ReciboPreviewView` ficou com textos de exemplo. Na Vila Verde, o cabeçalho do WhatsApp muda de "VILA VERDE MATERIAIS DE CONSTRUÇÃO" para o nome fantasia configurado. Falta publicar a versão do WPF. O texto fixo do SPED no Portal segue em EXC-FIS-012. |
| Status | Em validação |
| Critério de validação | (1) Teste `ERP.Tests/WPF/TextosInterfaceTests` aprovado: nenhum literal de string nos `.cs` nem texto fora de comentário nos `.xaml` do ERP.WPF contém "Vila Verde" ou o CNPJ da Vila Verde. **Atendido em 2026-10-26** (falhava antes da correção; suíte completa: 1337 aprovados, 0 falhas). (2) Versão publicada e um comprovante de pagamento conferido num PC da Vila Verde com o nome fantasia correto. |
| Origem | EXC-A10 (parte MKT) |
| Relacionados | EXC-FIS-012, EXC-FIS-013; CRITERIOS MKT-2 |
| Datas | Registrado em 2026-10-26; atualizado em 2026-10-26 (correção no código) |

### EXC-MKT-003 — Relatórios anteriores recomendam afirmações de divulgação que o estado atual não sustenta

| Campo | Valor |
|---|---|
| Área | MKT |
| Natureza | Fato |
| Descrição | DOC-25 recomenda usar "transparência técnica" e as rodadas de teste de segurança como argumento de venda e de conteúdo, e cita "offline nativo" como resposta a objeção. Com EXC-SEG-001 e EXC-SEG-009 abertos como Críticos, sem termos nem política de privacidade (EXC-FIS-002) e com a contingência fiscal com defeitos abertos (EXC-FIS-007, EXC-FIS-009), essas afirmações seriam enganosas hoje. A avaliação de marketing lista as afirmações proibidas e a condição para cada uma voltar a ser usada. |
| Evidência | DOC-25 (D, E); [EXC-A10-MARKETING](EXC-A10-MARKETING.md) §6.2 |
| Gravidade | Média (risco de publicidade enganosa e de perda de confiança se usadas) |
| Impacto | Uma afirmação desmentida por um incidente ou por um cliente atrasa a aquisição mais do que qualquer canal ajuda. |
| Confiança | Alta |
| Dependências | EXC-SEG-001, EXC-SEG-009, EXC-FIS-002, EXC-FIS-009 |
| Responsável | Matheus |
| Ação proposta | Usar em material só o que estiver como "Sim" na coluna "Pode usar hoje?" da §6.1 do relatório; revisar a lista a cada registro relacionado encerrado. |
| Status | Confirmado |
| Critério de validação | CRITERIOS MKT-2: cada afirmação de material publicado tem evidência de código e de uso ou teste real registrada. |
| Origem | EXC-A10 (parte MKT) |
| Relacionados | EXC-SEG-001, EXC-SEG-009, EXC-FIS-002, EXC-FIS-007, EXC-FIS-009 |
| Datas | Registrado em 2026-10-26 |

### EXC-MKT-004 — Nenhum canal de aquisição ou funil medido

| Campo | Valor |
|---|---|
| Área | MKT (também VEN) |
| Natureza | Lacuna |
| Descrição | Não há registro de contatos, origem, etapas, motivos de perda, custo ou horas por cliente. Não se sabe se existe site, perfil em rede social ou material de divulgação. Os canais propostos em DOC-25 e DOC-32 nunca foram medidos. |
| Evidência | Busca nos documentos do programa e no repositório (2026-10-26); [EXC-A10-MARKETING](EXC-A10-MARKETING.md) §13 |
| Gravidade | A classificar |
| Impacto | Sem medição não há como escolher canal nem saber se uma campanha funciona; números estimados acabariam tratados como fatos. |
| Confiança | — |
| Dependências | — |
| Responsável | Matheus |
| Ação proposta | Criar a planilha de funil com os campos da §13 do relatório antes do primeiro experimento. |
| Status | Lacuna |
| Critério de validação | CRITERIOS MKT-3: pelo menos um ciclo de experimento registrado com origem, etapas e custo por contato. |
| Origem | EXC-A10 (parte MKT) |
| Relacionados | EXC-MKT-001, EXC-VEN-001 |
| Datas | Registrado em 2026-10-26 |
