namespace EkstraSim.Backend.Database.Entities;

public class SimulatedRound
{

    public int Id { get; set; }
    public int LeagueId { get; set; }
    public League League { get; set; } = null!;
    public int SeasonId { get; set; }
    public Season Season { get; set; } = null!;
    public int Round { get; set; }

    public int NumberOfSimulations { get; set; }

    public List<SimulatedMatchResult> SimulatedMatchResults { get; set; } = new();
    public DateTime? SimulationDate { get; set; }
}
