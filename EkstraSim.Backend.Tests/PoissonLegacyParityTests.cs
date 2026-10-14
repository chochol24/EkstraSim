using System.Globalization;
using EkstraSim.Prediction.Evaluation;
using EkstraSim.Prediction.Metrics;
using EkstraSim.Prediction.Models;
using EkstraSim.Tests;
using Newtonsoft.Json;
using Shared;
using Xunit.Abstractions;

namespace EkstraSim.Backend.Tests;

public class PoissonLegacyParityTests
{
    private const int Simulations = 100_000;
    private const double StandardErrors = 5;
    private const double RoundingMargin = 0.002;
    private const int ComparisonMaxGoals = 20;

    private static readonly int[] Seasons = [RealDataFixture.Season2425, RealDataFixture.Season2526];

    private static readonly Lazy<IReadOnlyList<SeasonParity>> Parity = new(() => Task.Run(ComputeAsync).GetAwaiter().GetResult());

    private readonly ITestOutputHelper _output;

    public PoissonLegacyParityTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Theory]
    [Trait("Category", "RealData")]
    [Trait("Category", "Legacy")]
    [InlineData(RealDataFixture.Season2425)]
    [InlineData(RealDataFixture.Season2526)]
    public void LegacyEngineMatchesThePortWithTheAwayWeightDefect(int seasonId)
    {
        var violations = Season(seasonId).Matches
            .Where(m => m.Comparable)
            .SelectMany(m => Differences(m.Legacy, m.PortWithDefect)
                .Where(d => d.Difference > Tolerance(d.Port))
                .Select(d => Invariant($"mecz {m.MatchId} (k.{m.Round}) {d.Outcome}: oryginał {d.Legacy:F4}, port {d.Port:F4}")))
            .ToList();

        Assert.Empty(violations);
    }

    [Theory]
    [Trait("Category", "RealData")]
    [Trait("Category", "Legacy")]
    [InlineData(RealDataFixture.Season2425, 144, 0)]
    [InlineData(RealDataFixture.Season2526, 118, 26)]
    public void OnlyMatchesWithLaterKnownMatchesOfTheirTeamsAreLeftOut(int seasonId, int comparable, int other)
    {
        var matches = Season(seasonId).Matches;

        Assert.Equal(comparable, matches.Count(m => m.Comparable));
        Assert.Equal(other, matches.Count(m => !m.Comparable));
    }

    [Theory]
    [Trait("Category", "RealData")]
    [Trait("Category", "Legacy")]
    [InlineData(RealDataFixture.Season2425, 0.2555, 0.2518, 0.2281)]
    [InlineData(RealDataFixture.Season2526, 0.2588, 0.2605, 0.2270)]
    public void LegacyEngineLosesToTheConstantLikeThePort(int seasonId, double legacyRps, double portRps, double constantRps)
    {
        var parity = Season(seasonId);
        var legacy = parity.Matches.Average(m => m.LegacyRps);

        Assert.Equal(legacyRps, legacy, 0.001);
        Assert.Equal(legacy, parity.Matches.Average(m => m.PortWithDefectRps), 0.001);
        Assert.Equal(portRps, parity.Matches.Average(m => m.PortRps), 5e-5);
        Assert.Equal(constantRps, parity.ConstantRps, 5e-5);
        Assert.True(legacy > parity.ConstantRps + 0.02);
    }

    [Theory]
    [Trait("Category", "RealData")]
    [Trait("Category", "Legacy")]
    [InlineData(RealDataFixture.Season2425, 0.291, 0.118, 0.682, 0.138)]
    [InlineData(RealDataFixture.Season2526, 0.291, 0.104, 0.706, 0.148)]
    public void AwayWeightDefectInflatesTheAwayGoals(int seasonId, double meanIncrease, double minIncrease, double maxIncrease, double maxProbabilityShift)
    {
        var matches = Season(seasonId).Matches;
        var increase = matches.Select(m => m.PortAwayGoalsWithDefect - m.PortAwayGoals).ToList();

        Assert.Equal(meanIncrease, increase.Average(), 5e-4);
        Assert.Equal(minIncrease, increase.Min(), 5e-4);
        Assert.Equal(maxIncrease, increase.Max(), 5e-4);
        Assert.Equal(maxProbabilityShift, matches.Max(m => MaxDifference(m.PortWithDefect, m.Port)), 5e-4);
    }

    [Fact]
    [Trait("Category", "RealData")]
    [Trait("Category", "Legacy")]
    public void Report()
    {
        foreach (var parity in Parity.Value)
        {
            ReportSeason(parity);
        }

        var all = Parity.Value.SelectMany(p => p.Matches).ToList();
        var awayIncrease = all.Select(m => m.PortAwayGoalsWithDefect - m.PortAwayGoals).ToList();
        _output.WriteLine("");
        _output.WriteLine(Invariant($"obie wiosny ({all.Count} meczów): błąd wagi gościa podnosi λ gościa średnio o {awayIncrease.Average():F3} (od {awayIncrease.Min():F3} do {awayIncrease.Max():F3}), największa zmiana prawdopodobieństwa 1X2 {all.Max(m => MaxDifference(m.PortWithDefect, m.Port)):F3}"));

        Assert.Equal(Seasons.Length, Parity.Value.Count);
    }

    private void ReportSeason(SeasonParity parity)
    {
        var comparable = parity.Matches.Where(m => m.Comparable).ToList();
        var other = parity.Matches.Where(m => !m.Comparable).ToList();
        var awayIncrease = parity.Matches.Select(m => m.PortAwayGoalsWithDefect - m.PortAwayGoals).ToList();

        _output.WriteLine("");
        _output.WriteLine(Invariant($"=== Sezon {parity.SeasonId}: {parity.Matches.Count} meczów wiosny, N = {Simulations} symulacji na mecz ==="));
        _output.WriteLine(Invariant($"porównywalne (bez późniejszych meczów tych drużyn w znanym zbiorze): {comparable.Count}, największa różnica p {MaxOrZero(comparable, m => MaxDifference(m.Legacy, m.PortWithDefect)):F4}, średnia {AverageOrZero(comparable, m => MaxDifference(m.Legacy, m.PortWithDefect)):F4}"));
        _output.WriteLine(Invariant($"pozostałe (oryginał liczy formę także z meczów późniejszych): {other.Count}, największa różnica p {MaxOrZero(other, m => MaxDifference(m.Legacy, m.PortWithDefect)):F4}, średnia {AverageOrZero(other, m => MaxDifference(m.Legacy, m.PortWithDefect)):F4}"));

        foreach (var match in other.OrderByDescending(m => MaxDifference(m.Legacy, m.PortWithDefect)).Take(5))
        {
            _output.WriteLine(Invariant($"  mecz {match.MatchId} (k.{match.Round}): oryginał {Format(match.Legacy)}, port {Format(match.PortWithDefect)}"));
        }

        var homeGaps = comparable.Select(m => (Match: m, Gap: Math.Abs(m.LegacyHomeGoals - m.PortHomeGoals))).ToList();
        var largeGaps = homeGaps.Where(g => g.Gap > 0.02).ToList();
        _output.WriteLine(Invariant($"mecze porównywalne, λ gospodarza z macierzy oryginału (0–10 goli) a port: średnia |różnica| {homeGaps.Average(g => g.Gap):F4}; > 0,02 w {largeGaps.Count} meczach (λ portu {(largeGaps.Count > 0 ? largeGaps.Min(g => g.Match.PortHomeGoals) : 0):F2}–{(largeGaps.Count > 0 ? largeGaps.Max(g => g.Match.PortHomeGoals) : 0):F2})"));
        _output.WriteLine(Invariant($"mecze porównywalne, λ gościa z macierzy oryginału a port z błędem: średnia |różnica| {comparable.Average(m => Math.Abs(m.LegacyAwayGoals - m.PortAwayGoalsWithDefect)):F4}"));

        _output.WriteLine(Invariant($"RPS: oryginał {parity.Matches.Average(m => m.LegacyRps):F4}, port z błędem wagi gościa {parity.Matches.Average(m => m.PortWithDefectRps):F4}, port poprawiony na tych samych meczach {parity.Matches.Average(m => m.PortRps):F4}, port (przebieg walk-forward) {parity.PortRunRps:F4}, stała predykcja {parity.ConstantRps:F4}"));
        _output.WriteLine(Invariant($"błąd wagi gościa: λ gościa wyższe średnio o {awayIncrease.Average():F3} (od {awayIncrease.Min():F3} do {awayIncrease.Max():F3}), największa zmiana prawdopodobieństwa 1X2 {parity.Matches.Max(m => MaxDifference(m.PortWithDefect, m.Port)):F3}"));
    }

    private static async Task<IReadOnlyList<SeasonParity>> ComputeAsync()
    {
        return await Task.WhenAll(Seasons.Select(seasonId => Task.Run(() => ComputeSeasonAsync(seasonId))));
    }

    private static async Task<SeasonParity> ComputeSeasonAsync(int seasonId)
    {
        var promoted = RealDataFixture.PromotedTeamIds(seasonId);
        var season = SensitivitySeason.Build(
            RealDataFixture.Matches,
            RealDataFixture.SeasonChronology,
            RealDataFixture.LeagueId,
            seasonId,
            RealDataFixture.TrainingLastRound,
            promoted);
        var options = RealDataFixture.Options(seasonId) with { MaxGoals = ComparisonMaxGoals };
        var optionsWithDefect = options with { HistoricalScale = Constants.PreviousSeasonScale };
        var seasonIds = LegacyDatabase.SeasonIds(RealDataFixture.SeasonChronology, seasonId);
        var constant = ConstantBaseline.Fit(season.History, RealDataFixture.LeagueId);

        var known = season.History.Where(m => m.IsPlayed).ToList();
        var matches = new List<MatchParity>();

        foreach (var round in SeasonCalendar.RoundsInOrder(season.Evaluation))
        {
            var targets = season.Evaluation
                .Where(m => m.Round == round && m.IsPlayed)
                .OrderBy(m => m.Id)
                .ToList();

            var database = await LegacyDatabase.SeedAsync(known, targets, seasonIds);
            var legacy = await database.SimulateAsync(targets.Select(m => m.Id).ToList(), Simulations);

            var port = new PoissonModel();
            port.Train(known, options);
            var portWithDefect = new PoissonModel();
            portWithDefect.Train(known, optionsWithDefect);

            foreach (var (target, result) in targets.Zip(legacy))
            {
                var correct = port.Predict(target);
                var defect = portWithDefect.Predict(target);
                var equivalent = MatchPrediction.FromLambdas(target, "Port z błędem wagi gościa", correct.ExpectedHomeGoals, defect.ExpectedAwayGoals, ComparisonMaxGoals);
                var matrix = JsonConvert.DeserializeObject<double[,]>(result.ResultProbabilityMatrixJson!)!;
                var actual = PredictionMetrics.OutcomeOf(target.HomeScore!.Value, target.AwayScore!.Value);
                var legacyProbabilities = new OutcomeProbabilities(result.HomeWinProbability, result.DrawProbability, result.AwayWinProbability);
                var equivalentProbabilities = Probabilities(equivalent);

                matches.Add(new MatchParity(
                    target.Id,
                    round,
                    !known.Any(a => a.Date >= target.Date && (a.Involves(target.HomeTeamId) || a.Involves(target.AwayTeamId))),
                    legacyProbabilities,
                    equivalentProbabilities,
                    Probabilities(correct),
                    MatrixMean(matrix, home: true),
                    MatrixMean(matrix, home: false),
                    correct.ExpectedHomeGoals,
                    correct.ExpectedAwayGoals,
                    defect.ExpectedAwayGoals,
                    PredictionMetrics.RankedProbabilityScore(legacyProbabilities, actual),
                    PredictionMetrics.RankedProbabilityScore(equivalentProbabilities, actual),
                    PredictionMetrics.RankedProbabilityScore(Probabilities(correct), actual)));
            }

            known.AddRange(targets);
        }

        var run = WalkForwardEvaluator.Run(
            new PoissonModel(),
            season.History,
            season.Evaluation,
            RealDataFixture.Options(seasonId),
            promoted.ToHashSet());

        return new SeasonParity(
            seasonId,
            matches,
            run.SelectMany(r => r.Evaluations).Average(e => e.RankedProbability),
            ConstantBaseline.Evaluate(constant, season.Evaluation, promoted.ToHashSet()).Average(e => e.RankedProbability));
    }

    private static SeasonParity Season(int seasonId) => Parity.Value.Single(p => p.SeasonId == seasonId);

    private static double Tolerance(double probability)
    {
        return StandardErrors * Math.Sqrt(probability * (1 - probability) / Simulations) + RoundingMargin;
    }

    private static IEnumerable<(MatchOutcome Outcome, double Legacy, double Port, double Difference)> Differences(OutcomeProbabilities legacy, OutcomeProbabilities port)
    {
        return Enum.GetValues<MatchOutcome>().Select(outcome => (outcome, legacy.Of(outcome), port.Of(outcome), Math.Abs(legacy.Of(outcome) - port.Of(outcome))));
    }

    private static double MaxDifference(OutcomeProbabilities first, OutcomeProbabilities second)
    {
        return Enum.GetValues<MatchOutcome>().Max(outcome => Math.Abs(first.Of(outcome) - second.Of(outcome)));
    }

    private static OutcomeProbabilities Probabilities(MatchPrediction prediction)
    {
        return new OutcomeProbabilities(prediction.HomeWinProbability, prediction.DrawProbability, prediction.AwayWinProbability);
    }

    private static double MatrixMean(double[,] matrix, bool home)
    {
        var total = 0.0;

        for (var homeGoals = 0; homeGoals < matrix.GetLength(0); homeGoals++)
        {
            for (var awayGoals = 0; awayGoals < matrix.GetLength(1); awayGoals++)
            {
                total += (home ? homeGoals : awayGoals) * matrix[homeGoals, awayGoals];
            }
        }

        return total;
    }

    private static double MaxOrZero(IReadOnlyList<MatchParity> matches, Func<MatchParity, double> value) => matches.Count > 0 ? matches.Max(value) : 0;

    private static double AverageOrZero(IReadOnlyList<MatchParity> matches, Func<MatchParity, double> value) => matches.Count > 0 ? matches.Average(value) : 0;

    private static string Format(OutcomeProbabilities probabilities) => Invariant($"{probabilities.HomeWin:F3} / {probabilities.Draw:F3} / {probabilities.AwayWin:F3}");

    private static string Invariant(FormattableString value) => value.ToString(CultureInfo.InvariantCulture);

    private sealed record MatchParity(
        int MatchId,
        int Round,
        bool Comparable,
        OutcomeProbabilities Legacy,
        OutcomeProbabilities PortWithDefect,
        OutcomeProbabilities Port,
        double LegacyHomeGoals,
        double LegacyAwayGoals,
        double PortHomeGoals,
        double PortAwayGoals,
        double PortAwayGoalsWithDefect,
        double LegacyRps,
        double PortWithDefectRps,
        double PortRps);

    private sealed record SeasonParity(int SeasonId, IReadOnlyList<MatchParity> Matches, double PortRunRps, double ConstantRps);
}
