using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using PlatzPilot.Messages;
using PlatzPilot.Services;

namespace PlatzPilot.ViewModels;

public sealed partial class MainPageViewModel : ObservableObject
{
    private const string OnboardingCompletedKey = "HasCompletedOnboarding";
    private readonly IPreferencesService _preferencesService;
    private DateTime _lastRefreshTime = DateTime.MinValue;

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

        SeatList.PropertyChanged += OnSeatListPropertyChanged;
        WeakReferenceMessenger.Default.Register<AppResumedMessage>(this, (_, _) => HandleAppResumed());

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

    private void HandleAppResumed()
    {
        if (DateTime.Now - _lastRefreshTime <= TimeSpan.FromMinutes(5))
        {
            return;
        }

        if (SeatList.LoadSpacesCommand.CanExecute(null))
        {
            SeatList.LoadSpacesCommand.Execute(null);
            _lastRefreshTime = DateTime.Now;
        }
    }

    private void OnSeatListPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(SeatListViewModel.IsRefreshing))
        {
            return;
        }

        if (SeatList.IsRefreshing)
        {
            _lastRefreshTime = DateTime.Now;
        }
    }
    
    [RelayCommand]
    private void CompleteOnboarding()
    {
        _preferencesService.Set(OnboardingCompletedKey, true);
        IsOnboardingVisible = false;
    }
}
