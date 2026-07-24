// ── ERP.Application/Services/OrderProcessingService.cs ────────────────────────
using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using ERP.Domain.Entities;
using ERP.Domain.Enums;
using ERP.Domain.Interfaces;
using Serilog;

namespace ERP.Application.Services;

public class OrderProcessingService : IOrderProcessingService
{
    private readonly IUnitOfWork _uow;
    private readonly ISaleService _saleService;
    private readonly IContaReceberService _contaReceberService;
    private readonly IEnumerable<IChannelDispatcher> _dispatchers;

    public OrderProcessingService(
        IUnitOfWork uow, ISaleService saleService, IContaReceberService contaReceberService,
        IEnumerable<IChannelDispatcher> dispatchers)
    {
        _uow                 = uow;
        _saleService         = saleService;
        _contaReceberService = contaReceberService;
        _dispatchers         = dispatchers;
    }

    /// <summary>
    /// Ponto de entrada do webhook — processa UM pedido específico, sem rodada
    /// de lote por trás (ProcessingSession é reservado pra polling agendado).
    /// </summary>
    public async Task ProcessarPedidoIndividualAsync(Guid salesChannelId, string externalOrderId)
    {
        var canal = await _uow.OrderSync.GetCanalByIdAsync(salesChannelId)
            ?? throw new InvalidOperationException($"SalesChannel {salesChannelId} não encontrado.");

        var dispatcher = _dispatchers.FirstOrDefault(d => d.Tipo == canal.Tipo)
            ?? throw new InvalidOperationException($"Nenhum IChannelDispatcher registrado pra {canal.Tipo}.");

        var (sucesso, mensagem, pedidoDto) = await dispatcher.BuscarPedidoPorIdAsync(canal, externalOrderId);
        if (!sucesso || pedidoDto is null)
            throw new InvalidOperationException($"Não deu pra buscar o pedido {externalOrderId}: {mensagem}");

        await ProcessarPedidoAsync(canal, pedidoDto, sessao: null);
    }

    public async Task<ProcessingSession> ProcessarCanalAsync(Guid salesChannelId, DateTime desde)
    {
        var canal = await _uow.OrderSync.GetCanalByIdAsync(salesChannelId)
            ?? throw new InvalidOperationException($"SalesChannel {salesChannelId} não encontrado.");

        var dispatcher = _dispatchers.FirstOrDefault(d => d.Tipo == canal.Tipo)
            ?? throw new InvalidOperationException($"Nenhum IChannelDispatcher registrado pra {canal.Tipo}.");

        var sessao = new ProcessingSession { SalesChannelId = canal.Id, IniciadoEm = DateTime.UtcNow };
        await _uow.OrderSync.AddProcessingSessionAsync(sessao);
        await _uow.OrderSync.SalvarAsync(); // sessao.Id disponível pros eventos abaixo, se precisar

        var (sucessoBusca, mensagemBusca, pedidosDto) = await dispatcher.BuscarPedidosNovosAsync(canal, desde);
        if (!sucessoBusca)
        {
            sessao.Status = ProcessingSessionStatus.ConcluidoComErros;
            sessao.FinalizadoEm = DateTime.UtcNow;
            await _uow.OrderSync.SalvarAsync();
            return sessao;
            // Não lança exceção: a rodada em si não falhou, só não trouxe pedidos —
            // quem chama (job agendado) decide se tenta de novo ou alerta.
        }

        foreach (var pedidoDto in pedidosDto)
        {
            try
            {
                await ProcessarPedidoAsync(canal, pedidoDto, sessao);
            }
            catch (Exception ex)
            {
                // Isolamento de falha: um pedido com bug/exceção inesperada não pode
                // travar o lote inteiro. Fica registrado e a rodada continua.
                sessao.TotalErros++;
                var pedidoParcial = await _uow.OrderSync.GetExternalOrderAsync(canal.Id, pedidoDto.ExternalOrderId);
                if (pedidoParcial != null)
                    await RegistrarAcaoAsync(pedidoParcial, OrderActionType.GerarVendaInterna,
                        OrderActionStatus.Falhou, ProcessingErrorCode.ErroDesconhecido, ex.Message);
            }
        }

        sessao.FinalizadoEm = DateTime.UtcNow;
        sessao.Status = sessao.TotalErros > 0
            ? ProcessingSessionStatus.ConcluidoComErros
            : ProcessingSessionStatus.Concluido;
        await _uow.OrderSync.SalvarAsync();
        return sessao;
    }

