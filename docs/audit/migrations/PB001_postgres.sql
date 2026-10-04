START TRANSACTION;
ALTER TABLE "PublicEntityGroups" ADD "CanonicalNameNorm" character varying(200);

ALTER TABLE "Documents" ADD "CourtNorm" character varying(200);

ALTER TABLE "DocumentDelegations" ADD "DelegatedCourtNorm" character varying(300);

CREATE UNIQUE INDEX "IX_PublicEntityGroups_CanonicalNameNorm" ON "PublicEntityGroups" ("CanonicalNameNorm");

CREATE INDEX "IX_Documents_CourtNorm" ON "Documents" ("CourtNorm");

CREATE UNIQUE INDEX "IX_Documents_CourtNorm_FileNumber_FileType_FileYear" ON "Documents" ("CourtNorm", "FileNumber", "FileType", "FileYear") WHERE NOT "IsDeleted" AND "FileNumber" IS NOT NULL AND "CourtNorm" IS NOT NULL;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261004061229_PB001_NormalizationPg', '10.0.10');

COMMIT;

