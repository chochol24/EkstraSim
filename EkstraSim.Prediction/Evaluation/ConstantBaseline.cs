using EkstraSim.Prediction.Metrics;
using EkstraSim.Prediction.Models;

namespace EkstraSim.Prediction.Evaluation;

public static class ConstantBaseline
{
    public const string Name = "Stała";

    public static OutcomeProbabilities Fit(IReadOnlyList<MatchData> history, int leagueId)
    {
        var played = history.Where(m => m.LeagueId == leagueId && m.IsPlayed).ToList();

        if (played.Count == 0)
        {
            throw new ArgumentException($"Brak rozegranych meczów ligi {leagueId} w historii.", nameof(history));
        }

        var homeWins = played.Count(m => m.HomeScore > m.AwayScore);
        var draws = played.Count(m => m.HomeScore == m.AwayScore);
        var awayWins = played.Count - homeWins - draws;

        return new OutcomeProbabilities(
            (double)homeWins / played.Count,
            (double)draws / played.Count,
            (double)awayWins / played.Count);
    }

    public static IReadOnlyList<MatchEvaluation> Evaluate(
        OutcomeProbabilities probabilities,
        IReadOnlyList<MatchData> evaluation,
        ISet<int> promotedTeamIds)
    {
        var predicted = probabilities.MostLikely();

        return evaluation
            .Where(m => m.IsPlayed)
            .OrderBy(m => m.Id)
            .Select(match =>
            {
                var actual = PredictionMetrics.OutcomeOf(match.HomeScore!.Value, match.AwayScore!.Value);

                return new MatchEvaluation
                {
                    MatchId = match.Id,
                    ModelName = Name,
                    Round = match.Round,
                    InvolvesPromotedTeam = promotedTeamIds.Contains(match.HomeTeamId) || promotedTeamIds.Contains(match.AwayTeamId),
                    Actual = actual,
                    Predicted = predicted,
                    Brier = PredictionMetrics.BrierScore(probabilities, actual),
                    RankedProbability = PredictionMetrics.RankedProbabilityScore(probabilities, actual),
                    LogLoss = PredictionMetrics.LogLoss(probabilities, actual),
                    OutcomeCorrect = predicted == actual,
                    ProbabilityOfActualOutcome = probabilities.Of(actual)
                };
            })
            .ToList();
    }
}
