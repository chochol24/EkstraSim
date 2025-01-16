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

            foreach (var match in matchesToSimulate)
            {
                var homeAttackStrength = match.HomeTeam.AverageHomeGoalsScored / league.AverageHomeGoalsScored;
                var homeDefenseStrength = match.HomeTeam.AverageHomeGoalsConceded / league.AverageHomeGoalsConceded;
                var awayAttackStrength = match.AwayTeam.AverageAwayGoalsScored / league.AverageAwayGoalsScored;
                var awayDefenseStrength = match.AwayTeam.AverageAwayGoalsConceded / league.AverageAwayGoalsConceded;

                var homePred = homeAttackStrength * awayDefenseStrength * league.AverageHomeGoalsScored;
                var awayPred = awayAttackStrength * homeDefenseStrength * league.AverageAwayGoalsScored;

                predictionMatches.Add(match, new MatchPredictionGoals { HomeGoals = homePred.GetValueOrDefault(), AwayGoals = awayPred.GetValueOrDefault() });

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
                        else if (result.HomeScore < result.AwayScore)
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
        Stopwatch sw = Stopwatch.StartNew();
        int totalRounds = 34;
        if (currentRound == 0)
        {
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
        List<Team> teams = new List<Team>();
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
            Team = t,
            //SimulatedFinalLeagueId = simulatedLeague.Id,
            SimulatedFinalLeagueId = 1,

        }).ToList();

        Dictionary<int, List<SimulatedTeamSeasonStats>> simulatedSeasons = new Dictionary<int, List<SimulatedTeamSeasonStats>>();
        for (int i = 0; i < numberOfSimulations; i++)
        {
            List<Team> teamsSimulated = teams;
            for (int round = currentRound.GetValueOrDefault(); round <= totalRounds; round++)
            {
                var matchesToSimulate = await _context.Matches.Where(m => m.LeagueId == leagueId && m.SeasonId == seasonId && m.Round == round).ToListAsync();
                var response = await SimulateRoundForLeagueSim(matchesToSimulate, leagueId);
                teamsSimulated = await UpdateTeamAverages(response, teamsSimulated);
            }
            var seasonStats = await CalculateSeasonStatsTeams(teams, leagueId, seasonId);
            simulatedSeasons.Add(i, seasonStats);
        }

        foreach (var simulatedTeam in simulatedTeams)
        {
            var teamId = simulatedTeam.TeamId;

            // Wyciągnij statystyki tej drużyny z każdej symulacji
            var teamStatsAcrossSimulations = simulatedSeasons.Values
                .SelectMany(season => season)
                .Where(stats => stats.TeamId == teamId)
                .ToList();

            // Oblicz średnie
            simulatedTeam.AveragePoints = teamStatsAcrossSimulations.Average(stats => stats.Points);
            simulatedTeam.AverageGoalDifference = teamStatsAcrossSimulations.Average(stats => stats.GoalDifference);
            simulatedTeam.AverageGoalsScored = teamStatsAcrossSimulations.Average(stats => stats.GoalsScored);
            simulatedTeam.AverageGoalsConceded = teamStatsAcrossSimulations.Average(stats => stats.GoalsConceded);

            // Oblicz prawdopodobieństwo dla każdego miejsca
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

        simulatedLeague.Teams = simulatedTeams;
        foreach(var team in simulatedLeague.Teams)
        {
            Console.WriteLine($"{team.Team.Name}:");
            Console.WriteLine($"1. miejsce - {team.FirstPlaceProbability * 100:F2}%");
            Console.WriteLine($"2. miejsce - {team.SecondPlaceProbability * 100:F2}%");
            Console.WriteLine($"3. miejsce - {team.ThirdPlaceProbability * 100:F2}%");
            Console.WriteLine($"4. miejsce - {team.FourthPlaceProbability * 100:F2}%");
            Console.WriteLine($"5. miejsce - {team.FifthPlaceProbability * 100:F2}%");
            Console.WriteLine($"6. miejsce - {team.SixthPlaceProbability * 100:F2}%");
            Console.WriteLine($"7. miejsce - {team.SeventhPlaceProbability * 100:F2}%");
            Console.WriteLine($"8. miejsce - {team.EighthPlaceProbability * 100:F2}%");
            Console.WriteLine($"9. miejsce - {team.NinthPlaceProbability * 100:F2}%");
            Console.WriteLine($"10. miejsce - {team.TenthPlaceProbability * 100:F2}%");
            Console.WriteLine($"11. miejsce - {team.EleventhPlaceProbability * 100:F2}%");
            Console.WriteLine($"12. miejsce - {team.TwelfthPlaceProbability * 100:F2}%");
            Console.WriteLine($"13. miejsce - {team.ThirteenthPlaceProbability * 100:F2}%");
            Console.WriteLine($"14. miejsce - {team.FourteenthPlaceProbability * 100:F2}%");
            Console.WriteLine($"15. miejsce - {team.FifteenthPlaceProbability * 100:F2}%");
            Console.WriteLine($"16. miejsce - {team.SixteenthPlaceProbability * 100:F2}%");
            Console.WriteLine($"17. miejsce - {team.SeventeenthPlaceProbability * 100:F2}%");
            Console.WriteLine($"18. miejsce - {team.EighteenthPlaceProbability * 100:F2}%");
            Console.WriteLine("");
            Console.WriteLine($"1-4. miejsce - {team.TopFourProbability * 100:F2}%");
            Console.WriteLine($"Spadek - {team.RelegationProbability * 100:F2}%");
            Console.WriteLine("");
            Console.WriteLine($"Średnia ilość punktów - {team.AveragePoints}");
            Console.WriteLine($"Średnia ilość bramek strzelonych - {team.AverageGoalsScored}");
            Console.WriteLine($"Średnia ilość bramek straconych - {team.AverageGoalsConceded}");
            Console.WriteLine($"Średnia różnica bramek  - {team.AverageGoalDifference}");
        }
        sw.Stop();
        Console.WriteLine($"Czas przeliczania jednego sezonu ({numberOfSimulations} symulacji):{sw.Elapsed.ToString()}");
        var x = 3;
    }

    private async Task<List<SimulatedTeamSeasonStats>> CalculateSeasonStatsTeams(List<Team> teams, int leagueId, int seasonId)
    {
        Stopwatch sw = Stopwatch.StartNew();
        List<SimulatedTeamSeasonStats> listOfTeamsStats = new List<SimulatedTeamSeasonStats>();
        foreach (var team in teams)
        {
            var matchesHomeTeamSeason = team.HomeMatches
                .Where(x => x.LeagueId == leagueId && x.SeasonId == seasonId)
                .ToList();

            var matchesAwayTeamSeason = team.AwayMatches
                .Where(x => x.LeagueId == leagueId && x.SeasonId == seasonId)
                .ToList();

            SimulatedTeamSeasonStats simulatedTeamSeasonStats = new SimulatedTeamSeasonStats
            {
                TeamId = team.Id,
                GoalsScored = matchesHomeTeamSeason.Sum(m => m.HomeTeamScore.GetValueOrDefault()) +
                              matchesAwayTeamSeason.Sum(m => m.AwayTeamScore.GetValueOrDefault()),
                GoalsConceded = matchesHomeTeamSeason.Sum(m => m.AwayTeamScore.GetValueOrDefault()) +
                                matchesAwayTeamSeason.Sum(m => m.HomeTeamScore.GetValueOrDefault()),
                Wins = matchesHomeTeamSeason.Count(m => m.HomeTeamScore > m.AwayTeamScore) +
                       matchesAwayTeamSeason.Count(m => m.AwayTeamScore > m.HomeTeamScore),
                AwayWins = matchesAwayTeamSeason.Count(m => m.AwayTeamScore > m.HomeTeamScore)
            };

            simulatedTeamSeasonStats.Points = simulatedTeamSeasonStats.Wins * 3 +
                                              matchesHomeTeamSeason.Count(m => m.HomeTeamScore == m.AwayTeamScore) +
                                              matchesAwayTeamSeason.Count(m => m.HomeTeamScore == m.AwayTeamScore);

            listOfTeamsStats.Add(simulatedTeamSeasonStats);
        }

        var allMatches = teams.SelectMany(t => t.HomeMatches)
                          .Concat(teams.SelectMany(t => t.AwayMatches))
                          .Where(m => m.LeagueId == leagueId && m.SeasonId == seasonId)
                          .ToList();

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

    private async Task<List<Team>> UpdateTeamAverages(IEnumerable<SimulatedMatchResult> simulatedRoundResults, List<Team> teams)
    {
        foreach (var matchResult in simulatedRoundResults)
        {
            var match = await _context.Matches.Where(x => x.Id == matchResult.MatchId).Include(m => m.HomeTeam).Include(m => m.AwayTeam).FirstOrDefaultAsync();

            var homeTeam = teams.Where(x => x.Id == match.HomeTeamId).FirstOrDefault();
            var awayTeam = teams.Where(x => x.Id == match.AwayTeamId).FirstOrDefault();
            int homeMatchesCount = homeTeam.HomeMatches.Where(x => x.AwayTeamScore != null && x.HomeTeamScore != null).Count();
            int awayMatchesCount = awayTeam.AwayMatches.Where(x => x.AwayTeamScore != null && x.HomeTeamScore != null).Count();

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

