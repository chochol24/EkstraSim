using System.Globalization;
using EkstraSim.Prediction.Evaluation;
using EkstraSim.Prediction.Metrics;
using EkstraSim.Prediction.Models;
using EkstraSim.Prediction.Statistics;
using Xunit.Abstractions;

namespace EkstraSim.Tests;

public class PromotedTeamRealDataTests
{
    private static readonly Dictionary<int, Lazy<SpringEvaluation>> Springs = new()
    {
        [RealDataFixture.Season2425] = new(() => EvaluateSpring(RealDataFixture.Season2425)),
        [RealDataFixture.Season2526] = new(() => EvaluateSpring(RealDataFixture.Season2526))
    };

    private readonly ITestOutputHelper _output;

    public PromotedTeamRealDataTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Theory]
    [Trait("Category", "RealData")]
    [InlineData(RealDataFixture.Season2425, new[] { 22, 28, 29 })]
    [InlineData(RealDataFixture.Season2526, new[] { 20, 23, 27 })]
    public void PromotedTeamsFollowTheSeasonDefinition(int seasonId, int[] expectedTeamIds)
    {
        Assert.Equal(expectedTeamIds, PromotedTeamIds(seasonId));
    }

    [Theory]
    [Trait("Category", "RealData")]
    [InlineData(RealDataFixture.Season2425, 28, 0, null)]
    [InlineData(RealDataFixture.Season2425, 29, 0, null)]
    [InlineData(RealDataFixture.Season2425, 22, 135, 2)]
    [InlineData(RealDataFixture.Season2526, 27, 37, 5)]
    [InlineData(RealDataFixture.Season2526, 23, 34, 3)]
    [InlineData(RealDataFixture.Season2526, 20, 135, 2)]
    public void PromotedTeamHistoryMatchesTheDatabase(int seasonId, int teamId, int expectedCount, int? expectedLastSeasonId)
    {
        var histories = PromotedTeamHistory.Compute(
            RealDataFixture.Matches,
            RealDataFixture.SeasonChronology,
            RealDataFixture.LeagueId,
            seasonId,
            PromotedTeamIds(seasonId));

        Assert.Equal(new PromotedTeamHistory(teamId, expectedCount, expectedLastSeasonId), histories.Single(h => h.TeamId == teamId));
    }

    [Theory]
    [Trait("Category", "RealData")]
    [InlineData(RealDataFixture.Season2425, 31, 16, 99)]
    [InlineData(RealDataFixture.Season2526, null, 45, 99)]
    public void SpringCategoryGroupsMatchTheExpectedCounts(int seasonId, int? expectedColdStart, int expectedReturning, int expectedOthers)
    {
        var spring = Springs[seasonId].Value;
        var analysis = ModelComparison.PromotedByCategory(spring.ByModel, MetricKind.RankedProbability, spring.TeamsByMatch, spring.Promoted);
        var binary = ModelComparison.PromotedVersusRest(spring.ByModel, MetricKind.RankedProbability);

        Report(seasonId, spring, analysis);

        var expectedCategories = expectedColdStart.HasValue
            ? new[] { PromotedCategory.ColdStart, PromotedCategory.Returning }
            : [PromotedCategory.Returning];

        Assert.Equal(spring.ByModel.Count * expectedCategories.Length, analysis.Comparisons.Count);
        Assert.Equal(HolmCorrection.Adjust(analysis.Comparisons.Select(c => c.Test.PValue).ToList()), analysis.Comparisons.Select(c => c.AdjustedPValue));

        foreach (var (modelName, evaluations) in spring.ByModel)
        {
            var rows = analysis.Comparisons.Where(c => c.ModelName == modelName).ToList();
            var binaryRow = binary.Single(c => c.ModelName == modelName);

            Assert.Equal(expectedCategories, rows.Select(c => c.Category!.Value));
            Assert.Equal(expectedReturning, rows.Single(c => c.Category == PromotedCategory.Returning).PromotedCount);

            if (expectedColdStart.HasValue)
            {
                Assert.Equal(expectedColdStart.Value, rows.Single(c => c.Category == PromotedCategory.ColdStart).PromotedCount);
            }

            var withoutPromoted = evaluations
                .Where(e => !spring.Promoted.Any(team => spring.TeamsByMatch[e.MatchId].HomeTeamId == team.TeamId || spring.TeamsByMatch[e.MatchId].AwayTeamId == team.TeamId))
                .Select(e => e.MatchId)
                .ToList();

            Assert.Equal(evaluations.Where(e => !e.InvolvesPromotedTeam).Select(e => e.MatchId), withoutPromoted);
            Assert.Equal(expectedOthers, withoutPromoted.Count);
            Assert.All(rows, c => Assert.Equal(expectedOthers, c.OtherCount));
            Assert.All(rows, c => Assert.Equal(binaryRow.OtherMean, c.OtherMean));
            Assert.All(rows, c => Assert.Equal(binaryRow.OtherCount, c.OtherCount));
        }
    }

    [Fact]
    [Trait("Category", "RealData")]
    public void ReturningRowEqualsTheBinaryRowWhenEveryPromotedTeamReturns()
    {
        var spring = Springs[RealDataFixture.Season2526].Value;
        Assert.All(spring.Promoted, team => Assert.Equal(PromotedCategory.Returning, team.Category));

        foreach (var metric in Enum.GetValues<MetricKind>())
        {
            var graded = ModelComparison.PromotedByCategory(spring.ByModel, metric, spring.TeamsByMatch, spring.Promoted).Comparisons;
            var binary = ModelComparison.PromotedVersusRest(spring.ByModel, metric);

            Assert.Equal(binary.Count, graded.Count);

            foreach (var (gradedRow, binaryRow) in graded.Zip(binary))
            {
                Assert.Equal(binaryRow.ModelName, gradedRow.ModelName);
                Assert.Equal(PromotedCategory.Returning, gradedRow.Category);
                Assert.Null(binaryRow.Category);
                Assert.Equal(45, gradedRow.PromotedCount);
                Assert.Equal(
                    (binaryRow.PromotedMean, binaryRow.OtherMean, binaryRow.PromotedCount, binaryRow.OtherCount, binaryRow.Test.Statistic, binaryRow.Test.PValue, binaryRow.AdjustedPValue),
                    (gradedRow.PromotedMean, gradedRow.OtherMean, gradedRow.PromotedCount, gradedRow.OtherCount, gradedRow.Test.Statistic, gradedRow.Test.PValue, gradedRow.AdjustedPValue));
            }
        }
    }

    private void Report(int seasonId, SpringEvaluation spring, PromotedCategoryAnalysis analysis)
    {
        _output.WriteLine($"Sezon {seasonId}, wiosna (kolejki {RealDataFixture.TrainingLastRound + 1}+), RPS");

        foreach (var team in spring.Promoted)
        {
            var means = analysis.TeamMetrics
                .Where(m => m.TeamId == team.TeamId)
                .Select(m => string.Format(CultureInfo.InvariantCulture, "{0} {1:F4} ({2})", m.ModelName, m.Mean, m.Count));

            _output.WriteLine($"  drużyna {team.TeamId}: {team.Category}, {team.PriorMatchCount} meczów, ostatnio {team.LastPriorSeasonId?.ToString() ?? "—"} — {string.Join(", ", means)}");
        }

        foreach (var row in analysis.Comparisons)
        {
            _output.WriteLine(string.Format(
                CultureInfo.InvariantCulture,
                "  {0,-10} {1,-9} {2:F4} ({3}) vs {4:F4} ({5}), p = {6:F4}, Holm {7:F4}",
                row.ModelName, row.Category, row.PromotedMean, row.PromotedCount, row.OtherMean, row.OtherCount, row.Test.PValue, row.AdjustedPValue));
        }
    }

    private static List<int> PromotedTeamIds(int seasonId)
    {
        var chronology = RealDataFixture.SeasonChronology.ToList();
        var previousSeasonId = chronology[chronology.IndexOf(seasonId) - 1];

        return TeamIdsIn(seasonId).Except(TeamIdsIn(previousSeasonId)).Order().ToList();
    }

    private static HashSet<int> TeamIdsIn(int seasonId)
    {
        return RealDataFixture.Matches
            .Where(m => m.LeagueId == RealDataFixture.LeagueId && m.SeasonId == seasonId)
            .SelectMany(m => new[] { m.HomeTeamId, m.AwayTeamId })
            .ToHashSet();
    }

    private static SpringEvaluation EvaluateSpring(int seasonId)
    {
        var history = WalkForwardEvaluator.BuildHistory(RealDataFixture.Matches, RealDataFixture.SeasonChronology, seasonId, RealDataFixture.TrainingLastRound);
        var evaluation = WalkForwardEvaluator.BuildEvaluationSet(RealDataFixture.Matches, seasonId, RealDataFixture.TrainingLastRound);
        var promotedIds = PromotedTeamIds(seasonId);
        var options = RealDataFixture.Options(seasonId);

        var byModel = PredictionModelFactory.AvailableModels.ToDictionary(
            modelName => modelName,
            modelName => (IReadOnlyList<MatchEvaluation>)WalkForwardEvaluator
                .Run(PredictionModelFactory.Create(modelName), history, evaluation, options, promotedIds.ToHashSet())
                .SelectMany(round => round.Evaluations)
                .ToList());

        return new SpringEvaluation(
            byModel,
            evaluation.ToDictionary(m => m.Id, m => (m.HomeTeamId, m.AwayTeamId)),
            PromotedTeamHistory.Compute(RealDataFixture.Matches, RealDataFixture.SeasonChronology, RealDataFixture.LeagueId, seasonId, promotedIds));
    }

    private sealed record SpringEvaluation(
        IReadOnlyDictionary<string, IReadOnlyList<MatchEvaluation>> ByModel,
        IReadOnlyDictionary<int, (int HomeTeamId, int AwayTeamId)> TeamsByMatch,
        IReadOnlyList<PromotedTeamHistory> Promoted);
}
