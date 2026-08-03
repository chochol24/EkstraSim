using EkstraSim.Prediction.Models;

namespace EkstraSim.Tests;

public class PoissonModelTests
{
    [Fact]
    public void PredictsHandComputedLambdas()
    {
        var model = new PoissonModel();
        model.Train(TestLeague.TwoTeamSeason(), TestLeague.Options());

        var prediction = model.Predict(TestLeague.Fixture(3, round: 3, homeTeamId: 100, awayTeamId: 200));

        Assert.Equal(2.2816666666, prediction.ExpectedHomeGoals, precision: 8);
        Assert.Equal(1.0, prediction.ExpectedAwayGoals, precision: 8);
    }

    [Fact]
    public void EmptyHorizonsFallBackToLeagueAverages()
    {
        var model = new PoissonModel();
        model.Train(TestLeague.TwoTeamSeason(), TestLeague.Options());

        var prediction = model.Predict(TestLeague.Fixture(4, round: 3, homeTeamId: 900, awayTeamId: 901));

        Assert.Equal(1.5, prediction.ExpectedHomeGoals, precision: 8);
        Assert.Equal(1.0, prediction.ExpectedAwayGoals, precision: 8);
    }

    [Fact]
    public void HorizonScalesSumToWholePrediction()
    {
        var options = new TrainingOptions
        {
            LeagueId = TestLeague.LeagueId,
            SeasonId = TestLeague.SeasonId,
            SeasonChronology = [TestLeague.PreviousSeasonId, TestLeague.SeasonId],
            UseFormFactors = false,
            CurrentSeasonScale = 1.0,
            PreviousSeasonScale = 0,
            HistoricalScale = 0
        };

        var model = new PoissonModel();
        model.Train(TestLeague.TwoTeamSeason(), options);

        var prediction = model.Predict(TestLeague.Fixture(3, round: 3, homeTeamId: 100, awayTeamId: 200));

        Assert.Equal(8.0 / 3.0, prediction.ExpectedHomeGoals, precision: 8);
        Assert.Equal(1.0, prediction.ExpectedAwayGoals, precision: 8);
    }

    [Fact]
    public void ProbabilitiesFormValidDistribution()
    {
        var model = new PoissonModel();
        model.Train(TestLeague.TwoTeamSeason(), TestLeague.Options());

        var prediction = model.Predict(TestLeague.Fixture(3, round: 3, homeTeamId: 100, awayTeamId: 200));

        Assert.Equal(1.0, prediction.HomeWinProbability + prediction.DrawProbability + prediction.AwayWinProbability, precision: 10);
        Assert.Equal(1.0, ScoreGrid.Sum(prediction.ScoreProbabilities), precision: 10);
    }

    [Fact]
    public void UpdateWithRoundChangesParameters()
    {
        var model = new PoissonModel();
        model.Train(TestLeague.TwoTeamSeason(), TestLeague.Options());
        var before = model.GetParametersSnapshot();

        model.UpdateWithRound([TestLeague.Played(3, 3, homeTeamId: 100, awayTeamId: 200, homeScore: 4, awayScore: 0)]);
        var after = model.GetParametersSnapshot();

        Assert.True(ModelSnapshot.Distance(before, after) > 0);
        Assert.Equal(3, after.AfterRound);
    }

    [Fact]
    public void AbsorbingTheSameRoundTwiceIsIdempotent()
    {
        var round = new[] { TestLeague.Played(3, 3, homeTeamId: 100, awayTeamId: 200, homeScore: 4, awayScore: 0) };

        var model = new PoissonModel();
        model.Train(TestLeague.TwoTeamSeason(), TestLeague.Options());
        model.UpdateWithRound(round);
        var afterFirst = model.GetParametersSnapshot();

        model.UpdateWithRound(round);
        var afterSecond = model.GetParametersSnapshot();

        Assert.Equal(0, ModelSnapshot.Distance(afterFirst, afterSecond), precision: 12);
    }

    [Fact]
    public void UpdateWithEmptyRoundDoesNotThrow()
    {
        var model = new PoissonModel();
        model.Train(TestLeague.TwoTeamSeason(), TestLeague.Options());

        model.UpdateWithRound([]);

        Assert.Null(model.GetParametersSnapshot().AfterRound);
    }

