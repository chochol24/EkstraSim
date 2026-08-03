namespace EkstraSim.Prediction.Metrics;

public enum MatchOutcome
{
    HomeWin = 0,
    Draw = 1,
    AwayWin = 2
}

public readonly record struct OutcomeProbabilities(double HomeWin, double Draw, double AwayWin)
{
    public double Of(MatchOutcome outcome) => outcome switch
    {
        MatchOutcome.HomeWin => HomeWin,
        MatchOutcome.Draw => Draw,
        _ => AwayWin
    };

    public MatchOutcome MostLikely()
    {
        if (HomeWin >= Draw && HomeWin >= AwayWin)
        {
            return MatchOutcome.HomeWin;
        }

        return Draw >= AwayWin ? MatchOutcome.Draw : MatchOutcome.AwayWin;
    }

    public double Total => HomeWin + Draw + AwayWin;
}
