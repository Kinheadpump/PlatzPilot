using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace PlatzPilot.Models;

public class SeatFinderResponseDto
{
    [JsonPropertyName(SeatFinderJsonKeys.SeatEstimate)]
    public Dictionary<string, List<SeatRecordDto>>? SeatEstimates { get; set; }

    [JsonPropertyName(SeatFinderJsonKeys.ManualCount)]
    public Dictionary<string, List<SeatRecordDto>>? ManualCounts { get; set; }

    [JsonPropertyName(SeatFinderJsonKeys.Location)]
    public Dictionary<string, List<LocationMetadataDto>>? Locations { get; set; }
}