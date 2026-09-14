-- ═══════════════════════════════════════════════════════════════════════════════
-- استعلام تدقيق بياناتي: وقوعات شطب مخزنة بـ FileNumber خاطئ بعد تجديد
--
-- المبدأ: وقعة struck-off تالية لوقعة renewal في نفس الملف حيث FileNumber !=
-- آخر BaseNumber مسجّل قبل الشطب — مؤشر على تخزين السنة بدل الرقم الفعّال
-- (مثلاً "55" بدل "2026/55") أثناء التحديث القديم.
--
-- يشمل مساري الشطب معًا:
--   1) AddStruckOccurrenceAsync — عائلة منفذ عليها
--   2) UpdateStatusAsync — طالبة تنفيذ
--
-- يُعرض الملف، وقعة الشطب، آخر رقم أساسي قبلها، ووقعة التجديد إن وُجدت.
-- لا يُطبَّق تصحيح تلقائي — للمراجعة البشرية فقط.
-- ═══════════════════════════════════════════════════════════════════════════════

WITH
-- أ) أرقام الأساس مرتبة لكل ملف مرتبة بالسنة ثم CreatedAt تصاعديًا
RankedBaseNumbers AS (
    SELECT
        dbn.DocumentId,
        dbn.Year,
        dbn.BaseNumber,
        dbn.CreatedAt,
        ROW_NUMBER() OVER (
            PARTITION BY dbn.DocumentId
            ORDER BY dbn.Year DESC, dbn.CreatedAt DESC
        ) AS rn
    FROM DocumentBaseNumbers dbn
),
-- ب) كل وقعة شطب مع آخر رقم أساس فعّال قبلها
StruckOffWithBase AS (
    SELECT
        so.Id              AS OccId,
        so.DocumentId,
        so.EventDate       AS StruckEventDate,
        so.CreatedAt       AS StruckCreatedAt,
        so.FileNumber      AS StruckFileNumber,
        so.Year            AS StruckYear,
        COALESCE(
            CAST(strftime('%Y', so.EventDate) AS INTEGER),
            CAST(strftime('%Y', so.CreatedAt) AS INTEGER)
        )                  AS ReferenceYear,
        rbn.BaseNumber     AS LastBaseBeforeStruck
    FROM DocumentOccurrences so
    LEFT JOIN RankedBaseNumbers rbn
        ON rbn.DocumentId = so.DocumentId
       AND rbn.Year <= COALESCE(
           CAST(strftime('%Y', so.EventDate) AS INTEGER),
           CAST(strftime('%Y', so.CreatedAt) AS INTEGER)
       )
       AND rbn.rn = 1
    WHERE so.OccurrenceType = 'struck-off'
),
-- ج) للوقوعات المشبوهة: هل سبقها تجديد في نفس الملف؟
HasPriorRenewal AS (
    SELECT DISTINCT s.OccId
    FROM StruckOffWithBase s
    INNER JOIN DocumentOccurrences r
        ON r.DocumentId = s.DocumentId
       AND r.OccurrenceType = 'renewal'
       AND (
           r.EventDate < s.StruckEventDate
           OR (r.EventDate IS NULL AND r.CreatedAt < s.StruckCreatedAt)
           OR (s.StruckEventDate IS NULL AND r.CreatedAt < s.StruckCreatedAt)
       )
)
SELECT
    d.Id                              AS FileId,
    COALESCE(TRIM(d.BorrowerName) || ' ' || TRIM(d.BorrowerFamily),
             d.FileType || ' #' || CAST(d.Id AS TEXT))
                                      AS FileLabel,
    s.OccId                           AS StruckOffOccurrenceId,
    s.StruckFileNumber                AS StruckOff_FileNumber,
    s.StruckYear                      AS StruckOff_Year,
    s.StruckEventDate                 AS StruckOff_EventDate,
    s.StruckCreatedAt                 AS StruckOff_CreatedAt,
    s.LastBaseBeforeStruck            AS Last_BaseNumber_Before_StruckOff,
    CASE
        WHEN s.StruckFileNumber IS NULL THEN 'NO_FILE_NUMBER'
        WHEN s.LastBaseBeforeStruck IS NULL THEN 'NO_BASE_NUMBER'
        WHEN TRIM(s.StruckFileNumber) = TRIM(s.LastBaseBeforeStruck) THEN 'OK'
        ELSE 'MISMATCH'
    END                               AS AuditStatus
FROM StruckOffWithBase s
INNER JOIN Documents d ON d.Id = s.DocumentId
INNER JOIN HasPriorRenewal hr ON hr.OccId = s.OccId
WHERE
    -- فقط الحالات المشبوهة: لا يوجد رقم أساس أو الرقم مخالف
    s.StruckFileNumber IS NOT NULL
    AND (
        s.LastBaseBeforeStruck IS NULL
        OR TRIM(s.StruckFileNumber) != TRIM(s.LastBaseBeforeStruck)
    )
ORDER BY
    s.StruckCreatedAt DESC,
    d.Id;
