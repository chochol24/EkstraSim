using EkstraSim.Backend.Database.Services;
using EkstraSim.Shared.DTOs;
using FastEndpoints;

namespace EkstraSim.Backend.Endpoints.SimulatedRound.GET;

public class GetAllSimulatedRounds : EndpointWithoutRequest<IEnumerable<SimulatedRoundDTO>>
{
    private readonly SimulatedRoundService _roundService;

    public GetAllSimulatedRounds(SimulatedRoundService roundService)
    {
        _roundService = roundService;
    }
    public override void Configure()
    {
        Get("api/simulated-rounds");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var result = await _roundService.GetSimulatedRoundsAsync();
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
