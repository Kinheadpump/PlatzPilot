using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PlatzPilot.Configuration;

public static class AppConfigProvider
{
    private const string DefaultConfigFileName = "app_config.json";
    private static readonly object _syncObj = new();
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

    public static AppConfig Current { get; } = new();
    public static bool IsLoaded { get; private set; }

    public static AppConfig LoadFromEmbeddedResource(string? resourceName = null)
    {
        try
        {
            lock (_syncObj)
            {
                if (IsLoaded)
                {
                    return Current;
                }

                var assembly = typeof(AppConfigProvider).Assembly;
                var resolvedResourceName = string.IsNullOrWhiteSpace(resourceName)
                    ? assembly.GetManifestResourceNames()
                        .FirstOrDefault(name => name.EndsWith(DefaultConfigFileName, StringComparison.OrdinalIgnoreCase))
                    : resourceName;

                if (string.IsNullOrWhiteSpace(resolvedResourceName))
                {
                    return Current;
                }

                using var stream = assembly.GetManifestResourceStream(resolvedResourceName);
                if (stream == null)
                {
                    return Current;
                }

                var loaded = JsonSerializer.Deserialize<AppConfig>(stream, SerializerOptions);
                if (loaded != null)
                {
                    ApplyLoadedConfig(loaded);
                    IsLoaded = true;
                }
            }
        }
        catch (Exception ex)
        {
            var format = Current.Internal.ConfigLoadFailedFormat;
            if (string.IsNullOrWhiteSpace(format))
            {
                Debug.WriteLine(ex.Message);
                return Current;
            }

            Debug.WriteLine(string.Format(
                CultureInfo.CurrentCulture,
                format,
                DefaultConfigFileName,
                ex.Message));
        }

        return Current;
    }

    public static async Task<AppConfig> LoadFromPackageAsync(string? fileName = null, CancellationToken cancellationToken = default)
    {
        var resolvedFileName = string.IsNullOrWhiteSpace(fileName)
            ? DefaultConfigFileName
            : fileName;

        try
        {
            lock (_syncObj)
            {
                if (IsLoaded)
                {
                    return Current;
                }
            }

            await using var stream = await FileSystem.OpenAppPackageFileAsync(resolvedFileName)
                .ConfigureAwait(false);
            var loaded = await JsonSerializer.DeserializeAsync<AppConfig>(
                    stream,
                    SerializerOptions,
                    cancellationToken)
                .ConfigureAwait(false);

            lock (_syncObj)
            {
                if (IsLoaded)
                {
                    return Current;
                }

                if (loaded != null)
                {
                    ApplyLoadedConfig(loaded);
                    IsLoaded = true;
                }
            }
        }
        catch (Exception ex)
        {
            var format = Current.Internal.ConfigLoadFailedFormat;
            if (string.IsNullOrWhiteSpace(format))
            {
                Debug.WriteLine(ex.Message);
                return Current;
            }

            Debug.WriteLine(string.Format(
                CultureInfo.CurrentCulture,
                format,
                resolvedFileName,
                ex.Message));
        }

        return Current;
    }

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
        options.Converters.Add(new TimeSpanJsonConverter());
        return options;
    }

    private static void ApplyLoadedConfig(AppConfig loaded)
    {
        Current.UiNumbers = loaded.UiNumbers ?? new UiNumbersConfig();
        Current.Preferences = loaded.Preferences ?? new PreferencesConfig();
        Current.Urls = loaded.Urls ?? new UrlConfig();
        Current.SeatFinder = loaded.SeatFinder ?? new SeatFinderConfig();
        Current.SafeArrival = loaded.SafeArrival ?? new SafeArrivalConfig();
        Current.MensaForecast = loaded.MensaForecast ?? new MensaForecastConfig();
        Current.StudentAccess = loaded.StudentAccess ?? new StudentAccessConfig();
        Current.OpeningHours = loaded.OpeningHours ?? new OpeningHoursSettings();
        Current.RoomTypes = loaded.RoomTypes ?? new RoomTypeConfig();
        Current.Sort = loaded.Sort ?? new SortConfig();
        Current.Tabs = loaded.Tabs ?? new TabConfig();
        Current.Theme = loaded.Theme ?? new ThemeConfig();
        Current.Fonts = loaded.Fonts ?? new FontsConfig();
        Current.AppInfo = loaded.AppInfo ?? new AppInfoConfig();
        Current.Occupancy = loaded.Occupancy ?? new OccupancyConfig();
        Current.ColorBlindness = loaded.ColorBlindness ?? new ColorBlindnessConfig();
        Current.CampusSouth = loaded.CampusSouth ?? new CampusSouthConfig();
        Current.UiColors = loaded.UiColors ?? new UiColorsConfig();
        Current.UiAssets = loaded.UiAssets ?? new UiAssetsConfig();
        Current.Charts = loaded.Charts ?? new ChartConfig();
        Current.Internal = loaded.Internal ?? new InternalConfig();
        Current.BuildingNames = loaded.BuildingNames != null
            ? new Dictionary<string, string>(loaded.BuildingNames, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }
}
