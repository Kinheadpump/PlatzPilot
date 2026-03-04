using System;
using System.Collections.Generic;
using System.Linq;
using PlatzPilot.Configuration;
using PlatzPilot.Models;

namespace PlatzPilot.Services;

internal static class SafeArrivalPeakAnalysis
{
    internal static (int PeakBin, double PeakTrendMinutesPerDay) AnalyzeDailyPeaks(
        IReadOnlyList<SeatHistoryPoint> history,
        bool[] openBins,
        DateTime referenceDate,
        int capacity,
        SafeArrivalConfig settings,
        int binsPerDay,
        int binMinutes)
    {
        var dailyPeaksForQuantile = new List<(int Bin, double Weight)>();
        var dailyPeaksForTrend = new List<(double DayPosition, int Bin, double Weight)>();
        var thresholdRatio = Math.Clamp(
            settings.DailyPeakThresholdRatio,
            settings.DailyPeakThresholdMin,
            settings.DailyPeakThresholdMax);

        foreach (var dayGroup in history.GroupBy(point => point.Timestamp.Date))
        {
            var daysAgo = (referenceDate - dayGroup.Key).TotalDays;
            if (daysAgo < 0 || daysAgo > settings.HistoryWindowDays)
            {
                continue;
            }

            var dayPoints = dayGroup
                .Select(point => new
                {
                    Bin = GetBinIndex(point.Timestamp, binMinutes, binsPerDay),
                    FreeSeats = Math.Clamp(point.FreeSeats, 0, capacity)
                })
                .Where(point => openBins[point.Bin])
                .Select(point => new
                {
                    point.Bin,
                    OccupiedSeats = Math.Max(0, capacity - point.FreeSeats)
                })
                .ToList();

            if (dayPoints.Count == 0)
            {
                continue;
            }

            var peakOccupancy = dayPoints.Max(point => point.OccupiedSeats);
            if (peakOccupancy <= 0)
            {
                continue;
            }

            var threshold = peakOccupancy * thresholdRatio;
            var dayPeak = dayPoints
                .OrderBy(point => point.Bin)
                .FirstOrDefault(point => point.OccupiedSeats >= threshold)
                ?? dayPoints
                    .OrderByDescending(point => point.OccupiedSeats)
                    .ThenBy(point => point.Bin)
                    .First();

            var recencyWeight = Math.Exp(-daysAgo / settings.TauDays);
            var earlyWeight = 1.0 + settings.EarlyPeakBias * (1.0 - (double)dayPeak.Bin / (binsPerDay - 1));
            var totalWeight = recencyWeight * earlyWeight;
            if (totalWeight <= 0)
            {
                continue;
            }

            dailyPeaksForQuantile.Add((dayPeak.Bin, totalWeight));
            dailyPeaksForTrend.Add((-daysAgo, dayPeak.Bin, recencyWeight));
        }

        if (dailyPeaksForQuantile.Count == 0)
        {
            return (-1, 0);
        }

        var peakBin = WeightedQuantileBin(
            dailyPeaksForQuantile,
            Math.Clamp(settings.EarlyPeakQuantile, settings.EarlyPeakQuantileMin, settings.EarlyPeakQuantileMax));
        var trendMinutesPerDay = CalculateTrendMinutesPerDay(dailyPeaksForTrend, binMinutes);
        return (peakBin, trendMinutesPerDay);
    }

    internal static int FindExpectedPeakBin(
        double[] alpha,
        double[] beta,
        double[] support,
        bool[] openBins,
        int capacity,
        SafeArrivalConfig settings)
    {
        var peakBin = -1;
        var peakOccupancyRate = double.NegativeInfinity;

        for (var bin = 0; bin < openBins.Length; bin++)
        {
            if (!openBins[bin] || support[bin] < settings.MinWeightedSamplesForCandidate)
            {
                continue;
            }

            var expectedFreeSeats = SafeArrivalMath.ExpectedFreeSeats(alpha[bin], beta[bin], capacity);
            var occupancyRate = capacity > 0
                ? Math.Clamp((capacity - expectedFreeSeats) / capacity, 0, 1)
                : 0;

            if (occupancyRate > peakOccupancyRate)
            {
                peakOccupancyRate = occupancyRate;
                peakBin = bin;
            }
        }

        return peakBin;
    }

    private static int GetBinIndex(DateTime timestamp, int binMinutes, int binsPerDay)
    {
        var totalMinutes = timestamp.TimeOfDay.TotalMinutes;
        var bin = (int)(totalMinutes / binMinutes);
        return Math.Clamp(bin, 0, binsPerDay - 1);
    }

    private static int WeightedQuantileBin(List<(int Bin, double Weight)> weightedBins, double quantile)
    {
        var sorted = weightedBins
            .OrderBy(entry => entry.Bin)
            .ToList();

        var totalWeight = sorted.Sum(entry => entry.Weight);
        if (totalWeight <= 0)
        {
            return sorted[0].Bin;
        }

        var threshold = totalWeight * quantile;
        var cumulative = 0.0;
        foreach (var entry in sorted)
        {
            cumulative += entry.Weight;
            if (cumulative >= threshold)
            {
                return entry.Bin;
            }
        }

        return sorted[^1].Bin;
    }

    private static double CalculateTrendMinutesPerDay(List<(double DayPosition, int Bin, double Weight)> points, int binMinutes)
    {
        if (points.Count < 3)
        {
            return 0;
        }

        var totalWeight = points.Sum(point => point.Weight);
        if (totalWeight <= 0)
        {
            return 0;
        }

        var meanX = points.Sum(point => point.Weight * point.DayPosition) / totalWeight;
        var meanY = points.Sum(point => point.Weight * point.Bin) / totalWeight;

        var covariance = points.Sum(point => point.Weight * (point.DayPosition - meanX) * (point.Bin - meanY));
        var variance = points.Sum(point => point.Weight * (point.DayPosition - meanX) * (point.DayPosition - meanX));
        if (variance <= 0)
        {
            return 0;
        }

        var slopeBinsPerDay = covariance / variance;
        return slopeBinsPerDay * binMinutes;
    }
}