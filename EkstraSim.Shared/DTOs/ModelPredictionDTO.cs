namespace EkstraSim.Shared.DTOs;

public class ModelPredictionDTO
{
    public int Id { get; set; }
    public int ModelEvaluationRunId { get; set; }
    public string ModelName { get; set; } = string.Empty;

    public int MatchId { get; set; }
    public MatchDTO? Match { get; set; }
    public int? Round { get; set; }
    public bool InvolvesPromotedTeam { get; set; }

    public double ExpectedHomeGoals { get; set; }
    public double ExpectedAwayGoals { get; set; }

    public double HomeWinProbability { get; set; }
    public double DrawProbability { get; set; }
    public double AwayWinProbability { get; set; }

    public int PredictedHomeScore { get; set; }
    public int PredictedAwayScore { get; set; }
    public string? ResultProbabilityMatrixJson { get; set; }

    public int ActualHomeScore { get; set; }
    public int ActualAwayScore { get; set; }

    public double Brier { get; set; }
    public double RankedProbabilityScore { get; set; }
    public double LogLoss { get; set; }

    public bool OutcomeCorrect { get; set; }
    public bool ExactScoreCorrect { get; set; }
    public bool ExactScoreInTopThree { get; set; }

    public double ProbabilityOfActualScore { get; set; }
    public double ProbabilityOfActualOutcome { get; set; }
}
