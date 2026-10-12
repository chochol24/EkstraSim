using System.Globalization;
using EkstraSim.Prediction.Evaluation;
using EkstraSim.Prediction.Metrics;
using EkstraSim.Prediction.Models;
using EkstraSim.Prediction.Statistics;
using Xunit.Abstractions;

namespace EkstraSim.Tests;

public class SensitivityRealDataTests
{
    private const double MeanTolerance = 5e-5;
    private const double TeamMeanTolerance = 5e-4;
    private const double CategoryPValueTolerance = 5e-3;
    private const double InvarianceTolerance = 1e-12;

    private static readonly (int SeasonId, int TrainingLastRound)[] SeasonCutoffs =
    [
        (RealDataFixture.Season2122, 19),
        (RealDataFixture.Season2223, 17),
        (RealDataFixture.Season2324, 19),
        (RealDataFixture.Season2425, RealDataFixture.TrainingLastRound),
        (RealDataFixture.Season2526, RealDataFixture.TrainingLastRound)
    ];

    private static readonly Dictionary<int, string> SeasonNames = new()
    {
        [RealDataFixture.Season2122] = "2021/22",
        [RealDataFixture.Season2223] = "2022/23",
        [RealDataFixture.Season2324] = "2023/24",
        [RealDataFixture.Season2425] = "2024/25",
        [RealDataFixture.Season2526] = "2025/26"
    };

    private static readonly int[] EvaluatedSeasons = [RealDataFixture.Season2425, RealDataFixture.Season2526];

    private static readonly string[] ModelOrder = [PredictionModelFactory.Elo, PredictionModelFactory.DixonColes, PredictionModelFactory.Poisson];

    private static readonly (string Name, Dictionary<string, string> Labels)[] Scenarios =
    [
        ("domyślne", new Dictionary<string, string>()),
        ("ξ z literatury", new Dictionary<string, string> { [PredictionModelFactory.DixonColes] = LiteratureLabel }),
        ("formy wyłączone", new Dictionary<string, string> { [PredictionModelFactory.Poisson] = FormsOffLabel }),
        ("ξ z literatury + formy wyłączone", new Dictionary<string, string> { [PredictionModelFactory.DixonColes] = LiteratureLabel, [PredictionModelFactory.Poisson] = FormsOffLabel })
    ];

    private const string LiteratureLabel = "ξ = 0.00186 (literatura)";
    private const string FormsOffLabel = "formy wył.";

    private static readonly Lazy<IReadOnlyList<SeasonSweep>> Sweeps = new(ComputeSweeps);

    private readonly ITestOutputHelper _output;

    public SensitivityRealDataTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public static IEnumerable<object[]> SeasonIds => SeasonCutoffs.Select(season => new object[] { season.SeasonId });

    [Theory]
    [Trait("Category", "RealData")]
    [MemberData(nameof(SeasonIds))]
    public void DefaultVariantsEqualTheDirectWalkForward(int seasonId)
    {
        var sweep = Sweep(seasonId);
        var history = WalkForwardEvaluator.BuildHistory(RealDataFixture.Matches, RealDataFixture.SeasonChronology, seasonId, sweep.Season.TrainingLastRound);
        var evaluation = WalkForwardEvaluator.BuildEvaluationSet(RealDataFixture.Matches, seasonId, sweep.Season.TrainingLastRound);
        var promotedIds = RealDataFixture.PromotedTeamIds(seasonId).ToHashSet();

        Assert.Equal(RealDataFixture.Options(seasonId), Built(seasonId, sweep.Season.TrainingLastRound).Options);

        foreach (var modelName in ModelOrder)
        {
            var direct = WalkForwardEvaluator
                .Run(PredictionModelFactory.Create(modelName), history, evaluation, RealDataFixture.Options(seasonId), promotedIds)
                .SelectMany(round => round.Evaluations)
                .OrderBy(e => e.MatchId)
                .ToList();

            var outcome = Default(sweep, modelName);

            Assert.Equal(direct.Select(Fingerprint), outcome.Evaluations!.Select(Fingerprint));
        }
    }

