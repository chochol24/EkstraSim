namespace EkstraSim.Prediction.Models;

public sealed class MatchPrediction
{
    public int MatchId { get; init; }
    public string ModelName { get; init; } = string.Empty;

    public double ExpectedHomeGoals { get; init; }
    public double ExpectedAwayGoals { get; init; }

    public double HomeWinProbability { get; init; }
    public double DrawProbability { get; init; }
    public double AwayWinProbability { get; init; }

    public int PredictedHomeScore { get; init; }
    public int PredictedAwayScore { get; init; }

    public double[,] ScoreProbabilities { get; init; } = new double[0, 0];

    public static MatchPrediction FromLambdas(MatchData match, string modelName, double lambdaHome, double lambdaAway, int maxGoals)
    {
        var grid = ScoreGrid.FromIndependentPoisson(lambdaHome, lambdaAway, maxGoals);
        return FromGrid(match, modelName, lambdaHome, lambdaAway, grid);
    }

    public static MatchPrediction FromGrid(MatchData match, string modelName, double lambdaHome, double lambdaAway, double[,] grid)
    {
        var outcomes = ScoreGrid.Outcomes(grid);
        var best = ScoreGrid.MostLikelyScore(grid);

        return new MatchPrediction
        {
            MatchId = match.Id,
            ModelName = modelName,
            ExpectedHomeGoals = lambdaHome,
            ExpectedAwayGoals = lambdaAway,
            HomeWinProbability = outcomes.HomeWin,
            DrawProbability = outcomes.Draw,
            AwayWinProbability = outcomes.AwayWin,
            PredictedHomeScore = best.Home,
            PredictedAwayScore = best.Away,
            ScoreProbabilities = grid
        };
    }

    public double ProbabilityOf(int homeScore, int awayScore) => ScoreGrid.ProbabilityOf(ScoreProbabilities, homeScore, awayScore);

    public IEnumerable<(int Home, int Away)> TopScores(int count) => ScoreGrid.RankedScores(ScoreProbabilities).Take(count);
}
