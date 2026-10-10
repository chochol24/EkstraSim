using EkstraSim.Prediction.Evaluation;
using EkstraSim.Shared.DTOs;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace EkstraSim.Backend.Database.Services.Research;

public class PromotedTeamsService
{
    private readonly IDbContextFactory<EkstraSimDbContext> _dbFactory;

    public PromotedTeamsService(IDbContextFactory<EkstraSimDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<List<PromotedTeamDTO>> GetPromotedTeamsAsync(int leagueId, int seasonId)
    {
        await using var context = await _dbFactory.CreateDbContextAsync();
        return await GetPromotedTeamsAsync(context, leagueId, seasonId);
    }

    public async Task<List<PromotedTeamDTO>> GetPromotedTeamsAsync(EkstraSimDbContext context, int leagueId, int seasonId)
    {
        var chronology = await SeasonStructureService.GetSeasonChronologyAsync(context, leagueId);
        var index = chronology.IndexOf(seasonId);

        if (index <= 0)
        {
            return [];
        }

        var previousSeasonId = chronology[index - 1];

        var currentTeamIds = await TeamIdsInSeasonAsync(context, leagueId, seasonId);
        var previousTeamIds = await TeamIdsInSeasonAsync(context, leagueId, previousSeasonId);

        var promotedIds = currentTeamIds.Except(previousTeamIds).ToList();

        var promoted = await context.Teams
            .Where(t => promotedIds.Contains(t.Id))
            .OrderBy(t => t.Name)
            .Select(t => new PromotedTeamDTO { TeamId = t.Id, Name = t.Name })
            .ToListAsync();

        return await CompleteHistoryAsync(context, leagueId, seasonId, promoted, chronology);
    }

    public async Task<List<PromotedTeamDTO>> CompleteHistoryAsync(
        EkstraSimDbContext context,
        int leagueId,
        int seasonId,
        IReadOnlyList<PromotedTeamDTO> promoted,
        IReadOnlyList<int>? chronology = null)
    {
        var missing = promoted.Where(team => !team.PriorMatchCount.HasValue).ToList();
        var computed = missing.Count > 0
            ? (await ComputeHistoryAsync(context, leagueId, seasonId, missing, chronology)).ToDictionary(team => team.TeamId)
            : new Dictionary<int, PromotedTeamDTO>();

        return promoted
            .Select(team => team.PriorMatchCount.HasValue ? team : computed[team.TeamId])
            .Select(team => new PromotedTeamDTO
            {
                TeamId = team.TeamId,
                Name = team.Name,
                PriorMatchCount = team.PriorMatchCount,
                LastPriorSeasonId = team.LastPriorSeasonId,
                LastPriorSeasonName = team.LastPriorSeasonName,
                Category = PromotedTeamHistory.CategoryOf(team.PriorMatchCount!.Value).ToString()
            })
            .ToList();
    }

    public static List<PromotedTeamDTO> ReadSnapshot(string? promotedTeamsJson)
    {
        if (string.IsNullOrWhiteSpace(promotedTeamsJson))
        {
            return [];
        }

        return (JsonConvert.DeserializeObject<List<PromotedTeamDTO>>(promotedTeamsJson) ?? [])
            .DistinctBy(team => team.TeamId)
            .ToList();
    }

    private static async Task<List<PromotedTeamDTO>> ComputeHistoryAsync(
        EkstraSimDbContext context,
        int leagueId,
        int seasonId,
        IReadOnlyList<PromotedTeamDTO> teams,
        IReadOnlyList<int>? chronology)
    {
        var teamIds = teams.Select(team => team.TeamId).ToList();
        chronology ??= await SeasonStructureService.GetSeasonChronologyAsync(context, leagueId);

        var matches = (await context.Matches
                .AsNoTracking()
                .Where(m => m.LeagueId == leagueId
                    && m.SeasonId != null
                    && (teamIds.Contains(m.HomeTeamId) || teamIds.Contains(m.AwayTeamId)))
                .ToListAsync())
            .ToMatchData();

        var seasonNames = await context.Seasons
            .AsNoTracking()
            .Where(s => s.LeagueId == leagueId)
            .ToDictionaryAsync(s => s.Id, s => s.Name);

        var histories = PromotedTeamHistory.Compute(matches, chronology, leagueId, seasonId, teamIds);

        return teams
            .Zip(histories, (team, history) => new PromotedTeamDTO
            {
                TeamId = team.TeamId,
                Name = team.Name,
                PriorMatchCount = history.PriorMatchCount,
                LastPriorSeasonId = history.LastPriorSeasonId,
                LastPriorSeasonName = history.LastPriorSeasonId.HasValue
                    ? seasonNames.GetValueOrDefault(history.LastPriorSeasonId.Value)
                    : null
            })
            .ToList();
    }

    private static async Task<HashSet<int>> TeamIdsInSeasonAsync(EkstraSimDbContext context, int leagueId, int seasonId)
    {
        var homeIds = await context.Matches
            .Where(m => m.SeasonId == seasonId && m.LeagueId == leagueId)
            .Select(m => m.HomeTeamId)
            .Distinct()
            .ToListAsync();

        var awayIds = await context.Matches
            .Where(m => m.SeasonId == seasonId && m.LeagueId == leagueId)
            .Select(m => m.AwayTeamId)
            .Distinct()
            .ToListAsync();

        return homeIds.Concat(awayIds).ToHashSet();
    }
}
