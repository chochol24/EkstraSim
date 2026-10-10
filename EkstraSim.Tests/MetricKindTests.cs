using EkstraSim.Prediction.Metrics;

namespace EkstraSim.Tests;

public class MetricKindTests
{
    public static IEnumerable<object[]> Metrics => Enum.GetValues<MetricKind>().Select(metric => new object[] { metric });

    [Theory]
    [MemberData(nameof(Metrics))]
    public void ParsesEveryMemberName(MetricKind metric)
    {
        Assert.True(MetricKindExtensions.TryParseName(metric.ToString(), out var parsed));
        Assert.Equal(metric, parsed);
    }

    [Theory]
    [InlineData("brier", MetricKind.Brier)]
    [InlineData("RANKEDPROBABILITY", MetricKind.RankedProbability)]
    [InlineData(" logLoss ", MetricKind.LogLoss)]
    [InlineData("probabilityofactualscore", MetricKind.ProbabilityOfActualScore)]
    public void IgnoresCaseAndSurroundingWhitespace(string name, MetricKind expected)
    {
        Assert.True(MetricKindExtensions.TryParseName(name, out var parsed));
        Assert.Equal(expected, parsed);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BlankNameMeansRankedProbability(string? name)
    {
        Assert.True(MetricKindExtensions.TryParseName(name, out var parsed));
        Assert.Equal(MetricKind.RankedProbability, parsed);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("5")]
    [InlineData("-1")]
    [InlineData("Foo")]
    [InlineData("RPS")]
    [InlineData("RankedProbabilityScore")]
    [InlineData("Brier,LogLoss")]
    public void RejectsNumbersAndUnknownNames(string name)
    {
        Assert.False(MetricKindExtensions.TryParseName(name, out _));
    }
}
