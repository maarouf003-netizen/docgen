# `P3-01` — منصة قياس `PostgreSQL/Release` وقياس 10k/50k — 2026-11-21

> **النطاق:** بناء `harness` قياس مستقل على `PostgreSQL` حقيقي وبناء `Release`، بذر حتمي
> عند `10,000` و`50,000` مستند، وقياس الاستعلامات الساخنة (`p50`/`p95`) مع
> `EXPLAIN (ANALYZE, BUFFERS, FORMAT JSON)` ومحاولة `pg_stat_statements`.
> **لا تغيير في كود الإنتاج، ولا في `DTO`، ولا في `frontend/`، ولا في `ci.yml`، ولا هجرة جديدة.**
> **PR:** [#242](https://github.com/maarouf003-netizen/docgen/pull/242).

## 1. ما نُفِّذ

1. **مشروع مستقل:** `backend/tools/DocGenerator.Benchmarks` (تطبيق `console` على `net10.0`)،
   مضاف إلى `backend/DocGenerator.sln` تحت مجلد `tools`. يشير إلى
   `DocGenerator.Infrastructure` فقط، فلا يلمس `src` ولا الاختبارات.
2. **قاعدة قياس معزولة:** كل تشغيل ينشئ قاعدة `docgen_bench_p301_<scale>_<seed>_<timestamp>`،
   يشغّل **هجرات `DocGeneratorPostgresDbContext` الحقيقية** (`MigrateAsync`، لا `EnsureCreated`)،
   ثم يسقطها في النهاية (`DROP DATABASE ... WITH (FORCE)`) إلا مع `--keep-database`.
3. **بذر حتمي:** مولّد واحد ببذرة ثابتة (افتراضيًا `20261121`). لا وقت حقيقي في الحقول الدلالية،
   ولا `Random.Shared`/`new Random()` غير مثبّت. يحفظ معرّفات النطاق (فروع، دوائر، قيود بوابة،
   مستلم التنبيهات، مستند البوابة) في `SeedManifest` لتغذية الاستعلامات نفسها في كل تشغيل.
4. **تثبيت الإحصاءات:** بعد البذر يُنفَّذ `VACUUM (ANALYZE)` قبل أي قياس. هذا ليس تجميلًا:
   بدونه يخطط `PostgreSQL` بأرقام تقديرية قديمة فيختار أول استعلام خطة أسوأ بمئات المرات
   (انظر §4.1).
5. **بروتوكول القياس:** سياق `DbContext` جديد لكل تكرار (كطلب حقيقي)، `warmup` ثم `N` تكرارات،
   وحساب `p50`/`p95` بالاستيفاء الخطي (R-7). تُثبَّت النتيجة (عدد الصفوف + بصمة قصيرة) في كل
   تكرار؛ أي تغيّر يُفشل القياس بدل إخفائه.
6. **التقاط الخطط:** `DbCommandInterceptor` يسجّل SQL الفعلي ومعاملاته أثناء تنفيذ واحد، ثم
   يُعاد تنفيذ كل أمر بـ`EXPLAIN (ANALYZE, BUFFERS, FORMAT JSON)` على اتصال مستقل بنفس
   المعاملات. لا SQL يدوي موازٍ لاستعلامات الإنتاج.
7. **فحص `pg_stat_statements` بصدق:** فحص `pg_available_extensions` و`pg_extension`
   و`shared_preload_libraries`، ومحاولة التثبيت عند التوفر، وإعادة التصفير قبل القياس،
   وتسجيل الحالة صراحةً عند الغياب. **هذه البيئة لا تحوي الإضافة** (`pg_available_extensions`
   لا يعرضها)، فلم تُختلق أرقام.
8. **تقارير في المستودع:**
   - `docs/audit/measurements/20261121-p3-01-10000.json` + `.md`
   - `docs/audit/measurements/20261121-p3-01-50000.json` + `.md`

## 2. الاستعلامات الساخنة المقيسة

| المعرّف | المصدر الإنتاجي | الوصف |
|---|---|---|
| `documents-list-page1` | `DocumentRepository.SearchAsync` | قائمة المستندات الافتراضية (20 صفًا) مع شجرة `WithStandardIncludes` |
| `documents-list-page100` | `DocumentRepository.SearchAsync` | إزاحة عميقة (`Skip 1980`) لكشف كلفة الإزاحة |
| `documents-list-text` | `DocumentRepository.SearchAsync` + `DocumentPartySearch` | بحث نصي باسم طرف في قائمة المحامين |
| `portal-list-page1` | `PortalRepository.SearchScopedAsync` | قائمة بوابة الجهة العامة مع مسند الرؤية |
| `portal-list-text` | `PortalRepository.SearchScopedAsync` | بحث البوابة نصيًا |
| `portal-file-detail` | `PortalRepository.IsDocumentInScopeAsync` + `DocumentRepository.GetByIdAsync` | تفاصيل ملف البوابة (فحص النطاق + التحميل القياسي) |
| `stats-dashboard` | `StatisticsRepository.GetDashboardStatsAsync` | لوحة الإحصاءات (عدادات + مبالغ + مقترضون) |
| `stats-monthly` | `StatisticsRepository.GetMonthlyStatsAsync` | التجميع الشهري |
| `stats-branches` | `StatisticsRepository.GetBranchesSummaryAsync` | ملخص الفروع |
| `head-alert-summaries` | `HeadAlertRepository.ProjectSummariesAsync` | ملخص تنبيهات رئيس القسم لمستلم واحد (20 صفًا) |

## 3. النتائج

**البيئة:** `Windows 10.0.26200` · `.NET 10.0.10` · بناء `Release` · 8 معالجات منطقية ·
`PostgreSQL 16.4` (محمول، `shared_buffers=128MB`، `work_mem=4MB`، `random_page_cost=4`).
**البذرة:** `20261121` · إحماء `5` · تكرارات `20`.

### 3.1 عند `10,000` مستند

- بصمة البيانات: `d53ef0e6993c82c13d1f9da2e35e1a16` · زمن البذر `34.0s` · `93` خطة، `0` أخطاء.
- مكرّرة في تشغيل مستقل بنفس البصمة تمامًا (§5.2).

| الاستعلام | `p50` ms | `p95` ms | صفوف |
|---|---:|---:|---:|
| `documents-list-page1` | 85.575 | 91.228 | 20 |
| `documents-list-page100` | 127.506 | 135.218 | 20 |
| `documents-list-text` | 449.165 | 462.862 | 20 |
| `portal-list-page1` | 68.997 | 74.978 | 20 |
| `portal-list-text` | 190.149 | 194.341 | 20 |
| `portal-file-detail` | 71.333 | 79.334 | 1 |
| `stats-dashboard` | 41.820 | 90.941 | 1 |
| `stats-monthly` | 17.105 | 23.819 | 8 |
| `stats-branches` | 22.704 | 32.993 | 5 |
| `head-alert-summaries` | 3.823 | 4.650 | 20 |

### 3.2 عند `50,000` مستند

- بصمة البيانات: `a3f7e28ace7f054f2c74aa8c558f72d6` · زمن البذر `136.1s` · `93` خطة، `0` أخطاء.
- عدّ الجداول: `Documents=50,000` · `ApplicantPublicEntities=30,000` ·
  `ExecutedPublicEntities=20,000` · `Guarantors=16,667` · `HeadAlerts=5,000` ·
  `HeadAlertRecipients=12,500`.

| الاستعلام | `p50` ms | `p95` ms | صفوف |
|---|---:|---:|---:|
| `documents-list-page1` | 111.633 | 117.355 | 20 |
| `documents-list-page100` | 157.518 | 165.354 | 20 |
| `documents-list-text` | 1307.632 | 1317.049 | 20 |
| `portal-list-page1` | 252.665 | 257.622 | 20 |
| `portal-list-text` | 651.604 | 661.344 | 20 |
| `portal-file-detail` | 68.875 | 75.536 | 1 |
| `stats-dashboard` | 185.276 | 204.467 | 1 |
| `stats-monthly` | 79.870 | 113.918 | 36 |
| `stats-branches` | 109.786 | 128.719 | 5 |
| `head-alert-summaries` | 5.324 | 5.418 | 20 |

> الأرقام أعلاه منسوخة حرفيًا من ملفَّي `20261121-p3-01-{10000,50000}.json` المولَّدين آليًا
> (وليس من قياس يدوي). ملفات `.md` الملحقة تحوي ملخص الخطط لكل استعلام.

## 4. ما كشفته المراجعة النقدية (وما أُصلح)

### 4.1 فخّ «أول استعلام بعد بذر كثيف»: إحصاءات قديمة ⇒ خطة كارثية

أول تشغيل عند `10k` فشل بمهلة `30s` (`Npgsql`: `Timeout during reading attempt`) داخل
`DocumentRepository.SearchAsync`، رغم أن `SELECT count(*)` والاستعلامات البسيطة كانت أسرع من
`15ms`. تشخيص حيّ عبر `pg_stat_activity` أظهر أن استعلام تضمين `Guarantors` بقي `active` بلا
حدث انتظار لأكثر من `30s`، وبدأ `autovacuum: VACUUM ANALYZE` في اللحظة نفسها تقريبًا.

**السبب:** البذر يدرج مئات الآلاف من الصفوف في دقائق، فلا تكون `ANALYZE` قد حدّثت الإحصاءات؛
فيخطط `PostgreSQL` بأرقام تقديرية (جدول صغير/كبير) ويختار خطة أسوأ بمئات المرات.بعد أن
أنهى `autovacuum` التحليل تغيّرت الخطة، فبدت القراءة التالية `~100ms`.

**الإصلاح:** `VACUUM (ANALYZE)` صريح بعد البذر وقبل أي إحصاء أو قياس
(`BenchmarkDatabase.PrepareForMeasurementAsync`). أُعيد التشغيل بمهلة `30s` الافتراضية فنجح،
وبقيت النتائج مستقرة عبر كل التكرارات. **هذا شرط صحة للقياس، لا التفاف على بطء حقيقي.**

**دليل مساند:** `EXPLAIN (ANALYZE, BUFFERS)` يدوي للاستعلام نفسه بعد التحليل:
`Execution Time: 1.698ms` (الخطة في §4.3)، بينما قبل التحليل تجاوز `30s`.

### 4.2 نتيجة مباشرة للبند التالي (`P3-02`)

- `documents-list-text`: `p95 = 462.862ms` عند `10k` و`1317.049ms` عند `50k`.
- `portal-list-text`: `p95 = 194.341ms` عند `10k` و`661.344ms` عند `50k`.

هذان الرقمان يثبتان أن البحث النصي الحالي (`SearchText.Contains` + `OR` على عدة مجموعات أطراف)
هو أول هدف لـ`P3-02`. **دقة القراءة:** على `10k` يكون `documents-list-text` تحت حد
`p95 < 500ms` بالكاد (`462.9ms`، هامش ~`7%`)، والبوابة أسرع (`194.3ms`)؛ لكن عند `50k`
يتجاوز الحد بكثر (`1317ms` و`661ms`). لذلك `P3-02` لا يزال مطلوبًا: هامش `10k` هش،
والسلوك ينمو خطيًا تقريبًا مع الحجم. القرار مبني على قياس، لا تخمين.

### 4.3 ملاحظات خطط أخرى

- **قوائم المستندات:** الخطة الأساسية `Index Scan Backward using IX_Documents_CreatedAt` مع
  `Incremental Sort`، لكن `WithStandardIncludes` يولّد **18 أمرًا** (SplitQuery) لكل صفحة.
  هذا يفسّر `~90-120ms` ولا يعني `Seq Scan` على جدول كبير في المسار الأساسي.
- **البوابة:** `ScopePredicate` يُترجم إلى `EXISTS` على `ApplicantPublicEntities` و
  `ExecutedPublicEntities`؛ الخطة الحالية مقبولة عند `10k` لكنها تنمو خطيًا تقريبًا مع
  `50k` (`75ms → 258ms`).
- **`stats-dashboard`:** يستخدم `Seq Scan` مقصودًا ويجمع المبالغ **عميل-side** (`ToListAsync`
  لكل المستندات) لدقة `decimal`؛ لذلك `p95=204ms` عند `50k`. هذا مرشّح واضح لـ`P3-04`
  (Read models/إسقاطات) لا لعيب في المنصة.
- **`head-alert-summaries`:** ثابت تقريبًا (`4.65ms → 5.42ms`) لأن الفلترة على مستلم واحد
  مع فهارس `HeadAlertRecipients`؛ لا مشكلة أداء ظاهرة.
- **`pg_stat_statements`:** غير متوفرة في توزيعة `PostgreSQL` المحمولة
  (`pg_available_extensions` لا يعرضها)، فسُجّلت الحالة صراحةً في التقريرين بدل أرقام وهمية.

## 5. المراجعة النقدية الإلزامية بعد أول تشغيل أخضر

### 5.1 تدقيق العقود عبر المسار كاملًا

- لم يُلمس أي `DTO` أو `Controller` أو `Service` إنتاجي. `git diff --stat` للتغيير يقتصر على:
  `backend/DocGenerator.sln` + `backend/tools/DocGenerator.Benchmarks/**` +
  `docs/audit/**` + `docs/PERFECTION_ROADMAP.md` + `docs/README.md` + `RUN_GUIDE.md`.
- لا وجود لأي تعديل في `frontend/` ولا في `backend/src/**`.
- الحقول التي تقرأها المنصة هي نفس الحقول التي تقرأها الخدمات (لا نسخ SQL يدوي): مرّ القياس
  عبر `DocumentRepository`, `PortalRepository`, `StatisticsRepository`, `HeadAlertRepository`
  نفسها.
- **قرار:** لا حاجة لـ`contract-audit` لأن العقد لم يتغيّر؛ التحقق أعلاه يثبت عدم المساس به.

### 5.2 عدّ الاختبارات قبل/بعد

- **لا تغيير في أي مشروع اختبار**؛ أُضيف مشروع أدوات مستقل فقط. الأرقام الرسمية بعد تشغيل
  المصفوفة الكاملة: `Application` **2487/2487**، `Api` **1548/1548**، `Api/Sequential`
  **5/5**، `Architecture` **29/29**، `vitest` **1808/1808** — بلا فشل ولا تخطٍّ (مطابقة لخط
  الأساس). الأرقام مولَّدة بـ`scripts/test-report.mjs` من ملفات `trx`.
- **حتمية البذر** أُثبتت تجريبيًا: تشغيلان مستقلان عند `10k` أنتجا البصمة نفسها
  `d53ef0e6993c82c13d1f9da2e35e1a16` وعدد الصفوف نفسه في كل الجداول المرتبطة.

### 5.3 بقايا المصطلحات

`grep` على `TestDb`, `TestDatabase`, `UseSqlite`, `SqliteException`, `usePostgres`,
`ProviderName` داخل الملفات الجديدة: **صفر** نتائج. المنصة ترمي صراحةً إن غاب `PostgreSQL`؛
لا يوجد مسار «تخطٍّ صامت» ولا مزوّد بديل.

### 5.4 فروع المزوّد الصامتة

- المنصة تبني `DocGeneratorPostgresDbContext` بـ`UseNpgsql` حصرًا، ولا تقبل أي مزوّد آخر.
- لا أقفال صفوف أو استشارية تُخفى في مسار بديل: لم يُضف أي مسار تنفيذ إنتاجي.
- `pg_stat_statements` غيابه **معلن** في التقرير (`note`) ولا يُستبدل بأرقام تقديرية.

### 5.5 قائمة تحقق مقابل الخطة

| بند `P3-01` | الحالة |
|---|---|
| `harness` على `PostgreSQL`/`Release` | ✅ مشروع مستقل + `dotnet run -c Release` |
| مقياسا `10k` و`50k` | ✅ تشغيلان كاملان ببذر ثابت |
| زمن كل استعلام ساخن `p50`/`p95` | ✅ `20` تكرارًا بعد `5` إحماء |
| `EXPLAIN (ANALYZE, BUFFERS, FORMAT JSON)` | ✅ `93` خطة/تقرير، `0` أخطاء |
| `pg_stat_statements` | ✅ فُحصت؛ **غير متوفرة** في هذه التوزيعة وسُجّل ذلك صراحةً |
| بذر حتمي | ✅ بذرة ثابتة + بصمة `MD5` متطابقة بين تشغيلين |
| حفظ النتائج | ✅ `docs/audit/measurements/*.json` + `.md` |
| لا `SQLite`/`Debug` كحكم | ✅ `Release` + `Npgsql` فقط، والمنصة تطبع تحذيرًا في `Debug` |
| لا مساس بكود الإنتاج أو `ci.yml` | ✅ |

### 5.6 الاصطلاحات

- مشروع الأدوات خارج `src`/`tests` كما هو معتاد لأدوات التطوير؛ لا نمط جديد في كود الإنتاج.
- استُخدمت `PostgresTestDatabase` الاختبارية كمرجع للإنشاء/الإسقاط، لكن المنصة لا تعتمد على
  مشروع اختبار (لا مرجع إلى `TestSupport`).
- أرقام الاختبارات تُولَّد بـ`scripts/test-report.mjs` (لا كتابة يدوية).
- التواريخ في التقرير `UTC` (`DateTime.UtcNow`) داخل أداة قياس، وليست حقولًا دلالية في التطبيق.

### 5.7 الهجرات والنشر

- **لا هجرة جديدة.** لم يُعدَّل أي ملف في `MigrationsPostgres` ولا `DocGeneratorDbContext`.
- القائمة المعلّقة تبقى **4** كما هي:
  `20261114140029_AddIdempotencyResponseMetadata`،
  `20261114160957_P0_06_OptimisticConcurrency`،
  `20261114175636_P0_07_SafeConstraints`،
  `20261114211659_P0_07b_AmountConstraints`.
- أمر النشر يبقى:
  `dotnet ef database update --context DocGeneratorPostgresDbContext` (بعد
  `scripts/p0-07b-preflight.sql`). لا تُشغَّل `--context DocGeneratorDbContext` (لا هجرات له).

### 5.8 `CI` وحماية الفروع

- الفرع: `perf/p3-01-measurement-harness`؛ لا دفع إلى `master`.
- المشروع الجديد يدخل `dotnet restore --locked-mode` (له `packages.lock.json`)، و`dotnet build`
  و`dotnet format` و`dotnet test` كما بقية الحل.
- البوابات المطلوبة على الـ`PR` تبقى الثماني: `backend`, `frontend`, `guards`, `audit`,
  `backend-postgres`, `migration-guard`, `CodeQL (csharp)`, `CodeQL (javascript-typescript)`.

### 5.9 التوثيق التشغيلي

- أُضيف قسم تشغيل المنصة إلى `RUN_GUIDE.md` (أوامر التشغيل، شرط `Release`، وفحص
  `pg_stat_statements`).
- لم تكن هناك عبارة قديمة عن `SQLite` في `RUN_GUIDE.md` تحتاج تصحيحًا في هذا التغيير.

## 6. بوابات التحقق (المصفوفة الكاملة، 2026-11-21)

| البوابة | النتيجة |
|---|---|
| `dotnet build -warnaserror` | 0 تحذير / 0 خطأ |
| `dotnet format --verify-no-changes` | نظيف |
| `Application` / `PostgreSQL` | **2487/2487** · 0 فشل · 0 تخطٍّ · 11m12s |
| `Api` / `PostgreSQL` (`Category!=Sequential`) | **1548/1548** · 0 فشل · 0 تخطٍّ · 12m06s |
| `Api` / `Sequential` | **5/5** · 0 فشل · 0m02s |
| `Architecture` | **29/29** |
| الحراس الثمانية (`subhead`/`determinism`/`migration-watchlist`/`migration-safety`/`secrets`/`postgres-suite`/`test-report`/`npm-audit`) | نظيفة |
| بوابة التبعيات (`dependency-audit`) | نظيفة — لا High/Critical في 9 مشاريع |
| الواجهة: `oxlint` · `tsc -b` · `vitest` · `build` | 0 · 0 · **1808/1808** (171 ملفًا) · ناجح |
| منصة القياس (`--self-test`) | ✅ `self-test: ok` |

> الأرقام أعلاه مولَّدة بـ`node scripts/test-report.mjs <trx> --label "…"` من ملفات `trx`
> الفعلية، لا مكتوبة يدويًا.

## 7. الحدود

- القياس **محلي** على خادم واحد بلا حمل متزامن، وببيانات صناعية حتمية. الأرقام تثبت أن كل
  استعلام ساخن مقيس على المسار الحقيقي وبخطة قابلة للتدقيق، ولا تُقرأ كوعد زمن إنتاج.
- `pg_stat_statements` غير متاحة في هذه التوزيعة المحمولة؛ عند توفرها في `staging` تُعاد
  المنصة بلا تغيير وتملأ قسمها تلقائيًا.
- `EXPLAIN` يعيد تنفيذ الاستعلام مرة إضافية؛ وُضعت لقطة `pg_stat_statements` قبل التقاط الخطط
  حتى لا تتلوث أرقامها بهذه الإعادة.
- `stats-dashboard` يجمع المبالغ عميل-side لدقة `decimal` (سلوك إنتاجي قائم)؛ قياسه يعكس ذلك
  ولا يوصي بتغييره ضمن `P3-01`.