    // ── Lock por pedido — evita que webhooks concorrentes do mesmo pedido rodem
    // o pipeline (SKU/estoque/venda) em paralelo. A proteção contra corrida no
    // INSERT (TentarInserirExternalOrderAsync) só cobre o instante da criação;
    // sem isso aqui, duas entregas quase simultâneas do MESMO webhook (comum no
    // Mercado Livre) conseguiam as duas passar pelo caminho de "pedido existe,
    // retoma" ao mesmo tempo — e cada uma gerava sua própria Sale, duplicando
    // a venda de verdade (visto em produção: duas Sales pro mesmo pedido).
    // Lock em memória, por processo — suficiente pro tier atual (1 instância);
    // se um dia escalar pra múltiplas instâncias, precisa virar lock no banco
    // (ex: sp_getapplock) em vez de SemaphoreSlim.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, System.Threading.SemaphoreSlim> _locksPorPedido = new();

    private async Task ProcessarPedidoAsync(SalesChannel canal, ExternalOrderDto pedidoDto, ProcessingSession? sessao)
    {
        var chave = $"{canal.Id}:{pedidoDto.ExternalOrderId}";
        var semaforo = _locksPorPedido.GetOrAdd(chave, _ => new System.Threading.SemaphoreSlim(1, 1));

        await semaforo.WaitAsync();
        try
        {
            await ExecutarProcessamentoPedidoAsync(canal, pedidoDto, sessao);
        }
        finally
        {
            semaforo.Release();
        }
    }

    private async Task ExecutarProcessamentoPedidoAsync(SalesChannel canal, ExternalOrderDto pedidoDto, ProcessingSession? sessao)
    {
        var existente = await _uow.OrderSync.GetExternalOrderAsync(canal.Id, pedidoDto.ExternalOrderId);
        if (existente != null)
        {
            // Já ingerido — só há trabalho a fazer se ele ficou preso num estado
            // não-terminal (ex: SkuMapping foi cadastrado depois do conflito, e um
            // novo webhook/retry chegou pro mesmo pedido). Pedido já concluído com
            // sucesso ou cancelado não deve ser retocado.
            if (existente.InternalStatus is ExternalOrderStatus.VendaGerada or ExternalOrderStatus.Cancelado)
                return;

            await RegistrarEventoAsync(existente, OrderEventType.PedidoRecebido,
                "Novo webhook pro mesmo pedido — retomando processamento pendente.");
            if (!await ResolverSkuAsync(existente, canal.Id)) return;
            if (!await ReservarEstoqueAsync(existente)) return;
            await GerarVendaInternaAsync(existente, canal);
            return;
        }

        var pedido = new ExternalOrder
        {
            SalesChannelId    = canal.Id,
            ExternalOrderId   = pedidoDto.ExternalOrderId,
            ExternalStatus    = pedidoDto.ExternalStatus,
            InternalStatus    = ExternalOrderStatus.Recebido,
            DataPedidoExterno = pedidoDto.DataPedidoExterno,
            ValorTotal        = pedidoDto.ValorTotal,
            RawPayloadJson    = pedidoDto.RawPayloadJson,
            Itens = pedidoDto.Itens.Select(i => new ExternalOrderItem
            {
                SkuExterno    = i.SkuExterno,
                DescricaoItem = i.DescricaoItem,
                Quantidade    = i.Quantidade,
                ValorUnitario = i.ValorUnitario
            }).ToList()
        };

        var inserido = await _uow.OrderSync.TentarInserirExternalOrderAsync(pedido);
        if (!inserido)
        {
            // Corrida: o Mercado Livre manda o mesmo webhook várias vezes quase ao
            // mesmo tempo (o próprio payload tem um campo "attempts" avisando disso).
            // Outra requisição concorrente inseriu esse pedido um instante antes —
            // não é erro de verdade. NÃO continua o processamento a partir daqui:
            // a outra tentativa já está seguindo o pipeline sozinha, e continuar
            // nas duas ao mesmo tempo arriscaria reservar estoque/gerar venda em
            // duplicidade. Um retry futuro (novo webhook) passa pelo caminho normal
            // de "pedido já existe" acima, que é sequencial e seguro.
            Log.Information(
                "Pedido {ExternalOrderId} já estava sendo inserido por outra requisição concorrente — ignorando esta.",
                pedidoDto.ExternalOrderId);
            return;
        }
        if (sessao is not null) sessao.TotalPedidosProcessados++;

        await RegistrarEventoAsync(pedido, OrderEventType.PedidoRecebido, "Pedido recebido do canal.");

        if (!await ResolverSkuAsync(pedido, canal.Id)) return;
        if (!await ReservarEstoqueAsync(pedido)) return;
        await GerarVendaInternaAsync(pedido, canal);
    }

