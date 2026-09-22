using ERP.Domain.Entities;
using System;
using System.Collections.Generic; // 🟢 Necessário para listar o histórico!
using System.Threading.Tasks;

namespace ERP.Domain.Interfaces;

public interface ICaixaRepository
{
    // Busca se existe algum caixa aberto hoje
    Task<Caixa?> GetCaixaAbertoAsync();

    // Busca o caixa aberto DE UM USUÁRIO ESPECÍFICO
    Task<Caixa?> GetCaixaAbertoByUsuarioAsync(Guid usuarioId);
    
    // Busca um caixa específico pelo ID
    Task<Caixa?> GetByIdAsync(Guid id);

    // 🟢 NOVO: Busca o histórico de TODOS os caixas para o calendário de auditoria
    Task<IEnumerable<Caixa>> GetAllAsync();

    /// <summary>
    /// Fase C — busca o caixa certo pra uma data (resumo/extrato), sem
    /// carregar todo o histórico do tenant em memória (GetAllAsync acima é
    /// isso, e não escala). Ordem de preferência, replicando exatamente o
    /// que o WPF fazia antes (ResumoCaixaViewModel.CarregarResumoAsync):
    ///   1. Se a data é hoje: o caixa ABERTO do usuário, se existir.
    ///   2. Senão: o caixa do usuário com algum movimento nessa data.
    ///   3. Senão: qualquer caixa (de qualquer usuário) com movimento nessa
    ///      data — mantém o comportamento antigo de fallback, mas nunca foi
    ///      claro qual cenário de negócio isso cobre (nenhum teste/uso
    ///      documentado exercita esse terceiro caso).
    /// </summary>
    Task<Caixa?> ObterCaixaPorDataEUsuarioAsync(DateTime data, Guid usuarioId);
    
    // Adiciona um caixa novo (Abertura)
    Task AddAsync(Caixa caixa);
    
    // Atualiza um caixa (Fechamento ou novo movimento)
    void Update(Caixa caixa);

    // Adiciona o movimento direto na tabela, ignorando a memória velha!
    Task AddMovimentoAsync(ERP.Domain.Entities.CaixaMovimento movimento);

    /// <summary>Idempotência financeira granular (achado de auditoria pré-Fase-2
    /// do Offline-First, 08/2026) — checa por linha de pagamento específica,
    /// não por venda inteira (uma venda pode ter várias linhas legítimas).</summary>
    Task<bool> ExisteMovimentoParaSalePaymentAsync(Guid salePaymentId);

    /// <summary>Achado (11/09) — cancelar venda nunca revertia o movimento de
    /// caixa: precisa achar o lançamento original (por linha de pagamento)
    /// pra saber em qual Caixa lançar o estorno.</summary>
    Task<ERP.Domain.Entities.CaixaMovimento?> ObterMovimentoOriginalAsync(Guid salePaymentId);

    // S8: saldo em dinheiro para validação de sangria (Abertura + Suprimento + VendaDinheiro − Sangria)
    Task<decimal> GetSaldoDinheiroAsync(Guid caixaId);

    /// <summary>Movimentos de TODOS os caixas (qualquer operador) num período — pro Extrato Financeiro.</summary>
    Task<IEnumerable<ERP.Domain.Entities.CaixaMovimento>> GetMovimentosPorPeriodoAsync(DateTime inicio, DateTime fim);
}