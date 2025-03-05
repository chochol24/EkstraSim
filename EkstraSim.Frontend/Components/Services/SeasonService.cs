using EkstraSim.Shared.DTOs;

namespace EkstraSim.Frontend.Components.Services;

public class SeasonService
{
    private readonly HttpClient _httpClient;

    public SeasonService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IEnumerable<SeasonDTO>> GetSeasonsAsync()
    {
        var teams = await _httpClient.GetFromJsonAsync<IEnumerable<SeasonDTO>>("/v1/api/seasons");
        return teams ?? new List<SeasonDTO>();
    }

}