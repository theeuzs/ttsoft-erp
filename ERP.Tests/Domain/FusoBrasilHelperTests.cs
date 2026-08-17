// ERP.Tests/Domain/FusoBrasilHelperTests.cs
using ERP.Domain.Common;
using FluentAssertions;
using System;
using Xunit;

namespace ERP.Tests.Domain;

/// <summary>
/// S21 FIX (17/08) — a primeira venda real depois do S18/S19/S20 foi
/// rejeitada pela SEFAZ com código 703 ("Data-Hora de Emissão posterior
/// ao horário de recebimento") porque o horário enviado estava 3h no
/// futuro. Causa: DateTime.Now/"...zzz" usam o fuso AMBIENTE do sistema
/// operacional do servidor pra montar o offset — no Azure, depois de um
/// redeploy, isso ficou inconsistente (valor numérico e offset não
/// combinavam entre si). O recibo do WPF mostrou o mesmo sintoma (19:10
/// em vez de 16:10) porque confia no SaleDate vindo da API.
///
/// A prova central destes testes: o cálculo bate certo independente de
/// qual TimeZoneInfo.Local a máquina rodando o teste tiver — é
/// exatamente essa independência que resolve o bug.
/// </summary>
public class FusoBrasilHelperTests
{
    [Fact(DisplayName = "AgoraNoBrasil está sempre 3 horas atrás de UtcNow (Brasil não tem mais horário de verão)")]
    public void AgoraNoBrasil_SempreTresHorasAtrasDeUtc()
    {
        var antes  = DateTime.UtcNow;
        var brasil = FusoBrasilHelper.AgoraNoBrasil();
        var depois = DateTime.UtcNow;

        // Calcula em cima da janela [antes, depois] pra não ter teste flaky
        // por causa dos poucos milissegundos entre as três chamadas.
        var offsetMinimo = antes.AddHours(-3) - TimeSpan.FromSeconds(1);
        var offsetMaximo = depois.AddHours(-3) + TimeSpan.FromSeconds(1);

        brasil.Should().BeOnOrAfter(offsetMinimo);
        brasil.Should().BeOnOrBefore(offsetMaximo);
    }

    [Fact(DisplayName = "AgoraNoBrasilComOffset sempre termina em -03:00")]
    public void AgoraNoBrasilComOffset_SempreTerminaEmMenosTresHoras()
    {
        string resultado = FusoBrasilHelper.AgoraNoBrasilComOffset();

        resultado.Should().EndWith("-03:00");
    }

    [Fact(DisplayName = "AgoraNoBrasilComOffset é um DateTimeOffset válido, coerente com AgoraNoBrasil")]
    public void AgoraNoBrasilComOffset_ParseiaParaOMesmoInstanteQueAgoraNoBrasil()
    {
        var brasilDateTime = FusoBrasilHelper.AgoraNoBrasil();
        string comOffset   = FusoBrasilHelper.AgoraNoBrasilComOffset();

        var parseado = DateTimeOffset.Parse(comOffset);

        // Mesmo relógio (dia/hora/minuto), só formatos diferentes.
        parseado.DateTime.Should().BeCloseTo(brasilDateTime, TimeSpan.FromSeconds(2));
        parseado.Offset.Should().Be(TimeSpan.FromHours(-3));
    }

    [Fact(DisplayName = "FusoBrasil resolvido tem offset fixo de -03:00 (sem horário de verão)")]
    public void FusoBrasil_TemOffsetFixoMenosTresHoras()
    {
        var offsetHoje = FusoBrasilHelper.FusoBrasil.GetUtcOffset(DateTime.UtcNow);
        var offsetJaneiro = FusoBrasilHelper.FusoBrasil.GetUtcOffset(new DateTime(2026, 1, 15));
        var offsetJulho   = FusoBrasilHelper.FusoBrasil.GetUtcOffset(new DateTime(2026, 7, 15));

        offsetHoje.Should().Be(TimeSpan.FromHours(-3));
        offsetJaneiro.Should().Be(TimeSpan.FromHours(-3), "Brasil não observa mais horário de verão desde 2019");
        offsetJulho.Should().Be(TimeSpan.FromHours(-3));
    }
}
