# PR-3 — `HeadAlertTaxonomy_Contract` (M3): سجل تنفيذ وبوابة نشر

> **النوع:** سجل تنفيذ مؤرخ (سقطة). لا يُحرَّر بعد التجميد؛ أي تحديث يكون بملحق/وثيقة جديدة.
> **التاريخ:** 2026-10-26 · **الفرع:** `feature/head-alerts-pr3-contract` · **الأساس:** `master` = `aa1e50c`.
> **المرجع الحاكم:** `docs/HEAD_ALERTS_RESTRUCTURE_PLAN.md` §0.2 (G1/G2)، §6.3 (M3)، §15 (المرحلة 3)، §16.
> **النطاق:** `M3` + الكيان النهائي + إزالة معالجة `nullable` + اختبارات الهجرة/الخدمة. **لا تغيير في API أو الواجهة** (وفق قيد تقسيم الـ`PRs`).

---

## 1. الحالة باختصار

| البند | الحالة |
|---|---|
| كود `PR-3` + الهجرتان | ✅ منجز على الفرع |
| بوابة G1 (مراجعة SQL + مطابقة `ModelSnapshot` + اختبارات الهجرة) | ✅ مستوفاة |
| بوابة G2 (Dry-run على نسخة إنتاجية مستعادة) | ⏳ **لم تُنفَّذ** — تتطلب نسخة/اتصال إنتاج من المالك |
| تطبيق الهجرتين على الإنتاج | ⛔ **محظور** قبل G2 + تأكيد كل النسخ على `PR-2` + صفر `NULL` |
| الدمج في `master` | ⛔ **محظور** قبل البوابات نفسها (§0.2، §15 المرحلة 3) |

**تصريح صريح:** لم يُجرَ أي اتصال بقاعدة الإنتاج، ولم تُطبَّق `HeadAlertTaxonomy_Contract_Pg` على `Supabase`.
حالة `PostgreSQL` تبقى **62 هجرة مطبَّقة**، والهجرة الجديدة تجعل العدد **63 بعد التطبيق**.

---

## 2. الهجرات

| السياق | المعرّف | الاسم | الملف |
|---|---|---|---|
| SQLite | `20261026151929` | `HeadAlertTaxonomy_Contract` | `backend/src/DocGenerator.Infrastructure/Persistence/Migrations/20261026151929_HeadAlertTaxonomy_Contract.cs` |
| PostgreSQL | `20261026152238` | `HeadAlertTaxonomy_Contract_Pg` | `backend/src/DocGenerator.Infrastructure/Persistence/MigrationsPostgres/20261026152238_HeadAlertTaxonomy_Contract_Pg.cs` |

نص `SQL` المولّد والمُراجَع (دلتا من `Backfill` إلى `Contract` لكل سياق، مُولَّد بـ`dotnet ef migrations script`):

- `docs/audit/migrations/HEADALERT_PR3_sqlite.sql`
- `docs/audit/migrations/HEADALERT_PR3_postgres.sql`

### 2.1 ما تفعله `M3` (السياقان)

1. **حارس ما قبل التشديد:** يعدّ الصفوف المخالفة قبل أي تغيير ويُسقط الهجرة كاملة (والعملية تتراجع) إن وُجد:
   - `Kind IS NULL`؛
   - `LastContentChangedAt IS NULL`؛
   - `TRIM(Kind) = ''` (فارغ أو مسافات فقط).
2. **`Kind` → `NOT NULL`** (بلا قيمة افتراضية دائمة) و**`LastContentChangedAt` → `NOT NULL`** (بلا قيمة افتراضية دائمة).
3. **قيد `CK_HeadAlerts_Kind_NotEmpty`** بصيغة `TRIM("Kind") <> ''` على الجدول.

**بنية الحارس:**

- SQLite: جدول مؤقت `_HeadAlertContractGuard` بثلاثة قيود `CHECK` مُسمّاة (`CK_HeadAlertContractGuard_NullKind`، `..._NullLastChanged`، `..._EmptyKind`) — فتظهر رسالة تشخيصية تحمل اسم الفحص، لا خطأ `NULL` عامًا. لا `IFNULL` ولا `COALESCE` على البيانات، ولا تحويل صامت.
- PostgreSQL: بلوك `DO $$ … $$` يعدّ الحالات الثلاث و`RAISE EXCEPTION` برسالة عربية صريحة عند أي مخالفة، ثم `ALTER COLUMN … SET NOT NULL` ثم `ADD CONSTRAINT`.

