using EkstraSim.Prediction.Models;
using MathNet.Numerics.Distributions;
using MathNet.Numerics.Optimization;

namespace EkstraSim.Tests;

public class DixonColesModelTests
{
    private static readonly int[] SyntheticTeams = [100, 200, 300, 400, 500, 600];
    private static readonly double[] TrueAttack = [1.4, 1.2, 1.0, 0.9, 0.8, 0.7];
    private static readonly double[] TrueDefence = [0.75, 0.9, 1.05, 1.15, 1.25, 1.2];
    private const double TrueHomeAdvantage = 1.3;
    private const double StationarityTolerance = 1e-6;

    internal static List<MatchData> SyntheticSeason(int repetitions, int seed)
    {
        var random = new Random(seed);
        var matches = new List<MatchData>();
        var id = 1;
        var round = 1;

        for (var repetition = 0; repetition < repetitions; repetition++)
        {
            for (var home = 0; home < SyntheticTeams.Length; home++)
            {
                for (var away = 0; away < SyntheticTeams.Length; away++)
                {
                    if (home == away)
                    {
                        continue;
                    }

                    var lambdaHome = TrueAttack[home] * TrueDefence[away] * TrueHomeAdvantage;
                    var lambdaAway = TrueAttack[away] * TrueDefence[home];

                    matches.Add(TestLeague.Played(
                        id++,
                        round++,
                        SyntheticTeams[home],
                        SyntheticTeams[away],
                        Poisson.Sample(random, lambdaHome),
                        Poisson.Sample(random, lambdaAway)));
                }
            }
        }

        return matches;
    }

    private static List<MatchData> SyntheticLeague(int teamsPerSeason, int seasons, int seed)
    {
        var random = new Random(seed);
        var teamCount = teamsPerSeason + 2 * (seasons - 1);
        var attack = Enumerable.Range(0, teamCount).Select(_ => Math.Exp(0.25 * Normal.Sample(random, 0, 1))).ToArray();
        var defence = Enumerable.Range(0, teamCount).Select(_ => Math.Exp(0.25 * Normal.Sample(random, 0, 1))).ToArray();
        var matchesPerRound = teamsPerSeason / 2;
        var matches = new List<MatchData>();
        var id = 1;

        for (var season = 0; season < seasons; season++)
        {
            var seasonStart = new DateTime(2019, 7, 20).AddDays(365 * season);
            var teams = Enumerable.Range(2 * season, teamsPerSeason).ToList();
            var index = 0;

            foreach (var home in teams)
            {
                foreach (var away in teams)
                {
                    if (home == away)
                    {
                        continue;
                    }

                    var round = index++ / matchesPerRound + 1;
                    matches.Add(new MatchData(
                        id++,
                        seasonStart.AddDays(7 * (round - 1)),
                        round,
                        TestLeague.SeasonId + season,
                        TestLeague.LeagueId,
                        1000 + home,
                        1000 + away,
                        Poisson.Sample(random, attack[home] * defence[away] * TrueHomeAdvantage),
                        Poisson.Sample(random, attack[away] * defence[home])));
                }
            }
        }

        return matches;
    }

    private static void AssertAccepted(DixonColesFitReport report)
    {
        Assert.True(report.ExitReason is ExitCondition.AbsoluteGradient or ExitCondition.RelativeGradient, $"wyjście: {report.ExitReason}");
        Assert.True(report.GradientNorm <= StationarityTolerance * Math.Max(1, Math.Abs(report.FinalObjective)), $"‖∇f‖∞ = {report.GradientNorm}");
        Assert.True(report.FinalObjective < report.StartObjective, $"NLL {report.StartObjective} → {report.FinalObjective}");
    }

    private static TrainingOptions FitOptions()
    {
        return new TrainingOptions
        {
            LeagueId = TestLeague.LeagueId,
            SeasonId = TestLeague.SeasonId,
            SeasonChronology = [TestLeague.SeasonId],
            TimeDecayXi = 0,
            RidgeLambda = 0
        };
    }

    [Fact]
    public void RecoversAttackAndDefenceFromSyntheticData()
    {
        var model = new DixonColesModel();
        model.Train(SyntheticSeason(repetitions: 24, seed: 20240719), FitOptions());

        var parameters = model.GetParametersSnapshot().Parameters;

        for (var i = 0; i < SyntheticTeams.Length; i++)
        {
            Assert.Equal(TrueAttack[i], parameters[$"attack_{SyntheticTeams[i]}"], tolerance: 0.15);
            Assert.Equal(TrueDefence[i], parameters[$"defence_{SyntheticTeams[i]}"], tolerance: 0.15);
        }
    }

