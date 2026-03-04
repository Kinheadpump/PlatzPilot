using System.Collections.ObjectModel;
using System.ComponentModel;
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

public partial class SeatListViewModel : ObservableObject
{
    private readonly AppConfig _config;
    private readonly SeatFinderService _seatFinderService;
    private readonly SafeArrivalForecastService _safeArrivalForecastService;
    private readonly IStudySpaceFeatureService _studySpaceFeatureService;
    private readonly INavigationService _navigationService;
    private readonly MensaForecastService _mensaForecastService;
    private readonly FilterViewModel _filters;
    private readonly NavigationViewModel _navigation;
    private readonly SettingsViewModel _settings;

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
    private bool _isOfflineBannerVisible;

    [ObservableProperty]
    private SafeArrivalRecommendation? _mensaSafeArrivalRecommendation;

    [ObservableProperty]
    private string _mensaFluxLabel = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowResultsButtonText))]
    private int _filteredLocationCount;

    private IReadOnlyDictionary<string, StudySpaceFeatureEntry> _spaceFeaturesById =
        new Dictionary<string, StudySpaceFeatureEntry>(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyDictionary<string, List<SeatHistoryPoint>> _historicalSeatDataByLocation =
        new Dictionary<string, List<SeatHistoryPoint>>(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, SafeArrivalRecommendation?> _spaceSafeArrivalCache = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, SafeArrivalRecommendation?> _buildingSafeArrivalCache = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, List<float>> _spaceChartSeriesCache = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, List<float>> _buildingChartSeriesCache = new(StringComparer.OrdinalIgnoreCase);
    private MensaForecastResult? _mensaForecastCache;
    private List<StudySpace> _allSpaces = [];
    private bool _spaceFeaturesLoaded;
    private bool _hasLoadedWeeklyHistory;
    private bool _hasComputedSafeArrival;
    private bool _hasComputedChartSeries;
    private DateTime _chartReferenceTime = DateTime.MinValue;
    private DateTime _safeArrivalReferenceDate = DateTime.MinValue;
    private string _lastLoadedBeforeParameter = string.Empty;
    private DateTime _lastLiveSnapshotFetchUtc = DateTime.MinValue;
    private CancellationTokenSource? _offlineBannerCts;
    private static readonly HashSet<string> BadischeLandesbibliothekIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "BLB",
        "WIS"
    };
    private const string BadischeLandesbibliothekKey = "BLB_WIS";
    public bool IsDataVisible => !IsBusy && UiLocations.Count > 0 && _navigation.IsMainContentVisible;
    public bool IsListEmpty => UiLocations.Count == 0 && _navigation.IsMainContentVisible;
    public bool IsHomeEmpty => UiLocations.Count == 0 && _navigation.CurrentTab == _config.Tabs.Home;
    public bool IsFavoritesEmpty => !IsBusy && UiLocations.Count == 0 && _navigation.CurrentTab == _config.Tabs.Favorites;
    public bool IsNoResultsEmpty => UiLocations.Count == 0 && _allSpaces.Count > 0 && _navigation.CurrentTab == _config.Tabs.Home && _navigation.IsMainContentVisible;
    public string EmptyStateTitle => IsNoResultsEmpty ? _config.UiText.NoResultsTitle : SeatListViewModel.WelcomeMessage;
    public string EmptyStateSubtitle => IsNoResultsEmpty ? _config.UiText.NoResultsSubtitle : string.Empty;
    public bool IsEmptySubtitleVisible => IsNoResultsEmpty;
    public string ShowResultsButtonText =>
        string.Format(CultureInfo.CurrentCulture, _config.UiText.ShowResultsFormat, FilteredLocationCount);
    public static string WelcomeMessage => GetWelcomeMessage();

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

    public SeatListViewModel(
        SeatFinderService seatFinderService,
        SafeArrivalForecastService safeArrivalForecastService,
        IStudySpaceFeatureService studySpaceFeatureService,
        INavigationService navigationService,
        MensaForecastService mensaForecastService,
        FilterViewModel filters,
        NavigationViewModel navigation,
        SettingsViewModel settings,
        AppConfig config)
    {
        _config = config;
        _seatFinderService = seatFinderService;
        _safeArrivalForecastService = safeArrivalForecastService;
        _studySpaceFeatureService = studySpaceFeatureService;
        _navigationService = navigationService;
        _mensaForecastService = mensaForecastService;
        _filters = filters;
        _navigation = navigation;
        _settings = settings;

        _filters.FiltersChanged += OnFiltersChanged;
        _navigation.PropertyChanged += OnNavigationPropertyChanged;
        _settings.PropertyChanged += OnSettingsPropertyChanged;

        FilteredLocationCount = 0;
    }

    [RelayCommand]
    private async Task ResetFiltersAsync()
    {
        var hadActiveFilters = _filters.IsAnyFilterActive();

        _filters.ResetToDefaults();
        UpdateFilteredLocationPreviewCount();

        if (!hadActiveFilters)
        {
            return;
        }

        var requestedBeforeParameter = _filters.GetApiBeforeParameter();
        if (ShouldRefreshSnapshot(requestedBeforeParameter))
        {
            await LoadSpacesAsync();
        }
        else
        {
            ApplyFilter();
        }
    }

    [RelayCommand]
    private async Task ApplySheetFiltersAsync()
    {
        var requestedBeforeParameter = _filters.GetApiBeforeParameter();
        var shouldRefreshData = ShouldRefreshSnapshot(requestedBeforeParameter);

        if (shouldRefreshData)
        {
            await LoadSpacesAsync();
        }
        else
        {
            ApplyFilter();
        }

        _filters.IsFilterExpanded = false;
    }

    private void OnFiltersChanged(object? sender, FilterChangedEventArgs e)
    {
        if (IsBusy)
        {
            return;
        }

        if (e.ChangeKind == FilterChangeKind.ImmediateApply)
        {
            ApplyFilter();
            return;
        }

        UpdateFilteredLocationPreviewCount();
    }

    private void OnNavigationPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(NavigationViewModel.CurrentTab))
        {
            return;
        }

        if (!string.Equals(_navigation.CurrentTab, _config.Tabs.Home, StringComparison.Ordinal))
        {
            _filters.IsSearchActive = false;
            _filters.IsFilterExpanded = false;
        }

        if (_navigation.IsMainContentVisible)
        {
            ApplyFilter();
        }

        NotifyCollectionVisibilityChanged();
    }

    private void OnSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(SettingsViewModel.IsColorBlindMode) &&
            e.PropertyName != nameof(SettingsViewModel.IsCampusSouthOnly) &&
            e.PropertyName != nameof(SettingsViewModel.IsHideClosedLocations))
        {
            return;
        }

        ApplyFilter();
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

        if (_navigation.CurrentTab == _config.Tabs.Favorites && !location.IsFavorite)
        {
            UiLocations.Remove(location);
            NotifyCollectionVisibilityChanged();
            FilteredLocationCount = UiLocations.Count;
        }
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
            var requestedBeforeParameter = _filters.GetApiBeforeParameter();
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
            var updatedHistory = _historicalSeatDataByLocation;
            var spaceSafeArrivalCache = _spaceSafeArrivalCache;
            var buildingSafeArrivalCache = _buildingSafeArrivalCache;
            var spaceChartSeriesCache = _spaceChartSeriesCache;
            var buildingChartSeriesCache = _buildingChartSeriesCache;
            var safeArrivalReferenceDate = _safeArrivalReferenceDate;
            var chartReferenceTime = _chartReferenceTime;
            var hasLoadedWeeklyHistory = _hasLoadedWeeklyHistory;
            var hasComputedSafeArrival = _hasComputedSafeArrival;
            var hasComputedChartSeries = _hasComputedChartSeries;

            await Task.Run(() =>
            {
                if (shouldReloadWeeklyHistory)
                {
                    updatedHistory = BuildHistoricalSeatData(_allSpaces);
                    hasLoadedWeeklyHistory = true;
                }
                else
                {
                    updatedHistory = AppendLatestSeatDataToHistory(_historicalSeatDataByLocation, _allSpaces);
                }

                if (shouldReloadWeeklyHistory || !hasComputedSafeArrival)
                {
                    var safeArrivalResult = BuildSafeArrivalCaches(updatedHistory);
                    spaceSafeArrivalCache = safeArrivalResult.SpaceCache;
                    buildingSafeArrivalCache = safeArrivalResult.BuildingCache;
                    safeArrivalReferenceDate = safeArrivalResult.ReferenceDate;
                    hasComputedSafeArrival = true;
                }
                else
                {
                    ApplyCachedSafeArrivalRecommendations(spaceSafeArrivalCache);
                }

                if (shouldReloadWeeklyHistory || !hasComputedChartSeries)
                {
                    var chartSeriesResult = BuildChartSeriesCaches(updatedHistory);
                    spaceChartSeriesCache = chartSeriesResult.SpaceCache;
                    buildingChartSeriesCache = chartSeriesResult.BuildingCache;
                    chartReferenceTime = chartSeriesResult.ReferenceTime;
                    hasComputedChartSeries = true;
                }

                var forecastReferenceDate = safeArrivalReferenceDate == DateTime.MinValue
                    ? DateTime.Today
                    : safeArrivalReferenceDate;
                mensaResult = _mensaForecastService.BuildForecast(
                    _allSpaces,
                    updatedHistory,
                    forecastReferenceDate,
                    _filters.GetReferenceDateTime(),
                    DateTime.Now);
            });

            _historicalSeatDataByLocation = updatedHistory;
            _spaceSafeArrivalCache = spaceSafeArrivalCache;
            _buildingSafeArrivalCache = buildingSafeArrivalCache;
            _spaceChartSeriesCache = spaceChartSeriesCache;
            _buildingChartSeriesCache = buildingChartSeriesCache;
            _safeArrivalReferenceDate = safeArrivalReferenceDate;
            _chartReferenceTime = chartReferenceTime;
            _hasLoadedWeeklyHistory = hasLoadedWeeklyHistory;
            _hasComputedSafeArrival = hasComputedSafeArrival;
            _hasComputedChartSeries = hasComputedChartSeries;

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

    partial void OnSelectedLocationChanged(UiLocation? value)
    {
        if (value == null)
        {
            return;
        }

        MainThread.BeginInvokeOnMainThread(async () => await GoToDetailAsync(value));
        SelectedLocation = null;
    }

    private async Task EnsureSpaceFeaturesLoadedAsync()
    {
        if (_spaceFeaturesLoaded)
        {
            return;
        }

        _spaceFeaturesLoaded = true;
        _spaceFeaturesById = await _studySpaceFeatureService.LoadAsync();
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

    private IReadOnlyDictionary<string, List<SeatHistoryPoint>> BuildHistoricalSeatData(IEnumerable<StudySpace> spaces)
    {
        var historyByLocation = new Dictionary<string, List<SeatHistoryPoint>>(StringComparer.OrdinalIgnoreCase);

        foreach (var space in spaces)
        {
            if (space.SeatHistory.Count == 0)
            {
                continue;
            }

            var history = space.SeatHistory
                .OrderByDescending(point => point.Timestamp)
                .ToList();

            if (history.Count > _config.SeatFinder.WeeklyHistoryPoints)
            {
                history.RemoveRange(_config.SeatFinder.WeeklyHistoryPoints, history.Count - _config.SeatFinder.WeeklyHistoryPoints);
            }

            historyByLocation[space.Id] = history;
        }

        return historyByLocation;
    }

    private IReadOnlyDictionary<string, List<SeatHistoryPoint>> AppendLatestSeatDataToHistory(
        IReadOnlyDictionary<string, List<SeatHistoryPoint>> existingHistory,
        IEnumerable<StudySpace> spaces)
    {
        var historyByLocation = new Dictionary<string, List<SeatHistoryPoint>>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in existingHistory)
        {
            historyByLocation[entry.Key] = entry.Value.ToList();
        }

        foreach (var space in spaces)
        {
            if (space.SeatHistory.Count == 0)
            {
                continue;
            }

            if (!historyByLocation.TryGetValue(space.Id, out var history))
            {
                history = [];
                historyByLocation[space.Id] = history;
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

        return historyByLocation;
    }

    private SafeArrivalCacheResult BuildSafeArrivalCaches(IReadOnlyDictionary<string, List<SeatHistoryPoint>> historyByLocation)
    {
        var referenceDate = DateTime.Today;
        var spaceCache = new Dictionary<string, SafeArrivalRecommendation?>(StringComparer.OrdinalIgnoreCase);
        var buildingCache = new Dictionary<string, SafeArrivalRecommendation?>(StringComparer.OrdinalIgnoreCase);

        foreach (var space in _allSpaces)
        {
            if (!historyByLocation.TryGetValue(space.Id, out var history) || history.Count == 0)
            {
                space.SafeArrivalRecommendation = null;
                continue;
            }

            space.SafeArrivalRecommendation = _safeArrivalForecastService.Calculate(space, history, referenceDate);
            spaceCache[space.Id] = space.SafeArrivalRecommendation;
        }

        foreach (var group in _allSpaces.GroupBy(GetBuildingGroupKey))
        {
            var buildingKey = group.Key?.Trim();
            var spacesInBuilding = group.ToList();

            if (string.IsNullOrWhiteSpace(buildingKey) || spacesInBuilding.Count <= 1)
            {
                continue;
            }

            buildingCache[buildingKey] = CalculateBuildingRecommendation(spacesInBuilding, historyByLocation, referenceDate);
        }

        return new SafeArrivalCacheResult(referenceDate, spaceCache, buildingCache);
    }

    private void ApplyCachedSafeArrivalRecommendations(IReadOnlyDictionary<string, SafeArrivalRecommendation?> cache)
    {
        if (cache.Count == 0)
        {
            return;
        }

        foreach (var space in _allSpaces)
        {
            if (cache.TryGetValue(space.Id, out var recommendation))
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

        _spaceChartSeriesCache.Remove(MensaForecastService.MensaVirtualSpaceId);
        if (result?.ChartSeries != null && result.ChartSeries.Count > 0)
        {
            _spaceChartSeriesCache[MensaForecastService.MensaVirtualSpaceId] = result.ChartSeries.ToList();
        }
    }

    private ChartSeriesCacheResult BuildChartSeriesCaches(IReadOnlyDictionary<string, List<SeatHistoryPoint>> historyByLocation)
    {
        var spaceCache = new Dictionary<string, List<float>>(StringComparer.OrdinalIgnoreCase);
        var buildingCache = new Dictionary<string, List<float>>(StringComparer.OrdinalIgnoreCase);

        if (_allSpaces.Count == 0)
        {
            return new ChartSeriesCacheResult(DateTime.MinValue, spaceCache, buildingCache);
        }

        var chartConfig = _config.Charts;
        var binMinutes = Math.Max(1, chartConfig.BinMinutes);
        var endTime = NormalizeToChartBin(DateTime.Now, binMinutes);
        var startTime = endTime.AddHours(-chartConfig.HistoryHours);

        foreach (var space in _allSpaces)
        {
            if (!historyByLocation.TryGetValue(space.Id, out var history) || history.Count == 0)
            {
                continue;
            }

            var series = BuildOccupancySeries(history, startTime, endTime, binMinutes);
            if (series.Count > 0)
            {
                spaceCache[space.Id] = series;
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

            var series = BuildAggregateOccupancySeries(historyByLocation, spacesInBuilding, startTime, endTime, binMinutes);
            if (series.Count > 0)
            {
                buildingCache[buildingKey] = series;
            }
        }

        return new ChartSeriesCacheResult(endTime, spaceCache, buildingCache);
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
        IReadOnlyDictionary<string, List<SeatHistoryPoint>> historyByLocation,
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
            if (!historyByLocation.TryGetValue(space.Id, out var history) || history.Count == 0)
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

        if (_navigation.CurrentTab == _config.Tabs.Home && _settings.IsHideClosedLocations)
        {
            results = results
                .Where(location => location.IsOpen || location.IsStudentOnlyClosed)
                .ToList();
        }

        if (_navigation.CurrentTab == _config.Tabs.Favorites)
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
        var mensaSpace = _mensaForecastService.BuildVirtualSpace(_mensaForecastCache, _filters.GetReferenceDateTime());
        if (mensaSpace != null)
        {
            spaces.Add(mensaSpace);
        }

        return spaces;
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

        if (_navigation.CurrentTab == _config.Tabs.Home && _settings.IsHideClosedLocations)
        {
            results = results
                .Where(location => location.IsOpen || location.IsStudentOnlyClosed)
                .ToList();
        }

        if (_navigation.CurrentTab == _config.Tabs.Favorites)
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
        if (!_settings.IsCampusSouthOnly)
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
        if (string.IsNullOrWhiteSpace(_filters.SearchText))
        {
            return spaces;
        }

        var search = _filters.SearchText.Trim();
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
        return _filters.IsGroupRoomSelected || _filters.IsSilentStudySelected;
    }

    private HashSet<string> GetSelectedRoomTypes()
    {
        var selectedRoomTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (_filters.IsGroupRoomSelected)
        {
            selectedRoomTypes.Add(_config.RoomTypes.Group);
        }

        if (_filters.IsSilentStudySelected)
        {
            selectedRoomTypes.Add(_config.RoomTypes.SilentStudy);
            selectedRoomTypes.Add(_config.RoomTypes.SilentStudyLegacy);
        }

        return selectedRoomTypes;
    }

    private IEnumerable<StudySpace> FilterSpacesByReservation(IEnumerable<StudySpace> spaces)
    {
        if (!_filters.IsNoReservationSelected)
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
        if (!_filters.RequireFreeWifi && !_filters.RequirePowerOutlets && !_filters.RequireWhiteboard)
        {
            return spaces;
        }

        return spaces.Where(space =>
        {
            if (!_spaceFeaturesById.TryGetValue(space.Id, out var features))
            {
                return false;
            }

            if (_filters.RequireFreeWifi && !features.FreeWifi)
            {
                return false;
            }

            if (_filters.RequirePowerOutlets && !features.PowerOutlets)
            {
                return false;
            }

            if (_filters.RequireWhiteboard && !features.Whiteboard)
            {
                return false;
            }

            return true;
        });
    }

    private IEnumerable<StudySpace> FilterSpacesByOpeningHours(IEnumerable<StudySpace> spaces)
    {
        if (_filters.MinimumOpenHours <= _config.UiNumbers.MinOpeningHours)
        {
            return spaces;
        }

        var referenceTime = _filters.GetReferenceDateTime();
        var requiredUntil = referenceTime.AddHours(_filters.MinimumOpenHours);

        return spaces.Where(space =>
        {
            return space.OpeningHours?.IsOpenUntil(referenceTime, requiredUntil) ?? false;
        });
    }

    private List<UiLocation> SortLocations(List<UiLocation> locations)
    {
        return _filters.SelectedSortOption switch
        {
            var value when string.Equals(value, _config.Sort.MostFree, StringComparison.Ordinal) =>
                locations
                    .OrderBy(location => GetRelevanceRank(location))
                    .ThenByDescending(location => location.FreeSeats)
                    .ThenBy(location => location.Name)
                    .ToList(),
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
        var referenceTime = _filters.GetReferenceDateTime();
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
        var isMensa = string.Equals(space.Id, MensaForecastService.MensaVirtualSpaceId, StringComparison.OrdinalIgnoreCase);
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

    private SafeArrivalRecommendation? CalculateBuildingRecommendation(
        List<StudySpace> spaces,
        IReadOnlyDictionary<string, List<SeatHistoryPoint>> historyByLocation,
        DateTime referenceDate)
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
                return historyByLocation.TryGetValue(space.Id, out var history)
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
            ReferenceTime = referenceDate
        };

        return _safeArrivalForecastService.Calculate(buildingSpace, aggregatedHistory, referenceDate);
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

    private sealed record SafeArrivalCacheResult(
        DateTime ReferenceDate,
        Dictionary<string, SafeArrivalRecommendation?> SpaceCache,
        Dictionary<string, SafeArrivalRecommendation?> BuildingCache);

    private sealed record ChartSeriesCacheResult(
        DateTime ReferenceTime,
        Dictionary<string, List<float>> SpaceCache,
        Dictionary<string, List<float>> BuildingCache);

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
        if (!_settings.IsHapticFeedbackEnabled)
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

        await _navigationService.NavigateToDetailAsync(selectedLocation);
    }

}
