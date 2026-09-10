using ERP.Domain.Common;

namespace ERP.Domain.Entities;

/// <summary>
/// Achado (09/09) — Metas de Vendas e Pontos de Fidelidade já existiam no
/// código (backend completo + integração real no PDV e em Finalizar Venda),
/// mas sem nenhum interruptor por tenant: Metas de Vendas aparecia de forma
/// IMPLÍCITA (só se existisse uma linha em MetasVendas pro mês/vendedor —
/// frágil, um dado acidental faz a feature aparecer sem ninguém ter pedido),
/// e Pontos de Fidelidade não tinha controle nenhum (o botão "Usar
/// Fidelidade" sempre aparecia em Finalizar Venda, pra todo tenant).
///
/// Uma linha por tenant, mesmo padrão do TenantFiscalConfiguration — só que
/// pra features em vez de configuração fiscal.
/// </summary>
public class TenantFeatureFlags : BaseEntity
{
    public bool MetasVendasHabilitado      { get; set; } = false;
    public bool PontosFidelidadeHabilitado { get; set; } = false;
}
