using AutoMapper;
using EkstraSim.Shared.DTOs;
using EkstraSim.Shared.Requests;
using Microsoft.EntityFrameworkCore;

namespace EkstraSim.Backend.Database.Services;

public class SimulatedSeasonService
{
    private readonly EkstraSimDbContext _context;

    private readonly IMapper _mapper;
    public SimulatedSeasonService(EkstraSimDbContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    public async Task<IEnumerable<SimulatedFinalLeagueDTO>> GetAllSimulationsOfSeason(GetAllSimulationsOfSeasonRequest request)
    {
        var simulations = await _context.SimulatedFinalLeagues
            .Include(x => x.Teams)
                .ThenInclude(t => t.Team)
            .Include(x => x.Season)
            .Include(x => x.League)
            .Where(x => x.SeasonId == request.SeasonId && x.LeagueId == request.LeagueId)
            .ToListAsync();

        List<SimulatedFinalLeagueDTO> result = [];
        foreach (var simulation in simulations)
        {
            result.Add(_mapper.Map<SimulatedFinalLeagueDTO>(simulation));
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