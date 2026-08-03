using EkstraSim.Prediction.Metrics;
using EkstraSim.Prediction.Models;

namespace EkstraSim.Tests;

public class PredictionMetricsTests
{
    private static readonly OutcomeProbabilities Sample = new(0.5, 0.3, 0.2);

    [Theory]
    [InlineData(2, 1, MatchOutcome.HomeWin)]
    [InlineData(1, 1, MatchOutcome.Draw)]
    [InlineData(0, 3, MatchOutcome.AwayWin)]
    public void ClassifiesOutcomes(int homeScore, int awayScore, MatchOutcome expected)
    {
        Assert.Equal(expected, PredictionMetrics.OutcomeOf(homeScore, awayScore));
    }

    [Fact]
    public void BrierScoreMatchesHandCalculation()
    {
        Assert.Equal(0.38, PredictionMetrics.BrierScore(Sample, MatchOutcome.HomeWin), precision: 12);
    }

    [Fact]
    public void BrierScoreIsZeroForPerfectForecast()
    {
        var certain = new OutcomeProbabilities(1, 0, 0);

        Assert.Equal(0, PredictionMetrics.BrierScore(certain, MatchOutcome.HomeWin), precision: 12);
    }

    [Fact]
    public void BrierScoreIsTwoForFullyWrongForecast()
    {
        var certain = new OutcomeProbabilities(1, 0, 0);

        Assert.Equal(2.0, PredictionMetrics.BrierScore(certain, MatchOutcome.AwayWin), precision: 12);
    }

    [Fact]
    public void RankedProbabilityScoreMatchesHandCalculation()
    {
        Assert.Equal(0.145, PredictionMetrics.RankedProbabilityScore(Sample, MatchOutcome.HomeWin), precision: 12);
        Assert.Equal(0.445, PredictionMetrics.RankedProbabilityScore(Sample, MatchOutcome.AwayWin), precision: 12);
    }

    [Fact]
    public void RankedProbabilityScorePenalisesDistanceBetweenOutcomes()
    {
        var homeHeavy = new OutcomeProbabilities(0.9, 0.05, 0.05);

        var missedByOne = PredictionMetrics.RankedProbabilityScore(homeHeavy, MatchOutcome.Draw);
        var missedByTwo = PredictionMetrics.RankedProbabilityScore(homeHeavy, MatchOutcome.AwayWin);

        Assert.True(missedByTwo > missedByOne);
    }

    [Fact]
    public void LogLossMatchesNegativeLogOfActualProbability()
    {
        Assert.Equal(-Math.Log(0.3), PredictionMetrics.LogLoss(Sample, MatchOutcome.Draw), precision: 12);
    }

    [Fact]
    public void LogLossIsFiniteForZeroProbability()
    {
        var impossible = new OutcomeProbabilities(1, 0, 0);

        var loss = PredictionMetrics.LogLoss(impossible, MatchOutcome.Draw);

        Assert.True(double.IsFinite(loss));
        Assert.True(loss > 30);
    }

    [Fact]
    public void MostLikelyOutcomePicksHighestProbability()
    {
        Assert.Equal(MatchOutcome.HomeWin, Sample.MostLikely());
        Assert.Equal(MatchOutcome.Draw, new OutcomeProbabilities(0.2, 0.5, 0.3).MostLikely());
        Assert.Equal(MatchOutcome.AwayWin, new OutcomeProbabilities(0.2, 0.3, 0.5).MostLikely());
    }

    [Fact]
    public void EvaluateFillsEveryMetric()
    {
        var match = TestLeague.Fixture(1, round: 5, homeTeamId: 100, awayTeamId: 200);
        var prediction = MatchPrediction.FromLambdas(match, "Test", 2.4, 0.3, ScoreGrid.DefaultMaxGoals);

        var evaluation = PredictionMetrics.Evaluate(prediction, actualHomeScore: 2, actualAwayScore: 0);

        Assert.Equal(1, evaluation.MatchId);
        Assert.Equal("Test", evaluation.ModelName);
        Assert.Equal(MatchOutcome.HomeWin, evaluation.Actual);
        Assert.True(evaluation.OutcomeCorrect);
        Assert.True(evaluation.ExactScoreCorrect);
        Assert.True(evaluation.ExactScoreInTopThree);
        Assert.Equal(prediction.ProbabilityOf(2, 0), evaluation.ProbabilityOfActualScore, precision: 12);
        Assert.Equal(prediction.HomeWinProbability, evaluation.ProbabilityOfActualOutcome, precision: 12);
    }

