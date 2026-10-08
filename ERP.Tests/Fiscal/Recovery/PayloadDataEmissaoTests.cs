using System.Text.Json;
using ERP.Application.DTOs.FocusNfe;
using ERP.Application.Fiscal.Recovery;
using FluentAssertions;
using Xunit;

namespace ERP.Tests.Fiscal;

/// <summary>
/// Etapa 4A-5a. Prova que a troca da DataEmissao altera SOMENTE esse valor:
/// (1) no texto do PayloadJson guardado (byte a byte) e (2) no JSON que de fato
/// vai para a Focus (System.Text.Json, snake_case), comparado como texto e campo
/// a campo, e nao apenas como objetos .NET. Nenhum teste chama a Focus.
/// </summary>
public class PayloadDataEmissaoTests
{
    private const string DataAntiga = "2026-10-08T09:01:12-03:00";
    private const string DataNova = "2026-10-08T09:07:45-03:00";

    private static readonly DateTimeOffset AntigaComoOffset =
        new(2026, 10, 8, 9, 1, 12, TimeSpan.FromHours(-3));

    private static readonly DateTimeOffset NovaComoOffset =
        new(2026, 10, 8, 9, 7, 45, TimeSpan.FromHours(-3));

    // Formato do que JsonConvert.SerializeObject(FocusNfceRequest) gera em producao
    // (FiscalService, linha 120): PascalCase, compacto, nulos explicitos, na ordem da classe.
    // Tem acentos crus e escapados, aspas e barras escapadas, e textos com cara de data.
    private const string PayloadCompleto = """
        {"CnpjEmitente":"12820608000141","NaturezaOperacao":"VENDA AO CONSUMIDOR","DataEmissao":"2026-10-08T09:01:12-03:00","TipoDocumento":"1","PresencaComprador":"1","ConsumidorFinal":"1","FinalidadeEmissao":"1","ModalidadeFrete":"9","ValorFrete":null,"CnpjTransportador":null,"CpfTransportador":null,"NomeTransportador":null,"InscricaoEstadualTransportador":null,"EnderecoTransportador":null,"MunicipioTransportador":null,"UfTransportador":null,"VeiculoPlaca":null,"VeiculoUf":null,"Volumes":null,"InformacoesAdicionaisContribuinte":"Venda de 08/10/2026 – vendedor João | ref 2026-10-08T09:00:00-03:00 | \"balcão\" C:\\caixa\\1 | \u00e7\u00e3o","Nome":"CONSUMIDOR FINAL","CpfCnpj":null,"LogradouroDestinatario":null,"NumeroDestinatario":null,"BairroDestinatario":null,"MunicipioDestinatario":null,"UfDestinatario":null,"CepDestinatario":null,"IeDestinatario":null,"IndicadorIeDestinatario":null,"Itens":[{"NumeroItem":"1","CodigoProduto":"1001","Descricao":"PARAFUSO SEXTAVADO 1/4\" X 2\" AÇO INOX","Cfop":"5102","UnidadeComercial":"UN","QuantidadeComercial":"10.0000","ValorUnitarioComercial":"1.5000","ValorBruto":"15.00","CodigoNcm":"73181500","IcmsOrigem":"0","IcmsSituacaoTributaria":"102","IcmsModalidadeBaseCalculoSt":null,"IcmsMargemValorAdicionadoSt":null,"IcmsBaseCalculoSt":null,"IcmsAliquotaSt":null,"IcmsValorSt":null,"PisSituacaoTributaria":"07","CofinsSituacaoTributaria":"07","ChaveAcessoDfeReferenciado":null,"NumeroItemDfeReferenciado":null},{"NumeroItem":"2","CodigoProduto":"2002","Descricao":"TORNEIRA JARDIM 1/2 CROMADA","Cfop":"5102","UnidadeComercial":"PC","QuantidadeComercial":"1.0000","ValorUnitarioComercial":"39.9000","ValorBruto":"39.90","CodigoNcm":"84818099","IcmsOrigem":"0","IcmsSituacaoTributaria":"102","IcmsModalidadeBaseCalculoSt":null,"IcmsMargemValorAdicionadoSt":null,"IcmsBaseCalculoSt":null,"IcmsAliquotaSt":null,"IcmsValorSt":null,"PisSituacaoTributaria":"07","CofinsSituacaoTributaria":"07","ChaveAcessoDfeReferenciado":null,"NumeroItemDfeReferenciado":null}],"Pagamentos":[{"FormaPagamento":"01","ValorPagamento":"54.90"}],"NotasReferenciadas":null}
        """;

