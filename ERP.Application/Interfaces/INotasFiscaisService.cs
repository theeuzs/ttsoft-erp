// ── ERP.Application/Interfaces/INotasFiscaisService.cs ───────────────────────
using ERP.Application.DTOs;

namespace ERP.Application.Interfaces;

/// <summary>
/// Serviço de consulta de notas fiscais emitidas.
/// Sprint 2A: extrai o acesso ao AppDbContext que estava direto no controller.
/// Operações de emissão e cancelamento continuam nos services focusnfe específicos.
/// </summary>
public interface INotasFiscaisService
{
    /// <summary>
    /// Retorna as notas fiscais emitidas, paginadas e ordenadas por data de emissão
    /// decrescente. Filtradas pelo tenant corrente.
    /// </summary>
    Task<PagedResult<NotaFiscalDto>> GetAllAsync(
        int page     = 1,
        int pageSize = 50,
        CancellationToken ct = default);
}

/// <summary>Projeção de NfseEmitida para a API — sem campos internos de controle.</summary>
public class NotaFiscalDto
{
    public Guid     Id               { get; init; }
    public string?  NumeroNfse       { get; init; }
    public string   ReferenciaNfse   { get; init; } = string.Empty;
    public DateTime DataEmissao      { get; init; }
    public string   Status           { get; init; } = string.Empty;
    public string   TomadorNome      { get; init; } = string.Empty;
    public string?  TomadorCpfCnpj   { get; init; }
    public string   DescricaoServico { get; init; } = string.Empty;
    public decimal  ValorServico      { get; init; }
    public decimal  ValorISS          { get; init; }
    public decimal  ValorLiquido      { get; init; }
    public string?  UrlDanfse         { get; init; }
    public string?  MensagemErro      { get; init; }
    public Guid?    VendaId           { get; init; }
}
