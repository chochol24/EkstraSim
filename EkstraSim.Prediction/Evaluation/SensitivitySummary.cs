using EkstraSim.Prediction.Metrics;
using EkstraSim.Prediction.Statistics;

namespace EkstraSim.Prediction.Evaluation;

public sealed record MetricMeans(double RankedProbability, double Brier, double LogLoss)
{
    public static MetricMeans Of(IReadOnlyList<MatchEvaluation> evaluations)
    {
        return new MetricMeans(
            evaluations.Average(e => e.RankedProbability),
            evaluations.Average(e => e.Brier),
            evaluations.Average(e => e.LogLoss));
    }

    public MetricMeans Minus(MetricMeans other)
    {
        return new MetricMeans(RankedProbability - other.RankedProbability, Brier - other.Brier, LogLoss - other.LogLoss);
    }
}

public sealed class VariantSummary
{
    public SensitivityVariant Variant { get; init; } = null!;
    public string? Error { get; init; }

    public MetricMeans? Means { get; init; }
    public MetricMeans? Delta { get; init; }

    public IReadOnlyList<string> Ranking { get; init; } = [];
    public IReadOnlyList<PairwiseComparison> Pairwise { get; init; } = [];
    public IReadOnlyList<PromotedTeamComparison> Promoted { get; init; } = [];
    public PromotedCategoryAnalysis PromotedByCategory { get; init; } = new();

    public bool Succeeded => Means != null;
}

public sealed class ScenarioSummary
{
    public IReadOnlyDictionary<string, SensitivityVariant> Variants { get; init; } = new Dictionary<string, SensitivityVariant>();
    public IReadOnlyDictionary<string, MetricMeans> Means { get; init; } = new Dictionary<string, MetricMeans>();

    public IReadOnlyList<string> Ranking { get; init; } = [];
    public IReadOnlyList<PairwiseComparison> Pairwise { get; init; } = [];
    public IReadOnlyList<PromotedTeamComparison> Promoted { get; init; } = [];
    public PromotedCategoryAnalysis PromotedByCategory { get; init; } = new();
}

public static class SensitivitySummary
{
    public static IReadOnlyList<VariantSummary> Summarise(SensitivitySeason season, IReadOnlyList<VariantOutcome> outcomes)
    {
        var defaults = DefaultOutcomes(outcomes);

        return outcomes
            .Select(outcome =>
            {
                if (!outcome.Succeeded)
                {
                    return new VariantSummary { Variant = outcome.Variant, Error = outcome.Error };
                }

                var modelName = outcome.Variant.ModelName;
                var set = defaults.ToDictionary(pair => pair.Key, pair => pair.Value.Evaluations!);
                set[modelName] = outcome.Evaluations!;

                var means = MetricMeans.Of(outcome.Evaluations!);
                var (ranking, pairwise, promoted, byCategory) = Statistics(season, set);

                return new VariantSummary
                {
                    Variant = outcome.Variant,
                    Means = means,
                    Delta = means.Minus(MetricMeans.Of(defaults[modelName].Evaluations!)),
                    Ranking = ranking,
                    Pairwise = pairwise,
                    Promoted = promoted,
                    PromotedByCategory = byCategory
                };
            })
            .ToList();
    }

    public static ScenarioSummary Scenario(
        SensitivitySeason season,
        IReadOnlyList<VariantOutcome> outcomes,
        IReadOnlyDictionary<string, string> labelByModel)
    {
        var defaults = DefaultOutcomes(outcomes);

        var unknownModel = labelByModel.Keys.FirstOrDefault(modelName => !defaults.ContainsKey(modelName));
        if (unknownModel != null)
        {
            throw new ArgumentException($"Model {unknownModel} nie występuje w przemiataniu.", nameof(labelByModel));
        }

        var chosen = defaults.ToDictionary(
            pair => pair.Key,
            pair => labelByModel.TryGetValue(pair.Key, out var label) ? Chosen(outcomes, pair.Key, label) : pair.Value);

        var set = chosen.ToDictionary(pair => pair.Key, pair => pair.Value.Evaluations!);
        var (ranking, pairwise, promoted, byCategory) = Statistics(season, set);

        return new ScenarioSummary
        {
            Variants = chosen.ToDictionary(pair => pair.Key, pair => pair.Value.Variant),
            Means = set.ToDictionary(pair => pair.Key, pair => MetricMeans.Of(pair.Value)),
            Ranking = ranking,
            Pairwise = pairwise,
            Promoted = promoted,
            PromotedByCategory = byCategory
        };
    }

    private static Dictionary<string, VariantOutcome> DefaultOutcomes(IReadOnlyList<VariantOutcome> outcomes)
    {
        var defaults = new Dictionary<string, VariantOutcome>();

        foreach (var modelName in outcomes.Select(o => o.Variant.ModelName).Distinct())
        {
            var candidates = outcomes.Where(o => o.Variant.ModelName == modelName && o.Variant.IsDefault).ToList();

            if (candidates.Count != 1)
            {
                throw new ArgumentException($"Model {modelName} ma {candidates.Count} wariantów domyślnych, oczekiwano 1.", nameof(outcomes));
            }

            if (!candidates[0].Succeeded)
            {
                throw new ArgumentException($"Wariant domyślny modelu {modelName} nie ma wyniku: {candidates[0].Error}", nameof(outcomes));
            }

            defaults[modelName] = candidates[0];
        }

        return defaults;
    }

    private static VariantOutcome Chosen(IReadOnlyList<VariantOutcome> outcomes, string modelName, string label)
    {
        var matching = outcomes.Where(o => o.Variant.ModelName == modelName && o.Variant.Label == label).ToList();

        if (matching.Count != 1)
        {
            throw new ArgumentException($"Model {modelName} ma {matching.Count} wariantów „{label}”, oczekiwano 1.", nameof(outcomes));
        }

        var outcome = matching[0];

        if (!outcome.Succeeded)
        {
            throw new ArgumentException($"Wariant „{label}” modelu {modelName} nie ma wyniku: {outcome.Error}", nameof(outcomes));
        }

        return outcome;
    }

    private static (IReadOnlyList<string> Ranking, IReadOnlyList<PairwiseComparison> Pairwise, IReadOnlyList<PromotedTeamComparison> Promoted, PromotedCategoryAnalysis ByCategory) Statistics(
        SensitivitySeason season,
        IReadOnlyDictionary<string, IReadOnlyList<MatchEvaluation>> set)
    {
        var ranking = set
            .OrderBy(pair => pair.Value.Average(e => e.RankedProbability))
            .ThenBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => pair.Key)
            .ToList();

        return (
            ranking,
            ModelComparison.Pairwise(set, MetricKind.RankedProbability),
            ModelComparison.PromotedVersusRest(set, MetricKind.RankedProbability),
            ModelComparison.PromotedByCategory(set, MetricKind.RankedProbability, season.TeamsByMatch, season.PromotedTeams));
    }
}
