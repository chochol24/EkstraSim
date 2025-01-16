using Microsoft.EntityFrameworkCore;
using Shared;
using System.Diagnostics;
using EkstraSim.Shared.DTOs;
using EkstraSim.Backend.Database.Entities;

namespace EkstraSim.Backend.Database.Services;

public interface ITeamService
{
	public Task<IEnumerable<TeamDTO>> GetAllTeamsAsync();
	public Task BaseRecalculateEloRankingAllTeamsAsync();
	public Task UpdateAverageTeamGoals();
}

public class TeamService : ITeamService
{
	private readonly EkstraSimDbContext _context;
	public TeamService(EkstraSimDbContext context)
	{
		_context = context;
	}

	public async Task BaseRecalculateEloRankingAllTeamsAsync()
	{
		//TODO add season
		Stopwatch stopwatch = new Stopwatch();
		stopwatch.Start();

		await _context.Teams.ForEachAsync(x => x.ELO = 1300);
		await _context.SaveChangesAsync();
		var matches = await _context.Matches
									.Where(m => m.SeasonId != null)
									.Where(m => m.AwayTeamScore != null && m.HomeTeamScore != null)
									.Include(m => m.HomeTeam)
									.Include(m => m.AwayTeam)
									.OrderBy(m => m.Date)
									.ThenBy(m => m.Round)
									.ToListAsync();


		foreach (var match in matches)
		{
			//Elos
			double homeElo = decimal.ToDouble(match.HomeTeam.ELO);
			double awayElo = decimal.ToDouble(match.AwayTeam.ELO);

			//Result W
			double homeW = match.HomeTeamScore > match.AwayTeamScore 
				           ? 1 : match.HomeTeamScore == match.AwayTeamScore
						   ? 0.5 : 0;
			double awayW =  1 - homeW;

			//dr: ranking difference
			double dr = homeElo - awayElo + 100;

			//W_e: expected result
			double homeW_e = 1 / (Math.Pow(10, -dr / 400) + 1);
			double awayW_e = 1 - homeW_e;

			//G: goal difference
			double goalDifference = Math.Abs(match.HomeTeamScore.GetValueOrDefault() - match.AwayTeamScore.GetValueOrDefault());
			double G = goalDifference == 1
					   ? 1 : goalDifference == 2
					   ? 1.5 : (11 + goalDifference) / 8;

			var newHomeElo = homeElo + Constants.KValueEkstraklasa * G * (homeW - homeW_e);
			var newAwayElo = awayElo + Constants.KValueEkstraklasa * G * (awayW - awayW_e);

			match.HomeTeam.ELO = (decimal)newHomeElo;
			match.AwayTeam.ELO = (decimal)newAwayElo;

			_context.Teams.Update(match.HomeTeam);
			_context.Teams.Update(match.AwayTeam);
		}

		await _context.SaveChangesAsync();
		stopwatch.Stop();
	}

	public async Task<IEnumerable<TeamDTO>> GetAllTeamsAsync()
	{
		var teams = await _context.Teams.ToListAsync();

		List<TeamDTO> result = [];
		foreach (var team in teams)
		{
			result.Add(new TeamDTO 
			{ 
				Elo = team.ELO,
				Name = team.Name,
				Id = team.Id
			});
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

	public async Task UpdateAverageTeamGoals()
	{
		var teams = await _context.Teams
			.Include(t => t.AwayMatches)
			.Include(t => t.HomeMatches)
			.ToListAsync();
		if (teams != null)
		{
			foreach (var team in teams)
			{
				int homematchesCount = team.HomeMatches.Where(x => x.AwayTeamScore != null && x.HomeTeamScore != null).Count();
				int awaymatchesCount = team.AwayMatches.Where(x => x.AwayTeamScore != null && x.HomeTeamScore != null).Count();
				double homeGoalsScored = 0;
				double homeGoalsConceded = 0;
				double awayGoalsScored = 0;
				double awayGoalsConceded = 0;
				foreach (var match in team.HomeMatches)
				{
					homeGoalsScored += match.HomeTeamScore.GetValueOrDefault();
					homeGoalsConceded += match.AwayTeamScore.GetValueOrDefault();
				}
				foreach (var match in team.AwayMatches)
				{
					awayGoalsScored += match.AwayTeamScore.GetValueOrDefault();
					awayGoalsConceded += match.HomeTeamScore.GetValueOrDefault();
				}

				team.AverageHomeGoalsScored = Math.Round(homeGoalsScored / homematchesCount, 3);
				team.AverageAwayGoalsScored = Math.Round(awayGoalsScored / awaymatchesCount, 3);
				team.AverageHomeGoalsConceded = Math.Round(homeGoalsConceded / homematchesCount, 3);
				team.AverageAwayGoalsConceded = Math.Round(awayGoalsConceded / awaymatchesCount, 3);

				_context.Teams.Update(team);
			}
			await _context.SaveChangesAsync();
		}

	}
}
