using EkstraSim.Shared.DTOs;
using EkstraSim.Shared.Requests;
using EkstraSim.Shared.Results;

namespace EkstraSim.Frontend.Components.Services;

public class MatchService
{
    private readonly HttpServiceHelper _httpHelper;

    public MatchService(HttpServiceHelper httpHelper)
    {
        _httpHelper = httpHelper;
    }

    public async Task<EkstraSimResult<List<MatchDTO>>> GetRoundMatchesAsync(GetMatchesByRoundRequest req)
    {
        return await _httpHelper.SendGetListAsync<MatchDTO>($"/v1/api/matches/{req.LeagueId}/{req.SeasonId}/{req.Round}");
    }

    public async Task<EkstraSimResult<bool>> UpdateMatchResultAsync(UpdateMatchResultRequest req)
    {
        return await _httpHelper.SendPutAsync<bool>("/v1/api/matches/update-result", req);
    }
}