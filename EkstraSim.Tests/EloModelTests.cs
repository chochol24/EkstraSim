using EkstraSim.Prediction.Models;
using MathNet.Numerics.Distributions;

namespace EkstraSim.Tests;

public class EloModelTests
{
    private static readonly int[] Teams = [100, 200, 300, 400, 500, 600];
    private static readonly double[] Strength = [1.5, 1.25, 1.05, 0.9, 0.78, 0.65];

    private static List<MatchData> SyntheticSeason(int repetitions, int seed)
    {
        var random = new Random(seed);
        var matches = new List<MatchData>();
        var id = 1;
        var round = 1;

        for (var repetition = 0; repetition < repetitions; repetition++)
        {
            for (var home = 0; home < Teams.Length; home++)
            {
                for (var away = 0; away < Teams.Length; away++)
                {
                    if (home == away)
                    {
                        continue;
                    }

                    var lambdaHome = Strength[home] / Strength[away] * 1.35;
                    var lambdaAway = Strength[away] / Strength[home] * 1.05;

                    matches.Add(TestLeague.Played(
                        id++,
                        round++,
                        Teams[home],
                        Teams[away],
                        Poisson.Sample(random, lambdaHome),
                        Poisson.Sample(random, lambdaAway)));
                }
            }
        }

        return matches;
    }

    [Fact]
    public void SingleMatchReproducesTeamServiceFormula()
    {
        var model = new EloModel();
        model.Train(
            [TestLeague.Played(1, round: 1, homeTeamId: 100, awayTeamId: 200, homeScore: 2, awayScore: 0)],
            TestLeague.Options());

        var parameters = model.GetParametersSnapshot().Parameters;

        Assert.Equal(1305.399026, parameters["rating_100"], tolerance: 1e-6);
        Assert.Equal(1294.600974, parameters["rating_200"], tolerance: 1e-6);
    }

    [Fact]
    public void DrawBetweenEqualsCostsTheHomeSide()
    {
        var model = new EloModel();
        model.Train(
            [TestLeague.Played(1, round: 1, homeTeamId: 100, awayTeamId: 200, homeScore: 1, awayScore: 1)],
            TestLeague.Options());

        var parameters = model.GetParametersSnapshot().Parameters;

        Assert.True(parameters["rating_100"] < 1300);
        Assert.True(parameters["rating_200"] > 1300);
        Assert.Equal(2600, parameters["rating_100"] + parameters["rating_200"], precision: 8);
    }

    [Fact]
    public void RatingsAreZeroSum()
    {
        var model = new EloModel();
        model.Train(SyntheticSeason(repetitions: 6, seed: 11), TestLeague.Options());

        var total = model.GetParametersSnapshot().Parameters
            .Where(pair => pair.Key.StartsWith("rating_"))
            .Sum(pair => pair.Value);

        Assert.Equal(Teams.Length * 1300.0, total, precision: 6);
    }

    [Fact]
    public void ReplayIsDeterministic()
    {
        var history = SyntheticSeason(repetitions: 6, seed: 22);

        var first = new EloModel();
        first.Train(history, TestLeague.Options());

        var second = new EloModel();
        second.Train(history, TestLeague.Options());

        Assert.Equal(0, ModelSnapshot.Distance(first.GetParametersSnapshot(), second.GetParametersSnapshot()), precision: 12);
    }

    [Fact]
    public void StrongerTeamsEndUpHigherRated()
    {
        var model = new EloModel();
        model.Train(SyntheticSeason(repetitions: 10, seed: 33), TestLeague.Options());

        var parameters = model.GetParametersSnapshot().Parameters;

        Assert.True(parameters["rating_100"] > parameters["rating_300"]);
        Assert.True(parameters["rating_300"] > parameters["rating_600"]);
    }

