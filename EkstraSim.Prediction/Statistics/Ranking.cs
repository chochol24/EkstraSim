namespace EkstraSim.Prediction.Statistics;

public static class Ranking
{
    public static (double[] Ranks, double TieAdjustment) AverageRanks(IReadOnlyList<double> values)
    {
        var order = Enumerable.Range(0, values.Count)
            .OrderBy(index => values[index])
            .ToArray();

        var ranks = new double[values.Count];
        double tieAdjustment = 0;
        var position = 0;

        while (position < order.Length)
        {
            var groupEnd = position;
            while (groupEnd + 1 < order.Length
                && values[order[groupEnd + 1]].Equals(values[order[position]]))
            {
                groupEnd++;
            }

            var groupSize = groupEnd - position + 1;
            var averageRank = (position + groupEnd + 2) / 2.0;

            for (var i = position; i <= groupEnd; i++)
            {
                ranks[order[i]] = averageRank;
            }

            if (groupSize > 1)
            {
                tieAdjustment += Math.Pow(groupSize, 3) - groupSize;
            }

            position = groupEnd + 1;
        }

        return (ranks, tieAdjustment);
    }
}
