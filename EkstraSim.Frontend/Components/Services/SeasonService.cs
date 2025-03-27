using EkstraSim.Shared.DTOs;
using EkstraSim.Shared.Results;

namespace EkstraSim.Frontend.Components.Services;

public class SeasonService
{
    private readonly HttpServiceHelper _httpHelper;

    public SeasonService(HttpServiceHelper httpHelper)
    {
        _httpHelper = httpHelper;
    }

    public async Task<EkstraSimResult<List<SeasonDTO>>> GetSeasonsAsync()
    {
        return await _httpHelper.SendGetAsync<List<SeasonDTO>>("/v1/api/seasons");
    }
}