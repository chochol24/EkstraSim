using EkstraSim.Backend.Database.Services;
using EkstraSim.Shared.DTOs;
using EkstraSim.Shared.Requests;
using EkstraSim.Shared.Results;
using FastEndpoints;

namespace EkstraSim.Backend.Endpoints.SimulatedMatch;


public class GetSimulatedMatchById : Endpoint<GetSimulatedMatchResultsRequest, EkstraSimResult<SimulatedMatchResultDTO>>
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
        await SendAsync(result, result.Success ? 200 : 500, ct);
    }
}