using EkstraSim.Frontend.Components.Pages.Simulations;
using EkstraSim.Shared.DTOs;
using EkstraSim.Shared.Requests;
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
    private bool isSubmitting = false;

    private string formButtonText = "Pokaż formularz";
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
        var result = await _simulationService.GetAllSimulationsOfSeason(new SeasonAndLeagueRequest(selectedSeason.Id, selectedSeason.LeagueId));

        if(result.Data != null && result.Success == true)
        {
            simulations = result.Data.OrderByDescending(x => x.RoundBeforeSimulation).ToList();
        }
        else
        {
            Snackbar.Add(result.ErrorMessage ?? SnackbarMessages.Error_Base, Severity.Error);
        }
    }

    private async Task GetSeasonsAsync()
    {
        var result = await _seasonService.GetSeasonsAsync(new SeasonRequest(1));
        if (result.Data != null && result.Success == true)
        {
            seasons = result.Data.OrderByDescending(x => x.Name).ToList();
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

        if(isFormVisible && selectedSimulation == null)
        {
            formButtonText = "Ukryj formularz";
        }
        else if ((!isFormVisible && selectedSimulation == null) || (!isFormVisible && selectedSimulation != null))
        {
            formButtonText = "Pokaż formularz";
        }
        else if (isFormVisible && selectedSimulation != null)
        {
            formButtonText = "Pokaż szczegółowe dane drużyny";
        }
    }

    private async Task OnSubmitForm()
    {
        isSubmitting = true;
    }

    private async Task AfterSubmitForm()
    {
        isSubmitting = false;
        selectedSimulation = null;
        selectedTeam = null;
        selectedSeason = null;
        await GetSeasonsAsync();
        StateHasChanged();
    }
}
