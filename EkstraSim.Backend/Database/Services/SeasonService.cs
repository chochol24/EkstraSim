using AutoMapper;
using EkstraSim.Shared.DTOs;
using EkstraSim.Shared.Resources;
using EkstraSim.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace EkstraSim.Backend.Database.Services;

public class SeasonService
{
    private readonly EkstraSimDbContext _context;
    private readonly IMapper _mapper;
    public SeasonService(EkstraSimDbContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    public async Task<EkstraSimResult<IEnumerable<SeasonDTO>>> GetSeasonsAsync()
    {
        try
        {
            var seasons = await _context.Seasons
                .Include(x => x.League)
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
