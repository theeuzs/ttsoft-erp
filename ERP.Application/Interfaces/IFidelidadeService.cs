namespace ERP.Application.Interfaces;

public interface IFidelidadeService
{
    /// <summary>Saldo atual de pontos do cliente.</summary>
    Task<int> GetSaldoAsync(Guid customerId);

    /// <summary>Acumula pontos baseado no total da venda (1 real = 1 ponto).</summary>
    Task AcumularPontosAsync(Guid customerId, Guid saleId, decimal totalVenda);

    /// <summary>Resgata pontos e retorna o valor de desconto equivalente (1 ponto = R$ 0,01).</summary>
    Task<decimal> ResgatarPontosAsync(Guid customerId, int pontos, string descricao = "Resgate PDV");

    /// <summary>Histórico de movimentações do cliente.</summary>
    Task<List<MovimentoPontosDto>> GetHistoricoAsync(Guid customerId, int pagina = 1, int pageSize = 20);
}

public record MovimentoPontosDto(
    Guid     Id,
    string   Tipo,
    int      Pontos,
    string   Descricao,
    DateTime Data,
    string?  NumeroVenda
);