    [Theory]
    [Trait("Category", "RealData")]
    [InlineData(RealDataFixture.Season2425, 0.2189, 0.2287, 0.2518, 0.0833, 0.0833, 0.1403)]
    [InlineData(RealDataFixture.Season2526, 0.2169, 0.2324, 0.2604, 0.0027, 0.0035, 0.0129)]
    public void DefaultsReproduceRuns9And10(int seasonId, double elo, double dixonColes, double poisson, double eloPoisson, double dixonColesElo, double dixonColesPoisson)
    {
        var summary = Sweep(seasonId).Summaries.First(s => s.Variant.IsDefault);
        var means = ModelOrder.ToDictionary(name => name, name => MetricMeans.Of(Default(Sweep(seasonId), name).Evaluations!).RankedProbability);

        Assert.Equal(elo, means[PredictionModelFactory.Elo], MeanTolerance);
        Assert.Equal(dixonColes, means[PredictionModelFactory.DixonColes], MeanTolerance);
        Assert.Equal(poisson, means[PredictionModelFactory.Poisson], MeanTolerance);
        Assert.Equal(eloPoisson, Holm(summary.Pairwise, PredictionModelFactory.Elo, PredictionModelFactory.Poisson), MeanTolerance);
        Assert.Equal(dixonColesElo, Holm(summary.Pairwise, PredictionModelFactory.DixonColes, PredictionModelFactory.Elo), MeanTolerance);
        Assert.Equal(dixonColesPoisson, Holm(summary.Pairwise, PredictionModelFactory.DixonColes, PredictionModelFactory.Poisson), MeanTolerance);
        Assert.Equal([PredictionModelFactory.Elo, PredictionModelFactory.DixonColes, PredictionModelFactory.Poisson], summary.Ranking);
    }

    [Theory]
    [Trait("Category", "RealData")]
    [InlineData(RealDataFixture.Season2425, 28, 0.250, 0.237, 0.231)]
    [InlineData(RealDataFixture.Season2425, 29, 0.234, 0.215, 0.259)]
    [InlineData(RealDataFixture.Season2425, 22, 0.285, 0.259, 0.301)]
    [InlineData(RealDataFixture.Season2526, 27, 0.215, 0.194, 0.233)]
    [InlineData(RealDataFixture.Season2526, 23, 0.226, 0.211, 0.244)]
    [InlineData(RealDataFixture.Season2526, 20, 0.312, 0.284, 0.341)]
    public void DefaultsReproduceThePromotedTeamMeansOfRuns9And10(int seasonId, int teamId, double dixonColes, double elo, double poisson)
    {
        var teamMetrics = Sweep(seasonId).Summaries.First(s => s.Variant.IsDefault).PromotedByCategory.TeamMetrics;
        double Mean(string modelName) => teamMetrics.Single(m => m.TeamId == teamId && m.ModelName == modelName).Mean;

        Assert.Equal(dixonColes, Mean(PredictionModelFactory.DixonColes), TeamMeanTolerance);
        Assert.Equal(elo, Mean(PredictionModelFactory.Elo), TeamMeanTolerance);
        Assert.Equal(poisson, Mean(PredictionModelFactory.Poisson), TeamMeanTolerance);
        Assert.All(teamMetrics.Where(m => m.TeamId == teamId), m => Assert.Equal(16, m.Count));
    }

    [Theory]
    [Trait("Category", "RealData")]
    [InlineData(RealDataFixture.Season2425, 0.217, 0.210, 0.247, 0.41, 0.41, 0.97)]
    [InlineData(RealDataFixture.Season2526, 0.228, 0.215, 0.260, 1.0, 1.0, 1.0)]
    public void DefaultsReproduceTheCategoryTestsOfRuns9And10(int seasonId, double dixonColes, double elo, double poisson, double dixonColesReturning, double eloReturning, double poissonReturning)
    {
        var comparisons = Sweep(seasonId).Summaries.First(s => s.Variant.IsDefault).PromotedByCategory.Comparisons;
        PromotedTeamComparison Returning(string modelName) => comparisons.Single(c => c.ModelName == modelName && c.Category == PromotedCategory.Returning);

        Assert.Equal(dixonColes, Returning(PredictionModelFactory.DixonColes).OtherMean, TeamMeanTolerance);
        Assert.Equal(elo, Returning(PredictionModelFactory.Elo).OtherMean, TeamMeanTolerance);
        Assert.Equal(poisson, Returning(PredictionModelFactory.Poisson).OtherMean, TeamMeanTolerance);
        Assert.Equal(dixonColesReturning, Returning(PredictionModelFactory.DixonColes).AdjustedPValue, CategoryPValueTolerance);
        Assert.Equal(eloReturning, Returning(PredictionModelFactory.Elo).AdjustedPValue, CategoryPValueTolerance);
        Assert.Equal(poissonReturning, Returning(PredictionModelFactory.Poisson).AdjustedPValue, CategoryPValueTolerance);
        Assert.All(comparisons, c => Assert.Equal(99, c.OtherCount));
    }

