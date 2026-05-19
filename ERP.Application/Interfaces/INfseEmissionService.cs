using ERP.Domain.Entities;

namespace ERP.Application.Interfaces;

public interface INfseEmissionService
{
    /// <summary>Emite uma NFS-e via FocusNFe e persiste o resultado.</summary>
    Task<(bool Sucesso, string Mensagem, NfseEmitida? Nfse)> EmitirAsync(
        EmitirNfseDto dto, string token, bool isProducao);

    Task<(bool Sucesso, string Mensagem)> CancelarAsync(
        string referencia, string motivo, string token, bool isProducao);
}

public class EmitirNfseDto
{
    public Guid?   ClienteId        { get; set; }
    public string  TomadorNome      { get; set; } = string.Empty;
    public string? TomadorCpfCnpj   { get; set; }
    public string? TomadorEmail     { get; set; }
    public string? TomadorEndereco  { get; set; }
    public string  DescricaoServico { get; set; } = string.Empty;
    public string? CodigoServico    { get; set; }
    public string? CodigoCnae       { get; set; }
    public decimal ValorServico     { get; set; }
    public decimal AliquotaISS      { get; set; } = 2m;
    public string? CodigoMunicipio  { get; set; }
    public Guid?   VendaId          { get; set; }
}
