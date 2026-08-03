namespace EkstraSim.Prediction.Metrics;

public sealed class MetricSummary
{
    public string ModelName { get; init; } = string.Empty;
    public int MatchCount { get; init; }

    public double Brier { get; init; }
    public double RankedProbability { get; init; }
    public double LogLoss { get; init; }

    public double OutcomeAccuracy { get; init; }
    public double ExactScoreAccuracy { get; init; }
    public double ExactScoreTopThreeAccuracy { get; init; }

    public double MeanProbabilityOfActualScore { get; init; }
    public double MeanProbabilityOfActualOutcome { get; init; }

    public static MetricSummary Empty(string modelName) => new() { ModelName = modelName };

    public static MetricSummary From(IReadOnlyList<MatchEvaluation> evaluations, string? modelName = null)
    {
        var name = modelName ?? evaluations.FirstOrDefault()?.ModelName ?? string.Empty;

        if (evaluations.Count == 0)
        {
            return Empty(name);
        }

        return new MetricSummary
        {
            ModelName = name,
            MatchCount = evaluations.Count,
            Brier = evaluations.Average(e => e.Brier),
            RankedProbability = evaluations.Average(e => e.RankedProbability),
            LogLoss = evaluations.Average(e => e.LogLoss),
            OutcomeAccuracy = evaluations.Count(e => e.OutcomeCorrect) / (double)evaluations.Count,
            ExactScoreAccuracy = evaluations.Count(e => e.ExactScoreCorrect) / (double)evaluations.Count,
            ExactScoreTopThreeAccuracy = evaluations.Count(e => e.ExactScoreInTopThree) / (double)evaluations.Count,
            MeanProbabilityOfActualScore = evaluations.Average(e => e.ProbabilityOfActualScore),
            MeanProbabilityOfActualOutcome = evaluations.Average(e => e.ProbabilityOfActualOutcome)
        };
    }
}
