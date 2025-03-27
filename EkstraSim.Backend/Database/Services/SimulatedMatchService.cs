using AutoMapper;
using EkstraSim.Shared.DTOs;
using EkstraSim.Shared.Resources;
using EkstraSim.Shared.Results;
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

    public async Task<EkstraSimResult<SimulatedMatchResultDTO>> GetSimulatedMatchByIdAsync(int matchId)
    {
        try
        {
            var match = await _context.SimulatedMatchResults
                .Include(x => x.Season)
                .Include(x => x.League)
                .Include(x => x.Match)
                    .ThenInclude(x => x.HomeTeam)
                .Include(x => x.Match)
                    .ThenInclude(x => x.AwayTeam)
                .FirstOrDefaultAsync(x => x.Id == matchId);

            if (match == null)
            {
                return new EkstraSimResult<SimulatedMatchResultDTO>
                {
                    Success = false,
                    Data = default,
                    ErrorMessage = SnackbarMessages.Error_SimulatedMatch_NotFound // lub inna stała, np. "Nie znaleziono meczu."
                };
            }

            var result = _mapper.Map<SimulatedMatchResultDTO>(match);

            return new EkstraSimResult<SimulatedMatchResultDTO>
            {
                Success = true,
                Data = result
            };
        }
        catch (Exception ex)
        {
            return new EkstraSimResult<SimulatedMatchResultDTO>
            {
                Success = false,
                Data = default,
                ErrorMessage = $"{SnackbarMessages.Error_Get}{ex.Message}"
            };
        }
    }

}
