using EkstraSim.Backend.Database.Services.Research;
using EkstraSim.Shared.DTOs;
using EkstraSim.Shared.Requests;
using EkstraSim.Shared.Results;
using FastEndpoints;

namespace EkstraSim.Backend.Endpoints.Research;

public class GetEvaluationRunById : Endpoint<ResearchRunRequest, EkstraSimResult<ModelEvaluationRunDTO>>
{
    private readonly ResearchRunService _researchRunService;

    public GetEvaluationRunById(ResearchRunService researchRunService)
    {
        _researchRunService = researchRunService;
    }

    public override void Configure()
    {
        Get("api/research/runs/{RunId}");
        AllowAnonymous();
    }

    public override async Task HandleAsync(ResearchRunRequest request, CancellationToken ct)
    {
        var result = await _researchRunService.GetRunAsync(request.RunId);
        await SendAsync(result, result.Success ? 200 : 500, ct);
    }
}