    [Theory]
    [Trait("Category", "RealData")]
    [InlineData(RealDataFixture.Season2122, 0.2103, 0.2140)]
    [InlineData(RealDataFixture.Season2223, 0.2067, 0.2169)]
    [InlineData(RealDataFixture.Season2324, 0.2318, 0.2321)]
    [InlineData(RealDataFixture.Season2425, 0.2186, 0.2253)]
    [InlineData(RealDataFixture.Season2526, 0.2207, 0.2352)]
    public void LiteratureTimeDecayAndFormsOffReproduceTheDocumentedMeans(int seasonId, double literatureTimeDecay, double formsOff)
    {
        var summaries = Sweep(seasonId).Summaries;

        Assert.Equal(literatureTimeDecay, summaries.Single(s => s.Variant.Label == LiteratureLabel).Means!.RankedProbability, MeanTolerance);
        Assert.Equal(formsOff, summaries.Single(s => s.Variant.Label == FormsOffLabel).Means!.RankedProbability, MeanTolerance);
    }

    [Fact]
    [Trait("Category", "RealData")]
    public void OnlyTheFastestTimeDecayIn2023_24FailsToConverge()
    {
        var failed = Sweeps.Value
            .SelectMany(sweep => sweep.Outcomes.Where(o => !o.Succeeded).Select(o => (sweep.Season.SeasonId, o)))
            .ToList();

        var (seasonId, outcome) = Assert.Single(failed);
        Assert.Equal(RealDataFixture.Season2324, seasonId);
        Assert.Equal("ξ = 0.015", outcome.Variant.Label);
        Assert.Contains("po kolejce 26", outcome.Error);
    }

    [Theory]
    [Trait("Category", "RealData")]
    [InlineData(RealDataFixture.Season2425, "ξ z literatury", "D<E<P", 0.7870, 0.0497, 0.0555)]
    [InlineData(RealDataFixture.Season2526, "ξ z literatury", "E<D<P", 0.2219, 0.0023, 0.0023)]
    [InlineData(RealDataFixture.Season2425, "formy wyłączone", "E<P<D", 0.1213, 0.6529, 0.6442)]
    [InlineData(RealDataFixture.Season2526, "formy wyłączone", "E<D<P", 0.0053, 0.5597, 0.0096)]
    [InlineData(RealDataFixture.Season2425, "ξ z literatury + formy wyłączone", "D<E<P", 0.7870, 0.7240, 0.7240)]
    [InlineData(RealDataFixture.Season2526, "ξ z literatury + formy wyłączone", "E<D<P", 0.2219, 0.0121, 0.0121)]
    public void ScenariosReproduceTheDocumentedConclusions(int seasonId, string scenarioName, string ranking, double dixonColesElo, double dixonColesPoisson, double eloPoisson)
    {
        var sweep = Sweep(seasonId);
        var scenario = SensitivitySummary.Scenario(sweep.Season, sweep.Outcomes, Scenarios.Single(s => s.Name == scenarioName).Labels);

        Assert.Equal(ranking, RankingCode(scenario.Ranking));
        Assert.Equal(dixonColesElo, Holm(scenario.Pairwise, PredictionModelFactory.DixonColes, PredictionModelFactory.Elo), MeanTolerance);
        Assert.Equal(dixonColesPoisson, Holm(scenario.Pairwise, PredictionModelFactory.DixonColes, PredictionModelFactory.Poisson), MeanTolerance);
        Assert.Equal(eloPoisson, Holm(scenario.Pairwise, PredictionModelFactory.Elo, PredictionModelFactory.Poisson), MeanTolerance);
    }

