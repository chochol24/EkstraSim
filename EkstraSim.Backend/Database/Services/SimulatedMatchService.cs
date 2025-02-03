using AutoMapper;
using EkstraSim.Shared.DTOs;
using Microsoft.EntityFrameworkCore;

namespace EkstraSim.Backend.Database.Services;


public class SimulatedMatchService
{
    private readonly EkstraSimDbContext _context;

    private readonly IMapper _mapper;
    public SimulatedMatchService(EkstraSimDbContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    public async Task<SimulatedMatchResultDTO> GetSimulatedMatchByIdAsync(int matchId)
    {
        var match = await _context.SimulatedMatchResults
            .Include(x => x.Season)
            .Include(x => x.League)
            .Include(x => x.Match)
                .ThenInclude(x => x.HomeTeam)
            .Include(x => x.Match)
                .ThenInclude(x => x.AwayTeam)
            .FirstOrDefaultAsync(x => x.Id == matchId);

        return _mapper.Map<SimulatedMatchResultDTO>(match);
    }
}
