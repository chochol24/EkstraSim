using EkstraSim.Shared.DTOs;

namespace EkstraSim.Frontend.Components.Services;

public class TeamService
{
	private readonly HttpClient _httpClient;

	public TeamService(HttpClient httpClient)
	{
		_httpClient = httpClient;
		_httpClient.BaseAddress ??= new Uri("https://localhost:7050/");
	}

	public async Task<IEnumerable<TeamDTO>> GetTeamsAsync()
	{
		var teams = await _httpClient.GetFromJsonAsync<IEnumerable<TeamDTO>>("/v1/api/teams");
		return teams ?? new List<TeamDTO>();
	}

}