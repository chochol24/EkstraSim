using EkstraSim.Shared.DTOs;

namespace EkstraSim.Frontend.Components.Services;

public class SimulationService
{
    private readonly HttpClient _httpClient;

    public SimulationService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress ??= new Uri("https://localhost:7050/");
    }

    public async Task<IEnumerable<SimulatedRoundDTO>> GetSimulatedRounds()
    {
        var rounds = await _httpClient.GetFromJsonAsync<IEnumerable<SimulatedRoundDTO>>("/v1/api/simulated-rounds");
        return rounds ?? null;
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
}
