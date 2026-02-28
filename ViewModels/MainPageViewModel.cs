using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PlatzPilot.Models;
using PlatzPilot.Services;

namespace PlatzPilot.ViewModels;

public partial class MainPageViewModel : ObservableObject
{
    private readonly SeatFinderService _seatFinderService;

    private const string FavoritesKey = "PlatzPilot_Favorites";
    private const string SortModeKey = "PlatzPilot_SortMode";
    private const string TabModeKey = "PlatzPilot_CurrentTab";
    private const string ThemeKey = "PlatzPilot_Theme";

    private const string ThemeLight = "Light";
    private const string ThemeDark = "Dark";

    private const string TabHome = "Home";
    private const string TabFavorites = "Favorites";
    private const string TabSettings = "Settings";

    private const string SortByRelevance = "Relevanz";
    private const string SortByMostFree = "Meiste freie Plätze";
    private const string SortByMostTotal = "Meiste Plätze insgesamt";
    private const string SortByAlphabetical = "Alphabetisch (A-Z)";

    private static readonly Dictionary<string, string> BuildingNames = new()
    {
        { "30.50", "KIT-Bibliothek Süd Altbau" },
        { "30.51", "KIT-Bibliothek Süd Neubau" },
        { "50.19", "Informatikom" }
    };

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDataVisible))]
    [NotifyPropertyChangedFor(nameof(IsListEmpty))]
    private ObservableCollection<UiLocation> _uiLocations = new();

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isRefreshing;

    [ObservableProperty]
    private bool _isFilterExpanded;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private DateTime _selectedDate = DateTime.Now.Date;

    [ObservableProperty]
    private TimeSpan _selectedTime = DateTime.Now.TimeOfDay;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NowButtonColor))]
    private bool _useNow = true;

    public Color NowButtonColor => UseNow ? Color.FromArgb("#27ae60") : Color.FromArgb("#7f8c8d");

    // --- TAB-STEUERUNG UND SICHTBARKEIT ---
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMainContentVisible))]
    [NotifyPropertyChangedFor(nameof(IsSettingsContentVisible))]
    private string _currentTab = TabHome;

    // Diese Variablen steuern, was auf dem Bildschirm angezeigt wird
    public bool IsMainContentVisible => CurrentTab != TabSettings;
    public bool IsSettingsContentVisible => CurrentTab == TabSettings;

    public List<string> SortOptions { get; } = new()
    {
        SortByRelevance,
        SortByMostFree,
        SortByMostTotal,
        SortByAlphabetical
    };

    [ObservableProperty]
    private string _selectedSortOption;

    private List<StudySpace> _allSpaces = new();
    private bool _isUpdatingDateTimeSelection;

    // Die Liste wird nur gezeigt, wenn wir NICHT in den Einstellungen sind
    public bool IsDataVisible => !IsBusy && UiLocations.Count > 0 && IsMainContentVisible;
    public bool IsListEmpty => !IsBusy && UiLocations.Count == 0 && IsMainContentVisible;

    public MainPageViewModel(SeatFinderService seatFinderService)
    {
        _seatFinderService = seatFinderService;
        _selectedSortOption = Preferences.Default.Get(SortModeKey, SortByRelevance);
        _currentTab = ResolveInitialTab(Preferences.Default.Get(TabModeKey, TabHome));

        if (!SortOptions.Contains(_selectedSortOption))
        {
            _selectedSortOption = SortByRelevance;
        }

        ApplySavedTheme();
    }

    [RelayCommand]
    private void ToggleFilter() => IsFilterExpanded = !IsFilterExpanded;

    [RelayCommand]
    private async Task SetNowAsync()
    {
        var now = DateTime.Now;

        _isUpdatingDateTimeSelection = true;
        SelectedDate = now.Date;
        SelectedTime = now.TimeOfDay;
        _isUpdatingDateTimeSelection = false;

        UseNow = true;
        ApplyFilter();
        await LoadSpacesAsync();
    }

    partial void OnSelectedSortOptionChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        Preferences.Default.Set(SortModeKey, value);
        ApplyFilter();
    }

    [RelayCommand]
    private void SwitchTab(string tabName)
    {
        tabName = NormalizeTab(tabName);
        CurrentTab = tabName;
        Preferences.Default.Set(TabModeKey, tabName);

        // Wenn wir nicht in den Einstellungen sind, Liste aktualisieren
        if (IsMainContentVisible)
        {
            ApplyFilter();
        }
    }

    private List<string> GetFavoriteNames()
    {
        var json = Preferences.Default.Get(FavoritesKey, "[]");
        return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
    }

    private void SaveFavoriteNames(List<string> favs)
    {
        Preferences.Default.Set(FavoritesKey, JsonSerializer.Serialize(favs));
    }

    [RelayCommand]
    private void ToggleFavorite(UiLocation location)
    {
        if (location == null) return;

        location.IsFavorite = !location.IsFavorite;

        var favs = GetFavoriteNames();
        if (location.IsFavorite && !favs.Contains(location.Name)) favs.Add(location.Name);
        else if (!location.IsFavorite) favs.Remove(location.Name);

        SaveFavoriteNames(favs);

        if (CurrentTab == TabFavorites && !location.IsFavorite)
        {
            UiLocations.Remove(location);
            NotifyCollectionVisibilityChanged();
        }
    }

    [RelayCommand]
    private void ToggleTheme()
    {
        if (Application.Current == null) return;

        var currentTheme = Application.Current.RequestedTheme;
        var nextTheme = currentTheme == AppTheme.Dark ? AppTheme.Light : AppTheme.Dark;

        Application.Current.UserAppTheme = nextTheme;
        Preferences.Default.Set(ThemeKey, nextTheme == AppTheme.Light ? ThemeLight : ThemeDark);
    }

    [RelayCommand]
    public async Task LoadSpacesAsync()
    {
        if (IsBusy) return;
        try
        {
            IsBusy = true;
            _allSpaces = await _seatFinderService.FetchSeatDataAsync(before: GetApiBeforeParameter());
            ApplyFilter();
        }
        finally
        {
            IsBusy = false;
            IsRefreshing = false;
        }
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    partial void OnSelectedDateChanged(DateTime value)
    {
        if (_isUpdatingDateTimeSelection) return;
        _ = HandleSelectedTimeChangedAsync();
    }

    partial void OnSelectedTimeChanged(TimeSpan value)
    {
        if (_isUpdatingDateTimeSelection) return;
        _ = HandleSelectedTimeChangedAsync();
    }

    private async Task HandleSelectedTimeChangedAsync()
    {
        UseNow = false;
        ApplyFilter();
        await LoadSpacesAsync();
    }

    private void ApplyFilter()
    {
        if (_allSpaces.Count == 0)
        {
            UiLocations.Clear();
            NotifyCollectionVisibilityChanged();
            return;
        }

        var filteredSpaces = FilterSpacesBySearch(_allSpaces);
        var results = MapSpacesToUiLocations(filteredSpaces);

        if (CurrentTab == TabFavorites)
        {
            results = results.Where(r => r.IsFavorite).ToList();
        }

        var sortedResults = SortLocations(results);
        ReplaceUiLocations(sortedResults);
    }

    private IEnumerable<StudySpace> FilterSpacesBySearch(IEnumerable<StudySpace> spaces)
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            return spaces;
        }

        var search = SearchText.Trim();
        return spaces.Where(space => MatchesSearch(space, search));
    }

    private static bool MatchesSearch(StudySpace space, string search)
    {
        return ContainsIgnoreCase(space.Name, search) || ContainsIgnoreCase(space.Id, search);
    }

    private static bool ContainsIgnoreCase(string? text, string search)
    {
        return !string.IsNullOrWhiteSpace(text) &&
               text.Contains(search, StringComparison.OrdinalIgnoreCase);
    }

    private List<UiLocation> SortLocations(List<UiLocation> locations)
    {
        return SelectedSortOption switch
        {
            SortByMostFree => locations.OrderByDescending(r => r.FreeSeats).ToList(),
            SortByMostTotal => locations.OrderByDescending(r => r.TotalSeats).ToList(),
            SortByAlphabetical => locations.OrderBy(r => r.Name).ToList(),
            _ => locations
        };
    }

    private void ReplaceUiLocations(IEnumerable<UiLocation> locations)
    {
        UiLocations.Clear();
        foreach (var location in locations)
        {
            UiLocations.Add(location);
        }

        NotifyCollectionVisibilityChanged();
    }

    private void NotifyCollectionVisibilityChanged()
    {
        OnPropertyChanged(nameof(IsDataVisible));
        OnPropertyChanged(nameof(IsListEmpty));
    }

    private List<UiLocation> MapSpacesToUiLocations(IEnumerable<StudySpace> spaces)
    {
        var referenceTime = GetReferenceDateTime();
        var favoriteNames = GetFavoriteNames();
        var results = new List<UiLocation>();
        var buildingGroups = spaces.GroupBy(s => s.Building);

        foreach (var bg in buildingGroups)
        {
            var spacesInBuilding = bg.ToList();
            foreach (var space in spacesInBuilding)
            {
                space.ReferenceTime = referenceTime;
            }

            if (string.IsNullOrWhiteSpace(bg.Key) || spacesInBuilding.Count == 1)
            {
                results.AddRange(spacesInBuilding.Select(space => CreateSingleLocation(space, favoriteNames, referenceTime)));
                continue;
            }

            results.Add(CreateGroupedLocation(bg.Key, spacesInBuilding, favoriteNames, referenceTime));
        }

        return results;
    }

    private static UiLocation CreateSingleLocation(StudySpace space, List<string> favoriteNames, DateTime referenceTime) => new()
    {
        Name = space.Name,
        Subtitle = "1 Lernort",
        BuildingNumber = space.Building,
        TotalSeats = space.TotalSeats,
        FreeSeats = space.FreeSeats,
        OccupiedSeats = space.OccupiedSeats,
        IsManualCount = space.IsManualCount,
        SubSpaces = new List<StudySpace> { space },
        IsFavorite = favoriteNames.Contains(space.Name),
        ReferenceTime = referenceTime
    };

    private static UiLocation CreateGroupedLocation(string buildingKey, List<StudySpace> spaces, List<string> favoriteNames, DateTime referenceTime)
    {
        string displayName = BuildingNames.TryGetValue(buildingKey, out var mappedName)
            ? mappedName
            : $"Gebäude {buildingKey}";

        return new()
        {
            Name = displayName,
            Subtitle = $"{spaces.Count} Lernorte",
            BuildingNumber = buildingKey,
            TotalSeats = spaces.Sum(s => s.TotalSeats),
            FreeSeats = spaces.Sum(s => s.FreeSeats),
            OccupiedSeats = spaces.Sum(s => s.OccupiedSeats),
            IsManualCount = spaces.Any(s => s.IsManualCount),
            SubSpaces = spaces,
            IsFavorite = favoriteNames.Contains(displayName),
            ReferenceTime = referenceTime
        };
    }

    private DateTime GetReferenceDateTime() => UseNow ? DateTime.Now : SelectedDate.Date + SelectedTime;

    private string GetApiBeforeParameter() => UseNow ? "now" : GetReferenceDateTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

    [RelayCommand]
    private async Task GoToDetailAsync(UiLocation selectedLocation)
    {
        if (selectedLocation == null) return;

        await Shell.Current.GoToAsync("DetailPage", new Dictionary<string, object> { { "LocationData", selectedLocation } });
    }

    private static string NormalizeTab(string tabName)
    {
        return tabName switch
        {
            TabHome => TabHome,
            TabFavorites => TabFavorites,
            TabSettings => TabSettings,
            _ => TabHome
        };
    }

    private static string ResolveInitialTab(string tabName)
    {
        var normalizedTab = NormalizeTab(tabName);
        return normalizedTab == TabSettings ? TabHome : normalizedTab;
    }

    private static void ApplySavedTheme()
    {
        if (Application.Current == null)
        {
            return;
        }

        var savedTheme = Preferences.Default.Get(ThemeKey, "System");
        Application.Current.UserAppTheme = savedTheme switch
        {
            ThemeLight => AppTheme.Light,
            ThemeDark => AppTheme.Dark,
            _ => AppTheme.Unspecified
        };
    }
}
