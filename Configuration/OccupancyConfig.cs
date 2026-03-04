namespace PlatzPilot.Configuration;

public sealed class OccupancyConfig
{
    public double LowThreshold { get; set; } = 0.4;
    public double MediumThreshold { get; set; } = 0.7;
    public double HighThreshold { get; set; } = 0.9;
    public string LowColor { get; set; } = "#2ecc71";
    public string MediumColor { get; set; } = "#f1c40f";
    public string HighColor { get; set; } = "#e67e22";
    public string FullColor { get; set; } = "#e74c3c";
    public string ClosedColor { get; set; } = "#b0b0b0";
}