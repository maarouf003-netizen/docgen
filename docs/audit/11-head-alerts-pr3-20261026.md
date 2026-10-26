# PR-3 — `HeadAlertTaxonomy_Contract` (M3): سجل تنفيذ وبوابة نشر

> **النوع:** سجل تنفيذ مؤرخ (سقطة). لا يُحرَّر بعد التجميد؛ أي تحديث يكون بملحق/وثيقة جديدة.
> **التاريخ:** 2026-10-26 · **الفرع:** `feature/head-alerts-pr3-contract` · **الأساس:** `master` = `aa1e50c`.
> **المرجع الحاكم:** `docs/HEAD_ALERTS_RESTRUCTURE_PLAN.md` §0.2 (G1/G2)، §6.3 (M3)، §15 (المرحلة 3)، §16.
> **النطاق:** `M3` + الكيان النهائي + إزالة معالجة `nullable` + اختبارات الهجرة/الخدمة. **لا تغيير في API أو الواجهة** (وفق قيد تقسيم الـ`PRs`).
>
> **ملاحظة تحرير (2026-10-26، قبل التجميد):** صُححت في §6.1–§6.3 مفاتيح بيئة `dotnet ef` (`ConnectionStrings__DefaultConnection` و`POSTGRES_CONNECTION`)، واستعلام فحص الصفر على جدول فارغ (`COALESCE`)، وخطوات نسخ WAL وتسلسل `sqlite_sequence`، بعد مراجعة نقدية.
> وفي مراجعة ثانية (قبل التجميد أيضًا) صُحح وصف ذرّية هجرة SQLite (§2.2/§5.3)، وأُضيف مسار استعادة الحالة الوسيطة (§6.3.1)، وحُدِّد حدّ `TRIM` (§2.1) ومعالجة `sqlite_sequence` في `Down` (§2.3)، وأُضيف اختبار Postgres حيّ (§4.1/§7/§8). لا تغيير على نطاق `M3`.

---

## 1. الحالة باختصار

| البند | الحالة |
|---|---|
| كود `PR-3` + الهجرتان | ✅ منجز على الفرع |
| اختبار Postgres حيّ لمسار الرفض ثم التطبيق | ✅ مضاف (`HeadAlertContractPostgresTests` — مبوَّب بـ`DOCGEN_TEST_POSTGRES`، يُنفَّذ فعليًا في CI) |
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

- SQLite: جدول مؤقت `_HeadAlertContractGuard` بثلاثة قيود `CHECK` مُسمّاة (`CK_HeadAlertContractGuard_NullKind`، `..._NullLastChanged`، `..._EmptyKind`) — فتظهر رسالة تشخيصية تحمل اسم الفحص، لا خطأ `NULL` عامًا. لا `IFNULL` ولا `COALESCE` على **قيم الأعمدة** (الوحيد `COALESCE(SUM(...), 0)` لمعالجة الجدول الفارغ، وهو تجميع لا تحويل بيانات)، ولا تحويل صامت.
- PostgreSQL: بلوك `DO $$ … $$` يعدّ الحالات الثلاث و`RAISE EXCEPTION` برسالة عربية صريحة عند أي مخالفة، ثم `ALTER COLUMN … SET NOT NULL` ثم `ADD CONSTRAINT`. الهجرة كلها داخل معاملة واحدة، و`SET NOT NULL` يأخذ `ACCESS EXCLUSIVE` تلقائيًا (يحجب الكتابة أثناء تنفيذه فقط)؛ الجدول صغير (28 صفًا) فالتأثير لحظي — بخلاف SQLite لا حاجة لإيقاف مسبق للكتابة.

> **حدّ `TRIM` (مقبول وموثّق):** `TRIM(x)` في SQLite وPostgreSQL يزيل **المسافة العادية (U+0020) فقط**؛ فالقيم `"\t"`/`"\n"`/المسافة غير الفاصلة (NBSP) تمرّ من الحارس ومن القيد معًا. كما لا يرفض القيد نوعًا صالحًا محاطًا بمسافات (`' legacy '`) — وهو لا يطابق فلاتر النوع فيصبح التنبيه غير مرئي. هذا مقبول لأن الأنواع تأتي من كتالوج مغلق (`HeadAlertKindCatalog`) ولا يوجد مسار كتابة من مصدر خارجي؛ وأي إدخال مستقبلي من مصدر خارجي يستوجب تشديدًا: `"Kind" !~ '^\s*$'` **و** `TRIM("Kind") = "Kind"` في PostgreSQL، وتوسيع `TRIM` إلى `char(9)||char(10)||char(13)||char(32)` أو تحقق صريح في طبقة أعلى على SQLite.

### 2.2 ما لا تفعله `M3`