    // ── Passo 2: resolver SKU externo → Product interno ─────────────
    private async Task<bool> ResolverSkuAsync(ExternalOrder pedido, Guid salesChannelId)
    {
        foreach (var item in pedido.Itens)
        {
            var mapping = await _uow.OrderSync.GetSkuMappingAsync(salesChannelId, item.SkuExterno);
            if (mapping is null)
            {
                pedido.InternalStatus = ExternalOrderStatus.AguardandoSku;
                await RegistrarConflitoAsync(pedido, OrderConflictType.SkuNaoMapeado,
                    $"SKU '{item.SkuExterno}' sem mapeamento pra este canal.");
                await RegistrarAcaoAsync(pedido, OrderActionType.ResolverSku, OrderActionStatus.Falhou,
                    ProcessingErrorCode.SkuNaoMapeado, item.SkuExterno);
                return false;
            }
            item.ProductId = mapping.ProductId;
        }

        await RegistrarAcaoAsync(pedido, OrderActionType.ResolverSku, OrderActionStatus.Concluida);
        await RegistrarEventoAsync(pedido, OrderEventType.SkuResolvido, "Todos os SKUs resolvidos.");
        return true;
    }

    // ── Passo 3: reservar estoque sombra (checa concorrência entre canais) ──
    private async Task<bool> ReservarEstoqueAsync(ExternalOrder pedido)
    {
        foreach (var item in pedido.Itens)
        {
            var product = await _uow.Products.GetByIdAsync(item.ProductId!.Value);
            if (product is null)
            {
                await RegistrarConflitoAsync(pedido, OrderConflictType.SkuNaoMapeado,
                    $"SkuMapping aponta pra Product {item.ProductId} que não existe mais.");
                return false;
            }

            // BufferSeguranca do mapeamento entra aqui — é a margem de segurança
            // que existe pra evitar vender no marketplace algo que já está "quase"
            // sem estoque local (definida no cadastro do SkuMapping, por canal).
            var mapping = await _uow.OrderSync.GetSkuMappingAsync(pedido.SalesChannelId, item.SkuExterno);
            var bufferSeguranca = mapping?.BufferSeguranca ?? 0;

            var reservadoAtual = await _uow.OrderSync.GetTotalReservadoAsync(product.Id);
            var disponivel = product.Stock - reservadoAtual - bufferSeguranca;
            if (disponivel < item.Quantidade)
            {
                pedido.InternalStatus = ExternalOrderStatus.ConflitoAberto;
                await RegistrarConflitoAsync(pedido, OrderConflictType.EstoqueInsuficiente,
                    $"'{product.Name}': disponível {disponivel:N2}, pedido precisa de {item.Quantidade:N2}.");
                await RegistrarAcaoAsync(pedido, OrderActionType.ReservarEstoque, OrderActionStatus.Falhou,
                    ProcessingErrorCode.EstoqueInsuficiente, product.Name);
                return false;
            }

            await _uow.OrderSync.AddShadowStockReservationAsync(new ShadowStockReservation
            {
                ExternalOrderId = pedido.Id,
                CorrelationId   = pedido.CorrelationId,
                ProductId       = product.Id,
                Quantidade      = item.Quantidade,
                Status          = StatusReservaEstoque.Reservada
            });
        }

        pedido.InternalStatus = ExternalOrderStatus.EstoqueReservado;
        await _uow.OrderSync.SalvarAsync();
        await RegistrarAcaoAsync(pedido, OrderActionType.ReservarEstoque, OrderActionStatus.Concluida);
        await RegistrarEventoAsync(pedido, OrderEventType.EstoqueReservado, "Estoque sombra reservado pra todos os itens.");
        return true;
    }

