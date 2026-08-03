using EkstraSim.Prediction.Models;

namespace EkstraSim.Tests;

public class LowScoreCorrectionTests
{
    private const double LambdaHome = 1.6;
    private const double LambdaAway = 1.1;
    private const double Rho = -0.05;

    [Fact]
    public void CorrectsGoallessDraw()
    {
        Assert.Equal(1 - LambdaHome * LambdaAway * Rho, ScoreGrid.LowScoreCorrection(0, 0, LambdaHome, LambdaAway, Rho), precision: 12);
    }

    [Fact]
    public void CorrectsSingleAwayGoal()
    {
        Assert.Equal(1 + LambdaHome * Rho, ScoreGrid.LowScoreCorrection(0, 1, LambdaHome, LambdaAway, Rho), precision: 12);
    }

    [Fact]
    public void CorrectsSingleHomeGoal()
    {
        Assert.Equal(1 + LambdaAway * Rho, ScoreGrid.LowScoreCorrection(1, 0, LambdaHome, LambdaAway, Rho), precision: 12);
    }

    [Fact]
    public void CorrectsOneAll()
    {
        Assert.Equal(1 - Rho, ScoreGrid.LowScoreCorrection(1, 1, LambdaHome, LambdaAway, Rho), precision: 12);
    }

    [Theory]
    [InlineData(2, 0)]
    [InlineData(0, 2)]
    [InlineData(2, 1)]
    [InlineData(3, 3)]
    public void LeavesHigherScoresUntouched(int homeGoals, int awayGoals)
    {
        Assert.Equal(1.0, ScoreGrid.LowScoreCorrection(homeGoals, awayGoals, LambdaHome, LambdaAway, Rho), precision: 12);
    }

    [Fact]
    public void ZeroRhoReducesToIndependentPoisson()
    {
        var independent = ScoreGrid.FromIndependentPoisson(LambdaHome, LambdaAway);
        var corrected = ScoreGrid.FromDixonColes(LambdaHome, LambdaAway, rho: 0);

        for (var home = 0; home <= 10; home++)
        {
            for (var away = 0; away <= 10; away++)
            {
                Assert.Equal(independent[home, away], corrected[home, away], precision: 12);
            }
        }
    }

    [Fact]
    public void NegativeRhoLiftsDrawsAndLowersOneNilOutcomes()
    {
        var independent = ScoreGrid.FromIndependentPoisson(LambdaHome, LambdaAway);
        var corrected = ScoreGrid.FromDixonColes(LambdaHome, LambdaAway, Rho);

        Assert.True(corrected[0, 0] > independent[0, 0]);
        Assert.True(corrected[1, 1] > independent[1, 1]);
        Assert.True(corrected[1, 0] < independent[1, 0]);
        Assert.True(corrected[0, 1] < independent[0, 1]);
    }

    [Fact]
    public void CorrectedGridStaysNormalised()
    {
        var corrected = ScoreGrid.FromDixonColes(LambdaHome, LambdaAway, Rho);

        Assert.Equal(1.0, ScoreGrid.Sum(corrected), precision: 10);
    }
}
