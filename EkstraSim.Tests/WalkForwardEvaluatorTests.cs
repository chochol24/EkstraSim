using EkstraSim.Prediction.Evaluation;
using EkstraSim.Prediction.Models;

namespace EkstraSim.Tests;

public class WalkForwardEvaluatorTests
{
    private const int PreviousSeason = 9;
    private const int TargetSeason = 10;
    private const int Cutoff = 4;

    private const int RelegatedTeam = 500;

    private static readonly int[] Teams = [100, 200, 300, 400];

    public static IEnumerable<object[]> Models => PredictionModelFactory.AvailableModels.Select(name => new object[] { name });

    private static MatchData Match(int id, int seasonId, int round, DateTime date, int home, int away, int? homeScore, int? awayScore)
    {
        return new MatchData(id, date, round, seasonId, TestLeague.LeagueId, home, away, homeScore, awayScore);
    }

    private static List<MatchData> League(bool lastRoundUnplayed = false)
    {
        var matches = new List<MatchData>();
        var id = 1;

        var previousStart = new DateTime(2023, 8, 1);
        for (var round = 1; round <= 6; round++)
        {
            var date = previousStart.AddDays(round * 7);
            matches.Add(Match(id++, PreviousSeason, round, date, Teams[0], Teams[1], 2, 1));
            matches.Add(Match(id++, PreviousSeason, round, date, Teams[2], Teams[3], 1, 1));
        }

        var targetStart = new DateTime(2024, 8, 1);
        for (var round = 1; round <= 8; round++)
        {
            var date = targetStart.AddDays(round * 7);
            var played = !(lastRoundUnplayed && round == 8);

            matches.Add(Match(id++, TargetSeason, round, date, Teams[0], Teams[2], played ? 2 : null, played ? 0 : null));
            matches.Add(Match(id++, TargetSeason, round, date, Teams[1], Teams[3], played ? 1 : null, played ? 2 : null));
        }

        return matches;
    }

    private static List<MatchData> LeagueWithVariedScores()
    {
        var matches = new List<MatchData>();
        var id = 1;

        var previousStart = new DateTime(2023, 8, 1);
        for (var round = 1; round <= 6; round++)
        {
            var date = previousStart.AddDays(round * 7);
            matches.Add(Match(id++, PreviousSeason, round, date, Teams[0], Teams[1], 2, 1));
            matches.Add(Match(id++, PreviousSeason, round, date, Teams[2], Teams[3], 1, 1));
        }

        var targetStart = new DateTime(2024, 8, 1);
        for (var round = 1; round <= 8; round++)
        {
            var date = targetStart.AddDays(round * 7);

            matches.Add(Match(id++, TargetSeason, round, date, Teams[0], Teams[2], round % 4, (round + 1) % 3));
            matches.Add(Match(id++, TargetSeason, round, date, Teams[1], Teams[3], (round + 2) % 3, round % 3));
        }

        return matches;
    }

    private static List<MatchData> LeagueWithRelegatedTeam()
    {
        var matches = new List<MatchData>();
        var id = 1;

        var previousStart = new DateTime(2023, 8, 1);
        for (var round = 1; round <= 6; round++)
        {
            var date = previousStart.AddDays(round * 7);
            matches.Add(Match(id++, PreviousSeason, round, date, Teams[0], Teams[1], round % 3, (round + 1) % 2));
            matches.Add(Match(id++, PreviousSeason, round, date, Teams[2], Teams[3], (round + 1) % 3, round % 2));
            matches.Add(Match(id++, PreviousSeason, round, date.AddDays(1), RelegatedTeam, Teams[round % 4], round % 2, (round + 2) % 3));
        }

        var targetStart = new DateTime(2024, 8, 1);
        for (var round = 1; round <= 8; round++)
        {
            var date = targetStart.AddDays(round * 7);

            matches.Add(Match(id++, TargetSeason, round, date, Teams[0], Teams[2], round % 4, (round + 1) % 3));
            matches.Add(Match(id++, TargetSeason, round, date, Teams[1], Teams[3], (round + 2) % 3, round % 3));
        }

        return matches;
    }

