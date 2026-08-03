using EkstraSim.Backend.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EkstraSim.Backend.Database.Configurations;

public class ModelRoundMetricConfiguration : IEntityTypeConfiguration<ModelRoundMetric>
{
    public void Configure(EntityTypeBuilder<ModelRoundMetric> builder)
    {
        builder.HasKey(m => m.Id);

        builder.Property(m => m.ModelName)
               .HasMaxLength(50)
               .IsRequired();

        builder.HasIndex(m => new { m.ModelEvaluationRunId, m.ModelName, m.Round })
               .IsUnique();
    }
}
