using System.Collections.Generic;
using System.Windows.Input;
using System.Globalization; // WICHTIG für die Formatierung der Koordinaten!
using System.Text.RegularExpressions;
using Microsoft.Maui.ApplicationModel;
using Microcharts;
using PlatzPilot.Configuration;
using PlatzPilot.Models;
using PlatzPilot.ViewModels;
using SkiaSharp;

namespace PlatzPilot.Views;

public partial class DetailPage : ContentPage, IQueryAttributable
{
    private UiLocation? _locationData;
    private readonly SeatListViewModel _seatList;
    private static readonly Lazy<Regex> LevelNumberRegex = new(() =>
        new Regex(AppConfigProvider.Current.Internal.LevelNumberRegex, RegexOptions.Compiled));

    public UiLocation? LocationData
    {
        get => _locationData;
        set
        {
            _locationData = value;
            SortedSubSpaces = SortByLevel(value?.SubSpaces);
            OnPropertyChanged();
            OnPropertyChanged(nameof(SortedSubSpaces));
            UpdateChart();
            OnPropertyChanged(nameof(OccupancyChart));
            OnPropertyChanged(nameof(HasChartData));
        }
    }

    public List<StudySpace> SortedSubSpaces { get; private set; } = [];

    public ICommand OpenUrlCommand { get; }
    public ICommand OpenMapCommand { get; }
    public ICommand GoBackCommand { get; }

    public Chart? OccupancyChart { get; private set; }
    public bool HasChartData => OccupancyChart != null;
    public double ChartHeight => AppConfigProvider.Current.Charts.Height;

    public DetailPage(SeatListViewModel seatList)
    {
        InitializeComponent();

        _seatList = seatList;
        OpenUrlCommand = new Command<string>(async url => await OpenUrlAsync(url));
        OpenMapCommand = new Command(async () => await OpenMapAsync());
        GoBackCommand = new Command(async () => await Shell.Current.GoToAsync(AppConfigProvider.Current.Internal.BackNavigationRoute));

        BindingContext = this;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        await _seatList.RefreshIfStaleAsync();
        TryUpdateLocationData();
    }

    protected override bool OnBackButtonPressed()
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await Shell.Current.GoToAsync(AppConfigProvider.Current.Internal.BackNavigationRoute);
        });

        return true;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        var key = AppConfigProvider.Current.Internal.LocationDataKey;
        if (query.TryGetValue(key, out var value) && value is UiLocation location)
        {
            LocationData = location;
            TryUpdateLocationData();
        }
    }

    private void TryUpdateLocationData()
    {
        if (LocationData == null)
        {
            return;
        }

        var updated = _seatList.FindMatchingLocation(LocationData);
        if (updated != null && !ReferenceEquals(updated, LocationData))
        {
            LocationData = updated;
        }
    }

    private async Task OpenUrlAsync(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        var normalizedUrl = NormalizeUrl(url);
        if (!Uri.TryCreate(normalizedUrl, UriKind.Absolute, out var uri))
        {
            return;
        }

        try
        {
            await Launcher.Default.OpenAsync(uri);
        }
        catch (Exception ex)
        {
            Console.WriteLine(string.Format(
                CultureInfo.CurrentCulture,
                AppConfigProvider.Current.Internal.UrlOpenFailedFormat,
                ex.Message));
        }
    }

    private static string NormalizeUrl(string url)
    {
        var config = AppConfigProvider.Current.Internal;
        if (url.StartsWith(config.UrlSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith(config.UrlSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return url;
        }

        return $"{config.UrlSchemeDefaultPrefix}{url}";
    }

    private async Task OpenMapAsync()
    {
        if (LocationData == null || !LocationData.HasLocation)
        {
            return;
        }

        try
        {
            var location = new Location(LocationData.Latitude, LocationData.Longitude);
            var options = new MapLaunchOptions { Name = LocationData.Name };
            await Map.Default.OpenAsync(location, options);
        }
        catch (Exception)
        {
            await OpenMapInBrowserAsync(LocationData);
        }
    }

    private static async Task OpenMapInBrowserAsync(UiLocation locationData)
    {
        try
        {
            var lat = locationData.Latitude.ToString(CultureInfo.InvariantCulture);
            var lon = locationData.Longitude.ToString(CultureInfo.InvariantCulture);
            var mapUrl = string.Format(
                CultureInfo.InvariantCulture,
                AppConfigProvider.Current.Internal.MapSearchUrlFormat,
                lat,
                lon);
            await Launcher.Default.OpenAsync(new Uri(mapUrl));
        }
        catch
        {
            // Letztes Sicherheitsnetz
        }
    }

    private static List<StudySpace> SortByLevel(IEnumerable<StudySpace>? spaces)
    {
        if (spaces == null)
        {
            return [];
        }

        return spaces
            .OrderBy(space => ExtractLevelOrder(space.Level))
            .ThenBy(space => space.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private static int ExtractLevelOrder(string? level)
    {
        if (string.IsNullOrWhiteSpace(level))
        {
            return int.MaxValue;
        }

        var match = LevelNumberRegex.Value.Match(level);
        if (!match.Success || !int.TryParse(match.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedLevel))
        {
            return int.MaxValue - 1;
        }

        return parsedLevel;
    }

    private void UpdateChart()
    {
        var location = LocationData;
        if (location == null || location.SubSpaces.Count == 0)
        {
            OccupancyChart = null;
            return;
        }

        var chartConfig = AppConfigProvider.Current.Charts;
        var series = location.OccupancySeries;
        var minPoints = Math.Max(2, chartConfig.MinSeriesPoints);
        if (series == null || series.Count < minPoints)
        {
            OccupancyChart = null;
            return;
        }

        var isDarkTheme = Application.Current?.RequestedTheme == AppTheme.Dark;
        var lineColor = SKColor.Parse(isDarkTheme ? chartConfig.LineColorDark : chartConfig.LineColorLight);
        var backgroundColor = SKColor.Parse(isDarkTheme ? chartConfig.BackgroundColorDark : chartConfig.BackgroundColorLight);

        var entries = series
            .Select(value => new ChartEntry(value)
            {
                Label = string.Empty,
                ValueLabel = string.Empty,
                Color = lineColor
            })
            .ToList();

        OccupancyChart = new LineChart
        {
            Entries = entries,
            LineMode = LineMode.Spline,
            LineSize = chartConfig.LineSize,
            LineAreaAlpha = chartConfig.LineAreaAlpha,
            MinValue = chartConfig.MinValue,
            MaxValue = chartConfig.MaxValue,
            LabelTextSize = chartConfig.LabelTextSize,
            ValueLabelTextSize = chartConfig.ValueLabelTextSize,
            SerieLabelTextSize = chartConfig.SerieLabelTextSize,
            ShowYAxisLines = chartConfig.ShowYAxisLines,
            ShowYAxisText = chartConfig.ShowYAxisText,
            ValueLabelOption = ValueLabelOption.None,
            BackgroundColor = backgroundColor
        };
    }

}