    // ── Passo 4: gerar Sale interna + Conta a Receber de repasse ────
    private async Task GerarVendaInternaAsync(ExternalOrder pedido, SalesChannel canal)
    {
        Guid vendaId;
        string? saleNumber = null;

        if (pedido.VendaId is not null)
        {
            // Venda já foi criada numa tentativa anterior — só a etapa de repasse
            // falhou depois (ex: o bug de tamanho do Document que corrigimos).
            // NÃO recria a venda: CreateAsync tem efeito colateral real (baixa de
            // estoque de verdade) — rodar de novo geraria uma segunda baixa.
            vendaId = pedido.VendaId.Value;
        }
        else
        {
            if (canal.UsuarioIntegracaoId is null)
                throw new InvalidOperationException(
                    $"SalesChannel '{canal.Nome}' não tem UsuarioIntegracaoId configurado — " +
                    "não dá pra gerar venda automática sem um usuário responsável.");

            var dto = new CreateSaleDto
            {
                UsuarioId  = canal.UsuarioIntegracaoId.Value,
                Origem     = SaleOrigin.Marketplace,
                Notes      = $"Pedido {canal.Tipo} #{pedido.ExternalOrderId}",
                Items      = pedido.Itens.Select(i => new CreateSaleItemDto
                {
                    ProductId = i.ProductId!.Value,
                    Quantity  = i.Quantidade,
                    UnitPrice = i.ValorUnitario // best-effort — SaleService recalcula pelo preço local (ver nota abaixo)
                }).ToList(),
                // PaymentMethod aqui é só rótulo informativo do Sale/SalePayment (relatórios) —
                // não dispara nenhum movimento financeiro por si só. O financeiro real do
                // repasse é o ContaReceber logo abaixo, não isso.
                Payments = new List<CreateSalePaymentDto>
                {
                    new() { PaymentMethod = PaymentMethod.APrazo, Amount = pedido.ValorTotal }
                }
            };

            var saleDto = await _saleService.CreateAsync(dto);
            saleNumber  = saleDto.SaleNumber;
            vendaId     = saleDto.Id;

            // MarcarVendaGeradaAsync, não mutar "pedido" direto e salvar: o
            // _saleService.CreateAsync acima já chamou SaveChanges (pra criar a
            // Sale), e o SaveChangesAsync do projeto faz ChangeTracker.Clear()
            // toda vez — "pedido" (buscado antes) perdeu o rastreamento nesse
            // meio-tempo. Mutar ele agora e salvar seria no-op silencioso.
            await _uow.OrderSync.MarcarVendaGeradaAsync(pedido.Id, vendaId);
            pedido.VendaId = vendaId; // só pro objeto em memória ficar coerente pro resto deste método
            pedido.InternalStatus = ExternalOrderStatus.VendaGerada;

            // Confirma as reservas sombra desse pedido — a baixa real já aconteceu
            // dentro de CreateAsync, então a reserva sai de "comprometido, ainda
            // não vendido" pra "já virou venda de verdade".
            var reservas = await _uow.OrderSync.GetReservasAtivasPorPedidoAsync(pedido.Id);
            foreach (var reserva in reservas)
            {
                reserva.Status     = StatusReservaEstoque.Confirmada;
                reserva.LiberadaEm = DateTime.UtcNow;
            }
            await _uow.OrderSync.SalvarAsync();
        }

        // Cliente de repasse: cria uma vez por canal, reaproveita depois.
        var clienteRepasse = await _uow.OrderSync.GetClienteRepasseAsync(canal.Id)
            ?? await _uow.OrderSync.CriarClienteRepasseAsync(canal);
        await _uow.OrderSync.SalvarAsync(); // persiste o vínculo SalesChannel.ClienteRepasseId, se criado agora

        // IMPORTANTE: usa pedido.ValorTotal (o que o marketplace cobrou de verdade),
        // não saleDto.Total — CreateAsync recalcula preço pela tabela local (grupo
        // de preço do cliente), que pode divergir do valor anunciado no canal.
        //
        // GAP CONHECIDO: se o crash acontecer DEPOIS daqui (ex: nessa própria
        // chamada) e alguém reprocessar o mesmo pedido, isso pode gerar um
        // segundo Contas a Receber pro mesmo VendaId — não tem proteção de
        // idempotência nessa etapa ainda. Janela estreita, baixo risco pra
        // um piloto — registrar se algum dia isso realmente acontecer.
        await _contaReceberService.GerarContaAPrazoAsync(
            clienteRepasse.Id, vendaId, pedido.ValorTotal,
            $"Repasse {canal.Tipo} — Pedido #{pedido.ExternalOrderId} (aguardando liquidação)");

        await RegistrarAcaoAsync(pedido, OrderActionType.GerarVendaInterna, OrderActionStatus.Concluida);
        await RegistrarEventoAsync(pedido, OrderEventType.VendaGerada,
            saleNumber is not null
                ? $"Venda {saleNumber} gerada; Conta a Receber de repasse criada."
                : "Conta a Receber de repasse criada (venda já existia de uma tentativa anterior).");
    }