- لا تحذف أي عمود.
- لا تضيف `Default` على `Kind`/`LastContentChangedAt` (لا يوجد افتراضي دائم — الإدراج بلا نوع يفشل).
- لا تعيد بناء أي جدول في PostgreSQL؛ أما SQLite فإعادة بناء الجدول سلوك EF متوقع، وقد تحقق الاختبار من حفظ كل الفهارس والـ`FK`.
- لا تحافظ تلقائيًا على `sqlite_sequence` بعد إعادة بناء SQLite: EF ينسخ الصفوف إلى جدول جديد فيُضبط التسلسل على أعلى `Id` **موجود**؛ إن كان الأعلى محذوفًا يتراجع التسلسل. لذلك أُضيفت خطوة تسجيل/إعادة رفع يدوية في G2 (§6.2–§6.3) بدل حل ملتوٍ داخل الهجرة. وينطبق الشيء نفسه على `Down` (يعيد البناء ضمنيًا أيضًا) — §2.3.
- **SQLite — لا ذرّية عبر الدفعات:** EF يولّد **ثلاث معاملات** منفصلة: (1) الحارس + جدول `ef_temp_HeadAlerts` + نسخ الصفوف، (2) `DROP TABLE "HeadAlerts"` + `RENAME` (مع `PRAGMA foreign_keys`)، (3) إنشاء الفهارس؛ ثم `INSERT` سجل الهجرة **خارج معاملة صريحة**. فالحارس يضمن سلامة البيانات لحظة الدفعة الأولى فقط، والضمان الكامل يتطلب **إيقاف الكتابة** خلال النافذة (خطة §6.4). انهيار بين الدفعات يترك حالة وسيطة — مسار الاستعادة في §6.3.1.
- لا تمسّ بيانات الصفوف (لا تحويل ولا حذف).

### 2.3 الـ`Down`

- يسقط `CK_HeadAlerts_Kind_NotEmpty` ويعيد العمودين `nullable` فقط.
- **لا يعيد أي قيمة إلى `NULL` ولا يحذف صفوفًا** — التراجع التطبيقي فقط مع إبقاء البيانات (الخطة §16.2).
- في SQLite يُعاد بناء الجدول ضمنيًا لإعادة القابلية للفراغ؛ الاختبار يثبت بقاء الصف ونصه ونوعه بعد `Down`.
- **تنبيه `sqlite_sequence` في `Down`:** إعادة البناء الضمنية تُعيد ضبط `AUTOINCREMENT` على أعلى `Id` موجود كما في `Up`؛ فإن كان أعلى `Id` محذوفًا تراجع التسلسل عند التراجع أيضًا. على قاعدة حيّة: سجّل `seq` قبل `Down` وأعد رفعه بعده بمنطق §6.3 (لا تُنقص القيمة الحالية).
- **بعد `Down` يُنشر كود `PR-2` فورًا:** المخطط المتراجع يقبل `Kind = NULL`، وكود `PR-3` يقرأ الخاصية كـ`string` غير قابل للفراغ (لا يفرضه CLR) — فقراءة صف بـ`NULL` تُنتج `null` في الذاكرة وقد تنفجر لاحقًا ب`NullReferenceException`. لا تُترك نسخة `PR-3` تعمل على مخطط متراجع.

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
| `Migration_Contract_RejectsNullAndEmptyKind` (`[Theory]` × 4) | بعد `Backfill`، إدخال `Kind = NULL` / `LastContentChangedAt = NULL` / `Kind = ''` / `Kind = '   '` يُفشل `Contract`، ويظهر **اسم القيد المتوقع** في الخطأ، ولا يُسجَّل المعرّف في `__EFMigrationsHistory`، ويبقى المخطط nullable بلا القيد، وبيانات الصف المخالف كما هي (لا تحويل صامت، ولا جدول إعادة بناء مؤقت). |
| `Migration_Contract_EnforcesNonNullAndPreservesSchema` | بعد `Contract`: `notnull = 1` للعمودين، `dflt_value = NULL` (لا Default)، القيد يرفض `''`/`'   '` (وأيضًا عبر مسار الكيان `SaveChanges`، لا SQL خام فقط)، الكتابة الصحيحة تمر، ومجموعة أسماء الفهارس ومجموعة جداول الـFK **مطابقة تمامًا** لما قبل الهجرة (ومنها `UX_HeadAlerts_DedupeKey_Active` و`IX_HeadAlerts_Kind_Active_LastChanged` و`IX_HeadAlerts_ExecutionCircuitId_Kind_IsArchived`)، **ونص فلتر الفهرس الجزئي** (`DedupeKey IS NOT NULL AND IsArchived = 0`) محفوظ، و`PRAGMA integrity_check = ok` و`foreign_key_check` فارغ. |
| `Migration_Contract_Down_RestoresNullableColumns` | `Down` يعيد `notnull = 0` ويسقط القيد، مع بقاء الصف وقيمه (`Kind`/`LastContentChangedAt`/`Message`). |
| `HeadAlertContractPostgresTests.Contract_RejectsNullKind_ThenAppliesCleanly` | **Postgres حيّ (مبوَّب بـ`DOCGEN_TEST_POSTGRES`):** هجرة إلى `Backfill_Pg` ← صف بـ`Kind = NULL` و`LastContentChangedAt = NULL` ← رفض `Contract_Pg` بـ`P0001` ورسالة الحارس، مع بقاء العمودين `nullable` بلا قيد ولا سجل هجرة وبلا تحويل للقيمتين ← **إصلاح تدريجي يثبت الفروع الثلاثة:** بعد إصلاح `Kind` يبقى الرفض على `LastContentChangedAt = NULL`، وبعد إصلاح الطابع (`= CreatedAt`، نفس بديل `PR-2`) يبقى الرفض على النوع الأبيض ← إصلاح النوع ← تطبيق ناجح ← `NOT NULL` للعمودين + القيد، ورفض `Kind = ''` بـ`23514` و`NULL` بـ`23502`. ينشئ قاعدة معزولة خاصة به (`docgen_pr3_*`) ويسقطها، فلا يتعارض مع اختبار Postgres المشترك الذي ينفّذ `EnsureDeleted`. |

