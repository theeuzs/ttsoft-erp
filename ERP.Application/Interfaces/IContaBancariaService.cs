// ── ERP.Application/Interfaces/IContaBancariaService.cs ───────────────────────
using ERP.Application.DTOs;
using ERP.Domain.Enums;

namespace ERP.Application.Interfaces;

public interface IContaBancariaService
{
    Task<IReadOnlyList<ContaBancariaDto>> ObterContasAtivasAsync();
    Task<ContaBancariaDto?> ObterPorIdAsync(Guid id);
    Task CriarContaAsync(CriarContaBancariaDto dto);
    Task InativarContaAsync(Guid id);

    Task RegistrarMovimentoAsync(
        Guid contaBancariaId, decimal valor, string descricao, TipoMovimentoContaBancaria tipo,
        OrigemMovimentoFinanceiro origemTipo = OrigemMovimentoFinanceiro.Manual, Guid? origemId = null);

    Task<IReadOnlyList<MovimentoContaBancariaDto>> ObterExtratoAsync(Guid contaBancariaId);

    /// <summary>
    /// Soma de todos os Caixas abertos agora (qualquer operador) + todas as Contas
    /// Bancárias ativas. É a visão de "quanto dinheiro a loja tem, no total, agora".
    /// </summary>
    Task<PosicaoFinanceiraDto> ObterPosicaoFinanceiraAsync();

    // ── Conciliação Bancária ──────────────────────────────────────────────────
    /// <summary>Processa um extrato OFX já lido em texto, sugerindo match pra cada linha.</summary>
    Task<IReadOnlyList<SugestaoConciliacaoDto>> ProcessarExtratoOfxAsync(Guid contaBancariaId, string conteudoOfx);

    /// <summary>Confirma que um lançamento já existente bate com a linha do extrato.</summary>
    Task ConfirmarConciliacaoAsync(Guid movimentoId, string fitId);

    /// <summary>Cria um lançamento novo (não existia no sistema) já marcado como conciliado.</summary>
    Task CriarEConciliarAsync(Guid contaBancariaId, SugestaoConciliacaoDto transacaoOfx);

    Task<IReadOnlyList<MovimentoContaBancariaDto>> ObterNaoConciliadosAsync(Guid contaBancariaId);

    // ── Conta padrão (recebe vendas PIX/Cartão automaticamente do PDV) ────────
    Task<ContaBancariaDto?> ObterContaPadraoAsync();
    Task DefinirComoContaPadraoAsync(Guid contaBancariaId);

    /// <summary>
    /// Chamado pelo PDV ao finalizar uma venda em PIX/Cartão. Lança automático
    /// na conta padrão, se existir uma configurada — não faz nada (e não lança
    /// exceção) se nenhuma conta padrão estiver definida, pra não travar a venda
    /// por causa de uma configuração financeira pendente.
    /// </summary>
    Task RegistrarRecebimentoVendaAsync(Guid? vendaId, decimal valor, string descricao);

    /// <summary>
    /// Estorna uma entrada de PIX de uma venda cancelada — cria uma SAÍDA
    /// compensatória, nunca apaga a entrada original (auditoria preservada).
    /// </summary>
    Task RegistrarEstornoVendaAsync(Guid vendaId, decimal valor, string descricao);
}