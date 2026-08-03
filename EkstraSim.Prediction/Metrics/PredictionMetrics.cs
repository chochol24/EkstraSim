using EkstraSim.Prediction.Models;

namespace EkstraSim.Prediction.Metrics;

public static class PredictionMetrics
{
    private const double ProbabilityFloor = 1e-15;
    public const int TopScoreCount = 3;

    public static MatchOutcome OutcomeOf(int homeScore, int awayScore)
    {
        if (homeScore > awayScore)
        {
            return MatchOutcome.HomeWin;
        }

        return homeScore == awayScore ? MatchOutcome.Draw : MatchOutcome.AwayWin;
    }

    public static double BrierScore(OutcomeProbabilities probabilities, MatchOutcome actual)
    {
        double total = 0;

        foreach (var outcome in Enum.GetValues<MatchOutcome>())
        {
            var indicator = outcome == actual ? 1.0 : 0.0;
            var difference = probabilities.Of(outcome) - indicator;
            total += difference * difference;
        }

        return total;
    }

    public static double RankedProbabilityScore(OutcomeProbabilities probabilities, MatchOutcome actual)
    {
        var cumulativeForecast = 0.0;
        var cumulativeObserved = 0.0;
        double total = 0;

        var ordered = new[] { MatchOutcome.HomeWin, MatchOutcome.Draw };

        foreach (var outcome in ordered)
        {
            cumulativeForecast += probabilities.Of(outcome);
            cumulativeObserved += outcome == actual ? 1.0 : 0.0;
            var difference = cumulativeForecast - cumulativeObserved;
            total += difference * difference;
        }

        return total / ordered.Length;
    }

    public static double LogLoss(OutcomeProbabilities probabilities, MatchOutcome actual)
    {
        return -Math.Log(Math.Max(ProbabilityFloor, probabilities.Of(actual)));
    }

    public static MatchEvaluation Evaluate(MatchPrediction prediction, int actualHomeScore, int actualAwayScore)
    {
        var probabilities = new OutcomeProbabilities(
            prediction.HomeWinProbability,
            prediction.DrawProbability,
            prediction.AwayWinProbability);

        var actual = OutcomeOf(actualHomeScore, actualAwayScore);
        var topScores = prediction.TopScores(TopScoreCount).ToList();

        return new MatchEvaluation
        {
            MatchId = prediction.MatchId,
            ModelName = prediction.ModelName,
            Actual = actual,
            Predicted = probabilities.MostLikely(),
            Brier = BrierScore(probabilities, actual),
            RankedProbability = RankedProbabilityScore(probabilities, actual),
            LogLoss = LogLoss(probabilities, actual),
            OutcomeCorrect = probabilities.MostLikely() == actual,
            ExactScoreCorrect = prediction.PredictedHomeScore == actualHomeScore
                && prediction.PredictedAwayScore == actualAwayScore,
            ExactScoreInTopThree = topScores.Contains((actualHomeScore, actualAwayScore)),
            ProbabilityOfActualScore = prediction.ProbabilityOf(actualHomeScore, actualAwayScore),
            ProbabilityOfActualOutcome = probabilities.Of(actual)
        };
    }
}
