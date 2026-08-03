namespace EkstraSim.Prediction.Models;

public sealed class PoissonRegression
{
    private const int MaxIterations = 100;
    private const double Tolerance = 1e-11;
    private const double SingularThreshold = 1e-14;

    public double Intercept { get; private init; }
    public double Slope { get; private init; }

    public double Predict(double x) => Math.Exp(Intercept + Slope * x);

    public static PoissonRegression Fit(IReadOnlyList<(double X, double Y)> samples)
    {
        if (samples.Count == 0)
        {
            return new PoissonRegression { Intercept = 0, Slope = 0 };
        }

        var meanResponse = Math.Max(1e-6, samples.Average(s => s.Y));
        var intercept = Math.Log(meanResponse);
        double slope = 0;

        for (var iteration = 0; iteration < MaxIterations; iteration++)
        {
            double gradientIntercept = 0, gradientSlope = 0;
            double hessian00 = 0, hessian01 = 0, hessian11 = 0;

            foreach (var (x, y) in samples)
            {
                var fitted = Math.Exp(intercept + slope * x);
                if (!double.IsFinite(fitted))
                {
                    return new PoissonRegression { Intercept = Math.Log(meanResponse), Slope = 0 };
                }

                var residual = y - fitted;
                gradientIntercept += residual;
                gradientSlope += residual * x;
                hessian00 += fitted;
                hessian01 += fitted * x;
                hessian11 += fitted * x * x;
            }

            var determinant = hessian00 * hessian11 - hessian01 * hessian01;
            if (Math.Abs(determinant) < SingularThreshold)
            {
                break;
            }

            var stepIntercept = (hessian11 * gradientIntercept - hessian01 * gradientSlope) / determinant;
            var stepSlope = (hessian00 * gradientSlope - hessian01 * gradientIntercept) / determinant;

            intercept += stepIntercept;
            slope += stepSlope;

            if (Math.Max(Math.Abs(stepIntercept), Math.Abs(stepSlope)) < Tolerance)
            {
                break;
            }
        }

        if (!double.IsFinite(intercept) || !double.IsFinite(slope))
        {
            return new PoissonRegression { Intercept = Math.Log(meanResponse), Slope = 0 };
        }

        return new PoissonRegression { Intercept = intercept, Slope = slope };
    }
}