    [Theory]
    [Trait("Category", "RealData")]
    [MemberData(nameof(SeasonIds))]
    public void EloInitialRatingDoesNotChangeEvaluations(int seasonId)
    {
        var sweep = Sweep(seasonId);
        var baseline = Default(sweep, PredictionModelFactory.Elo).Evaluations!;
        var shifted = sweep.Outcomes.Single(o => o.Variant.Parameter == nameof(TrainingOptions.EloInitialRating)).Evaluations!;
        var (rps, brier, logLoss) = MaxDifferences(baseline, shifted);

        _output.WriteLine(Invariant($"{SeasonNames[seasonId]}: start 1500 vs 1300 — maks. |ΔRPS| {rps:E1}, |ΔBrier| {brier:E1}, |ΔLogLoss| {logLoss:E1}"));

        Assert.Equal(baseline.Select(e => e.MatchId), shifted.Select(e => e.MatchId));
        Assert.InRange(rps, 0, InvarianceTolerance);
        Assert.InRange(brier, 0, InvarianceTolerance);
        Assert.InRange(logLoss, 0, InvarianceTolerance);
    }

    [Fact]
    [Trait("Category", "RealData")]
    public void ValidationSeasonsUseTheWinterBreak()
    {
        int Cutoff(int seasonId) => SeasonCutoffs.Single(season => season.SeasonId == seasonId).TrainingLastRound;
        int? Detected(int seasonId) => SeasonCalendar.DetectSplit(RealDataFixture.Matches.Where(m => m.SeasonId == seasonId).ToList()).AutumnLastRound;

        Assert.Equal(Detected(RealDataFixture.Season2122), Cutoff(RealDataFixture.Season2122));
        Assert.Equal(Detected(RealDataFixture.Season2223), Cutoff(RealDataFixture.Season2223));
        Assert.Equal(25, Detected(RealDataFixture.Season2324));
        Assert.Equal(19, Cutoff(RealDataFixture.Season2324));
        Assert.Equal(Detected(RealDataFixture.Season2425), Cutoff(RealDataFixture.Season2425));
        Assert.Equal(Detected(RealDataFixture.Season2526), Cutoff(RealDataFixture.Season2526));

        Assert.Equal(
            [135, 153, 135, 144, 144],
            SeasonCutoffs.Select(season => Sweep(season.SeasonId).Season.Evaluation.Count));
    }

    [Theory]
    [Trait("Category", "RealData")]
    [MemberData(nameof(SeasonIds))]
    public void DefaultVariantsConverge(int seasonId)
    {
        var sweep = Sweep(seasonId);

        Assert.Equal(SensitivityGrid.Default, sweep.Outcomes.Select(o => o.Variant));
        Assert.All(sweep.Outcomes.Where(o => o.Variant.IsDefault), o => Assert.True(o.Succeeded, o.Error));
        Assert.All(sweep.Outcomes.Where(o => o.Succeeded), o => Assert.Equal(sweep.Season.Evaluation.Count, o.Evaluations!.Count));
    }

    [Fact]
    [Trait("Category", "RealData")]
    public void Report()
    {
        foreach (var sweep in Sweeps.Value)
        {
            ReportSeason(sweep);
        }

        ReportCrossSection();
        ReportScenarios();
        ReportBaseline();

        Assert.Equal(SeasonCutoffs.Length, Sweeps.Value.Count);
    }

