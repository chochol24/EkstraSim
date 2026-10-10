using EkstraSim.Shared.Resources;

namespace EkstraSim.Shared.Requests;

public static class ResearchRequestRules
{
    public static string? Validate(CreateEvaluationRunRequest request)
    {
        if (request.TrainingLastRound < 1)
        {
            return string.Format(SnackbarMessages.Research_Cutoff_TooLow, request.TrainingLastRound);
        }

        var invalid = ValidateTraining(request.TimeDecayXi, request.RidgeLambda) ?? ValidateTolerance(request.StabilityTolerance);
        if (invalid != null)
        {
            return invalid;
        }

        if (request.StabilityWindow < 1)
        {
            return string.Format(SnackbarMessages.Research_Window_TooLow, request.StabilityWindow);
        }

        return null;
    }

    public static string? Validate(PredictRoundRequest request)
    {
        if (request.Round < 1)
        {
            return string.Format(SnackbarMessages.Research_Round_TooLow, request.Round);
        }

        if (request.TrainingLastRound < 0)
        {
            return string.Format(SnackbarMessages.Research_PredictCutoff_Negative, request.TrainingLastRound);
        }

        if (request.TrainingLastRound >= request.Round)
        {
            return string.Format(SnackbarMessages.Research_PredictCutoff_NotBeforeRound, request.TrainingLastRound, request.Round);
        }

        return ValidateTraining(request.TimeDecayXi, request.RidgeLambda);
    }

    public static string? ValidateTolerance(double tolerance)
    {
        return IsFiniteNonNegative(tolerance) ? null : SnackbarMessages.Research_Tolerance_Invalid;
    }

    private static string? ValidateTraining(double timeDecayXi, double ridgeLambda)
    {
        if (!IsFiniteNonNegative(timeDecayXi))
        {
            return SnackbarMessages.Research_TimeDecay_Invalid;
        }

        return IsFiniteNonNegative(ridgeLambda) ? null : SnackbarMessages.Research_Ridge_Invalid;
    }

    private static bool IsFiniteNonNegative(double value) => double.IsFinite(value) && value >= 0;
}
