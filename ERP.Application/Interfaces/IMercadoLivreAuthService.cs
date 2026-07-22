using ERP.Domain.Entities;

namespace ERP.Application.Interfaces;

/// <summary>
/// Fluxo OAuth do Mercado Livre (Authorization Code, server-side). Um app só
/// (client_id/client_secret configurado uma vez) — cada SalesChannel (cada
/// loja) autoriza esse mesmo app separadamente e ganha seu próprio
/// AccessToken/RefreshToken, guardados na própria linha do SalesChannel.
/// </summary>
public interface IMercadoLivreAuthService
{
    /// <summary>Monta a URL pra onde o dono da loja deve ser redirecionado pra autorizar o app.</summary>
    string ObterUrlAutorizacao(Guid salesChannelId);

    /// <summary>Troca o "code" recebido no callback por access_token/refresh_token e salva no SalesChannel.</summary>
    Task<(bool Sucesso, string Mensagem)> TrocarCodigoPorTokenAsync(string code, Guid salesChannelId);

    /// <summary>Renova o access_token usando o refresh_token guardado — chamado quando o token expira.</summary>
    Task<(bool Sucesso, string Mensagem)> RenovarTokenAsync(SalesChannel canal);
}
