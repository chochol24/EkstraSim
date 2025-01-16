using EkstraSim.Backend.Database.Entities;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

namespace EkstraSim.Backend.Database.Configurations;

public class LeagueConfiguration : IEntityTypeConfiguration<League>
{
	public void Configure(EntityTypeBuilder<League> builder)
	{
		builder.HasKey(l => l.Id);

		builder.Property(l => l.Name)
			   .HasMaxLength(100)
			   .IsRequired(true);

		builder.Property(t => t.AverageHomeGoalsScored)
			   .IsRequired(false);
		builder.Property(t => t.AverageHomeGoalsConceded)
			   .IsRequired(false);
		builder.Property(t => t.AverageAwayGoalsScored)
			   .IsRequired(false);
		builder.Property(t => t.AverageAwayGoalsConceded)
			   .IsRequired(false);

		builder.HasIndex(l => l.Name).IsUnique();
	}
}