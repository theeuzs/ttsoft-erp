using Blazored.LocalStorage;
using ERP.Portal.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

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

await builder.Build().RunAsync();
