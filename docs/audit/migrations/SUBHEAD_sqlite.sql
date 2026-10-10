BEGIN TRANSACTION;
DROP INDEX "IX_Users_BranchId";

ALTER TABLE "Users" ADD "CreatedById" INTEGER NULL;

ALTER TABLE "Users" ADD "SectionId" INTEGER NULL;

ALTER TABLE "ReviewLetters" ADD "RecipientSectionId" INTEGER NULL;

ALTER TABLE "ExecutionCircuits" ADD "SectionId" INTEGER NULL;

ALTER TABLE "DocumentDelegations" ADD "RedirectedToSectionId" INTEGER NULL;

ALTER TABLE "DocumentDelegations" ADD "RejectReason" TEXT NULL;

ALTER TABLE "DocumentDelegations" ADD "Version" INTEGER NOT NULL DEFAULT 0;

ALTER TABLE "DocumentAppeals" ADD "ForwardReason" TEXT NULL;

ALTER TABLE "DocumentAppeals" ADD "ForwardState" TEXT NOT NULL DEFAULT 'Owned';

ALTER TABLE "DocumentAppeals" ADD "ForwardedAt" datetime2 NULL;

ALTER TABLE "DocumentAppeals" ADD "ForwardedById" INTEGER NULL;

ALTER TABLE "DocumentAppeals" ADD "Version" INTEGER NOT NULL DEFAULT 0;

ALTER TABLE "Correspondences" ADD "RecipientSectionId" INTEGER NULL;

UPDATE "DocumentAppeals" SET "Version" = 1

UPDATE "DocumentDelegations" SET "Version" = 1

CREATE TABLE "Sections" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_Sections" PRIMARY KEY AUTOINCREMENT,
    "BranchId" INTEGER NOT NULL,
    "Name" TEXT NOT NULL,
    "NameNorm" TEXT NOT NULL,
    "IsActive" INTEGER NOT NULL DEFAULT 1,
    "CreatedAt" TEXT NOT NULL,
    "UpdatedAt" TEXT NOT NULL,
    CONSTRAINT "FK_Sections_Branches_BranchId" FOREIGN KEY ("BranchId") REFERENCES "Branches" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "HeadSuccessions" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_HeadSuccessions" PRIMARY KEY AUTOINCREMENT,
    "BranchId" INTEGER NOT NULL,
    "SectionId" INTEGER NULL,
    "UserId" INTEGER NOT NULL,
    "Role" TEXT NOT NULL,
    "Event" TEXT NOT NULL,
    "At" datetime2 NOT NULL,
    "ActorName" TEXT NULL,
    "Reason" TEXT NULL,
    CONSTRAINT "CK_HeadSuccessions_Event" CHECK ("Event" IN ('appointed', 'deactivated', 'succeeded', 'circuit-transferred', 'renamed')),
    CONSTRAINT "FK_HeadSuccessions_Branches_BranchId" FOREIGN KEY ("BranchId") REFERENCES "Branches" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_HeadSuccessions_Sections_SectionId" FOREIGN KEY ("SectionId") REFERENCES "Sections" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_HeadSuccessions_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT
);

CREATE UNIQUE INDEX "IX_Users_BranchId" ON "Users" ("BranchId") WHERE "Role" = 'Head' AND "IsActive";

CREATE INDEX "IX_Users_CreatedById" ON "Users" ("CreatedById");

CREATE UNIQUE INDEX "IX_Users_SectionId" ON "Users" ("SectionId") WHERE "Role" = 'SubHead' AND "IsActive";

CREATE INDEX "IX_ReviewLetters_RecipientSectionId" ON "ReviewLetters" ("RecipientSectionId");

CREATE INDEX "IX_ExecutionCircuits_SectionId" ON "ExecutionCircuits" ("SectionId");

CREATE INDEX "IX_DocumentDelegations_RedirectedToSectionId" ON "DocumentDelegations" ("RedirectedToSectionId");

