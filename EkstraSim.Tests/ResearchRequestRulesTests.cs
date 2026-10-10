using EkstraSim.Shared.Requests;
using EkstraSim.Shared.Resources;

namespace EkstraSim.Tests;

public class ResearchRequestRulesTests
{
    public static IEnumerable<object[]> InvalidNonNegatives =>
        new[] { -1e-12, -0.1, double.NaN, double.PositiveInfinity, double.NegativeInfinity }.Select(value => new object[] { value });

    [Fact]
    public void DefaultRequestsAreValid()
    {
        Assert.Null(ResearchRequestRules.Validate(new CreateEvaluationRunRequest()));
        Assert.Null(ResearchRequestRules.Validate(new PredictRoundRequest { Round = 1 }));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(1)]
    [InlineData(18)]
    public void RunCutoffIsAutomaticOrAtLeastOne(int? cutoff)
    {
        Assert.Null(ResearchRequestRules.Validate(new CreateEvaluationRunRequest { TrainingLastRound = cutoff }));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void RunCutoffBelowOneIsRejected(int cutoff)
    {
        var message = ResearchRequestRules.Validate(new CreateEvaluationRunRequest { TrainingLastRound = cutoff });

        Assert.Equal(string.Format(SnackbarMessages.Research_Cutoff_TooLow, cutoff), message);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.0065)]
    [InlineData(1.0)]
    public void TrainingCoefficientsAcceptZeroAndPositiveValues(double value)
    {
        Assert.Null(ResearchRequestRules.Validate(new CreateEvaluationRunRequest { TimeDecayXi = value, RidgeLambda = value }));
        Assert.Null(ResearchRequestRules.Validate(new PredictRoundRequest { Round = 19, TimeDecayXi = value, RidgeLambda = value }));
    }

    [Theory]
    [MemberData(nameof(InvalidNonNegatives))]
    public void TimeDecayMustBeFiniteAndNonNegative(double value)
    {
        Assert.Equal(SnackbarMessages.Research_TimeDecay_Invalid, ResearchRequestRules.Validate(new CreateEvaluationRunRequest { TimeDecayXi = value }));
        Assert.Equal(SnackbarMessages.Research_TimeDecay_Invalid, ResearchRequestRules.Validate(new PredictRoundRequest { Round = 19, TimeDecayXi = value }));
    }

