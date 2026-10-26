START TRANSACTION;
DO $$
DECLARE
    null_kind bigint;
    null_last_changed bigint;
    empty_kind bigint;
BEGIN
    SELECT COUNT(*) FILTER (WHERE "Kind" IS NULL),
           COUNT(*) FILTER (WHERE "LastContentChangedAt" IS NULL),
           COUNT(*) FILTER (WHERE "Kind" IS NOT NULL AND TRIM("Kind") = '')
      INTO null_kind, null_last_changed, empty_kind
      FROM "HeadAlerts";

    IF null_kind > 0 THEN
        RAISE EXCEPTION 'HeadAlertTaxonomy_Contract_Pg: % صفًا بـ Kind = NULL — نفّذ Backfill أولًا', null_kind;
    END IF;
    IF null_last_changed > 0 THEN
        RAISE EXCEPTION 'HeadAlertTaxonomy_Contract_Pg: % صفًا بـ LastContentChangedAt = NULL — نفّذ Backfill أولًا', null_last_changed;
    END IF;
    IF empty_kind > 0 THEN
        RAISE EXCEPTION 'HeadAlertTaxonomy_Contract_Pg: % صفًا بـ Kind فارغ أو أبيض — عالجه قبل Contract', empty_kind;
    END IF;
END $$;

ALTER TABLE "HeadAlerts" ALTER COLUMN "LastContentChangedAt" SET NOT NULL;

ALTER TABLE "HeadAlerts" ALTER COLUMN "Kind" SET NOT NULL;

ALTER TABLE "HeadAlerts" ADD CONSTRAINT "CK_HeadAlerts_Kind_NotEmpty" CHECK (TRIM("Kind") <> '');

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261026152238_HeadAlertTaxonomy_Contract_Pg', '10.0.10');

COMMIT;

