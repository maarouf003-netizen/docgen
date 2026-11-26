-- =============================================================================
-- P6-04 — بذر حتمي لقاعدة E2E المؤقتة (فقط بعد `dotnet ef database update`)
-- =============================================================================
-- القاعدة المستهدفة: مؤقتة، اسمها يحتوي `e2e` (مثل docgen_e2e)، ومضيفها محلي.
-- لا تُشغَّل على قاعدة إنتاج/مشتركة — حارس `frontend/scripts/e2e-safety-guard.mjs`
-- يمنع حتى بدء التشغيل في هذه الحالة.
--
-- الحسابات الأربعة وكلمة المرور الثابتة (اختبارية معلنة، غير سرّية):
--   e2e.manager  — Manager       (بلا فرع)
--   e2e.head     — Head          (فرع E2E-DAM)
--   e2e.lawyer   — Lawyer        (فرع E2E-DAM)
--   e2e.portal   — EntityManager (بلا ربط بوابة: صفحات القراءة تُرجع نطاقًا فارغًا)
--   كلمة المرور للجميع: E2eTest!Pass2026
--
-- الهاش أدناه بصيغة التطبيق `PasswordHasher`: `$docgen$v1$<iterations>$<saltB64>$<hashB64>`
-- (PBKDF2-HMAC-SHA256، 600000 تكرار، ملح 16 بايت، مفتاح 32 بايت). الملح ثابت
-- عمدًا لتكون النتيجة حتمية؛ لا يُستخدم في أي بيئة حقيقية.
--
-- التنفيذ:
--   psql -v ON_ERROR_STOP=1 -f scripts/e2e/seed.sql
-- (مضيف/مستخدم/قاعدة عبر PGHOST/PGPORT/PGUSER/PGDATABASE/PGPASSWORD).
-- السكربت idempotent: إعادة تشغيله لا تُنشئ صفوفًا مكررة.
-- =============================================================================

\set ON_ERROR_STOP on

BEGIN;

INSERT INTO "Branches" ("Name", "Code", "Address", "Phone", "Governorate", "IsActive", "CreatedAt", "Version")
SELECT
    'فرع دمشق — اختبار E2E',
    'E2E-DAM',
    'دمشق',
    NULL,
    'دمشق',
    TRUE,
    TIMESTAMPTZ '2026-01-01T00:00:00Z',
    0
WHERE NOT EXISTS (SELECT 1 FROM "Branches" WHERE "Code" = 'E2E-DAM');

-- مدير اختبار (بلا فرع — يرى كل الفروع).
INSERT INTO "Users"
    ("Username", "Email", "PasswordHash", "FullName", "Role", "BranchId", "SectionId", "CreatedById",
     "IsActive", "TokenVersion", "FailedLoginCount", "LockoutEndUtc", "CreatedAt", "UpdatedAt",
     "LastLogin", "PortalGroupId", "PortalEntryId", "Version")
SELECT
    'e2e.manager', NULL,
    '$docgen$v1$600000$ZG9jZ2VuLWUyZS1zYWx0MQ==$gpoMk5ywB6hcs+48hiJThGO+GoZGcErP1NrpZcfCqtA=',
    'مدير اختبار E2E', 'Manager', NULL, NULL, NULL,
    TRUE, 0, 0, NULL,
    TIMESTAMPTZ '2026-01-01T00:00:00Z', TIMESTAMPTZ '2026-01-01T00:00:00Z', NULL, NULL, NULL, 0
WHERE NOT EXISTS (SELECT 1 FROM "Users" WHERE "Username" = 'e2e.manager');

-- رئيس قسم في فرع الاختبار.
INSERT INTO "Users"
    ("Username", "Email", "PasswordHash", "FullName", "Role", "BranchId", "SectionId", "CreatedById",
     "IsActive", "TokenVersion", "FailedLoginCount", "LockoutEndUtc", "CreatedAt", "UpdatedAt",
     "LastLogin", "PortalGroupId", "PortalEntryId", "Version")
