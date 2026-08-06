// ── ERP.Infrastructure/Services/NfeRecebidaService.cs ───────────────────────
using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text.Json;

namespace ERP.Infrastructure.Services;

/// <summary>
/// Item MD-e do roadmap fiscal — descobre notas de fornecedor emitidas
/// contra o CNPJ da loja via GET /v2/nfes_recebidas (confirmado na doc
/// oficial da Focus), e permite manifestar via POST /v2/nfes_recebidas/
/// {chave}/manifesto. Os nomes exatos dos campos do JSON de resposta da
/// listagem não puderam ser confirmados sem acesso à Focus ativa — o parsing
/// abaixo é defensivo (tenta variações plausíveis) e deve ser conferido contra
/// uma resposta real assim que possível.
/// </summary>
public class NfeRecebidaService : INfeRecebidaService
{
    private readonly Persistence.Context.AppDbContext _ctx;
    private readonly IFiscalConfigurationProvider _configProvider;
    private readonly IFocusNfeHttpClient _httpClient;

    public NfeRecebidaService(
        Persistence.Context.AppDbContext ctx, IFiscalConfigurationProvider configProvider, IFocusNfeHttpClient httpClient)
    {
        _ctx            = ctx;
        _configProvider = configProvider;
        _httpClient     = httpClient;
    }

    public async Task<int> BuscarNovasAsync()
    {
        var config = await _configProvider.ObterConfiguracaoAsync();
        if (string.IsNullOrWhiteSpace(config.TokenFocusNfe))
            throw new InvalidOperationException("Token da Focus NFe não configurado.");
        if (string.IsNullOrWhiteSpace(config.Cnpj))
            throw new InvalidOperationException("CNPJ da empresa não configurado — vá em Configurações → Empresa e Fiscal.");

        _httpClient.SetApiToken(config.TokenFocusNfe);
        string baseServidor = config.UsarAmbienteProducao ? "https://api.focusnfe.com.br" : "https://homologacao.focusnfe.com.br";

        var ultimaVersao = await _ctx.NfesRecebidas.AsNoTracking()
            .OrderByDescending(n => n.Versao)
            .Select(n => (long?)n.Versao)
            .FirstOrDefaultAsync() ?? 0;

        string endpoint = $"{baseServidor}/v2/nfes_recebidas?cnpj={config.Cnpj}&versao={ultimaVersao}";
        var resultado = await _httpClient.GetAsync(endpoint);

        if (resultado.IsFailed)
            throw new InvalidOperationException($"Erro ao consultar notas recebidas: {resultado.Errors[0].Message}");

        int novasOuAtualizadas = 0;
        using var doc = JsonDocument.Parse(resultado.Value);

        if (doc.RootElement.ValueKind != JsonValueKind.Array) return 0;

        foreach (var item in doc.RootElement.EnumerateArray())
        {
            string? chave = ObterString(item, "chave_nfe", "chave");
            if (string.IsNullOrWhiteSpace(chave)) continue;

            long versao = ObterLong(item, "versao") ?? 0;

            var existente = await _ctx.NfesRecebidas.FirstOrDefaultAsync(n => n.Chave == chave);
            if (existente is null)
            {
                _ctx.NfesRecebidas.Add(new Domain.Entities.NfeRecebida
                {
                    Chave         = chave,
                    CnpjEmitente  = ObterString(item, "cnpj_emitente"),
                    NomeEmitente  = ObterString(item, "nome_emitente", "razao_social_emitente"),
                    DataEmissao   = ObterData(item, "data_emissao"),
                    ValorTotal    = ObterDecimal(item, "valor_nota_fiscal", "valor_total"),
                    Versao        = versao,
                    DescobertaEm  = DateTime.Now,
                });
                novasOuAtualizadas++;
            }
            else
            {
                existente.Versao = versao;
                novasOuAtualizadas++;
            }
        }

        await _ctx.SaveChangesAsync();
        return novasOuAtualizadas;
    }

    public async Task<IReadOnlyList<NfeRecebidaDto>> ListarAsync()
    {
        var notas = await _ctx.NfesRecebidas.AsNoTracking()
            .OrderByDescending(n => n.DescobertaEm)
            .ToListAsync();

        return notas.Select(n => new NfeRecebidaDto(
            n.Id, n.Chave, n.CnpjEmitente, n.NomeEmitente, n.DataEmissao,
            n.ValorTotal, n.StatusManifestacao, n.Importada, n.DescobertaEm)).ToList();
    }