    private void ReportSeason(SeasonSweep sweep)
    {
        var season = sweep.Season;
        var evaluated = EvaluatedSeasons.Contains(season.SeasonId);

        _output.WriteLine("");
        _output.WriteLine(Invariant($"=== Sezon {SeasonNames[season.SeasonId]} (Id {season.SeasonId}): odcięcie po k.{season.TrainingLastRound}, {season.Evaluation.Count} meczów wiosny, {(evaluated ? "ewaluacja" : "walidacja")} ==="));

        if (evaluated)
        {
            _output.WriteLine("Beniaminki: " + string.Join(", ", season.PromotedTeams.Select(team =>
                Invariant($"{team.TeamId} ({team.Category}, {team.PriorMatchCount} meczów, ostatnio {team.LastPriorSeasonId?.ToString() ?? "—"})"))));
        }

        var header = Invariant($"{"model",-11} {"parametr",-16} {"wartość",-26} {"RPS",7} {"ΔRPS",8} {"Brier",7} {"LogLoss",7} {"ranking",-7} {"E–P",6} {"DC–E",6} {"DC–P",6}");
        _output.WriteLine(evaluated ? header + "   binarnie (Holm) | reszta zimny  powr.  | Holm kat.   | per drużyna" : header);

        foreach (var summary in sweep.Summaries)
        {
            var variant = summary.Variant;
            var prefix = Invariant($"{variant.ModelName,-11} {Shorten(variant.Parameter),-16} {variant.Label,-26}");

            if (!summary.Succeeded)
            {
                _output.WriteLine($"{prefix} nie zbiega: {summary.Error}");
                continue;
            }

            var line = Invariant($"{prefix} {summary.Means!.RankedProbability,7:F4} {summary.Delta!.RankedProbability,8:+0.0000;-0.0000;0} {summary.Means.Brier,7:F4} {summary.Means.LogLoss,7:F4} {RankingCode(summary.Ranking),-7} {Holm(summary.Pairwise, PredictionModelFactory.Elo, PredictionModelFactory.Poisson),6:F4} {Holm(summary.Pairwise, PredictionModelFactory.DixonColes, PredictionModelFactory.Elo),6:F4} {Holm(summary.Pairwise, PredictionModelFactory.DixonColes, PredictionModelFactory.Poisson),6:F4}");

            if (evaluated)
            {
                var binary = summary.Promoted.Single(c => c.ModelName == variant.ModelName);
                line += Invariant($"   {binary.Difference,7:+0.0000;-0.0000} ({binary.AdjustedPValue:F2}) | ") + PromotedColumns(summary.PromotedByCategory, variant.ModelName);
            }

            _output.WriteLine(line);
        }
    }

    private void ReportCrossSection()
    {
        _output.WriteLine("");
        _output.WriteLine("=== Przekrój: RPS wariantu (ranking z pozostałymi modelami domyślnymi) w sezonach ===");
        _output.WriteLine(Invariant($"{"model",-11} {"wartość",-26} ") + string.Join(" ", SeasonCutoffs.Select(season => Invariant($"{SeasonNames[season.SeasonId],-15}"))));

        for (var index = 0; index < SensitivityGrid.Default.Count; index++)
        {
            var variant = SensitivityGrid.Default[index];
            var cells = Sweeps.Value.Select(sweep =>
            {
                var summary = sweep.Summaries[index];
                return summary.Succeeded
                    ? Invariant($"{summary.Means!.RankedProbability:F4} {RankingCode(summary.Ranking),-9}")
                    : Invariant($"{"nie zbiega",-15}");
            });

            _output.WriteLine(Invariant($"{variant.ModelName,-11} {variant.Label,-26} ") + string.Join(" ", cells));
        }
    }

    private void ReportScenarios()
    {
        _output.WriteLine("");
        _output.WriteLine("=== Scenariusze (wariant per model; Holm jak w runie) ===");

        foreach (var (name, labels) in Scenarios)
        {
            _output.WriteLine($"--- {name}");

            foreach (var sweep in Sweeps.Value)
            {
                var scenario = SensitivitySummary.Scenario(sweep.Season, sweep.Outcomes, labels);
                var means = string.Join(" ", ModelOrder.Select(model => Invariant($"{Short(model)} {scenario.Means[model].RankedProbability:F4}")));
                var line = Invariant($"  {SeasonNames[sweep.Season.SeasonId]}: {means} | {RankingCode(scenario.Ranking),-7} | Holm E–P {Holm(scenario.Pairwise, PredictionModelFactory.Elo, PredictionModelFactory.Poisson):F4}, DC–E {Holm(scenario.Pairwise, PredictionModelFactory.DixonColes, PredictionModelFactory.Elo):F4}, DC–P {Holm(scenario.Pairwise, PredictionModelFactory.DixonColes, PredictionModelFactory.Poisson):F4}");

                if (EvaluatedSeasons.Contains(sweep.Season.SeasonId))
                {
                    line += " | powr. − reszta: " + string.Join(" ", ModelOrder.Select(model =>
                    {
                        var returning = scenario.PromotedByCategory.Comparisons.Single(c => c.ModelName == model && c.Category == PromotedCategory.Returning);
                        return Invariant($"{Short(model)} {returning.Difference:+0.000;-0.000} (Holm {returning.AdjustedPValue:F2})");
                    }));
                }

                _output.WriteLine(line);
            }
        }
    }

