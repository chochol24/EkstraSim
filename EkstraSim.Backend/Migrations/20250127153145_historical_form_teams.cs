using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EkstraSim.Backend.Migrations
{
    /// <inheritdoc />
    public partial class historical_form_teams : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "AverageAwayGoalsConcededCurrentSeason",
                table: "Teams",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "AverageAwayGoalsConcededHistorical",
                table: "Teams",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "AverageAwayGoalsConcededPreviousSeason",
                table: "Teams",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "AverageAwayGoalsScoredCurrentSeason",
                table: "Teams",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "AverageAwayGoalsScoredHistorical",
                table: "Teams",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "AverageAwayGoalsScoredPreviousSeason",
                table: "Teams",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "AverageHomeGoalsConcededCurrentSeason",
                table: "Teams",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "AverageHomeGoalsConcededHistorical",
                table: "Teams",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "AverageHomeGoalsConcededPreviousSeason",
                table: "Teams",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "AverageHomeGoalsScoredCurrentSeason",
                table: "Teams",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "AverageHomeGoalsScoredHistorical",
                table: "Teams",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "AverageHomeGoalsScoredPreviousSeason",
                table: "Teams",
                type: "float",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AverageAwayGoalsConcededCurrentSeason",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "AverageAwayGoalsConcededHistorical",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "AverageAwayGoalsConcededPreviousSeason",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "AverageAwayGoalsScoredCurrentSeason",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "AverageAwayGoalsScoredHistorical",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "AverageAwayGoalsScoredPreviousSeason",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "AverageHomeGoalsConcededCurrentSeason",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "AverageHomeGoalsConcededHistorical",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "AverageHomeGoalsConcededPreviousSeason",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "AverageHomeGoalsScoredCurrentSeason",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "AverageHomeGoalsScoredHistorical",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "AverageHomeGoalsScoredPreviousSeason",
                table: "Teams");
        }
    }
}
