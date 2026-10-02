START TRANSACTION;
ALTER TABLE "PersonalReminders" ALTER COLUMN "RecurrenceEnd" TYPE timestamp with time zone;

ALTER TABLE "PersonalReminders" ALTER COLUMN "DueDate" TYPE timestamp with time zone;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261002151805_RF013_ReminderTimestamptzPg', '10.0.10');

COMMIT;

