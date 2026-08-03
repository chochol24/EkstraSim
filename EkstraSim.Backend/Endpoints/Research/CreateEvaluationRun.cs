using EkstraSim.Backend.Database.Services.Research;
using EkstraSim.Shared.DTOs;
using EkstraSim.Shared.Requests;
using EkstraSim.Shared.Results;
using FastEndpoints;

namespace EkstraSim.Backend.Endpoints.Research;

public class CreateEvaluationRun : Endpoint<CreateEvaluationRunRequest, EkstraSimResult<ModelEvaluationRunDTO>>
{
    private readonly ResearchRunService _researchRunService;

    public CreateEvaluationRun(ResearchRunService researchRunService)
    {
        _researchRunService = researchRunService;
    }

    public override void Configure()
    {
        Post("api/research/runs");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CreateEvaluationRunRequest request, CancellationToken ct)
    {
        var result = await _researchRunService.CreateAsync(request);
        await SendAsync(result, result.Success ? 200 : 500, ct);
    }
}
