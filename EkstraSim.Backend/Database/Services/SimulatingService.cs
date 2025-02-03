using EkstraSim.Backend.Database.Entities;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using Shared;
using Newtonsoft.Json;

namespace EkstraSim.Backend.Database.Services;

public class MatchResult
{
    public int HomeScore { get; set; }
    public int AwayScore { get; set; }
}
public class MatchPredictionGoals
{
    public double HomeGoals { get; set; }
    public double AwayGoals { get; set; }
}

public interface ISimulatingService
{
    Task<MatchResult> SimulateMatch(Match match, MatchPredictionGoals predicition);
    Task SimulateRestOfTheSeason(int leagueId, int seasonId, int? currentRound = 0, int? numberOfSimulations = 1000);
    Task<IEnumerable<SimulatedMatchResult>> SimulateRoundEndpoint(int leagueId, int seasonId, int round = 0, int numberOfSimulations = 1000);
}

public partial class SimulatingService : ISimulatingService
{
    private readonly EkstraSimDbContext _context;
    private readonly DbContextOptions<EkstraSimDbContext> _options;
    private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(10);
    public SimulatingService(EkstraSimDbContext context, DbContextOptions<EkstraSimDbContext> options)
    {
        _context = context;
        _options = options;
    }

    private static Random _random = new Random();

    public async Task<MatchResult> SimulateMatch(Match match, MatchPredictionGoals predicition)
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

