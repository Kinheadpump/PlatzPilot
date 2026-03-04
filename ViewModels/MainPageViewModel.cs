using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PlatzPilot.Models;

namespace PlatzPilot.ViewModels;

public partial class MainPageViewModel : ObservableObject
{
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

        SeatList.PropertyChanged += ForwardPropertyChanged;
        Filters.PropertyChanged += ForwardPropertyChanged;
        Navigation.PropertyChanged += ForwardPropertyChanged;
        Settings.PropertyChanged += ForwardPropertyChanged;
    }

    public SeatListViewModel SeatList { get; }
    public FilterViewModel Filters { get; }
    public NavigationViewModel Navigation { get; }
    public SettingsViewModel Settings { get; }

    public ObservableCollection<UiLocation> UiLocations
    {
        get => SeatList.UiLocations;
        set => SeatList.UiLocations = value;
    }

    public UiLocation? SelectedLocation
    {
        get => SeatList.SelectedLocation;
        set => SeatList.SelectedLocation = value;
    }

    public bool IsBusy
    {
        get => SeatList.IsBusy;
        set => SeatList.IsBusy = value;
    }

    public bool IsRefreshing
    {
        get => SeatList.IsRefreshing;
        set => SeatList.IsRefreshing = value;
    }

    public bool IsFilterExpanded
    {
        get => Filters.IsFilterExpanded;
        set => Filters.IsFilterExpanded = value;
    }

    public bool IsSearchActive
    {
        get => Filters.IsSearchActive;
        set => Filters.IsSearchActive = value;
    }

    public string SearchText
    {
        get => Filters.SearchText;
        set => Filters.SearchText = value;
    }

    public bool IsColorBlindMode
    {
        get => Settings.IsColorBlindMode;
        set => Settings.IsColorBlindMode = value;
    }

    public bool IsCampusSouthOnly
    {
        get => Settings.IsCampusSouthOnly;
        set => Settings.IsCampusSouthOnly = value;
    }

    public bool IsHapticFeedbackEnabled
    {
        get => Settings.IsHapticFeedbackEnabled;
        set => Settings.IsHapticFeedbackEnabled = value;
    }

    public bool IsHideClosedLocations
    {
        get => Settings.IsHideClosedLocations;
        set => Settings.IsHideClosedLocations = value;
    }

    public bool IsOfflineBannerVisible
    {
        get => SeatList.IsOfflineBannerVisible;
        set => SeatList.IsOfflineBannerVisible = value;
    }

    public SafeArrivalRecommendation? MensaSafeArrivalRecommendation
    {
        get => SeatList.MensaSafeArrivalRecommendation;
        set => SeatList.MensaSafeArrivalRecommendation = value;
    }

    public string MensaFluxLabel
    {
        get => SeatList.MensaFluxLabel;
        set => SeatList.MensaFluxLabel = value;
    }

    public bool UseNow
    {
        get => Filters.UseNow;
        set => Filters.UseNow = value;
    }

    public DateTime SelectedDate
    {
        get => Filters.SelectedDate;
        set => Filters.SelectedDate = value;
    }

    public TimeSpan SelectedTime
    {
        get => Filters.SelectedTime;
        set => Filters.SelectedTime = value;
    }

    public bool IsGroupRoomSelected
    {
        get => Filters.IsGroupRoomSelected;
        set => Filters.IsGroupRoomSelected = value;
    }

    public bool IsSilentStudySelected
    {
        get => Filters.IsSilentStudySelected;
        set => Filters.IsSilentStudySelected = value;
    }

    public bool IsNoReservationSelected
    {
        get => Filters.IsNoReservationSelected;
        set => Filters.IsNoReservationSelected = value;
    }

    public bool RequireFreeWifi
    {
        get => Filters.RequireFreeWifi;
        set => Filters.RequireFreeWifi = value;
    }

    public bool RequirePowerOutlets
    {
        get => Filters.RequirePowerOutlets;
        set => Filters.RequirePowerOutlets = value;
    }

    public bool RequireWhiteboard
    {
        get => Filters.RequireWhiteboard;
        set => Filters.RequireWhiteboard = value;
    }

    public double MinimumOpenHours
    {
        get => Filters.MinimumOpenHours;
        set => Filters.MinimumOpenHours = value;
    }

    public int FilteredLocationCount
    {
        get => SeatList.FilteredLocationCount;
        set => SeatList.FilteredLocationCount = value;
    }

    public string CurrentTab
    {
        get => Navigation.CurrentTab;
        set => Navigation.CurrentTab = value;
    }

    public string SelectedSortOption
    {
        get => Filters.SelectedSortOption;
        set => Filters.SelectedSortOption = value;
    }

    public List<string> SortOptions => Filters.SortOptions;

    public bool IsMainContentVisible => Navigation.IsMainContentVisible;
    public bool IsSettingsContentVisible => Navigation.IsSettingsContentVisible;
    public bool IsDataVisible => SeatList.IsDataVisible;
    public bool IsListEmpty => SeatList.IsListEmpty;
    public bool IsHomeEmpty => SeatList.IsHomeEmpty;
    public bool IsFavoritesEmpty => SeatList.IsFavoritesEmpty;
    public bool IsNoResultsEmpty => SeatList.IsNoResultsEmpty;
    public string EmptyStateTitle => SeatList.EmptyStateTitle;
    public string EmptyStateSubtitle => SeatList.EmptyStateSubtitle;
    public bool IsEmptySubtitleVisible => SeatList.IsEmptySubtitleVisible;
    public bool IsBeforeMode => Filters.IsBeforeMode;
    public bool IsSearchInactive => Filters.IsSearchInactive;
    public DateTime MaxSelectableDate => Filters.MaxSelectableDate;
    public double MinimumOpenHoursMin => Filters.MinimumOpenHoursMin;
    public double MinimumOpenHoursMax => Filters.MinimumOpenHoursMax;
    public string MinimumOpenHoursText => Filters.MinimumOpenHoursText;
    public string ShowResultsButtonText => SeatList.ShowResultsButtonText;
    public string SettingsVersionText => Settings.SettingsVersionText;
    public static string WelcomeMessage => SeatListViewModel.WelcomeMessage;

    public IRelayCommand ToggleSearchCommand => Filters.ToggleSearchCommand;
    public IRelayCommand ToggleFilterCommand => Filters.ToggleFilterCommand;
    public IRelayCommand CloseFilterSheetCommand => Filters.CloseFilterSheetCommand;
    public IAsyncRelayCommand ResetFiltersCommand => SeatList.ResetFiltersCommand;
    public IAsyncRelayCommand ApplySheetFiltersCommand => SeatList.ApplySheetFiltersCommand;
    public IAsyncRelayCommand SetWhenNowCommand => Filters.SetWhenNowCommand;
    public IAsyncRelayCommand SetWhenBeforeCommand => Filters.SetWhenBeforeCommand;
    public IRelayCommand<string> SwitchTabCommand => Navigation.SwitchTabCommand;
    public IRelayCommand<UiLocation> ToggleFavoriteCommand => SeatList.ToggleFavoriteCommand;
    public IRelayCommand ToggleThemeCommand => Settings.ToggleThemeCommand;
    public IRelayCommand ToggleColorBlindModeCommand => Settings.ToggleColorBlindModeCommand;
    public IRelayCommand ToggleCampusSouthOnlyCommand => Settings.ToggleCampusSouthOnlyCommand;
    public IRelayCommand ToggleHapticFeedbackCommand => Settings.ToggleHapticFeedbackCommand;
    public IRelayCommand ToggleHideClosedLocationsCommand => Settings.ToggleHideClosedLocationsCommand;
    public IAsyncRelayCommand OpenGithubCommand => Settings.OpenGithubCommand;
    public IAsyncRelayCommand ShowImpressumCommand => Settings.ShowImpressumCommand;
    public IAsyncRelayCommand ShowPrivacyCommand => Settings.ShowPrivacyCommand;
    public IAsyncRelayCommand ShowLicensesCommand => Settings.ShowLicensesCommand;
    public IAsyncRelayCommand LoadSpacesCommand => SeatList.LoadSpacesCommand;

    public Task LoadSpacesAsync() => SeatList.LoadSpacesAsync();

    private void ForwardPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.PropertyName))
        {
            return;
        }

        OnPropertyChanged(e.PropertyName);
    }
}
