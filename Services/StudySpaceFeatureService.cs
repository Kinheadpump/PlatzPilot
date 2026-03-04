using System.Globalization;
using System.Text.Json;
using PlatzPilot.Configuration;
using PlatzPilot.Models;

namespace PlatzPilot.Services;

public interface IStudySpaceFeatureService
{
    Task<IReadOnlyDictionary<string, StudySpaceFeatureEntry>> LoadAsync(CancellationToken cancellationToken = default);
}

public sealed class StudySpaceFeatureService : IStudySpaceFeatureService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly SeatFinderConfig _settings;
    private readonly InternalConfig _internal;
    private readonly SemaphoreSlim _loadGate = new(1, 1);
    private IReadOnlyDictionary<string, StudySpaceFeatureEntry>? _cache;

    public StudySpaceFeatureService(AppConfig config)
    {
        _settings = config.SeatFinder;
        _internal = config.Internal;
    }

    public async Task<IReadOnlyDictionary<string, StudySpaceFeatureEntry>> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (_cache != null)
        {
            return _cache;
        }

        await _loadGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_cache != null)
            {
                return _cache;
            }

            await using var stream = await FileSystem.OpenAppPackageFileAsync(_settings.SpaceFeaturesFileName)
                .ConfigureAwait(false);
            var catalog = await JsonSerializer.DeserializeAsync<StudySpaceFeatureCatalog>(
                    stream,
                    SerializerOptions,
                    cancellationToken)
                .ConfigureAwait(false);

            var map = new Dictionary<string, StudySpaceFeatureEntry>(StringComparer.OrdinalIgnoreCase);
            if (catalog?.Spaces != null)
            {
                foreach (var entry in catalog.Spaces)
                {
                    if (string.IsNullOrWhiteSpace(entry.Id))
                    {
                        continue;
                    }

                    map[entry.Id.Trim()] = entry;
                }
            }

            _cache = map;
            return _cache;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(string.Format(
                CultureInfo.CurrentCulture,
                _internal.SpaceFeaturesLoadFailedFormat,
                _settings.SpaceFeaturesFileName,
                ex.Message));

            _cache ??= new Dictionary<string, StudySpaceFeatureEntry>(StringComparer.OrdinalIgnoreCase);
            return _cache;
        }
        finally
        {
            _loadGate.Release();
        }
    }
}
