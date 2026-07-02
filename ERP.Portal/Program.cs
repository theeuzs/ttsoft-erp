using Blazored.LocalStorage;
using ERP.Portal.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using System.Globalization;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<ERP.Portal.App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var apiUrl = builder.Configuration["ApiUrl"] ?? "https://erp-ttsoft-api-g8bde4f6aqcwb9aw.brazilsouth-01.azurewebsites.net";

builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(apiUrl) });
builder.Services.AddBlazoredLocalStorage();
builder.Services.AddScoped<ERP.Portal.Services.AuthService>();
builder.Services.AddScoped<CadastroApiService>();
builder.Services.AddScoped<ERP.Portal.Services.ApiClient>();
builder.Services.AddSingleton(sp =>
    new ERP.Portal.Services.PortalChatService(apiUrl));

// S13 FIX: força cultura pt-BR para que R$ apareça corretamente independente
// da locale do browser/SO. Sem isso, Blazor WASM usa a locale do browser —
// num computador europeu, ToString("C") vira € em vez de R$.
var culture = new CultureInfo("pt-BR");
CultureInfo.DefaultThreadCurrentCulture   = culture;
CultureInfo.DefaultThreadCurrentUICulture = culture;

await builder.Build().RunAsync();
