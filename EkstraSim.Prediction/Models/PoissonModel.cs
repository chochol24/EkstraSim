namespace EkstraSim.Prediction.Models;

public sealed class PoissonModel : IPredictionModel
{
    private sealed class TeamHorizons
    {
        public GoalAverages Current { get; init; } = GoalAverages.Neutral;
        public GoalAverages Previous { get; init; } = GoalAverages.Neutral;
        public GoalAverages Historical { get; init; } = GoalAverages.Neutral;
    }

    private const int FormMatchCount = 10;
    private const int HomeAwayFormMatchCount = 5;
    private const int HeadToHeadMatchCount = 5;

    private TrainingOptions _options = new();
    private readonly List<MatchData> _played = [];
    private readonly HashSet<int> _knownMatchIds = [];
    private GoalAverages _league = GoalAverages.Neutral;
    private Dictionary<int, TeamHorizons> _teams = [];
    private int? _lastRound;

    public string Name => "Poisson";

    public void Train(IReadOnlyList<MatchData> history, TrainingOptions options)
    {
        _options = options;
        _played.Clear();
        _knownMatchIds.Clear();
        _lastRound = null;

        Absorb(history);
        Recompute();
    }

    public void UpdateWithRound(IReadOnlyList<MatchData> playedRound)
    {
        Absorb(playedRound);

        var rounds = playedRound.Where(m => m.Round.HasValue).Select(m => m.Round!.Value).ToList();
        if (rounds.Count > 0)
        {
            _lastRound = rounds.Max();
        }

        Recompute();
    }

    public MatchPrediction Predict(MatchData match)
    {
        var home = GetHorizons(match.HomeTeamId);
        var away = GetHorizons(match.AwayTeamId);

        var lambdaHome =
            HorizonGoals(home.Current.HomeScored, away.Current.AwayConceded, isHome: true) * _options.CurrentSeasonScale
            + HorizonGoals(home.Previous.HomeScored, away.Previous.AwayConceded, isHome: true) * _options.PreviousSeasonScale
            + HorizonGoals(home.Historical.HomeScored, away.Historical.AwayConceded, isHome: true) * _options.HistoricalScale;

        var lambdaAway =
            HorizonGoals(away.Current.AwayScored, home.Current.HomeConceded, isHome: false) * _options.CurrentSeasonScale
            + HorizonGoals(away.Previous.AwayScored, home.Previous.HomeConceded, isHome: false) * _options.PreviousSeasonScale
            + HorizonGoals(away.Historical.AwayScored, home.Historical.HomeConceded, isHome: false) * _options.HistoricalScale;

        if (_options.UseFormFactors)
        {
            lambdaHome *= FormMultiplier(match, match.HomeTeamId, isHome: true);
            lambdaAway *= FormMultiplier(match, match.AwayTeamId, isHome: false);
        }

        return MatchPrediction.FromLambdas(match, Name, lambdaHome, lambdaAway, _options.MaxGoals);
    }

    public ModelSnapshot GetParametersSnapshot()
    {
        var parameters = new Dictionary<string, double>
        {
            ["league_home_scored"] = _league.HomeScored,
            ["league_home_conceded"] = _league.HomeConceded,
            ["league_away_scored"] = _league.AwayScored,
            ["league_away_conceded"] = _league.AwayConceded
        };

        foreach (var (teamId, horizons) in _teams)
        {
            parameters[$"team_{teamId}_home_scored"] = horizons.Current.HomeScored;
            parameters[$"team_{teamId}_home_conceded"] = horizons.Current.HomeConceded;
            parameters[$"team_{teamId}_away_scored"] = horizons.Current.AwayScored;
            parameters[$"team_{teamId}_away_conceded"] = horizons.Current.AwayConceded;
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
            if (!match.IsPlayed || !_knownMatchIds.Add(match.Id))
            {
                continue;
            }

            _played.Add(match);
        }

        _played.Sort((left, right) => left.Date.CompareTo(right.Date));
    }

    private void Recompute()
    {
        var leagueMatches = _played
            .Where(m => m.LeagueId == _options.LeagueId && m.SeasonId.HasValue)
            .ToList();

        _league = GoalAverages.ForLeague(leagueMatches);

        var previousSeasonId = _options.PreviousSeasonId;

        var current = leagueMatches.Where(m => m.SeasonId == _options.SeasonId).ToList();
        var previous = previousSeasonId.HasValue
            ? leagueMatches.Where(m => m.SeasonId == previousSeasonId.Value).ToList()
            : [];
        var historical = leagueMatches
            .Where(m => m.SeasonId != _options.SeasonId && m.SeasonId != previousSeasonId)
            .ToList();

        var teamIds = leagueMatches
            .SelectMany(m => new[] { m.HomeTeamId, m.AwayTeamId })
            .Distinct()
            .ToList();

        _teams = teamIds.ToDictionary(
            teamId => teamId,
            teamId => new TeamHorizons
            {
                Current = GoalAverages.ForTeam(teamId, current, _league),
                Previous = GoalAverages.ForTeam(teamId, previous, _league),
                Historical = GoalAverages.ForTeam(teamId, historical, _league)
            });
    }

    private TeamHorizons GetHorizons(int teamId)
    {
        if (_teams.TryGetValue(teamId, out var horizons))
        {
            return horizons;
        }

        return new TeamHorizons
        {
            Current = _league,
            Previous = _league,
            Historical = _league
        };
    }

    private double HorizonGoals(double attackAverage, double defenceAverage, bool isHome)
    {
        var leagueAttack = isHome ? _league.HomeScored : _league.AwayScored;
        var leagueDefence = isHome ? _league.AwayConceded : _league.HomeConceded;

        if (leagueAttack <= 0 || leagueDefence <= 0)
        {
            return 0;
        }

        var attackStrength = attackAverage / leagueAttack;
        var defenceStrength = defenceAverage / leagueDefence;

        return attackStrength * defenceStrength * leagueAttack;
    }

    private double FormMultiplier(MatchData match, int teamId, bool isHome)
    {
        var priorMatches = _played
            .Where(m => m.SeasonId.HasValue && m.Date < match.Date)
            .ToList();

        var recent = priorMatches
            .Where(m => m.Involves(teamId))
            .OrderByDescending(m => m.Date)
            .Take(FormMatchCount)
            .ToList();

        var venue = priorMatches
            .Where(m => isHome ? m.HomeTeamId == teamId : m.AwayTeamId == teamId)
            .OrderByDescending(m => m.Date)
            .Take(HomeAwayFormMatchCount)
            .ToList();

        var headToHead = priorMatches
            .Where(m => m.Involves(match.HomeTeamId) && m.Involves(match.AwayTeamId))
            .OrderByDescending(m => m.Date)
            .Take(HeadToHeadMatchCount)
            .ToList();

        return BalanceFactor(recent, teamId, 0.8, 1.2)
            * BalanceFactor(venue, teamId, 0.8, 1.2)
            * BalanceFactor(headToHead, teamId, 0.95, 1.05);
    }

    private static double BalanceFactor(List<MatchData> matches, int teamId, double lower, double upper)
    {
        if (matches.Count == 0)
        {
            return 1.0;
        }

        var scored = matches.Average(m => (double)m.GoalsScoredBy(teamId));
        var conceded = matches.Average(m => (double)m.GoalsConcededBy(teamId));
        var factor = (scored - conceded) / 2.0 + 1.0;

        return Math.Max(lower, Math.Min(upper, factor));
    }
}
