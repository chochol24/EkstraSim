using AutoMapper;
using EkstraSim.Shared.DTOs;
using EkstraSim.Shared.Requests;
using Microsoft.EntityFrameworkCore;

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

    public async Task<IEnumerable<MatchDTO>> GetMatchesByRound(GetMatchesByRoundRequest req)
    {
        var matches = await _context.Matches
            .Include(x => x.AwayTeam)
            .Include(x => x.HomeTeam)
            .Where(x => x.SeasonId == req.SeasonId && x.LeagueId == req.LeagueId && x.Round == req.Round)
            .ToListAsync();

        List<MatchDTO> result = [];
        foreach (var match in matches)
        {
            result.Add(_mapper.Map<MatchDTO>(match));
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
