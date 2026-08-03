using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.Optimization;

namespace EkstraSim.Prediction.Models;

public sealed class DixonColesModel : IPredictionModel
{
    private const double MaxRho = 0.3;
    private const double InfeasiblePenalty = 1e12;
    private const int MaxIterations = 20000;
    private const double ConvergenceTolerance = 1e-7;

    private TrainingOptions _options = new();
    private readonly List<MatchData> _played = [];
    private readonly HashSet<int> _knownMatchIds = [];

    private List<int> _teamIds = [];
    private Dictionary<int, int> _teamIndex = [];
    private double[] _attack = [];
    private double[] _defence = [];
    private double _homeAdvantage = 1.35;
    private double _rho;
    private DateTime _reference = DateTime.MinValue;
    private int? _lastRound;

    public string Name => "DixonColes";

    public void Train(IReadOnlyList<MatchData> history, TrainingOptions options)
    {
        _options = options;
        _played.Clear();
        _knownMatchIds.Clear();
        _lastRound = null;

        Absorb(history);
        Fit();
    }

    public void UpdateWithRound(IReadOnlyList<MatchData> playedRound)
    {
        Absorb(playedRound);

        var rounds = playedRound.Where(m => m.Round.HasValue).Select(m => m.Round!.Value).ToList();
        if (rounds.Count > 0)
        {
            _lastRound = rounds.Max();
        }

        Fit();
    }

    public MatchPrediction Predict(MatchData match)
    {
        var homeAttack = AttackOf(match.HomeTeamId);
        var homeDefence = DefenceOf(match.HomeTeamId);
        var awayAttack = AttackOf(match.AwayTeamId);
        var awayDefence = DefenceOf(match.AwayTeamId);

        var lambdaHome = homeAttack * awayDefence * _homeAdvantage;
        var lambdaAway = awayAttack * homeDefence;

        var grid = ScoreGrid.FromDixonColes(lambdaHome, lambdaAway, _rho, _options.MaxGoals);

        return MatchPrediction.FromGrid(match, Name, lambdaHome, lambdaAway, grid);
    }

    public ModelSnapshot GetParametersSnapshot()
    {
        var parameters = new Dictionary<string, double>
        {
            ["home_advantage"] = _homeAdvantage,
            ["rho"] = _rho
        };

        for (var i = 0; i < _teamIds.Count; i++)
        {
            parameters[$"attack_{_teamIds[i]}"] = _attack[i];
            parameters[$"defence_{_teamIds[i]}"] = _defence[i];
        }

        return new ModelSnapshot
        {
            ModelName = Name,
            AfterRound = _lastRound,
            Parameters = parameters
        };
    }

    private void Absorb(IReadOnlyList<MatchData> matches)
    {
        foreach (var match in matches)
        {
            if (!match.IsPlayed || match.LeagueId != _options.LeagueId || !match.SeasonId.HasValue)
            {
                continue;
            }

            if (!_knownMatchIds.Add(match.Id))
            {
                continue;
            }

            _played.Add(match);
        }

        _played.Sort((left, right) => left.Date.CompareTo(right.Date));
    }

    private void Fit()
    {
        if (_played.Count == 0)
        {
            _teamIds = [];
            _teamIndex = [];
            _attack = [];
            _defence = [];
            return;
        }

        _reference = _played[^1].Date;

        _teamIds = _played
            .SelectMany(m => new[] { m.HomeTeamId, m.AwayTeamId })
            .Distinct()
            .OrderBy(id => id)
            .ToList();
        _teamIndex = _teamIds
            .Select((id, index) => (id, index))
            .ToDictionary(pair => pair.id, pair => pair.index);

        var teamCount = _teamIds.Count;
        var weights = _played.Select(TimeWeight).ToArray();
        var ridgeWeights = RidgeWeights(weights, teamCount);
        var start = InitialGuess(weights, teamCount);

        var objective = ObjectiveFunction.Value(vector => NegativeLogLikelihood(vector, weights, ridgeWeights, teamCount));
        var solver = new NelderMeadSimplex(ConvergenceTolerance, MaxIterations);

        Vector<double> solution;
        try
        {
            solution = solver.FindMinimum(objective, Vector<double>.Build.DenseOfArray(start)).MinimizingPoint;
        }
        catch (Exception)
        {
            solution = Vector<double>.Build.DenseOfArray(start);
        }

        Unpack(solution, teamCount, out _attack, out _defence, out _homeAdvantage, out _rho);
    }

