namespace EkstraSim.Shared.DTOs;

public class MatchDTO
{
    public int Id { get; set; }
    public DateTime Date { get; set; }
    public int? SeasonId { get; set; }
    public SeasonDTO? Season { get; set; }
    public int? LeagueId { get; set; }
    public LeagueDTO? League { get; set; }
    public int? Round { get; set; }

    public int HomeTeamId { get; set; }
    public TeamDTO HomeTeam { get; set; } = null!;
    public int AwayTeamId { get; set; }
    public TeamDTO AwayTeam { get; set; } = null!;

    public int? HomeTeamScore { get; set; }
    public int? AwayTeamScore { get; set; }
    public string? DisplayResult => HomeTeamScore.HasValue && AwayTeamScore.HasValue
        ? $"{HomeTeam.Name} {HomeTeamScore}:{AwayTeamScore} {AwayTeam.Name}"
        : null;

}
