using EkstraSim.Backend.Database.Services;
using EkstraSim.Shared.DTOs;
using FastEndpoints;

namespace EkstraSim.Backend.Endpoints;

public class GetTeamsEndpoint : EndpointWithoutRequest<IEnumerable<TeamDTO>>
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
		if (result != null)
		{
			await SendAsync(result, cancellation: ct);
		}
		else
		{
			
			//obsluga bledu
		}
	}
}
