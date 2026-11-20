# `P1-08/8b+8c` — `ApiFactory` على `PostgreSQL` وحذف `SQLite` نهائيًا — 2026-11-20

> نطاق هذه الجولة (مدموجة بقرار نطاق معتمد): جعل `ApiFactory` على `PostgreSQL` فقط،
> وتحويل مصانع المضيف السبعة، وحذف `TestDatabase`/`EnsureCreatedDatabaseInitializer`
> وحزم `SQLite` من `src`/`tests`، وإعادة تنظيم `CI`. لا هجرة جديدة.

## 1. ما نُفِّذ

1. **`ApiFactory` Postgres-only:** قاعدة فريدة لكل مصنع، هجرات EF الحقيقية
   (`Database:AutoMigrate=true`) + بذر التطوير، و`ResetAsync` عبر `Respawn`
   (مع تفريغ `IMemoryCache` وإعادة البذر). أُزيل `_dbPath` وراية `usePostgres`
   واستبدال السياق بـ`SQLite`. وبقي `PostgresApiFactory` غلافًا للتوافق حتى لا تتغيّر
   أسماء المجموعات/الأصناف.
2. **تبويب API:** **41** ملفًا اكتسب `[PostgresFact]`/`[PostgresTheory]` في هذه الجولة
   (المشروع كله صار **83** ملفًا مبوَّبًا)؛ فبلا `DOCGEN_TEST_POSTGRES` تُتخطّى صراحةً
   (901 ناجحة/600 متخطّاة) بدل فشل المصنع.
3. **مصانع المضيف السبعة:** `PostgresHostDatabase` جديدة (نسخة من قالب مهاجَر + إسقاط،
   أو قاعدة فارغة عند `migrate: false`) استُخدمت في
   `AllowedHosts`/`SecurityHeaders`/`Swagger`/`ProductionSpa`/حارس الإنتاج،
   و`DevelopmentEnvironmentGuard` اكتفى بسلسلة شكلية لأن الحارس يرفض قبل الاتصال.
   حُذف `TestDatabase` و`EnsureCreatedDatabaseInitializer`.
4. **حذف `SQLite` من `src`:** أُزيلت حزم `Microsoft.EntityFrameworkCore.Sqlite` و
   `SQLitePCLRaw.bundle_e_sqlite3` (ومن `Directory.Packages.props`)، وصار
   `DbExceptionClassifier` على `Postgres` وحده (`23505`/`23503`/`23514` + `40001`/`40P01`)،
   وأُزيل فرع `SQLite` من `DbLoginRateLimiter` (قفل المفتاح صار المسار الوحيد).
5. **`CI`:** `backend` = بناء/تنسيق/معمارية فقط (لا تكرار اختبارات بتخطٍّ صامت)؛
   `backend-postgres` = كل اختبارات القاعدة (Application + Api) على `PostgreSQL` مع
   `DOCGEN_REQUIRE_POSTGRES=1`، وجمع التغطية ورفعها هناك.

## 2. ما كشفه النقل (وأُصلح)

1. **`QueryCounter`** كان يُسجَّل داخل `TestDatabase.UseSqlite`؛ بعد إزالته فشلت ميزانيات
   N+1 السبعة بـ«No service». أُعيد تسجيله Singleton على سياق `Postgres` الحقيقي
   (إعادة تسجيل الخيارات مع `AddInterceptors`) في `ApiFactory.ConfigureDatabaseServices`.
2. **`UserBranchInvariantTests`** كانت تتوقّع نص `SQLite` («UNIQUE constraint failed:
   Users.BranchId»)؛ صار التحقق على `PostgresException 23505` واسم الفهرس
   `IX_Users_BranchId`.
3. **`StrikeOffReminders…FallsBackToCreatedAt`** كانت تبذر ملفًا مرقّمًا بلا دائرة؛
   `CK_Documents_FileNumberRequiresCircuit` (من المهاجر) رفضه. صار البذر ينشئ/يعيد
   استخدام دائرة للفرع، كما يفعل التطبيق.
4. **`ApiFactoryDatabaseConfigurationTests`** كانت تثبّت `EnsureCreated` و
   `AutoMigrate=false`؛ صارت تثبت `AutoMigrate=true` ووجود هجرة Baseline مطبقة.
