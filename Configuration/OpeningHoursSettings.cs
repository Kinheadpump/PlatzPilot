namespace PlatzPilot.Configuration;

public sealed class OpeningHoursSettings
{
    public int MaxNextOpeningWeeks { get; set; } = 3;
    public int DaysPerWeek { get; set; } = 7;
    public int FullDayHours { get; set; } = 24;
    public int AlwaysOpenHourThreshold { get; set; } = 23;
}