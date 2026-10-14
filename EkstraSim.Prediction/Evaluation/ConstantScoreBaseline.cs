using EkstraSim.Prediction.Models;

namespace EkstraSim.Prediction.Evaluation;

public sealed record ExactScoreHit(int MatchId, int? Round, bool Correct);

public static class ConstantScoreBaseline
{
    public static (int Home, int Away) Fit(IReadOnlyList<MatchData> history, int leagueId)
    {
        var played = history.Where(m => m.LeagueId == leagueId && m.IsPlayed).ToList();

        if (played.Count == 0)
        {
            throw new ArgumentException($"Brak rozegranych meczów ligi {leagueId} w historii.", nameof(history));
        }

        return played
            .GroupBy(m => (Home: m.HomeScore!.Value, Away: m.AwayScore!.Value))
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key.Home + group.Key.Away)
            .ThenBy(group => group.Key.Home)
            .First()
            .Key;
    }

    public static IReadOnlyList<ExactScoreHit> Evaluate((int Home, int Away) score, IReadOnlyList<MatchData> evaluation)
    {
        return evaluation
            .Where(m => m.IsPlayed)
            .OrderBy(m => m.Id)
            .Select(match => new ExactScoreHit(
                match.Id,
                match.Round,
                match.HomeScore == score.Home && match.AwayScore == score.Away))
            .ToList();
    }
}
