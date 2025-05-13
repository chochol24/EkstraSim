using EkstraSim.Backend.Database.Services;
using EkstraSim.Shared.Requests;
using FastEndpoints;

namespace EkstraSim.Backend.Endpoints.SimulatedMatch;

public class SimulateLeagueTable : Endpoint<SimulateTableRequest>
{
	private readonly ISimulatingService _simulatingService;

	public SimulateLeagueTable(ISimulatingService simulatingService)
	{
		_simulatingService = simulatingService;
	}

	public override void Configure()
	{
		Put("api/simulate/table");
		AllowAnonymous();
	}

	public override async Task HandleAsync(SimulateTableRequest request, CancellationToken ct)
	{

		await _simulatingService.SimulateRestOfTheSeason(request.LeagueId, request.SeasonId, request.Round, request.NumberOfSimualtions, request.Comments);
	}
}
