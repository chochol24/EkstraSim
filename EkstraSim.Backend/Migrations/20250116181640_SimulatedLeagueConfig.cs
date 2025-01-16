using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EkstraSim.Backend.Migrations
{
    /// <inheritdoc />
    public partial class SimulatedLeagueConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SimulatedFinalLeagues",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SeasonId = table.Column<int>(type: "int", nullable: false),
                    LeagueId = table.Column<int>(type: "int", nullable: false),
                    RoundBeforeSimulation = table.Column<int>(type: "int", nullable: false),
                    NumberOfSimulations = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SimulatedFinalLeagues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SimulatedFinalLeagues_Leagues_LeagueId",
                        column: x => x.LeagueId,
                        principalTable: "Leagues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SimulatedFinalLeagues_Seasons_SeasonId",
                        column: x => x.SeasonId,
                        principalTable: "Seasons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SimulatedTeamInFinalTables",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TeamId = table.Column<int>(type: "int", nullable: false),
                    SimulatedFinalLeagueId = table.Column<int>(type: "int", nullable: false),
                    AveragePoints = table.Column<double>(type: "float", nullable: false),
                    AverageGoalDifference = table.Column<double>(type: "float", nullable: false),
                    AverageGoalsScored = table.Column<double>(type: "float", nullable: false),
                    AverageGoalsConceded = table.Column<double>(type: "float", nullable: false),
                    FirstPlaceProbability = table.Column<double>(type: "float", nullable: false),
                    SecondPlaceProbability = table.Column<double>(type: "float", nullable: false),
                    ThirdPlaceProbability = table.Column<double>(type: "float", nullable: false),
                    FourthPlaceProbability = table.Column<double>(type: "float", nullable: false),
                    FifthPlaceProbability = table.Column<double>(type: "float", nullable: false),
                    SixthPlaceProbability = table.Column<double>(type: "float", nullable: false),
                    SeventhPlaceProbability = table.Column<double>(type: "float", nullable: false),
                    EighthPlaceProbability = table.Column<double>(type: "float", nullable: false),
                    NinthPlaceProbability = table.Column<double>(type: "float", nullable: false),
                    TenthPlaceProbability = table.Column<double>(type: "float", nullable: false),
                    EleventhPlaceProbability = table.Column<double>(type: "float", nullable: false),
                    TwelfthPlaceProbability = table.Column<double>(type: "float", nullable: false),
                    ThirteenthPlaceProbability = table.Column<double>(type: "float", nullable: false),
                    FourteenthPlaceProbability = table.Column<double>(type: "float", nullable: false),
                    FifteenthPlaceProbability = table.Column<double>(type: "float", nullable: false),
                    SixteenthPlaceProbability = table.Column<double>(type: "float", nullable: false),
                    SeventeenthPlaceProbability = table.Column<double>(type: "float", nullable: false),
                    EighteenthPlaceProbability = table.Column<double>(type: "float", nullable: false),
                    TopFourProbability = table.Column<double>(type: "float", nullable: false),
                    RelegationProbability = table.Column<double>(type: "float", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SimulatedTeamInFinalTables", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SimulatedTeamInFinalTables_SimulatedFinalLeagues_SimulatedFinalLeagueId",
                        column: x => x.SimulatedFinalLeagueId,
                        principalTable: "SimulatedFinalLeagues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SimulatedTeamInFinalTables_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SimulatedFinalLeagues_LeagueId",
                table: "SimulatedFinalLeagues",
                column: "LeagueId");

            migrationBuilder.CreateIndex(
                name: "IX_SimulatedFinalLeagues_SeasonId",
                table: "SimulatedFinalLeagues",
                column: "SeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_SimulatedTeamInFinalTables_SimulatedFinalLeagueId",
                table: "SimulatedTeamInFinalTables",
                column: "SimulatedFinalLeagueId");

            migrationBuilder.CreateIndex(
                name: "IX_SimulatedTeamInFinalTables_TeamId",
                table: "SimulatedTeamInFinalTables",
                column: "TeamId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SimulatedTeamInFinalTables");

            migrationBuilder.DropTable(
                name: "SimulatedFinalLeagues");
        }
    }
}
