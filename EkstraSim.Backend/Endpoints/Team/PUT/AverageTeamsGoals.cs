using EkstraSim.Backend.Database.Services;
using FastEndpoints;

namespace EkstraSim.Backend.Endpoints.Team.PUT;

public class AverageTeamsGoals : EndpointWithoutRequest
{
	private readonly ITeamService _teamService;

	public AverageTeamsGoals(ITeamService teamService)
	{
		_teamService = teamService;
	}

	public override void Configure()
	{
		Put("api/team/goals");
		AllowAnonymous();
	}

	public override async Task HandleAsync(CancellationToken ct)
	{
		await _teamService.UpdateAverageTeamGoals();
	}
}