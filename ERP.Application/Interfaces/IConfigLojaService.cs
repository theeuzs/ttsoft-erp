// ── ERP.Application/Interfaces/IConfigLojaService.cs ─────────────────────────
namespace ERP.Application.Interfaces;

/// <summary>
/// Serviço de configuração da loja (filial matriz) — nome, CNPJ, endereço,
/// telefone. Encapsula toda a lógica de domínio e acesso a dados — controllers
/// não tocam em AppDbContext diretamente.
/// </summary>
public interface IConfigLojaService
{
    /// <summary>
    /// Config pública da loja (nome fantasia + telefone), usada por
    /// CalculadoraPublica e CatalogoPublico sem autenticação.
    /// </summary>
    Task<(string NomeFantasia, string Telefone)> GetPublicAsync(
        Guid? tenantId, CancellationToken ct = default);

    /// <summary>Configurações completas da filial matriz do tenant autenticado.</summary>
    Task<ConfigLojaDto> GetAsync(CancellationToken ct = default);

    /// <summary>Atualiza (ou cria, se ainda não existir) a filial matriz do tenant autenticado.</summary>
    Task PutAsync(ConfigLojaDto dto, CancellationToken ct = default);
}

public class ConfigLojaDto
{
    public Guid   Id       { get; set; }
    public string Nome     { get; set; } = "";
    public string CNPJ     { get; set; } = "";
    public string Endereco { get; set; } = "";
    public string Telefone { get; set; } = "";
}
