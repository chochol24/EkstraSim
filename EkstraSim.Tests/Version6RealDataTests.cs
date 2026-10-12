using System.Globalization;
using EkstraSim.Prediction.Evaluation;
using EkstraSim.Prediction.Metrics;
using EkstraSim.Prediction.Models;
using EkstraSim.Prediction.Statistics;
using Xunit.Abstractions;

namespace EkstraSim.Tests;

public class Version6RealDataTests
{
    private const int DiagnosticCutoff = 3;
    private const int FirstSpringRound = RealDataFixture.TrainingLastRound + 1;
    private const double MeanTolerance = 5e-5;
    private const double RhoTolerance = 5e-4;

    private static readonly string[] ModelOrder = [PredictionModelFactory.Elo, PredictionModelFactory.DixonColes, PredictionModelFactory.Poisson];

    private static readonly (int SeasonId, int TrainingLastRound)[] RunCutoffs =
    [
        (RealDataFixture.Season2425, RealDataFixture.TrainingLastRound),
        (RealDataFixture.Season2526, RealDataFixture.TrainingLastRound),
        (RealDataFixture.Season2425, DiagnosticCutoff),
        (RealDataFixture.Season2526, DiagnosticCutoff)
    ];

    private static readonly double[] Tolerances = [0.10, StabilityAnalysis.DefaultTolerance, 0.50];

    private static readonly Lazy<IReadOnlyList<RunEvaluation>> Runs = new(ComputeRuns);

    private readonly ITestOutputHelper _output;

    public Version6RealDataTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public static IEnumerable<object[]> RunKeys => RunCutoffs.Select(run => new object[] { run.SeasonId, run.TrainingLastRound });

    [Theory]
    [Trait("Category", "RealData")]
    [MemberData(nameof(RunKeys))]
    public void OnlyDixonColesChangesFromVersion5(int seasonId, int trainingLastRound)
    {
        var run = Run(seasonId, trainingLastRound);

        foreach (var modelName in new[] { PredictionModelFactory.Elo, PredictionModelFactory.Poisson })
        {
            Assert.Equal(Sorted(run.Version5[modelName]).Select(Fingerprint), Sorted(run.Version6[modelName]).Select(Fingerprint));
            Assert.Equal(run.Version5[modelName].Select(r => (r.Round, r.ParameterDrift)), run.Version6[modelName].Select(r => (r.Round, r.ParameterDrift)));
        }

        Assert.NotEqual(Sorted(run.Version5[PredictionModelFactory.DixonColes]).Select(Fingerprint), Sorted(run.Version6[PredictionModelFactory.DixonColes]).Select(Fingerprint));
        Assert.Equal(run.Season.Evaluation.Count, Sorted(run.Version6[PredictionModelFactory.DixonColes]).Count);
    }

    [Theory]
    [Trait("Category", "RealData")]
    [InlineData(RealDataFixture.Season2425, RealDataFixture.TrainingLastRound, 0)]
    [InlineData(RealDataFixture.Season2526, RealDataFixture.TrainingLastRound, 72)]
    [InlineData(RealDataFixture.Season2425, DiagnosticCutoff, 135)]
    [InlineData(RealDataFixture.Season2526, DiagnosticCutoff, 205)]
    public void DateFilterNarrowingsDoNotDependOnTheTimeDecay(int seasonId, int trainingLastRound, int expectedNarrowed)
    {
        var run = Run(seasonId, trainingLastRound);

        foreach (var modelName in ModelOrder)
        {
            Assert.Equal(expectedNarrowed, run.Version6[modelName].Sum(r => r.DateFilteredMatchIds.Count));
            Assert.Equal(run.Version5[modelName].SelectMany(r => r.DateFilteredMatchIds), run.Version6[modelName].SelectMany(r => r.DateFilteredMatchIds));
        }
    }

