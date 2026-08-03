using AutoMapper;
using EkstraSim.Prediction.Evaluation;
using EkstraSim.Prediction.Metrics;
using EkstraSim.Prediction.Models;
using EkstraSim.Shared.DTOs;
using EkstraSim.Shared.Requests;
using EkstraSim.Shared.Resources;
using EkstraSim.Shared.Results;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace EkstraSim.Backend.Database.Services.Research;

public class RoundPredictionService
{
    private readonly IDbContextFactory<EkstraSimDbContext> _dbFactory;
    private readonly IMapper _mapper;

    public RoundPredictionService(IDbContextFactory<EkstraSimDbContext> dbFactory, IMapper mapper)
    {
        _dbFactory = dbFactory;
        _mapper = mapper;
    }

    public async Task<EkstraSimResult<IEnumerable<ModelPredictionDTO>>> PredictRoundAsync(PredictRoundRequest request)
    {
        try
        {
            if (!PredictionModelFactory.IsKnown(request.ModelName))
            {
                return Failure($"Nieznany model '{request.ModelName}'. Dostepne: {string.Join(", ", PredictionModelFactory.AvailableModels)}.");
            }

            await using var context = await _dbFactory.CreateDbContextAsync();

            var entities = await context.Matches
                .Include(m => m.HomeTeam)
                .Include(m => m.AwayTeam)
                .Where(m => m.LeagueId == request.LeagueId && m.SeasonId != null)
                .ToListAsync();

            var leagueMatches = entities.ToMatchData();
            var chronology = await SeasonStructureService.GetSeasonChronologyAsync(context, request.LeagueId);

            var trainingLastRound = request.TrainingLastRound ?? request.Round - 1;
            if (trainingLastRound < 0)
            {
                return Failure("Kolejka odciecia nie moze byc ujemna.");
            }

            var toPredict = leagueMatches
                .Where(m => m.SeasonId == request.SeasonId && m.Round == request.Round)
                .OrderBy(m => m.Id)
                .ToList();

            if (toPredict.Count == 0)
            {
                return Failure($"Brak meczow w kolejce {request.Round} sezonu {request.SeasonId}.");
            }

            var history = WalkForwardEvaluator.BuildHistory(leagueMatches, chronology, request.SeasonId, trainingLastRound);

            var options = new TrainingOptions
            {
                LeagueId = request.LeagueId,
                SeasonId = request.SeasonId,
                SeasonChronology = chronology,
                UseFormFactors = request.UseFormFactors,
                TimeDecayXi = request.TimeDecayXi,
                RidgeLambda = request.RidgeLambda
            };

            var model = PredictionModelFactory.Create(request.ModelName);
            model.Train(history, options);

            var matchesById = entities.ToDictionary(m => m.Id);
            var results = toPredict
                .Select(match => ToDto(model.Predict(match), match, matchesById))
                .ToList();

            return new EkstraSimResult<IEnumerable<ModelPredictionDTO>>
            {
                Success = true,
                Data = results
            };
        }
        catch (Exception ex)
        {
            return Failure($"{SnackbarMessages.Error_Base} {ex.Message}");
        }
    }

    private ModelPredictionDTO ToDto(MatchPrediction prediction, MatchData match, Dictionary<int, Entities.Match> matchesById)
    {
        var dto = new ModelPredictionDTO
        {
            ModelName = prediction.ModelName,
            MatchId = prediction.MatchId,
            Round = match.Round,
            ExpectedHomeGoals = prediction.ExpectedHomeGoals,
            ExpectedAwayGoals = prediction.ExpectedAwayGoals,
            HomeWinProbability = prediction.HomeWinProbability,
            DrawProbability = prediction.DrawProbability,
            AwayWinProbability = prediction.AwayWinProbability,
            PredictedHomeScore = prediction.PredictedHomeScore,
            PredictedAwayScore = prediction.PredictedAwayScore,
            ResultProbabilityMatrixJson = JsonConvert.SerializeObject(prediction.ScoreProbabilities)
        };

        if (matchesById.TryGetValue(match.Id, out var entity))
        {
            dto.Match = _mapper.Map<MatchDTO>(entity);
        }

        if (!match.IsPlayed)
        {
            return dto;
        }

        var evaluation = PredictionMetrics.Evaluate(prediction, match.HomeScore!.Value, match.AwayScore!.Value);

        dto.ActualHomeScore = match.HomeScore.Value;
        dto.ActualAwayScore = match.AwayScore.Value;
        dto.Brier = evaluation.Brier;
        dto.RankedProbabilityScore = evaluation.RankedProbability;
        dto.LogLoss = evaluation.LogLoss;
        dto.OutcomeCorrect = evaluation.OutcomeCorrect;
        dto.ExactScoreCorrect = evaluation.ExactScoreCorrect;
        dto.ExactScoreInTopThree = evaluation.ExactScoreInTopThree;
        dto.ProbabilityOfActualScore = evaluation.ProbabilityOfActualScore;
        dto.ProbabilityOfActualOutcome = evaluation.ProbabilityOfActualOutcome;

        return dto;
    }

    private static EkstraSimResult<IEnumerable<ModelPredictionDTO>> Failure(string message) => new()
    {
        Success = false,
        Data = new List<ModelPredictionDTO>(),
        ErrorMessage = message
    };
}
