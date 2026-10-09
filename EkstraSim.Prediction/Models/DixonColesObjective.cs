using MathNet.Numerics.LinearAlgebra;

namespace EkstraSim.Prediction.Models;

public sealed class DixonColesObjective
{
    public const double MaxRho = 0.3;
    public const double InfeasiblePenalty = 1e12;
    private const double ClampLimit = 20;

    private readonly IReadOnlyList<MatchData> _played;
    private readonly IReadOnlyDictionary<int, int> _teamIndex;
    private readonly double[] _weights;
    private readonly double[] _ridgeWeights;
    private readonly double _ridgeLambda;
    private readonly int _teamCount;

    public DixonColesObjective(
        IReadOnlyList<MatchData> played,
        IReadOnlyDictionary<int, int> teamIndex,
        double[] weights,
        double[] ridgeWeights,
        double ridgeLambda)
    {
        _played = played;
        _teamIndex = teamIndex;
        _weights = weights;
        _ridgeWeights = ridgeWeights;
        _ridgeLambda = ridgeLambda;
        _teamCount = teamIndex.Count;
    }

    public int Dimension => 2 * _teamCount + 2;

    public double[] InitialGuess()
    {
        var scored = new double[_teamCount];
        var conceded = new double[_teamCount];
        var appearances = new double[_teamCount];
        double homeGoals = 0, awayGoals = 0, totalWeight = 0;

        for (var i = 0; i < _played.Count; i++)
        {
            var match = _played[i];
            var weight = _weights[i];
            var home = _teamIndex[match.HomeTeamId];
            var away = _teamIndex[match.AwayTeamId];

            scored[home] += weight * match.HomeScore!.Value;
            conceded[home] += weight * match.AwayScore!.Value;
            scored[away] += weight * match.AwayScore!.Value;
            conceded[away] += weight * match.HomeScore!.Value;
            appearances[home] += weight;
            appearances[away] += weight;

            homeGoals += weight * match.HomeScore!.Value;
            awayGoals += weight * match.AwayScore!.Value;
            totalWeight += weight;
        }

        var overallHome = totalWeight > 0 ? homeGoals / totalWeight : 1.5;
        var overallAway = totalWeight > 0 ? awayGoals / totalWeight : 1.15;
        var overallRate = Math.Max(0.1, (overallHome + overallAway) / 2);

        var start = new double[Dimension];

        for (var i = 0; i < _teamCount; i++)
        {
            var games = appearances[i] > 0 ? appearances[i] : 1;
            var scoredRate = scored[i] / games;
            var concededRate = conceded[i] / games;

            start[i] = Math.Log(Math.Max(0.2, scoredRate / overallRate));
            start[_teamCount + i] = Math.Log(Math.Max(0.2, concededRate / overallRate * Math.Max(0.1, overallAway)));
        }

        start[2 * _teamCount] = Math.Log(Math.Max(0.5, overallHome / Math.Max(0.1, overallAway)));
        start[2 * _teamCount + 1] = Atanh(-0.03 / MaxRho);

        return start;
    }

    public double Value(Vector<double> vector) => Evaluate(vector, null);

    public (double Value, Vector<double> Gradient) ValueAndGradient(Vector<double> vector)
    {
        var gradient = new double[Dimension];
        var value = Evaluate(vector, gradient);
        return (value, Vector<double>.Build.DenseOfArray(gradient));
    }

    public bool HasClampedParameter(Vector<double> vector)
    {
        for (var i = 0; i <= 2 * _teamCount; i++)
        {
            if (Inside(vector[i]) == 0)
            {
                return true;
            }
        }

        return false;
    }

    public static void Unpack(Vector<double> vector, int teamCount, out double[] attack, out double[] defence, out double homeAdvantage, out double rho)
    {
        attack = new double[teamCount];
        defence = new double[teamCount];
        double attackSum = 0;

        for (var i = 0; i < teamCount; i++)
        {
            attack[i] = Math.Exp(Clamp(vector[i]));
            defence[i] = Math.Exp(Clamp(vector[teamCount + i]));
            attackSum += attack[i];
        }

        var meanAttack = attackSum / teamCount;
        if (meanAttack > 0)
        {
            for (var i = 0; i < teamCount; i++)
            {
                attack[i] /= meanAttack;
                defence[i] *= meanAttack;
            }
        }

        homeAdvantage = Math.Exp(Clamp(vector[2 * teamCount]));
        rho = MaxRho * Math.Tanh(vector[2 * teamCount + 1]);
    }

