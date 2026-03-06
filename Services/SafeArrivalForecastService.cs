using PlatzPilot.Configuration;
using PlatzPilot.Models;

namespace PlatzPilot.Services;

public sealed class SafeArrivalForecastService
{
    private const int MaxEffectiveSampleDays = 14;

    private readonly SafeArrivalConfig _settings;
    private readonly StudentAccessConfig _studentAccess;
    private readonly int _binMinutes;
    private readonly int _binsPerDay;
    private readonly int _smoothingRadius;

    public SafeArrivalForecastService(AppConfig config)
    {
        _settings = config.SafeArrival;
        _studentAccess = config.StudentAccess;
        _binMinutes = Math.Max(1, _settings.BinMinutes);
        _binsPerDay = (24 * 60) / _binMinutes;

        var window = Math.Max(1, _settings.SmoothingWindow);
        _smoothingRadius = Math.Max(0, window / 2);
    }

    /// <summary>
    /// Computes a conservative "latest safe arrival" recommendation from historical occupancy.
    /// The algorithm:
    /// - Filters history to open times (and optionally excludes today).
    /// - Groups by (day, time-bin) and applies exponential recency weighting.
    /// - Fits a Beta-Binomial predictive model per bin (alpha/beta priors) and smooths via
    ///   moving-average across bins.
    /// - Detects an early-weighted daily peak and peak trend, then searches only from opening
    ///   to before the expected peak for the latest bin meeting the probability goal
    ///   (with a fallback relaxed goal).
    /// Outputs the recommended time, probability, expected free seats, confidence flags,
    /// and peak diagnostics for UI.
    /// </summary>
    public SafeArrivalRecommendation Calculate(StudySpace space, IReadOnlyList<SeatHistoryPoint>? history, DateTime referenceTime)
    {
        if (space.TotalSeats <= 0 || history == null || history.Count == 0)
        {
            return EmptyResult();
        }

        var includeToday = referenceTime.Hour >= 20; // Include today's data if it's already late in the day, otherwise it may add noise.

        var filteredHistory = history
            .Where(point => SafeArrivalSchedule.IsOpenAtTime(space, point.Timestamp, _studentAccess))
            .Where(point => includeToday || point.Timestamp.Date < referenceTime.Date)
            .ToList();

        if (filteredHistory.Count == 0)
        {
            return EmptyResult();
        }

        var modelReferenceDate = filteredHistory.Max(point => point.Timestamp.Date);
        var capacity = space.TotalSeats;
        var alphaRaw = new double[_binsPerDay];
        var betaRaw = new double[_binsPerDay];
        var supportRaw = new double[_binsPerDay];
        var dayCoverageMask = new int[_binsPerDay];

        var sampleCounts = new int[_binsPerDay];
        var weightSums = new double[_binsPerDay];
        var weightedFreeSums = new double[_binsPerDay];

        foreach (var dayGroup in filteredHistory.GroupBy(point => new
                 {
                     Bin = GetBinIndex(point.Timestamp),
                     Day = point.Timestamp.Date
                 }))
        {
            // Use newest history day as reference so "before" filters do not discard most of the week.
            var daysAgo = (modelReferenceDate - dayGroup.Key.Day).TotalDays;
            if (daysAgo < 0 || daysAgo > _settings.HistoryWindowDays)
            {
                continue;
            }

            var binIndex = dayGroup.Key.Bin;
            var dayOffset = Math.Clamp((int)daysAgo, 0, _settings.DayCoverageMaxOffsetDays);
            var weightRaw = Math.Exp(-daysAgo / _settings.TauDays);
            var averageFreeSeats = dayGroup
                .Select(point => Math.Clamp(point.FreeSeats, 0, capacity))
                .DefaultIfEmpty(0)
                .Average();
            var freeFraction = capacity > 0
                ? Math.Clamp(averageFreeSeats / capacity, 0, 1)
                : 0;

            sampleCounts[binIndex]++;
            weightSums[binIndex] += weightRaw;
            weightedFreeSums[binIndex] += weightRaw * freeFraction;
            dayCoverageMask[binIndex] |= (1 << dayOffset);
        }

        // N_eff normalization per bin: normalize day weights, then scale to effective sample size.
        for (var bin = 0; bin < _binsPerDay; bin++)
        {
            var daysWithDataCount = sampleCounts[bin];
            if (daysWithDataCount == 0)
            {
                alphaRaw[bin] = _settings.AlphaPrior;
                betaRaw[bin] = _settings.BetaPrior;
                continue;
            }

            var weightSum = weightSums[bin];
            if (weightSum <= 0)
            {
                alphaRaw[bin] = _settings.AlphaPrior;
                betaRaw[bin] = _settings.BetaPrior;
                continue;
            }

            var weightedFraction = weightedFreeSums[bin] / weightSum;
            weightedFraction = Math.Clamp(weightedFraction, 0, 1);

            var nEff = Math.Min(daysWithDataCount, MaxEffectiveSampleDays);
            var successes = nEff * weightedFraction;

            alphaRaw[bin] = _settings.AlphaPrior + successes;
            betaRaw[bin] = _settings.BetaPrior + Math.Max(0, nEff - successes);
            supportRaw[bin] = weightSum;
        }

        var alpha = MovingAverage(alphaRaw);
        var beta = MovingAverage(betaRaw);
        var support = MovingAverage(supportRaw);
        var openBins = SafeArrivalSchedule.BuildOpenBinMaskForSpace(space, referenceTime.Date, _binsPerDay, _binMinutes, _studentAccess);
        if (!SafeArrivalSchedule.HasAnyOpenBin(openBins) && space.OpeningHours != null &&
            space.OpeningHours.TryGetNextOpeningTime(referenceTime, out var nextOpening))
        {
            openBins = SafeArrivalSchedule.BuildOpenBinMaskForSpace(space, nextOpening.Date, _binsPerDay, _binMinutes, _studentAccess);
        }
        var peakAnalysis = SafeArrivalPeakAnalysis.AnalyzeDailyPeaks(
            filteredHistory,
            openBins,
            modelReferenceDate,
            capacity,
            _settings,
            _binsPerDay,
            _binMinutes);

        var peakBin = peakAnalysis.PeakBin;
        var peakTrendMinutesPerDay = peakAnalysis.PeakTrendMinutesPerDay;
        if (peakBin < 0)
        {
            peakBin = SafeArrivalPeakAnalysis.FindExpectedPeakBin(alpha, beta, support, openBins, capacity, _settings);
            peakTrendMinutesPerDay = 0;
        }

        if (peakBin < 0)
        {
            return EmptyResult();
        }

        var peakExpectedFreeSeats = SafeArrivalMath.ExpectedFreeSeats(alpha[peakBin], beta[peakBin], capacity);
        var peakOccupancyRate = capacity > 0
            ? Math.Clamp((capacity - peakExpectedFreeSeats) / capacity, 0, 1)
            : 0;

        var bufferSeats = Math.Max((int)Math.Ceiling(_settings.BufferRatio * capacity), 1);
        var kEff = Math.Min(capacity, _settings.RequiredFreeSeats + bufferSeats);
        var leadBins = Math.Max(1, _settings.LeadTimeBeforePeakMinutes / _binMinutes);
        var searchUpperBoundBin = Math.Max(0, peakBin - leadBins);
        var firstOpenBin = Array.FindIndex(openBins, isOpen => isOpen);
        if (firstOpenBin < 0)
        {
            return EmptyResult();
        }

        searchUpperBoundBin = Math.Max(searchUpperBoundBin, firstOpenBin);

        var relaxedGoal = Math.Max(_settings.MinFallbackProbability, _settings.GoalProbability - _settings.FallbackProbabilityMargin);

        var latestStrictBin = -1;
        var latestStrictProbability = 0.0;
        var latestStrictExpectedFreeSeats = 0.0;

        var latestFallbackBin = -1;
        var latestFallbackProbability = 0.0;
        var latestFallbackExpectedFreeSeats = 0.0;

        // Recommendation is only searched from opening up to shortly before peak occupancy.
        // This avoids suggesting very late times (e.g. 23:00) when places become freer again.
        for (var bin = 0; bin <= searchUpperBoundBin; bin++)
        {
            if (!openBins[bin] || support[bin] < _settings.MinWeightedSamplesForCandidate)
            {
                continue;
            }

            var probability = SafeArrivalMath.TailProbabilityAtLeastK(capacity, kEff, alpha[bin], beta[bin]);
            var expectedFreeSeats = SafeArrivalMath.ExpectedFreeSeats(alpha[bin], beta[bin], capacity);

            if (probability >= relaxedGoal)
            {
                latestFallbackBin = bin;
                latestFallbackProbability = probability;
                latestFallbackExpectedFreeSeats = expectedFreeSeats;
            }

            if (probability < _settings.GoalProbability)
            {
                continue;
            }

            latestStrictBin = bin;
            latestStrictProbability = probability;
            latestStrictExpectedFreeSeats = expectedFreeSeats;
        }

        if (latestStrictBin < 0 && latestFallbackBin < 0)
        {
            return new SafeArrivalRecommendation
            {
                HasRecommendation = false,
                LatestSafeTime = TimeSpan.Zero,
                Probability = 0,
                ExpectedFreeSeats = 0,
                ConfidenceFlag = false,
                HasPeakData = true,
                PeakTime = TimeSpan.FromMinutes(peakBin * _binMinutes),
                PeakExpectedFreeSeats = peakExpectedFreeSeats,
                PeakOccupancyRate = peakOccupancyRate,
                PeakTrendMinutesPerDay = peakTrendMinutesPerDay
            };
        }

        var usesFallbackRecommendation = latestStrictBin < 0;
        var chosenBin = usesFallbackRecommendation ? latestFallbackBin : latestStrictBin;
        var chosenProbability = usesFallbackRecommendation ? latestFallbackProbability : latestStrictProbability;
        var chosenExpectedFreeSeats = usesFallbackRecommendation ? latestFallbackExpectedFreeSeats : latestStrictExpectedFreeSeats;

        var coveredDays = SafeArrivalMath.CountBits(dayCoverageMask[chosenBin]);
        var confidenceFlag = !usesFallbackRecommendation &&
                             coveredDays >= _settings.MinCoveredDaysForConfidence &&
                             support[chosenBin] >= _settings.MinWeightedSamplesForConfidence;

        return new SafeArrivalRecommendation
        {
            HasRecommendation = true,
            LatestSafeTime = TimeSpan.FromMinutes(chosenBin * _binMinutes),
            Probability = chosenProbability,
            ExpectedFreeSeats = chosenExpectedFreeSeats,
            ConfidenceFlag = confidenceFlag,
            HasPeakData = true,
            PeakTime = TimeSpan.FromMinutes(peakBin * _binMinutes),
            PeakExpectedFreeSeats = peakExpectedFreeSeats,
            PeakOccupancyRate = peakOccupancyRate,
            PeakTrendMinutesPerDay = peakTrendMinutesPerDay
        };
    }

