// ── ERP.Domain/Interfaces/IContaBancariaRepository.cs ─────────────────────────
using ERP.Domain.Entities;
using ERP.Domain.Enums;

namespace ERP.Domain.Interfaces;

public interface IContaBancariaRepository
{
    Task<ContaBancaria?> GetByIdAsync(Guid id);
    Task<IEnumerable<ContaBancaria>> GetAllAtivasAsync();
    Task<IEnumerable<ContaBancaria>> GetAllAsync();

    Task AddAsync(ContaBancaria conta);
    void Update(ContaBancaria conta);

    Task AddMovimentoAsync(MovimentoContaBancaria movimento);
    Task<IEnumerable<MovimentoContaBancaria>> GetMovimentosAsync(Guid contaBancariaId);

    /// <summary>Saldo atual = SaldoInicial + soma de Entradas − soma de Saídas.</summary>
    Task<decimal> GetSaldoAsync(Guid contaBancariaId);

    // ── Conciliação Bancária ──────────────────────────────────────────────────
    /// <summary>
    /// Candidatos não conciliados que batem com uma linha do extrato OFX —
    /// mesmo valor/tipo, dentro de uma janela de dias (bancos às vezes postam
    /// com atraso de 1-3 dias em relação ao lançamento no sistema).
    /// </summary>
    Task<IEnumerable<MovimentoContaBancaria>> BuscarCandidatosConciliacaoAsync(
        Guid contaBancariaId, decimal valor, TipoMovimentoContaBancaria tipo,
        DateTime dataMin, DateTime dataMax);

    Task<IEnumerable<MovimentoContaBancaria>> GetNaoConciliadosAsync(Guid contaBancariaId);

    Task MarcarConciliadoAsync(Guid movimentoId, string? fitId);

    /// <summary>true se já existe um movimento (dessa conta) com esse FitId — já processado antes.</summary>
    Task<bool> FitIdJaProcessadoAsync(Guid contaBancariaId, string fitId);

    // ── Conta padrão (recebe vendas PIX/Cartão automaticamente do PDV) ────────
    Task<ContaBancaria?> GetContaPadraoAsync();

    /// <summary>Marca uma conta como padrão, desmarcando qualquer outra que já fosse.</summary>
    Task DefinirContaPadraoAsync(Guid contaBancariaId);

    /// <summary>Movimentos de TODAS as contas bancárias num período — pro Extrato Financeiro.</summary>
    Task<IEnumerable<MovimentoContaBancaria>> GetMovimentosPorPeriodoAsync(DateTime inicio, DateTime fim);
}