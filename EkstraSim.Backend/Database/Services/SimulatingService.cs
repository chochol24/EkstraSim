using EkstraSim.Backend.Database.Entities;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace EkstraSim.Backend.Database.Services;

public class MatchResult
{
	public int HomeScore { get; set; }
	public int AwayScore { get; set; }
}
public class MatchPredictionGoals
{
	public double HomeGoals {  get; set; }
	public double AwayGoals {  get; set; }
}

public interface ISimulatingService
{
	Task<MatchResult> SimulateMatch(Match match, MatchPredictionGoals predicition);
	Task SimulateRestOfTheSeason(int leagueId, int seasonId, int? currentRound = 0, int? numberOfSimulations = 1000);
	Task<IEnumerable<SimulatedMatchResult>> SimulateRoundEndpoint(int leagueId, int seasonId, int round = 0, int numberOfSimulations = 1000);
}

public class SimulatingService : ISimulatingService
{
	private readonly EkstraSimDbContext _context;
	public SimulatingService(EkstraSimDbContext context)
	{
		_context = context;
	}
	public Task<MatchResult> SimulateMatch(Match match, MatchPredictionGoals predicition)
	{
		var random = new Random();

		double homeLambda = predicition.HomeGoals;
		double awayLambda = predicition.AwayGoals;

		int SimulateGoals(double lambda)
		{
			double l = Math.Exp(-lambda);
			int k = 0;
			double p = 1.0;
			do
			{
				k++;
				p *= random.NextDouble();
			} while (p > l);
			return k - 1;
		}

		int homeScore = SimulateGoals(homeLambda);
		int awayScore = SimulateGoals(awayLambda);

		return Task.FromResult(new MatchResult
		{
			HomeScore = homeScore,
			AwayScore = awayScore,
		});
	}

	public async Task<IEnumerable<SimulatedMatchResult>> SimulateRound(List<Match> matchesToSimulate, int leagueId, int numberOfSimulations)
	{
		Stopwatch sw = new Stopwatch();
		sw.Start();
		
		if (matchesToSimulate.Any())
		{
			Dictionary<int, Dictionary<Match, MatchResult>> simulationResults = [];

			Dictionary<Match, Dictionary<int, int>> homeGoalCountsPerMatch = new();
			Dictionary<Match, Dictionary<int, int>> awayGoalCountsPerMatch = new();

			Dictionary<Match, MatchPredictionGoals> predictionMatches = [];

			var league = _context.Leagues.Where(x => x.Id == leagueId).FirstOrDefault();

			foreach(var match in matchesToSimulate)
			{
				var homeAttackStrength = match.HomeTeam.AverageHomeGoalsScored / league.AverageHomeGoalsScored;
				var homeDefenseStrength = match.HomeTeam.AverageHomeGoalsConceded / league.AverageHomeGoalsConceded;
				var awayAttackStrength = match.AwayTeam.AverageAwayGoalsScored / league.AverageAwayGoalsScored;
				var awayDefenseStrength = match.AwayTeam.AverageAwayGoalsConceded / league.AverageAwayGoalsConceded;

				var homePred = homeAttackStrength * awayDefenseStrength * league.AverageHomeGoalsScored;
				var awayPred = awayAttackStrength * homeDefenseStrength * league.AverageAwayGoalsScored;

				predictionMatches.Add(match, new MatchPredictionGoals { HomeGoals = homePred.GetValueOrDefault(), AwayGoals = awayPred.GetValueOrDefault()});

				homeGoalCountsPerMatch[match] = Enumerable.Range(0, 11).ToDictionary(x => x, x => 0);
				awayGoalCountsPerMatch[match] = Enumerable.Range(0, 11).ToDictionary(x => x, x => 0);

			}

			for (int i = 0; i < numberOfSimulations; i++)
			{
				Dictionary<Match, MatchResult> roundResult = [];
				foreach (Match match in matchesToSimulate)
				{
					var prediction = predictionMatches[match];
					var matchResult = await SimulateMatch(match, prediction);
					roundResult.Add(match, matchResult);

					if (matchResult.HomeScore <= 10)
					{
						homeGoalCountsPerMatch[match][matchResult.HomeScore]++;
					}
					if (matchResult.AwayScore <= 10)
					{
						awayGoalCountsPerMatch[match][matchResult.AwayScore]++;
					}
				}
				simulationResults.Add(i, roundResult);
			}

			List<SimulatedMatchResult> simulatedResults = new();

			foreach (Match match in matchesToSimulate)
			{
				int totalHomeWins = 0;
				int totalAwayWins = 0;
				int totalDraws = 0;

				double totalHomeGoals = 0;
				double totalAwayGoals = 0;

				double[,] resultProbabilityMatrix = new double[11, 11];

				foreach (var simulation in simulationResults.Values)
				{
					if (simulation.TryGetValue(match, out var result))
					{
						totalHomeGoals += result.HomeScore;
						totalAwayGoals += result.AwayScore;

						if (result.HomeScore > result.AwayScore)
							totalHomeWins++;
						else if(result.HomeScore < result.AwayScore)
							totalAwayWins++;
						else totalDraws++;

						if (result.HomeScore <= 10 && result.AwayScore <= 10)
						{
							resultProbabilityMatrix[result.HomeScore, result.AwayScore]++;
						}
					}
				}

				for (int i = 0; i <= 10; i++)
				{
					for (int j = 0; j <= 10; j++)
					{
						resultProbabilityMatrix[i, j] /= numberOfSimulations;
					}
				}

				double maxProbability = 0;
				int bestHomeScore = 0;
				int bestAwayScore = 0;

				for (int i = 0; i <= 10; i++)
				{
					for (int j = 0; j <= 10; j++)
					{
						if (resultProbabilityMatrix[i, j] > maxProbability)
						{
							maxProbability = resultProbabilityMatrix[i, j];
							bestHomeScore = i;
							bestAwayScore = j;
						}
					}
				}

				var simulatedMatchResult = new SimulatedMatchResult
				{
					MatchId = match.Id,
					Round = match.Round,
					SeasonId = match.SeasonId,
					LeagueId = match.LeagueId,
					PredictedHomeScore = bestHomeScore,
					PredictedAwayScore = bestAwayScore,
					HomeWinProbability = (double)totalHomeWins / numberOfSimulations,
					DrawProbability = (double)totalDraws / numberOfSimulations,
					AwayWinProbability = (double)totalAwayWins / numberOfSimulations,
					NumberOfSimulations = numberOfSimulations
				};
				simulatedResults.Add(simulatedMatchResult);

				Console.WriteLine($"Wyniki dla meczu {match.HomeTeam.Name} vs {match.AwayTeam.Name}:");
				Console.WriteLine("Procentowe prawdopodobieństwo dla każdego wyniku:");
				for (int homeGoals = 0; homeGoals <= 10; homeGoals++)
				{
					for (int awayGoals = 0; awayGoals <= 10; awayGoals++)
					{
						double percentage = resultProbabilityMatrix[homeGoals, awayGoals] * 100;
						if (percentage > 0)
						{
							Console.WriteLine($"Wynik {homeGoals}:{awayGoals} - {percentage:F2}%");
						}
					}
				}

				Console.WriteLine($"Najbardziej prawdopodobny wynik: {bestHomeScore}:{bestAwayScore} z prawdopodobieństwem {maxProbability * 100:F2}%");
				Console.WriteLine();
			}
			sw.Stop();
			Console.WriteLine($"Czas na wykonanie {numberOfSimulations} symulacji: {sw.Elapsed}");
			return simulatedResults;
		}
		else
		{
			return null;
		}
	}

