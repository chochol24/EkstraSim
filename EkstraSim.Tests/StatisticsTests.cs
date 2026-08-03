using EkstraSim.Prediction.Metrics;
using EkstraSim.Prediction.Statistics;

namespace EkstraSim.Tests;

public class StatisticsTests
{
    [Fact]
    public void AverageRanksHandlesTies()
    {
        var (ranks, tieAdjustment) = Ranking.AverageRanks([1.0, 2.0, 2.0, 3.0]);

        Assert.Equal([1.0, 2.5, 2.5, 4.0], ranks);
        Assert.Equal(6.0, tieAdjustment, precision: 12);
    }

    [Fact]
    public void AverageRanksWithoutTiesHasNoAdjustment()
    {
        var (ranks, tieAdjustment) = Ranking.AverageRanks([10.0, 30.0, 20.0]);

        Assert.Equal([1.0, 3.0, 2.0], ranks);
        Assert.Equal(0, tieAdjustment, precision: 12);
    }

    [Fact]
    public void WilcoxonMatchesHandCalculatedExample()
    {
        double[] first = [1.2, 0.8, -0.3, 2.1, 1.5, -0.6, 0.9, 1.8];
        var second = new double[first.Length];

        var result = WilcoxonSignedRankTest.Paired(first, second);

        Assert.True(result.IsConclusive);
        Assert.Equal(8, result.SampleSize);
        Assert.Equal(33.0, result.Statistic, precision: 12);
        Assert.Equal(14.5 / Math.Sqrt(51.0), result.ZScore, precision: 10);
        Assert.InRange(result.PValue, 0.0420, 0.0426);
        Assert.True(result.IsSignificantAt(0.05));
    }

    [Fact]
    public void WilcoxonIgnoresZeroDifferences()
    {
        double[] first = [1, 1, 2, 3, 4, 5, 6];
        double[] second = [1, 1, 1, 1, 1, 1, 1];

        var result = WilcoxonSignedRankTest.Paired(first, second);

        Assert.Equal(5, result.SampleSize);
        Assert.False(result.IsConclusive);
    }

    [Fact]
    public void WilcoxonIsSymmetricInSignOfZ()
    {
        double[] worse = [1.0, 1.2, 0.9, 1.4, 1.1, 1.3, 1.5];
        double[] better = [0.5, 0.6, 0.4, 0.7, 0.55, 0.65, 0.75];

        var forward = WilcoxonSignedRankTest.Paired(worse, better);
        var backward = WilcoxonSignedRankTest.Paired(better, worse);

        Assert.Equal(forward.PValue, backward.PValue, precision: 12);
        Assert.Equal(forward.ZScore, -backward.ZScore, precision: 12);
    }

    [Fact]
    public void WilcoxonFindsNoDifferenceForIdenticalSamples()
    {
        double[] values = [1, 2, 3, 4, 5, 6, 7, 8];

        var result = WilcoxonSignedRankTest.Paired(values, values);

        Assert.False(result.IsConclusive);
        Assert.Equal(1.0, result.PValue, precision: 12);
    }

    [Fact]
    public void WilcoxonRejectsMismatchedLengths()
    {
        Assert.Throws<ArgumentException>(() => WilcoxonSignedRankTest.Paired([1, 2, 3], [1, 2]));
    }

    [Fact]
    public void MannWhitneyMatchesHandCalculatedExample()
    {
        double[] first = [1, 2, 3, 4, 5];
        double[] second = [6, 7, 8, 9, 10];

        var result = MannWhitneyUTest.Compare(first, second);

        Assert.True(result.IsConclusive);
        Assert.Equal(10, result.SampleSize);
        Assert.Equal(0.0, result.Statistic, precision: 12);
        Assert.Equal(-12.0 / Math.Sqrt(275.0 / 12.0), result.ZScore, precision: 10);
        Assert.InRange(result.PValue, 0.0119, 0.0125);
    }

    [Fact]
    public void MannWhitneyFindsNoDifferenceForInterleavedSamples()
    {
        double[] first = [1, 3, 5, 7, 9, 11];
        double[] second = [2, 4, 6, 8, 10, 12];

        var result = MannWhitneyUTest.Compare(first, second);

        Assert.True(result.PValue > 0.5);
    }

    [Fact]
    public void MannWhitneyNeedsMinimumGroupSize()
    {
        var result = MannWhitneyUTest.Compare([1, 2, 3], [4, 5, 6]);

        Assert.False(result.IsConclusive);
    }

    [Fact]
    public void HolmCorrectionMatchesHandCalculation()
    {
        var adjusted = HolmCorrection.Adjust([0.01, 0.04, 0.03]);

        Assert.Equal(0.03, adjusted[0], precision: 12);
        Assert.Equal(0.06, adjusted[1], precision: 12);
        Assert.Equal(0.06, adjusted[2], precision: 12);
    }

    [Fact]
    public void HolmCorrectionIsMonotoneAndCapped()
    {
        var adjusted = HolmCorrection.Adjust([0.5, 0.6, 0.9]);

        Assert.All(adjusted, value => Assert.True(value <= 1.0));
        Assert.Equal(1.0, adjusted[2], precision: 12);
    }

    [Fact]
    public void HolmCorrectionHandlesEmptyInput()
    {
        Assert.Empty(HolmCorrection.Adjust([]));
    }

    [Fact]
    public void RollingMeanUsesExpandingWindowAtTheStart()
    {
        var rolling = StabilityAnalysis.RollingMean([3, 6, 9, 12], window: 3);

        Assert.Equal(3.0, rolling[0], precision: 12);
        Assert.Equal(4.5, rolling[1], precision: 12);
        Assert.Equal(6.0, rolling[2], precision: 12);
        Assert.Equal(9.0, rolling[3], precision: 12);
    }