    [Theory]
    [MemberData(nameof(InvalidNonNegatives))]
    public void RidgeMustBeFiniteAndNonNegative(double value)
    {
        Assert.Equal(SnackbarMessages.Research_Ridge_Invalid, ResearchRequestRules.Validate(new CreateEvaluationRunRequest { RidgeLambda = value }));
        Assert.Equal(SnackbarMessages.Research_Ridge_Invalid, ResearchRequestRules.Validate(new PredictRoundRequest { Round = 19, RidgeLambda = value }));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.25)]
    public void ToleranceAcceptsZeroAndPositiveValues(double tolerance)
    {
        Assert.Null(ResearchRequestRules.ValidateTolerance(tolerance));
        Assert.Null(ResearchRequestRules.Validate(new CreateEvaluationRunRequest { StabilityTolerance = tolerance }));
    }

    [Theory]
    [MemberData(nameof(InvalidNonNegatives))]
    public void ToleranceRuleIsSharedByRunCreationAndComparison(double tolerance)
    {
        Assert.Equal(SnackbarMessages.Research_Tolerance_Invalid, ResearchRequestRules.ValidateTolerance(tolerance));
        Assert.Equal(SnackbarMessages.Research_Tolerance_Invalid, ResearchRequestRules.Validate(new CreateEvaluationRunRequest { StabilityTolerance = tolerance }));
    }

    [Fact]
    public void StabilityWindowOfOneIsAccepted()
    {
        Assert.Null(ResearchRequestRules.Validate(new CreateEvaluationRunRequest { StabilityWindow = 1 }));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void StabilityWindowBelowOneIsRejected(int window)
    {
        var message = ResearchRequestRules.Validate(new CreateEvaluationRunRequest { StabilityWindow = window });

        Assert.Equal(string.Format(SnackbarMessages.Research_Window_TooLow, window), message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void PredictedRoundBelowOneIsRejected(int round)
    {
        var message = ResearchRequestRules.Validate(new PredictRoundRequest { Round = round });

        Assert.Equal(string.Format(SnackbarMessages.Research_Round_TooLow, round), message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(18)]
    public void PredictionCutoffBeforeTheRoundIsAccepted(int? cutoff)
    {
        Assert.Null(ResearchRequestRules.Validate(new PredictRoundRequest { Round = 19, TrainingLastRound = cutoff }));
    }

    [Theory]
    [InlineData(19)]
    [InlineData(20)]
    [InlineData(60)]
    public void PredictionCutoffAtOrAfterTheRoundIsRejected(int cutoff)
    {
        var message = ResearchRequestRules.Validate(new PredictRoundRequest { Round = 19, TrainingLastRound = cutoff });

        Assert.Equal(string.Format(SnackbarMessages.Research_PredictCutoff_NotBeforeRound, cutoff, 19), message);
    }

    [Fact]
    public void NegativePredictionCutoffIsRejected()
    {
        var message = ResearchRequestRules.Validate(new PredictRoundRequest { Round = 19, TrainingLastRound = -1 });

        Assert.Equal(string.Format(SnackbarMessages.Research_PredictCutoff_Negative, -1), message);
    }

    [Fact]
    public void FirstRoundCanBePredictedOnlyFromEarlierSeasons()
    {
        Assert.Null(ResearchRequestRules.Validate(new PredictRoundRequest { Round = 1, TrainingLastRound = 0 }));
        Assert.NotNull(ResearchRequestRules.Validate(new PredictRoundRequest { Round = 1, TrainingLastRound = 1 }));
    }

    [Fact]
    public void MessagesAreLoadedFromResources()
    {
        string?[] messages =
        [
            SnackbarMessages.Research_Cutoff_TooLow,
            SnackbarMessages.Research_Cutoff_NoRoundsToEvaluate,
            SnackbarMessages.Research_TimeDecay_Invalid,
            SnackbarMessages.Research_Ridge_Invalid,
            SnackbarMessages.Research_Tolerance_Invalid,
            SnackbarMessages.Research_Window_TooLow,
            SnackbarMessages.Research_Round_TooLow,
            SnackbarMessages.Research_PredictCutoff_Negative,
            SnackbarMessages.Research_PredictCutoff_NotBeforeRound,
            SnackbarMessages.Research_Model_Unknown,
            SnackbarMessages.Research_Metric_Unknown,
            SnackbarMessages.Research_Run_NotFound,
            SnackbarMessages.Research_Run_NotFinished,
            SnackbarMessages.Research_Run_EndedWithError,
            SnackbarMessages.Research_Run_Interrupted
        ];

        Assert.All(messages, message => Assert.False(string.IsNullOrWhiteSpace(message)));
        Assert.Contains("{0}", SnackbarMessages.Research_Cutoff_TooLow);
        Assert.Contains("{1}", SnackbarMessages.Research_PredictCutoff_NotBeforeRound);
        Assert.Contains("{1}", SnackbarMessages.Research_Model_Unknown);
        Assert.Contains("{1}", SnackbarMessages.Research_Metric_Unknown);
        Assert.Contains("{0}", SnackbarMessages.Research_Cutoff_NoRoundsToEvaluate);
        Assert.Contains("{0}", SnackbarMessages.Research_Run_NotFound);
        Assert.Contains("{0}", SnackbarMessages.Research_Run_NotFinished);
        Assert.Contains("{0}", SnackbarMessages.Research_Run_EndedWithError);
    }
}
