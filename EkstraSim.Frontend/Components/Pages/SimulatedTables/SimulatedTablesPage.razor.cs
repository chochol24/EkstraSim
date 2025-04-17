using EkstraSim.Frontend.Components.Pages.Simulations;
using EkstraSim.Shared.DTOs;
using EkstraSim.Shared.Resources;
using MudBlazor;

namespace EkstraSim.Frontend.Components.Pages.SimulatedTables;

public partial class SimulatedTablesPage
{
    private List<SeasonDTO> seasons = [];
    private List<SimulatedFinalLeagueDTO> simulations = [];

    private SimulatedFinalLeagueDTO selectedSimulation;
    private SimulatedTeamInFinalTableDTO selectedTeam;
    private SeasonDTO selectedSeason;

    private SimulatedTableForm tableForm = new();

    private bool isLoading = true;
    private bool isFormVisible = false;
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

        if(result.Data != null && result.Success == true)
        {
            simulations = result.Data;
        }
        else
        {
            Snackbar.Add(result.ErrorMessage ?? SnackbarMessages.Error_Base, Severity.Error);
        }
    }

    private async Task GetSeasonsAsync()
    {
        var result = await _seasonService.GetSeasonsAsync();
        if (result.Data != null && result.Success == true)
        {
            seasons = result.Data;
        }
        else
        {
            Snackbar.Add(result.ErrorMessage ?? SnackbarMessages.Error_Base, Severity.Error);
        }
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


    private void SwitchFormVisible()
    {
        isFormVisible = !isFormVisible;
    }
}
