using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DocGenerator.Infrastructure.Persistence.MigrationsPostgres
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
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UserName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ActionType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    DocumentId = table.Column<int>(type: "integer", nullable: true),
                    DocumentType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Details = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Branches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Branches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LoginAttempts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AttemptedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoginAttempts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Username = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Email = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    PasswordHash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    FullName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    BranchId = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    TokenVersion = table.Column<int>(type: "integer", nullable: false),
                    FailedLoginCount = table.Column<int>(type: "integer", nullable: false),
                    LockoutEndUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastLogin = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
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
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: false),
                    BranchId = table.Column<int>(type: "integer", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DocumentType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IsDraft = table.Column<bool>(type: "boolean", nullable: false),
                    BorrowerName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    BorrowerFather = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    BorrowerFamily = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    BorrowerMother = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    BorrowerBirth = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    BorrowerRegister = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    BorrowerNationalId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    BorrowerAddress = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    BorrowerAddressType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ContractType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ContractTypeSelector = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    ContractNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ContractDate = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    InclusionText = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    AmountNumeric = table.Column<decimal>(type: "numeric(20,2)", nullable: false),
                    AmountWords = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Currency = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Amount2Numeric = table.Column<decimal>(type: "numeric(20,2)", nullable: false),
                    Amount2Words = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Currency2 = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    InclusionAmountNumeric = table.Column<decimal>(type: "numeric(20,2)", nullable: false),
                    InclusionAmountWords = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    InclusionCurrency = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Court = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Applicant = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Lawyer = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    FileNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    FileType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    FileYear = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    FileIncoming = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    FileIncomingDate = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    UnderFilingNumber = table.Column<string>(type: "text", nullable: true),
                    BranchName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    ExecStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    ExecSubStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    CollectedAmount = table.Column<decimal>(type: "numeric(20,2)", nullable: true),
                    GeneralEntitySide = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ExecutedStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    ExecutedDescription = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    FileReceiptDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExecutedRequiredAmount = table.Column<decimal>(type: "numeric(20,2)", nullable: true),
                    ExecutedPaidAmount = table.Column<decimal>(type: "numeric(20,2)", nullable: true),
                    StruckOffDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    BaraetNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    BaraetDate = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    BaraetRegNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    BaraetRegDate = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    TarithNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TarithDate = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    TarithRegNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TarithRegDate = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    SeizureDate = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ImmediateActions = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    FullData = table.Column<string>(type: "text", nullable: true),
                    SearchText = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    FilePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ViewCount = table.Column<int>(type: "integer", nullable: false),
                    PrintCount = table.Column<int>(type: "integer", nullable: false)
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
                name: "DocumentBaseNumbers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DocumentId = table.Column<int>(type: "integer", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    BaseNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentBaseNumbers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentBaseNumbers_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DocumentBaseNumbers_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DocumentRegistrationDates",
                columns: table => new
                {
                    DocumentId = table.Column<int>(type: "integer", nullable: false),
                    Date = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentRegistrationDates", x => x.DocumentId);
                    table.ForeignKey(
                        name: "FK_DocumentRegistrationDates_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExecutedNaturalPersons",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DocumentId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Father = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Family = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    AddressType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    AddressOrRepresentative = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    RepresentationType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    DeceasedName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DeceasedFather = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DeceasedFamily = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExecutedNaturalPersons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExecutedNaturalPersons_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExecutedPublicEntities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DocumentId = table.Column<int>(type: "integer", nullable: false),
                    EntityName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    EntityBranch = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExecutedPublicEntities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExecutedPublicEntities_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExecutionActions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DocumentId = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_ExecutionActions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExecutionActions_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ExecutionActions_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExecutionApplicants",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DocumentId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Father = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Family = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LegalRepresentative = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    RepresentationType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    DeceasedName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DeceasedFather = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DeceasedFamily = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExecutionApplicants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExecutionApplicants_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Guarantors",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DocumentId = table.Column<int>(type: "integer", nullable: false),
                    GuarantorNumber = table.Column<int>(type: "integer", nullable: false),
                    GuarantorName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    GuarantorFather = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    GuarantorFamily = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    GuarantorMother = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    GuarantorBirth = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    GuarantorRegister = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    GuarantorNationalId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    GuarantorAddress = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    AddressType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
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
                name: "HeadAlerts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BranchId = table.Column<int>(type: "integer", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: false),
                    TargetType = table.Column<int>(type: "integer", nullable: false),
                    DocumentId = table.Column<int>(type: "integer", nullable: true),
                    TargetLawyerId = table.Column<int>(type: "integer", nullable: true),
                    Message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HeadAlerts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HeadAlerts_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HeadAlerts_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_HeadAlerts_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HeadAlerts_Users_TargetLawyerId",
                        column: x => x.TargetLawyerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Heirs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DocumentId = table.Column<int>(type: "integer", nullable: false),
                    GuarantorNumber = table.Column<int>(type: "integer", nullable: true),
                    HeirName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AddressType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    HeirAddress = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Heirs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Heirs_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RealEstates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DocumentId = table.Column<int>(type: "integer", nullable: false),
                    Property = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PropertyNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PropertyDistrict = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    LandRegistry = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ShareType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
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

            migrationBuilder.CreateTable(
                name: "ExecutedHeirs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DocumentId = table.Column<int>(type: "integer", nullable: false),
                    ExecutionApplicantId = table.Column<int>(type: "integer", nullable: true),
                    ExecutedNaturalPersonId = table.Column<int>(type: "integer", nullable: true),
                    HeirName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    HeirFather = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    HeirFamily = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AddressType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    HeirAddress = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExecutedHeirs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExecutedHeirs_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ExecutedHeirs_ExecutedNaturalPersons_ExecutedNaturalPersonId",
                        column: x => x.ExecutedNaturalPersonId,
                        principalTable: "ExecutedNaturalPersons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ExecutedHeirs_ExecutionApplicants_ExecutionApplicantId",
                        column: x => x.ExecutionApplicantId,
                        principalTable: "ExecutionApplicants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HeadAlertRecipients",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    HeadAlertId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    IsRead = table.Column<bool>(type: "boolean", nullable: false),
                    ReadAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HeadAlertRecipients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HeadAlertRecipients_HeadAlerts_HeadAlertId",
                        column: x => x.HeadAlertId,
                        principalTable: "HeadAlerts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HeadAlertRecipients_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RealEstateOwners",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RealEstateId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RealEstateOwners", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RealEstateOwners_RealEstates_RealEstateId",
                        column: x => x.RealEstateId,
                        principalTable: "RealEstates",
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
                name: "IX_DocumentBaseNumbers_CreatedById",
                table: "DocumentBaseNumbers",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentBaseNumbers_DocumentId",
                table: "DocumentBaseNumbers",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentBaseNumbers_DocumentId_Year",
                table: "DocumentBaseNumbers",
                columns: new[] { "DocumentId", "Year" },
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
                name: "IX_Documents_ExecutedStatus",
                table: "Documents",
                column: "ExecutedStatus");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_GeneralEntitySide",
                table: "Documents",
                column: "GeneralEntitySide");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_SearchText",
                table: "Documents",
                column: "SearchText");

            migrationBuilder.CreateIndex(
                name: "IX_ExecutedHeirs_DocumentId",
                table: "ExecutedHeirs",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_ExecutedHeirs_ExecutedNaturalPersonId",
                table: "ExecutedHeirs",
                column: "ExecutedNaturalPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_ExecutedHeirs_ExecutionApplicantId",
                table: "ExecutedHeirs",
                column: "ExecutionApplicantId");

            migrationBuilder.CreateIndex(
                name: "IX_ExecutedNaturalPersons_DocumentId",
                table: "ExecutedNaturalPersons",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_ExecutedPublicEntities_DocumentId",
                table: "ExecutedPublicEntities",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionActions_CreatedAt",
                table: "ExecutionActions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionActions_CreatedById",
                table: "ExecutionActions",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionActions_DocumentId",
                table: "ExecutionActions",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionApplicants_DocumentId",
                table: "ExecutionApplicants",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_Guarantors_DocumentId",
                table: "Guarantors",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_HeadAlertRecipients_HeadAlertId",
                table: "HeadAlertRecipients",
                column: "HeadAlertId");

            migrationBuilder.CreateIndex(
                name: "IX_HeadAlertRecipients_UserId",
                table: "HeadAlertRecipients",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_HeadAlerts_BranchId",
                table: "HeadAlerts",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_HeadAlerts_CreatedAt",
                table: "HeadAlerts",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_HeadAlerts_CreatedById",
                table: "HeadAlerts",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_HeadAlerts_DocumentId",
                table: "HeadAlerts",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_HeadAlerts_TargetLawyerId",
                table: "HeadAlerts",
                column: "TargetLawyerId");

            migrationBuilder.CreateIndex(
                name: "IX_Heirs_DocumentId",
                table: "Heirs",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_LoginAttempts_Key",
                table: "LoginAttempts",
                column: "Key");

            migrationBuilder.CreateIndex(
                name: "IX_RealEstateOwners_RealEstateId",
                table: "RealEstateOwners",
                column: "RealEstateId");

            migrationBuilder.CreateIndex(
                name: "IX_RealEstates_DocumentId",
                table: "RealEstates",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_BranchId",
                table: "Users",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username_BranchId",
                table: "Users",
                columns: new[] { "Username", "BranchId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "DocumentBaseNumbers");

            migrationBuilder.DropTable(
                name: "DocumentRegistrationDates");

            migrationBuilder.DropTable(
                name: "ExecutedHeirs");

            migrationBuilder.DropTable(
                name: "ExecutedPublicEntities");

            migrationBuilder.DropTable(
                name: "ExecutionActions");

            migrationBuilder.DropTable(
                name: "Guarantors");

            migrationBuilder.DropTable(
                name: "HeadAlertRecipients");

            migrationBuilder.DropTable(
                name: "Heirs");

            migrationBuilder.DropTable(
                name: "LoginAttempts");

            migrationBuilder.DropTable(
                name: "RealEstateOwners");

            migrationBuilder.DropTable(
                name: "ExecutedNaturalPersons");

            migrationBuilder.DropTable(
                name: "ExecutionApplicants");

            migrationBuilder.DropTable(
                name: "HeadAlerts");

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
