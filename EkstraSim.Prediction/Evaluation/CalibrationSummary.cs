using EkstraSim.Prediction.Models;

namespace EkstraSim.Prediction.Evaluation;

public sealed record ReliabilityBin(double From, double To, int Count, double MeanForecast, double ObservedFrequency);

public sealed class CalibrationSummary
{
    public static readonly IReadOnlyList<double> DefaultBinEdges = [0, 0.25, 0.40, 0.55, 0.70, 1];

    public string ModelName { get; init; } = string.Empty;
    public int MatchCount { get; init; }

    public double MeanExpectedHomeGoals { get; init; }
    public double MeanExpectedAwayGoals { get; init; }
    public double MeanHomeGoals { get; init; }
    public double MeanAwayGoals { get; init; }

    public double HomeGoalsSlope { get; init; }
    public double AwayGoalsSlope { get; init; }

    public double ExpectedGoalDifferenceStandardDeviation { get; init; }
    public double MaxExpectedHomeGoals { get; init; }
    public int MatchesAboveThreeExpectedHomeGoals { get; init; }

    public double MeanHomeWinProbability { get; init; }
    public double MeanDrawProbability { get; init; }
    public double MeanAwayWinProbability { get; init; }

    public double HomeWinFrequency { get; init; }
    public double DrawFrequency { get; init; }
    public double AwayWinFrequency { get; init; }

    public IReadOnlyList<ReliabilityBin> HomeWinReliability { get; init; } = [];
    public IReadOnlyList<ReliabilityBin> AwayWinReliability { get; init; } = [];

    public static CalibrationSummary From(
        IReadOnlyList<MatchPrediction> predictions,
        IReadOnlyList<MatchData> matches,
        IReadOnlyList<double>? binEdges = null)
    {
        binEdges ??= DefaultBinEdges;

        if (predictions.Count == 0)
        {
            throw new ArgumentException("Brak prognoz do kalibracji.", nameof(predictions));
        }

        if (binEdges.Count < 2 || binEdges.Zip(binEdges.Skip(1)).Any(pair => pair.Second <= pair.First))
        {
            throw new ArgumentException("Granice przedziałów muszą rosnąć i być co najmniej dwie.", nameof(binEdges));
        }

        var duplicate = predictions.GroupBy(p => p.MatchId).FirstOrDefault(group => group.Count() > 1);
        if (duplicate != null)
        {
            throw new ArgumentException($"Mecz {duplicate.Key} ma więcej niż jedną prognozę.", nameof(predictions));
        }

        var matchesById = matches.Where(m => m.IsPlayed).ToDictionary(m => m.Id);
        var missing = predictions.FirstOrDefault(p => !matchesById.ContainsKey(p.MatchId));
        if (missing != null)
        {
            throw new ArgumentException($"Brak rozegranego meczu {missing.MatchId} (model {missing.ModelName}).", nameof(matches));
        }

        var pairs = predictions.Select(p => (Prediction: p, Match: matchesById[p.MatchId])).ToList();

        var expectedHome = pairs.Select(pair => pair.Prediction.ExpectedHomeGoals).ToList();
        var expectedAway = pairs.Select(pair => pair.Prediction.ExpectedAwayGoals).ToList();
        var homeGoals = pairs.Select(pair => (double)pair.Match.HomeScore!.Value).ToList();
        var awayGoals = pairs.Select(pair => (double)pair.Match.AwayScore!.Value).ToList();

        var homeWins = pairs.Select(pair => pair.Match.HomeScore > pair.Match.AwayScore).ToList();
        var awayWins = pairs.Select(pair => pair.Match.HomeScore < pair.Match.AwayScore).ToList();

        return new CalibrationSummary
        {
            ModelName = predictions[0].ModelName,
            MatchCount = pairs.Count,
            MeanExpectedHomeGoals = expectedHome.Average(),
            MeanExpectedAwayGoals = expectedAway.Average(),
            MeanHomeGoals = homeGoals.Average(),
            MeanAwayGoals = awayGoals.Average(),
            HomeGoalsSlope = Slope(expectedHome, homeGoals),
            AwayGoalsSlope = Slope(expectedAway, awayGoals),
            ExpectedGoalDifferenceStandardDeviation = PopulationStandardDeviation(expectedHome.Zip(expectedAway, (home, away) => home - away).ToList()),
            MaxExpectedHomeGoals = expectedHome.Max(),
            MatchesAboveThreeExpectedHomeGoals = expectedHome.Count(lambda => lambda > 3),
            MeanHomeWinProbability = pairs.Average(pair => pair.Prediction.HomeWinProbability),
            MeanDrawProbability = pairs.Average(pair => pair.Prediction.DrawProbability),
            MeanAwayWinProbability = pairs.Average(pair => pair.Prediction.AwayWinProbability),
            HomeWinFrequency = homeWins.Average(won => won ? 1.0 : 0.0),
            DrawFrequency = pairs.Average(pair => pair.Match.HomeScore == pair.Match.AwayScore ? 1.0 : 0.0),
            AwayWinFrequency = awayWins.Average(won => won ? 1.0 : 0.0),
            HomeWinReliability = Reliability(pairs.Select(pair => pair.Prediction.HomeWinProbability).ToList(), homeWins, binEdges),
            AwayWinReliability = Reliability(pairs.Select(pair => pair.Prediction.AwayWinProbability).ToList(), awayWins, binEdges)
        };
    }

    private static double Slope(IReadOnlyList<double> forecast, IReadOnlyList<double> observed)
    {
        var forecastMean = forecast.Average();
        var observedMean = observed.Average();

        var covariance = forecast.Zip(observed, (x, y) => (x - forecastMean) * (y - observedMean)).Sum();
        var variance = forecast.Sum(x => (x - forecastMean) * (x - forecastMean));

        return variance > 0 ? covariance / variance : double.NaN;
    }

    private static double PopulationStandardDeviation(IReadOnlyList<double> values)
    {
        var mean = values.Average();
        return Math.Sqrt(values.Average(value => (value - mean) * (value - mean)));
    }

    private static IReadOnlyList<ReliabilityBin> Reliability(
        IReadOnlyList<double> forecasts,
        IReadOnlyList<bool> outcomes,
        IReadOnlyList<double> binEdges)
    {
        var outside = forecasts.Where(forecast => !(forecast >= binEdges[0] && forecast <= binEdges[^1])).ToList();
        if (outside.Count > 0)
        {
            throw new ArgumentException($"Prognoza {outside[0]} leży poza przedziałami [{binEdges[0]}, {binEdges[^1]}].", nameof(binEdges));
        }

        var bins = new List<ReliabilityBin>();

        for (var i = 0; i < binEdges.Count - 1; i++)
        {
            var from = binEdges[i];
            var to = binEdges[i + 1];
            var isLast = i == binEdges.Count - 2;

            var members = forecasts
                .Select((forecast, index) => (Forecast: forecast, Outcome: outcomes[index]))
                .Where(item => item.Forecast >= from && (item.Forecast < to || (isLast && item.Forecast <= to)))
                .ToList();

            bins.Add(new ReliabilityBin(
                from,
                to,
                members.Count,
                members.Count > 0 ? members.Average(item => item.Forecast) : double.NaN,
                members.Count > 0 ? members.Average(item => item.Outcome ? 1.0 : 0.0) : double.NaN));
        }

        return bins;
    }
}
