namespace EkstraSim.Shared.DTOs;

public class ModelComparisonDTO
{
    public int RunId { get; set; }
    public string Metric { get; set; } = string.Empty;

    public List<ModelSummaryDTO> Summaries { get; set; } = [];
    public List<PairwiseComparisonDTO> Pairwise { get; set; } = [];
    public List<PromotedComparisonDTO> Promoted { get; set; } = [];
    public List<StabilityDTO> Stability { get; set; } = [];
}

public class ModelSummaryDTO
{
    public string ModelName { get; set; } = string.Empty;
    public int MatchCount { get; set; }

    public double Brier { get; set; }
    public double RankedProbabilityScore { get; set; }
    public double LogLoss { get; set; }

    public double OutcomeAccuracy { get; set; }
    public double ExactScoreAccuracy { get; set; }
    public double ExactScoreTopThreeAccuracy { get; set; }

    public double MeanProbabilityOfActualScore { get; set; }
    public double MeanProbabilityOfActualOutcome { get; set; }
}

public class PairwiseComparisonDTO
{
    public string FirstModel { get; set; } = string.Empty;
    public string SecondModel { get; set; } = string.Empty;

    public double FirstMean { get; set; }
    public double SecondMean { get; set; }
    public int PairedMatchCount { get; set; }

    public string? BetterModel { get; set; }

    public double Statistic { get; set; }
    public double ZScore { get; set; }
    public double PValue { get; set; }
    public double AdjustedPValue { get; set; }
    public bool IsConclusive { get; set; }
    public bool IsSignificant { get; set; }
}

public class PromotedComparisonDTO
{
    public string ModelName { get; set; } = string.Empty;
    public int? FromRound { get; set; }
    public int? ToRound { get; set; }

    public double PromotedMean { get; set; }
    public double OtherMean { get; set; }
    public int PromotedCount { get; set; }
    public int OtherCount { get; set; }
    public double Difference { get; set; }

    public double PValue { get; set; }
    public double AdjustedPValue { get; set; }
    public bool IsConclusive { get; set; }
    public bool IsSignificant { get; set; }
}

public class StabilityDTO
{
    public string ModelName { get; set; } = string.Empty;
    public int? StabilisedFromRound { get; set; }
    public double Threshold { get; set; }
    public int Window { get; set; }

    public List<int> Rounds { get; set; } = [];
    public List<double> RollingMetric { get; set; } = [];
    public List<double> RollingDrift { get; set; } = [];
}
