-- DocGen — تدقيق مسبق لسجل أرقام الأساس v2 (PostgreSQL/Supabase)
-- قراءة فقط. نفّذه قبل تطبيق BaseNumberRegistry_Pg.
-- لا يطبع أسماء/بيانات شخصية: معرّفات وأعداد فقط.

\echo '== 1) حالة الهجرات =='
SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY 1;

\echo '== 2) أعداد عامة =='
SELECT
  (SELECT count(*) FROM "Documents") AS total_docs,
  (SELECT count(*) FROM "Documents" WHERE "IsDeleted" = false) AS active_docs,
  (SELECT count(*) FROM "Documents" WHERE "IsDeleted" = true) AS deleted_docs,
  (SELECT count(*) FROM "Documents" WHERE "FileNumber" IS NOT NULL AND btrim("FileNumber") <> '') AS numbered_docs,
  (SELECT count(*) FROM "DocumentBaseNumbers") AS base_rows;

\echo '== 3) V-18: ملفات مرقّمة بلا دائرة (يجب أن تكون 0) =='
SELECT count(*) AS numbered_without_circuit
FROM "Documents"
WHERE "IsDeleted" = false
  AND "FileNumber" IS NOT NULL
  AND btrim("FileNumber") <> ''
  AND "ExecutionCircuitId" IS NULL;
SELECT "Id" FROM "Documents"
WHERE "IsDeleted" = false
  AND "FileNumber" IS NOT NULL
  AND btrim("FileNumber") <> ''
  AND "ExecutionCircuitId" IS NULL
ORDER BY "Id" LIMIT 10;

\echo '== 4) سنوات قيد غير صالحة/تحتاج تطبيعًا (1900..السنة الحالية) =='
WITH normalized AS (
  SELECT "Id",
         regexp_replace(
           translate(btrim("FileYear"),
             '٠١٢٣٤٥٦٧٨٩۰۱۲۳۴۵۶۷۸۹',
             '01234567890123456789'),
           '\s', '', 'g') AS y
  FROM "Documents"
  WHERE "FileNumber" IS NOT NULL AND btrim("FileNumber") <> ''
)
SELECT count(*) AS invalid_or_out_of_range
FROM normalized
WHERE CASE
        WHEN y ~ '^[0-9]+$' THEN y::int NOT BETWEEN 1900 AND EXTRACT(YEAR FROM CURRENT_DATE)::int
        ELSE true
      END;

\echo '== 5) تكرارات الصفوف الحالية بعد التطبيع التقريبي (Intro فقط) =='
SELECT count(*) AS duplicate_key_groups
FROM (
  SELECT 1
  FROM "DocumentBaseNumbers"
  GROUP BY "DocumentId", "Year", lower(btrim("BaseNumber"))
  HAVING count(*) > 1
) d;

\echo '== 6) ملفات محذوفة لها أرقام/صفوف (R2) =='
SELECT
  (SELECT count(*) FROM "Documents" d
   WHERE d."IsDeleted" = true AND d."FileNumber" IS NOT NULL AND btrim(d."FileNumber") <> '') AS deleted_numbered_docs,
  (SELECT count(DISTINCT b."DocumentId") FROM "DocumentBaseNumbers" b
   JOIN "Documents" d ON d."Id" = b."DocumentId"
   WHERE d."IsDeleted" = true) AS deleted_docs_with_base_rows;

\echo '== 7) تبديل دائرة من التعديل + صفوف أقدم من الوقعة (R3) =='
WITH switches AS (
  SELECT "DocumentId", MIN("CreatedAt") AS first_switch
  FROM "DocumentOccurrences"
  WHERE "OccurrenceType" = 'circuit-referred'
    AND "Details" LIKE '%"via":"edit"%'
  GROUP BY "DocumentId"
)
SELECT count(*) AS base_rows_before_edit_switch
FROM "DocumentBaseNumbers" b
JOIN switches s ON s."DocumentId" = b."DocumentId"
WHERE b."CreatedAt" < s.first_switch;

\echo '== 8) تدوير السنة الحالية + FileNumber (خطر ترتيب CreatedAt في التعبئة) =='
SELECT count(DISTINCT d."Id") AS docs_with_current_rotation_and_filenumber
FROM "Documents" d
JOIN "DocumentBaseNumbers" b ON b."DocumentId" = d."Id"
WHERE d."FileNumber" IS NOT NULL
  AND btrim(d."FileNumber") <> ''
  AND b."Year" = EXTRACT(YEAR FROM CURRENT_DATE)::int;

\echo '== 9) صفوف بلا دائرة بعد الهجرة (يُفحص أيضًا عند الإقلاع R9) =='
-- إن كان العمود موجودًا (بعد الهجرة) استخدم:
-- SELECT count(*) FROM "DocumentBaseNumbers" WHERE "ExecutionCircuitId" IS NULL AND "CircuitName" IS NULL;
