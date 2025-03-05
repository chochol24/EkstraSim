using EkstraSim.Shared.Requests;

namespace EkstraSim.Frontend.Components.Services;

public class UpdateDatabaseService
{
    private readonly HttpClient _httpClient;
    private readonly string _prefix = "v1/api";

    public UpdateDatabaseService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task UpdateAverageLeagueGoals(AverageLeagueGoalsUpdateRequest request)
    {
        await _httpClient.PutAsJsonAsync($"{_prefix}/league/goals", request);
    }

    public async Task UpdateAverageTeamsGoals()
    {
        await _httpClient.PutAsync($"{_prefix}/team/goals", null);
    }
}

