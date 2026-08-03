namespace EkstraSim.Prediction.Models;

public sealed class ModelSnapshot
{
    public string ModelName { get; init; } = string.Empty;
    public int? AfterRound { get; set; }
    public IReadOnlyDictionary<string, double> Parameters { get; init; } = new Dictionary<string, double>();

    public static double Distance(ModelSnapshot? previous, ModelSnapshot? current)
    {
        if (previous == null || current == null)
        {
            return 0;
        }

        var keys = previous.Parameters.Keys.Intersect(current.Parameters.Keys);
        double sumOfSquares = 0;

        foreach (var key in keys)
        {
            var delta = current.Parameters[key] - previous.Parameters[key];
            sumOfSquares += delta * delta;
        }

        return Math.Sqrt(sumOfSquares);
    }
}
