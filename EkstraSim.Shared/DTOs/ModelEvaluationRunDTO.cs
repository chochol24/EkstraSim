namespace EkstraSim.Shared.DTOs;

public class ModelEvaluationRunDTO
{
    public int Id { get; set; }

    public int LeagueId { get; set; }
    public LeagueDTO? League { get; set; }
    public int SeasonId { get; set; }
    public SeasonDTO? Season { get; set; }

    public int TrainingLastRound { get; set; }
    public string Models { get; set; } = string.Empty;
    public string? OptionsJson { get; set; }
    public string? PromotedTeamsJson { get; set; }
    public string? Comments { get; set; }

    public EvaluationRunStatus Status { get; set; }
    public string? ErrorMessage { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }

    public int EvaluatedMatchCount { get; set; }
    public int EvaluatedRoundCount { get; set; }

    public List<string> ModelNames => Models
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .ToList();
}
