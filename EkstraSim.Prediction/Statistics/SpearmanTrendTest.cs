using MathNet.Numerics.Distributions;

namespace EkstraSim.Prediction.Statistics;

public static class SpearmanTrendTest
{
    public const string TestName = "Spearman";
    private const int MinimumSampleSize = 6;
    private const double Epsilon = 1e-12;

    public static TestResult Test(IReadOnlyList<double> x, IReadOnlyList<double> y)
    {
        if (x.Count != y.Count)
        {
            throw new ArgumentException("Samples must have equal length.", nameof(y));
        }

        var count = x.Count;
        if (count < MinimumSampleSize)
        {
            return TestResult.Inconclusive(TestName, count);
        }

        var (xRanks, _) = Ranking.AverageRanks(x);
        var (yRanks, _) = Ranking.AverageRanks(y);

        var xMean = xRanks.Average();
        var yMean = yRanks.Average();
        double covariance = 0;
        double xVariance = 0;
        double yVariance = 0;

        for (var i = 0; i < count; i++)
        {
            var xDeviation = xRanks[i] - xMean;
            var yDeviation = yRanks[i] - yMean;
            covariance += xDeviation * yDeviation;
            xVariance += xDeviation * xDeviation;
            yVariance += yDeviation * yDeviation;
        }

        if (xVariance <= 0 || yVariance <= 0)
        {
            return TestResult.Inconclusive(TestName, count);
        }

        var rho = Math.Clamp(covariance / Math.Sqrt(xVariance * yVariance), -1.0, 1.0);
        var degreesOfFreedom = count - 2;
        var t = rho * Math.Sqrt(degreesOfFreedom / Math.Max(Epsilon, 1 - rho * rho));
        var pValue = 2.0 * (1.0 - StudentT.CDF(0, 1, degreesOfFreedom, Math.Abs(t)));

        return new TestResult
        {
            Name = TestName,
            Statistic = rho,
            ZScore = t,
            PValue = Math.Min(1.0, Math.Max(0.0, pValue)),
            SampleSize = count,
            IsConclusive = true
        };
    }
}
