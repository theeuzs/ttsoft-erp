using System.Threading.Tasks;

namespace ERP.Application.Interfaces;

/// <summary>
/// Item 6 do roadmap fiscal — Carta de Correção Eletrônica. Só existe pra
/// NF-e (não NFC-e — a SEFAZ não prevê esse evento pro documento
/// simplificado). Corrige erro formal (descrição, endereço) sem precisar
/// cancelar a nota — hoje, sem isso, todo erro de descrição vira
/// cancelamento, que é bem mais grave numa venda B2B.
/// </summary>
public interface INfeCorrecaoService
{
    Task<(bool Sucesso, string Mensagem, string? UrlPdf)> EmitirCartaCorrecaoAsync(
        string referencia, string textoCorrecao, string token, bool isProducao);
}
