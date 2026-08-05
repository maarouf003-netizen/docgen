using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocGenerator.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UserName = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    ActionType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    DocumentId = table.Column<int>(type: "INTEGER", nullable: true),
                    DocumentType = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Details = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Branches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Address = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    Phone = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Branches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Username = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Email = table.Column<string>(type: "TEXT", maxLength: 150, nullable: true),
                    PasswordHash = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    FullName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Role = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    BranchId = table.Column<int>(type: "INTEGER", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastLogin = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Users_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Documents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedById = table.Column<int>(type: "INTEGER", nullable: true),
                    BranchId = table.Column<int>(type: "INTEGER", nullable: true),
                    DocumentType = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    IsDraft = table.Column<bool>(type: "INTEGER", nullable: false),
                    BorrowerName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    BorrowerFather = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    BorrowerFamily = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    BorrowerMother = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    BorrowerBirth = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    BorrowerRegister = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    BorrowerNationalId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    BorrowerAddress = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    BorrowerAddressType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    ContractType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    ContractTypeSelector = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true),
                    ContractNumber = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    ContractDate = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    InclusionText = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    AmountNumeric = table.Column<decimal>(type: "decimal(20,2)", nullable: false),
                    AmountWords = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    Currency = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Amount2Numeric = table.Column<decimal>(type: "decimal(20,2)", nullable: false),
                    Amount2Words = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    Currency2 = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    InclusionAmountNumeric = table.Column<decimal>(type: "decimal(20,2)", nullable: false),
                    InclusionAmountWords = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    InclusionCurrency = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Court = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Applicant = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Lawyer = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    FileNumber = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    FileType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    FileYear = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    FileIncoming = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    FileIncomingDate = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    BranchName = table.Column<string>(type: "TEXT", maxLength: 150, nullable: true),
                    ExecStatus = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true),
                    BaraetNumber = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    BaraetDate = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    BaraetRegNumber = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    BaraetRegDate = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    TarithNumber = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    TarithDate = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    TarithRegNumber = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    TarithRegDate = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    SeizureDate = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    ImmediateActions = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    FullData = table.Column<string>(type: "text", nullable: true),
                    SearchText = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    FilePath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    ViewCount = table.Column<int>(type: "INTEGER", nullable: false),
                    PrintCount = table.Column<int>(type: "INTEGER", nullable: false),
                    DownloadCount = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Documents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Documents_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Documents_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Guarantors",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DocumentId = table.Column<int>(type: "INTEGER", nullable: false),
                    GuarantorNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    GuarantorName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    GuarantorFather = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    GuarantorFamily = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    GuarantorMother = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    GuarantorBirth = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    GuarantorRegister = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    GuarantorNationalId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    GuarantorAddress = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    AddressType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Guarantors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Guarantors_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RealEstates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DocumentId = table.Column<int>(type: "INTEGER", nullable: false),
                    Owner = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Property = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    PropertyNumber = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    PropertyDistrict = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    LandRegistry = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    ShareType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RealEstates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RealEstates_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Timestamp",
                table: "AuditLogs",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_Branches_Code",
                table: "Branches",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Documents_BranchId",
                table: "Documents",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_CreatedAt",
                table: "Documents",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_CreatedById",
                table: "Documents",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_DocumentType",
                table: "Documents",
                column: "DocumentType");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_SearchText",
                table: "Documents",
                column: "SearchText");

            migrationBuilder.CreateIndex(
                name: "IX_Guarantors_DocumentId",
                table: "Guarantors",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_RealEstates_DocumentId",
                table: "RealEstates",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_BranchId",
                table: "Users",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "Guarantors");

            migrationBuilder.DropTable(
                name: "RealEstates");

            migrationBuilder.DropTable(
                name: "Documents");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Branches");
        }
    }
}
