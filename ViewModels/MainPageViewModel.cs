using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using PlatzPilot.Configuration;
using PlatzPilot.Messages;
using PlatzPilot.Services;

namespace PlatzPilot.ViewModels;

public sealed partial class MainPageViewModel : ObservableObject, IDisposable
{
    private const string _onboardingCompletedKey = "HasCompletedOnboarding";
    private readonly AppConfig _config;
    private readonly IPreferencesService _preferencesService;
    private DateTime _lastRefreshTime = DateTime.MinValue;
    private CityConfig? _selectedCity;
    private bool _disposed;

    public MainPageViewModel(
        SeatListViewModel seatList,
        FilterViewModel filters,
        NavigationViewModel navigation,
        SettingsViewModel settings,
        AppConfig config,
        IPreferencesService preferencesService)
    {
        _config = config;
        _preferencesService = preferencesService;

        SeatList = seatList;
        Filters = filters;
        Navigation = navigation;
        Settings = settings;

        SeatList.PropertyChanged += OnSeatListPropertyChanged;
        WeakReferenceMessenger.Default.Register<AppResumedMessage>(this, (_, _) =>
            MainThreadHelper.BeginInvoke(HandleAppResumed));
        WeakReferenceMessenger.Default.Register<CityChangedMessage>(this, (_, _) =>
            MainThreadHelper.BeginInvoke(UpdateSelectedCityFromPreferences));

        foreach (var city in _config.SeatFinder.Cities.OrderBy(city => city.DisplayName))
        {
            AvailableCities.Add(city);
        }

        UpdateSelectedCityFromPreferences();

        var hasCompletedOnboarding = _preferencesService.Get(_onboardingCompletedKey, false);
        IsOnboardingVisible = !hasCompletedOnboarding;
    }

    public SeatListViewModel SeatList { get; }
    public FilterViewModel Filters { get; }
    public NavigationViewModel Navigation { get; }
    public SettingsViewModel Settings { get; }

    public ObservableCollection<CityConfig> AvailableCities { get; } = new();

    public CityConfig? SelectedCity
    {
        get => _selectedCity;
        set
        {
            if (SetProperty(ref _selectedCity, value) && value != null)
            {
                _preferencesService.SelectedCityId = value.Id;
                WeakReferenceMessenger.Default.Send(new CityChangedMessage());
            }
        }
    }

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
        _preferencesService.Set(_onboardingCompletedKey, true);
        IsOnboardingVisible = false;

        // Lade die Daten der gewählten Stadt herunter
        if (SeatList.LoadSpacesCommand.CanExecute(null))
        {
            SeatList.LoadSpacesCommand.Execute(null);
        }
    }

    private void UpdateSelectedCityFromPreferences()
    {
        var selectedCityId = _preferencesService.SelectedCityId;
        var city = AvailableCities.FirstOrDefault(c => string.Equals(c.Id, selectedCityId, StringComparison.OrdinalIgnoreCase))
            ?? AvailableCities.FirstOrDefault();

        if (ReferenceEquals(_selectedCity, city))
        {
            return;
        }

        _selectedCity = city;
        OnPropertyChanged(nameof(SelectedCity));
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        SeatList.PropertyChanged -= OnSeatListPropertyChanged;
        WeakReferenceMessenger.Default.UnregisterAll(this);
    }
}