        return await Task.FromResult(new MatchResult
        {
            HomeScore = homeScore,
            AwayScore = awayScore,
        });
    }

    public async Task<IEnumerable<SimulatedMatchResult>> SimulateRound(List<Match> matchesToSimulate, int leagueId, int numberOfSimulations, EkstraSimDbContext ekstraSimDbContext = null)
    {
        if (ekstraSimDbContext == null)
        {
            ekstraSimDbContext = _context;
        }

        Stopwatch sw = Stopwatch.StartNew();

        if (!matchesToSimulate.Any())
            return null;

        var league = await ekstraSimDbContext.Leagues.FirstOrDefaultAsync(x => x.Id == leagueId);
        if (league == null)
            throw new InvalidOperationException($"League with ID {leagueId} not found.");

        Dictionary<int, Dictionary<Match, MatchResult>> simulationResults = new();
        Dictionary<Match, MatchPredictionGoals> predictionMatches = new();

        foreach (var match in matchesToSimulate)
        {
            var homeAttackStrengthCurrent = match.HomeTeam.AverageHomeGoalsScoredCurrentSeason / league.AverageHomeGoalsScored;
            var homeDefenseStrengthCurrent = match.HomeTeam.AverageHomeGoalsConcededCurrentSeason / league.AverageHomeGoalsConceded;
            var awayAttackStrengthCurrent = match.AwayTeam.AverageAwayGoalsScoredCurrentSeason / league.AverageAwayGoalsScored;
            var awayDefenseStrengthCurrent = match.AwayTeam.AverageAwayGoalsConcededCurrentSeason / league.AverageAwayGoalsConceded;

            var homeAttackStrengthPrevious = match.HomeTeam.AverageHomeGoalsScoredPreviousSeason / league.AverageHomeGoalsScored;
            var homeDefenseStrengthPrevious = match.HomeTeam.AverageHomeGoalsConcededPreviousSeason / league.AverageHomeGoalsConceded;
            var awayAttackStrengthPrevious = match.AwayTeam.AverageAwayGoalsScoredPreviousSeason / league.AverageAwayGoalsScored;
            var awayDefenseStrengthPrevious = match.AwayTeam.AverageAwayGoalsConcededPreviousSeason / league.AverageAwayGoalsConceded;


            var homeAttackStrengthHistorical = match.HomeTeam.AverageHomeGoalsScoredHistorical / league.AverageHomeGoalsScored;
            var homeDefenseStrengthHistorical = match.HomeTeam.AverageHomeGoalsConcededHistorical / league.AverageHomeGoalsConceded;
            var awayAttackStrengthHistorical = match.AwayTeam.AverageAwayGoalsScoredHistorical / league.AverageAwayGoalsScored;
            var awayDefenseStrengthHistorical = match.AwayTeam.AverageAwayGoalsConcededHistorical / league.AverageAwayGoalsConceded;

            var homePred = (homeAttackStrengthCurrent * awayDefenseStrengthCurrent * league.AverageHomeGoalsScored * 0.60)
                         + (homeAttackStrengthPrevious * awayDefenseStrengthPrevious * league.AverageHomeGoalsScored * 0.30)
                         + (homeAttackStrengthHistorical * awayDefenseStrengthHistorical * league.AverageHomeGoalsScored * 0.10);

            var awayPred = (awayAttackStrengthCurrent * homeDefenseStrengthCurrent * league.AverageAwayGoalsScored * 0.60)
                         + (awayAttackStrengthPrevious * homeDefenseStrengthPrevious * league.AverageAwayGoalsScored * 0.30)
                         + (awayAttackStrengthHistorical * homeDefenseStrengthHistorical * league.AverageAwayGoalsScored * 0.10);


            predictionMatches.Add(match, new MatchPredictionGoals { HomeGoals = homePred.GetValueOrDefault(), AwayGoals = awayPred.GetValueOrDefault() });
        }

        for (int i = 0; i < numberOfSimulations; i++)
        {
            Dictionary<Match, MatchResult> roundResult = [];
            foreach (Match match in matchesToSimulate)
            {
                var prediction = predictionMatches[match];
                var matchResult = await SimulateMatch(match, prediction);
                roundResult.Add(match, matchResult);

            }
            simulationResults.Add(i, roundResult);
        }

        List<SimulatedMatchResult> simulatedResults = new();

        foreach (Match match in matchesToSimulate)
        {
            int totalHomeWins = 0, totalAwayWins = 0, totalDraws = 0;
            double totalHomeGoals = 0, totalAwayGoals = 0;

            double[,] resultProbabilityMatrix = new double[11, 11];

            foreach (var simulation in simulationResults.Values)
            {
                if (simulation.TryGetValue(match, out var result))
                {
                    totalHomeGoals += result.HomeScore;
                    totalAwayGoals += result.AwayScore;

                    if (result.HomeScore > result.AwayScore)
                        totalHomeWins++;
                    else if (result.HomeScore < result.AwayScore)
                        totalAwayWins++;
                    else 
                        totalDraws++;

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

            string matrixJson = JsonConvert.SerializeObject(resultProbabilityMatrix, Formatting.Indented);
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
                NumberOfSimulations = numberOfSimulations,
                ResultProbabilityMatrixJson = matrixJson
            };
            simulatedResults.Add(simulatedMatchResult);


            //Console.WriteLine($"Wyniki dla meczu {match.HomeTeam.Name} vs {match.AwayTeam.Name}:");
            //Console.WriteLine("Procentowe prawdopodobieństwo dla każdego wyniku:");
            //for (int homeGoals = 0; homeGoals <= 10; homeGoals++)
            //{
            //    for (int awayGoals = 0; awayGoals <= 10; awayGoals++)
            //    {
            //        double percentage = resultProbabilityMatrix[homeGoals, awayGoals] * 100;
            //        if (percentage > 0)
            //        {
            //            Console.WriteLine($"Wynik {homeGoals}:{awayGoals} - {percentage:F2}%");
            //        }
            //    }
            //}
            //Console.WriteLine($"Najbardziej prawdopodobny wynik: {bestHomeScore}:{bestAwayScore} z prawdopodobieństwem {maxProbability * 100:F2}%");
            //Console.WriteLine();

        }
        sw.Stop();
        Console.WriteLine($"Czas na wykonanie {numberOfSimulations} symulacji rundy: {sw.Elapsed}");
        return simulatedResults;

    }

    public async Task<IEnumerable<SimulatedMatchResult>> SimulateRoundForLeagueSim(List<Match> matchesToSimulate, int leagueId, EkstraSimDbContext context)
    {
        return await SimulateRound(matchesToSimulate, leagueId, 1, context);
    }

    public async Task<IEnumerable<SimulatedMatchResult>> SimulateRoundEndpoint(int leagueId, int seasonId, int round = 0, int numberOfSimulations = 1000)
    {
        if (round < 0 || round > Constants.NumberOfRoundsEkstaklasa)
            return null;

        List<Match> matchesToSimulate = [];
        if (round == 0)
        {
            var lastMatch = await _context.Matches
            .AsNoTracking()
            .Where(x => x.LeagueId == leagueId && x.SeasonId == seasonId && x.HomeTeamScore != null && x.AwayTeamScore != null)
            .OrderByDescending(x => x.Date)
            .FirstOrDefaultAsync();

            if (lastMatch?.Round is int roundLastMatch && roundLastMatch <= 33)
                round = roundLastMatch + 1;
            else
                return null;
        }

        matchesToSimulate = await _context.Matches
                .AsNoTracking()
                .Where(x => x.LeagueId == leagueId && x.SeasonId == seasonId && x.HomeTeamScore == null && x.AwayTeamScore == null && x.Round == round)
                .Include(x => x.AwayTeam)
                .Include(x => x.HomeTeam)
                .ToListAsync();

        if (!matchesToSimulate.Any())
            return null;

        var results = await SimulateRound(matchesToSimulate, leagueId, numberOfSimulations);


        var simulatedRound = new SimulatedRound
        {
            LeagueId = leagueId,
            SeasonId = seasonId,
            Round = round,
            NumberOfSimulations = numberOfSimulations
        };

        foreach(var resultMatch  in results)
        {
           simulatedRound.SimulatedMatchResults.Add(resultMatch);
        }

        _context.SimulatedRounds.Add(simulatedRound);
        await _context.SaveChangesAsync();

        return results;
    }

    public async Task SimulateRestOfTheSeason(int leagueId, int seasonId, int? currentRound = 0, int? numberOfSimulations = 1000)
    {
        Stopwatch sw = Stopwatch.StartNew();
        if (currentRound == 0)
        {
            var lastMatch = await _context.Matches
                .Where(x => x.LeagueId == leagueId && x.SeasonId == seasonId && x.HomeTeamScore != null && x.AwayTeamScore != null)
                .OrderByDescending(x => x.Date)
                .FirstOrDefaultAsync();

            if (lastMatch?.Round.GetValueOrDefault() > -Constants.NumberOfRoundsEkstaklasa)
                return;

            currentRound = lastMatch?.Round.GetValueOrDefault() + 1 ?? 1;
        }


        var teamIds = await _context.Matches
            .Where(m => m.LeagueId == leagueId && m.SeasonId == seasonId && m.Round == currentRound)
            .Select(m => m.HomeTeamId)
            .Union(
                _context.Matches
                .Where(m => m.LeagueId == leagueId && m.SeasonId == seasonId && m.Round == currentRound)
                .Select(m => m.AwayTeamId)
            )
            .Distinct()
            .ToListAsync();

        var teams = await _context.Teams
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

        _context.SimulatedFinalLeagues.Add(simulatedLeague);
        await _context.SaveChangesAsync();

        var simulatedTeams = teams.Select(t => new SimulatedTeamInFinalTable
        {
            TeamId = t.Id,
            SimulatedFinalLeagueId = simulatedLeague.Id
        }).ToList();

        var simulatedSeasons = new List<List<SimulatedTeamSeasonStats>>(numberOfSimulations.Value);

        var tasks = new List<Task>();

        for (int i = 0; i < numberOfSimulations; i++)
        {
            var currentIndex = i;
            tasks.Add(Task.Run(async () =>
            {
                await _semaphore.WaitAsync();
                try
                {
                    Console.WriteLine($"Rozpoczynam symulację dla zadaniu {currentIndex}");
                    var optionsBuilder = new DbContextOptionsBuilder<EkstraSimDbContext>();
                    optionsBuilder.UseSqlServer("Server=DESKTOP-FIU7O2T\\SQLEXPRESS;Database=EkstraSimDB;Trusted_Connection=True;TrustServerCertificate=True;");

                    Stopwatch swForEach = Stopwatch.StartNew();

                    using (var dbContext = new EkstraSimDbContext(optionsBuilder.Options))
                    {
                       
                        var teamsSimulated = teams.Select(t => new Team
                        {
                            Id = t.Id,
                            HomeMatches = t.HomeMatches.Select(hm => new Match
                            {
                                Id = hm.Id,
                                HomeTeamId = hm.HomeTeamId,
                                AwayTeamId = hm.AwayTeamId,
                                HomeTeamScore = hm.HomeTeamScore,
                                AwayTeamScore = hm.AwayTeamScore,
                                Round = hm.Round,
                                Date = hm.Date,
                                LeagueId = hm.LeagueId,
                                SeasonId = hm.SeasonId
                            }).ToList(),
                            AwayMatches = t.AwayMatches.Select(am => new Match
                            {
                                Id = am.Id,
                                HomeTeamId = am.HomeTeamId,
                                AwayTeamId = am.AwayTeamId,
                                HomeTeamScore = am.HomeTeamScore,
                                AwayTeamScore = am.AwayTeamScore,
                                Round = am.Round,
                                Date = am.Date,
                                LeagueId = am.LeagueId,
                                SeasonId = am.SeasonId
                            }).ToList()
                        }).ToList();

                        for (int round = currentRound.GetValueOrDefault(); round <= Constants.NumberOfRoundsEkstaklasa; round++)
                        {
                            var matchesToSimulate = await dbContext.Matches
                                .Include(m => m.HomeTeam)
                                .Include(m => m.AwayTeam)
                                .Where(m => m.LeagueId == leagueId && m.SeasonId == seasonId && m.Round == round)
                                .Select(m => new Match
                                {
                                    Id = m.Id,
                                    HomeTeamId = m.HomeTeamId,
                                    AwayTeamId = m.AwayTeamId,
                                    HomeTeam = m.HomeTeam,
                                    AwayTeam = m.AwayTeam,
                                    HomeTeamScore = null,
                                    AwayTeamScore = null,
                                    Round = m.Round,
                                    Date = m.Date,
                                    LeagueId = m.LeagueId,
                                    SeasonId = m.SeasonId
                                })
                                .ToListAsync()
                                .ConfigureAwait(false);

                            var response = await SimulateRoundForLeagueSim(matchesToSimulate, leagueId, dbContext);
                            teamsSimulated = await UpdateTeamAverages(response, teamsSimulated, dbContext);
                        }

                        var seasonStats = await CalculateSeasonStatsTeams(teamsSimulated, leagueId, seasonId);
                        simulatedSeasons.Add(seasonStats);
                    }

                    swForEach.Stop();
                    Console.WriteLine($"Czas przeliczania jednego symulacji ({currentIndex} symulacja): {swForEach.Elapsed}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Błąd w symulacji {currentIndex}: {ex.Message}");
                }
                finally
                {
                    _semaphore.Release(); // Zwolnij semafor
                }
            }));
        }
        try
        {
            await Task.WhenAll(tasks);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Błąd przy oczekiwaniach na wszystkie zadania: {ex.Message}");
        }
       

        foreach (var simulatedTeam in simulatedTeams)
        {
            var teamId = simulatedTeam.TeamId;

            var teamStatsAcrossSimulations = simulatedSeasons
                .SelectMany(season => season)
                .Where(stats => stats.TeamId == teamId)
                .ToList();

            simulatedTeam.AveragePoints = teamStatsAcrossSimulations.Average(stats => stats.Points);
            simulatedTeam.AverageGoalDifference = teamStatsAcrossSimulations.Average(stats => stats.GoalDifference);
            simulatedTeam.AverageGoalsScored = teamStatsAcrossSimulations.Average(stats => stats.GoalsScored);
            simulatedTeam.AverageGoalsConceded = teamStatsAcrossSimulations.Average(stats => stats.GoalsConceded);


            int totalSimulations = simulatedSeasons.Count;

            simulatedTeam.FirstPlaceProbability = (double)teamStatsAcrossSimulations.Count(stats => stats.Place == 1) / totalSimulations;
            simulatedTeam.SecondPlaceProbability = (double)teamStatsAcrossSimulations.Count(stats => stats.Place == 2) / totalSimulations;
            simulatedTeam.ThirdPlaceProbability = (double)teamStatsAcrossSimulations.Count(stats => stats.Place == 3) / totalSimulations;

            simulatedTeam.FourthPlaceProbability = (double)teamStatsAcrossSimulations.Count(stats => stats.Place == 4) / totalSimulations;
            simulatedTeam.FifthPlaceProbability = (double)teamStatsAcrossSimulations.Count(stats => stats.Place == 5) / totalSimulations;
            simulatedTeam.SixthPlaceProbability = (double)teamStatsAcrossSimulations.Count(stats => stats.Place == 6) / totalSimulations;

            simulatedTeam.SeventhPlaceProbability = (double)teamStatsAcrossSimulations.Count(stats => stats.Place == 7) / totalSimulations;
            simulatedTeam.EighthPlaceProbability = (double)teamStatsAcrossSimulations.Count(stats => stats.Place == 8) / totalSimulations;
            simulatedTeam.NinthPlaceProbability = (double)teamStatsAcrossSimulations.Count(stats => stats.Place == 9) / totalSimulations;

            simulatedTeam.TenthPlaceProbability = (double)teamStatsAcrossSimulations.Count(stats => stats.Place == 10) / totalSimulations;
            simulatedTeam.EleventhPlaceProbability = (double)teamStatsAcrossSimulations.Count(stats => stats.Place == 11) / totalSimulations;
            simulatedTeam.TwelfthPlaceProbability = (double)teamStatsAcrossSimulations.Count(stats => stats.Place == 12) / totalSimulations;

            simulatedTeam.ThirteenthPlaceProbability = (double)teamStatsAcrossSimulations.Count(stats => stats.Place == 13) / totalSimulations;
            simulatedTeam.FourteenthPlaceProbability = (double)teamStatsAcrossSimulations.Count(stats => stats.Place == 14) / totalSimulations;
            simulatedTeam.FifteenthPlaceProbability = (double)teamStatsAcrossSimulations.Count(stats => stats.Place == 15) / totalSimulations;

            simulatedTeam.SixteenthPlaceProbability = (double)teamStatsAcrossSimulations.Count(stats => stats.Place == 16) / totalSimulations;
            simulatedTeam.SeventeenthPlaceProbability = (double)teamStatsAcrossSimulations.Count(stats => stats.Place == 17) / totalSimulations;
            simulatedTeam.EighteenthPlaceProbability = (double)teamStatsAcrossSimulations.Count(stats => stats.Place == 18) / totalSimulations;

            // Oblicz prawdopodobieństwa wybranych wyników
            simulatedTeam.TopFourProbability = (double)teamStatsAcrossSimulations.Count(stats => stats.Place <= 4) / totalSimulations;
            simulatedTeam.RelegationProbability = (double)teamStatsAcrossSimulations.Count(stats => stats.Place >= 16) / totalSimulations;
        }

        _context.SimulatedTeamInFinalTables.AddRange(simulatedTeams);
        await _context.SaveChangesAsync();

        simulatedLeague.Teams = simulatedTeams;
        sw.Stop();
        Console.WriteLine($"Czas przeliczania jednego sezonu ({numberOfSimulations} symulacji):{sw.Elapsed.ToString()}");
    }

    private async Task<List<SimulatedTeamSeasonStats>> CalculateSeasonStatsTeams(List<Team> teams, int leagueId, int seasonId)
    {
        Stopwatch sw = Stopwatch.StartNew();

        var allMatches = teams.SelectMany(t => t.HomeMatches)
            .Where(m => m.LeagueId == leagueId && m.SeasonId == seasonId)
            .ToList();



        List<SimulatedTeamSeasonStats> listOfTeamsStats = [];
        foreach (var team in teams)
        {
            var matchesHomeTeamSeason = allMatches.Where(m => m.HomeTeamId == team.Id).ToList();

            var matchesAwayTeamSeason = allMatches.Where(m => m.AwayTeamId == team.Id).ToList();

            var goalsScored = matchesHomeTeamSeason.Sum(m => m.HomeTeamScore.GetValueOrDefault()) +
                         matchesAwayTeamSeason.Sum(m => m.AwayTeamScore.GetValueOrDefault());

            var goalsConceded = matchesHomeTeamSeason.Sum(m => m.AwayTeamScore.GetValueOrDefault()) +
                                matchesAwayTeamSeason.Sum(m => m.HomeTeamScore.GetValueOrDefault());

            var wins = matchesHomeTeamSeason.Count(m => m.HomeTeamScore > m.AwayTeamScore) +
                       matchesAwayTeamSeason.Count(m => m.AwayTeamScore > m.HomeTeamScore);

            var awayWins = matchesAwayTeamSeason.Count(m => m.AwayTeamScore > m.HomeTeamScore);

            var draws = matchesHomeTeamSeason.Count(m => m.HomeTeamScore == m.AwayTeamScore) +
                        matchesAwayTeamSeason.Count(m => m.HomeTeamScore == m.AwayTeamScore);


            listOfTeamsStats.Add(new SimulatedTeamSeasonStats
            {
                TeamId = team.Id,
                GoalsScored = goalsScored,
                GoalsConceded = goalsConceded,
                Wins = wins,
                AwayWins = awayWins,
                Points = wins * 3 + draws
            });
        }

        listOfTeamsStats.Sort(new TeamStatsComparer(allMatches));

        for (int i = 0; i < listOfTeamsStats.Count; i++)
        {
            listOfTeamsStats[i].Place = i + 1;
        }

        sw.Stop();
        Console.WriteLine($"Czas przeliczania jednego sezonu (tabela):{sw.Elapsed.ToString()}");
        return listOfTeamsStats;
    }

    public class SimulatedTeamSeasonStats
    {
        public int TeamId { get; set; }
        public double Points { get; set; } = 0;
        public double GoalDifference => GoalsScored - GoalsConceded;
        public double GoalsScored { get; set; } = 0;
        public double GoalsConceded { get; set; } = 0;
        public int Wins { get; set; } = 0;
        public int AwayWins { get; set; } = 0;
        public int Place { get; set; }
    }

    private async Task<List<Team>> UpdateTeamAverages(IEnumerable<SimulatedMatchResult> simulatedRoundResults, List<Team> teams, EkstraSimDbContext ekstraSimDbContext)
    {
        var matchIds = simulatedRoundResults.Select(r => r.MatchId).ToList();

        var matches = await ekstraSimDbContext.Matches
            .AsNoTracking()
            .Where(m => matchIds.Contains(m.Id))
            .Include(m => m.HomeTeam)
            .Include(m => m.AwayTeam)
            .ToListAsync();

        var matchesDict = matches.ToDictionary(m => m.Id);
        var teamsDict = teams.ToDictionary(t => t.Id);

        foreach (var matchResult in simulatedRoundResults)
        {
            if (!matchesDict.TryGetValue(matchResult.MatchId, out var match))
                continue;

            if (!teamsDict.TryGetValue(match.HomeTeamId, out var homeTeam) || !teamsDict.TryGetValue(match.AwayTeamId, out var awayTeam))
                continue;

            if (homeTeam != null)
            {
                var homeMatchesCount = homeTeam.HomeMatches.Count(m => m.AwayTeamScore != null && m.HomeTeamScore != null);

                homeTeam.AverageHomeGoalsScoredCurrentSeason = UpdateAverage(homeTeam.AverageHomeGoalsScoredCurrentSeason, homeMatchesCount, matchResult.PredictedHomeScore);
                homeTeam.AverageHomeGoalsConcededCurrentSeason = UpdateAverage(homeTeam.AverageHomeGoalsConcededCurrentSeason, homeMatchesCount, matchResult.PredictedAwayScore);

                var matchHome = homeTeam.HomeMatches.FirstOrDefault(m => m.Id == match.Id);
                if (matchHome != null)
                {
                    matchHome.HomeTeamScore = matchResult.PredictedHomeScore;
                    matchHome.AwayTeamScore = matchResult.PredictedAwayScore;
                }
            }

            if (awayTeam != null)
            {
                var awayMatchesCount = awayTeam.AwayMatches.Count(m => m.AwayTeamScore != null && m.HomeTeamScore != null);

                awayTeam.AverageAwayGoalsScoredCurrentSeason = UpdateAverage(awayTeam.AverageAwayGoalsScoredCurrentSeason, awayMatchesCount, matchResult.PredictedAwayScore);
                awayTeam.AverageAwayGoalsConcededCurrentSeason = UpdateAverage(awayTeam.AverageAwayGoalsConcededCurrentSeason, awayMatchesCount, matchResult.PredictedHomeScore);

                var matchAway = awayTeam.AwayMatches.FirstOrDefault(m => m.Id == match.Id);
                if (matchAway != null)
                {
                    matchAway.HomeTeamScore = matchResult.PredictedHomeScore;
                    matchAway.AwayTeamScore = matchResult.PredictedAwayScore;
                }
            }
        }
        return teams;
    }

    private double UpdateAverage(double? currentAverage, int currentCount, int newValue)
    {
        return ((currentAverage ?? 0) * currentCount + newValue) / (currentCount + 1);
    }
}

