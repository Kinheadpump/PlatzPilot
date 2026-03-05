using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace PlatzPilot.ViewModels;

public sealed partial class MainPageViewModel : ObservableObject
{
    private const string OnboardingCompletedKey = "HasCompletedOnboarding";

    public MainPageViewModel(
        SeatListViewModel seatList,
        FilterViewModel filters,
        NavigationViewModel navigation,
        SettingsViewModel settings)
    {

        SeatList = seatList;
        Filters = filters;
        Navigation = navigation;
        Settings = settings;

        var hasCompletedOnboarding = Preferences.Default.Get(OnboardingCompletedKey, false);
        IsOnboardingVisible = !hasCompletedOnboarding;

    #if DEBUG
        IsOnboardingVisible = true;
    #endif
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
        Preferences.Default.Set(OnboardingCompletedKey, true);
        IsOnboardingVisible = false;
    }
}
