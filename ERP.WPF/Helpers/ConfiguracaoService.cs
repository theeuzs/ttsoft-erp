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

    /// <summary>Achado (20/08) — o campo de CNPJ na tela de Configurações
    /// nunca tinha correspondente aqui: preenchia, salvava, e sumia,
    /// porque não existia onde persistir.</summary>
    public string Cnpj { get; set; } = string.Empty;

    public string RodapeLinha1 { get; set; } = "Obrigado pela preferência!";
    public string RodapeLinha2 { get; set; } = "Volte sempre!";
    public string RodapeLinha3 { get; set; } = "Trocas apenas com cupom em até 7 dias.";

    // Achado (21/08) — Focus NFe tem token SEPARADO por ambiente por
    // empresa (confirmado testando de verdade: token de produção não
    // funciona em homologacao.focusnfe.com.br). Antes só existia um campo,
    // obrigando trocar o token na mão pra testar e trocar de volta depois.
    // [JsonPropertyName] preserva o nome antigo no arquivo — sem isso, quem
    // já tinha um token salvo localmente perderia ele na próxima leitura.
    [System.Text.Json.Serialization.JsonPropertyName("TokenFocusNfe")]
    public string TokenFocusNfeProducao { get; set; } = string.Empty;
    public string TokenFocusNfeHomologacao { get; set; } = string.Empty;
    public bool   UsarAmbienteProducao { get; set; } = false;
    public string ChavePix  { get; set; } = string.Empty;
    public string CidadePix { get; set; } = string.Empty;

    /// <summary>Código morto da auditoria ativado — token de API do provedor
    /// de Pix, pra confirmação automática (PixPollingService). Vazio = só
    /// confirmação manual, como já era.</summary>
    public string PixApiToken { get; set; } = string.Empty;
    /// <summary>"openpix" ou "gerencianet".</summary>
    public string PixProvedor { get; set; } = "openpix";

    /// <summary>Código morto da auditoria ativado — porta COM da balança
    /// serial (Toledo/Filizola). Padrão COM1 se não configurado.</summary>
    public string BalancaComPort { get; set; } = "COM1";
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

        // ── Descriptografar os tokens FocusNFe (produção e homologação) ──
        if (!string.IsNullOrWhiteSpace(config.TokenFocusNfeProducao))
        {
            try { config.TokenFocusNfeProducao = CriptografiaService.Desencriptar(config.TokenFocusNfeProducao); }
            catch { config.TokenFocusNfeProducao = string.Empty; } // token corrompido → limpa sem travar
        }
        if (!string.IsNullOrWhiteSpace(config.TokenFocusNfeHomologacao))
        {
            try { config.TokenFocusNfeHomologacao = CriptografiaService.Desencriptar(config.TokenFocusNfeHomologacao); }
            catch { config.TokenFocusNfeHomologacao = string.Empty; }
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
        string tokenProducaoOriginal     = config.TokenFocusNfeProducao;
        string tokenHomologacaoOriginal  = config.TokenFocusNfeHomologacao;
        config.TokenFocusNfeProducao    = CriptografiaService.Encriptar(tokenProducaoOriginal);
        config.TokenFocusNfeHomologacao = CriptografiaService.Encriptar(tokenHomologacaoOriginal);

        string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(CaminhoArquivo, json);

        config.TokenFocusNfeProducao    = tokenProducaoOriginal;
        config.TokenFocusNfeHomologacao = tokenHomologacaoOriginal;
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