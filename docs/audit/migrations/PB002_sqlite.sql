BEGIN TRANSACTION;
CREATE TABLE "ef_temp_Documents" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_Documents" PRIMARY KEY AUTOINCREMENT,
    "Amount2Numeric" decimal(20,2) NOT NULL,
    "Amount2Words" TEXT NULL,
    "Amount3Numeric" decimal(20,2) NOT NULL,
    "Amount3Words" TEXT NULL,
    "AmountNumeric" decimal(20,2) NOT NULL,
    "AmountWords" TEXT NULL,
    "AnnexDate" TEXT NULL,
    "AnnexNumber" TEXT NULL,
    "AnnexType" TEXT NULL,
    "Applicant" TEXT NULL,
    "ApplicantRegistryId" INTEGER NULL,
    "BaraetDate" TEXT NULL,
    "BaraetNumber" TEXT NULL,
    "BaraetRegDate" TEXT NULL,
    "BaraetRegNumber" TEXT NULL,
    "BorrowerAddress" TEXT NULL,
    "BorrowerAddressType" TEXT NULL,
    "BorrowerBirth" TEXT NULL,
    "BorrowerFamily" TEXT NULL,
    "BorrowerFather" TEXT NULL,
    "BorrowerMother" TEXT NULL,
    "BorrowerName" TEXT NULL,
    "BorrowerNationalId" TEXT NULL,
    "BorrowerNature" TEXT NOT NULL DEFAULT 'natural',
    "BorrowerRegister" TEXT NULL,
    "BorrowerRegistrationNumber" TEXT NULL,
    "BorrowerRepresentativeAddress" TEXT NULL,
    "BorrowerRepresentativeAddressType" TEXT NULL,
    "BorrowerRepresentativeCapacity" TEXT NULL,
    "BorrowerRepresentativeFamily" TEXT NULL,
    "BorrowerRepresentativeFather" TEXT NULL,
    "BorrowerRepresentativeName" TEXT NULL,
    "BorrowerRepresentedBy" TEXT NULL,
    "BranchId" INTEGER NULL,
    "BranchName" TEXT NULL,
    "CollectedAmount" decimal(20,2) NULL,
    "CollectedAmount2" decimal(20,2) NULL,
    "CollectedAmount3" decimal(20,2) NULL,
    "CollectedCurrency" TEXT NULL,
    "CollectedCurrency2" TEXT NULL,
    "CollectedCurrency3" TEXT NULL,
    "ContractDate" TEXT NULL,
    "ContractNumber" TEXT NULL,
    "ContractType" TEXT NULL,
    "ContractTypeSelector" TEXT NULL,
    "Court" TEXT NULL,
    "CourtNorm" TEXT NULL,
    "CreatedAt" TEXT NOT NULL,
    "CreatedById" INTEGER NOT NULL,
    "Currency" TEXT NULL,
    "Currency2" TEXT NULL,
    "Currency3" TEXT NULL,
    "DeletedAt" TEXT NULL,
    "DocumentType" TEXT NULL,
    "ExecStatus" TEXT NULL,
    "ExecSubStatus" TEXT NULL,
    "ExecutedDepositDate" datetime2 NULL,
    "ExecutedDescription" TEXT NULL,
    "ExecutedExecutionDate" datetime2 NULL,
    "ExecutedPaidAmount" decimal(20,2) NULL,
    "ExecutedPaidAmount2" decimal(20,2) NULL,
    "ExecutedPaidAmount3" decimal(20,2) NULL,
    "ExecutedPaidCurrency" TEXT NULL,
    "ExecutedPaidCurrency2" TEXT NULL,
    "ExecutedPaidCurrency3" TEXT NULL,
    "ExecutedRequiredAmount" decimal(20,2) NULL,
    "ExecutedRequiredAmount2" decimal(20,2) NULL,
    "ExecutedRequiredAmount3" decimal(20,2) NULL,
    "ExecutedRequiredCurrency" TEXT NULL,
    "ExecutedRequiredCurrency2" TEXT NULL,
    "ExecutedRequiredCurrency3" TEXT NULL,
    "ExecutedStatus" TEXT NULL,
    "FileArrivalDate" TEXT NULL,
    "FileArrivalNumber" TEXT NULL,
    "FileIncoming" TEXT NULL,
    "FileIncomingDate" TEXT NULL,
    "FileNumber" TEXT NULL,
    "FilePath" TEXT NULL,
    "FileReceiptDate" datetime2 NULL,
    "FileReceiptNumber" TEXT NULL,
    "FileType" TEXT NULL,
    "FileYear" TEXT NULL,
    "ForcedExecutionDate" TEXT NULL,
    "ForcibleTransferDate" datetime2 NULL,
    "ForcibleTransferNoticeNumber" TEXT NULL,
    "FullData" text NULL,
    "GeneralEntitySide" TEXT NOT NULL,
    "ImmediateActions" TEXT NULL,
    "InclusionAmount2Numeric" decimal(20,2) NOT NULL,
    "InclusionAmount2Words" TEXT NULL,
    "InclusionAmount3Numeric" decimal(20,2) NOT NULL,
    "InclusionAmount3Words" TEXT NULL,
    "InclusionAmountNumeric" decimal(20,2) NOT NULL,
    "InclusionAmountWords" TEXT NULL,
    "InclusionCurrency" TEXT NULL,
    "InclusionCurrency2" TEXT NULL,
    "InclusionCurrency3" TEXT NULL,
    "InclusionText" TEXT NULL,
    "IsDeleted" INTEGER NOT NULL DEFAULT 0,
    "IsDraft" INTEGER NOT NULL,
    "Lawyer" TEXT NULL,
    "NoFundsDemandDate" datetime2 NULL,
    "NoFundsDemandNumber" TEXT NULL,
    "Notes" TEXT NULL,
    "PrintCount" INTEGER NOT NULL,
    "ReferredAt" datetime2 NULL,
    "ReferredFromLawyer" TEXT NULL,
    "RenewalDate" datetime2 NULL,
    "RenewalFileNumber" TEXT NULL,
    "RenewalFileReceiptDate" datetime2 NULL,
    "RenewalFileReceiptNumber" TEXT NULL,
    "RenewalFileType" TEXT NULL,
    "SayerDate" TEXT NULL,
    "SayerNumber" TEXT NULL,
    "SayerRegDate" TEXT NULL,
    "SayerRegNumber" TEXT NULL,
    "SearchText" TEXT NULL,
    "SeizureDate" TEXT NULL,
    "SoldAssetIds" text NULL,
    "SourceDelegationId" INTEGER NULL,
    "StartReferralDate" datetime2 NULL,
    "StartReferralNumber" TEXT NULL,
    "StruckOffDate" TEXT NULL,
    "TarithDate" TEXT NULL,
    "TarithNumber" TEXT NULL,
    "TarithRegDate" TEXT NULL,
    "TarithRegNumber" TEXT NULL,
    "UnderFilingNumber" TEXT NULL,
    "UpdatedAt" TEXT NOT NULL,
    "Version" INTEGER NOT NULL,
    "ViewCount" INTEGER NOT NULL,
    "WasDepositExecuted" INTEGER NOT NULL,
    CONSTRAINT "CK_Documents_ExecStatus" CHECK ("ExecStatus" IN ('', 'منفذ جبريا', 'منفذ بالتسوية', 'تريث', 'منفذ إنابة', 'مسترد', 'محال الى البداية', 'مشطوب')),
    CONSTRAINT "CK_Documents_ExecSubStatus" CHECK ("ExecSubStatus" IN ('منفذ جزئيا', 'منفذ كاملا')),
    CONSTRAINT "CK_Documents_ExecutedStatus" CHECK ("ExecutedStatus" IN ('', 'منفذ', 'مشطوب')),
    CONSTRAINT "CK_Documents_GeneralEntitySide" CHECK ("GeneralEntitySide" IN ('applicant', 'executed', 'deposit')),
    CONSTRAINT "FK_Documents_Branches_BranchId" FOREIGN KEY ("BranchId") REFERENCES "Branches" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_Documents_DocumentDelegations_SourceDelegationId" FOREIGN KEY ("SourceDelegationId") REFERENCES "DocumentDelegations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_Documents_Users_CreatedById" FOREIGN KEY ("CreatedById") REFERENCES "Users" ("Id") ON DELETE RESTRICT
);

