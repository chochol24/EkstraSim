using System.Globalization;
using EkstraSim.Prediction.Evaluation;
using EkstraSim.Prediction.Metrics;
using EkstraSim.Prediction.Models;
using EkstraSim.Prediction.Statistics;
using Xunit.Abstractions;

namespace EkstraSim.Tests;

public class BaselineAndCalibrationRealDataTests
{
    private const double MeanTolerance = 5e-5;
    private const double ThousandthTolerance = 5e-4;
    private const double HundredthTolerance = 5e-3;
    private const double NoiseTolerance = 5e-6;
    private const string PoissonWithoutForms = "PoissonBezForm";
    private const int SimulationsPerMatch = 1000;
    private const int Replications = 200;
    private const int NoiseSeed = 20261014;

    private static readonly string[] ModelOrder = [PredictionModelFactory.Elo, PredictionModelFactory.DixonColes, PredictionModelFactory.Poisson];
    private static readonly string[] CalibratedModels = [.. ModelOrder, PoissonWithoutForms];
    private static readonly int[] Seasons = [RealDataFixture.Season2425, RealDataFixture.Season2526];
    private static readonly MetricKind[] OutcomeMetrics = [MetricKind.RankedProbability, MetricKind.Brier, MetricKind.LogLoss];

    private static readonly Lazy<IReadOnlyList<SeasonEvaluation>> Evaluations = new(Compute);

    private readonly ITestOutputHelper _output;

    public BaselineAndCalibrationRealDataTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Theory]
    [Trait("Category", "RealData")]
    [InlineData(RealDataFixture.Season2425, PredictionModelFactory.Elo, 1.470, 1.141, 1.134, 0.881, 0.526, 2.33, 0, 0.445, 0.251)]
    [InlineData(RealDataFixture.Season2425, PredictionModelFactory.DixonColes, 1.502, 1.164, 1.089, 0.393, 0.521, 2.56, 0, 0.446, 0.257)]
    [InlineData(RealDataFixture.Season2425, PredictionModelFactory.Poisson, 1.739, 1.155, 0.427, -0.052, 1.282, 4.34, 16, 0.492, 0.216)]
    [InlineData(RealDataFixture.Season2425, PoissonWithoutForms, 1.544, 1.187, 0.777, 0.110, 0.648, 2.87, 0, 0.453, 0.244)]
    [InlineData(RealDataFixture.Season2526, PredictionModelFactory.Elo, 1.491, 1.142, 1.142, 0.974, 0.398, 2.36, 0, 0.450, 0.253)]
    [InlineData(RealDataFixture.Season2526, PredictionModelFactory.DixonColes, 1.551, 1.172, 0.828, 0.360, 0.477, 2.78, 0, 0.452, 0.263)]
    [InlineData(RealDataFixture.Season2526, PredictionModelFactory.Poisson, 1.899, 1.146, 0.239, 0.011, 1.074, 5.45, 17, 0.522, 0.221)]
    [InlineData(RealDataFixture.Season2526, PoissonWithoutForms, 1.677, 1.208, 0.299, 0.174, 0.585, 3.61, 6, 0.476, 0.240)]
    public void CalibrationPinsTheSpreadOfTheForecasts(int seasonId, string modelName, double meanLambdaHome, double meanLambdaAway, double homeSlope, double awaySlope, double differenceSpread, double maxLambdaHome, int aboveThree, double homeWin, double draw)
    {
        var evaluation = Season(seasonId);
        var summary = CalibrationSummary.From(Predictions(evaluation.Rounds[modelName]), evaluation.Season.Evaluation);

        Assert.Equal(meanLambdaHome, summary.MeanExpectedHomeGoals, ThousandthTolerance);
        Assert.Equal(meanLambdaAway, summary.MeanExpectedAwayGoals, ThousandthTolerance);
        Assert.Equal(homeSlope, summary.HomeGoalsSlope, ThousandthTolerance);
        Assert.Equal(awaySlope, summary.AwayGoalsSlope, ThousandthTolerance);
        Assert.Equal(differenceSpread, summary.ExpectedGoalDifferenceStandardDeviation, ThousandthTolerance);
        Assert.Equal(maxLambdaHome, summary.MaxExpectedHomeGoals, HundredthTolerance);
        Assert.Equal(aboveThree, summary.MatchesAboveThreeExpectedHomeGoals);
        Assert.Equal(homeWin, summary.MeanHomeWinProbability, ThousandthTolerance);
        Assert.Equal(draw, summary.MeanDrawProbability, ThousandthTolerance);
    }

    [Theory]
    [Trait("Category", "RealData")]
    [InlineData(RealDataFixture.Season2425, 1.611, 1.243, 0.465, 0.243, 0.292)]
    [InlineData(RealDataFixture.Season2526, 1.444, 1.146, 0.424, 0.278, 0.299)]
    public void SpringOutcomesAreTheSameForEveryModel(int seasonId, double homeGoals, double awayGoals, double homeWins, double draws, double awayWins)
    {
        var evaluation = Season(seasonId);

        foreach (var modelName in CalibratedModels)
        {
            var summary = CalibrationSummary.From(Predictions(evaluation.Rounds[modelName]), evaluation.Season.Evaluation);

            Assert.Equal(evaluation.Season.Evaluation.Count, summary.MatchCount);
            Assert.Equal(homeGoals, summary.MeanHomeGoals, ThousandthTolerance);
            Assert.Equal(awayGoals, summary.MeanAwayGoals, ThousandthTolerance);
            Assert.Equal(homeWins, summary.HomeWinFrequency, ThousandthTolerance);
            Assert.Equal(draws, summary.DrawFrequency, ThousandthTolerance);
            Assert.Equal(awayWins, summary.AwayWinFrequency, ThousandthTolerance);
        }
    }

    [Theory]
    [Trait("Category", "RealData")]
    [InlineData(PredictionModelFactory.Poisson, true, 0, 43, 0.169, 0.395)]
    [InlineData(PredictionModelFactory.Poisson, true, 4, 70, 0.805, 0.514)]
    [InlineData(PredictionModelFactory.Poisson, false, 2, 40, 0.480, 0.225)]
    [InlineData(PoissonWithoutForms, true, 4, 14, 0.773, 0.643)]
    [InlineData(PredictionModelFactory.DixonColes, true, 2, 134, 0.470, 0.448)]
    [InlineData(PredictionModelFactory.DixonColes, true, 3, 49, 0.617, 0.592)]
    [InlineData(PredictionModelFactory.Elo, true, 3, 47, 0.614, 0.638)]
    public void ReliabilityOfBothSpringsPinsTheBins(string modelName, bool homeWin, int binIndex, int count, double meanForecast, double observed)
    {
        var summary = BothSprings(modelName);
        var bin = (homeWin ? summary.HomeWinReliability : summary.AwayWinReliability)[binIndex];

        Assert.Equal(288, summary.MatchCount);
        Assert.Equal(count, bin.Count);
        Assert.Equal(meanForecast, bin.MeanForecast, ThousandthTolerance);
        Assert.Equal(observed, bin.ObservedFrequency, ThousandthTolerance);
    }

    [Theory]
    [Trait("Category", "RealData")]
    [InlineData(PredictionModelFactory.Poisson, 0.975)]
    [InlineData(PoissonWithoutForms, 0.893)]
    [InlineData(PredictionModelFactory.DixonColes, 0.782)]
    [InlineData(PredictionModelFactory.Elo, 0.707)]
    public void HighestHomeWinForecastOfBothSprings(string modelName, double highest)
    {
        var predictions = Evaluations.Value.SelectMany(e => Predictions(e.Rounds[modelName]));

        Assert.Equal(highest, predictions.Max(p => p.HomeWinProbability), ThousandthTolerance);
    }

    [Theory]
    [Trait("Category", "RealData")]
    [InlineData(RealDataFixture.Season2425, 0.9941, 1.0576, 1.0003, 1.0739, 0.9951, 0.9306, 0.9997, 0.9459, 1616, 0.3199)]
    [InlineData(RealDataFixture.Season2526, 1.0101, 1.0757, 0.9969, 1.0976, 0.9944, 0.9250, 1.0031, 0.9345, 1922, 0.3403)]
    public void FormComponentsPinTheDoubleHomeAdvantage(int seasonId, double homeRecent, double homeVenue, double homeHeadToHead, double homeProduct, double awayRecent, double awayVenue, double awayHeadToHead, double awayProduct, int historyMatches, double historyGoalDifference)
    {
        var evaluation = Season(seasonId);
        var forms = evaluation.Forms;

        Assert.Equal(evaluation.Season.Evaluation.Count, forms.Count);
        Assert.Equal(homeRecent, forms.Average(f => f.Home.Recent), MeanTolerance);
        Assert.Equal(homeVenue, forms.Average(f => f.Home.Venue), MeanTolerance);
        Assert.Equal(homeHeadToHead, forms.Average(f => f.Home.HeadToHead), MeanTolerance);
        Assert.Equal(homeProduct, forms.Average(f => f.Home.Product), MeanTolerance);
        Assert.Equal(awayRecent, forms.Average(f => f.Away.Recent), MeanTolerance);
        Assert.Equal(awayVenue, forms.Average(f => f.Away.Venue), MeanTolerance);
        Assert.Equal(awayHeadToHead, forms.Average(f => f.Away.HeadToHead), MeanTolerance);
        Assert.Equal(awayProduct, forms.Average(f => f.Away.Product), MeanTolerance);
        Assert.Equal(historyMatches, evaluation.HistoryMatchCount);
        Assert.Equal(historyGoalDifference, evaluation.HistoryHomeGoalDifference, MeanTolerance);
    }

    [Theory]
    [Trait("Category", "RealData")]
    [InlineData(RealDataFixture.Season2425)]
    [InlineData(RealDataFixture.Season2526)]
    public void FormComponentsReproduceThePoissonPredictions(int seasonId)
    {
        var evaluation = Season(seasonId);
        var withForms = Predictions(evaluation.Rounds[PredictionModelFactory.Poisson]);
        var withoutForms = Predictions(evaluation.Rounds[PoissonWithoutForms]);
        var matchIds = evaluation.Season.Evaluation
            .Where(m => m.IsPlayed)
            .OrderBy(m => m.Round)
            .ThenBy(m => m.Id)
            .Select(m => m.Id)
            .ToList();
        var formsById = matchIds.Zip(evaluation.Forms).ToDictionary(pair => pair.First, pair => pair.Second);

        foreach (var (formed, plain) in withForms.Zip(withoutForms))
        {
            var forms = formsById[formed.MatchId];

            Assert.Equal(plain.MatchId, formed.MatchId);
            Assert.Equal(plain.ExpectedHomeGoals * forms.Home.Product, formed.ExpectedHomeGoals, 1e-12);
            Assert.Equal(plain.ExpectedAwayGoals * forms.Away.Product, formed.ExpectedAwayGoals, 1e-12);
        }
    }

    [Theory]
    [Trait("Category", "RealData")]
    [InlineData(RealDataFixture.Season2425, 0.4375, 0.2686, 0.2939, 0.2281, 67, 1, 1, 20)]
    [InlineData(RealDataFixture.Season2526, 0.4428, 0.2674, 0.2898, 0.2270, 61, 1, 1, 20)]
    public void ReferenceRulesPinTheHits(int seasonId, double homeWin, double draw, double awayWin, double constantRps, int alwaysHomeHits, int scoreHome, int scoreAway, int scoreHits)
    {
        var evaluation = Season(seasonId);

        Assert.Equal(homeWin, evaluation.ConstantProbabilities.HomeWin, MeanTolerance);
        Assert.Equal(draw, evaluation.ConstantProbabilities.Draw, MeanTolerance);
        Assert.Equal(awayWin, evaluation.ConstantProbabilities.AwayWin, MeanTolerance);
        Assert.Equal(MatchOutcome.HomeWin, evaluation.ConstantProbabilities.MostLikely());
        Assert.Equal(constantRps, MetricMeans.Of(evaluation.Constant).RankedProbability, MeanTolerance);
        Assert.Equal(alwaysHomeHits, evaluation.Constant.Count(e => e.OutcomeCorrect));
        Assert.Equal(alwaysHomeHits, evaluation.Season.Evaluation.Count(m => m.HomeScore > m.AwayScore));
        Assert.Equal((scoreHome, scoreAway), evaluation.ConstantScore);
        Assert.Equal(scoreHits, evaluation.ConstantScoreHits.Count(h => h.Correct));
        Assert.Equal(evaluation.Season.Evaluation.Count, evaluation.ConstantScoreHits.Count);
    }

    [Theory]
    [Trait("Category", "RealData")]
    [InlineData(RealDataFixture.Season2425, MetricKind.RankedProbability, PredictionModelFactory.DixonColes, 0.2186, 0.2281, 0.1818, 0.4574)]
    [InlineData(RealDataFixture.Season2425, MetricKind.RankedProbability, PredictionModelFactory.Elo, 0.2189, 0.2281, 0.1525, 0.4574)]
    [InlineData(RealDataFixture.Season2425, MetricKind.RankedProbability, PredictionModelFactory.Poisson, 0.2518, 0.2281, 0.2303, 0.4574)]
    [InlineData(RealDataFixture.Season2425, MetricKind.Brier, PredictionModelFactory.DixonColes, 0.6215, 0.6408, 0.1973, 0.5856)]
    [InlineData(RealDataFixture.Season2425, MetricKind.Brier, PredictionModelFactory.Elo, 0.6211, 0.6408, 0.1987, 0.5856)]
    [InlineData(RealDataFixture.Season2425, MetricKind.Brier, PredictionModelFactory.Poisson, 0.6941, 0.6408, 0.1952, 0.5856)]
    [InlineData(RealDataFixture.Season2425, MetricKind.LogLoss, PredictionModelFactory.DixonColes, 1.0339, 1.0613, 0.1496, 0.4489)]
    [InlineData(RealDataFixture.Season2425, MetricKind.LogLoss, PredictionModelFactory.Elo, 1.0333, 1.0613, 0.1636, 0.4489)]
    [InlineData(RealDataFixture.Season2425, MetricKind.LogLoss, PredictionModelFactory.Poisson, 1.1493, 1.0613, 0.2628, 0.4489)]
    [InlineData(RealDataFixture.Season2526, MetricKind.RankedProbability, PredictionModelFactory.DixonColes, 0.2207, 0.2270, 0.2008, 0.2008)]
    [InlineData(RealDataFixture.Season2526, MetricKind.RankedProbability, PredictionModelFactory.Elo, 0.2169, 0.2270, 0.0280, 0.0841)]
    [InlineData(RealDataFixture.Season2526, MetricKind.RankedProbability, PredictionModelFactory.Poisson, 0.2604, 0.2270, 0.0588, 0.1176)]
    [InlineData(RealDataFixture.Season2526, MetricKind.Brier, PredictionModelFactory.DixonColes, 0.6457, 0.6548, 0.3605, 0.3605)]
    [InlineData(RealDataFixture.Season2526, MetricKind.Brier, PredictionModelFactory.Elo, 0.6345, 0.6548, 0.0334, 0.1003)]
    [InlineData(RealDataFixture.Season2526, MetricKind.Brier, PredictionModelFactory.Poisson, 0.7324, 0.6548, 0.0382, 0.1003)]
    [InlineData(RealDataFixture.Season2526, MetricKind.LogLoss, PredictionModelFactory.DixonColes, 1.0699, 1.0813, 0.3339, 0.3339)]
    [InlineData(RealDataFixture.Season2526, MetricKind.LogLoss, PredictionModelFactory.Elo, 1.0534, 1.0813, 0.0501, 0.1502)]
    [InlineData(RealDataFixture.Season2526, MetricKind.LogLoss, PredictionModelFactory.Poisson, 1.2145, 1.0813, 0.0615, 0.1502)]
    public void ModelsVersusConstantPinTheTests(int seasonId, MetricKind metric, string modelName, double modelMean, double constantMean, double pValue, double holm)
    {
        var evaluation = Season(seasonId);
        var rows = ModelComparison.VersusBaseline(ByModel(evaluation, ModelOrder), evaluation.Constant, metric);
        var row = rows.Single(r => r.FirstModel == modelName);

        Assert.Equal(3, rows.Count);
        Assert.Equal(ConstantBaseline.Name, row.SecondModel);
        Assert.Equal(evaluation.Season.Evaluation.Count, row.PairedMatchCount);
        Assert.Equal(modelMean, row.FirstMean, MeanTolerance);
        Assert.Equal(constantMean, row.SecondMean, MeanTolerance);
        Assert.Equal(pValue, row.Test.PValue, MeanTolerance);
        Assert.Equal(holm, row.AdjustedPValue, MeanTolerance);
    }

    [Theory]
    [Trait("Category", "RealData")]
    [InlineData(RealDataFixture.Season2425, 0.2253, 0.7855, 62, 19, 41)]
    [InlineData(RealDataFixture.Season2526, 0.2352, 0.4612, 63, 18, 43)]
    public void PoissonWithoutFormsIsCloseToTheConstant(int seasonId, double rps, double pValue, int outcomeHits, int exactScoreHits, int topThreeHits)
    {
        var evaluation = Season(seasonId);
        var evaluations = Sorted(evaluation.Rounds[PoissonWithoutForms]);
        var single = new Dictionary<string, IReadOnlyList<MatchEvaluation>> { [PoissonWithoutForms] = evaluations };
        var row = ModelComparison.VersusBaseline(single, evaluation.Constant, MetricKind.RankedProbability).Single();

        Assert.Equal(rps, MetricMeans.Of(evaluations).RankedProbability, MeanTolerance);
        Assert.Equal(pValue, row.Test.PValue, MeanTolerance);
        Assert.Equal(outcomeHits, evaluations.Count(e => e.OutcomeCorrect));
        Assert.Equal(exactScoreHits, evaluations.Count(e => e.ExactScoreCorrect));
        Assert.Equal(topThreeHits, evaluations.Count(e => e.ExactScoreInTopThree));
    }

    [Theory]
    [Trait("Category", "RealData")]
    [InlineData(RealDataFixture.Season2425, PredictionModelFactory.DixonColes, 19, 23, 13, 32, 0.0567, 1.0)]
    [InlineData(RealDataFixture.Season2425, PredictionModelFactory.DixonColes, 24, 28, 15, 30, -0.0445, 1.0)]
    [InlineData(RealDataFixture.Season2425, PredictionModelFactory.DixonColes, 29, 33, 15, 30, 0.0579, 0.3972)]
    [InlineData(RealDataFixture.Season2425, PredictionModelFactory.DixonColes, 34, 34, 2, 7, 0.1768, 1.0)]
    [InlineData(RealDataFixture.Season2425, PredictionModelFactory.Elo, 19, 23, 13, 32, 0.0497, 1.0)]
    [InlineData(RealDataFixture.Season2425, PredictionModelFactory.Elo, 24, 28, 15, 30, -0.0386, 1.0)]
    [InlineData(RealDataFixture.Season2425, PredictionModelFactory.Elo, 29, 33, 15, 30, 0.0499, 0.5786)]
    [InlineData(RealDataFixture.Season2425, PredictionModelFactory.Elo, 34, 34, 2, 7, 0.1530, 1.0)]
    [InlineData(RealDataFixture.Season2425, PredictionModelFactory.Poisson, 19, 23, 13, 32, 0.0228, 1.0)]
    [InlineData(RealDataFixture.Season2425, PredictionModelFactory.Poisson, 24, 28, 15, 30, -0.0657, 1.0)]
    [InlineData(RealDataFixture.Season2425, PredictionModelFactory.Poisson, 29, 33, 15, 30, 0.0402, 1.0)]
    [InlineData(RealDataFixture.Season2425, PredictionModelFactory.Poisson, 34, 34, 2, 7, 0.3347, 1.0)]
    [InlineData(RealDataFixture.Season2526, PredictionModelFactory.DixonColes, 19, 23, 15, 30, -0.0514, 1.0)]
    [InlineData(RealDataFixture.Season2526, PredictionModelFactory.DixonColes, 24, 28, 14, 31, 0.0334, 1.0)]
    [InlineData(RealDataFixture.Season2526, PredictionModelFactory.DixonColes, 29, 33, 13, 32, 0.0585, 1.0)]
    [InlineData(RealDataFixture.Season2526, PredictionModelFactory.DixonColes, 34, 34, 3, 6, 0.0325, 1.0)]
    [InlineData(RealDataFixture.Season2526, PredictionModelFactory.Elo, 19, 23, 15, 30, -0.0579, 1.0)]
    [InlineData(RealDataFixture.Season2526, PredictionModelFactory.Elo, 24, 28, 14, 31, 0.0223, 1.0)]
    [InlineData(RealDataFixture.Season2526, PredictionModelFactory.Elo, 29, 33, 13, 32, 0.0532, 1.0)]
    [InlineData(RealDataFixture.Season2526, PredictionModelFactory.Elo, 34, 34, 3, 6, 0.0310, 1.0)]
    [InlineData(RealDataFixture.Season2526, PredictionModelFactory.Poisson, 19, 23, 15, 30, -0.1250, 0.4474)]
    [InlineData(RealDataFixture.Season2526, PredictionModelFactory.Poisson, 24, 28, 14, 31, 0.0254, 1.0)]
    [InlineData(RealDataFixture.Season2526, PredictionModelFactory.Poisson, 29, 33, 13, 32, 0.0774, 1.0)]
    [InlineData(RealDataFixture.Season2526, PredictionModelFactory.Poisson, 34, 34, 3, 6, 0.1268, 1.0)]
    public void PromotedWindowsPinTheRows(int seasonId, string modelName, int fromRound, int toRound, int promotedCount, int otherCount, double difference, double holm)
    {
        var evaluation = Season(seasonId);
        var windows = ModelComparison.RoundWindows(SeasonCalendar.RoundsInOrder(evaluation.Season.Evaluation));
        var rows = ModelComparison.PromotedVersusRest(ByModel(evaluation, ModelOrder), MetricKind.RankedProbability, windows);
        var row = rows.Single(r => r.ModelName == modelName && r.FromRound == fromRound && r.ToRound == toRound);

        Assert.Equal([(19, 23), (24, 28), (29, 33), (34, 34)], windows);
        Assert.Equal(12, rows.Count);
        Assert.Equal(promotedCount, row.PromotedCount);
        Assert.Equal(otherCount, row.OtherCount);
        Assert.Equal(difference, row.Difference, MeanTolerance);
        Assert.Equal(holm, row.AdjustedPValue, MeanTolerance);
    }

    [Theory]
    [Trait("Category", "RealData")]
    [InlineData(RealDataFixture.Season2425, PredictionModelFactory.Elo, 0.00012, 0.00094)]
    [InlineData(RealDataFixture.Season2425, PredictionModelFactory.DixonColes, 0.00012, 0.00088)]
    [InlineData(RealDataFixture.Season2425, PredictionModelFactory.Poisson, 0.00021, 0.00090)]
    [InlineData(RealDataFixture.Season2526, PredictionModelFactory.Elo, 0.00023, 0.00094)]
    [InlineData(RealDataFixture.Season2526, PredictionModelFactory.DixonColes, 0.00025, 0.00093)]
    [InlineData(RealDataFixture.Season2526, PredictionModelFactory.Poisson, 0.00020, 0.00098)]
    public void MonteCarloNoisePinsTheShiftAndSpread(int seasonId, string modelName, double shift, double spread)
    {
        var noise = Season(seasonId).Noise;

        Assert.Equal(SimulationsPerMatch, noise.SimulationsPerMatch);
        Assert.Equal(Replications, noise.SimulatedMeans[modelName].Count);
        Assert.Equal(shift, noise.MeanShift(modelName), NoiseTolerance);
        Assert.Equal(spread, noise.StandardDeviation(modelName), NoiseTolerance);
    }

    [Theory]
    [Trait("Category", "RealData")]
    [InlineData(RealDataFixture.Season2425, 0.42)]
    [InlineData(RealDataFixture.Season2526, 0.005)]
    public void MonteCarloNoiseReversesDixonColesAndElo(int seasonId, double reversalRate)
    {
        var noise = Season(seasonId).Noise;

        Assert.Equal(reversalRate, noise.ReversalRate(PredictionModelFactory.DixonColes, PredictionModelFactory.Elo), 1e-12);
        Assert.Equal(0.0, noise.ReversalRate(PredictionModelFactory.Elo, PredictionModelFactory.Poisson));
        Assert.Equal(0.0, noise.ReversalRate(PredictionModelFactory.DixonColes, PredictionModelFactory.Poisson));
    }

    [Theory]
    [Trait("Category", "RealData")]
    [InlineData(RealDataFixture.Season2425)]
    [InlineData(RealDataFixture.Season2526)]
    public void MonteCarloExactMeansAreTheRunScores(int seasonId)
    {
        var evaluation = Season(seasonId);

        foreach (var modelName in ModelOrder)
        {
            Assert.Equal(MetricMeans.Of(Sorted(evaluation.Rounds[modelName])).RankedProbability, evaluation.Noise.ExactMeans[modelName], 1e-15);
        }
    }

    [Fact]
    [Trait("Category", "RealData")]
    public void Report()
    {
        foreach (var evaluation in Evaluations.Value)
        {
            ReportSeason(evaluation);
        }

        ReportReliability();

        Assert.Equal(Seasons.Length, Evaluations.Value.Count);
    }

    private void ReportSeason(SeasonEvaluation evaluation)
    {
        var season = evaluation.Season;
        var byModel = ByModel(evaluation, ModelOrder);

        _output.WriteLine("");
        _output.WriteLine(Invariant($"=== Sezon {season.SeasonId}, odcięcie po k.{season.TrainingLastRound}: {season.Evaluation.Count} meczów ==="));

        _output.WriteLine("kalibracja:");
        _output.WriteLine(Invariant($"  {"model",-15} {"RPS",8} {"śr. λ",7} {"śr. μ",7} {"gole d.",7} {"gole g.",7} {"nach. d.",8} {"nach. g.",8} {"SD λ−μ",7} {"max λ",6} {"λ>3",4} {"p1",6} {"pX",6} {"p2",6} | {"fr. 1",6} {"fr. X",6} {"fr. 2",6}"));

        foreach (var modelName in CalibratedModels)
        {
            var summary = CalibrationSummary.From(Predictions(evaluation.Rounds[modelName]), season.Evaluation);
            var rps = MetricMeans.Of(Sorted(evaluation.Rounds[modelName])).RankedProbability;

            _output.WriteLine(Invariant(
                $"  {modelName,-15} {rps,8:F4} {summary.MeanExpectedHomeGoals,7:F3} {summary.MeanExpectedAwayGoals,7:F3} {summary.MeanHomeGoals,7:F3} {summary.MeanAwayGoals,7:F3} {summary.HomeGoalsSlope,8:F3} {summary.AwayGoalsSlope,8:F3} {summary.ExpectedGoalDifferenceStandardDeviation,7:F3} {summary.MaxExpectedHomeGoals,6:F2} {summary.MatchesAboveThreeExpectedHomeGoals,4} {summary.MeanHomeWinProbability,6:F3} {summary.MeanDrawProbability,6:F3} {summary.MeanAwayWinProbability,6:F3} | {summary.HomeWinFrequency,6:F3} {summary.DrawFrequency,6:F3} {summary.AwayWinFrequency,6:F3}"));
        }

        var forms = evaluation.Forms;
        _output.WriteLine(Invariant($"mnożniki formy Poissona (średnie z {forms.Count} meczów): gospodarz forma10 {forms.Average(f => f.Home.Recent):F4}, dom {forms.Average(f => f.Home.Venue):F4}, H2H {forms.Average(f => f.Home.HeadToHead):F4}, łącznie {forms.Average(f => f.Home.Product):F4}; gość forma10 {forms.Average(f => f.Away.Recent):F4}, wyjazd {forms.Average(f => f.Away.Venue):F4}, H2H {forms.Average(f => f.Away.HeadToHead):F4}, łącznie {forms.Average(f => f.Away.Product):F4}"));
        _output.WriteLine(Invariant($"historia treningowa: {evaluation.HistoryMatchCount} meczów, średnia różnica bramek gospodarza {evaluation.HistoryHomeGoalDifference:+0.0000;-0.0000}"));

        var constant = evaluation.ConstantProbabilities;
        _output.WriteLine(Invariant($"stała predykcja: P = {constant.HomeWin:F4} / {constant.Draw:F4} / {constant.AwayWin:F4}, RPS {MetricMeans.Of(evaluation.Constant).RankedProbability:F4}, Brier {MetricMeans.Of(evaluation.Constant).Brier:F4}, log-loss {MetricMeans.Of(evaluation.Constant).LogLoss:F4}, trafienia 1X2 („zawsze {constant.MostLikely()}”) {evaluation.Constant.Count(e => e.OutcomeCorrect)}"));
        _output.WriteLine(Invariant($"najczęstszy wynik historii: {evaluation.ConstantScore.Home}:{evaluation.ConstantScore.Away}, trafienia top-1 {evaluation.ConstantScoreHits.Count(h => h.Correct)}"));
        _output.WriteLine("  trafienia modeli (1X2 / top-1 / top-3): " + string.Join("; ", CalibratedModels.Select(modelName =>
        {
            var evaluations = Sorted(evaluation.Rounds[modelName]);
            return Invariant($"{modelName} {evaluations.Count(e => e.OutcomeCorrect)} / {evaluations.Count(e => e.ExactScoreCorrect)} / {evaluations.Count(e => e.ExactScoreInTopThree)}");
        })));

        _output.WriteLine("modele vs stała predykcja (Wilcoxon, Holm w rodzinie 3 modeli):");

        foreach (var metric in OutcomeMetrics)
        {
            foreach (var row in ModelComparison.VersusBaseline(byModel, evaluation.Constant, metric))
            {
                _output.WriteLine(Invariant($"  {metric,-17} {row.FirstModel,-11} {row.FirstMean:F4} vs {row.SecondMean:F4}, różnica {row.FirstMean - row.SecondMean:+0.0000;-0.0000}, n {row.PairedMatchCount}, p {row.Test.PValue:F4}, Holm {row.AdjustedPValue:F4}"));
            }
        }

        var poissonOnly = new Dictionary<string, IReadOnlyList<MatchEvaluation>> { [PredictionModelFactory.Poisson] = byModel[PredictionModelFactory.Poisson] };
        var noForms = new Dictionary<string, IReadOnlyList<MatchEvaluation>> { [PoissonWithoutForms] = Sorted(evaluation.Rounds[PoissonWithoutForms]) };
        foreach (var single in new[] { poissonOnly, noForms })
        {
            var row = ModelComparison.VersusBaseline(single, evaluation.Constant, MetricKind.RankedProbability).Single();
            _output.WriteLine(Invariant($"  (pojedynczo, bez rodziny) {row.FirstModel,-15} RPS {row.FirstMean:F4} vs {row.SecondMean:F4}, p {row.Test.PValue:F4}"));
        }

        var windows = ModelComparison.RoundWindows(SeasonCalendar.RoundsInOrder(season.Evaluation));
        _output.WriteLine(Invariant($"beniaminki w oknach kolejek ({string.Join(", ", windows.Select(w => Invariant($"{w.From}–{w.To}")))}), RPS, Holm w rodzinie okien:"));

        foreach (var row in ModelComparison.PromotedVersusRest(byModel, MetricKind.RankedProbability, windows))
        {
            _output.WriteLine(Invariant($"  {row.ModelName,-11} {row.FromRound}–{row.ToRound}: {row.PromotedMean:F4} ({row.PromotedCount}) vs {row.OtherMean:F4} ({row.OtherCount}), różnica {row.Difference:+0.0000;-0.0000}, p {row.Test.PValue:F4}, Holm {row.AdjustedPValue:F4}"));
        }

        var noise = evaluation.Noise;
        _output.WriteLine(Invariant($"szum Monte Carlo (N = {noise.SimulationsPerMatch} na mecz, {noise.Replications} powtórzeń, ziarno {NoiseSeed}):"));

        foreach (var modelName in ModelOrder)
        {
            var means = noise.SimulatedMeans[modelName];
            _output.WriteLine(Invariant($"  {modelName,-11} RPS dokładny {noise.ExactMeans[modelName]:F4}, z symulacji średnio {means.Average():F4} (przesunięcie {noise.MeanShift(modelName):+0.00000;-0.00000}), SD {noise.StandardDeviation(modelName):F5}, zakres {means.Min():F4}–{means.Max():F4}"));
        }

        _output.WriteLine(Invariant($"  odwrócona kolejność Dixon-Coles / Elo: {noise.ReversalRate(PredictionModelFactory.DixonColes, PredictionModelFactory.Elo):P1} powtórzeń; Elo / Poisson: {noise.ReversalRate(PredictionModelFactory.Elo, PredictionModelFactory.Poisson):P1}; Dixon-Coles / Poisson: {noise.ReversalRate(PredictionModelFactory.DixonColes, PredictionModelFactory.Poisson):P1}"));
    }

    private void ReportReliability()
    {
        _output.WriteLine("");
        _output.WriteLine("=== Krzywa niezawodności, obie wiosny razem ===");

        var matches = Evaluations.Value.SelectMany(e => e.Season.Evaluation).ToList();

        foreach (var modelName in CalibratedModels)
        {
            var predictions = Evaluations.Value.SelectMany(e => Predictions(e.Rounds[modelName])).ToList();
            var summary = CalibrationSummary.From(predictions, matches);

            _output.WriteLine(Invariant($"{modelName} ({summary.MatchCount} meczów), max p1 {predictions.Max(p => p.HomeWinProbability):F3}:"));
            _output.WriteLine("  p1: " + string.Join("; ", summary.HomeWinReliability.Select(Bin)));
            _output.WriteLine("  p2: " + string.Join("; ", summary.AwayWinReliability.Select(Bin)));
        }
    }

    private static string Bin(ReliabilityBin bin)
    {
        return bin.Count == 0
            ? Invariant($"[{bin.From:F2}, {bin.To:F2}) —")
            : Invariant($"[{bin.From:F2}, {bin.To:F2}) n {bin.Count}, prognoza {bin.MeanForecast:F3}, faktycznie {bin.ObservedFrequency:F3}");
    }

    private static IReadOnlyList<SeasonEvaluation> Compute()
    {
        return Seasons
            .AsParallel()
            .AsOrdered()
            .Select(seasonId =>
            {
                var promoted = RealDataFixture.PromotedTeamIds(seasonId);
                var season = SensitivitySeason.Build(
                    RealDataFixture.Matches,
                    RealDataFixture.SeasonChronology,
                    RealDataFixture.LeagueId,
                    seasonId,
                    RealDataFixture.TrainingLastRound,
                    promoted);
                var promotedIds = promoted.ToHashSet();
                var options = RealDataFixture.Options(seasonId);

                var rounds = ModelOrder.ToDictionary(
                    modelName => modelName,
                    modelName => (IReadOnlyList<RoundEvaluation>)WalkForwardEvaluator.Run(
                        PredictionModelFactory.Create(modelName), season.History, season.Evaluation, options, promotedIds));
                rounds[PoissonWithoutForms] = WalkForwardEvaluator.Run(
                    new PoissonModel(), season.History, season.Evaluation, options with { UseFormFactors = false }, promotedIds);

                var probabilities = ConstantBaseline.Fit(season.History, RealDataFixture.LeagueId);
                var score = ConstantScoreBaseline.Fit(season.History, RealDataFixture.LeagueId);
                var history = season.History.Where(m => m.LeagueId == RealDataFixture.LeagueId && m.IsPlayed).ToList();

                return new SeasonEvaluation(
                    season,
                    rounds,
                    probabilities,
                    ConstantBaseline.Evaluate(probabilities, season.Evaluation, promotedIds),
                    score,
                    ConstantScoreBaseline.Evaluate(score, season.Evaluation),
                    FormComponents(season, options),
                    history.Count,
                    history.Average(m => (double)(m.HomeScore!.Value - m.AwayScore!.Value)),
                    MonteCarloNoise.Run(
                        ModelOrder.ToDictionary(modelName => modelName, modelName => Predictions(rounds[modelName])),
                        season.Evaluation,
                        SimulationsPerMatch,
                        Replications,
                        NoiseSeed));
            })
            .ToList();
    }

    private static IReadOnlyList<(FormFactors Home, FormFactors Away)> FormComponents(SensitivitySeason season, TrainingOptions options)
    {
        var model = new PoissonModel();
        model.Train(season.History, options);

        var components = new List<(FormFactors Home, FormFactors Away)>();

        foreach (var round in SeasonCalendar.RoundsInOrder(season.Evaluation))
        {
            var matches = season.Evaluation
                .Where(m => m.Round == round && m.IsPlayed)
                .OrderBy(m => m.Id)
                .ToList();

            components.AddRange(matches.Select(match => (
                model.FormComponents(match, match.HomeTeamId, isHome: true),
                model.FormComponents(match, match.AwayTeamId, isHome: false))));

            model.UpdateWithRound(matches);
        }

        return components;
    }

    private static SeasonEvaluation Season(int seasonId)
    {
        return Evaluations.Value.Single(evaluation => evaluation.Season.SeasonId == seasonId);
    }

    private static CalibrationSummary BothSprings(string modelName)
    {
        var predictions = Evaluations.Value.SelectMany(e => Predictions(e.Rounds[modelName])).ToList();
        var matches = Evaluations.Value.SelectMany(e => e.Season.Evaluation).ToList();

        return CalibrationSummary.From(predictions, matches);
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<MatchEvaluation>> ByModel(SeasonEvaluation evaluation, IEnumerable<string> modelNames)
    {
        return modelNames.ToDictionary(modelName => modelName, modelName => (IReadOnlyList<MatchEvaluation>)Sorted(evaluation.Rounds[modelName]));
    }

    private static IReadOnlyList<MatchPrediction> Predictions(IReadOnlyList<RoundEvaluation> rounds)
    {
        return rounds.SelectMany(round => round.Predictions).OrderBy(p => p.MatchId).ToList();
    }

    private static List<MatchEvaluation> Sorted(IReadOnlyList<RoundEvaluation> rounds)
    {
        return rounds.SelectMany(round => round.Evaluations).OrderBy(e => e.MatchId).ToList();
    }

    private static string Invariant(FormattableString value) => value.ToString(CultureInfo.InvariantCulture);

    private sealed record SeasonEvaluation(
        SensitivitySeason Season,
        IReadOnlyDictionary<string, IReadOnlyList<RoundEvaluation>> Rounds,
        OutcomeProbabilities ConstantProbabilities,
        IReadOnlyList<MatchEvaluation> Constant,
        (int Home, int Away) ConstantScore,
        IReadOnlyList<ExactScoreHit> ConstantScoreHits,
        IReadOnlyList<(FormFactors Home, FormFactors Away)> Forms,
        int HistoryMatchCount,
        double HistoryHomeGoalDifference,
        MonteCarloNoiseResult Noise);
}
