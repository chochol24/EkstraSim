using EkstraSim.Shared.DTOs;
using EkstraSim.Shared.Requests;
using EkstraSim.Shared.Resources;
using MudBlazor;

namespace EkstraSim.Frontend.Components.Pages.Simulations;

public partial class RoundsSimulationGrid
{
    private List<SimulatedRoundDTO> simulatedRounds = [];
    private List<SeasonDTO> seasons = [];
    private SeasonDTO selectedSeason;
    private bool isLoading = true;
    private bool isSubmitting = false;

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
    private async Task SeasonChangeAsync(SeasonDTO season)
    {
        selectedSeason = season;

        var result = await _simulationService.GetSimulatedRoundsBySeason(new SeasonAndLeagueRequest(season.Id, season.LeagueId));
        if (result.Data != null && result.Success == true)
        {
            simulatedRounds = result.Data;
        }
        else
        {
            Snackbar.Add(result.ErrorMessage ?? SnackbarMessages.Error_Base, Severity.Error);
            simulatedRounds.Clear();
        }
    }

    private void OpenSimulationDetails(DataGridRowClickEventArgs<SimulatedRoundDTO> args)
    {
        var x = args.Item;
        _navigationManager.NavigateTo($"/simulated-round/{args.Item.Id}");
        StateHasChanged();
    }

    private async Task OnSubmitForm()
    {
        isSubmitting = true;
    }

    private async Task AfterSubmitForm()
    {
        isSubmitting = false;
        selectedSeason = null;
        await GetSeasonsAsync();
        StateHasChanged();
    }
}
