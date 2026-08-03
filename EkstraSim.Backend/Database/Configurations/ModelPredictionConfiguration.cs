using EkstraSim.Backend.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EkstraSim.Backend.Database.Configurations;

public class ModelPredictionConfiguration : IEntityTypeConfiguration<ModelPrediction>
{
    public void Configure(EntityTypeBuilder<ModelPrediction> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.ModelName)
               .HasMaxLength(50)
               .IsRequired();

        builder.HasOne(p => p.Match)
               .WithMany()
               .HasForeignKey(p => p.MatchId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => new { p.ModelEvaluationRunId, p.ModelName, p.Round });
        builder.HasIndex(p => new { p.ModelEvaluationRunId, p.MatchId });
    }
}