    [Theory]
    [Trait("Category", "RealData")]
    [InlineData(RealDataFixture.Season2425, 0.2189, 0.2186, 0.2518, "D<E<P", 0.7870, 0.0497, 0.0555)]
    [InlineData(RealDataFixture.Season2526, 0.2169, 0.2207, 0.2604, "E<D<P", 0.2219, 0.0023, 0.0023)]
    public void Version6ReproducesTheLiteratureScenario(int seasonId, double elo, double dixonColes, double poisson, string ranking, double dixonColesElo, double dixonColesPoisson, double eloPoisson)
    {
        var run = Run(seasonId, RealDataFixture.TrainingLastRound);
        var byModel = ByModel(run);
        var pairs = ModelComparison.Pairwise(byModel, MetricKind.RankedProbability);

        Assert.Equal(elo, MetricMeans.Of(byModel[PredictionModelFactory.Elo]).RankedProbability, MeanTolerance);
        Assert.Equal(dixonColes, MetricMeans.Of(byModel[PredictionModelFactory.DixonColes]).RankedProbability, MeanTolerance);
        Assert.Equal(poisson, MetricMeans.Of(byModel[PredictionModelFactory.Poisson]).RankedProbability, MeanTolerance);
        Assert.Equal(ranking, RankingCode(Ranking(byModel)));
        Assert.Equal(dixonColesElo, Holm(pairs, PredictionModelFactory.DixonColes, PredictionModelFactory.Elo), MeanTolerance);
        Assert.Equal(dixonColesPoisson, Holm(pairs, PredictionModelFactory.DixonColes, PredictionModelFactory.Poisson), MeanTolerance);
        Assert.Equal(eloPoisson, Holm(pairs, PredictionModelFactory.Elo, PredictionModelFactory.Poisson), MeanTolerance);
    }

    [Theory]
    [Trait("Category", "RealData")]
    [InlineData(RealDataFixture.Season2425, PredictionModelFactory.Elo, 0.6211, 1.0333, 65, 23, 46)]
    [InlineData(RealDataFixture.Season2425, PredictionModelFactory.DixonColes, 0.6215, 1.0339, 64, 16, 44)]
    [InlineData(RealDataFixture.Season2425, PredictionModelFactory.Poisson, 0.6941, 1.1493, 56, 12, 30)]
    [InlineData(RealDataFixture.Season2526, PredictionModelFactory.Elo, 0.6345, 1.0534, 65, 18, 49)]
    [InlineData(RealDataFixture.Season2526, PredictionModelFactory.DixonColes, 0.6457, 1.0699, 64, 16, 51)]
    [InlineData(RealDataFixture.Season2526, PredictionModelFactory.Poisson, 0.7324, 1.2145, 58, 17, 40)]
    public void EvaluationRunsPinTheMetricsAndHits(int seasonId, string modelName, double brier, double logLoss, int outcomeHits, int exactScoreHits, int topThreeHits)
    {
        var evaluations = ByModel(Run(seasonId, RealDataFixture.TrainingLastRound))[modelName];
        var means = MetricMeans.Of(evaluations);

        Assert.Equal(brier, means.Brier, MeanTolerance);
        Assert.Equal(logLoss, means.LogLoss, MeanTolerance);
        Assert.Equal(outcomeHits, evaluations.Count(e => e.OutcomeCorrect));
        Assert.Equal(exactScoreHits, evaluations.Count(e => e.ExactScoreCorrect));
        Assert.Equal(topThreeHits, evaluations.Count(e => e.ExactScoreInTopThree));
    }

    [Theory]
    [Trait("Category", "RealData")]
    [InlineData(RealDataFixture.Season2425, 22, 0.2585, 0.2659, 0.3012)]
    [InlineData(RealDataFixture.Season2425, 28, 0.2367, 0.2367, 0.2315)]
    [InlineData(RealDataFixture.Season2425, 29, 0.2151, 0.2201, 0.2590)]
    [InlineData(RealDataFixture.Season2526, 20, 0.2837, 0.3030, 0.3410)]
    [InlineData(RealDataFixture.Season2526, 23, 0.2110, 0.2125, 0.2439)]
    [InlineData(RealDataFixture.Season2526, 27, 0.1938, 0.2035, 0.2331)]
    public void EvaluationRunsPinThePromotedTeamMeans(int seasonId, int teamId, double elo, double dixonColes, double poisson)
    {
        var run = Run(seasonId, RealDataFixture.TrainingLastRound);
        var teamMetrics = ModelComparison.PromotedByCategory(ByModel(run), MetricKind.RankedProbability, run.Season.TeamsByMatch, run.Season.PromotedTeams).TeamMetrics;
        double Mean(string modelName) => teamMetrics.Single(m => m.TeamId == teamId && m.ModelName == modelName).Mean;

        Assert.Equal(elo, Mean(PredictionModelFactory.Elo), MeanTolerance);
        Assert.Equal(dixonColes, Mean(PredictionModelFactory.DixonColes), MeanTolerance);
        Assert.Equal(poisson, Mean(PredictionModelFactory.Poisson), MeanTolerance);
        Assert.All(teamMetrics.Where(m => m.TeamId == teamId), m => Assert.Equal(16, m.Count));
    }

