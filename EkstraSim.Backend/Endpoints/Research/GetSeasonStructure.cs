using EkstraSim.Backend.Database.Services.Research;
using EkstraSim.Shared.DTOs;
using EkstraSim.Shared.Requests;
using EkstraSim.Shared.Results;
using FastEndpoints;

namespace EkstraSim.Backend.Endpoints.Research;

public class GetSeasonStructure : Endpoint<SeasonAndLeagueRequest, EkstraSimResult<SeasonStructureDTO>>
{
    private readonly SeasonStructureService _seasonStructureService;

    public GetSeasonStructure(SeasonStructureService seasonStructureService)
    {
        _seasonStructureService = seasonStructureService;
    }

    public override void Configure()
    {
        Get("api/research/season-structure/{SeasonId}/{LeagueId}");
        AllowAnonymous();
    }

    public override async Task HandleAsync(SeasonAndLeagueRequest request, CancellationToken ct)
    {
        var result = await _seasonStructureService.GetStructureAsync(request.LeagueId, request.SeasonId);
        await SendAsync(result, result.Success ? 200 : 500, ct);
    }
}
