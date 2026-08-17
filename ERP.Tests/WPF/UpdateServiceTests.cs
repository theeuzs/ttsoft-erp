// ERP.Tests/WPF/UpdateServiceTests.cs
using ERP.WPF.Services;
using FluentAssertions;
using System;
using System.IO;
using System.Security.Cryptography;
using Xunit;

namespace ERP.Tests.WPF;

/// <summary>
/// S17 FIX (08/2026) — updater ganhou verificação de SHA-256 em duas
/// camadas (UpdateService no WPF + ERP.Updater, que valida de novo antes
/// do File.Copy e não confia em quem o chamou). Aqui só cobre os três
/// helpers puros do lado WPF (UrlEhHttps/CalcularSha256Arquivo/HashConfere)
/// — sem HTTP, sem MessageBox, sem Process.Start.
///
/// A validação do lado ERP.Updater (Program.cs, top-level statements) NÃO
/// tem cobertura automatizada: o cálculo/comparação de lá está duplicado
/// (mesmo algoritmo, ~5 linhas) porque ERP.Updater é um exe standalone sem
/// ProjectReference pra nenhuma lib do sistema — puxar uma referência só
/// pra compartilhar essas 5 linhas seria maior cirurgia do que o item
/// pedia. Cobertura de lá é manual (build + teste real do fluxo de
/// atualização), igual já era antes desta mudança (zero testes existiam
/// pro Updater).
/// </summary>
public class UpdateServiceTests
{
    // ── UrlEhHttps ──────────────────────────────────────────────────────

    [Theory]
    [InlineData("https://ttsoftupdates.blob.core.windows.net/releases/erp.exe")]
    [InlineData("HTTPS://TTSOFTUPDATES.BLOB.CORE.WINDOWS.NET/erp.exe")]
    public void UrlEhHttps_com_https_retorna_true(string url)
        => UpdateService.UrlEhHttps(url).Should().BeTrue();

    [Theory]
    [InlineData("http://ttsoftupdates.blob.core.windows.net/releases/erp.exe")]
    [InlineData("ftp://exemplo.com/erp.exe")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("nao-e-uma-url")]
    [InlineData("/caminho/relativo/erp.exe")]
    public void UrlEhHttps_qualquer_coisa_que_nao_seja_https_absoluto_retorna_false(string? url)
        => UpdateService.UrlEhHttps(url).Should().BeFalse();

    // ── HashConfere ─────────────────────────────────────────────────────

    [Fact]
    public void HashConfere_hash_identico_confere()
        => UpdateService.HashConfere(
            "A1B2C3D4E5F6", "A1B2C3D4E5F6").Should().BeTrue();

    [Fact]
    public void HashConfere_diferenca_de_maiusculas_minusculas_ainda_confere()
        => UpdateService.HashConfere(
            "a1b2c3d4e5f6", "A1B2C3D4E5F6").Should().BeTrue();

    [Fact]
    public void HashConfere_espacos_nas_pontas_sao_ignorados()
        => UpdateService.HashConfere(
            "  A1B2C3D4E5F6  ", "A1B2C3D4E5F6").Should().BeTrue();

    [Fact]
    public void HashConfere_hash_diferente_nao_confere()
        => UpdateService.HashConfere(
            "A1B2C3D4E5F6", "000000000000").Should().BeFalse();

    [Fact]
    public void HashConfere_esperado_ausente_nao_confere_mesmo_com_calculado_valido()
        => UpdateService.HashConfere(
            null, "A1B2C3D4E5F6").Should().BeFalse();

    [Fact]
    public void HashConfere_esperado_vazio_nao_confere()
        => UpdateService.HashConfere(
            "", "A1B2C3D4E5F6").Should().BeFalse();

    [Fact]
    public void HashConfere_esperado_so_com_espacos_nao_confere()
        => UpdateService.HashConfere(
            "   ", "A1B2C3D4E5F6").Should().BeFalse();

    [Fact]
    public void HashConfere_ambos_vazios_nao_confere()
        => UpdateService.HashConfere("", "").Should().BeFalse();

    // ── CalcularSha256Arquivo ───────────────────────────────────────────

    [Fact]
    public void CalcularSha256Arquivo_bate_com_SHA256_calculado_de_forma_independente()
    {
        string caminho = Path.Combine(Path.GetTempPath(), $"s17-teste-{Guid.NewGuid():N}.bin");
        byte[] conteudo = System.Text.Encoding.UTF8.GetBytes("conteudo de teste do S17 FIX");

        try
        {
            File.WriteAllBytes(caminho, conteudo);

            string esperado = Convert.ToHexString(SHA256.HashData(conteudo));
            string calculado = UpdateService.CalcularSha256Arquivo(caminho);

            calculado.Should().Be(esperado);
        }
        finally
        {
            File.Delete(caminho);
        }
    }

    [Fact]
    public void CalcularSha256Arquivo_conteudos_diferentes_geram_hashes_diferentes()
    {
        string caminhoA = Path.Combine(Path.GetTempPath(), $"s17-a-{Guid.NewGuid():N}.bin");
        string caminhoB = Path.Combine(Path.GetTempPath(), $"s17-b-{Guid.NewGuid():N}.bin");

        try
        {
            File.WriteAllText(caminhoA, "arquivo original");
            File.WriteAllText(caminhoB, "arquivo adulterado");

            string hashA = UpdateService.CalcularSha256Arquivo(caminhoA);
            string hashB = UpdateService.CalcularSha256Arquivo(caminhoB);

            hashA.Should().NotBe(hashB);
        }
        finally
        {
            File.Delete(caminhoA);
            File.Delete(caminhoB);
        }
    }

    // ── Integração dos três, sem UI/HTTP: simula o caminho de decisão real ──

    [Fact]
    public void Fluxo_arquivo_correto_com_manifesto_https_e_hash_valido_seria_aceito()
    {
        string caminho = Path.Combine(Path.GetTempPath(), $"s17-fluxo-ok-{Guid.NewGuid():N}.bin");
        try
        {
            File.WriteAllText(caminho, "versao nova legitima do exe");
            string hashReal = UpdateService.CalcularSha256Arquivo(caminho);

            var manifesto = new VersaoInfo
            {
                UrlDownload = "https://ttsoftupdates.blob.core.windows.net/releases/erp.exe",
                Sha256      = hashReal
            };

            UpdateService.UrlEhHttps(manifesto.UrlDownload).Should().BeTrue();
            UpdateService.HashConfere(manifesto.Sha256, UpdateService.CalcularSha256Arquivo(caminho))
                .Should().BeTrue();
        }
        finally
        {
            File.Delete(caminho);
        }
    }

    [Fact]
    public void Fluxo_arquivo_adulterado_apos_publicacao_do_manifesto_seria_bloqueado()
    {
        string caminho = Path.Combine(Path.GetTempPath(), $"s17-fluxo-adulterado-{Guid.NewGuid():N}.bin");
        try
        {
            File.WriteAllText(caminho, "versao original que foi hasheada");
            string hashPublicadoNoManifesto = UpdateService.CalcularSha256Arquivo(caminho);

            // Arquivo baixado não é o que o manifesto descreveu (download
            // corrompido, ou blob trocado depois do manifesto publicado).
            File.WriteAllText(caminho, "conteudo diferente do que foi hasheado");

            UpdateService.HashConfere(hashPublicadoNoManifesto, UpdateService.CalcularSha256Arquivo(caminho))
                .Should().BeFalse();
        }
        finally
        {
            File.Delete(caminho);
        }
    }
}
