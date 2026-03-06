using System.Net.Http;
using System.Text.Json;
using System.Globalization;
using System.Threading;
using PlatzPilot.Configuration;
using PlatzPilot.Models;

namespace PlatzPilot.Services;

public class SeatFinderService
{
    public const string HttpClientName = "SeatFinder";
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly SeatFinderConfig _settings;
    private readonly InternalConfig _internal;
    private readonly TimeSpan _requestTimeout;

    public SeatFinderService(IHttpClientFactory httpClientFactory, AppConfig config)
    {
        _httpClientFactory = httpClientFactory;
        _settings = config.SeatFinder;
        _internal = config.Internal;
        _requestTimeout = TimeSpan.FromSeconds(Math.Max(1, _settings.RequestTimeoutSeconds));
    }

    public async Task<List<StudySpace>> FetchSeatDataAsync(
        int limit,
        string? after = null,
        string? before = null,
        CancellationToken cancellationToken = default)
    {
        var resultList = new List<StudySpace>();
        string locationString = string.Join(_settings.LocationSeparator, _settings.Locations);

        var query = _settings.Query;
        var resolvedAfter = after ?? string.Empty;
        var resolvedBefore = string.IsNullOrWhiteSpace(before) ? _settings.NowToken : before;
        
        var queryParams = new Dictionary<string, string>
        {
            { query.CallbackParam, $"{_settings.CallbackPrefix}{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}" },
            { query.TimestampParam, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString() },
            
            // Block 0: Zeitverlauf der Auslastung
            { query.Location0Param, locationString },
            { query.Values0Param, query.Values0Value },
            { query.After0Param, resolvedAfter },
            { query.Before0Param, resolvedBefore },
            { query.Limit0Param, limit.ToString() },
            
            // Block 1: Stammdaten (Limit immer 1, da Stammdaten sich nicht ändern)
            { query.Location1Param, locationString },
            { query.Values1Param, query.Values1Value },
            { query.After1Param, string.Empty },
            { query.Before1Param, _settings.NowToken },
            { query.Limit1Param, _settings.MetadataLimit.ToString(CultureInfo.InvariantCulture) }
        };

        string requestUrl = $"{_settings.BaseUrl}{_settings.QueryStartSeparator}{BuildQueryString(queryParams)}";

        try
        {
            var client = _httpClientFactory.CreateClient(HttpClientName);

            // Combine request timeout with caller cancellation to avoid hanging requests.
            using var timeoutCts = new CancellationTokenSource(_requestTimeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, cancellationToken);

            var responseText = await client.GetStringAsync(requestUrl, linkedCts.Token);

            int startIdx = responseText.IndexOf('(');
            int endIdx = responseText.LastIndexOf(')');

            if (startIdx == -1 || endIdx == -1) 
            {
                System.Diagnostics.Debug.WriteLine(_internal.JsonpParseErrorText);
                return resultList;
            }

            string cleanJson = responseText.Substring(startIdx + 1, endIdx - startIdx - 1);

            var parsedData = JsonSerializer.Deserialize<List<SeatFinderResponseDto>>(cleanJson);
            if (parsedData == null || parsedData.Count < _settings.JsonpMinBlocks) return resultList;

            var liveData = parsedData[0];
            var metaData = parsedData[1].Locations;

            if (metaData == null) return resultList;

            foreach (var kvp in metaData)
            {
                string locationId = kvp.Key;
                var metaInfo = kvp.Value.FirstOrDefault();
                
                if (metaInfo == null) continue;

                var correctedLevel = metaInfo.Level;
                // Korrektur für bekannte Inkonsistenz in den Daten (L3 hat Level "0" statt "3")
                if (string.Equals(locationId, "L3", StringComparison.OrdinalIgnoreCase))
                {
                    correctedLevel = "3";
                }
                
                var space = new StudySpace
                {
                    Id = locationId,
                    Name = string.IsNullOrWhiteSpace(metaInfo.LongName) ? metaInfo.Name : metaInfo.LongName,
                    TotalSeats = metaInfo.AvailableSeats,
                    Building = metaInfo.Building,
                    Level = correctedLevel,
                    Room = metaInfo.Room,
                    OpeningHours = metaInfo.OpeningHours,
                    Url = metaInfo.Url,
                    SuperLocation = metaInfo.SuperLocation,
                    SubLocations = metaInfo.SubLocations ?? []
                };

                ParseCoordinates(metaInfo.GeoCoordinates, space);
                space.SeatHistory = BuildHistoricalEstimateHistory(locationId, liveData);
                AssignCurrentSeatData(locationId, liveData, space);

                resultList.Add(space);
            }
        }
        catch (TaskCanceledException ex)
        {
            System.Diagnostics.Debug.WriteLine(string.Format(
                CultureInfo.CurrentCulture,
                _internal.HttpRequestErrorFormat,
                ex.Message));
            throw;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(string.Format(
                CultureInfo.CurrentCulture,
                _internal.HttpRequestErrorFormat,
                ex.Message));
        }

        return resultList;
    }

