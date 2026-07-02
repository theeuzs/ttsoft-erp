using ERP.Application.Interfaces;
using ERP.Persistence.Context;

namespace ERP.WPF.Services;

/// <summary>
/// Implementação de IRequestTenant para o WPF Desktop.
///
/// Na API, IRequestTenant é preenchido por TenantMiddleware a partir do JWT
/// de cada requisição HTTP (scoped por requisição).
///
/// No WPF não há HTTP — o tenant é fixo por processo (lido do licenca.json
/// no startup via TenantService.GetCurrentTenantId). Esta classe lê os
/// valores estáticos do AppDbContext que o App.xaml.cs já preenche no login.
///
/// Isso permite que HaverService e FidelidadeService usem a mesma injeção
/// de dependência que a API, sem precisar de IServiceProvider/CreateScope.
/// </summary>
public class WpfRequestTenant : IRequestTenant
{
    public Guid    TenantId              { get => AppDbContext.GetGlobalTenantId(); set { } }
    public Guid    UserId                { get => AppDbContext.GetCurrentUserId();   set { } }
    public string  UserName              { get => AppDbContext.GetCurrentUserName(); set { } }
    // S9: lê o limite de desconto da sessão WPF — populado pelo AppSession.Login após autenticação.
    public decimal MaxDiscountPercentage { get => ERP.WPF.State.AppSession.MaxDiscountPercentage; set { } }
    // S13: lê o limite de sangria da sessão WPF — mesmo padrão do MaxDiscountPercentage.
    public decimal MaxSangriaValue       { get => ERP.WPF.State.AppSession.MaxSangriaValue;       set { } }
}