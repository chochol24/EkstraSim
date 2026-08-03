namespace EkstraSim.Shared.Requests;

public record ResearchRunRequest(int RunId);

public record ResearchRunMetricRequest(int RunId, string? Metric, string? ModelName);

public class PredictRoundRequest
{
    public int LeagueId { get; set; }
    public int SeasonId { get; set; }
    public int Round { get; set; }
    public string ModelName { get; set; } = string.Empty;
    public int? TrainingLastRound { get; set; }
    public bool UseFormFactors { get; set; } = true;
    public double TimeDecayXi { get; set; } = 0.0065;
    public double RidgeLambda { get; set; } = 0.05;
}
