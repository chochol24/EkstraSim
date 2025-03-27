using EkstraSim.Shared.DTOs;
using EkstraSim.Shared.Results;

namespace EkstraSim.Frontend.Components.Services;

public class TeamService
{
    private readonly HttpServiceHelper _httpHelper;

    public TeamService(HttpServiceHelper httpHelper)
    {
        _httpHelper = httpHelper;
    }

    public async Task<EkstraSimResult<List<TeamDTO>>> GetTeamsAsync()
    {
        var result = await _httpHelper.SendGetAsync<List<TeamDTO>>("/v1/api/teams");
        return result;
    }
}