5. **تعارض اعتماديات EF:** إزالة حزمة `Sqlite` (10.0.10) أسقطت الحد الأدنى الذي كان
   يرفع `Microsoft.EntityFrameworkCore` من 10.0.4 (حدّ مزوّد Npgsql 10.0.3) إلى 10.0.10،
   فظهر `MSB3277` في `TestSupport` و`Architecture.Tests`. الحل الصريح: `PackageVersion`
   لـ`Microsoft.EntityFrameworkCore` و`.Relational` = 10.0.10 وإشارة مباشرة في المشروعين،
   مع بقاء مزوّد Npgsql 10.0.3 (متوافق مع 10.0.10).
6. **`HeadAlertSummaryPostgresTests`** كانت تُرجع صامتة عند غياب السلسلة، وتقارن
   `SQLite` بـ`Postgres`. أُعيدت كتابتها توقعات صريحة على `Postgres` فقط بـ`[PostgresFact]`.
7. **`DatabaseInitializerTests`** كانت تعتمد `EnsureCreated` على `SQLite`. أُعيدت على
   `Postgres` بقاعدة معزولة: `autoMigrate=true` يُهاجر من الصفر، و`false` يبدأ مهاجَرًا.
   اختبار حارس R9 (ملف مرقّم بلا دائرة) أصبح يحتاج **إسقاط القيد** في قاعدة الاختبار
   لصنع الحالة التاريخية التي يحرسها التطبيق — موثَّق في الاختبار.
8. **اختبارات وحدوية** كانت تصنع `SqliteException` مصطنعة (`DbExceptionClassifierTests`،
   `GlobalExceptionHandlerTests`)؛ صارت تصنع `PostgresException` بالرموز نفسها.
9. **قالب قاعدة مهاجَر للاستنساخ:** `PostgresTestDb.CreateMigrated()` يشغّل `Migrate`
   مرة واحدة لكل تشغيل ثم يستنسخ (~1s بدل 5–8s هجرات لكل مصنع). مسار الهجرات الكامل
   يبقى مغطّى بـ`DatabaseInitializerTests` و`PostgresBaselineTests`، فالتسريع لا يستبدل
   التحقق. (بروتوكول `docs/P1-08-SQLITE-REMOVAL-PLAN.md` §5.7.)
10. **بقايا مزوّد في `src`:** `HeadAlertOutboxLock` كان يحمل راية `usePostgres` وفرع قفل
    محلي `SemaphoreSlim` بديل — وكل مستدعٍ يمرّر `true`. أُزيل الفرع والراية (بقي
    `SemaphoreSlim` كمانع إعادة دخول داخل العملية فقط)، وصار القفل الاستشاري هو المسار
    الوحيد، مع تحديث `DependencyInjection` واختبار المشغّل.
11. **تدرّج صامت عند غياب `Npgsql`:** `DocumentRepository.GetByIdForUpdateAsync` كان
    يعود لقراءة **بلا قفل صف**، و`TransactionRunner.AcquireScopeLockAsync` كان يتخطّى
    قفل النطاق، إن لم يكن المزوّد `Npgsql`. صار كلاهما يرفض المزوّد الآخر صراحةً
    (`InvalidOperationException`) بدل إخفاء سباق.
12. **رسائل المزوّد في اختبارات التنقية:** كانت تثبّت صيغة `SQLite Error 19: …`؛ صارت
    صيغة `Postgres` الحقيقية (`23505: duplicate key value violates unique constraint "IX_…"`)
    مع تأكيدات عدم كشف اسم القيد/الفهرس.

## 3. جذر عطل «انهيار مضيف الاختبار» (وأُصلح)

ظهرت في أول تشغيل كامل بعد نقل المصانع ظاهرتان: توقّف بلا تقدم (‏`CPU` شبه ساكن)،
وانتهاء المضيف عند ~900 حالة بلا استثناء ولا حدث `WER`/`.NET Runtime` في سجل النظام.
عُزل السبب بأخذ **لقطات مكدّس حيّة** لـ`testhost` أثناء التوقف (`dotnet-stack report`):