كما أُضيف مساعد `InsertLegacyAlertAsync` (إدراج `SQL` خام) للاختبارات التي تحتاج صفوفًا سابقة لـ`M2`؛ لأن كيان `PR-3` لم يعد يمثّل `Kind = NULL`.

### 4.2 تعديلات اختبارات قائمة

- `UserManagementServiceTests`: بذر التنبيهات صار يضبط `Kind = HeadAlertKindCatalog.Legacy` (النموذج لم يعد يقبل `null`).
- اختبارات `M1/M2` القائمة عُدّلت لتتوافق مع النموذج النهائي (إدراج قديم عبر `SQL` خام حيث يلزم).

### 4.3 نتائج التشغيل

| البوابة | النتيجة |
|---|---|
| `dotnet test` (Application.Tests) | ✅ 2060/2060 — 14m46s |
| `dotnet test` (Api.Tests) | ✅ 698/698 — 4m40s |
| `dotnet test` الكامل | ✅ 2757/2757 (2060 + 697) قبل إضافة اختبار Postgres؛ وبعده أُعيد تشغيل `Api.Tests` وحده: 698/698 ⇒ **إجمالي 2758** (2060 + 698) |

---

## 5. بوابة G1 — الأدلة

1. **مطابقة النموذج:** `dotnet ef migrations has-pending-model-changes --context DocGeneratorDbContext` و`--context DocGeneratorPostgresDbContext` يعيدان **No changes** (النموذج = آخر `ModelSnapshot`). وأُضيف الفحص نفسه إلى CI (خطوتا `SQLite/Postgres model matches snapshot (G1)` في وظيفة `migration-guard`) ليفشل البناء تلقائيًا عند أي تغيير نموذج بلا هجرة — بدل الاعتماد على تنفيذ يدوي قابل للنسيان.
2. **ترتيب الهجرات:** `dotnet ef migrations list --no-connect` للسياقين يعرض `HeadAlertTaxonomy_Contract` و`HeadAlertTaxonomy_Contract_Pg` **أخيرًا** بعد `Backfill`، بلا فجوات.
3. **مراجعة SQL:** النص المولّد في `docs/audit/migrations/HEADALERT_PR3_*.sql` رُوجع سطرًا بسطر:
   - SQLite: الحارس + جدول `ef_temp_HeadAlerts` + نسخ الصفوف في **معاملة أولى**، ثم `DROP`/`RENAME` في **معاملة ثانية** (مع `PRAGMA foreign_keys = 0/1`)، ثم إنشاء الفهارس في **معاملة ثالثة**، وأخيرًا `INSERT` سجل الهجرة خارج معاملة صريحة. الأعمدة `Kind TEXT NOT NULL` و`LastContentChangedAt TEXT NOT NULL` بلا `DEFAULT`، والقيد `CHECK (TRIM("Kind") <> '')`، وتُعاد **كل** الفهارس (15 فهرسًا: 14 عاديًا + الفريد الجزئي). راجع §2.2 (نافذة الانهيار) و§6.3.1 (الاستعادة).
   - PostgreSQL: `DO`/`RAISE` ثم `SET NOT NULL` ثم `ADD CONSTRAINT`، بلا `DEFAULT` ولا إعادة بناء.
4. **لا افتراضي دائم:** لا `DEFAULT` على العمودين في أي من النصين؛ أثبته أيضًا اختبار `dflt_value IS NULL`.
5. **الاختبارات:** اختبارات `Migration_Contract_*` خضراء (نظريًا وعمليًا) ضمن `Api.Tests`.

> **ملاحظة G1:** نص `SQL` المحفوظ مُولَّد بـ`dotnet ef migrations script <Backfill> <Contract> --output` بترميز `UTF-8` نظيف (بدون سجل تشغيل أو ترميز `OEM`)، وقد أُعيد توليده بعد مراجعة الرسائل العربية في حارس Postgres.

---

## 6. بوابة G2 — إجراء التنفيذ الإلزامي (لم تُنفَّذ بعد)

