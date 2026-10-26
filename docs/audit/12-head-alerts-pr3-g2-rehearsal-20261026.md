# ملحق 12 — تمرين `G2` المحلي لـ PR‑3 (2026‑10‑26)

> **الحالة:** ⚠️ **تمرين (Rehearsal) — ليس `G2` الإنتاجية.**
> نُفِّذ على نسخة معزولة من قاعدة **التطوير** المحلية، لا على نسخة إنتاجية مستعادة ولا على `Supabase`.
> **بوابة `G2` في `docs/audit/11-head-alerts-pr3-20261026.md` §6 تبقى غير مستوفاة**، والدمج/التطبيق
> على الإنتاج يبقيان محظورين حتى تنفيذها على نسخة إنتاجية حقيقية.

---

## 1. الغرض والنطاق

| البند | القيمة |
|---|---|
| الهدف | التحقق العملي من تسلسل `M1` ← `M2` ← `M3`، وسلوك الحارس، وأثر إعادة بناء `SQLite` على البيانات والتسلسل، قبل نافذة النشر. |
| البيئة | جهاز تطوير محلي (`win32`)، `dotnet 10.0.302`، `EF Core 10.0.10`. |
| المصدر | `backend/src/DocGenerator.Api/docgen.db` — قاعدة تطوير محلية (وليست إنتاجية). |
| نقطة البداية | `20261014180913_AddInclusionNotes` — **83** هجرة مطبَّقة من **87**، أي حالة ما قبل `PR‑2` بالضبط. |
| المعزولية | نسخة في `%TEMP%\opencode\g2-dryrun\`؛ لم تُلمَس القاعدة الأصلية (تحقق: بصمة `SHA-256` أدناه). |
| ما لم يُنفَّذ | `PostgreSQL` (لا خادم/اتصال متاح محليًا)، وإقلاع التطبيق وفحص الدخان (يتطلب بيانات دخول حقيقية لكل دور). |

بصمة النسخة الأصلية قبل أي تشغيل:

```
SHA-256(pr3-pre.db) = 8933E3EF2BBFC4DE66751028CC981BD1214F92E813843EA733E86533CC3ECAA6
```

> **قيد جوهري على الاستدلال:** بيانات التطوير لا تحوي الحالات التاريخية التي تُقلق الإنتاج
> (صفوف `Kind` مضبوط مع `LastContentChangedAt = NULL`، أو أنواع قديمة غير معروفة). لذلك أُضيفت
> تلك الحالات **صناعيًا** في نسخ منفصلة لاختبار الحارس (ال§5)، ولا تُغني عن فحص `0 | 0 | 0`
> على نسخة الإنتاج.

---

## 2. الحالة قبل التطبيق (Baseline)

| القياس | القيمة |
|---|---|
| هجرات مطبَّقة | **83** (`20261014180913_AddInclusionNotes` أخيرًا) |
| هجرات معلّقة | **4**: `Expand` ← `Outbox_Create` ← `Backfill` ← `Contract` |
| `HeadAlerts` | **51** صفًا · `MAX(Id) = 83` · `MIN(Id) = 1` · لا أعمدة `Kind`/`LastContentChangedAt` |
| `sqlite_sequence.HeadAlerts` | **84** (أعلى `Id` محذوف ⇒ القيمة أكبر من `MAX(Id)` بواحد) |
| `HeadAlertRecipients` | **58** صفًا · `sqlite_sequence = 91` |
| فهارس `HeadAlerts` | **10** |
| `PRAGMA integrity_check` | `ok` |
| `PRAGMA foreign_key_check` | فارغ |
| إجمالي الجداول | 48 |

> الحالة `seq = 84 > MAX(Id) = 83` **مهمّة عمدًا**: هي السيناريو الذي يكشف تراجع التسلسل عند
> إعادة بناء الجدول. لولاها لمرّ التمرين دون كشف العيب الموثَّق في §6.

---

## 3. التطبيق بالترتيب

نُفِّذت الأوامر من `backend/` بـ`--no-build`، مع توجيه الاتصال صراحةً إلى النسخة المعزولة:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:Database__UsePostgres = "false"
$env:ConnectionStrings__DefaultConnection = "Data Source=<g2-dryrun>\pr3-dryrun.db;Pooling=False"
dotnet ef database update <Migration> --context DocGeneratorDbContext `
  --project src/DocGenerator.Infrastructure --startup-project src/DocGenerator.Api --no-build
