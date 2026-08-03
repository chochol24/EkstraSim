namespace EkstraSim.Prediction.Models;

public interface IPredictionModel
{
    string Name { get; }

    void Train(IReadOnlyList<MatchData> history, TrainingOptions options);

    void UpdateWithRound(IReadOnlyList<MatchData> playedRound);

    MatchPrediction Predict(MatchData match);

    ModelSnapshot GetParametersSnapshot();
}
