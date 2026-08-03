using EkstraSim.Prediction.Models;

namespace EkstraSim.Prediction.Evaluation;

public sealed record SeasonSplit(int? AutumnLastRound, int? SpringFirstRound, double? BreakDays);

public static class SeasonCalendar
{
    public static SeasonSplit DetectSplit(IReadOnlyList<MatchData> seasonMatches)
    {
        var rounds = seasonMatches
            .Where(m => m.Round.HasValue)
            .GroupBy(m => m.Round!.Value)
            .Select(g => new
            {
                Round = g.Key,
                First = g.Min(m => m.Date),
                Last = g.Max(m => m.Date)
            })
            .OrderBy(r => r.Round)
            .ToList();

        if (rounds.Count < 2)
        {
            return new SeasonSplit(null, null, null);
        }

        var widestGap = double.MinValue;
        var autumnLastRound = rounds[0].Round;
        var springFirstRound = rounds[1].Round;

        for (var i = 0; i < rounds.Count - 1; i++)
        {
            var gap = (rounds[i + 1].First - rounds[i].Last).TotalDays;
            if (gap > widestGap)
            {
                widestGap = gap;
                autumnLastRound = rounds[i].Round;
                springFirstRound = rounds[i + 1].Round;
            }
        }

        return new SeasonSplit(autumnLastRound, springFirstRound, widestGap);
    }

    public static List<int> RoundsInOrder(IReadOnlyList<MatchData> matches)
    {
        return matches
            .Where(m => m.Round.HasValue)
            .Select(m => m.Round!.Value)
            .Distinct()
            .OrderBy(round => round)
            .ToList();
    }

    public static List<int> TeamIds(IReadOnlyList<MatchData> matches)
    {
        return matches
            .SelectMany(m => new[] { m.HomeTeamId, m.AwayTeamId })
            .Distinct()
            .OrderBy(id => id)
            .ToList();
    }
}
