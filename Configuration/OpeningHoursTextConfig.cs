namespace PlatzPilot.Configuration;

public sealed class OpeningHoursTextConfig
{
    public string ClosedSpecialText { get; set; } = string.Empty;
    public string UnknownText { get; set; } = string.Empty;
    public string AlwaysOpenText { get; set; } = string.Empty;
    public string ClosedText { get; set; } = string.Empty;
    public string HoursSuffix { get; set; } = string.Empty;
    public string TimeRangeFormat { get; set; } = string.Empty;
    public string TimeRangeSeparator { get; set; } = string.Empty;
    public string ClosedOpensTodayFormat { get; set; } = string.Empty;
    public string ClosedOpensTomorrowFormat { get; set; } = string.Empty;
    public string ClosedOpensOnDateFormat { get; set; } = string.Empty;
    public string NoRoomsText { get; set; } = string.Empty;
    public string UnknownObjectText { get; set; } = string.Empty;
}