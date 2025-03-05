using EkstraSim.Backend.Database.Services;
using EkstraSim.Shared.Requests;
using FastEndpoints;

namespace EkstraSim.Backend.Endpoints.SimulatedMatch;

public class SimulateRoundMatches : Endpoint<SimulateRoundRequest>
{
	private readonly ISimulatingService _simulatingService;

	public SimulateRoundMatches(ISimulatingService simulatingService)
	{
		_simulatingService = simulatingService;
	}

	public override void Configure()
	{
		Put("api/simulate/round");
		AllowAnonymous();
	}

	public override async Task HandleAsync(SimulateRoundRequest request,CancellationToken ct)
	{
		await _simulatingService.SimulateRoundEndpoint(request.LeagueId, request.SeasonId, request.Round, request.NumberOfSimualtions);
	}
}
