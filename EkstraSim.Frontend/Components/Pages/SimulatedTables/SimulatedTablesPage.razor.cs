using EkstraSim.Shared.DTOs;
using MudBlazor;

namespace EkstraSim.Frontend.Components.Pages.SimulatedTables;

public partial class SimulatedTablesPage
{
    private List<SeasonDTO> seasons = [];
    private List<SimulatedFinalLeagueDTO> simulations = [];

    private SimulatedFinalLeagueDTO selectedSimulation;
    private SimulatedTeamInFinalTableDTO selectedTeam;
    private SeasonDTO selectedSeason;

    private bool isLoading = true;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            isLoading = true;
            StateHasChanged();

            await GetSeasonsAsync();

            isLoading = false;
            StateHasChanged();
        }
    }

    private async Task GetSimulationsOfSelectedSeasonAsync()
    {
        var result = await _simulationService.GetAllSimulationsOfSeason(new Shared.Requests.SeasonAndLeagueRequest(selectedSeason.Id, selectedSeason.LeagueId));
        simulations = result.ToList();
    }

    private async Task GetSeasonsAsync()
    {
        var result = await _seasonService.GetSeasonsAsync();
        seasons = result.ToList();
    }

    private void SelectTeam(DataGridRowClickEventArgs<SimulatedTeamInFinalTableDTO> args)
    {
        selectedTeam = args.Item;
    }

    private async Task SeasonChangeAsync(SeasonDTO season)
    {
        if(selectedSeason != season)
        {
            selectedSimulation = null;
            selectedTeam = null;
        }
        selectedSeason = season;
        await GetSimulationsOfSelectedSeasonAsync();
    }

    private void AssignPlaces(SimulatedFinalLeagueDTO sim)
    {
        selectedTeam = null;
        var assignedPlaces = new HashSet<int>();
        var remainingTeams = new HashSet<SimulatedTeamInFinalTableDTO>(sim.Teams);

        while (remainingTeams.Any())
        {
            var bestTeam = remainingTeams
                .Select(team => new
                {
                    Team = team,
                    BestPlace = team.PlacesDistribution
                        .Where(x => !assignedPlaces.Contains(x.Key))
                        .OrderByDescending(x => x.Value)
                        .FirstOrDefault()
                })
                .OrderByDescending(x => x.BestPlace.Value)
                .FirstOrDefault();

            if (bestTeam != null && bestTeam.BestPlace.Key > 0)
            {
                bestTeam.Team.Place = bestTeam.BestPlace.Key;
                assignedPlaces.Add(bestTeam.BestPlace.Key);
                remainingTeams.Remove(bestTeam.Team);
            }
            else
            {
                break;
            }
        }
        selectedSimulation = sim;
    }

    private string GetRowClass(SimulatedTeamInFinalTableDTO team, int index)
    {
        return team.Place switch
        {
            1 => "first-place",
            2 or 3 => "top-three",
            >= 16 and <= 18 => "relegation-zone",
            _ => ""
        };
    }
}