INSERT INTO "ef_temp_Documents" ("Id", "Amount2Numeric", "Amount2Words", "Amount3Numeric", "Amount3Words", "AmountNumeric", "AmountWords", "AnnexDate", "AnnexNumber", "AnnexType", "Applicant", "ApplicantRegistryId", "BaraetDate", "BaraetNumber", "BaraetRegDate", "BaraetRegNumber", "BorrowerAddress", "BorrowerAddressType", "BorrowerBirth", "BorrowerFamily", "BorrowerFather", "BorrowerMother", "BorrowerName", "BorrowerNationalId", "BorrowerNature", "BorrowerRegister", "BorrowerRegistrationNumber", "BorrowerRepresentativeAddress", "BorrowerRepresentativeAddressType", "BorrowerRepresentativeCapacity", "BorrowerRepresentativeFamily", "BorrowerRepresentativeFather", "BorrowerRepresentativeName", "BorrowerRepresentedBy", "BranchId", "BranchName", "CollectedAmount", "CollectedAmount2", "CollectedAmount3", "CollectedCurrency", "CollectedCurrency2", "CollectedCurrency3", "ContractDate", "ContractNumber", "ContractType", "ContractTypeSelector", "Court", "CourtNorm", "CreatedAt", "CreatedById", "Currency", "Currency2", "Currency3", "DeletedAt", "DocumentType", "ExecStatus", "ExecSubStatus", "ExecutedDepositDate", "ExecutedDescription", "ExecutedExecutionDate", "ExecutedPaidAmount", "ExecutedPaidAmount2", "ExecutedPaidAmount3", "ExecutedPaidCurrency", "ExecutedPaidCurrency2", "ExecutedPaidCurrency3", "ExecutedRequiredAmount", "ExecutedRequiredAmount2", "ExecutedRequiredAmount3", "ExecutedRequiredCurrency", "ExecutedRequiredCurrency2", "ExecutedRequiredCurrency3", "ExecutedStatus", "FileArrivalDate", "FileArrivalNumber", "FileIncoming", "FileIncomingDate", "FileNumber", "FilePath", "FileReceiptDate", "FileReceiptNumber", "FileType", "FileYear", "ForcedExecutionDate", "ForcibleTransferDate", "ForcibleTransferNoticeNumber", "FullData", "GeneralEntitySide", "ImmediateActions", "InclusionAmount2Numeric", "InclusionAmount2Words", "InclusionAmount3Numeric", "InclusionAmount3Words", "InclusionAmountNumeric", "InclusionAmountWords", "InclusionCurrency", "InclusionCurrency2", "InclusionCurrency3", "InclusionText", "IsDeleted", "IsDraft", "Lawyer", "NoFundsDemandDate", "NoFundsDemandNumber", "Notes", "PrintCount", "ReferredAt", "ReferredFromLawyer", "RenewalDate", "RenewalFileNumber", "RenewalFileReceiptDate", "RenewalFileReceiptNumber", "RenewalFileType", "SayerDate", "SayerNumber", "SayerRegDate", "SayerRegNumber", "SearchText", "SeizureDate", "SoldAssetIds", "SourceDelegationId", "StartReferralDate", "StartReferralNumber", "StruckOffDate", "TarithDate", "TarithNumber", "TarithRegDate", "TarithRegNumber", "UnderFilingNumber", "UpdatedAt", "Version", "ViewCount", "WasDepositExecuted")
SELECT "Id", "Amount2Numeric", "Amount2Words", "Amount3Numeric", "Amount3Words", "AmountNumeric", "AmountWords", "AnnexDate", "AnnexNumber", "AnnexType", "Applicant", "ApplicantRegistryId", "BaraetDate", "BaraetNumber", "BaraetRegDate", "BaraetRegNumber", "BorrowerAddress", "BorrowerAddressType", "BorrowerBirth", "BorrowerFamily", "BorrowerFather", "BorrowerMother", "BorrowerName", "BorrowerNationalId", "BorrowerNature", "BorrowerRegister", "BorrowerRegistrationNumber", "BorrowerRepresentativeAddress", "BorrowerRepresentativeAddressType", "BorrowerRepresentativeCapacity", "BorrowerRepresentativeFamily", "BorrowerRepresentativeFather", "BorrowerRepresentativeName", "BorrowerRepresentedBy", "BranchId", "BranchName", "CollectedAmount", "CollectedAmount2", "CollectedAmount3", "CollectedCurrency", "CollectedCurrency2", "CollectedCurrency3", "ContractDate", "ContractNumber", "ContractType", "ContractTypeSelector", "Court", "CourtNorm", "CreatedAt", "CreatedById", "Currency", "Currency2", "Currency3", "DeletedAt", "DocumentType", "ExecStatus", "ExecSubStatus", "ExecutedDepositDate", "ExecutedDescription", "ExecutedExecutionDate", "ExecutedPaidAmount", "ExecutedPaidAmount2", "ExecutedPaidAmount3", "ExecutedPaidCurrency", "ExecutedPaidCurrency2", "ExecutedPaidCurrency3", "ExecutedRequiredAmount", "ExecutedRequiredAmount2", "ExecutedRequiredAmount3", "ExecutedRequiredCurrency", "ExecutedRequiredCurrency2", "ExecutedRequiredCurrency3", "ExecutedStatus", "FileArrivalDate", "FileArrivalNumber", "FileIncoming", "FileIncomingDate", "FileNumber", "FilePath", "FileReceiptDate", "FileReceiptNumber", "FileType", "FileYear", "ForcedExecutionDate", "ForcibleTransferDate", "ForcibleTransferNoticeNumber", "FullData", "GeneralEntitySide", "ImmediateActions", "InclusionAmount2Numeric", "InclusionAmount2Words", "InclusionAmount3Numeric", "InclusionAmount3Words", "InclusionAmountNumeric", "InclusionAmountWords", "InclusionCurrency", "InclusionCurrency2", "InclusionCurrency3", "InclusionText", "IsDeleted", "IsDraft", "Lawyer", "NoFundsDemandDate", "NoFundsDemandNumber", "Notes", "PrintCount", "ReferredAt", "ReferredFromLawyer", "RenewalDate", "RenewalFileNumber", "RenewalFileReceiptDate", "RenewalFileReceiptNumber", "RenewalFileType", "SayerDate", "SayerNumber", "SayerRegDate", "SayerRegNumber", "SearchText", "SeizureDate", "SoldAssetIds", "SourceDelegationId", "StartReferralDate", "StartReferralNumber", "StruckOffDate", "TarithDate", "TarithNumber", "TarithRegDate", "TarithRegNumber", "UnderFilingNumber", "UpdatedAt", "Version", "ViewCount", "WasDepositExecuted"
FROM "Documents";