CREATE INDEX "IX_DocumentAppeals_ForwardedById" ON "DocumentAppeals" ("ForwardedById");

CREATE INDEX "IX_DocumentAppeals_ForwardState" ON "DocumentAppeals" ("ForwardState");

CREATE INDEX "IX_Correspondences_RecipientSectionId" ON "Correspondences" ("RecipientSectionId");

CREATE INDEX "IX_HeadSuccessions_At" ON "HeadSuccessions" ("At");

CREATE INDEX "IX_HeadSuccessions_BranchId" ON "HeadSuccessions" ("BranchId");

CREATE INDEX "IX_HeadSuccessions_SectionId" ON "HeadSuccessions" ("SectionId");

CREATE INDEX "IX_HeadSuccessions_UserId" ON "HeadSuccessions" ("UserId");

CREATE INDEX "IX_Sections_BranchId" ON "Sections" ("BranchId");

CREATE UNIQUE INDEX "IX_Sections_BranchId_NameNorm" ON "Sections" ("BranchId", "NameNorm");

CREATE INDEX "IX_Sections_IsActive" ON "Sections" ("IsActive");

CREATE TABLE "ef_temp_Users" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_Users" PRIMARY KEY AUTOINCREMENT,
    "BranchId" INTEGER NULL,
    "CreatedAt" TEXT NOT NULL,
    "CreatedById" INTEGER NULL,
    "Email" TEXT NULL,
    "FailedLoginCount" INTEGER NOT NULL,
    "FullName" TEXT NOT NULL,
    "IsActive" INTEGER NOT NULL,
    "LastLogin" TEXT NULL,
    "LockoutEndUtc" TEXT NULL,
    "PasswordHash" TEXT NOT NULL,
    "PortalEntryId" INTEGER NULL,
    "PortalGroupId" INTEGER NULL,
    "Role" TEXT NOT NULL,
    "SectionId" INTEGER NULL,
    "TokenVersion" INTEGER NOT NULL,
    "UpdatedAt" TEXT NOT NULL,
    "Username" TEXT NOT NULL,
    CONSTRAINT "CK_Users_BranchRequiredForBranchRoles" CHECK ("BranchId" IS NOT NULL OR "Role" NOT IN ('Lawyer', 'Head', 'SubHead')),
    CONSTRAINT "FK_Users_Branches_BranchId" FOREIGN KEY ("BranchId") REFERENCES "Branches" ("Id"),
    CONSTRAINT "FK_Users_PublicEntities_PortalEntryId" FOREIGN KEY ("PortalEntryId") REFERENCES "PublicEntities" ("Id") ON DELETE SET NULL,
    CONSTRAINT "FK_Users_PublicEntityGroups_PortalGroupId" FOREIGN KEY ("PortalGroupId") REFERENCES "PublicEntityGroups" ("Id") ON DELETE SET NULL,
    CONSTRAINT "FK_Users_Sections_SectionId" FOREIGN KEY ("SectionId") REFERENCES "Sections" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_Users_Users_CreatedById" FOREIGN KEY ("CreatedById") REFERENCES "Users" ("Id") ON DELETE SET NULL
);

INSERT INTO "ef_temp_Users" ("Id", "BranchId", "CreatedAt", "CreatedById", "Email", "FailedLoginCount", "FullName", "IsActive", "LastLogin", "LockoutEndUtc", "PasswordHash", "PortalEntryId", "PortalGroupId", "Role", "SectionId", "TokenVersion", "UpdatedAt", "Username")
SELECT "Id", "BranchId", "CreatedAt", "CreatedById", "Email", "FailedLoginCount", "FullName", "IsActive", "LastLogin", "LockoutEndUtc", "PasswordHash", "PortalEntryId", "PortalGroupId", "Role", "SectionId", "TokenVersion", "UpdatedAt", "Username"
FROM "Users";

