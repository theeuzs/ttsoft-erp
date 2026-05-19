using System.Threading.Tasks;

namespace ERP.Application.Interfaces;

public interface INfeStatusService
{
    // 👇 Mudou de ConsultarStatusNfceAsync para ConsultarStatusNotaAsync
    Task<(bool Sucesso, string Status, string UrlDanfe)> ConsultarStatusNotaAsync(string referencia, string token, bool isProducao);
    
    Task<string> ConsultarMotivoRejeicaoAsync(string referencia, string token, bool isProducao);
}