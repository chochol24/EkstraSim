using EkstraSim.Shared.DTOs;
using EkstraSim.Shared.Requests;

namespace EkstraSim.Frontend.Components.Services;

public class SimulationService
{
    private readonly HttpClient _httpClient;

    public SimulationService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IEnumerable<SimulatedRoundDTO>> GetSimulatedRoundsBySeason(SeasonAndLeagueRequest request)
    {
        var rounds = await _httpClient.GetFromJsonAsync<IEnumerable<SimulatedRoundDTO>>($"/v1/api/simulated-rounds/{request.SeasonId}/{request.LeagueId}");
        return rounds ?? null;
    }

    public async Task<IEnumerable<SimulatedFinalLeagueDTO>> GetAllSimulationsOfSeason(SeasonAndLeagueRequest request)
    {
        var simulations = await _httpClient.GetFromJsonAsync<IEnumerable<SimulatedFinalLeagueDTO>>($"/v1/api/simulated-season/{request.SeasonId}/{request.LeagueId}");
        return simulations ?? null;
    }

    public async Task<SimulatedRoundDTO> GetSimulatedRoundResults(int simulatedRoundId)
    {
        var round = await _httpClient.GetFromJsonAsync<SimulatedRoundDTO>($"v1/api/simulated-round/{simulatedRoundId}");
        return round ?? null;
    }

    public async Task<SimulatedMatchResultDTO> GetSimulatedMatch(int simulatedMatchId)
    {
        var match = await _httpClient.GetFromJsonAsync<SimulatedMatchResultDTO>($"v1/api/simulated-match/{simulatedMatchId}");
        return match ?? null;
    }

    public async Task SimulateRound(SimulateRoundRequest simulatedRoundRequest)
    {
        await _httpClient.PutAsJsonAsync($"v1/api/simulate/round", simulatedRoundRequest);
    }
}