    private static int Ocorrencias(string texto, string trecho)
    {
        var n = 0;
        var i = 0;
        while ((i = texto.IndexOf(trecho, i, StringComparison.Ordinal)) >= 0)
        {
            n++;
            i += trecho.Length;
        }
        return n;
    }

    /// <summary>Esperado de uma troca correta: o payload com a data antiga (que aparece UMA vez) trocada pela nova.</summary>
    private static string Esperado(string payload)
    {
        Ocorrencias(payload, DataAntiga).Should().Be(1, "o teste so e valido se a data antiga aparece uma unica vez");
        return payload.Replace(DataAntiga, DataNova);
    }

    /// <summary>
    /// Mesmo caminho de producao ate o fio: o HostedService desserializa o PayloadJson
    /// com Newtonsoft (padrao) e o FocusNfeHttpClient serializa com System.Text.Json
    /// (padrao), tipado como object. Devolve o corpo que iria para a Focus.
    /// </summary>
    private static string CorpoNoFio(string payloadPersistido)
    {
        var request = Newtonsoft.Json.JsonConvert.DeserializeObject<FocusNfceRequest>(payloadPersistido);
        request.Should().NotBeNull();
        return System.Text.Json.JsonSerializer.Serialize((object)request!);
    }

    /// <summary>Compara dois JSON de objeto campo a campo (mesma lista e ordem de nomes) e devolve os nomes cujo valor difere.</summary>
    private static List<string> CamposDiferentes(string jsonA, string jsonB)
    {
        using var a = JsonDocument.Parse(jsonA);
        using var b = JsonDocument.Parse(jsonB);

        var nomesA = a.RootElement.EnumerateObject().Select(p => p.Name).ToList();
        var nomesB = b.RootElement.EnumerateObject().Select(p => p.Name).ToList();
        nomesA.Should().Equal(nomesB, "os nomes e a ordem dos campos no fio nao podem mudar");

        return a.RootElement.EnumerateObject()
            .Where(p => p.Value.GetRawText() != b.RootElement.GetProperty(p.Name).GetRawText())
            .Select(p => p.Name)
            .ToList();
    }

    // ── Leitura ──────────────────────────────────────────────────────────

    [Fact(DisplayName = "LerTexto devolve o texto bruto da DataEmissao de primeiro nivel")]
    public void LerTexto_DevolveTextoBruto()
    {
        PayloadDataEmissao.LerTexto(PayloadCompleto).Should().Be(DataAntiga);
    }

    [Fact(DisplayName = "Ler devolve a data com o offset do payload")]
    public void Ler_DevolveComOffset()
    {
        var data = PayloadDataEmissao.Ler(PayloadCompleto);

        data.Should().Be(AntigaComoOffset);
        data!.Value.Offset.Should().Be(TimeSpan.FromHours(-3));
    }

    [Fact(DisplayName = "Ler devolve nulo quando o texto nao tem offset valido")]
    public void Ler_SemOffset_Nulo()
    {
        PayloadDataEmissao.Ler("{\"DataEmissao\":\"2026-10-08T09:01:12\"}").Should().BeNull();
        PayloadDataEmissao.LerTexto("{\"DataEmissao\":\"2026-10-08T09:01:12\"}").Should().Be("2026-10-08T09:01:12");
    }

    // ── Troca: so o valor muda, byte a byte ──────────────────────────────

    [Fact(DisplayName = "Substituir troca so o valor: o resto do payload completo fica byte a byte igual")]
    public void Substituir_ResultadoIgualAoOriginalComSoADataTrocada()
    {
        var resultado = PayloadDataEmissao.Substituir(PayloadCompleto, DataNova);

        resultado.Should().Be(Esperado(PayloadCompleto));
        resultado.Length.Should().Be(PayloadCompleto.Length);
        PayloadDataEmissao.Ler(resultado).Should().Be(NovaComoOffset);
    }

    [Fact(DisplayName = "Substituir e reversivel: voltar a data original reconstitui o payload original exato")]
    public void Substituir_EhReversivel()
    {
        var trocado = PayloadDataEmissao.Substituir(PayloadCompleto, DataNova);
        var devolvido = PayloadDataEmissao.Substituir(trocado, DataAntiga);

        devolvido.Should().Be(PayloadCompleto);
    }

    [Fact(DisplayName = "Substituir com a mesma data devolve o payload identico")]
    public void Substituir_MesmaData_Identico()
    {
        PayloadDataEmissao.Substituir(PayloadCompleto, DataAntiga).Should().Be(PayloadCompleto);
    }

