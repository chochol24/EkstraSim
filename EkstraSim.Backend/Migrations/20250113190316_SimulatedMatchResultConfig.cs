using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EkstraSim.Backend.Migrations
{
    /// <inheritdoc />
    public partial class SimulatedMatchResultConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "AverageAwayGoalsConceded",
                table: "Teams",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "AverageAwayGoalsScored",
                table: "Teams",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "AverageHomeGoalsConceded",
                table: "Teams",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "AverageHomeGoalsScored",
                table: "Teams",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "AverageAwayGoalsConceded",
                table: "Leagues",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "AverageAwayGoalsScored",
                table: "Leagues",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "AverageHomeGoalsConceded",
                table: "Leagues",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "AverageHomeGoalsScored",
                table: "Leagues",
                type: "float",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SimulatedMatchResults",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MatchId = table.Column<int>(type: "int", nullable: false),
                    Round = table.Column<int>(type: "int", nullable: true),
                    SeasonId = table.Column<int>(type: "int", nullable: true),
                    LeagueId = table.Column<int>(type: "int", nullable: true),
                    PredictedHomeScore = table.Column<int>(type: "int", nullable: false),
                    PredictedAwayScore = table.Column<int>(type: "int", nullable: false),
                    HomeWinProbability = table.Column<decimal>(type: "decimal(5,4)", nullable: false),
                    DrawProbability = table.Column<decimal>(type: "decimal(5,4)", nullable: false),
                    AwayWinProbability = table.Column<decimal>(type: "decimal(5,4)", nullable: false),
                    NumberOfSimulations = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SimulatedMatchResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SimulatedMatchResults_Leagues_LeagueId",
                        column: x => x.LeagueId,
                        principalTable: "Leagues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SimulatedMatchResults_Matches_MatchId",
                        column: x => x.MatchId,
                        principalTable: "Matches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SimulatedMatchResults_Seasons_SeasonId",
                        column: x => x.SeasonId,
                        principalTable: "Seasons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SimulatedMatchResults_LeagueId",
                table: "SimulatedMatchResults",
                column: "LeagueId");

            migrationBuilder.CreateIndex(
                name: "IX_SimulatedMatchResults_MatchId",
                table: "SimulatedMatchResults",
                column: "MatchId");

            migrationBuilder.CreateIndex(
                name: "IX_SimulatedMatchResults_SeasonId",
                table: "SimulatedMatchResults",
                column: "SeasonId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SimulatedMatchResults");

            migrationBuilder.DropColumn(
                name: "AverageAwayGoalsConceded",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "AverageAwayGoalsScored",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "AverageHomeGoalsConceded",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "AverageHomeGoalsScored",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "AverageAwayGoalsConceded",
                table: "Leagues");

            migrationBuilder.DropColumn(
                name: "AverageAwayGoalsScored",
                table: "Leagues");

            migrationBuilder.DropColumn(
                name: "AverageHomeGoalsConceded",
                table: "Leagues");

            migrationBuilder.DropColumn(
                name: "AverageHomeGoalsScored",
                table: "Leagues");
        }
    }
}
