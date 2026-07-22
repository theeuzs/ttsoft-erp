// ── ERP.Application/Services/RecebivelOperadoraService.cs ────────────────────
using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using ERP.Domain.Entities;
using ERP.Domain.Enums;
using ERP.Domain.Interfaces;
using System.Linq;

namespace ERP.Application.Services;

public class RecebivelOperadoraService : IRecebivelOperadoraService
{
    private readonly IUnitOfWork _uow;
    public RecebivelOperadoraService(IUnitOfWork uow) => _uow = uow;

    public async Task<IReadOnlyList<RecebivelOperadoraDto>> ObterPendentesAsync()
    {
        var pendentes = await _uow.RecebiveisOperadora.GetPendentesAsync();
        return pendentes.Select(r => new RecebivelOperadoraDto
        {
            Id                      = r.Id,
            OperadoraNome           = r.OperadoraRecebimento?.Nome ?? "(operadora removida)",
            FormaRecebimento        = r.FormaRecebimento,
            ValorBruto              = r.ValorBruto,
            ValorTaxa               = r.ValorTaxa,
            ValorLiquido            = r.ValorLiquido,
            DataVenda               = r.DataVenda,
            DataPrevistaLiquidacao  = r.DataPrevistaLiquidacao,
            Status                  = r.Status,
            Nsu                     = r.Nsu
        }).ToList();
    }

    public async Task RegistrarRecebivelVendaAsync(Guid? vendaId, PaymentMethod formaPagamento, decimal valorBruto)
    {
        var operadora = await _uow.OperadorasRecebimento.GetPadraoAsync();
        if (operadora is null) return; // sem operadora padrão configurada — não trava a venda.

        // S17: parcelamento em cartão de crédito ainda não é rastreado no PDV
        // (PaymentMethod não distingue à vista de parcelado hoje) — assume à
        // vista como padrão. Se a maquininha parcelar, o prazo real pode ser
        // maior que o estimado aqui; ajustar quando o PDV capturar parcelas.
        var formaRecebimento = formaPagamento == PaymentMethod.CartaoDebito
            ? FormaRecebimentoOperadora.Debito
            : FormaRecebimentoOperadora.CreditoVista;

        var dataVenda = DateTime.Now;

        // A conta de taxa/prazo mora na própria Operadora (evita Modelo de
        // Domínio Anêmico) — este service só orquestra, não calcula.
        var (valorTaxa, valorLiquido, dataPrevista) =
            operadora.CalcularRecebimento(formaRecebimento, valorBruto, dataVenda);

        var recebivel = new RecebivelOperadora
        {
            OperadoraRecebimentoId  = operadora.Id,
            VendaId                 = vendaId,
            FormaRecebimento        = formaRecebimento,
            ValorBruto              = valorBruto,
            ValorTaxa               = valorTaxa,
            ValorLiquido            = valorLiquido,
            DataVenda               = dataVenda,
            DataPrevistaLiquidacao  = dataPrevista,
            Status                  = StatusRecebivel.Pendente
        };

        await _uow.RecebiveisOperadora.AddAsync(recebivel);
        await _uow.CommitAsync();
    }

    public async Task LiquidarLoteAsync(IReadOnlyList<Guid> recebivelIds, decimal valorRealDepositado, DateTime dataLiquidacao)
    {
        if (recebivelIds.Count == 0)
            throw new InvalidOperationException("Selecione ao menos um recebível pra liquidar.");

        var recebiveis = (await _uow.RecebiveisOperadora.GetByIdsAsync(recebivelIds)).ToList();
        if (recebiveis.Count == 0)
            throw new InvalidOperationException("Nenhum dos recebíveis selecionados foi encontrado.");

        var operadoraId = recebiveis[0].OperadoraRecebimentoId;
        var operadora = await _uow.OperadorasRecebimento.GetByIdAsync(operadoraId)
            ?? throw new InvalidOperationException("Operadora do recebível não encontrada.");

        if (operadora.ContaDestinoId is null)
            throw new InvalidOperationException(
                $"A operadora '{operadora.Nome}' não tem uma Conta Destino configurada — defina antes de liquidar.");

        // Cria o movimento na Conta Bancária com o valor REAL depositado (pode
        // diferir da soma dos líquidos calculados, por arredondamento da
        // operadora) — o Id já existe antes de salvar (BaseEntity gera na
        // criação), então dá pra usar pra vincular os recebíveis já aqui.
        var movimento = new MovimentoContaBancaria
        {
            ContaBancariaId = operadora.ContaDestinoId.Value,
            Valor           = valorRealDepositado,
            Descricao       = $"Liquidação {operadora.Nome} — {recebiveis.Count} recebível(is)",
            Tipo            = TipoMovimentoContaBancaria.Entrada,
            DataHora        = dataLiquidacao,
            OrigemTipo      = OrigemMovimentoFinanceiro.LiquidacaoOperadora
            // OrigemId fica null de propósito — é um lote de N recebíveis, sem
            // um único dono. Cada RecebivelOperadora já aponta de volta pra cá
            // via MovimentoContaBancariaId, então a rastreabilidade existe na
            // direção contrária.
        };

        await _uow.ContasBancarias.AddMovimentoAsync(movimento);
        await _uow.RecebiveisOperadora.MarcarLiquidadosAsync(recebivelIds, movimento.Id, dataLiquidacao);
        await _uow.CommitAsync();
    }

    public async Task<bool> TemLiquidadoPorVendaAsync(Guid vendaId)
    {
        var recebiveis = await _uow.RecebiveisOperadora.GetByVendaIdAsync(vendaId);
        return recebiveis.Any(r => r.Status == StatusRecebivel.Liquidado);
    }

    public async Task CancelarPendentesPorVendaAsync(Guid vendaId)
        => await _uow.RecebiveisOperadora.CancelarPendentesPorVendaAsync(vendaId);
}