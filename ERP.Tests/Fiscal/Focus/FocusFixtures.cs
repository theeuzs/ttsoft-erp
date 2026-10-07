namespace ERP.Tests.Fiscal;

/// <summary>
/// Corpos reais da Focus capturados em 07/10/2026 (GET, producao, somente
/// leitura) e os dois corpos de 422 informados pelo suporte da Focus.
/// Campos irrelevantes para o contrato (qrcode_url, url_consulta_nf) omitidos.
/// </summary>
public static class FocusFixtures
{
    public const string ChaveNormalizada = "41260912820608000141650010000033221640357603";
    public const string ChaveBruta = "NFe" + ChaveNormalizada;

    // Caso 1: venda 5D5E1AC1, rejeitada por 704 (data atrasada).
    public const string Rejeitada704 = """
        {
          "cnpj_emitente": "12820608000141",
          "ref": "5d5e1ac1-5bf1-480e-a090-31759ea6dcc9",
          "status": "erro_autorizacao",
          "status_sefaz": "704",
          "mensagem_sefaz": "NFC-e com Data-Hora de emissao atrasada. Tolerancia de ate 5 minutos"
        }
        """;

    // Casos 2 e 3: NFC-e autorizada (o /v2/nfe devolve o mesmo corpo).
    public const string Autorizada = """
        {
          "cnpj_emitente": "12820608000141",
          "ref": "5c8078ba-ed08-4c08-87ff-0c08b4a9f758",
          "status": "autorizado",
          "status_sefaz": "100",
          "mensagem_sefaz": "Autorizado o uso da NF-e",
          "chave_nfe": "NFe41260912820608000141650010000033221640357603",
          "numero": "3322",
          "serie": "1",
          "protocolo": "141261595914973",
          "caminho_xml_nota_fiscal": "/arquivos/12820608000141_187834/202609/XMLs/41260912820608000141650010000033221640357603-nfe.xml",
          "caminho_danfe": "/notas_fiscais_consumidor/NFe41260912820608000141650010000033221640357603.html"
        }
        """;

    // Caso 4: ref inexistente.
    public const string NaoEncontrado = """
        {
          "codigo": "nao_encontrado",
          "mensagem": "Nota fiscal não encontrada"
        }
        """;

    // Caso 5: token invalido.
    public const string PermissaoNegada = """
        {
          "codigo": "permissao_negada",
          "mensagem": "Access token inválido (host: api.focusnfe.com.br)"
        }
        """;

    // Informado pelo suporte da Focus (segundo POST com a mesma ref).
    public const string PendingOperation = """
        {
          "codigo": "pending_operation",
          "mensagem": "A nota fiscal ainda está em processamento"
        }
        """;

    public const string AlreadyProcessed = """
        {
          "codigo": "already_processed",
          "mensagem": "A nota fiscal já foi autorizada"
        }
        """;

    // Sinteticos (nao capturados): usados so para testar ramos do classificador.
    public const string Processando = """
        { "status": "processando_autorizacao" }
        """;

    public const string Denegado = """
        { "status": "denegado", "status_sefaz": "301", "mensagem_sefaz": "Uso Denegado" }
        """;

    public const string Cancelado = """
        { "status": "cancelado" }
        """;

    public const string RejeicaoOutroCodigoSefaz = """
        { "status": "erro_autorizacao", "status_sefaz": "778", "mensagem_sefaz": "Rejeicao qualquer" }
        """;

    public const string Html502 = "<html><body><h1>502 Bad Gateway</h1></body></html>";
}
