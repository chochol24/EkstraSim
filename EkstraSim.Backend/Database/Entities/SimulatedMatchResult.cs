namespace EkstraSim.Backend.Database.Entities;

public class SimulatedMatchResult
{
	public int Id { get; set; }
	public int MatchId { get; set; } 
	public Match Match { get; set; } = null!;
	public int? Round { get; set; }
	public int? SeasonId { get; set; }
	public Season? Season { get; set; }
	public int? LeagueId { get; set; }
	public League? League { get; set; }


	public int PredictedHomeScore { get; set; }
	public int PredictedAwayScore { get; set; }

	public double HomeWinProbability { get; set; }
	public double DrawProbability { get; set; }
	public double AwayWinProbability { get; set; }

	public int NumberOfSimulations { get; set; }

    public int? SimulatedRoundId { get; set; } // Klucz obcy
    public SimulatedRound SimulatedRound { get; set; } = null!;
}
