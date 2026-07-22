// ── ERP.Domain/Entities/SalesChannel.cs ───────────────────────────────────────
using ERP.Domain.Common;
using ERP.Domain.Enums;

namespace ERP.Domain.Entities;

/// <summary>
/// Uma conexão de canal de venda (Mercado Livre, Shopee) pertencente a um tenant.
/// Guarda a credencial OAuth por tenant — substitui a credencial única e global
/// que existe hoje no appsettings (ver Roadmap, item 3.1).
/// </summary>
public class SalesChannel : BaseEntity
{
    public SalesChannelType Tipo       { get; set; }
    public string           Nome       { get; set; } = string.Empty; // ex: "Mercado Livre - Loja X"
    public bool              IsAtivo    { get; set; } = true;

    /// <summary>O que este canal sabe fazer — usado pelo ProcessingSession para pular etapas.</summary>
    public ChannelCapability Capacidades { get; set; } = ChannelCapability.None;

    /// <summary>Identificador da conta do vendedor no marketplace (seller id).</summary>
    public string? ExternalAccountId { get; set; }

    public string? AccessToken   { get; set; }
    public string? RefreshToken  { get; set; }
    public DateTime? TokenExpiraEm { get; set; }

    /// <summary>
    /// Cliente sintético que representa o próprio marketplace como devedor
    /// (ex: "Mercado Livre - Repasse") — usado pra gerar a Conta a Receber
    /// "Aguardando Repasse" via ContaReceberService.GerarContaAPrazoAsync,
    /// sem tocar no schema de ContaReceber (CustomerId lá é obrigatório).
    /// Resolvido/criado sob demanda pelo OrderProcessingService na primeira
    /// venda do canal, não precisa de tela de configuração ainda.
    /// </summary>
    public Guid?     ClienteRepasseId { get; set; }
    public Customer? ClienteRepasse   { get; set; }

    /// <summary>
    /// Usuário atribuído às vendas geradas automaticamente por este canal —
    /// não abre caixa (Marketplace não exige, ver ISalePolicyService), só
    /// existe pra toda venda ter uma origem rastreável (princípio #1 do roadmap).
    /// </summary>
    public Guid? UsuarioIntegracaoId { get; set; }

    public List<SalesChannelPricingPolicy> PoliticasDePreco { get; set; } = new();
    public List<SkuMapping>                MapeamentosSku    { get; set; } = new();
}