	public async Task<IEnumerable<SimulatedMatchResult>> SimulateRoundForLeagueSim(List<Match> matchesToSimulate, int leagueId)
	{
		var response = await SimulateRound(matchesToSimulate, leagueId, 1);
		return response;
	}

	public async Task<IEnumerable<SimulatedMatchResult>> SimulateRoundEndpoint(int leagueId, int seasonId, int round = 0, int numberOfSimulations = 1000)
	{
		if (round < 0 || round > 34)
		{
			return null;
		}

		List<Match> matchesToSimulate = [];
		if (round == 0)
		{
			//todo add infor about number of teams in leagu etc.
			//todo dodac obsluge jesli jakis mecz rundy przelozony 

			var lastMatch = await _context.Matches
				.Where(x => x.LeagueId == leagueId && x.SeasonId == seasonId && x.HomeTeamScore != null && x.AwayTeamScore != null)
				.OrderByDescending(x => x.Date)
				.FirstOrDefaultAsync();

			var roundLastMatch = lastMatch.Round.GetValueOrDefault();
			if (roundLastMatch <= 33)
			{
				round = roundLastMatch + 1;
			}
			else
			{
				return null;
			}
		}
		matchesToSimulate = await _context.Matches
				.Where(x => x.LeagueId == leagueId && x.SeasonId == seasonId && x.HomeTeamScore == null && x.AwayTeamScore == null && x.Round == round)
				.Include(x => x.AwayTeam)
				.Include(x => x.HomeTeam)
				.ToListAsync();

		var response = await SimulateRound(matchesToSimulate, leagueId, numberOfSimulations);
		return response;
	}

