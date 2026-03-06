using System.Collections.Concurrent;
using System.Threading;

namespace PlatzPilot.Services;

internal static class SafeArrivalMath
{
    private const int LogGammaCacheLimit = 2048;
    private const int LogBetaBinomialCacheLimit = 4096;
    private static readonly ConcurrentDictionary<double, double> LogGammaCache = new();
    private static readonly ConcurrentDictionary<LogBetaBinomialKey, double> LogBetaBinomialCache = new();
    private static int _logGammaCacheSize;
    private static int _logBetaBinomialCacheSize;
    private static readonly double[] LogGammaCoefficients =
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

    internal static double ExpectedFreeSeats(double alpha, double beta, int capacity)
    {
        var denominator = alpha + beta;
        if (denominator <= 0)
        {
            return 0;
        }

        return capacity * alpha / denominator;
    }

    internal static double TailProbabilityAtLeastK(int n, int kMin, double alpha, double beta)
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

    internal static double LogSumExp(IReadOnlyList<double> values)
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

    internal static int CountBits(long value)
    {
        var remaining = (ulong)value;
        var count = 0;
        while (remaining != 0)
        {
            count += (int)(remaining & 1UL);
            remaining >>= 1;
        }

        return count;
    }

    private static double LogBetaBinomialPmf(int n, int k, double alphaPost, double betaPost)
    {
        var cacheKey = new LogBetaBinomialKey(n, k, alphaPost, betaPost);
        if (LogBetaBinomialCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        // Fully in log-space to avoid overflow for large capacities.
        var logCombination = LogGamma(n + 1) - LogGamma(k + 1) - LogGamma(n - k + 1);
        var logBetaPart = BetaLn(k + alphaPost, n - k + betaPost) - BetaLn(alphaPost, betaPost);
        var value = logCombination + logBetaPart;

        // Memoize to reduce CPU churn on repeated probability evaluations.
        if (Volatile.Read(ref _logBetaBinomialCacheSize) < LogBetaBinomialCacheLimit &&
            LogBetaBinomialCache.TryAdd(cacheKey, value))
        {
            Interlocked.Increment(ref _logBetaBinomialCacheSize);
        }

        return value;
    }

    private static double BetaLn(double a, double b)
    {
        return LogGamma(a) + LogGamma(b) - LogGamma(a + b);
    }

    // Lanczos approximation for numerically stable log-gamma.
    private static double LogGamma(double x)
    {
        if (LogGammaCache.TryGetValue(x, out var cached))
        {
            return cached;
        }

        double value;
        if (x < 0.5)
        {
            value = Math.Log(Math.PI) - Math.Log(Math.Sin(Math.PI * x)) - LogGamma(1 - x);
        }
        else
        {
            var z = x - 1;
            var accumulator = 0.99999999999980993;
            for (var i = 0; i < LogGammaCoefficients.Length; i++)
            {
                accumulator += LogGammaCoefficients[i] / (z + i + 1);
            }

            var temp = z + LogGammaCoefficients.Length - 0.5;
            value = 0.5 * Math.Log(2 * Math.PI) + (z + 0.5) * Math.Log(temp) - temp + Math.Log(accumulator);
        }

        // Keep the cache bounded to avoid unbounded memory growth.
        if (Volatile.Read(ref _logGammaCacheSize) < LogGammaCacheLimit &&
            LogGammaCache.TryAdd(x, value))
        {
            Interlocked.Increment(ref _logGammaCacheSize);
        }

        return value;
    }

    private readonly record struct LogBetaBinomialKey(int N, int K, double Alpha, double Beta);
}
