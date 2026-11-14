using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DocGenerator.Infrastructure.Persistence.MigrationsPostgres
{
    /// <inheritdoc />
    public partial class InitialPostgresBaseline : Migration
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
                    Governorate = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Branches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HeadAlertOutboxMessages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Operation = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Kind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Payload = table.Column<string>(type: "text", nullable: false),
                    PayloadVersion = table.Column<int>(type: "integer", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    NextAttemptAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LockedUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LockedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    ConflictRetries = table.Column<int>(type: "integer", nullable: false),
                    LastError = table.Column<string>(type: "text", nullable: true),
                    TargetUserId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HeadAlertOutboxMessages", x => x.Id);
                    table.CheckConstraint("CK_HeadAlertOutbox_Attempts", "\"Attempts\" >= 0");
                    table.CheckConstraint("CK_HeadAlertOutbox_ConflictRetries", "\"ConflictRetries\" >= 0");
                    table.CheckConstraint("CK_HeadAlertOutbox_Kind_Operation", "(\"Operation\" = 'upsert' AND \"Kind\" IS NOT NULL) OR (\"Operation\" = 'circuit_renamed' AND \"Kind\" IS NULL)");
                    table.CheckConstraint("CK_HeadAlertOutbox_Operation", "\"Operation\" IN ('upsert','circuit_renamed')");
                    table.CheckConstraint("CK_HeadAlertOutbox_PayloadVersion", "\"PayloadVersion\" > 0");
                    table.CheckConstraint("CK_HeadAlertOutbox_Status", "\"Status\" IN ('pending','processing','processed','skipped','failed')");
                });

            migrationBuilder.CreateTable(
                name: "IdempotencyKeys",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Operation = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ResponseBody = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdempotencyKeys", x => x.Id);
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
                name: "PublicEntityGroups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CanonicalName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CanonicalNameNorm = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    EntityType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PublicEntityGroups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DocumentFieldChanges",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AuditLogId = table.Column<int>(type: "integer", nullable: false),
                    DocumentId = table.Column<int>(type: "integer", nullable: false),
                    FieldKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    FieldLabel = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    OldValue = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    NewValue = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentFieldChanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentFieldChanges_AuditLogs_AuditLogId",
                        column: x => x.AuditLogId,
                        principalTable: "AuditLogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Sections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BranchId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NameNorm = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sections_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
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
                    ReminderBaseDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedById = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppealActions", x => x.Id);
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
                });

            migrationBuilder.CreateTable(
                name: "ApplicantPublicEntities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DocumentId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Branch = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Governorate = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RegistryId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicantPublicEntities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppSuggestions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SenderId = table.Column<int>(type: "integer", nullable: false),
                    Message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsRead = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppSuggestions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AssetOwners",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AssetId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssetOwners", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Assets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DocumentId = table.Column<int>(type: "integer", nullable: false),
                    AssetKind = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ShareType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Property = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PropertyNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CaseNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PropertyDistrict = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    LandRegistry = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RightType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    VehicleType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    VehicleClass = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PlateNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    VehicleGovernorate = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RegisterNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RegistrationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ShopGovernorate = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ShopDescription = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ShopLocation = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    PublicEntity = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    LicenseNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LicenseDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LicenseIssuer = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    SeizureDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Assets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CorrespondenceMessages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CorrespondenceId = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    BodyHtml = table.Column<string>(type: "text", nullable: false),
                    BodyPlainText = table.Column<string>(type: "text", nullable: false),
                    MessageNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    MessageDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AuthorId = table.Column<int>(type: "integer", nullable: false),
                    AuthorName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AuthorRole = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CorrespondenceMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CorrespondenceReceipts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CorrespondenceId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    UserName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SeenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CorrespondenceReceipts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Correspondences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BranchId = table.Column<int>(type: "integer", nullable: true),
                    Governorate = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: false),
                    TargetUserId = table.Column<int>(type: "integer", nullable: false),
                    RecipientSectionId = table.Column<int>(type: "integer", nullable: true),
                    DocumentId = table.Column<int>(type: "integer", nullable: true),
                    CorrespondenceNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CorrespondenceDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Importance = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Correspondences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Correspondences_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Correspondences_Sections_RecipientSectionId",
                        column: x => x.RecipientSectionId,
                        principalTable: "Sections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DelegationAssetReservations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DelegationId = table.Column<int>(type: "integer", nullable: false),
                    SourceDocumentId = table.Column<int>(type: "integer", nullable: false),
                    AssetId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DelegationAssetReservations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DelegationAssets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DelegationId = table.Column<int>(type: "integer", nullable: false),
                    AssetKind = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    AssetLabel = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    SalePrice = table.Column<decimal>(type: "numeric(20,2)", nullable: true),
                    SnapshotAdjusted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DelegationAssets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DocumentAnnexes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DocumentId = table.Column<int>(type: "integer", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    AnnexType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    AnnexNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    AnnexDate = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentAnnexes", x => x.Id);
                });

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
                    ForwardedById = table.Column<int>(type: "integer", nullable: true),
                    ForwardedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ForwardState = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Owned"),
                    ForwardReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentAppeals", x => x.Id);
                    table.CheckConstraint("CK_DocumentAppeals_ForwardState", "\"ForwardState\" IN ('Owned', 'ForwardedToHead')");
                    table.CheckConstraint("CK_DocumentAppeals_Status", "\"Status\" IN ('pending', 'decided', 'struck-off')");
                });

            migrationBuilder.CreateTable(
                name: "DocumentAssignments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DocumentId = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    LawyerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AssignedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AssignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentAssignments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DocumentBaseNumbers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DocumentId = table.Column<int>(type: "integer", nullable: false),
                    ExecutionCircuitId = table.Column<int>(type: "integer", nullable: true),
                    CircuitName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    FileType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TypeKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    BaseNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    NumberKey = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CreatedById = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedById = table.Column<int>(type: "integer", nullable: true),
                    VoidedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    VoidedById = table.Column<int>(type: "integer", nullable: true),
                    VoidReason = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentBaseNumbers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DocumentDelegations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SourceDocumentId = table.Column<int>(type: "integer", nullable: false),
                    DelegatedCourt = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    DelegatedCourtNorm = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    DelegatedCircuitId = table.Column<int>(type: "integer", nullable: true),
                    IsExternal = table.Column<bool>(type: "boolean", nullable: false),
                    ExternalBranchId = table.Column<int>(type: "integer", nullable: true),
                    DelegationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DelegationText = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    DepositBookNumber = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DepositBookDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AssignedLawyerId = table.Column<int>(type: "integer", nullable: true),
                    RedirectedToSectionId = table.Column<int>(type: "integer", nullable: true),
                    RejectReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    ReturnDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SaleCoversFullDebt = table.Column<bool>(type: "boolean", nullable: true),
                    LocalAdditionCount = table.Column<int>(type: "integer", nullable: false),
                    LocalAdditionLastMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    LocalAdditionLastAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedById = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentDelegations", x => x.Id);
                    table.CheckConstraint("CK_DocumentDelegations_Status", "\"Status\" IN ('بانتظار رئيس القسم', 'محالة', 'مسجلة أصولًا', 'منفذ إنابة')");
                    table.ForeignKey(
                        name: "FK_DocumentDelegations_Branches_ExternalBranchId",
                        column: x => x.ExternalBranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DocumentDelegations_Sections_RedirectedToSectionId",
                        column: x => x.RedirectedToSectionId,
                        principalTable: "Sections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DocumentInterventionHeirs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DocumentInterventionId = table.Column<int>(type: "integer", nullable: false),
                    HeirName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    HeirFather = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    HeirFamily = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Capacity = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    AddressType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    HeirAddress = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    RepresentativeName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RepresentativeFather = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RepresentativeFamily = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RepresentativeCapacity = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    RepresentativeAddressType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    RepresentativeAddress = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentInterventionHeirs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DocumentInterventions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DocumentId = table.Column<int>(type: "integer", nullable: false),
                    Nature = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "natural"),
                    ReceiptNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ReceiptDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AcceptanceDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Subject = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Father = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Family = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Mother = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Birth = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Register = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    NationalId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    AddressType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    RepresentativeName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RepresentativeFather = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RepresentativeFamily = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RepresentativeCapacity = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    RepresentativeAddressType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    RepresentativeAddress = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    EntityName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    EntityBranch = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RegistrationNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RepresentedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Governorate = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RegistryId = table.Column<int>(type: "integer", nullable: true),
                    CreatedById = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentInterventions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DocumentOccurrences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DocumentId = table.Column<int>(type: "integer", nullable: false),
                    OccurrenceType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Source = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValue: "manual"),
                    EventDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FileNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    FileType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Year = table.Column<int>(type: "integer", nullable: true),
                    ReceiptNumber = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ReceiptDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Details = table.Column<string>(type: "text", nullable: true),
                    DocumentInterventionId = table.Column<int>(type: "integer", nullable: true),
                    FromCircuitName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ToCircuitName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedById = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentOccurrences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentOccurrences_DocumentInterventions_DocumentIntervent~",
                        column: x => x.DocumentInterventionId,
                        principalTable: "DocumentInterventions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DocumentRegistrationDates",
                columns: table => new
                {
                    DocumentId = table.Column<int>(type: "integer", nullable: false),
                    Date = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    DateParsed = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentRegistrationDates", x => x.DocumentId);
                });

            migrationBuilder.CreateTable(
                name: "Documents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
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
                    BorrowerRepresentativeName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    BorrowerRepresentativeFather = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    BorrowerRepresentativeFamily = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    BorrowerRepresentativeCapacity = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    BorrowerRepresentativeAddressType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    BorrowerRepresentativeAddress = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    BorrowerNature = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "natural"),
                    BorrowerRegistrationNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    BorrowerRepresentedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ContractType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ContractTypeSelector = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    ContractNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ContractDate = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    AnnexType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    AnnexNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    AnnexDate = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    InclusionText = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    InclusionNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    AmountNumeric = table.Column<decimal>(type: "numeric(20,2)", nullable: false),
                    AmountWords = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Currency = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Amount2Numeric = table.Column<decimal>(type: "numeric(20,2)", nullable: false),
                    Amount2Words = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Currency2 = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Amount3Numeric = table.Column<decimal>(type: "numeric(20,2)", nullable: false),
                    Amount3Words = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Currency3 = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    InclusionAmountNumeric = table.Column<decimal>(type: "numeric(20,2)", nullable: false),
                    InclusionAmountWords = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    InclusionCurrency = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    InclusionAmount2Numeric = table.Column<decimal>(type: "numeric(20,2)", nullable: false),
                    InclusionAmount2Words = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    InclusionCurrency2 = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    InclusionAmount3Numeric = table.Column<decimal>(type: "numeric(20,2)", nullable: false),
                    InclusionAmount3Words = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    InclusionCurrency3 = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Court = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CourtNorm = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ExecutionCircuitId = table.Column<int>(type: "integer", nullable: true),
                    NeedsRegistration = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    Applicant = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Lawyer = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ApplicantRegistryId = table.Column<int>(type: "integer", nullable: true),
                    ReferredFromLawyer = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ReferredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FileNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    FileType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    FileYear = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    FileIncoming = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    FileIncomingDate = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    UnderFilingNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    FileArrivalNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    FileArrivalDate = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    BranchName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    ExecStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    ExecSubStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    CollectedAmount = table.Column<decimal>(type: "numeric(20,2)", nullable: true),
                    CollectedAmount2 = table.Column<decimal>(type: "numeric(20,2)", nullable: true),
                    CollectedAmount3 = table.Column<decimal>(type: "numeric(20,2)", nullable: true),
                    CollectedCurrency = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CollectedCurrency2 = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CollectedCurrency3 = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    GeneralEntitySide = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ExecutedStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    WasDepositExecuted = table.Column<bool>(type: "boolean", nullable: false),
                    ExecutedDescription = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    FileReceiptDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FileReceiptNumber = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ExecutedRequiredAmount = table.Column<decimal>(type: "numeric(20,2)", nullable: true),
                    ExecutedRequiredCurrency = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ExecutedRequiredAmount2 = table.Column<decimal>(type: "numeric(20,2)", nullable: true),
                    ExecutedRequiredCurrency2 = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ExecutedRequiredAmount3 = table.Column<decimal>(type: "numeric(20,2)", nullable: true),
                    ExecutedRequiredCurrency3 = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ExecutedPaidAmount = table.Column<decimal>(type: "numeric(20,2)", nullable: true),
                    ExecutedPaidCurrency = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ExecutedPaidAmount2 = table.Column<decimal>(type: "numeric(20,2)", nullable: true),
                    ExecutedPaidCurrency2 = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ExecutedPaidAmount3 = table.Column<decimal>(type: "numeric(20,2)", nullable: true),
                    ExecutedPaidCurrency3 = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ExecutedDepositDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExecutedExecutionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    StruckOffDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    StrikeOffReminderDismissedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RenewalFileReceiptNumber = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RenewalFileReceiptDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RenewalFileNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RenewalFileType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RenewalDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    BaraetNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    BaraetDate = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    BaraetRegNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    BaraetRegDate = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ForcedExecutionDate = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ForcibleTransferDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ForcibleTransferNoticeNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TarithNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TarithDate = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    TarithRegNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TarithRegDate = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    NoFundsDemandNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    NoFundsDemandDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    StartReferralNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    StartReferralDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SayerNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SayerDate = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    SayerRegNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SayerRegDate = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    SoldAssetIds = table.Column<string>(type: "text", nullable: true),
                    SeizureDate = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ImmediateActions = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    FullData = table.Column<string>(type: "text", nullable: true),
                    SearchText = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    FilePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PrintCount = table.Column<int>(type: "integer", nullable: false),
                    SourceDelegationId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Documents", x => x.Id);
                    table.CheckConstraint("CK_Documents_ExecStatus", "\"ExecStatus\" IN ('', 'منفذ جبريا', 'منفذ بالتسوية', 'تريث', 'منفذ إنابة', 'مسترد', 'محال الى البداية', 'مشطوب')");
                    table.CheckConstraint("CK_Documents_ExecSubStatus", "\"ExecSubStatus\" IN ('منفذ جزئيا', 'منفذ كاملا')");
                    table.CheckConstraint("CK_Documents_ExecutedStatus", "\"ExecutedStatus\" IN ('', 'منفذ', 'مشطوب')");
                    table.CheckConstraint("CK_Documents_GeneralEntitySide", "\"GeneralEntitySide\" IN ('applicant', 'executed', 'deposit')");
                    table.ForeignKey(
                        name: "FK_Documents_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Documents_DocumentDelegations_SourceDelegationId",
                        column: x => x.SourceDelegationId,
                        principalTable: "DocumentDelegations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
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
                    DeceasedFamily = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RepresentativeName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RepresentativeFather = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RepresentativeFamily = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RepresentativeCapacity = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    RepresentativeAddressType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    RepresentativeAddress = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
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
                    AddressType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    GuarantorNature = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "natural"),
                    GuarantorRegistrationNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    GuarantorRepresentedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RepresentativeName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RepresentativeFather = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RepresentativeFamily = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RepresentativeCapacity = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    RepresentativeAddressType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    RepresentativeAddress = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
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
                name: "Heirs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DocumentId = table.Column<int>(type: "integer", nullable: false),
                    GuarantorNumber = table.Column<int>(type: "integer", nullable: true),
                    HeirName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    HeirFather = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    HeirFamily = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    HeirCapacity = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    AddressType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    HeirAddress = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    RepresentativeName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RepresentativeFather = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RepresentativeFamily = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RepresentativeCapacity = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    RepresentativeAddressType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    RepresentativeAddress = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
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
                    HeirAddress = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    RepresentativeName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RepresentativeFather = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RepresentativeFamily = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RepresentativeCapacity = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    RepresentativeAddressType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    RepresentativeAddress = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
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
                });

            migrationBuilder.CreateTable(
                name: "ExecutedPublicEntities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DocumentId = table.Column<int>(type: "integer", nullable: false),
                    EntityName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    EntityBranch = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Governorate = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RegistryId = table.Column<int>(type: "integer", nullable: true),
                    EntityNature = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "public"),
                    RegistrationNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RepresentedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AddressType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
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
                    ActionDateParsed = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReminderDuration = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ReminderColor = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ReminderBaseDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    ApplicantNature = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "natural"),
                    ApplicantRegistrationNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ApplicantRepresentedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ApplicantAddressType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ApplicantAddress = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    RegistryId = table.Column<int>(type: "integer", nullable: true),
                    RepresentationType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    DeceasedName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DeceasedFather = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DeceasedFamily = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RepresentativeName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RepresentativeFather = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RepresentativeFamily = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RepresentativeCapacity = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    RepresentativeLegalRepresentative = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
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
                name: "ExecutionCircuits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BranchId = table.Column<int>(type: "integer", nullable: false),
                    SectionId = table.Column<int>(type: "integer", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NameNorm = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedById = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExecutionCircuits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExecutionCircuits_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExecutionCircuits_Sections_SectionId",
                        column: x => x.SectionId,
                        principalTable: "Sections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ForumMessageReads",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MessageId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    UserName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ReadAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ForumMessageReads", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ForumMessages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Body = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    AuthorId = table.Column<int>(type: "integer", nullable: false),
                    AuthorName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AuthorRole = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AuthorLocation = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AuthorSection = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IsPinned = table.Column<bool>(type: "boolean", nullable: false),
                    QuotedMessageId = table.Column<int>(type: "integer", nullable: true),
                    QuotedAuthorName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    QuotedExcerpt = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EditedById = table.Column<int>(type: "integer", nullable: true),
                    EditedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ForumMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ForumMessages_ForumMessages_QuotedMessageId",
                        column: x => x.QuotedMessageId,
                        principalTable: "ForumMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
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
                    Kind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DedupeKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ExecutionCircuitId = table.Column<int>(type: "integer", nullable: true),
                    DocumentId = table.Column<int>(type: "integer", nullable: true),
                    TargetLawyerId = table.Column<int>(type: "integer", nullable: true),
                    DelegationId = table.Column<int>(type: "integer", nullable: true),
                    AppealId = table.Column<int>(type: "integer", nullable: true),
                    ReviewLetterId = table.Column<int>(type: "integer", nullable: true),
                    PublicEntityId = table.Column<int>(type: "integer", nullable: true),
                    Message = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastContentChangedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsArchived = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    ArchivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HeadAlerts", x => x.Id);
                    table.CheckConstraint("CK_HeadAlerts_Kind_NotEmpty", "TRIM(\"Kind\") <> ''");
                    table.ForeignKey(
                        name: "FK_HeadAlerts_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HeadAlerts_DocumentAppeals_AppealId",
                        column: x => x.AppealId,
                        principalTable: "DocumentAppeals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_HeadAlerts_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_HeadAlerts_ExecutionCircuits_ExecutionCircuitId",
                        column: x => x.ExecutionCircuitId,
                        principalTable: "ExecutionCircuits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "HeadSuccessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BranchId = table.Column<int>(type: "integer", nullable: false),
                    SectionId = table.Column<int>(type: "integer", nullable: true),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Event = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    At = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ActorName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HeadSuccessions", x => x.Id);
                    table.CheckConstraint("CK_HeadSuccessions_Event", "\"Event\" IN ('appointed', 'deactivated', 'succeeded', 'circuit-transferred', 'renamed')");
                    table.ForeignKey(
                        name: "FK_HeadSuccessions_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HeadSuccessions_Sections_SectionId",
                        column: x => x.SectionId,
                        principalTable: "Sections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ParentEditSuggestions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GroupId = table.Column<int>(type: "integer", nullable: false),
                    EntryId = table.Column<int>(type: "integer", nullable: false),
                    ProposedCanonicalName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ProposedEntityType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    ProposedCitationFormula = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: false),
                    CreatedBranchId = table.Column<int>(type: "integer", nullable: false),
                    ReviewedById = table.Column<int>(type: "integer", nullable: true),
                    ReviewReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReviewedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParentEditSuggestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ParentEditSuggestions_PublicEntityGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "PublicEntityGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PersonalReminders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LawyerId = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Color = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Recurrence = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RecurrenceEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsArchived = table.Column<bool>(type: "boolean", nullable: false),
                    CompletedOccurrenceKeys = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonalReminders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PublicEntities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GroupId = table.Column<int>(type: "integer", nullable: false),
                    Governorate = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    BranchName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsParentEntity = table.Column<bool>(type: "boolean", nullable: false),
                    CoverageLabel = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    CitationFormula = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    NeedsReview = table.Column<bool>(type: "boolean", nullable: false),
                    ReviewedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReviewedById = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PublicEntities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PublicEntities_PublicEntityGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "PublicEntityGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PublicEntityAliases",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PublicEntityId = table.Column<int>(type: "integer", nullable: false),
                    AliasText = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PublicEntityAliases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PublicEntityAliases_PublicEntities_PublicEntityId",
                        column: x => x.PublicEntityId,
                        principalTable: "PublicEntities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
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
                    SectionId = table.Column<int>(type: "integer", nullable: true),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    TokenVersion = table.Column<int>(type: "integer", nullable: false),
                    FailedLoginCount = table.Column<int>(type: "integer", nullable: false),
                    LockoutEndUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastLogin = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PortalGroupId = table.Column<int>(type: "integer", nullable: true),
                    PortalEntryId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                    table.CheckConstraint("CK_Users_BranchRequiredForBranchRoles", "\"BranchId\" IS NOT NULL OR \"Role\" NOT IN ('Lawyer', 'Head', 'SubHead')");
                    table.ForeignKey(
                        name: "FK_Users_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Users_PublicEntities_PortalEntryId",
                        column: x => x.PortalEntryId,
                        principalTable: "PublicEntities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Users_PublicEntityGroups_PortalGroupId",
                        column: x => x.PortalGroupId,
                        principalTable: "PublicEntityGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Users_Sections_SectionId",
                        column: x => x.SectionId,
                        principalTable: "Sections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Users_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

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

            migrationBuilder.CreateTable(
                name: "ReviewLetters",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BranchId = table.Column<int>(type: "integer", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: false),
                    DocumentId = table.Column<int>(type: "integer", nullable: true),
                    RecipientSectionId = table.Column<int>(type: "integer", nullable: true),
                    LetterNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    LetterDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsAnswered = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReviewLetters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReviewLetters_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReviewLetters_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ReviewLetters_Sections_RecipientSectionId",
                        column: x => x.RecipientSectionId,
                        principalTable: "Sections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReviewLetters_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReviewLetterMessages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ReviewLetterId = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    BodyHtml = table.Column<string>(type: "text", nullable: false),
                    BodyPlainText = table.Column<string>(type: "text", nullable: false),
                    MessageNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    MessageDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AuthorId = table.Column<int>(type: "integer", nullable: false),
                    AuthorName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AuthorRole = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IsSeenByLawyer = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReviewLetterMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReviewLetterMessages_ReviewLetters_ReviewLetterId",
                        column: x => x.ReviewLetterId,
                        principalTable: "ReviewLetters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

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
                columns: new[] { "AppealId", "Year" });

            migrationBuilder.CreateIndex(
                name: "IX_AppealBaseNumbers_CreatedById",
                table: "AppealBaseNumbers",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicantPublicEntities_DocumentId",
                table: "ApplicantPublicEntities",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicantPublicEntities_RegistryId",
                table: "ApplicantPublicEntities",
                column: "RegistryId");

            migrationBuilder.CreateIndex(
                name: "IX_AppSuggestions_CreatedAt",
                table: "AppSuggestions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AppSuggestions_SenderId",
                table: "AppSuggestions",
                column: "SenderId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetOwners_AssetId",
                table: "AssetOwners",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_Assets_DocumentId",
                table: "Assets",
                column: "DocumentId");

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
                name: "IX_CorrespondenceMessages_BodyPlainText",
                table: "CorrespondenceMessages",
                column: "BodyPlainText");

            migrationBuilder.CreateIndex(
                name: "IX_CorrespondenceMessages_CorrespondenceId",
                table: "CorrespondenceMessages",
                column: "CorrespondenceId");

            migrationBuilder.CreateIndex(
                name: "IX_CorrespondenceReceipts_CorrespondenceId_UserId",
                table: "CorrespondenceReceipts",
                columns: new[] { "CorrespondenceId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CorrespondenceReceipts_UserId",
                table: "CorrespondenceReceipts",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Correspondences_BranchId",
                table: "Correspondences",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_Correspondences_CorrespondenceNumber",
                table: "Correspondences",
                column: "CorrespondenceNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Correspondences_CreatedById",
                table: "Correspondences",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_Correspondences_DocumentId",
                table: "Correspondences",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_Correspondences_Governorate",
                table: "Correspondences",
                column: "Governorate");

            migrationBuilder.CreateIndex(
                name: "IX_Correspondences_Importance",
                table: "Correspondences",
                column: "Importance");

            migrationBuilder.CreateIndex(
                name: "IX_Correspondences_RecipientSectionId",
                table: "Correspondences",
                column: "RecipientSectionId");

            migrationBuilder.CreateIndex(
                name: "IX_Correspondences_TargetUserId",
                table: "Correspondences",
                column: "TargetUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Correspondences_UpdatedAt",
                table: "Correspondences",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_DelegationAssetReservations_DelegationId",
                table: "DelegationAssetReservations",
                column: "DelegationId");

            migrationBuilder.CreateIndex(
                name: "IX_DelegationAssetReservations_SourceDocumentId_AssetId",
                table: "DelegationAssetReservations",
                columns: new[] { "SourceDocumentId", "AssetId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DelegationAssets_DelegationId",
                table: "DelegationAssets",
                column: "DelegationId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentAnnexes_DocumentId_Sequence",
                table: "DocumentAnnexes",
                columns: new[] { "DocumentId", "Sequence" },
                unique: true);

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
                name: "IX_DocumentAppeals_ForwardedById",
                table: "DocumentAppeals",
                column: "ForwardedById");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentAppeals_ForwardState",
                table: "DocumentAppeals",
                column: "ForwardState");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentAppeals_Status",
                table: "DocumentAppeals",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentAssignments_DocumentId",
                table: "DocumentAssignments",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentBaseNumbers_CreatedById",
                table: "DocumentBaseNumbers",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentBaseNumbers_DocumentId_Year_CreatedAt",
                table: "DocumentBaseNumbers",
                columns: new[] { "DocumentId", "Year", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentBaseNumbers_ExecutionCircuitId_Year",
                table: "DocumentBaseNumbers",
                columns: new[] { "ExecutionCircuitId", "Year" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentBaseNumbers_UpdatedById",
                table: "DocumentBaseNumbers",
                column: "UpdatedById");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentBaseNumbers_VoidedById",
                table: "DocumentBaseNumbers",
                column: "VoidedById");

            migrationBuilder.CreateIndex(
                name: "UX_DocumentBaseNumbers_Active",
                table: "DocumentBaseNumbers",
                columns: new[] { "ExecutionCircuitId", "TypeKey", "Year", "NumberKey" },
                unique: true,
                filter: "\"VoidedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentDelegations_AssignedLawyerId",
                table: "DocumentDelegations",
                column: "AssignedLawyerId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentDelegations_CreatedById",
                table: "DocumentDelegations",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentDelegations_DelegatedCircuitId",
                table: "DocumentDelegations",
                column: "DelegatedCircuitId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentDelegations_ExternalBranchId",
                table: "DocumentDelegations",
                column: "ExternalBranchId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentDelegations_RedirectedToSectionId",
                table: "DocumentDelegations",
                column: "RedirectedToSectionId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentDelegations_SourceDocumentId",
                table: "DocumentDelegations",
                column: "SourceDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentDelegations_Status",
                table: "DocumentDelegations",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentFieldChanges_AuditLogId",
                table: "DocumentFieldChanges",
                column: "AuditLogId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentFieldChanges_DocumentId_Id",
                table: "DocumentFieldChanges",
                columns: new[] { "DocumentId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentInterventionHeirs_DocumentInterventionId",
                table: "DocumentInterventionHeirs",
                column: "DocumentInterventionId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentInterventions_CreatedById",
                table: "DocumentInterventions",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentInterventions_DocumentId",
                table: "DocumentInterventions",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentInterventions_RegistryId",
                table: "DocumentInterventions",
                column: "RegistryId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentOccurrences_CreatedById",
                table: "DocumentOccurrences",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentOccurrences_DocumentId",
                table: "DocumentOccurrences",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentOccurrences_DocumentInterventionId",
                table: "DocumentOccurrences",
                column: "DocumentInterventionId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentOccurrences_EventDate",
                table: "DocumentOccurrences",
                column: "EventDate");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentOccurrences_OccurrenceType",
                table: "DocumentOccurrences",
                column: "OccurrenceType");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentRegistrationDates_DateParsed",
                table: "DocumentRegistrationDates",
                column: "DateParsed");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_ApplicantRegistryId",
                table: "Documents",
                column: "ApplicantRegistryId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_BranchId",
                table: "Documents",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_CourtNorm",
                table: "Documents",
                column: "CourtNorm");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_CourtNorm_FileNumber_FileType_FileYear",
                table: "Documents",
                columns: new[] { "CourtNorm", "FileNumber", "FileType", "FileYear" },
                unique: true,
                filter: "NOT \"IsDeleted\" AND \"FileNumber\" IS NOT NULL AND \"ExecutionCircuitId\" IS NULL AND \"CourtNorm\" IS NOT NULL");

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
                name: "IX_Documents_ExecutionCircuitId",
                table: "Documents",
                column: "ExecutionCircuitId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_ExecutionCircuitId_FileNumber_FileType_FileYear",
                table: "Documents",
                columns: new[] { "ExecutionCircuitId", "FileNumber", "FileType", "FileYear" },
                unique: true,
                filter: "NOT \"IsDeleted\" AND \"FileNumber\" IS NOT NULL AND \"ExecutionCircuitId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_GeneralEntitySide",
                table: "Documents",
                column: "GeneralEntitySide");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_SearchText",
                table: "Documents",
                column: "SearchText");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_SourceDelegationId",
                table: "Documents",
                column: "SourceDelegationId",
                unique: true);

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
                name: "IX_ExecutedPublicEntities_RegistryId",
                table: "ExecutedPublicEntities",
                column: "RegistryId");

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
                name: "IX_ExecutionApplicants_RegistryId",
                table: "ExecutionApplicants",
                column: "RegistryId");

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionCircuits_BranchId",
                table: "ExecutionCircuits",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionCircuits_BranchId_NameNorm",
                table: "ExecutionCircuits",
                columns: new[] { "BranchId", "NameNorm" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionCircuits_CreatedById",
                table: "ExecutionCircuits",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionCircuits_IsActive",
                table: "ExecutionCircuits",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionCircuits_SectionId",
                table: "ExecutionCircuits",
                column: "SectionId");

            migrationBuilder.CreateIndex(
                name: "IX_ForumMessageReads_MessageId_UserId",
                table: "ForumMessageReads",
                columns: new[] { "MessageId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ForumMessageReads_UserId_MessageId",
                table: "ForumMessageReads",
                columns: new[] { "UserId", "MessageId" });

            migrationBuilder.CreateIndex(
                name: "IX_ForumMessages_AuthorId",
                table: "ForumMessages",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_ForumMessages_CreatedAt",
                table: "ForumMessages",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ForumMessages_IsPinned",
                table: "ForumMessages",
                column: "IsPinned",
                unique: true,
                filter: "\"IsPinned\"");

            migrationBuilder.CreateIndex(
                name: "IX_ForumMessages_QuotedMessageId",
                table: "ForumMessages",
                column: "QuotedMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_Guarantors_DocumentId",
                table: "Guarantors",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_HeadAlertOutboxMessages_Status_LockedUntil",
                table: "HeadAlertOutboxMessages",
                columns: new[] { "Status", "LockedUntil" });

            migrationBuilder.CreateIndex(
                name: "IX_HeadAlertOutboxMessages_Status_NextAttemptAt_Id",
                table: "HeadAlertOutboxMessages",
                columns: new[] { "Status", "NextAttemptAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_HeadAlertOutboxMessages_Status_ProcessedAt",
                table: "HeadAlertOutboxMessages",
                columns: new[] { "Status", "ProcessedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_HeadAlertOutboxMessages_Status_TargetUserId",
                table: "HeadAlertOutboxMessages",
                columns: new[] { "Status", "TargetUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_HeadAlertRecipients_HeadAlertId",
                table: "HeadAlertRecipients",
                column: "HeadAlertId");

            migrationBuilder.CreateIndex(
                name: "IX_HeadAlertRecipients_HeadAlertId_IsRead",
                table: "HeadAlertRecipients",
                columns: new[] { "HeadAlertId", "IsRead" });

            migrationBuilder.CreateIndex(
                name: "IX_HeadAlertRecipients_UserId",
                table: "HeadAlertRecipients",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_HeadAlertRecipients_UserId_IsRead",
                table: "HeadAlertRecipients",
                columns: new[] { "UserId", "IsRead" });

            migrationBuilder.CreateIndex(
                name: "UX_HeadAlertRecipients_HeadAlertId_UserId",
                table: "HeadAlertRecipients",
                columns: new[] { "HeadAlertId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HeadAlerts_AppealId",
                table: "HeadAlerts",
                column: "AppealId");

            migrationBuilder.CreateIndex(
                name: "IX_HeadAlerts_BranchId",
                table: "HeadAlerts",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_HeadAlerts_BranchId_CreatedById_Active_List",
                table: "HeadAlerts",
                columns: new[] { "BranchId", "CreatedById", "IsArchived", "LastContentChangedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_HeadAlerts_BranchId_IsArchived_LastContentChangedAt_Id",
                table: "HeadAlerts",
                columns: new[] { "BranchId", "IsArchived", "LastContentChangedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_HeadAlerts_CreatedAt",
                table: "HeadAlerts",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_HeadAlerts_CreatedById",
                table: "HeadAlerts",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_HeadAlerts_DelegationId",
                table: "HeadAlerts",
                column: "DelegationId");

            migrationBuilder.CreateIndex(
                name: "IX_HeadAlerts_DocumentId",
                table: "HeadAlerts",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_HeadAlerts_ExecutionCircuitId_Kind_IsArchived",
                table: "HeadAlerts",
                columns: new[] { "ExecutionCircuitId", "Kind", "IsArchived" });

            migrationBuilder.CreateIndex(
                name: "IX_HeadAlerts_IsArchived_LastContentChangedAt_Id",
                table: "HeadAlerts",
                columns: new[] { "IsArchived", "LastContentChangedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_HeadAlerts_Kind_Active_LastChanged",
                table: "HeadAlerts",
                columns: new[] { "Kind", "IsArchived", "LastContentChangedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_HeadAlerts_PublicEntityId",
                table: "HeadAlerts",
                column: "PublicEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_HeadAlerts_ReviewLetterId",
                table: "HeadAlerts",
                column: "ReviewLetterId");

            migrationBuilder.CreateIndex(
                name: "IX_HeadAlerts_TargetLawyerId_Kind_IsArchived",
                table: "HeadAlerts",
                columns: new[] { "TargetLawyerId", "Kind", "IsArchived" });

            migrationBuilder.CreateIndex(
                name: "UX_HeadAlerts_DedupeKey_Active",
                table: "HeadAlerts",
                column: "DedupeKey",
                unique: true,
                filter: "\"DedupeKey\" IS NOT NULL AND \"IsArchived\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_HeadSuccessions_At",
                table: "HeadSuccessions",
                column: "At");

            migrationBuilder.CreateIndex(
                name: "IX_HeadSuccessions_BranchId",
                table: "HeadSuccessions",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_HeadSuccessions_SectionId",
                table: "HeadSuccessions",
                column: "SectionId");

            migrationBuilder.CreateIndex(
                name: "IX_HeadSuccessions_UserId",
                table: "HeadSuccessions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Heirs_DocumentId",
                table: "Heirs",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyKeys_ExpiresAt",
                table: "IdempotencyKeys",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyKeys_Key_Operation_UserId",
                table: "IdempotencyKeys",
                columns: new[] { "Key", "Operation", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LoginAttempts_Key",
                table: "LoginAttempts",
                column: "Key");

            migrationBuilder.CreateIndex(
                name: "IX_ParentEditSuggestions_CreatedBranchId",
                table: "ParentEditSuggestions",
                column: "CreatedBranchId");

            migrationBuilder.CreateIndex(
                name: "IX_ParentEditSuggestions_CreatedById",
                table: "ParentEditSuggestions",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_ParentEditSuggestions_EntryId",
                table: "ParentEditSuggestions",
                column: "EntryId");

            migrationBuilder.CreateIndex(
                name: "IX_ParentEditSuggestions_GroupId_CreatedBranchId",
                table: "ParentEditSuggestions",
                columns: new[] { "GroupId", "CreatedBranchId" },
                unique: true,
                filter: "\"Status\" = 'pending'");

            migrationBuilder.CreateIndex(
                name: "IX_ParentEditSuggestions_ReviewedById",
                table: "ParentEditSuggestions",
                column: "ReviewedById");

            migrationBuilder.CreateIndex(
                name: "IX_ParentEditSuggestions_Status",
                table: "ParentEditSuggestions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_PersonalReminders_IsArchived",
                table: "PersonalReminders",
                column: "IsArchived");

            migrationBuilder.CreateIndex(
                name: "IX_PersonalReminders_LawyerId",
                table: "PersonalReminders",
                column: "LawyerId");

            migrationBuilder.CreateIndex(
                name: "IX_PersonalReminders_LawyerId_DueDate",
                table: "PersonalReminders",
                columns: new[] { "LawyerId", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_PublicEntities_CreatedById",
                table: "PublicEntities",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_PublicEntities_Governorate",
                table: "PublicEntities",
                column: "Governorate");

            migrationBuilder.CreateIndex(
                name: "IX_PublicEntities_GroupId",
                table: "PublicEntities",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_PublicEntities_GroupId_Governorate_BranchName",
                table: "PublicEntities",
                columns: new[] { "GroupId", "Governorate", "BranchName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PublicEntities_IsActive",
                table: "PublicEntities",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_PublicEntities_IsParentEntity",
                table: "PublicEntities",
                column: "IsParentEntity");

            migrationBuilder.CreateIndex(
                name: "IX_PublicEntities_NeedsReview",
                table: "PublicEntities",
                column: "NeedsReview");

            migrationBuilder.CreateIndex(
                name: "IX_PublicEntities_ReviewedById",
                table: "PublicEntities",
                column: "ReviewedById");

            migrationBuilder.CreateIndex(
                name: "IX_PublicEntities_Status",
                table: "PublicEntities",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_PublicEntityAliases_AliasText",
                table: "PublicEntityAliases",
                column: "AliasText");

            migrationBuilder.CreateIndex(
                name: "IX_PublicEntityAliases_PublicEntityId",
                table: "PublicEntityAliases",
                column: "PublicEntityId");

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

            migrationBuilder.CreateIndex(
                name: "IX_PublicEntityGroups_CanonicalName",
                table: "PublicEntityGroups",
                column: "CanonicalName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PublicEntityGroups_CanonicalNameNorm",
                table: "PublicEntityGroups",
                column: "CanonicalNameNorm",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PublicEntityGroups_EntityType",
                table: "PublicEntityGroups",
                column: "EntityType");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewLetterMessages_BodyPlainText",
                table: "ReviewLetterMessages",
                column: "BodyPlainText");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewLetterMessages_ReviewLetterId",
                table: "ReviewLetterMessages",
                column: "ReviewLetterId");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewLetters_BranchId",
                table: "ReviewLetters",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewLetters_CreatedById",
                table: "ReviewLetters",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewLetters_DocumentId",
                table: "ReviewLetters",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewLetters_IsAnswered",
                table: "ReviewLetters",
                column: "IsAnswered");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewLetters_LetterNumber",
                table: "ReviewLetters",
                column: "LetterNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReviewLetters_RecipientSectionId",
                table: "ReviewLetters",
                column: "RecipientSectionId");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewLetters_UpdatedAt",
                table: "ReviewLetters",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Sections_BranchId",
                table: "Sections",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_Sections_BranchId_NameNorm",
                table: "Sections",
                columns: new[] { "BranchId", "NameNorm" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sections_IsActive",
                table: "Sections",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Users_BranchId",
                table: "Users",
                column: "BranchId",
                unique: true,
                filter: "\"Role\" = 'Head' AND \"IsActive\"");

            migrationBuilder.CreateIndex(
                name: "IX_Users_CreatedById",
                table: "Users",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_Users_PortalEntryId",
                table: "Users",
                column: "PortalEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_PortalGroupId",
                table: "Users",
                column: "PortalGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_SectionId",
                table: "Users",
                column: "SectionId",
                unique: true,
                filter: "\"Role\" = 'SubHead' AND \"IsActive\"");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true,
                filter: "\"BranchId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username_BranchId",
                table: "Users",
                columns: new[] { "Username", "BranchId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AppealActions_DocumentAppeals_AppealId",
                table: "AppealActions",
                column: "AppealId",
                principalTable: "DocumentAppeals",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AppealActions_Users_CreatedById",
                table: "AppealActions",
                column: "CreatedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AppealBaseNumbers_DocumentAppeals_AppealId",
                table: "AppealBaseNumbers",
                column: "AppealId",
                principalTable: "DocumentAppeals",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AppealBaseNumbers_Users_CreatedById",
                table: "AppealBaseNumbers",
                column: "CreatedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ApplicantPublicEntities_Documents_DocumentId",
                table: "ApplicantPublicEntities",
                column: "DocumentId",
                principalTable: "Documents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ApplicantPublicEntities_PublicEntities_RegistryId",
                table: "ApplicantPublicEntities",
                column: "RegistryId",
                principalTable: "PublicEntities",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_AppSuggestions_Users_SenderId",
                table: "AppSuggestions",
                column: "SenderId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AssetOwners_Assets_AssetId",
                table: "AssetOwners",
                column: "AssetId",
                principalTable: "Assets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Assets_Documents_DocumentId",
                table: "Assets",
                column: "DocumentId",
                principalTable: "Documents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CorrespondenceMessages_Correspondences_CorrespondenceId",
                table: "CorrespondenceMessages",
                column: "CorrespondenceId",
                principalTable: "Correspondences",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CorrespondenceReceipts_Correspondences_CorrespondenceId",
                table: "CorrespondenceReceipts",
                column: "CorrespondenceId",
                principalTable: "Correspondences",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Correspondences_Documents_DocumentId",
                table: "Correspondences",
                column: "DocumentId",
                principalTable: "Documents",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Correspondences_Users_CreatedById",
                table: "Correspondences",
                column: "CreatedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Correspondences_Users_TargetUserId",
                table: "Correspondences",
                column: "TargetUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DelegationAssetReservations_DocumentDelegations_DelegationId",
                table: "DelegationAssetReservations",
                column: "DelegationId",
                principalTable: "DocumentDelegations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_DelegationAssets_DocumentDelegations_DelegationId",
                table: "DelegationAssets",
                column: "DelegationId",
                principalTable: "DocumentDelegations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentAnnexes_Documents_DocumentId",
                table: "DocumentAnnexes",
                column: "DocumentId",
                principalTable: "Documents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentAppeals_Documents_DocumentId",
                table: "DocumentAppeals",
                column: "DocumentId",
                principalTable: "Documents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentAppeals_Users_AssignedLawyerId",
                table: "DocumentAppeals",
                column: "AssignedLawyerId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentAppeals_Users_CreatedById",
                table: "DocumentAppeals",
                column: "CreatedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentAssignments_Documents_DocumentId",
                table: "DocumentAssignments",
                column: "DocumentId",
                principalTable: "Documents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentBaseNumbers_Documents_DocumentId",
                table: "DocumentBaseNumbers",
                column: "DocumentId",
                principalTable: "Documents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentBaseNumbers_ExecutionCircuits_ExecutionCircuitId",
                table: "DocumentBaseNumbers",
                column: "ExecutionCircuitId",
                principalTable: "ExecutionCircuits",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentBaseNumbers_Users_CreatedById",
                table: "DocumentBaseNumbers",
                column: "CreatedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentBaseNumbers_Users_UpdatedById",
                table: "DocumentBaseNumbers",
                column: "UpdatedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentBaseNumbers_Users_VoidedById",
                table: "DocumentBaseNumbers",
                column: "VoidedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentDelegations_Documents_SourceDocumentId",
                table: "DocumentDelegations",
                column: "SourceDocumentId",
                principalTable: "Documents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentDelegations_ExecutionCircuits_DelegatedCircuitId",
                table: "DocumentDelegations",
                column: "DelegatedCircuitId",
                principalTable: "ExecutionCircuits",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentDelegations_Users_AssignedLawyerId",
                table: "DocumentDelegations",
                column: "AssignedLawyerId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentDelegations_Users_CreatedById",
                table: "DocumentDelegations",
                column: "CreatedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentInterventionHeirs_DocumentInterventions_DocumentInt~",
                table: "DocumentInterventionHeirs",
                column: "DocumentInterventionId",
                principalTable: "DocumentInterventions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentInterventions_Documents_DocumentId",
                table: "DocumentInterventions",
                column: "DocumentId",
                principalTable: "Documents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentInterventions_PublicEntities_RegistryId",
                table: "DocumentInterventions",
                column: "RegistryId",
                principalTable: "PublicEntities",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentInterventions_Users_CreatedById",
                table: "DocumentInterventions",
                column: "CreatedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentOccurrences_Documents_DocumentId",
                table: "DocumentOccurrences",
                column: "DocumentId",
                principalTable: "Documents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentOccurrences_Users_CreatedById",
                table: "DocumentOccurrences",
                column: "CreatedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentRegistrationDates_Documents_DocumentId",
                table: "DocumentRegistrationDates",
                column: "DocumentId",
                principalTable: "Documents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Documents_ExecutionCircuits_ExecutionCircuitId",
                table: "Documents",
                column: "ExecutionCircuitId",
                principalTable: "ExecutionCircuits",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Documents_Users_CreatedById",
                table: "Documents",
                column: "CreatedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ExecutedHeirs_ExecutionApplicants_ExecutionApplicantId",
                table: "ExecutedHeirs",
                column: "ExecutionApplicantId",
                principalTable: "ExecutionApplicants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ExecutedPublicEntities_PublicEntities_RegistryId",
                table: "ExecutedPublicEntities",
                column: "RegistryId",
                principalTable: "PublicEntities",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ExecutionActions_Users_CreatedById",
                table: "ExecutionActions",
                column: "CreatedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ExecutionApplicants_PublicEntities_RegistryId",
                table: "ExecutionApplicants",
                column: "RegistryId",
                principalTable: "PublicEntities",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ExecutionCircuits_Users_CreatedById",
                table: "ExecutionCircuits",
                column: "CreatedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ForumMessageReads_ForumMessages_MessageId",
                table: "ForumMessageReads",
                column: "MessageId",
                principalTable: "ForumMessages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ForumMessages_Users_AuthorId",
                table: "ForumMessages",
                column: "AuthorId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_HeadAlertRecipients_HeadAlerts_HeadAlertId",
                table: "HeadAlertRecipients",
                column: "HeadAlertId",
                principalTable: "HeadAlerts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_HeadAlertRecipients_Users_UserId",
                table: "HeadAlertRecipients",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_HeadAlerts_PublicEntities_PublicEntityId",
                table: "HeadAlerts",
                column: "PublicEntityId",
                principalTable: "PublicEntities",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_HeadAlerts_ReviewLetters_ReviewLetterId",
                table: "HeadAlerts",
                column: "ReviewLetterId",
                principalTable: "ReviewLetters",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_HeadAlerts_Users_CreatedById",
                table: "HeadAlerts",
                column: "CreatedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_HeadAlerts_Users_TargetLawyerId",
                table: "HeadAlerts",
                column: "TargetLawyerId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_HeadSuccessions_Users_UserId",
                table: "HeadSuccessions",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ParentEditSuggestions_PublicEntities_EntryId",
                table: "ParentEditSuggestions",
                column: "EntryId",
                principalTable: "PublicEntities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ParentEditSuggestions_Users_CreatedById",
                table: "ParentEditSuggestions",
                column: "CreatedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ParentEditSuggestions_Users_ReviewedById",
                table: "ParentEditSuggestions",
                column: "ReviewedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_PersonalReminders_Users_LawyerId",
                table: "PersonalReminders",
                column: "LawyerId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PublicEntities_Users_CreatedById",
                table: "PublicEntities",
                column: "CreatedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PublicEntities_Users_ReviewedById",
                table: "PublicEntities",
                column: "ReviewedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // قيد أضافته هجرة BaseNumberRegistry تاريخيًا بـSQL خام. يبقى في
            // Baseline PostgreSQL فقط (لا في النموذج المشترك) لأن اختبارات SQLite
            // تسمح عمدًا بالملفات القديمة بلا دائرة (AllowCircuitlessLegacy).
            migrationBuilder.Sql("""
                ALTER TABLE "Documents"
                ADD CONSTRAINT "CK_Documents_FileNumberRequiresCircuit"
                CHECK ("ExecutionCircuitId" IS NOT NULL
                    OR "FileNumber" IS NULL
                    OR btrim("FileNumber") = '');
                """);

            // RF-019 (SEC-008): إلحاق-فقط على مستوى القاعدة — كائنا التدقيق.
            // مشغّلات لا يمثّلها نموذج EF؛ تُضمّ إلى Baseline صراحةً.
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION fn_prevent_audit_mutation() RETURNS trigger AS $$
                BEGIN
                    RAISE EXCEPTION 'سجل التدقيق إلحاق-فقط: ممنوع % على %', TG_OP, TG_TABLE_NAME;
                    RETURN NULL;
                END;
                $$ LANGUAGE plpgsql;

                CREATE TRIGGER trg_auditlogs_no_update BEFORE UPDATE ON "AuditLogs"
                    FOR EACH ROW EXECUTE FUNCTION fn_prevent_audit_mutation();
                CREATE TRIGGER trg_auditlogs_no_delete BEFORE DELETE ON "AuditLogs"
                    FOR EACH ROW EXECUTE FUNCTION fn_prevent_audit_mutation();
                CREATE TRIGGER trg_documentfieldchanges_no_update BEFORE UPDATE ON "DocumentFieldChanges"
                    FOR EACH ROW EXECUTE FUNCTION fn_prevent_audit_mutation();
                CREATE TRIGGER trg_documentfieldchanges_no_delete BEFORE DELETE ON "DocumentFieldChanges"
                    FOR EACH ROW EXECUTE FUNCTION fn_prevent_audit_mutation();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DocumentDelegations_Users_AssignedLawyerId",
                table: "DocumentDelegations");

            migrationBuilder.DropForeignKey(
                name: "FK_DocumentDelegations_Users_CreatedById",
                table: "DocumentDelegations");

            migrationBuilder.DropForeignKey(
                name: "FK_Documents_Users_CreatedById",
                table: "Documents");

            migrationBuilder.DropForeignKey(
                name: "FK_ExecutionCircuits_Users_CreatedById",
                table: "ExecutionCircuits");

            migrationBuilder.DropForeignKey(
                name: "FK_PublicEntities_Users_CreatedById",
                table: "PublicEntities");

            migrationBuilder.DropForeignKey(
                name: "FK_PublicEntities_Users_ReviewedById",
                table: "PublicEntities");

            migrationBuilder.DropForeignKey(
                name: "FK_DocumentDelegations_Documents_SourceDocumentId",
                table: "DocumentDelegations");

            migrationBuilder.DropTable(
                name: "AppealActions");

            migrationBuilder.DropTable(
                name: "AppealBaseNumbers");

            migrationBuilder.DropTable(
                name: "ApplicantPublicEntities");

            migrationBuilder.DropTable(
                name: "AppSuggestions");

            migrationBuilder.DropTable(
                name: "AssetOwners");

            migrationBuilder.DropTable(
                name: "CorrespondenceMessages");

            migrationBuilder.DropTable(
                name: "CorrespondenceReceipts");

            migrationBuilder.DropTable(
                name: "DelegationAssetReservations");

            migrationBuilder.DropTable(
                name: "DelegationAssets");

            migrationBuilder.DropTable(
                name: "DocumentAnnexes");

            migrationBuilder.DropTable(
                name: "DocumentAssignments");

            migrationBuilder.DropTable(
                name: "DocumentBaseNumbers");

            migrationBuilder.DropTable(
                name: "DocumentFieldChanges");

            migrationBuilder.DropTable(
                name: "DocumentInterventionHeirs");

            migrationBuilder.DropTable(
                name: "DocumentOccurrences");

            migrationBuilder.DropTable(
                name: "DocumentRegistrationDates");

            migrationBuilder.DropTable(
                name: "ExecutedHeirs");

            migrationBuilder.DropTable(
                name: "ExecutedPublicEntities");

            migrationBuilder.DropTable(
                name: "ExecutionActions");

            migrationBuilder.DropTable(
                name: "ForumMessageReads");

            migrationBuilder.DropTable(
                name: "Guarantors");

            migrationBuilder.DropTable(
                name: "HeadAlertOutboxMessages");

            migrationBuilder.DropTable(
                name: "HeadAlertRecipients");

            migrationBuilder.DropTable(
                name: "HeadSuccessions");

            migrationBuilder.DropTable(
                name: "Heirs");

            migrationBuilder.DropTable(
                name: "IdempotencyKeys");

            migrationBuilder.DropTable(
                name: "LoginAttempts");

            migrationBuilder.DropTable(
                name: "ParentEditSuggestions");

            migrationBuilder.DropTable(
                name: "PersonalReminders");

            migrationBuilder.DropTable(
                name: "PublicEntityAliases");

            migrationBuilder.DropTable(
                name: "PublicEntityChangeEvents");

            migrationBuilder.DropTable(
                name: "ReviewLetterMessages");

            migrationBuilder.DropTable(
                name: "Assets");

            migrationBuilder.DropTable(
                name: "Correspondences");

            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "DocumentInterventions");

            migrationBuilder.DropTable(
                name: "ExecutedNaturalPersons");

            migrationBuilder.DropTable(
                name: "ExecutionApplicants");

            migrationBuilder.DropTable(
                name: "ForumMessages");

            migrationBuilder.DropTable(
                name: "HeadAlerts");

            migrationBuilder.DropTable(
                name: "DocumentAppeals");

            migrationBuilder.DropTable(
                name: "ReviewLetters");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "PublicEntities");

            migrationBuilder.DropTable(
                name: "PublicEntityGroups");

            migrationBuilder.DropTable(
                name: "Documents");

            migrationBuilder.DropTable(
                name: "DocumentDelegations");

            migrationBuilder.DropTable(
                name: "ExecutionCircuits");

            migrationBuilder.DropTable(
                name: "Sections");

            migrationBuilder.DropTable(
                name: "Branches");

            migrationBuilder.Sql("""
                ALTER TABLE "Documents" DROP CONSTRAINT IF EXISTS "CK_Documents_FileNumberRequiresCircuit";
                DROP TRIGGER IF EXISTS trg_auditlogs_no_update ON "AuditLogs";
                DROP TRIGGER IF EXISTS trg_auditlogs_no_delete ON "AuditLogs";
                DROP TRIGGER IF EXISTS trg_documentfieldchanges_no_update ON "DocumentFieldChanges";
                DROP TRIGGER IF EXISTS trg_documentfieldchanges_no_delete ON "DocumentFieldChanges";
                DROP FUNCTION IF EXISTS fn_prevent_audit_mutation();
                """);
        }
    }
}
