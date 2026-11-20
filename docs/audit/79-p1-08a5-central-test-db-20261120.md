# `P1-08/8a-5` — إزالة `TestDb` المركزي ونقل `Application.Tests` إلى `PostgreSQL` — 2026-11-20

> نطاق هذه الجولة: حذف `TestDb` (‏`SQLite` في الذاكرة) من `Application.Tests`، ونقل كل
> أصناف القاعدة إلى `PostgreSQL` بـ`PostgresDbTest` (‏قاعدة معزولة لكل صنف + تصفير قبل
> كل حالة)، وتحويل سمات اختبارات القاعدة إلى `[PostgresFact]`/`[PostgresTheory]`،
> وتأهيل الاختبارات التي كانت تعتمد على `SQLite` خارج `TestDb`. لا هجرة جديدة.

## 1. المشكلة المقيسة

- `TestDb.Create()` كان يبني `SQLite` في الذاكرة لكل حالة: كلفة ~1ms وعزل كامل — وهو
  ما جعل ~2400 حالة تعمل بسرعة على مزوّد غير إنتاجي.
- النقل الحرفي إلى `PostgresTestDb` لكل اختبار **غير قابل للتطبيق**: قياس الاستنساخ
  `CREATE DATABASE … TEMPLATE` + الإسقاط ≈ **1.2s + 0.8s** لكل حالة ⇒ ~80 دقيقة
  إضافية لتشغيل واحد. وقد ظهر ذلك عمليًا في أول محاولة (تعليق ثم 41 دقيقة).
- `SQLite` كان يخفي فرقين سلوكيين حقيقيين: فرض أطوال `character varying`، وصياغة
  تخطيط الاستعلام (`EXPLAIN QUERY PLAN`).

## 2. الحل

1. **`PostgresDbTest`** (في `DocGenerator.Application.Tests`): قاعدة واحدة **لكل صنف**
   من القالب المشترك (‏`ConcurrentDictionary<Type, PostgresTestDb>`)، وتُصفَّر قبل كل
   حالة، ويُهيَّأ `_db` بعد التصفير؛ فيبقى بذر الحالة في باني الصنف كما هو.
2. **تصفير رخيص** في `PostgresTestDb.Reset()`: `DELETE` لكل الجداول تحت
   `session_replication_role=replica` (بلا فحص مفاتيح أجنبية) + إعادة تسلسلات بهوية
   واحدة (`SELECT setval(...), …`). القياس:
   - `TRUNCATE … RESTART IDENTITY CASCADE`: **~680ms** (ثم ~1.9s بلا RESTART)؛
     يُعيد إنشاء ملفات كل جدول/فهرس.
   - `DELETE` تحت `replica`: **~10–60ms**.
   - أول أمر على `backend` جديد: **~300–600ms** (تحليل/تخطيط)، وبقاء الاتصال دافئًا
     يخفض التصفير إلى **~15ms** ⇒ صار اتصال القاعدة مُجمَّعًا في `PostgresTestDb`.
3. **تجميع الاتصالات للسياقات**: كانت سلسلة كل قاعدة `Pooling=false` (لتسهيل الإسقاط)،
   فيفتح `EF` اتصالًا فيزيائيًا **لكل عملية** — قياس `SaveChanges` ~28ms بدل 2–3ms بعد
   التجميع. صار `Pooling=true` (‏`MaxPoolSize=4`، عمر خمول 15s)، والإسقاط ما زال
   `DROP … WITH (FORCE)`، وقالب القاعدة يُنشأ **غير مُجمَّع** كي لا يفشل
   `CREATE DATABASE … TEMPLATE` بجلسة خاملة على القالب.
4. **حذف `TestDb`** نهائيًا (`Create` + `CreateOptions`)، وتحويل كل `TestDb.Create()`
   في الطرق إلى `NewContext()` على قاعدة الصنف — إلا حيث كان العزل بين قاعدتين شرطًا
   حقيقيًا فصُرّح بقاعدة مستقلة (‏`RF006` بذرتان، تقريرا الإحصاءات).
5. **السمات**: كل صنف قاعدة صار `[PostgresFact]`/`[PostgresTheory]`؛ الاختبارات
   الوحدوية البحتة بقيت `[Fact]`/`[Theory]`.

## 3. الاختبارات التي كانت تعتمد على `SQLite` خارج `TestDb`

| الموضع | ما تغيّر |
|---|---|
| `SafeConstraintTests` | الكتابة الخام السالبة تُرفض بـ`PostgresException` (`23514`) مع اسم القيد، ومعاملات `@id` بدل `$id` |
| `SearchMeasurementTests` | `strpos` بدل `instr`، و`EXPLAIN` بدل `EXPLAIN QUERY PLAN`، وتثبيت بنيوي على `Seq Scan`/`Guarantors` |
| `HeadAlertTextLimitsTests` | `length()` على `PostgreSQL` بدل `SQLite` (نفس دلالة نقاط `Unicode`) |
| `TransferRedirectConcurrencyTests` | سياقان متزامنان على قاعدة `Postgres` بدل ملف WAL |
| `DbExceptionClassifierTests` | فصل وحدوي (رموز مصطنعة) عن صنف قاعدة حيّة لتعارض التفرّد/التزامن |
| `HeadAlertModelProviderTests` | فحص `DDL` لمزوّد واحد بعد سقوط مقارنة المزوّدين |

## 4. ما كشفه النقل (وأُصلح)

