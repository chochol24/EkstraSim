namespace EkstraSim.Shared.DTOs;

public class SimulatedMatchResultDTO
{
    public int Id { get; set; }
    public int MatchId { get; set; }
    public MatchDTO Match { get; set; } = null!;
    public int? Round { get; set; }
    public int? SeasonId { get; set; }
    public SeasonDTO? Season { get; set; }
    public int? LeagueId { get; set; }
    public LeagueDTO? League { get; set; }


    public int PredictedHomeScore { get; set; }
    public int PredictedAwayScore { get; set; }

    public double HomeWinProbability { get; set; }
    public double DrawProbability { get; set; }
    public double AwayWinProbability { get; set; }

    public int NumberOfSimulations { get; set; }

    public int? SimulatedRoundId { get; set; } 
    public SimulatedRoundDTO SimulatedRound { get; set; } = null!;
}
