namespace EkstraSim.Shared.Requests;

public class SimulateRoundRequest()
{
    public int Round { get; set; } = 0;
    public int SeasonId { get; set; } = 0;
    public int LeagueId { get; set; } = 0;
    public int NumberOfSimualtions { get; set; } = 0;
}

