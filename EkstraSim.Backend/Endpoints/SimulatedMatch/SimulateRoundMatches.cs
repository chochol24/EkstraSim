using EkstraSim.Backend.Database.Services;
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
		await _simulatingService.SimulateRoundEndpoint(request.LeagueId, request.SeasonId, 19, 100000);
	}
}

public class SimulateRoundRequest
{
	public int LeagueId {  get; set; }
	public int SeasonId { get; set; }
	//public int? Round {  get; set; }
	//public int? NumberOfSimulations { get; set; }
}