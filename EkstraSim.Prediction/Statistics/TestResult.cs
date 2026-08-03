namespace EkstraSim.Prediction.Statistics;

public sealed class TestResult
{
    public string Name { get; init; } = string.Empty;
    public double Statistic { get; init; }
    public double ZScore { get; init; }
    public double PValue { get; init; } = 1.0;
    public int SampleSize { get; init; }
    public bool IsConclusive { get; init; }

    public static TestResult Inconclusive(string name, int sampleSize) => new()
    {
        Name = name,
        SampleSize = sampleSize,
        PValue = 1.0,
        IsConclusive = false
    };

    public bool IsSignificantAt(double alpha) => IsConclusive && PValue < alpha;
}
