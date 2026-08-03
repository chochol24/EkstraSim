namespace EkstraSim.Prediction.Metrics;

public sealed class MatchEvaluation
{
    public int MatchId { get; init; }
    public string ModelName { get; init; } = string.Empty;
    public int? Round { get; init; }
    public bool InvolvesPromotedTeam { get; init; }

    public MatchOutcome Actual { get; init; }
    public MatchOutcome Predicted { get; init; }

    public double Brier { get; init; }
    public double RankedProbability { get; init; }
    public double LogLoss { get; init; }

    public bool OutcomeCorrect { get; init; }
    public bool ExactScoreCorrect { get; init; }
    public bool ExactScoreInTopThree { get; init; }

    public double ProbabilityOfActualScore { get; init; }
    public double ProbabilityOfActualOutcome { get; init; }

    public MatchEvaluation WithContext(int? round, bool involvesPromotedTeam)
    {
        return new MatchEvaluation
        {
            MatchId = MatchId,
            ModelName = ModelName,
            Round = round,
            InvolvesPromotedTeam = involvesPromotedTeam,
            Actual = Actual,
            Predicted = Predicted,
            Brier = Brier,
            RankedProbability = RankedProbability,
            LogLoss = LogLoss,
            OutcomeCorrect = OutcomeCorrect,
            ExactScoreCorrect = ExactScoreCorrect,
            ExactScoreInTopThree = ExactScoreInTopThree,
            ProbabilityOfActualScore = ProbabilityOfActualScore,
            ProbabilityOfActualOutcome = ProbabilityOfActualOutcome
        };
    }
}
