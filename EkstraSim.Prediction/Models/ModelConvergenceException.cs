using System.Globalization;
using MathNet.Numerics.Optimization;

namespace EkstraSim.Prediction.Models;

public sealed class ModelConvergenceException : Exception
{
    public ModelConvergenceException(DixonColesFitReport report, string reason, Exception? innerException = null)
        : base(Describe(report, reason), innerException)
    {
        Report = report;
        Reason = reason;
    }

    public DixonColesFitReport Report { get; }

    public string Reason { get; }

    private static string Describe(DixonColesFitReport report, string reason)
    {
        var details = new List<string>
        {
            report.AfterRound.HasValue ? $"po kolejce {report.AfterRound}" : "trening"
        };

        if (report.Iterations.HasValue)
        {
            details.Add($"{report.Iterations} iteracji");
        }

        details.Add($"powód: {reason}");

        if (report.ExitReason != ExitCondition.None)
        {
            details.Add($"wyjście MathNet: {report.ExitReason}");
        }

        if (double.IsFinite(report.FinalObjective))
        {
            details.Add(string.Format(CultureInfo.InvariantCulture, "NLL {0:F3} → {1:F3}", report.StartObjective, report.FinalObjective));
        }

        if (double.IsFinite(report.GradientNorm))
        {
            details.Add(string.Format(CultureInfo.InvariantCulture, "‖∇f‖∞ = {0:E2}", report.GradientNorm));
        }

        return $"Dixon-Coles: optymalizacja nie zbiegła ({string.Join("; ", details)}).";
    }
}
