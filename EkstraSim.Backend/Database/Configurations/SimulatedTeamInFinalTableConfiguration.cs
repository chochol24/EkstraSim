using EkstraSim.Backend.Database.Entities;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

namespace EkstraSim.Backend.Database.Configurations;

public class SimulatedTeamInFinalTableConfiguration : IEntityTypeConfiguration<SimulatedTeamInFinalTable>
{
	public void Configure(EntityTypeBuilder<SimulatedTeamInFinalTable> builder)
	{
		builder.HasKey(sflt => sflt.Id);

		builder.HasOne(sflt => sflt.Team)
			   .WithMany()
			   .HasForeignKey(sflt => sflt.TeamId)
			   .OnDelete(DeleteBehavior.Restrict);

		builder.HasOne(sfl => sfl.SimulatedFinalLeague)
			   .WithMany(t => t.Teams)
			   .HasForeignKey(sfl => sfl.SimulatedFinalLeagueId)
			   .OnDelete(DeleteBehavior.Restrict);

		builder.Property(sflt => sflt.AveragePoints)
			   .IsRequired();
		builder.Property(sflt => sflt.AverageGoalDifference)
			   .IsRequired();
		builder.Property(sflt => sflt.AverageGoalsScored)
			   .IsRequired();
		builder.Property(sflt => sflt.AverageGoalsConceded)
			   .IsRequired();

		builder.Property(sflt => sflt.FirstPlaceProbability)
			   .IsRequired();
		builder.Property(sflt => sflt.SecondPlaceProbability)
			   .IsRequired();
		builder.Property(sflt => sflt.ThirdPlaceProbability)
			   .IsRequired();
		builder.Property(sflt => sflt.FourthPlaceProbability)
			   .IsRequired();
		builder.Property(sflt => sflt.FifthPlaceProbability)
			   .IsRequired();
		builder.Property(sflt => sflt.SixthPlaceProbability)
			   .IsRequired();
		builder.Property(sflt => sflt.SeventhPlaceProbability)
			   .IsRequired();
		builder.Property(sflt => sflt.EighthPlaceProbability)
			   .IsRequired();
		builder.Property(sflt => sflt.NinthPlaceProbability)
			   .IsRequired();
		builder.Property(sflt => sflt.TenthPlaceProbability)
			   .IsRequired();
		builder.Property(sflt => sflt.EleventhPlaceProbability)
			   .IsRequired();
		builder.Property(sflt => sflt.TwelfthPlaceProbability)
			   .IsRequired();
		builder.Property(sflt => sflt.ThirteenthPlaceProbability)
			   .IsRequired();
		builder.Property(sflt => sflt.FourteenthPlaceProbability)
			   .IsRequired();
		builder.Property(sflt => sflt.FifteenthPlaceProbability)
			   .IsRequired();
		builder.Property(sflt => sflt.SixteenthPlaceProbability)
			   .IsRequired();
		builder.Property(sflt => sflt.SeventeenthPlaceProbability)
			   .IsRequired();
		builder.Property(sflt => sflt.EighteenthPlaceProbability)
			   .IsRequired();


		builder.Property(sflt => sflt.TopFourProbability)
			   .IsRequired();
		builder.Property(sflt => sflt.RelegationProbability)
			   .IsRequired();

	}
}
