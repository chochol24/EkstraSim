using EkstraSim.Backend.Database.Entities;
using EkstraSim.Prediction.Evaluation;
using EkstraSim.Prediction.Metrics;
using EkstraSim.Prediction.Models;
using EkstraSim.Shared;
using EkstraSim.Shared.DTOs;
using EkstraSim.Shared.Requests;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace EkstraSim.Backend.Database.Services.Research;

public class ResearchOrchestrationService
{
    private const int SaveBatchSize = 500;

    private readonly IDbContextFactory<EkstraSimDbContext> _dbFactory;
    private readonly ILogger<ResearchOrchestrationService> _logger;

    public ResearchOrchestrationService(
        IDbContextFactory<EkstraSimDbContext> dbFactory,
        ILogger<ResearchOrchestrationService> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    public async Task ExecuteAsync(int runId, CancellationToken ct = default)
    {
        await using var context = await _dbFactory.CreateDbContextAsync(ct);

        var run = await context.ModelEvaluationRuns.FirstOrDefaultAsync(r => r.Id == runId, ct);
        if (run == null)
        {
            _logger.LogWarning("Nie znaleziono badania o Id {RunId}.", runId);
            return;
        }

        run.Status = EvaluationRunStatus.Running;
        run.StartedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(ct);

        try
        {
            await RunEvaluationAsync(context, run, ct);

            run.Status = EvaluationRunStatus.Completed;
            run.FinishedAt = DateTime.UtcNow;
            await context.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Badanie {RunId} zakonczylo sie bledem.", runId);

            run.Status = EvaluationRunStatus.Failed;
            run.ErrorMessage = ex.Message;
            run.FinishedAt = DateTime.UtcNow;
            await context.SaveChangesAsync(ct);
        }
    }

    private async Task RunEvaluationAsync(EkstraSimDbContext context, ModelEvaluationRun run, CancellationToken ct)
    {
        var leagueMatches = (await context.Matches
                .Where(m => m.LeagueId == run.LeagueId && m.SeasonId != null)
                .ToListAsync(ct))
            .ToMatchData();

        var chronology = await SeasonStructureService.GetSeasonChronologyAsync(context, run.LeagueId);

        var history = WalkForwardEvaluator.BuildHistory(leagueMatches, chronology, run.SeasonId, run.TrainingLastRound);
        var evaluationSet = WalkForwardEvaluator.BuildEvaluationSet(leagueMatches, run.SeasonId, run.TrainingLastRound);

        if (evaluationSet.Count == 0)
        {
            throw new InvalidOperationException(
                $"Brak rozegranych meczow po kolejce {run.TrainingLastRound} w sezonie {run.SeasonId} - nie ma czego oceniac.");
        }

        var promotedTeamIds = ReadPromotedTeamIds(run.PromotedTeamsJson);
        var options = BuildOptions(run, chronology);
        var modelNames = SplitModels(run.Models);

        var matchesById = evaluationSet.ToDictionary(m => m.Id);
        var predictionRows = new List<ModelPrediction>();
        var metricRows = new List<ModelRoundMetric>();

        foreach (var modelName in modelNames)
        {
            var model = PredictionModelFactory.Create(modelName);
            var rounds = WalkForwardEvaluator.Run(model, history, evaluationSet, options, promotedTeamIds);

            foreach (var round in rounds)
            {
                for (var i = 0; i < round.Evaluations.Count; i++)
                {
                    predictionRows.Add(ToEntity(run.Id, round.Predictions[i], round.Evaluations[i], matchesById));
                }

                metricRows.Add(ToEntity(run.Id, round));
            }
        }

        await context.ModelRoundMetrics.AddRangeAsync(metricRows, ct);

        for (var offset = 0; offset < predictionRows.Count; offset += SaveBatchSize)
        {
            var batch = predictionRows.Skip(offset).Take(SaveBatchSize).ToList();
            await context.ModelPredictions.AddRangeAsync(batch, ct);
            await context.SaveChangesAsync(ct);
        }

        await context.SaveChangesAsync(ct);

        run.EvaluatedMatchCount = evaluationSet.Count;
        run.EvaluatedRoundCount = SeasonCalendar.RoundsInOrder(evaluationSet).Count;
    }

    private static ModelPrediction ToEntity(
        int runId,
        MatchPrediction prediction,
        MatchEvaluation evaluation,
        Dictionary<int, MatchData> matchesById)
    {
        var match = matchesById[prediction.MatchId];

        return new ModelPrediction
        {
            ModelEvaluationRunId = runId,
            ModelName = prediction.ModelName,
            MatchId = prediction.MatchId,
            Round = evaluation.Round,
            InvolvesPromotedTeam = evaluation.InvolvesPromotedTeam,
            ExpectedHomeGoals = prediction.ExpectedHomeGoals,
            ExpectedAwayGoals = prediction.ExpectedAwayGoals,
            HomeWinProbability = prediction.HomeWinProbability,
            DrawProbability = prediction.DrawProbability,
            AwayWinProbability = prediction.AwayWinProbability,
            PredictedHomeScore = prediction.PredictedHomeScore,
            PredictedAwayScore = prediction.PredictedAwayScore,
            ResultProbabilityMatrixJson = JsonConvert.SerializeObject(prediction.ScoreProbabilities),
            ActualHomeScore = match.HomeScore!.Value,
            ActualAwayScore = match.AwayScore!.Value,
            Brier = evaluation.Brier,
            RankedProbabilityScore = evaluation.RankedProbability,
            LogLoss = evaluation.LogLoss,
            OutcomeCorrect = evaluation.OutcomeCorrect,
            ExactScoreCorrect = evaluation.ExactScoreCorrect,
            ExactScoreInTopThree = evaluation.ExactScoreInTopThree,
            ProbabilityOfActualScore = evaluation.ProbabilityOfActualScore,
            ProbabilityOfActualOutcome = evaluation.ProbabilityOfActualOutcome
        };
    }

    private static ModelRoundMetric ToEntity(int runId, RoundEvaluation round)
    {
        var summary = round.Summary;

        return new ModelRoundMetric
        {
            ModelEvaluationRunId = runId,
            ModelName = round.ModelName,
            Round = round.Round,
            MatchCount = summary.MatchCount,
            Brier = summary.Brier,
            RankedProbabilityScore = summary.RankedProbability,
            LogLoss = summary.LogLoss,
            OutcomeAccuracy = summary.OutcomeAccuracy,
            ExactScoreAccuracy = summary.ExactScoreAccuracy,
            ExactScoreTopThreeAccuracy = summary.ExactScoreTopThreeAccuracy,
            MeanProbabilityOfActualScore = summary.MeanProbabilityOfActualScore,
            ParameterDrift = round.ParameterDrift
        };
    }

    public static TrainingOptions BuildOptions(ModelEvaluationRun run, IReadOnlyList<int> chronology)
    {
        var options = string.IsNullOrWhiteSpace(run.OptionsJson)
            ? new CreateEvaluationRunRequest()
            : JsonConvert.DeserializeObject<CreateEvaluationRunRequest>(run.OptionsJson) ?? new CreateEvaluationRunRequest();

        return new TrainingOptions
        {
            LeagueId = run.LeagueId,
            SeasonId = run.SeasonId,
            SeasonChronology = chronology,
            UseFormFactors = options.UseFormFactors,
            TimeDecayXi = options.TimeDecayXi,
            RidgeLambda = options.RidgeLambda
        };
    }

    public static HashSet<int> ReadPromotedTeamIds(string? promotedTeamsJson)
    {
        if (string.IsNullOrWhiteSpace(promotedTeamsJson))
        {
            return [];
        }

        var promoted = JsonConvert.DeserializeObject<List<PromotedTeamDTO>>(promotedTeamsJson);
        return promoted?.Select(p => p.TeamId).ToHashSet() ?? [];
    }

    public static List<string> SplitModels(string models)
    {
        return models
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(PredictionModelFactory.IsKnown)
            .ToList();
    }
}
