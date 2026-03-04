namespace PlatzPilot.Configuration;

public sealed class RoomTypeConfig
{
    public string Group { get; set; } = string.Empty;
    public string SilentStudy { get; set; } = string.Empty;
    public string SilentStudyLegacy { get; set; } = string.Empty;
    public string NoReservation { get; set; } = string.Empty;
    public string NoReservationLegacy { get; set; } = string.Empty;
}