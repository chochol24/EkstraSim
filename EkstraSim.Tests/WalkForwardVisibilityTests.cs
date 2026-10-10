using EkstraSim.Prediction.Evaluation;
using EkstraSim.Prediction.Models;

namespace EkstraSim.Tests;

public class WalkForwardVisibilityTests
{
    private const int PreviousSeason = 9;
    private const int TargetSeason = 10;
    private const int Cutoff = 4;
    private const double DetectableDifference = 1e-6;

    private static readonly int[] Teams = [10, 20, 30, 40, 50, 60];
    private static readonly DateTime PreviousStart = new(2023, 8, 4);
    private static readonly DateTime TargetStart = new(2024, 8, 2);

    public static IEnumerable<object[]> Models => PredictionModelFactory.AvailableModels.Select(name => new object[] { name });

    [Theory]
    [MemberData(nameof(Models))]
    public void RoundPredictionsSeeOnlyTrainingAndEarlierRounds(string modelName)
    {
        var (history, evaluation) = Split(League(withPostponements: false));
        var reference = new WalkForwardReference(history, evaluation, Options());

        Assert.All(evaluation, match => Assert.Empty(reference.HiddenFrom(match)));

        var rounds = WalkForwardEvaluator.Run(PredictionModelFactory.Create(modelName), history, evaluation, Options());
        var mismatches = reference.Mismatches(modelName, rounds);

        Assert.True(mismatches.Count == 0, $"{modelName}: {mismatches.Count} predykcji niezgodnych z modelem referencyjnym — {string.Join("; ", mismatches)}.");
        Assert.All(rounds, round => Assert.Empty(round.DateFilteredMatchIds));
    }

    [Theory]
    [MemberData(nameof(Models))]
    public void PostponedMatchesStayHiddenUntilTheirDate(string modelName)
    {
        var (history, evaluation) = Split(League(withPostponements: true));
        var reference = new WalkForwardReference(history, evaluation, Options());

        var rounds = WalkForwardEvaluator.Run(PredictionModelFactory.Create(modelName), history, evaluation, Options());
        var mismatches = reference.Mismatches(modelName, rounds);

        Assert.True(mismatches.Count == 0, $"{modelName}: {mismatches.Count} predykcji widzi mecze rozegrane później — {string.Join("; ", mismatches)}.");
        Assert.Equal(
            evaluation.Where(m => reference.HiddenFrom(m).Count > 0).Select(m => m.Id),
            rounds.SelectMany(round => round.DateFilteredMatchIds));
    }

    [Theory]
    [MemberData(nameof(Models))]
    public void FixtureDetectsAbsorbingARoundBeforePredicting(string modelName)
    {
        var (history, evaluation) = Split(League(withPostponements: false));
        var reference = new WalkForwardReference(history, evaluation, Options());

        foreach (var round in SeasonCalendar.RoundsInOrder(evaluation))
        {
            var matchesInRound = evaluation.Where(m => m.Round == round).ToList();
            var match = matchesInRound[0];
            var visible = reference.VisibleTo(match);

            var beforeRound = reference.Predict(modelName, visible, match);
            var withRound = reference.Predict(modelName, visible.Concat(matchesInRound).ToList(), match);

            Assert.True(
                WalkForwardReference.Differs(beforeRound, withRound, DetectableDifference),
                $"{modelName}: wchłonięcie kolejki {round} nie zmienia predykcji — test nie wykryłby wchłaniania przed predykcją.");
        }
    }

    [Theory]
    [MemberData(nameof(Models))]
    public void FixtureDetectsMatchesPlayedAfterThePredictedOne(string modelName)
    {
        var (history, evaluation) = Split(League(withPostponements: true));
        var reference = new WalkForwardReference(history, evaluation, Options());

        var affected = evaluation.Where(m => reference.HiddenFrom(m).Count > 0).ToList();

        Assert.Equal(9, affected.Count);
        Assert.Equal([5, 6, 8], affected.Select(m => m.Round!.Value).Distinct().Order());

        foreach (var match in affected)
        {
            var visibleOnly = reference.Predict(modelName, reference.VisibleTo(match), match);
            var withLaterMatches = reference.Predict(modelName, reference.AbsorbedBefore(match), match);

            Assert.True(
                WalkForwardReference.Differs(visibleOnly, withLaterMatches, DetectableDifference),
                $"{modelName}: mecz {match.Id} z kolejki {match.Round} nie reaguje na mecze rozegrane po nim — test nie wykryłby ich widoczności.");
        }
    }

    [Theory]
    [MemberData(nameof(Models))]
    public void DateFilterHidesPostponedMatchesFromTheRoundPrediction(string modelName)
    {
        var (history, evaluation) = Split(League(withPostponements: true));
        var reference = new WalkForwardReference(history, evaluation, Options());
        var round = evaluation.First(m => reference.HiddenFrom(m).Count > 0).Round!.Value;
        var matchesInRound = evaluation.Where(m => m.Round == round).OrderBy(m => m.Id).ToList();
        var absorbed = reference.AbsorbedBefore(matchesInRound[0]);

        var model = PredictionModelFactory.Create(modelName);
        model.Train(absorbed, Options());

        var (predictions, filtered) = WalkForwardEvaluator.PredictWithDateFilter(
            model, absorbed, matchesInRound, Options(), () => PredictionModelFactory.Create(modelName), round);

        Assert.Equal(matchesInRound.Where(m => reference.HiddenFrom(m).Count > 0).Select(m => m.Id), filtered);
        Assert.NotEmpty(filtered);
        Assert.Equal(matchesInRound.Select(m => m.Id), predictions.Select(p => p.MatchId));

        foreach (var (match, prediction) in matchesInRound.Zip(predictions))
        {
            var expected = reference.Predict(modelName, reference.VisibleTo(match), match);

            Assert.False(
                WalkForwardReference.Differs(expected, prediction, WalkForwardReference.Tolerance),
                $"{modelName}: mecz {match.Id} z kolejki {round} widzi mecze rozegrane po nim.");
        }

        Assert.Contains(
            matchesInRound.Zip(predictions),
            pair => filtered.Contains(pair.First.Id)
                && WalkForwardReference.Differs(model.Predict(pair.First), pair.Second, DetectableDifference));
    }

