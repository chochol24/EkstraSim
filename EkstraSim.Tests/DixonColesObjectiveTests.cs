using EkstraSim.Prediction.Models;
using MathNet.Numerics.LinearAlgebra;

namespace EkstraSim.Tests;

public class DixonColesObjectiveTests
{
    private const double TimeDecayXi = 0.0065;
    private const double RidgeLambda = 0.05;

    private static DixonColesObjective BuildObjective(List<MatchData> matches)
    {
        var played = matches.OrderBy(m => m.Date).ThenBy(m => m.Id).ToList();
        var teamIndex = played
            .SelectMany(m => new[] { m.HomeTeamId, m.AwayTeamId })
            .Distinct()
            .OrderBy(id => id)
            .Select((id, index) => (id, index))
            .ToDictionary(pair => pair.id, pair => pair.index);

        var reference = played[^1].Date;
        var weights = played.Select(m => Math.Exp(-TimeDecayXi * (reference - m.Date).TotalDays)).ToArray();

        var effective = new double[teamIndex.Count];
        for (var i = 0; i < played.Count; i++)
        {
            effective[teamIndex[played[i].HomeTeamId]] += weights[i];
            effective[teamIndex[played[i].AwayTeamId]] += weights[i];
        }

        var ridgeWeights = effective.Select(count => 1.0 / (1.0 + count)).ToArray();

        return new DixonColesObjective(played, teamIndex, weights, ridgeWeights, RidgeLambda);
    }

    private static List<MatchData> LowScoringSeason()
    {
        var matches = DixonColesModelTests.SyntheticSeason(repetitions: 4, seed: 4321);

        foreach (var (home, away) in new[] { (0, 0), (0, 1), (1, 0), (1, 1) })
        {
            Assert.Contains(matches, m => m.HomeScore == home && m.AwayScore == away);
        }

        return matches;
    }

    private static Vector<double> Perturbed(Vector<double> point, int seed)
    {
        var random = new Random(seed);
        return point.Map(value => value + (random.NextDouble() - 0.5) * 0.4);
    }

    private static double MaxRelativeGradientError(DixonColesObjective objective, Vector<double> point)
    {
        var analytic = objective.ValueAndGradient(point).Gradient;
        var worst = 0.0;

        for (var i = 0; i < point.Count; i++)
        {
            var step = 1e-6 * Math.Max(1, Math.Abs(point[i]));
            var forward = point.Clone();
            var backward = point.Clone();
            forward[i] += step;
            backward[i] -= step;

            var numeric = (objective.Value(forward) - objective.Value(backward)) / (2 * step);
            worst = Math.Max(worst, Math.Abs(numeric - analytic[i]) / Math.Max(1, Math.Abs(numeric)));
        }

        return worst;
    }

    [Fact]
    public void AnalyticGradientMatchesCentralDifferencesAtTheStartingPoint()
    {
        var objective = BuildObjective(LowScoringSeason());
        var start = Vector<double>.Build.DenseOfArray(objective.InitialGuess());

        Assert.True(MaxRelativeGradientError(objective, start) <= 1e-5);
    }

    [Fact]
    public void AnalyticGradientMatchesCentralDifferencesAwayFromTheStartingPoint()
    {
        var objective = BuildObjective(LowScoringSeason());
        var point = Perturbed(Vector<double>.Build.DenseOfArray(objective.InitialGuess()), seed: 17);

        Assert.True(objective.Value(point) < DixonColesObjective.InfeasiblePenalty);
        Assert.True(MaxRelativeGradientError(objective, point) <= 1e-5);
    }

    [Fact]
    public void ValueAndGradientReturnsTheSameValueAsValue()
    {
        var objective = BuildObjective(LowScoringSeason());
        var point = Perturbed(Vector<double>.Build.DenseOfArray(objective.InitialGuess()), seed: 23);

        Assert.Equal(objective.Value(point), objective.ValueAndGradient(point).Value);
    }

    [Fact]
    public void ObjectiveIsFlatAlongTheAttackDefenceShift()
    {
        var objective = BuildObjective(LowScoringSeason());
        var point = Perturbed(Vector<double>.Build.DenseOfArray(objective.InitialGuess()), seed: 31);
        var teamCount = (objective.Dimension - 2) / 2;

        var direction = Vector<double>.Build.Dense(objective.Dimension, i => i < teamCount ? 1 : i < 2 * teamCount ? -1 : 0);
        var shifted = point + 0.37 * direction;

        Assert.Equal(objective.Value(point), objective.Value(shifted), 1e-9);

        var gradient = objective.ValueAndGradient(point).Gradient;
        Assert.Equal(0, gradient.DotProduct(direction), 1e-9);
    }
}
