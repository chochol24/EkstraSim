using AutoMapper;
using EkstraSim.Backend.Database.Entities;
using EkstraSim.Prediction.Evaluation;
using EkstraSim.Prediction.Metrics;
using EkstraSim.Prediction.Models;
using EkstraSim.Prediction.Statistics;
using EkstraSim.Shared;
using EkstraSim.Shared.DTOs;
using EkstraSim.Shared.Requests;
using EkstraSim.Shared.Resources;
using EkstraSim.Shared.Results;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace EkstraSim.Backend.Database.Services.Research;

public class ResearchRunService
{
    private readonly IDbContextFactory<EkstraSimDbContext> _dbFactory;
    private readonly IMapper _mapper;
    private readonly PromotedTeamsService _promotedTeams;
    private readonly IResearchRunLauncher _launcher;

    public ResearchRunService(
        IDbContextFactory<EkstraSimDbContext> dbFactory,
        IMapper mapper,
        PromotedTeamsService promotedTeams,
        IResearchRunLauncher launcher)
    {
        _dbFactory = dbFactory;
        _mapper = mapper;
        _promotedTeams = promotedTeams;
        _launcher = launcher;
    }

    public async Task<EkstraSimResult<ModelEvaluationRunDTO>> CreateAsync(CreateEvaluationRunRequest request)
    {
        try
        {
            await using var context = await _dbFactory.CreateDbContextAsync();

            var season = await context.Seasons
                .FirstOrDefaultAsync(s => s.Id == request.SeasonId && s.LeagueId == request.LeagueId);

            if (season == null)
            {
                return Failure<ModelEvaluationRunDTO>($"Sezon {request.SeasonId} nie istnieje w lidze {request.LeagueId}.");
            }

            var models = request.Models.Where(PredictionModelFactory.IsKnown).ToList();
            if (models.Count == 0)
            {
                models = PredictionModelFactory.AvailableModels.ToList();
            }

            var trainingLastRound = request.TrainingLastRound ?? await DetectCutoffAsync(context, request.LeagueId, request.SeasonId);
            if (trainingLastRound <= 0)
            {
                return Failure<ModelEvaluationRunDTO>("Nie udalo sie wyznaczyc kolejki odciecia - podaj ja recznie.");
            }

            var promoted = await _promotedTeams.GetPromotedTeamsAsync(context, request.LeagueId, request.SeasonId);

            var run = new ModelEvaluationRun
            {
                LeagueId = request.LeagueId,
                SeasonId = request.SeasonId,
                TrainingLastRound = trainingLastRound,
                Models = string.Join(",", models),
                OptionsJson = JsonConvert.SerializeObject(request),
                PromotedTeamsJson = JsonConvert.SerializeObject(promoted),
                Comments = request.Comments,
                Status = EvaluationRunStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };

            context.ModelEvaluationRuns.Add(run);
            await context.SaveChangesAsync();

            _launcher.Launch(run.Id);

            return new EkstraSimResult<ModelEvaluationRunDTO>
            {
                Success = true,
                Data = _mapper.Map<ModelEvaluationRunDTO>(run)
            };
        }
        catch (Exception ex)
        {
            return Failure<ModelEvaluationRunDTO>($"{SnackbarMessages.Error_Base} {ex.Message}");
        }
    }

    public async Task<EkstraSimResult<IEnumerable<ModelEvaluationRunDTO>>> GetRunsAsync(int? leagueId, int? seasonId)
    {
        try
        {
            await using var context = await _dbFactory.CreateDbContextAsync();

            var query = context.ModelEvaluationRuns
                .Include(r => r.League)
                .Include(r => r.Season)
                .AsQueryable();

            if (leagueId.HasValue)
            {
                query = query.Where(r => r.LeagueId == leagueId.Value);
            }

            if (seasonId.HasValue)
            {
                query = query.Where(r => r.SeasonId == seasonId.Value);
            }

            var runs = await query.OrderByDescending(r => r.CreatedAt).ToListAsync();

            return new EkstraSimResult<IEnumerable<ModelEvaluationRunDTO>>
            {
                Success = true,
                Data = runs.Select(_mapper.Map<ModelEvaluationRunDTO>).ToList()
            };
        }
        catch (Exception ex)
        {
            return Failure<IEnumerable<ModelEvaluationRunDTO>>($"{SnackbarMessages.Error_Get}{ex.Message}");
        }
    }

