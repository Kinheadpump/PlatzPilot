using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PlatzPilot.Models;

public class LocationMetadataDto
{
    [JsonPropertyName(SeatFinderJsonKeys.Timestamp)]
    public TimestampDto? Timestamp { get; set; }

    [JsonPropertyName(SeatFinderJsonKeys.Name)]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName(SeatFinderJsonKeys.LongName)]
    public string LongName { get; set; } = string.Empty;

    [JsonPropertyName(SeatFinderJsonKeys.Url)]
    public string? Url { get; set; }

    [JsonPropertyName(SeatFinderJsonKeys.Building)]
    public string? Building { get; set; }

    [JsonPropertyName(SeatFinderJsonKeys.Level)]
    public string? Level { get; set; }

    [JsonPropertyName(SeatFinderJsonKeys.Room)]
    public string? Room { get; set; }

    [JsonPropertyName(SeatFinderJsonKeys.GeoCoordinates)]
    public string? GeoCoordinates { get; set; }

    [JsonPropertyName(SeatFinderJsonKeys.AvailableSeats)]
    public int AvailableSeats { get; set; }

    // Der interne Speicher fuer die fertigen Daten
    private OpeningHoursDto? _openingHours;

    [JsonPropertyName(SeatFinderJsonKeys.OpeningHours)]
    public JsonElement? OpeningHoursRaw
    {
        get => null;
        set
        {
            if (value.HasValue && value.Value.ValueKind == JsonValueKind.Object)
            {
                try
                {
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    _openingHours = value.Value.Deserialize<OpeningHoursDto>(options);
                }
                catch
                {
                    _openingHours = null; // Stilles Fehlschlagen, Fallback greift
                }
            }
        }
    }

    [JsonIgnore]
    public OpeningHoursDto? OpeningHours
    {
        get => _openingHours;
        set => _openingHours = value;
    }

    [JsonPropertyName(SeatFinderJsonKeys.SuperLocation)]
    public string? SuperLocation { get; set; }

    [JsonPropertyName(SeatFinderJsonKeys.SubLocations)]
    public List<string>? SubLocations { get; set; }
}