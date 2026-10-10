using EkstraSim.Prediction.Evaluation;
using EkstraSim.Prediction.Models;

namespace EkstraSim.Tests;

public class PromotedTeamHistoryTests
{
    private const int League = 1;
    private const int OtherLeague = 2;
    private const int Promoted = 100;

    private static readonly List<int> Chronology = [5, 2, 7, 3];

    [Theory]
    [InlineData(0, PromotedCategory.ColdStart)]
    [InlineData(1, PromotedCategory.Returning)]
    [InlineData(135, PromotedCategory.Returning)]
    public void CategoryFollowsThePriorMatchCount(int priorMatchCount, PromotedCategory expected)
    {
        Assert.Equal(expected, PromotedTeamHistory.CategoryOf(priorMatchCount));
        Assert.Equal(expected, new PromotedTeamHistory(Promoted, priorMatchCount, null).Category);
    }

    [Fact]
    public void CategoryRejectsANegativeCount()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PromotedTeamHistory.CategoryOf(-1));
    }

    [Fact]
    public void TeamWithoutEarlierMatchesHasNoHistory()
    {
        List<MatchData> matches =
        [
            Match(1, 5, 1, 2),
            Match(2, 7, Promoted, 1)
        ];

        var history = Assert.Single(PromotedTeamHistory.Compute(matches, Chronology, League, 7, [Promoted]));

        Assert.Equal(new PromotedTeamHistory(Promoted, 0, null), history);
        Assert.Equal(PromotedCategory.ColdStart, history.Category);
    }

    [Fact]
    public void OnlyPlayedLeagueMatchesFromEarlierSeasonsCount()
    {
        List<MatchData> matches =
        [
            Match(1, 5, Promoted, 1),
            Match(2, 2, 2, Promoted),
            Match(3, 2, Promoted, 3, played: false),
            Match(4, 2, Promoted, 4, leagueId: OtherLeague),
            Match(5, 7, Promoted, 1),
            Match(6, 3, Promoted, 2),
            Match(7, 2, 1, 2)
        ];

        var history = Assert.Single(PromotedTeamHistory.Compute(matches, Chronology, League, 7, [Promoted]));

        Assert.Equal(2, history.PriorMatchCount);
        Assert.Equal(PromotedCategory.Returning, history.Category);
    }

    [Fact]
    public void LastSeasonIsTheLatestInChronologyNotTheHighestId()
    {
        List<MatchData> matches =
        [
            Match(1, 5, Promoted, 1),
            Match(2, 5, 2, Promoted),
            Match(3, 2, Promoted, 3),
            Match(4, 3, Promoted, 4)
        ];

        var beforeSeven = Assert.Single(PromotedTeamHistory.Compute(matches, Chronology, League, 7, [Promoted]));
        var beforeTwo = Assert.Single(PromotedTeamHistory.Compute(matches, Chronology, League, 2, [Promoted]));

        Assert.Equal(new PromotedTeamHistory(Promoted, 3, 2), beforeSeven);
        Assert.Equal(new PromotedTeamHistory(Promoted, 2, 5), beforeTwo);
    }

    [Fact]
    public void HistoriesFollowTheOrderOfRequestedTeams()
    {
        List<MatchData> matches =
        [
            Match(1, 5, 1, 2),
            Match(2, 2, 1, 3)
        ];

        var histories = PromotedTeamHistory.Compute(matches, Chronology, League, 7, [3, Promoted, 1]);

        Assert.Equal(
            [new PromotedTeamHistory(3, 1, 2), new PromotedTeamHistory(Promoted, 0, null), new PromotedTeamHistory(1, 2, 2)],
            histories);
    }

    [Fact]
    public void FirstSeasonInChronologyHasNoHistory()
    {
        List<MatchData> matches = [Match(1, 5, Promoted, 1)];

        var history = Assert.Single(PromotedTeamHistory.Compute(matches, Chronology, League, 5, [Promoted]));

        Assert.Equal(0, history.PriorMatchCount);
    }

    [Fact]
    public void SeasonOutsideTheChronologyIsRejected()
    {
        Assert.Throws<ArgumentException>(() => PromotedTeamHistory.Compute([], Chronology, League, 9, [Promoted]));
    }

    private static MatchData Match(int id, int seasonId, int homeTeamId, int awayTeamId, bool played = true, int leagueId = League)
    {
        return new MatchData(
            id,
            new DateTime(2020, 1, 1).AddDays(id),
            1,
            seasonId,
            leagueId,
            homeTeamId,
            awayTeamId,
            played ? 1 : null,
            played ? 0 : null);
    }
}
