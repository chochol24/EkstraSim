using System.Reflection;
using EkstraSim.Prediction.Evaluation;
using EkstraSim.Prediction.Metrics;
using EkstraSim.Prediction.Models;
using EkstraSim.Prediction.Statistics;
using MathNet.Numerics.Optimization;

namespace EkstraSim.Tests;

public class SensitivitySweepTests
{
    private const int OldSeason = 8;
    private const int PreviousSeason = 9;
    private const int TargetSeason = 10;
    private const int Cutoff = 4;
    private const int ColdStartTeam = 70;
    private const int ReturningTeam = 80;
    private const int RenumberedMatchId = 9000;

    private static readonly IReadOnlyList<int> Chronology = [OldSeason, PreviousSeason, TargetSeason];

    private static readonly Lazy<SensitivitySeason> LazySeason = new(() => SensitivitySeason.Build(
        League(), Chronology, TestLeague.LeagueId, TargetSeason, Cutoff, [ReturningTeam, ColdStartTeam]));

    private static readonly Lazy<IReadOnlyList<VariantOutcome>> LazyOutcomes = new(() => SensitivitySweep.Run(Season, SensitivityGrid.Default, 4));

    private static readonly string[] OptionProperties = typeof(TrainingOptions)
        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Where(property => property.CanWrite)
        .Select(property => property.Name)
        .ToArray();

    private static SensitivitySeason Season => LazySeason.Value;

    private static IReadOnlyList<VariantOutcome> Outcomes => LazyOutcomes.Value;

    public static IEnumerable<object[]> Models => PredictionModelFactory.AvailableModels.Select(name => new object[] { name });

    [Fact]
    public void GridHasExactlyOneDefaultVariantPerModel()
    {
        var options = DefaultBase();

        Assert.Equal(24, SensitivityGrid.Default.Count);
        Assert.Equal(PredictionModelFactory.AvailableModels.Order(), SensitivityGrid.Default.Select(v => v.ModelName).Distinct().Order());
        Assert.All(SensitivityGrid.Default, v => Assert.Equal(PredictionModelFactory.CanonicalName(v.ModelName), v.ModelName));

        foreach (var modelName in PredictionModelFactory.AvailableModels)
        {
            var variants = SensitivityGrid.Default.Where(v => v.ModelName == modelName).ToList();
            var defaultVariant = Assert.Single(variants, v => v.IsDefault);

            Assert.Equal(SensitivityGrid.DefaultParameter, defaultVariant.Parameter);
            Assert.Equal(options, defaultVariant.Apply(options));
            Assert.Equal(variants.Count, variants.Select(v => v.Label).Distinct().Count());
            Assert.All(variants.Where(v => !v.IsDefault), v => Assert.NotEqual(SensitivityGrid.DefaultParameter, v.Parameter));
        }

        Assert.Equal(12, SensitivityGrid.Default.Count(v => v.ModelName == PredictionModelFactory.DixonColes));
        Assert.Equal(4, SensitivityGrid.Default.Count(v => v.ModelName == PredictionModelFactory.Poisson));
        Assert.Equal(8, SensitivityGrid.Default.Count(v => v.ModelName == PredictionModelFactory.Elo));
    }

    [Fact]
    public void GridVariantsChangeExactlyTheDeclaredProperties()
    {
        var bases = new[] { DefaultBase(), PerturbedBase() };

        foreach (var variant in SensitivityGrid.Default)
        {
            var declared = variant.IsDefault ? [] : variant.Parameter.Split('/');
            var changed = OptionProperties
                .Where(name => bases.Any(options => !Equals(Value(options, name), Value(variant.Apply(options), name))))
                .ToList();

            Assert.True(
                declared.Order().SequenceEqual(changed.Order()),
                $"{variant.ModelName} „{variant.Label}”: deklaruje [{string.Join(", ", declared)}], zmienia [{string.Join(", ", changed)}].");
        }

        Assert.Equal(3, SensitivityGrid.Default.Single(v => v.Label == "horyzonty 0.7/0.3/0").Parameter.Split('/').Length);
    }

