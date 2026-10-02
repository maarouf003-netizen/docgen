START TRANSACTION;
CREATE UNIQUE INDEX "IX_Documents_Court_FileNumber_FileType_FileYear" ON "Documents" ("Court", "FileNumber", "FileType", "FileYear") WHERE NOT "IsDeleted" AND "FileNumber" IS NOT NULL;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261002003604_RF009_NumberingUniqueIndexPg', '10.0.10');

COMMIT;

