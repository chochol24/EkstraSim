using EkstraSim.Shared.DTOs;
using EkstraSim.Shared.Requests;
using EkstraSim.Shared.Results;

namespace EkstraSim.Frontend.Components.Services;

public class SeasonService
{
    private readonly HttpServiceHelper _httpHelper;

    public SeasonService(HttpServiceHelper httpHelper)
    {
        _httpHelper = httpHelper;
    }

    public async Task<EkstraSimResult<List<SeasonDTO>>> GetSeasonsAsync(SeasonRequest request)
    {
        return await _httpHelper.SendGetAsync<List<SeasonDTO>>($"/v1/api/seasons/{request.LeagueId}");
    }
}