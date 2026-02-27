using System.Collections.ObjectModel;
using System.Text.Json;
using System.Globalization;
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
    private string _currentTab = "Home";

    // Diese Variablen steuern, was auf dem Bildschirm angezeigt wird
    public bool IsMainContentVisible => CurrentTab != "Settings";
    public bool IsSettingsContentVisible => CurrentTab == "Settings";

    public List<string> SortOptions { get; } = new()
    {
        "Relevanz",
        "Meiste freie Plätze",
        "Meiste Plätze insgesamt",
        "Alphabetisch (A-Z)"
    };

    [ObservableProperty]
    private string _selectedSortOption;

    private List<StudySpace> _allSpaces = new();
    private bool _suppressTimeSelectionChanged;

    // Die Liste wird nur gezeigt, wenn wir NICHT in den Einstellungen sind
    public bool IsDataVisible => !IsBusy && UiLocations.Count > 0 && IsMainContentVisible;
    public bool IsListEmpty => !IsBusy && UiLocations.Count == 0 && IsMainContentVisible;

    public MainPageViewModel(SeatFinderService seatFinderService)
    {
        _seatFinderService = seatFinderService;
        _selectedSortOption = Preferences.Default.Get(SortModeKey, "Relevanz");
        _currentTab = Preferences.Default.Get(TabModeKey, "Home");
        if (_currentTab == "Settings")
        {
            _currentTab = "Home";
        }

        var savedTheme = Preferences.Default.Get("PlatzPilot_Theme", "System");
        if (Application.Current != null)
        {
            if (savedTheme == "Light") Application.Current.UserAppTheme = AppTheme.Light;
            else if (savedTheme == "Dark") Application.Current.UserAppTheme = AppTheme.Dark;
        }
    }

    [RelayCommand]
    private void ToggleFilter() => IsFilterExpanded = !IsFilterExpanded;

    [RelayCommand]
    private async Task SetNowAsync()
    {
        var now = DateTime.Now;

        _suppressTimeSelectionChanged = true;
        SelectedDate = now.Date;
        SelectedTime = now.TimeOfDay;
        _suppressTimeSelectionChanged = false;

        UseNow = true;
        ApplyFilter();
        await LoadSpacesAsync();
    }

    partial void OnSelectedSortOptionChanged(string value)
    {
        Preferences.Default.Set(SortModeKey, value);
        ApplyFilter();
    }

    [RelayCommand]
    private void SwitchTab(string tabName)
    {
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

        if (CurrentTab == "Favorites" && !location.IsFavorite)
        {
            UiLocations.Remove(location);
            OnPropertyChanged(nameof(IsDataVisible));
            OnPropertyChanged(nameof(IsListEmpty));
        }
    }
    // NEU: Der Befehl für den Dark/Light Mode Schalter
    [RelayCommand]
    private void ToggleTheme()
    {
        if (Application.Current == null) return;

        // Prüfen, was gerade aktiv ist (Hell oder Dunkel)
        var currentTheme = Application.Current.RequestedTheme;

        if (currentTheme == AppTheme.Dark)
        {
            Application.Current.UserAppTheme = AppTheme.Light;
            Preferences.Default.Set("PlatzPilot_Theme", "Light");
        }
        else
        {
            Application.Current.UserAppTheme = AppTheme.Dark;
            Preferences.Default.Set("PlatzPilot_Theme", "Dark");
        }
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
        if (_suppressTimeSelectionChanged) return;
        _ = HandleSelectedTimeChangedAsync();
    }

    partial void OnSelectedTimeChanged(TimeSpan value)
    {
        if (_suppressTimeSelectionChanged) return;
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
        if (_allSpaces == null || !_allSpaces.Any()) return;

        var filteredSpaces = string.IsNullOrWhiteSpace(SearchText) 
            ? _allSpaces 
            : _allSpaces.Where(s => s.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) || s.Id.Contains(SearchText, StringComparison.OrdinalIgnoreCase));

        var results = MapSpacesToUiLocations(filteredSpaces);

        if (CurrentTab == "Favorites")
        {
            results = results.Where(r => r.IsFavorite).ToList();
        }

        results = SelectedSortOption switch
        {
            "Meiste freie Plätze" => results.OrderByDescending(r => r.FreeSeats).ToList(),
            "Meiste Plätze insgesamt" => results.OrderByDescending(r => r.TotalSeats).ToList(),
            "Alphabetisch (A-Z)" => results.OrderBy(r => r.Name).ToList(),
            
            // "Relevanz" und jeder andere Wert: 
            // Wir machen KEIN OrderBy, dadurch bleibt die JSON-Reihenfolge exakt erhalten!
            _ => results
        };

        UiLocations.Clear();
        foreach (var location in results) UiLocations.Add(location);
        
        OnPropertyChanged(nameof(IsDataVisible));
        OnPropertyChanged(nameof(IsListEmpty));
    }

    private List<UiLocation> MapSpacesToUiLocations(IEnumerable<StudySpace> spaces)
    {
        var referenceTime = GetReferenceDateTime();
        var savedFavs = GetFavoriteNames();
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
                foreach (var space in spacesInBuilding) results.Add(CreateSingleLocation(space, savedFavs, referenceTime));
            else
                results.Add(CreateGroupedLocation(bg.Key, spacesInBuilding, savedFavs, referenceTime));
        }
        return results;
    }

    private UiLocation CreateSingleLocation(StudySpace space, List<string> savedFavs, DateTime referenceTime) => new()
    {
        Name = space.Name, Subtitle = "1 Lernort", BuildingNumber = space.Building, TotalSeats = space.TotalSeats, FreeSeats = space.FreeSeats, OccupiedSeats = space.OccupiedSeats, IsManualCount = space.IsManualCount, SubSpaces = new List<StudySpace> { space }, IsFavorite = savedFavs.Contains(space.Name), ReferenceTime = referenceTime
    };

    private UiLocation CreateGroupedLocation(string buildingKey, List<StudySpace> spaces, List<string> savedFavs, DateTime referenceTime)
    {
        string displayName = BuildingNames.TryGetValue(buildingKey, out var mappedName) ? mappedName : $"Gebäude {buildingKey}";
        return new()
        {
            Name = displayName, Subtitle = $"{spaces.Count} Lernorte", BuildingNumber = buildingKey, TotalSeats = spaces.Sum(s => s.TotalSeats), FreeSeats = spaces.Sum(s => s.FreeSeats), OccupiedSeats = spaces.Sum(s => s.OccupiedSeats), IsManualCount = spaces.Any(s => s.IsManualCount), SubSpaces = spaces, IsFavorite = savedFavs.Contains(displayName), ReferenceTime = referenceTime
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
}
