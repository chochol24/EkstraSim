using EkstraSim.Backend.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EkstraSim.Backend.Database.Configurations;

public class TeamConfiguration : IEntityTypeConfiguration<Team>
{
	public void Configure(EntityTypeBuilder<Team> builder)
	{
		builder.HasKey(t => t.Id);

		builder.Property(t => t.Name)
			   .HasMaxLength(100)
			   .IsRequired(true);

		builder.Property(t => t.ELO)
			   .IsRequired(true);

		builder.Property(t => t.AverageHomeGoalsScored)
			   .IsRequired(false);
		builder.Property(t => t.AverageHomeGoalsConceded)
			   .IsRequired(false);
		builder.Property(t => t.AverageAwayGoalsScored)
			   .IsRequired(false);
		builder.Property(t => t.AverageAwayGoalsConceded)
			   .IsRequired(false);

		builder.HasIndex(t => t.Name).IsUnique();
	}
}
