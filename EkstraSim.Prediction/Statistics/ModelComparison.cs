using EkstraSim.Prediction.Metrics;

namespace EkstraSim.Prediction.Statistics;

public sealed class PairwiseComparison
{
    public string FirstModel { get; init; } = string.Empty;
    public string SecondModel { get; init; } = string.Empty;
    public MetricKind Metric { get; init; }

    public double FirstMean { get; init; }
    public double SecondMean { get; init; }
    public int PairedMatchCount { get; init; }

    public TestResult Test { get; init; } = TestResult.Inconclusive(WilcoxonSignedRankTest.TestName, 0);
    public double AdjustedPValue { get; init; } = 1.0;

    public string? BetterModel
    {
        get
        {
            if (FirstMean.Equals(SecondMean))
            {
                return null;
            }

            var firstIsBetter = Metric.LowerIsBetter() ? FirstMean < SecondMean : FirstMean > SecondMean;
            return firstIsBetter ? FirstModel : SecondModel;
        }
    }
}

public sealed class PromotedTeamComparison
{
    public string ModelName { get; init; } = string.Empty;
    public MetricKind Metric { get; init; }
    public int? FromRound { get; init; }
    public int? ToRound { get; init; }

    public double PromotedMean { get; init; }
    public double OtherMean { get; init; }
    public int PromotedCount { get; init; }
    public int OtherCount { get; init; }

    public TestResult Test { get; init; } = TestResult.Inconclusive(MannWhitneyUTest.TestName, 0);
    public double AdjustedPValue { get; init; } = 1.0;

    public double Difference => PromotedMean - OtherMean;
}

public static class ModelComparison
{
    public static IReadOnlyList<PairwiseComparison> Pairwise(
        IReadOnlyDictionary<string, IReadOnlyList<MatchEvaluation>> evaluationsByModel,
        MetricKind metric)
    {
        var modelNames = evaluationsByModel.Keys.OrderBy(name => name, StringComparer.Ordinal).ToList();
        var comparisons = new List<PairwiseComparison>();

        for (var i = 0; i < modelNames.Count; i++)
        {
            for (var j = i + 1; j < modelNames.Count; j++)
            {
                comparisons.Add(BuildPair(
                    modelNames[i],
                    modelNames[j],
                    evaluationsByModel[modelNames[i]],
                    evaluationsByModel[modelNames[j]],
                    metric));
            }
        }

        return ApplyHolm(comparisons);
    }

    public static IReadOnlyList<PromotedTeamComparison> PromotedVersusRest(
        IReadOnlyDictionary<string, IReadOnlyList<MatchEvaluation>> evaluationsByModel,
        MetricKind metric,
        IReadOnlyList<(int From, int To)>? roundWindows = null)
    {
        var comparisons = new List<PromotedTeamComparison>();

        foreach (var (modelName, evaluations) in evaluationsByModel.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            if (roundWindows == null || roundWindows.Count == 0)
            {
                comparisons.Add(BuildPromoted(modelName, evaluations, metric, null, null));
                continue;
            }

            foreach (var (from, to) in roundWindows)
            {
                var window = evaluations
                    .Where(e => e.Round.HasValue && e.Round.Value >= from && e.Round.Value <= to)
                    .ToList();

                comparisons.Add(BuildPromoted(modelName, window, metric, from, to));
            }
        }

        var adjusted = HolmCorrection.Adjust(comparisons.Select(c => c.Test.PValue).ToList());

        return comparisons
            .Select((comparison, index) => new PromotedTeamComparison
            {
                ModelName = comparison.ModelName,
                Metric = comparison.Metric,
                FromRound = comparison.FromRound,
                ToRound = comparison.ToRound,
                PromotedMean = comparison.PromotedMean,
                OtherMean = comparison.OtherMean,
                PromotedCount = comparison.PromotedCount,
                OtherCount = comparison.OtherCount,
                Test = comparison.Test,
                AdjustedPValue = adjusted[index]
            })
            .ToList();
    }

    private static PairwiseComparison BuildPair(
        string firstModel,
        string secondModel,
        IReadOnlyList<MatchEvaluation> first,
        IReadOnlyList<MatchEvaluation> second,
        MetricKind metric)
    {
        var firstById = first.GroupBy(e => e.MatchId).ToDictionary(g => g.Key, g => g.First());
        var secondById = second.GroupBy(e => e.MatchId).ToDictionary(g => g.Key, g => g.First());

        var sharedIds = firstById.Keys.Intersect(secondById.Keys).OrderBy(id => id).ToList();

        var firstValues = sharedIds.Select(id => metric.ValueOf(firstById[id])).ToList();
        var secondValues = sharedIds.Select(id => metric.ValueOf(secondById[id])).ToList();

        return new PairwiseComparison
        {
            FirstModel = firstModel,
            SecondModel = secondModel,
            Metric = metric,
            PairedMatchCount = sharedIds.Count,
            FirstMean = firstValues.Count > 0 ? firstValues.Average() : 0,
            SecondMean = secondValues.Count > 0 ? secondValues.Average() : 0,
            Test = sharedIds.Count > 0
                ? WilcoxonSignedRankTest.Paired(firstValues, secondValues)
                : TestResult.Inconclusive(WilcoxonSignedRankTest.TestName, 0)
        };
    }

    private static PromotedTeamComparison BuildPromoted(
        string modelName,
        IReadOnlyList<MatchEvaluation> evaluations,
        MetricKind metric,
        int? fromRound,
        int? toRound)
    {
        var promoted = evaluations.Where(e => e.InvolvesPromotedTeam).Select(e => metric.ValueOf(e)).ToList();
        var others = evaluations.Where(e => !e.InvolvesPromotedTeam).Select(e => metric.ValueOf(e)).ToList();

        return new PromotedTeamComparison
        {
            ModelName = modelName,
            Metric = metric,
            FromRound = fromRound,
            ToRound = toRound,
            PromotedCount = promoted.Count,
            OtherCount = others.Count,
            PromotedMean = promoted.Count > 0 ? promoted.Average() : 0,
            OtherMean = others.Count > 0 ? others.Average() : 0,
            Test = MannWhitneyUTest.Compare(promoted, others)
        };
    }

    private static IReadOnlyList<PairwiseComparison> ApplyHolm(List<PairwiseComparison> comparisons)
    {
        var adjusted = HolmCorrection.Adjust(comparisons.Select(c => c.Test.PValue).ToList());

        return comparisons
            .Select((comparison, index) => new PairwiseComparison
            {
                FirstModel = comparison.FirstModel,
                SecondModel = comparison.SecondModel,
                Metric = comparison.Metric,
                FirstMean = comparison.FirstMean,
                SecondMean = comparison.SecondMean,
                PairedMatchCount = comparison.PairedMatchCount,
                Test = comparison.Test,
                AdjustedPValue = adjusted[index]
            })
            .ToList();
    }
}
