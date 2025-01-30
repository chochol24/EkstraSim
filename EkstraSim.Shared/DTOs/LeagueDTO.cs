
namespace EkstraSim.Shared.DTOs;

public class LeagueDTO
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    public double? AverageHomeGoalsScored { get; set; }
    public double? AverageAwayGoalsScored { get; set; }
    public double? AverageHomeGoalsConceded { get; set; }
    public double? AverageAwayGoalsConceded { get; set; }

    public ICollection<SeasonDTO> Seasons { get; set; } = new List<SeasonDTO>();
    public ICollection<MatchDTO> Matches { get; set; } = new List<MatchDTO>();
}