### 2.2 ما لا تفعله `M3`

- لا تحذف أي عمود.
- لا تضيف `Default` على `Kind`/`LastContentChangedAt` (لا يوجد افتراضي دائم — الإدراج بلا نوع يفشل).
- لا تعيد بناء أي جدول في PostgreSQL؛ أما SQLite فإعادة بناء الجدول سلوك EF متوقع، وقد تحقق الاختبار من حفظ كل الفهارس والـ`FK`.
- لا تمسّ بيانات الصفوف (لا تحويل ولا حذف).

### 2.3 الـ`Down`

- يسقط `CK_HeadAlerts_Kind_NotEmpty` ويعيد العمودين `nullable` فقط.
- **لا يعيد أي قيمة إلى `NULL` ولا يحذف صفوفًا** — التراجع التطبيقي فقط مع إبقاء البيانات (الخطة §16.2).
- في SQLite يُعاد بناء الجدول ضمنيًا لإعادة القابلية للفراغ؛ الاختبار يثبت بقاء الصف ونصه ونوعه بعد `Down`.

---

## 3. تغييرات النموذج والكود

| الملف | التغيير |
|---|---|
| `backend/src/DocGenerator.Domain/Entities/HeadAlert.cs` | `Kind` أصبح `string` (`= string.Empty`) و`LastContentChangedAt` أصبح `DateTime` (`= DateTime.UtcNow`)؛ تحديث التوثيق بأن `M3` مكتملة. |
| `backend/src/DocGenerator.Infrastructure/Persistence/Configurations/HeadAlertConfiguration.cs` | `IsRequired()` للعمودين + `HasCheckConstraint("CK_HeadAlerts_Kind_NotEmpty", "TRIM(\"Kind\") <> ''")` عبر `ToTable`. |
| `.../Persistence/HeadAlertRepository.cs` | إزالة شرطي `a.Kind != null` من استعلامَي الأنواع (النوع الآن غير قابل للفراغ). |
| `backend/src/DocGenerator.Application/Services/HeadAlertService.cs` | إزالة `LastContentChangedAt ?? CreatedAt` في مسارَي الحداثة (العمود مضمون). |
| `.../Services/HeadAlertSuccessionService.cs` | `ChooseHolder` يستخدم الطابع الزمني غير القابل للفراغ مباشرة. |
| `.../Migrations/DocGeneratorDbContextModelSnapshot.cs` | مطابقة النموذج النهائي (عمودان `required` + القيد). |
| `.../MigrationsPostgres/DocGeneratorPostgresDbContextModelSnapshot.cs` | نفسه للسياق Postgres. |

**مبرهنة سلامة:** لا يوجد في `backend/src` أي استخدام متبقٍ لـ`Kind`/`LastContentChangedAt` كـ`nullable` (تمّ الفحص بالبحث الشامل: لا `Kind != null`، لا `?? CreatedAt`، ولا `HasMaxLength` بلا `IsRequired`).

**قرار تصميم:** التهيئة الابتدائية في الكيان (`string.Empty` / `DateTime.UtcNow`) مأخوذة من نموذج الخطة §6.1، والقيد في القاعدة هو ما يمنع بقاء `Kind` فارغًا — لا اعتماد على التهيئة وحدها. أي مسار كتابة جديد لا يضبط `Kind` يفشل عند `SaveChanges`/القيد بدل أن يصبح `legacy` ضمنًا.

---

## 4. الاختبارات

### 4.1 اختبارات هجرة جديدة (`backend/tests/DocGenerator.Api.Tests/HeadAlertSchemaMigrationTests.cs`)

| الاختبار | ما يثبته |
|---|---|
| `Migration_Contract_RejectsNullAndEmptyKind` (`[Theory]` × 4) | بعد `Backfill`، إدخال `Kind = NULL` / `LastContentChangedAt = NULL` / `Kind = ''` / `Kind = '   '` يُفشل `Contract`، ويظهر **اسم القيد المتوقع** في الخطأ، ولا يُسجَّل المعرّف في `__EFMigrationsHistory`. |
| `Migration_Contract_EnforcesNonNullAndPreservesSchema` | بعد `Contract`: `notnull = 1` للعمودين، `dflt_value = NULL` (لا Default)، القيد يرفض `''`/`'   '`، الكتابة الصحيحة تمر، والفهرس الفريد الجزئي `UX_HeadAlerts_DedupeKey_Active` و`IX_HeadAlerts_Kind_Active_LastChanged` و`IX_HeadAlerts_ExecutionCircuitId_Kind_IsArchived` والفهارس الأجنبية باقية، و`PRAGMA integrity_check = ok` و`foreign_key_check` فارغ. |
| `Migration_Contract_Down_RestoresNullableColumns` | `Down` يعيد `notnull = 0` ويسقط القيد، مع بقاء الصف وقيمه (`Kind`/`LastContentChangedAt`/`Message`). |

