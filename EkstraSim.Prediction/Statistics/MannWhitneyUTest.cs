using MathNet.Numerics.Distributions;

namespace EkstraSim.Prediction.Statistics;

public static class MannWhitneyUTest
{
    public const string TestName = "Mann-Whitney U";
    private const int MinimumGroupSize = 4;

    public static TestResult Compare(IReadOnlyList<double> first, IReadOnlyList<double> second)
    {
        var firstCount = first.Count;
        var secondCount = second.Count;

        if (firstCount < MinimumGroupSize || secondCount < MinimumGroupSize)
        {
            return TestResult.Inconclusive(TestName, firstCount + secondCount);
        }

        var combined = new List<double>(firstCount + secondCount);
        combined.AddRange(first);
        combined.AddRange(second);

        var (ranks, tieAdjustment) = Ranking.AverageRanks(combined);

        double firstRankSum = 0;
        for (var i = 0; i < firstCount; i++)
        {
            firstRankSum += ranks[i];
        }

        var total = firstCount + secondCount;
        var statistic = firstRankSum - firstCount * (firstCount + 1) / 2.0;
        var expected = firstCount * (double)secondCount / 2.0;

        var varianceBase = firstCount * (double)secondCount / 12.0;
        var tieTerm = tieAdjustment / (total * (double)(total - 1));
        var variance = varianceBase * (total + 1 - tieTerm);

        if (variance <= 0)
        {
            return TestResult.Inconclusive(TestName, total);
        }

        var deviation = Math.Abs(statistic - expected);
        var continuityCorrected = Math.Max(0, deviation - 0.5);
        var z = continuityCorrected / Math.Sqrt(variance);
        var pValue = 2.0 * (1.0 - Normal.CDF(0, 1, z));

        return new TestResult
        {
            Name = TestName,
            Statistic = statistic,
            ZScore = statistic >= expected ? z : -z,
            PValue = Math.Min(1.0, Math.Max(0.0, pValue)),
            SampleSize = total,
            IsConclusive = true
        };
    }
}