CREATE TABLE "ef_temp_DocumentDelegations" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_DocumentDelegations" PRIMARY KEY AUTOINCREMENT,
    "AssignedLawyerId" INTEGER NULL,
    "CreatedAt" TEXT NOT NULL,
    "CreatedById" INTEGER NOT NULL,
    "DelegatedCourt" TEXT NULL,
    "DelegatedCourtNorm" TEXT NULL,
    "DelegationDate" datetime2 NULL,
    "DelegationText" TEXT NULL,
    "DepositBookDate" datetime2 NULL,
    "DepositBookNumber" TEXT NULL,
    "ExternalBranchId" INTEGER NULL,
    "IsExternal" INTEGER NOT NULL,
    "ReturnDate" datetime2 NULL,
    "SaleCoversFullDebt" INTEGER NULL,
    "SourceDocumentId" INTEGER NOT NULL,
    "Status" TEXT NOT NULL,
    "UpdatedAt" TEXT NOT NULL,
    CONSTRAINT "CK_DocumentDelegations_Status" CHECK ("Status" IN ('بانتظار رئيس القسم', 'محالة', 'مسجلة أصولًا', 'منفذ إنابة')),
    CONSTRAINT "FK_DocumentDelegations_Branches_ExternalBranchId" FOREIGN KEY ("ExternalBranchId") REFERENCES "Branches" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_DocumentDelegations_Documents_SourceDocumentId" FOREIGN KEY ("SourceDocumentId") REFERENCES "Documents" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_DocumentDelegations_Users_AssignedLawyerId" FOREIGN KEY ("AssignedLawyerId") REFERENCES "Users" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_DocumentDelegations_Users_CreatedById" FOREIGN KEY ("CreatedById") REFERENCES "Users" ("Id") ON DELETE RESTRICT
);

