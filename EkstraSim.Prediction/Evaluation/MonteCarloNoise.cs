using EkstraSim.Prediction.Metrics;
using EkstraSim.Prediction.Models;

namespace EkstraSim.Prediction.Evaluation;

public sealed class MonteCarloNoiseResult
{
    public int SimulationsPerMatch { get; init; }
    public int Replications { get; init; }

    public IReadOnlyDictionary<string, double> ExactMeans { get; init; } = new Dictionary<string, double>();
    public IReadOnlyDictionary<string, IReadOnlyList<double>> SimulatedMeans { get; init; } = new Dictionary<string, IReadOnlyList<double>>();

    public double MeanShift(string modelName) => SimulatedMeans[modelName].Average() - ExactMeans[modelName];

    public double StandardDeviation(string modelName)
    {
        var means = SimulatedMeans[modelName];
        var average = means.Average();
        return Math.Sqrt(means.Average(mean => (mean - average) * (mean - average)));
    }

    public double ReversalRate(string first, string second)
    {
        var exactSign = Math.Sign(ExactMeans[first] - ExactMeans[second]);
        if (exactSign == 0)
        {
            throw new ArgumentException($"Dokładne średnie modeli {first} i {second} są równe, więc kolejności nie da się odwrócić.", nameof(second));
        }

        return SimulatedMeans[first]
            .Zip(SimulatedMeans[second], (firstMean, secondMean) => Math.Sign(firstMean - secondMean) != exactSign ? 1.0 : 0.0)
            .Average();
    }
}

public static class MonteCarloNoise
{
    public static MonteCarloNoiseResult Run(
        IReadOnlyDictionary<string, IReadOnlyList<MatchPrediction>> predictionsByModel,
        IReadOnlyList<MatchData> matches,
        int simulationsPerMatch,
        int replications,
        int seed)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(simulationsPerMatch, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(replications, 1);

        var actualById = matches
            .Where(m => m.IsPlayed)
            .ToDictionary(m => m.Id, m => PredictionMetrics.OutcomeOf(m.HomeScore!.Value, m.AwayScore!.Value));

        var models = predictionsByModel
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => (Name: pair.Key, Predictions: pair.Value.OrderBy(p => p.MatchId).ToList()))
            .ToList();

        var missing = models.SelectMany(model => model.Predictions).FirstOrDefault(p => !actualById.ContainsKey(p.MatchId));
        if (missing != null)
        {
            throw new ArgumentException($"Brak rozegranego meczu {missing.MatchId} (model {missing.ModelName}).", nameof(matches));
        }

        var exactMeans = models.ToDictionary(
            model => model.Name,
            model => model.Predictions.Average(p => PredictionMetrics.RankedProbabilityScore(
                new OutcomeProbabilities(p.HomeWinProbability, p.DrawProbability, p.AwayWinProbability),
                actualById[p.MatchId])));

        var withoutDistribution = models
            .SelectMany(model => model.Predictions)
            .FirstOrDefault(p => p.ScoreProbabilities.Length == 0 || !(ScoreGrid.Sum(p.ScoreProbabilities) > 0));
        if (withoutDistribution != null)
        {
            throw new ArgumentException($"Prognoza meczu {withoutDistribution.MatchId} (model {withoutDistribution.ModelName}) nie ma rozkładu wyniku.", nameof(predictionsByModel));
        }

        var cumulativeByModel = models.ToDictionary(
            model => model.Name,
            model => model.Predictions.Select(p => Cumulative(p.ScoreProbabilities)).ToList());

        var simulatedMeans = models.ToDictionary(model => model.Name, _ => new List<double>());
        var random = new Random(seed);

        for (var replication = 0; replication < replications; replication++)
        {
            foreach (var (name, predictions) in models)
            {
                var cumulative = cumulativeByModel[name];
                var total = 0.0;

                for (var i = 0; i < predictions.Count; i++)
                {
                    var sampled = Sample(cumulative[i], simulationsPerMatch, random);
                    total += PredictionMetrics.RankedProbabilityScore(sampled, actualById[predictions[i].MatchId]);
                }

                simulatedMeans[name].Add(total / predictions.Count);
            }
        }

        return new MonteCarloNoiseResult
        {
            SimulationsPerMatch = simulationsPerMatch,
            Replications = replications,
            ExactMeans = exactMeans,
            SimulatedMeans = simulatedMeans.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<double>)pair.Value)
        };
    }

    private static (double[] Bounds, MatchOutcome[] Outcomes) Cumulative(double[,] grid)
    {
        var bounds = new double[grid.Length];
        var outcomes = new MatchOutcome[grid.Length];
        var running = 0.0;
        var index = 0;

        for (var home = 0; home < grid.GetLength(0); home++)
        {
            for (var away = 0; away < grid.GetLength(1); away++)
            {
                running += grid[home, away];
                bounds[index] = running;
                outcomes[index] = PredictionMetrics.OutcomeOf(home, away);
                index++;
            }
        }

        return (bounds, outcomes);
    }

    private static OutcomeProbabilities Sample((double[] Bounds, MatchOutcome[] Outcomes) cumulative, int count, Random random)
    {
        var (bounds, outcomes) = cumulative;
        var total = bounds[^1];
        var counts = new int[3];

        for (var draw = 0; draw < count; draw++)
        {
            var target = random.NextDouble() * total;
            counts[(int)outcomes[FirstAbove(bounds, target)]]++;
        }

        return new OutcomeProbabilities(
            (double)counts[(int)MatchOutcome.HomeWin] / count,
            (double)counts[(int)MatchOutcome.Draw] / count,
            (double)counts[(int)MatchOutcome.AwayWin] / count);
    }

    private static int FirstAbove(double[] bounds, double target)
    {
        var low = 0;
        var high = bounds.Length - 1;

        while (low < high)
        {
            var middle = (low + high) / 2;
            if (bounds[middle] > target)
            {
                high = middle;
            }
            else
            {
                low = middle + 1;
            }
        }

        return low;
    }
}
