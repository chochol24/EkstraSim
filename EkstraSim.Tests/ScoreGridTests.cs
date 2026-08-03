using EkstraSim.Prediction.Models;

namespace EkstraSim.Tests;

public class ScoreGridTests
{
    [Theory]
    [InlineData(0.5, 0.4)]
    [InlineData(1.5, 1.2)]
    [InlineData(2.8, 1.9)]
    [InlineData(6.0, 5.0)]
    public void GridSumsToOne(double lambdaHome, double lambdaAway)
    {
        var grid = ScoreGrid.FromIndependentPoisson(lambdaHome, lambdaAway);

        Assert.Equal(1.0, ScoreGrid.Sum(grid), precision: 10);
    }

    [Theory]
    [InlineData(1.5, 1.2)]
    [InlineData(3.0, 0.7)]
    public void OutcomeProbabilitiesSumToOne(double lambdaHome, double lambdaAway)
    {
        var grid = ScoreGrid.FromIndependentPoisson(lambdaHome, lambdaAway);

        var (homeWin, draw, awayWin) = ScoreGrid.Outcomes(grid);

        Assert.Equal(1.0, homeWin + draw + awayWin, precision: 10);
    }

    [Fact]
    public void StrongerHomeSideGetsHigherWinProbability()
    {
        var grid = ScoreGrid.FromIndependentPoisson(2.5, 0.8);

        var (homeWin, _, awayWin) = ScoreGrid.Outcomes(grid);

        Assert.True(homeWin > awayWin);
    }

    [Fact]
    public void EqualLambdasGiveSymmetricOutcomes()
    {
        var grid = ScoreGrid.FromIndependentPoisson(1.4, 1.4);

        var (homeWin, _, awayWin) = ScoreGrid.Outcomes(grid);

        Assert.Equal(homeWin, awayWin, precision: 10);
    }

    [Fact]
    public void GridMatchesIndependentPoissonProduct()
    {
        const double lambdaHome = 1.7;
        const double lambdaAway = 1.1;
        var grid = ScoreGrid.FromIndependentPoisson(lambdaHome, lambdaAway);

        var expected = Math.Exp(-lambdaHome) * Math.Pow(lambdaHome, 2) / 2.0
            * Math.Exp(-lambdaAway) * lambdaAway;

        Assert.Equal(expected, ScoreGrid.ProbabilityOf(grid, 2, 1), precision: 5);
    }

    [Fact]
    public void MostLikelyScoreFollowsLambdaModes()
    {
        var grid = ScoreGrid.FromIndependentPoisson(2.4, 0.3);

        var (home, away) = ScoreGrid.MostLikelyScore(grid);

        Assert.Equal(2, home);
        Assert.Equal(0, away);
    }

    [Fact]
    public void RankedScoresAreOrderedByProbability()
    {
        var grid = ScoreGrid.FromIndependentPoisson(1.6, 1.3);

        var ranked = ScoreGrid.RankedScores(grid).Take(5).ToList();
        var probabilities = ranked.Select(s => ScoreGrid.ProbabilityOf(grid, s.Home, s.Away)).ToList();

        Assert.Equal(probabilities.OrderByDescending(p => p), probabilities);
    }

    [Fact]
    public void ProbabilityOutsideGridIsZero()
    {
        var grid = ScoreGrid.FromIndependentPoisson(1.5, 1.5);

        Assert.Equal(0, ScoreGrid.ProbabilityOf(grid, 11, 0));
        Assert.Equal(0, ScoreGrid.ProbabilityOf(grid, -1, 2));
    }

    [Fact]
    public void NonPositiveLambdaIsHandledWithoutNaN()
    {
        var grid = ScoreGrid.FromIndependentPoisson(0, 1.2);

        Assert.Equal(1.0, ScoreGrid.Sum(grid), precision: 10);
        Assert.True(ScoreGrid.ProbabilityOf(grid, 0, 1) > 0);
    }
}
