using EkstraSim.Backend.Database.Services.Research;
using EkstraSim.Shared.DTOs;
using EkstraSim.Shared.Results;
using FastEndpoints;

namespace EkstraSim.Backend.Endpoints.Research;

public class GetEvaluationRunsRequest
{
    public int? LeagueId { get; set; }
    public int? SeasonId { get; set; }
}

public class GetEvaluationRuns : Endpoint<GetEvaluationRunsRequest, EkstraSimResult<IEnumerable<ModelEvaluationRunDTO>>>
{
    private readonly ResearchRunService _researchRunService;

    public GetEvaluationRuns(ResearchRunService researchRunService)
    {
        _researchRunService = researchRunService;
    }

    public override void Configure()
    {
        Get("api/research/runs");
        AllowAnonymous();
    }

    public override async Task HandleAsync(GetEvaluationRunsRequest request, CancellationToken ct)
    {
        var result = await _researchRunService.GetRunsAsync(request.LeagueId, request.SeasonId);
        await SendAsync(result, result.Success ? 200 : 500, ct);
    }
}