    [Fact]
    public void ExactScoreMissIsRecorded()
    {
        var match = TestLeague.Fixture(2, round: 5, homeTeamId: 100, awayTeamId: 200);
        var prediction = MatchPrediction.FromLambdas(match, "Test", 2.4, 0.3, ScoreGrid.DefaultMaxGoals);

        var evaluation = PredictionMetrics.Evaluate(prediction, actualHomeScore: 0, actualAwayScore: 4);

        Assert.False(evaluation.ExactScoreCorrect);
        Assert.False(evaluation.ExactScoreInTopThree);
        Assert.False(evaluation.OutcomeCorrect);
        Assert.Equal(MatchOutcome.AwayWin, evaluation.Actual);
    }

    [Fact]
    public void TopThreeIsWiderThanTopOne()
    {
        var match = TestLeague.Fixture(3, round: 5, homeTeamId: 100, awayTeamId: 200);
        var prediction = MatchPrediction.FromLambdas(match, "Test", 1.6, 1.3, ScoreGrid.DefaultMaxGoals);

        var top = prediction.TopScores(3).ToList();
        var second = top[1];

        var evaluation = PredictionMetrics.Evaluate(prediction, second.Home, second.Away);

        Assert.False(evaluation.ExactScoreCorrect);
        Assert.True(evaluation.ExactScoreInTopThree);
    }

    [Fact]
    public void SummaryAveragesPerMatchMetrics()
    {
        var evaluations = new List<MatchEvaluation>
        {
            new() { ModelName = "M", Brier = 0.4, RankedProbability = 0.2, LogLoss = 1.0, OutcomeCorrect = true, ExactScoreCorrect = true, ExactScoreInTopThree = true, ProbabilityOfActualScore = 0.1, ProbabilityOfActualOutcome = 0.5 },
            new() { ModelName = "M", Brier = 0.6, RankedProbability = 0.3, LogLoss = 2.0, OutcomeCorrect = false, ExactScoreCorrect = false, ExactScoreInTopThree = true, ProbabilityOfActualScore = 0.05, ProbabilityOfActualOutcome = 0.25 }
        };

        var summary = MetricSummary.From(evaluations);

        Assert.Equal("M", summary.ModelName);
        Assert.Equal(2, summary.MatchCount);
        Assert.Equal(0.5, summary.Brier, precision: 12);
        Assert.Equal(0.25, summary.RankedProbability, precision: 12);
        Assert.Equal(1.5, summary.LogLoss, precision: 12);
        Assert.Equal(0.5, summary.OutcomeAccuracy, precision: 12);
        Assert.Equal(0.5, summary.ExactScoreAccuracy, precision: 12);
        Assert.Equal(1.0, summary.ExactScoreTopThreeAccuracy, precision: 12);
        Assert.Equal(0.075, summary.MeanProbabilityOfActualScore, precision: 12);
    }

    [Fact]
    public void EmptySummaryIsSafe()
    {
        var summary = MetricSummary.From([], "None");

        Assert.Equal("None", summary.ModelName);
        Assert.Equal(0, summary.MatchCount);
        Assert.Equal(0, summary.Brier, precision: 12);
    }

    [Fact]
    public void MetricKindSelectsTheRightField()
    {
        var evaluation = new MatchEvaluation { Brier = 0.4, RankedProbability = 0.2, LogLoss = 1.1, ProbabilityOfActualScore = 0.07 };

        Assert.Equal(0.4, MetricKind.Brier.ValueOf(evaluation), precision: 12);
        Assert.Equal(0.2, MetricKind.RankedProbability.ValueOf(evaluation), precision: 12);
        Assert.Equal(1.1, MetricKind.LogLoss.ValueOf(evaluation), precision: 12);
        Assert.Equal(0.07, MetricKind.ProbabilityOfActualScore.ValueOf(evaluation), precision: 12);

        Assert.True(MetricKind.Brier.LowerIsBetter());
        Assert.False(MetricKind.ProbabilityOfActualScore.LowerIsBetter());
    }
}
