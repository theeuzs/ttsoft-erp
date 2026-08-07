// ── ERP.Application/Services/MotorFinanceiroService.cs ────────────────────────
using ERP.Application.Interfaces;
using ERP.Domain.Enums;

namespace ERP.Application.Services;

/// <summary>
/// Ponto único de entrada pro PDV processar o recebimento de uma venda. Antes,
/// essa decisão (Dinheiro → Caixa, PIX → Conta Bancária, Cartão → Recebível de
/// Operadora, A Prazo → Conta a Receber, Haver → saldo do cliente) vivia inline
/// dentro do FinalizarVendaViewModel.
///
/// Idempotência financeira granular (achado de auditoria pré-Fase-2 do
/// Offline-First, 08/2026): a pergunta certa não é "essa venda já foi
/// processada?" — é "essa LINHA de pagamento específica já gerou esse
/// lançamento?". Uma venda pode ter várias linhas legítimas (2 cartões,
/// PIX+dinheiro, débito+crédito) que não são duplicatas umas das outras.
/// Confirmado com dados reais de produção: os 3 casos que pareciam duplicata
/// eram todos legítimos (split de pagamento ou recebimento+estorno) — nenhum
/// era bug de verdade. SalePaymentId (gerado no cliente, mesmo padrão do
/// Sale.Id da Fase 1) é a chave de idempotência certa.
/// </summary>
public class MotorFinanceiroService : IMotorFinanceiroService
{
    private readonly ICaixaService             _caixaService;
    private readonly IContaReceberService      _contaReceberService;
    private readonly IHaverService             _haverService;
    private readonly IContaBancariaService     _contaBancariaService;
    private readonly IRecebivelOperadoraService _recebivelOperadoraService;

    public MotorFinanceiroService(
        ICaixaService caixaService, IContaReceberService contaReceberService,
        IHaverService haverService, IContaBancariaService contaBancariaService,
        IRecebivelOperadoraService recebivelOperadoraService)
    {
        _caixaService              = caixaService;
        _contaReceberService       = contaReceberService;
        _haverService              = haverService;
        _contaBancariaService      = contaBancariaService;
        _recebivelOperadoraService = recebivelOperadoraService;
    }

    public async Task ProcessarRecebimentoVendaAsync(
        Guid vendaId, Guid usuarioId, Guid? clienteId, string nomeCliente, string nomeVendedor,
        string nomeOperador, decimal troco,
        IEnumerable<(Guid SalePaymentId, PaymentMethod Forma, decimal Valor)> pagamentos)
    {
        if (usuarioId == Guid.Empty)
            throw new InvalidOperationException("Sessão de usuário perdida! Por favor, faça login novamente.");

        foreach (var p in pagamentos)
        {
            switch (p.Forma)
            {
                case PaymentMethod.APrazo:
                    await RegistrarAPrazoAsync(vendaId, p.SalePaymentId, clienteId, nomeVendedor, p.Valor);
                    break;
                case PaymentMethod.Dinheiro:
                    await RegistrarDinheiroAsync(vendaId, p.SalePaymentId, usuarioId, p.Valor, troco);
                    break;
                case PaymentMethod.Haver:
                    await RegistrarHaverAsync(vendaId, p.SalePaymentId, usuarioId, clienteId, nomeOperador, p.Valor);
                    break;
                default:
                    await RegistrarDigitalAsync(vendaId, p.SalePaymentId, usuarioId, nomeCliente, p.Forma, p.Valor);
                    break;
            }
        }
    }

    public async Task RegistrarLiquidacaoOperadoraAsync(
        IReadOnlyList<Guid> recebivelIds, decimal valorRealDepositado, DateTime dataLiquidacao)
        => await _recebivelOperadoraService.LiquidarLoteAsync(recebivelIds, valorRealDepositado, dataLiquidacao);

    public async Task VerificarPodeCancelarVendaAsync(Guid vendaId)
    {
        var temLiquidado = await _recebivelOperadoraService.TemLiquidadoPorVendaAsync(vendaId);
        if (temLiquidado)
            throw new InvalidOperationException(
                "Esta venda tem um recebível de cartão já LIQUIDADO — o dinheiro já está confirmado " +
                "na Conta Bancária. Cancelar automaticamente quebraria a Conciliação Bancária (o extrato " +
                "do banco não vai devolver sozinho). Estorne manualmente: lance uma Saída na Conta " +
                "Bancária pelo valor líquido, e ajuste o recebível se necessário.");
    }