CREATE TABLE "ef_temp_DocumentAppeals" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_DocumentAppeals" PRIMARY KEY AUTOINCREMENT,
    "AppealBaseNumber" TEXT NULL,
    "AppealTypeLabel" TEXT NULL,
    "AppealYear" TEXT NULL,
    "AppealedDecisionDate" datetime2 NULL,
    "AppealedDecisionSummary" TEXT NULL,
    "AppealedDecisionText" TEXT NULL,
    "AppellantsJson" text NOT NULL,
    "AppellateCourt" TEXT NULL,
    "AppelleesJson" text NOT NULL,
    "AssignedAt" TEXT NULL,
    "AssignedLawyerId" INTEGER NULL,
    "CreatedAt" TEXT NOT NULL,
    "CreatedById" INTEGER NOT NULL,
    "DecisionDate" datetime2 NULL,
    "DecisionNumber" TEXT NULL,
    "DecisionRuling" TEXT NULL,
    "DefenseOpinion" TEXT NULL,
    "DepositBookDate" datetime2 NULL,
    "DepositBookNumber" TEXT NULL,
    "Direction" TEXT NOT NULL,
    "DocumentId" INTEGER NOT NULL,
    "ForwardReason" TEXT NULL,
    "ForwardState" TEXT NOT NULL DEFAULT 'Owned',
    "ForwardedAt" datetime2 NULL,
    "ForwardedById" INTEGER NULL,
    "GroundsSummary" TEXT NULL,
    "InspectionBookDate" datetime2 NULL,
    "InspectionBookNumber" TEXT NULL,
    "Notes" TEXT NULL,
    "NoticeDate" datetime2 NULL,
    "NoticeNumber" TEXT NULL,
    "Outcome" TEXT NULL,
    "RegistrationDate" datetime2 NULL,
    "Status" TEXT NOT NULL,
    "StruckOffDate" datetime2 NULL,
    "StruckOffDecisionNumber" TEXT NULL,
    "UpdatedAt" TEXT NOT NULL,
    "Version" INTEGER NOT NULL,
    CONSTRAINT "CK_DocumentAppeals_ForwardState" CHECK ("ForwardState" IN ('Owned', 'ForwardedToHead')),
    CONSTRAINT "CK_DocumentAppeals_Status" CHECK ("Status" IN ('pending', 'decided', 'struck-off')),
    CONSTRAINT "FK_DocumentAppeals_Documents_DocumentId" FOREIGN KEY ("DocumentId") REFERENCES "Documents" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_DocumentAppeals_Users_AssignedLawyerId" FOREIGN KEY ("AssignedLawyerId") REFERENCES "Users" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_DocumentAppeals_Users_CreatedById" FOREIGN KEY ("CreatedById") REFERENCES "Users" ("Id") ON DELETE RESTRICT
);

INSERT INTO "ef_temp_DocumentAppeals" ("Id", "AppealBaseNumber", "AppealTypeLabel", "AppealYear", "AppealedDecisionDate", "AppealedDecisionSummary", "AppealedDecisionText", "AppellantsJson", "AppellateCourt", "AppelleesJson", "AssignedAt", "AssignedLawyerId", "CreatedAt", "CreatedById", "DecisionDate", "DecisionNumber", "DecisionRuling", "DefenseOpinion", "DepositBookDate", "DepositBookNumber", "Direction", "DocumentId", "ForwardReason", "ForwardState", "ForwardedAt", "ForwardedById", "GroundsSummary", "InspectionBookDate", "InspectionBookNumber", "Notes", "NoticeDate", "NoticeNumber", "Outcome", "RegistrationDate", "Status", "StruckOffDate", "StruckOffDecisionNumber", "UpdatedAt", "Version")
SELECT "Id", "AppealBaseNumber", "AppealTypeLabel", "AppealYear", "AppealedDecisionDate", "AppealedDecisionSummary", "AppealedDecisionText", "AppellantsJson", "AppellateCourt", "AppelleesJson", "AssignedAt", "AssignedLawyerId", "CreatedAt", "CreatedById", "DecisionDate", "DecisionNumber", "DecisionRuling", "DefenseOpinion", "DepositBookDate", "DepositBookNumber", "Direction", "DocumentId", "ForwardReason", "ForwardState", "ForwardedAt", "ForwardedById", "GroundsSummary", "InspectionBookDate", "InspectionBookNumber", "Notes", "NoticeDate", "NoticeNumber", "Outcome", "RegistrationDate", "Status", "StruckOffDate", "StruckOffDecisionNumber", "UpdatedAt", "Version"
FROM "DocumentAppeals";