```

| # | الهجرة | الزمن الجداري | `sqlite_sequence.HeadAlerts` بعدها | ملاحظة |
|---|---|---|---|---|
| 1 | `HeadAlertTaxonomy_Expand` | 12.4 ث | 84 | `ALTER TABLE ADD COLUMN` فقط؛ لا إعادة بناء |
| 2 | `HeadAlertOutbox_Create` | 11.8 ث | 84 | ينشئ `HeadAlertOutboxMessages` وقيدَي `CHECK` |
| 3 | `HeadAlertTaxonomy_Backfill` | 11.8 ث | **83** ⚠️ | **إعادة بناء `HeadAlerts`** (انظر §6) |
| 4 | `HeadAlertTaxonomy_Contract` | 9.1 ث | **83** | إعادة بناء ثانية + قيد + فهارس |

> الأزمنة تشمل إقلاع `EF`/المضيف (≈8–10 ث)، وليست زمن تنفيذ SQL الصافي.

---

## 4. اختبار الحارس (أربعة فروع رفض)

أُخذت أربع نسخ من حالة ما بعد `Backfill`، وحُقن في كل منها مخالفة واحدة، ثم شُغِّل `Contract`:

| النسخة | المخالفة المحقونة | نتيجة `Contract` |
|---|---|---|
| `guardA` | `Kind = NULL` لصف واحد | ❌ `SQLite Error 19: CHECK constraint failed: CK_HeadAlertContractGuard_NullKind` |
| `guardB` | `LastContentChangedAt = NULL` مع `Kind` سليم | ❌ `... CK_HeadAlertContractGuard_NullLastChanged` |
| `guardC` | `Kind = '   '` (فراغ أبيض) | ❌ `... CK_HeadAlertContractGuard_EmptyKind` |
| `guardD` | `Kind` مضبوط + `LastContentChangedAt = NULL` (فجوة `M2` المعروفة) | ❌ `... CK_HeadAlertContractGuard_NullLastChanged` |

**السلوك بعد الرفض (تحقق على `guardA`):**

- `HeadAlerts` سليمة: 51 صفًا، والعمودان ما زالا `NULL`-able.
- لا بقايا: لا `_HeadAlertContractGuard` ولا `ef_temp_HeadAlerts` في `sqlite_master`.
- `__EFMigrationsHistory` ينتهي عند `..._Backfill` (لم يُسجَّل `Contract`).
- `PRAGMA integrity_check` = `ok`.

⇒ الرفض **ذرّي ونظيف**، ويعيد الحالة إلى ما قبل `Contract` تمامًا؛ لا حاجة لمسار الاستعادة في هذه الحالة.

---

## 5. التحقق بعد التطبيق (مقابل §6.3 في الوثيقة 11)

| البند | النتيجة |
|---|---|
| `PRAGMA integrity_check` | ✅ `ok` |
| `PRAGMA foreign_key_check` | ✅ فارغ |
| `pragma_table_info('HeadAlerts')` — `Kind` | ✅ `TEXT` · `notnull = 1` · `dflt_value = NULL` |
| `pragma_table_info('HeadAlerts')` — `LastContentChangedAt` | ✅ `TEXT` · `notnull = 1` · `dflt_value = NULL` |
| `CK_HeadAlerts_Kind_NotEmpty` | ✅ موجود ومُنفَّذ (`TRIM("Kind") <> ''`) |
| `UX_HeadAlerts_DedupeKey_Active` | ✅ موجود (فريد جزئي: `DedupeKey IS NOT NULL AND IsArchived = 0`) |
| فهارس `HeadAlerts` | ✅ **15** (10 قديمة + 5 جديدة) |
| `Kind IS NULL` / `Kind` فارغ / `LastContentChangedAt IS NULL` | ✅ `0 / 0 / 0` |
| تكرار `DedupeKey` بين الصفوف النشطة | ✅ 0 |
| تكرار `(HeadAlertId, UserId)` في المستلمين | ✅ 0 |
| تطابق `(Id, BranchId, CreatedAt, Message, TargetType, FK)` قبل/بعد | ✅ `0` فروق في الاتجاهين (`EXCEPT` مزدوج) |
| إجمالي الصفوف | ✅ `HeadAlerts` 51 → 51 · `HeadAlertRecipients` 58 → 58 |
| `sqlite_sequence.HeadAlertRecipients` | ✅ 91 قبل/بعد |

**اختبارات الإدراج السلبية على المخطط النهائي:**

| المحاولة | النتيجة |
|---|---|
| `Kind = NULL` | ❌ `NOT NULL constraint failed: HeadAlerts.Kind` |
| `Kind = ''` | ❌ `CHECK constraint failed: CK_HeadAlerts_Kind_NotEmpty` |
| `Kind = '   '` | ❌ `CHECK constraint failed: CK_HeadAlerts_Kind_NotEmpty` |
| `LastContentChangedAt = NULL` | ❌ `NOT NULL constraint failed: HeadAlerts.LastContentChangedAt` |
| صف سليم (`Kind='legacy'` + وقت) | ✅ أُدرج، ثم حُذف، وأُعيد ضبط التسلسل |

> `IsArchived` يحتفظ بـ`DEFAULT 0` الموروث من `M1`؛ شرط «بلا Default دائم» يخصّ `Kind`
> و`LastContentChangedAt` فقط، وكلاهما بلا Default كما تحقّق أعلاه.

---

## 6. الاكتشاف الحرج: تراجع `sqlite_sequence` يحدث في `Backfill` (M2) لا في `Contract` (M3)

### 6.1 القياس

| اللحظة | `seq` |
|---|---|
| قبل `Expand` | **84** |
| بعد `Expand` | 84 |
| بعد `Outbox_Create` | 84 |
| **بعد `Backfill`** | **83** ⚠️ |
| بعد `Contract` | 83 |
| بعد إعادة الرفع اليدوي | 84 |

### 6.2 السبب

`HeadAlertTaxonomy_Backfill` يضيف الـFK `FK_HeadAlerts_ExecutionCircuits_ExecutionCircuitId`
(`onDelete: SetNull`)، و`SQLite` لا يدعم إضافة FK إلى جدول قائم، فيولّد `EF` إعادة بناء كاملة:

```sql
CREATE TABLE "ef_temp_HeadAlerts" (...);
INSERT INTO "ef_temp_HeadAlerts" (...) SELECT ... FROM "HeadAlerts";
DROP TABLE "HeadAlerts";
ALTER TABLE "ef_temp_HeadAlerts" RENAME TO "HeadAlerts";
```

إعادة البناء تُعيد `sqlite_sequence` إلى `MAX(Id)` الموجود (83)، فتضيع القيمة 84.

### 6.3 الأثر التشغيلي

- التعليمات في `docs/audit/11` §6.2 تقول: «سجّل `seq` **قبل `Contract`**» — وهذا **غير كافٍ**:
  التسجيل قبل `Contract` يسجّل القيمة **المراجعة أصلًا** (83)، فتصبح «إعادة الرفع» بلا أثر
  ويضيع `seq = 84` بصمت.
- النتيجة العملية: أي `Id` أعلى محذوف قد يُعاد استخدامه بعد الهجرة (خطر collision مع سجلات
  `AuditLogs`/مراجع خارجية تشير إلى `HeadAlertId`).
- العيب نفسه يظهر في `Down` كذلك (84 → 83)، وهو ما كان موثَّقًا لـ`Contract` فقط.

### 6.4 التصحيح المعتمد (يُطبَّق في نافذة النشر)

```sql
-- 1) قبل Backfill (أول إعادة بناء) — لا قبل Contract فقط:
SELECT seq FROM sqlite_sequence WHERE name = 'HeadAlerts';