    [Theory]
    [Trait("Category", "RealData")]
    [InlineData(RealDataFixture.Season2425, PredictionModelFactory.Elo, 0.0273, 0.2404, 0.2104, 0.0157, 0.9716, 0.0482, 0.3738)]
    [InlineData(RealDataFixture.Season2425, PredictionModelFactory.DixonColes, 0.0311, 0.2404, 0.2089, 0.0175, 0.9716, 0.0570, 0.2890)]
    [InlineData(RealDataFixture.Season2425, PredictionModelFactory.Poisson, 0.0159, 0.5096, 0.2469, -0.0012, 0.9716, 0.0544, 0.9716)]
    [InlineData(RealDataFixture.Season2526, PredictionModelFactory.Elo, 0.0063, 1.0, 0.2150, null, null, 0.0063, 1.0)]
    [InlineData(RealDataFixture.Season2526, PredictionModelFactory.DixonColes, 0.0137, 1.0, 0.2164, null, null, 0.0137, 1.0)]
    [InlineData(RealDataFixture.Season2526, PredictionModelFactory.Poisson, 0.0006, 1.0, 0.2602, null, null, 0.0006, 1.0)]
    public void EvaluationRunsPinThePromotedTeamTests(int seasonId, string modelName, double binaryDifference, double binaryHolm, double otherMean, double? coldStartDifference, double? coldStartHolm, double returningDifference, double returningHolm)
    {
        var run = Run(seasonId, RealDataFixture.TrainingLastRound);
        var byModel = ByModel(run);
        var binary = ModelComparison.PromotedVersusRest(byModel, MetricKind.RankedProbability).Single(c => c.ModelName == modelName);
        var categories = ModelComparison.PromotedByCategory(byModel, MetricKind.RankedProbability, run.Season.TeamsByMatch, run.Season.PromotedTeams).Comparisons
            .Where(c => c.ModelName == modelName)
            .ToList();
        var coldStart = categories.SingleOrDefault(c => c.Category == PromotedCategory.ColdStart);
        var returning = categories.Single(c => c.Category == PromotedCategory.Returning);

        Assert.Equal(binaryDifference, binary.Difference, MeanTolerance);
        Assert.Equal(binaryHolm, binary.AdjustedPValue, MeanTolerance);
        Assert.Equal(otherMean, returning.OtherMean, MeanTolerance);
        Assert.Equal(coldStartDifference.HasValue, coldStart != null);
        if (coldStart != null)
        {
            Assert.Equal(coldStartDifference!.Value, coldStart.Difference, MeanTolerance);
            Assert.Equal(coldStartHolm!.Value, coldStart.AdjustedPValue, MeanTolerance);
        }

        Assert.Equal(returningDifference, returning.Difference, MeanTolerance);
        Assert.Equal(returningHolm, returning.AdjustedPValue, MeanTolerance);
    }

