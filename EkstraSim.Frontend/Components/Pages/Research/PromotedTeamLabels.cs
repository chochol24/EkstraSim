using EkstraSim.Shared.DTOs;

namespace EkstraSim.Frontend.Components.Pages.Research;

public static class PromotedTeamLabels
{
    public static string WithHistory(PromotedTeamDTO team)
    {
        if (!team.PriorMatchCount.HasValue)
        {
            return team.Name;
        }

        var matches = MatchCount(team.PriorMatchCount.Value);

        return team.LastPriorSeasonName == null
            ? $"{team.Name} ({matches})"
            : $"{team.Name} ({matches}, ostatnio {team.LastPriorSeasonName})";
    }

    public static string MatchCount(int count)
    {
        var noun = count == 1
            ? "mecz"
            : count % 10 is >= 2 and <= 4 && count % 100 is < 12 or > 14
                ? "mecze"
                : "meczów";

        return $"{count} {noun}";
    }

    public static string Category(string? category) => category switch
    {
        "ColdStart" => "zimny start",
        "Returning" => "powracający",
        _ => "—"
    };
}