> **الحالة:** ⏳ مؤجَّلة إلى نافذة النشر. لا توجد نسخة إنتاجية/اتصال متاح في هذه الجلسة، ولم يُدَّعَ تنفيذها.
> **لا يُطبَّق أي شيء على الإنتاج قبل إتمام الخطوات التالية بالترتيب وإرفاق مخرجاتها في ملحق هذه الوثيقة.**

### 6.1 الشروط المسبقة

- [ ] نسخة احتياطية حديثة ومُتحقَّق منها (PostgreSQL: `pg_dump`). **SQLite:** أوقف الخدمة أولًا، ثم انسخ `docgen.db` **مع** `docgen.db-wal` و`docgen.db-shm` (أو نفّذ `PRAGMA wal_checkpoint(TRUNCATE)` قبل النسخ)؛ نسخ الملف الرئيسي وحده مع WAL غير مُفرَّغ قد يُفقد أحدث المعاملات. — §16.1.
- [ ] استعادة النسخة على بيئة معزولة وقياس زمن الاستعادة (لا نافذة بلا استعادة مجرَّبة).
- [ ] تأكيد أن **كل النسخ** تعمل بكود `PR-2` (لا نسخة قديمة تكتب `Kind = NULL`).
- [ ] فحص الصفر:

  ```sql
  -- SQLite (COALESCE ضروري: SUM على جدول فارغ يعيد NULL لا 0)
  SELECT COALESCE(SUM("Kind" IS NULL), 0),
         COALESCE(SUM("LastContentChangedAt" IS NULL), 0),
         COALESCE(SUM("Kind" IS NOT NULL AND TRIM("Kind") = ''), 0)
  FROM "HeadAlerts";

  -- PostgreSQL
  SELECT COUNT(*) FILTER (WHERE "Kind" IS NULL),
         COUNT(*) FILTER (WHERE "LastContentChangedAt" IS NULL),
         COUNT(*) FILTER (WHERE "Kind" IS NOT NULL AND TRIM("Kind") = '')
  FROM "HeadAlerts";
  ```

  النتيجة المطلوبة: `0 | 0 | 0` (حتى على جدول فارغ). أي قيمة غير صفرية توقف النشر (لا تُعالج بـ`UPDATE` عشوائي — راجع §6.5).

> **حدّ معروف في `Backfill` (M2):** شرط الإصلاح فيه هو `WHERE "Kind" IS NULL OR TRIM("Kind") = ''`، فصفٌّ بـ`Kind` مضبوط و`LastContentChangedAt = NULL` **لا يصلحه M2**. هذا مقصود ومقبول لأن فحص `0 | 0 | 0` أعلاه يكشفه قبل التطبيق، وحارس `M3` يرفض الهجرة برسالة صريحة إن فات الفحص. المعالجة المعتمدة بعد مراجعة الصف: `UPDATE "HeadAlerts" SET "LastContentChangedAt" = "CreatedAt" WHERE "LastContentChangedAt" IS NULL;` — وهي القيمة نفسها التي كان كود `PR-2` يستخدمها بديلًا (`?? CreatedAt`)، فلا تغيير دلالي.

### 6.2 تطبيق الهجرات بالترتيب على النسخة المستعادة

> **تصحيح 2026-10-26 (قبل التجميد):** المفتاح الفعلي لـSQLite هو `ConnectionStrings__DefaultConnection` وليس `ConnectionStrings__Default` (الأخير يُهمَل بصمت، فيسقط الأمر على `docgen.db` الحقيقي). ومصنع Postgres زمن-التصميم يقرأ `POSTGRES_CONNECTION` **حصرًا**؛ `Database__UsePostgres` لا يوجّه `dotnet ef`. نفّذ من مجلد `backend`، وتأكد قبل كل أمر أن `Data Source`/`Database` يشيران إلى النسخة المستعادة.

```powershell
# نفّذ من مجلد backend؛ splatting لخيارات المشروع في كل أمر.
$efArgs = @("--project", "src/DocGenerator.Infrastructure", "--startup-project", "src/DocGenerator.Api")

# SQLite (نسخة مستعادة في ملف منفصل — لا docgen.db الحقيقي)
$env:DATABASE_URL = $null
$env:Database__UsePostgres = "false"
$env:ConnectionStrings__DefaultConnection = "Data Source=C:\temp\pr3-dryrun.db"
$env:ASPNETCORE_ENVIRONMENT = "Development"   # لتجاوز حارس Program.cs في dry-run فقط، لا في الإنتاج
dotnet ef database update HeadAlertTaxonomy_Expand --context DocGeneratorDbContext @efArgs
dotnet ef database update HeadAlertOutbox_Create --context DocGeneratorDbContext @efArgs
dotnet ef database update HeadAlertTaxonomy_Backfill --context DocGeneratorDbContext @efArgs
dotnet ef database update HeadAlertTaxonomy_Contract --context DocGeneratorDbContext @efArgs

# PostgreSQL (قاعدة مرحلية مستعادة) — المصنع يقرأ POSTGRES_CONNECTION فقط
$env:POSTGRES_CONNECTION = "Host=...;Port=5432;Database=pr3_dryrun;Username=...;Password=..."
dotnet ef database update HeadAlertTaxonomy_Expand_Pg --context DocGeneratorPostgresDbContext @efArgs
dotnet ef database update HeadAlertOutbox_Create_Pg --context DocGeneratorPostgresDbContext @efArgs
dotnet ef database update HeadAlertTaxonomy_Backfill_Pg --context DocGeneratorPostgresDbContext @efArgs
dotnet ef database update HeadAlertTaxonomy_Contract_Pg --context DocGeneratorPostgresDbContext @efArgs
```