    private static SafeArrivalRecommendation EmptyResult()
    {
        return new SafeArrivalRecommendation
        {
            HasRecommendation = false,
            LatestSafeTime = TimeSpan.Zero,
            Probability = 0,
            ExpectedFreeSeats = 0,
            ConfidenceFlag = false,
            HasPeakData = false,
            PeakTime = TimeSpan.Zero,
            PeakExpectedFreeSeats = 0,
            PeakOccupancyRate = 0,
            PeakTrendMinutesPerDay = 0
        };
    }

    private int GetBinIndex(DateTime timestamp)
    {
        var totalMinutes = timestamp.TimeOfDay.TotalMinutes;
        var bin = (int)(totalMinutes / _binMinutes);
        return Math.Clamp(bin, 0, _binsPerDay - 1);
    }

    private double[] MovingAverage(double[] values)
    {
        var result = new double[values.Length];

        for (var i = 0; i < values.Length; i++)
        {
            var start = Math.Max(0, i - _smoothingRadius);
            var end = Math.Min(values.Length - 1, i + _smoothingRadius);

            var sum = 0.0;
            for (var j = start; j <= end; j++)
            {
                sum += values[j];
            }

            var count = end - start + 1;
            result[i] = count > 0 ? sum / count : values[i];
        }

        return result;
    }

}