    private void ReportBaseline()
    {
        _output.WriteLine("");
        _output.WriteLine("=== Stała linia bazowa: P(1/X/2) z częstości w historii treningowej ===");

        foreach (var sweep in Sweeps.Value)
        {
            var season = sweep.Season;
            var probabilities = ConstantBaseline.Fit(season.History, RealDataFixture.LeagueId);
            var promotedIds = season.PromotedTeams.Select(team => team.TeamId).ToHashSet();
            var baseline = ConstantBaseline.Evaluate(probabilities, season.Evaluation, promotedIds);
            var models = ModelOrder.ToDictionary(name => name, name => Default(sweep, name).Evaluations!);

            _output.WriteLine(Invariant($"{SeasonNames[season.SeasonId]}: P = {probabilities.HomeWin:F4} / {probabilities.Draw:F4} / {probabilities.AwayWin:F4}, RPS {MetricMeans.Of(baseline).RankedProbability:F4} (modele: {string.Join(" ", ModelOrder.Select(model => Invariant($"{Short(model)} {MetricMeans.Of(models[model]).RankedProbability:F4}")))})"));

            if (!EvaluatedSeasons.Contains(season.SeasonId))
            {
                continue;
            }

            var baselineSet = new Dictionary<string, IReadOnlyList<MatchEvaluation>> { [ConstantBaseline.Name] = baseline };
            var binary = ModelComparison.PromotedVersusRest(baselineSet, MetricKind.RankedProbability).Single();
            var byCategory = ModelComparison.PromotedByCategory(baselineSet, MetricKind.RankedProbability, season.TeamsByMatch, season.PromotedTeams);
            var defaults = sweep.Summaries.First(s => s.Variant.IsDefault).PromotedByCategory;

            _output.WriteLine(Invariant($"  binarnie: beniaminki {binary.PromotedMean:F4} ({binary.PromotedCount}) vs reszta {binary.OtherMean:F4} ({binary.OtherCount}), różnica {binary.Difference:+0.0000;-0.0000}, p {binary.Test.PValue:F4}"));
            _output.WriteLine(Invariant($"  {"",-11}   reszta zimny  powr.  | Holm kat.   | per drużyna"));
            _output.WriteLine(Invariant($"  {ConstantBaseline.Name,-11} ") + PromotedColumns(byCategory, ConstantBaseline.Name));

            foreach (var model in ModelOrder)
            {
                _output.WriteLine(Invariant($"  {model,-11} ") + PromotedColumns(defaults, model));
            }

            foreach (var category in byCategory.Comparisons.Select(c => c.Category!.Value).Distinct())
            {
                var excess = new[] { ConstantBaseline.Name }.Concat(ModelOrder).Select(model =>
                {
                    var analysis = model == ConstantBaseline.Name ? byCategory : defaults;
                    var row = analysis.Comparisons.Single(c => c.ModelName == model && c.Category == category);
                    return Invariant($"{Short(model)} {row.Difference:+0.000;-0.000}");
                });

                _output.WriteLine($"  nadwyżka {category} − reszta: {string.Join(" ", excess)}");
            }

            foreach (var team in season.PromotedTeams)
            {
                var excess = new[] { ConstantBaseline.Name }.Concat(ModelOrder).Select(model =>
                {
                    var analysis = model == ConstantBaseline.Name ? byCategory : defaults;
                    var mean = analysis.TeamMetrics.Single(m => m.ModelName == model && m.TeamId == team.TeamId).Mean;
                    var rest = analysis.Comparisons.First(c => c.ModelName == model).OtherMean;
                    return Invariant($"{Short(model)} {mean - rest:+0.000;-0.000}");
                });

                _output.WriteLine(Invariant($"  nadwyżka drużyny {team.TeamId} ({team.PriorMatchCount} meczów) − reszta: {string.Join(" ", excess)}"));
            }
        }
    }