INSERT INTO "ef_temp_DocumentDelegations" ("Id", "AssignedLawyerId", "CreatedAt", "CreatedById", "DelegatedCourt", "DelegatedCourtNorm", "DelegationDate", "DelegationText", "DepositBookDate", "DepositBookNumber", "ExternalBranchId", "IsExternal", "ReturnDate", "SaleCoversFullDebt", "SourceDocumentId", "Status", "UpdatedAt")
SELECT "Id", "AssignedLawyerId", "CreatedAt", "CreatedById", "DelegatedCourt", "DelegatedCourtNorm", "DelegationDate", "DelegationText", "DepositBookDate", "DepositBookNumber", "ExternalBranchId", "IsExternal", "ReturnDate", "SaleCoversFullDebt", "SourceDocumentId", "Status", "UpdatedAt"
FROM "DocumentDelegations";

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
    CONSTRAINT "CK_DocumentAppeals_Status" CHECK ("Status" IN ('pending', 'decided', 'struck-off')),
    CONSTRAINT "FK_DocumentAppeals_Documents_DocumentId" FOREIGN KEY ("DocumentId") REFERENCES "Documents" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_DocumentAppeals_Users_AssignedLawyerId" FOREIGN KEY ("AssignedLawyerId") REFERENCES "Users" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_DocumentAppeals_Users_CreatedById" FOREIGN KEY ("CreatedById") REFERENCES "Users" ("Id") ON DELETE RESTRICT
);

