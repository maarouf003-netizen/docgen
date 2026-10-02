START TRANSACTION;
ALTER TABLE "Documents" ADD "Version" bigint NOT NULL DEFAULT 0;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261002134010_RF010_ConcurrencyVersionPg', '10.0.10');

COMMIT;