    [Fact]
    public void GridHoldsTheAgreedValues()
    {
        var options = DefaultBase();

        IEnumerable<T> Values<T>(string parameter, Func<TrainingOptions, T> read) => SensitivityGrid.Default
            .Where(v => v.Parameter == parameter)
            .Select(v => read(v.Apply(options)));

        Assert.Equal(0.0065 / 3.5, SensitivityGrid.LiteratureTimeDecayXi);
        Assert.Equal([0, 0.001, 0.0065 / 3.5, 0.003, 0.004, 0.01, 0.015], Values(nameof(TrainingOptions.TimeDecayXi), o => o.TimeDecayXi));
        Assert.Contains("literatura", SensitivityGrid.Default.Single(v => v.Apply(options).TimeDecayXi == SensitivityGrid.LiteratureTimeDecayXi).Label);
        Assert.Equal([0, 0.5, 5, 50], Values(nameof(TrainingOptions.RidgeLambda), o => o.RidgeLambda));
        Assert.Equal([false], Values(nameof(TrainingOptions.UseFormFactors), o => o.UseFormFactors));
        Assert.Equal(
            [(0.7, 0.3, 0.0), (0.6, 0.3, 0.1)],
            SensitivityGrid.Default.Where(v => v.Parameter.Contains('/')).Select(v => v.Apply(options)).Select(o => (o.CurrentSeasonScale, o.PreviousSeasonScale, o.HistoricalScale)));
        Assert.Equal([5.0, 15, 20, 30], Values(nameof(TrainingOptions.EloK), o => o.EloK));
        Assert.Equal([0.0, 200], Values(nameof(TrainingOptions.EloHomeAdvantage), o => o.EloHomeAdvantage));
        Assert.Equal([1500.0], Values(nameof(TrainingOptions.EloInitialRating), o => o.EloInitialRating));
    }

    [Fact]
    public void SeasonIsBuiltLikeARun()
    {
        var leagueMatches = League().Where(m => m.LeagueId == TestLeague.LeagueId).ToList();

        Assert.Equal(Ids(WalkForwardEvaluator.BuildHistory(leagueMatches, Chronology, TargetSeason, Cutoff)), Ids(Season.History));
        Assert.Equal(Ids(WalkForwardEvaluator.BuildEvaluationSet(leagueMatches, TargetSeason, Cutoff)), Ids(Season.Evaluation));
        Assert.All(Season.History, m => Assert.Equal(TestLeague.LeagueId, m.LeagueId));
        Assert.Equal(DefaultBase(), Season.Options);
        Assert.Equal((true, 0.0065, 0.05), (Season.Options.UseFormFactors, Season.Options.TimeDecayXi, Season.Options.RidgeLambda));
        Assert.Equal(
            [new PromotedTeamHistory(ColdStartTeam, 0, null), new PromotedTeamHistory(ReturningTeam, 10, OldSeason)],
            Season.PromotedTeams);
        Assert.Equal(Season.Evaluation.Count, Season.TeamsByMatch.Count);
    }

    [Theory]
    [MemberData(nameof(Models))]
    public void DefaultVariantEqualsTheDirectWalkForwardSortedByMatchId(string modelName)
    {
        var leagueMatches = League().Where(m => m.LeagueId == TestLeague.LeagueId).ToList();
        var history = WalkForwardEvaluator.BuildHistory(leagueMatches, Chronology, TargetSeason, Cutoff);
        var evaluation = WalkForwardEvaluator.BuildEvaluationSet(leagueMatches, TargetSeason, Cutoff);
        var options = new TrainingOptions { LeagueId = TestLeague.LeagueId, SeasonId = TargetSeason, SeasonChronology = Chronology };

        var direct = WalkForwardEvaluator
            .Run(PredictionModelFactory.Create(modelName), history, evaluation, options, new HashSet<int> { ColdStartTeam, ReturningTeam })
            .SelectMany(round => round.Evaluations)
            .OrderBy(e => e.MatchId)
            .ToList();

        var outcome = Outcomes.Single(o => o.Variant.ModelName == modelName && o.Variant.IsDefault);

        Assert.True(outcome.Succeeded, outcome.Error);
        Assert.Equal(direct.Select(Fingerprint), outcome.Evaluations!.Select(Fingerprint));
        Assert.Contains(direct, e => e.InvolvesPromotedTeam);
    }

