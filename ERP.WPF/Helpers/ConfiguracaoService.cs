using System;
using System.IO;
using System.Text.Json;

namespace ERP.WPF.Helpers;

public class ReciboConfig
{
    // Caminho da logo: pode ser relativo (ex: "Assets\logo_cliente.png")
    // ou absoluto. O sistema tenta as duas formas automaticamente.
    public string CaminhoLogo { get; set; } = string.Empty;

    public string RazaoSocial  { get; set; } = "NOME DA EMPRESA";
    public string NomeFantasia { get; set; } = "NOME FANTASIA";
    public string Telefone     { get; set; } = "Telefone / WhatsApp";
    public string Endereco     { get; set; } = "Endereço da Loja";

    public string RodapeLinha1 { get; set; } = "Obrigado pela preferência!";
    public string RodapeLinha2 { get; set; } = "Volte sempre!";
    public string RodapeLinha3 { get; set; } = "Trocas apenas com cupom em até 7 dias.";

    public string TokenFocusNfe       { get; set; } = string.Empty;
    public bool   UsarAmbienteProducao { get; set; } = false;
    public string ChavePix  { get; set; } = string.Empty;
    public string CidadePix { get; set; } = string.Empty;
}

public static class ConfiguracaoService
{
    private static readonly string CaminhoArquivo =
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config_recibo.json");

    public static ReciboConfig Carregar()
    {
        if (!File.Exists(CaminhoArquivo))
            return new ReciboConfig();

        string json = File.ReadAllText(CaminhoArquivo);
        var config = JsonSerializer.Deserialize<ReciboConfig>(json) ?? new ReciboConfig();

        // ── Descriptografar o token FocusNFe ────────────────────────────
        if (!string.IsNullOrWhiteSpace(config.TokenFocusNfe))
        {
            try { config.TokenFocusNfe = CriptografiaService.Desencriptar(config.TokenFocusNfe); }
            catch { config.TokenFocusNfe = string.Empty; } // token corrompido → limpa sem travar
        }

        // ── Resolver caminho da logo ─────────────────────────────────────
        // Problema original: "C:\Users\mathe\..." não funciona em outras máquinas.
        // Solução: tentamos 3 formas, nesta ordem:
        //   1. O caminho como está (caso já seja absoluto e válido)
        //   2. Relativo à pasta do executável (recomendado para distribuição)
        //   3. Subpasta padrão Assets\
        config.CaminhoLogo = ResolverCaminhoLogo(config.CaminhoLogo);

        return config;
    }

    public static void Salvar(ReciboConfig config)
    {
        string tokenOriginal = config.TokenFocusNfe;
        config.TokenFocusNfe = CriptografiaService.Encriptar(tokenOriginal);

        string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(CaminhoArquivo, json);

        config.TokenFocusNfe = tokenOriginal;
    }

    // ── Helper: resolve o caminho da logo de forma inteligente ───────────
    private static string ResolverCaminhoLogo(string caminho)
    {
        if (string.IsNullOrWhiteSpace(caminho))
            return string.Empty;

        // 1. Tenta o caminho exato (funciona se já for absoluto e correto)
        if (File.Exists(caminho))
            return caminho;

        // 2. Tenta relativo à pasta do executável
        //    Isso cobre o caso de distribuição: logo fica em Assets\logo_cliente.png
        string relativo = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, caminho);
        if (File.Exists(relativo))
            return relativo;

        // 3. Tenta só o nome do arquivo dentro de Assets\
        //    Útil se o cliente moveu o logo manualmente para lá
        string nomeArquivo = Path.GetFileName(caminho);
        string naPastaAssets = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", nomeArquivo);
        if (File.Exists(naPastaAssets))
            return naPastaAssets;

        // Não achou em nenhum lugar — retorna vazio para o recibo imprimir sem logo
        // (melhor do que travar o sistema)
        return string.Empty;
    }
}
