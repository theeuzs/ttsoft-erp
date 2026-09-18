// ERP.WPF/Services/SyncEngineService.cs
using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using ERP.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using System.Linq;
using System.Text.Json;

namespace ERP.WPF.Services;

/// <summary>
/// Fase 1 do Offline-First (docs/OFFLINE_FIRST_ARCHITECTURE.md) — o motor
/// de sincronização em si. Só infraestrutura: NÃO inicia timer automático
/// aqui (isso é integração de Fase 2/3), não mexe em nenhuma tela do PDV.
/// Só os métodos que o Fase 2 vai chamar quando ligar isso de verdade.
///
/// ATUALIZADO na Fase B (08/2026): chama ISaleService.CreateAsync — a MESMA
/// instância que o PDV online usa, resolvida pelo container de DI do WPF.
/// Antes da Fase B isso era o SaleService local (Azure SQL direto, sem
/// HTTP); depois da troca de DI, é HttpSaleService (POST /api/sales). O
/// motor de sync nunca precisou saber qual — só reaproveita a MESMA peça
/// injetada, com o dado vindo da fila local em vez da tela. É essa
/// dependência compartilhada que faz a troca de DI valer tanto pro caminho
/// online quanto pro de sincronização, sem precisar de nenhuma mudança
/// nessa classe.
/// </summary>
public class SyncEngineService
{
    private readonly OfflineSyncService _offlineDb;
    private readonly ISaleService _saleService;
    private readonly IProductService _productService;
    private readonly ICustomerService _customerService;
    private readonly IMotorFinanceiroService _motorFinanceiro;
    private readonly IServiceScopeFactory _scopeFactory;

    public SyncEngineService(
        OfflineSyncService offlineDb, ISaleService saleService,
        IProductService productService, ICustomerService customerService,
        IMotorFinanceiroService motorFinanceiro, IServiceScopeFactory scopeFactory)
    {
        _offlineDb       = offlineDb;
        _saleService     = saleService;
        _productService  = productService;
        _customerService = customerService;
        _motorFinanceiro = motorFinanceiro;
        _scopeFactory    = scopeFactory;
    }

    /// <summary>Grava uma venda offline (SQLite + Outbox, numa transação só —
    /// §16.5). O <paramref name="dto"/> já precisa ter <c>Id</c> preenchido
    /// com um Guid gerado no momento da venda, ANTES de qualquer tentativa
    /// de rede — é esse Id que garante idempotência (§7). Quem chama isso é
    /// o PDV (Fase 2) quando detecta que está offline — essa classe não
    /// decide sozinha quando gravar offline vs. mandar direto.</summary>
    public async Task SalvarVendaOfflineAsync(CreateSaleDto dto)
    {
        if (!dto.Id.HasValue)
            throw new InvalidOperationException(
                "CreateSaleDto.Id precisa estar preenchido antes de salvar offline — é a chave de idempotência (§7).");

        await _offlineDb.SalvarVendaOfflineComOutboxAsync(dto.Id.Value, dto);
    }

