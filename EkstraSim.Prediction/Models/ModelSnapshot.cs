namespace EkstraSim.Prediction.Models;

public sealed class ModelSnapshot
{
    private const double Epsilon = 1e-12;

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

    public static double NormalisedDistance(ModelSnapshot? previous, ModelSnapshot? current)
    {
        if (previous == null || current == null)
        {
            return 0;
        }

        var keys = previous.Parameters.Keys.Intersect(current.Parameters.Keys).ToList();

        if (keys.Count == 0)
        {
            return 0;
        }

        double weightedSumOfSquares = 0;
        var totalWeight = 0;

        foreach (var group in GroupIntoFamilies(keys))
        {
            double sumOfSquares = 0;

            foreach (var key in group)
            {
                var delta = current.Parameters[key] - previous.Parameters[key];
                sumOfSquares += delta * delta;
            }

            var rootMeanSquareChange = Math.Sqrt(sumOfSquares / group.Count);
            var relativeChange = rootMeanSquareChange / Spread(previous, group);

            weightedSumOfSquares += group.Count * relativeChange * relativeChange;
            totalWeight += group.Count;
        }

        return Math.Sqrt(weightedSumOfSquares / totalWeight);
    }

    private static List<List<string>> GroupIntoFamilies(List<string> keys)
    {
        var families = keys
            .GroupBy(Family)
            .Where(family => family.Count() > 1)
            .Select(family => family.ToList())
            .ToList();

        return families.Count > 0 ? families : [keys];
    }

    private static string Family(string key)
    {
        var segments = key
            .Split('_')
            .Where(segment => segment.Length > 0 && !segment.All(char.IsDigit));

        return string.Join('_', segments);
    }

    private static double Spread(ModelSnapshot snapshot, List<string> keys)
    {
        double sum = 0;

        foreach (var key in keys)
        {
            sum += snapshot.Parameters[key];
        }

        var mean = sum / keys.Count;
        double sumOfSquaredDeviations = 0;

        foreach (var key in keys)
        {
            var deviation = snapshot.Parameters[key] - mean;
            sumOfSquaredDeviations += deviation * deviation;
        }

        var standardDeviation = Math.Sqrt(sumOfSquaredDeviations / keys.Count);

        if (standardDeviation > Epsilon)
        {
            return standardDeviation;
        }

        double sumOfSquaredValues = 0;

        foreach (var key in keys)
        {
            var value = snapshot.Parameters[key];
            sumOfSquaredValues += value * value;
        }

        var magnitude = Math.Sqrt(sumOfSquaredValues / keys.Count);

        return magnitude > Epsilon ? magnitude : 1;
    }
}
