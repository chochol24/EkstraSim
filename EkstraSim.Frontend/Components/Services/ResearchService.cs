using EkstraSim.Shared.DTOs;
using EkstraSim.Shared.Requests;
using EkstraSim.Shared.Results;

namespace EkstraSim.Frontend.Components.Services;

public class ResearchService
{
    private const string Prefix = "/v1/api/research";

    private readonly HttpServiceHelper _httpHelper;

    public ResearchService(HttpServiceHelper httpHelper)
    {
        _httpHelper = httpHelper;
    }

    public async Task<EkstraSimResult<List<string>>> GetModelsAsync()
    {
        return await _httpHelper.SendGetAsync<List<string>>($"{Prefix}/models");
    }

    public async Task<EkstraSimResult<SeasonStructureDTO>> GetSeasonStructureAsync(SeasonAndLeagueRequest request)
    {
        return await _httpHelper.SendGetAsync<SeasonStructureDTO>($"{Prefix}/season-structure/{request.SeasonId}/{request.LeagueId}");
    }

    public async Task<EkstraSimResult<List<ModelEvaluationRunDTO>>> GetRunsAsync(int? leagueId = null, int? seasonId = null)
    {
        var query = new List<string>();

        if (leagueId.HasValue)
        {
            query.Add($"leagueId={leagueId.Value}");
        }

        if (seasonId.HasValue)
        {
            query.Add($"seasonId={seasonId.Value}");
        }

        var suffix = query.Count > 0 ? $"?{string.Join("&", query)}" : string.Empty;

        return await _httpHelper.SendGetAsync<List<ModelEvaluationRunDTO>>($"{Prefix}/runs{suffix}");
    }

    public async Task<EkstraSimResult<ModelEvaluationRunDTO>> GetRunAsync(int runId)
    {
        return await _httpHelper.SendGetAsync<ModelEvaluationRunDTO>($"{Prefix}/runs/{runId}");
    }

    public async Task<EkstraSimResult<List<ModelRoundMetricDTO>>> GetRoundMetricsAsync(int runId)
    {
        return await _httpHelper.SendGetAsync<List<ModelRoundMetricDTO>>($"{Prefix}/runs/{runId}/round-metrics");
    }

    public async Task<EkstraSimResult<List<ModelPredictionDTO>>> GetPredictionsAsync(int runId, string? modelName = null, int? round = null)
    {
        var query = new List<string>();

        if (!string.IsNullOrWhiteSpace(modelName))
        {
            query.Add($"modelName={Uri.EscapeDataString(modelName)}");
        }

        if (round.HasValue)
        {
            query.Add($"round={round.Value}");
        }

        var suffix = query.Count > 0 ? $"?{string.Join("&", query)}" : string.Empty;

        return await _httpHelper.SendGetAsync<List<ModelPredictionDTO>>($"{Prefix}/runs/{runId}/predictions{suffix}");
    }

    public async Task<EkstraSimResult<ModelComparisonDTO>> GetComparisonAsync(int runId, string? metric = null)
    {
        var suffix = string.IsNullOrWhiteSpace(metric) ? string.Empty : $"?metric={Uri.EscapeDataString(metric)}";

        return await _httpHelper.SendGetAsync<ModelComparisonDTO>($"{Prefix}/runs/{runId}/comparison{suffix}");
    }

    public async Task<EkstraSimResult<ModelEvaluationRunDTO>> CreateRunAsync(CreateEvaluationRunRequest request)
    {
        return await _httpHelper.SendPostEnvelopeAsync<ModelEvaluationRunDTO>($"{Prefix}/runs", request);
    }

    public async Task<EkstraSimResult<List<ModelPredictionDTO>>> PredictRoundAsync(PredictRoundRequest request)
    {
        return await _httpHelper.SendPutEnvelopeAsync<List<ModelPredictionDTO>>($"{Prefix}/predict-round", request);
    }

    public async Task<EkstraSimResult<CsvImportResultDTO>> ImportCsvAsync(ImportSeasonCsvRequest request)
    {
        return await _httpHelper.SendPostEnvelopeAsync<CsvImportResultDTO>($"{Prefix}/import-csv", request);
    }
}
