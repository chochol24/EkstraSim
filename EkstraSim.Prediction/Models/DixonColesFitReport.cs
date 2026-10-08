using MathNet.Numerics.Optimization;

namespace EkstraSim.Prediction.Models;

public sealed record DixonColesFitReport(
    int? AfterRound,
    int Dimension,
    int? Iterations,
    ExitCondition ExitReason,
    double StartObjective,
    double FinalObjective,
    double GradientNorm);
