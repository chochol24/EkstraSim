using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EkstraSim.Backend.Migrations
{
    /// <inheritdoc />
    public partial class research_run_algorithm_version : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AlgorithmVersion",
                table: "ModelEvaluationRuns",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EffectiveOptionsJson",
                table: "ModelEvaluationRuns",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AlgorithmVersion",
                table: "ModelEvaluationRuns");

            migrationBuilder.DropColumn(
                name: "EffectiveOptionsJson",
                table: "ModelEvaluationRuns");
        }
    }
}
