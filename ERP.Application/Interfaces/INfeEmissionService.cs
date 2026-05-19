using ERP.Application.DTOs.FocusNfe;
using System.Threading.Tasks;
namespace ERP.Application.Interfaces;

public interface INfeEmissionService
{
    Task<(bool Sucesso, string Mensagem, string UrlDanfe)> EmitirNfeA4Async(string referencia, FocusNfceRequest nfe, string token, bool isProducao);
}