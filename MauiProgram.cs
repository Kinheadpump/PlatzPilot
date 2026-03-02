using Microsoft.Extensions.Logging;
using PlatzPilot.Configuration;
using PlatzPilot.Services;
using PlatzPilot.ViewModels;
using PlatzPilot.Views;

namespace PlatzPilot;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var appConfig = AppConfigProvider.LoadFromPackage();

        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                foreach (var font in appConfig.Fonts.Entries)
                {
                    if (string.IsNullOrWhiteSpace(font.FileName) ||
                        string.IsNullOrWhiteSpace(font.Alias))
                    {
                        continue;
                    }

                    fonts.AddFont(font.FileName, font.Alias);
                }
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        builder.Services.AddSingleton(appConfig);

        // ==========================================
        // 1. SERVICES
        // ==========================================
        // AddSingleton: Es wird nur EINE einzige Instanz für die gesamte App-Laufzeit erstellt.
        // Das spart Arbeitsspeicher und verhindert, dass wir zu viele HttpClients öffnen.
        builder.Services.AddSingleton<SeatFinderService>();
        builder.Services.AddSingleton<SafeArrivalForecastService>();


        // ==========================================
        // 2. VIEWMODELS
        // ==========================================
        // AddTransient: Jedes Mal, wenn wir die Seite aufrufen, wird ein frisches ViewModel erstellt.
        builder.Services.AddTransient<MainPageViewModel>();


        // ==========================================
        // 3. VIEWS 
        // ==========================================
        // Damit MAUI weiß, dass es beim Erstellen der MainPage das ViewModel automatisch übergeben soll.
        builder.Services.AddTransient<MainPage>();
        builder.Services.AddTransient<DetailPage>();


        return builder.Build();
    }
}
