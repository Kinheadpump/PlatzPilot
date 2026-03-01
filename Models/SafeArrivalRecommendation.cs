namespace PlatzPilot.Models;

public sealed class SafeArrivalRecommendation
{
    public bool HasRecommendation { get; init; }
    public TimeSpan LatestSafeTime { get; init; }
    public double Probability { get; init; }
    public double ExpectedFreeSeats { get; init; }
    public bool ConfidenceFlag { get; init; }
}
