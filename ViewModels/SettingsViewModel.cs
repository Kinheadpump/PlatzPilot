using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PlatzPilot.Configuration;
using PlatzPilot.Services;

namespace PlatzPilot.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly AppConfig _config;
    private readonly INavigationService _navigationService;

    [ObservableProperty]
    private bool _isColorBlindMode;

    [ObservableProperty]
    private bool _isCampusSouthOnly;

    [ObservableProperty]
    private bool _isHapticFeedbackEnabled;

    [ObservableProperty]
    private bool _isHideClosedLocations;

    public SettingsViewModel(AppConfig config, INavigationService navigationService)
    {
        _config = config;
        _navigationService = navigationService;

        _isColorBlindMode = Preferences.Default.Get(_config.Preferences.ColorBlindModeKey, false);
        _isCampusSouthOnly = Preferences.Default.Get(_config.Preferences.CampusSouthOnlyKey, false);
        _isHapticFeedbackEnabled = Preferences.Default.Get(_config.Preferences.HapticFeedbackKey, true);
        _isHideClosedLocations = Preferences.Default.Get(_config.Preferences.HideClosedLocationsKey, false);

        ApplySavedTheme();
    }

    public string SettingsVersionText =>
        string.Format(System.Globalization.CultureInfo.CurrentCulture, _config.UiText.SettingsVersionFormat, _config.AppInfo.Version);

    [RelayCommand]
    private void ToggleTheme()
    {
        if (Application.Current == null)
        {
            return;
        }

        var currentTheme = Application.Current.RequestedTheme;
        var nextTheme = currentTheme == AppTheme.Dark ? AppTheme.Light : AppTheme.Dark;

        Application.Current.UserAppTheme = nextTheme;
        Preferences.Default.Set(_config.Preferences.ThemeKey, nextTheme == AppTheme.Light ? _config.Theme.Light : _config.Theme.Dark);
    }

    [RelayCommand]
    private void ToggleColorBlindMode()
    {
        IsColorBlindMode = !IsColorBlindMode;
    }

    [RelayCommand]
    private void ToggleCampusSouthOnly()
    {
        IsCampusSouthOnly = !IsCampusSouthOnly;
    }

    [RelayCommand]
    private void ToggleHapticFeedback()
    {
        IsHapticFeedbackEnabled = !IsHapticFeedbackEnabled;
    }

    [RelayCommand]
    private void ToggleHideClosedLocations()
    {
        IsHideClosedLocations = !IsHideClosedLocations;
    }

    [RelayCommand]
    private async Task OpenGithubAsync()
    {
        await _navigationService.OpenUrlAsync(_config.Urls.Github);
    }

    [RelayCommand]
    private async Task ShowImpressumAsync()
    {
        await _navigationService.OpenUrlAsync(_config.Urls.Impressum);
    }

    [RelayCommand]
    private async Task ShowPrivacyAsync()
    {
        await _navigationService.OpenUrlAsync(_config.Urls.Privacy);
    }

    [RelayCommand]
    private async Task ShowLicensesAsync()
    {
        await ShowDialogAsync(_config.UiText.LicensesTitle, _config.UiText.LicensesText);
    }

    partial void OnIsColorBlindModeChanged(bool value)
    {
        Preferences.Default.Set(_config.Preferences.ColorBlindModeKey, value);
    }

    partial void OnIsCampusSouthOnlyChanged(bool value)
    {
        Preferences.Default.Set(_config.Preferences.CampusSouthOnlyKey, value);
    }

    partial void OnIsHapticFeedbackEnabledChanged(bool value)
    {
        Preferences.Default.Set(_config.Preferences.HapticFeedbackKey, value);
    }

    partial void OnIsHideClosedLocationsChanged(bool value)
    {
        Preferences.Default.Set(_config.Preferences.HideClosedLocationsKey, value);
    }

    private void ApplySavedTheme()
    {
        if (Application.Current == null)
        {
            return;
        }

        var savedTheme = Preferences.Default.Get(_config.Preferences.ThemeKey, _config.Theme.System);
        Application.Current.UserAppTheme = savedTheme switch
        {
            var value when string.Equals(value, _config.Theme.Light, StringComparison.Ordinal) => AppTheme.Light,
            var value when string.Equals(value, _config.Theme.Dark, StringComparison.Ordinal) => AppTheme.Dark,
            _ => AppTheme.Unspecified
        };
    }

    private async Task ShowDialogAsync(string title, string message)
    {
        var page = Application.Current?.Windows.FirstOrDefault()?.Page;
        if (page == null)
        {
            return;
        }

        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            await page.DisplayAlertAsync(title, message, _config.UiText.OkButtonLabel);
        });
    }
}