    private static TrainingOptions Options()
    {
        return new TrainingOptions
        {
            LeagueId = TestLeague.LeagueId,
            SeasonId = TargetSeason,
            SeasonChronology = [PreviousSeason, TargetSeason],
            UseFormFactors = false
        };
    }

    [Fact]
    public void HistoryCoversEarlierSeasonsAndRoundsUpToCutoff()
    {
        var history = WalkForwardEvaluator.BuildHistory(League(), [PreviousSeason, TargetSeason], TargetSeason, Cutoff);

        Assert.All(history, m => Assert.True(m.IsPlayed));
        Assert.Equal(12, history.Count(m => m.SeasonId == PreviousSeason));
        Assert.Equal(8, history.Count(m => m.SeasonId == TargetSeason));
        Assert.All(history.Where(m => m.SeasonId == TargetSeason), m => Assert.True(m.Round <= Cutoff));
    }

    [Fact]
    public void HistoryIsChronological()
    {
        var history = WalkForwardEvaluator.BuildHistory(League(), [PreviousSeason, TargetSeason], TargetSeason, Cutoff);

        Assert.Equal(history.OrderBy(m => m.Date).Select(m => m.Id), history.Select(m => m.Id));
    }

    [Fact]
    public void HistoryForFirstSeasonInChronologyHasNoPrior()
    {
        var history = WalkForwardEvaluator.BuildHistory(League(), [PreviousSeason, TargetSeason], PreviousSeason, Cutoff);

        Assert.All(history, m => Assert.Equal(PreviousSeason, m.SeasonId));
    }

    [Fact]
    public void EvaluationSetCoversRoundsAfterCutoffOnly()
    {
        var evaluationSet = WalkForwardEvaluator.BuildEvaluationSet(League(), TargetSeason, Cutoff);

        Assert.Equal(8, evaluationSet.Count);
        Assert.All(evaluationSet, m => Assert.Equal(TargetSeason, m.SeasonId));
        Assert.All(evaluationSet, m => Assert.True(m.Round > Cutoff));
    }

    [Fact]
    public void EvaluationSetSkipsUnplayedMatches()
    {
        var evaluationSet = WalkForwardEvaluator.BuildEvaluationSet(League(lastRoundUnplayed: true), TargetSeason, Cutoff);

        Assert.Equal(6, evaluationSet.Count);
        Assert.DoesNotContain(evaluationSet, m => m.Round == 8);
    }

    [Fact]
    public void ProducesOneResultPerEvaluatedRound()
    {
        var league = League();
        var history = WalkForwardEvaluator.BuildHistory(league, [PreviousSeason, TargetSeason], TargetSeason, Cutoff);
        var evaluationSet = WalkForwardEvaluator.BuildEvaluationSet(league, TargetSeason, Cutoff);

        var rounds = WalkForwardEvaluator.Run(new PoissonModel(), history, evaluationSet, Options());

        Assert.Equal([5, 6, 7, 8], rounds.Select(r => r.Round));
        Assert.All(rounds, r => Assert.Equal(2, r.Evaluations.Count));
        Assert.All(rounds, r => Assert.Equal(2, r.Predictions.Count));
        Assert.All(rounds, r => Assert.Equal("Poisson", r.ModelName));
    }

    [Fact]
    public void PromotedFlagMarksMatchesWithPromotedSides()
    {
        var league = League();
        var history = WalkForwardEvaluator.BuildHistory(league, [PreviousSeason, TargetSeason], TargetSeason, Cutoff);
        var evaluationSet = WalkForwardEvaluator.BuildEvaluationSet(league, TargetSeason, Cutoff);

        var rounds = WalkForwardEvaluator.Run(new PoissonModel(), history, evaluationSet, Options(), new HashSet<int> { 300 });

        var flagged = rounds.SelectMany(r => r.Evaluations).Where(e => e.InvolvesPromotedTeam).ToList();
        var notFlagged = rounds.SelectMany(r => r.Evaluations).Where(e => !e.InvolvesPromotedTeam).ToList();

        Assert.Equal(4, flagged.Count);
        Assert.Equal(4, notFlagged.Count);
    }

