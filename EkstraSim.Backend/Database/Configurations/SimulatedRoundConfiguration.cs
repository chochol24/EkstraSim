using EkstraSim.Backend.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EkstraSim.Backend.Database.Configurations;

public class SimulatedRoundConfiguration : IEntityTypeConfiguration<SimulatedRound>
{
    public void Configure(EntityTypeBuilder<SimulatedRound> builder)
    {
        builder.HasKey(r => r.Id);

        builder.HasMany(r => r.SimulatedMatchResults)
               .WithOne(m => m.SimulatedRound)
               .HasForeignKey(m => m.SimulatedRoundId);

    }
}
