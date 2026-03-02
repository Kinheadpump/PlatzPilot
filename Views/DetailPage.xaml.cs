using System.Collections.Generic;
using System.Windows.Input;
using System.Globalization; // WICHTIG für die Formatierung der Koordinaten!
using System.Text.RegularExpressions;
using Microsoft.Maui.ApplicationModel;
using PlatzPilot.Configuration;
using PlatzPilot.Models;

namespace PlatzPilot.Views;

public partial class DetailPage : ContentPage, IQueryAttributable
{
    private UiLocation? _locationData;
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
        }
    }

    public List<StudySpace> SortedSubSpaces { get; private set; } = [];

    public ICommand OpenUrlCommand { get; }
    public ICommand OpenMapCommand { get; }
    public ICommand GoBackCommand { get; }

    public DetailPage()
    {
        InitializeComponent();

        OpenUrlCommand = new Command<string>(async url => await OpenUrlAsync(url));
        OpenMapCommand = new Command(async () => await OpenMapAsync());
        GoBackCommand = new Command(async () => await Shell.Current.GoToAsync(AppConfigProvider.Current.Internal.BackNavigationRoute));

        BindingContext = this;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        var key = AppConfigProvider.Current.Internal.LocationDataKey;
        if (query.TryGetValue(key, out var value) && value is UiLocation location)
        {
            LocationData = location;
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
}
