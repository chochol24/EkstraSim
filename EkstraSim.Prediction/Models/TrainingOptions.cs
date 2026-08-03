using Shared;

namespace EkstraSim.Prediction.Models;

public sealed class TrainingOptions
{
    public int LeagueId { get; init; }
    public int SeasonId { get; init; }

    public IReadOnlyList<int> SeasonChronology { get; init; } = [];

    public int MaxGoals { get; init; } = ScoreGrid.DefaultMaxGoals;

    public bool UseFormFactors { get; init; } = true;

    public double CurrentSeasonScale { get; init; } = Constants.CurrentSeasonScale;
    public double PreviousSeasonScale { get; init; } = Constants.PreviousSeasonScale;
    public double HistoricalScale { get; init; } = Constants.HistoricalScale;

    public double TimeDecayXi { get; init; } = 0.0065;
    public double RidgeLambda { get; init; } = 0.05;

    public double EloK { get; init; } = Constants.KValueEkstraklasa;
    public double EloInitialRating { get; init; } = 1300;
    public double EloHomeAdvantage { get; init; } = 100;

    public int? PreviousSeasonId
    {
        get
        {
            var index = SeasonChronology.ToList().IndexOf(SeasonId);
            if (index <= 0)
            {
                return null;
            }

            return SeasonChronology[index - 1];
        }
    }
}
