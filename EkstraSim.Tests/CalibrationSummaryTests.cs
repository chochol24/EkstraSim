using EkstraSim.Prediction.Evaluation;
using EkstraSim.Prediction.Models;

namespace EkstraSim.Tests;

public class CalibrationSummaryTests
{
    [Fact]
    public void SummaryMatchesAHandComputedExample()
    {
        var summary = CalibrationSummary.From(HandPredictions(), HandMatches());

        Assert.Equal("Poisson", summary.ModelName);
        Assert.Equal(4, summary.MatchCount);
        Assert.Equal(2.5, summary.MeanExpectedHomeGoals, 12);
        Assert.Equal(0.75, summary.MeanExpectedAwayGoals, 12);
        Assert.Equal(1.5, summary.MeanHomeGoals, 12);
        Assert.Equal(0.5, summary.MeanAwayGoals, 12);
        Assert.Equal(0.8, summary.HomeGoalsSlope, 12);
        Assert.Equal(0.0, summary.AwayGoalsSlope, 12);
        Assert.Equal(Math.Sqrt(1.8125), summary.ExpectedGoalDifferenceStandardDeviation, 12);
        Assert.Equal(4.0, summary.MaxExpectedHomeGoals);
        Assert.Equal(1, summary.MatchesAboveThreeExpectedHomeGoals);
        Assert.Equal(0.6, summary.MeanHomeWinProbability, 12);
        Assert.Equal(0.2075, summary.MeanDrawProbability, 12);
        Assert.Equal(0.1925, summary.MeanAwayWinProbability, 12);
        Assert.Equal(0.5, summary.HomeWinFrequency, 12);
        Assert.Equal(0.25, summary.DrawFrequency, 12);
        Assert.Equal(0.25, summary.AwayWinFrequency, 12);
    }

    [Fact]
    public void ReliabilityBinsGroupForecastsByProbability()
    {
        var summary = CalibrationSummary.From(HandPredictions(), HandMatches());

        Assert.Equal([1, 0, 1, 0, 2], summary.HomeWinReliability.Select(bin => bin.Count));
        Assert.Equal(0.2, summary.HomeWinReliability[0].MeanForecast, 12);
        Assert.Equal(0.0, summary.HomeWinReliability[0].ObservedFrequency, 12);
        Assert.True(double.IsNaN(summary.HomeWinReliability[1].MeanForecast));
        Assert.True(double.IsNaN(summary.HomeWinReliability[1].ObservedFrequency));
        Assert.Equal(0.5, summary.HomeWinReliability[2].MeanForecast, 12);
        Assert.Equal(1.0, summary.HomeWinReliability[2].ObservedFrequency, 12);
        Assert.Equal(0.85, summary.HomeWinReliability[4].MeanForecast, 12);
        Assert.Equal(0.5, summary.HomeWinReliability[4].ObservedFrequency, 12);

        Assert.Equal([3, 0, 1, 0, 0], summary.AwayWinReliability.Select(bin => bin.Count));
        Assert.Equal(0.09, summary.AwayWinReliability[0].MeanForecast, 12);
        Assert.Equal(0.0, summary.AwayWinReliability[0].ObservedFrequency, 12);
        Assert.Equal(1.0, summary.AwayWinReliability[2].ObservedFrequency, 12);

        Assert.Equal(CalibrationSummary.DefaultBinEdges.SkipLast(1), summary.HomeWinReliability.Select(bin => bin.From));
        Assert.Equal(CalibrationSummary.DefaultBinEdges.Skip(1), summary.HomeWinReliability.Select(bin => bin.To));
    }

    [Fact]
    public void BinsIncludeTheirLowerEdgeAndTheLastBinIncludesItsUpperEdge()
    {
        List<MatchPrediction> predictions =
        [
            Prediction(1, 1.0, 1.0, 0.25, 0.25, 0.5),
            Prediction(2, 1.0, 1.0, 1.0, 0.0, 0.0)
        ];
        List<MatchData> matches =
        [
            TestLeague.Played(1, 20, 100, 200, 1, 0),
            TestLeague.Played(2, 20, 300, 400, 0, 0)
        ];

        var summary = CalibrationSummary.From(predictions, matches, [0, 0.25, 1]);

        Assert.Equal([0, 2], summary.HomeWinReliability.Select(bin => bin.Count));
    }

    [Fact]
    public void InvalidInputIsRejected()
    {
        Assert.Throws<ArgumentException>(() => CalibrationSummary.From([], HandMatches()));
        Assert.Throws<ArgumentException>(() => CalibrationSummary.From(HandPredictions(), HandMatches().Skip(1).ToList()));
        Assert.Throws<ArgumentException>(() => CalibrationSummary.From(HandPredictions(), HandMatches(), [0.5]));
        Assert.Throws<ArgumentException>(() => CalibrationSummary.From(HandPredictions(), HandMatches(), [0, 0.5, 0.5, 1]));
        Assert.Throws<ArgumentException>(() => CalibrationSummary.From([.. HandPredictions(), Prediction(1, 1.0, 1.0, 0.3, 0.3, 0.4)], HandMatches()));
        Assert.Throws<ArgumentException>(() => CalibrationSummary.From(HandPredictions(), HandMatches(), [0.1, 1]));
        Assert.Throws<ArgumentException>(() => CalibrationSummary.From([Prediction(1, 1.0, 1.0, double.NaN, 0.3, 0.4)], HandMatches()));
    }

    private static List<MatchPrediction> HandPredictions()
    {
        return
        [
            Prediction(1, 1.0, 1.0, 0.20, 0.30, 0.50),
            Prediction(2, 2.0, 1.0, 0.50, 0.30, 0.20),
            Prediction(3, 3.0, 0.5, 0.80, 0.15, 0.05),
            Prediction(4, 4.0, 0.5, 0.90, 0.08, 0.02)
        ];
    }

    private static List<MatchData> HandMatches()
    {
        return
        [
            TestLeague.Played(1, 20, 100, 200, 0, 1),
            TestLeague.Played(2, 20, 300, 400, 2, 0),
            TestLeague.Played(3, 21, 200, 300, 1, 1),
            TestLeague.Played(4, 21, 400, 100, 3, 0)
        ];
    }

    private static MatchPrediction Prediction(int matchId, double lambdaHome, double lambdaAway, double homeWin, double draw, double awayWin)
    {
        return new MatchPrediction
        {
            MatchId = matchId,
            ModelName = "Poisson",
            ExpectedHomeGoals = lambdaHome,
            ExpectedAwayGoals = lambdaAway,
            HomeWinProbability = homeWin,
            DrawProbability = draw,
            AwayWinProbability = awayWin
        };
    }
}
