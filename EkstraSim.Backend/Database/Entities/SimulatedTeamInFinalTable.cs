namespace EkstraSim.Backend.Database.Entities;

public class SimulatedTeamInFinalTable
{
	public int Id { get; set; }
	public int TeamId { get; set; }
	public Team Team { get; set; } = null!;

	public int SimulatedFinalLeagueId { get; set; }
	public SimulatedFinalLeague SimulatedFinalLeague { get; set; } = null!;

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
}
