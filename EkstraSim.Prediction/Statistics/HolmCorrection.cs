namespace EkstraSim.Prediction.Statistics;

public static class HolmCorrection
{
    public static double[] Adjust(IReadOnlyList<double> pValues)
    {
        var count = pValues.Count;
        var adjusted = new double[count];

        if (count == 0)
        {
            return adjusted;
        }

        var order = Enumerable.Range(0, count)
            .OrderBy(index => pValues[index])
            .ToArray();

        double running = 0;

        for (var step = 0; step < count; step++)
        {
            var index = order[step];
            var candidate = (count - step) * pValues[index];
            running = Math.Max(running, candidate);
            adjusted[index] = Math.Min(1.0, running);
        }

        return adjusted;
    }
}
