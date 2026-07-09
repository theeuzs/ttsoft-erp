using Blazored.LocalStorage;
using ERP.Mobile.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Debug;
using Microsoft.Maui.Controls.Hosting;
using Microsoft.Maui.Hosting;

namespace ERP.Mobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular"));

        builder.Services.AddMauiBlazorWebView();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        // S15 FIX: "http://192.168.1.100:5000" estava hardcoded aqui — funcionava
        // só na rede local de quem escreveu o código, travava qualquer outro
        // teste/deploy. Agora diferencia por build config:
        //   DEBUG:   10.0.2.2 é o alias padrão do emulador Android para o
        //            localhost da máquina host (convenção conhecida, não um IP
        //            específico de uma LAN). Testando em device físico ou iOS
        //            simulator, troque manualmente pelo IP da sua máquina na
        //            rede local durante o teste.
        //   RELEASE: aponta direto para a API de produção do ConstruTTor.
#if DEBUG
        var apiUrl = "http://10.0.2.2:5000";
#else
        var apiUrl = "https://erp-ttsoft-api-g8bde4f6aqcwb9aw.brazilsouth-01.azurewebsites.net";
#endif

        builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(apiUrl) });
        builder.Services.AddBlazoredLocalStorage();
        builder.Services.AddScoped<MobileApiService>();
        builder.Services.AddScoped<MobileAuthService>();

        return builder.Build();
    }
}