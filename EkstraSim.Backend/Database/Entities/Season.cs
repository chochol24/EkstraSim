namespace EkstraSim.Backend.Database.Entities;

public class Season
{
	public int Id { get; set; }
	public string Name { get; set; } = string.Empty;
	public int LeagueId { get; set; }
	public League League { get; set; } = null!;

	public ICollection<Match> Matches { get; set; } = new List<Match>();
}
