using System.Text.Json.Serialization;

namespace PlatzPilot.Models;

public class SeatRecordDto
{
    [JsonPropertyName(SeatFinderJsonKeys.Timestamp)]
    public TimestampDto? Timestamp { get; set; }

    [JsonPropertyName(SeatFinderJsonKeys.LocationName)]
    public string LocationName { get; set; } = string.Empty;

    [JsonPropertyName(SeatFinderJsonKeys.OccupiedSeats)]
    public int OccupiedSeats { get; set; }

    [JsonPropertyName(SeatFinderJsonKeys.FreeSeats)]
    public int FreeSeats { get; set; }
}