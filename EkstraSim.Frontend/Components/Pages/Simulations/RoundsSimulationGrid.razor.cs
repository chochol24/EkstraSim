using EkstraSim.Shared.DTOs;
using EkstraSim.Shared.Resources;
using MudBlazor;

namespace EkstraSim.Frontend.Components.Pages.Simulations;

public partial class RoundsSimulationGrid
{
    private List<SimulatedRoundDTO> simulatedRounds = [];
    private List<SeasonDTO> seasons = [];
    private SeasonDTO selectedSeason;
    private bool isLoading = true;

    protected override async Task OnInitializedAsync()
    {
        isLoading = true;
        StateHasChanged();

        await GetSeasonsAsync();

        isLoading = false;
        StateHasChanged();
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
            Snackbar.Add(SnackbarMessages.Seasons_Get_Failed, Severity.Error);
        }
    }
    private async Task SeasonChangeAsync(SeasonDTO season)
    {
        selectedSeason = season;

        var result = await _simulationService.GetSimulatedRoundsBySeason(new Shared.Requests.SeasonAndLeagueRequest(season.Id, season.LeagueId));
        if (result.Data != null && result.Success == true)
        {
            simulatedRounds = result.Data;
        }
        else
        {
            Snackbar.Add(SnackbarMessages.Simulations_Rounds_Get_Failed, Severity.Error);
        }
    }

    private void OpenSimulationDetails(DataGridRowClickEventArgs<SimulatedRoundDTO> args)
    {
        var x = args.Item;
        _navigationManager.NavigateTo($"/simulated-round/{args.Item.Id}");
        StateHasChanged();
    }
}
