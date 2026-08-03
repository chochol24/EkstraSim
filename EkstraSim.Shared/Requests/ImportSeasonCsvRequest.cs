using System.ComponentModel.DataAnnotations;

namespace EkstraSim.Shared.Requests;

public class ImportSeasonCsvRequest
{
    [Required(ErrorMessage = "Id ligi jest wymagane")]
    [Range(1, int.MaxValue, ErrorMessage = "Id ligi musi być większe niż 0")]
    public int LeagueId { get; set; }

    [Required(ErrorMessage = "Nazwa sezonu jest wymagana")]
    public string SeasonName { get; set; } = string.Empty;

    public string? FilePath { get; set; }

    public string? CsvContent { get; set; }
}
