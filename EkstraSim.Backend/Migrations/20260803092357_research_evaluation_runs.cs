using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EkstraSim.Backend.Migrations
{
    /// <inheritdoc />
    public partial class research_evaluation_runs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ModelEvaluationRuns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LeagueId = table.Column<int>(type: "int", nullable: false),
                    SeasonId = table.Column<int>(type: "int", nullable: false),
                    TrainingLastRound = table.Column<int>(type: "int", nullable: false),
                    Models = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    OptionsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PromotedTeamsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Comments = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FinishedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EvaluatedMatchCount = table.Column<int>(type: "int", nullable: false),
                    EvaluatedRoundCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelEvaluationRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModelEvaluationRuns_Leagues_LeagueId",
                        column: x => x.LeagueId,
                        principalTable: "Leagues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ModelEvaluationRuns_Seasons_SeasonId",
                        column: x => x.SeasonId,
                        principalTable: "Seasons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ModelPredictions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ModelEvaluationRunId = table.Column<int>(type: "int", nullable: false),
                    ModelName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    MatchId = table.Column<int>(type: "int", nullable: false),
                    Round = table.Column<int>(type: "int", nullable: true),
                    InvolvesPromotedTeam = table.Column<bool>(type: "bit", nullable: false),
                    ExpectedHomeGoals = table.Column<double>(type: "float", nullable: false),
                    ExpectedAwayGoals = table.Column<double>(type: "float", nullable: false),
                    HomeWinProbability = table.Column<double>(type: "float", nullable: false),
                    DrawProbability = table.Column<double>(type: "float", nullable: false),
                    AwayWinProbability = table.Column<double>(type: "float", nullable: false),
                    PredictedHomeScore = table.Column<int>(type: "int", nullable: false),
                    PredictedAwayScore = table.Column<int>(type: "int", nullable: false),
                    ResultProbabilityMatrixJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ActualHomeScore = table.Column<int>(type: "int", nullable: false),
                    ActualAwayScore = table.Column<int>(type: "int", nullable: false),
                    Brier = table.Column<double>(type: "float", nullable: false),
                    RankedProbabilityScore = table.Column<double>(type: "float", nullable: false),
                    LogLoss = table.Column<double>(type: "float", nullable: false),
                    OutcomeCorrect = table.Column<bool>(type: "bit", nullable: false),
                    ExactScoreCorrect = table.Column<bool>(type: "bit", nullable: false),
                    ExactScoreInTopThree = table.Column<bool>(type: "bit", nullable: false),
                    ProbabilityOfActualScore = table.Column<double>(type: "float", nullable: false),
                    ProbabilityOfActualOutcome = table.Column<double>(type: "float", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelPredictions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModelPredictions_Matches_MatchId",
                        column: x => x.MatchId,
                        principalTable: "Matches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ModelPredictions_ModelEvaluationRuns_ModelEvaluationRunId",
                        column: x => x.ModelEvaluationRunId,
                        principalTable: "ModelEvaluationRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ModelRoundMetrics",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ModelEvaluationRunId = table.Column<int>(type: "int", nullable: false),
                    ModelName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Round = table.Column<int>(type: "int", nullable: false),
                    MatchCount = table.Column<int>(type: "int", nullable: false),
                    Brier = table.Column<double>(type: "float", nullable: false),
                    RankedProbabilityScore = table.Column<double>(type: "float", nullable: false),
                    LogLoss = table.Column<double>(type: "float", nullable: false),
                    OutcomeAccuracy = table.Column<double>(type: "float", nullable: false),
                    ExactScoreAccuracy = table.Column<double>(type: "float", nullable: false),
                    ExactScoreTopThreeAccuracy = table.Column<double>(type: "float", nullable: false),
                    MeanProbabilityOfActualScore = table.Column<double>(type: "float", nullable: false),
                    ParameterDrift = table.Column<double>(type: "float", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelRoundMetrics", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModelRoundMetrics_ModelEvaluationRuns_ModelEvaluationRunId",
                        column: x => x.ModelEvaluationRunId,
                        principalTable: "ModelEvaluationRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ModelEvaluationRuns_LeagueId_SeasonId",
                table: "ModelEvaluationRuns",
                columns: new[] { "LeagueId", "SeasonId" });

            migrationBuilder.CreateIndex(
                name: "IX_ModelEvaluationRuns_SeasonId",
                table: "ModelEvaluationRuns",
                column: "SeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_ModelPredictions_MatchId",
                table: "ModelPredictions",
                column: "MatchId");

            migrationBuilder.CreateIndex(
                name: "IX_ModelPredictions_ModelEvaluationRunId_MatchId",
                table: "ModelPredictions",
                columns: new[] { "ModelEvaluationRunId", "MatchId" });

            migrationBuilder.CreateIndex(
                name: "IX_ModelPredictions_ModelEvaluationRunId_ModelName_Round",
                table: "ModelPredictions",
                columns: new[] { "ModelEvaluationRunId", "ModelName", "Round" });

            migrationBuilder.CreateIndex(
                name: "IX_ModelRoundMetrics_ModelEvaluationRunId_ModelName_Round",
                table: "ModelRoundMetrics",
                columns: new[] { "ModelEvaluationRunId", "ModelName", "Round" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ModelPredictions");

            migrationBuilder.DropTable(
                name: "ModelRoundMetrics");

            migrationBuilder.DropTable(
                name: "ModelEvaluationRuns");
        }
    }
}
