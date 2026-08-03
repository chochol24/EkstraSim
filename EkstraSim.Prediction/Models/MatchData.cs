namespace EkstraSim.Prediction.Models;

public sealed record MatchData(
    int Id,
    DateTime Date,
    int? Round,
    int? SeasonId,
    int? LeagueId,
    int HomeTeamId,
    int AwayTeamId,
    int? HomeScore,
    int? AwayScore)
{
    public bool IsPlayed => HomeScore.HasValue && AwayScore.HasValue;

    public bool Involves(int teamId) => HomeTeamId == teamId || AwayTeamId == teamId;

    public int GoalsScoredBy(int teamId) => HomeTeamId == teamId
        ? HomeScore.GetValueOrDefault()
        : AwayScore.GetValueOrDefault();

    public int GoalsConcededBy(int teamId) => HomeTeamId == teamId
        ? AwayScore.GetValueOrDefault()
        : HomeScore.GetValueOrDefault();
}
