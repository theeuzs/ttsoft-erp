using System.Threading.Tasks;

namespace ERP.Application.Interfaces;

public interface INfeCancellationService
{
    /// <param name="tipoDocumento">"NFCE" (padrão) ou "NFE" — decide se o
    /// endpoint é /v2/nfce/ ou /v2/nfe/. Antes: hardcoded pra nfce, então
    /// cancelar uma NF-e A4 nunca funcionava.</param>
    Task<(bool Sucesso, string Mensagem)> CancelarNotaAsync(string referencia, string justificativa, string token, bool isProducao, string tipoDocumento = "NFCE");
}