	public async Task SimulateRestOfTheSeason(int leagueId, int seasonId, int? currentRound = 0, int? numberOfSimulations = 1000)
	{
		int totalRounds = 34;
		List<Team> teams = new List<Team>();
		if (currentRound == 0)
		{
			//todo add infor about number of teams in leagu etc.
			//todo dodac obsluge jesli jakis mecz rundy przelozony 

			var lastMatch = await _context.Matches
				.Where(x => x.LeagueId == leagueId && x.SeasonId == seasonId && x.HomeTeamScore != null && x.AwayTeamScore != null)
				.OrderByDescending(x => x.Date)
				.FirstOrDefaultAsync();

			var roundLastMatch = lastMatch.Round.GetValueOrDefault();
			if (roundLastMatch < totalRounds)
			{
				currentRound = roundLastMatch + 1;
				
			}
			else
			{
				return;
			}
		}
		var teams1Id = await _context.Matches.Where(m => m.LeagueId == leagueId && m.SeasonId == seasonId && m.Round == currentRound).Select(x => x.HomeTeamId).ToListAsync();
		var teams2Id = await _context.Matches.Where(m => m.LeagueId == leagueId && m.SeasonId == seasonId && m.Round == currentRound).Select(x => x.AwayTeamId).ToListAsync();
		var teamIds = teams1Id.Union(teams2Id).ToList();

		teams = await _context.Teams
		   .Where(t => teamIds.Contains(t.Id))
		   .Include(t => t.AwayMatches)
		   .Include(t => t.HomeMatches)
		   .ToListAsync();

		var simulatedLeague = new SimulatedFinalLeague
		{
			LeagueId = leagueId,
			SeasonId = seasonId,
			RoundBeforeSimulation = currentRound.GetValueOrDefault(),
			NumberOfSimulations = numberOfSimulations.GetValueOrDefault()
		};

		//tu zapis do bazy zeby miec id


		var simulatedTeams = teams.Select(t => new SimulatedTeamInFinalTable
		{
			TeamId = t.Id,
			//SimulatedFinalLeagueId = simulatedLeague.Id,
			SimulatedFinalLeagueId = 1,
		}).ToList();

		for (int i = 0; i < numberOfSimulations; i++)
		{
			for (int round = currentRound.GetValueOrDefault(); round <= totalRounds; round++)
			{
				var matchesToSimulate = await _context.Matches.Where(m => m.LeagueId == leagueId && m.SeasonId == seasonId && m.Round == round).ToListAsync();
				var response = await SimulateRoundForLeagueSim(matchesToSimulate, leagueId);
				await UpdateTeamAverages(response, teams);
				var x = 3;
			}
		}
	}

	private async Task<List<Team>> UpdateTeamAverages(IEnumerable<SimulatedMatchResult> simulatedRoundResults, List<Team> teams)
	{
		foreach (var matchResult in simulatedRoundResults)
		{
			var match = _context.Matches.Where(x => x.Id == matchResult.MatchId).Include(m => m.HomeTeam).Include(m => m.AwayTeam).FirstOrDefault();

			var homeTeam = teams.Where(x => x.Id == match.HomeTeamId).FirstOrDefault();
			var awayTeam = teams.Where(x => x.Id == match.AwayTeamId).FirstOrDefault();
			int homeMatchesCount = homeTeam.HomeMatches.Where(x => x.AwayTeamScore != null && x.HomeTeamScore != null).Count();
			int awayMatchesCount = awayTeam.AwayMatches.Where(x => x.AwayTeamScore != null && x.HomeTeamScore != null).Count();

			if(awayTeam.Id == 15)
			{
				var x = 4;
			}
			if (homeTeam != null)
			{
				homeTeam.AverageHomeGoalsScored =
					((homeTeam.AverageHomeGoalsScored ?? 0) * homeMatchesCount + matchResult.PredictedHomeScore)
					/ (homeMatchesCount + 1);

				homeTeam.AverageHomeGoalsConceded =
					((homeTeam.AverageHomeGoalsConceded ?? 0) * homeMatchesCount + matchResult.PredictedAwayScore)
					/ (homeMatchesCount + 1);

				var matchHome = homeTeam.HomeMatches.Where(m => m.Id == match.Id).FirstOrDefault();
				matchHome.HomeTeamScore = matchResult.PredictedHomeScore;
				matchHome.AwayTeamScore = matchResult.PredictedAwayScore;
			}

			if (awayTeam != null)
			{
				awayTeam.AverageAwayGoalsScored =
					((awayTeam.AverageAwayGoalsScored ?? 0) * awayMatchesCount + matchResult.PredictedAwayScore)
					/ (awayMatchesCount + 1);

				awayTeam.AverageAwayGoalsConceded =
					((awayTeam.AverageAwayGoalsConceded ?? 0) * awayMatchesCount + matchResult.PredictedHomeScore)
					/ (awayMatchesCount + 1);

				var matchAway = awayTeam.AwayMatches.Where(m => m.Id == match.Id).FirstOrDefault();
				matchAway.HomeTeamScore = matchResult.PredictedHomeScore;
				matchAway.AwayTeamScore = matchResult.PredictedAwayScore;
			}
		}
		return teams;
	}

}

