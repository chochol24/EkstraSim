using MathNet.Numerics.Distributions;

namespace EkstraSim.Prediction.Statistics;

public static class WilcoxonSignedRankTest
{
    public const string TestName = "Wilcoxon signed-rank";
    private const int MinimumSampleSize = 6;

    public static TestResult Paired(IReadOnlyList<double> first, IReadOnlyList<double> second)
    {
        if (first.Count != second.Count)
        {
            throw new ArgumentException("Paired samples must have equal length.", nameof(second));
        }

        var differences = new List<double>(first.Count);

        for (var i = 0; i < first.Count; i++)
        {
            var difference = first[i] - second[i];
            if (difference != 0)
            {
                differences.Add(difference);
            }
        }

        var count = differences.Count;
        if (count < MinimumSampleSize)
        {
            return TestResult.Inconclusive(TestName, count);
        }

        var absolute = differences.Select(Math.Abs).ToList();
        var (ranks, tieAdjustment) = Ranking.AverageRanks(absolute);

        double positiveRankSum = 0;
        for (var i = 0; i < count; i++)
        {
            if (differences[i] > 0)
            {
                positiveRankSum += ranks[i];
            }
        }

        var expected = count * (count + 1) / 4.0;
        var variance = count * (count + 1) * (2.0 * count + 1) / 24.0 - tieAdjustment / 48.0;

        if (variance <= 0)
        {
            return TestResult.Inconclusive(TestName, count);
        }

        var deviation = Math.Abs(positiveRankSum - expected);
        var continuityCorrected = Math.Max(0, deviation - 0.5);
        var z = continuityCorrected / Math.Sqrt(variance);
        var pValue = 2.0 * (1.0 - Normal.CDF(0, 1, z));

        return new TestResult
        {
            Name = TestName,
            Statistic = positiveRankSum,
            ZScore = positiveRankSum >= expected ? z : -z,
            PValue = Math.Min(1.0, Math.Max(0.0, pValue)),
            SampleSize = count,
            IsConclusive = true
        };
    }
}
