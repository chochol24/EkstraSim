using EkstraSim.Prediction.Evaluation;
using EkstraSim.Shared.DTOs;
using EkstraSim.Shared.Resources;
using EkstraSim.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace EkstraSim.Backend.Database.Services.Research;

public class SeasonStructureService
{
    private readonly IDbContextFactory<EkstraSimDbContext> _dbFactory;
    private readonly PromotedTeamsService _promotedTeams;

    public SeasonStructureService(IDbContextFactory<EkstraSimDbContext> dbFactory, PromotedTeamsService promotedTeams)
    {
        _dbFactory = dbFactory;
        _promotedTeams = promotedTeams;
    }

    public async Task<List<int>> GetSeasonChronologyAsync(int leagueId)
    {
        await using var context = await _dbFactory.CreateDbContextAsync();
        return await GetSeasonChronologyAsync(context, leagueId);
    }

    public static async Task<List<int>> GetSeasonChronologyAsync(EkstraSimDbContext context, int leagueId)
    {
        var seasons = await context.Seasons
            .Where(s => s.LeagueId == leagueId)
            .Select(s => new
            {
                s.Id,
                s.Name,
                FirstMatch = context.Matches
                    .Where(m => m.SeasonId == s.Id)
                    .Min(m => (DateTime?)m.Date)
            })
            .ToListAsync();

        return seasons
            .OrderBy(s => s.FirstMatch ?? DateTime.MaxValue)
            .ThenBy(s => s.Name, StringComparer.Ordinal)
            .Select(s => s.Id)
            .ToList();
    }

    public async Task<EkstraSimResult<SeasonStructureDTO>> GetStructureAsync(int leagueId, int seasonId)
    {
        try
        {
            await using var context = await _dbFactory.CreateDbContextAsync();

            var season = await context.Seasons.FirstOrDefaultAsync(s => s.Id == seasonId && s.LeagueId == leagueId);
            if (season == null)
            {
                return new EkstraSimResult<SeasonStructureDTO>
                {
                    Success = false,
                    ErrorMessage = SnackbarMessages.Error_Seasons_Null
                };
            }

            var matches = (await context.Matches
                    .Where(m => m.SeasonId == seasonId && m.LeagueId == leagueId)
                    .ToListAsync())
                .ToMatchData();

            var split = SeasonCalendar.DetectSplit(matches);
            var chronology = await GetSeasonChronologyAsync(context, leagueId);
            var index = chronology.IndexOf(seasonId);
            var previousSeasonId = index > 0 ? chronology[index - 1] : (int?)null;

            var promoted = await _promotedTeams.GetPromotedTeamsAsync(context, leagueId, seasonId);

            var structure = new SeasonStructureDTO
            {
                SeasonId = seasonId,
                SeasonName = season.Name,
                LeagueId = leagueId,
                TeamCount = SeasonCalendar.TeamIds(matches).Count,
                RoundCount = SeasonCalendar.RoundsInOrder(matches).DefaultIfEmpty(0).Max(),
                PlayedMatchCount = matches.Count(m => m.IsPlayed),
                UnplayedMatchCount = matches.Count(m => !m.IsPlayed),
                AutumnLastRound = split.AutumnLastRound,
                SpringFirstRound = split.SpringFirstRound,
                WinterBreakDays = split.BreakDays,
                PreviousSeasonId = previousSeasonId,
                PromotedTeams = promoted
            };

            return new EkstraSimResult<SeasonStructureDTO>
            {
                Success = true,
                Data = structure
            };
        }
        catch (Exception ex)
        {
            return new EkstraSimResult<SeasonStructureDTO>
            {
                Success = false,
                ErrorMessage = $"{SnackbarMessages.Error_Get}{ex.Message}"
            };
        }
    }
}
