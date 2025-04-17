using EkstraSim.Shared.DTOs;
using EkstraSim.Shared.Requests;
using EkstraSim.Shared.Resources;
using MudBlazor;
using Shared;

namespace EkstraSim.Frontend.Components.Pages.Matches;

public partial class MatchesPage
{
    private List<int> _rounds = [];
    private List<MatchDTO> matches = [];
    private List<SeasonDTO> seasons = [];
    private SeasonDTO? selectedSeason;
    private bool isLoading = true;
    private Dictionary<int, bool> editStates = new Dictionary<int, bool>();
    private MudToggleIconButton? toggleButton;
    private int currentPickedRound = 0;
    public MatchesPage() 
    {
        for(int i = 1; i <= Constants.NumberOfRoundsEkstaklasa; i++)
        {
            _rounds.Add(i);
        }
    }
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
        if(result.Data != null && result.Success == true)
        {
            seasons = result.Data;
        }
        else
        {
            Snackbar.Add(result.ErrorMessage ?? SnackbarMessages.Error_Base, Severity.Error);
        }
    }

    private void SeasonChange(SeasonDTO season)
    {
        selectedSeason = season;
    }

    private async Task OpenRoundMatchesAsync(DataGridRowClickEventArgs<int> args)
    {
        var round = args.Item;

        if(selectedSeason is not null)
        {
            await LoadMatches(round);
            currentPickedRound = round;
        }
    }

    private bool IsEditing(MatchDTO match)
    {
        return editStates.TryGetValue(match.Id, out var isEditing) && isEditing;
    }

    private async Task ToggleEdit(MatchDTO match)
    {
        if (editStates.ContainsKey(match.Id))
        {
            editStates[match.Id] = !editStates[match.Id];
            if(!editStates[match.Id])
            {
                var result = await _matchService.UpdateMatchResultAsync(new UpdateMatchResultRequest(match.Id, match.HomeTeamScore.GetValueOrDefault(), match.AwayTeamScore.GetValueOrDefault()));
                if (result.Success)
                {
                    Snackbar.Add(SnackbarMessages.Match_Result_Updated, Severity.Success);
                }
                else
                {
                    Snackbar.Add(SnackbarMessages.Match_Result_Update_Failed, Severity.Error);
                }
            }
        }
        else
        {
            editStates[match.Id] = true;
        }
        
    }

    private async Task CancelEdit(MatchDTO match)
    {
        if (editStates.ContainsKey(match.Id))
        {
            editStates[match.Id] = false;
            toggleButton.Toggled = false;
            await LoadMatches(currentPickedRound);
        }
    }

    private async Task LoadMatches(int round)
    {
        if(selectedSeason is not null)
        {
            matches.Clear();
            var result = await _matchService.GetRoundMatchesAsync(new GetMatchesByRoundRequest(selectedSeason.LeagueId, selectedSeason.Id, round));
            if (result.Success && result.Data is not null)
            {
                matches = result.Data.ToList();
            }
        }
    }

    private async Task UpdateAverageLeagueGoals()
    {
        var result = await _dataBaseService.UpdateAverageLeagueGoalsAsync(new AverageLeagueGoalsUpdateRequest(1));
        if (result.Success)
        {
            Snackbar.Add(SnackbarMessages.League_Averages_Updated, Severity.Success);
        }
        else
        {
            Snackbar.Add(SnackbarMessages.League_Averages_Update_Failed, Severity.Error);
        }

    }

    private async Task UpdateAverageTeamsGoals()
    {
        var result = await _dataBaseService.UpdateAverageTeamsGoalsAsync();
        if (result.Success)
        {
            Snackbar.Add(SnackbarMessages.Team_Averages_Updated, Severity.Success);
        }
        else
        {
            Snackbar.Add(SnackbarMessages.Team_Averages_Update_Failed, Severity.Error);
        }
    }
}
