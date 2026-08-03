using EkstraSim.Backend.Database.Services.Research;
using EkstraSim.Shared.DTOs;
using EkstraSim.Shared.Results;
using FastEndpoints;

namespace EkstraSim.Backend.Endpoints.Research;

public class GetRunPredictionsRequest
{
    public int RunId { get; set; }
    public string? ModelName { get; set; }
    public int? Round { get; set; }
}

public class GetRunPredictions : Endpoint<GetRunPredictionsRequest, EkstraSimResult<IEnumerable<ModelPredictionDTO>>>
{
    private readonly ResearchRunService _researchRunService;

    public GetRunPredictions(ResearchRunService researchRunService)
    {
        _researchRunService = researchRunService;
    }

    public override void Configure()
    {
        Get("api/research/runs/{RunId}/predictions");
        AllowAnonymous();
    }

    public override async Task HandleAsync(GetRunPredictionsRequest request, CancellationToken ct)
    {
        var result = await _researchRunService.GetPredictionsAsync(request.RunId, request.ModelName, request.Round);
        await SendAsync(result, result.Success ? 200 : 500, ct);
    }
}