    private static string PromotedColumns(PromotedCategoryAnalysis analysis, string modelName)
    {
        var rows = analysis.Comparisons.Where(c => c.ModelName == modelName).ToList();
        string Mean(PromotedCategory category) => rows.FirstOrDefault(c => c.Category == category) is { } row ? Invariant($"{row.PromotedMean:F4}") : "  —   ";
        string Holm(PromotedCategory category) => rows.FirstOrDefault(c => c.Category == category) is { } row ? Invariant($"{row.AdjustedPValue:F2}") : " — ";
        var teams = string.Join(" ", analysis.TeamMetrics.Where(m => m.ModelName == modelName).Select(m => Invariant($"{m.TeamId}:{m.Mean:F3}")));

        return Invariant($"{rows[0].OtherMean:F4} {Mean(PromotedCategory.ColdStart)} {Mean(PromotedCategory.Returning)} | {Holm(PromotedCategory.ColdStart),4} {Holm(PromotedCategory.Returning),4}   | {teams}");
    }

    private static IReadOnlyList<SeasonSweep> ComputeSweeps()
    {
        return SeasonCutoffs
            .AsParallel()
            .AsOrdered()
            .Select(season =>
            {
                var built = Built(season.SeasonId, season.TrainingLastRound) with { Options = RealDataFixture.Options(season.SeasonId) };
                var outcomes = SensitivitySweep.Run(built, SensitivityGrid.Default, Environment.ProcessorCount);

                return new SeasonSweep(built, outcomes, SensitivitySummary.Summarise(built, outcomes));
            })
            .ToList();
    }

    private static SensitivitySeason Built(int seasonId, int trainingLastRound)
    {
        return SensitivitySeason.Build(
            RealDataFixture.Matches,
            RealDataFixture.SeasonChronology,
            RealDataFixture.LeagueId,
            seasonId,
            trainingLastRound,
            RealDataFixture.PromotedTeamIds(seasonId));
    }

    private static SeasonSweep Sweep(int seasonId) => Sweeps.Value.Single(sweep => sweep.Season.SeasonId == seasonId);

    private static VariantOutcome Default(SeasonSweep sweep, string modelName)
    {
        return sweep.Outcomes.Single(o => o.Variant.ModelName == modelName && o.Variant.IsDefault);
    }

    private static double Holm(IReadOnlyList<PairwiseComparison> pairs, string first, string second)
    {
        return pairs.Single(p => (p.FirstModel == first && p.SecondModel == second) || (p.FirstModel == second && p.SecondModel == first)).AdjustedPValue;
    }

    private static (double Rps, double Brier, double LogLoss) MaxDifferences(IReadOnlyList<MatchEvaluation> first, IReadOnlyList<MatchEvaluation> second)
    {
        var pairs = first.Zip(second).ToList();

        return (
            pairs.Max(pair => Math.Abs(pair.First.RankedProbability - pair.Second.RankedProbability)),
            pairs.Max(pair => Math.Abs(pair.First.Brier - pair.Second.Brier)),
            pairs.Max(pair => Math.Abs(pair.First.LogLoss - pair.Second.LogLoss)));
    }

    private static string RankingCode(IReadOnlyList<string> ranking) => string.Join("<", ranking.Select(Short));

    private static string Short(string modelName) => modelName switch
    {
        PredictionModelFactory.DixonColes => "D",
        PredictionModelFactory.Elo => "E",
        PredictionModelFactory.Poisson => "P",
        _ => "S"
    };

    private static string Shorten(string parameter) => parameter.Contains('/') ? "wagi horyzontów" : parameter;

    private static string Invariant(FormattableString value) => value.ToString(CultureInfo.InvariantCulture);

    private static object Fingerprint(MatchEvaluation e)
    {
        return (e.MatchId, e.ModelName, e.Round, e.InvolvesPromotedTeam, e.Actual, e.Predicted, e.Brier, e.RankedProbability, e.LogLoss,
            e.OutcomeCorrect, e.ExactScoreCorrect, e.ExactScoreInTopThree, e.ProbabilityOfActualScore, e.ProbabilityOfActualOutcome);
    }

    private sealed record SeasonSweep(SensitivitySeason Season, IReadOnlyList<VariantOutcome> Outcomes, IReadOnlyList<VariantSummary> Summaries);
}
