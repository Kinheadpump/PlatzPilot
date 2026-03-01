using PlatzPilot.Models;

namespace PlatzPilot.Models;

public sealed class SeatHistoryPoint
{
    public DateTime Timestamp { get; init; }
    public int FreeSeats { get; init; }
    public int OccupiedSeats { get; init; }
    public bool IsManualCount { get; init; }
}