SELECT
    'e2e.head', NULL,
    '$docgen$v1$600000$ZG9jZ2VuLWUyZS1zYWx0MQ==$gpoMk5ywB6hcs+48hiJThGO+GoZGcErP1NrpZcfCqtA=',
    'رئيس قسم اختبار E2E', 'Head', b."Id", NULL, NULL,
    TRUE, 0, 0, NULL,
    TIMESTAMPTZ '2026-01-01T00:00:00Z', TIMESTAMPTZ '2026-01-01T00:00:00Z', NULL, NULL, NULL, 0
FROM "Branches" b
WHERE b."Code" = 'E2E-DAM'
  AND NOT EXISTS (SELECT 1 FROM "Users" WHERE "Username" = 'e2e.head');

-- محامٍ في فرع الاختبار.
INSERT INTO "Users"
    ("Username", "Email", "PasswordHash", "FullName", "Role", "BranchId", "SectionId", "CreatedById",
     "IsActive", "TokenVersion", "FailedLoginCount", "LockoutEndUtc", "CreatedAt", "UpdatedAt",
     "LastLogin", "PortalGroupId", "PortalEntryId", "Version")
SELECT
    'e2e.lawyer', NULL,
    '$docgen$v1$600000$ZG9jZ2VuLWUyZS1zYWx0MQ==$gpoMk5ywB6hcs+48hiJThGO+GoZGcErP1NrpZcfCqtA=',
    'محامي اختبار E2E', 'Lawyer', b."Id", NULL, NULL,
    TRUE, 0, 0, NULL,
    TIMESTAMPTZ '2026-01-01T00:00:00Z', TIMESTAMPTZ '2026-01-01T00:00:00Z', NULL, NULL, NULL, 0
FROM "Branches" b
WHERE b."Code" = 'E2E-DAM'
  AND NOT EXISTS (SELECT 1 FROM "Users" WHERE "Username" = 'e2e.lawyer');

-- مندوب جهة عامة بلا ربط بوابة (صفحات القراءة تعرض نطاقًا فارغًا بلا أخطاء).
INSERT INTO "Users"
    ("Username", "Email", "PasswordHash", "FullName", "Role", "BranchId", "SectionId", "CreatedById",
     "IsActive", "TokenVersion", "FailedLoginCount", "LockoutEndUtc", "CreatedAt", "UpdatedAt",
     "LastLogin", "PortalGroupId", "PortalEntryId", "Version")
SELECT
    'e2e.portal', NULL,
    '$docgen$v1$600000$ZG9jZ2VuLWUyZS1zYWx0MQ==$gpoMk5ywB6hcs+48hiJThGO+GoZGcErP1NrpZcfCqtA=',
    'مندوب جهة اختبار E2E', 'EntityManager', NULL, NULL, NULL,
    TRUE, 0, 0, NULL,
    TIMESTAMPTZ '2026-01-01T00:00:00Z', TIMESTAMPTZ '2026-01-01T00:00:00Z', NULL, NULL, NULL, 0
WHERE NOT EXISTS (SELECT 1 FROM "Users" WHERE "Username" = 'e2e.portal');

-- تحقق ختامي: يفشل البذر إن لم تكتمل الحسابات الأربعة (fail-closed بدل اختبارات غامضة).
DO $$
DECLARE
    missing integer;
BEGIN
    SELECT count(*) INTO missing
    FROM (VALUES ('e2e.manager'), ('e2e.head'), ('e2e.lawyer'), ('e2e.portal')) AS expected(username)
    WHERE NOT EXISTS (SELECT 1 FROM "Users" u WHERE u."Username" = expected.username);

    IF missing > 0 THEN
        RAISE EXCEPTION 'E2E seed incomplete: % expected user(s) missing', missing;
    END IF;
END $$;

COMMIT;
