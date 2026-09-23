using ERP.Application.DTOs;
using System;
using System.Threading.Tasks;
using ERP.Domain.Enums;

namespace ERP.Application.Interfaces;

public interface ICaixaService
{
    // 🟢 Agora recebe o ID do usuário
    Task<CaixaDto?> ObterCaixaAbertoAsync(Guid usuarioId);
    
    Task AbrirCaixaAsync(AbrirCaixaDto dto);

    // 🟢 Registra movimento no caixa DO USUÁRIO
    // S{N} FIX — achado testando Fase C: autorizadorToken é usado só pela
    // implementação HTTP (ver HttpCaixaService) — quando um gerente/admin
    // autoriza uma Sangria/Suprimento de um Vendedor sem a permissão, o
    // token DELE (obtido separadamente, sem trocar a identidade da
    // chamada) vai aqui, pro servidor validar como PROVA de autorização,
    // sem nunca virar "quem" fez a chamada (isso continua sendo usuarioId).
    // A implementação local (CaixaService, server-side) ignora — já roda
    // no servidor, não tem esse conceito de "chamada HTTP separada".
    Task RegistrarMovimentoAsync(Guid usuarioId, decimal valor, string descricao,
                                  PaymentMethod formaPagamento, TipoMovimentoCaixa tipo,
                                  decimal maxSangriaValue = 0m, Guid? vendaId = null, Guid? salePaymentId = null,
                                  string? autorizadorToken = null);

    // 🟢 Fecha o caixa DO USUÁRIO
    Task FecharCaixaAsync(Guid usuarioId);

    /// <summary>Idempotência financeira granular (achado de auditoria pré-Fase-2
    /// do Offline-First, 08/2026) — checa por linha de pagamento específica.</summary>
    Task<bool> ExisteMovimentoParaSalePaymentAsync(Guid salePaymentId);

    /// <summary>
    /// Fase C, módulo Caixa — resumo/extrato agregado pra uma data (hoje ou
    /// passada). Movido do WPF pro servidor: essa agregação já teve pelo
    /// menos 2 bugs reais (ver comentários em ResumoCaixaDto). Devolve null
    /// se não achar nenhum caixa pra essa data/usuário.
    /// </summary>
    Task<ResumoCaixaDto?> ObterResumoAsync(Guid usuarioId, DateTime data);
}