- عشرات الخيوط محجوبة في `PostgresHostDatabase.Dispose()` و`ApiFactory.Dispose(bool)`
  داخل `Task.GetAwaiter().GetResult()`.
- الإسقاط كان `await` داخل `PostgresTestDb.DisposeAsync`، واستمراريته تعود إلى
  `SynchronizationContext` الخاص بـxUnit (`MaxConcurrencySyncContext`) — وهو محجوب
  بالخيط الذي ينتظر الـ`Task` نفسه ⇒ **جمود كامل** يُنهي الجولة بلا استثناء.

**الإصلاح (بنية اختبار، لا سلوك إنتاج):**

1. `PostgresTestDb.Dispose()` متزامن (إسقاط عبر `PostgresTestDatabase.Drop` المتزامن)،
   و`DisposeAsync()` يفوّض إليه ويعيد `ValueTask.CompletedTask` — فلا `await` يلتقط السياق.
2. `PostgresHostDatabase` صار يعمل كاملًا بالمسارات المتزامنة
   (`PostgresTestDatabase.Create`/`Drop`، و`_clone.Dispose()`).
3. `ApiFactory.Dispose` يستدعي `_database.Dispose()`؛ والاختبارات الثلاثة التي كانت
   تكتب `pg.DisposeAsync().AsTask().GetAwaiter().GetResult()` صارت `pg.Dispose()`.

**نتيجة المراجعة:** هذا الجمود كان كامنًا في مسار `Dispose` القديم أيضًا (‏`DropAsync`
داخل `Dispose`)؛ كشفه تسريع المصانع لأن التخلص صار يحدث أكثر تكرارًا وتحت توازٍ أعلى.
وثُبّت في التعليقات كي لا يعود أحد إلى `await` داخل `Dispose` في المصانع.

**ملاحظة بيئية منفصلة (لا علاقة لها بالعطل):** تشغيل `Application` و`Api` **معًا** محليًا
على `max_connections=100` يُنتج `53300 too many clients`؛ لذلك البوابة الرسمية تبقى
تسلسلية كما في `CI`، والمشغّل المتوازي `scripts/test-backend-local.sh` موثَّق بشرط ≥300.

## 4. مراجعة نقدية بعد أول تشغيل أخضر (نتائجها وما أُصلح)

1. **نقص تغطية مُخفى في عدّ الاختبارات:** `Application.Tests` نزل من **2489** إلى
   **2484**. السبب ليس حذفًا عشوائيًا: حالتان (`Theory` بستّ حالات) كانتا تختبران رموز
   `SQLite` الموسّعة، فاستُبدلتا بحالتين فقط. النقص يعني ضياع تأكيد أن `23502`
   (‏NOT NULL) و`23514` (‏CHECK) **ليستا** تفردًا، وأن الرمز غير المعروف يبقى 500.
   أُعيدت التغطية بصيغة `Postgres`: `[Theory]` لـ`23503`/`23502`/`23514` + حالة
   `XX000` (‏fail-safe) ⇒ العدّ يعود **2487**. الفارق المتبقي (-2 عن 2489) مقصود: رمزا
   `SQLITE_CONSTRAINT_PRIMARYKEY/ROWID` لا مقابل لهما في `PostgreSQL` (التفرد كله
   `23505`)، وحالة «رمز 19 بلا رمز موسّع» يقابلها `XX000`.
2. **فرع مزوّد ثالث يُسقط قفلًا (لم يُرصد في الجولة الأولى):**
   `HeadAlertOutboxStore.ClaimCandidateAsync` كان يقرأ بلا `FOR UPDATE SKIP LOCKED` إن
   لم يكن المزوّد `Npgsql` — أي مسار «التقاط مزدوج» صامت لو غاب المزوّد. أُزيل الفرع،
   وصار الاستعلام الوحيد هو ذا `SKIP LOCKED`.
3. **دقّة نوع:** `PostgresTestDb` كان يملك `Dispose()` عامًّا بلا تطبيق `IDisposable`
   (يعمل مع `await using` فقط). أُضيف الواجهة صريحًا كي لا يُستعمل النمط الخطأ لاحقًا.

**فجوات مقبولة وموثّقة (لا تُغلق في هذه المرحلة):**

