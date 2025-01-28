using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EkstraSim.Backend.Migrations
{
    /// <inheritdoc />
    public partial class simulated_round : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SimulatedRoundId",
                table: "SimulatedMatchResults",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SimulatedRounds",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LeagueId = table.Column<int>(type: "int", nullable: false),
                    SeasonId = table.Column<int>(type: "int", nullable: false),
                    Round = table.Column<int>(type: "int", nullable: false),
                    NumberOfSimulations = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SimulatedRounds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SimulatedRounds_Leagues_LeagueId",
                        column: x => x.LeagueId,
                        principalTable: "Leagues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SimulatedRounds_Seasons_SeasonId",
                        column: x => x.SeasonId,
                        principalTable: "Seasons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SimulatedMatchResults_SimulatedRoundId",
                table: "SimulatedMatchResults",
                column: "SimulatedRoundId");

            migrationBuilder.CreateIndex(
                name: "IX_SimulatedRounds_LeagueId",
                table: "SimulatedRounds",
                column: "LeagueId");

            migrationBuilder.CreateIndex(
                name: "IX_SimulatedRounds_SeasonId",
                table: "SimulatedRounds",
                column: "SeasonId");

            migrationBuilder.AddForeignKey(
                name: "FK_SimulatedMatchResults_SimulatedRounds_SimulatedRoundId",
                table: "SimulatedMatchResults",
                column: "SimulatedRoundId",
                principalTable: "SimulatedRounds",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SimulatedMatchResults_SimulatedRounds_SimulatedRoundId",
                table: "SimulatedMatchResults");

            migrationBuilder.DropTable(
                name: "SimulatedRounds");

            migrationBuilder.DropIndex(
                name: "IX_SimulatedMatchResults_SimulatedRoundId",
                table: "SimulatedMatchResults");

            migrationBuilder.DropColumn(
                name: "SimulatedRoundId",
                table: "SimulatedMatchResults");
        }
    }
}
