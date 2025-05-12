using EkstraSim.Backend.Database.Entities;

namespace EkstraSim.Backend.Database.Services;

public partial class SimulatingService
{
    public static class TeamStatsComparer
    {
        public static List<SimulatedTeamSeasonStats> SortSeason(List<SimulatedTeamSeasonStats> stats, List<Match> allMatches)
        {
            var grouped = stats
                .GroupBy(s => s.Points)
                .OrderByDescending(g => g.Key);

            var finalSorted = new List<SimulatedTeamSeasonStats>();
            foreach (var group in grouped)
            {
                var tieGroup = group.ToList();
                if (tieGroup.Count == 1)
                {
                    finalSorted.Add(tieGroup[0]);
                }
                else
                {
                    var sortedSubgroup = SortTieGroup(tieGroup, allMatches);
                    finalSorted.AddRange(sortedSubgroup);
                }
            }

            for (int i = 0; i < finalSorted.Count; i++)
                finalSorted[i].Place = i + 1;

            return finalSorted;
        }

        private static List<SimulatedTeamSeasonStats> SortTieGroup(List<SimulatedTeamSeasonStats> tieGroup, List<Match> allMatches)
        {
           var headToHeadMatches = allMatches
                .Where(m => tieGroup.Any(t => t.TeamId == m.HomeTeamId)
                         && tieGroup.Any(t => t.TeamId == m.AwayTeamId))
                .ToList();

            var miniStats = tieGroup.Select(team => new
            {
                Team = team,
                H2HPoints = CalculatePointsInMatches(team.TeamId, headToHeadMatches),
                H2HGoalDiff = CalculateGoalDifference(team.TeamId, headToHeadMatches),
                H2HGoalsScored = CalculateGoalsScored(team.TeamId, headToHeadMatches)
            }).ToList();

            bool isPair = miniStats.Count == 2;

            var ordered = miniStats
                .OrderByDescending(s => s.H2HPoints)
                .ThenByDescending(s => s.H2HGoalDiff);

            if (!isPair)
                ordered = ordered.ThenByDescending(s => s.H2HGoalsScored);

            ordered = ordered
                .ThenByDescending(s => s.Team.GoalDifference)
                .ThenByDescending(s => s.Team.GoalsScored)
                .ThenByDescending(s => s.Team.Wins)
                .ThenByDescending(s => s.Team.AwayWins);

            ordered = ordered
                .ThenBy(s => Guid.NewGuid());

            return ordered.Select(s => s.Team).ToList();
        }

        private static int CalculatePointsInMatches(int teamId, List<Match> matches)
        {
            int points = 0;
            foreach (var m in matches)
            {
                if (m.HomeTeamId == teamId)
                {
                    if (m.HomeTeamScore > m.AwayTeamScore) points += 3;
                    else if (m.HomeTeamScore == m.AwayTeamScore) points += 1;
                }
                else if (m.AwayTeamId == teamId)
                {
                    if (m.AwayTeamScore > m.HomeTeamScore) points += 3;
                    else if (m.AwayTeamScore == m.HomeTeamScore) points += 1;
                }
            }
            return points;
        }

        private static int CalculateGoalDifference(int teamId, List<Match> matches)
        {
            int diff = 0;
            foreach (var m in matches)
            {
                if (m.HomeTeamId == teamId)
                    diff += m.HomeTeamScore.GetValueOrDefault() - m.AwayTeamScore.GetValueOrDefault();
                else if (m.AwayTeamId == teamId)
                    diff += m.AwayTeamScore.GetValueOrDefault() - m.HomeTeamScore.GetValueOrDefault();
            }
            return diff;
        }

        private static int CalculateGoalsScored(int teamId, List<Match> matches)
        {
            int goals = 0;
            foreach (var m in matches)
            {
                if (m.HomeTeamId == teamId)
                    goals += m.HomeTeamScore.GetValueOrDefault();
                else if (m.AwayTeamId == teamId)
                    goals += m.AwayTeamScore.GetValueOrDefault();
            }
            return goals;
        }

    }

}