    [Fact]
    public void EvaluationsCarryRoundContext()
    {
        var league = League();
        var history = WalkForwardEvaluator.BuildHistory(league, [PreviousSeason, TargetSeason], TargetSeason, Cutoff);
        var evaluationSet = WalkForwardEvaluator.BuildEvaluationSet(league, TargetSeason, Cutoff);

        var rounds = WalkForwardEvaluator.Run(new PoissonModel(), history, evaluationSet, Options());

        foreach (var round in rounds)
        {
            Assert.All(round.Evaluations, e => Assert.Equal(round.Round, e.Round));
        }
    }

    [Fact]
    public void ParameterDriftIsNeverNegative()
    {
        var league = League();
        var history = WalkForwardEvaluator.BuildHistory(league, [PreviousSeason, TargetSeason], TargetSeason, Cutoff);
        var evaluationSet = WalkForwardEvaluator.BuildEvaluationSet(league, TargetSeason, Cutoff);

        var rounds = WalkForwardEvaluator.Run(new PoissonModel(), history, evaluationSet, Options());

        Assert.All(rounds, r => Assert.True(r.ParameterDrift >= 0));
    }

    [Fact]
    public void RepeatedIdenticalResultsProduceNoDrift()
    {
        var league = League();
        var history = WalkForwardEvaluator.BuildHistory(league, [PreviousSeason, TargetSeason], TargetSeason, Cutoff);
        var evaluationSet = WalkForwardEvaluator.BuildEvaluationSet(league, TargetSeason, Cutoff);

        var rounds = WalkForwardEvaluator.Run(new PoissonModel(), history, evaluationSet, Options());

        Assert.All(rounds, r => Assert.Equal(0, r.ParameterDrift, precision: 10));
    }

    [Fact]
    public void VaryingResultsMoveTheParameters()
    {
        var league = LeagueWithVariedScores();
        var history = WalkForwardEvaluator.BuildHistory(league, [PreviousSeason, TargetSeason], TargetSeason, Cutoff);
        var evaluationSet = WalkForwardEvaluator.BuildEvaluationSet(league, TargetSeason, Cutoff);

        var rounds = WalkForwardEvaluator.Run(new PoissonModel(), history, evaluationSet, Options());

        Assert.Contains(rounds, r => r.ParameterDrift > 0);
    }

    [Theory]
    [MemberData(nameof(Models))]
    public void DriftIsMeasuredOnTeamsOfTheEvaluatedSeasonOnly(string modelName)
    {
        var league = LeagueWithRelegatedTeam();
        var history = WalkForwardEvaluator.BuildHistory(league, [PreviousSeason, TargetSeason], TargetSeason, Cutoff);
        var evaluationSet = WalkForwardEvaluator.BuildEvaluationSet(league, TargetSeason, Cutoff);
        var active = Teams.ToHashSet();

        var rounds = WalkForwardEvaluator.Run(PredictionModelFactory.Create(modelName), history, evaluationSet, Options());

        var replay = PredictionModelFactory.Create(modelName);
        replay.Train(history, Options());
        var previous = replay.GetParametersSnapshot();
        var fullSnapshotDriftDiffers = false;

        Assert.True(
            previous.RestrictToTeams(active).Parameters.Count < previous.Parameters.Count,
            $"{modelName}: snapshot nie ma parametrów drużyny spadkowej — fixture nic nie sprawdza.");

        foreach (var round in rounds)
        {
            replay.UpdateWithRound(evaluationSet.Where(m => m.Round == round.Round).OrderBy(m => m.Id).ToList());
            var current = replay.GetParametersSnapshot();

            Assert.Equal(
                ModelSnapshot.NormalisedDistance(previous.RestrictToTeams(active), current.RestrictToTeams(active)),
                round.ParameterDrift,
                precision: 12);

            fullSnapshotDriftDiffers |= Math.Abs(ModelSnapshot.NormalisedDistance(previous, current) - round.ParameterDrift) > 1e-9;
            previous = current;
        }

        Assert.True(
            fullSnapshotDriftDiffers,
            $"{modelName}: dryf po pełnym snapshocie równy zawężonemu — test nie wykryłby braku zawężenia.");
    }