    public async Task ManifestarAsync(Guid id, string tipo, string? justificativa = null)
    {
        var nota = await _ctx.NfesRecebidas.FirstOrDefaultAsync(n => n.Id == id)
            ?? throw new KeyNotFoundException("Nota não encontrada.");

        if (tipo == "nao_realizada" && (string.IsNullOrWhiteSpace(justificativa) || justificativa.Length < 15))
            throw new InvalidOperationException("Justificativa obrigatória (mínimo 15 caracteres) pra 'operação não realizada'.");

        var config = await _configProvider.ObterConfiguracaoAsync();
        _httpClient.SetApiToken(config.TokenFocusNfe);
        string baseServidor = config.UsarAmbienteProducao ? "https://api.focusnfe.com.br" : "https://homologacao.focusnfe.com.br";

        var body = tipo == "nao_realizada"
            ? new { tipo, justificativa }
            : (object)new { tipo };

        var resultado = await _httpClient.PostAsync($"{baseServidor}/v2/nfes_recebidas/{nota.Chave}/manifesto", body);

        if (resultado.IsFailed)
            throw new InvalidOperationException($"Falha ao manifestar: {resultado.Errors[0].Message}");

        nota.StatusManifestacao = tipo switch
        {
            "ciencia"         => "Ciencia",
            "confirmacao"     => "Confirmacao",
            "desconhecimento" => "Desconhecimento",
            "nao_realizada"   => "NaoRealizada",
            _                 => nota.StatusManifestacao
        };
        await _ctx.SaveChangesAsync();
    }

    public async Task<string> BaixarXmlParaImportacaoAsync(Guid id)
    {
        var nota = await _ctx.NfesRecebidas.FirstOrDefaultAsync(n => n.Id == id)
            ?? throw new KeyNotFoundException("Nota não encontrada.");

        if (nota.StatusManifestacao == "Nenhuma")
            throw new InvalidOperationException("Dê ciência da nota antes de baixar o XML — a Focus só libera o XML completo depois da manifestação.");

        var config = await _configProvider.ObterConfiguracaoAsync();
        _httpClient.SetApiToken(config.TokenFocusNfe);
        string baseServidor = config.UsarAmbienteProducao ? "https://api.focusnfe.com.br" : "https://homologacao.focusnfe.com.br";

        // Endpoint inferido pelo padrão da doc da Focus (sufixo .xml pra pedir
        // essa representação específica) — é o item de MENOR confiança dessa
        // integração, já que não consegui confirmar o path exato sem acesso
        // à Focus ativa. Testar isso primeiro quando reativar.
        var resultado = await _httpClient.GetAsync($"{baseServidor}/v2/nfes_recebidas/{nota.Chave}.xml");
        if (resultado.IsFailed)
            throw new InvalidOperationException($"Falha ao baixar XML: {resultado.Errors[0].Message}");

        var caminho = Path.Combine(Path.GetTempPath(), $"nfe_recebida_{nota.Chave}.xml");
        await File.WriteAllTextAsync(caminho, resultado.Value);

        nota.Importada = true;
        await _ctx.SaveChangesAsync();

        return caminho;
    }

    // ── Parsing defensivo — nomes exatos do JSON da Focus não confirmados ──
    private static string? ObterString(JsonElement el, params string[] nomesPossiveis)
    {
        foreach (var nome in nomesPossiveis)
            if (el.TryGetProperty(nome, out var prop) && prop.ValueKind == JsonValueKind.String)
                return prop.GetString();
        return null;
    }

    private static long? ObterLong(JsonElement el, params string[] nomesPossiveis)
    {
        foreach (var nome in nomesPossiveis)
            if (el.TryGetProperty(nome, out var prop))
            {
                if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt64(out var v)) return v;
                if (prop.ValueKind == JsonValueKind.String && long.TryParse(prop.GetString(), out var v2)) return v2;
            }
        return null;
    }

    private static decimal? ObterDecimal(JsonElement el, params string[] nomesPossiveis)
    {
        foreach (var nome in nomesPossiveis)
            if (el.TryGetProperty(nome, out var prop))
            {
                if (prop.ValueKind == JsonValueKind.Number && prop.TryGetDecimal(out var v)) return v;
                if (prop.ValueKind == JsonValueKind.String && decimal.TryParse(prop.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var v2)) return v2;
            }
        return null;
    }

    private static DateTime? ObterData(JsonElement el, params string[] nomesPossiveis)
    {
        foreach (var nome in nomesPossiveis)
            if (el.TryGetProperty(nome, out var prop) && prop.ValueKind == JsonValueKind.String
                && DateTime.TryParse(prop.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var v))
                return v;
        return null;
    }
}
