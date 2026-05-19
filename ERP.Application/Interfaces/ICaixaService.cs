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
    Task RegistrarMovimentoAsync(Guid usuarioId, decimal valor, string descricao, 
                                  PaymentMethod formaPagamento, TipoMovimentoCaixa tipo);

    // 🟢 Fecha o caixa DO USUÁRIO
    Task FecharCaixaAsync(Guid usuarioId);
}