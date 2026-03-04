using System;

namespace PlatzPilot.Configuration;

public sealed class MensaForecastConfig
{
    public double Latitude { get; set; } = 49.0118;
    public double Longitude { get; set; } = 8.4169;
    public int MinSpaceCapacity { get; set; } = 50;
    public int QueueBufferMinutes { get; set; } = 15;
    public int EatingBufferMinutes { get; set; } = 45;
    public TimeSpan WindowStart { get; set; } = new(11, 0, 0);
    public TimeSpan CashDeskClose { get; set; } = new(14, 0, 0);
    public int StepMinutes { get; set; } = 5;
}