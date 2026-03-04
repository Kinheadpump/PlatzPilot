using Microsoft.Maui.ApplicationModel;
using PlatzPilot.Configuration;
using PlatzPilot.Views;

namespace PlatzPilot;

public partial class App : Application
{
    private readonly Task<AppConfig> _configLoadTask;

    public App()
    {
        InitializeComponent();
        _configLoadTask = AppConfigProvider.LoadFromPackageAsync();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(new StartupPage());
        _ = ShowShellWhenReadyAsync(window);
        return window;
    }

    private async Task ShowShellWhenReadyAsync(Window window)
    {
        try
        {
            await _configLoadTask.ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Load errors are already logged in AppConfigProvider.
        }

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            window.Page = new AppShell();
        });
    }
}
