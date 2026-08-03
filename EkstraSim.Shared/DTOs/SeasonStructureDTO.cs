namespace EkstraSim.Shared.DTOs;

public class SeasonStructureDTO
{
    public int SeasonId { get; set; }
    public string SeasonName { get; set; } = string.Empty;
    public int LeagueId { get; set; }

    public int TeamCount { get; set; }
    public int RoundCount { get; set; }
    public int PlayedMatchCount { get; set; }
    public int UnplayedMatchCount { get; set; }

    public int? AutumnLastRound { get; set; }
    public int? SpringFirstRound { get; set; }
    public double? WinterBreakDays { get; set; }

    public List<PromotedTeamDTO> PromotedTeams { get; set; } = [];
    public int? PreviousSeasonId { get; set; }
}

public class PromotedTeamDTO
{
    public int TeamId { get; set; }
    public string Name { get; set; } = string.Empty;
}
