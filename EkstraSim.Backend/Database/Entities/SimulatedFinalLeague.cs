namespace EkstraSim.Backend.Database.Entities;

public class SimulatedFinalLeague
{
	public int Id { get; set; }
	public int SeasonId { get; set; }
	public Season Season { get; set; } = null!;
	public int LeagueId { get; set; }
	public League League { get; set; } = null!;
	public int RoundBeforeSimulation { get; set; }

	public List<SimulatedTeamInFinalTable> Teams { get; set; } = new();

	public int NumberOfSimulations { get; set; }
}
