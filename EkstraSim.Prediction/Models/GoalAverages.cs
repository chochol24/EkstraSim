namespace EkstraSim.Prediction.Models;

public sealed class GoalAverages
{
    public double HomeScored { get; init; }
    public double HomeConceded { get; init; }
    public double AwayScored { get; init; }
    public double AwayConceded { get; init; }

    public static readonly GoalAverages Neutral = new()
    {
        HomeScored = 1.5,
        HomeConceded = 1.2,
        AwayScored = 1.2,
        AwayConceded = 1.5
    };

    public static GoalAverages ForLeague(IReadOnlyList<MatchData> matches)
    {
        if (matches.Count == 0)
        {
            return Neutral;
        }

        return new GoalAverages
        {
            HomeScored = matches.Average(m => m.HomeScore.GetValueOrDefault()),
            HomeConceded = matches.Average(m => m.AwayScore.GetValueOrDefault()),
            AwayScored = matches.Average(m => m.AwayScore.GetValueOrDefault()),
            AwayConceded = matches.Average(m => m.HomeScore.GetValueOrDefault())
        };
    }

    public static GoalAverages ForTeam(int teamId, IReadOnlyList<MatchData> matches, GoalAverages fallback)
    {
        var homeMatches = matches.Where(m => m.HomeTeamId == teamId).ToList();
        var awayMatches = matches.Where(m => m.AwayTeamId == teamId).ToList();

        return new GoalAverages
        {
            HomeScored = homeMatches.Count > 0 ? homeMatches.Average(m => m.HomeScore.GetValueOrDefault()) : fallback.HomeScored,
            HomeConceded = homeMatches.Count > 0 ? homeMatches.Average(m => m.AwayScore.GetValueOrDefault()) : fallback.HomeConceded,
            AwayScored = awayMatches.Count > 0 ? awayMatches.Average(m => m.AwayScore.GetValueOrDefault()) : fallback.AwayScored,
            AwayConceded = awayMatches.Count > 0 ? awayMatches.Average(m => m.HomeScore.GetValueOrDefault()) : fallback.AwayConceded
        };
    }
}
