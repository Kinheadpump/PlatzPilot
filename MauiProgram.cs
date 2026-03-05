using Microsoft.Extensions.Logging;
using Microcharts.Maui;
using PlatzPilot.Configuration;
using PlatzPilot.Services;
using PlatzPilot.ViewModels;
using PlatzPilot.Views;

namespace PlatzPilot;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var appConfig = AppConfigProvider.LoadFromEmbeddedResource();

        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMicrocharts()
            .ConfigureFonts(fonts =>
            {
                if (appConfig.Fonts.Entries.Count == 0)
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                    return;
                }

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
        builder.Services.AddHttpClient(SeatFinderService.HttpClientName);

        // ==========================================
        // 1. SERVICES
        // ==========================================
        // AddSingleton: Es wird nur EINE einzige Instanz für die gesamte App-Laufzeit erstellt.
        // Das spart Arbeitsspeicher und verhindert, dass wir zu viele HttpClients öffnen.
        builder.Services.AddSingleton<SeatFinderService>();
        builder.Services.AddSingleton<IStudySpaceFeatureService, StudySpaceFeatureService>();
        builder.Services.AddSingleton<SafeArrivalForecastService>();
        builder.Services.AddSingleton<MensaForecastService>();
        builder.Services.AddSingleton<INavigationService, NavigationService>();


        // ==========================================
        // 2. VIEWMODELS
        // ==========================================
        // AddSingleton: Die MainPage bleibt stabil und behält Cache/State.
        builder.Services.AddSingleton<FilterViewModel>();
        builder.Services.AddSingleton<NavigationViewModel>();
        builder.Services.AddSingleton<SettingsViewModel>();
        builder.Services.AddSingleton<SeatListViewModel>();
        builder.Services.AddSingleton<MainPageViewModel>();


        // ==========================================
        // 3. VIEWS 
        // ==========================================
        // Damit MAUI weiß, dass es beim Erstellen der MainPage das ViewModel automatisch übergeben soll.
        builder.Services.AddSingleton<MainPage>();
        builder.Services.AddTransient<DetailPage>();
        builder.Services.AddSingleton<AppShell>();


        return builder.Build();
    }
}
