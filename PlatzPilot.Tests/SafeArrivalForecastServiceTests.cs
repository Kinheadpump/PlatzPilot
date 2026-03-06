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

    [Fact]
    public void Calculate_FlatArrayAggregation_DoesNotThrowAndBinsSafely()
    {
        var config = new AppConfig();
        config.SafeArrival.BinMinutes = 15;
        config.SafeArrival.SmoothingWindow = 3;
        config.SafeArrival.HistoryWindowDays = 7;

        var service = new SafeArrivalForecastService(config);
        var space = new StudySpace
        {
            Id = "TEST",
            Name = "Library Hall",
            TotalSeats = 120,
            OpeningHours = null
        };

        var history = new List<SeatHistoryPoint>();
        var baseDay = DateTime.Today.AddDays(-3);
        for (var day = 0; day < 3; day++)
        {
            var date = baseDay.AddDays(day);
            history.Add(MakePoint(date.AddHours(0).AddMinutes(0), 80, space.TotalSeats));
            history.Add(MakePoint(date.AddHours(12).AddMinutes(30), 60, space.TotalSeats));
            history.Add(MakePoint(date.AddHours(23).AddMinutes(59), 20, space.TotalSeats));
        }

        var exception = Record.Exception(() => service.Calculate(space, history, DateTime.Today.AddHours(10)));

        Assert.Null(exception);
        var result = service.Calculate(space, history, DateTime.Today.AddHours(10));
        Assert.InRange(result.Probability, 0, 1);
        Assert.InRange(result.PeakOccupancyRate, 0, 1);
    }

    private static SeatHistoryPoint MakePoint(DateTime timestamp, int freeSeats, int capacity)
    {
        return new SeatHistoryPoint
        {
            Timestamp = timestamp,
            FreeSeats = Math.Clamp(freeSeats, 0, capacity),
            OccupiedSeats = Math.Max(0, capacity - freeSeats),
            IsManualCount = false
        };
    }
}
