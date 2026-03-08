using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using PlatzPilot.Configuration;
using PlatzPilot.Constants;
using PlatzPilot.Messages;
using PlatzPilot.Services;
using System.Globalization;
using System.Threading;
using PlatzPilot.Localization;
using PlatzPilot.Resources.Strings;

namespace PlatzPilot.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private const string _crashReportOptOutKey = "CrashReportOptOut";
    private readonly AppConfig _config;
    private readonly INavigationService _navigationService;
    private readonly IPreferencesService _preferencesService;

    private bool _isColorBlindMode;
    private bool _isCampusSouthOnly;
    private bool _isHapticFeedbackEnabled;
    private bool _isHideClosedLocations;
    private bool _isAboutOpen;
    private int _debugClickCount = 0;

    public bool IsColorBlindMode
    {
        get => _isColorBlindMode;
        set
        {
            if (SetProperty(ref _isColorBlindMode, value))
            {
                OnIsColorBlindModeChanged(value);
            }
        }
    }

    public bool IsCampusSouthOnly
    {
        get => _isCampusSouthOnly;
        set
        {
            if (SetProperty(ref _isCampusSouthOnly, value))
            {
                OnIsCampusSouthOnlyChanged(value);
            }
        }
    }

    public bool IsHapticFeedbackEnabled
    {
        get => _isHapticFeedbackEnabled;
        set
        {
            if (SetProperty(ref _isHapticFeedbackEnabled, value))
            {
                OnIsHapticFeedbackEnabledChanged(value);
            }
        }
    }

    public bool IsHideClosedLocations
    {
        get => _isHideClosedLocations;
        set
        {
            if (SetProperty(ref _isHideClosedLocations, value))
            {
                OnIsHideClosedLocationsChanged(value);
            }
        }
    }

    public bool IsAboutOpen
    {
        get => _isAboutOpen;
        set => SetProperty(ref _isAboutOpen, value);
    }

    public bool IsCrashReportEnabled
    {
        get => !Preferences.Default.Get(_crashReportOptOutKey, false);
        set
        {
            var shouldOptOut = !value;
            var currentOptOut = Preferences.Default.Get(_crashReportOptOutKey, false);
            if (shouldOptOut == currentOptOut)
            {
                return;
            }

            Preferences.Default.Set(_crashReportOptOutKey, shouldOptOut);
            OnPropertyChanged();
            OnPropertyChanged(nameof(CrashReportIcon));
        }
    }

    public string CrashReportIcon
    {
        get
        {
            var isDarkTheme = Application.Current?.RequestedTheme == AppTheme.Dark;
            if (IsCrashReportEnabled)
            {
                return isDarkTheme ? AppAssets.CampusSouthOnIconDark : AppAssets.CampusSouthOnIconLight;
            }

            return isDarkTheme ? AppAssets.CampusSouthOffIconDark : AppAssets.CampusSouthOffIconLight;
        }
    }

    public SettingsViewModel(
        AppConfig config,
        INavigationService navigationService,
        IPreferencesService preferencesService)
    {
        _config = config;
        _navigationService = navigationService;
        _preferencesService = preferencesService;

        _isColorBlindMode = _preferencesService.Get(_config.Preferences.ColorBlindModeKey, false);
        _isCampusSouthOnly = _preferencesService.Get(_config.Preferences.CampusSouthOnlyKey, false);
        _isHapticFeedbackEnabled = _preferencesService.Get(_config.Preferences.HapticFeedbackKey, true);
        _isHideClosedLocations = _preferencesService.Get(_config.Preferences.HideClosedLocationsKey, false);

        ApplySavedTheme();

        WeakReferenceMessenger.Default.Register<CrashReportSettingsChangedMessage>(this, (_, _) =>
        {
            OnPropertyChanged(nameof(IsCrashReportEnabled));
            OnPropertyChanged(nameof(CrashReportIcon));
        });
    }

    public string AboutAppName =>
        string.IsNullOrWhiteSpace(AppInfo.Name) ? _config.AppInfo.Name : AppInfo.Name;

    public string AboutVersionText
    {
        get
        {
            var version = string.IsNullOrWhiteSpace(AppInfo.VersionString) ? _config.AppInfo.Version : AppInfo.VersionString;
            var build = string.IsNullOrWhiteSpace(AppInfo.BuildString) ? "0" : AppInfo.BuildString;

            return string.Format(CultureInfo.CurrentCulture, AppResources.SettingsVersionFormat, version, build);
        }
    }

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
        _preferencesService.Set(_config.Preferences.ThemeKey, nextTheme == AppTheme.Light ? _config.Theme.Light : _config.Theme.Dark);
        OnPropertyChanged(nameof(CrashReportIcon));
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
    private void ToggleCrashReport()
    {
        IsCrashReportEnabled = !IsCrashReportEnabled;
    }

    [RelayCommand]
    private void SecretCrashTap()
    {
        _debugClickCount++;
        if (_debugClickCount < 5)
        {
            return;
        }

        _debugClickCount = 0;
        ThreadPool.QueueUserWorkItem(_ => throw new Exception("Secret crash trigger (test)."));
    }

    [RelayCommand]
    private async Task ChangeLanguageAsync()
    {
        var page = Application.Current?.Windows.FirstOrDefault()?.Page;
        if (page == null)
        {
            return;
        }

        var selection = await MainThread.InvokeOnMainThreadAsync(() =>
            page.DisplayActionSheetAsync(
                AppResources.LanguageSelectTitle,
                AppResources.LanguageOptionCancel,
                null,
                AppResources.LanguageOptionGerman,
                AppResources.LanguageOptionEnglish));

        if (string.IsNullOrWhiteSpace(selection) ||
            string.Equals(selection, AppResources.LanguageOptionCancel, StringComparison.Ordinal))
        {
            return;
        }

        var cultureCode = string.Equals(selection, AppResources.LanguageOptionGerman, StringComparison.Ordinal)
            ? "de"
            : "en";

        LocalizationResourceManager.Instance.SetCulture(new CultureInfo(cultureCode));
        _preferencesService.Set(_config.Preferences.LanguageKey, cultureCode);
    }

    [RelayCommand]
    private void OpenAbout()
    {
        IsAboutOpen = true;
    }

    [RelayCommand]
    private void CloseAbout()
    {
        IsAboutOpen = false;
    }

    [RelayCommand]
    private async Task OpenGithubAsync()
    {
        CloseAboutIfOpen();
        await _navigationService.OpenUrlAsync(_config.Urls.Github);
    }

    [RelayCommand]
    private async Task ShowImpressumAsync()
    {
        CloseAboutIfOpen();
        await _navigationService.OpenUrlAsync(_config.Urls.Impressum);
    }

    [RelayCommand]
    private async Task ShowPrivacyAsync()
    {
        CloseAboutIfOpen();
        await _navigationService.OpenUrlAsync(_config.Urls.Privacy);
    }

    [RelayCommand]
    private async Task ShowLicensesAsync()
    {
        CloseAboutIfOpen();
        await ShowDialogAsync(AppResources.LicensesTitle, AppResources.LicensesText);
    }

    private void OnIsColorBlindModeChanged(bool value)
    {
        _preferencesService.Set(_config.Preferences.ColorBlindModeKey, value);
    }

    private void OnIsCampusSouthOnlyChanged(bool value)
    {
        _preferencesService.Set(_config.Preferences.CampusSouthOnlyKey, value);
    }

    private void OnIsHapticFeedbackEnabledChanged(bool value)
    {
        _preferencesService.Set(_config.Preferences.HapticFeedbackKey, value);
    }

    private void OnIsHideClosedLocationsChanged(bool value)
    {
        _preferencesService.Set(_config.Preferences.HideClosedLocationsKey, value);
    }

    private void ApplySavedTheme()
    {
        if (Application.Current == null)
        {
            return;
        }

        var savedTheme = _preferencesService.Get(_config.Preferences.ThemeKey, _config.Theme.System);
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
            await page.DisplayAlertAsync(title, message, AppResources.OkButtonLabel);
        });
    }

    private void CloseAboutIfOpen()
    {
        if (IsAboutOpen)
        {
            IsAboutOpen = false;
        }
    }
}

