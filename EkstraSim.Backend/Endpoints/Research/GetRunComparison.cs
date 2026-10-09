using EkstraSim.Backend.Database.Services.Research;
using EkstraSim.Shared.DTOs;
using EkstraSim.Shared.Results;
using FastEndpoints;

namespace EkstraSim.Backend.Endpoints.Research;

public class GetRunComparisonRequest
{
    public int RunId { get; set; }
    public string? Metric { get; set; }
    public double? Tolerance { get; set; }
    public int? FromRound { get; set; }
    public int? ToRound { get; set; }
}

public class GetRunComparison : Endpoint<GetRunComparisonRequest, EkstraSimResult<ModelComparisonDTO>>
{
    private readonly ResearchRunService _researchRunService;

    public GetRunComparison(ResearchRunService researchRunService)
    {
        _researchRunService = researchRunService;
    }

    public override void Configure()
    {
        Get("api/research/runs/{RunId}/comparison");
        AllowAnonymous();
    }

    public override async Task HandleAsync(GetRunComparisonRequest request, CancellationToken ct)
    {
        var result = await _researchRunService.GetComparisonAsync(
            request.RunId, request.Metric, request.Tolerance, request.FromRound, request.ToRound);
        await SendAsync(result, result.Success ? 200 : 500, ct);
    }
}
