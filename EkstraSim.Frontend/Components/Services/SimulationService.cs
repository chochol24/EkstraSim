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

    public async Task<IEnumerable<SimulatedRoundDTO>> GetSimulatedRoundResults()
    {
        var teams = await _httpClient.GetFromJsonAsync<IEnumerable<SimulatedRoundDTO>>("/v1/api/simulated-rounds");
        return teams ?? new List<SimulatedRoundDTO>();
    }

}
