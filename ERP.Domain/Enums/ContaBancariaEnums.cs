// ── ERP.Domain/Enums/ContaBancariaEnums.cs ────────────────────────────────────
namespace ERP.Domain.Enums;

public enum TipoMovimentoContaBancaria
{
    Entrada = 1,
    Saida   = 2
}

/// <summary>
/// De onde nasceu um MovimentoContaBancaria — permite clicar num movimento e
/// abrir a venda/conta/liquidação exata que o gerou, em vez de depender só da
/// descrição em texto livre.
/// </summary>
public enum OrigemMovimentoFinanceiro
{
    Manual               = 0, // lançamento manual, digitado direto na tela
    Venda                = 1,
    LiquidacaoOperadora  = 2,
    ContaPagar           = 3,
    ConciliacaoOfx       = 4  // descoberto no extrato, sem lançamento prévio no sistema
}