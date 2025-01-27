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

    public double? AverageHomeGoalsScoredCurrentSeason { get; set; }
    public double? AverageAwayGoalsScoredCurrentSeason { get; set; }
    public double? AverageHomeGoalsConcededCurrentSeason { get; set; }
    public double? AverageAwayGoalsConcededCurrentSeason { get; set; }

    public double? AverageHomeGoalsScoredPreviousSeason { get; set; }
    public double? AverageAwayGoalsScoredPreviousSeason { get; set; }
    public double? AverageHomeGoalsConcededPreviousSeason { get; set; }
    public double? AverageAwayGoalsConcededPreviousSeason { get; set; }

    public double? AverageHomeGoalsScoredHistorical { get; set; }
    public double? AverageAwayGoalsScoredHistorical { get; set; }
    public double? AverageHomeGoalsConcededHistorical { get; set; }
    public double? AverageAwayGoalsConcededHistorical { get; set; }


    public ICollection<Match> HomeMatches { get; set; } = new List<Match>();
	public ICollection<Match> AwayMatches { get; set; } = new List<Match>();
}