    [Theory]
    [MemberData(nameof(Models))]
    public void DateFilterUsesTheBaseModelWhenNothingIsHidden(string modelName)
    {
        var (history, evaluation) = Split(League(withPostponements: false));
        var round = SeasonCalendar.RoundsInOrder(evaluation).First();
        var matchesInRound = evaluation.Where(m => m.Round == round).OrderBy(m => m.Id).ToList();
        var created = 0;

        var model = PredictionModelFactory.Create(modelName);
        model.Train(history, Options());

        var (predictions, filtered) = WalkForwardEvaluator.PredictWithDateFilter(
            model, history, matchesInRound, Options(),
            () =>
            {
                created++;
                return PredictionModelFactory.Create(modelName);
            },
            round);

        Assert.Empty(filtered);
        Assert.Equal(0, created);
        Assert.All(
            matchesInRound.Zip(predictions),
            pair => Assert.True(WalkForwardReference.Identical(model.Predict(pair.First), pair.Second)));
    }

    [Fact]
    public void FailedRestrictedFitNamesTheRoundAndTheMatch()
    {
        var (history, evaluation) = Split(League(withPostponements: true));
        var reference = new WalkForwardReference(history, evaluation, Options());
        var firstAffected = evaluation.First(m => reference.HiddenFrom(m).Count > 0);

        var exception = Assert.Throws<ModelConvergenceException>(() => WalkForwardEvaluator.Run(
            PredictionModelFactory.Create("Poisson"), history, evaluation, Options(),
            createModel: () => new DixonColesModel(maxIterations: 1)));

        var inner = Assert.IsType<ModelConvergenceException>(exception.InnerException);

        Assert.StartsWith($"model tymczasowy filtra daty przed kolejką {firstAffected.Round}, mecz {firstAffected.Id},", exception.Reason);
        Assert.EndsWith(inner.Reason, exception.Reason);
        Assert.Same(inner.Report, exception.Report);
    }

    private static (List<MatchData> History, List<MatchData> Evaluation) Split(List<MatchData> league)
    {
        return (
            WalkForwardEvaluator.BuildHistory(league, [PreviousSeason, TargetSeason], TargetSeason, Cutoff),
            WalkForwardEvaluator.BuildEvaluationSet(league, TargetSeason, Cutoff));
    }

    private static TrainingOptions Options()
    {
        return new TrainingOptions
        {
            LeagueId = TestLeague.LeagueId,
            SeasonId = TargetSeason,
            SeasonChronology = [PreviousSeason, TargetSeason],
            UseFormFactors = true
        };
    }

    private static List<MatchData> League(bool withPostponements)
    {
        var schedule = DoubleRoundRobin(Teams);
        var matches = new List<MatchData>();
        var id = 1;

        foreach (var (seasonId, start) in new[] { (PreviousSeason, PreviousStart), (TargetSeason, TargetStart) })
        {
            for (var index = 0; index < schedule.Count; index++)
            {
                var round = index + 1;

                for (var slot = 0; slot < schedule[index].Length; slot++)
                {
                    var (home, away) = schedule[index][slot];
                    matches.Add(new MatchData(
                        id, start.AddDays(7 * round + slot), round, seasonId, TestLeague.LeagueId,
                        home, away, (id * 7 + 3) % 4, (id * 5 + 1) % 3));
                    id++;
                }
            }
        }

        if (!withPostponements)
        {
            return matches;
        }

        return matches
            .Select(m => m.SeasonId == TargetSeason && m.Round == 2 && m.Date == TargetStart.AddDays(14)
                ? m with { Date = TargetStart.AddDays(7 * 6 + 3) }
                : m.SeasonId == TargetSeason && m.Round == 7 && m.Date == TargetStart.AddDays(49)
                    ? m with { Date = TargetStart.AddDays(7 * 8 + 3) }
                    : m)
            .ToList();
    }

    private static List<(int Home, int Away)[]> DoubleRoundRobin(int[] teams)
    {
        var rotation = teams.ToList();
        var firstHalf = new List<(int Home, int Away)[]>();

        for (var round = 0; round < teams.Length - 1; round++)
        {
            var pairs = new (int Home, int Away)[teams.Length / 2];

            for (var i = 0; i < pairs.Length; i++)
            {
                var first = rotation[i];
                var second = rotation[teams.Length - 1 - i];
                pairs[i] = (round + i) % 2 == 0 ? (first, second) : (second, first);
            }

            firstHalf.Add(pairs);

            var last = rotation[^1];
            rotation.RemoveAt(rotation.Count - 1);
            rotation.Insert(1, last);
        }

        return firstHalf
            .Concat(firstHalf.Select(pairs => pairs.Select(p => (p.Away, p.Home)).ToArray()))
            .ToList();
    }
}