> الأسماء بلا `--no-build` تُطبِّق كل ما قبلها ضمنًا؛ ومع ذلك يُذكر كل اسم صراحةً لتوثيق الترتيب. `Contract` **آخرًا دائمًا**.
> **⚠️ قبل `Contract` على SQLite:** سجّل `SELECT seq FROM sqlite_sequence WHERE name = 'HeadAlerts';` — إعادة بناء الجدول التي يولّدها EF تعيد ضبط التسلسل على أعلى `Id` **موجود**، فإن كان أعلى `Id` محذوفًا تراجع التسلسل. أعِد القيمة المسجلة بعد `Contract` (الخطوة في §6.3).

### 6.3 التحقق بعد التطبيق على النسخة المستعادة

- [ ] SQLite: `PRAGMA integrity_check;` = `ok` و`PRAGMA foreign_key_check;` فارغ.
- [ ] `pragma_table_info('HeadAlerts')`: `notnull = 1` للعمودين و`dflt_value IS NULL`.
- [ ] وجود `CK_HeadAlerts_Kind_NotEmpty` في `sqlite_master`/`information_schema`، ووجود `UX_HeadAlerts_DedupeKey_Active` والفهارس الزمنية/النوعية.
- [ ] **SQLite — تسلسل `AUTOINCREMENT`:** القيمة المسجلة قبل `Contract` لم تتراجع بعد إعادة البناء. إن تراجعت، أعد رفعها بأمان (لا تُنقص القيمة الحالية):
  ```sql
  UPDATE sqlite_sequence
  SET seq = MAX(seq, <القيمة_المسجلة>)
  WHERE name = 'HeadAlerts';

  -- إن لم يكن هناك صف (جدول فارغ) أدرجه بالقيمة المسجلة إن كانت أكبر من صفر:
  INSERT INTO sqlite_sequence (name, seq)
  SELECT 'HeadAlerts', <القيمة_المسجلة>
  WHERE <القيمة_المسجلة> > 0
    AND NOT EXISTS (SELECT 1 FROM sqlite_sequence WHERE name = 'HeadAlerts');
  ```
- [ ] تطابق أعداد الصفوف وبصمة الأعمدة المفتاحية `(Id, BranchId, Kind, DedupeKey, CreatedAt, LastContentChangedAt)` مع ما قبل الهجرة، مع تقرير مفسَّر لكل فرق مقصود من `M2`.
- [ ] محاولة إدراج `Kind = ''`/`NULL` تفشل على مستوى القاعدة (اختبار سلبي واحد لكل سياق).
- [ ] إقلاع التطبيق على النسخة المستعادة + فحص دخان: تسجيل دخول كل دور، فتح `/alerts`، إنشاء تنبيه يدوي، تعليم قراءة، فتح تفاصيل نص طويل (12,000 حرف)، عدّاد الشارة، `/alerts/sent`.

### 6.3.1 إن انهار التطبيق بين دفعات SQLite (حالة وسيطة)

EF ينفّذ هجرة SQLite في **ثلاث معاملات** وسجل الهجرة يُكتب بعدها (§2.2)؛ فالانهيار في المنتصف يترك أحد الأشكال التالية. **لا تُعِد التشغيل قبل تحديد الحالة** بـ`sqlite_master` و`PRAGMA table_info('HeadAlerts')` و`PRAGMA index_list('HeadAlerts')`.

| الحالة (بعد اكتمال…) | العلامة المميّزة | المعالجة |
|---|---|---|
| الدفعة 1 (الحارس + النسخ) | `ef_temp_HeadAlerts` **و** `HeadAlerts` موجودان | `DROP TABLE "ef_temp_HeadAlerts";` ثم إعادة `Contract` — الحارس يعمل من جديد. البيانات الأصلية لم تُمسّ. |
| الدفعة 2 (`DROP`/`RENAME`) | `HeadAlerts` موجود بلا فهارس (`UX_HeadAlerts_DedupeKey_Active` مفقود) وبلا سجل هجرة | **المسار المعتمد:** استعادة النسخة الاحتياطية وإعادة الخطوات كاملة. البديل المُراجَع فقط: إعادة `Contract` (تعيد البناء والفهارس) مع إيقاف الكتابة وتأكيد عدد الصفوف قبل/بعد. |
| الدفعة 3 (الفهارس) وقبل سجل الهجرة | كل البنية سليمة (أعمدة `NOT NULL` + القيد + الفهارس) لكن `__EFMigrationsHistory` بلا المعرّف | إعادة `Contract` آمنة (تعيد البناء مرة أخرى) وتُفضَّل على إدراج سجل يدوي. |
| بعد سجل الهجرة | `PRAGMA table_info` + القيد + الفهارس سليمة | لا شيء — أكمل §6.3. |

