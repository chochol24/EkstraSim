using EkstraSim.Backend.Database.Services;
using EkstraSim.Shared.DTOs;
using EkstraSim.Shared.Requests;
using EkstraSim.Shared.Results;
using FastEndpoints;

namespace EkstraSim.Backend.Endpoints.SimulatedRound.GET;

public class GetSimulatedRoundById : Endpoint<GetSimulatedRoundRequest, EkstraSimResult<SimulatedRoundDTO>>
{
    private readonly SimulatedRoundService _roundService;

    public GetSimulatedRoundById(SimulatedRoundService roundService)
    {
        _roundService = roundService;
    }
    public override void Configure()
    {
        Get("api/simulated-round/{simulatedRoundId}");
        AllowAnonymous();
    }

    public override async Task HandleAsync(GetSimulatedRoundRequest request,CancellationToken ct)
    {
        var result = await _roundService.GetSimulatedRoundByIdAsync(request.SimulatedRoundId);
        await SendAsync(result, result.Success ? 200 : 500, ct);
    }
}