    [Theory]
    [Trait("Category", "RealData")]
    [InlineData(RealDataFixture.Season2425, RealDataFixture.TrainingLastRound, PredictionModelFactory.Elo, 0.0839, 0.0863, 33, 19, 19)]
    [InlineData(RealDataFixture.Season2425, RealDataFixture.TrainingLastRound, PredictionModelFactory.DixonColes, 0.1320, 0.1355, 33, 19, 19)]
    [InlineData(RealDataFixture.Season2425, RealDataFixture.TrainingLastRound, PredictionModelFactory.Poisson, 0.1606, 0.1780, 33, 20, 19)]
    [InlineData(RealDataFixture.Season2526, RealDataFixture.TrainingLastRound, PredictionModelFactory.Elo, 0.0978, 0.1037, 27, 20, 19)]
    [InlineData(RealDataFixture.Season2526, RealDataFixture.TrainingLastRound, PredictionModelFactory.DixonColes, 0.1359, 0.1326, 19, 19, 19)]
    [InlineData(RealDataFixture.Season2526, RealDataFixture.TrainingLastRound, PredictionModelFactory.Poisson, 0.1735, 0.1744, 19, 19, 19)]
    [InlineData(RealDataFixture.Season2425, DiagnosticCutoff, PredictionModelFactory.Elo, 0.0842, 0.0894, 24, 14, 4)]
    [InlineData(RealDataFixture.Season2425, DiagnosticCutoff, PredictionModelFactory.DixonColes, 0.1310, 0.2186, 33, 18, 16)]
    [InlineData(RealDataFixture.Season2425, DiagnosticCutoff, PredictionModelFactory.Poisson, 0.1635, 0.2716, 33, 20, 16)]
    [InlineData(RealDataFixture.Season2526, DiagnosticCutoff, PredictionModelFactory.Elo, 0.1004, 0.0991, 20, 4, 4)]
    [InlineData(RealDataFixture.Season2526, DiagnosticCutoff, PredictionModelFactory.DixonColes, 0.1384, 0.1676, 18, 13, 13)]
    [InlineData(RealDataFixture.Season2526, DiagnosticCutoff, PredictionModelFactory.Poisson, 0.1720, 0.2549, 20, 20, 18)]
    public void RunsPinTheStability(int seasonId, int trainingLastRound, string modelName, double driftLevel, double meanRollingDrift, int settledAt010, int settledAt025, int settledAt050)
    {
        var observations = Observations(Run(seasonId, trainingLastRound).Version6);
        StabilityResult At(double tolerance) => StabilityAnalysis.DetectAll(observations, tolerance).Single(r => r.ModelName == modelName);

        Assert.Equal(driftLevel, At(StabilityAnalysis.DefaultTolerance).DriftLevel, MeanTolerance);
        Assert.Equal(meanRollingDrift, At(StabilityAnalysis.DefaultTolerance).RollingDrift.Average(), MeanTolerance);
        Assert.Equal(settledAt010, At(0.10).StabilisedFromRound);
        Assert.Equal(settledAt025, At(StabilityAnalysis.DefaultTolerance).StabilisedFromRound);
        Assert.Equal(settledAt050, At(0.50).StabilisedFromRound);
    }

    [Theory]
    [Trait("Category", "RealData")]
    [InlineData(RealDataFixture.Season2425, PredictionModelFactory.Elo, null, null, -0.200, 0.2817)]
    [InlineData(RealDataFixture.Season2425, PredictionModelFactory.DixonColes, null, null, -0.768, 0.0)]
    [InlineData(RealDataFixture.Season2425, PredictionModelFactory.Poisson, null, null, -0.874, 0.0)]
    [InlineData(RealDataFixture.Season2425, PredictionModelFactory.Elo, null, RealDataFixture.TrainingLastRound, -0.236, 0.3977)]
    [InlineData(RealDataFixture.Season2425, PredictionModelFactory.DixonColes, null, RealDataFixture.TrainingLastRound, -0.679, 0.0108)]
    [InlineData(RealDataFixture.Season2425, PredictionModelFactory.Poisson, null, RealDataFixture.TrainingLastRound, -0.807, 0.0008)]
    [InlineData(RealDataFixture.Season2425, PredictionModelFactory.Elo, FirstSpringRound, null, -0.171, 0.5276)]
    [InlineData(RealDataFixture.Season2425, PredictionModelFactory.DixonColes, FirstSpringRound, null, -0.326, 0.4343)]
    [InlineData(RealDataFixture.Season2425, PredictionModelFactory.Poisson, FirstSpringRound, null, -0.494, 0.1552)]
    [InlineData(RealDataFixture.Season2526, PredictionModelFactory.Elo, null, null, 0.104, 0.5761)]
    [InlineData(RealDataFixture.Season2526, PredictionModelFactory.DixonColes, null, null, -0.585, 0.0011)]
    [InlineData(RealDataFixture.Season2526, PredictionModelFactory.Poisson, null, null, -0.847, 0.0)]
    [InlineData(RealDataFixture.Season2526, PredictionModelFactory.Elo, null, RealDataFixture.TrainingLastRound, 0.246, 0.3760)]
    [InlineData(RealDataFixture.Season2526, PredictionModelFactory.DixonColes, null, RealDataFixture.TrainingLastRound, -0.529, 0.0856)]
    [InlineData(RealDataFixture.Season2526, PredictionModelFactory.Poisson, null, RealDataFixture.TrainingLastRound, -0.821, 0.0005)]
    [InlineData(RealDataFixture.Season2526, PredictionModelFactory.Elo, FirstSpringRound, null, -0.229, 0.7854)]
    [InlineData(RealDataFixture.Season2526, PredictionModelFactory.DixonColes, FirstSpringRound, null, 0.382, 0.4316)]
    [InlineData(RealDataFixture.Season2526, PredictionModelFactory.Poisson, FirstSpringRound, null, -0.147, 0.7854)]
    public void DiagnosticRunsPinTheTrends(int seasonId, string modelName, int? fromRound, int? toRound, double rho, double holm)
    {
        var observations = Observations(Run(seasonId, DiagnosticCutoff).Version6);
        var result = StabilityAnalysis.DetectAll(observations, StabilityAnalysis.DefaultTolerance, StabilityAnalysis.DefaultWindow, fromRound, toRound)
            .Single(r => r.ModelName == modelName);

        Assert.Equal(rho, result.Trend.Statistic, RhoTolerance);
        Assert.Equal(holm, result.TrendAdjustedPValue, MeanTolerance);
    }