CREATE TABLE "ef_temp_Correspondences" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_Correspondences" PRIMARY KEY AUTOINCREMENT,
    "BranchId" INTEGER NULL,
    "CorrespondenceDate" datetime2 NOT NULL,
    "CorrespondenceNumber" TEXT NOT NULL,
    "CreatedAt" TEXT NOT NULL,
    "CreatedById" INTEGER NOT NULL,
    "DocumentId" INTEGER NULL,
    "Governorate" TEXT NOT NULL,
    "Importance" TEXT NOT NULL,
    "RecipientSectionId" INTEGER NULL,
    "TargetUserId" INTEGER NOT NULL,
    "UpdatedAt" TEXT NOT NULL,
    CONSTRAINT "FK_Correspondences_Branches_BranchId" FOREIGN KEY ("BranchId") REFERENCES "Branches" ("Id") ON DELETE SET NULL,
    CONSTRAINT "FK_Correspondences_Documents_DocumentId" FOREIGN KEY ("DocumentId") REFERENCES "Documents" ("Id") ON DELETE SET NULL,
    CONSTRAINT "FK_Correspondences_Sections_RecipientSectionId" FOREIGN KEY ("RecipientSectionId") REFERENCES "Sections" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_Correspondences_Users_CreatedById" FOREIGN KEY ("CreatedById") REFERENCES "Users" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_Correspondences_Users_TargetUserId" FOREIGN KEY ("TargetUserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT
);

INSERT INTO "ef_temp_Correspondences" ("Id", "BranchId", "CorrespondenceDate", "CorrespondenceNumber", "CreatedAt", "CreatedById", "DocumentId", "Governorate", "Importance", "RecipientSectionId", "TargetUserId", "UpdatedAt")
SELECT "Id", "BranchId", "CorrespondenceDate", "CorrespondenceNumber", "CreatedAt", "CreatedById", "DocumentId", "Governorate", "Importance", "RecipientSectionId", "TargetUserId", "UpdatedAt"
FROM "Correspondences";

CREATE TABLE "ef_temp_DocumentDelegations" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_DocumentDelegations" PRIMARY KEY AUTOINCREMENT,
    "AssignedLawyerId" INTEGER NULL,
    "CreatedAt" TEXT NOT NULL,
    "CreatedById" INTEGER NOT NULL,
    "DelegatedCircuitId" INTEGER NULL,
    "DelegatedCourt" TEXT NULL,
    "DelegatedCourtNorm" TEXT NULL,
    "DelegationDate" datetime2 NULL,
    "DelegationText" TEXT NULL,
    "DepositBookDate" datetime2 NULL,
    "DepositBookNumber" TEXT NULL,
    "ExternalBranchId" INTEGER NULL,
    "IsExternal" INTEGER NOT NULL,
    "RedirectedToSectionId" INTEGER NULL,
    "RejectReason" TEXT NULL,
    "ReturnDate" datetime2 NULL,
    "SaleCoversFullDebt" INTEGER NULL,
    "SourceDocumentId" INTEGER NOT NULL,
    "Status" TEXT NOT NULL,
    "UpdatedAt" TEXT NOT NULL,
    "Version" INTEGER NOT NULL,
    CONSTRAINT "CK_DocumentDelegations_Status" CHECK ("Status" IN ('بانتظار رئيس القسم', 'محالة', 'مسجلة أصولًا', 'منفذ إنابة')),
    CONSTRAINT "FK_DocumentDelegations_Branches_ExternalBranchId" FOREIGN KEY ("ExternalBranchId") REFERENCES "Branches" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_DocumentDelegations_Documents_SourceDocumentId" FOREIGN KEY ("SourceDocumentId") REFERENCES "Documents" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_DocumentDelegations_ExecutionCircuits_DelegatedCircuitId" FOREIGN KEY ("DelegatedCircuitId") REFERENCES "ExecutionCircuits" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_DocumentDelegations_Sections_RedirectedToSectionId" FOREIGN KEY ("RedirectedToSectionId") REFERENCES "Sections" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_DocumentDelegations_Users_AssignedLawyerId" FOREIGN KEY ("AssignedLawyerId") REFERENCES "Users" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_DocumentDelegations_Users_CreatedById" FOREIGN KEY ("CreatedById") REFERENCES "Users" ("Id") ON DELETE RESTRICT
);

