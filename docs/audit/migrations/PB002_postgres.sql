START TRANSACTION;
ALTER TABLE "Documents" ADD CONSTRAINT "CK_Documents_ExecStatus" CHECK ("ExecStatus" IN ('', 'منفذ جبريا', 'منفذ بالتسوية', 'تريث', 'منفذ إنابة', 'مسترد', 'محال الى البداية', 'مشطوب'));

ALTER TABLE "Documents" ADD CONSTRAINT "CK_Documents_ExecSubStatus" CHECK ("ExecSubStatus" IN ('منفذ جزئيا', 'منفذ كاملا'));

ALTER TABLE "Documents" ADD CONSTRAINT "CK_Documents_ExecutedStatus" CHECK ("ExecutedStatus" IN ('', 'منفذ', 'مشطوب'));

ALTER TABLE "Documents" ADD CONSTRAINT "CK_Documents_GeneralEntitySide" CHECK ("GeneralEntitySide" IN ('applicant', 'executed', 'deposit'));

ALTER TABLE "DocumentDelegations" ADD CONSTRAINT "CK_DocumentDelegations_Status" CHECK ("Status" IN ('بانتظار رئيس القسم', 'محالة', 'مسجلة أصولًا', 'منفذ إنابة'));

ALTER TABLE "DocumentAppeals" ADD CONSTRAINT "CK_DocumentAppeals_Status" CHECK ("Status" IN ('pending', 'decided', 'struck-off'));

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261004070818_PB002_StatusChecksPg', '10.0.10');

COMMIT;