    [Fact]
    public void StabilityDetectsFirstRoundOfLastingCalm()
    {
        List<RoundObservation> observations =
        [
            new(20, 0.25, 1.0),
            new(21, 0.24, 0.8),
            new(22, 0.23, 0.5),
            new(23, 0.23, 0.05),
            new(24, 0.22, 0.04),
            new(25, 0.22, 0.03)
        ];

        var result = StabilityAnalysis.Detect("Poisson", observations, threshold: 0.2, window: 2);

        Assert.Equal(24, result.StabilisedFromRound);
        Assert.Equal("Poisson", result.ModelName);
    }

    [Fact]
    public void StabilityIgnoresEarlyCalmFollowedByDrift()
    {
        List<RoundObservation> observations =
        [
            new(20, 0.25, 0.01),
            new(21, 0.24, 0.01),
            new(22, 0.23, 5.0),
            new(23, 0.23, 5.0)
        ];

        var result = StabilityAnalysis.Detect("Elo", observations, threshold: 0.2, window: 1);

        Assert.Null(result.StabilisedFromRound);
    }

    [Fact]
    public void StabilityHandlesNoObservations()
    {
        var result = StabilityAnalysis.Detect("DixonColes", [], threshold: 0.1);

        Assert.Null(result.StabilisedFromRound);
        Assert.Empty(result.Rounds);
    }

    [Fact]
    public void PairwiseComparisonRanksModelsAndAppliesHolm()
    {
        var byModel = new Dictionary<string, IReadOnlyList<MatchEvaluation>>
        {
            ["Good"] = BuildEvaluations("Good", [0.10, 0.12, 0.11, 0.13, 0.09, 0.14, 0.10, 0.12]),
            ["Average"] = BuildEvaluations("Average", [0.20, 0.22, 0.21, 0.23, 0.19, 0.24, 0.20, 0.22]),
            ["Poor"] = BuildEvaluations("Poor", [0.30, 0.32, 0.31, 0.33, 0.29, 0.34, 0.30, 0.32])
        };

        var comparisons = ModelComparison.Pairwise(byModel, MetricKind.Brier);

        Assert.Equal(3, comparisons.Count);
        Assert.All(comparisons, c => Assert.Equal(8, c.PairedMatchCount));
        Assert.All(comparisons, c => Assert.True(c.AdjustedPValue >= c.Test.PValue));

        var goodVersusPoor = comparisons.Single(c => c.FirstModel == "Good" && c.SecondModel == "Poor");
        Assert.Equal("Good", goodVersusPoor.BetterModel);
        Assert.True(goodVersusPoor.Test.IsSignificantAt(0.05));
    }

    [Fact]
    public void PairwiseComparisonOnlyUsesSharedMatches()
    {
        var byModel = new Dictionary<string, IReadOnlyList<MatchEvaluation>>
        {
            ["A"] = BuildEvaluations("A", [0.1, 0.2, 0.3], startId: 1),
            ["B"] = BuildEvaluations("B", [0.4, 0.5], startId: 2)
        };

        var comparison = ModelComparison.Pairwise(byModel, MetricKind.Brier).Single();

        Assert.Equal(2, comparison.PairedMatchCount);
    }

    [Fact]
    public void HigherIsBetterMetricFlipsTheWinner()
    {
        var byModel = new Dictionary<string, IReadOnlyList<MatchEvaluation>>
        {
            ["Sharp"] = BuildEvaluations("Sharp", [0.10, 0.11, 0.12], asScoreProbability: true),
            ["Blunt"] = BuildEvaluations("Blunt", [0.05, 0.06, 0.07], asScoreProbability: true)
        };

        var comparison = ModelComparison.Pairwise(byModel, MetricKind.ProbabilityOfActualScore).Single();

        Assert.Equal("Sharp", comparison.BetterModel);
    }

    [Fact]
    public void PromotedComparisonSplitsByFlagAndWindow()
    {
        var evaluations = new List<MatchEvaluation>();

        for (var i = 0; i < 12; i++)
        {
            evaluations.Add(new MatchEvaluation
            {
                MatchId = i + 1,
                ModelName = "Poisson",
                Round = 20 + i / 6,
                InvolvesPromotedTeam = i % 2 == 0,
                Brier = i % 2 == 0 ? 0.6 + i * 0.01 : 0.2 + i * 0.01
            });
        }

        var byModel = new Dictionary<string, IReadOnlyList<MatchEvaluation>> { ["Poisson"] = evaluations };

        var overall = ModelComparison.PromotedVersusRest(byModel, MetricKind.Brier).Single();

        Assert.Equal(6, overall.PromotedCount);
        Assert.Equal(6, overall.OtherCount);
        Assert.True(overall.Difference > 0);
        Assert.True(overall.Test.IsConclusive);

        var windowed = ModelComparison.PromotedVersusRest(byModel, MetricKind.Brier, [(20, 20), (21, 21)]);

        Assert.Equal(2, windowed.Count);
        Assert.All(windowed, w => Assert.Equal(3, w.PromotedCount));
    }

    private static List<MatchEvaluation> BuildEvaluations(
        string modelName,
        double[] values,
        int startId = 1,
        bool asScoreProbability = false)
    {
        return values
            .Select((value, index) => new MatchEvaluation
            {
                MatchId = startId + index,
                ModelName = modelName,
                Round = 20 + index,
                Brier = asScoreProbability ? 0 : value,
                ProbabilityOfActualScore = asScoreProbability ? value : 0
            })
            .ToList();
    }
}