كما أُضيف مساعد `InsertLegacyAlertAsync` (إدراج `SQL` خام) للاختبارات التي تحتاج صفوفًا سابقة لـ`M2`؛ لأن كيان `PR-3` لم يعد يمثّل `Kind = NULL`.

### 4.2 تعديلات اختبارات قائمة

- `UserManagementServiceTests`: بذر التنبيهات صار يضبط `Kind = HeadAlertKindCatalog.Legacy` (النموذج لم يعد يقبل `null`).
- اختبارات `M1/M2` القائمة عُدّلت لتتوافق مع النموذج النهائي (إدراج قديم عبر `SQL` خام حيث يلزم).

### 4.3 نتائج التشغيل

| البوابة | النتيجة |
|---|---|
| `dotnet test` (Application.Tests) | ✅ 2060/2060 — 14m46s |
| `dotnet test` (Api.Tests) | ✅ 697/697 — 10m32s |
| `dotnet test` الكامل (إعادة تشغيل نهائية) | ✅ 2757/2757، صفر فشل/تخطٍّ |

---

## 5. بوابة G1 — الأدلة

1. **مطابقة النموذج:** `dotnet ef migrations has-pending-model-changes --context DocGeneratorDbContext` و`--context DocGeneratorPostgresDbContext` يعيدان **No changes** (النموذج = آخر `ModelSnapshot`).
2. **ترتيب الهجرات:** `dotnet ef migrations list --no-connect` للسياقين يعرض `HeadAlertTaxonomy_Contract` و`HeadAlertTaxonomy_Contract_Pg` **أخيرًا** بعد `Backfill`، بلا فجوات.
3. **مراجعة SQL:** النص المولّد في `docs/audit/migrations/HEADALERT_PR3_*.sql` رُوجع سطرًا بسطر:
   - SQLite: الحارس أولًا داخل المعاملة، ثم إعادة بناء بـ`Kind TEXT NOT NULL` و`LastContentChangedAt TEXT NOT NULL` بلا `DEFAULT`، والقيد `CHECK (TRIM("Kind") <> '')`، ثم إعادة إنشاء **كل** الفهارس (15 فهرسًا: 14 عاديًا + الفريد الجزئي)، وإعادة تفعيل `foreign_keys`.
   - PostgreSQL: `DO`/`RAISE` ثم `SET NOT NULL` ثم `ADD CONSTRAINT`، بلا `DEFAULT` ولا إعادة بناء.
4. **لا افتراضي دائم:** لا `DEFAULT` على العمودين في أي من النصين؛ أثبته أيضًا اختبار `dflt_value IS NULL`.
5. **الاختبارات:** اختبارات `Migration_Contract_*` خضراء (نظريًا وعمليًا) ضمن `Api.Tests`.

> **ملاحظة G1:** نص `SQL` المحفوظ مُولَّد بـ`dotnet ef migrations script <Backfill> <Contract> --output` بترميز `UTF-8` نظيف (بدون سجل تشغيل أو ترميز `OEM`)، وقد أُعيد توليده بعد مراجعة الرسائل العربية في حارس Postgres.

---

## 6. بوابة G2 — إجراء التنفيذ الإلزامي (لم تُنفَّذ بعد)

> **الحالة:** ⏳ مؤجَّلة إلى نافذة النشر. لا توجد نسخة إنتاجية/اتصال متاح في هذه الجلسة، ولم يُدَّعَ تنفيذها.
> **لا يُطبَّق أي شيء على الإنتاج قبل إتمام الخطوات التالية بالترتيب وإرفاق مخرجاتها في ملحق هذه الوثيقة.**

### 6.1 الشروط المسبقة

