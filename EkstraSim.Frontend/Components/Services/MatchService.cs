using EkstraSim.Shared.DTOs;
using EkstraSim.Shared.Requests;
using EkstraSim.Shared.Results;

namespace EkstraSim.Frontend.Components.Services;

public class MatchService
{
    private readonly HttpClient _httpClient;

    public MatchService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<EkstraSimResult<IEnumerable<MatchDTO>>> GetRoundMatchesAsync(GetMatchesByRoundRequest req)
    {
        try
        {
            var matches = await _httpClient.GetFromJsonAsync<IEnumerable<MatchDTO>>($"/v1/api/matches/{req.LeagueId}/{req.SeasonId}/{req.Round}");
            return new EkstraSimResult<IEnumerable<MatchDTO>>
            {
                Success = true,
                Data = matches ?? new List<MatchDTO>()
            };
        }
        catch (Exception ex)
        {
            return new EkstraSimResult<IEnumerable<MatchDTO>>
            {
                Success = false,
                ErrorMessage = ex.Message,
                Data = new List<MatchDTO>()
            };
        }
    }

    public async Task<EkstraSimResult<bool>> UpdateMatchResultAsync(UpdateMatchResultRequest req)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync("/v1/api/matches/update-result", req);
            if (response.IsSuccessStatusCode)
            {
                return new EkstraSimResult<bool>
                {
                    Success = true,
                    Data = true
                };
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                return new EkstraSimResult<bool>
                {
                    Success = false,
                    Data = false,
                    ErrorMessage = error
                };
            }
        }
        catch (Exception ex)
        {
            return new EkstraSimResult<bool>
            {
                Success = false,
                Data = false,
                ErrorMessage = ex.Message
            };
        }
    }
}