> **قاعدة:** لا تُعِد تشغيل الهجرة على حالة وسيطة بلا فحص، ولا تفتح الكتابة قبل اجتياز §6.3 كاملًا. إن كان الشك في سلامة البيانات فالمسار الوحيد المقبول هو الاستعادة من النسخة الاحتياطية.

### 6.4 التطبيق في النافذة الحقيقية

1. إيقاف الكتابة/الخدمة (نافذة صيانة قصيرة — SQLite يعيد بناء الجدول).
2. تطبيق الثلاث المعلّقة على SQLite بالترتيب ثم `Contract`، وعلى PostgreSQL `Contract_Pg` فقط (الثلاث مطبَّقة).
3. تنفيذ §6.3 على القاعدة الحقيقية قبل فتح الكتابة.
4. **لا تنشر كود `PR-3` قبل نجاح الهجرة:** الكيان يقرأ `LastContentChangedAt` كـ`DateTime` غير قابل للفراغ؛ أي صف قديم بـ`NULL` يفشل عند القراءة. الترتيب: هجرة ← تحقق §6.3 ← نشر الكود ← فحص الدخان.
5. توثيق المخرجات في ملحق `12-…` (لا تُحرَّر هذه الوثيقة بعد التجميد).

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
| بناء + اختبارات الخلفية | `dotnet test --nologo` (من `backend/`) | ✅ تشغيل كامل 2757/2757 (2060 + 697)، ثم `Api.Tests` 698/698 بعد إضافة اختبار Postgres ⇒ 2758 (2060 + 698)، صفر فشل |
| نموذج SQLite مطابق | `dotnet ef migrations has-pending-model-changes --context DocGeneratorDbContext` | ✅ `No changes have been made to the model since the last migration.` |
| نموذج Postgres مطابق | `dotnet ef migrations has-pending-model-changes --context DocGeneratorPostgresDbContext` | ✅ `No changes...` (نفس الرسالة) |
| ترتيب الهجرات (SQLite) | `dotnet ef migrations list --context DocGeneratorDbContext --no-connect` | ✅ `…Backfill` ← `…Contract` أخيرًا |
| ترتيب الهجرات (Postgres) | `dotnet ef migrations list --context DocGeneratorPostgresDbContext --no-connect` | ✅ `…Backfill_Pg` ← `…Contract_Pg` أخيرًا |
| بوابات الواجهة (بلا تغيير واجهة في PR-3) | `npx oxlint src` · `npx tsc -b` · `npx vitest run` · `npm run build` | ✅ oxlint و`tsc -b` بلا ملاحظات · `vitest` 152/152 ملفًا و1599/1599 اختبارًا · `vite build` ناجح |
| اختبار `M3` الحيّ على PostgreSQL | `dotnet test backend/tests/DocGenerator.Api.Tests --filter "FullyQualifiedName~HeadAlertContractPostgresTests"` مع `DOCGEN_TEST_POSTGRES` | يُشغَّل في CI (وظيفة `backend-postgres`)؛ محليًا يعود فورًا (نجاح صامت) عند غياب المتغير — سلوك مطابق لاختبار Postgres القائم |

> **ملاحظة (محدَّثة):** `M3` على PostgreSQL مغطّى آليًا باختبار حيّ (`HeadAlertContractPostgresTests`) يثبت فرع الرفض ثم التطبيق الناجح وقيد القاعدة. يبقى فحص G2 (§6.3) إلزاميًا لأنه يثبت السلوك على **نسخة الإنتاج المستعادة** (بيانات وأحجام حقيقية) لا على قاعدة اختبار نظيفة.

> **ملاحظة رصد (CI):** ظهر في تشغيلين سابقين فشل عابر واحد في كل مرة (`MissingRoleClaimTests` ثم `CorrespondenceIntegrationTests`، وكلتاهما من `IsolatedApiTestBase`) واختفى بإعادة التشغيل؛ لا ارتباط له بـ`PR-3` (نجح التشغيل نفسه بعد إعادة المحاولة، ونجحت تشغيلات `master`). وسُدّت ثغرة رصد حقيقية: `dotnet test -v q` كان يكتم رسالة الفشل وتتبّعه كليًا، فأُضيف `--logger "console;verbosity=normal"` لكل خطوات اختبار الخلفية في `.github/workflows/ci.yml`.

---

## 8. المخاطر والقيود

