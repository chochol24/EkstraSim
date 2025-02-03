using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EkstraSim.Backend.Migrations
{
    /// <inheritdoc />
    public partial class dates_and_json_matrix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "SimulationDate",
                table: "SimulatedRounds",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResultProbabilityMatrixJson",
                table: "SimulatedMatchResults",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SimulationDate",
                table: "SimulatedMatchResults",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SimulationDate",
                table: "SimulatedFinalLeagues",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SimulationDate",
                table: "SimulatedRounds");

            migrationBuilder.DropColumn(
                name: "ResultProbabilityMatrixJson",
                table: "SimulatedMatchResults");

            migrationBuilder.DropColumn(
                name: "SimulationDate",
                table: "SimulatedMatchResults");

            migrationBuilder.DropColumn(
                name: "SimulationDate",
                table: "SimulatedFinalLeagues");
        }
    }
}
