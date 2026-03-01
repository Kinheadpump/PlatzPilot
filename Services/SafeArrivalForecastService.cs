using PlatzPilot.Models;

namespace PlatzPilot.Services;

public sealed class SafeArrivalForecastService
{
    private const int BinMinutes = 5;
    private const int BinsPerDay = (24 * 60) / BinMinutes;
    private const int SmoothingWindow = 5;
    private const int SmoothingRadius = SmoothingWindow / 2;

    private const double AlphaPrior = 1.0;
    private const double BetaPrior = 1.0;

    public int RequiredFreeSeats { get; set; } = 1;
    public double GoalProbability { get; set; } = 0.90;
    public double TauDays { get; set; } = 3.0;
    public double BufferRatio { get; set; } = 0.05;
    public double MinWeightedSamplesForCandidate { get; set; } = 0.25;
    public int MinCoveredDaysForConfidence { get; set; } = 3;
    public double MinWeightedSamplesForConfidence { get; set; } = 1.5;

    public SafeArrivalRecommendation Calculate(StudySpace space, IReadOnlyList<SeatHistoryPoint>? history, DateTime referenceTime)
    {
        if (space.TotalSeats <= 0 || history == null || history.Count == 0)
        {
            return new SafeArrivalRecommendation
            {
                HasRecommendation = false,
                LatestSafeTime = TimeSpan.Zero,
                Probability = 0,
                ExpectedFreeSeats = 0,
                ConfidenceFlag = false
            };
        }

        var capacity = space.TotalSeats;
        var alphaRaw = Enumerable.Repeat(AlphaPrior, BinsPerDay).ToArray();
        var betaRaw = Enumerable.Repeat(BetaPrior, BinsPerDay).ToArray();
        var supportRaw = new double[BinsPerDay];
        var dayCoverageMask = new int[BinsPerDay];

        foreach (var point in history)
        {
            var daysAgo = (referenceTime.Date - point.Timestamp.Date).TotalDays;
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

        var bufferSeats = Math.Max((int)Math.Ceiling(BufferRatio * capacity), 1);
        var kEff = Math.Min(capacity, RequiredFreeSeats + bufferSeats);

        var dayStart = referenceTime.Date;
        var latestSafeBin = -1;
        var latestProbability = 0.0;
        var latestExpectedFreeSeats = 0.0;
        var confidence = false;

        for (var bin = 0; bin < BinsPerDay; bin++)
        {
            var binTime = dayStart.AddMinutes(bin * BinMinutes);
            if (space.OpeningHours != null && !space.OpeningHours.IsCurrentlyOpen(binTime))
            {
                continue;
            }

            if (support[bin] < MinWeightedSamplesForCandidate)
            {
                continue;
            }

            var probability = TailProbabilityAtLeastK(capacity, kEff, alpha[bin], beta[bin]);
            if (probability < GoalProbability)
            {
                continue;
            }

            latestSafeBin = bin;
            latestProbability = probability;
            latestExpectedFreeSeats = capacity * alpha[bin] / (alpha[bin] + beta[bin]);

            var coveredDays = CountBits(dayCoverageMask[bin]);
            confidence = coveredDays >= MinCoveredDaysForConfidence && support[bin] >= MinWeightedSamplesForConfidence;
        }

        if (latestSafeBin < 0)
        {
            return new SafeArrivalRecommendation
            {
                HasRecommendation = false,
                LatestSafeTime = TimeSpan.Zero,
                Probability = 0,
                ExpectedFreeSeats = 0,
                ConfidenceFlag = false
            };
        }

        return new SafeArrivalRecommendation
        {
            HasRecommendation = true,
            LatestSafeTime = TimeSpan.FromMinutes(latestSafeBin * BinMinutes),
            Probability = latestProbability,
            ExpectedFreeSeats = latestExpectedFreeSeats,
            ConfidenceFlag = confidence
        };
    }

    private static int GetBinIndex(DateTime timestamp)
    {
        var totalMinutes = timestamp.TimeOfDay.TotalMinutes;
        var bin = (int)(totalMinutes / BinMinutes);
        return Math.Clamp(bin, 0, BinsPerDay - 1);
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
