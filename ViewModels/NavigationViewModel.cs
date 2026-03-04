using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PlatzPilot.Configuration;

namespace PlatzPilot.ViewModels;

public partial class NavigationViewModel : ObservableObject
{
    private readonly AppConfig _config;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMainContentVisible))]
    [NotifyPropertyChangedFor(nameof(IsSettingsContentVisible))]
    private string _currentTab = string.Empty;

    public NavigationViewModel(AppConfig config)
    {
        _config = config;
        _currentTab = ResolveInitialTab(Preferences.Default.Get(_config.Preferences.TabModeKey, _config.Tabs.Home));
    }

    public bool IsMainContentVisible => CurrentTab != _config.Tabs.Settings;
    public bool IsSettingsContentVisible => CurrentTab == _config.Tabs.Settings;

    [RelayCommand]
    private void SwitchTab(string tabName)
    {
        tabName = NormalizeTab(tabName);
        CurrentTab = tabName;
        Preferences.Default.Set(_config.Preferences.TabModeKey, tabName);
    }

    private string NormalizeTab(string tabName)
    {
        if (string.Equals(tabName, _config.Tabs.Home, StringComparison.Ordinal))
        {
            return _config.Tabs.Home;
        }

        if (string.Equals(tabName, _config.Tabs.Favorites, StringComparison.Ordinal))
        {
            return _config.Tabs.Favorites;
        }

        if (string.Equals(tabName, _config.Tabs.Settings, StringComparison.Ordinal))
        {
            return _config.Tabs.Settings;
        }

        return _config.Tabs.Home;
    }

    private string ResolveInitialTab(string tabName)
    {
        var normalizedTab = NormalizeTab(tabName);
        return string.Equals(normalizedTab, _config.Tabs.Settings, StringComparison.Ordinal)
            ? _config.Tabs.Home
            : normalizedTab;
    }
}
