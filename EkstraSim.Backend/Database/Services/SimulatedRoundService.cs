using AutoMapper;
using EkstraSim.Shared.DTOs;
using EkstraSim.Shared.Resources;
using EkstraSim.Shared.Results;
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

    public async Task<EkstraSimResult<IEnumerable<SimulatedRoundDTO>>> GetSimulatedRoundsAsync(int seasonId, int leagueId)
    {
        try
        {
            var rounds = await _context.SimulatedRounds
                .Include(x => x.Season)
                .Include(x => x.League)
                .Include(x => x.SimulatedMatchResults)
                .Where(x => x.SeasonId == seasonId && x.LeagueId == leagueId)
                .ToListAsync();

            var result = rounds.Select(round => _mapper.Map<SimulatedRoundDTO>(round)).ToList();

            if (!result.Any())
            {
                return new EkstraSimResult<IEnumerable<SimulatedRoundDTO>>
                {
                    Success = false,
                    Data = new List<SimulatedRoundDTO>(),
                    ErrorMessage = SnackbarMessages.Error_SimulatedRounds_Null
                };
            }

            return new EkstraSimResult<IEnumerable<SimulatedRoundDTO>>
            {
                Success = true,
                Data = result
            };
        }
        catch (Exception ex)
        {
            return new EkstraSimResult<IEnumerable<SimulatedRoundDTO>>
            {
                Success = false,
                Data = new List<SimulatedRoundDTO>(),
                ErrorMessage = $"{SnackbarMessages.Error_Get}{ex.Message}"
            };
        }
    }


    public async Task<EkstraSimResult<SimulatedRoundDTO>> GetSimulatedRoundByIdAsync(int roundId)
    {
        try
        {
            var round = await _context.SimulatedRounds
                .Include(x => x.Season)
                .Include(x => x.League)
                .Include(x => x.SimulatedMatchResults)
                    .ThenInclude(x => x.Match)
                        .ThenInclude(x => x.HomeTeam)
                .Include(x => x.SimulatedMatchResults)
                    .ThenInclude(x => x.Match)
                        .ThenInclude(x => x.AwayTeam)
                .FirstOrDefaultAsync(x => x.Id == roundId);

            if (round == null)
            {
                return new EkstraSimResult<SimulatedRoundDTO>
                {
                    Success = false,
                    Data = default,
                    ErrorMessage = SnackbarMessages.Error_SimulatedRound_NotFound
                };
            }

            var result = _mapper.Map<SimulatedRoundDTO>(round);

            return new EkstraSimResult<SimulatedRoundDTO>
            {
                Success = true,
                Data = result
            };
        }
        catch (Exception ex)
        {
            return new EkstraSimResult<SimulatedRoundDTO>
            {
                Success = false,
                Data = default,
                ErrorMessage = $"{SnackbarMessages.Error_Get}{ex.Message}"
            };
        }
    }

}
