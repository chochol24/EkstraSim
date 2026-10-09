namespace EkstraSim.Prediction.Statistics;

public sealed record RoundObservation(int Round, double MetricValue, double ParameterDrift);

public sealed class StabilityResult
{
    public string ModelName { get; init; } = string.Empty;
    public int? StabilisedFromRound { get; init; }
    public double DriftLevel { get; init; }
    public double MeanDrift { get; init; }
    public double Tolerance { get; init; }
    public double Threshold { get; init; }
    public TestResult Trend { get; init; } = TestResult.Inconclusive(SpearmanTrendTest.TestName, 0);
    public int Window { get; init; }
    public IReadOnlyList<int> Rounds { get; init; } = [];
    public IReadOnlyList<double> RollingMetric { get; init; } = [];
    public IReadOnlyList<double> RollingDrift { get; init; } = [];
}

public static class StabilityAnalysis
{
    public const int DefaultWindow = 3;
    public const double DefaultTolerance = 0.25;

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
        double tolerance,
        int window = DefaultWindow)
    {
        var ordered = observations.OrderBy(o => o.Round).ToList();

        if (ordered.Count == 0)
        {
            return new StabilityResult
            {
                ModelName = modelName,
                Tolerance = tolerance,
                Window = window
            };
        }

        var drifts = ordered.Select(o => o.ParameterDrift).ToList();
        var rollingMetric = RollingMean(ordered.Select(o => o.MetricValue).ToList(), window);
        var rollingDrift = RollingMean(drifts, window);

        var levelCount = Math.Min(drifts.Count, Math.Max(window, (int)Math.Ceiling(drifts.Count / 3.0)));
        var driftLevel = drifts.Skip(drifts.Count - levelCount).Average();
        var threshold = (1 + tolerance) * driftLevel;

        int? stabilisedFrom = null;

        for (var i = ordered.Count - 1; i >= 0; i--)
        {
            if (rollingDrift[i] > threshold)
            {
                break;
            }

            stabilisedFrom = ordered[i].Round;
        }

        return new StabilityResult
        {
            ModelName = modelName,
            StabilisedFromRound = stabilisedFrom,
            DriftLevel = driftLevel,
            MeanDrift = drifts.Average(),
            Tolerance = tolerance,
            Threshold = threshold,
            Trend = SpearmanTrendTest.Test(ordered.Select(o => (double)o.Round).ToList(), drifts),
            Window = window,
            Rounds = ordered.Select(o => o.Round).ToList(),
            RollingMetric = rollingMetric,
            RollingDrift = rollingDrift
        };
    }
}
