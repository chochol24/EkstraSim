namespace Shared.DTOs;

public class TeamDTO
{
	public int Id { get; set; }
	public string Name { get; set; } = null!;
	public decimal Elo { get; set; }
}
