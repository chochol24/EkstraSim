namespace EkstraSim.Shared.DTOs;

public class SimulatedRoundDTO
{

    public int Id { get; set; }
    public int LeagueId { get; set; }
    public LeagueDTO League { get; set; } = null!;
    public int SeasonId { get; set; }
    public SeasonDTO Season { get; set; } = null!;
    public int Round { get; set; }

    public int NumberOfSimulations { get; set; }

    public List<SimulatedMatchResultDTO> SimulatedMatchResults { get; set; } = new();
}
