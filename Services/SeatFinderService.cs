using System.Text.Json;
using System.Globalization;
using PlatzPilot.Models;

namespace PlatzPilot.Services;

public class SeatFinderService
{
    private readonly HttpClient _httpClient;
    
    private const string BaseUrl = "https://seatfinder.bibliothek.kit.edu/karlsruhe/getdata.php";
    
    private readonly string[] _locations = 
    {
        "LSG", "LSM", "LST", "LSN", "LSW", "LBS", "BIB-N", "L3", "L2", "SAR", 
        "L1", "LEG", "FBC", "FBP", "LAF", "FBA", "FBI", "FBM", "FBH", "FBD", "BLB", "WIS"
    };

    public SeatFinderService()
    {
        _httpClient = new HttpClient();
    }

    public async Task<List<StudySpace>> FetchSeatDataAsync(int limit = 1, string after = "", string before = "now")
    {
        var resultList = new List<StudySpace>();
        string locationString = string.Join(",", _locations);
        
        var queryParams = new Dictionary<string, string>
        {
            { "callback", $"PlatzPilot_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}" },
            { "_", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString() },
            
            // Block 0: Zeitverlauf der Auslastung
            { "location[0]", locationString },
            { "values[0]", "seatestimate,manualcount" },
            { "after[0]", after },
            { "before[0]", before },
            { "limit[0]", limit.ToString() },
            
            // Block 1: Stammdaten (Limit immer 1, da Stammdaten sich nicht ändern)
            { "location[1]", locationString },
            { "values[1]", "location" },
            { "after[1]", "" },
            { "before[1]", "now" },
            { "limit[1]", "1" }
        };

        string requestUrl = $"{BaseUrl}?{BuildQueryString(queryParams)}";

        try
        {
            var responseText = await _httpClient.GetStringAsync(requestUrl);

            int startIdx = responseText.IndexOf('(');
            int endIdx = responseText.LastIndexOf(')');

            if (startIdx == -1 || endIdx == -1) 
            {
                System.Diagnostics.Debug.WriteLine("Fehler: Konnte JSONP-Format nicht parsen.");
                return resultList;
            }

            string cleanJson = responseText.Substring(startIdx + 1, endIdx - startIdx - 1);

            var parsedData = JsonSerializer.Deserialize<List<SeatFinderResponseDto>>(cleanJson);
            if (parsedData == null || parsedData.Count < 2) return resultList;

            var liveData = parsedData[0];
            var metaData = parsedData[1].Locations;

            if (metaData == null) return resultList;

            foreach (var kvp in metaData)
            {
                string locationId = kvp.Key;
                var metaInfo = kvp.Value.FirstOrDefault();
                
                if (metaInfo == null) continue;

                var space = new StudySpace
                {
                    Id = locationId,
                    Name = string.IsNullOrWhiteSpace(metaInfo.LongName) ? metaInfo.Name : metaInfo.LongName,
                    TotalSeats = metaInfo.AvailableSeats,
                    Building = metaInfo.Building,
                    Level = metaInfo.Level,
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
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Fehler beim HTTP-Request: {ex.Message}");
        }

        return resultList;
    }

    static private string BuildQueryString(Dictionary<string, string> parameters)
    {
        var encodedParams = parameters.Select(kvp => 
            $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}");
        return string.Join("&", encodedParams);
    }

    static private List<SeatHistoryPoint> BuildHistoricalEstimateHistory(string locationId, SeatFinderResponseDto liveData)
    {
        if (liveData.SeatEstimates != null &&
            liveData.SeatEstimates.TryGetValue(locationId, out var estimateRecords) &&
            estimateRecords.Count > 0)
        {
            return MapRecords(estimateRecords, isManual: false);
        }

        return [];
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

    static private void ParseCoordinates(string? geoString, StudySpace space)
    {
        if (string.IsNullOrWhiteSpace(geoString)) return;
        
        var coords = geoString.Split(';');
        if (coords.Length == 2 && 
            double.TryParse(coords[0], NumberStyles.Any, CultureInfo.InvariantCulture, out double lat) &&
            double.TryParse(coords[1], NumberStyles.Any, CultureInfo.InvariantCulture, out double lon))
        {
            space.Latitude = lat;
            space.Longitude = lon;
        }
    }
}
