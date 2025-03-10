using EkstraSim.Shared.DTOs;
using EkstraSim.Shared.Requests;
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
        var result = await _seasonService.GetSeasonsAsync();
        seasons = result.ToList();
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
                await _matchService.UpdateMatchResult(new Shared.Requests.UpdateMatchResultRequest(match.Id, match.HomeTeamScore.GetValueOrDefault(), match.AwayTeamScore.GetValueOrDefault()));
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
        matches.Clear();
        var result = await _matchService.GetRoundMatchesAsync(new Shared.Requests.GetMatchesByRoundRequest(selectedSeason.LeagueId, selectedSeason.Id, round));
        matches = result.ToList();
    }

    private async Task UpdateAverageLeagueGoals()
    {
        await _dataBaseService.UpdateAverageLeagueGoals(new AverageLeagueGoalsUpdateRequest(1));
    }

    private async Task UpdateAverageTeamsGoals()
    {
        await _dataBaseService.UpdateAverageTeamsGoals();
    }
}