-- 2) بعد Contract (أو بعد أي Down/Up):
UPDATE sqlite_sequence
SET seq = MAX(seq, :recorded_seq)
WHERE name = 'HeadAlerts';

-- 3) إن لم يوجد صف (جدول فارغ أصلًا):
INSERT INTO sqlite_sequence (name, seq)
SELECT 'HeadAlerts', :recorded_seq
WHERE :recorded_seq > 0
  AND NOT EXISTS (SELECT 1 FROM sqlite_sequence WHERE name = 'HeadAlerts');
```

> لا يُعالَج هذا داخل هجرة `EF` (قرار معماري قائم): إعادة بناء الجدول سلوك محرّك، وأي محاولة
> للعبث بـ`sqlite_sequence` داخل الهجرة هشّة عبر إصدارات `SQLite`. الحل تشغيلي موثَّق.

---

## 7. اختبار `Down` ثم `Up` مجددًا

| الخطوة | النتيجة |
|---|---|
| `database update ..._Backfill` (أي `Down` لـ`Contract`) | ✅ `Kind`/`LastContentChangedAt` عادا `nullable`، القيد اختفى، 51 صفًا سليمة، `integrity_check = ok` |
| التسلسل بعد `Down` | ⚠️ 84 → **83** (متوقع وفق §6) |
| إعادة `Contract` (`Up` مرة ثانية) | ✅ نجحت، العمودان `NOT NULL` بلا Default، القيد موجود ومُنفَّذ فعليًا عند الإدراج |
| التسلسل بعد `Up` الثاني | ⚠️ 83 (يلزمه رفع يدوي مرة أخرى) |

⇒ مسار «إعادة `Contract` آمنة» في §6.3.1 صحيح تقنيًا، لكنه **يستلزم** إعادة رفع التسلسل في كل مرة.

---

## 8. سلسلة كاملة على قاعدة فارغة (تحقق إضافي)

تطبيق **كل** الهجرات على ملف فارغ (`fresh.db`): ✅ `87/87` هجرة، 15 فهرسًا، القيد موجود،
`integrity_check = ok`، `foreign_key_check` فارغ، في 24.3 ث.

هذا يثبت تماسك السلسلة من الصفر، ولا علاقة له بتراجع التسلسل (لا صفوف ولا `sqlite_sequence` عند الإنشاء).

---

## 9. ما تبقّى لاستيفاء `G2` الحقيقية

| البند | الحالة |
|---|---|
| نسخة إنتاجية مستعادة (SQLite أو `pg_dump`/staging) | ❌ غير متاحة في هذه الجلسة |
| فحص `0 \| 0 \| 0` على بيانات إنتاجية | ❌ لم يُنفَّذ (بيانات التطوير لا تحتويه) |
| `PostgreSQL` — تطبيق `Contract_Pg` على قاعدة مرحلية | ❌ لا خادم/اتصال محلي؛ الدليل المتاح هو اختبار CI الحيّ `HeadAlertContractPostgresTests` (40 ث، ناجح) |
| إقلاع التطبيق + فحص الدخان (كل دور، `/alerts`، إنشاء/قراءة، 12,000 حرف، الشارة، `/alerts/sent`) | ❌ يتطلب بيانات دخول إنتاجية |
| قياس زمن الاستعادة | ❌ لا نسخة إنتاجية |

---

## 10. الخلاصة

1. ✅ **تسلسل الهجرات صحيح** على `SQLite` من حالة `PR‑2` (83/87) ومن قاعدة فارغة (87/87).
2. ✅ **الحارس يعمل** ويرفض الفروع الأربعة برسائل صريحة، ويرجع الحالة نظيفة دون بقايا.
3. ✅ **المخطط النهائي مطابق** للمطلوب: `NOT NULL` بلا Default، القيد، 15 فهرسًا، والفهرس الفريد الجزئي.
4. ✅ **البيانات محفوظة**: `0` فروق في الأعمدة الثابتة واتجاهي المقارنة، وأعداد المستلمين لم تتغير.
5. ⚠️ **عيب موثَّق اكتُشف وصُحِّح:** تسجيل `sqlite_sequence` يجب أن يسبق **`Backfill`** لا `Contract`.
6. ⛔ **`G2` الإنتاجية لم تُستوفَ**: تحتاج نسخة إنتاجية مستعادة + `PostgreSQL` مرحلي + فحص دخان.
   **لا دمج ولا تطبيق على الإنتاج بناءً على هذا الملحق.**
