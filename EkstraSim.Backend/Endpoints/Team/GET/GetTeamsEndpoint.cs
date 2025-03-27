using EkstraSim.Backend.Database.Services;
using EkstraSim.Shared.DTOs;
using EkstraSim.Shared.Results;
using FastEndpoints;

namespace EkstraSim.Backend.Endpoints;

public class GetTeamsEndpoint : EndpointWithoutRequest<EkstraSimResult<IEnumerable<TeamDTO>>>
{
	private readonly ITeamService _teamService;

	public GetTeamsEndpoint(ITeamService teamService)
	{
		_teamService = teamService;
	}
	public override void Configure()
	{
		Get("api/teams");
		AllowAnonymous();
	}

    public override async Task HandleAsync(CancellationToken ct)
    {
        var result = await _teamService.GetAllTeamsAsync();
        await SendAsync(result, result.Success ? 200 : 500, ct);
    }
}
