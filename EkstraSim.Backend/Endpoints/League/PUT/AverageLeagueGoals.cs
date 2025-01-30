using EkstraSim.Backend.Database.Services;
using EkstraSim.Shared.Requests;
using FastEndpoints;

namespace EkstraSim.Backend.Endpoints.League.PUT;

public class AverageLeagueGoals : Endpoint<AverageLeagueGoalsUpdateRequest>
{
	private readonly ILeagueService _leagueService;

	public AverageLeagueGoals(ILeagueService leagueService)
	{
		_leagueService = leagueService;
	}

	public override void Configure()
	{
		Put("api/league/goals");
		AllowAnonymous();
	}

	public override async Task HandleAsync(AverageLeagueGoalsUpdateRequest request, CancellationToken ct)
	{
		await _leagueService.UpdateAverageLeagueGoals(request.LeagueId);
	}
}

