namespace PlatzPilot.Configuration;

public sealed class SafeArrivalConfig
{
    public int BinMinutes { get; set; } = 5;
    public int SmoothingWindow { get; set; } = 5;
    public double AlphaPrior { get; set; } = 1.0;
    public double BetaPrior { get; set; } = 1.0;
    public int RequiredFreeSeats { get; set; } = 1;
    public double GoalProbability { get; set; } = 0.85;
    public double TauDays { get; set; } = 2.5;
    public double BufferRatio { get; set; } = 0.08;
    public double MinWeightedSamplesForCandidate { get; set; } = 0.2;
    public double EarlyPeakQuantile { get; set; } = 0.45;
    public double EarlyPeakBias { get; set; } = 0.7;
    public double DailyPeakThresholdRatio { get; set; } = 0.85;
    public int LeadTimeBeforePeakMinutes { get; set; } = 15;
    public int MinCoveredDaysForConfidence { get; set; } = 2;
    public double MinWeightedSamplesForConfidence { get; set; } = 1.0;
    public double FallbackProbabilityMargin { get; set; } = 0.18;
    public double MinFallbackProbability { get; set; } = 0.65;
    public int HistoryWindowDays { get; set; } = 7;
    public int DayCoverageMaxOffsetDays { get; set; } = 30;
    public double EarlyPeakQuantileMin { get; set; } = 0.05;
    public double EarlyPeakQuantileMax { get; set; } = 0.95;
    public double TrendFlatThresholdMinutes { get; set; } = 2.0;
    public double DailyPeakThresholdMin { get; set; } = 0.6;
    public double DailyPeakThresholdMax { get; set; } = 0.98;
    public double HighProbabilityThreshold { get; set; } = 0.90;
    public double MediumProbabilityThreshold { get; set; } = 0.80;
}