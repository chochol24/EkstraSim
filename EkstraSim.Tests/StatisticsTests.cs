using EkstraSim.Prediction.Evaluation;
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
    public void StabilitySettlesWhereRollingDriftStaysWithinTheRelativeThreshold()
    {
        List<RoundObservation> observations =
        [
            new(20, 0.25, 1.0),
            new(21, 0.24, 0.8),
            new(22, 0.23, 0.5),
            new(23, 0.23, 0.3),
            new(24, 0.22, 0.12),
            new(25, 0.22, 0.10),
            new(26, 0.21, 0.11),
            new(27, 0.21, 0.10),
            new(28, 0.20, 0.09)
        ];

        var result = StabilityAnalysis.Detect("Poisson", observations, tolerance: 0.25, window: 1);

        Assert.Equal("Poisson", result.ModelName);
        Assert.Equal(0.10, result.DriftLevel, precision: 12);
        Assert.Equal(0.125, result.Threshold, precision: 12);
        Assert.Equal(0.25, result.Tolerance);
        Assert.Equal(3.12 / 9, result.MeanDrift, precision: 12);
        Assert.Equal(24, result.StabilisedFromRound);
    }

    [Theory]
    [InlineData(0.2)]
    [InlineData(0.0)]
    public void FlatDriftSettlesFromTheFirstRound(double drift)
    {
        var observations = Enumerable.Range(20, 8).Select(round => new RoundObservation(round, 0.2, drift)).ToList();

        var result = StabilityAnalysis.Detect("Elo", observations, StabilityAnalysis.DefaultTolerance);

        Assert.Equal(20, result.StabilisedFromRound);
        Assert.Equal(drift, result.DriftLevel, precision: 12);
        Assert.False(result.Trend.IsConclusive);
    }

    [Fact]
    public void StabilityIgnoresEarlyCalmFollowedByAJump()
    {
        double[] drifts = [0.1, 0.1, 0.1, 0.9, 0.1, 0.1, 0.1, 0.1, 0.1];
        var observations = drifts.Select((drift, i) => new RoundObservation(20 + i, 0.2, drift)).ToList();

        var result = StabilityAnalysis.Detect("Elo", observations, tolerance: 0.25, window: 1);

        Assert.Equal(24, result.StabilisedFromRound);
    }

    [Theory]
    [InlineData(12, 3, 4)]
    [InlineData(6, 3, 3)]
    [InlineData(31, 3, 11)]
    [InlineData(16, 5, 6)]
    [InlineData(16, 8, 8)]
    [InlineData(2, 3, 2)]
    public void DriftLevelAveragesTheLastThirdButNoFewerRoundsThanTheWindow(int count, int window, int expectedRounds)
    {
        var observations = Enumerable.Range(1, count).Select(i => new RoundObservation(i, 0.2, i)).ToList();

        var result = StabilityAnalysis.Detect("Poisson", observations, StabilityAnalysis.DefaultTolerance, window);

        var expectedLevel = Enumerable.Range(count - expectedRounds + 1, expectedRounds).Average();
        Assert.Equal(expectedLevel, result.DriftLevel, precision: 12);
        Assert.Equal((1 + StabilityAnalysis.DefaultTolerance) * expectedLevel, result.Threshold, precision: 12);
    }

    [Fact]
    public void StabilityReportsTheDriftTrend()
    {
        var observations = Enumerable.Range(20, 10).Select(round => new RoundObservation(round, 0.2, 1.0 / round)).ToList();

        var result = StabilityAnalysis.Detect("Poisson", observations, StabilityAnalysis.DefaultTolerance);

        Assert.True(result.Trend.IsConclusive);
        Assert.Equal(-1.0, result.Trend.Statistic, precision: 12);
        Assert.True(result.Trend.PValue < 0.001);
        Assert.Equal(10, result.Trend.SampleSize);
    }

    [Fact]
    public void StabilityHandlesNoObservations()
    {
        var result = StabilityAnalysis.Detect("DixonColes", [], tolerance: 0.25);

        Assert.Null(result.StabilisedFromRound);
        Assert.Empty(result.Rounds);
        Assert.False(result.Trend.IsConclusive);
    }

    [Fact]
    public void DetectAllAdjustsTrendPValuesAcrossModelsWithHolm()
    {
        var rounds = Enumerable.Range(19, 16).ToList();
        var observationsByModel = new Dictionary<string, IReadOnlyList<RoundObservation>>
        {
            ["Poisson"] = rounds.Select((round, i) => new RoundObservation(round, 0.2, 0.3 - 0.005 * i + 0.02 * (i * 7 % 5))).ToList(),
            ["Elo"] = rounds.Select(round => new RoundObservation(round, 0.2, 0.1)).ToList(),
            ["DixonColes"] = rounds.Select((round, i) => new RoundObservation(round, 0.2, 0.3 + 0.02 * (i * 7 % 5))).ToList()
        };

        var results = StabilityAnalysis.DetectAll(observationsByModel, StabilityAnalysis.DefaultTolerance);

        Assert.Equal(["DixonColes", "Elo", "Poisson"], results.Select(r => r.ModelName));

        foreach (var result in results)
        {
            var alone = StabilityAnalysis.Detect(result.ModelName, observationsByModel[result.ModelName], StabilityAnalysis.DefaultTolerance);
            Assert.Equal(alone.Trend.Statistic, result.Trend.Statistic);
            Assert.Equal(alone.Trend.PValue, result.Trend.PValue);
            Assert.Equal(alone.StabilisedFromRound, result.StabilisedFromRound);
        }

        var dixonColes = results[0];
        var elo = results[1];
        var poisson = results[2];

        Assert.False(elo.Trend.IsConclusive);
        Assert.Equal(1.0, elo.TrendAdjustedPValue);
        Assert.True(poisson.Trend.IsConclusive && dixonColes.Trend.IsConclusive);
        Assert.True(poisson.Trend.PValue > 0 && poisson.Trend.PValue < dixonColes.Trend.PValue);
        Assert.Equal(Math.Min(1.0, 3 * poisson.Trend.PValue), poisson.TrendAdjustedPValue, precision: 12);
        Assert.Equal(Math.Min(1.0, Math.Max(3 * poisson.Trend.PValue, 2 * dixonColes.Trend.PValue)), dixonColes.TrendAdjustedPValue, precision: 12);
    }

    [Fact]
    public void DetectAllLimitsTheAnalysisToTheRequestedRounds()
    {
        var observations = Enumerable.Range(4, 31).Select(round => new RoundObservation(round, 0.2, 1.0 / round + 0.01 * (round % 3))).ToList();
        var observationsByModel = new Dictionary<string, IReadOnlyList<RoundObservation>> { ["Poisson"] = observations };

        var spring = Assert.Single(StabilityAnalysis.DetectAll(observationsByModel, 0.25, fromRound: 19));
        var expected = StabilityAnalysis.Detect("Poisson", observations.Where(o => o.Round >= 19).ToList(), 0.25);

        Assert.Equal(Enumerable.Range(19, 16), spring.Rounds);
        Assert.Equal(expected.DriftLevel, spring.DriftLevel);
        Assert.Equal(expected.StabilisedFromRound, spring.StabilisedFromRound);
        Assert.Equal(expected.Trend.Statistic, spring.Trend.Statistic);
        Assert.Equal(expected.Trend.PValue, spring.TrendAdjustedPValue);

        var autumn = Assert.Single(StabilityAnalysis.DetectAll(observationsByModel, 0.25, toRound: 18));
        Assert.Equal(Enumerable.Range(4, 15), autumn.Rounds);

        Assert.Empty(StabilityAnalysis.DetectAll(observationsByModel, 0.25, fromRound: 40));
    }

    [Fact]
    public void SpearmanIsOneForIncreasingAndMinusOneForDecreasingSeries()
    {
        var rounds = Enumerable.Range(19, 16).Select(r => (double)r).ToList();
        var increasing = rounds.Select(r => r * r).ToList();
        var decreasing = rounds.Select(r => 1.0 / r).ToList();

        var up = SpearmanTrendTest.Test(rounds, increasing);
        var down = SpearmanTrendTest.Test(rounds, decreasing);

        Assert.Equal("Spearman", up.Name);
        Assert.True(up.IsConclusive);
        Assert.Equal(1.0, up.Statistic, precision: 12);
        Assert.Equal(-1.0, down.Statistic, precision: 12);
        Assert.True(up.ZScore > 0 && down.ZScore < 0);
        Assert.True(up.PValue < 0.001 && down.PValue < 0.001);
        Assert.Equal(16, up.SampleSize);
    }

    [Fact]
    public void SpearmanMatchesMathNetWithTies()
    {
        double[] rounds = [19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30];
        double[] drift = [0.30, 0.25, 0.25, 0.40, 0.20, 0.20, 0.20, 0.15, 0.30, 0.10, 0.12, 0.10];

        var result = SpearmanTrendTest.Test(rounds, drift);
        var expected = MathNet.Numerics.Statistics.Correlation.Spearman(rounds, drift);

        Assert.True(result.IsConclusive);
        Assert.Equal(expected, result.Statistic, precision: 12);

        var degreesOfFreedom = rounds.Length - 2;
        var t = expected * Math.Sqrt(degreesOfFreedom / (1 - expected * expected));
        Assert.Equal(t, result.ZScore, precision: 10);
        Assert.Equal(
            2 * (1 - MathNet.Numerics.Distributions.StudentT.CDF(0, 1, degreesOfFreedom, Math.Abs(t))),
            result.PValue,
            precision: 12);
    }

    [Fact]
    public void SpearmanIsInconclusiveBelowSixObservations()
    {
        var result = SpearmanTrendTest.Test([1, 2, 3, 4, 5], [0.5, 0.4, 0.3, 0.2, 0.1]);

        Assert.False(result.IsConclusive);
        Assert.Equal(1.0, result.PValue);
        Assert.Equal(5, result.SampleSize);
    }

    [Fact]
    public void SpearmanIsInconclusiveForAConstantSeries()
    {
        double[] rounds = [19, 20, 21, 22, 23, 24, 25, 26];
        double[] constant = [0.2, 0.2, 0.2, 0.2, 0.2, 0.2, 0.2, 0.2];

        Assert.False(SpearmanTrendTest.Test(rounds, constant).IsConclusive);
        Assert.False(SpearmanTrendTest.Test(constant, rounds).IsConclusive);
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

    private const int ColdHome = 1;
    private const int ColdAway = 2;
    private const int ReturningTeam = 3;

    private static readonly (int MatchId, int HomeTeamId, int AwayTeamId)[] GradedMatches =
    [
        (1, ColdHome, 11),
        (2, 12, ColdHome),
        (3, ColdAway, 13),
        (4, ColdHome, ColdAway),
        (5, ColdHome, ReturningTeam),
        (6, ReturningTeam, 14),
        (7, 15, ReturningTeam),
        (8, 11, 12),
        (9, 13, 14),
        (10, 15, 16),
        (11, 17, 18),
        (12, 12, 11),
        (13, 14, 13),
        (14, 16, 15),
        (15, 18, 17)
    ];

    private static readonly List<PromotedTeamHistory> GradedPromoted =
    [
        new(ColdHome, 0, null),
        new(ColdAway, 0, null),
        new(ReturningTeam, 135, 2)
    ];

    [Fact]
    public void GradedComparisonPutsAMixedMatchInBothCategoriesAndTestsAgainstMatchesWithoutPromotedTeams()
    {
        var (byModel, teams) = GradedFixture();

        var analysis = ModelComparison.PromotedByCategory(byModel, MetricKind.Brier, teams, GradedPromoted);

        Assert.Equal(
            [("Elo", PromotedCategory.ColdStart), ("Elo", PromotedCategory.Returning), ("Poisson", PromotedCategory.ColdStart), ("Poisson", PromotedCategory.Returning)],
            analysis.Comparisons.Select(c => (c.ModelName, c.Category!.Value)));
        Assert.All(analysis.Comparisons, c => Assert.Equal(MetricKind.Brier, c.Metric));
        Assert.All(analysis.Comparisons, c => Assert.Null(c.FromRound));

        foreach (var model in new[] { "Elo", "Poisson" })
        {
            var cold = analysis.Comparisons.Single(c => c.ModelName == model && c.Category == PromotedCategory.ColdStart);
            var returning = analysis.Comparisons.Single(c => c.ModelName == model && c.Category == PromotedCategory.Returning);

            Assert.Equal(5, cold.PromotedCount);
            Assert.Equal(MeanOf(byModel[model], 1, 2, 3, 4, 5), cold.PromotedMean, precision: 12);
            Assert.Equal(3, returning.PromotedCount);
            Assert.Equal(MeanOf(byModel[model], 5, 6, 7), returning.PromotedMean, precision: 12);

            Assert.All([cold, returning], c => Assert.Equal(8, c.OtherCount));
            Assert.All([cold, returning], c => Assert.Equal(MeanOf(byModel[model], 8, 9, 10, 11, 12, 13, 14, 15), c.OtherMean, precision: 12));

            var binary = ModelComparison.PromotedVersusRest(byModel, MetricKind.Brier).Single(c => c.ModelName == model);
            Assert.Null(binary.Category);
            Assert.Equal(binary.OtherCount, cold.OtherCount);
            Assert.Equal(binary.OtherMean, cold.OtherMean);

            Assert.True(cold.Test.IsConclusive);
            Assert.True(cold.Difference > 0);
            Assert.False(returning.Test.IsConclusive);
            Assert.Equal(1.0, returning.Test.PValue);
        }
    }

    [Fact]
    public void GradedComparisonAppliesOneHolmFamilyAcrossModelsAndCategories()
    {
        var (byModel, teams) = GradedFixture();

        var comparisons = ModelComparison.PromotedByCategory(byModel, MetricKind.Brier, teams, GradedPromoted).Comparisons;
        var expected = HolmCorrection.Adjust(comparisons.Select(c => c.Test.PValue).ToList());

        Assert.Equal(4, comparisons.Count);
        Assert.Equal(expected, comparisons.Select(c => c.AdjustedPValue));
        Assert.All(comparisons.Where(c => c.Test.IsConclusive), c => Assert.True(c.AdjustedPValue > c.Test.PValue));
        Assert.Equal(Math.Min(1.0, 4 * comparisons.Min(c => c.Test.PValue)), comparisons.Min(c => c.AdjustedPValue), precision: 12);
    }

    [Fact]
    public void GradedComparisonAveragesEachPromotedTeamOverItsOwnMatches()
    {
        var (byModel, teams) = GradedFixture();

        var metrics = ModelComparison.PromotedByCategory(byModel, MetricKind.Brier, teams, GradedPromoted).TeamMetrics;

        Assert.Equal(
            [(ColdHome, "Elo"), (ColdHome, "Poisson"), (ColdAway, "Elo"), (ColdAway, "Poisson"), (ReturningTeam, "Elo"), (ReturningTeam, "Poisson")],
            metrics.Select(m => (m.TeamId, m.ModelName)));

        foreach (var model in new[] { "Elo", "Poisson" })
        {
            var coldHome = metrics.Single(m => m.TeamId == ColdHome && m.ModelName == model);
            var coldAway = metrics.Single(m => m.TeamId == ColdAway && m.ModelName == model);
            var returning = metrics.Single(m => m.TeamId == ReturningTeam && m.ModelName == model);

            Assert.Equal((4, PromotedCategory.ColdStart), (coldHome.Count, coldHome.Category));
            Assert.Equal(MeanOf(byModel[model], 1, 2, 4, 5), coldHome.Mean, precision: 12);
            Assert.Equal((2, PromotedCategory.ColdStart), (coldAway.Count, coldAway.Category));
            Assert.Equal(MeanOf(byModel[model], 3, 4), coldAway.Mean, precision: 12);
            Assert.Equal((3, PromotedCategory.Returning), (returning.Count, returning.Category));
            Assert.Equal(MeanOf(byModel[model], 5, 6, 7), returning.Mean, precision: 12);
        }
    }

    [Fact]
    public void GradedComparisonSkipsACategoryWithoutTeams()
    {
        var (byModel, teams) = GradedFixture();

        var analysis = ModelComparison.PromotedByCategory(byModel, MetricKind.Brier, teams, [new PromotedTeamHistory(ReturningTeam, 37, 5)]);

        Assert.Equal(["Elo", "Poisson"], analysis.Comparisons.Select(c => c.ModelName));
        Assert.All(analysis.Comparisons, c => Assert.Equal(PromotedCategory.Returning, c.Category));
        Assert.All(analysis.Comparisons, c => Assert.Equal((3, 12), (c.PromotedCount, c.OtherCount)));
        Assert.All(analysis.TeamMetrics, m => Assert.Equal(ReturningTeam, m.TeamId));

        var none = ModelComparison.PromotedByCategory(byModel, MetricKind.Brier, teams, []);

        Assert.Empty(none.Comparisons);
        Assert.Empty(none.TeamMetrics);
    }

    [Fact]
    public void GradedComparisonRejectsAMatchWithoutTeams()
    {
        var (byModel, teams) = GradedFixture();
        teams.Remove(8);

        Assert.Throws<ArgumentException>(() => ModelComparison.PromotedByCategory(byModel, MetricKind.Brier, teams, GradedPromoted));
    }

    private static (Dictionary<string, IReadOnlyList<MatchEvaluation>> ByModel, Dictionary<int, (int HomeTeamId, int AwayTeamId)> Teams) GradedFixture()
    {
        var promotedIds = GradedPromoted.Select(team => team.TeamId).ToHashSet();

        List<MatchEvaluation> Evaluations(string modelName, double scale) => GradedMatches
            .Select(match => new MatchEvaluation
            {
                MatchId = match.MatchId,
                ModelName = modelName,
                Round = 20,
                InvolvesPromotedTeam = promotedIds.Contains(match.HomeTeamId) || promotedIds.Contains(match.AwayTeamId),
                Brier = scale * (match.MatchId <= 7 ? 0.5 + 0.01 * match.MatchId : 0.1 + 0.01 * match.MatchId)
            })
            .ToList();

        var byModel = new Dictionary<string, IReadOnlyList<MatchEvaluation>>
        {
            ["Poisson"] = Evaluations("Poisson", 1.0),
            ["Elo"] = Evaluations("Elo", 0.9)
        };

        return (byModel, GradedMatches.ToDictionary(match => match.MatchId, match => (match.HomeTeamId, match.AwayTeamId)));
    }

    private static double MeanOf(IReadOnlyList<MatchEvaluation> evaluations, params int[] matchIds)
    {
        return evaluations.Where(e => matchIds.Contains(e.MatchId)).Average(e => e.Brier);
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
