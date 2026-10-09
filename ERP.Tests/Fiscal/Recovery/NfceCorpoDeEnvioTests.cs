using System.Net;
using System.Net.Http;
using ERP.Application.DTOs.FocusNfe;
using ERP.Application.Fiscal.Recovery;
using ERP.Infrastructure.HttpClients;
using FluentAssertions;
using Xunit;

namespace ERP.Tests.Fiscal;

/// <summary>4A-5d: o corpo do POST da recuperacao e o MESMO que o envio de hoje produz.</summary>
public class NfceCorpoDeEnvioTests
{
    private sealed class CapturaHandler : HttpMessageHandler
    {
        public string? Corpo { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Corpo = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") };
        }
    }

    [Fact(DisplayName = "O corpo montado e IDENTICO ao que o FocusNfeHttpClient real envia para o mesmo payload")]
    public async Task Corpo_IdenticoAoFocusNfeHttpClientReal()
    {
        var handler = new CapturaHandler();
        var cliente = new FocusNfeHttpClient(new HttpClient(handler));

        // Mesmo passo que o NfeContingencyHostedService faz hoje: Newtonsoft (padrao) sobre o PayloadJson guardado.
        var request = Newtonsoft.Json.JsonConvert.DeserializeObject<FocusNfceRequest>(RecoveryFixtures.PayloadNfce);
        await cliente.PostAsync("https://api.focusnfe.com.br/v2/nfce?ref=x", request!);

        handler.Corpo.Should().NotBeNullOrWhiteSpace();
        NfceCorpoDeEnvio.Montar(RecoveryFixtures.PayloadNfce).Should().Be(handler.Corpo);
    }

    [Fact(DisplayName = "O corpo usa os nomes do fio (snake_case), nao os do payload guardado (PascalCase)")]
    public void Corpo_NomesDoFio()
    {
        var corpo = NfceCorpoDeEnvio.Montar(RecoveryFixtures.PayloadNfce);

        corpo.Should().Contain("\"data_emissao\":\"" + RecoveryFixtures.DataAntiga + "\"");
        corpo.Should().Contain("\"cnpj_emitente\"").And.Contain("\"itens\"").And.Contain("\"pagamentos\"");
        corpo.Should().NotContain("\"DataEmissao\"").And.NotContain("\"CnpjEmitente\"");
    }

    [Fact(DisplayName = "Trocar so a DataEmissao no payload muda so data_emissao no corpo que vai para a Focus")]
    public void Corpo_SoDataEmissaoMuda()
    {
        const string nova = "2026-10-08T12:00:00-03:00";
        var original = NfceCorpoDeEnvio.Montar(RecoveryFixtures.PayloadNfce);
        var regenerado = NfceCorpoDeEnvio.Montar(PayloadDataEmissao.Substituir(RecoveryFixtures.PayloadNfce, nova));

        regenerado.Should().Be(original.Replace(RecoveryFixtures.DataAntiga, nova));
    }

    [Theory(DisplayName = "Payload vazio lanca ArgumentException")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void PayloadVazio_Lanca(string? payload)
    {
        var act = () => NfceCorpoDeEnvio.Montar(payload!);

        act.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "Payload 'null' lanca InvalidOperationException")]
    public void PayloadNull_Lanca()
    {
        var act = () => NfceCorpoDeEnvio.Montar("null");

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact(DisplayName = "Payload com formato que nao vira a classe lanca (nao envia lixo)")]
    public void PayloadInconvertivel_Lanca()
    {
        var act = () => NfceCorpoDeEnvio.Montar("{\"Itens\":\"nao-e-uma-lista\"}");

        act.Should().Throw<Exception>();
    }
}