    [Fact]
    public void SummaryAggregatesTheRound()
    {
        var league = League();
        var history = WalkForwardEvaluator.BuildHistory(league, [PreviousSeason, TargetSeason], TargetSeason, Cutoff);
        var evaluationSet = WalkForwardEvaluator.BuildEvaluationSet(league, TargetSeason, Cutoff);

        var rounds = WalkForwardEvaluator.Run(new PoissonModel(), history, evaluationSet, Options());
        var summary = rounds[0].Summary;

        Assert.Equal("Poisson", summary.ModelName);
        Assert.Equal(2, summary.MatchCount);
        Assert.InRange(summary.Brier, 0, 2);
        Assert.InRange(summary.RankedProbability, 0, 1);
    }

    [Fact]
    public void EveryRegisteredModelCompletesTheWalkForward()
    {
        var league = League();
        var history = WalkForwardEvaluator.BuildHistory(league, [PreviousSeason, TargetSeason], TargetSeason, Cutoff);
        var evaluationSet = WalkForwardEvaluator.BuildEvaluationSet(league, TargetSeason, Cutoff);

        foreach (var modelName in PredictionModelFactory.AvailableModels)
        {
            var rounds = WalkForwardEvaluator.Run(PredictionModelFactory.Create(modelName), history, evaluationSet, Options());

            Assert.Equal(4, rounds.Count);
            Assert.All(rounds, r => Assert.Equal(modelName, r.ModelName));
            Assert.All(rounds.SelectMany(r => r.Predictions), p => Assert.True(double.IsFinite(p.ExpectedHomeGoals)));
        }
    }

    [Fact]
    public void FactoryRejectsUnknownModel()
    {
        Assert.False(PredictionModelFactory.IsKnown("Bayesian"));
        Assert.Throws<ArgumentException>(() => PredictionModelFactory.Create("Bayesian"));
    }

    [Fact]
    public void FactoryIsCaseInsensitive()
    {
        Assert.True(PredictionModelFactory.IsKnown("poisson"));
        Assert.Equal("DixonColes", PredictionModelFactory.Create("dixoncoles").Name);
    }

    [Theory]
    [InlineData("Poisson", "Poisson")]
    [InlineData("  poisson ", "Poisson")]
    [InlineData("DIXONCOLES", "DixonColes")]
    [InlineData("dixonColes", "DixonColes")]
    [InlineData(" Elo ", "Elo")]
    public void CanonicalNameTrimsAndIgnoresCase(string name, string expected)
    {
        Assert.Equal(expected, PredictionModelFactory.CanonicalName(name));
        Assert.True(PredictionModelFactory.IsKnown(name));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Bayesian")]
    [InlineData("Dixon Coles")]
    [InlineData("Poisson,Elo")]
    public void CanonicalNameIsNullForUnknownModels(string? name)
    {
        Assert.Null(PredictionModelFactory.CanonicalName(name));
    }

    [Fact]
    public void CanonicalNamesAreTheAvailableModels()
    {
        Assert.All(
            PredictionModelFactory.AvailableModels,
            name => Assert.Equal(name, PredictionModelFactory.CanonicalName(name.ToLowerInvariant())));
    }
}
