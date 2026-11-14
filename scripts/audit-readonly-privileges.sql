-- P0-08 — أقل صلاحية لدور التطبيق على جداول التدقيق (إلحاق فقط).
--
-- لا يُشغَّل تلقائيًا ضمن الهجرات: المالك/الـDBA وحده يعرف اسم دور التطبيق في كل بيئة،
-- كما أن الجداول يجب أن تكون مملوكة لدور الهجرة لا لدور التطبيق (إلا فإن المالك يتجاوز
-- الصلاحيات؛ تبقى مشغّلات القاعدة طبقة الحماية الثانية).
--
-- الاستخدام (مرة واحدة لكل بيئة، بعد الهجرات وقبل تشغيل النسخة الجديدة):
--   psql "$DATABASE_URL" -v app_role=docgen_app -f scripts/audit-readonly-privileges.sql
--
-- تحقق بعد التنفيذ (يجب أن يكون كل سطر `false`):
--   SELECT has_table_privilege(:'app_role', 'public."AuditLogs"', 'UPDATE'),
--          has_table_privilege(:'app_role', 'public."AuditLogs"', 'DELETE'),
--          has_table_privilege(:'app_role', 'public."DocumentFieldChanges"', 'UPDATE'),
--          has_table_privilege(:'app_role', 'public."DocumentFieldChanges"', 'DELETE');

REVOKE UPDATE, DELETE, TRUNCATE ON TABLE "AuditLogs" FROM :"app_role";
REVOKE UPDATE, DELETE, TRUNCATE ON TABLE "DocumentFieldChanges" FROM :"app_role";

GRANT SELECT, INSERT ON TABLE "AuditLogs", "DocumentFieldChanges" TO :"app_role";

-- تحقق من وجود مشغّلات منع التعديل/الحذف (طبقة ثانية مستقلة عن الصلاحيات):
--   SELECT tgname FROM pg_trigger
--    WHERE tgname IN ('trg_auditlogs_no_update', 'trg_auditlogs_no_delete',
--                     'trg_documentfieldchanges_no_update', 'trg_documentfieldchanges_no_delete');
-- المتوقع: 4 صفوف.
