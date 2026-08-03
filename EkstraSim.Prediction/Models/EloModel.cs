namespace EkstraSim.Prediction.Models;

public sealed class EloModel : IPredictionModel
{
    public const double RatingScale = 400.0;
    private const int MinimumSamplesForRegression = 20;

    private TrainingOptions _options = new();
    private readonly List<MatchData> _played = [];
    private readonly HashSet<int> _knownMatchIds = [];

    private Dictionary<int, double> _ratings = [];
    private PoissonRegression _homeGoals = new();
    private PoissonRegression _awayGoals = new();
    private int? _lastRound;

    public string Name => "Elo";

    public void Train(IReadOnlyList<MatchData> history, TrainingOptions options)
    {
        _options = options;
        _played.Clear();
        _knownMatchIds.Clear();
        _lastRound = null;

        Absorb(history);
        Replay();
    }

    public void UpdateWithRound(IReadOnlyList<MatchData> playedRound)
    {
        Absorb(playedRound);

        var rounds = playedRound.Where(m => m.Round.HasValue).Select(m => m.Round!.Value).ToList();
        if (rounds.Count > 0)
        {
            _lastRound = rounds.Max();
        }

        Replay();
    }

    public MatchPrediction Predict(MatchData match)
    {
        var difference = RatingOf(match.HomeTeamId) - RatingOf(match.AwayTeamId);
        var feature = difference / RatingScale;

        var lambdaHome = _homeGoals.Predict(feature);
        var lambdaAway = _awayGoals.Predict(feature);

        return MatchPrediction.FromLambdas(match, Name, lambdaHome, lambdaAway, _options.MaxGoals);
    }

    public ModelSnapshot GetParametersSnapshot()
    {
        var parameters = new Dictionary<string, double>
        {
            ["home_goals_intercept"] = _homeGoals.Intercept,
            ["home_goals_slope"] = _homeGoals.Slope,
            ["away_goals_intercept"] = _awayGoals.Intercept,
            ["away_goals_slope"] = _awayGoals.Slope
        };

        foreach (var (teamId, rating) in _ratings.OrderBy(pair => pair.Key))
        {
            parameters[$"rating_{teamId}"] = rating;
        }

        return new ModelSnapshot
        {
            ModelName = Name,
            AfterRound = _lastRound,
            Parameters = parameters
        };
    }

    public double RatingOf(int teamId) => _ratings.TryGetValue(teamId, out var rating) ? rating : _options.EloInitialRating;

    public double ExpectedScore(double homeRating, double awayRating)
    {
        var difference = homeRating - awayRating + _options.EloHomeAdvantage;
        return 1.0 / (Math.Pow(10, -difference / RatingScale) + 1);
    }

    public static double GoalDifferenceMultiplier(int goalDifference)
    {
        return goalDifference switch
        {
            1 => 1.0,
            2 => 1.5,
            _ => (11 + goalDifference) / 8.0
        };
    }

    private void Absorb(IReadOnlyList<MatchData> matches)
    {
        foreach (var match in matches)
        {
            if (!match.IsPlayed || !match.SeasonId.HasValue)
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

    private void Replay()
    {
        _ratings = [];
        var samples = new List<(double X, double Y)>();
        var awaySamples = new List<(double X, double Y)>();

        foreach (var match in _played)
        {
            var homeRating = RatingOf(match.HomeTeamId);
            var awayRating = RatingOf(match.AwayTeamId);

            if (match.LeagueId == _options.LeagueId)
            {
                var feature = (homeRating - awayRating) / RatingScale;
                samples.Add((feature, match.HomeScore!.Value));
                awaySamples.Add((feature, match.AwayScore!.Value));
            }

            var homeScore = match.HomeScore!.Value;
            var awayScore = match.AwayScore!.Value;

            var homeOutcome = homeScore > awayScore ? 1.0 : homeScore == awayScore ? 0.5 : 0.0;
            var expectedHome = ExpectedScore(homeRating, awayRating);
            var multiplier = GoalDifferenceMultiplier(Math.Abs(homeScore - awayScore));
            var adjustment = _options.EloK * multiplier * (homeOutcome - expectedHome);

            _ratings[match.HomeTeamId] = homeRating + adjustment;
            _ratings[match.AwayTeamId] = awayRating - adjustment;
        }

        if (samples.Count >= MinimumSamplesForRegression)
        {
            _homeGoals = PoissonRegression.Fit(samples);
            _awayGoals = PoissonRegression.Fit(awaySamples);
        }
        else
        {
            _homeGoals = FlatRegression(samples, GoalAverages.Neutral.HomeScored);
            _awayGoals = FlatRegression(awaySamples, GoalAverages.Neutral.AwayScored);
        }
    }

    private static PoissonRegression FlatRegression(List<(double X, double Y)> samples, double fallbackMean)
    {
        var mean = samples.Count > 0 ? samples.Average(s => s.Y) : fallbackMean;
        return PoissonRegression.Fit([(0.0, Math.Max(1e-6, mean))]);
    }
}
