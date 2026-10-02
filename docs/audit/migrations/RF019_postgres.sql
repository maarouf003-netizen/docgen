START TRANSACTION;
CREATE OR REPLACE FUNCTION fn_prevent_audit_mutation() RETURNS trigger AS $$ BEGIN RAISE EXCEPTION 'سجل التدقيق إلحاق-فقط: ممنوع % على %', TG_OP, TG_TABLE_NAME; RETURN NULL; END; $$ LANGUAGE plpgsql;

CREATE TRIGGER trg_auditlogs_no_update BEFORE UPDATE ON "AuditLogs" FOR EACH ROW EXECUTE FUNCTION fn_prevent_audit_mutation();

CREATE TRIGGER trg_auditlogs_no_delete BEFORE DELETE ON "AuditLogs" FOR EACH ROW EXECUTE FUNCTION fn_prevent_audit_mutation();

CREATE TRIGGER trg_documentfieldchanges_no_update BEFORE UPDATE ON "DocumentFieldChanges" FOR EACH ROW EXECUTE FUNCTION fn_prevent_audit_mutation();

CREATE TRIGGER trg_documentfieldchanges_no_delete BEFORE DELETE ON "DocumentFieldChanges" FOR EACH ROW EXECUTE FUNCTION fn_prevent_audit_mutation();

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261002162231_RF019_AuditAppendOnlyPg', '10.0.10');

COMMIT;

