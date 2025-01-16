using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using EkstraSim.Backend.Database.Entities;

namespace EkstraSim.Backend.Database.Configurations;

public class SeasonConfiguration : IEntityTypeConfiguration<Season>
{
	public void Configure(EntityTypeBuilder<Season> builder)
	{
		builder.HasKey(s => s.Id);

		builder.Property(s => s.Name)
			   .HasMaxLength(50)
			   .IsRequired(true);

		builder.HasOne(s => s.League)
			   .WithMany(l => l.Seasons)
			   .HasForeignKey(s => s.LeagueId)
			   .OnDelete(DeleteBehavior.Restrict);

		builder.HasIndex(s => new { s.LeagueId, s.Name }).IsUnique();
	}
}
