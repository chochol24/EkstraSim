using EkstraSim.Backend.Database.Services;
using EkstraSim.Shared.DTOs;
using EkstraSim.Shared.Requests;
using EkstraSim.Shared.Results;
using FastEndpoints;

namespace EkstraSim.Backend.Endpoints.Season.GET;

public class GetSeasons : Endpoint<SeasonRequest, EkstraSimResult<IEnumerable<SeasonDTO>>>
{
    private readonly SeasonService _seasonService;

    public GetSeasons(SeasonService seasonService)
    {
        _seasonService = seasonService;
    }
    public override void Configure()
    {
        Get("api/seasons/{LeagueId}");
        AllowAnonymous();
    }

    public override async Task HandleAsync(SeasonRequest request, CancellationToken ct)
    {
        var result = await _seasonService.GetSeasonsByLeagueIdAsync(request.LeagueId);
        await SendAsync(result, result.Success ? 200 : 500, ct);
    }
}