    public async Task EstornarVendaAsync(
        Guid vendaId, Guid usuarioId, string descricaoEstorno, decimal troco,
        IEnumerable<(Guid SalePaymentId, PaymentMethod Forma, decimal Valor)> pagamentos)
    {
        var trocoRestante = troco;

        foreach (var p in pagamentos)
        {
            // Haver já é revertido dentro do CancelAsync da própria venda (saldo do
            // cliente devolvido lá); A Prazo já tem as contas a receber canceladas
            // separadamente. Nenhum dos dois passa por aqui de novo.
            if (p.Forma == PaymentMethod.Haver || p.Forma == PaymentMethod.APrazo) continue;

            if (p.Forma == PaymentMethod.Dinheiro)
            {
                decimal valorEstornar = p.Valor;
                if (trocoRestante > 0)
                {
                    valorEstornar -= trocoRestante;
                    trocoRestante = 0;
                }

                if (valorEstornar > 0)
                {
                    // Estorno de Dinheiro é uma Sangria manual, não passa pela
                    // checagem de idempotência de RegistrarDinheiroAsync — é um
                    // fluxo diferente (cancelamento, não sincronização), disparado
                    // no máximo uma vez por cancelamento de venda.
                    await _caixaService.RegistrarMovimentoAsync(
                        usuarioId, valorEstornar, descricaoEstorno, p.Forma, TipoMovimentoCaixa.Sangria,
                        vendaId: vendaId, salePaymentId: p.SalePaymentId);
                }
            }
            else if (p.Forma == PaymentMethod.Pix)
            {
                // PIX caiu na hora — estorna com uma Saída compensatória na Conta
                // Bancária. Idempotência por (SalePaymentId, Saida) — se esse
                // pagamento já foi estornado, não estorna de novo.
                if (await _contaBancariaService.ExisteMovimentoParaSalePaymentAsync(p.SalePaymentId, TipoMovimentoContaBancaria.Saida))
                    continue;

                await _contaBancariaService.RegistrarEstornoVendaAsync(vendaId, p.Valor, descricaoEstorno, p.SalePaymentId);
            }
            else if (p.Forma == PaymentMethod.CartaoDebito || p.Forma == PaymentMethod.CartaoCredito)
            {
                // Nenhum dinheiro chegou a entrar em lugar nenhum ainda (garantido
                // por VerificarPodeCancelarVendaAsync já ter passado) — só cancela
                // o recebível, sem gerar nenhum movimento financeiro.
                await _recebivelOperadoraService.CancelarPendentesPorVendaAsync(vendaId);
            }
        }
    }

    private async Task RegistrarAPrazoAsync(Guid vendaId, Guid salePaymentId, Guid? clienteId, string nomeVendedor, decimal valor)
    {
        if (clienteId is null)
            throw new InvalidOperationException("Venda a prazo precisa de um cliente selecionado.");

        // Idempotência granular — segunda camada é a constraint única em
        // ContaReceber.SalePaymentId no banco; essa checagem aqui evita a
        // exceção de constraint no caso comum (retry do Sync Engine).
        if (await _contaReceberService.ExisteContaParaSalePaymentAsync(salePaymentId)) return;

        // Mantido igual ao original: usa o nome do VENDEDOR aqui, não do cliente.
        await _contaReceberService.GerarContaAPrazoAsync(
            clienteId.Value, vendaId, valor, $"Venda A Prazo - {nomeVendedor}", salePaymentId);
    }

    private async Task RegistrarDinheiroAsync(Guid vendaId, Guid salePaymentId, Guid usuarioId, decimal valor, decimal troco)
    {
        if (await _caixaService.ExisteMovimentoParaSalePaymentAsync(salePaymentId)) return;

        decimal valorParaCaixa = troco > 0 ? valor - troco : valor;
        await _caixaService.RegistrarMovimentoAsync(
            usuarioId, valorParaCaixa, "VENDA - DINHEIRO", PaymentMethod.Dinheiro, TipoMovimentoCaixa.Venda,
            vendaId: vendaId, salePaymentId: salePaymentId);
    }

    private async Task RegistrarHaverAsync(Guid vendaId, Guid salePaymentId, Guid usuarioId, Guid? clienteId, string nomeOperador, decimal valor)
    {
        if (clienteId is null) return;
        if (await _caixaService.ExisteMovimentoParaSalePaymentAsync(salePaymentId)) return;

        // Mantido igual ao original: usa o nome do OPERADOR logado aqui, não do cliente.
        await _haverService.RegistrarMovimentoVendaAsync(
            clienteId.Value, valor, "Saida", "Uso em venda", nomeOperador);

        await _caixaService.RegistrarMovimentoAsync(
            usuarioId, valor, "VENDA (Haver)", PaymentMethod.Haver, TipoMovimentoCaixa.Venda,
            vendaId: vendaId, salePaymentId: salePaymentId);
    }

    private async Task RegistrarDigitalAsync(Guid vendaId, Guid salePaymentId, Guid usuarioId, string nomeCliente, PaymentMethod forma, decimal valor)
    {
        if (await _caixaService.ExisteMovimentoParaSalePaymentAsync(salePaymentId)) return;

        await _caixaService.RegistrarMovimentoAsync(
            usuarioId, valor, $"VENDA DIGITAL - {forma}", forma, TipoMovimentoCaixa.Venda,
            vendaId: vendaId, salePaymentId: salePaymentId);

        // Ponto único de decisão: PIX cai direto na Conta Bancária (é
        // literalmente instantâneo, 1 pra 1, sem intermediário). Cartão gera um
        // Recebível de Operadora — dinheiro real, mas só vira saldo bancário
        // quando a operadora liquidar (dias depois, em lote, com taxa
        // descontada). Nenhum dos dois trava a venda se a configuração
        // (Conta Padrão / Operadora Padrão) ainda não existir.
        if (forma == PaymentMethod.Pix)
        {
            if (await _contaBancariaService.ExisteMovimentoParaSalePaymentAsync(salePaymentId, TipoMovimentoContaBancaria.Entrada))
                return;
            await _contaBancariaService.RegistrarRecebimentoVendaAsync(
                vendaId, valor, $"Venda - {nomeCliente} ({forma})", salePaymentId);
        }
        else if (forma == PaymentMethod.CartaoDebito || forma == PaymentMethod.CartaoCredito)
        {
            if (await _recebivelOperadoraService.ExisteRecebivelParaSalePaymentAsync(salePaymentId)) return;
            await _recebivelOperadoraService.RegistrarRecebivelVendaAsync(vendaId, forma, valor, salePaymentId);
        }
    }
}