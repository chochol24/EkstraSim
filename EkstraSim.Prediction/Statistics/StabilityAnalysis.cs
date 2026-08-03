namespace EkstraSim.Prediction.Statistics;

public sealed record RoundObservation(int Round, double MetricValue, double ParameterDrift);

public sealed class StabilityResult
{
    public string ModelName { get; init; } = string.Empty;
    public int? StabilisedFromRound { get; init; }
    public double Threshold { get; init; }
    public int Window { get; init; }
    public IReadOnlyList<int> Rounds { get; init; } = [];
    public IReadOnlyList<double> RollingMetric { get; init; } = [];
    public IReadOnlyList<double> RollingDrift { get; init; } = [];
}

public static class StabilityAnalysis
{
    public const int DefaultWindow = 3;

    public static double[] RollingMean(IReadOnlyList<double> values, int window)
    {
        var effectiveWindow = Math.Max(1, window);
        var result = new double[values.Count];

        for (var i = 0; i < values.Count; i++)
        {
            var from = Math.Max(0, i - effectiveWindow + 1);
            double sum = 0;

            for (var j = from; j <= i; j++)
            {
                sum += values[j];
            }

            result[i] = sum / (i - from + 1);
        }

        return result;
    }

    public static StabilityResult Detect(
        string modelName,
        IReadOnlyList<RoundObservation> observations,
        double threshold,
        int window = DefaultWindow)
    {
        var ordered = observations.OrderBy(o => o.Round).ToList();

        if (ordered.Count == 0)
        {
            return new StabilityResult
            {
                ModelName = modelName,
                Threshold = threshold,
                Window = window
            };
        }

        var rollingMetric = RollingMean(ordered.Select(o => o.MetricValue).ToList(), window);
        var rollingDrift = RollingMean(ordered.Select(o => o.ParameterDrift).ToList(), window);

        int? stabilisedFrom = null;

        for (var i = ordered.Count - 1; i >= 0; i--)
        {
            if (rollingDrift[i] >= threshold)
            {
                break;
            }

            stabilisedFrom = ordered[i].Round;
        }

        return new StabilityResult
        {
            ModelName = modelName,
            StabilisedFromRound = stabilisedFrom,
            Threshold = threshold,
            Window = window,
            Rounds = ordered.Select(o => o.Round).ToList(),
            RollingMetric = rollingMetric,
            RollingDrift = rollingDrift
        };
    }
}
