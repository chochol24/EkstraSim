using AutoMapper;
using EkstraSim.Shared.DTOs;
using EkstraSim.Shared.Requests;
using EkstraSim.Shared.Resources;
using EkstraSim.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace EkstraSim.Backend.Database.Services;

public class SimulatedSeasonService
{
    private readonly IDbContextFactory<EkstraSimDbContext> _dbFactory;

    private readonly IMapper _mapper;
    public SimulatedSeasonService(IDbContextFactory<EkstraSimDbContext> dbFactory, IMapper mapper)
    {
        _dbFactory = dbFactory;
        _mapper = mapper;
    }

    public async Task<EkstraSimResult<IEnumerable<SimulatedFinalLeagueDTO>>> GetAllSimulationsOfSeason(SeasonAndLeagueRequest request)
    {
        try
        {
            await using var context = await _dbFactory.CreateDbContextAsync();
            var simulations = await context.SimulatedFinalLeagues
                .Include(x => x.Teams)
                    .ThenInclude(t => t.Team)
                .Include(x => x.Season)
                .Include(x => x.League)
                .Where(x => x.SeasonId == request.SeasonId && x.LeagueId == request.LeagueId)
                .ToListAsync();

            var result = simulations.Select(simulation => _mapper.Map<SimulatedFinalLeagueDTO>(simulation)).ToList();

            if (!result.Any())
            {
                return new EkstraSimResult<IEnumerable<SimulatedFinalLeagueDTO>>
                {
                    Success = false,
                    Data = new List<SimulatedFinalLeagueDTO>(),
                    ErrorMessage = SnackbarMessages.Error_Simulations_Null
                };
            }

            return new EkstraSimResult<IEnumerable<SimulatedFinalLeagueDTO>>
            {
                Success = true,
                Data = result
            };
        }
        catch (Exception ex)
        {
            return new EkstraSimResult<IEnumerable<SimulatedFinalLeagueDTO>>
            {
                Success = false,
                Data = new List<SimulatedFinalLeagueDTO>(),
                ErrorMessage = $"{SnackbarMessages.Error_Get}{ex.Message}"
            };
        }
    }

}