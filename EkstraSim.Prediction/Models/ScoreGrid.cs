using MathNet.Numerics.Distributions;

namespace EkstraSim.Prediction.Models;

public static class ScoreGrid
{
    public const int DefaultMaxGoals = 10;

    public static double[,] FromIndependentPoisson(double lambdaHome, double lambdaAway, int maxGoals = DefaultMaxGoals)
    {
        var homeMass = PoissonMass(lambdaHome, maxGoals);
        var awayMass = PoissonMass(lambdaAway, maxGoals);
        var grid = new double[maxGoals + 1, maxGoals + 1];

        for (var home = 0; home <= maxGoals; home++)
        {
            for (var away = 0; away <= maxGoals; away++)
            {
                grid[home, away] = homeMass[home] * awayMass[away];
            }
        }

        return Normalize(grid);
    }

    public static double[,] FromDixonColes(double lambdaHome, double lambdaAway, double rho, int maxGoals = DefaultMaxGoals)
    {
        var homeMass = PoissonMass(lambdaHome, maxGoals);
        var awayMass = PoissonMass(lambdaAway, maxGoals);
        var grid = new double[maxGoals + 1, maxGoals + 1];

        for (var home = 0; home <= maxGoals; home++)
        {
            for (var away = 0; away <= maxGoals; away++)
            {
                var joint = homeMass[home] * awayMass[away] * LowScoreCorrection(home, away, lambdaHome, lambdaAway, rho);
                grid[home, away] = Math.Max(0, joint);
            }
        }

        return Normalize(grid);
    }

    public static double LowScoreCorrection(int homeGoals, int awayGoals, double lambdaHome, double lambdaAway, double rho)
    {
        if (homeGoals == 0 && awayGoals == 0)
        {
            return 1 - lambdaHome * lambdaAway * rho;
        }

        if (homeGoals == 0 && awayGoals == 1)
        {
            return 1 + lambdaHome * rho;
        }

        if (homeGoals == 1 && awayGoals == 0)
        {
            return 1 + lambdaAway * rho;
        }

        if (homeGoals == 1 && awayGoals == 1)
        {
            return 1 - rho;
        }

        return 1;
    }

    public static double[] PoissonMass(double lambda, int maxGoals)
    {
        var safeLambda = double.IsFinite(lambda) && lambda > 0 ? lambda : 1e-6;
        var mass = new double[maxGoals + 1];

        for (var k = 0; k <= maxGoals; k++)
        {
            mass[k] = Poisson.PMF(safeLambda, k);
        }

        return mass;
    }

    public static double[,] Normalize(double[,] grid)
    {
        var total = Sum(grid);
        if (total <= 0)
        {
            return grid;
        }

        var size = grid.GetLength(0);
        for (var home = 0; home < size; home++)
        {
            for (var away = 0; away < grid.GetLength(1); away++)
            {
                grid[home, away] /= total;
            }
        }

        return grid;
    }

    public static double Sum(double[,] grid)
    {
        double total = 0;
        for (var home = 0; home < grid.GetLength(0); home++)
        {
            for (var away = 0; away < grid.GetLength(1); away++)
            {
                total += grid[home, away];
            }
        }

        return total;
    }

    public static (double HomeWin, double Draw, double AwayWin) Outcomes(double[,] grid)
    {
        double homeWin = 0, draw = 0, awayWin = 0;

        for (var home = 0; home < grid.GetLength(0); home++)
        {
            for (var away = 0; away < grid.GetLength(1); away++)
            {
                var probability = grid[home, away];
                if (home > away)
                {
                    homeWin += probability;
                }
                else if (home == away)
                {
                    draw += probability;
                }
                else
                {
                    awayWin += probability;
                }
            }
        }

        return (homeWin, draw, awayWin);
    }

    public static (int Home, int Away) MostLikelyScore(double[,] grid)
    {
        return RankedScores(grid).First();
    }

    public static IEnumerable<(int Home, int Away)> RankedScores(double[,] grid)
    {
        var scores = new List<(int Home, int Away, double Probability)>();

        for (var home = 0; home < grid.GetLength(0); home++)
        {
            for (var away = 0; away < grid.GetLength(1); away++)
            {
                scores.Add((home, away, grid[home, away]));
            }
        }

        return scores
            .OrderByDescending(x => x.Probability)
            .ThenBy(x => x.Home + x.Away)
            .ThenBy(x => x.Home)
            .Select(x => (x.Home, x.Away));
    }

    public static double ProbabilityOf(double[,] grid, int homeScore, int awayScore)
    {
        if (homeScore < 0 || awayScore < 0
            || homeScore >= grid.GetLength(0) || awayScore >= grid.GetLength(1))
        {
            return 0;
        }

        return grid[homeScore, awayScore];
    }
}