- `DatabaseInitializer.GetInvalidRegistryYearIdsAsync` يحتفظ بفرع C# بديل (تحقق قراءة
  فقط، لا قفل ولا كتابة) غير قابل للوصول بعد `P1-08`؛ إزالته تنظيف مستقل لا يغيّر سلوك
  `PostgreSQL` — تُترك لتجنّب لمس منطق تحقّق المخطط في جولة إعادة هيكلة.
- `postgres-suite-guard` يكتشف المجموعات عبر `PostgresApiFactory`/`Collection`/`new
  PostgresApiFactory`، ولا يكتشف صنفًا مُبوَّبًا بـ`[PostgresFact]` وحده
  (مثل `PostgresTestDbDisposeTests`). شبكة الأمان تغطي التخطّي (`NotExecuted`) لا
  «غياب الصنف من `trx`». الفجوة قديمة، وموثّقة هنا لتُعالج في توسيع الحارس.
- `Application.Tests` يبني قالبه بـ`EnsureCreated` (‏`docs/audit/79` §5): لا مشغّلات
  هجرة ولا `CK_Documents_FileNumberRequiresCircuit` هناك، ولا اختبار فيه يعتمد عليها؛
  طبقة القاعدة الحقيقية تُغطَّى في `Api.Tests` بمصنع مهاجَر.

## 5. التحقق

| البوابة | النتيجة |
|---|---|
| `dotnet build -warnaserror` | 0 تحذير / 0 خطأ |
| `dotnet format --verify-no-changes` | نظيف (`format=0`) |
| `Application` / `PostgreSQL` | **2487/2487** · 0 فشل · 0 تخطٍّ · 12m21s (بعد استعادة تغطية المصنّف) |
| `Api` / `PostgreSQL` (متوازية) | **1548/1548** · 0 فشل · 0 تخطٍّ |
| `Api` / `PostgreSQL` (`Sequential`) | **5/5** · 0 فشل |
| `Api` بعد إصلاحات المراجعة (‏`HeadAlert`/`Outbox`/حارس التنظيف/المعالج) | 35/35 |
| `Application` بلا `PostgreSQL` | 742 ناجحة · 1645 متخطّاة · 0 فشل |
| `Api` بلا `PostgreSQL` | 901 ناجحة · 600 متخطّاة · 0 فشل |
| `Architecture` | 29/29 |
| حارس الجمود `PostgresTestDbDisposeTests` | 1/1 |
| الحراس الثمانية (‏`subhead`/`determinism`/`migration-watchlist`/`migration-safety`/`secrets`/`postgres-suite`/`test-report`/`npm-audit`) | نظيفة |
| الواجهة: `oxlint` · `tsc -b` · `vitest` · `build` | 0 · 0 · **1808/1808** (171 ملفًا) · ناجح |

**زمن المصانع قبل/بعد:** تشغيل `Api` الكامل على `PostgreSQL` كان **37m58s**؛ صار
**19m33s** في التشغيل المعزول (و27m37s في التشغيل المذكور أعلاه لأنه تزامن مع
`vitest`/`build` للواجهة). الفرق من القالب المهاجَر: استنساخ ~1s بدل 5–8s هجرات
لكل مصنع.

**قراءة الأرقام بلا `PostgreSQL`:** التخطّي **ظاهر ومقصود** (`PostgresFact`)، لا فشل
ولا مرور صامت؛ وفي `CI` يمنع `DOCGEN_REQUIRE_POSTGRES=1` أي تخطٍّ داخل
`backend-postgres` (حارس `postgres-suite-guard.mjs` يتحقق من تنفيذ كل مجموعة).

## 6. الحدود

- `P1-08` مكتملة: لا `TestDb`/`TestDatabase`، ولا `UseSqlite`/`SqliteException` في
  `src`/`tests`، ولا فرع مزوّد بديل في `src`؛ وبقيت إشارات تاريخية في تعليقات توضيحية
  ورسالة حارس الإقلاع (`SQLite is not supported at runtime`) فقط.
- التالي: `P3-01` (‏`harness` قياس `PostgreSQL/Release`) ثم `P3-02` (بحث البوابة)،
  ثم `P6-06` — وفق ترتيب الخريطة.
- **لا هجرة جديدة**؛ قائمة المعلّقة تبقى **4**.
