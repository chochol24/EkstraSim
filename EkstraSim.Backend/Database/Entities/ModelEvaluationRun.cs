using EkstraSim.Shared;

namespace EkstraSim.Backend.Database.Entities;

public class ModelEvaluationRun
{
    public int Id { get; set; }

    public int LeagueId { get; set; }
    public League League { get; set; } = null!;
    public int SeasonId { get; set; }
    public Season Season { get; set; } = null!;

    public int TrainingLastRound { get; set; }
    public string Models { get; set; } = string.Empty;
    public string? OptionsJson { get; set; }
    public string? PromotedTeamsJson { get; set; }
    public string? Comments { get; set; }

    public EvaluationRunStatus Status { get; set; } = EvaluationRunStatus.Pending;
    public string? ErrorMessage { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }

    public int EvaluatedMatchCount { get; set; }
    public int EvaluatedRoundCount { get; set; }

    public List<ModelPrediction> Predictions { get; set; } = [];
    public List<ModelRoundMetric> RoundMetrics { get; set; } = [];
}
