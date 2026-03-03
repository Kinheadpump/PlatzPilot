using PlatzPilot.Configuration;
using PlatzPilot.Models;

namespace PlatzPilot.Services;

public sealed class SafeArrivalForecastService
{
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

    public SafeArrivalRecommendation Calculate(StudySpace space, IReadOnlyList<SeatHistoryPoint>? history, DateTime referenceTime)
    {
        if (space.TotalSeats <= 0 || history == null || history.Count == 0)
        {
            return EmptyResult();
        }

        var includeToday = referenceTime.Hour >= 20; // Include today's data if it's already late in the day, otherwise it may add noise.

        var filteredHistory = history
            .Where(point => IsOpenAtTime(space, point.Timestamp))
            .Where(point => includeToday || point.Timestamp.Date < referenceTime.Date)
            .ToList();

        if (filteredHistory.Count == 0)
        {
            return EmptyResult();
        }

        var modelReferenceDate = filteredHistory.Max(point => point.Timestamp.Date);
        var capacity = space.TotalSeats;
        var alphaRaw = Enumerable.Repeat(_settings.AlphaPrior, _binsPerDay).ToArray();
        var betaRaw = Enumerable.Repeat(_settings.BetaPrior, _binsPerDay).ToArray();
        var supportRaw = new double[_binsPerDay];
        var dayCoverageMask = new int[_binsPerDay];

        foreach (var point in filteredHistory)
        {
            // Use newest history day as reference so "before" filters do not discard most of the week.
            var daysAgo = (modelReferenceDate - point.Timestamp.Date).TotalDays;
            if (daysAgo < 0 || daysAgo > _settings.HistoryWindowDays)
            {
                continue;
            }

            var weight = Math.Exp(-daysAgo / _settings.TauDays);
            var clampedFreeSeats = Math.Clamp(point.FreeSeats, 0, capacity);
            var occupiedSeats = Math.Max(0, capacity - clampedFreeSeats);
            var binIndex = GetBinIndex(point.Timestamp);

            alphaRaw[binIndex] += weight * clampedFreeSeats;
            betaRaw[binIndex] += weight * occupiedSeats;
            supportRaw[binIndex] += weight;

            var dayOffset = Math.Clamp((int)daysAgo, 0, _settings.DayCoverageMaxOffsetDays);
            dayCoverageMask[binIndex] |= (1 << dayOffset);
        }

        var alpha = MovingAverage(alphaRaw);
        var beta = MovingAverage(betaRaw);
        var support = MovingAverage(supportRaw);
        var openBins = BuildOpenBinMaskForSpace(space, referenceTime.Date);
        if (!HasAnyOpenBin(openBins) && space.OpeningHours != null &&
            space.OpeningHours.TryGetNextOpeningTime(referenceTime, out var nextOpening))
        {
            openBins = BuildOpenBinMaskForSpace(space, nextOpening.Date);
        }
        var peakAnalysis = AnalyzeDailyPeaks(filteredHistory, openBins, modelReferenceDate, capacity);

        var peakBin = peakAnalysis.PeakBin;
        var peakTrendMinutesPerDay = peakAnalysis.PeakTrendMinutesPerDay;
        if (peakBin < 0)
        {
            peakBin = FindExpectedPeakBin(alpha, beta, support, openBins, capacity);
            peakTrendMinutesPerDay = 0;
        }

        if (peakBin < 0)
        {
            return EmptyResult();
        }

        var peakExpectedFreeSeats = ExpectedFreeSeats(alpha[peakBin], beta[peakBin], capacity);
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

            var probability = TailProbabilityAtLeastK(capacity, kEff, alpha[bin], beta[bin]);
            var expectedFreeSeats = ExpectedFreeSeats(alpha[bin], beta[bin], capacity);

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

        var coveredDays = CountBits(dayCoverageMask[chosenBin]);
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

    private (int PeakBin, double PeakTrendMinutesPerDay) AnalyzeDailyPeaks(IReadOnlyList<SeatHistoryPoint> history, bool[] openBins, DateTime referenceDate, int capacity)
    {
        var dailyPeaksForQuantile = new List<(int Bin, double Weight)>();
        var dailyPeaksForTrend = new List<(double DayPosition, int Bin, double Weight)>();
        var thresholdRatio = Math.Clamp(
            _settings.DailyPeakThresholdRatio,
            _settings.DailyPeakThresholdMin,
            _settings.DailyPeakThresholdMax);

        foreach (var dayGroup in history.GroupBy(point => point.Timestamp.Date))
        {
            var daysAgo = (referenceDate - dayGroup.Key).TotalDays;
            if (daysAgo < 0 || daysAgo > _settings.HistoryWindowDays)
            {
                continue;
            }

            var dayPoints = dayGroup
                .Select(point => new
                {
                    Bin = GetBinIndex(point.Timestamp),
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

            var recencyWeight = Math.Exp(-daysAgo / _settings.TauDays);
            var earlyWeight = 1.0 + _settings.EarlyPeakBias * (1.0 - (double)dayPeak.Bin / (_binsPerDay - 1));
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
            Math.Clamp(_settings.EarlyPeakQuantile, _settings.EarlyPeakQuantileMin, _settings.EarlyPeakQuantileMax));
        var trendMinutesPerDay = CalculateTrendMinutesPerDay(dailyPeaksForTrend);
        return (peakBin, trendMinutesPerDay);
    }

    private int FindExpectedPeakBin(double[] alpha, double[] beta, double[] support, bool[] openBins, int capacity)
    {
        var peakBin = -1;
        var peakOccupancyRate = double.NegativeInfinity;

        for (var bin = 0; bin < _binsPerDay; bin++)
        {
            if (!openBins[bin] || support[bin] < _settings.MinWeightedSamplesForCandidate)
            {
                continue;
            }

            var expectedFreeSeats = ExpectedFreeSeats(alpha[bin], beta[bin], capacity);
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

    private double CalculateTrendMinutesPerDay(List<(double DayPosition, int Bin, double Weight)> points)
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
        return slopeBinsPerDay * _binMinutes;
    }

    private bool[] BuildOpenBinMask(OpeningHoursDto? openingHours, DateTime dayStart)
    {
        var openBins = new bool[_binsPerDay];

        // If opening hours are unknown, do not block the recommendation by schedule.
        if (openingHours == null)
        {
            Array.Fill(openBins, true);
            return openBins;
        }

        for (var bin = 0; bin < _binsPerDay; bin++)
        {
            var binTime = dayStart.AddMinutes(bin * _binMinutes);
            openBins[bin] = openingHours.IsCurrentlyOpen(binTime);
        }

        return openBins;
    }

    private bool[] BuildOpenBinMaskForSpace(StudySpace space, DateTime dayStart)
    {
        if (IsStudentAccessLocation(space))
        {
            return BuildStudentAccessBinMask(dayStart);
        }

        return BuildOpenBinMask(space.OpeningHours, dayStart);
    }

    private bool[] BuildStudentAccessBinMask(DateTime dayStart)
    {
        var openBins = new bool[_binsPerDay];
        for (var bin = 0; bin < _binsPerDay; bin++)
        {
            var binTime = dayStart.AddMinutes(bin * _binMinutes).TimeOfDay;
            openBins[bin] = binTime >= _studentAccess.Start && binTime < _studentAccess.End;
        }

        return openBins;
    }

    private bool IsOpenAtTime(StudySpace space, DateTime timestamp)
    {
        if (IsStudentAccessLocation(space))
        {
            var time = timestamp.TimeOfDay;
            return time >= _studentAccess.Start && time < _studentAccess.End;
        }

        return space.OpeningHours?.IsCurrentlyOpen(timestamp) ?? true;
    }

    private bool IsStudentAccessLocation(StudySpace space)
    {
        if (_studentAccess.LocationIds.Any(id => string.Equals(space.Id, id, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(space.Building) &&
            _studentAccess.BuildingIds.Any(id => string.Equals(space.Building.Trim(), id, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return _studentAccess.NameContains.Any(token =>
            space.Name.Contains(token, StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasAnyOpenBin(bool[] openBins)
    {
        foreach (var isOpen in openBins)
        {
            if (isOpen)
            {
                return true;
            }
        }

        return false;
    }

    private static double ExpectedFreeSeats(double alpha, double beta, int capacity)
    {
        var denominator = alpha + beta;
        if (denominator <= 0)
        {
            return 0;
        }

        return capacity * alpha / denominator;
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

    private static double TailProbabilityAtLeastK(int n, int kMin, double alpha, double beta)
    {
        if (kMin <= 0)
        {
            return 1;
        }

        if (kMin > n)
        {
            return 0;
        }

        var logTerms = new double[n - kMin + 1];
        var termIndex = 0;

        for (var k = kMin; k <= n; k++)
        {
            logTerms[termIndex++] = LogBetaBinomialPmf(n, k, alpha, beta);
        }

        var logTail = LogSumExp(logTerms);
        return Math.Exp(logTail);
    }

    private static double LogBetaBinomialPmf(int n, int k, double alpha, double beta)
    {
        var logCombination = LogGamma(n + 1) - LogGamma(k + 1) - LogGamma(n - k + 1);
        var logBetaPart = LogBeta(k + alpha, n - k + beta) - LogBeta(alpha, beta);

        return logCombination + logBetaPart;
    }

    private static double LogBeta(double a, double b)
    {
        return LogGamma(a) + LogGamma(b) - LogGamma(a + b);
    }

    private static double LogSumExp(IReadOnlyList<double> values)
    {
        if (values.Count == 0)
        {
            return double.NegativeInfinity;
        }

        var maxValue = values[0];
        for (var i = 1; i < values.Count; i++)
        {
            if (values[i] > maxValue)
            {
                maxValue = values[i];
            }
        }

        if (double.IsNegativeInfinity(maxValue))
        {
            return maxValue;
        }

        var sum = 0.0;
        for (var i = 0; i < values.Count; i++)
        {
            sum += Math.Exp(values[i] - maxValue);
        }

        return maxValue + Math.Log(sum);
    }

    private static int CountBits(int value)
    {
        var count = 0;
        while (value != 0)
        {
            count += value & 1;
            value >>= 1;
        }

        return count;
    }

    // Lanczos approximation for numerically stable log-gamma.
    private static double LogGamma(double x)
    {
        double[] coefficients =
        [
            676.5203681218851,
            -1259.1392167224028,
            771.32342877765313,
            -176.61502916214059,
            12.507343278686905,
            -0.13857109526572012,
            9.9843695780195716e-6,
            1.5056327351493116e-7
        ];

        if (x < 0.5)
        {
            return Math.Log(Math.PI) - Math.Log(Math.Sin(Math.PI * x)) - LogGamma(1 - x);
        }

        x -= 1;
        var accumulator = 0.99999999999980993;
        for (var i = 0; i < coefficients.Length; i++)
        {
            accumulator += coefficients[i] / (x + i + 1);
        }

        var temp = x + coefficients.Length - 0.5;
        return 0.5 * Math.Log(2 * Math.PI) + (x + 0.5) * Math.Log(temp) - temp + Math.Log(accumulator);
    }
}