1. **`BorrowerName` 800 حرفًا فوق حد العمود (100)**: اختبار `SearchText` كان يضع أسماء
   أطول من أعمدة `character varying` ويمرّ على `SQLite` الذي لا يفرض الطول. صار
   الاختبار يستخدم قيمًا داخل الحدود ويُفيض نص البحث عبر عشرة ورثة (‏>1000).
2. **دلالة `TestDb.Create()` داخل طريقة**: كانت تعني **قاعدة مستقلة**، لا سياقًا على
   قاعدة الصنف. في `StatisticsRepositoryTests` (باني الصنف يبذر فرع «DAM») صار
   استبدالها بـ`NewContext()` يُدرج فرعًا مكررًا ⇒ صُرّح بقاعدة معزولة للحالتين.
3. **`RF006` بذرتان مختلفتان**: تحتاج قاعدتين فارغتين فعلًا ⇒ قاعدتان معزولتان.
4. **مهلة إنشاء القاعدة**: تحت التوازي تجاوز `CREATE DATABASE … TEMPLATE` الافتراضي
   30s ⇒ رُفعت مهلة أمر الإنشاء/الإسقاط إلى **120s**.
5. **اقتباس أسماء التسلسلات**: `pg_sequences` يعيد أسماء بحالة مختلطة
   (‏`"AppSuggestions_Id_seq"`)، فتمريرها بلا اقتباس يفشل؛ صار `setval` على معرّف مقتبس.

## 5. تدقيق مشغّلات/قيود الهجرات (`EnsureCreated` لا ينشئها)

- القالب يُبنى بـ`EnsureCreated` من `DocGeneratorPostgresDbContext`، فلا يحوي ما هو
  **SQL خام في الهجرة فقط**:
  - مشغّلات الإلحاق الأربعة (`trg_auditlogs_no_update/no_delete`،
    `trg_documentfieldchanges_no_update/no_delete`).
  - `CK_Documents_FileNumberRequiresCircuit`.
- **لا اختبار في `Application.Tests` يعتمد عليها**: `AppendOnlyGuardTests` يثبت الحارس
  التطبيقي قبل القاعدة، ومصنّف الأخطاء يختبر التعرف على اسم القيد نصيًا. أما طبقة
  القاعدة الحقيقية (مشغّلات/قيد المسار) فتُغطَّى في `Api.Tests` عبر المهاجر
  (`PostgresBaselineTests`) — وهذه فجوة تخطيط معروفة تُغلق في `8b` حين ينتقل
  `ApiFactory` إلى الهجرات، وتُوثَّق هنا بدل تركها صامتة.

## 6. التحقق

- **`Application.Tests` على `PostgreSQL`** (‏`DOCGEN_TEST_POSTGRES` + `DOCGEN_REQUIRE_POSTGRES=1`):
  **2489/2489**، 0 متخطّاة، 0 فاشلة (18m 55s).
- **بدون `DOCGEN_TEST_POSTGRES`:** **744 ناجحة، 1645 متخطّاة صراحةً، 0 فاشلة** —
  اختبارات القاعدة تُتخطّى ظاهرًا (`[PostgresFact]`) ولا تفشل، والوحدوية تعمل بلا قاعدة.
- **`Architecture.Tests`:** 29/29 · `dotnet build -warnaserror`: 0/0.
- **القياس الحاكم لتصميم العزل:** استنساخ قاعدة لكل حالة ≈ **2.0s** (إنشاء 1.2s +
  إسقاط 0.8s) ⇒ ~80 دقيقة؛ قاعدة لكل صنف + تصفير دافئ ≈ **15ms/حالة**.
- **`Api.Tests`:** على `PostgreSQL` **1546/1546** (0 متخطّاة) + **5/5** `Sequential`،
  و`postgres-suite-guard` على `api-postgres-8a5.trx`: **43 مجموعة** نُفِّذت بلا تخطٍّ؛
  وعلى `SQLite` (بلا متغيّر): **1107 ناجحة + 412 متخطّاة** + **5/5** `Sequential`.
- **الحراس:** `subhead` · `determinism` · `migration-watchlist` · `migration-safety` ·
  `secrets` (19 مطابقة خط أساس بعد حذف قيد `HeadAlertModelProviderTests` الذي زال
  مصدره) · `postgres-suite-guard --self-test` · `npm-audit-guard` (0 ثغرة) ·
  `dependency-audit` (0 High/Critical).
- `dotnet build -warnaserror`: 0/0 · `dotnet format --verify-no-changes`: نظيف.
- **الواجهة** (`npm ci --legacy-peer-deps`: 0 ثغرات): `oxlint src scripts` · `tsc -b` نظيف ·
  `vitest run` **1808/1808** في **171** ملفًا · `npm run build` ناجح ·
  `npm run generate:api` بلا تغيير في `generated.ts` · `npm run guard` (`CSP`): **94** ملفًا.

## 7. الحدود

- **`DbExceptionClassifierTests` الوحدوي** يحتفظ برموز `SQLite` المصطنعة (‏`2067/1555/2579`)
  لأن `DbExceptionClassifier` نفسه ما زال يصنّفها حتى `8c` (حذف الحزمة وفروع المزوّد
  معًا)؛ لا قاعدة `SQLite` تُفتح في أي اختبار بعد هذه الجولة.
- التالي: `8b` (‏`Api.Tests` على `Postgres` فقط، ومصانع المضيف السبعة) ثم `8c`
  (حذف حزم `SQLite` وتنظيف الأقفال و`CI`).
- **لا هجرة جديدة**؛ قائمة المعلّقة تبقى **4**.
