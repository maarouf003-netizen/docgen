# P0-07 — تدقيق قيود القاعدة: القيود الآمنة والمؤجلة (2026-11-14)

> نطاق هذه الجولة: القيود التي **لا يمكن أن تفشل على البيانات القائمة**، مع توثيق ما تأجّل
> ولماذا وكيف يُتحقق منه قبل التنفيذ. المرجع: `docs/PERFECTION_ROADMAP.md` §`P0-07`،
> و`docs/audit/03-integrity.md` (`INT-001`, `INT-010`, `INT-011`, `INT-015`).

## 1. ما طُبِّق في هذه الجولة

| البند | القيد/الفهرس | الجداول | لماذا آمن |
|---|---|---|---|
| `Version` غير سالب | `CHECK ("Version" >= 0)` باسم `CK_<Table>_VersionNonNegative` | 16 كيانًا يحمل `IVersionedEntity` | كل النسخ تُبدأ بـ`0` وتُزاد مركزياً؛ لا سلبية ممكنة |
| فهرس تصفية الحسابات | `IX_Users_BranchId_SectionId` (غير فريد) | `Users` | فهرس فقط، لا يرفض أي صف |
| ثبات التوقيت | `timestamptz` لـ`PersonalReminders.DueDate/RecurrenceEnd` | `PersonalReminders` | مُطبّق أصلًا في `DocGeneratorPostgresDbContext` (RF-013/`INT-011`) ومؤكَّد باللقطة |

القيد يُطبَّق مركزياً في `DocGeneratorDbContext.OnModelCreating` عبر المرور على كل كيان
`IVersionedEntity`، فأي كيان جديد يدخل النطاق يحصل على القيد تلقائياً.

الهجرة: `20261114175636_P0_07_SafeConstraints` (16 `CHECK` + فهرس واحد). **معلّقة** حتى
`dotnet ef database update --context DocGeneratorPostgresDbContext` في نافذة النشر.

## 2. ما تأجّل بقرار (ولماذا)

| البند | السبب | ما يلزم قبل التنفيذ |
|---|---|---|
| وحدانية رقم الأساس (`INT-001`): فهرس فريد جزئي على `(Circuit, BaseNumber, DocumentType, Year) WHERE IsDeleted=0` | قد توجد صفوف مكررة تاريخياً؛ الهجرة ستفشل عند أول تكرار | تشغيل كشف التكرارات أدناه على قاعدة الإنتاج، تنظيف/معالجة المكرر بقرار المالك، ثم هجرة القيد + فحص خدمي `409` |
| `Amount >= 0` (المبالغ الستة + المدفوع) | قد توجد قيم سالبة قديمة (تصحيح/ترحيل)؛ رفضها قد يمنع تحديث صف قديم | استعلام القيم السالبة، قرار المالك حول تسويتها، ثم قيد |
| قيود الحالات/الأنواع (`INT-015`) | الكتالوجات الحالية في الذاكرة؛ تعدد مصادرها يجعل القيد عرضة للانحراف عند إضافة حالة | توحيد الكتالوجات في مصدر واحد ثابت ثم توليد `CHECK` منها |
| `Year` في `DocumentBaseNumbers` وغيرها | لا مرجع عمل موثق لحدود السنة (هل 1900؟ 2100؟) — سؤال عمل لا خلل | قرار المالك على النطاق المقبول |
| فهارس البحث النصي (`INT-009`) وخطط `EXPLAIN` (`INT-010`) | تحتاج قياساً على بيانات واقعية الحجم | قياس على Postgres مرحلي ثم قرار `pg_trgm`/فهرس |

## 3. استعلامات الفحص المسبق (قراءة فقط — قبل تنفيذ المؤجل)

```sql
-- تكرارات رقم الأساس (نفس الدائرة + الرقم + النوع + السنة) بين غير المحذوف
SELECT "CircuitId", "BaseNumber", "DocumentType", "Year", COUNT(*) AS duplicates
FROM "DocumentBaseNumbers" d
JOIN "Documents" doc ON doc."Id" = d."DocumentId" AND doc."IsDeleted" = false
GROUP BY 1,2,3,4
HAVING COUNT(*) > 1;

-- مبالغ سالبة
SELECT COUNT(*) FROM "Documents"
WHERE "Amount" < 0 OR "Paid1" < 0 OR "Paid2" < 0 OR "Paid3" < 0
   OR "AmountNumeric" < 0;
```

إن كانت النتيجتان صفراً يمكن فتح جولة P0-07b لإضافة القيدين بهجرة مستقلة.

## 4. التحقق

- اختبارات `SafeConstraintTests`: قيد النموذج على 16 نوعاً، فهرس المستخدمين، ورفض
  `UPDATE ... Version = -1` من `SQLite` نفسه.
- `PostgresBaselineTests`: أربع هجرات، 12 عمود `Version`، و16 قيد `CK_%_VersionNonNegative`
  بعد تطبيق السلسلة على قاعدة فارغة.
- `dotnet ef migrations has-pending-model-changes` = لا تغييرات؛ سكربت الهجرة بلا `datetime2`.
