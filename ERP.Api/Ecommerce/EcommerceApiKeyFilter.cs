using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ERP.Api.Ecommerce;

// S{N} FIX: Theuzs e-commerce - autenticacao servidor-a-servidor por chave fixa.
// Falha FECHADO: sem TenantId ou sem chave configurada, o canal inteiro responde 503.
public sealed class EcommerceApiKeyFilter : IAuthorizationFilter
{
    public const string Header = "X-Theuzs-Key";

    private readonly EcommerceOptions _opt;
    private readonly ILogger<EcommerceApiKeyFilter> _log;

    public EcommerceApiKeyFilter(IOptions<EcommerceOptions> opt, ILogger<EcommerceApiKeyFilter> log)
    {
        _opt = opt.Value;
        _log = log;
    }

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        if (!_opt.EstaConfigurado)
        {
            _log.LogWarning("Ecommerce: canal chamado sem configuracao (TenantId/ApiKey ausentes).");
            context.Result = new StatusCodeResult(StatusCodes.Status503ServiceUnavailable);
            return;
        }

        var recebida = context.HttpContext.Request.Headers[Header].ToString();
        var esperadaBytes = Encoding.UTF8.GetBytes(_opt.ApiKey);
        var recebidaBytes = Encoding.UTF8.GetBytes(recebida);

        // Comparacao em tempo constante (FixedTimeEquals retorna false se os tamanhos diferem).
        if (recebida.Length == 0 || !CryptographicOperations.FixedTimeEquals(esperadaBytes, recebidaBytes))
        {
            _log.LogWarning("Ecommerce: chave invalida de {Ip}", context.HttpContext.Connection.RemoteIpAddress);
            context.Result = new UnauthorizedResult();
        }
    }
}
