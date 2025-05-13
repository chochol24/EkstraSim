using AutoMapper;
using EkstraSim.Shared.DTOs;
using EkstraSim.Shared.Resources;
using EkstraSim.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace EkstraSim.Backend.Database.Services;

public class SeasonService
{
    private readonly IDbContextFactory<EkstraSimDbContext> _dbFactory;
    private readonly IMapper _mapper;
    public SeasonService(IDbContextFactory<EkstraSimDbContext> dbFactory, IMapper mapper)
    {
        _dbFactory = dbFactory;
        _mapper = mapper;
    }

    public async Task<EkstraSimResult<IEnumerable<SeasonDTO>>> GetSeasonsByLeagueIdAsync(int leagueId)
    {
        try
        {
            await using var context = await _dbFactory.CreateDbContextAsync();
            var seasons = await context.Seasons
                .Include(x => x.League)
                .Where(x => x.LeagueId == leagueId)
                .ToListAsync();

            var result = seasons.Select(season => _mapper.Map<SeasonDTO>(season)).ToList();

            if (!result.Any())
            {
                return new EkstraSimResult<IEnumerable<SeasonDTO>>
                {
                    Success = false,
                    Data = new List<SeasonDTO>(),
                    ErrorMessage = SnackbarMessages.Error_Seasons_Null
                };
            }

            return new EkstraSimResult<IEnumerable<SeasonDTO>>
            {
                Success = true,
                Data = result
            };
        }
        catch (Exception ex)
        {
            return new EkstraSimResult<IEnumerable<SeasonDTO>>
            {
                Success = false,
                Data = new List<SeasonDTO>(),
                ErrorMessage = $"{SnackbarMessages.Error_Get}{ex.Message}"
            };
        }
    }

}
