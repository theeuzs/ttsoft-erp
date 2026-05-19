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
    
    // Adiciona um caixa novo (Abertura)
    Task AddAsync(Caixa caixa);
    
    // Atualiza um caixa (Fechamento ou novo movimento)
    void Update(Caixa caixa);

    // Adiciona o movimento direto na tabela, ignorando a memória velha!
    Task AddMovimentoAsync(ERP.Domain.Entities.CaixaMovimento movimento);
}