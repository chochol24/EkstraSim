using AutoMapper;
using EkstraSim.Shared.DTOs;
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

    public async Task<IEnumerable<SeasonDTO>> GetSeasonsAsync()
    {
        var seasons = await _context.Seasons
            .Include(x => x.League)
            .ToListAsync();

        List<SeasonDTO> result = [];
        foreach (var season in seasons)
        {
            result.Add(_mapper.Map<SeasonDTO>(season));
        }

        if (result != null)
        {
            return result.AsEnumerable();
        }
        else
        {
            //TODO obsluga
            throw new Exception();
        }
    }
}
