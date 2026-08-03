namespace EkstraSim.Shared.DTOs;

public class ModelRoundMetricDTO
{
    public int Id { get; set; }
    public int ModelEvaluationRunId { get; set; }
    public string ModelName { get; set; } = string.Empty;

    public int Round { get; set; }
    public int MatchCount { get; set; }

    public double Brier { get; set; }
    public double RankedProbabilityScore { get; set; }
    public double LogLoss { get; set; }

    public double OutcomeAccuracy { get; set; }
    public double ExactScoreAccuracy { get; set; }
    public double ExactScoreTopThreeAccuracy { get; set; }

    public double MeanProbabilityOfActualScore { get; set; }
    public double ParameterDrift { get; set; }
}
