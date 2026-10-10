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

    public static bool TryParseName(string? name, out MetricKind metric)
    {
        metric = MetricKind.RankedProbability;

        if (string.IsNullOrWhiteSpace(name))
        {
            return true;
        }

        foreach (var candidate in Enum.GetValues<MetricKind>())
        {
            if (string.Equals(candidate.ToString(), name.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                metric = candidate;
                return true;
            }
        }

        return false;
    }
}
