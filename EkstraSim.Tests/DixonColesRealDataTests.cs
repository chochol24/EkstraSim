using System.Globalization;
using EkstraSim.Prediction.Evaluation;
using EkstraSim.Prediction.Models;
using MathNet.Numerics.Optimization;
using Xunit.Abstractions;

namespace EkstraSim.Tests;

public class DixonColesRealDataTests
{
    private const int ExpectedFitCount = 17;
    private const double StationarityTolerance = 1e-6;
    private const double AuditStartObjective = 248.939860312;
    private const double NelderMeadTrainingObjective = 248.738;

    private readonly ITestOutputHelper _output;

    public DixonColesRealDataTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Theory]
    [Trait("Category", "RealData")]
    [InlineData(RealDataFixture.Season2425)]
    [InlineData(RealDataFixture.Season2526)]
    public void EveryWalkForwardFitConvergesToAStationaryPoint(int seasonId)
    {
        var (reports, fits) = ReplayWalkForward(seasonId);

        _output.WriteLine($"Sezon {seasonId}: {reports.Count} dopasowań");
        _output.WriteLine(string.Format(
            CultureInfo.InvariantCulture,
            "{0,-9} {1,5} {2,-17} {3,12} {4,12} {5,10} {6,10} {7,9}",
            "dopas.", "iter", "wyjście", "NLL start", "NLL koniec", "|∇f|∞", "rho", "gamma"));

        for (var i = 0; i < reports.Count; i++)
        {
            var report = reports[i];
            _output.WriteLine(string.Format(
                CultureInfo.InvariantCulture,
                "{0,-9} {1,5} {2,-17} {3,12:F6} {4,12:F6} {5,10:E2} {6,10:F5} {7,9:F5}",
                Label(report.AfterRound), report.Iterations, report.ExitReason, report.StartObjective,
                report.FinalObjective, report.GradientNorm, fits[i].Rho, fits[i].HomeAdvantage));
        }

        Assert.Equal(ExpectedFitCount, reports.Count);
        Assert.Equal(fits.Select(fit => fit.AfterRound), reports.Select(report => report.AfterRound));

        var violations = reports
            .Select(report => (Label: Label(report.AfterRound), Problems: Problems(report)))
            .Where(entry => entry.Problems.Count > 0)
            .Select(entry => $"{entry.Label}: {string.Join(", ", entry.Problems)}")
            .ToList();

        Assert.True(
            violations.Count == 0,
            $"Sezon {seasonId}: {violations.Count} z {reports.Count} dopasowań nie spełnia kryterium — {string.Join("; ", violations)}.");
    }

    [Fact]
    [Trait("Category", "RealData")]
    public void TrainingObjectiveMatchesTheAuditBaseline()
    {
        var seasonId = RealDataFixture.Season2425;
        var model = new DixonColesModel();
        model.Train(History(seasonId), RealDataFixture.Options(seasonId));

        var report = Assert.Single(model.FitReports);

        Assert.Equal(AuditStartObjective, report.StartObjective, 1e-8);
        Assert.True(
            report.FinalObjective <= NelderMeadTrainingObjective,
            $"NLL końcowe {report.FinalObjective.ToString("F6", CultureInfo.InvariantCulture)} powyżej wyniku Neldera-Meada {NelderMeadTrainingObjective}.");
    }

    private static List<string> Problems(DixonColesFitReport report)
    {
        var problems = new List<string>();

        if (report.ExitReason is not (ExitCondition.AbsoluteGradient or ExitCondition.RelativeGradient))
        {
            problems.Add($"wyjście {report.ExitReason}");
        }

        if (!(report.GradientNorm <= StationarityTolerance * Math.Max(1, Math.Abs(report.FinalObjective))))
        {
            problems.Add(string.Format(CultureInfo.InvariantCulture, "‖∇f‖∞ = {0:E2}", report.GradientNorm));
        }

        if (!(report.FinalObjective < report.StartObjective))
        {
            problems.Add("brak ruchu z punktu startowego");
        }

        return problems;
    }

    private static List<MatchData> History(int seasonId)
    {
        return WalkForwardEvaluator.BuildHistory(
            RealDataFixture.Matches,
            RealDataFixture.SeasonChronology,
            seasonId,
            RealDataFixture.TrainingLastRound);
    }

    private static (IReadOnlyList<DixonColesFitReport> Reports, List<Fit> Fits) ReplayWalkForward(int seasonId)
    {
        var evaluationSet = WalkForwardEvaluator.BuildEvaluationSet(
            RealDataFixture.Matches,
            seasonId,
            RealDataFixture.TrainingLastRound);

        var model = new DixonColesModel();
        var recorder = new RecordingModel(model);
        WalkForwardEvaluator.Run(recorder, History(seasonId), evaluationSet, RealDataFixture.Options(seasonId));

        return (model.FitReports, recorder.Fits);
    }

    private static string Label(int? afterRound) => afterRound.HasValue ? $"k.{afterRound}" : "trening";

    private sealed record Fit(int? AfterRound, double Rho, double HomeAdvantage);

    private sealed class RecordingModel : IPredictionModel
    {
        private readonly IPredictionModel _inner;

        public RecordingModel(IPredictionModel inner)
        {
            _inner = inner;
        }

        public List<Fit> Fits { get; } = [];

        public string Name => _inner.Name;

        public void Train(IReadOnlyList<MatchData> history, TrainingOptions options)
        {
            _inner.Train(history, options);
            Record();
        }

        public void UpdateWithRound(IReadOnlyList<MatchData> playedRound)
        {
            _inner.UpdateWithRound(playedRound);
            Record();
        }

        public MatchPrediction Predict(MatchData match) => _inner.Predict(match);

        public ModelSnapshot GetParametersSnapshot() => _inner.GetParametersSnapshot();

        private void Record()
        {
            var snapshot = _inner.GetParametersSnapshot();
            Fits.Add(new Fit(snapshot.AfterRound, snapshot.Parameters["rho"], snapshot.Parameters["home_advantage"]));
        }
    }
}
