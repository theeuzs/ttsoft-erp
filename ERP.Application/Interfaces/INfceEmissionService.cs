using ERP.Application.DTOs.FocusNfe;
using FluentResults;
using System.Threading.Tasks;

namespace ERP.Application.Interfaces;

public interface INfceEmissionService
{
    // O retorno agora é uma tupla elegante com a URL e o Status, envelopada no FluentResults
    Task<(bool Sucesso, string Mensagem, string UrlDanfe)> EmitirNfceAsync(string referencia, FocusNfceRequest nfce, string token, bool isProducao);
}