    public async Task<EkstraSimResult<ModelEvaluationRunDTO>> GetRunAsync(int runId)
    {
        try
        {
            await using var context = await _dbFactory.CreateDbContextAsync();

            var run = await context.ModelEvaluationRuns
                .Include(r => r.League)
                .Include(r => r.Season)
                .FirstOrDefaultAsync(r => r.Id == runId);

            if (run == null)
            {
                return Failure<ModelEvaluationRunDTO>($"Badanie {runId} nie istnieje.");
            }

            return new EkstraSimResult<ModelEvaluationRunDTO>
            {
                Success = true,
                Data = _mapper.Map<ModelEvaluationRunDTO>(run)
            };
        }
        catch (Exception ex)
        {
            return Failure<ModelEvaluationRunDTO>($"{SnackbarMessages.Error_Get}{ex.Message}");
        }
    }

    public async Task<EkstraSimResult<IEnumerable<ModelRoundMetricDTO>>> GetRoundMetricsAsync(int runId)
    {
        try
        {
            await using var context = await _dbFactory.CreateDbContextAsync();

            var metrics = await context.ModelRoundMetrics
                .Where(m => m.ModelEvaluationRunId == runId)
                .OrderBy(m => m.ModelName)
                .ThenBy(m => m.Round)
                .ToListAsync();

            return new EkstraSimResult<IEnumerable<ModelRoundMetricDTO>>
            {
                Success = true,
                Data = metrics.Select(_mapper.Map<ModelRoundMetricDTO>).ToList()
            };
        }
        catch (Exception ex)
        {
            return Failure<IEnumerable<ModelRoundMetricDTO>>($"{SnackbarMessages.Error_Get}{ex.Message}");
        }
    }

    public async Task<EkstraSimResult<IEnumerable<ModelPredictionDTO>>> GetPredictionsAsync(int runId, string? modelName, int? round)
    {
        try
        {
            await using var context = await _dbFactory.CreateDbContextAsync();

            var query = context.ModelPredictions
                .Include(p => p.Match)
                    .ThenInclude(m => m.HomeTeam)
                .Include(p => p.Match)
                    .ThenInclude(m => m.AwayTeam)
                .Where(p => p.ModelEvaluationRunId == runId);

            if (!string.IsNullOrWhiteSpace(modelName))
            {
                query = query.Where(p => p.ModelName == modelName);
            }

            if (round.HasValue)
            {
                query = query.Where(p => p.Round == round.Value);
            }

            var predictions = await query
                .OrderBy(p => p.Round)
                .ThenBy(p => p.ModelName)
                .ThenBy(p => p.MatchId)
                .ToListAsync();

            return new EkstraSimResult<IEnumerable<ModelPredictionDTO>>
            {
                Success = true,
                Data = predictions.Select(_mapper.Map<ModelPredictionDTO>).ToList()
            };
        }
        catch (Exception ex)
        {
            return Failure<IEnumerable<ModelPredictionDTO>>($"{SnackbarMessages.Error_Get}{ex.Message}");
        }
    }