INSERT INTO "ef_temp_DocumentDelegations" ("Id", "AssignedLawyerId", "CreatedAt", "CreatedById", "DelegatedCircuitId", "DelegatedCourt", "DelegatedCourtNorm", "DelegationDate", "DelegationText", "DepositBookDate", "DepositBookNumber", "ExternalBranchId", "IsExternal", "RedirectedToSectionId", "RejectReason", "ReturnDate", "SaleCoversFullDebt", "SourceDocumentId", "Status", "UpdatedAt", "Version")
SELECT "Id", "AssignedLawyerId", "CreatedAt", "CreatedById", "DelegatedCircuitId", "DelegatedCourt", "DelegatedCourtNorm", "DelegationDate", "DelegationText", "DepositBookDate", "DepositBookNumber", "ExternalBranchId", "IsExternal", "RedirectedToSectionId", "RejectReason", "ReturnDate", "SaleCoversFullDebt", "SourceDocumentId", "Status", "UpdatedAt", "Version"
FROM "DocumentDelegations";

CREATE TABLE "ef_temp_ExecutionCircuits" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_ExecutionCircuits" PRIMARY KEY AUTOINCREMENT,
    "BranchId" INTEGER NOT NULL,
    "CreatedAt" TEXT NOT NULL,
    "CreatedById" INTEGER NOT NULL,
    "IsActive" INTEGER NOT NULL DEFAULT 1,
    "Name" TEXT NOT NULL,
    "NameNorm" TEXT NOT NULL,
    "SectionId" INTEGER NULL,
    "UpdatedAt" TEXT NOT NULL,
    "Version" INTEGER NOT NULL,
    CONSTRAINT "FK_ExecutionCircuits_Branches_BranchId" FOREIGN KEY ("BranchId") REFERENCES "Branches" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_ExecutionCircuits_Sections_SectionId" FOREIGN KEY ("SectionId") REFERENCES "Sections" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_ExecutionCircuits_Users_CreatedById" FOREIGN KEY ("CreatedById") REFERENCES "Users" ("Id") ON DELETE RESTRICT
);

INSERT INTO "ef_temp_ExecutionCircuits" ("Id", "BranchId", "CreatedAt", "CreatedById", "IsActive", "Name", "NameNorm", "SectionId", "UpdatedAt", "Version")
SELECT "Id", "BranchId", "CreatedAt", "CreatedById", "IsActive", "Name", "NameNorm", "SectionId", "UpdatedAt", "Version"
FROM "ExecutionCircuits";

