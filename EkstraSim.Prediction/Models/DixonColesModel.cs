using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.Optimization;

namespace EkstraSim.Prediction.Models;

public sealed class DixonColesModel : IPredictionModel
{
    public const int DefaultMaxIterations = 2000;
    public const double StationarityTolerance = 1e-6;
    private const double GradientTolerance = 1e-9;
    private const int LbfgsMemory = 10;

    private readonly int _maxIterations;
    private TrainingOptions _options = new();
    private readonly List<MatchData> _played = [];
    private readonly HashSet<int> _knownMatchIds = [];
    private readonly List<DixonColesFitReport> _fitReports = [];

    private List<int> _teamIds = [];
    private Dictionary<int, int> _teamIndex = [];
    private double[] _attack = [];
    private double[] _defence = [];
    private double _homeAdvantage = 1.35;
    private double _rho;
    private int? _lastRound;

    public DixonColesModel() : this(DefaultMaxIterations)
    {
    }

    public DixonColesModel(int maxIterations)
    {
        _maxIterations = maxIterations;
    }

    public string Name => "DixonColes";

    public IReadOnlyList<DixonColesFitReport> FitReports => _fitReports;

    public void Train(IReadOnlyList<MatchData> history, TrainingOptions options)
    {
        _options = options;
        _played.Clear();
        _knownMatchIds.Clear();
        _fitReports.Clear();
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

        _played.Sort((left, right) =>
        {
            var byDate = left.Date.CompareTo(right.Date);
            return byDate != 0 ? byDate : left.Id.CompareTo(right.Id);
        });
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

        var reference = _played[^1].Date;

        var teamIds = _played
            .SelectMany(m => new[] { m.HomeTeamId, m.AwayTeamId })
            .Distinct()
            .OrderBy(id => id)
            .ToList();
        var teamIndex = teamIds
            .Select((id, index) => (id, index))
            .ToDictionary(pair => pair.id, pair => pair.index);

        var teamCount = teamIds.Count;
        var weights = _played.Select(match => TimeWeight(match, reference)).ToArray();
        var ridgeWeights = RidgeWeights(weights, teamIndex);
        var objective = new DixonColesObjective(_played, teamIndex, weights, ridgeWeights, _options.RidgeLambda);
        var start = Vector<double>.Build.DenseOfArray(objective.InitialGuess());
        var startObjective = objective.Value(start);

        MinimizationResult result;
        try
        {
            var solver = new LimitedMemoryBfgsMinimizer(GradientTolerance, 0, 0, LbfgsMemory, _maxIterations);
            result = solver.FindMinimum(ObjectiveFunction.Gradient(objective.ValueAndGradient), start);
        }
        catch (OptimizationException ex)
        {
            throw new ModelConvergenceException(
                new DixonColesFitReport(_lastRound, objective.Dimension, null, ExitCondition.None, startObjective, double.NaN, double.NaN),
                $"{ex.GetType().Name}: {ex.Message}",
                ex);
        }

        var (finalObjective, gradient) = objective.ValueAndGradient(result.MinimizingPoint);
        var report = new DixonColesFitReport(
            _lastRound,
            objective.Dimension,
            result.Iterations,
            result.ReasonForExit,
            startObjective,
            finalObjective,
            gradient.InfinityNorm());

        var rejection = RejectionReason(report, objective.HasClampedParameter(result.MinimizingPoint));
        if (rejection != null)
        {
            throw new ModelConvergenceException(report, rejection);
        }

        _fitReports.Add(report);
        _teamIds = teamIds;
        _teamIndex = teamIndex;
        DixonColesObjective.Unpack(result.MinimizingPoint, teamCount, out _attack, out _defence, out _homeAdvantage, out _rho);
    }

    private string? RejectionReason(DixonColesFitReport report, bool hasClampedParameter)
    {
        if (report.FinalObjective >= DixonColesObjective.InfeasiblePenalty)
        {
            return "punkt niedopuszczalny (τ ≤ 0 albo λ, μ ≤ 0)";
        }

        if (hasClampedParameter)
        {
            return "parametr na granicy przycięcia ±20 (MLE nie istnieje)";
        }

        var gradientExit = report.ExitReason is ExitCondition.AbsoluteGradient or ExitCondition.RelativeGradient;
        var stationary = report.GradientNorm <= StationarityTolerance * Math.Max(1, Math.Abs(report.FinalObjective));

        if (gradientExit && stationary)
        {
            return null;
        }

        if (report.Iterations >= _maxIterations)
        {
            return $"wyczerpany limit {_maxIterations} iteracji";
        }

        return gradientExit ? "niespełniony warunek stacjonarności" : $"wyjście {report.ExitReason}";
    }

    private double TimeWeight(MatchData match, DateTime reference)
    {
        var days = (reference - match.Date).TotalDays;
        return Math.Exp(-_options.TimeDecayXi * Math.Max(0, days));
    }

    private double[] RidgeWeights(double[] weights, IReadOnlyDictionary<int, int> teamIndex)
    {
        var effective = new double[teamIndex.Count];

        for (var i = 0; i < _played.Count; i++)
        {
            effective[teamIndex[_played[i].HomeTeamId]] += weights[i];
            effective[teamIndex[_played[i].AwayTeamId]] += weights[i];
        }

        return effective.Select(count => 1.0 / (1.0 + count)).ToArray();
    }

    private double AttackOf(int teamId) => _teamIndex.TryGetValue(teamId, out var index) ? _attack[index] : 1.0;

    private double DefenceOf(int teamId) => _teamIndex.TryGetValue(teamId, out var index) ? _defence[index] : 1.0;
}
