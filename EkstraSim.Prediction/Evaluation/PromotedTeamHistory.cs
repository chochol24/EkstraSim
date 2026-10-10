using EkstraSim.Prediction.Models;

namespace EkstraSim.Prediction.Evaluation;

public enum PromotedCategory
{
    ColdStart = 0,
    Returning = 1
}

public sealed record PromotedTeamHistory(int TeamId, int PriorMatchCount, int? LastPriorSeasonId)
{
    public PromotedCategory Category => CategoryOf(PriorMatchCount);

    public static PromotedCategory CategoryOf(int priorMatchCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(priorMatchCount);

        return priorMatchCount == 0 ? PromotedCategory.ColdStart : PromotedCategory.Returning;
    }

    public static IReadOnlyList<PromotedTeamHistory> Compute(
        IReadOnlyList<MatchData> matches,
        IReadOnlyList<int> seasonChronology,
        int leagueId,
        int seasonId,
        IReadOnlyList<int> teamIds)
    {
        var targetIndex = seasonChronology.ToList().IndexOf(seasonId);

        if (targetIndex < 0)
        {
            throw new ArgumentException($"Sezon {seasonId} nie występuje w chronologii ligi {leagueId}.", nameof(seasonId));
        }

        var positionBySeason = seasonChronology
            .Take(targetIndex)
            .Select((id, position) => (Id: id, Position: position))
            .ToDictionary(season => season.Id, season => season.Position);

        var earlierMatches = matches
            .Where(m => m.IsPlayed && m.LeagueId == leagueId && m.SeasonId.HasValue && positionBySeason.ContainsKey(m.SeasonId.Value))
            .ToList();

        return teamIds
            .Select(teamId =>
            {
                var played = earlierMatches.Where(m => m.Involves(teamId)).ToList();
                var lastSeasonId = played.Count > 0
                    ? played.MaxBy(m => positionBySeason[m.SeasonId!.Value])!.SeasonId
                    : null;

                return new PromotedTeamHistory(teamId, played.Count, lastSeasonId);
            })
            .ToList();
    }
}
