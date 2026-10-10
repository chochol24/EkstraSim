using EkstraSim.Prediction.Evaluation;
using EkstraSim.Prediction.Models;
using Xunit.Abstractions;

namespace EkstraSim.Tests;

public class WalkForwardRealDataTests
{
    private readonly ITestOutputHelper _output;

    public WalkForwardRealDataTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public static IEnumerable<object[]> FilteredPredictionsBySeasonAndModel =>
        from season in new[] { (Id: RealDataFixture.Season2425, Count: 0), (Id: RealDataFixture.Season2526, Count: 72) }
        from modelName in PredictionModelFactory.AvailableModels
        select new object[] { season.Id, season.Count, modelName };

    public static IEnumerable<object[]> RestrictedModelsBySeasonAndModel =>
        from season in new[] { (Id: RealDataFixture.Season2425, Count: 0), (Id: RealDataFixture.Season2526, Count: 9) }
        from modelName in PredictionModelFactory.AvailableModels
        select new object[] { season.Id, season.Count, modelName };

    [Theory]
    [Trait("Category", "RealData")]
    [InlineData(RealDataFixture.Season2425, 0, new int[0])]
    [InlineData(RealDataFixture.Season2526, 72, new[] { 19, 20, 21, 22, 23, 24, 25, 32 })]
    public void PredictionsAffectedByLaterMatchesMatchTheAudit(int seasonId, int expectedCount, int[] expectedRounds)
    {
        var (history, evaluation) = Split(seasonId);
        var reference = new WalkForwardReference(history, evaluation, RealDataFixture.Options(seasonId));

        var affected = evaluation
            .Select(match => (Match: match, Hidden: reference.HiddenFrom(match)))
            .Where(entry => entry.Hidden.Count > 0)
            .ToList();

        foreach (var (match, hidden) in affected)
        {
            _output.WriteLine($"k.{match.Round} mecz {match.Id} ({match.Date:yyyy-MM-dd}) — później: {string.Join(", ", hidden.Select(h => $"{h.Id} (k.{h.Round}, {h.Date:yyyy-MM-dd})"))}");
        }

        Assert.Equal(expectedCount, affected.Count);
        Assert.Equal(expectedRounds, affected.Select(entry => entry.Match.Round!.Value).Distinct().Order());
    }

    [Theory]
    [Trait("Category", "RealData")]
    [MemberData(nameof(FilteredPredictionsBySeasonAndModel))]
    public void EveryPredictionUsesOnlyMatchesPlayedBeforeIt(int seasonId, int expectedFiltered, string modelName)
    {
        var (history, evaluation) = Split(seasonId);
        var options = RealDataFixture.Options(seasonId);
        var reference = new WalkForwardReference(history, evaluation, options);

        var rounds = WalkForwardEvaluator.Run(PredictionModelFactory.Create(modelName), history, evaluation, options);
        var mismatches = reference.Mismatches(modelName, rounds);

        Assert.True(
            mismatches.Count == 0,
            $"Sezon {seasonId}, {modelName}: {mismatches.Count} predykcji widzi mecze rozegrane później — {string.Join("; ", mismatches.Take(10))}.");

        var filtered = rounds.SelectMany(r => r.DateFilteredMatchIds).ToList();
        var affected = evaluation.Where(m => reference.HiddenFrom(m).Count > 0).Select(m => m.Id).ToList();

        Assert.Equal(expectedFiltered, filtered.Count);
        Assert.Equal(affected, filtered);
    }

    [Theory]
    [Trait("Category", "RealData")]
    [MemberData(nameof(RestrictedModelsBySeasonAndModel))]
    public void RestrictedModelIsTrainedOncePerDistinctHiddenSetInARound(int seasonId, int expectedRestricted, string modelName)
    {
        var (history, evaluation) = Split(seasonId);
        var options = RealDataFixture.Options(seasonId);
        var reference = new WalkForwardReference(history, evaluation, options);
        var created = 0;

        WalkForwardEvaluator.Run(
            PredictionModelFactory.Create(modelName), history, evaluation, options,
            createModel: () =>
            {
                created++;
                return PredictionModelFactory.Create(modelName);
            });

        var distinctHiddenSets = evaluation
            .GroupBy(m => m.Round)
            .Sum(round => round
                .Select(m => string.Join(",", reference.HiddenFrom(m).Select(h => h.Id)))
                .Where(key => key.Length > 0)
                .Distinct()
                .Count());

        Assert.Equal(expectedRestricted, distinctHiddenSets);
        Assert.Equal(distinctHiddenSets, created);
    }

    public static IEnumerable<object[]> Models => PredictionModelFactory.AvailableModels.Select(name => new object[] { name });

    [Theory]
    [Trait("Category", "RealData")]
    [MemberData(nameof(Models))]
    public void PredictRoundPathEqualsTheFirstEvaluatedRound(string modelName)
    {
        const int seasonId = RealDataFixture.Season2526;
        const int round = RealDataFixture.TrainingLastRound + 1;
        var (history, evaluation) = Split(seasonId);
        var options = RealDataFixture.Options(seasonId);

        var walkForward = WalkForwardEvaluator.Run(PredictionModelFactory.Create(modelName), history, evaluation, options)
            .Single(r => r.Round == round);

        var matches = evaluation.Where(m => m.Round == round).OrderBy(m => m.Id).ToList();
        var model = PredictionModelFactory.Create(modelName);
        model.Train(history, options);

        var (predictions, filtered) = WalkForwardEvaluator.PredictWithDateFilter(
            model, history, matches, options, () => PredictionModelFactory.Create(modelName), round);

        _output.WriteLine($"{modelName}, k.{round}: {matches.Count} meczów, zawężone: {string.Join(", ", filtered)}");

        Assert.Equal(walkForward.DateFilteredMatchIds, filtered);
        Assert.Equal(walkForward.Predictions.Select(p => p.MatchId), predictions.Select(p => p.MatchId));
        Assert.All(
            walkForward.Predictions.Zip(predictions),
            pair => Assert.True(
                WalkForwardReference.Identical(pair.First, pair.Second),
                $"{modelName}: mecz {pair.First.MatchId} — predykcja predict-round różni się od walk-forward."));

        Assert.NotEmpty(filtered);
        Assert.Contains(
            matches.Zip(predictions),
            pair => WalkForwardReference.Differs(model.Predict(pair.First), pair.Second, WalkForwardReference.Tolerance));
    }

    private static (List<MatchData> History, List<MatchData> Evaluation) Split(int seasonId)
    {
        return (
            WalkForwardEvaluator.BuildHistory(RealDataFixture.Matches, RealDataFixture.SeasonChronology, seasonId, RealDataFixture.TrainingLastRound),
            WalkForwardEvaluator.BuildEvaluationSet(RealDataFixture.Matches, seasonId, RealDataFixture.TrainingLastRound));
    }
}