    [Fact(DisplayName = "Substituir aceita a nova data com espacos nas pontas e grava sem eles")]
    public void Substituir_NovaDataComEspacos()
    {
        PayloadDataEmissao.Substituir(PayloadCompleto, "  " + DataNova + "  ").Should().Be(Esperado(PayloadCompleto));
    }

    [Theory(DisplayName = "Substituir funciona com a propriedade no inicio, no meio e no fim, sem mexer no resto")]
    [InlineData("{\"DataEmissao\":\"2026-10-08T09:01:12-03:00\",\"B\":1,\"C\":\"x\"}")]
    [InlineData("{\"A\":1,\"DataEmissao\":\"2026-10-08T09:01:12-03:00\",\"C\":\"x\"}")]
    [InlineData("{\"A\":1,\"B\":\"x\",\"DataEmissao\":\"2026-10-08T09:01:12-03:00\"}")]
    [InlineData("{\"DataEmissao\":\"2026-10-08T09:01:12-03:00\"}")]
    public void Substituir_Posicoes(string payload)
    {
        PayloadDataEmissao.Substituir(payload, DataNova).Should().Be(Esperado(payload));
    }

    [Fact(DisplayName = "Substituir preserva espacos, quebras de linha CRLF e tabs de um payload formatado")]
    public void Substituir_PreservaFormatacao()
    {
        var payload = "{\r\n\t\"A\" : 1 ,\r\n\t\"DataEmissao\"   :   \"" + DataAntiga + "\" ,\r\n\t\"B\":[ 1 , 2 ]\r\n}\r\n";

        PayloadDataEmissao.Substituir(payload, DataNova).Should().Be(Esperado(payload));
    }

    [Fact(DisplayName = "Substituir preserva um payload indentado (reformatado pelo System.Text.Json)")]
    public void Substituir_PayloadIndentado()
    {
        using var doc = JsonDocument.Parse(PayloadCompleto);
        var indentado = System.Text.Json.JsonSerializer.Serialize(
            doc.RootElement, new JsonSerializerOptions { WriteIndented = true });

        PayloadDataEmissao.Substituir(indentado, DataNova).Should().Be(Esperado(indentado));
    }

    [Fact(DisplayName = "Substituir nao normaliza numeros: 1.50, 1E3, -0.0, inteiro enorme e 0.10 ficam como estavam")]
    public void Substituir_PreservaNumeros()
    {
        var payload = "{\"A\":1.50,\"B\":1E3,\"C\":-0.0,\"D\":10000000000000000000000,\"E\":0.10," +
                      "\"DataEmissao\":\"" + DataAntiga + "\",\"F\":[1.0,2.00],\"G\":{\"H\":null}}";

        PayloadDataEmissao.Substituir(payload, DataNova).Should().Be(Esperado(payload));
    }

    [Fact(DisplayName = "Substituir preserva emoji, acentos, aspas tipograficas e escapes \\uXXXX como estavam")]
    public void Substituir_PreservaUnicodeEEscapes()
    {
        var payload = "{\"DataEmissao\":\"" + DataAntiga + "\",\"T\":\"😀 ç ã – “aspas” \\ud83d\\ude00 \\u00e7\\n\\t \\\"x\\\" \\\\\"}";

        PayloadDataEmissao.Substituir(payload, DataNova).Should().Be(Esperado(payload));
    }

    [Fact(DisplayName = "Substituir troca so o literal da propriedade, nao outro texto igual a data (nao e um Replace cego)")]
    public void Substituir_NaoTrocaOutroTextoIgual()
    {
        var payload = "{\"DataEmissao\":\"" + DataAntiga + "\",\"Obs\":\"emitida em " + DataAntiga + "\"}";

        PayloadDataEmissao.Substituir(payload, DataNova).Should()
            .Be("{\"DataEmissao\":\"" + DataNova + "\",\"Obs\":\"emitida em " + DataAntiga + "\"}");
    }

    [Fact(DisplayName = "Substituir nao toca em DataEmissao aninhada (so a de primeiro nivel)")]
    public void Substituir_NaoTocaAninhada()
    {
        var payload = "{\"Itens\":[{\"DataEmissao\":\"2000-01-01T00:00:00-03:00\"}]," +
                      "\"X\":{\"DataEmissao\":\"2001-01-01T00:00:00-03:00\"}," +
                      "\"DataEmissao\":\"" + DataAntiga + "\"}";

        PayloadDataEmissao.Substituir(payload, DataNova).Should().Be(
            "{\"Itens\":[{\"DataEmissao\":\"2000-01-01T00:00:00-03:00\"}]," +
            "\"X\":{\"DataEmissao\":\"2001-01-01T00:00:00-03:00\"}," +
            "\"DataEmissao\":\"" + DataNova + "\"}");
    }

