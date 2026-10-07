using System.Text.Json;
using System.Text.RegularExpressions;

namespace ERP.Application.Fiscal.Focus;

/// <summary>
/// Converte status HTTP + corpo em FocusResponse. Funcao pura, sem I/O.
/// Tolerante: corpo vazio, HTML de erro (502/503), JSON malformado ou um
/// JSON que nao e objeto NUNCA lancam excecao; os campos ficam nulos e
/// RawBody preserva o que veio.
/// </summary>
public static class FocusResponseParser
{
    private static readonly Regex ChaveComPrefixoNfe =
        new("^NFe([0-9]{44})$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex ChaveSoDigitos =
        new("^[0-9]{44}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static FocusResponse FromHttp(int httpStatus, string? body)
    {
        var raw = body ?? string.Empty;
        var baseResponse = new FocusResponse { HttpStatus = httpStatus, RawBody = raw };

        if (string.IsNullOrWhiteSpace(raw))
            return baseResponse;

        try
        {
            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return baseResponse;

            var chaveBruta = Text(root, "chave_nfe");

            return baseResponse with
            {
                Codigo = Text(root, "codigo"),
                Mensagem = Text(root, "mensagem"),
                Status = Text(root, "status"),
                StatusSefaz = Text(root, "status_sefaz"),
                MensagemSefaz = Text(root, "mensagem_sefaz"),
                ChaveNfeBruta = chaveBruta,
                ChaveNfe = NormalizarChave(chaveBruta),
                Numero = Text(root, "numero"),
                Serie = Text(root, "serie"),
                Protocolo = Text(root, "protocolo"),
                CaminhoXmlNotaFiscal = Text(root, "caminho_xml_nota_fiscal"),
                CaminhoDanfe = Text(root, "caminho_danfe")
            };
        }
        catch (JsonException)
        {
            return baseResponse;
        }
    }

    public static FocusResponse FromTransportError(string? message)
    {
        return new FocusResponse
        {
            HttpStatus = 0,
            TransportError = string.IsNullOrWhiteSpace(message)
                ? "Falha de transporte sem detalhe."
                : message
        };
    }

    /// <summary>
    /// Normalizacao extremamente conservadora da chave de acesso:
    ///   "NFe" + 44 digitos -> os 44 digitos
    ///   44 digitos         -> os mesmos 44 digitos
    ///   qualquer outra coisa -> nulo (nunca tenta "consertar" chave fiscal;
    ///   o valor original continua em ChaveNfeBruta e em RawBody).
    /// Mais estrito de proposito que as 3 copias antigas de LimparChaveAcesso,
    /// que nao sao alteradas nesta etapa.
    /// </summary>
    public static string? NormalizarChave(string? bruta)
    {
        if (string.IsNullOrWhiteSpace(bruta))
            return null;

        var texto = bruta.Trim();

        var comPrefixo = ChaveComPrefixoNfe.Match(texto);
        if (comPrefixo.Success)
            return comPrefixo.Groups[1].Value;

        return ChaveSoDigitos.IsMatch(texto) ? texto : null;
    }

    private static string? Text(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var prop))
            return null;

        return prop.ValueKind switch
        {
            JsonValueKind.String => prop.GetString(),
            JsonValueKind.Number => prop.GetRawText(),
            _ => null
        };
    }
}
