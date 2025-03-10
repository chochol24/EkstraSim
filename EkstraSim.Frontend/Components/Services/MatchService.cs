using EkstraSim.Shared.DTOs;
using EkstraSim.Shared.Requests;

namespace EkstraSim.Frontend.Components.Services;

public class MatchService
{
    private readonly HttpClient _httpClient;

    public MatchService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }
    public async Task<IEnumerable<MatchDTO>> GetRoundMatchesAsync(GetMatchesByRoundRequest req)
    {
        var teams = await _httpClient.GetFromJsonAsync<IEnumerable<MatchDTO>>($"/v1/api/matches/{req.LeagueId}/{req.SeasonId}/{req.Round}");
        return teams ?? new List<MatchDTO>();
    }

    public async Task UpdateMatchResult(UpdateMatchResultRequest req)
    {
        await _httpClient.PutAsJsonAsync($"/v1/api/matches/update-result", req);
    }
}