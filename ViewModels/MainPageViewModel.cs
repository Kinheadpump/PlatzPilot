using System.Collections.ObjectModel;
using System.Diagnostics;
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
    private readonly SafeArrivalForecastService _safeArrivalForecastService;

    private const string FavoritesKey = "PlatzPilot_Favorites";
    private const string SortModeKey = "PlatzPilot_SortMode";
    private const string TabModeKey = "PlatzPilot_CurrentTab";
    private const string ThemeKey = "PlatzPilot_Theme";
    private const string SpaceFeaturesFileName = "study_space_features.json";
    private const string PrivacyPageUrl = "https://kinheadpump.github.io/PlatzPilot/privacy";
    private const string ImpressumPageUrl = "https://kinheadpump.github.io/PlatzPilot/impressum";

    private const string ThemeLight = "Light";
    private const string ThemeDark = "Dark";

    private const string TabHome = "Home";
    private const string TabFavorites = "Favorites";
    private const string TabSettings = "Settings";

    private const string SortByRelevance = "Relevanz";
    private const string SortByMostFree = "Meiste freie Plätze";
    private const string SortByMostTotal = "Meiste Plätze insgesamt";
    private const string SortByAlphabetical = "Alphabetisch (A-Z)";

    private const string RoomTypeGroup = "gruppenraum";
    private const string RoomTypeSilent = "silent study";
    private const string RoomTypePcPool = "pc-pool";

    private const int MinOpeningHoursSliderValue = 0;
    private const int MaxOpeningHoursSliderValue = 12;
    private const int WeeklyHistoryPoints = 2016;
    private const int LiveRefreshIntervalMinutes = 5;

    private static readonly Dictionary<string, string> BuildingNames = new()
    {
        { "30.50", "KIT-Bibliothek Süd Altbau" },
        { "30.51", "KIT-Bibliothek Süd Neubau" },
        { "50.19", "Informatikom" }
    };

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDataVisible))]
    [NotifyPropertyChangedFor(nameof(IsListEmpty))]
    private ObservableCollection<UiLocation> _uiLocations = [];

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isRefreshing;

    [ObservableProperty]
    private bool _isFilterExpanded;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSearchInactive))]
    private bool _isSearchActive;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBeforeMode))]
    private bool _useNow = true;

    [ObservableProperty]
    private DateTime _selectedDate = DateTime.Now.Date;

    [ObservableProperty]
    private TimeSpan _selectedTime = DateTime.Now.TimeOfDay;

    [ObservableProperty]
    private bool _isGroupRoomSelected;

    [ObservableProperty]
    private bool _isSilentStudySelected;

    [ObservableProperty]
    private bool _isPcPoolSelected;

    [ObservableProperty]
    private bool _requireFreeWifi;

    [ObservableProperty]
    private bool _requirePowerOutlets;

    [ObservableProperty]
    private bool _requireWhiteboard;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MinimumOpenHoursText))]
    private double _minimumOpenHours = MinOpeningHoursSliderValue;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowResultsButtonText))]
    private int _filteredLocationCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMainContentVisible))]
    [NotifyPropertyChangedFor(nameof(IsSettingsContentVisible))]
    private string _currentTab = TabHome;

    [ObservableProperty]
    private string _selectedSortOption;

    private readonly Dictionary<string, StudySpaceFeatureEntry> _spaceFeaturesById = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<SeatHistoryPoint>> _historicalSeatDataByLocation = new(StringComparer.OrdinalIgnoreCase);
    private List<StudySpace> _allSpaces = [];
    private bool _spaceFeaturesLoaded;
    private bool _hasLoadedWeeklyHistory;
    private bool _isUpdatingDateTimeSelection;
    private string _lastLoadedBeforeParameter = string.Empty;
    private DateTime _lastLiveSnapshotFetchUtc = DateTime.MinValue;

    public bool IsMainContentVisible => CurrentTab != TabSettings;
    public bool IsSettingsContentVisible => CurrentTab == TabSettings;
    public bool IsDataVisible => !IsBusy && UiLocations.Count > 0 && IsMainContentVisible;
    public bool IsListEmpty => !IsBusy && UiLocations.Count == 0 && IsMainContentVisible;
    public bool IsBeforeMode => !UseNow;
    public bool IsSearchInactive => !IsSearchActive;
    public DateTime MaxSelectableDate => DateTime.Today;
    public string MinimumOpenHoursText => $"Noch geöffnet für: mind. {MinimumOpenHours:0} Stunden";
    public string ShowResultsButtonText => $"{FilteredLocationCount} Gebäude anzeigen";

    public List<string> SortOptions { get; } =
    [
        SortByRelevance,
        SortByMostFree,
        SortByMostTotal,
        SortByAlphabetical
    ];

    public MainPageViewModel(SeatFinderService seatFinderService, SafeArrivalForecastService safeArrivalForecastService)
    {
        _seatFinderService = seatFinderService;
        _safeArrivalForecastService = safeArrivalForecastService;
        _selectedSortOption = Preferences.Default.Get(SortModeKey, SortByRelevance);
        _currentTab = ResolveInitialTab(Preferences.Default.Get(TabModeKey, TabHome));

        if (!SortOptions.Contains(_selectedSortOption))
        {
            _selectedSortOption = SortByRelevance;
        }

        ApplySavedTheme();
        FilteredLocationCount = 0;
    }

    [RelayCommand]
    private void ToggleSearch()
    {
        IsSearchActive = !IsSearchActive;

        if (!IsSearchActive && !string.IsNullOrWhiteSpace(SearchText))
        {
            SearchText = string.Empty;
        }
    }

    [RelayCommand]
    private void ToggleFilter()
    {
        var shouldOpen = !IsFilterExpanded;
        IsFilterExpanded = shouldOpen;

        if (!shouldOpen)
        {
            return;
        }

        UpdateFilteredLocationPreviewCount();
    }

    [RelayCommand]
    private void CloseFilterSheet() => IsFilterExpanded = false;

    [RelayCommand]
    private void ResetFilters()
    {
        SyncSelectedDateTimeToNow();
        UseNow = true;

        IsGroupRoomSelected = false;
        IsSilentStudySelected = false;
        IsPcPoolSelected = false;
        RequireFreeWifi = false;
        RequirePowerOutlets = false;
        RequireWhiteboard = false;
        MinimumOpenHours = MinOpeningHoursSliderValue;

        UpdateFilteredLocationPreviewCount();
    }

    [RelayCommand]
    private async Task ApplySheetFiltersAsync()
    {
        var requestedBeforeParameter = GetApiBeforeParameter();
        var shouldRefreshData = ShouldRefreshSnapshot(requestedBeforeParameter);

        if (shouldRefreshData)
        {
            await LoadSpacesAsync();
        }
        else
        {
            ApplyFilter();
        }

        IsFilterExpanded = false;
    }

    [RelayCommand]
    private async Task SetWhenNowAsync()
    {
        SyncSelectedDateTimeToNow();
        UseNow = true;
        UpdateFilteredLocationPreviewCount();
        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task SetWhenBeforeAsync()
    {
        _isUpdatingDateTimeSelection = true;
        if (SelectedDate.Date > DateTime.Today)
        {
            SelectedDate = MaxSelectableDate;
        }

        if (SelectedTime == TimeSpan.Zero)
        {
            SelectedTime = TimeSpan.FromHours(12);
        }

        _isUpdatingDateTimeSelection = false;
        UseNow = false;
        UpdateFilteredLocationPreviewCount();
        await Task.CompletedTask;
    }

    partial void OnSelectedSortOptionChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        Preferences.Default.Set(SortModeKey, value);
        UpdateFilteredLocationPreviewCount();
    }

    [RelayCommand]
    private void SwitchTab(string tabName)
    {
        tabName = NormalizeTab(tabName);
        CurrentTab = tabName;
        Preferences.Default.Set(TabModeKey, tabName);

        if (CurrentTab != TabHome)
        {
            IsSearchActive = false;
            IsFilterExpanded = false;
        }

        if (IsMainContentVisible)
        {
            ApplyFilter();
        }
    }

    private List<string> GetFavoriteNames()
    {
        var json = Preferences.Default.Get(FavoritesKey, "[]");
        return JsonSerializer.Deserialize<List<string>>(json) ?? [];
    }

    private void SaveFavoriteNames(List<string> favorites)
    {
        Preferences.Default.Set(FavoritesKey, JsonSerializer.Serialize(favorites));
    }

    [RelayCommand]
    private void ToggleFavorite(UiLocation location)
    {
        if (location == null)
        {
            return;
        }

        location.IsFavorite = !location.IsFavorite;

        var favorites = GetFavoriteNames();
        if (location.IsFavorite && !favorites.Contains(location.Name))
        {
            favorites.Add(location.Name);
        }
        else if (!location.IsFavorite)
        {
            favorites.Remove(location.Name);
        }

        SaveFavoriteNames(favorites);

        if (CurrentTab == TabFavorites && !location.IsFavorite)
        {
            UiLocations.Remove(location);
            NotifyCollectionVisibilityChanged();
            FilteredLocationCount = UiLocations.Count;
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
        Preferences.Default.Set(ThemeKey, nextTheme == AppTheme.Light ? ThemeLight : ThemeDark);
    }

    [RelayCommand]
    private async Task OpenGithubAsync()
    {
        const string repositoryUrl = "https://github.com/Kinheadpump/PlatzPilot";
        await Browser.Default.OpenAsync(repositoryUrl, BrowserLaunchMode.SystemPreferred);
    }

    [RelayCommand]
    private async Task ShowImpressumAsync()
    {
        await Browser.Default.OpenAsync(ImpressumPageUrl, BrowserLaunchMode.SystemPreferred);
    }

    [RelayCommand]
    private async Task ShowPrivacyAsync()
    {
        await Browser.Default.OpenAsync(PrivacyPageUrl, BrowserLaunchMode.SystemPreferred);
    }

    [RelayCommand]
    private async Task ShowLicensesAsync()
    {
        const string licenseText = """
Diese App verwendet folgende Open-Source-Bibliotheken unter der MIT-Lizenz:

1. .NET MAUI Community Toolkit
Copyright (c) .NET Foundation and Contributors

2. Microcharts
Copyright (c) 2017 Aloïs Deniel

Die vollständige MIT-Lizenz lautet:
Die hiermit unentgeltlich erteilte Erlaubnis, Kopien der Software und der zugehörigen Dokumentation zu nutzen, zu kopieren, zu ändern und zu vertreiben, wird unter der Bedingung erteilt, dass der obige Urheberrechtshinweis in allen Kopien enthalten ist. DIE SOFTWARE WIRD OHNE JEDE AUSDRÜCKLICHE ODER IMPLIZIERTE GARANTIE BEREITGESTELLT.
""";

        await ShowDialogAsync("Open-Source-Lizenzen", licenseText);
    }

    [RelayCommand]
    public async Task LoadSpacesAsync()
    {
        if (IsBusy)
        {
            return;
        }

        try
        {
            IsBusy = true;
            await EnsureSpaceFeaturesLoadedAsync();

            var forceWeeklyReload = IsRefreshing;
            if (!_hasLoadedWeeklyHistory || forceWeeklyReload)
            {
                _allSpaces = await _seatFinderService.FetchSeatDataAsync(limit: WeeklyHistoryPoints, before: "now");
                ReplaceHistoricalSeatData(_allSpaces);
                _hasLoadedWeeklyHistory = true;
            }
            else
            {
                _allSpaces = await _seatFinderService.FetchSeatDataAsync(limit: 1, before: GetApiBeforeParameter());
                AppendLatestSeatDataToHistory(_allSpaces);
            }

            UpdateSafeArrivalRecommendations(GetReferenceDateTime());
            _lastLoadedBeforeParameter = GetApiBeforeParameter();
            _lastLiveSnapshotFetchUtc = DateTime.UtcNow;
            ApplyFilter();
        }
        finally
        {
            IsBusy = false;
            IsRefreshing = false;
        }
    }

    private bool ShouldRefreshSnapshot(string requestedBeforeParameter)
    {
        if (!_hasLoadedWeeklyHistory)
        {
            return true;
        }

        if (!string.Equals(requestedBeforeParameter, "now", StringComparison.Ordinal))
        {
            return !string.Equals(requestedBeforeParameter, _lastLoadedBeforeParameter, StringComparison.Ordinal);
        }

        if (_lastLiveSnapshotFetchUtc == DateTime.MinValue)
        {
            return true;
        }

        return DateTime.UtcNow - _lastLiveSnapshotFetchUtc >= TimeSpan.FromMinutes(LiveRefreshIntervalMinutes);
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    partial void OnUseNowChanged(bool value) => UpdateFilteredLocationPreviewCount();

    partial void OnSelectedDateChanged(DateTime value)
    {
        if (_isUpdatingDateTimeSelection)
        {
            return;
        }

        if (value.Date > DateTime.Today)
        {
            _isUpdatingDateTimeSelection = true;
            SelectedDate = MaxSelectableDate;
            _isUpdatingDateTimeSelection = false;
        }
        UpdateFilteredLocationPreviewCount();
    }

    partial void OnSelectedTimeChanged(TimeSpan value)
    {
        if (_isUpdatingDateTimeSelection)
        {
            return;
        }
        UpdateFilteredLocationPreviewCount();
    }

    private void SyncSelectedDateTimeToNow()
    {
        _isUpdatingDateTimeSelection = true;
        var now = DateTime.Now;
        SelectedDate = now.Date;
        SelectedTime = GetCurrentTimeRoundedToMinute();
        _isUpdatingDateTimeSelection = false;
    }

    private static TimeSpan GetCurrentTimeRoundedToMinute()
    {
        var now = DateTime.Now;
        return new TimeSpan(now.Hour, now.Minute, 0);
    }

    partial void OnIsGroupRoomSelectedChanged(bool value) => UpdateFilteredLocationPreviewCount();
    partial void OnIsSilentStudySelectedChanged(bool value) => UpdateFilteredLocationPreviewCount();
    partial void OnIsPcPoolSelectedChanged(bool value) => UpdateFilteredLocationPreviewCount();
    partial void OnRequireFreeWifiChanged(bool value) => UpdateFilteredLocationPreviewCount();
    partial void OnRequirePowerOutletsChanged(bool value) => UpdateFilteredLocationPreviewCount();
    partial void OnRequireWhiteboardChanged(bool value) => UpdateFilteredLocationPreviewCount();

    partial void OnMinimumOpenHoursChanged(double value)
    {
        var rounded = Math.Clamp(Math.Round(value), MinOpeningHoursSliderValue, MaxOpeningHoursSliderValue);
        if (Math.Abs(rounded - value) > 0.001)
        {
            MinimumOpenHours = rounded;
            return;
        }

        UpdateFilteredLocationPreviewCount();
    }

    private async Task EnsureSpaceFeaturesLoadedAsync()
    {
        if (_spaceFeaturesLoaded)
        {
            return;
        }

        _spaceFeaturesLoaded = true;

        try
        {
            await using var stream = await FileSystem.OpenAppPackageFileAsync(SpaceFeaturesFileName);
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var catalog = await JsonSerializer.DeserializeAsync<StudySpaceFeatureCatalog>(stream, options);
            if (catalog?.Spaces == null)
            {
                return;
            }

            foreach (var entry in catalog.Spaces)
            {
                if (string.IsNullOrWhiteSpace(entry.Id))
                {
                    continue;
                }

                _spaceFeaturesById[entry.Id.Trim()] = entry;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Konnte {SpaceFeaturesFileName} nicht laden: {ex.Message}");
        }
    }

    private void ReplaceHistoricalSeatData(IEnumerable<StudySpace> spaces)
    {
        _historicalSeatDataByLocation.Clear();

        foreach (var space in spaces)
        {
            if (space.SeatHistory.Count == 0)
            {
                continue;
            }

            _historicalSeatDataByLocation[space.Id] = space.SeatHistory
                .OrderByDescending(point => point.Timestamp)
                .ToList();
        }
    }

    private void AppendLatestSeatDataToHistory(IEnumerable<StudySpace> spaces)
    {
        foreach (var space in spaces)
        {
            if (space.SeatHistory.Count == 0)
            {
                continue;
            }

            if (!_historicalSeatDataByLocation.TryGetValue(space.Id, out var history))
            {
                history = [];
                _historicalSeatDataByLocation[space.Id] = history;
            }

            var knownTimestamps = new HashSet<DateTime>(history.Select(point => point.Timestamp));
            foreach (var point in space.SeatHistory)
            {
                if (!knownTimestamps.Add(point.Timestamp))
                {
                    continue;
                }

                history.Add(point);
            }

            history.Sort((a, b) => b.Timestamp.CompareTo(a.Timestamp));
            if (history.Count > WeeklyHistoryPoints)
            {
                history.RemoveRange(WeeklyHistoryPoints, history.Count - WeeklyHistoryPoints);
            }
        }
    }

    private void UpdateSafeArrivalRecommendations(DateTime referenceTime)
    {
        foreach (var space in _allSpaces)
        {
            if (!_historicalSeatDataByLocation.TryGetValue(space.Id, out var history) || history.Count == 0)
            {
                space.SafeArrivalRecommendation = null;
                continue;
            }

            space.SafeArrivalRecommendation = _safeArrivalForecastService.Calculate(space, history, referenceTime);
        }
    }

    private void ApplyFilter()
    {
        if (_allSpaces.Count == 0)
        {
            ReplaceUiLocations([]);
            return;
        }

        var filteredSpaces = ApplySpaceFilters(_allSpaces);
        var results = MapSpacesToUiLocations(filteredSpaces);

        if (CurrentTab == TabFavorites)
        {
            results = results.Where(location => location.IsFavorite).ToList();
        }

        var sortedResults = SortLocations(results);
        ReplaceUiLocations(sortedResults);
    }

    private void UpdateFilteredLocationPreviewCount()
    {
        if (_allSpaces.Count == 0)
        {
            FilteredLocationCount = 0;
            return;
        }

        var filteredSpaces = ApplySpaceFilters(_allSpaces);
        var results = MapSpacesToUiLocations(filteredSpaces);

        if (CurrentTab == TabFavorites)
        {
            results = results.Where(location => location.IsFavorite).ToList();
        }

        FilteredLocationCount = SortLocations(results).Count;
    }

    private IEnumerable<StudySpace> ApplySpaceFilters(IEnumerable<StudySpace> spaces)
    {
        var searchFiltered = FilterSpacesBySearch(spaces);
        var roomTypeFiltered = FilterSpacesByRoomType(searchFiltered);
        var equipmentFiltered = FilterSpacesByEquipment(roomTypeFiltered);
        return FilterSpacesByOpeningHours(equipmentFiltered);
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

    private IEnumerable<StudySpace> FilterSpacesByRoomType(IEnumerable<StudySpace> spaces)
    {
        if (!IsRoomTypeFilterActive())
        {
            return spaces;
        }

        var selectedRoomTypes = GetSelectedRoomTypes();

        return spaces.Where(space =>
        {
            if (!_spaceFeaturesById.TryGetValue(space.Id, out var features) || features.RoomTypes.Count == 0)
            {
                return false;
            }

            return features.RoomTypes.Any(roomType => selectedRoomTypes.Contains(NormalizeRoomType(roomType)));
        });
    }

    private bool IsRoomTypeFilterActive()
    {
        return IsGroupRoomSelected || IsSilentStudySelected || IsPcPoolSelected;
    }

    private HashSet<string> GetSelectedRoomTypes()
    {
        var selectedRoomTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (IsGroupRoomSelected)
        {
            selectedRoomTypes.Add(RoomTypeGroup);
        }

        if (IsSilentStudySelected)
        {
            selectedRoomTypes.Add(RoomTypeSilent);
        }

        if (IsPcPoolSelected)
        {
            selectedRoomTypes.Add(RoomTypePcPool);
        }

        return selectedRoomTypes;
    }

    private static string NormalizeRoomType(string roomType)
    {
        return roomType.Trim().ToLowerInvariant();
    }

    private IEnumerable<StudySpace> FilterSpacesByEquipment(IEnumerable<StudySpace> spaces)
    {
        if (!RequireFreeWifi && !RequirePowerOutlets && !RequireWhiteboard)
        {
            return spaces;
        }

        return spaces.Where(space =>
        {
            if (!_spaceFeaturesById.TryGetValue(space.Id, out var features))
            {
                return false;
            }

            if (RequireFreeWifi && !features.FreeWifi)
            {
                return false;
            }

            if (RequirePowerOutlets && !features.PowerOutlets)
            {
                return false;
            }

            if (RequireWhiteboard && !features.Whiteboard)
            {
                return false;
            }

            return true;
        });
    }

    private IEnumerable<StudySpace> FilterSpacesByOpeningHours(IEnumerable<StudySpace> spaces)
    {
        if (MinimumOpenHours <= 0)
        {
            return spaces;
        }

        var referenceTime = GetReferenceDateTime();
        var requiredUntil = referenceTime.AddHours(MinimumOpenHours);

        return spaces.Where(space =>
        {
            return space.OpeningHours?.IsOpenUntil(referenceTime, requiredUntil) ?? false;
        });
    }

    private List<UiLocation> SortLocations(List<UiLocation> locations)
    {
        return SelectedSortOption switch
        {
            SortByMostFree => locations.OrderByDescending(location => location.FreeSeats).ToList(),
            SortByMostTotal => locations.OrderByDescending(location => location.TotalSeats).ToList(),
            SortByAlphabetical => locations.OrderBy(location => location.Name).ToList(),
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

        FilteredLocationCount = UiLocations.Count;
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
        var buildingGroups = spaces.GroupBy(space => space.Building);

        foreach (var group in buildingGroups)
        {
            var spacesInBuilding = group.ToList();
            foreach (var space in spacesInBuilding)
            {
                space.ReferenceTime = referenceTime;
            }

            if (string.IsNullOrWhiteSpace(group.Key) || spacesInBuilding.Count == 1)
            {
                results.AddRange(spacesInBuilding.Select(space => CreateSingleLocation(space, favoriteNames, referenceTime)));
                continue;
            }

            results.Add(CreateGroupedLocation(group.Key, spacesInBuilding, favoriteNames, referenceTime));
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
        SubSpaces = [space],
        IsFavorite = favoriteNames.Contains(space.Name),
        ReferenceTime = referenceTime,
        BestArrivalText = FormatBestArrivalText(space.SafeArrivalRecommendation)
    };

    private static UiLocation CreateGroupedLocation(string buildingKey, List<StudySpace> spaces, List<string> favoriteNames, DateTime referenceTime)
    {
        var displayName = BuildingNames.TryGetValue(buildingKey, out var mappedName)
            ? mappedName
            : $"Gebäude {buildingKey}";

        return new UiLocation
        {
            Name = displayName,
            Subtitle = $"{spaces.Count} Lernorte",
            BuildingNumber = buildingKey,
            TotalSeats = spaces.Sum(space => space.TotalSeats),
            FreeSeats = spaces.Sum(space => space.FreeSeats),
            OccupiedSeats = spaces.Sum(space => space.OccupiedSeats),
            IsManualCount = spaces.Any(space => space.IsManualCount),
            SubSpaces = spaces,
            IsFavorite = favoriteNames.Contains(displayName),
            ReferenceTime = referenceTime,
            BestArrivalText = FormatBestArrivalText(SelectBestRecommendation(spaces))
        };
    }

    private static SafeArrivalRecommendation? SelectBestRecommendation(IEnumerable<StudySpace> spaces)
    {
        return spaces
            .Select(space => space.SafeArrivalRecommendation)
            .Where(recommendation => recommendation?.HasRecommendation == true)
            .OrderBy(recommendation => recommendation!.LatestSafeTime)
            .ThenBy(recommendation => recommendation!.Probability)
            .LastOrDefault();
    }

    private static string FormatBestArrivalText(SafeArrivalRecommendation? recommendation)
    {
        if (recommendation == null || !recommendation.HasRecommendation)
        {
            return "Beste Ankunft: keine sichere Zeit";
        }

        var timeText = recommendation.LatestSafeTime.ToString(@"hh\:mm", CultureInfo.InvariantCulture);
        var probabilityText = recommendation.Probability.ToString("P0", CultureInfo.CurrentCulture);
        var confidenceText = recommendation.ConfidenceFlag ? string.Empty : " (unsicher)";

        return $"Beste Ankunft bis {timeText} ({probabilityText}){confidenceText}";
    }

    private DateTime GetReferenceDateTime()
    {
        return UseNow ? DateTime.Now : SelectedDate.Date + SelectedTime;
    }


    private string GetApiBeforeParameter()
    {
        return UseNow
            ? "now"
            : GetReferenceDateTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
    }

    [RelayCommand]
    private async Task GoToDetailAsync(UiLocation selectedLocation)
    {
        if (selectedLocation == null)
        {
            return;
        }

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

    private static async Task ShowDialogAsync(string title, string message)
    {
        var page = Application.Current?.Windows.FirstOrDefault()?.Page;
        if (page == null)
        {
            return;
        }

        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            await page.DisplayAlertAsync(title, message, "OK");
        });
    }
}