    // ── Helpers de rastro ────────────────────────────────────────────
    private async Task RegistrarEventoAsync(ExternalOrder pedido, OrderEventType tipo, string descricao,
        OrderEventSeverity severity = OrderEventSeverity.Info)
    {
        await _uow.OrderSync.AddOrderEventAsync(new OrderEvent
        {
            ExternalOrderId = pedido.Id,
            CorrelationId   = pedido.CorrelationId,
            Tipo            = tipo,
            Severity        = severity,
            Descricao       = descricao
        });
        await _uow.OrderSync.SalvarAsync();
    }

    private async Task RegistrarAcaoAsync(ExternalOrder pedido, OrderActionType tipo, OrderActionStatus status,
        ProcessingErrorCode? erro = null, string? mensagem = null)
    {
        await _uow.OrderSync.AddOrderActionAsync(new OrderAction
        {
            ExternalOrderId = pedido.Id,
            CorrelationId   = pedido.CorrelationId,
            Tipo            = tipo,
            Status          = status,
            ErroCodigo      = erro,
            ErroMensagem    = mensagem,
            ConcluidaEm     = status != OrderActionStatus.Pendente ? DateTime.UtcNow : null
        });
        await _uow.OrderSync.SalvarAsync();
    }

    private async Task RegistrarConflitoAsync(ExternalOrder pedido, OrderConflictType tipo, string descricao)
    {
        await _uow.OrderSync.AddOrderConflictAsync(new OrderConflict
        {
            ExternalOrderId = pedido.Id,
            CorrelationId   = pedido.CorrelationId,
            Tipo            = tipo,
            Descricao       = descricao
        });
        await RegistrarEventoAsync(pedido, OrderEventType.ConflitoDetectado, descricao, OrderEventSeverity.Warning);
    }
}