- [ ] نسخة احتياطية حديثة ومُتحقَّق منها (SQLite: نسخ الملف · PostgreSQL: `pg_dump`) — §16.1.
- [ ] استعادة النسخة على بيئة معزولة وقياس زمن الاستعادة (لا نافذة بلا استعادة مجرَّبة).
- [ ] تأكيد أن **كل النسخ** تعمل بكود `PR-2` (لا نسخة قديمة تكتب `Kind = NULL`).
- [ ] فحص الصفر:

  ```sql
  -- SQLite
  SELECT SUM("Kind" IS NULL), SUM("LastContentChangedAt" IS NULL),
         SUM("Kind" IS NOT NULL AND TRIM("Kind") = '') FROM "HeadAlerts";

  -- PostgreSQL
  SELECT COUNT(*) FILTER (WHERE "Kind" IS NULL),
         COUNT(*) FILTER (WHERE "LastContentChangedAt" IS NULL),
         COUNT(*) FILTER (WHERE "Kind" IS NOT NULL AND TRIM("Kind") = '')
  FROM "HeadAlerts";
  ```

  النتيجة المطلوبة: `0 | 0 | 0`. أي قيمة غير صفرية توقف النشر (لا تُعالج بـ`UPDATE` عشوائي — راجع §6.5).

### 6.2 تطبيق الهجرات بالترتيب على النسخة المستعادة

```powershell
# SQLite (نسخة مستعادة في ملف منفصل — لا docgen.db الحقيقي)
$env:ConnectionStrings__Default="Data Source=C:\temp\pr3-dryrun.db"
dotnet ef database update HeadAlertTaxonomy_Expand --context DocGeneratorDbContext
dotnet ef database update HeadAlertOutbox_Create --context DocGeneratorDbContext
dotnet ef database update HeadAlertTaxonomy_Backfill --context DocGeneratorDbContext
dotnet ef database update HeadAlertTaxonomy_Contract --context DocGeneratorDbContext

# PostgreSQL (قاعدة مرحلية مستعادة)
$env:ConnectionStrings__Default="Host=...;Port=5432;Database=pr3_dryrun;Username=...;Password=..."
$env:Database__UsePostgres="true"
dotnet ef database update HeadAlertTaxonomy_Expand_Pg --context DocGeneratorPostgresDbContext
dotnet ef database update HeadAlertOutbox_Create_Pg --context DocGeneratorPostgresDbContext
dotnet ef database update HeadAlertTaxonomy_Backfill_Pg --context DocGeneratorPostgresDbContext
dotnet ef database update HeadAlertTaxonomy_Contract_Pg --context DocGeneratorPostgresDbContext
```

> الأسماء بلا `--no-build` تُطبِّق كل ما قبلها ضمنًا؛ ومع ذلك يُذكر كل اسم صراحةً لتوثيق الترتيب. `Contract` **آخرًا دائمًا**.

### 6.3 التحقق بعد التطبيق على النسخة المستعادة

- [ ] SQLite: `PRAGMA integrity_check;` = `ok` و`PRAGMA foreign_key_check;` فارغ.
- [ ] `pragma_table_info('HeadAlerts')`: `notnull = 1` للعمودين و`dflt_value IS NULL`.
- [ ] وجود `CK_HeadAlerts_Kind_NotEmpty` في `sqlite_master`/`information_schema`، ووجود `UX_HeadAlerts_DedupeKey_Active` والفهارس الزمنية/النوعية.
- [ ] تطابق أعداد الصفوف وبصمة الأعمدة المفتاحية `(Id, BranchId, Kind, DedupeKey, CreatedAt, LastContentChangedAt)` مع ما قبل الهجرة، مع تقرير مفسَّر لكل فرق مقصود من `M2`.
- [ ] محاولة إدراج `Kind = ''`/`NULL` تفشل على مستوى القاعدة (اختبار سلبي واحد لكل سياق).
- [ ] إقلاع التطبيق على النسخة المستعادة + فحص دخان: تسجيل دخول كل دور، فتح `/alerts`، إنشاء تنبيه يدوي، تعليم قراءة، فتح تفاصيل نص طويل (12,000 حرف)، عدّاد الشارة، `/alerts/sent`.

### 6.4 التطبيق في النافذة الحقيقية

1. إيقاف الكتابة/الخدمة (نافذة صيانة قصيرة — SQLite يعيد بناء الجدول).
2. تطبيق الثلاث المعلّقة على SQLite بالترتيب ثم `Contract`، وعلى PostgreSQL `Contract_Pg` فقط (الثلاث مطبَّقة).
3. تنفيذ §6.3 على القاعدة الحقيقية قبل فتح الكتابة.
4. توثيق المخرجات في ملحق `12-…` (لا تُحرَّر هذه الوثيقة بعد التجميد).

