using EkstraSim.Shared.DTOs;
using EkstraSim.Shared.Requests;
using EkstraSim.Shared.Resources;
using MudBlazor;
using Shared;

namespace EkstraSim.Frontend.Components.Pages.Matches;

public partial class MatchesPage
{
    private List<int> _rounds = new();
    private List<MatchDTO> matches = new();
    private List<SeasonDTO> seasons = new();
    private SeasonDTO? selectedSeason;
    private bool isLoading = true;
    private bool isEditingMode = false;
    private int currentPickedRound = 0;
    private Dictionary<int, (int? Home, int? Away)> originalScores = new();
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
        await GetSeasonsAsync();
        isLoading = false;
    }

    private async Task GetSeasonsAsync()
    {
        var result = await _seasonService.GetSeasonsAsync(new SeasonRequest(1));
        if(result.Data != null && result.Success == true)
        {
            seasons = result.Data.OrderByDescending(x => x.Name).ToList();
        }
        else
        {
            Snackbar.Add(result.ErrorMessage ?? SnackbarMessages.Error_Base, Severity.Error);
        }
    }

    private void SeasonChange(SeasonDTO season)
    {
        selectedSeason = season;
        matches.Clear();
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
    private async Task LoadMatches(int round)
    {
        matches.Clear();
        var result = await _matchService.GetRoundMatchesAsync(new GetMatchesByRoundRequest(selectedSeason!.LeagueId, selectedSeason.Id, round));
        if (result.Success && result.Data != null)
        {
            matches = result.Data.ToList();
            originalScores = matches.ToDictionary(m => m.Id, m => (m.HomeTeamScore, m.AwayTeamScore));
            isEditingMode = false;
        }
        else
        {
            Snackbar.Add(result.ErrorMessage ?? "Błąd podczas pobierania meczów.", Severity.Error);
        }
    }

    private void ToggleEditAll()
    {
        isEditingMode = !isEditingMode;
        if (!isEditingMode)
        {
            foreach (var m in matches)
            {
                var orig = originalScores[m.Id];
                m.HomeTeamScore = orig.Home;
                m.AwayTeamScore = orig.Away;
            }
        }
    }

    private async Task SaveAllChanges()
    {
        var updates = matches
            .Where(m => originalScores.ContainsKey(m.Id) && (originalScores[m.Id].Home != m.HomeTeamScore || originalScores[m.Id].Away != m.AwayTeamScore))
            .ToList();

        foreach (var match in updates)
        {
            var result = await _matchService
                .UpdateMatchResultAsync(new UpdateMatchResultRequest(match.Id, match.HomeTeamScore.GetValueOrDefault(), match.AwayTeamScore.GetValueOrDefault()));
            if (!result.Success)
            {
                Snackbar.Add($"Nie udało się zaktualizować meczu {match.Id}.", Severity.Error);
            }
        }
        if (updates.Any())
        {
            Snackbar.Add("Zapisano wszystkie zmiany.", Severity.Success);
        }
        isEditingMode = false;
        foreach (var m in matches)
        {
            originalScores[m.Id] = (m.HomeTeamScore, m.AwayTeamScore);
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

        var result2 = await _dataBaseService.UpdateAverageTeamsGoalsAsync();
        if (result2.Success)
        {
            Snackbar.Add(SnackbarMessages.Team_Averages_Updated, Severity.Success);
        }
        else
        {
            Snackbar.Add(SnackbarMessages.Team_Averages_Update_Failed, Severity.Error);
        }
    }


}
