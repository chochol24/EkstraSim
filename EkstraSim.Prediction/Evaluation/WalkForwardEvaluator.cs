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
    public IReadOnlyList<int> DateFilteredMatchIds { get; init; } = [];

    public MetricSummary Summary => MetricSummary.From(Evaluations, ModelName);
}

public static class WalkForwardEvaluator
{
    public static List<RoundEvaluation> Run(
        IPredictionModel model,
        IReadOnlyList<MatchData> history,
        IReadOnlyList<MatchData> evaluationMatches,
        TrainingOptions options,
        ISet<int>? promotedTeamIds = null,
        Func<IPredictionModel>? createModel = null)
    {
        createModel ??= () => PredictionModelFactory.Create(model.Name);

        model.Train(history, options);

        var absorbed = history.Where(m => m.IsPlayed).ToList();
        var activeTeamIds = ActiveTeamIds(history, evaluationMatches);
        var previousSnapshot = model.GetParametersSnapshot().RestrictToTeams(activeTeamIds);
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
            var dateFilteredMatchIds = new List<int>();
            var restrictedModels = new Dictionary<string, IPredictionModel>();

            foreach (var match in matchesInRound)
            {
                var hiddenIds = absorbed
                    .Where(m => m.Date >= match.Date)
                    .Select(m => m.Id)
                    .Order()
                    .ToList();

                var predictor = model;

                if (hiddenIds.Count > 0)
                {
                    predictor = RestrictedModel(absorbed, hiddenIds, restrictedModels, createModel, options);
                    dateFilteredMatchIds.Add(match.Id);
                }

                var prediction = predictor.Predict(match);
                predictions.Add(prediction);

                var involvesPromoted = promotedTeamIds != null
                    && (promotedTeamIds.Contains(match.HomeTeamId) || promotedTeamIds.Contains(match.AwayTeamId));

                evaluations.Add(PredictionMetrics
                    .Evaluate(prediction, match.HomeScore!.Value, match.AwayScore!.Value)
                    .WithContext(round, involvesPromoted));
            }

            model.UpdateWithRound(matchesInRound);
            absorbed.AddRange(matchesInRound);
            var currentSnapshot = model.GetParametersSnapshot().RestrictToTeams(activeTeamIds);

            results.Add(new RoundEvaluation
            {
                Round = round,
                ModelName = model.Name,
                Predictions = predictions,
                Evaluations = evaluations,
                ParameterDrift = ModelSnapshot.NormalisedDistance(previousSnapshot, currentSnapshot),
                DateFilteredMatchIds = dateFilteredMatchIds
            });

            previousSnapshot = currentSnapshot;
        }

        return results;
    }

    private static HashSet<int> ActiveTeamIds(IReadOnlyList<MatchData> history, IReadOnlyList<MatchData> evaluationMatches)
    {
        var seasonIds = evaluationMatches
            .Where(m => m.SeasonId.HasValue)
            .Select(m => m.SeasonId!.Value)
            .ToHashSet();

        return history
            .Concat(evaluationMatches)
            .Where(m => m.SeasonId.HasValue && seasonIds.Contains(m.SeasonId.Value))
            .SelectMany(m => new[] { m.HomeTeamId, m.AwayTeamId })
            .ToHashSet();
    }

    private static IPredictionModel RestrictedModel(
        IReadOnlyList<MatchData> absorbed,
        IReadOnlyList<int> hiddenIds,
        Dictionary<string, IPredictionModel> restrictedModels,
        Func<IPredictionModel> createModel,
        TrainingOptions options)
    {
        var key = string.Join(",", hiddenIds);

        if (restrictedModels.TryGetValue(key, out var restricted))
        {
            return restricted;
        }

        var hidden = hiddenIds.ToHashSet();
        restricted = createModel();
        restricted.Train(absorbed.Where(m => !hidden.Contains(m.Id)).ToList(), options);
        restrictedModels[key] = restricted;

        return restricted;
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
