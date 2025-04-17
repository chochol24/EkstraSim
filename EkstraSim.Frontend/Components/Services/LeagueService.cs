using EkstraSim.Shared.DTOs;
using EkstraSim.Shared.Results;

namespace EkstraSim.Frontend.Components.Services;

public class LeagueService
{
    private readonly HttpServiceHelper _httpHelper;

    public LeagueService(HttpServiceHelper httpHelper)
    {
        _httpHelper = httpHelper;
    }

    public async Task<EkstraSimResult<List<LeagueDTO>>> GetAllLeaguesAsync()
    {
        return await _httpHelper.SendGetAsync<List<LeagueDTO>>($"/v1/api/leagues");
    }

}