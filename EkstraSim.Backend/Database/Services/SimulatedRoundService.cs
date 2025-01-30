using AutoMapper;
using EkstraSim.Shared.DTOs;
using Microsoft.EntityFrameworkCore;

namespace EkstraSim.Backend.Database.Services;

public class SimulatedRoundService
{
    private readonly EkstraSimDbContext _context;

    private readonly IMapper _mapper;
    public SimulatedRoundService(EkstraSimDbContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    public async Task<IEnumerable<SimulatedRoundDTO>> GetSimulatedRoundsAsync()
    {
        var rounds = await _context.SimulatedRounds
            .Include(x => x.Season)
            .Include(x => x.League)
            .Include(x => x.SimulatedMatchResults)
            .ToListAsync();

        List<SimulatedRoundDTO> result = [];
        foreach (var round in rounds)
        {
            result.Add(_mapper.Map<SimulatedRoundDTO>(round));
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
