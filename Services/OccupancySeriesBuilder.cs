using PlatzPilot.Models;

namespace PlatzPilot.Services;

public static class OccupancySeriesBuilder
{
    public static List<float> BuildSeries(
        IEnumerable<SeatHistoryPoint> history,
        DateTime startTime,
        DateTime endTime,
        int binMinutes)
    {
        var buckets = new Dictionary<DateTime, (int free, int occupied)>();

        foreach (var point in history)
        {
            if (point.Timestamp < startTime || point.Timestamp > endTime)
            {
                continue;
            }

            var bucketTime = NormalizeToChartBin(point.Timestamp, binMinutes);
            if (!buckets.TryGetValue(bucketTime, out var totals))
            {
                totals = (0, 0);
            }

            totals.free += point.FreeSeats;
            totals.occupied += point.OccupiedSeats;
            buckets[bucketTime] = totals;
        }

        var series = new List<float>();
        float lastValue = 0f;
        var hasValue = false;

        for (var time = startTime; time <= endTime; time = time.AddMinutes(binMinutes))
        {
            if (buckets.TryGetValue(time, out var totals))
            {
                var value = (float)CalculateOccupancyRate(totals.free, totals.occupied);
                lastValue = value;
                hasValue = true;
                series.Add(value);
                continue;
            }

            series.Add(hasValue ? lastValue : 0f);
        }

        return series;
    }

    public static List<(int free, int occupied)> BuildBucketSeries(
        IEnumerable<SeatHistoryPoint> history,
        DateTime startTime,
        DateTime endTime,
        int binMinutes,
        int bucketCount)
    {
        var buckets = new Dictionary<DateTime, (int free, int occupied)>();

        foreach (var point in history)
        {
            if (point.Timestamp < startTime || point.Timestamp > endTime)
            {
                continue;
            }

            var bucketTime = NormalizeToChartBin(point.Timestamp, binMinutes);
            if (!buckets.TryGetValue(bucketTime, out var totals))
            {
                totals = (0, 0);
            }

            totals.free += point.FreeSeats;
            totals.occupied += point.OccupiedSeats;
            buckets[bucketTime] = totals;
        }

        var series = new List<(int free, int occupied)>(bucketCount);
        var hasValue = false;
        var lastTotals = (free: 0, occupied: 0);

        var index = 0;
        for (var time = startTime; time <= endTime && index < bucketCount; time = time.AddMinutes(binMinutes))
        {
            if (buckets.TryGetValue(time, out var totals))
            {
                lastTotals = totals;
                hasValue = true;
                series.Add(totals);
            }
            else
            {
                series.Add(hasValue ? lastTotals : (0, 0));
            }

            index++;
        }

        while (series.Count < bucketCount)
        {
            series.Add(hasValue ? lastTotals : (0, 0));
        }

        return series;
    }

    public static int GetBucketCount(DateTime startTime, DateTime endTime, int binMinutes)
    {
        if (endTime < startTime || binMinutes <= 0)
        {
            return 0;
        }

        var totalMinutes = (endTime - startTime).TotalMinutes;
        return (int)Math.Floor(totalMinutes / binMinutes) + 1;
    }

    public static DateTime NormalizeToChartBin(DateTime timestamp, int binMinutes)
    {
        var totalMinutes = (timestamp.Hour * 60) + timestamp.Minute;
        var normalizedMinutes = (totalMinutes / binMinutes) * binMinutes;
        return timestamp.Date.AddMinutes(normalizedMinutes);
    }

    public static double CalculateOccupancyRate(int freeSeats, int occupiedSeats)
    {
        var total = freeSeats + occupiedSeats;
        if (total <= 0)
        {
            return 0;
        }

        return Math.Clamp(occupiedSeats / (double)total, 0, 1);
    }
}