    private double TimeWeight(MatchData match)
    {
        var days = (_reference - match.Date).TotalDays;
        return Math.Exp(-_options.TimeDecayXi * Math.Max(0, days));
    }

    private double[] RidgeWeights(double[] weights, int teamCount)
    {
        var effective = new double[teamCount];

        for (var i = 0; i < _played.Count; i++)
        {
            effective[_teamIndex[_played[i].HomeTeamId]] += weights[i];
            effective[_teamIndex[_played[i].AwayTeamId]] += weights[i];
        }

        return effective.Select(count => 1.0 / (1.0 + count)).ToArray();
    }

    private double[] InitialGuess(double[] weights, int teamCount)
    {
        var scored = new double[teamCount];
        var conceded = new double[teamCount];
        var appearances = new double[teamCount];
        double homeGoals = 0, awayGoals = 0, totalWeight = 0;

        for (var i = 0; i < _played.Count; i++)
        {
            var match = _played[i];
            var weight = weights[i];
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

        var start = new double[2 * teamCount + 2];

        for (var i = 0; i < teamCount; i++)
        {
            var games = appearances[i] > 0 ? appearances[i] : 1;
            var scoredRate = scored[i] / games;
            var concededRate = conceded[i] / games;

            start[i] = Math.Log(Math.Max(0.2, scoredRate / overallRate));
            start[teamCount + i] = Math.Log(Math.Max(0.2, concededRate / overallRate * Math.Max(0.1, overallAway)));
        }

        start[2 * teamCount] = Math.Log(Math.Max(0.5, overallHome / Math.Max(0.1, overallAway)));
        start[2 * teamCount + 1] = Atanh(-0.03 / MaxRho);

        return start;
    }

    private double NegativeLogLikelihood(Vector<double> vector, double[] weights, double[] ridgeWeights, int teamCount)
    {
        Unpack(vector, teamCount, out var attack, out var defence, out var homeAdvantage, out var rho);

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
                return InfeasiblePenalty;
            }

            var homeScore = match.HomeScore!.Value;
            var awayScore = match.AwayScore!.Value;
            var tau = ScoreGrid.LowScoreCorrection(homeScore, awayScore, lambda, mu, rho);

            if (tau <= 0 || !double.IsFinite(tau))
            {
                return InfeasiblePenalty;
            }

            logLikelihood += weights[i] * (
                Math.Log(tau)
                + homeScore * Math.Log(lambda) - lambda
                + awayScore * Math.Log(mu) - mu);
        }

        if (!double.IsFinite(logLikelihood))
        {
            return InfeasiblePenalty;
        }

        double penalty = 0;
        for (var i = 0; i < teamCount; i++)
        {
            var logAttack = Math.Log(attack[i]);
            var logDefence = Math.Log(defence[i]);
            penalty += _options.RidgeLambda * ridgeWeights[i] * (logAttack * logAttack + logDefence * logDefence);
        }

        return -logLikelihood + penalty;
    }

    private static void Unpack(Vector<double> vector, int teamCount, out double[] attack, out double[] defence, out double homeAdvantage, out double rho)
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

    private static double Clamp(double value) => Math.Max(-20, Math.Min(20, value));

    private static double Atanh(double value)
    {
        var safe = Math.Max(-0.999999, Math.Min(0.999999, value));
        return 0.5 * Math.Log((1 + safe) / (1 - safe));
    }

    private double AttackOf(int teamId) => _teamIndex.TryGetValue(teamId, out var index) ? _attack[index] : 1.0;

    private double DefenceOf(int teamId) => _teamIndex.TryGetValue(teamId, out var index) ? _defence[index] : 1.0;
}
