using EkstraSim.Prediction.Models;

namespace EkstraSim.Tests;

public static class TestLeague
{
    public const int LeagueId = 1;
    public const int SeasonId = 10;
    public const int PreviousSeasonId = 9;

    private static readonly DateTime Origin = new(2024, 7, 20);

    public static MatchData Played(int id, int round, int homeTeamId, int awayTeamId, int homeScore, int awayScore, int? seasonId = null)
    {
        return new MatchData(
            id,
            Origin.AddDays(round * 7),
            round,
            seasonId ?? SeasonId,
            LeagueId,
            homeTeamId,
            awayTeamId,
            homeScore,
            awayScore);
    }

    public static MatchData Fixture(int id, int round, int homeTeamId, int awayTeamId, int? seasonId = null)
    {
        return new MatchData(
            id,
            Origin.AddDays(round * 7),
            round,
            seasonId ?? SeasonId,
            LeagueId,
            homeTeamId,
            awayTeamId,
            null,
            null);
    }

    public static TrainingOptions Options(bool useFormFactors = false, IReadOnlyList<int>? chronology = null)
    {
        return new TrainingOptions
        {
            LeagueId = LeagueId,
            SeasonId = SeasonId,
            SeasonChronology = chronology ?? [PreviousSeasonId, SeasonId],
            UseFormFactors = useFormFactors
        };
    }

    public static List<MatchData> TwoTeamSeason()
    {
        return
        [
            Played(1, 1, homeTeamId: 100, awayTeamId: 200, homeScore: 2, awayScore: 1),
            Played(2, 2, homeTeamId: 200, awayTeamId: 100, homeScore: 1, awayScore: 1)
        ];
    }

    public static List<MatchData> RoundRobin(int[] teamIds, int seasonId, int firstMatchId, int goalsHome = 2, int goalsAway = 1)
    {
        var matches = new List<MatchData>();
        var id = firstMatchId;
        var round = 1;

        foreach (var home in teamIds)
        {
            foreach (var away in teamIds)
            {
                if (home == away)
                {
                    continue;
                }

                matches.Add(Played(id++, round++, home, away, goalsHome, goalsAway, seasonId));
            }
        }

        return matches;
    }
}
