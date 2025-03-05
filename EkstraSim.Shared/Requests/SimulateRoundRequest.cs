using Shared;
using System.ComponentModel.DataAnnotations;

namespace EkstraSim.Shared.Requests;

public class SimulateRoundRequest()
{
    [Required(ErrorMessage = "Runda jest wymagana")]
    [Range(1, Constants.NumberOfRoundsEkstaklasa, ErrorMessage = "Dana runda nie istnieje.")]
    public int Round { get; set; }

    [Required(ErrorMessage = "Id sezonu jest wymagane")]
    [Range(1, int.MaxValue, ErrorMessage = "Id sezonu musi być większe niż 0")]
    public int SeasonId { get; set; }

    [Required(ErrorMessage = "Id ligi jest wymagane")]
    [Range(1, int.MaxValue, ErrorMessage = "Id ligi musi być większe niż 0")]
    public int LeagueId { get; set; }

    [Required(ErrorMessage = "Liczba symulacji jest wymagana")]
    [Range(1, 1000000, ErrorMessage = "Liczba symulacji musi być pomiędzy 1 a 1000000")]
    public int NumberOfSimualtions { get; set; }
}

