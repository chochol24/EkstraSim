using EkstraSim.Shared.DTOs;
using EkstraSim.Shared.Requests;
using EkstraSim.Shared.Results;

namespace EkstraSim.Frontend.Components.Services;

public class SimulationService
{
    private readonly HttpServiceHelper _httpHelper;

    public SimulationService(HttpServiceHelper httpHelper)
    {
        _httpHelper = httpHelper;
    }

    public async Task<EkstraSimResult<List<SimulatedRoundDTO>>> GetSimulatedRoundsBySeason(SeasonAndLeagueRequest request)
    {
        return await _httpHelper.SendGetAsync<List<SimulatedRoundDTO>>($"/v1/api/simulated-rounds/{request.SeasonId}/{request.LeagueId}");
    }

    public async Task<EkstraSimResult<List<SimulatedFinalLeagueDTO>>> GetAllSimulationsOfSeason(SeasonAndLeagueRequest request)
    {
        return await _httpHelper.SendGetAsync<List<SimulatedFinalLeagueDTO>>($"/v1/api/simulated-season/{request.SeasonId}/{request.LeagueId}");
    }

    public async Task<EkstraSimResult<SimulatedRoundDTO>> GetSimulatedRoundResults(int simulatedRoundId)
    {
        return await _httpHelper.SendGetAsync<SimulatedRoundDTO>($"v1/api/simulated-round/{simulatedRoundId}");
    }

    public async Task<EkstraSimResult<SimulatedMatchResultDTO>> GetSimulatedMatch(int simulatedMatchId)
    {
        return await _httpHelper.SendGetAsync<SimulatedMatchResultDTO>($"v1/api/simulated-match/{simulatedMatchId}");
    }

    public async Task<EkstraSimResult<bool>> SimulateRound(SimulateRoundRequest simulatedRoundRequest)
    {
        return await _httpHelper.SendPutAsync<bool>($"v1/api/simulate/round", simulatedRoundRequest);
    }

    public async Task<EkstraSimResult<bool>> SimulateTable(SimulateTableRequest simulateTableRequest)
    {
        return await _httpHelper.SendPutAsync<bool>($"v1/api/simulate/table", simulateTableRequest);
    }
}