### 6.5 عند وجود صفوف مخالفة

- الحارس يرفض التطبيق ولا يوجد أي تحويل صامت — هذا مقصود.
- عالج الصفوف المخالفة **قبل** إعادة المحاولة:
  - `Kind IS NULL`: حدّد النوع الدلالي الصحيح يدويًا من سياق الحدث (لا `legacy` تلقائيًا)، أو أرشِف الصف إن كان تاريخيًا غير قابل للتصنيف.
  - `LastContentChangedAt IS NULL`: اضبطه على `CreatedAt` للصفوف التاريخية (سياسة `M2` نفسها) بعد مراجعة العدد.
  - `Kind` فارغ/أبيض: صحّح النوع أو أرشِف.
- أعد تشغيل فحص الصفر ثم الهجرة.

---

## 7. ملحق التحقق النهائي

| الفحص | الأمر | النتيجة |
|---|---|---|
| بناء + اختبارات الخلفية | `dotnet test --nologo` (من `backend/`) | ✅ 2757/2757 (2060 + 697)، صفر فشل |
| نموذج SQLite مطابق | `dotnet ef migrations has-pending-model-changes --context DocGeneratorDbContext` | ✅ `No changes have been made to the model since the last migration.` |
| نموذج Postgres مطابق | `dotnet ef migrations has-pending-model-changes --context DocGeneratorPostgresDbContext` | ✅ `No changes...` (نفس الرسالة) |
| ترتيب الهجرات (SQLite) | `dotnet ef migrations list --context DocGeneratorDbContext --no-connect` | ✅ `…Backfill` ← `…Contract` أخيرًا |
| ترتيب الهجرات (Postgres) | `dotnet ef migrations list --context DocGeneratorPostgresDbContext --no-connect` | ✅ `…Backfill_Pg` ← `…Contract_Pg` أخيرًا |
| بوابات الواجهة (بلا تغيير واجهة في PR-3) | `npx oxlint src` · `npx tsc -b` · `npx vitest run` · `npm run build` | ✅ oxlint و`tsc -b` بلا ملاحظات · `vitest` 152/152 ملفًا و1599/1599 اختبارًا · `vite build` ناجح |

> **ملاحظة:** اختبار `M3` على PostgreSQL الفعلي غير ممكن محليًا بلا قاعدة؛ يغطيه فحص G2 (§6.3) و`DatabaseInitializerTests` عند ضبط `DOCGEN_TEST_POSTGRES`.

---

## 8. المخاطر والقيود

| الخطر | المعالجة |
|---|---|
| تطبيق `Contract` قبل `PR-2` على كل النسخ | حارس `NULL` يمنع فقط البيانات المخالفة؛ **الترتيب التشغيلي إلزامي** (§16.1). |
| نسخة قديمة تكتب `Kind = NULL` بعد `Contract` | ستفشل كتابتها بـ`NOT NULL` — لذلك لا تُطبَّق `M3` قبل تحديث كل النسخ. |
| نافذة SQLite تعيد بناء الجدول | إيقاف الكتابة + نسخة احتياطية + `integrity_check` قبل إعادة الخدمة. |
| تشغيل `Contract` بلا G2 | محظور بهذه الوثيقة؛ G2 شرط دمج ونشر. |
| افتراض أن `dotnet test` يغطي Postgres | لا؛ اختبارات `M3` الحالية على SQLite. فحص Postgres الفعلي جزء من G2 (§6.3) + `DatabaseInitializerTests` عند توفر `DOCGEN_TEST_POSTGRES`. |

---

## 9. قائمة تسليم النشر (يُقرّها المشغّل)

- [ ] G2 منفَّذة على نسخة مستعادة، ومخرجاتها موثقة في ملحق.
- [ ] كل النسخ على `PR-2`.
- [ ] فحص الصفر `0 | 0 | 0` على القاعدة الحقيقية.
- [ ] نسخة احتياطية + استعادة مجرَّبة.
- [ ] تطبيق `HeadAlertTaxonomy_Contract` (SQLite) و`HeadAlertTaxonomy_Contract_Pg` (PostgreSQL) بالترتيب.
- [ ] تحقق §6.3 + فحص دخان.
- [ ] شطب السطرين من قائمة `RUN_GUIDE.md` §9 التراكمية بعد النجاح.
