using EkstraSim.Prediction.Metrics;
using EkstraSim.Prediction.Models;

namespace EkstraSim.Prediction.Evaluation;

public sealed class RoundEvaluation
{
    public int Round { get; init; }
    public string ModelName { get; init; } = string.Empty;
    public List<MatchPrediction> Predictions { get; init; } = [];
    public List<MatchEvaluation> Evaluations { get; init; } = [];
    public double ParameterDrift { get; init; }

    public MetricSummary Summary => MetricSummary.From(Evaluations, ModelName);
}

public static class WalkForwardEvaluator
{
    public static List<RoundEvaluation> Run(
        IPredictionModel model,
        IReadOnlyList<MatchData> history,
        IReadOnlyList<MatchData> evaluationMatches,
        TrainingOptions options,
        ISet<int>? promotedTeamIds = null)
    {
        model.Train(history, options);

        var previousSnapshot = model.GetParametersSnapshot();
        var rounds = SeasonCalendar.RoundsInOrder(evaluationMatches);
        var results = new List<RoundEvaluation>();

        foreach (var round in rounds)
        {
            var matchesInRound = evaluationMatches
                .Where(m => m.Round == round && m.IsPlayed)
                .OrderBy(m => m.Id)
                .ToList();

            if (matchesInRound.Count == 0)
            {
                continue;
            }

            var predictions = new List<MatchPrediction>(matchesInRound.Count);
            var evaluations = new List<MatchEvaluation>(matchesInRound.Count);

            foreach (var match in matchesInRound)
            {
                var prediction = model.Predict(match);
                predictions.Add(prediction);

                var involvesPromoted = promotedTeamIds != null
                    && (promotedTeamIds.Contains(match.HomeTeamId) || promotedTeamIds.Contains(match.AwayTeamId));

                evaluations.Add(PredictionMetrics
                    .Evaluate(prediction, match.HomeScore!.Value, match.AwayScore!.Value)
                    .WithContext(round, involvesPromoted));
            }

            model.UpdateWithRound(matchesInRound);
            var currentSnapshot = model.GetParametersSnapshot();

            results.Add(new RoundEvaluation
            {
                Round = round,
                ModelName = model.Name,
                Predictions = predictions,
                Evaluations = evaluations,
                ParameterDrift = ModelSnapshot.Distance(previousSnapshot, currentSnapshot)
            });

            previousSnapshot = currentSnapshot;
        }

        return results;
    }

    public static List<MatchData> BuildHistory(
        IReadOnlyList<MatchData> leagueMatches,
        IReadOnlyList<int> seasonChronology,
        int targetSeasonId,
        int trainingLastRound)
    {
        var targetIndex = seasonChronology.ToList().IndexOf(targetSeasonId);
        var earlierSeasons = targetIndex > 0
            ? seasonChronology.Take(targetIndex).ToHashSet()
            : [];

        return leagueMatches
            .Where(m => m.IsPlayed)
            .Where(m => m.SeasonId.HasValue
                && (earlierSeasons.Contains(m.SeasonId.Value)
                    || (m.SeasonId.Value == targetSeasonId && m.Round <= trainingLastRound)))
            .OrderBy(m => m.Date)
            .ToList();
    }

    public static List<MatchData> BuildEvaluationSet(
        IReadOnlyList<MatchData> leagueMatches,
        int targetSeasonId,
        int trainingLastRound)
    {
        return leagueMatches
            .Where(m => m.SeasonId == targetSeasonId && m.Round > trainingLastRound && m.IsPlayed)
            .OrderBy(m => m.Round)
            .ThenBy(m => m.Id)
            .ToList();
    }
}
