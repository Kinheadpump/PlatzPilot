using System.Collections.Generic;

namespace PlatzPilot.Configuration;

public sealed class SeatFinderConfig
{
    public string BaseUrl { get; set; } = string.Empty;
    public string NowToken { get; set; } = string.Empty;
    public List<string> Locations { get; set; } = [];
    public int WeeklyHistoryPoints { get; set; } = 2304;
    public int LiveSnapshotPoints { get; set; } = 1;
    public int LiveRefreshIntervalMinutes { get; set; } = 5;
    public int MetadataLimit { get; set; } = 1;
    public string CallbackPrefix { get; set; } = string.Empty;
    public string TimestampFormat { get; set; } = string.Empty;
    public string LocationSeparator { get; set; } = string.Empty;
    public string CoordinateSeparator { get; set; } = string.Empty;
    public string QueryStartSeparator { get; set; } = string.Empty;
    public string QueryPairSeparator { get; set; } = string.Empty;
    public string QueryParameterSeparator { get; set; } = string.Empty;
    public SeatFinderQueryConfig Query { get; set; } = new();
    public string SpaceFeaturesFileName { get; set; } = string.Empty;
    public int RequestTimeoutSeconds { get; set; } = 10;
    public int JsonpMinBlocks { get; set; } = 2;
    public int WeeklyHistoryWindowDays { get; set; } = 7;
}
