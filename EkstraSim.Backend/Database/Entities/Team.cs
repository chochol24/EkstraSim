namespace EkstraSim.Backend.Database.Entities;

public class Team
{
	public int Id { get; set; }
	public string Name { get; set; } = string.Empty;
	public decimal ELO { get; set; }

	public double? AverageHomeGoalsScored { get; set; }
	public double? AverageAwayGoalsScored { get; set; }
	public double? AverageHomeGoalsConceded { get; set; }
	public double? AverageAwayGoalsConceded { get; set; }

	public ICollection<Match> HomeMatches { get; set; } = new List<Match>();
	public ICollection<Match> AwayMatches { get; set; } = new List<Match>();
}
