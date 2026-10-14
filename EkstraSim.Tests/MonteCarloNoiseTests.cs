using EkstraSim.Prediction.Evaluation;
using EkstraSim.Prediction.Metrics;
using EkstraSim.Prediction.Models;

namespace EkstraSim.Tests;

public class MonteCarloNoiseTests
{
    [Fact]
    public void ExactMeansAreTheRankedProbabilityScoresOfThePredictions()
    {
        var (predictions, matches) = TwoModels();

        var result = MonteCarloNoise.Run(predictions, matches, simulationsPerMatch: 10, replications: 2, seed: 7);

        foreach (var (name, modelPredictions) in predictions)
        {
            var expected = modelPredictions.Average(p => PredictionMetrics.RankedProbabilityScore(
                new OutcomeProbabilities(p.HomeWinProbability, p.DrawProbability, p.AwayWinProbability),
                Outcome(matches.Single(m => m.Id == p.MatchId))));

            Assert.Equal(expected, result.ExactMeans[name], 15);
            Assert.Equal(2, result.SimulatedMeans[name].Count);
        }
    }

    [Fact]
    public void SameSeedGivesTheSameSimulation()
    {
        var (predictions, matches) = TwoModels();

        var first = MonteCarloNoise.Run(predictions, matches, simulationsPerMatch: 50, replications: 5, seed: 11);
        var second = MonteCarloNoise.Run(predictions, matches, simulationsPerMatch: 50, replications: 5, seed: 11);
        var other = MonteCarloNoise.Run(predictions, matches, simulationsPerMatch: 50, replications: 5, seed: 12);

        Assert.Equal(first.SimulatedMeans["A"], second.SimulatedMeans["A"]);
        Assert.Equal(first.SimulatedMeans["B"], second.SimulatedMeans["B"]);
        Assert.NotEqual(first.SimulatedMeans["A"], other.SimulatedMeans["A"]);
    }

    [Fact]
    public void ManySimulationsConvergeToTheExactScore()
    {
        var (predictions, matches) = TwoModels();

        var result = MonteCarloNoise.Run(predictions, matches, simulationsPerMatch: 200_000, replications: 1, seed: 3);

        Assert.Equal(result.ExactMeans["A"], result.SimulatedMeans["A"][0], 0.003);
        Assert.Equal(result.ExactMeans["B"], result.SimulatedMeans["B"][0], 0.003);
    }

    [Fact]
    public void CellsWithoutProbabilityAreNeverDrawn()
    {
        var match = TestLeague.Played(1, 20, 100, 200, 1, 1);
        var grid = new double[3, 3];
        grid[1, 1] = 1.0;
        var prediction = MatchPrediction.FromGrid(match, "A", 1.0, 1.0, grid);
        var predictions = new Dictionary<string, IReadOnlyList<MatchPrediction>> { ["A"] = [prediction] };

        var result = MonteCarloNoise.Run(predictions, [match], simulationsPerMatch: 1000, replications: 3, seed: 5);

        Assert.All(result.SimulatedMeans["A"], mean => Assert.Equal(0.0, mean));
        Assert.Equal(0.0, result.ExactMeans["A"]);
    }

    [Fact]
    public void ShiftSpreadAndReversalsAreComputedFromTheReplications()
    {
        var result = new MonteCarloNoiseResult
        {
            ExactMeans = new Dictionary<string, double> { ["A"] = 0.20, ["B"] = 0.21 },
            SimulatedMeans = new Dictionary<string, IReadOnlyList<double>>
            {
                ["A"] = [0.19, 0.21, 0.23, 0.21],
                ["B"] = [0.22, 0.20, 0.24, 0.22]
            }
        };

        Assert.Equal(0.01, result.MeanShift("A"), 12);
        Assert.Equal(Math.Sqrt(0.0002), result.StandardDeviation("A"), 12);
        Assert.Equal(0.25, result.ReversalRate("A", "B"), 12);
        Assert.Equal(0.25, result.ReversalRate("B", "A"), 12);
    }

    [Fact]
    public void ReversalRateNeedsDifferentExactMeans()
    {
        var result = new MonteCarloNoiseResult
        {
            ExactMeans = new Dictionary<string, double> { ["A"] = 0.21, ["B"] = 0.21 },
            SimulatedMeans = new Dictionary<string, IReadOnlyList<double>>
            {
                ["A"] = [0.20, 0.22],
                ["B"] = [0.21, 0.21]
            }
        };

        Assert.Throws<ArgumentException>(() => result.ReversalRate("A", "B"));
    }

    [Fact]
    public void PredictionsWithoutAScoreDistributionAreRejected()
    {
        var match = TestLeague.Played(1, 20, 100, 200, 1, 1);
        var empty = new MatchPrediction { MatchId = 1, ModelName = "A" };
        var zero = MatchPrediction.FromGrid(match, "A", 1.0, 1.0, new double[3, 3]);

        Assert.Throws<ArgumentException>(() => MonteCarloNoise.Run(new Dictionary<string, IReadOnlyList<MatchPrediction>> { ["A"] = [empty] }, [match], 10, 1, 1));
        Assert.Throws<ArgumentException>(() => MonteCarloNoise.Run(new Dictionary<string, IReadOnlyList<MatchPrediction>> { ["A"] = [zero] }, [match], 10, 1, 1));
    }

    [Fact]
    public void InvalidInputIsRejected()
    {
        var (predictions, matches) = TwoModels();

        Assert.Throws<ArgumentOutOfRangeException>(() => MonteCarloNoise.Run(predictions, matches, 0, 1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => MonteCarloNoise.Run(predictions, matches, 1, 0, 1));
        Assert.Throws<ArgumentException>(() => MonteCarloNoise.Run(predictions, matches.Skip(1).ToList(), 1, 1, 1));
    }

    private static (Dictionary<string, IReadOnlyList<MatchPrediction>> Predictions, List<MatchData> Matches) TwoModels()
    {
        List<MatchData> matches =
        [
            TestLeague.Played(1, 20, 100, 200, 2, 0),
            TestLeague.Played(2, 20, 300, 400, 1, 1),
            TestLeague.Played(3, 21, 200, 300, 0, 2)
        ];

        var predictions = new Dictionary<string, IReadOnlyList<MatchPrediction>>
        {
            ["B"] = matches.Select(m => MatchPrediction.FromLambdas(m, "B", 1.2, 1.2, 10)).ToList(),
            ["A"] = matches.Select(m => MatchPrediction.FromLambdas(m, "A", 1.8, 0.9, 10)).ToList()
        };

        return (predictions, matches);
    }

    private static MatchOutcome Outcome(MatchData match) => PredictionMetrics.OutcomeOf(match.HomeScore!.Value, match.AwayScore!.Value);
}
