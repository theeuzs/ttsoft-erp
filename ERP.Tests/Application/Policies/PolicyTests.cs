using ERP.Application.Helpers;
using FluentAssertions;
using Xunit;

namespace ERP.Tests.Application.Policies;

// ═══════════════════════════════════════════════════════════════════════════
//  LockoutPolicy
// ═══════════════════════════════════════════════════════════════════════════

public class LockoutPolicyTests
{
    [Fact(DisplayName = "LockoutPolicy — 4 tentativas: sem lockout")]
    public void Calcular_4Tentativas_SemLockout()
    {
        var (novas, lockout) = LockoutPolicy.Calcular(3); // 3 tentativas → tenta 4ª
        novas.Should().Be(4);
        lockout.Should().BeNull("conta só é bloqueada na 5ª tentativa");
    }

    [Fact(DisplayName = "LockoutPolicy — 5ª tentativa: lockout em 15 minutos")]
    public void Calcular_5Tentativas_LockoutEm15Min()
    {
        var (novas, lockout) = LockoutPolicy.Calcular(4); // 4 tentativas → tenta 5ª
        novas.Should().Be(LockoutPolicy.MaxTentativas);
        lockout.Should().NotBeNull();
        lockout!.Value.Should().BeCloseTo(
            DateTime.UtcNow.AddMinutes(LockoutPolicy.MinutosBloqueio),
            precision: TimeSpan.FromSeconds(2));
    }

    [Fact(DisplayName = "LockoutPolicy — EstaBloqueada: futura → true")]
    public void EstaBloqueada_DataFutura_True()
        => LockoutPolicy.EstaBloqueada(DateTime.UtcNow.AddMinutes(5)).Should().BeTrue();

    [Fact(DisplayName = "LockoutPolicy — EstaBloqueada: passada → false")]
    public void EstaBloqueada_DataPassada_False()
        => LockoutPolicy.EstaBloqueada(DateTime.UtcNow.AddMinutes(-1)).Should().BeFalse();

    [Fact(DisplayName = "LockoutPolicy — EstaBloqueada: null → false")]
    public void EstaBloqueada_Null_False()
        => LockoutPolicy.EstaBloqueada(null).Should().BeFalse();

    [Fact(DisplayName = "LockoutPolicy — MensagemErro com lockout menciona minutos")]
    public void MensagemErro_ComLockout_MencionaMinutos()
    {
        var msg = LockoutPolicy.MensagemErro(5, DateTime.UtcNow.AddMinutes(15));
        msg.Should().Contain("bloqueada").And.Contain("15");
    }

    [Fact(DisplayName = "LockoutPolicy — MensagemErro sem lockout mostra tentativas restantes")]
    public void MensagemErro_SemLockout_MostraTentativasRestantes()
    {
        var msg = LockoutPolicy.MensagemErro(3, null);
        msg.Should().Contain("2"); // MaxTentativas(5) - novas(3) = 2 restantes
    }
}

// ═══════════════════════════════════════════════════════════════════════════
//  DescontoPolicy
// ═══════════════════════════════════════════════════════════════════════════

public class DescontoPolicyTests
{
    [Fact(DisplayName = "DescontoPolicy — desconto válido dentro do limite: OK")]
    public void Validar_DescontoValido_Ok()
    {
        var (ok, erro) = DescontoPolicy.Validar(10m, 30m, "Cimento");
        ok.Should().BeTrue();
        erro.Should().BeNull();
    }

    [Fact(DisplayName = "DescontoPolicy — desconto negativo: erro")]
    public void Validar_DescontoNegativo_Erro()
    {
        var (ok, erro) = DescontoPolicy.Validar(-1m, 30m);
        ok.Should().BeFalse();
        erro.Should().Contain("negativo");
    }

    [Fact(DisplayName = "DescontoPolicy — desconto acima de 100%: erro")]
    public void Validar_DescontoAcima100_Erro()
    {
        var (ok, erro) = DescontoPolicy.Validar(101m, 100m);
        ok.Should().BeFalse();
        erro.Should().Contain("100%");
    }

