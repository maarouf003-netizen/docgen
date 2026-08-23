using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DocGenerator.Infrastructure.Persistence.MigrationsPostgres
{
    /// <inheritdoc />
    public partial class AddDocumentAppealsPg : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AppealId",
                table: "HeadAlerts",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DocumentAppeals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DocumentId = table.Column<int>(type: "integer", nullable: false),
                    Direction = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AppealTypeLabel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    AppellantsJson = table.Column<string>(type: "text", nullable: false),
                    AppelleesJson = table.Column<string>(type: "text", nullable: false),
                    AppealedDecisionText = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    AppealedDecisionSummary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    AppealedDecisionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    InspectionBookNumber = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    InspectionBookDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    GroundsSummary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    NoticeNumber = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    NoticeDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AppellateCourt = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    AppealBaseNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    AppealYear = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    DepositBookNumber = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DepositBookDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DefenseOpinion = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    RegistrationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DecisionNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DecisionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DecisionRuling = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Outcome = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    StruckOffDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    StruckOffDecisionNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    AssignedLawyerId = table.Column<int>(type: "integer", nullable: true),
                    AssignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedById = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentAppeals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentAppeals_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DocumentAppeals_Users_AssignedLawyerId",
                        column: x => x.AssignedLawyerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DocumentAppeals_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppealActions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AppealId = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    ActionDate = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ReminderDuration = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ReminderColor = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CreatedById = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppealActions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppealActions_DocumentAppeals_AppealId",
                        column: x => x.AppealId,
                        principalTable: "DocumentAppeals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AppealActions_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppealBaseNumbers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AppealId = table.Column<int>(type: "integer", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    BaseNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppealBaseNumbers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppealBaseNumbers_DocumentAppeals_AppealId",
                        column: x => x.AppealId,
                        principalTable: "DocumentAppeals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AppealBaseNumbers_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HeadAlerts_AppealId",
                table: "HeadAlerts",
                column: "AppealId");

            migrationBuilder.CreateIndex(
                name: "IX_AppealActions_AppealId",
                table: "AppealActions",
                column: "AppealId");

            migrationBuilder.CreateIndex(
                name: "IX_AppealActions_CreatedAt",
                table: "AppealActions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AppealActions_CreatedById",
                table: "AppealActions",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_AppealBaseNumbers_AppealId",
                table: "AppealBaseNumbers",
                column: "AppealId");

            migrationBuilder.CreateIndex(
                name: "IX_AppealBaseNumbers_AppealId_Year",
                table: "AppealBaseNumbers",
                columns: new[] { "AppealId", "Year" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppealBaseNumbers_CreatedById",
                table: "AppealBaseNumbers",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentAppeals_AssignedLawyerId",
                table: "DocumentAppeals",
                column: "AssignedLawyerId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentAppeals_CreatedAt",
                table: "DocumentAppeals",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentAppeals_CreatedById",
                table: "DocumentAppeals",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentAppeals_Direction",
                table: "DocumentAppeals",
                column: "Direction");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentAppeals_DocumentId",
                table: "DocumentAppeals",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentAppeals_Status",
                table: "DocumentAppeals",
                column: "Status");

            migrationBuilder.AddForeignKey(
                name: "FK_HeadAlerts_DocumentAppeals_AppealId",
                table: "HeadAlerts",
                column: "AppealId",
                principalTable: "DocumentAppeals",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_HeadAlerts_DocumentAppeals_AppealId",
                table: "HeadAlerts");

            migrationBuilder.DropTable(
                name: "AppealActions");

            migrationBuilder.DropTable(
                name: "AppealBaseNumbers");

            migrationBuilder.DropTable(
                name: "DocumentAppeals");

            migrationBuilder.DropIndex(
                name: "IX_HeadAlerts_AppealId",
                table: "HeadAlerts");

            migrationBuilder.DropColumn(
                name: "AppealId",
                table: "HeadAlerts");
        }
    }
}
