using System.Reflection;
using PlatzPilot.Services;

namespace PlatzPilot.Tests;

public sealed class SafeArrivalMathTests
{
    [Fact]
    public void TailProbabilityAtLeastK_MatchesKnownBetaBinomialValue()
    {
        // Known case: n=5, alpha=2, beta=2, P(K >= 3) = 0.5.
        const int n = 5;
        const int kMin = 3;
        const int alpha = 2;
        const int beta = 2;
        const double expected = 0.5;

        var mathType = typeof(SafeArrivalForecastService).Assembly
            .GetType("PlatzPilot.Services.SafeArrivalMath", throwOnError: true);
        var method = mathType!.GetMethod(
            "TailProbabilityAtLeastK",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.NotNull(method);

        var result = (double)method!.Invoke(null, new object[] { n, kMin, (double)alpha, (double)beta })!;

        Assert.InRange(result, expected - 1e-10, expected + 1e-10);
        Assert.Equal(expected, ComputeTailProbabilityReference(n, kMin, alpha, beta), 10);
    }

    private static double ComputeTailProbabilityReference(int n, int kMin, int alpha, int beta)
    {
        var betaBase = Beta(alpha, beta);
        var tail = 0.0;
        for (var k = kMin; k <= n; k++)
        {
            var pmf = Combination(n, k) * Beta(k + alpha, n - k + beta) / betaBase;
            tail += pmf;
        }

        return tail;
    }

    private static double Combination(int n, int k)
    {
        return Factorial(n) / (Factorial(k) * Factorial(n - k));
    }

    private static double Beta(int a, int b)
    {
        return Factorial(a - 1) * Factorial(b - 1) / Factorial(a + b - 1);
    }

    private static double Factorial(int value)
    {
        var result = 1.0;
        for (var i = 2; i <= value; i++)
        {
            result *= i;
        }

        return result;
    }
}
