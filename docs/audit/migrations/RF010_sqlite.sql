BEGIN TRANSACTION;
ALTER TABLE "Documents" ADD "Version" INTEGER NOT NULL DEFAULT 0;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261002133853_RF010_ConcurrencyVersion', '10.0.10');

COMMIT;

