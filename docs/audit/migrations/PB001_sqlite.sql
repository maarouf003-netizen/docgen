BEGIN TRANSACTION;
ALTER TABLE "PublicEntityGroups" ADD "CanonicalNameNorm" TEXT NULL;

ALTER TABLE "Documents" ADD "CourtNorm" TEXT NULL;

ALTER TABLE "DocumentDelegations" ADD "DelegatedCourtNorm" TEXT NULL;

CREATE UNIQUE INDEX "IX_PublicEntityGroups_CanonicalNameNorm" ON "PublicEntityGroups" ("CanonicalNameNorm");

CREATE INDEX "IX_Documents_CourtNorm" ON "Documents" ("CourtNorm");

CREATE UNIQUE INDEX "IX_Documents_CourtNorm_FileNumber_FileType_FileYear" ON "Documents" ("CourtNorm", "FileNumber", "FileType", "FileYear") WHERE NOT "IsDeleted" AND "FileNumber" IS NOT NULL AND "CourtNorm" IS NOT NULL;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261004061212_PB001_Normalization', '10.0.10');

COMMIT;

