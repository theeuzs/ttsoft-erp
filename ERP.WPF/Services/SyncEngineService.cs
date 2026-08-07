// ERP.WPF/Services/SyncEngineService.cs
using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using ERP.Infrastructure.Services;
using Serilog;
using System.Text.Json;

namespace ERP.WPF.Services;

/// <summary>
/// Fase 1 do Offline-First (docs/OFFLINE_FIRST_ARCHITECTURE.md) — o motor
/// de sincronização em si. Só infraestrutura: NÃO inicia timer automático
/// aqui (isso é integração de Fase 2/3), não mexe em nenhuma tela do PDV.
/// Só os métodos que o Fase 2 vai chamar quando ligar isso de verdade.
///
/// Importante: isso NÃO fala com a API por HTTP. Chama ISaleService.CreateAsync
/// — a MESMA instância local que a venda online do PDV já usa, resolvida
/// pelo container de DI do próprio WPF (confirmado em WpfEstoqueSyncService.cs:
/// o SaleService já roda dentro do processo WPF, falando direto com o Azure
/// SQL via EF Core, sem passar pela API). O motor de sync só reaproveita
/// essa mesma peça, com o dado vindo da fila local em vez da tela.
/// </summary>
public class SyncEngineService
{
    private readonly OfflineSyncService _offlineDb;
    private readonly ISaleService _saleService;
    private readonly IProductService _productService;
    private readonly ICustomerService _customerService;

    public SyncEngineService(
        OfflineSyncService offlineDb, ISaleService saleService,
        IProductService productService, ICustomerService customerService)
    {
        _offlineDb       = offlineDb;
        _saleService     = saleService;
        _productService  = productService;
        _customerService = customerService;
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

                await _offlineDb.MarcarEventoSincronizadoAsync(outboxId, dto.Id.Value);
                sucessos++;
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
    /// a cada 15 min enquanto online, e uma vez ao abrir o sistema.</summary>
    public async Task SincronizarCatalogoAsync()
    {
        try
        {
            var produtos = await _productService.GetAllAsync();
            await _offlineDb.SincronizarProdutosAsync(produtos.Cast<object>());
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "SyncEngine: falha ao sincronizar catálogo de produtos");
        }

        try
        {
            var clientes = await _customerService.GetAllAsync();
            await _offlineDb.SincronizarClientesAsync(clientes.Cast<object>());
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "SyncEngine: falha ao sincronizar catálogo de clientes");
        }
    }
}