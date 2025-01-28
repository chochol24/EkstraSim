using EkstraSim.Backend.Database.Entities;
using Microsoft.EntityFrameworkCore;

namespace EkstraSim.Backend.Database;

public class EkstraSimDbContext : DbContext
{
	public EkstraSimDbContext(DbContextOptions<EkstraSimDbContext> options)
	   : base(options)
	{
	}

	public DbSet<Team> Teams { get; set; }
	public DbSet<Season> Seasons { get; set; }
	public DbSet<League> Leagues { get; set; }
	public DbSet<Match> Matches { get; set; }
	public DbSet<SimulatedMatchResult> SimulatedMatchResults { get; set; }
	public DbSet<SimulatedTeamInFinalTable> SimulatedTeamInFinalTables { get; set; }
	public DbSet<SimulatedFinalLeague> SimulatedFinalLeagues{ get; set; }
	public DbSet<SimulatedRound> SimulatedRounds{ get; set; }
	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		base.OnModelCreating(modelBuilder);

		modelBuilder.ApplyConfigurationsFromAssembly(typeof(EkstraSimDbContext).Assembly);

	}
}
