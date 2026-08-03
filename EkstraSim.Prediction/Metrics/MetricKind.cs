namespace EkstraSim.Prediction.Metrics;

public enum MetricKind
{
    Brier = 0,
    RankedProbability = 1,
    LogLoss = 2,
    ProbabilityOfActualScore = 3
}

public static class MetricKindExtensions
{
    public static double ValueOf(this MetricKind metric, MatchEvaluation evaluation) => metric switch
    {
        MetricKind.Brier => evaluation.Brier,
        MetricKind.RankedProbability => evaluation.RankedProbability,
        MetricKind.LogLoss => evaluation.LogLoss,
        _ => evaluation.ProbabilityOfActualScore
    };

    public static bool LowerIsBetter(this MetricKind metric) => metric != MetricKind.ProbabilityOfActualScore;
}
