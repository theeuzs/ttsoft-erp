using ERP.Domain.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ERP.Domain.Interfaces;

public interface IContaReceberRepository
{
    Task AddAsync(ContaReceber entity);
    Task<IEnumerable<ContaReceber>> GetAllAsync();
    Task<IEnumerable<ContaReceber>> GetBySaleIdAsync(Guid saleId);
    void Update(ContaReceber entity);

    /// <summary>
    /// Soma ao vivo do saldo devedor pendente do cliente (ValorTotal-ValorRecebido-
    /// ValorDesconto de toda conta Pendente) — Customer.SaldoDevedor existe no
    /// schema mas não é mantido em lugar nenhum, então não pode ser usado pra
    /// decisão nenhuma (nem bloqueio, nem aviso); isso calcula na hora, sempre correto.
    /// </summary>
    Task<decimal> GetSaldoDevedorAtualAsync(Guid customerId);
}