    public async Task<EkstraSimResult<ModelComparisonDTO>> GetComparisonAsync(int runId, string? metricName)
    {
        try
        {
            await using var context = await _dbFactory.CreateDbContextAsync();

            var run = await context.ModelEvaluationRuns.FirstOrDefaultAsync(r => r.Id == runId);
            if (run == null)
            {
                return Failure<ModelComparisonDTO>($"Badanie {runId} nie istnieje.");
            }

            var metric = ParseMetric(metricName);

            var rows = await context.ModelPredictions
                .Where(p => p.ModelEvaluationRunId == runId)
                .Select(p => new EvaluationRow(
                    p.MatchId,
                    p.ModelName,
                    p.Round,
                    p.InvolvesPromotedTeam,
                    p.ActualHomeScore,
                    p.ActualAwayScore,
                    p.HomeWinProbability,
                    p.DrawProbability,
                    p.AwayWinProbability,
                    p.Brier,
                    p.RankedProbabilityScore,
                    p.LogLoss,
                    p.OutcomeCorrect,
                    p.ExactScoreCorrect,
                    p.ExactScoreInTopThree,
                    p.ProbabilityOfActualScore,
                    p.ProbabilityOfActualOutcome))
                .ToListAsync();

            if (rows.Count == 0)
            {
                return Failure<ModelComparisonDTO>("Badanie nie ma jeszcze zapisanych predykcji.");
            }

            var evaluationsByModel = rows
                .GroupBy(r => r.ModelName)
                .ToDictionary(
                    g => g.Key,
                    g => (IReadOnlyList<MatchEvaluation>)g.Select(ToEvaluation).ToList());

            var roundMetrics = await context.ModelRoundMetrics
                .Where(m => m.ModelEvaluationRunId == runId)
                .OrderBy(m => m.Round)
                .ToListAsync();

            var options = string.IsNullOrWhiteSpace(run.OptionsJson)
                ? new CreateEvaluationRunRequest()
                : JsonConvert.DeserializeObject<CreateEvaluationRunRequest>(run.OptionsJson) ?? new CreateEvaluationRunRequest();

            var comparison = new ModelComparisonDTO
            {
                RunId = runId,
                Metric = metric.ToString(),
                Summaries = evaluationsByModel
                    .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .Select(pair => ToSummaryDto(MetricSummary.From(pair.Value, pair.Key)))
                    .ToList(),
                Pairwise = ModelComparison.Pairwise(evaluationsByModel, metric).Select(ToPairwiseDto).ToList(),
                Promoted = BuildPromotedComparisons(evaluationsByModel, metric, roundMetrics),
                Stability = BuildStability(roundMetrics, metric, options)
            };

            return new EkstraSimResult<ModelComparisonDTO>
            {
                Success = true,
                Data = comparison
            };
        }
        catch (Exception ex)
        {
            return Failure<ModelComparisonDTO>($"{SnackbarMessages.Error_Get}{ex.Message}");
        }
    }

    private static List<PromotedComparisonDTO> BuildPromotedComparisons(
        Dictionary<string, IReadOnlyList<MatchEvaluation>> evaluationsByModel,
        MetricKind metric,
        List<ModelRoundMetric> roundMetrics)
    {
        var rounds = roundMetrics.Select(m => m.Round).Distinct().OrderBy(r => r).ToList();
        var windows = BuildRoundWindows(rounds);

        var overall = ModelComparison.PromotedVersusRest(evaluationsByModel, metric);
        var windowed = windows.Count > 0
            ? ModelComparison.PromotedVersusRest(evaluationsByModel, metric, windows)
            : [];

        return overall.Concat(windowed).Select(ToPromotedDto).ToList();
    }

    private static List<(int From, int To)> BuildRoundWindows(List<int> rounds)
    {
        if (rounds.Count < 4)
        {
            return [];
        }

        var windows = new List<(int From, int To)>();
        const int windowSize = 5;

        for (var start = 0; start < rounds.Count; start += windowSize)
        {
            var slice = rounds.Skip(start).Take(windowSize).ToList();
            if (slice.Count > 0)
            {
                windows.Add((slice.First(), slice.Last()));
            }
        }

        return windows;
    }

    private static List<StabilityDTO> BuildStability(
        List<ModelRoundMetric> roundMetrics,
        MetricKind metric,
        CreateEvaluationRunRequest options)
    {
        return roundMetrics
            .GroupBy(m => m.ModelName)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(group =>
            {
                var observations = group
                    .OrderBy(m => m.Round)
                    .Select(m => new RoundObservation(m.Round, MetricValueOf(m, metric), m.ParameterDrift))
                    .ToList();

                var result = StabilityAnalysis.Detect(group.Key, observations, options.StabilityThreshold, options.StabilityWindow);

                return new StabilityDTO
                {
                    ModelName = result.ModelName,
                    StabilisedFromRound = result.StabilisedFromRound,
                    Threshold = result.Threshold,
                    Window = result.Window,
                    Rounds = result.Rounds.ToList(),
                    RollingMetric = result.RollingMetric.ToList(),
                    RollingDrift = result.RollingDrift.ToList()
                };
            })
            .ToList();
    }

    private static double MetricValueOf(ModelRoundMetric metric, MetricKind kind) => kind switch
    {
        MetricKind.Brier => metric.Brier,
        MetricKind.RankedProbability => metric.RankedProbabilityScore,
        MetricKind.LogLoss => metric.LogLoss,
        _ => metric.MeanProbabilityOfActualScore
    };

