using EkstraSim.Shared.DTOs;
using Microsoft.EntityFrameworkCore;

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

        return await context.Teams
            .Where(t => promotedIds.Contains(t.Id))
            .OrderBy(t => t.Name)
            .Select(t => new PromotedTeamDTO { TeamId = t.Id, Name = t.Name })
            .ToListAsync();
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