    /// <summary>Processa a fila da Outbox — tenta sincronizar cada evento
    /// pendente, um de cada vez, sem parar no primeiro erro (uma venda com
    /// problema não pode travar as outras). Chamado tanto pelo ciclo
    /// periódico (retry, §10) quanto imediatamente após uma venda offline
    /// ser gravada, se já estiver online naquele instante.</summary>
    /// <returns>Quantos eventos foram sincronizados com sucesso nessa passada.</returns>
    public async Task<int> ProcessarOutboxAsync()
    {
        // Achado (17/09) — recupera vendas presas em VendasOffline sem par
        // na SyncOutbox antes de processar, senão ficam pendentes pra
        // sempre sem chance de sincronizar (ver comentário no método).
        await _offlineDb.ReconciliarVendasOfflineOrfasAsync();

        var pendentes = await _offlineDb.GetEventosPendentesAsync();
        int sucessos = 0;

        foreach (var (outboxId, payloadJson, _) in pendentes)
        {
            try
            {
                var dto = JsonSerializer.Deserialize<CreateSaleDto>(payloadJson);
                if (dto == null || !dto.Id.HasValue)
                {
                    Log.Warning("SyncEngine: evento {OutboxId} com payload inválido, pulando", outboxId);
                    continue;
                }

                // Idempotência (§7) acontece DENTRO do SaleService.CreateAsync —
                // se essa venda já existir no Azure (ex: sincronizou antes mas a
                // resposta se perdeu), ele devolve a existente em vez de duplicar.
                // Esse método não precisa (e não deve) checar isso de novo aqui.
                await _saleService.CreateAsync(dto);

                // Achado do teste manual da Fase 2 (08/2026) — faltava exatamente
                // isso: CreateAsync sozinho só grava a venda, nunca o financeiro
                // (Caixa/Conta Bancária/Recebível/Conta a Receber). Uma venda
                // offline sincronizada ficava "sem dinheiro" no Azure até essa
                // correção — confirmado com dado real (venda de R$79,80 apareceu
                // em Sales, CaixaMovimentos não ganhou linha nova nenhuma).
                // Idempotência aqui é por SalePaymentId (correção anterior),
                // então chamar isso de novo numa segunda sincronização da MESMA
                // venda (ex: retry) é seguro — vira no-op.
                string? nomeCliente = null;
                if (dto.CustomerId.HasValue)
                {
                    try
                    {
                        var cliente = await _customerService.GetByIdAsync(dto.CustomerId.Value);
                        nomeCliente = cliente?.Name;
                    }
                    catch { /* nome é só descritivo — não trava a sincronização se falhar */ }
                }

                if (dto.Payments.Any(p => !p.Id.HasValue))
                    throw new InvalidOperationException(
                        "CreateSalePaymentDto.Id precisa estar preenchido em toda linha antes de sincronizar — é a chave de idempotência financeira granular.");

                await _motorFinanceiro.ProcessarRecebimentoVendaAsync(
                    dto.Id.Value, dto.UsuarioId, dto.CustomerId,
                    nomeCliente ?? "Consumidor Final", dto.SellerName ?? "Balcão", "Sync Automático",
                    dto.Troco,
                    dto.Payments.Select(p => (p.Id!.Value, p.PaymentMethod, p.Amount)));

                await _offlineDb.MarcarEventoSincronizadoAsync(outboxId, dto.Id.Value);
                sucessos++;
            }
            catch (ERP.Application.Exceptions.SessaoExpiradaException)
            {
                // Revisão cruzada com GPT (08/2026) — sessão expirada é um
                // problema GLOBAL de autenticação, não um erro dessa venda
                // específica. Diferente do catch genérico abaixo (best-effort
                // por item, segue pra próxima venda): aqui a gente PARA o
                // ciclo inteiro. Continuar tentando as próximas vendas com o
                // mesmo token inválido só geraria uma sequência de 401 sem
                // nenhum benefício — e cada evento tentado de novo no próximo
                // ciclo já é o comportamento certo (nada precisa ser marcado
                // como sincronizado nem descartado; o item simplesmente
                // continua pendente na fila, do jeito que já estava).
                //
                // De propósito NÃO chama RegistrarFalhaEventoAsync aqui — não
                // é justo incrementar o contador de tentativas dessa venda
                // específica por causa de um problema que não é dela.
                Log.Warning(
                    "SyncEngine: sessão da API expirada/inválida — ciclo de sincronização interrompido " +
                    "(evento {OutboxId} e os seguintes continuam pendentes; próximo ciclo tenta de novo " +
                    "com o token que estiver em AppSession.JwtToken naquele momento)", outboxId);
                break;
            }
            catch (Exception ex)
            {
                // Best-effort por evento — uma venda com erro (ex: produto que
                // não existe mais) não pode travar as outras vendas da fila.
                var entidadeId = ExtrairEntidadeId(payloadJson);
                if (entidadeId.HasValue)
                    await _offlineDb.RegistrarFalhaEventoAsync(outboxId, entidadeId.Value, ex.Message);

                Log.Warning(ex, "SyncEngine: falha ao sincronizar evento {OutboxId}", outboxId);
            }
        }

        return sucessos;
    }

