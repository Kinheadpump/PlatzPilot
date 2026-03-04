using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PlatzPilot.Configuration;

public static class AppConfigProvider
{
    private const string DefaultConfigFileName = "app_config.json";
    private static readonly SemaphoreSlim LoadSemaphore = new(1, 1);
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

    public static AppConfig Current { get; } = new();
    public static bool IsLoaded { get; private set; }

    public static async Task<AppConfig> LoadFromPackageAsync(string? fileName = null, CancellationToken cancellationToken = default)
    {
        var resolvedFileName = string.IsNullOrWhiteSpace(fileName)
            ? DefaultConfigFileName
            : fileName;

        var lockTaken = false;
        try
        {
            await LoadSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            lockTaken = true;

            if (IsLoaded)
            {
                return Current;
            }

            await using var stream = await FileSystem.OpenAppPackageFileAsync(resolvedFileName)
                .ConfigureAwait(false);
            var loaded = await JsonSerializer.DeserializeAsync<AppConfig>(
                    stream,
                    SerializerOptions,
                    cancellationToken)
                .ConfigureAwait(false);

            if (loaded != null)
            {
                ApplyLoadedConfig(loaded);
                IsLoaded = true;
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
        finally
        {
            if (lockTaken)
            {
                LoadSemaphore.Release();
            }
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
        Current.UiText = loaded.UiText ?? new UiTextConfig();
        Current.UiNumbers = loaded.UiNumbers ?? new UiNumbersConfig();
        Current.Preferences = loaded.Preferences ?? new PreferencesConfig();
        Current.Urls = loaded.Urls ?? new UrlConfig();
        Current.SeatFinder = loaded.SeatFinder ?? new SeatFinderConfig();
        Current.SafeArrival = loaded.SafeArrival ?? new SafeArrivalConfig();
        Current.MensaForecast = loaded.MensaForecast ?? new MensaForecastConfig();
        Current.StudentAccess = loaded.StudentAccess ?? new StudentAccessConfig();
        Current.OpeningHoursText = loaded.OpeningHoursText ?? new OpeningHoursTextConfig();
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
