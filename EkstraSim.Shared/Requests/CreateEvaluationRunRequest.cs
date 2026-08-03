using System.ComponentModel.DataAnnotations;

namespace EkstraSim.Shared.Requests;

public class CreateEvaluationRunRequest
{
    [Required(ErrorMessage = "Id ligi jest wymagane")]
    [Range(1, int.MaxValue, ErrorMessage = "Id ligi musi być większe niż 0")]
    public int LeagueId { get; set; }

    [Required(ErrorMessage = "Id sezonu jest wymagane")]
    [Range(1, int.MaxValue, ErrorMessage = "Id sezonu musi być większe niż 0")]
    public int SeasonId { get; set; }

    public int? TrainingLastRound { get; set; }

    public List<string> Models { get; set; } = [];

    public bool UseFormFactors { get; set; } = true;
    public double TimeDecayXi { get; set; } = 0.0065;
    public double RidgeLambda { get; set; } = 0.05;

    public double StabilityThreshold { get; set; } = 0.05;
    public int StabilityWindow { get; set; } = 3;

    public string? Comments { get; set; }
}