    [Theory]
    [Trait("Category", "RealData")]
    [InlineData(RealDataFixture.Season2425)]
    [InlineData(RealDataFixture.Season2526)]
    public void DiagnosticDriftEqualsEvaluationDriftInSpring(int seasonId)
    {
        var evaluation = Run(seasonId, RealDataFixture.TrainingLastRound);
        var diagnostic = Run(seasonId, DiagnosticCutoff);

        foreach (var modelName in ModelOrder)
        {
            Assert.Equal(
                evaluation.Version6[modelName].Select(r => (r.Round, r.ParameterDrift)),
                diagnostic.Version6[modelName].Where(r => r.Round >= FirstSpringRound).Select(r => (r.Round, r.ParameterDrift)));
        }
    }

    [Fact]
    [Trait("Category", "RealData")]
    public void Report()
    {
        foreach (var run in Runs.Value)
        {
            ReportRun(run);
        }

        Assert.Equal(RunCutoffs.Length, Runs.Value.Count);
    }

    private void ReportRun(RunEvaluation run)
    {
        var season = run.Season;
        var byModel = ByModel(run);
        var version5 = run.Version5.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<MatchEvaluation>)Sorted(pair.Value));

        _output.WriteLine("");
        _output.WriteLine(Invariant($"=== Sezon {season.SeasonId}, odcięcie po k.{season.TrainingLastRound}: {season.Evaluation.Count} meczów, wersja 6 (ξ = {RealDataFixture.Options(season.SeasonId).TimeDecayXi:R}) ==="));
        _output.WriteLine(Invariant($"{"model",-11} {"RPS",8} {"Brier",8} {"LogLoss",8} {"1X2",10} {"top-1",10} {"top-3",10} {"zawężone",8} | Δ v6 − v5: {"RPS",10} {"Brier",10} {"LogLoss",10} {"1X2",4} {"top-1",5} {"top-3",5}"));

        foreach (var modelName in ModelOrder)
        {
            var current = byModel[modelName];
            var previous = version5[modelName];
            var means = MetricMeans.Of(current);
            var before = MetricMeans.Of(previous);

            _output.WriteLine(Invariant(
                $"{modelName,-11} {means.RankedProbability,8:F4} {means.Brier,8:F4} {means.LogLoss,8:F4} {Hits(current, e => e.OutcomeCorrect),10} {Hits(current, e => e.ExactScoreCorrect),10} {Hits(current, e => e.ExactScoreInTopThree),10} {run.Version6[modelName].Sum(r => r.DateFilteredMatchIds.Count),8} | {"",10} {means.RankedProbability - before.RankedProbability,10:+0.000000;-0.000000;0} {means.Brier - before.Brier,10:+0.000000;-0.000000;0} {means.LogLoss - before.LogLoss,10:+0.000000;-0.000000;0} {Count(current, e => e.OutcomeCorrect) - Count(previous, e => e.OutcomeCorrect),4:+0;-0;0} {Count(current, e => e.ExactScoreCorrect) - Count(previous, e => e.ExactScoreCorrect),5:+0;-0;0} {Count(current, e => e.ExactScoreInTopThree) - Count(previous, e => e.ExactScoreInTopThree),5:+0;-0;0}"));
        }

        var pairs = ModelComparison.Pairwise(byModel, MetricKind.RankedProbability);
        var pairsBefore = ModelComparison.Pairwise(version5, MetricKind.RankedProbability);
        _output.WriteLine(Invariant($"ranking {RankingCode(Ranking(byModel))} (wersja 5: {RankingCode(Ranking(version5))})"));

        foreach (var pair in pairs)
        {
            var before = pairsBefore.Single(p => p.FirstModel == pair.FirstModel && p.SecondModel == pair.SecondModel);
            _output.WriteLine(Invariant($"  {pair.FirstModel} vs {pair.SecondModel}: średnie {pair.FirstMean:F4} / {pair.SecondMean:F4}, p {pair.Test.PValue:F4}, Holm {pair.AdjustedPValue:F4} (wersja 5: p {before.Test.PValue:F4}, Holm {before.AdjustedPValue:F4})"));
        }

        ReportPromoted(run, byModel);
        ReportStability(run);
    }

    private void ReportPromoted(RunEvaluation run, IReadOnlyDictionary<string, IReadOnlyList<MatchEvaluation>> byModel)
    {
        var season = run.Season;
        var binary = ModelComparison.PromotedVersusRest(byModel, MetricKind.RankedProbability);
        var byCategory = ModelComparison.PromotedByCategory(byModel, MetricKind.RankedProbability, season.TeamsByMatch, season.PromotedTeams);

        _output.WriteLine("beniaminki binarnie (RPS, Holm):");

        foreach (var row in binary)
        {
            _output.WriteLine(Invariant($"  {row.ModelName,-11} {row.PromotedMean:F4} ({row.PromotedCount}) vs {row.OtherMean:F4} ({row.OtherCount}), różnica {row.Difference:+0.0000;-0.0000}, p {row.Test.PValue:F4}, Holm {row.AdjustedPValue:F4}"));
        }

        _output.WriteLine("kategorie (RPS, Holm kategorii):");

        foreach (var row in byCategory.Comparisons)
        {
            _output.WriteLine(Invariant($"  {row.ModelName,-11} {row.Category,-10} {row.PromotedMean:F4} ({row.PromotedCount}) vs {row.OtherMean:F4} ({row.OtherCount}), różnica {row.Difference:+0.0000;-0.0000}, p {row.Test.PValue:F4}, Holm {row.AdjustedPValue:F4}"));
        }

        foreach (var team in season.PromotedTeams)
        {
            var means = ModelOrder.Select(modelName => byCategory.TeamMetrics.Single(m => m.TeamId == team.TeamId && m.ModelName == modelName))
                .Select(m => Invariant($"{m.ModelName} {m.Mean:F4} ({m.Count})"));
            _output.WriteLine(Invariant($"  drużyna {team.TeamId} ({team.Category}, {team.PriorMatchCount} meczów): {string.Join(", ", means)}"));
        }
    }

    private void ReportStability(RunEvaluation run)
    {
        var ranges = run.Season.TrainingLastRound == DiagnosticCutoff
            ? new (int? From, int? To)[] { (null, null), (null, RealDataFixture.TrainingLastRound), (FirstSpringRound, null) }
            : new (int? From, int? To)[] { (null, null) };

        foreach (var (version, rounds) in new[] { ("6", run.Version6), ("5", run.Version5) })
        {
            var observations = Observations(rounds);

            foreach (var tolerance in Tolerances)
            {
                var results = StabilityAnalysis.DetectAll(observations, tolerance);
                _output.WriteLine(Invariant($"stabilność wersja {version}, δ = {tolerance:F2}: ") + string.Join("; ", results.Select(r =>
                    Invariant($"{r.ModelName} osiada od {r.StabilisedFromRound?.ToString(CultureInfo.InvariantCulture) ?? "—"}, L {r.DriftLevel:F4}, średni dryf {r.MeanDrift:F4}, średnia krocząca {r.RollingDrift.Average():F4}"))));
            }

            foreach (var (from, to) in ranges)
            {
                var results = StabilityAnalysis.DetectAll(observations, StabilityAnalysis.DefaultTolerance, StabilityAnalysis.DefaultWindow, from, to);
                _output.WriteLine(Invariant($"trend wersja {version}, kolejki {from?.ToString(CultureInfo.InvariantCulture) ?? "od początku"}–{to?.ToString(CultureInfo.InvariantCulture) ?? "34"}: ") + string.Join("; ", results.Select(r =>
                    Invariant($"{r.ModelName} ρ {r.Trend.Statistic:+0.000;-0.000} (p {r.Trend.PValue:F4}, Holm {r.TrendAdjustedPValue:F4})"))));
            }
        }
    }

    private static IReadOnlyList<RunEvaluation> ComputeRuns()
    {
        return RunCutoffs
            .AsParallel()
            .AsOrdered()
            .Select(run =>
            {
                var season = SensitivitySeason.Build(
                    RealDataFixture.Matches,
                    RealDataFixture.SeasonChronology,
                    RealDataFixture.LeagueId,
                    run.SeasonId,
                    run.TrainingLastRound,
                    RealDataFixture.PromotedTeamIds(run.SeasonId));

                return new RunEvaluation(
                    season,
                    WalkForward(season, RealDataFixture.Options(run.SeasonId)),
                    WalkForward(season, RealDataFixture.OptionsVersion5(run.SeasonId)));
            })
            .ToList();
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<RoundEvaluation>> WalkForward(SensitivitySeason season, TrainingOptions options)
    {
        var promotedIds = season.PromotedTeams.Select(team => team.TeamId).ToHashSet();

        return ModelOrder.ToDictionary(
            modelName => modelName,
            modelName => (IReadOnlyList<RoundEvaluation>)WalkForwardEvaluator.Run(
                PredictionModelFactory.Create(modelName), season.History, season.Evaluation, options, promotedIds));
    }

    private static RunEvaluation Run(int seasonId, int trainingLastRound)
    {
        return Runs.Value.Single(run => run.Season.SeasonId == seasonId && run.Season.TrainingLastRound == trainingLastRound);
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<MatchEvaluation>> ByModel(RunEvaluation run)
    {
        return run.Version6.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<MatchEvaluation>)Sorted(pair.Value));
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<RoundObservation>> Observations(IReadOnlyDictionary<string, IReadOnlyList<RoundEvaluation>> rounds)
    {
        return rounds.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<RoundObservation>)pair.Value
                .OrderBy(r => r.Round)
                .Select(r => new RoundObservation(r.Round, r.Summary.RankedProbability, r.ParameterDrift))
                .ToList());
    }

    private static List<MatchEvaluation> Sorted(IReadOnlyList<RoundEvaluation> rounds)
    {
        return rounds.SelectMany(round => round.Evaluations).OrderBy(e => e.MatchId).ToList();
    }

    private static IReadOnlyList<string> Ranking(IReadOnlyDictionary<string, IReadOnlyList<MatchEvaluation>> byModel)
    {
        return byModel.OrderBy(pair => MetricMeans.Of(pair.Value).RankedProbability).Select(pair => pair.Key).ToList();
    }

    private static double Holm(IReadOnlyList<PairwiseComparison> pairs, string first, string second)
    {
        return pairs.Single(p => (p.FirstModel == first && p.SecondModel == second) || (p.FirstModel == second && p.SecondModel == first)).AdjustedPValue;
    }

    private static int Count(IReadOnlyList<MatchEvaluation> evaluations, Func<MatchEvaluation, bool> hit) => evaluations.Count(hit);

    private static string Hits(IReadOnlyList<MatchEvaluation> evaluations, Func<MatchEvaluation, bool> hit)
    {
        var count = evaluations.Count(hit);
        return Invariant($"{count} ({100.0 * count / evaluations.Count:F1}%)");
    }

    private static string RankingCode(IReadOnlyList<string> ranking) => string.Join("<", ranking.Select(name => name switch
    {
        PredictionModelFactory.DixonColes => "D",
        PredictionModelFactory.Elo => "E",
        _ => "P"
    }));

    private static string Invariant(FormattableString value) => value.ToString(CultureInfo.InvariantCulture);

    private static object Fingerprint(MatchEvaluation e)
    {
        return (e.MatchId, e.ModelName, e.Round, e.InvolvesPromotedTeam, e.Actual, e.Predicted, e.Brier, e.RankedProbability, e.LogLoss,
            e.OutcomeCorrect, e.ExactScoreCorrect, e.ExactScoreInTopThree, e.ProbabilityOfActualScore, e.ProbabilityOfActualOutcome);
    }

    private sealed record RunEvaluation(
        SensitivitySeason Season,
        IReadOnlyDictionary<string, IReadOnlyList<RoundEvaluation>> Version6,
        IReadOnlyDictionary<string, IReadOnlyList<RoundEvaluation>> Version5);
}
