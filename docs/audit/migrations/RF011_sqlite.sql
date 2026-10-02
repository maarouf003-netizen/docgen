BEGIN TRANSACTION;
CREATE TABLE "IdempotencyKeys" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_IdempotencyKeys" PRIMARY KEY AUTOINCREMENT,
    "Key" TEXT NOT NULL,
    "Operation" TEXT NOT NULL,
    "UserId" INTEGER NOT NULL,
    "Fingerprint" TEXT NOT NULL,
    "ResponseBody" text NULL,
    "CreatedAt" datetime2 NOT NULL,
    "ExpiresAt" datetime2 NOT NULL
);

CREATE INDEX "IX_IdempotencyKeys_ExpiresAt" ON "IdempotencyKeys" ("ExpiresAt");

CREATE UNIQUE INDEX "IX_IdempotencyKeys_Key_Operation_UserId" ON "IdempotencyKeys" ("Key", "Operation", "UserId");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261002171957_RF011_IdempotencyKeys', '10.0.10');

COMMIT;