    [Fact]
    public void RatingGapTranslatesIntoGoalExpectation()
    {
        var model = new EloModel();
        model.Train(SyntheticSeason(repetitions: 10, seed: 44), TestLeague.Options());

        var favourite = model.Predict(TestLeague.Fixture(9001, round: 99, homeTeamId: 100, awayTeamId: 600));
        var underdog = model.Predict(TestLeague.Fixture(9002, round: 99, homeTeamId: 600, awayTeamId: 100));

        Assert.True(favourite.ExpectedHomeGoals > underdog.ExpectedHomeGoals);
        Assert.True(favourite.ExpectedAwayGoals < underdog.ExpectedAwayGoals);
        Assert.True(favourite.HomeWinProbability > favourite.AwayWinProbability);
    }

    [Fact]
    public void RegressionSlopeIsPositiveForHomeGoals()
    {
        var model = new EloModel();
        model.Train(SyntheticSeason(repetitions: 10, seed: 55), TestLeague.Options());

        var parameters = model.GetParametersSnapshot().Parameters;

        Assert.True(parameters["home_goals_slope"] > 0);
        Assert.True(parameters["away_goals_slope"] < 0);
    }

    [Fact]
    public void PredictionIsAValidDistribution()
    {
        var model = new EloModel();
        model.Train(SyntheticSeason(repetitions: 6, seed: 66), TestLeague.Options());

        var prediction = model.Predict(TestLeague.Fixture(9003, round: 99, homeTeamId: 100, awayTeamId: 200));

        Assert.Equal(1.0, ScoreGrid.Sum(prediction.ScoreProbabilities), precision: 10);
        Assert.Equal(1.0, prediction.HomeWinProbability + prediction.DrawProbability + prediction.AwayWinProbability, precision: 10);
    }

    [Fact]
    public void UnknownTeamStartsAtInitialRating()
    {
        var model = new EloModel();
        model.Train(SyntheticSeason(repetitions: 6, seed: 77), TestLeague.Options());

        Assert.Equal(1300, model.RatingOf(9999), precision: 10);
    }

    [Fact]
    public void UpdateWithRoundContinuesTheReplay()
    {
        var model = new EloModel();
        model.Train(SyntheticSeason(repetitions: 6, seed: 88), TestLeague.Options());
        var before = model.RatingOf(100);

        model.UpdateWithRound([TestLeague.Played(9500, round: 400, homeTeamId: 100, awayTeamId: 200, homeScore: 5, awayScore: 0)]);

        Assert.True(model.RatingOf(100) > before);
        Assert.Equal(400, model.GetParametersSnapshot().AfterRound);
    }

    [Fact]
    public void AbsorbingTheSameRoundTwiceIsIdempotent()
    {
        var round = new[] { TestLeague.Played(9600, round: 500, homeTeamId: 100, awayTeamId: 200, homeScore: 3, awayScore: 1) };

        var model = new EloModel();
        model.Train(SyntheticSeason(repetitions: 6, seed: 99), TestLeague.Options());
        model.UpdateWithRound(round);
        var afterFirst = model.GetParametersSnapshot();

        model.UpdateWithRound(round);
        var afterSecond = model.GetParametersSnapshot();

        Assert.Equal(0, ModelSnapshot.Distance(afterFirst, afterSecond), precision: 12);
    }

    [Theory]
    [InlineData(1, 1.0)]
    [InlineData(2, 1.5)]
    [InlineData(3, 1.75)]
    [InlineData(4, 1.875)]
    [InlineData(0, 1.375)]
    public void GoalDifferenceMultiplierMatchesTeamService(int goalDifference, double expected)
    {
        Assert.Equal(expected, EloModel.GoalDifferenceMultiplier(goalDifference), precision: 12);
    }

    [Fact]
    public void ExpectedScoreIsSymmetricAroundHomeAdvantage()
    {
        var model = new EloModel();
        model.Train([TestLeague.Played(1, 1, 100, 200, 1, 0)], TestLeague.Options());

        var neutral = model.ExpectedScore(1300, 1300);

        Assert.True(neutral > 0.5);
        Assert.Equal(0.640065, neutral, tolerance: 1e-6);
    }

    [Fact]
    public void ModelNameIsStable()
    {
        Assert.Equal("Elo", new EloModel().Name);
    }
}
