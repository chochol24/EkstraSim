using System.Text.RegularExpressions;

namespace EkstraSim.Shared.DTOs;

public class SeasonDTO
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int LeagueId { get; set; }
    public LeagueDTO League { get; set; } = null!;

    public ICollection<MatchDTO> Matches { get; set; } = new List<MatchDTO>();
}
