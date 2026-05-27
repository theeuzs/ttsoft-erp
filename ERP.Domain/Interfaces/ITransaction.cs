namespace ERP.Domain.Interfaces;

/// <summary>
/// Abstração de transação de banco de dados.
/// Mantém o Domain desacoplado de qualquer ORM/infra (EF Core, Dapper, etc.).
/// A implementação concreta fica em ERP.Infrastructure (EfTransaction).
/// </summary>
public interface ITransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken ct = default);
    Task RollbackAsync(CancellationToken ct = default);
}
