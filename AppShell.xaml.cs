using PlatzPilot.Configuration;
using PlatzPilot.Services;
using PlatzPilot.Views;

namespace PlatzPilot;

public partial class AppShell : Shell
{
    private bool _crashReportChecked;

    public AppShell()
    {
        InitializeComponent();

        var routes = AppConfigProvider.Current.Internal;
        Routing.RegisterRoute(routes.MainPageRoute, typeof(MainPage));
        Routing.RegisterRoute(routes.DetailPageRoute, typeof(DetailPage));
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_crashReportChecked)
        {
            return;
        }

        _crashReportChecked = true;

        var webhookUrl = AppConfigProvider.Current.Urls.DiscordCrashWebhook;
        if (!string.IsNullOrWhiteSpace(webhookUrl))
        {
            await CrashHandler.CheckForCrashReportAsync(webhookUrl);
        }
    }
}
