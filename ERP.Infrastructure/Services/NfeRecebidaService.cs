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

        var versaoAtual = await _ctx.NfesRecebidas.AsNoTracking()
            .OrderByDescending(n => n.Versao)
            .Select(n => (long?)n.Versao)
            .FirstOrDefaultAsync() ?? 0;

        int novasOuAtualizadas = 0;

        // Achado testando com dado real (21/08) — a Focus devolve só os 100
        // primeiros registros por chamada (confirmado na doc oficial, no
        // endpoint irmão de CT-es recebidos, que usa o mesmo mecanismo de
        // versao). Sem repetir a chamada, parava sempre no mesmo ponto (no
        // caso real, ficou preso em março mesmo já estando em agosto). O
        // cliente HTTP desse projeto só devolve o corpo da resposta, não os
        // headers — não dá pra ler X-Max-Version como a doc recomenda, então
        // uso a maior versao vista no próprio lote como cursor da próxima
        // chamada, parando quando vier menos de 100 (sinal de última página).
        const int tamanhoPagina = 100;
        while (true)
        {
            string endpoint = $"{baseServidor}/v2/nfes_recebidas?cnpj={config.Cnpj}&versao={versaoAtual}";
            var resultado = await _httpClient.GetAsync(endpoint);

            if (resultado.IsFailed)
                throw new InvalidOperationException($"Erro ao consultar notas recebidas: {resultado.Errors[0].Message}");

            using var doc = JsonDocument.Parse(resultado.Value);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) break;

            int itensNestaPagina = 0;
            long maiorVersaoNesteLote = versaoAtual;

            // Achado testando de verdade com dado real (21/08) — a Focus pode
            // devolver a MESMA chave duas vezes na mesma resposta (ex: um
            // evento de "descoberta" e um de "manifestação mudou" pra mesma
            // nota, ambos mais novos que a última versão vista). Sem isso, a
            // segunda ocorrência não encontrava a primeira (ainda não
            // commitada, só existe em memória) e tentava Add() de novo — vira
            // "duplicate key" no unique index de Chave.
            var jaProcessadasNesteLote = new Dictionary<string, Domain.Entities.NfeRecebida>();

            foreach (var item in doc.RootElement.EnumerateArray())
            {
                itensNestaPagina++;
                string? chave = ObterString(item, "chave_nfe", "chave");
                if (string.IsNullOrWhiteSpace(chave)) continue;

                long versao = ObterLong(item, "versao") ?? 0;
                if (versao > maiorVersaoNesteLote) maiorVersaoNesteLote = versao;

                if (jaProcessadasNesteLote.TryGetValue(chave, out var jaAdicionada))
                {
                    jaAdicionada.Versao = versao; // segunda ocorrência da mesma nota neste lote — só atualiza a versão
                    continue;
                }

                // Mesmo achado do NotaFiscalAvulsaService (S27, 20/08) — sem
                // AsTracking(), `existente.Versao = versao` mais embaixo não
                // seria rastreado (AppDbContext roda NoTracking global).
                var existente = await _ctx.NfesRecebidas.AsTracking().FirstOrDefaultAsync(n => n.Chave == chave);
                if (existente is null)
                {
                    var nova = new Domain.Entities.NfeRecebida
                    {
                        Chave         = chave,
                        CnpjEmitente  = ObterString(item, "cnpj_emitente"),
                        NomeEmitente  = ObterString(item, "nome_emitente", "razao_social_emitente"),
                        DataEmissao   = ObterData(item, "data_emissao"),
                        ValorTotal    = ObterDecimal(item, "valor_nota_fiscal", "valor_total"),
                        Versao        = versao,
                        DescobertaEm  = ERP.Domain.Common.FusoBrasilHelper.AgoraNoBrasil(),
                    };
                    _ctx.NfesRecebidas.Add(nova);
                    jaProcessadasNesteLote[chave] = nova;
                    novasOuAtualizadas++;
                }
                else
                {
                    jaProcessadasNesteLote[chave] = existente;
                    existente.Versao = versao;
                    novasOuAtualizadas++;
                }
            }

            await _ctx.SaveChangesAsync();

            if (itensNestaPagina < tamanhoPagina || maiorVersaoNesteLote == versaoAtual)
                break; // última página (veio menos que o limite, ou não avançou — evita laço infinito)

            versaoAtual = maiorVersaoNesteLote;
        }

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
        // Mesmo achado do S27 — sem AsTracking(), StatusManifestacao não
        // seria persistido (AppDbContext roda NoTracking global).
        var nota = await _ctx.NfesRecebidas.AsTracking().FirstOrDefaultAsync(n => n.Id == id)
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
        // Mesmo achado do S27 — sem AsTracking(), Importada=true não seria
        // persistido (AppDbContext roda NoTracking global).
        var nota = await _ctx.NfesRecebidas.AsTracking().FirstOrDefaultAsync(n => n.Id == id)
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