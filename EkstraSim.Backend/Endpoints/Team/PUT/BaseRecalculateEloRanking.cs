using EkstraSim.Backend.Database.Services;
using FastEndpoints;

namespace EkstraSim.Backend.Endpoints.Team.PUT;

public class BaseRecalculateEloRanking : EndpointWithoutRequest
{
	private readonly ITeamService _teamService;

	public BaseRecalculateEloRanking(ITeamService teamService)
	{
		_teamService = teamService;
	}

	public override void Configure()
	{
		Put("api/team/rebase-elo");
		AllowAnonymous();
	}

	public override async Task HandleAsync(CancellationToken ct)
	{
		await _teamService.BaseRecalculateEloRankingAllTeamsAsync();
	}
}
