using EkstraSim.Backend.Database.Entities;

namespace EkstraSim.Backend.Database.Services;

public partial class SimulatingService
{
    public class TeamStatsComparer : IComparer<SimulatedTeamSeasonStats>
    {
        private readonly List<Match> _allMatches;

        public TeamStatsComparer(List<Match> allMatches)
        {
            _allMatches = allMatches;
        }

        public int Compare(SimulatedTeamSeasonStats x, SimulatedTeamSeasonStats y)
        {
            //1. Punkty
            int result = y.Points.CompareTo(x.Points);
            if (result != 0)
                return result;

            //2. Punkty w bezpośrednich meczach
            var directMatches = GetDirectMatches(x.TeamId, y.TeamId);
            var xDirectPoints = CalculatePointsInMatches(x.TeamId, directMatches);
            var yDirectPoints = CalculatePointsInMatches(y.TeamId, directMatches);

            result = yDirectPoints.CompareTo(xDirectPoints);
            if (result != 0)
                return result;

            //3. Różnica bramek w bezpośrednich meczach 
            var xDirectGoalDifference = CalculateGoalDifference(x.TeamId, directMatches);
            var yDirectGoalDifference = CalculateGoalDifference(y.TeamId, directMatches);

            result = yDirectGoalDifference.CompareTo(xDirectGoalDifference);
            if (result != 0)
                return result;

            //4. Różnica bramek w całym sezonie
            result = y.GoalDifference.CompareTo(x.GoalDifference);
            if (result != 0)
                return result;

            //5. Liczba bramek w sezonie
            result = y.GoalsScored.CompareTo(x.GoalsScored);
            if (result != 0)
                return result;

            //6. Liczba wygranych w sezonie
            result = y.Wins.CompareTo(x.Wins);
            if (result != 0)
                return result;

            //7. Liczba zwycięstw na wyjeździe
            result = y.AwayWins.CompareTo(x.AwayWins);
            return result;

        }
        private List<Match> GetDirectMatches(int team1Id, int team2Id)
        {
            return _allMatches
                .Where(m => (m.HomeTeamId == team1Id && m.AwayTeamId == team2Id) ||
                            (m.HomeTeamId == team2Id && m.AwayTeamId == team1Id))
                .ToList();
        }

        private int CalculatePointsInMatches(int teamId, List<Match> matches)
        {
            int points = 0;
            foreach (var match in matches)
            {
                if (match.HomeTeamId == teamId)
                {
                    if (match.HomeTeamScore > match.AwayTeamScore) points += 3;
                    else if (match.HomeTeamScore == match.AwayTeamScore) points += 1;
                }
                else if (match.AwayTeamId == teamId)
                {
                    if (match.AwayTeamScore > match.HomeTeamScore) points += 3;
                    else if (match.AwayTeamScore == match.HomeTeamScore) points += 1;
                }
            }
            return points;
        }

        private int CalculateGoalDifference(int teamId, List<Match> matches)
        {
            int goalDifference = 0;
            foreach (var match in matches)
            {
                if (match.HomeTeamId == teamId)
                    goalDifference += match.HomeTeamScore.GetValueOrDefault() - match.AwayTeamScore.GetValueOrDefault();
                else if (match.AwayTeamId == teamId)
                    goalDifference += match.AwayTeamScore.GetValueOrDefault() - match.HomeTeamScore.GetValueOrDefault();
            }
            return goalDifference;
        }
    }
}

