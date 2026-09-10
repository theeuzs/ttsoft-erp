namespace ERP.Application.Interfaces;

/// <summary>Mesmo padrão do IFiscalConfigurationProvider — uma leitura, um
/// gravação, sempre no tenant atual (resolvido pelo IRequestTenant/DbContext
/// de quem implementa).</summary>
public interface ITenantFeatureFlagsProvider
{
    Task<TenantFeatureFlagsDto> ObterAsync();
    Task SalvarAsync(TenantFeatureFlagsDto flags);
}

public class TenantFeatureFlagsDto
{
    public bool MetasVendasHabilitado      { get; set; }
    public bool PontosFidelidadeHabilitado { get; set; }
}
