using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Globalization;

namespace PlatzPilot.Configuration;

public static class AppConfigProvider
{
    private const string DefaultConfigFileName = "app_config.json";

    public static AppConfig Current { get; private set; } = new();

    public static AppConfig LoadFromPackage(string? fileName = null)
    {
        var resolvedFileName = string.IsNullOrWhiteSpace(fileName)
            ? DefaultConfigFileName
            : fileName;

        try
        {
            using var stream = FileSystem.OpenAppPackageFileAsync(resolvedFileName)
                .GetAwaiter()
                .GetResult();
            using var reader = new StreamReader(stream);
            var json = reader.ReadToEnd();

            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            };
            options.Converters.Add(new TimeSpanJsonConverter());

            var loaded = JsonSerializer.Deserialize<AppConfig>(json, options);
            if (loaded != null)
            {
                Current = loaded;
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
}
