-- P0-07b — فحص مسبق للقيود المؤجلة (قراءة فقط، مخرجات مجمّعة بلا PII).
--
-- الاستخدام على قاعدة الإنتاج أو نسخة منها (دور قراءة فقط مفضّل):
--   psql "$DATABASE_URL" -f scripts/p0-07b-preflight.sql
--
-- النواتج أعداد فقط: لا أسماء ولا أرقام هوية ولا عناوين. أرسل جدول النتائج كما هو.

\set ON_ERROR_STOP on

BEGIN TRANSACTION READ ONLY;
SET LOCAL statement_timeout = '60s';

-- 1) المبالغ السالبة لكل عمود (المؤجل: CHECK (col >= 0)).
SELECT 'Documents.AmountNumeric' AS check_name, COUNT(*) AS violations FROM "Documents" WHERE "AmountNumeric" < 0
UNION ALL SELECT 'Documents.Amount2Numeric', COUNT(*) FROM "Documents" WHERE "Amount2Numeric" < 0
UNION ALL SELECT 'Documents.Amount3Numeric', COUNT(*) FROM "Documents" WHERE "Amount3Numeric" < 0
UNION ALL SELECT 'Documents.InclusionAmountNumeric', COUNT(*) FROM "Documents" WHERE "InclusionAmountNumeric" < 0
UNION ALL SELECT 'Documents.InclusionAmount2Numeric', COUNT(*) FROM "Documents" WHERE "InclusionAmount2Numeric" < 0
UNION ALL SELECT 'Documents.InclusionAmount3Numeric', COUNT(*) FROM "Documents" WHERE "InclusionAmount3Numeric" < 0
UNION ALL SELECT 'Documents.CollectedAmount', COUNT(*) FROM "Documents" WHERE "CollectedAmount" < 0
UNION ALL SELECT 'Documents.CollectedAmount2', COUNT(*) FROM "Documents" WHERE "CollectedAmount2" < 0
UNION ALL SELECT 'Documents.CollectedAmount3', COUNT(*) FROM "Documents" WHERE "CollectedAmount3" < 0
UNION ALL SELECT 'Documents.ExecutedRequiredAmount', COUNT(*) FROM "Documents" WHERE "ExecutedRequiredAmount" < 0
UNION ALL SELECT 'Documents.ExecutedRequiredAmount2', COUNT(*) FROM "Documents" WHERE "ExecutedRequiredAmount2" < 0
UNION ALL SELECT 'Documents.ExecutedRequiredAmount3', COUNT(*) FROM "Documents" WHERE "ExecutedRequiredAmount3" < 0
UNION ALL SELECT 'Documents.ExecutedPaidAmount', COUNT(*) FROM "Documents" WHERE "ExecutedPaidAmount" < 0
UNION ALL SELECT 'Documents.ExecutedPaidAmount2', COUNT(*) FROM "Documents" WHERE "ExecutedPaidAmount2" < 0
UNION ALL SELECT 'Documents.ExecutedPaidAmount3', COUNT(*) FROM "Documents" WHERE "ExecutedPaidAmount3" < 0
ORDER BY check_name;

-- 2) تكرارات رقم الأساس الفعّالة — المتوقع صفر لأن الفهرس الفريد الجزئي
--    UX_DocumentBaseNumbers_Active يحرسها منذ الـBaseline (تحقق لا إصلاح).
SELECT 'DocumentBaseNumbers.active_duplicate_keys' AS check_name, COUNT(*) AS violations
FROM (
    SELECT 1
    FROM "DocumentBaseNumbers"
    WHERE "VoidedAt" IS NULL
    GROUP BY "ExecutionCircuitId", "TypeKey", "Year", "NumberKey"
    HAVING COUNT(*) > 1
) duplicates;

-- 3) توزيع سنوات رقم الأساس (قرار نطاق السنة يحتاج هذا التوزيع، بلا رفض مسبق).
SELECT MIN("Year") AS min_year, MAX("Year") AS max_year,
       COUNT(*) FILTER (WHERE "Year" < 1900 OR "Year" > 2100) AS outside_1900_2100,
       COUNT(*) AS total_rows
FROM "DocumentBaseNumbers";

ROLLBACK;