CREATE TABLE "ef_temp_ReviewLetters" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_ReviewLetters" PRIMARY KEY AUTOINCREMENT,
    "BranchId" INTEGER NOT NULL,
    "CreatedAt" TEXT NOT NULL,
    "CreatedById" INTEGER NOT NULL,
    "DocumentId" INTEGER NULL,
    "IsAnswered" INTEGER NOT NULL,
    "LetterDate" datetime2 NOT NULL,
    "LetterNumber" TEXT NOT NULL,
    "RecipientSectionId" INTEGER NULL,
    "UpdatedAt" TEXT NOT NULL,
    CONSTRAINT "FK_ReviewLetters_Branches_BranchId" FOREIGN KEY ("BranchId") REFERENCES "Branches" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_ReviewLetters_Documents_DocumentId" FOREIGN KEY ("DocumentId") REFERENCES "Documents" ("Id") ON DELETE SET NULL,
    CONSTRAINT "FK_ReviewLetters_Sections_RecipientSectionId" FOREIGN KEY ("RecipientSectionId") REFERENCES "Sections" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_ReviewLetters_Users_CreatedById" FOREIGN KEY ("CreatedById") REFERENCES "Users" ("Id") ON DELETE RESTRICT
);

INSERT INTO "ef_temp_ReviewLetters" ("Id", "BranchId", "CreatedAt", "CreatedById", "DocumentId", "IsAnswered", "LetterDate", "LetterNumber", "RecipientSectionId", "UpdatedAt")
SELECT "Id", "BranchId", "CreatedAt", "CreatedById", "DocumentId", "IsAnswered", "LetterDate", "LetterNumber", "RecipientSectionId", "UpdatedAt"
FROM "ReviewLetters";

COMMIT;

PRAGMA foreign_keys = 0;

BEGIN TRANSACTION;
DROP TABLE "Users";

ALTER TABLE "ef_temp_Users" RENAME TO "Users";

DROP TABLE "DocumentAppeals";

ALTER TABLE "ef_temp_DocumentAppeals" RENAME TO "DocumentAppeals";

DROP TABLE "Correspondences";

ALTER TABLE "ef_temp_Correspondences" RENAME TO "Correspondences";

DROP TABLE "DocumentDelegations";

ALTER TABLE "ef_temp_DocumentDelegations" RENAME TO "DocumentDelegations";

DROP TABLE "ExecutionCircuits";

ALTER TABLE "ef_temp_ExecutionCircuits" RENAME TO "ExecutionCircuits";

DROP TABLE "ReviewLetters";

ALTER TABLE "ef_temp_ReviewLetters" RENAME TO "ReviewLetters";

COMMIT;

PRAGMA foreign_keys = 1;

BEGIN TRANSACTION;
CREATE UNIQUE INDEX "IX_Users_BranchId" ON "Users" ("BranchId") WHERE "Role" = 'Head' AND "IsActive";

CREATE INDEX "IX_Users_CreatedById" ON "Users" ("CreatedById");

CREATE INDEX "IX_Users_PortalEntryId" ON "Users" ("PortalEntryId");

CREATE INDEX "IX_Users_PortalGroupId" ON "Users" ("PortalGroupId");

CREATE UNIQUE INDEX "IX_Users_SectionId" ON "Users" ("SectionId") WHERE "Role" = 'SubHead' AND "IsActive";

CREATE UNIQUE INDEX "IX_Users_Username" ON "Users" ("Username") WHERE "BranchId" IS NULL;

CREATE UNIQUE INDEX "IX_Users_Username_BranchId" ON "Users" ("Username", "BranchId");

CREATE INDEX "IX_DocumentAppeals_AssignedLawyerId" ON "DocumentAppeals" ("AssignedLawyerId");

CREATE INDEX "IX_DocumentAppeals_CreatedAt" ON "DocumentAppeals" ("CreatedAt");

CREATE INDEX "IX_DocumentAppeals_CreatedById" ON "DocumentAppeals" ("CreatedById");

CREATE INDEX "IX_DocumentAppeals_Direction" ON "DocumentAppeals" ("Direction");

CREATE INDEX "IX_DocumentAppeals_DocumentId" ON "DocumentAppeals" ("DocumentId");

