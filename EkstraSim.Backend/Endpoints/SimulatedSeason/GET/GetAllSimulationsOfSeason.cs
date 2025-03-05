using EkstraSim.Backend.Database.Services;
using EkstraSim.Shared.DTOs;
using EkstraSim.Shared.Requests;
using FastEndpoints;

namespace EkstraSim.Backend.Endpoints.SimulatedSeason.GET;

public class GetAllSimulationsOfSeason : Endpoint<SeasonAndLeagueRequest ,IEnumerable<SimulatedFinalLeagueDTO>>
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
        if (result != null)
        {
            await SendAsync(result, cancellation: ct);
        }
        else
        {

            //obsluga bledu
        }
    }
}

