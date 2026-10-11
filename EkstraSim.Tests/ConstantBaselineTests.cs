using EkstraSim.Prediction.Evaluation;
using EkstraSim.Prediction.Metrics;
using EkstraSim.Prediction.Models;

namespace EkstraSim.Tests;

public class ConstantBaselineTests
{
    private const int OtherLeagueId = TestLeague.LeagueId + 1;

    [Fact]
    public void FrequenciesComeFromPlayedMatchesOfTheLeagueOnly()
    {
        List<MatchData> history =
        [
            TestLeague.Played(1, 1, 100, 200, 2, 1),
            TestLeague.Played(2, 2, 200, 100, 3, 0),
            TestLeague.Played(3, 3, 100, 300, 1, 0),
            TestLeague.Played(4, 4, 300, 100, 1, 1),
            TestLeague.Played(5, 5, 200, 300, 0, 0),
            TestLeague.Played(6, 6, 300, 200, 0, 2),
            TestLeague.Played(7, 7, 100, 200, 0, 3) with { LeagueId = OtherLeagueId },
            TestLeague.Played(8, 8, 200, 300, 1, 4) with { LeagueId = OtherLeagueId },
            TestLeague.Fixture(9, 9, 100, 300),
            TestLeague.Fixture(10, 10, 300, 200)
        ];

        var probabilities = ConstantBaseline.Fit(history, TestLeague.LeagueId);

        Assert.Equal(3.0 / 6, probabilities.HomeWin);
        Assert.Equal(2.0 / 6, probabilities.Draw);
        Assert.Equal(1.0 / 6, probabilities.AwayWin);
        Assert.Equal(1.0, probabilities.Total, 1e-15);
    }

    [Fact]
    public void FitRequiresPlayedLeagueMatches()
    {
        List<MatchData> history =
        [
            TestLeague.Played(1, 1, 100, 200, 2, 1) with { LeagueId = OtherLeagueId },
            TestLeague.Fixture(2, 2, 200, 100)
        ];

        Assert.Throws<ArgumentException>(() => ConstantBaseline.Fit(history, TestLeague.LeagueId));
    }

    [Fact]
    public void MetricsEqualThePredictionMetrics()
    {
        var probabilities = new OutcomeProbabilities(0.45, 0.3, 0.25);
        List<MatchData> evaluation =
        [
            TestLeague.Played(1, 20, 100, 200, 2, 0),
            TestLeague.Played(2, 20, 300, 400, 1, 1),
            TestLeague.Played(3, 21, 200, 300, 0, 1)
        ];

        var evaluations = ConstantBaseline.Evaluate(probabilities, evaluation, new HashSet<int>());

        Assert.Equal(3, evaluations.Count);

        foreach (var (match, result) in evaluation.Zip(evaluations))
        {
            var actual = PredictionMetrics.OutcomeOf(match.HomeScore!.Value, match.AwayScore!.Value);

            Assert.Equal(match.Id, result.MatchId);
            Assert.Equal(ConstantBaseline.Name, result.ModelName);
            Assert.Equal(match.Round, result.Round);
            Assert.Equal(actual, result.Actual);
            Assert.Equal(MatchOutcome.HomeWin, result.Predicted);
            Assert.Equal(actual == MatchOutcome.HomeWin, result.OutcomeCorrect);
            Assert.Equal(PredictionMetrics.BrierScore(probabilities, actual), result.Brier);
            Assert.Equal(PredictionMetrics.RankedProbabilityScore(probabilities, actual), result.RankedProbability);
            Assert.Equal(PredictionMetrics.LogLoss(probabilities, actual), result.LogLoss);
            Assert.Equal(probabilities.Of(actual), result.ProbabilityOfActualOutcome);
            Assert.False(result.ExactScoreCorrect);
            Assert.False(result.ExactScoreInTopThree);
            Assert.Equal(0, result.ProbabilityOfActualScore);
        }

        Assert.Equal([MatchOutcome.HomeWin, MatchOutcome.Draw, MatchOutcome.AwayWin], evaluations.Select(e => e.Actual));
    }

    [Fact]
    public void PromotedFlagUnplayedMatchesAndOrder()
    {
        var probabilities = new OutcomeProbabilities(0.2, 0.3, 0.5);
        List<MatchData> evaluation =
        [
            TestLeague.Played(30, 21, 100, 200, 2, 0),
            TestLeague.Played(10, 22, 300, 700, 1, 1),
            TestLeague.Fixture(5, 22, 700, 100),
            TestLeague.Played(20, 23, 700, 400, 0, 1),
            TestLeague.Played(15, 23, 200, 300, 3, 3)
        ];

        var evaluations = ConstantBaseline.Evaluate(probabilities, evaluation, new HashSet<int> { 700 });

        Assert.Equal([10, 15, 20, 30], evaluations.Select(e => e.MatchId));
        Assert.Equal([true, false, true, false], evaluations.Select(e => e.InvolvesPromotedTeam));
        Assert.Equal([22, 23, 23, 21], evaluations.Select(e => e.Round!.Value));
        Assert.All(evaluations, e => Assert.Equal(MatchOutcome.AwayWin, e.Predicted));
        Assert.Equal([false, false, true, false], evaluations.Select(e => e.OutcomeCorrect));
    }
}