    [Fact(DisplayName = "DescontoPolicy — desconto excede limite do cargo: erro com nome do produto")]
    public void Validar_ExcedeLimiteCargo_ErroComNomeProduto()
    {
        var (ok, erro) = DescontoPolicy.Validar(20m, 10m, "Areia");
        ok.Should().BeFalse();
        erro.Should().Contain("Areia").And.Contain("10,00%");
    }

    [Fact(DisplayName = "DescontoPolicy — desconto exatamente no limite: OK")]
    public void Validar_DescontoExatoNoLimite_Ok()
    {
        var (ok, _) = DescontoPolicy.Validar(10m, 10m);
        ok.Should().BeTrue("desconto exatamente no limite deve ser permitido");
    }

    [Theory(DisplayName = "DescontoPolicy — CalcularTotal")]
    [InlineData(100.0, 2.0, 10.0, 180.0)]  // 100*2*(1-0.10) = 180
    [InlineData(50.0,  1.0, 0.0,  50.0)]   // sem desconto
    [InlineData(200.0, 3.0, 50.0, 300.0)]  // 200*3*0.5 = 300
    public void CalcularTotal_Correto(double preco, double qtd, double desc, double esperado)
        => DescontoPolicy.CalcularTotal((decimal)preco, (decimal)qtd, (decimal)desc)
            .Should().Be((decimal)esperado);
}

// ═══════════════════════════════════════════════════════════════════════════
//  SangriaPolicy
// ═══════════════════════════════════════════════════════════════════════════

public class SangriaPolicyTests
{
    [Fact(DisplayName = "SangriaPolicy — valor válido dentro do saldo: OK")]
    public void Validar_ValorDentroDoSaldo_Ok()
    {
        var (ok, erro) = SangriaPolicy.Validar(100m, 500m);
        ok.Should().BeTrue();
        erro.Should().BeNull();
    }

    [Fact(DisplayName = "SangriaPolicy — valor zero: erro")]
    public void Validar_ValorZero_Erro()
    {
        var (ok, erro) = SangriaPolicy.Validar(0m, 500m);
        ok.Should().BeFalse();
        erro.Should().Contain("maior que zero");
    }

    [Fact(DisplayName = "SangriaPolicy — valor maior que saldo: erro com valores")]
    public void Validar_ValorMaiorQueSaldo_Erro()
    {
        var (ok, erro) = SangriaPolicy.Validar(600m, 500m);
        ok.Should().BeFalse();
        erro.Should().Contain("R$ 600").And.Contain("R$ 500");
    }

    [Fact(DisplayName = "SangriaPolicy — valor dentro saldo mas acima limite do cargo: erro")]
    public void Validar_AcimaLimiteCargo_Erro()
    {
        var (ok, erro) = SangriaPolicy.Validar(300m, 500m, maxSangriaValue: 200m);
        ok.Should().BeFalse();
        erro.Should().Contain("limite do seu cargo").And.Contain("R$ 200");
    }

    [Fact(DisplayName = "SangriaPolicy — maxSangriaValue 0 significa sem limite de cargo")]
    public void Validar_MaxSangriaZero_SemLimiteCargo()
    {
        // Gerente/Admin tem maxSangriaValue = 99999 ou 0 (sem limite)
        var (ok, _) = SangriaPolicy.Validar(5000m, 10000m, maxSangriaValue: 0m);
        ok.Should().BeTrue("maxSangriaValue = 0 deve significar sem limite de cargo");
    }

    [Fact(DisplayName = "SangriaPolicy — valor exatamente igual ao saldo: OK")]
    public void Validar_ValorIgualSaldo_Ok()
    {
        var (ok, _) = SangriaPolicy.Validar(500m, 500m);
        ok.Should().BeTrue("sangria do saldo exato deve ser permitida");
    }
}