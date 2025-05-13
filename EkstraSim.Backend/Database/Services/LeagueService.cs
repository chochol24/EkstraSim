using AutoMapper;
using EkstraSim.Shared.DTOs;
using EkstraSim.Shared.Resources;
using EkstraSim.Shared.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace EkstraSim.Backend.Database.Services;

public interface ILeagueService
{
	public Task UpdateAverageLeagueGoals(int leagueId);
    public Task<EkstraSimResult<IEnumerable<LeagueDTO>>> GetAllLeagues();
}

public class LeagueService : ILeagueService
{
    private readonly IDbContextFactory<EkstraSimDbContext> _dbFactory;
    private readonly IMapper _mapper;
	public LeagueService(IDbContextFactory<EkstraSimDbContext> dbFactory, IMapper mapper)
	{
        _dbFactory = dbFactory;
        _mapper = mapper;
	}

    public async Task<EkstraSimResult<IEnumerable<LeagueDTO>>> GetAllLeagues()
    {
		try
		{
            await using var context = await _dbFactory.CreateDbContextAsync();
            var leagues = await context.Leagues.ToListAsync();
			if (leagues.IsNullOrEmpty())
			{
				return new EkstraSimResult<IEnumerable<LeagueDTO>>
				{
					Success = false,
					Data = Enumerable.Empty<LeagueDTO>(),
					ErrorMessage = $"{SnackbarMessages.Error_Leagues_Null}"
				};
			}

			var result = leagues.Select(_mapper.Map<LeagueDTO>).ToList();

			return new EkstraSimResult<IEnumerable<LeagueDTO>>
			{
				Success = true,
				Data = result
			};
		}
		catch (Exception ex)
		{
            return new EkstraSimResult<IEnumerable<LeagueDTO>>
            {
                Success = false,
                Data = new List<LeagueDTO>(),
                ErrorMessage = $"{SnackbarMessages.Error_Get}{ex.Message}"
            };
        }

    }

    public async Task UpdateAverageLeagueGoals(int leagueId)
	{
        await using var context = await _dbFactory.CreateDbContextAsync();
        var league = await context.Leagues.FindAsync(leagueId);
		if (league != null)
		{
			var matches = await context.Matches.Where(x => x.LeagueId == leagueId && x.HomeTeamScore != null && x.AwayTeamScore != null).ToListAsync();
			int matchesCount = matches.Count();

			double homeGoals = 0;
			double awayGoals = 0;
			foreach (var match in matches)
			{
				homeGoals += match.HomeTeamScore.GetValueOrDefault();
				awayGoals += match.AwayTeamScore.GetValueOrDefault();
			}

			league.AverageHomeGoalsScored = homeGoals / matchesCount;
			league.AverageAwayGoalsScored = awayGoals / matchesCount;
			league.AverageHomeGoalsConceded = awayGoals / matchesCount;
			league.AverageAwayGoalsConceded = homeGoals / matchesCount;

            context.Leagues.Update(league);

			await context.SaveChangesAsync();
		}

	}
}