namespace PlatzPilot.Configuration;

public sealed class ChartConfig
{
    public int HistoryHours { get; set; } = 24;
    public int BinMinutes { get; set; } = 5;
    public double Height { get; set; } = 120;
    public float LineSize { get; set; } = 2f;
    public byte LineAreaAlpha { get; set; } = 38;
    public float LabelTextSize { get; set; } = 0f;
    public float ValueLabelTextSize { get; set; } = 0f;
    public float SerieLabelTextSize { get; set; } = 0f;
    public bool ShowYAxisLines { get; set; }
    public bool ShowYAxisText { get; set; }
    public float MinValue { get; set; } = 0f;
    public float MaxValue { get; set; } = 1f;
    public int MinSeriesPoints { get; set; } = 2;
    public string LineColorLight { get; set; } = "#1f6feb";
    public string LineColorDark { get; set; } = "#9ec1ff";
    public string BackgroundColorLight { get; set; } = "#ffffff";
    public string BackgroundColorDark { get; set; } = "#1e1e1e";
}
