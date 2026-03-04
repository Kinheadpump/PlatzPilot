using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace PlatzPilot.Models;

public class ExceptionalOpeningHoursDto
{
    [JsonPropertyName(SeatFinderJsonKeys.Start)]
    public TimestampDto? Start { get; set; }

    [JsonPropertyName(SeatFinderJsonKeys.End)]
    public TimestampDto? End { get; set; }

    [JsonPropertyName(SeatFinderJsonKeys.OpeningHours)]
    public List<List<TimestampDto>>? OpeningHours { get; set; }
}
