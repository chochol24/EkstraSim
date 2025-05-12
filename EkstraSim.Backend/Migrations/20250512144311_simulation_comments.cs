using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EkstraSim.Backend.Migrations
{
    /// <inheritdoc />
    public partial class simulation_comments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Comments",
                table: "SimulatedTeamInFinalTables",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Comments",
                table: "SimulatedRounds",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Comments",
                table: "SimulatedMatchResults",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Comments",
                table: "SimulatedFinalLeagues",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Comments",
                table: "SimulatedTeamInFinalTables");

            migrationBuilder.DropColumn(
                name: "Comments",
                table: "SimulatedRounds");

            migrationBuilder.DropColumn(
                name: "Comments",
                table: "SimulatedMatchResults");

            migrationBuilder.DropColumn(
                name: "Comments",
                table: "SimulatedFinalLeagues");
        }
    }
}
