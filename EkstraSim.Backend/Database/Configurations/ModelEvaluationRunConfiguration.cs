using EkstraSim.Backend.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EkstraSim.Backend.Database.Configurations;

public class ModelEvaluationRunConfiguration : IEntityTypeConfiguration<ModelEvaluationRun>
{
    public void Configure(EntityTypeBuilder<ModelEvaluationRun> builder)
    {
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Models)
               .HasMaxLength(200)
               .IsRequired();

        builder.Property(r => r.Status)
               .HasConversion<int>();

        builder.HasOne(r => r.League)
               .WithMany()
               .HasForeignKey(r => r.LeagueId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.Season)
               .WithMany()
               .HasForeignKey(r => r.SeasonId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(r => r.Predictions)
               .WithOne(p => p.ModelEvaluationRun)
               .HasForeignKey(p => p.ModelEvaluationRunId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(r => r.RoundMetrics)
               .WithOne(m => m.ModelEvaluationRun)
               .HasForeignKey(m => m.ModelEvaluationRunId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(r => new { r.LeagueId, r.SeasonId });
    }
}