    private sealed record EvaluationRow(
        int MatchId,
        string ModelName,
        int? Round,
        bool InvolvesPromotedTeam,
        int ActualHomeScore,
        int ActualAwayScore,
        double HomeWinProbability,
        double DrawProbability,
        double AwayWinProbability,
        double Brier,
        double RankedProbabilityScore,
        double LogLoss,
        bool OutcomeCorrect,
        bool ExactScoreCorrect,
        bool ExactScoreInTopThree,
        double ProbabilityOfActualScore,
        double ProbabilityOfActualOutcome);

    private static MatchEvaluation ToEvaluation(EvaluationRow row)
    {
        var probabilities = new OutcomeProbabilities(row.HomeWinProbability, row.DrawProbability, row.AwayWinProbability);

        return new MatchEvaluation
        {
            MatchId = row.MatchId,
            ModelName = row.ModelName,
            Round = row.Round,
            InvolvesPromotedTeam = row.InvolvesPromotedTeam,
            Actual = PredictionMetrics.OutcomeOf(row.ActualHomeScore, row.ActualAwayScore),
            Predicted = probabilities.MostLikely(),
            Brier = row.Brier,
            RankedProbability = row.RankedProbabilityScore,
            LogLoss = row.LogLoss,
            OutcomeCorrect = row.OutcomeCorrect,
            ExactScoreCorrect = row.ExactScoreCorrect,
            ExactScoreInTopThree = row.ExactScoreInTopThree,
            ProbabilityOfActualScore = row.ProbabilityOfActualScore,
            ProbabilityOfActualOutcome = row.ProbabilityOfActualOutcome
        };
    }

    private static ModelSummaryDTO ToSummaryDto(MetricSummary summary) => new()
    {
        ModelName = summary.ModelName,
        MatchCount = summary.MatchCount,
        Brier = summary.Brier,
        RankedProbabilityScore = summary.RankedProbability,
        LogLoss = summary.LogLoss,
        OutcomeAccuracy = summary.OutcomeAccuracy,
        ExactScoreAccuracy = summary.ExactScoreAccuracy,
        ExactScoreTopThreeAccuracy = summary.ExactScoreTopThreeAccuracy,
        MeanProbabilityOfActualScore = summary.MeanProbabilityOfActualScore,
        MeanProbabilityOfActualOutcome = summary.MeanProbabilityOfActualOutcome
    };

    private static PairwiseComparisonDTO ToPairwiseDto(PairwiseComparison comparison) => new()
    {
        FirstModel = comparison.FirstModel,
        SecondModel = comparison.SecondModel,
        FirstMean = comparison.FirstMean,
        SecondMean = comparison.SecondMean,
        PairedMatchCount = comparison.PairedMatchCount,
        BetterModel = comparison.BetterModel,
        Statistic = comparison.Test.Statistic,
        ZScore = comparison.Test.ZScore,
        PValue = comparison.Test.PValue,
        AdjustedPValue = comparison.AdjustedPValue,
        IsConclusive = comparison.Test.IsConclusive,
        IsSignificant = comparison.Test.IsConclusive && comparison.AdjustedPValue < 0.05
    };

    private static PromotedComparisonDTO ToPromotedDto(PromotedTeamComparison comparison) => new()
    {
        ModelName = comparison.ModelName,
        FromRound = comparison.FromRound,
        ToRound = comparison.ToRound,
        PromotedMean = comparison.PromotedMean,
        OtherMean = comparison.OtherMean,
        PromotedCount = comparison.PromotedCount,
        OtherCount = comparison.OtherCount,
        Difference = comparison.Difference,
        PValue = comparison.Test.PValue,
        AdjustedPValue = comparison.AdjustedPValue,
        IsConclusive = comparison.Test.IsConclusive,
        IsSignificant = comparison.Test.IsConclusive && comparison.AdjustedPValue < 0.05
    };

    private static MetricKind ParseMetric(string? name)
    {
        return Enum.TryParse<MetricKind>(name, ignoreCase: true, out var metric) ? metric : MetricKind.RankedProbability;
    }

    private static async Task<int> DetectCutoffAsync(EkstraSimDbContext context, int leagueId, int seasonId)
    {
        var matches = (await context.Matches
                .Where(m => m.SeasonId == seasonId && m.LeagueId == leagueId)
                .ToListAsync())
            .ToMatchData();

        return SeasonCalendar.DetectSplit(matches).AutumnLastRound ?? 0;
    }

    private static EkstraSimResult<T> Failure<T>(string message) => new()
    {
        Success = false,
        ErrorMessage = message
    };
}