    [Fact]
    public void EvaluationsAreSortedByMatchId()
    {
        var roundOrder = Season.Evaluation.OrderBy(m => m.Round).ThenBy(m => m.Id).Select(m => m.Id).ToList();
        Assert.NotEqual(roundOrder.Order(), roundOrder);

        foreach (var outcome in Outcomes.Where(o => o.Succeeded))
        {
            Assert.Equal(roundOrder.Order(), outcome.Evaluations!.Select(e => e.MatchId));
        }
    }

    [Fact]
    public void ParallelismDoesNotChangeTheResult()
    {
        var sequential = SensitivitySweep.Run(Season, SensitivityGrid.Default, 1);

        Assert.Equal(Outcomes.Select(o => o.Variant), sequential.Select(o => o.Variant));

        foreach (var (parallel, single) in Outcomes.Zip(sequential))
        {
            Assert.Equal(parallel.Error, single.Error);
            Assert.Equal(parallel.Evaluations?.Select(Fingerprint), single.Evaluations?.Select(Fingerprint));
        }

        Assert.Throws<ArgumentOutOfRangeException>(() => SensitivitySweep.Run(Season, SensitivityGrid.Default, 0));
    }

    [Fact]
    public void ConvergenceFailureBecomesTheVariantError()
    {
        var variants = SensitivityGrid.Default.Where(v => v.ModelName != PredictionModelFactory.DixonColes || v.IsDefault).ToList();

        var outcomes = SensitivitySweep.Run(Season, variants, 4, name => name == PredictionModelFactory.Elo
            ? new FailingModel(name, () => new ModelConvergenceException(
                new DixonColesFitReport(null, 0, null, ExitCondition.None, double.NaN, double.NaN, double.NaN), "atrapa"))
            : PredictionModelFactory.Create(name));

        Assert.Equal(variants, outcomes.Select(o => o.Variant));
        Assert.All(outcomes.Where(o => o.Variant.ModelName == PredictionModelFactory.Elo), o =>
        {
            Assert.False(o.Succeeded);
            Assert.Null(o.Evaluations);
            Assert.Contains("atrapa", o.Error);
        });
        Assert.All(outcomes.Where(o => o.Variant.ModelName != PredictionModelFactory.Elo), o =>
        {
            Assert.True(o.Succeeded);
            Assert.Null(o.Error);
            Assert.Equal(Season.Evaluation.Count, o.Evaluations!.Count);
        });
    }

    [Fact]
    public void OtherExceptionsEscapeTheSweep()
    {
        var exception = Assert.Throws<AggregateException>(() => SensitivitySweep.Run(
            Season,
            SensitivityGrid.Default.Where(v => v.IsDefault).ToList(),
            1,
            name => name == PredictionModelFactory.Elo
                ? new FailingModel(name, () => new InvalidOperationException("błąd kodu"))
                : PredictionModelFactory.Create(name)));

        Assert.All(exception.InnerExceptions, inner => Assert.IsType<InvalidOperationException>(inner));
    }

