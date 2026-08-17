// ERP.Tests/Application/FocusNfceRequestSerializationTests.cs
using ERP.Application.DTOs.FocusNfe;
using FluentAssertions;
using System.Text.Json;
using Xunit;

namespace ERP.Tests.Application;

/// <summary>
/// S19 FIX (13/08) — achado durante o teste de homologação/produção do
/// S18: uma venda comum (sem nota referenciada) quebrava a emissão com
/// "erro_validacao_schema: cannot `fill_with' a NFe::XMLNodeArray with a
/// :NilClass" — erro do backend da Focus, não da SEFAZ. Causa:
/// NotasReferenciadas não tinha valor padrão (só Itens/Pagamentos
/// tinham), então ficava `null` em qualquer venda que não referencia
/// outra nota — e o JsonSerializer.Serialize (sem ignorar nulos) mandava
/// "notas_referenciadas": null, que a Focus não aceita onde espera um
/// array (mesmo vazio).
/// </summary>
public class FocusNfceRequestSerializationTests
{
    [Fact(DisplayName = "NotasReferenciadas nunca serializa como null — sempre array, mesmo vazio (S19)")]
    public void Serializar_SemNotaReferenciada_NuncaMandaNullNoCampoDeArray()
    {
        var request = new FocusNfceRequest();
        // Não seta NotasReferenciadas — é exatamente o caso de uma venda comum.

        string json = JsonSerializer.Serialize(request);

        json.Should().NotContain("\"notas_referenciadas\":null",
            "a Focus não aceita null onde espera um array — nem vazio");
        json.Should().Contain("\"notas_referenciadas\":[]",
            "sem nota referenciada, o campo deve vir como array vazio");
    }

    [Fact(DisplayName = "NotasReferenciadas com nota real ainda serializa certo (não regrediu o fluxo de devolução) (S19)")]
    public void Serializar_ComNotaReferenciada_SerializaOArrayComOItem()
    {
        var request = new FocusNfceRequest
        {
            NotasReferenciadas = new() { new NotaReferenciadaRequest { ChaveNfe = "35260812345678000199550010000001231234567890" } }
        };

        string json = JsonSerializer.Serialize(request);

        json.Should().NotContain("\"notas_referenciadas\":null");
        json.Should().Contain("35260812345678000199550010000001231234567890");
    }
}
