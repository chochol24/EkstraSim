using EkstraSim.Backend.Database.Services;
using FastEndpoints;

namespace EkstraSim.Backend.Endpoints.SimulatedMatch;

public class SimulateLeagueTable : Endpoint<SimulateLeagueTableRequest>
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

	public override async Task HandleAsync(SimulateLeagueTableRequest request, CancellationToken ct)
	{
		await _simulatingService.SimulateRestOfTheSeason(request.LeagueId, request.SeasonId, request.Round.GetValueOrDefault(), request.NumberOfSimulations.GetValueOrDefault());
	}
}

public class SimulateLeagueTableRequest
{
	public int LeagueId { get; set; }
	public int SeasonId { get; set; }
	public int? Round {  get; set; }
	public int? NumberOfSimulations { get; set; }
}