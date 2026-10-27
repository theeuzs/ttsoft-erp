// ERP.Tests/WPF/TextosInterfaceTests.cs
using FluentAssertions;
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace ERP.Tests.WPF;

/// <summary>
/// EXC-MKT-002 (10/2026) — o WPF exibia "Vila Verde" (nome da loja piloto)
/// em títulos de MessageBox, numa dica de tela, no comprovante de pagamento
/// e no texto de venda/orçamento enviado por WhatsApp, que qualquer loja
/// veria (os dois últimos chegam ao cliente final da loja). Os documentos
/// agora usam o NomeFantasia do config_recibo.json, como os recibos já usavam.
/// Este teste lê o código-fonte do ERP.WPF e falha se o nome voltar a
/// aparecer em texto que chega na tela: literais de string nos .cs e
/// qualquer trecho dos .xaml fora de comentário. Comentários de código
/// citando a Vila Verde continuam permitidos (são contexto para quem
/// desenvolve, não interface).
///
/// Depende do código-fonte estar ao lado do binário de teste (rodar a
/// partir do repositório, como no CI); sem ele o teste falha avisando.
/// </summary>
public class TextosInterfaceTests
{
    // Nome e CNPJ da loja piloto (o CNPJ aparecia fixo no ReciboPreviewView).
    private static readonly string[] TextosProibidos = { "Vila Verde", "12.820.608/0001-41", "12820608000141" };

    // Literal C# normal ("..." com escapes), de uma linha só.
    private static readonly Regex LiteralCs = new(@"""(?:[^""\\\r\n]|\\.)*""", RegexOptions.Compiled);
    private static readonly Regex ComentarioXaml = new(@"<!--.*?-->", RegexOptions.Compiled | RegexOptions.Singleline);

    [Fact]
    public void Textos_de_interface_do_WPF_nao_citam_a_loja_piloto()
    {
        var pastaWpf = Path.Combine(AcharRaizDoRepositorio(), "ERP.WPF");

        var ocorrencias = Directory
            .EnumerateFiles(pastaWpf, "*.*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                     || f.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase))
            .Where(f => !EstaEmPastaGerada(f))
            .SelectMany(f => TextosVisiveis(f).Select(t => $"{Path.GetRelativePath(pastaWpf, f)}: {t}"))
            .Where(t => TextosProibidos.Any(n => t.Contains(n, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        ocorrencias.Should().BeEmpty(
            "o WPF é usado por qualquer loja; use o nome do produto (ConstruTTor) nos textos de interface");
    }

    private static string[] TextosVisiveis(string arquivo)
    {
        var conteudo = File.ReadAllText(arquivo);

        if (arquivo.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase))
            return ComentarioXaml.Replace(conteudo, "").Split('\n');

        // .cs: só literais, ignorando linhas de comentário (// e ///) para
        // não confundir aspas dentro de comentário com string de verdade.
        return conteudo.Split('\n')
            .Where(l => !l.TrimStart().StartsWith("//"))
            .SelectMany(l => LiteralCs.Matches(l).Select(m => m.Value))
            .ToArray();
    }

    private static bool EstaEmPastaGerada(string arquivo)
    {
        var partes = arquivo.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return partes.Any(p => p.Equals("obj", StringComparison.OrdinalIgnoreCase)
                            || p.Equals("bin", StringComparison.OrdinalIgnoreCase));
    }

    private static string AcharRaizDoRepositorio()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "ERP.sln")))
            dir = dir.Parent;

        return dir?.FullName ?? throw new InvalidOperationException(
            "ERP.sln não encontrado acima de " + AppContext.BaseDirectory +
            ": este teste precisa do código-fonte do repositório.");
    }
}
