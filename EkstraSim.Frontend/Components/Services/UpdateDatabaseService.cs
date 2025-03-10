using EkstraSim.Shared.Requests;
using EkstraSim.Shared.Results;

namespace EkstraSim.Frontend.Components.Services;

public class UpdateDatabaseService
{
    private readonly HttpServiceHelper _helper;
    private readonly string _prefix = "v1/api";

    public UpdateDatabaseService(HttpClient httpClient)
    {
        _helper = new HttpServiceHelper(httpClient);
    }

    public async Task<EkstraSimResult<bool>> UpdateAverageLeagueGoalsAsync(AverageLeagueGoalsUpdateRequest request)
    {
        return await _helper.SendPutRequestAsync<bool>($"{_prefix}/league/goals", request);
    }
        
    public async Task<EkstraSimResult<bool>> UpdateAverageTeamsGoalsAsync()
    {
        return await _helper.SendPutRequestAsync<bool>($"{_prefix}/team/goals");
    }
}