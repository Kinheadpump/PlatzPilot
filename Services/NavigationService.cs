using PlatzPilot.Configuration;
using PlatzPilot.Models;

namespace PlatzPilot.Services;

public interface INavigationService
{
    Task NavigateToDetailAsync(UiLocation location);
    Task OpenUrlAsync(string url);
}

public sealed class NavigationService : INavigationService
{
    private readonly InternalConfig _internal;

    public NavigationService(AppConfig config)
    {
        _internal = config.Internal;
    }

    public Task NavigateToDetailAsync(UiLocation location)
    {
        if (location == null)
        {
            return Task.CompletedTask;
        }

        return Shell.Current.GoToAsync(
            _internal.DetailPageRoute,
            new Dictionary<string, object> { { _internal.LocationDataKey, location } });
    }

    public Task OpenUrlAsync(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return Task.CompletedTask;
        }

        return Browser.Default.OpenAsync(url, BrowserLaunchMode.SystemPreferred);
    }
}