INSERT INTO "ef_temp_DocumentAppeals" ("Id", "AppealBaseNumber", "AppealTypeLabel", "AppealYear", "AppealedDecisionDate", "AppealedDecisionSummary", "AppealedDecisionText", "AppellantsJson", "AppellateCourt", "AppelleesJson", "AssignedAt", "AssignedLawyerId", "CreatedAt", "CreatedById", "DecisionDate", "DecisionNumber", "DecisionRuling", "DefenseOpinion", "DepositBookDate", "DepositBookNumber", "Direction", "DocumentId", "GroundsSummary", "InspectionBookDate", "InspectionBookNumber", "Notes", "NoticeDate", "NoticeNumber", "Outcome", "RegistrationDate", "Status", "StruckOffDate", "StruckOffDecisionNumber", "UpdatedAt")
SELECT "Id", "AppealBaseNumber", "AppealTypeLabel", "AppealYear", "AppealedDecisionDate", "AppealedDecisionSummary", "AppealedDecisionText", "AppellantsJson", "AppellateCourt", "AppelleesJson", "AssignedAt", "AssignedLawyerId", "CreatedAt", "CreatedById", "DecisionDate", "DecisionNumber", "DecisionRuling", "DefenseOpinion", "DepositBookDate", "DepositBookNumber", "Direction", "DocumentId", "GroundsSummary", "InspectionBookDate", "InspectionBookNumber", "Notes", "NoticeDate", "NoticeNumber", "Outcome", "RegistrationDate", "Status", "StruckOffDate", "StruckOffDecisionNumber", "UpdatedAt"
FROM "DocumentAppeals";

