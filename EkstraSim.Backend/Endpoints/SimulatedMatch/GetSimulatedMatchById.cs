using EkstraSim.Backend.Database.Services;
using EkstraSim.Shared.DTOs;
using EkstraSim.Shared.Requests;
using FastEndpoints;

namespace EkstraSim.Backend.Endpoints.SimulatedMatch;


public class GetSimulatedMatchById : Endpoint<GetSimulatedMatchResultsRequest, SimulatedMatchResultDTO>
{
    private readonly SimulatedMatchService _matchService;

    public GetSimulatedMatchById(SimulatedMatchService matchService)
    {
        _matchService = matchService;
    }
    public override void Configure()
    {
        Get("api/simulated-match/{SimulatedMatchId}");
        AllowAnonymous();
    }

    public override async Task HandleAsync(GetSimulatedMatchResultsRequest request, CancellationToken ct)
    {
        var result = await _matchService.GetSimulatedMatchByIdAsync(request.SimulatedMatchId);
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