    [Fact]
    public void RecoversHomeAdvantageAndNearZeroRho()
    {
        var model = new DixonColesModel();
        model.Train(SyntheticSeason(repetitions: 24, seed: 987654), FitOptions());

        var parameters = model.GetParametersSnapshot().Parameters;

        Assert.Equal(TrueHomeAdvantage, parameters["home_advantage"], tolerance: 0.15);
        Assert.Equal(0, parameters["rho"], tolerance: 0.08);
    }

    [Fact]
    public void AttackParametersAverageToOne()
    {
        var model = new DixonColesModel();
        model.Train(SyntheticSeason(repetitions: 8, seed: 111), FitOptions());

        var parameters = model.GetParametersSnapshot().Parameters;
        var attacks = SyntheticTeams.Select(id => parameters[$"attack_{id}"]).ToList();

        Assert.Equal(1.0, attacks.Average(), precision: 8);
    }

    [Fact]
    public void StrongerTeamGetsHigherExpectedGoals()
    {
        var model = new DixonColesModel();
        model.Train(SyntheticSeason(repetitions: 12, seed: 424242), FitOptions());

        var strongAtHome = model.Predict(TestLeague.Fixture(9001, round: 99, homeTeamId: 100, awayTeamId: 600));
        var weakAtHome = model.Predict(TestLeague.Fixture(9002, round: 99, homeTeamId: 600, awayTeamId: 100));

        Assert.True(strongAtHome.ExpectedHomeGoals > weakAtHome.ExpectedHomeGoals);
        Assert.True(strongAtHome.HomeWinProbability > weakAtHome.HomeWinProbability);
    }

    [Fact]
    public void PredictionIsAValidDistribution()
    {
        var model = new DixonColesModel();
        model.Train(SyntheticSeason(repetitions: 6, seed: 5), FitOptions());

        var prediction = model.Predict(TestLeague.Fixture(9003, round: 99, homeTeamId: 100, awayTeamId: 200));

        Assert.Equal(1.0, ScoreGrid.Sum(prediction.ScoreProbabilities), precision: 10);
        Assert.Equal(1.0, prediction.HomeWinProbability + prediction.DrawProbability + prediction.AwayWinProbability, precision: 10);
    }

    [Fact]
    public void UnknownTeamsFallBackToLeagueAverageStrength()
    {
        var model = new DixonColesModel();
        model.Train(SyntheticSeason(repetitions: 6, seed: 7), FitOptions());

        var prediction = model.Predict(TestLeague.Fixture(9004, round: 99, homeTeamId: 8001, awayTeamId: 8002));
        var homeAdvantage = model.GetParametersSnapshot().Parameters["home_advantage"];

        Assert.Equal(homeAdvantage, prediction.ExpectedHomeGoals, precision: 8);
        Assert.Equal(1.0, prediction.ExpectedAwayGoals, precision: 8);
    }

    [Fact]
    public void TimeDecayFavoursRecentForm()
    {
        var history = new List<MatchData>();
        var id = 1;
        var round = 1;

        for (var i = 0; i < 10; i++)
        {
            history.Add(TestLeague.Played(id++, round++, homeTeamId: 100, awayTeamId: 200, homeScore: 0, awayScore: 2));
            history.Add(TestLeague.Played(id++, round++, homeTeamId: 200, awayTeamId: 100, homeScore: 3, awayScore: 0));
        }

        for (var i = 0; i < 10; i++)
        {
            history.Add(TestLeague.Played(id++, round++, homeTeamId: 100, awayTeamId: 200, homeScore: 4, awayScore: 0));
            history.Add(TestLeague.Played(id++, round++, homeTeamId: 200, awayTeamId: 100, homeScore: 0, awayScore: 3));
        }

        var withoutDecay = new DixonColesModel();
        withoutDecay.Train(history, FitOptions());

        var withDecay = new DixonColesModel();
        withDecay.Train(history, new TrainingOptions
        {
            LeagueId = TestLeague.LeagueId,
            SeasonId = TestLeague.SeasonId,
            SeasonChronology = [TestLeague.SeasonId],
            TimeDecayXi = 0.02,
            RidgeLambda = 0
        });

        var flat = withoutDecay.GetParametersSnapshot().Parameters["attack_100"];
        var decayed = withDecay.GetParametersSnapshot().Parameters["attack_100"];

        Assert.True(decayed > flat);
    }

