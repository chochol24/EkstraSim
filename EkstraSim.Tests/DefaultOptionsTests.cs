using EkstraSim.Prediction.Evaluation;
using EkstraSim.Prediction.Models;
using EkstraSim.Shared.Requests;

namespace EkstraSim.Tests;

public class DefaultOptionsTests
{
    [Fact]
    public void EveryDefaultTimeDecayIsTheLiteratureValue()
    {
        var expected = BitConverter.DoubleToInt64Bits(SensitivityGrid.LiteratureTimeDecayXi);

        Assert.Equal(expected, BitConverter.DoubleToInt64Bits(new TrainingOptions().TimeDecayXi));
        Assert.Equal(expected, BitConverter.DoubleToInt64Bits(new CreateEvaluationRunRequest().TimeDecayXi));
        Assert.Equal(expected, BitConverter.DoubleToInt64Bits(new PredictRoundRequest().TimeDecayXi));
        Assert.Equal(0.0065 / 3.5, SensitivityGrid.LiteratureTimeDecayXi);
    }

    [Fact]
    public void OtherDefaultsAreUnchanged()
    {
        var options = new TrainingOptions();
        var run = new CreateEvaluationRunRequest();
        var round = new PredictRoundRequest();

        Assert.Equal((true, 0.05), (options.UseFormFactors, options.RidgeLambda));
        Assert.Equal((true, 0.05), (run.UseFormFactors, run.RidgeLambda));
        Assert.Equal((true, 0.05), (round.UseFormFactors, round.RidgeLambda));
        Assert.Equal((0.67, 0.3, 0.03), (options.CurrentSeasonScale, options.PreviousSeasonScale, options.HistoricalScale));
        Assert.Equal((10.0, 1300.0, 100.0), (options.EloK, options.EloInitialRating, options.EloHomeAdvantage));
    }

    [Fact]
    public void AlgorithmVersionIsSix()
    {
        Assert.Equal(6, ResearchAlgorithm.Version);
    }
}
