using EkstraSim.Backend.Database.Services;
using EkstraSim.Shared.DTOs;
using EkstraSim.Shared.Requests;
using EkstraSim.Shared.Results;
using FastEndpoints;

namespace EkstraSim.Backend.Endpoints.SimulatedSeason.GET;

public class GetAllSimulationsOfSeason : Endpoint<SeasonAndLeagueRequest , EkstraSimResult<IEnumerable<SimulatedFinalLeagueDTO>>>
{
    private readonly SimulatedSeasonService _seasonService;

    public GetAllSimulationsOfSeason(SimulatedSeasonService seasonService)
    {
        _seasonService = seasonService;
    }
    public override void Configure()
    {
        Get("api/simulated-season/{SeasonId}/{LeagueId}");
        AllowAnonymous();
    }

    public override async Task HandleAsync(SeasonAndLeagueRequest request, CancellationToken ct)
    {
        var result = await _seasonService.GetAllSimulationsOfSeason(request);
        await SendAsync(result, result.Success ? 200 : 500, ct);
    }
}

