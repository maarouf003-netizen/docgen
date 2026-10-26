BEGIN TRANSACTION;
CREATE TEMP TABLE "_HeadAlertContractGuard" (
    "NullKind" INTEGER NOT NULL
        CONSTRAINT "CK_HeadAlertContractGuard_NullKind" CHECK ("NullKind" = 0),
    "NullLastContentChangedAt" INTEGER NOT NULL
        CONSTRAINT "CK_HeadAlertContractGuard_NullLastChanged" CHECK ("NullLastContentChangedAt" = 0),
    "EmptyKind" INTEGER NOT NULL
        CONSTRAINT "CK_HeadAlertContractGuard_EmptyKind" CHECK ("EmptyKind" = 0)
);

INSERT INTO "_HeadAlertContractGuard" ("NullKind", "NullLastContentChangedAt", "EmptyKind")
SELECT COALESCE(SUM("Kind" IS NULL), 0),
       COALESCE(SUM("LastContentChangedAt" IS NULL), 0),
       COALESCE(SUM("Kind" IS NOT NULL AND TRIM("Kind") = ''), 0)
FROM "HeadAlerts";

DROP TABLE "_HeadAlertContractGuard";

CREATE TABLE "ef_temp_HeadAlerts" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_HeadAlerts" PRIMARY KEY AUTOINCREMENT,
    "AppealId" INTEGER NULL,
    "ArchivedAt" TEXT NULL,
    "BranchId" INTEGER NOT NULL,
    "CreatedAt" TEXT NOT NULL,
    "CreatedById" INTEGER NOT NULL,
    "DedupeKey" TEXT NULL,
    "DelegationId" INTEGER NULL,
    "DocumentId" INTEGER NULL,
    "ExecutionCircuitId" INTEGER NULL,
    "IsArchived" INTEGER NOT NULL DEFAULT 0,
    "Kind" TEXT NOT NULL,
    "LastContentChangedAt" TEXT NOT NULL,
    "Message" TEXT NOT NULL,
    "PublicEntityId" INTEGER NULL,
    "ReviewLetterId" INTEGER NULL,
    "TargetLawyerId" INTEGER NULL,
    "TargetType" INTEGER NOT NULL,
    CONSTRAINT "CK_HeadAlerts_Kind_NotEmpty" CHECK (TRIM("Kind") <> ''),
    CONSTRAINT "FK_HeadAlerts_Branches_BranchId" FOREIGN KEY ("BranchId") REFERENCES "Branches" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_HeadAlerts_DocumentAppeals_AppealId" FOREIGN KEY ("AppealId") REFERENCES "DocumentAppeals" ("Id") ON DELETE SET NULL,
    CONSTRAINT "FK_HeadAlerts_Documents_DocumentId" FOREIGN KEY ("DocumentId") REFERENCES "Documents" ("Id") ON DELETE SET NULL,
    CONSTRAINT "FK_HeadAlerts_ExecutionCircuits_ExecutionCircuitId" FOREIGN KEY ("ExecutionCircuitId") REFERENCES "ExecutionCircuits" ("Id") ON DELETE SET NULL,
    CONSTRAINT "FK_HeadAlerts_PublicEntities_PublicEntityId" FOREIGN KEY ("PublicEntityId") REFERENCES "PublicEntities" ("Id") ON DELETE SET NULL,
    CONSTRAINT "FK_HeadAlerts_ReviewLetters_ReviewLetterId" FOREIGN KEY ("ReviewLetterId") REFERENCES "ReviewLetters" ("Id") ON DELETE SET NULL,
    CONSTRAINT "FK_HeadAlerts_Users_CreatedById" FOREIGN KEY ("CreatedById") REFERENCES "Users" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_HeadAlerts_Users_TargetLawyerId" FOREIGN KEY ("TargetLawyerId") REFERENCES "Users" ("Id") ON DELETE RESTRICT
);

INSERT INTO "ef_temp_HeadAlerts" ("Id", "AppealId", "ArchivedAt", "BranchId", "CreatedAt", "CreatedById", "DedupeKey", "DelegationId", "DocumentId", "ExecutionCircuitId", "IsArchived", "Kind", "LastContentChangedAt", "Message", "PublicEntityId", "ReviewLetterId", "TargetLawyerId", "TargetType")
SELECT "Id", "AppealId", "ArchivedAt", "BranchId", "CreatedAt", "CreatedById", "DedupeKey", "DelegationId", "DocumentId", "ExecutionCircuitId", "IsArchived", "Kind", "LastContentChangedAt", "Message", "PublicEntityId", "ReviewLetterId", "TargetLawyerId", "TargetType"
FROM "HeadAlerts";

COMMIT;

PRAGMA foreign_keys = 0;

BEGIN TRANSACTION;
DROP TABLE "HeadAlerts";

ALTER TABLE "ef_temp_HeadAlerts" RENAME TO "HeadAlerts";

COMMIT;

PRAGMA foreign_keys = 1;

BEGIN TRANSACTION;
CREATE INDEX "IX_HeadAlerts_AppealId" ON "HeadAlerts" ("AppealId");

CREATE INDEX "IX_HeadAlerts_BranchId" ON "HeadAlerts" ("BranchId");

CREATE INDEX "IX_HeadAlerts_BranchId_CreatedById_Active_List" ON "HeadAlerts" ("BranchId", "CreatedById", "IsArchived", "LastContentChangedAt", "Id");

CREATE INDEX "IX_HeadAlerts_BranchId_IsArchived_LastContentChangedAt_Id" ON "HeadAlerts" ("BranchId", "IsArchived", "LastContentChangedAt", "Id");

CREATE INDEX "IX_HeadAlerts_CreatedAt" ON "HeadAlerts" ("CreatedAt");

CREATE INDEX "IX_HeadAlerts_CreatedById" ON "HeadAlerts" ("CreatedById");

CREATE INDEX "IX_HeadAlerts_DelegationId" ON "HeadAlerts" ("DelegationId");

CREATE INDEX "IX_HeadAlerts_DocumentId" ON "HeadAlerts" ("DocumentId");

CREATE INDEX "IX_HeadAlerts_ExecutionCircuitId_Kind_IsArchived" ON "HeadAlerts" ("ExecutionCircuitId", "Kind", "IsArchived");

CREATE INDEX "IX_HeadAlerts_IsArchived_LastContentChangedAt_Id" ON "HeadAlerts" ("IsArchived", "LastContentChangedAt", "Id");

CREATE INDEX "IX_HeadAlerts_Kind_Active_LastChanged" ON "HeadAlerts" ("Kind", "IsArchived", "LastContentChangedAt", "Id");

CREATE INDEX "IX_HeadAlerts_PublicEntityId" ON "HeadAlerts" ("PublicEntityId");

CREATE INDEX "IX_HeadAlerts_ReviewLetterId" ON "HeadAlerts" ("ReviewLetterId");

CREATE INDEX "IX_HeadAlerts_TargetLawyerId_Kind_IsArchived" ON "HeadAlerts" ("TargetLawyerId", "Kind", "IsArchived");

CREATE UNIQUE INDEX "UX_HeadAlerts_DedupeKey_Active" ON "HeadAlerts" ("DedupeKey") WHERE "DedupeKey" IS NOT NULL AND "IsArchived" = 0;

COMMIT;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261026151929_HeadAlertTaxonomy_Contract', '10.0.10');

