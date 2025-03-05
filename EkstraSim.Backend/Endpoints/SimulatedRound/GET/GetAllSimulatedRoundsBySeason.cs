using EkstraSim.Backend.Database.Services;
using EkstraSim.Shared.DTOs;
using EkstraSim.Shared.Requests;
using FastEndpoints;

namespace EkstraSim.Backend.Endpoints.SimulatedRound.GET;

public class GetAllSimulatedRoundsBySeason : Endpoint<SeasonAndLeagueRequest ,IEnumerable<SimulatedRoundDTO>>
{
    private readonly SimulatedRoundService _roundService;

    public GetAllSimulatedRoundsBySeason(SimulatedRoundService roundService)
    {
        _roundService = roundService;
    }
    public override void Configure()
    {
        Get("api/simulated-rounds/{SeasonId}/{LeagueId}");
        AllowAnonymous();
    }

    public override async Task HandleAsync(SeasonAndLeagueRequest request, CancellationToken ct)
    {
        var result = await _roundService.GetSimulatedRoundsAsync(request.SeasonId, request.LeagueId);
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
