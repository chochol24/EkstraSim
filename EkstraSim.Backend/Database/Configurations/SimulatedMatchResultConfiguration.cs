using EkstraSim.Backend.Database.Entities;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

namespace EkstraSim.Backend.Database.Configurations;

public class SimulatedMatchResultConfiguration : IEntityTypeConfiguration<SimulatedMatchResult>
{
	public void Configure(EntityTypeBuilder<SimulatedMatchResult> builder)
	{
		builder.HasKey(smr => smr.Id);

		builder.Property(smr => smr.PredictedHomeScore)
			   .IsRequired();

		builder.Property(smr => smr.PredictedAwayScore)
			   .IsRequired();

		builder.Property(smr => smr.NumberOfSimulations)
			   .IsRequired();

		builder.Property(smr => smr.HomeWinProbability)
			   .IsRequired()
			   .HasColumnType("decimal(5,4)");

		builder.Property(smr => smr.DrawProbability)
			   .IsRequired()
			   .HasColumnType("decimal(5,4)");

		builder.Property(smr => smr.AwayWinProbability)
			   .IsRequired()
			   .HasColumnType("decimal(5,4)");

		builder.HasOne(smr => smr.Match)
			   .WithMany()
			   .HasForeignKey(smr => smr.MatchId)
			   .OnDelete(DeleteBehavior.Cascade);

		builder.HasOne(smr => smr.Season)
			   .WithMany()
			   .HasForeignKey(smr => smr.SeasonId)
			   .OnDelete(DeleteBehavior.Restrict);

		builder.HasOne(smr => smr.League)
			   .WithMany()
			   .HasForeignKey(smr => smr.LeagueId)
			   .OnDelete(DeleteBehavior.Restrict);
	}
}