    [Fact]
    public void VariantOfOneModelLeavesTheOtherModelsOnDefaults()
    {
        var summaries = SensitivitySummary.Summarise(Season, Outcomes);
        var defaults = summaries.First(s => s.Variant.IsDefault);
        Assert.Equal(Outcomes.Select(o => o.Variant), summaries.Select(s => s.Variant));

        foreach (var summary in summaries.Where(s => s.Succeeded && !s.Variant.IsDefault))
        {
            var modelName = summary.Variant.ModelName;
            var outcome = Outcomes.Single(o => o.Variant == summary.Variant);

            var otherPair = summary.Pairwise.Single(p => p.FirstModel != modelName && p.SecondModel != modelName);
            var expectedPair = defaults.Pairwise.Single(p => p.FirstModel == otherPair.FirstModel && p.SecondModel == otherPair.SecondModel);
            Assert.Equal(
                (expectedPair.FirstMean, expectedPair.SecondMean, expectedPair.PairedMatchCount, expectedPair.Test.Statistic, expectedPair.Test.PValue),
                (otherPair.FirstMean, otherPair.SecondMean, otherPair.PairedMatchCount, otherPair.Test.Statistic, otherPair.Test.PValue));

            Assert.Equal(
                defaults.Promoted.Where(c => c.ModelName != modelName).Select(GroupKey),
                summary.Promoted.Where(c => c.ModelName != modelName).Select(GroupKey));
            Assert.Equal(
                defaults.PromotedByCategory.Comparisons.Where(c => c.ModelName != modelName).Select(GroupKey),
                summary.PromotedByCategory.Comparisons.Where(c => c.ModelName != modelName).Select(GroupKey));
            Assert.Equal(
                defaults.PromotedByCategory.TeamMetrics.Where(m => m.ModelName != modelName).Select(m => (m.TeamId, m.ModelName, m.Mean, m.Count)),
                summary.PromotedByCategory.TeamMetrics.Where(m => m.ModelName != modelName).Select(m => (m.TeamId, m.ModelName, m.Mean, m.Count)));

            Assert.Equal(MetricMeans.Of(outcome.Evaluations!), summary.Means);
            Assert.All(
                summary.Pairwise.Where(p => p.FirstModel == modelName || p.SecondModel == modelName),
                p => Assert.Equal(summary.Means!.RankedProbability, p.FirstModel == modelName ? p.FirstMean : p.SecondMean));
        }

        Assert.All(
            PredictionModelFactory.AvailableModels,
            modelName => Assert.Contains(summaries, s => s.Succeeded && s.Variant.ModelName == modelName && s.Delta!.RankedProbability != 0));
    }

    [Fact]
    public void DefaultVariantsHaveZeroDeltaAndTheSameStatistics()
    {
        var defaults = SensitivitySummary.Summarise(Season, Outcomes).Where(s => s.Variant.IsDefault).ToList();

        Assert.Equal(3, defaults.Count);
        Assert.All(defaults, s => Assert.Equal(new MetricMeans(0, 0, 0), s.Delta));
        Assert.All(defaults, s => Assert.Equal(defaults[0].Pairwise.Select(PairKey), s.Pairwise.Select(PairKey)));
        Assert.All(defaults, s => Assert.Equal(defaults[0].Ranking, s.Ranking));
        Assert.Equal(3, defaults[0].Pairwise.Count);
        Assert.Equal(6, defaults[0].PromotedByCategory.Comparisons.Count);
    }

    [Fact]
    public void FailedVariantHasNoStatistics()
    {
        var failed = new SensitivityVariant(PredictionModelFactory.DixonColes, nameof(TrainingOptions.TimeDecayXi), "ξ = 9", false, o => o with { TimeDecayXi = 9 });
        var outcomes = Outcomes.Append(new VariantOutcome(failed, null, "nie zbiega")).ToList();

        var row = SensitivitySummary.Summarise(Season, outcomes).Last();

        Assert.Same(failed, row.Variant);
        Assert.False(row.Succeeded);
        Assert.Equal("nie zbiega", row.Error);
        Assert.Null(row.Means);
        Assert.Null(row.Delta);
        Assert.Empty(row.Ranking);
        Assert.Empty(row.Pairwise);
        Assert.Empty(row.Promoted);
        Assert.Empty(row.PromotedByCategory.Comparisons);
    }

