using Microsoft.EntityFrameworkCore;
using Shared;
using System.Diagnostics;
using EkstraSim.Shared.DTOs;
using AutoMapper;

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
	private readonly IMapper _mapper;
	public TeamService(EkstraSimDbContext context, IMapper mapper)
	{
		_context = context;
		_mapper = mapper;
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
			result.Add(_mapper.Map<TeamDTO>(team));
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
				//current season
				//zmienic nie na sztywno
				var homeMatchesCurrentSeason = team.HomeMatches.Where(x => x.AwayTeamScore != null && x.HomeTeamScore != null && x.LeagueId == 1 && x.SeasonId == 6);
				var awayMatchesCurrentSeason = team.AwayMatches.Where(x => x.AwayTeamScore != null && x.HomeTeamScore != null && x.LeagueId == 1 && x.SeasonId == 6);
				double homeGoalsScoredCurrentSeason = 0;
				double homeGoalsConcededCurrentSeason = 0;
				double awayGoalsScoredCurrentSeason = 0;
				double awayGoalsConcededCurrentSeason = 0;

				foreach(var match in homeMatchesCurrentSeason)
				{
					homeGoalsScoredCurrentSeason += match.HomeTeamScore.GetValueOrDefault();
					homeGoalsConcededCurrentSeason += match.AwayTeamScore.GetValueOrDefault();
				}
                foreach (var match in awayMatchesCurrentSeason)
                {
                    awayGoalsScoredCurrentSeason += match.AwayTeamScore.GetValueOrDefault();
                    awayGoalsConcededCurrentSeason += match.HomeTeamScore.GetValueOrDefault();
                }

				//previous zmienic nie na sztywno
                var homeMatchesPreviousSeason = team.HomeMatches.Where(x => x.AwayTeamScore != null && x.HomeTeamScore != null && x.LeagueId == 1 && x.SeasonId == 1);
                var awayMatchesPreviousSeason = team.AwayMatches.Where(x => x.AwayTeamScore != null && x.HomeTeamScore != null && x.LeagueId == 1 && x.SeasonId == 1);
                double homeGoalsScoredPreviousSeason = 0;
                double homeGoalsConcededPreviousSeason = 0;
                double awayGoalsScoredPreviousSeason = 0;
                double awayGoalsConcededPreviousSeason = 0;

                foreach (var match in homeMatchesPreviousSeason)
                {
                    homeGoalsScoredPreviousSeason += match.HomeTeamScore.GetValueOrDefault();
                    homeGoalsConcededPreviousSeason += match.AwayTeamScore.GetValueOrDefault();
                }
                foreach (var match in awayMatchesPreviousSeason)
                {
                    awayGoalsScoredPreviousSeason += match.AwayTeamScore.GetValueOrDefault();
                    awayGoalsConcededPreviousSeason += match.HomeTeamScore.GetValueOrDefault();
                }

				//historical

                var homeMatchesHistorical = team.HomeMatches.Where(x => x.AwayTeamScore != null && x.HomeTeamScore != null && x.LeagueId == 1 && x.SeasonId != 6 && x.SeasonId != 1);
                var awayMatchesHistorical = team.AwayMatches.Where(x => x.AwayTeamScore != null && x.HomeTeamScore != null && x.LeagueId == 1 && x.SeasonId != 6 && x.SeasonId != 1);
                double homeGoalsScoredHistorical = 0;
                double homeGoalsConcededHistorical = 0;
                double awayGoalsScoredHistorical = 0;
                double awayGoalsConcededHistorical = 0;

                foreach (var match in homeMatchesHistorical)
                {
                    homeGoalsScoredHistorical += match.HomeTeamScore.GetValueOrDefault();
                    homeGoalsConcededHistorical += match.AwayTeamScore.GetValueOrDefault();
                }
                foreach (var match in awayMatchesHistorical)
                {
                    awayGoalsScoredHistorical += match.AwayTeamScore.GetValueOrDefault();
                    awayGoalsConcededHistorical += match.HomeTeamScore.GetValueOrDefault();
                }

				//wszystkie
                team.AverageHomeGoalsScored = Math.Round(homeGoalsScored / homematchesCount, 3);
                team.AverageAwayGoalsScored = Math.Round(awayGoalsScored / awaymatchesCount, 3);
                team.AverageHomeGoalsConceded = Math.Round(homeGoalsConceded / homematchesCount, 3);
                team.AverageAwayGoalsConceded = Math.Round(awayGoalsConceded / awaymatchesCount, 3);
				//current

				var league = await _context.Leagues.Where(x => x.Id == 1).FirstOrDefaultAsync();

				if (homeMatchesCurrentSeason.Count() <= 0 || awayMatchesCurrentSeason.Count() <= 0)
				{
                    team.AverageHomeGoalsScoredCurrentSeason = league.AverageHomeGoalsScored;
					team.AverageAwayGoalsScoredCurrentSeason = league.AverageAwayGoalsScored;
                    team.AverageHomeGoalsConcededCurrentSeason = league.AverageHomeGoalsConceded;
					team.AverageAwayGoalsConcededCurrentSeason = league.AverageAwayGoalsConceded;
                }
				else
				{
                    team.AverageHomeGoalsScoredCurrentSeason = Math.Round(homeGoalsScoredCurrentSeason / homeMatchesCurrentSeason.Count(), 3);
                    team.AverageAwayGoalsScoredCurrentSeason = Math.Round(awayGoalsScoredCurrentSeason / awayMatchesCurrentSeason.Count(), 3);
                    team.AverageHomeGoalsConcededCurrentSeason = Math.Round(homeGoalsConcededCurrentSeason / homeMatchesCurrentSeason.Count(), 3);
                    team.AverageAwayGoalsConcededCurrentSeason = Math.Round(awayGoalsConcededCurrentSeason / awayMatchesCurrentSeason.Count(), 3);
                }

                //previous
                if (homeMatchesPreviousSeason.Count() <= 0 || awayMatchesPreviousSeason.Count() <= 0)
                {
                    team.AverageHomeGoalsScoredPreviousSeason = league.AverageHomeGoalsScored;
                    team.AverageAwayGoalsScoredPreviousSeason = league.AverageAwayGoalsScored;
                    team.AverageHomeGoalsConcededPreviousSeason = league.AverageHomeGoalsConceded;
                    team.AverageAwayGoalsConcededPreviousSeason = league.AverageAwayGoalsConceded;
                }
				else
				{
                    team.AverageHomeGoalsScoredPreviousSeason = Math.Round(homeGoalsScoredPreviousSeason / homeMatchesPreviousSeason.Count(), 3);
                    team.AverageAwayGoalsScoredPreviousSeason = Math.Round(awayGoalsScoredPreviousSeason / awayMatchesPreviousSeason.Count(), 3);
                    team.AverageHomeGoalsConcededPreviousSeason = Math.Round(homeGoalsConcededPreviousSeason / homeMatchesPreviousSeason.Count(), 3);
                    team.AverageAwayGoalsConcededPreviousSeason = Math.Round(awayGoalsConcededPreviousSeason / awayMatchesPreviousSeason.Count(), 3);
                }

                //historical
                if (homeMatchesHistorical.Count() <= 0 || awayMatchesHistorical.Count() <= 0)
                {
                    team.AverageHomeGoalsScoredHistorical = league.AverageHomeGoalsScored;
                    team.AverageAwayGoalsScoredHistorical = league.AverageAwayGoalsScored;
                    team.AverageHomeGoalsConcededHistorical = league.AverageHomeGoalsConceded;
                    team.AverageAwayGoalsConcededHistorical = league.AverageAwayGoalsConceded;
                }
				else
				{
                    team.AverageHomeGoalsScoredHistorical = Math.Round(homeGoalsScoredHistorical / homeMatchesHistorical.Count(), 3);
                    team.AverageAwayGoalsScoredHistorical = Math.Round(awayGoalsScoredHistorical / awayMatchesHistorical.Count(), 3);
                    team.AverageHomeGoalsConcededHistorical = Math.Round(homeGoalsConcededHistorical / homeMatchesHistorical.Count(), 3);
                    team.AverageAwayGoalsConcededHistorical = Math.Round(awayGoalsConcededHistorical / awayMatchesHistorical.Count(), 3);

                }

                _context.Teams.Update(team);
			}
			await _context.SaveChangesAsync();
		}

	}
}
