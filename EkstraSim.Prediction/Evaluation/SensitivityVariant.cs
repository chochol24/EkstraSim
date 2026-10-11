using System.Globalization;
using EkstraSim.Prediction.Models;

namespace EkstraSim.Prediction.Evaluation;

public sealed record SensitivityVariant(
    string ModelName,
    string Parameter,
    string Label,
    bool IsDefault,
    Func<TrainingOptions, TrainingOptions> Apply);

public static class SensitivityGrid
{
    public const string DefaultParameter = "domyślne";
    public const string DefaultLabel = "domyślne";
    public const double LiteratureTimeDecayXi = 0.0065 / 3.5;

    public static IReadOnlyList<SensitivityVariant> Default { get; } = Build();

    private static List<SensitivityVariant> Build()
    {
        return
        [
            Defaults(PredictionModelFactory.DixonColes),
            .. new[] { 0, 0.001, LiteratureTimeDecayXi, 0.003, 0.004, 0.01, 0.015 }.Select(TimeDecay),
            .. new[] { 0, 0.5, 5, 50 }.Select(Ridge),
            Defaults(PredictionModelFactory.Poisson),
            new SensitivityVariant(PredictionModelFactory.Poisson, nameof(TrainingOptions.UseFormFactors), "formy wył.", false, options => options with { UseFormFactors = false }),
            SeasonScales(0.7, 0.3, 0),
            SeasonScales(0.6, 0.3, 0.1),
            Defaults(PredictionModelFactory.Elo),
            .. new[] { 5, 15, 20, 30 }.Select(EloK),
            .. new[] { 0, 200 }.Select(EloHomeAdvantage),
            new SensitivityVariant(PredictionModelFactory.Elo, nameof(TrainingOptions.EloInitialRating), "start = 1500", false, options => options with { EloInitialRating = 1500 })
        ];
    }

    private static SensitivityVariant Defaults(string modelName)
    {
        return new SensitivityVariant(modelName, DefaultParameter, DefaultLabel, true, options => options);
    }

    private static SensitivityVariant TimeDecay(double xi)
    {
        var label = xi == LiteratureTimeDecayXi ? "ξ = 0.00186 (literatura)" : $"ξ = {Format(xi)}";
        return new SensitivityVariant(PredictionModelFactory.DixonColes, nameof(TrainingOptions.TimeDecayXi), label, false, options => options with { TimeDecayXi = xi });
    }

    private static SensitivityVariant Ridge(double lambda)
    {
        return new SensitivityVariant(PredictionModelFactory.DixonColes, nameof(TrainingOptions.RidgeLambda), $"λ = {Format(lambda)}", false, options => options with { RidgeLambda = lambda });
    }

    private static SensitivityVariant SeasonScales(double current, double previous, double historical)
    {
        return new SensitivityVariant(
            PredictionModelFactory.Poisson,
            $"{nameof(TrainingOptions.CurrentSeasonScale)}/{nameof(TrainingOptions.PreviousSeasonScale)}/{nameof(TrainingOptions.HistoricalScale)}",
            $"horyzonty {Format(current)}/{Format(previous)}/{Format(historical)}",
            false,
            options => options with { CurrentSeasonScale = current, PreviousSeasonScale = previous, HistoricalScale = historical });
    }

    private static SensitivityVariant EloK(int k)
    {
        return new SensitivityVariant(PredictionModelFactory.Elo, nameof(TrainingOptions.EloK), $"K = {k}", false, options => options with { EloK = k });
    }

    private static SensitivityVariant EloHomeAdvantage(int advantage)
    {
        return new SensitivityVariant(PredictionModelFactory.Elo, nameof(TrainingOptions.EloHomeAdvantage), $"przewaga = {advantage}", false, options => options with { EloHomeAdvantage = advantage });
    }

    private static string Format(double value) => value.ToString(CultureInfo.InvariantCulture);
}