    [Fact]
    public void ScenarioTakesTheChosenVariants()
    {
        var literature = Outcomes.Single(o => o.Variant.ModelName == PredictionModelFactory.DixonColes && o.Variant.Label.Contains("literatura"));
        var formsOff = Outcomes.Single(o => o.Variant.ModelName == PredictionModelFactory.Poisson && o.Variant.Parameter == nameof(TrainingOptions.UseFormFactors));
        var eloDefault = Outcomes.Single(o => o.Variant.ModelName == PredictionModelFactory.Elo && o.Variant.IsDefault);
        Assert.True(literature.Succeeded, literature.Error);

        var scenario = SensitivitySummary.Scenario(Season, Outcomes, new Dictionary<string, string>
        {
            [PredictionModelFactory.DixonColes] = literature.Variant.Label,
            [PredictionModelFactory.Poisson] = formsOff.Variant.Label
        });

        var set = new Dictionary<string, IReadOnlyList<MatchEvaluation>>
        {
            [PredictionModelFactory.DixonColes] = literature.Evaluations!,
            [PredictionModelFactory.Poisson] = formsOff.Evaluations!,
            [PredictionModelFactory.Elo] = eloDefault.Evaluations!
        };

        Assert.Same(literature.Variant, scenario.Variants[PredictionModelFactory.DixonColes]);
        Assert.Same(formsOff.Variant, scenario.Variants[PredictionModelFactory.Poisson]);
        Assert.Same(eloDefault.Variant, scenario.Variants[PredictionModelFactory.Elo]);
        Assert.All(set, pair => Assert.Equal(MetricMeans.Of(pair.Value), scenario.Means[pair.Key]));
        Assert.Equal(ModelComparison.Pairwise(set, MetricKind.RankedProbability).Select(PairKey), scenario.Pairwise.Select(PairKey));
        Assert.Equal(ModelComparison.PromotedVersusRest(set, MetricKind.RankedProbability).Select(GroupKey), scenario.Promoted.Select(GroupKey));
        Assert.Equal(
            ModelComparison.PromotedByCategory(set, MetricKind.RankedProbability, Season.TeamsByMatch, Season.PromotedTeams).Comparisons.Select(GroupKey),
            scenario.PromotedByCategory.Comparisons.Select(GroupKey));
        Assert.Equal(set.OrderBy(pair => pair.Value.Average(e => e.RankedProbability)).Select(pair => pair.Key), scenario.Ranking);
    }

    [Fact]
    public void ScenarioRejectsUnknownLabelsAndFailedVariants()
    {
        var failed = new SensitivityVariant(PredictionModelFactory.DixonColes, nameof(TrainingOptions.TimeDecayXi), "ξ = 9", false, o => o with { TimeDecayXi = 9 });
        var outcomes = Outcomes.Append(new VariantOutcome(failed, null, "nie zbiega")).ToList();

        Assert.Throws<ArgumentException>(() => SensitivitySummary.Scenario(Season, Outcomes, new Dictionary<string, string> { [PredictionModelFactory.Elo] = "K = 999" }));
        Assert.Throws<ArgumentException>(() => SensitivitySummary.Scenario(Season, Outcomes, new Dictionary<string, string> { ["Bayes"] = SensitivityGrid.DefaultLabel }));
        Assert.Throws<ArgumentException>(() => SensitivitySummary.Scenario(Season, outcomes, new Dictionary<string, string> { [PredictionModelFactory.DixonColes] = "ξ = 9" }));
    }

    [Theory]
    [MemberData(nameof(Models))]
    public void SummaryRequiresAConvergedDefaultVariantForEveryModel(string modelName)
    {
        var withoutDefault = Outcomes.Where(o => !(o.Variant.ModelName == modelName && o.Variant.IsDefault)).ToList();
        var failedDefault = Outcomes
            .Select(o => o.Variant.ModelName == modelName && o.Variant.IsDefault ? new VariantOutcome(o.Variant, null, "nie zbiega") : o)
            .ToList();
        var noLabels = new Dictionary<string, string>();

        Assert.Throws<ArgumentException>(() => SensitivitySummary.Summarise(Season, withoutDefault));
        Assert.Throws<ArgumentException>(() => SensitivitySummary.Scenario(Season, withoutDefault, noLabels));
        Assert.Throws<ArgumentException>(() => SensitivitySummary.Summarise(Season, failedDefault));
        Assert.Throws<ArgumentException>(() => SensitivitySummary.Scenario(Season, failedDefault, noLabels));
    }

    private static TrainingOptions DefaultBase()
    {
        return new TrainingOptions
        {
            LeagueId = TestLeague.LeagueId,
            SeasonId = TargetSeason,
            SeasonChronology = Chronology
        };
    }

    private static TrainingOptions PerturbedBase()
    {
        return DefaultBase() with
        {
            MaxGoals = 7,
            UseFormFactors = false,
            CurrentSeasonScale = 0.5,
            PreviousSeasonScale = 0.25,
            HistoricalScale = 0.25,
            TimeDecayXi = 0.0123,
            RidgeLambda = 1.23,
            EloK = 12.3,
            EloInitialRating = 1234,
            EloHomeAdvantage = 56
        };
    }

