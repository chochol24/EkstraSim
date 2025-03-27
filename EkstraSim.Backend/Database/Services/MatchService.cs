using AutoMapper;
using EkstraSim.Shared.DTOs;
using EkstraSim.Shared.Requests;
using EkstraSim.Shared.Resources;
using EkstraSim.Shared.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace EkstraSim.Backend.Database.Services;


public class MatchService
{
    private readonly EkstraSimDbContext _context;
    private readonly IMapper _mapper;
    public MatchService(EkstraSimDbContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    public async Task UpdateMatchResultById(UpdateMatchResultRequest req)
    {
        var match = await _context.Matches.FindAsync(req.MatchId);

        if (match != null)
        {
            match.HomeTeamScore = req.HomeTeamScore;
            match.AwayTeamScore = req.AwayTeamScore;

            _context.Matches.Update(match);

            await _context.SaveChangesAsync();
        }
    }

    public async Task<EkstraSimResult<IEnumerable<MatchDTO>>> GetMatchesByRound(GetMatchesByRoundRequest req)
    {
        try
        {
            var matches = await _context.Matches
                .Include(x => x.AwayTeam)
                .Include(x => x.HomeTeam)
                .Where(x => x.SeasonId == req.SeasonId && x.LeagueId == req.LeagueId && x.Round == req.Round)
                .ToListAsync();

            if(matches.IsNullOrEmpty())
            {
                return new EkstraSimResult<IEnumerable<MatchDTO>>
                {
                    Success = false,
                    Data = new List<MatchDTO>(),
                    ErrorMessage = $"{SnackbarMessages.Error_Matches_Null}"
                };
            }

            var result = matches.Select(_mapper.Map<MatchDTO>).ToList();

            return new EkstraSimResult<IEnumerable<MatchDTO>>
            {
                Success = true,
                Data = result
            };
        }
        catch (Exception ex)
        {
            return new EkstraSimResult<IEnumerable<MatchDTO>>
            {
                Success = false,
                Data = new List<MatchDTO>(),
                ErrorMessage = $"{SnackbarMessages.Error_Get}{ex.Message}"
            };
        }
    }
}
