using System.Numerics;

namespace EkstraSim.Shared.DTOs;

public class SimulatedTeamInFinalTableDTO
{
    public int Id { get; set; }
    public int TeamId { get; set; }
    public TeamDTO Team { get; set; } = null!;

    public int SimulatedFinalLeagueId { get; set; }
    public SimulatedFinalLeagueDTO SimulatedFinalLeague { get; set; } = null!;

    public double AveragePoints { get; set; }
    public double AverageGoalDifference { get; set; }
    public double AverageGoalsScored { get; set; }
    public double AverageGoalsConceded { get; set; }

    public double FirstPlaceProbability { get; set; }
    public double SecondPlaceProbability { get; set; }
    public double ThirdPlaceProbability { get; set; }
    public double FourthPlaceProbability { get; set; }
    public double FifthPlaceProbability { get; set; }
    public double SixthPlaceProbability { get; set; }
    public double SeventhPlaceProbability { get; set; }
    public double EighthPlaceProbability { get; set; }
    public double NinthPlaceProbability { get; set; }
    public double TenthPlaceProbability { get; set; }
    public double EleventhPlaceProbability { get; set; }
    public double TwelfthPlaceProbability { get; set; }
    public double ThirteenthPlaceProbability { get; set; }
    public double FourteenthPlaceProbability { get; set; }
    public double FifteenthPlaceProbability { get; set; }
    public double SixteenthPlaceProbability { get; set; }
    public double SeventeenthPlaceProbability { get; set; }
    public double EighteenthPlaceProbability { get; set; }

    public double TopFourProbability { get; set; }
    public double RelegationProbability { get; set; }

    public int Place { get; set; }

    public Dictionary<int, double> PlacesDistribution
    {
        get
        {
            return new Dictionary<int, double>
        {
            { 1, FirstPlaceProbability },
            { 2, SecondPlaceProbability },
            { 3, ThirdPlaceProbability },
            { 4, FourthPlaceProbability },
            { 5, FifthPlaceProbability },
            { 6, SixthPlaceProbability },
            { 7, SeventhPlaceProbability },
            { 8, EighthPlaceProbability },
            { 9, NinthPlaceProbability },
            { 10, TenthPlaceProbability },
            { 11, EleventhPlaceProbability },
            { 12, TwelfthPlaceProbability },
            { 13, ThirteenthPlaceProbability },
            { 14, FourteenthPlaceProbability },
            { 15, FifteenthPlaceProbability },
            { 16, SixteenthPlaceProbability },
            { 17, SeventeenthPlaceProbability },
            { 18, EighteenthPlaceProbability }
        };
        }
    }
}