    private static object? Value(TrainingOptions options, string propertyName)
    {
        return typeof(TrainingOptions).GetProperty(propertyName)!.GetValue(options);
    }

    private static List<int> Ids(IEnumerable<MatchData> matches) => matches.Select(m => m.Id).ToList();

    private static object Fingerprint(MatchEvaluation e)
    {
        return (e.MatchId, e.ModelName, e.Round, e.InvolvesPromotedTeam, e.Actual, e.Predicted, e.Brier, e.RankedProbability, e.LogLoss,
            e.OutcomeCorrect, e.ExactScoreCorrect, e.ExactScoreInTopThree, e.ProbabilityOfActualScore, e.ProbabilityOfActualOutcome);
    }

    private static object PairKey(PairwiseComparison p)
    {
        return (p.FirstModel, p.SecondModel, p.FirstMean, p.SecondMean, p.PairedMatchCount, p.Test.PValue, p.AdjustedPValue);
    }

    private static object GroupKey(PromotedTeamComparison c)
    {
        return (c.ModelName, c.Category, c.PromotedMean, c.OtherMean, c.PromotedCount, c.OtherCount, c.Test.PValue);
    }

    private static List<MatchData> League()
    {
        var seasons = new (int SeasonId, DateTime Start, int[] Teams)[]
        {
            (OldSeason, new DateTime(2022, 8, 5), [10, 20, 30, 40, 50, ReturningTeam]),
            (PreviousSeason, new DateTime(2023, 8, 4), [10, 20, 30, 40, 50, 60]),
            (TargetSeason, new DateTime(2024, 8, 2), [10, 20, 30, 40, ColdStartTeam, ReturningTeam])
        };

        var matches = new List<MatchData>();
        var id = 1;

        foreach (var (seasonId, start, teams) in seasons)
        {
            var schedule = DoubleRoundRobin(teams);

            for (var index = 0; index < schedule.Count; index++)
            {
                var round = index + 1;

                for (var slot = 0; slot < schedule[index].Length; slot++)
                {
                    var (home, away) = schedule[index][slot];
                    matches.Add(new MatchData(
                        id, start.AddDays(7 * round + slot), round, seasonId, TestLeague.LeagueId,
                        home, away, (id * 7 + 3) % 4, (id * 5 + 1) % 3));
                    id++;
                }
            }
        }

        var renumbered = matches.First(m => m.SeasonId == TargetSeason && m.Round == Cutoff + 1);
        matches[matches.IndexOf(renumbered)] = renumbered with { Id = RenumberedMatchId };
        matches.Add(new MatchData(id, new DateTime(2024, 8, 17), 2, TargetSeason, TestLeague.LeagueId + 1, 10, 20, 5, 0));

        return matches;
    }

    private static List<(int Home, int Away)[]> DoubleRoundRobin(int[] teams)
    {
        var rotation = teams.ToList();
        var firstHalf = new List<(int Home, int Away)[]>();

        for (var round = 0; round < teams.Length - 1; round++)
        {
            var pairs = new (int Home, int Away)[teams.Length / 2];

            for (var i = 0; i < pairs.Length; i++)
            {
                var first = rotation[i];
                var second = rotation[teams.Length - 1 - i];
                pairs[i] = (round + i) % 2 == 0 ? (first, second) : (second, first);
            }

            firstHalf.Add(pairs);

            var last = rotation[^1];
            rotation.RemoveAt(rotation.Count - 1);
            rotation.Insert(1, last);
        }

        return firstHalf
            .Concat(firstHalf.Select(pairs => pairs.Select(p => (p.Away, p.Home)).ToArray()))
            .ToList();
    }

    private sealed class FailingModel : IPredictionModel
    {
        private readonly Func<Exception> _failure;

        public FailingModel(string name, Func<Exception> failure)
        {
            Name = name;
            _failure = failure;
        }

        public string Name { get; }

        public void Train(IReadOnlyList<MatchData> history, TrainingOptions options) => throw _failure();

        public void UpdateWithRound(IReadOnlyList<MatchData> playedRound) => throw new NotSupportedException();

        public MatchPrediction Predict(MatchData match) => throw new NotSupportedException();

        public ModelSnapshot GetParametersSnapshot() => throw new NotSupportedException();
    }
}
