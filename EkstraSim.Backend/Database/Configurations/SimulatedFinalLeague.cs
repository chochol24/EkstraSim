using EkstraSim.Backend.Database.Entities;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

namespace EkstraSim.Backend.Database.Configurations;

public class SimulatedFinalLeagueConfiguration : IEntityTypeConfiguration<SimulatedFinalLeague>
{
	public void Configure(EntityTypeBuilder<SimulatedFinalLeague> builder)
	{
		builder.HasKey(sfl => sfl.Id);

		builder.Property(sfl => sfl.RoundBeforeSimulation)
			   .IsRequired();

		builder.HasOne(sfl => sfl.Season)
			   .WithMany()
			   .HasForeignKey(sfl => sfl.SeasonId)
			   .OnDelete(DeleteBehavior.Restrict);

		builder.HasOne(sfl => sfl.League)
			   .WithMany()
			   .HasForeignKey(sfl => sfl.LeagueId)
			   .OnDelete(DeleteBehavior.Restrict);


		builder.Property(smr => smr.NumberOfSimulations)
			   .IsRequired();
	}
}

