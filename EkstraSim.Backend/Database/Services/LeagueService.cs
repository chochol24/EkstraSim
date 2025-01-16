using Microsoft.EntityFrameworkCore;

namespace EkstraSim.Backend.Database.Services;

public interface ILeagueService
{
	public Task UpdateAverageLeagueGoals(int leagueId);
}

public class LeagueService : ILeagueService
{
	private readonly EkstraSimDbContext _context;
	public LeagueService(EkstraSimDbContext context)
	{
		_context = context;
	}

	public async Task UpdateAverageLeagueGoals(int leagueId)
	{
		var league = await _context.Leagues.FindAsync(leagueId);
		if (league != null)
		{
			var matches = await _context.Matches.Where(x => x.LeagueId == leagueId && x.HomeTeamScore != null && x.AwayTeamScore != null).ToListAsync();
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

			_context.Leagues.Update(league);

			await _context.SaveChangesAsync();
		}

	}
}