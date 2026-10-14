using EkstraSim.Prediction.Evaluation;
using EkstraSim.Prediction.Models;

namespace EkstraSim.Tests;

public class ConstantScoreBaselineTests
{
    private const int OtherLeagueId = TestLeague.LeagueId + 1;

    [Fact]
    public void MostFrequentScoreComesFromPlayedMatchesOfTheLeagueOnly()
    {
        List<MatchData> history =
        [
            TestLeague.Played(1, 1, 100, 200, 1, 1),
            TestLeague.Played(2, 2, 200, 100, 1, 1),
            TestLeague.Played(3, 3, 100, 300, 2, 1),
            TestLeague.Played(4, 4, 300, 100, 0, 0) with { LeagueId = OtherLeagueId },
            TestLeague.Played(5, 5, 200, 300, 0, 0) with { LeagueId = OtherLeagueId },
            TestLeague.Played(6, 6, 300, 200, 0, 0) with { LeagueId = OtherLeagueId },
            TestLeague.Fixture(7, 7, 100, 300),
            TestLeague.Fixture(8, 8, 300, 200)
        ];

        Assert.Equal((1, 1), ConstantScoreBaseline.Fit(history, TestLeague.LeagueId));
    }

    [Fact]
    public void TiesGoToFewerGoalsThenFewerHomeGoals()
    {
        List<MatchData> history =
        [
            TestLeague.Played(1, 1, 100, 200, 2, 2),
            TestLeague.Played(2, 2, 200, 100, 2, 2),
            TestLeague.Played(3, 3, 100, 300, 1, 0),
            TestLeague.Played(4, 4, 300, 100, 1, 0),
            TestLeague.Played(5, 5, 200, 300, 0, 1),
            TestLeague.Played(6, 6, 300, 200, 0, 1)
        ];

        Assert.Equal((0, 1), ConstantScoreBaseline.Fit(history, TestLeague.LeagueId));
    }

    [Fact]
    public void FitRequiresPlayedLeagueMatches()
    {
        List<MatchData> history =
        [
            TestLeague.Played(1, 1, 100, 200, 2, 1) with { LeagueId = OtherLeagueId },
            TestLeague.Fixture(2, 2, 200, 100)
        ];

        Assert.Throws<ArgumentException>(() => ConstantScoreBaseline.Fit(history, TestLeague.LeagueId));
    }

    [Fact]
    public void EvaluateMarksHitsInMatchIdOrderAndSkipsUnplayedMatches()
    {
        List<MatchData> evaluation =
        [
            TestLeague.Played(30, 21, 100, 200, 1, 1),
            TestLeague.Played(10, 22, 300, 400, 2, 1),
            TestLeague.Fixture(5, 22, 400, 100),
            TestLeague.Played(20, 23, 400, 300, 1, 1)
        ];

        var hits = ConstantScoreBaseline.Evaluate((1, 1), evaluation);

        Assert.Equal(
            [new ExactScoreHit(10, 22, false), new ExactScoreHit(20, 23, true), new ExactScoreHit(30, 21, true)],
            hits);
    }
}
