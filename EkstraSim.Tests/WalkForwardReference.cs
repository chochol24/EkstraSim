using EkstraSim.Prediction.Evaluation;
using EkstraSim.Prediction.Models;

namespace EkstraSim.Tests;

public sealed class WalkForwardReference
{
    public const double Tolerance = 1e-9;

    private readonly IReadOnlyList<MatchData> _history;
    private readonly IReadOnlyList<MatchData> _evaluationMatches;
    private readonly TrainingOptions _options;
    private readonly Dictionary<string, IPredictionModel> _models = [];

    public WalkForwardReference(IReadOnlyList<MatchData> history, IReadOnlyList<MatchData> evaluationMatches, TrainingOptions options)
    {
        _history = history;
        _evaluationMatches = evaluationMatches;
        _options = options;
    }

    public List<MatchData> AbsorbedBefore(MatchData match)
    {
        return _history
            .Concat(_evaluationMatches.Where(m => m.Round < match.Round))
            .Where(m => m.IsPlayed)
            .ToList();
    }

    public List<MatchData> HiddenFrom(MatchData match)
    {
        return AbsorbedBefore(match)
            .Where(m => m.Date >= match.Date)
            .OrderBy(m => m.Id)
            .ToList();
    }

    public List<MatchData> VisibleTo(MatchData match)
    {
        return AbsorbedBefore(match)
            .Where(m => m.Date < match.Date)
            .ToList();
    }

    public MatchPrediction Predict(string modelName, IReadOnlyList<MatchData> knownMatches, MatchData match)
    {
        var key = $"{modelName}|{string.Join(",", knownMatches.Select(m => m.Id).Order())}";

        if (!_models.TryGetValue(key, out var model))
        {
            model = PredictionModelFactory.Create(modelName);
            model.Train(knownMatches, _options);
            _models[key] = model;
        }

        return model.Predict(match);
    }

    public List<string> Mismatches(string modelName, IReadOnlyList<RoundEvaluation> rounds)
    {
        var matchesById = _evaluationMatches.ToDictionary(m => m.Id);
        var mismatches = new List<string>();

        foreach (var prediction in rounds.SelectMany(r => r.Predictions))
        {
            var match = matchesById[prediction.MatchId];
            var expected = Predict(modelName, VisibleTo(match), match);

            if (Differs(expected, prediction, Tolerance))
            {
                mismatches.Add($"k.{match.Round} mecz {match.Id} ({match.Date:yyyy-MM-dd})");
            }
        }

        return mismatches;
    }

    public static bool Differs(MatchPrediction expected, MatchPrediction actual, double tolerance)
    {
        return Math.Abs(expected.ExpectedHomeGoals - actual.ExpectedHomeGoals) > tolerance
            || Math.Abs(expected.ExpectedAwayGoals - actual.ExpectedAwayGoals) > tolerance
            || Math.Abs(expected.HomeWinProbability - actual.HomeWinProbability) > tolerance
            || Math.Abs(expected.DrawProbability - actual.DrawProbability) > tolerance
            || Math.Abs(expected.AwayWinProbability - actual.AwayWinProbability) > tolerance;
    }
}
