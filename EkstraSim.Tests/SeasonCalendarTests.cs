using EkstraSim.Prediction.Evaluation;
using EkstraSim.Prediction.Models;

namespace EkstraSim.Tests;

public class SeasonCalendarTests
{
    private static MatchData OnDate(int id, int round, DateTime date)
    {
        return new MatchData(id, date, round, TestLeague.SeasonId, TestLeague.LeagueId, 100 + round, 200 + round, 1, 1);
    }

    private static List<MatchData> SeasonWithWinterBreak()
    {
        var matches = new List<MatchData>();
        var id = 1;
        var date = new DateTime(2024, 7, 20);

        for (var round = 1; round <= 19; round++)
        {
            matches.Add(OnDate(id++, round, date));
            date = date.AddDays(7);
        }

        date = new DateTime(2025, 2, 7);

        for (var round = 20; round <= 34; round++)
        {
            matches.Add(OnDate(id++, round, date));
            date = date.AddDays(7);
        }

        return matches;
    }

    [Fact]
    public void FindsWinterBreakAsWidestGap()
    {
        var split = SeasonCalendar.DetectSplit(SeasonWithWinterBreak());

        Assert.Equal(19, split.AutumnLastRound);
        Assert.Equal(20, split.SpringFirstRound);
        Assert.True(split.BreakDays > 60);
    }

    [Fact]
    public void IgnoresRegularWeeklySpacing()
    {
        var matches = new List<MatchData>();
        var date = new DateTime(2024, 7, 20);

        for (var round = 1; round <= 10; round++)
        {
            matches.Add(OnDate(round, round, date));
            date = date.AddDays(7);
        }

        var split = SeasonCalendar.DetectSplit(matches);

        Assert.Equal(7, split.BreakDays);
    }

    [Fact]
    public void HandlesRoundsSpreadOverSeveralDays()
    {
        List<MatchData> matches =
        [
            OnDate(1, 1, new DateTime(2024, 7, 19)),
            OnDate(2, 1, new DateTime(2024, 7, 21)),
            OnDate(3, 2, new DateTime(2024, 7, 26)),
            OnDate(4, 2, new DateTime(2024, 7, 28)),
            OnDate(5, 3, new DateTime(2024, 12, 1))
        ];

        var split = SeasonCalendar.DetectSplit(matches);

        Assert.Equal(2, split.AutumnLastRound);
        Assert.Equal(3, split.SpringFirstRound);
    }

    [Fact]
    public void SingleRoundHasNoSplit()
    {
        var split = SeasonCalendar.DetectSplit([OnDate(1, 1, new DateTime(2024, 7, 19))]);

        Assert.Null(split.AutumnLastRound);
        Assert.Null(split.SpringFirstRound);
        Assert.Null(split.BreakDays);
    }

    [Fact]
    public void EmptySeasonHasNoSplit()
    {
        var split = SeasonCalendar.DetectSplit([]);

        Assert.Null(split.AutumnLastRound);
        Assert.Null(split.BreakDays);
    }

    [Fact]
    public void MatchesWithoutRoundAreIgnored()
    {
        List<MatchData> matches =
        [
            new(1, new DateTime(2024, 7, 19), null, TestLeague.SeasonId, TestLeague.LeagueId, 100, 200, 1, 0),
            OnDate(2, 1, new DateTime(2024, 8, 1)),
            OnDate(3, 2, new DateTime(2024, 8, 8))
        ];

        var split = SeasonCalendar.DetectSplit(matches);

        Assert.Equal(1, split.AutumnLastRound);
        Assert.Equal(7, split.BreakDays);
    }

    [Fact]
    public void RoundsInOrderIsSortedAndDistinct()
    {
        List<MatchData> matches =
        [
            OnDate(1, 3, new DateTime(2024, 8, 1)),
            OnDate(2, 1, new DateTime(2024, 7, 20)),
            OnDate(3, 3, new DateTime(2024, 8, 2)),
            OnDate(4, 2, new DateTime(2024, 7, 27))
        ];

        Assert.Equal([1, 2, 3], SeasonCalendar.RoundsInOrder(matches));
    }

    [Fact]
    public void TeamIdsCollectsBothSides()
    {
        List<MatchData> matches =
        [
            new(1, new DateTime(2024, 7, 20), 1, TestLeague.SeasonId, TestLeague.LeagueId, 100, 200, 1, 0),
            new(2, new DateTime(2024, 7, 27), 2, TestLeague.SeasonId, TestLeague.LeagueId, 200, 300, 2, 2)
        ];

        Assert.Equal([100, 200, 300], SeasonCalendar.TeamIds(matches));
    }
}
