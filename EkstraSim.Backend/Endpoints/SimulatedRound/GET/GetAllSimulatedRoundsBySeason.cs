using EkstraSim.Backend.Database.Services;
using EkstraSim.Shared.DTOs;
using EkstraSim.Shared.Requests;
using EkstraSim.Shared.Results;
using FastEndpoints;

namespace EkstraSim.Backend.Endpoints.SimulatedRound.GET;

public class GetAllSimulatedRoundsBySeason : Endpoint<SeasonAndLeagueRequest , EkstraSimResult<IEnumerable<SimulatedRoundDTO>>>
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
        await SendAsync(result, result.Success ? 200 : 500, ct);
    }
}