CREATE INDEX "IX_DocumentAppeals_ForwardedById" ON "DocumentAppeals" ("ForwardedById");

CREATE INDEX "IX_DocumentAppeals_ForwardState" ON "DocumentAppeals" ("ForwardState");

CREATE INDEX "IX_DocumentAppeals_Status" ON "DocumentAppeals" ("Status");

CREATE INDEX "IX_Correspondences_BranchId" ON "Correspondences" ("BranchId");

CREATE UNIQUE INDEX "IX_Correspondences_CorrespondenceNumber" ON "Correspondences" ("CorrespondenceNumber");

CREATE INDEX "IX_Correspondences_CreatedById" ON "Correspondences" ("CreatedById");

CREATE INDEX "IX_Correspondences_DocumentId" ON "Correspondences" ("DocumentId");

CREATE INDEX "IX_Correspondences_Governorate" ON "Correspondences" ("Governorate");

CREATE INDEX "IX_Correspondences_Importance" ON "Correspondences" ("Importance");

CREATE INDEX "IX_Correspondences_RecipientSectionId" ON "Correspondences" ("RecipientSectionId");

CREATE INDEX "IX_Correspondences_TargetUserId" ON "Correspondences" ("TargetUserId");

CREATE INDEX "IX_Correspondences_UpdatedAt" ON "Correspondences" ("UpdatedAt");

CREATE INDEX "IX_DocumentDelegations_AssignedLawyerId" ON "DocumentDelegations" ("AssignedLawyerId");

CREATE INDEX "IX_DocumentDelegations_CreatedById" ON "DocumentDelegations" ("CreatedById");

CREATE INDEX "IX_DocumentDelegations_DelegatedCircuitId" ON "DocumentDelegations" ("DelegatedCircuitId");

CREATE INDEX "IX_DocumentDelegations_ExternalBranchId" ON "DocumentDelegations" ("ExternalBranchId");

CREATE INDEX "IX_DocumentDelegations_RedirectedToSectionId" ON "DocumentDelegations" ("RedirectedToSectionId");

CREATE INDEX "IX_DocumentDelegations_SourceDocumentId" ON "DocumentDelegations" ("SourceDocumentId");

CREATE INDEX "IX_DocumentDelegations_Status" ON "DocumentDelegations" ("Status");

CREATE INDEX "IX_ExecutionCircuits_BranchId" ON "ExecutionCircuits" ("BranchId");

CREATE UNIQUE INDEX "IX_ExecutionCircuits_BranchId_NameNorm" ON "ExecutionCircuits" ("BranchId", "NameNorm");

CREATE INDEX "IX_ExecutionCircuits_CreatedById" ON "ExecutionCircuits" ("CreatedById");

CREATE INDEX "IX_ExecutionCircuits_IsActive" ON "ExecutionCircuits" ("IsActive");

CREATE INDEX "IX_ExecutionCircuits_SectionId" ON "ExecutionCircuits" ("SectionId");

CREATE INDEX "IX_ReviewLetters_BranchId" ON "ReviewLetters" ("BranchId");

CREATE INDEX "IX_ReviewLetters_CreatedById" ON "ReviewLetters" ("CreatedById");

CREATE INDEX "IX_ReviewLetters_DocumentId" ON "ReviewLetters" ("DocumentId");

CREATE INDEX "IX_ReviewLetters_IsAnswered" ON "ReviewLetters" ("IsAnswered");

CREATE UNIQUE INDEX "IX_ReviewLetters_LetterNumber" ON "ReviewLetters" ("LetterNumber");

CREATE INDEX "IX_ReviewLetters_RecipientSectionId" ON "ReviewLetters" ("RecipientSectionId");

CREATE INDEX "IX_ReviewLetters_UpdatedAt" ON "ReviewLetters" ("UpdatedAt");

COMMIT;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261006084513_AddSubHeadSections', '10.0.10');

