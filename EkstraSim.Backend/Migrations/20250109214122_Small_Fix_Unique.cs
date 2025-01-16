using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EkstraSim.Backend.Migrations
{
    /// <inheritdoc />
    public partial class Small_Fix_Unique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Matches_HomeTeamId_AwayTeamId_Date",
                table: "Matches");

            migrationBuilder.CreateIndex(
                name: "IX_Matches_HomeTeamId_AwayTeamId_Round_SeasonId",
                table: "Matches",
                columns: new[] { "HomeTeamId", "AwayTeamId", "Round", "SeasonId" },
                unique: true,
                filter: "[Round] IS NOT NULL AND [SeasonId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Matches_HomeTeamId_AwayTeamId_Round_SeasonId",
                table: "Matches");

            migrationBuilder.CreateIndex(
                name: "IX_Matches_HomeTeamId_AwayTeamId_Date",
                table: "Matches",
                columns: new[] { "HomeTeamId", "AwayTeamId", "Date" },
                unique: true);
        }
    }
}
