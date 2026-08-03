using EkstraSim.Prediction.Models;
using MathNet.Numerics.Distributions;

namespace EkstraSim.Tests;

public class PoissonRegressionTests
{
    private static List<(double X, double Y)> Synthetic(double intercept, double slope, int count, int seed)
    {
        var random = new Random(seed);
        var samples = new List<(double X, double Y)>(count);

        for (var i = 0; i < count; i++)
        {
            var x = -2.0 + 4.0 * random.NextDouble();
            var mean = Math.Exp(intercept + slope * x);
            samples.Add((x, Poisson.Sample(random, mean)));
        }

        return samples;
    }

    [Fact]
    public void RecoversKnownCoefficients()
    {
        var fit = PoissonRegression.Fit(Synthetic(intercept: 0.4, slope: 0.6, count: 6000, seed: 2024));

        Assert.Equal(0.4, fit.Intercept, tolerance: 0.05);
        Assert.Equal(0.6, fit.Slope, tolerance: 0.05);
    }

    [Fact]
    public void RecoversNegativeSlope()
    {
        var fit = PoissonRegression.Fit(Synthetic(intercept: 0.2, slope: -0.5, count: 6000, seed: 77));

        Assert.Equal(0.2, fit.Intercept, tolerance: 0.05);
        Assert.Equal(-0.5, fit.Slope, tolerance: 0.05);
    }

    [Fact]
    public void PredictsExponentialMean()
    {
        var fit = PoissonRegression.Fit(Synthetic(intercept: 0.3, slope: 0.5, count: 6000, seed: 5));

        Assert.Equal(Math.Exp(fit.Intercept + fit.Slope * 1.5), fit.Predict(1.5), precision: 12);
    }

    [Fact]
    public void ConstantFeatureFallsBackToInterceptOnly()
    {
        List<(double X, double Y)> samples = [(0, 2), (0, 1), (0, 3), (0, 2)];

        var fit = PoissonRegression.Fit(samples);

        Assert.Equal(0, fit.Slope, precision: 12);
        Assert.Equal(Math.Log(2.0), fit.Intercept, precision: 8);
    }

    [Fact]
    public void EmptySampleGivesNeutralFit()
    {
        var fit = PoissonRegression.Fit([]);

        Assert.Equal(0, fit.Intercept, precision: 12);
        Assert.Equal(0, fit.Slope, precision: 12);
        Assert.Equal(1.0, fit.Predict(3.0), precision: 12);
    }

    [Fact]
    public void AllZeroResponsesDoNotProduceNaN()
    {
        List<(double X, double Y)> samples = [(-1, 0), (0, 0), (1, 0)];

        var fit = PoissonRegression.Fit(samples);

        Assert.True(double.IsFinite(fit.Intercept));
        Assert.True(double.IsFinite(fit.Slope));
        Assert.True(fit.Predict(0) >= 0);
    }
}
