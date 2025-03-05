namespace EkstraSim.Shared.DTOs;

public class SimulatedFinalLeagueDTO
{
    public int Id { get; set; }
    public int SeasonId { get; set; }
    public SeasonDTO Season { get; set; } = null!;
    public int LeagueId { get; set; }
    public LeagueDTO League { get; set; } = null!;
    public int RoundBeforeSimulation { get; set; }

    public List<SimulatedTeamInFinalTableDTO> Teams { get; set; } = new();

    public int NumberOfSimulations { get; set; }
    public DateTime? SimulationDate { get; set; }
}
