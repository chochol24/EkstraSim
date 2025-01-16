namespace EkstraSim.Backend.Database.Entities;

public class League
{
	public int Id { get; set; }
	public string Name { get; set; } = string.Empty;

	public double? AverageHomeGoalsScored { get; set; }
	public double? AverageAwayGoalsScored { get; set; }
	public double? AverageHomeGoalsConceded { get; set; }
	public double? AverageAwayGoalsConceded { get; set; }

	public ICollection<Season> Seasons { get; set; } = new List<Season>();
	public ICollection<Match> Matches { get; set; } = new List<Match>();
}
