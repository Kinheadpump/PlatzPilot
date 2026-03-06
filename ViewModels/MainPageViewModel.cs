using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PlatzPilot.Services;

namespace PlatzPilot.ViewModels;

public sealed partial class MainPageViewModel : ObservableObject
{
    private const string OnboardingCompletedKey = "HasCompletedOnboarding";
    private readonly IPreferencesService _preferencesService;

    public MainPageViewModel(
        SeatListViewModel seatList,
        FilterViewModel filters,
        NavigationViewModel navigation,
        SettingsViewModel settings,
        IPreferencesService preferencesService)
    {
        _preferencesService = preferencesService;

        SeatList = seatList;
        Filters = filters;
        Navigation = navigation;
        Settings = settings;

        var hasCompletedOnboarding = _preferencesService.Get(OnboardingCompletedKey, false);
        IsOnboardingVisible = !hasCompletedOnboarding;
    }

    public SeatListViewModel SeatList { get; }
    public FilterViewModel Filters { get; }
    public NavigationViewModel Navigation { get; }
    public SettingsViewModel Settings { get; }

    private bool _isOnboardingVisible;

    public bool IsOnboardingVisible
    {
        get => _isOnboardingVisible;
        set => SetProperty(ref _isOnboardingVisible, value);
    }
    
    [RelayCommand]
    private void CompleteOnboarding()
    {
        _preferencesService.Set(OnboardingCompletedKey, true);
        IsOnboardingVisible = false;
    }
}
