using EkstraSim.Backend.Database.Entities;
using EkstraSim.Prediction.Models;

namespace EkstraSim.Backend.Database.Services.Research;

public static class MatchDataMapper
{
    public static MatchData ToMatchData(this Match match) => new(
        match.Id,
        match.Date,
        match.Round,
        match.SeasonId,
        match.LeagueId,
        match.HomeTeamId,
        match.AwayTeamId,
        match.HomeTeamScore,
        match.AwayTeamScore);

    public static List<MatchData> ToMatchData(this IEnumerable<Match> matches) => matches.Select(ToMatchData).ToList();
}