| الخطر | المعالجة |
|---|---|
| تطبيق `Contract` قبل `PR-2` على كل النسخ | حارس `NULL` يمنع فقط البيانات المخالفة؛ **الترتيب التشغيلي إلزامي** (§16.1). |
| نسخة قديمة تكتب `Kind = NULL` بعد `Contract` | ستفشل كتابتها بـ`NOT NULL` — لذلك لا تُطبَّق `M3` قبل تحديث كل النسخ. |
| نافذة SQLite تعيد بناء الجدول | إيقاف الكتابة + نسخة احتياطية + `integrity_check` قبل إعادة الخدمة. |
| تشغيل `Contract` بلا G2 | محظور بهذه الوثيقة؛ G2 شرط دمج ونشر. |
| افتراض أن `dotnet test` المحلي يغطي Postgres | صحيح جزئيًا فقط: اختبار `HeadAlertContractPostgresTests` مبوَّب بـ`DOCGEN_TEST_POSTGRES` ويعود فورًا (نجاح صامت) عند غيابه محليًا؛ التنفيذ الحقيقي في CI (`backend-postgres`) وG2 (§6.3). |
| حدّ `TRIM` يقبل محارف بيضاء غير المسافة (`\t`/`\n`/NBSP) | الأنواع من كتالوج مغلق ولا مسار كتابة من مصدر خارجي — موثّق في §2.1؛ تشديد مطلوب قبل أي مصدر خارجي. |
| انهيار بين دفعات SQLite الثلاث (حالة وسيطة) | إيقاف الخدمة/الكتابة إلزامي؛ مسار الاستعادة في §6.3.1، والاستعادة من النسخة الاحتياطية عند الشك. |
| تهيئة `LastContentChangedAt = DateTime.UtcNow` في الكيان قد تُخفي نسيان الإسناد | مقبول موثّق: التهيئة تمنع `DateTime.MinValue` لكنها لا تكشف نسيان الإسناد (يُكتب «الآن» صحيحًا شكليًا). التخفيف: كل مسارات الإنشاء الحالية تُسند القيمة صراحةً (`CreatedAt`/`occurredAt`)، وأي مسار جديد يخضع للمراجعة واختبارات الخدمة. |

---

## 9. قائمة تسليم النشر (يُقرّها المشغّل)

- [ ] G2 منفَّذة على نسخة مستعادة، ومخرجاتها موثقة في ملحق.
- [ ] كل النسخ على `PR-2`.
- [ ] فحص الصفر `0 | 0 | 0` على القاعدة الحقيقية.
- [ ] نسخة احتياطية + استعادة مجرَّبة.
- [ ] تسجيل `SELECT seq FROM sqlite_sequence WHERE name = 'HeadAlerts';` قبل `Contract` (وأيضًا قبل `Down` إن لزم)، والتحقق من عدم تراجعه بعده (§6.2–§6.3).
- [ ] تطبيق `HeadAlertTaxonomy_Contract` (SQLite) و`HeadAlertTaxonomy_Contract_Pg` (PostgreSQL) بالترتيب.
- [ ] تحقق §6.3 + فحص دخان، وعدم بقاء `ef_temp_HeadAlerts` (§6.3.1).
- [ ] شطب أسطر الهجرات المطبَّقة من قائمة `RUN_GUIDE.md` §9 التراكمية بعد النجاح (في نافذة النشر هذه: 4 لهجرات SQLite + 1 لـPostgreSQL).

---

## 10. المراجعة النقدية (2026-10-26) — النتائج والقرارات

> نُفِّذت مراجعة سطرًا بسطر على الهجرتين والكيان والخدمات والاختبارات والأدلة التشغيلية. لا يوجد عيب **حرج** مفتوح؛ وما يلي كل ما رُصد مرتبًا بالخطورة مع قراره.

