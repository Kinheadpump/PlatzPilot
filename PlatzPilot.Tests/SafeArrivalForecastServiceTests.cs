using PlatzPilot.Configuration;
using PlatzPilot.Models;
using PlatzPilot.Services;

namespace PlatzPilot.Tests;

public sealed class SafeArrivalForecastServiceTests
{
    [Fact]
    public void Calculate_LargeCapacity_ReturnsFiniteProbabilities()
    {
        var config = new AppConfig();
        config.SafeArrival.BinMinutes = 5;
        config.SafeArrival.SmoothingWindow = 5;

        var service = new SafeArrivalForecastService(config);
        var capacity = 1500;
        var space = new StudySpace
        {
            Id = "TEST",
            Name = "Large Hall",
            TotalSeats = capacity
        };

        var history = new List<SeatHistoryPoint>();
        var startDay = DateTime.Today.AddDays(-6).AddHours(11);
        for (var day = 0; day < 7; day++)
        {
            var baseTime = startDay.AddDays(day);
            for (var sample = 0; sample < 18; sample++)
            {
                var timestamp = baseTime.AddMinutes(sample * 10);
                var freeSeats = Math.Clamp(350 + (day * 15) + sample, 0, capacity);
                history.Add(new SeatHistoryPoint
                {
                    Timestamp = timestamp,
                    FreeSeats = freeSeats,
                    OccupiedSeats = Math.Max(0, capacity - freeSeats),
                    IsManualCount = false
                });
            }
        }

        var result = service.Calculate(space, history, DateTime.Today.AddHours(21));

        Assert.False(double.IsNaN(result.Probability));
        Assert.False(double.IsInfinity(result.Probability));
        Assert.InRange(result.Probability, 0, 1);
        Assert.False(double.IsNaN(result.PeakOccupancyRate));
        Assert.False(double.IsInfinity(result.PeakOccupancyRate));
        Assert.InRange(result.PeakOccupancyRate, 0, 1);
    }
}
