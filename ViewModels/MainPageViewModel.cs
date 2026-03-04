using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Networking;
using PlatzPilot.Configuration;
using PlatzPilot.Models;
using PlatzPilot.Services;

namespace PlatzPilot.ViewModels;

public partial class MainPageViewModel : ObservableObject
{
    private readonly AppConfig _config;
    private readonly SeatFinderService _seatFinderService;
    private readonly SafeArrivalForecastService _safeArrivalForecastService;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDataVisible))]
    [NotifyPropertyChangedFor(nameof(IsListEmpty))]
    [NotifyPropertyChangedFor(nameof(IsHomeEmpty))]
    [NotifyPropertyChangedFor(nameof(IsFavoritesEmpty))]
    [NotifyPropertyChangedFor(nameof(IsNoResultsEmpty))]
    [NotifyPropertyChangedFor(nameof(EmptyStateTitle))]
    [NotifyPropertyChangedFor(nameof(EmptyStateSubtitle))]
    [NotifyPropertyChangedFor(nameof(IsEmptySubtitleVisible))]
    private ObservableCollection<UiLocation> _uiLocations = [];

    [ObservableProperty]
    private UiLocation? _selectedLocation;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFavoritesEmpty))]
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
    private bool _isColorBlindMode;

    [ObservableProperty]
    private bool _isCampusSouthOnly;

    [ObservableProperty]
    private bool _isHapticFeedbackEnabled;

    [ObservableProperty]
    private bool _isHideClosedLocations;

    [ObservableProperty]
    private bool _isOfflineBannerVisible;

    [ObservableProperty]
    private SafeArrivalRecommendation? _mensaSafeArrivalRecommendation;

    [ObservableProperty]
    private string _mensaFluxLabel = string.Empty;

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
    private bool _isNoReservationSelected;

    [ObservableProperty]
    private bool _requireFreeWifi;

    [ObservableProperty]
    private bool _requirePowerOutlets;

    [ObservableProperty]
    private bool _requireWhiteboard;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MinimumOpenHoursText))]
    private double _minimumOpenHours;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowResultsButtonText))]
    private int _filteredLocationCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMainContentVisible))]
    [NotifyPropertyChangedFor(nameof(IsSettingsContentVisible))]
    [NotifyPropertyChangedFor(nameof(IsHomeEmpty))]
    [NotifyPropertyChangedFor(nameof(IsFavoritesEmpty))]
    [NotifyPropertyChangedFor(nameof(IsNoResultsEmpty))]
    [NotifyPropertyChangedFor(nameof(EmptyStateTitle))]
    [NotifyPropertyChangedFor(nameof(EmptyStateSubtitle))]
    [NotifyPropertyChangedFor(nameof(IsEmptySubtitleVisible))]
    private string _currentTab = string.Empty;

    [ObservableProperty]
    private string _selectedSortOption;

    private readonly Dictionary<string, StudySpaceFeatureEntry> _spaceFeaturesById = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, List<SeatHistoryPoint>> _historicalSeatDataByLocation = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, SafeArrivalRecommendation?> _spaceSafeArrivalCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, SafeArrivalRecommendation?> _buildingSafeArrivalCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<float>> _spaceChartSeriesCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<float>> _buildingChartSeriesCache = new(StringComparer.OrdinalIgnoreCase);
    private MensaForecastResult? _mensaForecastCache;
    private List<StudySpace> _allSpaces = [];
    private bool _spaceFeaturesLoaded;
    private bool _hasLoadedWeeklyHistory;
    private bool _hasComputedSafeArrival;
    private bool _hasComputedChartSeries;
    private DateTime _chartReferenceTime = DateTime.MinValue;
    private DateTime _safeArrivalReferenceDate = DateTime.MinValue;
    private bool _isUpdatingDateTimeSelection;
    private string _lastLoadedBeforeParameter = string.Empty;
    private DateTime _lastLiveSnapshotFetchUtc = DateTime.MinValue;
    private CancellationTokenSource? _offlineBannerCts;
    private static readonly HashSet<string> BadischeLandesbibliothekIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "BLB",
        "WIS"
    };
    private const string BadischeLandesbibliothekKey = "BLB_WIS";
    private const string MensaVirtualSpaceId = "MENSA_VIRTUAL";
    private const double MensaFluxThreshold = 15;
    private const int MensaFluxLookbackMinutes = 15;
    private const int MensaQueueBufferMinutes = 15;
    private const int MensaCapacityMin = 200;
    private const double MensaCoverageMinRatio = 0.5;
    private const int MensaCoverageStaleMinutes = 30;
    private const string MensaFluxFillingFastLabel = "Mensa füllt sich schnell";
    private const string MensaFluxEmptyingLabel = "Mensa leert sich";

    public bool IsMainContentVisible => CurrentTab != _config.Tabs.Settings;
    public bool IsSettingsContentVisible => CurrentTab == _config.Tabs.Settings;
    public bool IsDataVisible => !IsBusy && UiLocations.Count > 0 && IsMainContentVisible;
    public bool IsListEmpty => UiLocations.Count == 0 && IsMainContentVisible;
    public bool IsHomeEmpty => UiLocations.Count == 0 && CurrentTab == _config.Tabs.Home;
    public bool IsFavoritesEmpty => !IsBusy && UiLocations.Count == 0 && CurrentTab == _config.Tabs.Favorites;
    public bool IsNoResultsEmpty => UiLocations.Count == 0 && _allSpaces.Count > 0 && CurrentTab == _config.Tabs.Home && IsMainContentVisible;
    public string EmptyStateTitle => IsNoResultsEmpty ? _config.UiText.NoResultsTitle : MainPageViewModel.WelcomeMessage;
    public string EmptyStateSubtitle => IsNoResultsEmpty ? _config.UiText.NoResultsSubtitle : string.Empty;
    public bool IsEmptySubtitleVisible => IsNoResultsEmpty;
    public bool IsBeforeMode => !UseNow;
    public bool IsSearchInactive => !IsSearchActive;
    public DateTime MaxSelectableDate => DateTime.Today;
    public double MinimumOpenHoursMin => _config.UiNumbers.MinOpeningHours;
    public double MinimumOpenHoursMax => _config.UiNumbers.MaxOpeningHours;
    public string MinimumOpenHoursText =>
        string.Format(CultureInfo.CurrentCulture, _config.UiText.MinimumOpenHoursFormat, MinimumOpenHours);
    public string ShowResultsButtonText =>
        string.Format(CultureInfo.CurrentCulture, _config.UiText.ShowResultsFormat, FilteredLocationCount);
    public string SettingsVersionText =>
        string.Format(CultureInfo.CurrentCulture, _config.UiText.SettingsVersionFormat, _config.AppInfo.Version);
    public static string WelcomeMessage => GetWelcomeMessage();

    public List<string> SortOptions { get; }

    private static string GetWelcomeMessage()
    {
        var time = DateTime.Now.TimeOfDay;
        if (time >= new TimeSpan(6, 0, 0) && time < new TimeSpan(9, 0, 0))
        {
            return "Guten Morgen! ☕";
        }

        if (time >= new TimeSpan(9, 0, 0) && time < new TimeSpan(18, 0, 0))
        {
            return "Zeit zum produktiv sein! 📚";
        }

        if (time >= new TimeSpan(18, 0, 0) && time < new TimeSpan(22, 0, 0))
        {
            return "Spätschicht heute? 🌙";
        }

        return "Willkommen Nachteule! 🦉";
    }

    public MainPageViewModel(
        SeatFinderService seatFinderService,
        SafeArrivalForecastService safeArrivalForecastService,
        AppConfig config)
    {
        _config = config;
        _seatFinderService = seatFinderService;
        _safeArrivalForecastService = safeArrivalForecastService;
        SortOptions =
        [
            _config.Sort.Relevance,
            _config.Sort.MostFree,
            _config.Sort.MostTotal,
            _config.Sort.Alphabetical
        ];
        _minimumOpenHours = _config.UiNumbers.MinOpeningHours;
        _selectedSortOption = Preferences.Default.Get(_config.Preferences.SortModeKey, _config.Sort.Relevance);
        _currentTab = ResolveInitialTab(Preferences.Default.Get(_config.Preferences.TabModeKey, _config.Tabs.Home));
        _isColorBlindMode = Preferences.Default.Get(_config.Preferences.ColorBlindModeKey, false);
        _isCampusSouthOnly = Preferences.Default.Get(_config.Preferences.CampusSouthOnlyKey, false);
        _isHapticFeedbackEnabled = Preferences.Default.Get(_config.Preferences.HapticFeedbackKey, true);
        _isHideClosedLocations = Preferences.Default.Get(_config.Preferences.HideClosedLocationsKey, false);

        if (!SortOptions.Contains(_selectedSortOption))
        {
            _selectedSortOption = _config.Sort.Relevance;
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
    private async Task ResetFiltersAsync()
    {
        var hadActiveFilters = IsAnyFilterActive();

        SyncSelectedDateTimeToNow();
        UseNow = true;

        IsGroupRoomSelected = false;
        IsSilentStudySelected = false;
        IsNoReservationSelected = false;
        RequireFreeWifi = false;
        RequirePowerOutlets = false;
        RequireWhiteboard = false;
        MinimumOpenHours = _config.UiNumbers.MinOpeningHours;

        UpdateFilteredLocationPreviewCount();

        if (!hadActiveFilters)
        {
            return;
        }

        var requestedBeforeParameter = GetApiBeforeParameter();
        if (ShouldRefreshSnapshot(requestedBeforeParameter))
        {
            await LoadSpacesAsync();
        }
        else
        {
            ApplyFilter();
        }
    }

    private bool IsAnyFilterActive()
    {
        if (!UseNow)
        {
            return true;
        }

        if (IsGroupRoomSelected || IsSilentStudySelected || IsNoReservationSelected)
        {
            return true;
        }

        if (RequireFreeWifi || RequirePowerOutlets || RequireWhiteboard)
        {
            return true;
        }

        return MinimumOpenHours > _config.UiNumbers.MinOpeningHours;
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
            SelectedTime = _config.UiNumbers.DefaultBeforeTime;
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

        Preferences.Default.Set(_config.Preferences.SortModeKey, value);
        UpdateFilteredLocationPreviewCount();
    }

    partial void OnIsColorBlindModeChanged(bool value)
    {
        Preferences.Default.Set(_config.Preferences.ColorBlindModeKey, value);
        ApplyFilter();
    }

    partial void OnIsCampusSouthOnlyChanged(bool value)
    {
        Preferences.Default.Set(_config.Preferences.CampusSouthOnlyKey, value);
        ApplyFilter();
    }

    partial void OnIsHapticFeedbackEnabledChanged(bool value)
    {
        Preferences.Default.Set(_config.Preferences.HapticFeedbackKey, value);
    }

    partial void OnIsHideClosedLocationsChanged(bool value)
    {
        Preferences.Default.Set(_config.Preferences.HideClosedLocationsKey, value);
        ApplyFilter();
    }

    [RelayCommand]
    private void SwitchTab(string tabName)
    {
        tabName = NormalizeTab(tabName);
        CurrentTab = tabName;
        Preferences.Default.Set(_config.Preferences.TabModeKey, tabName);

        if (CurrentTab != _config.Tabs.Home)
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
        var json = Preferences.Default.Get(_config.Preferences.FavoritesKey, _config.Preferences.EmptyListJson);
        return JsonSerializer.Deserialize<List<string>>(json) ?? [];
    }

    private void SaveFavoriteNames(List<string> favorites)
    {
        Preferences.Default.Set(_config.Preferences.FavoritesKey, JsonSerializer.Serialize(favorites));
    }

    [RelayCommand]
    private void ToggleFavorite(UiLocation location)
    {
        if (location == null)
        {
            return;
        }

        var wasFavorite = location.IsFavorite;
        location.IsFavorite = !location.IsFavorite;

        if (!wasFavorite && location.IsFavorite)
        {
            PerformFavoriteHaptic();
        }

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

        if (CurrentTab == _config.Tabs.Favorites && !location.IsFavorite)
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
        await Browser.Default.OpenAsync(_config.Urls.Github, BrowserLaunchMode.SystemPreferred);
    }

    [RelayCommand]
    private async Task ShowImpressumAsync()
    {
        await Browser.Default.OpenAsync(_config.Urls.Impressum, BrowserLaunchMode.SystemPreferred);
    }

    [RelayCommand]
    private async Task ShowPrivacyAsync()
    {
        await Browser.Default.OpenAsync(_config.Urls.Privacy, BrowserLaunchMode.SystemPreferred);
    }

    [RelayCommand]
    private async Task ShowLicensesAsync()
    {
        await ShowDialogAsync(_config.UiText.LicensesTitle, _config.UiText.LicensesText);
    }

    [RelayCommand]
    public async Task LoadSpacesAsync()
    {
        if (IsBusy)
        {
            return;
        }

        if (!HasInternetAccess())
        {
            await ShowOfflineBannerAsync();
            IsRefreshing = false;
            ApplyFilter();
            return;
        }

        try
        {
            IsBusy = true;
            await EnsureSpaceFeaturesLoadedAsync();

            var forceWeeklyReload = IsRefreshing;
            var shouldReloadWeeklyHistory = !_hasLoadedWeeklyHistory || forceWeeklyReload;
            var requestedBeforeParameter = GetApiBeforeParameter();
            try
            {
                if (shouldReloadWeeklyHistory)
                {
                    var historyWindow = GetWeeklyHistoryWindow();
                    _allSpaces = await _seatFinderService.FetchSeatDataAsync(
                        limit: _config.SeatFinder.WeeklyHistoryPoints,
                        after: historyWindow.After,
                        before: historyWindow.Before);
                }
                else
                {
                    _allSpaces = await _seatFinderService.FetchSeatDataAsync(
                        limit: _config.SeatFinder.LiveSnapshotPoints,
                        before: requestedBeforeParameter);
                }
            }
            catch (TaskCanceledException)
            {
                await ShowOfflineBannerAsync();
                return;
            }

            ApplySpaceFeatureOverrides(_allSpaces);

            MensaForecastResult? mensaResult = null;
            await Task.Run(() =>
            {
                if (shouldReloadWeeklyHistory)
                {
                    ReplaceHistoricalSeatData(_allSpaces);
                    _hasLoadedWeeklyHistory = true;
                }
                else
                {
                    AppendLatestSeatDataToHistory(_allSpaces);
                }

                if (shouldReloadWeeklyHistory || !_hasComputedSafeArrival)
                {
                    UpdateSafeArrivalRecommendations();
                }
                else
                {
                    ApplyCachedSafeArrivalRecommendations();
                }

                if (shouldReloadWeeklyHistory || !_hasComputedChartSeries)
                {
                    UpdateChartSeriesCache();
                    _hasComputedChartSeries = true;
                }

                mensaResult = BuildMensaForecastResult();
            });

            ApplyMensaForecastResult(mensaResult);

            _lastLoadedBeforeParameter = shouldReloadWeeklyHistory
                ? _config.SeatFinder.NowToken
                : requestedBeforeParameter;
            _lastLiveSnapshotFetchUtc = DateTime.UtcNow;
        }
        finally
        {
            IsBusy = false;
            IsRefreshing = false;
        }

        ApplyFilter();
    }

    private bool ShouldRefreshSnapshot(string requestedBeforeParameter)
    {
        if (!_hasLoadedWeeklyHistory)
        {
            return true;
        }

        if (!string.Equals(requestedBeforeParameter, _config.SeatFinder.NowToken, StringComparison.Ordinal))
        {
            return !string.Equals(requestedBeforeParameter, _lastLoadedBeforeParameter, StringComparison.Ordinal);
        }

        if (!string.Equals(_lastLoadedBeforeParameter, _config.SeatFinder.NowToken, StringComparison.Ordinal))
        {
            return true;
        }

        if (_lastLiveSnapshotFetchUtc == DateTime.MinValue)
        {
            return true;
        }

        return DateTime.UtcNow - _lastLiveSnapshotFetchUtc >=
               TimeSpan.FromMinutes(_config.SeatFinder.LiveRefreshIntervalMinutes);
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    partial void OnSelectedLocationChanged(UiLocation? value)
    {
        if (value == null)
        {
            return;
        }

        MainThread.BeginInvokeOnMainThread(async () => await GoToDetailAsync(value));
        SelectedLocation = null;
    }

    partial void OnUseNowChanged(bool value)
    {
        UpdateFilteredLocationPreviewCount();
    }

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
    partial void OnIsNoReservationSelectedChanged(bool value) => UpdateFilteredLocationPreviewCount();
    partial void OnRequireFreeWifiChanged(bool value) => UpdateFilteredLocationPreviewCount();
    partial void OnRequirePowerOutletsChanged(bool value) => UpdateFilteredLocationPreviewCount();
    partial void OnRequireWhiteboardChanged(bool value) => UpdateFilteredLocationPreviewCount();

    partial void OnMinimumOpenHoursChanged(double value)
    {
        var epsilon = _config.UiNumbers.OpeningHoursSliderSnapEpsilon;
        var rounded = Math.Clamp(
            Math.Round(value),
            _config.UiNumbers.MinOpeningHours,
            _config.UiNumbers.MaxOpeningHours);
        if (Math.Abs(rounded - value) > epsilon)
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
            await using var stream = await FileSystem.OpenAppPackageFileAsync(_config.SeatFinder.SpaceFeaturesFileName);
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
            Debug.WriteLine(string.Format(
                CultureInfo.CurrentCulture,
                _config.Internal.SpaceFeaturesLoadFailedFormat,
                _config.SeatFinder.SpaceFeaturesFileName,
                ex.Message));
        }
    }

    private void ApplySpaceFeatureOverrides(IEnumerable<StudySpace> spaces)
    {
        foreach (var space in spaces)
        {
            if (_spaceFeaturesById.TryGetValue(space.Id, out var features) &&
                !string.IsNullOrWhiteSpace(features.Nickname))
            {
                space.Nickname = features.Nickname.Trim();
            }
            else
            {
                space.Nickname = string.Empty;
            }
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
            if (history.Count > _config.SeatFinder.WeeklyHistoryPoints)
            {
                history.RemoveRange(_config.SeatFinder.WeeklyHistoryPoints, history.Count - _config.SeatFinder.WeeklyHistoryPoints);
            }
        }
    }

    private void UpdateSafeArrivalRecommendations()
    {
        _safeArrivalReferenceDate = DateTime.Today;
        _spaceSafeArrivalCache.Clear();
        _buildingSafeArrivalCache.Clear();

        foreach (var space in _allSpaces)
        {
            if (!_historicalSeatDataByLocation.TryGetValue(space.Id, out var history) || history.Count == 0)
            {
                space.SafeArrivalRecommendation = null;
                continue;
            }

            space.SafeArrivalRecommendation = _safeArrivalForecastService.Calculate(space, history, _safeArrivalReferenceDate);
            _spaceSafeArrivalCache[space.Id] = space.SafeArrivalRecommendation;
        }

        foreach (var group in _allSpaces.GroupBy(GetBuildingGroupKey))
        {
            var buildingKey = group.Key?.Trim();
            var spacesInBuilding = group.ToList();

            if (string.IsNullOrWhiteSpace(buildingKey) || spacesInBuilding.Count <= 1)
            {
                continue;
            }

            _buildingSafeArrivalCache[buildingKey] = CalculateBuildingRecommendation(spacesInBuilding);
        }

        _hasComputedSafeArrival = true;
    }

    private void ApplyCachedSafeArrivalRecommendations()
    {
        if (!_hasComputedSafeArrival || _spaceSafeArrivalCache.Count == 0)
        {
            return;
        }

        foreach (var space in _allSpaces)
        {
            if (_spaceSafeArrivalCache.TryGetValue(space.Id, out var recommendation))
            {
                space.SafeArrivalRecommendation = recommendation;
            }
        }
    }

    private void ApplyMensaForecastResult(MensaForecastResult? result)
    {
        _mensaForecastCache = result;
        MensaSafeArrivalRecommendation = result?.Recommendation;
        MensaFluxLabel = result?.FluxLabel ?? string.Empty;

        _spaceChartSeriesCache.Remove(MensaVirtualSpaceId);
        if (result?.ChartSeries != null && result.ChartSeries.Count > 0)
        {
            _spaceChartSeriesCache[MensaVirtualSpaceId] = result.ChartSeries.ToList();
        }
    }

    private MensaForecastResult? BuildMensaForecastResult()
    {
        if (_allSpaces.Count == 0 || _historicalSeatDataByLocation.Count == 0)
        {
            return null;
        }

        var mensaConfig = _config.MensaForecast;

        var stepMinutes = Math.Max(1, mensaConfig.StepMinutes);
        var windowStart = mensaConfig.WindowStart;
        var bufferMinutes = Math.Max(0, mensaConfig.EatingBufferMinutes);
        var windowEnd = mensaConfig.CashDeskClose.Add(TimeSpan.FromMinutes(bufferMinutes));
        var openingHoursEnd = mensaConfig.CashDeskClose;

        if (windowEnd <= windowStart)
        {
            return null;
        }

        var windowStartMinute = NormalizeToStepMinutes(windowStart, stepMinutes);
        var windowEndMinute = NormalizeToStepMinutes(windowEnd, stepMinutes);
        if (windowEndMinute <= windowStartMinute)
        {
            return null;
        }

        var referenceDate = _safeArrivalReferenceDate == DateTime.MinValue
            ? DateTime.Today
            : _safeArrivalReferenceDate;

        var historyDays = Math.Max(1, _config.SafeArrival.HistoryWindowDays);
        var endDate = referenceDate.Date.AddDays(-1);
        var startDate = endDate.AddDays(-(historyDays - 1));

        var snapshots = BuildMensaSpaceSnapshots(
            startDate,
            endDate,
            windowStartMinute,
            windowEndMinute,
            stepMinutes,
            mensaConfig);

        var fluxLabel = BuildMensaFluxLabel(
            DateTime.Now,
            windowStartMinute,
            windowEndMinute,
            stepMinutes,
            snapshots);

        var coverageRatio = CalculateMensaCoverageRatio(GetReferenceDateTime());

        if (snapshots.Count == 0)
        {
            return new MensaForecastResult(
                null,
                fluxLabel,
                snapshots,
                windowStartMinute,
                windowEndMinute,
                stepMinutes,
                windowStart,
                windowEnd,
                openingHoursEnd,
                referenceDate,
                Array.Empty<float>(),
                0);
        }

        var history = BuildMensaVirtualHistory(
            startDate,
            endDate,
            windowStartMinute,
            windowEndMinute,
            stepMinutes,
            snapshots);
        AppendMensaLiveHistoryPoints(
            history,
            DateTime.Today,
            DateTime.Now,
            windowStartMinute,
            windowEndMinute,
            stepMinutes,
            snapshots);

        var peakDeficitHistory = CalculateMensaPeakDeficitHistory(
            referenceDate,
            historyDays,
            windowStartMinute,
            windowEndMinute,
            stepMinutes,
            snapshots);
        var capacity = Math.Max(peakDeficitHistory, MensaCapacityMin);
        NormalizeMensaHistoryCapacity(history, capacity);

        SafeArrivalRecommendation? recommendation = null;
        IReadOnlyList<float> chartSeries = Array.Empty<float>();
        if (history.Count > 0)
        {
            history.Sort((a, b) => b.Timestamp.CompareTo(a.Timestamp));

            var virtualSpace = new StudySpace
            {
                Id = MensaVirtualSpaceId,
                Name = "Mensa (virtuell)",
                TotalSeats = capacity,
                OpeningHours = BuildMensaOpeningHours(referenceDate, windowStart, windowEnd),
                ReferenceTime = referenceDate
            };

            recommendation = _safeArrivalForecastService.Calculate(virtualSpace, history, referenceDate);
            recommendation = ApplyMensaQueueBuffer(recommendation, windowStart);
            recommendation = ApplyMensaCoverageGuard(recommendation, coverageRatio);

            var chartConfig = _config.Charts;
            var binMinutes = Math.Max(1, chartConfig.BinMinutes);
            var endTime = NormalizeToChartBin(DateTime.Now, binMinutes);
            var startTime = endTime.AddHours(-chartConfig.HistoryHours);
            chartSeries = BuildOccupancySeries(history, startTime, endTime, binMinutes);
        }

        return new MensaForecastResult(
            recommendation,
            fluxLabel,
            snapshots,
            windowStartMinute,
            windowEndMinute,
            stepMinutes,
            windowStart,
            windowEnd,
            openingHoursEnd,
            referenceDate,
            chartSeries,
            capacity);
    }

    private static SafeArrivalRecommendation? ApplyMensaQueueBuffer(
        SafeArrivalRecommendation? recommendation,
        TimeSpan windowStart)
    {
        if (recommendation == null || !recommendation.HasRecommendation)
        {
            return recommendation;
        }

        var adjustedLatest = recommendation.LatestSafeTime - TimeSpan.FromMinutes(MensaQueueBufferMinutes);
        if (adjustedLatest < windowStart)
        {
            adjustedLatest = windowStart;
        }

        return new SafeArrivalRecommendation
        {
            HasRecommendation = recommendation.HasRecommendation,
            LatestSafeTime = adjustedLatest,
            Probability = recommendation.Probability,
            ExpectedFreeSeats = recommendation.ExpectedFreeSeats,
            ConfidenceFlag = recommendation.ConfidenceFlag,
            HasPeakData = recommendation.HasPeakData,
            PeakTime = recommendation.PeakTime,
            PeakExpectedFreeSeats = recommendation.PeakExpectedFreeSeats,
            PeakOccupancyRate = recommendation.PeakOccupancyRate,
            PeakTrendMinutesPerDay = recommendation.PeakTrendMinutesPerDay
        };
    }

    private List<MensaSpaceSnapshot> BuildMensaSpaceSnapshots(
        DateTime startDate,
        DateTime endDate,
        int windowStartMinute,
        int windowEndMinute,
        int stepMinutes,
        MensaForecastConfig mensaConfig)
    {
        var snapshots = new List<MensaSpaceSnapshot>();

        foreach (var space in _allSpaces)
        {
            if (space.TotalSeats < mensaConfig.MinSpaceCapacity)
            {
                continue;
            }

            if (!_historicalSeatDataByLocation.TryGetValue(space.Id, out var history) || history.Count == 0)
            {
                continue;
            }

            var occupiedByDay = BuildOccupiedByDay(
                history,
                startDate,
                endDate,
                windowStartMinute,
                windowEndMinute,
                stepMinutes);

            if (occupiedByDay.Count == 0)
            {
                continue;
            }

            var occupiedToday = BuildOccupiedForDay(
                history,
                DateTime.Today,
                windowStartMinute,
                windowEndMinute,
                stepMinutes);

            snapshots.Add(new MensaSpaceSnapshot(occupiedByDay, occupiedToday));
        }

        return snapshots;
    }

    private static Dictionary<DateTime, Dictionary<int, int>> BuildOccupiedByDay(
        IReadOnlyList<SeatHistoryPoint> history,
        DateTime startDate,
        DateTime endDate,
        int windowStartMinute,
        int windowEndMinute,
        int stepMinutes)
    {
        var occupiedByDay = new Dictionary<DateTime, Dictionary<int, int>>();

        foreach (var point in history)
        {
            var day = point.Timestamp.Date;
            if (day < startDate || day > endDate)
            {
                continue;
            }

            if (!IsMensaOpenDay(day))
            {
                continue;
            }

            var minute = NormalizeToStepMinutes(point.Timestamp.TimeOfDay, stepMinutes);
            if (minute < windowStartMinute || minute > windowEndMinute)
            {
                continue;
            }

            if (!occupiedByDay.TryGetValue(day, out var dayMap))
            {
                dayMap = new Dictionary<int, int>();
                occupiedByDay[day] = dayMap;
            }

            if (!dayMap.ContainsKey(minute))
            {
                dayMap[minute] = Math.Max(0, point.OccupiedSeats);
            }
        }

        return occupiedByDay;
    }

    private static Dictionary<int, int> BuildOccupiedForDay(
        IReadOnlyList<SeatHistoryPoint> history,
        DateTime day,
        int windowStartMinute,
        int windowEndMinute,
        int stepMinutes)
    {
        var occupiedByMinute = new Dictionary<int, int>();

        foreach (var point in history)
        {
            if (point.Timestamp.Date != day)
            {
                continue;
            }

            var minute = NormalizeToStepMinutes(point.Timestamp.TimeOfDay, stepMinutes);
            if (minute < windowStartMinute || minute > windowEndMinute)
            {
                continue;
            }

            if (!occupiedByMinute.ContainsKey(minute))
            {
                occupiedByMinute[minute] = Math.Max(0, point.OccupiedSeats);
            }
        }

        return occupiedByMinute;
    }

    private static List<SeatHistoryPoint> BuildMensaVirtualHistory(
        DateTime startDate,
        DateTime endDate,
        int windowStartMinute,
        int windowEndMinute,
        int stepMinutes,
        List<MensaSpaceSnapshot> snapshots)
    {
        var history = new List<SeatHistoryPoint>();

        for (var day = startDate.Date; day <= endDate.Date; day = day.AddDays(1))
        {
            if (!IsMensaOpenDay(day))
            {
                continue;
            }

            for (var minute = windowStartMinute; minute <= windowEndMinute; minute += stepMinutes)
            {
                if (!TryCalculateTotalDeficit(
                        day,
                        minute,
                        windowStartMinute,
                        windowEndMinute,
                        snapshots,
                        out var totalDeficit))
                {
                    continue;
                }

                var occupancy = Math.Max(0, (int)Math.Round(totalDeficit));

                history.Add(new SeatHistoryPoint
                {
                    Timestamp = day.AddMinutes(minute),
                    FreeSeats = 0,
                    OccupiedSeats = occupancy,
                    IsManualCount = false
                });
            }
        }

        return history;
    }

    private static int CalculateMensaPeakDeficitHistory(
        DateTime referenceDate,
        int historyWindowDays,
        int windowStartMinute,
        int windowEndMinute,
        int stepMinutes,
        List<MensaSpaceSnapshot> snapshots)
    {
        if (snapshots.Count == 0)
        {
            return 0;
        }

        var maxDays = Math.Min(7, Math.Max(1, historyWindowDays));
        var recentDays = GetRecentMensaOpenDays(referenceDate.Date, maxDays, historyWindowDays);
        var peak = 0.0;

        foreach (var day in recentDays)
        {
            if (!TryCalculateNetDropForDay(
                    day,
                    windowStartMinute,
                    windowEndMinute,
                    stepMinutes,
                    snapshots,
                    out var netDrop))
            {
                continue;
            }

            if (netDrop > peak)
            {
                peak = netDrop;
            }
        }

        return Math.Max(0, (int)Math.Round(peak));
    }

    private static bool TryCalculateNetDropForDay(
        DateTime day,
        int windowStartMinute,
        int windowEndMinute,
        int stepMinutes,
        List<MensaSpaceSnapshot> snapshots,
        out double netDrop)
    {
        netDrop = 0;
        var hasData = false;

        for (var minute = windowStartMinute; minute <= windowEndMinute; minute += stepMinutes)
        {
            if (!TryCalculateTotalDeficit(
                    day,
                    minute,
                    windowStartMinute,
                    windowEndMinute,
                    snapshots,
                    out var totalDeficit))
            {
                continue;
            }

            hasData = true;
            if (totalDeficit > netDrop)
            {
                netDrop = totalDeficit;
            }
        }

        return hasData;
    }

    private double CalculateMensaCoverageRatio(DateTime referenceTime)
    {
        if (_allSpaces.Count == 0)
        {
            return 0;
        }

        var cutoff = referenceTime - TimeSpan.FromMinutes(MensaCoverageStaleMinutes);
        var totalSeats = 0;
        var coveredSeats = 0;

        foreach (var space in _allSpaces)
        {
            var seats = Math.Max(0, space.TotalSeats);
            if (seats == 0)
            {
                continue;
            }

            totalSeats += seats;
            if (space.LastUpdated >= cutoff &&
                space.LastUpdated.Year > _config.UiNumbers.UnknownYearThreshold)
            {
                coveredSeats += seats;
            }
        }

        return totalSeats > 0 ? coveredSeats / (double)totalSeats : 0;
    }

    private static SafeArrivalRecommendation? ApplyMensaCoverageGuard(
        SafeArrivalRecommendation? recommendation,
        double coverageRatio)
    {
        if (recommendation == null || coverageRatio >= MensaCoverageMinRatio)
        {
            return recommendation;
        }

        return new SafeArrivalRecommendation
        {
            HasRecommendation = recommendation.HasRecommendation,
            LatestSafeTime = recommendation.LatestSafeTime,
            Probability = recommendation.Probability,
            ExpectedFreeSeats = recommendation.ExpectedFreeSeats,
            ConfidenceFlag = false,
            HasPeakData = recommendation.HasPeakData,
            PeakTime = recommendation.PeakTime,
            PeakExpectedFreeSeats = recommendation.PeakExpectedFreeSeats,
            PeakOccupancyRate = recommendation.PeakOccupancyRate,
            PeakTrendMinutesPerDay = recommendation.PeakTrendMinutesPerDay
        };
    }

    private static void NormalizeMensaHistoryCapacity(List<SeatHistoryPoint> history, int capacity)
    {
        if (history.Count == 0)
        {
            return;
        }

        var normalizedCapacity = Math.Max(0, capacity);
        for (var i = 0; i < history.Count; i++)
        {
            var point = history[i];
            history[i] = new SeatHistoryPoint
            {
                Timestamp = point.Timestamp,
                FreeSeats = Math.Max(0, normalizedCapacity - point.OccupiedSeats),
                OccupiedSeats = point.OccupiedSeats,
                IsManualCount = point.IsManualCount
            };
        }
    }

    private static void AppendMensaLiveHistoryPoints(
        List<SeatHistoryPoint> history,
        DateTime today,
        DateTime now,
        int windowStartMinute,
        int windowEndMinute,
        int stepMinutes,
        List<MensaSpaceSnapshot> snapshots)
    {
        if (history == null || snapshots.Count == 0)
        {
            return;
        }

        if (!IsMensaOpenDay(today))
        {
            return;
        }

        if (!TryGetLastMensaReferenceDay(today, snapshots, out var referenceDay))
        {
            return;
        }

        if (now.Date != today.Date)
        {
            now = today.AddMinutes(windowEndMinute);
        }

        var nowMinute = NormalizeToStepMinutes(now.TimeOfDay, stepMinutes);
        if (nowMinute < windowStartMinute)
        {
            return;
        }

        var lastMinute = Math.Min(nowMinute, windowEndMinute);

        for (var minute = windowStartMinute; minute <= lastMinute; minute += stepMinutes)
        {
            if (!TryCalculateHybridTotalDeficit(
                    referenceDay,
                    minute,
                    windowStartMinute,
                    windowEndMinute,
                    snapshots,
                    out var totalDeficit))
            {
                continue;
            }

            var occupancy = Math.Max(0, (int)Math.Round(totalDeficit));

            history.Add(new SeatHistoryPoint
            {
                Timestamp = today.AddMinutes(minute),
                FreeSeats = 0,
                OccupiedSeats = occupancy,
                IsManualCount = false
            });
        }
    }

    private static bool TryGetLastMensaReferenceDay(
        DateTime today,
        List<MensaSpaceSnapshot> snapshots,
        out DateTime referenceDay)
    {
        referenceDay = DateTime.MinValue;

        foreach (var snapshot in snapshots)
        {
            foreach (var day in snapshot.OccupiedByDay.Keys)
            {
                if (day >= today || !IsMensaOpenDay(day))
                {
                    continue;
                }

                if (day > referenceDay)
                {
                    referenceDay = day;
                }
            }
        }

        return referenceDay != DateTime.MinValue;
    }


    private static List<DateTime> GetRecentMensaOpenDays(DateTime referenceDay, int maxDays, int historyWindowDays)
    {
        var days = new List<DateTime>();
        var earliest = referenceDay.AddDays(-Math.Max(1, historyWindowDays));

        for (var day = referenceDay.AddDays(-1); day >= earliest && days.Count < maxDays; day = day.AddDays(-1))
        {
            if (IsMensaOpenDay(day))
            {
                days.Add(day);
            }
        }

        return days;
    }

    private string BuildMensaFluxLabel(
        DateTime now,
        int windowStartMinute,
        int windowEndMinute,
        int stepMinutes,
        List<MensaSpaceSnapshot> snapshots)
    {
        if (snapshots.Count == 0)
        {
            return string.Empty;
        }

        var nowDate = now.Date;
        if (!IsMensaOpenDay(nowDate))
        {
            return string.Empty;
        }

        var past = now.AddMinutes(-MensaFluxLookbackMinutes);
        if (past.Date != nowDate)
        {
            return string.Empty;
        }

        var nowMinute = NormalizeToStepMinutes(now.TimeOfDay, stepMinutes);
        var pastMinute = NormalizeToStepMinutes(past.TimeOfDay, stepMinutes);

        if (nowMinute < windowStartMinute || nowMinute > windowEndMinute ||
            pastMinute < windowStartMinute || pastMinute > windowEndMinute)
        {
            return string.Empty;
        }

        if (!TryCalculateTotalDeficit(
                nowDate,
                nowMinute,
                windowStartMinute,
                windowEndMinute,
                snapshots,
                out var deficitNow))
        {
            return string.Empty;
        }

        if (!TryCalculateTotalDeficit(
                nowDate,
                pastMinute,
                windowStartMinute,
                windowEndMinute,
                snapshots,
                out var deficitPast))
        {
            return string.Empty;
        }

        var flux = deficitNow - deficitPast;
        if (flux > MensaFluxThreshold)
        {
            return MensaFluxFillingFastLabel;
        }

        if (flux < -MensaFluxThreshold)
        {
            return MensaFluxEmptyingLabel;
        }

        return string.Empty;
    }

    private static bool TryCalculateTotalDeficit(
        DateTime day,
        int minute,
        int windowStartMinute,
        int windowEndMinute,
        List<MensaSpaceSnapshot> snapshots,
        out double totalDeficit)
    {
        totalDeficit = 0;
        if (minute < windowStartMinute || minute > windowEndMinute || windowEndMinute <= windowStartMinute)
        {
            return false;
        }

        var frac = (minute - windowStartMinute) / (double)(windowEndMinute - windowStartMinute);
        var hasData = false;

        foreach (var snapshot in snapshots)
        {
            if (!snapshot.OccupiedByDay.TryGetValue(day, out var dayMap))
            {
                continue;
            }

            if (!TryGetNearestOccupied(dayMap, windowStartMinute, out var occStart) ||
                !TryGetNearestOccupied(dayMap, windowEndMinute, out var occEnd) ||
                !TryGetNearestOccupied(dayMap, minute, out var occNow))
            {
                continue;
            }

            hasData = true;
            var baseline = occStart + (occEnd - occStart) * frac;
            var deficit = Math.Max(0, baseline - occNow);
            totalDeficit += deficit;
        }

        return hasData;
    }

    private static bool TryGetNearestOccupied(Dictionary<int, int> dayMap, int targetMinute, out int occupied)
    {
        occupied = 0;
        if (dayMap.Count == 0)
        {
            return false;
        }

        if (dayMap.TryGetValue(targetMinute, out occupied))
        {
            return true;
        }

        var bestDiff = int.MaxValue;
        var bestValue = 0;

        foreach (var entry in dayMap)
        {
            var diff = Math.Abs(entry.Key - targetMinute);
            if (diff >= bestDiff)
            {
                continue;
            }

            bestDiff = diff;
            bestValue = entry.Value;
        }

        // Wenn der am nächsten gelegene Punkt mehr als 30 Minuten entfernt ist,
        // betrachten wir es als Datenlücke und brechen ab.
        if (bestDiff > 30)
        {
            return false;
        }

        occupied = bestValue;
        return true;
    }

    private OpeningHoursDto BuildMensaOpeningHours(DateTime referenceDate, TimeSpan windowStart, TimeSpan windowEnd)
    {
        var format = string.IsNullOrWhiteSpace(_config.SeatFinder.TimestampFormat)
            ? "yyyy-MM-dd HH:mm:ss.ffffff"
            : _config.SeatFinder.TimestampFormat;

        var daysSinceMonday = ((int)referenceDate.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        var monday = referenceDate.Date.AddDays(-daysSinceMonday);

        var weeklyOpeningHours = new List<List<TimestampDto>>();
        for (var dayOffset = 0; dayOffset < 5; dayOffset++)
        {
            var day = monday.AddDays(dayOffset);
            weeklyOpeningHours.Add(
            [
                new TimestampDto { Date = day.Add(windowStart).ToString(format, CultureInfo.InvariantCulture) },
                new TimestampDto { Date = day.Add(windowEnd).ToString(format, CultureInfo.InvariantCulture) }
            ]);
        }

        return new OpeningHoursDto
        {
            WeeklyOpeningHours = weeklyOpeningHours
        };
    }

    private static int NormalizeToStepMinutes(TimeSpan timeOfDay, int stepMinutes)
    {
        var minutes = timeOfDay.TotalMinutes;
        var normalized = (int)Math.Round(minutes / stepMinutes, MidpointRounding.AwayFromZero) * stepMinutes;
        var maxMinute = (24 * 60) - stepMinutes;
        return Math.Clamp(normalized, 0, maxMinute);
    }

    private static bool IsMensaOpenDay(DateTime date)
    {
        return date.DayOfWeek >= DayOfWeek.Monday && date.DayOfWeek <= DayOfWeek.Friday;
    }

    private sealed record MensaForecastResult(
        SafeArrivalRecommendation? Recommendation,
        string FluxLabel,
        List<MensaSpaceSnapshot> Snapshots,
        int WindowStartMinute,
        int WindowEndMinute,
        int StepMinutes,
        TimeSpan WindowStart,
        TimeSpan WindowEnd,
        TimeSpan OpeningHoursEnd,
        DateTime ReferenceDate,
        IReadOnlyList<float> ChartSeries,
        int Capacity);

    private sealed record MensaSpaceSnapshot(
        Dictionary<DateTime, Dictionary<int, int>> OccupiedByDay,
        Dictionary<int, int> OccupiedToday);

    private void UpdateChartSeriesCache()
    {
        _spaceChartSeriesCache.Clear();
        _buildingChartSeriesCache.Clear();

        if (_allSpaces.Count == 0)
        {
            return;
        }

        var chartConfig = _config.Charts;
        var binMinutes = Math.Max(1, chartConfig.BinMinutes);
        var endTime = NormalizeToChartBin(DateTime.Now, binMinutes);
        var startTime = endTime.AddHours(-chartConfig.HistoryHours);
        _chartReferenceTime = endTime;

        foreach (var space in _allSpaces)
        {
            if (!_historicalSeatDataByLocation.TryGetValue(space.Id, out var history) || history.Count == 0)
            {
                continue;
            }

            var series = BuildOccupancySeries(history, startTime, endTime, binMinutes);
            if (series.Count > 0)
            {
                _spaceChartSeriesCache[space.Id] = series;
            }
        }

        foreach (var group in _allSpaces.GroupBy(GetBuildingGroupKey))
        {
            var buildingKey = group.Key?.Trim();
            var spacesInBuilding = group.ToList();

            if (string.IsNullOrWhiteSpace(buildingKey) || spacesInBuilding.Count <= 1)
            {
                continue;
            }

            var series = BuildAggregateOccupancySeries(spacesInBuilding, startTime, endTime, binMinutes);
            if (series.Count > 0)
            {
                _buildingChartSeriesCache[buildingKey] = series;
            }
        }
    }

    private static List<float> BuildOccupancySeries(
        IEnumerable<SeatHistoryPoint> history,
        DateTime startTime,
        DateTime endTime,
        int binMinutes)
    {
        var buckets = new Dictionary<DateTime, (int free, int occupied)>();

        foreach (var point in history)
        {
            if (point.Timestamp < startTime || point.Timestamp > endTime)
            {
                continue;
            }

            var bucketTime = NormalizeToChartBin(point.Timestamp, binMinutes);
            if (!buckets.TryGetValue(bucketTime, out var totals))
            {
                totals = (0, 0);
            }

            totals.free += point.FreeSeats;
            totals.occupied += point.OccupiedSeats;
            buckets[bucketTime] = totals;
        }

        var series = new List<float>();
        float lastValue = 0f;
        var hasValue = false;

        for (var time = startTime; time <= endTime; time = time.AddMinutes(binMinutes))
        {
            if (buckets.TryGetValue(time, out var totals))
            {
                var value = (float)CalculateOccupancyRate(totals.free, totals.occupied);
                lastValue = value;
                hasValue = true;
                series.Add(value);
                continue;
            }

            series.Add(hasValue ? lastValue : 0f);
        }

        return series;
    }

    private List<float> BuildAggregateOccupancySeries(
        List<StudySpace> spaces,
        DateTime startTime,
        DateTime endTime,
        int binMinutes)
    {
        var bucketCount = GetBucketCount(startTime, endTime, binMinutes);
        if (bucketCount == 0)
        {
            return [];
        }

        var totals = new (int free, int occupied)[bucketCount];

        foreach (var space in spaces)
        {
            if (!_historicalSeatDataByLocation.TryGetValue(space.Id, out var history) || history.Count == 0)
            {
                continue;
            }

            var series = BuildBucketSeries(history, startTime, endTime, binMinutes, bucketCount);
            for (var i = 0; i < bucketCount; i++)
            {
                totals[i].free += series[i].free;
                totals[i].occupied += series[i].occupied;
            }
        }

        var output = new List<float>(bucketCount);
        for (var i = 0; i < bucketCount; i++)
        {
            var value = (float)CalculateOccupancyRate(totals[i].free, totals[i].occupied);
            output.Add(value);
        }

        return output;
    }

    private static List<(int free, int occupied)> BuildBucketSeries(
        IEnumerable<SeatHistoryPoint> history,
        DateTime startTime,
        DateTime endTime,
        int binMinutes,
        int bucketCount)
    {
        var buckets = new Dictionary<DateTime, (int free, int occupied)>();

        foreach (var point in history)
        {
            if (point.Timestamp < startTime || point.Timestamp > endTime)
            {
                continue;
            }

            var bucketTime = NormalizeToChartBin(point.Timestamp, binMinutes);
            if (!buckets.TryGetValue(bucketTime, out var totals))
            {
                totals = (0, 0);
            }

            totals.free += point.FreeSeats;
            totals.occupied += point.OccupiedSeats;
            buckets[bucketTime] = totals;
        }

        var series = new List<(int free, int occupied)>(bucketCount);
        var hasValue = false;
        var lastTotals = (free: 0, occupied: 0);

        var index = 0;
        for (var time = startTime; time <= endTime && index < bucketCount; time = time.AddMinutes(binMinutes))
        {
            if (buckets.TryGetValue(time, out var totals))
            {
                lastTotals = totals;
                hasValue = true;
                series.Add(totals);
            }
            else
            {
                series.Add(hasValue ? lastTotals : (0, 0));
            }

            index++;
        }

        while (series.Count < bucketCount)
        {
            series.Add(hasValue ? lastTotals : (0, 0));
        }

        return series;
    }

    private static int GetBucketCount(DateTime startTime, DateTime endTime, int binMinutes)
    {
        if (endTime < startTime || binMinutes <= 0)
        {
            return 0;
        }

        var totalMinutes = (endTime - startTime).TotalMinutes;
        return (int)Math.Floor(totalMinutes / binMinutes) + 1;
    }

    private static DateTime NormalizeToChartBin(DateTime timestamp, int binMinutes)
    {
        var totalMinutes = (timestamp.Hour * 60) + timestamp.Minute;
        var normalizedMinutes = (totalMinutes / binMinutes) * binMinutes;
        return timestamp.Date.AddMinutes(normalizedMinutes);
    }

    private static double CalculateOccupancyRate(int freeSeats, int occupiedSeats)
    {
        var total = freeSeats + occupiedSeats;
        if (total <= 0)
        {
            return 0;
        }

        return Math.Clamp(occupiedSeats / (double)total, 0, 1);
    }

    private void ApplyFilter()
    {
        if (IsBusy)
        {
            return;
        }

        if (_allSpaces.Count == 0)
        {
            ReplaceUiLocations([]);
            return;
        }

        var filteredSpaces = ApplySpaceFilters(GetSpacesWithMensa());
        var results = MapSpacesToUiLocations(filteredSpaces);

        if (CurrentTab == _config.Tabs.Home && IsHideClosedLocations)
        {
            results = results
                .Where(location => location.IsOpen || location.IsStudentOnlyClosed)
                .ToList();
        }

        if (CurrentTab == _config.Tabs.Favorites)
        {
            results = results.Where(location => location.IsFavorite).ToList();
        }

        var sortedResults = SortLocations(results);
        ReplaceUiLocations(sortedResults);
    }

    private IEnumerable<StudySpace> GetSpacesWithMensa()
    {
        if (_allSpaces.Count == 0)
        {
            return _allSpaces;
        }

        var spaces = new List<StudySpace>(_allSpaces);
        var mensaSpace = BuildMensaVirtualSpace();
        if (mensaSpace != null)
        {
            spaces.Add(mensaSpace);
        }

        return spaces;
    }

    private StudySpace? BuildMensaVirtualSpace()
    {
        var cache = _mensaForecastCache;
        if (cache == null || cache.Snapshots.Count == 0)
        {
            return null;
        }

        var capacity = cache.Capacity;
        if (capacity <= 0)
        {
            return null;
        }

        var mensaConfig = _config.MensaForecast;
        var referenceTime = GetReferenceDateTime();
        var occupiedSeats = 0;
        var hasOccupancy = TryGetMensaOccupancy(referenceTime, cache, out occupiedSeats);
        var freeSeats = hasOccupancy
            ? Math.Max(0, capacity - occupiedSeats)
            : 0;

        return new StudySpace
        {
            Id = MensaVirtualSpaceId,
            Name = "Mensa am Adenauerring",
            TotalSeats = capacity,
            OccupiedSeats = occupiedSeats,
            FreeSeats = freeSeats,
            IsManualCount = false,
            IsMensaVirtual = true,
            Latitude = mensaConfig.Latitude,
            Longitude = mensaConfig.Longitude,
            OpeningHours = BuildMensaOpeningHours(cache.ReferenceDate, cache.WindowStart, cache.OpeningHoursEnd),
            LastUpdated = hasOccupancy ? referenceTime : DateTime.MinValue,
            ReferenceTime = referenceTime,
            SafeArrivalRecommendation = MensaSafeArrivalRecommendation
        };
    }

    private static bool TryGetMensaOccupancy(
        DateTime referenceTime,
        MensaForecastResult cache,
        out int occupiedSeats)
    {
        occupiedSeats = 0;

        if (!IsMensaOpenDay(referenceTime.Date))
        {
            return false;
        }

        var minute = NormalizeToStepMinutes(referenceTime.TimeOfDay, cache.StepMinutes);
        if (!TryCalculateTotalDeficit(
                referenceTime.Date,
                minute,
                cache.WindowStartMinute,
                cache.WindowEndMinute,
                cache.Snapshots,
                out var totalDeficit))
        {
            if (referenceTime.Date != DateTime.Today ||
                !TryCalculateLiveTotalDeficit(
                    referenceTime.Date,
                    minute,
                    cache.WindowStartMinute,
                    cache.WindowEndMinute,
                    cache.Snapshots,
                    out totalDeficit))
            {
                return false;
            }
        }

        occupiedSeats = Math.Max(0, (int)Math.Round(totalDeficit));
        return true;
    }

    private static bool TryCalculateLiveTotalDeficit(
        DateTime today,
        int minute,
        int windowStartMinute,
        int windowEndMinute,
        List<MensaSpaceSnapshot> snapshots,
        out double totalDeficit)
    {
        totalDeficit = 0;
        if (minute < windowStartMinute || minute > windowEndMinute || windowEndMinute <= windowStartMinute)
        {
            return false;
        }

        if (!TryGetLastMensaReferenceDay(today, snapshots, out var referenceDay))
        {
            return false;
        }

        return TryCalculateHybridTotalDeficit(
            referenceDay,
            minute,
            windowStartMinute,
            windowEndMinute,
            snapshots,
            out totalDeficit);
    }

    private static bool TryCalculateHybridTotalDeficit(
        DateTime referenceDay,
        int minute,
        int windowStartMinute,
        int windowEndMinute,
        List<MensaSpaceSnapshot> snapshots,
        out double totalDeficit)
    {
        totalDeficit = 0;
        var progressRatio = (minute - windowStartMinute) / (double)(windowEndMinute - windowStartMinute);
        var hasData = false;

        foreach (var snapshot in snapshots)
        {
            if (!snapshot.OccupiedByDay.TryGetValue(referenceDay, out var referenceDayMap))
            {
                continue;
            }

            if (!TryGetNearestOccupied(referenceDayMap, windowStartMinute, out var occStartReference) ||
                !TryGetNearestOccupied(referenceDayMap, windowEndMinute, out var occEndReference))
            {
                continue;
            }

            if (!TryGetNearestOccupied(snapshot.OccupiedToday, windowStartMinute, out var occStartToday) ||
                !TryGetNearestOccupied(snapshot.OccupiedToday, minute, out var occNow))
            {
                continue;
            }

            var deltaHist = occEndReference - occStartReference;
            var baseline = occStartToday + (progressRatio * deltaHist);
            var deficit = Math.Max(0, baseline - occNow);
            totalDeficit += deficit;
            hasData = true;
        }

        return hasData;
    }

    private void UpdateFilteredLocationPreviewCount()
    {
        if (IsBusy)
        {
            return;
        }

        if (_allSpaces.Count == 0)
        {
            FilteredLocationCount = 0;
            return;
        }

        var filteredSpaces = ApplySpaceFilters(GetSpacesWithMensa());
        var results = MapSpacesToUiLocations(filteredSpaces);

        if (CurrentTab == _config.Tabs.Home && IsHideClosedLocations)
        {
            results = results
                .Where(location => location.IsOpen || location.IsStudentOnlyClosed)
                .ToList();
        }

        if (CurrentTab == _config.Tabs.Favorites)
        {
            results = results.Where(location => location.IsFavorite).ToList();
        }

        FilteredLocationCount = SortLocations(results).Count;
    }

    private IEnumerable<StudySpace> ApplySpaceFilters(IEnumerable<StudySpace> spaces)
    {
        var searchFiltered = FilterSpacesBySearch(spaces);
        var campusFiltered = FilterSpacesByCampusScope(searchFiltered);
        var roomTypeFiltered = FilterSpacesByRoomType(campusFiltered);
        var reservationFiltered = FilterSpacesByReservation(roomTypeFiltered);
        var equipmentFiltered = FilterSpacesByEquipment(reservationFiltered);
        return FilterSpacesByOpeningHours(equipmentFiltered);
    }

    private IEnumerable<StudySpace> FilterSpacesByCampusScope(IEnumerable<StudySpace> spaces)
    {
        if (!IsCampusSouthOnly)
        {
            return spaces;
        }

        var tokens = _config.CampusSouth.ExcludedNameContains;
        if (tokens.Count == 0)
        {
            return spaces;
        }

        return spaces.Where(space => !IsExcludedByCampusScope(space.Name, tokens));
    }

    private static bool IsExcludedByCampusScope(string? name, List<string> tokens)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        foreach (var token in tokens)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                continue;
            }

            if (name.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
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
        return IsGroupRoomSelected || IsSilentStudySelected;
    }

    private HashSet<string> GetSelectedRoomTypes()
    {
        var selectedRoomTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (IsGroupRoomSelected)
        {
            selectedRoomTypes.Add(_config.RoomTypes.Group);
        }

        if (IsSilentStudySelected)
        {
            selectedRoomTypes.Add(_config.RoomTypes.SilentStudy);
            selectedRoomTypes.Add(_config.RoomTypes.SilentStudyLegacy);
        }

        return selectedRoomTypes;
    }

    private IEnumerable<StudySpace> FilterSpacesByReservation(IEnumerable<StudySpace> spaces)
    {
        if (!IsNoReservationSelected)
        {
            return spaces;
        }

        return spaces.Where(space =>
        {
            if (!_spaceFeaturesById.TryGetValue(space.Id, out var features))
            {
                return false;
            }

            return !features.RequiresReservation;
        });
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
        if (MinimumOpenHours <= _config.UiNumbers.MinOpeningHours)
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
            var value when string.Equals(value, _config.Sort.MostFree, StringComparison.Ordinal) =>
                locations.OrderByDescending(location => location.FreeSeats).ToList(),
            var value when string.Equals(value, _config.Sort.MostTotal, StringComparison.Ordinal) =>
                locations.OrderByDescending(location => location.TotalSeats).ToList(),
            var value when string.Equals(value, _config.Sort.Alphabetical, StringComparison.Ordinal) =>
                locations.OrderBy(location => location.Name).ToList(),
            _ => locations
                .Select((location, index) => new { location, index })
                .OrderBy(item => GetRelevanceRank(item.location))
                .ThenBy(item => item.index)
                .Select(item => item.location)
                .ToList()
        };
    }

    private const int RelevanceRankOpen = 0;
    private const int RelevanceRankStudentOnlyClosed = 1;
    private const int RelevanceRankClosed = 2;

    private static int GetRelevanceRank(UiLocation location)
    {
        if (location.IsOpen)
        {
            return RelevanceRankOpen;
        }

        if (location.IsStudentOnlyClosed)
        {
            return RelevanceRankStudentOnlyClosed;
        }

        return RelevanceRankClosed;
    }

    private void ReplaceUiLocations(IEnumerable<UiLocation> locations)
    {
        var updatedLocations = locations as IList<UiLocation> ?? locations.ToList();
        UiLocations = new ObservableCollection<UiLocation>(updatedLocations);
        FilteredLocationCount = updatedLocations.Count;
        NotifyCollectionVisibilityChanged();
    }

    private void NotifyCollectionVisibilityChanged()
    {
        OnPropertyChanged(nameof(IsDataVisible));
        OnPropertyChanged(nameof(IsListEmpty));
        OnPropertyChanged(nameof(IsHomeEmpty));
        OnPropertyChanged(nameof(IsFavoritesEmpty));
        OnPropertyChanged(nameof(IsNoResultsEmpty));
        OnPropertyChanged(nameof(EmptyStateTitle));
        OnPropertyChanged(nameof(EmptyStateSubtitle));
        OnPropertyChanged(nameof(IsEmptySubtitleVisible));
    }

    private string GetBuildingGroupKey(StudySpace space)
    {
        if (BadischeLandesbibliothekIds.Contains(space.Id))
        {
            return BadischeLandesbibliothekKey;
        }

        return space.Building ?? string.Empty;
    }

    private List<UiLocation> MapSpacesToUiLocations(IEnumerable<StudySpace> spaces)
    {
        var referenceTime = GetReferenceDateTime();
        var favoriteNames = GetFavoriteNames();
        var results = new List<UiLocation>();
        var buildingGroups = spaces.GroupBy(GetBuildingGroupKey);

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

    private UiLocation CreateSingleLocation(StudySpace space, List<string> favoriteNames, DateTime referenceTime)
    {
        var insight = BuildArrivalInsight(space.SafeArrivalRecommendation);
        var series = GetChartSeriesForSpace(space.Id);
        var isMensa = string.Equals(space.Id, MensaVirtualSpaceId, StringComparison.OrdinalIgnoreCase);
        var mensaOccupancyRate = isMensa && space.TotalSeats > 0
            ? Math.Clamp((double)space.OccupiedSeats / space.TotalSeats, 0, 1)
            : 0;
        var mensaOpeningStart = isMensa ? _mensaForecastCache?.WindowStart : null;
        var mensaOpeningEnd = isMensa ? _mensaForecastCache?.OpeningHoursEnd : null;
        var subtitle = isMensa ? "Campus-Radar" : AppText.SingleLocationSubtitle;

        return new UiLocation
        {
            Name = space.Name,
            TileName = space.DisplayName,
            Subtitle = subtitle,
            BuildingNumber = space.Building,
            TotalSeats = space.TotalSeats,
            FreeSeats = space.FreeSeats,
            OccupiedSeats = space.OccupiedSeats,
            IsManualCount = space.IsManualCount,
            SubSpaces = [space],
            IsFavorite = favoriteNames.Contains(space.Name),
            ReferenceTime = referenceTime,
            BestArrivalText = insight.RecommendedArrivalText,
            HasArrivalInsights = insight.HasInsights,
            PeakAverageText = insight.PeakAverageText,
            SafetyLevelText = insight.SafetyLevelText,
            PeakTrendText = insight.PeakTrendText,
            OccupancySeries = series,
            IsMensaVirtual = isMensa,
            MensaOccupancyRate = mensaOccupancyRate,
            MensaOpeningStart = mensaOpeningStart,
            MensaOpeningEnd = mensaOpeningEnd
        };
    }

    private UiLocation CreateGroupedLocation(string buildingKey, List<StudySpace> spaces, List<string> favoriteNames, DateTime referenceTime)
    {
        var normalizedBuildingKey = buildingKey.Trim();
        var isBadischeLandesbibliothek = string.Equals(normalizedBuildingKey, BadischeLandesbibliothekKey, StringComparison.OrdinalIgnoreCase);
        var displayName = _config.BuildingNames.TryGetValue(normalizedBuildingKey, out var mappedName)
            ? mappedName
            : isBadischeLandesbibliothek
                ? "Badische Landesbibliothek"
                : string.Format(CultureInfo.CurrentCulture, _config.UiText.BuildingFormat, normalizedBuildingKey);

        var buildingRecommendation = GetCachedBuildingRecommendation(normalizedBuildingKey);
        var series = GetChartSeriesForBuilding(normalizedBuildingKey);

        var insight = BuildArrivalInsight(buildingRecommendation);

        return new UiLocation
        {
            Name = displayName,
            TileName = displayName,
            Subtitle = string.Format(CultureInfo.CurrentCulture, _config.UiText.GroupedLocationSubtitleFormat, spaces.Count),
            BuildingNumber = isBadischeLandesbibliothek ? null : normalizedBuildingKey,
            BuildingDisplayOverride = isBadischeLandesbibliothek ? displayName : null,
            TotalSeats = spaces.Sum(space => space.TotalSeats),
            FreeSeats = spaces.Sum(space => space.FreeSeats),
            OccupiedSeats = spaces.Sum(space => space.OccupiedSeats),
            IsManualCount = spaces.Any(space => space.IsManualCount),
            SubSpaces = spaces,
            IsFavorite = favoriteNames.Contains(displayName),
            ReferenceTime = referenceTime,
            BestArrivalText = insight.RecommendedArrivalText,
            HasArrivalInsights = insight.HasInsights,
            PeakAverageText = insight.PeakAverageText,
            SafetyLevelText = insight.SafetyLevelText,
            PeakTrendText = insight.PeakTrendText,
            OccupancySeries = series
        };
    }

    private SafeArrivalRecommendation? GetCachedBuildingRecommendation(string buildingKey)
    {
        if (string.IsNullOrWhiteSpace(buildingKey))
        {
            return null;
        }

        return _buildingSafeArrivalCache.TryGetValue(buildingKey, out var recommendation)
            ? recommendation
            : null;
    }

    private IReadOnlyList<float> GetChartSeriesForSpace(string spaceId)
    {
        return _spaceChartSeriesCache.TryGetValue(spaceId, out var series)
            ? series
            : Array.Empty<float>();
    }

    private IReadOnlyList<float> GetChartSeriesForBuilding(string buildingKey)
    {
        return _buildingChartSeriesCache.TryGetValue(buildingKey, out var series)
            ? series
            : Array.Empty<float>();
    }

    private SafeArrivalRecommendation? CalculateBuildingRecommendation(List<StudySpace> spaces)
    {
        if (spaces.Count == 0)
        {
            return null;
        }

        var totalCapacity = spaces.Sum(space => Math.Max(space.TotalSeats, 0));
        if (totalCapacity <= 0)
        {
            return null;
        }

        var aggregatedHistory = spaces
            .SelectMany(space =>
            {
                return _historicalSeatDataByLocation.TryGetValue(space.Id, out var history)
                    ? history
                    : [];
            })
            .GroupBy(point => NormalizeToBin(point.Timestamp))
            .Select(group =>
            {
                var sumFreeSeats = group.Sum(point => point.FreeSeats);
                var clampedFreeSeats = Math.Clamp(sumFreeSeats, 0, totalCapacity);
                return new SeatHistoryPoint
                {
                    Timestamp = group.Key,
                    FreeSeats = clampedFreeSeats,
                    OccupiedSeats = Math.Max(0, totalCapacity - clampedFreeSeats),
                    IsManualCount = false
                };
            })
            .OrderByDescending(point => point.Timestamp)
            .ToList();

        if (aggregatedHistory.Count == 0)
        {
            return null;
        }

        var representativeOpeningHours = spaces
            .Select(space => space.OpeningHours)
            .FirstOrDefault(openingHours => openingHours != null);

        var buildingSpace = new StudySpace
        {
            Id = $"{_config.Internal.BuildingAggregateIdPrefix}{string.Join(_config.Internal.BuildingAggregateIdSeparator, spaces.Select(space => space.Id).OrderBy(id => id))}",
            Name = _config.Internal.BuildingAggregateName,
            TotalSeats = totalCapacity,
            Building = spaces.FirstOrDefault()?.Building,
            OpeningHours = representativeOpeningHours,
            ReferenceTime = _safeArrivalReferenceDate
        };

        return _safeArrivalForecastService.Calculate(buildingSpace, aggregatedHistory, _safeArrivalReferenceDate);
    }

    private DateTime NormalizeToBin(DateTime timestamp)
    {
        var binMinutes = Math.Max(1, _config.SafeArrival.BinMinutes);
        var minutes = (timestamp.Hour * 60) + timestamp.Minute;
        var normalizedMinutes = (minutes / binMinutes) * binMinutes;
        return timestamp.Date.AddMinutes(normalizedMinutes);
    }

    private static ArrivalInsight BuildArrivalInsight(SafeArrivalRecommendation? recommendation)
    {
        if (recommendation == null)
        {
            return new ArrivalInsight(
                RecommendedArrivalText: AppText.RecommendationNoneText,
                HasInsights: false,
                PeakAverageText: AppText.PeakNoneText,
                SafetyLevelText: string.Format(CultureInfo.CurrentCulture, AppText.QualityFormat, AppText.QualityLow),
                PeakTrendText: string.Format(CultureInfo.CurrentCulture, AppText.PeakTrendFormat, AppText.PeakTrendFlat));
        }

        var recommendedArrivalText = recommendation.HasRecommendation
            ? string.Format(CultureInfo.CurrentCulture, AppText.RecommendationFormat, recommendation.LatestSafeTime)
            : AppText.RecommendationNoneText;

        var hasInsights = recommendation.HasPeakData;
        var peakAverageText = recommendation.HasPeakData
            ? string.Format(CultureInfo.CurrentCulture, AppText.PeakFormat, recommendation.PeakTime)
            : AppText.PeakNoneText;
        var safetyLevelText = string.Format(CultureInfo.CurrentCulture, AppText.QualityFormat, GetSafetyLevel(recommendation));
        var peakTrendText = string.Format(CultureInfo.CurrentCulture, AppText.PeakTrendFormat, GetPeakTrendLabel(recommendation.PeakTrendMinutesPerDay));

        return new ArrivalInsight(
            RecommendedArrivalText: recommendedArrivalText,
            HasInsights: hasInsights,
            PeakAverageText: peakAverageText,
            SafetyLevelText: safetyLevelText,
            PeakTrendText: peakTrendText);
    }

    private static string GetSafetyLevel(SafeArrivalRecommendation recommendation)
    {
        if (!recommendation.HasRecommendation)
        {
            return AppText.QualityLow;
        }

        if (recommendation.ConfidenceFlag &&
            recommendation.Probability >= AppConfigProvider.Current.SafeArrival.HighProbabilityThreshold)
        {
            return AppText.QualityHigh;
        }

        if (recommendation.Probability >= AppConfigProvider.Current.SafeArrival.MediumProbabilityThreshold)
        {
            return AppText.QualityMedium;
        }

        return AppText.QualityLow;
    }

    private static string GetPeakTrendLabel(double trendMinutesPerDay)
    {
        var flatThreshold = AppConfigProvider.Current.SafeArrival.TrendFlatThresholdMinutes;
        if (trendMinutesPerDay > flatThreshold)
        {
            return AppText.PeakTrendLater;
        }

        if (trendMinutesPerDay < -flatThreshold)
        {
            return AppText.PeakTrendEarlier;
        }

        return AppText.PeakTrendFlat;
    }

    private readonly record struct ArrivalInsight(
        string RecommendedArrivalText,
        bool HasInsights,
        string PeakAverageText,
        string SafetyLevelText,
        string PeakTrendText);

    private DateTime GetReferenceDateTime()
    {
        return UseNow ? DateTime.Now : SelectedDate.Date + SelectedTime;
    }


    private string GetApiBeforeParameter()
    {
        return UseNow
            ? _config.SeatFinder.NowToken
            : GetReferenceDateTime().ToString(_config.Internal.ApiDateTimeFormat, CultureInfo.InvariantCulture);
    }

    private (string After, string Before) GetWeeklyHistoryWindow()
    {
        var now = DateTime.Now;
        var start = now.Date.AddDays(-7);
        return (
            start.ToString(_config.Internal.ApiDateTimeFormat, CultureInfo.InvariantCulture),
            now.ToString(_config.Internal.ApiDateTimeFormat, CultureInfo.InvariantCulture));
    }

    private bool HasInternetAccess()
    {
        return Connectivity.Current.NetworkAccess == NetworkAccess.Internet;
    }

    private void PerformFavoriteHaptic()
    {
        if (!IsHapticFeedbackEnabled)
        {
            return;
        }

        try
        {
            if (HapticFeedback.Default.IsSupported)
            {
                HapticFeedback.Default.Perform(HapticFeedbackType.Click);
                return;
            }

            if (Vibration.Default.IsSupported)
            {
                Vibration.Default.Vibrate(TimeSpan.FromMilliseconds(20));
            }
        }
        catch (Exception)
        {
        }
    }

    private async Task ShowOfflineBannerAsync()
    {
        _offlineBannerCts?.Cancel();
        _offlineBannerCts?.Dispose();
        var cts = new CancellationTokenSource();
        _offlineBannerCts = cts;

        IsOfflineBannerVisible = true;

        try
        {
            await Task.Delay(2500, cts.Token);
        }
        catch (TaskCanceledException)
        {
            return;
        }

        if (!cts.IsCancellationRequested)
        {
            IsOfflineBannerVisible = false;
        }
    }

    [RelayCommand]
    private async Task GoToDetailAsync(UiLocation selectedLocation)
    {
        if (selectedLocation == null)
        {
            return;
        }

        await Shell.Current.GoToAsync(
            _config.Internal.DetailPageRoute,
            new Dictionary<string, object> { { _config.Internal.LocationDataKey, selectedLocation } });
    }

    private string NormalizeTab(string tabName)
    {
        if (string.Equals(tabName, _config.Tabs.Home, StringComparison.Ordinal))
        {
            return _config.Tabs.Home;
        }

        if (string.Equals(tabName, _config.Tabs.Favorites, StringComparison.Ordinal))
        {
            return _config.Tabs.Favorites;
        }

        if (string.Equals(tabName, _config.Tabs.Settings, StringComparison.Ordinal))
        {
            return _config.Tabs.Settings;
        }

        return _config.Tabs.Home;
    }

    private string ResolveInitialTab(string tabName)
    {
        var normalizedTab = NormalizeTab(tabName);
        return string.Equals(normalizedTab, _config.Tabs.Settings, StringComparison.Ordinal)
            ? _config.Tabs.Home
            : normalizedTab;
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