| # | الخطورة | الملاحظة | القرار / الحالة |
|---|---|---|---|
| 1 | مرتفع | هجرة SQLite غير ذرّية (3 معاملات + سجل الهجرة خارج معاملة): انهيار وسطي يترك حالة غير متسقة. | ✅ موثّق: إيقاف الكتابة إلزامي + مسار استعادة §6.3.1 + تحذير `RUN_GUIDE` §9. لا حل برمجي آمن داخل هجرة EF. |
| 2 | مرتفع | مسار **رفض** الحارس على PostgreSQL بلا تغطية آلية (لا Postgres محليًا). | ✅ أُصلح: `HeadAlertContractPostgresTests` حيّ في CI (`backend-postgres`) يثبت الرفض (`P0001`) وعدم تغيّر المخطط/البيانات، ثم التطبيق الناجح وقيد القاعدة. وقد كشف أول تشغيل حيّ أن الحارس يرفض أيضًا `LastContentChangedAt = NULL` بعد إصلاح `Kind` وحده، فأُعيد ترتيب الاختبار ليثبت **الفروع الثلاثة بالتدريج**: `Kind = NULL` ← إصلاحه فيبقى رفض الطابع ← إصلاح الطابع فيبقى رفض النوع الأبيض ← ثم التطبيق الناجح. |
| 3 | متوسط | `sqlite_sequence` يتراجع بعد إعادة البناء (في `Up` و`Down`) فيُعاد استخدام `Id` محذوف. | ✅ موثّق + خطوة تسجيل/إعادة رفع في G2 (§2.3، §6.2–§6.3). |
| 4 | متوسط | `TRIM` يزيل U+0020 فقط، ولا يرفض نوعًا صالحًا محاطًا بمسافات (`' legacy '`). | ✅ موثّق بحدّه ومسار تشديده (§2.1)؛ مقبول لأن الأنواع من كتالوج مغلق بلا مصدر خارجي. |
| 5 | متوسط | تهيئة `Kind = string.Empty` و`LastContentChangedAt = DateTime.UtcNow` قد تُخفي نسيان إسناد. | ✅ تحقّق: مسارا الإنشاء الوحيدان في `HeadAlertService` يُسندان الحقلين صراحةً؛ أي مسار جديد إمّا يفشل عند الحفظ (قيد `Kind`) أو يُراجع. |
| 6 | متوسط | قائمة `RUN_GUIDE` §9 التراكمية: **40 سطرًا غير مشطوب** مقابل **0 مشطوب**، بينما المعلّق فعليًا 5 (4 SQLite + 1 Postgres) — مصدرا حقيقة متعارضان. | ⏳ **مفتوح (توثيقي):** يُحسم في G2 من `__EFMigrationsHistory` (المصدر الحاكم) ثم تُشطب الأسطر المطبقة ويُضاف جدول حالة مؤرخ. لم تُعدَّل الأسطر الآن لعدم توفر قاعدة حيّة تُثبت حالة كل هجرة. |
| 7 | متوسط | فشل عابر في `IsolatedApiTestBase` (اختباران مختلفان في تشغيلين) بلا سبب جذري محدد. | ✅ سُدّت ثغرة الرصد (لوغر `console;verbosity=normal`)؛ يُراقب في CI. لا علاقة له بـ`PR-3`. |
| 8 | منخفض | فحص G1 كان يدويًا (قابل للنسيان). | ✅ أُفرض في CI: خطوتا `has-pending-model-changes` للسياقين. |
| 9 | منخفض | `NpgsqlConnection.ClearAllPools()` في اختبار Postgres الجديد يمسّ مجمّعات **كل** الاختبارات المتوازية. | ✅ أُصلح: `Pooling=false` لقاعدة الاختبار المعزولة وإزالة الاستدعاء العام. |
| 10 | منخفض | لا اختبار `Down` على PostgreSQL (فقط SQLite). | ⏸️ مقبول: `Down` ثلاث عمليات بسيطة (إسقاط قيد + إعادتان) ومغطّى على SQLite؛ لا يبرر قاعدة إضافية في CI. |

**تدقيق العقود (نموذج ← هجرة ← قاعدة):** `Kind` نص (`64`) غير قابل للفراغ و`LastContentChangedAt` زمن غير قابل للفراغ في `ModelSnapshot` و`Designer` والهجرتين والإعدادات — لا `nullable` ولا `Default` دائم في أي منها؛ والعقود العامة (`HeadAlertSummaryDto` غير المستخدم في PR‑3) بقيت كما هي، فلا تغيير API.

**تدقيق بقايا `nullable`:** لا بقايا `LastContentChangedAt ?? …` أو `Kind != null` في `src` بعد التعديل (بحث شامل)؛ و`HeadAlertRepository` يفلتر بالنوع مباشرةً.

**تكافؤ الدلالة مع `PR-2`:** إزالة البديل `?? CreatedAt` لا تغيّر السلوك لأن `Backfill` (M2) يسند `LastContentChangedAt = COALESCE(LastContentChangedAt, CreatedAt)` — أي القيمة نفسها. وصفٌّ شاذ بـ`Kind` مضبوط و`LastContentChangedAt = NULL` لا يصلحه M2 (شرطه مرتبط بـ`Kind`)، لكن حارس `M3` يرفضه وفحص G2 يكشفه (§6.1).

**سلامة مسار صندوق الصادر أثناء الترقية:** `HeadAlertOutboxMessage.Kind` يبقى `string?` (خارج نطاق M3)، لكن `HeadAlertOutboxDispatcher` يرفض أي رسالة `Upsert` بنوع فارغ/غائب **قبل** استدعاء الخدمة (سطر 388)، ويرفض تعارض نوع الرسالة مع نوع الطلب (سطر 392). فرسالة معلّقة قديمة لا تصل إلى قاعدة مشدّدة بلا نوع، ولا تُفشل النشر.

**حدود التغطية المعلنة:** الاختبار الحيّ لـPostgres لا يعمل محليًا بلا `DOCGEN_TEST_POSTGRES` (نجاح صامت مقصود، مطابق لنمط المشروع)، وG2 تبقى إلزامية لأنها تثبت السلوك على بيانات الإنتاج المستعادة لا على قاعدة نظيفة.
