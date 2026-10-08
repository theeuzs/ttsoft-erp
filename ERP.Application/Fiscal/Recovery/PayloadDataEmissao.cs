using System.Text;
using System.Text.Json;

namespace ERP.Application.Fiscal.Recovery;

/// <summary>
/// Leitura e troca da DataEmissao dentro do PayloadJson congelado de uma
/// NfePendente (Etapa 4A-5a). Funcao pura, sem I/O e SEM chamar a Focus.
///
/// A troca e CIRURGICA, no texto: localiza (por bytes, com Utf8JsonReader) o
/// valor da propriedade de primeiro nivel "DataEmissao" e substitui apenas
/// esse literal. Todo o resto do payload sai byte a byte igual: nao ha
/// desserializacao nem reserializacao, entao nao existe a chance de mudar
/// escapes, acentos, formato de numero, ordem de campos, nulos explicitos ou
/// campos que a classe atual ja nao conheca.
///
/// O payload guardado e o do Newtonsoft em PascalCase (JsonConvert.
/// SerializeObject(request), FiscalService), nao o JSON de fio (snake_case).
///
/// Reversivel: Substituir(Substituir(p, nova), dataOriginal) == p. Entao, quem
/// guardar a data original consegue reconstituir o payload original exato.
/// </summary>
public static class PayloadDataEmissao
{
    public const string NomePropriedade = "DataEmissao";

    private static readonly byte[] NomeUtf8 = Encoding.UTF8.GetBytes(NomePropriedade);

    private enum Localizacao
    {
        Ok,
        Ausente,        // nao ha DataEmissao de primeiro nivel
        Ambigua,        // mais de uma de primeiro nivel
        NaoEhTexto,     // existe, mas o valor nao e string JSON
        Malformado      // JSON invalido (ou token inesperado)
    }

    /// <summary>Texto bruto da DataEmissao de primeiro nivel, ou nulo.</summary>
    public static string? LerTexto(string? payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
            return null;

        var bytes = Encoding.UTF8.GetBytes(payloadJson);
        return Localizar(bytes, out _, out _, out var texto) == Localizacao.Ok ? texto : null;
    }

    /// <summary>
    /// DataEmissao interpretada (formato estrito yyyy-MM-ddTHH:mm:sszzz, com
    /// offset), ou nulo se ausente, nao textual, ambigua, malformada ou sem
    /// offset valido.
    /// </summary>
    public static DateTimeOffset? Ler(string? payloadJson) =>
        DataEmissaoRegeneracao.ParseDataEmissao(LerTexto(payloadJson));

    /// <summary>
    /// Devolve o payload com SOMENTE o valor de DataEmissao trocado. Nunca
    /// cria a propriedade: se nao houver exatamente uma DataEmissao textual de
    /// primeiro nivel, lanca InvalidOperationException. A nova data precisa
    /// estar no formato estrito com offset, senao ArgumentException (e nada e
    /// alterado).
    /// </summary>
    public static string Substituir(string payloadJson, string novaDataEmissao)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
            throw new ArgumentException("Payload vazio.", nameof(payloadJson));

        if (DataEmissaoRegeneracao.ParseDataEmissao(novaDataEmissao) is null)
        {
            throw new ArgumentException(
                "Nova DataEmissao invalida (esperado yyyy-MM-ddTHH:mm:sszzz, com offset).",
                nameof(novaDataEmissao));
        }

        var bytes = Encoding.UTF8.GetBytes(payloadJson);
        var resultado = Localizar(bytes, out var inicio, out var fim, out _);

        if (resultado != Localizacao.Ok)
        {
            throw new InvalidOperationException(
                $"Nao foi possivel localizar uma unica DataEmissao textual de primeiro nivel no payload ({resultado}).");
        }

        // O formato estrito so tem caracteres ASCII seguros: nao precisa de escape.
        var novoValor = Encoding.UTF8.GetBytes("\"" + novaDataEmissao.Trim() + "\"");

        var saida = new byte[bytes.Length - (fim - inicio) + novoValor.Length];
        Buffer.BlockCopy(bytes, 0, saida, 0, inicio);
        Buffer.BlockCopy(novoValor, 0, saida, inicio, novoValor.Length);
        Buffer.BlockCopy(bytes, fim, saida, inicio + novoValor.Length, bytes.Length - fim);

        return Encoding.UTF8.GetString(saida);
    }

    /// <summary>
    /// Percorre o JSON e acha o literal string do valor de "DataEmissao" no
    /// primeiro nivel (profundidade 1). inicio/fim sao offsets de bytes do
    /// literal INCLUINDO as aspas (fim exclusivo).
    /// </summary>
    private static Localizacao Localizar(byte[] utf8, out int inicio, out int fim, out string? texto)
    {
        inicio = -1;
        fim = -1;
        texto = null;

        var achados = 0;
        var valorNaoTextual = false;

        try
        {
            var reader = new Utf8JsonReader(utf8);

            while (reader.Read())
            {
                if (reader.TokenType != JsonTokenType.PropertyName
                    || reader.CurrentDepth != 1
                    || !reader.ValueTextEquals(NomeUtf8))
                {
                    continue;
                }

                achados++;

                if (!reader.Read())
                    return Localizacao.Malformado;

                if (reader.TokenType != JsonTokenType.String)
                {
                    valorNaoTextual = true;
                    continue;
                }

                inicio = (int)reader.TokenStartIndex;
                fim = (int)reader.BytesConsumed;
                texto = reader.GetString();
            }
        }
        catch (JsonException)
        {
            return Localizacao.Malformado;
        }

        if (achados == 0)
            return Localizacao.Ausente;

        if (achados > 1)
            return Localizacao.Ambigua;

        if (valorNaoTextual || inicio < 0)
            return Localizacao.NaoEhTexto;

        // Defesa: o trecho tem que ser mesmo um literal entre aspas. Se alguma
        // suposicao sobre os offsets do leitor falhar, NAO se corrompe o payload.
        if (fim - inicio < 2 || utf8[inicio] != (byte)'"' || utf8[fim - 1] != (byte)'"')
            return Localizacao.Malformado;

        return Localizacao.Ok;
    }
}