    [Fact]
    public void RidgeShrinksSparseTeamsTowardsLeagueAverage()
    {
        var history = SyntheticSeason(repetitions: 6, seed: 31337);
        history.Add(TestLeague.Played(9500, round: 400, homeTeamId: 700, awayTeamId: 100, homeScore: 7, awayScore: 0));

        var weaklyRegularised = new DixonColesModel();
        weaklyRegularised.Train(history, RidgeOptions(0.05));

        var stronglyRegularised = new DixonColesModel();
        stronglyRegularised.Train(history, RidgeOptions(5.0));

        var sparseWeak = weaklyRegularised.GetParametersSnapshot().Parameters["attack_700"];
        var sparseStrong = stronglyRegularised.GetParametersSnapshot().Parameters["attack_700"];

        Assert.True(Math.Abs(Math.Log(sparseStrong)) < Math.Abs(Math.Log(sparseWeak)));
    }

    private static TrainingOptions RidgeOptions(double ridgeLambda)
    {
        return new TrainingOptions
        {
            LeagueId = TestLeague.LeagueId,
            SeasonId = TestLeague.SeasonId,
            SeasonChronology = [TestLeague.SeasonId],
            TimeDecayXi = 0,
            RidgeLambda = ridgeLambda
        };
    }

    [Fact]
    public void OptimiserMovesAwayFromTheStartingPointToAStationaryPoint()
    {
        var model = new DixonColesModel();
        model.Train(SyntheticSeason(repetitions: 12, seed: 2026), FitOptions());

        var report = Assert.Single(model.FitReports);

        Assert.Null(report.AfterRound);
        Assert.Equal(2 * SyntheticTeams.Length + 2, report.Dimension);
        AssertAccepted(report);
    }

    [Fact]
    public void EveryFitConvergesOnAProductionSizedProblem()
    {
        var league = SyntheticLeague(teamsPerSeason: 18, seasons: 3, seed: 1818);
        var lastSeason = TestLeague.SeasonId + 2;
        var history = league.Where(m => m.SeasonId != lastSeason || m.Round <= 18).ToList();

        var model = new DixonColesModel();
        model.Train(history, new TrainingOptions
        {
            LeagueId = TestLeague.LeagueId,
            SeasonId = lastSeason,
            SeasonChronology = [TestLeague.SeasonId, TestLeague.SeasonId + 1, lastSeason],
            TimeDecayXi = 0.0065,
            RidgeLambda = 0.05
        });

        foreach (var round in new[] { 19, 20, 21 })
        {
            model.UpdateWithRound(league.Where(m => m.SeasonId == lastSeason && m.Round == round).ToList());
        }

        Assert.Equal(new int?[] { null, 19, 20, 21 }, model.FitReports.Select(r => r.AfterRound));
        Assert.All(model.FitReports, report =>
        {
            Assert.True(report.Dimension >= 38, $"wymiar {report.Dimension}");
            AssertAccepted(report);
        });
    }

    [Fact]
    public void ExhaustedIterationLimitThrowsInsteadOfFallingBack()
    {
        var model = new DixonColesModel(maxIterations: 1);

        var exception = Assert.Throws<ModelConvergenceException>(
            () => model.Train(SyntheticSeason(repetitions: 6, seed: 99), FitOptions()));

        Assert.StartsWith("Dixon-Coles: optymalizacja nie zbiegła (trening;", exception.Message);
        Assert.Equal("wyczerpany limit 1 iteracji", exception.Reason);
        Assert.Null(exception.Report.AfterRound);
        Assert.Empty(model.FitReports);
    }

    [Fact]
    public void AbsorbingTheSameRoundTwiceIsIdempotent()
    {
        var round = new[] { TestLeague.Played(9600, round: 500, homeTeamId: 100, awayTeamId: 200, homeScore: 2, awayScore: 2) };

        var model = new DixonColesModel();
        model.Train(SyntheticSeason(repetitions: 6, seed: 42), FitOptions());
        model.UpdateWithRound(round);
        var afterFirst = model.GetParametersSnapshot();

        model.UpdateWithRound(round);
        var afterSecond = model.GetParametersSnapshot();

        Assert.Equal(0, ModelSnapshot.Distance(afterFirst, afterSecond), precision: 10);
        Assert.Equal(500, afterSecond.AfterRound);
    }

    [Fact]
    public void ModelNameIsStable()
    {
        Assert.Equal("DixonColes", new DixonColesModel().Name);
    }
}
