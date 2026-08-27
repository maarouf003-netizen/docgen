using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DocGenerator.Infrastructure.Persistence.MigrationsPostgres
{
    /// <inheritdoc />
    public partial class AddCoverageLabelPg : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CoverageLabel",
                table: "PublicEntities",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PublicEntityChangeEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EntryId = table.Column<int>(type: "integer", nullable: true),
                    GroupId = table.Column<int>(type: "integer", nullable: true),
                    ActionKind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    DecreeKind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    DecreeNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    DecreeDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PayloadJson = table.Column<string>(type: "text", nullable: false),
                    ActorUserId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PublicEntityChangeEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PublicEntityChangeEvents_PublicEntities_EntryId",
                        column: x => x.EntryId,
                        principalTable: "PublicEntities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PublicEntityChangeEvents_PublicEntityGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "PublicEntityGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PublicEntityChangeEvents_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PublicEntityChangeEvents_ActionKind",
                table: "PublicEntityChangeEvents",
                column: "ActionKind");

            migrationBuilder.CreateIndex(
                name: "IX_PublicEntityChangeEvents_ActorUserId",
                table: "PublicEntityChangeEvents",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PublicEntityChangeEvents_CreatedAtUtc",
                table: "PublicEntityChangeEvents",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_PublicEntityChangeEvents_EntryId",
                table: "PublicEntityChangeEvents",
                column: "EntryId");

            migrationBuilder.CreateIndex(
                name: "IX_PublicEntityChangeEvents_GroupId",
                table: "PublicEntityChangeEvents",
                column: "GroupId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PublicEntityChangeEvents");

            migrationBuilder.DropColumn(
                name: "CoverageLabel",
                table: "PublicEntities");
        }
    }
}
