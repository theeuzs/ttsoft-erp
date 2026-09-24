using ERP.Application.DTOs.FocusNfe;
using System.Threading.Tasks;
namespace ERP.Application.Interfaces;

public interface INfeEmissionService
{
    // Mesmo achado/fix do INfceEmissionService — ver comentário lá.
    Task<(bool Sucesso, string Mensagem, string UrlDanfe, string UrlXml, string Chave, string Numero)> EmitirNfeA4Async(string referencia, FocusNfceRequest nfe, string token, bool isProducao);
}