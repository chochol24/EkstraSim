using EkstraSim.Prediction.Metrics;
using EkstraSim.Prediction.Models;

namespace EkstraSim.Prediction.Evaluation;

public sealed record SensitivitySeason(
    int SeasonId,
    int TrainingLastRound,
    IReadOnlyList<MatchData> History,
    IReadOnlyList<MatchData> Evaluation,
    TrainingOptions Options,
    IReadOnlyList<PromotedTeamHistory> PromotedTeams)
{
    public IReadOnlyDictionary<int, (int HomeTeamId, int AwayTeamId)> TeamsByMatch =>
        Evaluation.ToDictionary(m => m.Id, m => (m.HomeTeamId, m.AwayTeamId));

    public static SensitivitySeason Build(
        IReadOnlyList<MatchData> matches,
        IReadOnlyList<int> chronology,
        int leagueId,
        int seasonId,
        int trainingLastRound,
        IReadOnlyCollection<int> promotedTeamIds)
    {
        var leagueMatches = matches.Where(m => m.LeagueId == leagueId).ToList();

        return new SensitivitySeason(
            seasonId,
            trainingLastRound,
            WalkForwardEvaluator.BuildHistory(leagueMatches, chronology, seasonId, trainingLastRound),
            WalkForwardEvaluator.BuildEvaluationSet(leagueMatches, seasonId, trainingLastRound),
            new TrainingOptions
            {
                LeagueId = leagueId,
                SeasonId = seasonId,
                SeasonChronology = chronology
            },
            PromotedTeamHistory.Compute(leagueMatches, chronology, leagueId, seasonId, promotedTeamIds.Order().ToList()));
    }
}

public sealed record VariantOutcome(SensitivityVariant Variant, IReadOnlyList<MatchEvaluation>? Evaluations, string? Error)
{
    public bool Succeeded => Evaluations != null;
}

public static class SensitivitySweep
{
    public static IReadOnlyList<VariantOutcome> Run(
        SensitivitySeason season,
        IReadOnlyList<SensitivityVariant> variants,
        int maxDegreeOfParallelism,
        Func<string, IPredictionModel>? createModel = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxDegreeOfParallelism, 1);
        createModel ??= PredictionModelFactory.Create;

        var promotedTeamIds = season.PromotedTeams.Select(team => team.TeamId).ToHashSet();
        var outcomes = new VariantOutcome[variants.Count];

        Parallel.For(
            0,
            variants.Count,
            new ParallelOptions { MaxDegreeOfParallelism = maxDegreeOfParallelism },
            index => outcomes[index] = Evaluate(season, variants[index], promotedTeamIds, createModel));

        return outcomes;
    }

    private static VariantOutcome Evaluate(
        SensitivitySeason season,
        SensitivityVariant variant,
        ISet<int> promotedTeamIds,
        Func<string, IPredictionModel> createModel)
    {
        try
        {
            var evaluations = WalkForwardEvaluator
                .Run(
                    createModel(variant.ModelName),
                    season.History,
                    season.Evaluation,
                    variant.Apply(season.Options),
                    promotedTeamIds,
                    () => createModel(variant.ModelName))
                .SelectMany(round => round.Evaluations)
                .OrderBy(e => e.MatchId)
                .ToList();

            return new VariantOutcome(variant, evaluations, null);
        }
        catch (ModelConvergenceException exception)
        {
            return new VariantOutcome(variant, null, exception.Message);
        }
    }
}
