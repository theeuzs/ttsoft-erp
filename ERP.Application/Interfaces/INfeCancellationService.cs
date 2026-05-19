using System.Threading.Tasks;

namespace ERP.Application.Interfaces;

public interface INfeCancellationService
{
    // 👇 Mudou de CancelarNfceAsync para CancelarNotaAsync
    Task<(bool Sucesso, string Mensagem)> CancelarNotaAsync(string referencia, string justificativa, string token, bool isProducao);
}