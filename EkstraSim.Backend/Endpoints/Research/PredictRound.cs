using EkstraSim.Backend.Database.Services.Research;
using EkstraSim.Shared.DTOs;
using EkstraSim.Shared.Requests;
using EkstraSim.Shared.Results;
using FastEndpoints;

namespace EkstraSim.Backend.Endpoints.Research;

public class PredictRound : Endpoint<PredictRoundRequest, EkstraSimResult<IEnumerable<ModelPredictionDTO>>>
{
    private readonly RoundPredictionService _roundPredictionService;

    public PredictRound(RoundPredictionService roundPredictionService)
    {
        _roundPredictionService = roundPredictionService;
    }

    public override void Configure()
    {
        Put("api/research/predict-round");
        AllowAnonymous();
    }

    public override async Task HandleAsync(PredictRoundRequest request, CancellationToken ct)
    {
        var result = await _roundPredictionService.PredictRoundAsync(request);
        await SendAsync(result, result.Success ? 200 : 500, ct);
    }
}
