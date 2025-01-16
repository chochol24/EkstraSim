namespace EkstraSim.Backend.Database.Entities;

public class Match
{
	//Basics
	public int Id { get; set; }
	public DateTime Date { get; set; }

	//Season informations
	public int? SeasonId { get; set; }
	public Season? Season { get; set; }
	public int? LeagueId { get; set; }
	public League? League { get; set; }
	public int? Round { get; set; }

	//Teams
	public int HomeTeamId { get; set; }
	public Team HomeTeam { get; set; } = null!;
	public int AwayTeamId { get; set; }
	public Team AwayTeam { get; set; } = null!;

	//Results
	public int? HomeTeamScore { get; set; }
	public int? AwayTeamScore { get; set; }
	public string? DisplayResult => HomeTeamScore.HasValue && AwayTeamScore.HasValue
		? $"{HomeTeam.Name} {HomeTeamScore}:{AwayTeamScore} {AwayTeam.Name}"
		: null;

	/*
	 * Match might be friendly - null Season, League, Round
	 * Match might be in future
	 */
}

