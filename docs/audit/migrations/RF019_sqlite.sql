BEGIN TRANSACTION;
CREATE TRIGGER TRG_AuditLogs_NoUpdate BEFORE UPDATE ON AuditLogs BEGIN SELECT RAISE(ABORT, 'سجل التدقيق إلحاق-فقط: ممنوع التعديل'); END;

CREATE TRIGGER TRG_AuditLogs_NoDelete BEFORE DELETE ON AuditLogs BEGIN SELECT RAISE(ABORT, 'سجل التدقيق إلحاق-فقط: ممنوع الحذف'); END;

CREATE TRIGGER TRG_DocumentFieldChanges_NoUpdate BEFORE UPDATE ON DocumentFieldChanges BEGIN SELECT RAISE(ABORT, 'سجل التدقيق إلحاق-فقط: ممنوع التعديل'); END;

CREATE TRIGGER TRG_DocumentFieldChanges_NoDelete BEFORE DELETE ON DocumentFieldChanges BEGIN SELECT RAISE(ABORT, 'سجل التدقيق إلحاق-فقط: ممنوع الحذف'); END;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261002162056_RF019_AuditAppendOnly', '10.0.10');

COMMIT;

