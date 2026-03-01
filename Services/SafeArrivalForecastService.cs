using PlatzPilot.Models;

namespace PlatzPilot.Services;

public sealed class SafeArrivalForecastService
{
    private const int BinMinutes = 5;
    private const int BinsPerDay = (24 * 60) / BinMinutes;
    private const int SmoothingWindow = 5;
    private const int SmoothingRadius = SmoothingWindow / 2;
    private static readonly TimeSpan StudentAccessStart = new(7, 0, 0);
    private static readonly TimeSpan StudentAccessEnd = new(22, 0, 0);
    private const string StudentAccessLocationId = "LAF";
    private const string StudentAccessBuildingId = "50.19";

    private const double AlphaPrior = 1.0;
    private const double BetaPrior = 1.0;

    public int RequiredFreeSeats { get; set; } = 1;
    public double GoalProbability { get; set; } = 0.85;
    public double TauDays { get; set; } = 2.5;
    public double BufferRatio { get; set; } = 0.08;
    public double MinWeightedSamplesForCandidate { get; set; } = 0.2;
    public double EarlyPeakQuantile { get; set; } = 0.45;
    public double EarlyPeakBias { get; set; } = 0.7;
    public int LeadTimeBeforePeakMinutes { get; set; } = 15;
    public int MinCoveredDaysForConfidence { get; set; } = 2;
    public double MinWeightedSamplesForConfidence { get; set; } = 1.0;
    public double FallbackProbabilityMargin { get; set; } = 0.18;
    public double MinFallbackProbability { get; set; } = 0.65;