COMMIT;

PRAGMA foreign_keys = 0;

BEGIN TRANSACTION;
DROP TABLE "Documents";

ALTER TABLE "ef_temp_Documents" RENAME TO "Documents";

DROP TABLE "DocumentDelegations";

ALTER TABLE "ef_temp_DocumentDelegations" RENAME TO "DocumentDelegations";

DROP TABLE "DocumentAppeals";

ALTER TABLE "ef_temp_DocumentAppeals" RENAME TO "DocumentAppeals";

COMMIT;

PRAGMA foreign_keys = 1;

BEGIN TRANSACTION;
CREATE INDEX "IX_Documents_ApplicantRegistryId" ON "Documents" ("ApplicantRegistryId");

CREATE INDEX "IX_Documents_BranchId" ON "Documents" ("BranchId");

CREATE UNIQUE INDEX "IX_Documents_Court_FileNumber_FileType_FileYear" ON "Documents" ("Court", "FileNumber", "FileType", "FileYear") WHERE NOT "IsDeleted" AND "FileNumber" IS NOT NULL;

CREATE INDEX "IX_Documents_CourtNorm" ON "Documents" ("CourtNorm");

CREATE UNIQUE INDEX "IX_Documents_CourtNorm_FileNumber_FileType_FileYear" ON "Documents" ("CourtNorm", "FileNumber", "FileType", "FileYear") WHERE NOT "IsDeleted" AND "FileNumber" IS NOT NULL AND "CourtNorm" IS NOT NULL;

CREATE INDEX "IX_Documents_CreatedAt" ON "Documents" ("CreatedAt");

CREATE INDEX "IX_Documents_CreatedById" ON "Documents" ("CreatedById");

CREATE INDEX "IX_Documents_DocumentType" ON "Documents" ("DocumentType");

CREATE INDEX "IX_Documents_ExecutedStatus" ON "Documents" ("ExecutedStatus");

CREATE INDEX "IX_Documents_GeneralEntitySide" ON "Documents" ("GeneralEntitySide");

CREATE INDEX "IX_Documents_SearchText" ON "Documents" ("SearchText");

CREATE UNIQUE INDEX "IX_Documents_SourceDelegationId" ON "Documents" ("SourceDelegationId");

CREATE INDEX "IX_DocumentDelegations_AssignedLawyerId" ON "DocumentDelegations" ("AssignedLawyerId");

CREATE INDEX "IX_DocumentDelegations_CreatedById" ON "DocumentDelegations" ("CreatedById");

CREATE INDEX "IX_DocumentDelegations_ExternalBranchId" ON "DocumentDelegations" ("ExternalBranchId");

CREATE INDEX "IX_DocumentDelegations_SourceDocumentId" ON "DocumentDelegations" ("SourceDocumentId");

CREATE INDEX "IX_DocumentDelegations_Status" ON "DocumentDelegations" ("Status");

CREATE INDEX "IX_DocumentAppeals_AssignedLawyerId" ON "DocumentAppeals" ("AssignedLawyerId");

CREATE INDEX "IX_DocumentAppeals_CreatedAt" ON "DocumentAppeals" ("CreatedAt");

CREATE INDEX "IX_DocumentAppeals_CreatedById" ON "DocumentAppeals" ("CreatedById");

CREATE INDEX "IX_DocumentAppeals_Direction" ON "DocumentAppeals" ("Direction");

CREATE INDEX "IX_DocumentAppeals_DocumentId" ON "DocumentAppeals" ("DocumentId");

CREATE INDEX "IX_DocumentAppeals_Status" ON "DocumentAppeals" ("Status");

COMMIT;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261004070753_PB002_StatusChecks', '10.0.10');