    private static Guid? ExtrairEntidadeId(string payloadJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(payloadJson);
            // Bug real achado pelos testes (08/2026): JsonSerializer.Serialize
            // sem opções customizadas preserva o nome exato da propriedade C#
            // (Id, PascalCase) — a busca por "id" minúsculo nunca encontrava
            // nada, então RegistrarFalhaEventoAsync nunca rodava numa falha
            // real; o log do Serilog aparecia (incondicional), mas
            // Tentativas/UltimoErro nunca eram gravados no banco, deixando a
            // tela de diagnóstico (Fase 3) cega justamente quando mais precisa
            // funcionar. "Id" é o nome real; "id" fica como fallback defensivo.
            if (doc.RootElement.TryGetProperty("Id", out var idProp) && idProp.TryGetGuid(out var g))
                return g;
            if (doc.RootElement.TryGetProperty("id", out var idPropLower) && idPropLower.TryGetGuid(out var g2))
                return g2;
        }
        catch { /* payload malformado — RegistrarFalhaEventoAsync simplesmente não roda pra esse caso */ }
        return null;
    }

    /// <summary>Sincronização de catálogo (§8, §10) — produtos e clientes,
    /// por snapshot/estado (sem risco, diferente de venda/estoque). Chamado
    /// a cada 15 min enquanto online, e uma vez ao abrir o sistema.
    ///
    /// Fase 3 — o retorno bool virou o sinal de conectividade mais confiável
    /// do app (ConnectivityIndicatorState): diferente do ciclo da Outbox, que
    /// só faz uma chamada de rede QUANDO existe venda pendente, esse aqui
    /// sempre tenta, mesmo sem nada pendente — é ele que sabe dizer "estamos
    /// online" mesmo num minuto sem venda nenhuma acontecendo.</summary>
    public async Task<bool> SincronizarCatalogoAsync()
    {
        // Achado do teste manual (08/2026) — rodar produtos e clientes em
        // sequência somava dois timeouts de conexão, um atrás do outro,
        // quando os dois falham. Em paralelo, os dois esperam o MESMO
        // timeout, não a soma — o indicador de conectividade responde bem
        // mais rápido quando genuinamente offline.
        var tarefaProdutos = SincronizarProdutosComResultadoAsync();
        var tarefaClientes = SincronizarClientesComResultadoAsync();
        await Task.WhenAll(tarefaProdutos, tarefaClientes);

        // Achado de auditoria (24/08) — antes só devolvia o resultado de
        // produtos, ignorando clientes por completo. Se produtos sincronizava
        // e clientes falhava, o indicador de conectividade acendia "online"
        // mesmo com metade do catálogo local desatualizado — sinal mentindo
        // pro operador, sem gerar nenhum aviso. Combinado: só é "online" de
        // verdade se os dois sincronizaram.
        return tarefaProdutos.Result && tarefaClientes.Result;
    }

    private async Task<bool> SincronizarProdutosComResultadoAsync()
    {
        try
        {
            // Achado (15/09) — "A second operation was started on this
            // context instance" em produção. _productService/_customerService
            // vêm do MESMO escopo de DI (um por tick do timer), e portanto
            // compartilham o MESMO AppDbContext — rodar os dois em paralelo
            // (Task.WhenAll, ver comentário acima) é exatamente o cenário que
            // o EF Core proíbe (DbContext não é thread-safe). Corrigido
            // abrindo um escopo próprio aqui dentro, só pra esta chamada —
            // cada uma das duas tarefas paralelas agora tem seu próprio
            // AppDbContext, sem perder o paralelismo que existe por um
          // motivo real (fast-fail quando offline).
            using var scope = _scopeFactory.CreateScope();
            var productService = scope.ServiceProvider.GetRequiredService<IProductService>();

            var produtos = await productService.GetAllAsync();
            await _offlineDb.SincronizarProdutosAsync(produtos.Cast<object>());
            return true;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "SyncEngine: falha ao sincronizar catálogo de produtos");
            return false;
        }
    }

    private async Task<bool> SincronizarClientesComResultadoAsync()
    {
        try
        {
            // Achado (15/09) — mesmo motivo do método de produtos acima:
            // escopo próprio evita compartilhar AppDbContext entre as duas
            // tarefas rodando em paralelo.
            using var scope = _scopeFactory.CreateScope();
            var customerService = scope.ServiceProvider.GetRequiredService<ICustomerService>();

            var clientes = await customerService.GetAllAsync();
            await _offlineDb.SincronizarClientesAsync(clientes.Cast<object>());
            return true;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "SyncEngine: falha ao sincronizar catálogo de clientes");
            return false;
        }
    }
}