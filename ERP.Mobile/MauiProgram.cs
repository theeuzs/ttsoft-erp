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

        var apiUrl = "http://192.168.1.100:5000";

        builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(apiUrl) });
        builder.Services.AddBlazoredLocalStorage();
        builder.Services.AddScoped<MobileApiService>();
        builder.Services.AddScoped<MobileAuthService>();

        return builder.Build();
    }
}