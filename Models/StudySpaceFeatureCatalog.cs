namespace PlatzPilot.Models;

public class StudySpaceFeatureCatalog
{
    public List<StudySpaceFeatureEntry> Spaces { get; set; } = [];
}

public class StudySpaceFeatureEntry
{
    public string Id { get; set; } = string.Empty;
    public List<string> RoomTypes { get; set; } = [];
    public bool FreeWifi { get; set; }
    public bool PowerOutlets { get; set; }
    public bool Whiteboard { get; set; }
}