    // ── O que NUNCA acontece: criar, adivinhar ou corromper ──────────────

    [Fact(DisplayName = "So existe DataEmissao aninhada: Ler devolve nulo e Substituir lanca (nunca cria a propriedade)")]
    public void SoAninhada_NaoCria()
    {
        var payload = "{\"Itens\":[{\"DataEmissao\":\"" + DataAntiga + "\"}]}";

        PayloadDataEmissao.Ler(payload).Should().BeNull();
        var act = () => PayloadDataEmissao.Substituir(payload, DataNova);
        act.Should().Throw<InvalidOperationException>();
    }

    [Theory(DisplayName = "Sem DataEmissao de primeiro nivel (ausente, raiz nao-objeto): Ler nulo e Substituir lanca")]
    [InlineData("{\"A\":1}")]
    [InlineData("{}")]
    [InlineData("[\"DataEmissao\"]")]
    [InlineData("\"DataEmissao\"")]
    [InlineData("{\"dataemissao\":\"2026-10-08T09:01:12-03:00\"}")]
    [InlineData("{\"data_emissao\":\"2026-10-08T09:01:12-03:00\"}")]
    public void Ausente(string payload)
    {
        PayloadDataEmissao.Ler(payload).Should().BeNull();
        var act = () => PayloadDataEmissao.Substituir(payload, DataNova);
        act.Should().Throw<InvalidOperationException>();
    }

    [Theory(DisplayName = "DataEmissao que nao e texto (null, numero, objeto, array, booleano): Ler nulo e Substituir lanca")]
    [InlineData("{\"DataEmissao\":null}")]
    [InlineData("{\"DataEmissao\":20261008}")]
    [InlineData("{\"DataEmissao\":{\"a\":1}}")]
    [InlineData("{\"DataEmissao\":[\"x\"]}")]
    [InlineData("{\"DataEmissao\":true}")]
    public void NaoTextual(string payload)
    {
        PayloadDataEmissao.Ler(payload).Should().BeNull();
        var act = () => PayloadDataEmissao.Substituir(payload, DataNova);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact(DisplayName = "Duas DataEmissao de primeiro nivel: ambiguo, Ler nulo e Substituir lanca")]
    public void Duplicada()
    {
        var payload = "{\"DataEmissao\":\"" + DataAntiga + "\",\"DataEmissao\":\"2026-10-08T09:02:00-03:00\"}";

        PayloadDataEmissao.Ler(payload).Should().BeNull();
        var act = () => PayloadDataEmissao.Substituir(payload, DataNova);
        act.Should().Throw<InvalidOperationException>();
    }

    [Theory(DisplayName = "JSON malformado: Ler nulo e Substituir lanca, sem corromper nada")]
    [InlineData("not json")]
    [InlineData("{")]
    [InlineData("[")]
    [InlineData("{\"DataEmissao\":\"2026-10-08T09:01:12-03:00\"")]
    [InlineData("{\"DataEmissao\":\"2026-10-08T09:01:12-03:00\",}")]
    [InlineData("{\"DataEmissao\":\"2026-10-08T09:01:12-03:00\"} extra")]
    [InlineData("{\"DataEmissao\":\"2026-10-08T09:01:12-03:00\",\"B\":}")]
    public void Malformado(string payload)
    {
        PayloadDataEmissao.Ler(payload).Should().BeNull();
        var act = () => PayloadDataEmissao.Substituir(payload, DataNova);
        act.Should().Throw<InvalidOperationException>();
    }