    private string BuildQueryString(Dictionary<string, string> parameters)
    {
        var pairSeparator = _settings.QueryPairSeparator;
        var parameterSeparator = _settings.QueryParameterSeparator;
        var encodedParams = parameters.Select(kvp =>
            $"{Uri.EscapeDataString(kvp.Key)}{pairSeparator}{Uri.EscapeDataString(kvp.Value)}");
        return string.Join(parameterSeparator, encodedParams);
    }

    static private List<SeatHistoryPoint> BuildHistoricalEstimateHistory(string locationId, SeatFinderResponseDto liveData)
    {
        var historyByTimestamp = new Dictionary<DateTime, SeatHistoryPoint>();

        if (liveData.SeatEstimates != null &&
            liveData.SeatEstimates.TryGetValue(locationId, out var estimateRecords) &&
            estimateRecords.Count > 0)
        {
            AddHistoryRecords(historyByTimestamp, estimateRecords, isManual: false);
        }

        if (liveData.ManualCounts != null &&
            liveData.ManualCounts.TryGetValue(locationId, out var manualRecords) &&
            manualRecords.Count > 0)
        {
            AddHistoryRecords(historyByTimestamp, manualRecords, isManual: true);
        }

        return historyByTimestamp.Values
            .OrderByDescending(point => point.Timestamp)
            .ToList();
    }

    static private void AssignCurrentSeatData(string locationId, SeatFinderResponseDto liveData, StudySpace space)
    {
        var latestEstimate = GetLatestRecord(locationId, liveData.SeatEstimates, isManual: false);
        var latestManual = GetLatestRecord(locationId, liveData.ManualCounts, isManual: true);

        var preferredRecord = latestManual != null &&
                              (latestEstimate == null || latestManual.Timestamp >= latestEstimate.Timestamp)
            ? latestManual
            : latestEstimate;

        if (preferredRecord == null)
        {
            return;
        }

        space.FreeSeats = preferredRecord.FreeSeats;
        space.OccupiedSeats = preferredRecord.OccupiedSeats;
        space.LastUpdated = preferredRecord.Timestamp;
        space.IsManualCount = preferredRecord.IsManualCount;
    }

    static private SeatHistoryPoint? GetLatestRecord(string locationId, Dictionary<string, List<SeatRecordDto>>? source, bool isManual)
    {
        if (source == null || !source.TryGetValue(locationId, out var records) || records.Count == 0)
        {
            return null;
        }

        return MapRecords(records, isManual).FirstOrDefault();
    }

    static private List<SeatHistoryPoint> MapRecords(List<SeatRecordDto> records, bool isManual)
    {
        return records
            .Select(record =>
            {
                var timestamp = record.Timestamp?.GetParsedDate();
                if (!timestamp.HasValue)
                {
                    return null;
                }

                return new SeatHistoryPoint
                {
                    Timestamp = timestamp.Value,
                    FreeSeats = Math.Max(0, record.FreeSeats),
                    OccupiedSeats = Math.Max(0, record.OccupiedSeats),
                    IsManualCount = isManual
                };
            })
            .Where(point => point != null)
            .Cast<SeatHistoryPoint>()
            .GroupBy(point => point.Timestamp)
            .Select(group => group.First())
            .OrderByDescending(point => point.Timestamp)
            .ToList();
    }

    static private void AddHistoryRecords(
        Dictionary<DateTime, SeatHistoryPoint> historyByTimestamp,
        List<SeatRecordDto> records,
        bool isManual)
    {
        foreach (var record in records)
        {
            var timestamp = record.Timestamp?.GetParsedDate();
            if (!timestamp.HasValue)
            {
                continue;
            }

            var point = new SeatHistoryPoint
            {
                Timestamp = timestamp.Value,
                FreeSeats = Math.Max(0, record.FreeSeats),
                OccupiedSeats = Math.Max(0, record.OccupiedSeats),
                IsManualCount = isManual
            };

            if (!historyByTimestamp.TryGetValue(point.Timestamp, out var existing))
            {
                historyByTimestamp[point.Timestamp] = point;
                continue;
            }

            if (point.IsManualCount && !existing.IsManualCount)
            {
                historyByTimestamp[point.Timestamp] = point;
            }
        }
    }

    static private void ParseCoordinates(string? geoString, StudySpace space)
    {
        if (string.IsNullOrWhiteSpace(geoString)) return;

        var separator = AppConfigProvider.Current.SeatFinder.CoordinateSeparator;
        if (string.IsNullOrWhiteSpace(separator))
        {
            return;
        }

        var coords = geoString.Split(new[] { separator }, StringSplitOptions.RemoveEmptyEntries);
        if (coords.Length == 2 && 
            double.TryParse(coords[0], NumberStyles.Any, CultureInfo.InvariantCulture, out double lat) &&
            double.TryParse(coords[1], NumberStyles.Any, CultureInfo.InvariantCulture, out double lon))
        {
            space.Latitude = lat;
            space.Longitude = lon;
        }
    }
}
