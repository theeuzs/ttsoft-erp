using ERP.Application.Interfaces;
using ERP.Domain.Entities;
using ERP.Persistence.Context;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace ERP.Infrastructure.Services;

public class NfseEmissionService : INfseEmissionService
{
    private readonly IFocusNfeHttpClient _httpClient;
    private readonly IServiceProvider    _sp;

    public NfseEmissionService(IFocusNfeHttpClient httpClient, IServiceProvider sp)
    {
        _httpClient = httpClient;
        _sp         = sp;
    }

    public async Task<(bool Sucesso, string Mensagem, NfseEmitida? Nfse)> EmitirAsync(
        EmitirNfseDto dto, string token, bool isProducao)
    {
        if (string.IsNullOrWhiteSpace(token))
            return (false, "Token FocusNFe não configurado.", null);

        _httpClient.SetApiToken(token);

        var referencia = $"nfse-{Guid.NewGuid():N}";
        var baseUrl    = isProducao
            ? "https://api.focusnfe.com.br"
            : "https://homologacao.focusnfe.com.br";

        // Payload para a FocusNFe (NFS-e nacional padrão)
        var payload = new
        {
            data_emissao        = DateTime.Now.ToString("yyyy-MM-dd"),
            natureza_operacao   = 1, // 1 = Tributação no município
            optante_simples_nacional = 1,
            prestador = new
            {
                codigo_municipio = dto.CodigoMunicipio ?? "4106902" // Curitiba padrão
            },
            tomador = new
            {
                cpf_cnpj = dto.TomadorCpfCnpj,
                razao_social = dto.TomadorNome,
                email        = dto.TomadorEmail
            },
            itens = new[]
            {
                new
                {
                    descricao          = dto.DescricaoServico,
                    codigo_cnae        = dto.CodigoCnae ?? "4744001",
                    codigo_tributacao_municipio = dto.CodigoServico ?? "04.00",
                    valor_unitario     = dto.ValorServico,
                    quantidade         = 1,
                    iss_retido         = false,
                    aliquota_iss       = dto.AliquotaISS / 100m
                }
            }
        };

        var endpoint = $"{baseUrl}/v2/nfse?ref={referencia}";
        var result   = await _httpClient.PostAsync(endpoint, payload);

        using var scope = _sp.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var nfse = new NfseEmitida
        {
            ReferenciaNfse  = referencia,
            ClienteId       = dto.ClienteId,
            TomadorNome     = dto.TomadorNome,
            TomadorCpfCnpj  = dto.TomadorCpfCnpj,
            TomadorEmail    = dto.TomadorEmail,
            DescricaoServico = dto.DescricaoServico,
            CodigoServico   = dto.CodigoServico,
            CodigoCnae      = dto.CodigoCnae,
            ValorServico    = dto.ValorServico,
            AliquotaISS     = dto.AliquotaISS,
            VendaId         = dto.VendaId,
            TenantId        = AppDbContext.GetGlobalTenantId()
        };

        if (result.IsFailed)
        {
            nfse.Status       = StatusNfse.Erro;
            nfse.MensagemErro = result.Errors[0].Message;
            ctx.NfseEmitidas.Add(nfse);
            await ctx.SaveChangesAsync();
            return (false, nfse.MensagemErro, nfse);
        }

        nfse.JsonResposta = result.Value;

        using var doc  = JsonDocument.Parse(result.Value);
        var root       = doc.RootElement;
        var status     = root.TryGetProperty("status", out var s) ? s.GetString() : "";

        if (status == "autorizado")
        {
            nfse.Status             = StatusNfse.Autorizada;
            nfse.NumeroNfse         = root.TryGetProperty("numero", out var n) ? n.GetString() : null;
            nfse.CodigoVerificacao  = root.TryGetProperty("codigo_verificacao", out var cv) ? cv.GetString() : null;
            nfse.UrlDanfse          = root.TryGetProperty("url", out var u) ? u.GetString() : null;
            ctx.NfseEmitidas.Add(nfse);
            await ctx.SaveChangesAsync();
            return (true, "NFS-e autorizada com sucesso!", nfse);
        }

        nfse.Status       = StatusNfse.Erro;
        nfse.MensagemErro = root.TryGetProperty("mensagem_sefaz", out var m)
            ? m.GetString() : $"Status: {status}";
        ctx.NfseEmitidas.Add(nfse);
        await ctx.SaveChangesAsync();
        return (false, nfse.MensagemErro ?? "Erro desconhecido", nfse);
    }

    public async Task<(bool Sucesso, string Mensagem)> CancelarAsync(
        string referencia, string motivo, string token, bool isProducao)
    {
        _httpClient.SetApiToken(token);
        var baseUrl  = isProducao
            ? "https://api.focusnfe.com.br"
            : "https://homologacao.focusnfe.com.br";

        var result = await _httpClient.DeleteWithBodyAsync(
            $"{baseUrl}/v2/nfse/{referencia}", new { justificativa = motivo });

        return result.IsSuccess
            ? (true, "NFS-e cancelada com sucesso.")
            : (false, result.Errors[0].Message);
    }
}
