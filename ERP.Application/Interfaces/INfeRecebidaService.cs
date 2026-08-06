using ERP.Application.DTOs;

namespace ERP.Application.Interfaces;

/// <summary>
/// Item MD-e do roadmap fiscal (P1) — notas de fornecedor descobertas
/// automaticamente via Focus NFe (Manifestação do Destinatário), sem
/// precisar que o fornecedor mande o XML por e-mail.
/// </summary>
public interface INfeRecebidaService
{
    /// <summary>Consulta a Focus por notas novas (versão maior que a última
    /// vista) e salva localmente. Retorna quantas foram encontradas.</summary>
    Task<int> BuscarNovasAsync();

    Task<IReadOnlyList<NfeRecebidaDto>> ListarAsync();

    /// <param name="tipo">"ciencia", "confirmacao", "desconhecimento" ou "nao_realizada".</param>
    Task ManifestarAsync(Guid id, string tipo, string? justificativa = null);

    /// <summary>Baixa o XML completo (só disponível depois de dar ciência)
    /// e salva num arquivo temporário local — devolve o caminho, pra
    /// alimentar o NfeImportService que já existe.</summary>
    Task<string> BaixarXmlParaImportacaoAsync(Guid id);
}
