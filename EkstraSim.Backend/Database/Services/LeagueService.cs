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

			league.AverageHomeGoalsScored = Math.Round(homeGoals / matchesCount, 3);
			league.AverageAwayGoalsScored = Math.Round(awayGoals / matchesCount, 3);
			league.AverageHomeGoalsConceded = Math.Round(awayGoals / matchesCount, 3);
			league.AverageAwayGoalsConceded = Math.Round(homeGoals / matchesCount, 3);

			_context.Leagues.Update(league);

			await _context.SaveChangesAsync();
		}

	}
}