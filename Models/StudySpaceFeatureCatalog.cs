using System.Text.Json.Serialization;

namespace PlatzPilot.Models;

public class StudySpaceFeatureCatalog
{
    public List<StudySpaceFeatureEntry> Spaces { get; set; } = [];
}

public class StudySpaceFeatureEntry
{
    public string Id { get; set; } = string.Empty;
    public string? Nickname { get; set; }
    [JsonPropertyName("buildingGroupKey")]
    public string? BuildingGroupKey { get; set; }
    public List<string> RoomTypes { get; set; } = [];
    public bool RequiresReservation { get; set; }
    public bool FreeWifi { get; set; }
    public bool PowerOutlets { get; set; }
    public bool Whiteboard { get; set; }
}
