using EkstraSim.Backend.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EkstraSim.Backend.Database.Configurations;

public class MatchConfiguration : IEntityTypeConfiguration<Match>
{
	public void Configure(EntityTypeBuilder<Match> builder)
	{
		builder.HasKey(m => m.Id);

		builder.Property(m => m.Date)
			   .IsRequired(true);

		builder.Property(m => m.Round)
			   .IsRequired(false);

		builder.Property(m => m.HomeTeamScore)
			   .IsRequired(false);

		builder.Property(m => m.AwayTeamScore)
			   .IsRequired(false);

		builder.HasOne(m => m.Season)
			   .WithMany(s => s.Matches)
			   .HasForeignKey(m => m.SeasonId)
			   .OnDelete(DeleteBehavior.Restrict);

		builder.HasOne(m => m.League)
			   .WithMany(l => l.Matches)
			   .HasForeignKey(m => m.LeagueId)
			   .OnDelete(DeleteBehavior.Restrict);

		builder.HasOne(m => m.HomeTeam)
			   .WithMany(ht => ht.HomeMatches)
			   .HasForeignKey(m => m.HomeTeamId)
			   .OnDelete(DeleteBehavior.Restrict);

		builder.HasOne(m => m.AwayTeam)
			   .WithMany(at => at.AwayMatches)
			   .HasForeignKey(m => m.AwayTeamId)
			   .OnDelete(DeleteBehavior.Restrict);

		builder.HasIndex(m => new { m.HomeTeamId, m.AwayTeamId, m.Round, m.SeasonId }).IsUnique();
	}
}