    private double Evaluate(Vector<double> vector, double[]? gradient)
    {
        var teamCount = _teamCount;
        Unpack(vector, teamCount, out var attack, out var defence, out var homeAdvantage, out var rho);

        var tanh = Math.Tanh(vector[2 * teamCount + 1]);
        double logLikelihood = 0;

        for (var i = 0; i < _played.Count; i++)
        {
            var match = _played[i];
            var home = _teamIndex[match.HomeTeamId];
            var away = _teamIndex[match.AwayTeamId];

            var lambda = attack[home] * defence[away] * homeAdvantage;
            var mu = attack[away] * defence[home];

            if (!double.IsFinite(lambda) || !double.IsFinite(mu) || lambda <= 0 || mu <= 0)
            {
                return Infeasible(gradient);
            }

            var homeScore = match.HomeScore!.Value;
            var awayScore = match.AwayScore!.Value;
            var tau = ScoreGrid.LowScoreCorrection(homeScore, awayScore, lambda, mu, rho);

            if (tau <= 0 || !double.IsFinite(tau))
            {
                return Infeasible(gradient);
            }

            logLikelihood += _weights[i] * (
                Math.Log(tau)
                + homeScore * Math.Log(lambda) - lambda
                + awayScore * Math.Log(mu) - mu);

            if (gradient == null)
            {
                continue;
            }

            var (tauLambda, tauMu, tauRho) = TauDerivatives(homeScore, awayScore, lambda, mu, rho);
            var weight = _weights[i];
            var scoreLambda = weight * (homeScore - lambda + lambda * tauLambda / tau);
            var scoreMu = weight * (awayScore - mu + mu * tauMu / tau);

            gradient[home] -= scoreLambda * Inside(vector[home]);
            gradient[teamCount + away] -= scoreLambda * Inside(vector[teamCount + away]);
            gradient[2 * teamCount] -= scoreLambda * Inside(vector[2 * teamCount]);
            gradient[away] -= scoreMu * Inside(vector[away]);
            gradient[teamCount + home] -= scoreMu * Inside(vector[teamCount + home]);
            gradient[2 * teamCount + 1] -= weight * tauRho / tau * MaxRho * (1 - tanh * tanh);
        }

        if (!double.IsFinite(logLikelihood))
        {
            return Infeasible(gradient);
        }

        double penalty = 0;
        double sharedTerm = 0;
        for (var i = 0; i < teamCount; i++)
        {
            var logAttack = Math.Log(attack[i]);
            var logDefence = Math.Log(defence[i]);
            penalty += _ridgeLambda * _ridgeWeights[i] * (logAttack * logAttack + logDefence * logDefence);

            if (gradient != null)
            {
                var scale = 2 * _ridgeLambda * _ridgeWeights[i];
                gradient[i] += scale * logAttack * Inside(vector[i]);
                gradient[teamCount + i] += scale * logDefence * Inside(vector[teamCount + i]);
                sharedTerm += scale * (logDefence - logAttack);
            }
        }

        if (gradient != null)
        {
            for (var i = 0; i < teamCount; i++)
            {
                gradient[i] += sharedTerm * attack[i] / teamCount * Inside(vector[i]);
            }
        }

        return -logLikelihood + penalty;
    }

    private static (double Lambda, double Mu, double Rho) TauDerivatives(int homeGoals, int awayGoals, double lambda, double mu, double rho)
    {
        return (homeGoals, awayGoals) switch
        {
            (0, 0) => (-mu * rho, -lambda * rho, -lambda * mu),
            (0, 1) => (rho, 0, lambda),
            (1, 0) => (0, rho, mu),
            (1, 1) => (0, 0, -1),
            _ => (0, 0, 0)
        };
    }

    private static double Infeasible(double[]? gradient)
    {
        if (gradient != null)
        {
            Array.Clear(gradient);
        }

        return InfeasiblePenalty;
    }

    private static double Clamp(double value) => Math.Max(-ClampLimit, Math.Min(ClampLimit, value));

    private static double Inside(double value) => value > -ClampLimit && value < ClampLimit ? 1 : 0;

    private static double Atanh(double value)
    {
        var safe = Math.Max(-0.999999, Math.Min(0.999999, value));
        return 0.5 * Math.Log((1 + safe) / (1 - safe));
    }
}