    [Theory(DisplayName = "Payload nulo, vazio ou em branco: Ler nulo e Substituir lanca ArgumentException")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void PayloadVazio(string? payload)
    {
        PayloadDataEmissao.Ler(payload).Should().BeNull();
        PayloadDataEmissao.LerTexto(payload).Should().BeNull();
        var act = () => PayloadDataEmissao.Substituir(payload!, DataNova);
        act.Should().Throw<ArgumentException>();
    }

    [Theory(DisplayName = "Nova data invalida (sem offset, com Z, em texto, vazia): ArgumentException e nada e escrito")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("hoje")]
    [InlineData("2026-10-08T09:07:45")]
    [InlineData("2026-10-08T09:07:45Z")]
    [InlineData("08/10/2026 09:07:45 -03:00")]
    [InlineData("2026-10-08T09:07:45-03:00\"},{\"X\":\"y")]
    public void NovaDataInvalida(string? nova)
    {
        var act = () => PayloadDataEmissao.Substituir(PayloadCompleto, nova!);

        act.Should().Throw<ArgumentException>();
    }

    // ── O que realmente vai para a Focus (formato no fio) ────────────────

    [Fact(DisplayName = "FIO: o corpo enviado so difere do original em data_emissao (como texto exato)")]
    public void Fio_SoDataEmissaoMuda_ComoTexto()
    {
        var regenerado = PayloadDataEmissao.Substituir(PayloadCompleto, DataNova);

        var fioOriginal = CorpoNoFio(PayloadCompleto);
        var fioRegenerado = CorpoNoFio(regenerado);

        Ocorrencias(fioOriginal, DataAntiga).Should().Be(1);
        fioRegenerado.Should().Be(fioOriginal.Replace(DataAntiga, DataNova));
        fioRegenerado.Should().Contain("\"data_emissao\":\"" + DataNova + "\"");
        fioRegenerado.Should().NotContain(DataAntiga);
    }

    [Fact(DisplayName = "FIO: campo a campo, a unica diferenca e data_emissao; nomes e ordem dos campos intactos")]
    public void Fio_SoDataEmissaoMuda_CampoACampo()
    {
        var regenerado = PayloadDataEmissao.Substituir(PayloadCompleto, DataNova);

        var diferentes = CamposDiferentes(CorpoNoFio(PayloadCompleto), CorpoNoFio(regenerado));

        diferentes.Should().Equal("data_emissao");
    }

    [Fact(DisplayName = "FIO: itens e pagamentos (valores fiscais e comerciais) ficam identicos")]
    public void Fio_ItensEPagamentosIntactos()
    {
        var regenerado = PayloadDataEmissao.Substituir(PayloadCompleto, DataNova);

        using var a = JsonDocument.Parse(CorpoNoFio(PayloadCompleto));
        using var b = JsonDocument.Parse(CorpoNoFio(regenerado));

        foreach (var nome in new[] { "itens", "pagamentos", "informacoes_adicionais_contribuinte", "cnpj_emitente", "natureza_operacao" })
        {
            b.RootElement.GetProperty(nome).GetRawText()
                .Should().Be(a.RootElement.GetProperty(nome).GetRawText(), $"campo {nome}");
        }
    }

    [Fact(DisplayName = "FIO: com um payload gerado pela propria classe (Newtonsoft padrao, como em producao), so data_emissao muda")]
    public void Fio_PayloadGeradoPelaClasse()
    {
        var request = new FocusNfceRequest
        {
            DataEmissao = DataAntiga,
            NaturezaOperacao = "VENDA AO CONSUMIDOR",
            Nome = "CONSUMIDOR FINAL",
            InformacoesAdicionaisContribuinte = "Obs com \"aspas\", barra \\ e acento: ação 2026-10-08T09:00:00-03:00",
            Itens = new List<FocusItemRequest>
            {
                new()
                {
                    NumeroItem = "1", CodigoProduto = "1001", Descricao = "PARAFUSO 1/4\" X 2\" AÇO",
                    Cfop = "5102", UnidadeComercial = "UN", QuantidadeComercial = "10.0000",
                    ValorUnitarioComercial = "1.5000", ValorBruto = "15.00", CodigoNcm = "73181500",
                    IcmsOrigem = "0", IcmsSituacaoTributaria = "102",
                    PisSituacaoTributaria = "07", CofinsSituacaoTributaria = "07"
                }
            },
            Pagamentos = new List<FocusPagamentoRequest>
            {
                new() { FormaPagamento = "01", ValorPagamento = "15.00" }
            }
        };

        // Mesma chamada do registro da pendencia (FiscalService, linha 120).
        var payload = Newtonsoft.Json.JsonConvert.SerializeObject(request);
        var regenerado = PayloadDataEmissao.Substituir(payload, DataNova);

        regenerado.Should().Be(Esperado(payload));
        PayloadDataEmissao.Ler(regenerado).Should().Be(NovaComoOffset);
        CamposDiferentes(CorpoNoFio(payload), CorpoNoFio(regenerado)).Should().Equal("data_emissao");
        CorpoNoFio(regenerado).Should().Be(CorpoNoFio(payload).Replace(DataAntiga, DataNova));
    }
}
