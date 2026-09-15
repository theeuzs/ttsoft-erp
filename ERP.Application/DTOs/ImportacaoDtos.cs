// ── ERP.Application/DTOs/ImportacaoDtos.cs ──────────────────────────────────
namespace ERP.Application.DTOs;

/// <summary>Tipo de dado sendo importado — cada um tem seu próprio conjunto
/// de colunas reconhecidas e sua própria lógica de gravação.</summary>
public enum TipoImportacao { Produtos, Clientes }

/// <summary>Uma linha do CSV já interpretada, antes de gravar — usada tanto
/// pra pré-visualização (mostrar pro usuário o que vai acontecer) quanto
/// pra gravação de verdade (mesma estrutura, evita reprocessar o arquivo).</summary>
public class LinhaImportacao
{
    public int NumeroLinha { get; set; } // 1-based, contando o cabeçalho como linha 1
    public Dictionary<string, string> ValoresBrutos { get; set; } = new();
    public List<string> Erros { get; set; } = [];
    public bool JaExiste { get; set; } // vai atualizar em vez de criar (achado por SKU/CPF)
    public bool TemErro => Erros.Count > 0;

    /// <summary>Junta os 3 primeiros valores da linha crua pra dar um
    /// resumo legível na prévia, sem precisar de coluna dinâmica na tela.</summary>
    public string Resumo => string.Join(" · ", ValoresBrutos.Values.Where(v => !string.IsNullOrWhiteSpace(v)).Take(3));

    public string StatusTexto => TemErro ? "❌ Erro" : JaExiste ? "🔄 Atualiza" : "🆕 Novo";
    public string ErrosTexto => string.Join(" ", Erros);
}

/// <summary>Resultado da leitura + validação do arquivo, antes de qualquer
/// gravação no banco — o usuário confirma isso antes de "Importar de verdade".</summary>
public class PreviaImportacaoDto
{
    public TipoImportacao TipoImportacao { get; set; }
    public List<string> ColunasDetectadasNoArquivo { get; set; } = [];
    public Dictionary<string, string?> MapeamentoDetectado { get; set; } = new(); // campo sistema -> coluna do CSV
    public List<string> CamposObrigatoriosFaltando { get; set; } = [];
    public List<LinhaImportacao> Linhas { get; set; } = [];
    public int TotalLinhas => Linhas.Count;
    public int TotalComErro => Linhas.Count(l => l.TemErro);
    public int TotalNovosRegistros => Linhas.Count(l => !l.TemErro && !l.JaExiste);
    public int TotalAtualizacoes => Linhas.Count(l => !l.TemErro && l.JaExiste);
    public bool PodeImportar => CamposObrigatoriosFaltando.Count == 0 && Linhas.Count > 0;
}

/// <summary>Resumo do que realmente aconteceu depois de confirmar a importação.</summary>
public class ResultadoImportacaoDto
{
    public int Criados { get; set; }
    public int Atualizados { get; set; }
    public int ComErro { get; set; }
    public List<string> MensagensErro { get; set; } = [];
}