    [Fact]
    public void UnplayedMatchesAreIgnoredDuringTraining()
    {
        var history = TestLeague.TwoTeamSeason();
        history.Add(TestLeague.Fixture(99, round: 5, homeTeamId: 100, awayTeamId: 200));

        var withFixture = new PoissonModel();
        withFixture.Train(history, TestLeague.Options());

        var withoutFixture = new PoissonModel();
        withoutFixture.Train(TestLeague.TwoTeamSeason(), TestLeague.Options());

        Assert.Equal(0, ModelSnapshot.Distance(withFixture.GetParametersSnapshot(), withoutFixture.GetParametersSnapshot()), precision: 12);
    }

    [Fact]
    public void FormFactorsOnlyUseMatchesBeforeTargetDate()
    {
        List<MatchData> history =
        [
            TestLeague.Played(1, round: 1, homeTeamId: 100, awayTeamId: 200, homeScore: 1, awayScore: 1),
            TestLeague.Played(2, round: 2, homeTeamId: 200, awayTeamId: 100, homeScore: 1, awayScore: 1),
            TestLeague.Played(3, round: 3, homeTeamId: 100, awayTeamId: 200, homeScore: 2, awayScore: 1)
        ];

        var model = new PoissonModel();
        model.Train(history, TestLeague.Options(useFormFactors: true));

        var beforeUpturn = model.Predict(TestLeague.Fixture(50, round: 2, homeTeamId: 100, awayTeamId: 200));
        var afterUpturn = model.Predict(TestLeague.Fixture(51, round: 4, homeTeamId: 100, awayTeamId: 200));

        Assert.True(afterUpturn.ExpectedHomeGoals > beforeUpturn.ExpectedHomeGoals);
    }

    [Fact]
    public void FormFactorsAreDisabledByOption()
    {
        var history = TestLeague.TwoTeamSeason();
        history.Add(TestLeague.Played(3, round: 3, homeTeamId: 100, awayTeamId: 200, homeScore: 6, awayScore: 0));

        var model = new PoissonModel();
        model.Train(history, TestLeague.Options());

        var early = model.Predict(TestLeague.Fixture(50, round: 2, homeTeamId: 100, awayTeamId: 200));
        var late = model.Predict(TestLeague.Fixture(51, round: 4, homeTeamId: 100, awayTeamId: 200));

        Assert.Equal(early.ExpectedHomeGoals, late.ExpectedHomeGoals, precision: 12);
    }

    [Fact]
    public void FormFactorsStayWithinConfiguredBounds()
    {
        var history = TestLeague.RoundRobin([100, 200, 300, 400], TestLeague.SeasonId, firstMatchId: 1, goalsHome: 5, goalsAway: 0);

        var withForm = new PoissonModel();
        withForm.Train(history, TestLeague.Options(useFormFactors: true));

        var withoutForm = new PoissonModel();
        withoutForm.Train(history, TestLeague.Options());

        var target = TestLeague.Fixture(500, round: 40, homeTeamId: 100, awayTeamId: 200);
        var ratio = withForm.Predict(target).ExpectedHomeGoals / withoutForm.Predict(target).ExpectedHomeGoals;

        Assert.InRange(ratio, 0.8 * 0.8 * 0.95, 1.2 * 1.2 * 1.05);
    }

    [Fact]
    public void PreviousSeasonHorizonUsesChronology()
    {
        var previous = TestLeague.RoundRobin([100, 200], TestLeague.PreviousSeasonId, firstMatchId: 100, goalsHome: 4, goalsAway: 0);
        var history = TestLeague.TwoTeamSeason();
        history.AddRange(previous);

        var model = new PoissonModel();
        model.Train(history, TestLeague.Options());

        var withoutChronology = new PoissonModel();
        withoutChronology.Train(history, TestLeague.Options(chronology: [TestLeague.SeasonId]));

        var target = TestLeague.Fixture(3, round: 3, homeTeamId: 100, awayTeamId: 200);

        Assert.NotEqual(
            model.Predict(target).ExpectedHomeGoals,
            withoutChronology.Predict(target).ExpectedHomeGoals,
            precision: 6);
    }

    [Fact]
    public void SnapshotDistanceIsZeroForIdenticalState()
    {
        var first = new PoissonModel();
        first.Train(TestLeague.TwoTeamSeason(), TestLeague.Options());

        var second = new PoissonModel();
        second.Train(TestLeague.TwoTeamSeason(), TestLeague.Options());

        Assert.Equal(0, ModelSnapshot.Distance(first.GetParametersSnapshot(), second.GetParametersSnapshot()), precision: 12);
    }

    [Fact]
    public void ModelNameIsStable()
    {
        Assert.Equal("Poisson", new PoissonModel().Name);
    }
}
