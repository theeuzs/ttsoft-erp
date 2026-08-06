// ── ERP.Domain/Entities/NfeRecebida.cs ──────────────────────────────────────
using ERP.Domain.Common;

namespace ERP.Domain.Entities;

/// <summary>
/// Item MD-e do roadmap fiscal (P1) — notas fiscais de fornecedor emitidas
/// contra o CNPJ da loja, descobertas automaticamente via Focus NFe (a
/// Focus recebe da Receita), sem precisar que o fornecedor mande o XML por
/// e-mail. O ciclo de vida é: descoberta → manifestação (ciência/confirmação)
/// → download do XML → importação (reaproveita o NfeImportService que já existia).
/// </summary>
public class NfeRecebida : BaseEntity
{
    public string Chave { get; set; } = string.Empty;

    public string? CnpjEmitente { get; set; }
    public string? NomeEmitente { get; set; }
    public DateTime? DataEmissao { get; set; }
    public decimal? ValorTotal { get; set; }

    /// <summary>Número de versão retornado pela Focus — usado pra paginação
    /// incremental (só busca o que é mais novo que a última versão vista).</summary>
    public long Versao { get; set; }

    /// <summary>"Nenhuma", "Ciencia", "Confirmacao", "Desconhecimento", "NaoRealizada".</summary>
    public string StatusManifestacao { get; set; } = "Nenhuma";

    /// <summary>true depois que o XML foi baixado e a nota entrou no fluxo
    /// do NfeImportService (entrada de estoque/pedido de compra).</summary>
    public bool Importada { get; set; } = false;

    public DateTime DescobertaEm { get; set; } = DateTime.Now;
}
