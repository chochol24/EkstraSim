using EkstraSim.Backend.Database.Services;
using EkstraSim.Shared.DTOs;
using EkstraSim.Shared.Results;
using FastEndpoints;

namespace EkstraSim.Backend.Endpoints.League.GET;

public class GetAllLeagues : EndpointWithoutRequest<EkstraSimResult<IEnumerable<LeagueDTO>>>
{
    private readonly ILeagueService _leagueService;

    public GetAllLeagues(ILeagueService leagueService)
    {
        _leagueService = leagueService;
    }
    public override void Configure()
    {
        Get("api/leagues");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var result = await _leagueService.GetAllLeagues();
        await SendAsync(result, result.Success ? 200 : 500, ct);
    }
}