// ── ERP.Application/Interfaces/IImportacaoService.cs ────────────────────────
using ERP.Application.DTOs;

namespace ERP.Application.Interfaces;

/// <summary>Importador de dados de sistema anterior — a ferramenta que falta
/// pra destravar a implantação de cliente novo sem trabalho manual. Fluxo
/// em 2 passos, sempre: (1) ler e validar sem gravar nada (PreVisualizarAsync),
/// (2) confirmar e gravar de verdade (ConfirmarImportacaoAsync) — o usuário
/// sempre vê o que vai acontecer antes de acontecer.</summary>
public interface IImportacaoService
{
    /// <summary>Lê o arquivo (detecta separador/codificação sozinho), tenta
    /// casar as colunas do arquivo com os campos do sistema, e valida cada
    /// linha — sem gravar nada no banco ainda.</summary>
    Task<PreviaImportacaoDto> PreVisualizarAsync(byte[] conteudoArquivo, TipoImportacao tipo);

    /// <summary>Grava de verdade as linhas sem erro da prévia já validada.
    /// Linhas com erro são sempre puladas, nunca gravadas parcialmente.</summary>
    Task<ResultadoImportacaoDto> ConfirmarImportacaoAsync(PreviaImportacaoDto previa);
}