    public SafeArrivalRecommendation Calculate(StudySpace space, IReadOnlyList<SeatHistoryPoint>? history, DateTime referenceTime)
    {
        if (space.TotalSeats <= 0 || history == null || history.Count == 0)
        {
            return EmptyResult();
        }

        var filteredHistory = history
            .Where(point => IsOpenAtTime(space, point.Timestamp))
            .ToList();

        if (filteredHistory.Count == 0)
        {
            return EmptyResult();
        }

        var modelReferenceDate = filteredHistory.Max(point => point.Timestamp.Date);
        var capacity = space.TotalSeats;
        var alphaRaw = Enumerable.Repeat(AlphaPrior, BinsPerDay).ToArray();
        var betaRaw = Enumerable.Repeat(BetaPrior, BinsPerDay).ToArray();
        var supportRaw = new double[BinsPerDay];
        var dayCoverageMask = new int[BinsPerDay];

        foreach (var point in filteredHistory)
        {
            // Use newest history day as reference so "before" filters do not discard most of the week.
            var daysAgo = (modelReferenceDate - point.Timestamp.Date).TotalDays;
            if (daysAgo < 0 || daysAgo > 7)
            {
                continue;
            }

            var weight = Math.Exp(-daysAgo / TauDays);
            var clampedFreeSeats = Math.Clamp(point.FreeSeats, 0, capacity);
            var occupiedSeats = Math.Max(0, capacity - clampedFreeSeats);
            var binIndex = GetBinIndex(point.Timestamp);

            alphaRaw[binIndex] += weight * clampedFreeSeats;
            betaRaw[binIndex] += weight * occupiedSeats;
            supportRaw[binIndex] += weight;

            var dayOffset = Math.Clamp((int)daysAgo, 0, 30);
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

        var bufferSeats = Math.Max((int)Math.Ceiling(BufferRatio * capacity), 1);
        var kEff = Math.Min(capacity, RequiredFreeSeats + bufferSeats);
        var leadBins = Math.Max(1, LeadTimeBeforePeakMinutes / BinMinutes);
        var searchUpperBoundBin = Math.Max(0, peakBin - leadBins);
        var firstOpenBin = Array.FindIndex(openBins, isOpen => isOpen);
        if (firstOpenBin < 0)
        {
            return EmptyResult();
        }

        searchUpperBoundBin = Math.Max(searchUpperBoundBin, firstOpenBin);

        var relaxedGoal = Math.Max(MinFallbackProbability, GoalProbability - FallbackProbabilityMargin);

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
            if (!openBins[bin] || support[bin] < MinWeightedSamplesForCandidate)
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

            if (probability < GoalProbability)
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
                PeakTime = TimeSpan.FromMinutes(peakBin * BinMinutes),
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
                             coveredDays >= MinCoveredDaysForConfidence &&
                             support[chosenBin] >= MinWeightedSamplesForConfidence;

        return new SafeArrivalRecommendation
        {
            HasRecommendation = true,
            LatestSafeTime = TimeSpan.FromMinutes(chosenBin * BinMinutes),
            Probability = chosenProbability,
            ExpectedFreeSeats = chosenExpectedFreeSeats,
            ConfidenceFlag = confidenceFlag,
            HasPeakData = true,
            PeakTime = TimeSpan.FromMinutes(peakBin * BinMinutes),
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

    private static int GetBinIndex(DateTime timestamp)
    {
        var totalMinutes = timestamp.TimeOfDay.TotalMinutes;
        var bin = (int)(totalMinutes / BinMinutes);
        return Math.Clamp(bin, 0, BinsPerDay - 1);
    }

    private (int PeakBin, double PeakTrendMinutesPerDay) AnalyzeDailyPeaks(IReadOnlyList<SeatHistoryPoint> history, bool[] openBins, DateTime referenceDate, int capacity)
    {
        var dailyPeaksForQuantile = new List<(int Bin, double Weight)>();
        var dailyPeaksForTrend = new List<(double DayPosition, int Bin, double Weight)>();

        foreach (var dayGroup in history.GroupBy(point => point.Timestamp.Date))
        {
            var daysAgo = (referenceDate - dayGroup.Key).TotalDays;
            if (daysAgo < 0 || daysAgo > 7)
            {
                continue;
            }

            var dayPeak = dayGroup
                .Select(point => new
                {
                    Bin = GetBinIndex(point.Timestamp),
                    FreeSeats = Math.Clamp(point.FreeSeats, 0, capacity)
                })
                .Where(point => openBins[point.Bin])
                .OrderBy(point => point.FreeSeats)
                .ThenBy(point => point.Bin)
                .FirstOrDefault();

            if (dayPeak == null)
            {
                continue;
            }

            var recencyWeight = Math.Exp(-daysAgo / TauDays);
            var earlyWeight = 1.0 + EarlyPeakBias * (1.0 - (double)dayPeak.Bin / (BinsPerDay - 1));
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

        var peakBin = WeightedQuantileBin(dailyPeaksForQuantile, Math.Clamp(EarlyPeakQuantile, 0.05, 0.95));
        var trendMinutesPerDay = CalculateTrendMinutesPerDay(dailyPeaksForTrend);
        return (peakBin, trendMinutesPerDay);
    }

    private int FindExpectedPeakBin(double[] alpha, double[] beta, double[] support, bool[] openBins, int capacity)
    {
        var peakBin = -1;
        var peakOccupancyRate = double.NegativeInfinity;

        for (var bin = 0; bin < BinsPerDay; bin++)
        {
            if (!openBins[bin] || support[bin] < MinWeightedSamplesForCandidate)
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

    private static double CalculateTrendMinutesPerDay(List<(double DayPosition, int Bin, double Weight)> points)
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
        return slopeBinsPerDay * BinMinutes;
    }

    private static bool[] BuildOpenBinMask(OpeningHoursDto? openingHours, DateTime dayStart)
    {
        var openBins = new bool[BinsPerDay];

        // If opening hours are unknown, do not block the recommendation by schedule.
        if (openingHours == null)
        {
            Array.Fill(openBins, true);
            return openBins;
        }

        for (var bin = 0; bin < BinsPerDay; bin++)
        {
            var binTime = dayStart.AddMinutes(bin * BinMinutes);
            openBins[bin] = openingHours.IsCurrentlyOpen(binTime);
        }

        return openBins;
    }

    private static bool[] BuildOpenBinMaskForSpace(StudySpace space, DateTime dayStart)
    {
        if (IsStudentAccessLocation(space))
        {
            return BuildStudentAccessBinMask(dayStart);
        }

        return BuildOpenBinMask(space.OpeningHours, dayStart);
    }

    private static bool[] BuildStudentAccessBinMask(DateTime dayStart)
    {
        var openBins = new bool[BinsPerDay];
        for (var bin = 0; bin < BinsPerDay; bin++)
        {
            var binTime = dayStart.AddMinutes(bin * BinMinutes).TimeOfDay;
            openBins[bin] = binTime >= StudentAccessStart && binTime < StudentAccessEnd;
        }

        return openBins;
    }

    private static bool IsOpenAtTime(StudySpace space, DateTime timestamp)
    {
        if (IsStudentAccessLocation(space))
        {
            var time = timestamp.TimeOfDay;
            return time >= StudentAccessStart && time < StudentAccessEnd;
        }

        return space.OpeningHours?.IsCurrentlyOpen(timestamp) ?? true;
    }

    private static bool IsStudentAccessLocation(StudySpace space)
    {
        if (string.Equals(space.Id, StudentAccessLocationId, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(space.Building) &&
            string.Equals(space.Building.Trim(), StudentAccessBuildingId, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return space.Name.Contains("Informatikom", StringComparison.OrdinalIgnoreCase);
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

    private static double[] MovingAverage(double[] values)
    {
        var result = new double[values.Length];

        for (var i = 0; i < values.Length; i++)
        {
            var start = Math.Max(0, i - SmoothingRadius);
            var end = Math.Min(values.Length - 1, i + SmoothingRadius);

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
