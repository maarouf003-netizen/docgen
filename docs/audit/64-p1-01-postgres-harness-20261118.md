# `P1-01` (الشريحة الأولى) — منصة اختبار `PostgreSQL` حقيقية + `Respawn` — 2026-11-18

> نطاق هذه الجولة: إضافة مسار `WebApplicationFactory` على `PostgreSQL` حقيقي (هجرات
> `EF` لا `EnsureCreated`)، وإعادة ضبط بـ`Respawn`، ونقل مجموعة اختبارات المصادقة
> (`AuthIntegrationTests`، 17 اختبارًا) إليه، مع حارس `CI` يمنع تخطيها الصامت.
> لا تغيير في كود الإنتاج ولا هجرة جديدة.

## 1. المشكلة المقيسة

- `backend-postgres` في `CI` يشغّل `postgres:15` فعلًا، لكن `ApiFactory` كان يبني
  `SQLite` مؤقتة عبر `TestDatabase.UseSqlite` + `EnsureCreatedDatabaseInitializer`،
  فاختبارات الـ`API` كلها (≈1540) لا تلمس مزوّد الإنتاج.
- اختبارات `PostgreSQL` الحيّة القائمة كانت محدودة النطاق (مخطط/استعلام/تبديل وقت)،
  وليست اختبارات نهاية-لنهاية عبر المضيف الحقيقي.
- لا آلية إعادة ضبط بين الاختبارات على قاعدة مشتركة؛ الاعتماد كان على أسماء فريدة
  وترتيب تنفيذ ضمن المجموعة.

## 2. الحل

1. **`ApiFactory` بنقطتي توسّع** (`ConfigureDatabaseSettings`/`ConfigureDatabaseServices`)
   مع بقاء الافتراضي `SQLite` لكل الاختبارات القائمة — لا كسر لـ66 ملفًا.
2. **`PostgresApiFactory`**: ينشئ قاعدة فريدة `docgen_it_<guid>` من
   `DOCGEN_TEST_POSTGRES`، يضبط `ConnectionStrings:DefaultConnection` و
   `Database:AutoMigrate=true`، ويترك `DocGeneratorPostgresDbContext` و
   `DatabaseInitializer` الحقيقيين يطبّقان الهجرات ويبذران.
3. **`Respawn 7.0.0`**: `ResetAsync()` يفرّغ جداول `public` مع تجاهل
   `__EFMigrationsHistory`، ويعيد البذر عبر `DbSeeder.SeedAsync` بكلمة الاختبار
   `123456`. `WithReseed=true` لإعادة عدّادات الهوية.
4. **`PostgresFactAttribute`/`PostgresTheoryAttribute`**: تخطٍّ محلي نظيف عند غياب
   السلسلة. عند `DOCGEN_REQUIRE_POSTGRES=1` يبقى الاختبار مُكتشَفًا ويفشل عبر
   `RequireOrSkip` برسالة صريحة — **لا استثناء في الاكتشاف** لأنه يُسقط الاختبار
   ويعيد نجاحًا صامتًا (أُثبت سلبيًا).
5. **إسقاط آمن**: `DROP DATABASE ... WITH (FORCE)` (PostgreSQL 13+)، مع بقاء
   `Pooling=false` على سلسلة القاعدة المعزولة.
6. **نقل `AuthIntegrationTests`**: `IClassFixture<PostgresApiFactory>` +
   `IAsyncLifetime` (إعادة ضبط قبل كل اختبار) + `[PostgresFact]`.
7. **اختبار حارس للمصنع**: يثبت أن الهجرات مطبّقة (لا `EnsureCreated`)، وأن
   `ResetAsync` يمحو كتابة مسبار ويعيد البذر ويُبقي سجل الهجرات.
8. **حارس `CI`**: خطوة تقرأ `api-postgres.trx` وتتأكد أن
   `AuthIntegrationTests` و`PostgresApiFactoryConfigurationTests` نُفّذت ونجحت
   ولم تُتخطَّ.

## 3. المراجعة النقدية (عقبات حقيقية)

- **ثغرة الاكتشاف:** أول تنفيذ وضع `RequireOrSkip()` داخل باني سمة `Skip`؛ عند
  `DOCGEN_REQUIRE_POSTGRES=1` بلا سلسلة رمى استثناءً أثناء الاكتشاف، فأعاد `dotnet test`
  **نجاحًا** مع «No test matches». أُصلح بفصل `IsConfigured`/`IsRequired`، وبقي
  الاستثناء في المصنع/الجسم فقط.
- **`IAsyncLifetime` مع `IClassFixture`:** إعادة الضبط تجري لكل اختبار لا لكل صنف؛
  كلفتها المقيسة ≈2 ثانية/اختبار (17 اختبارًا ≈36–40 ثانية إجمالًا). مقبولة لهذه
  المجموعة، وستُقاس قبل تعميمها على مجموعات أكبر.
- **قيود `AuditLogs`:** `TRUNCATE` في `Respawn` لا يطلق مشغّلات `DELETE`، فلا تتعارض
  مع منع التعديل/الحذف في جداول التدقيق. تحقق عملي: إعادة الضبط تعمل مع المخطط الكامل.
- **التوافق المحلي:** لا Docker على جهاز التحقق؛ استُخدمت ثنائيات `PostgreSQL 16.4`
  محمولة على منفذ `55432`، و`CI` يبقى على `postgres:15`. لم يُضف أي أصل إلى المستودع.

## 4. التحقق

- `dotnet build backend/DocGenerator.sln -warnaserror`: **0 تحذير / 0 خطأ**.
- `dotnet format --verify-no-changes`: **نظيف**.
- المجموعة المنقولة على `PostgreSQL 16.4` محليًا: **19/19** (17 مصادقة + 2 حارس مصنع)
  في ≈40 ثانية.
- بدون `DOCGEN_TEST_POSTGRES`: **17/17 متخطّاة** (SQLite path غير متأثر).
- **تحقق سلبي 1:** `DOCGEN_REQUIRE_POSTGRES=1` بلا سلسلة → **17 فشلًا، exit=1**،
  والرسالة: «`DOCGEN_REQUIRE_POSTGRES=1` لكن `DOCGEN_TEST_POSTGRES` غير معرّف…».
- **تحقق سلبي 2:** `ResetAsync` بعد إدراج فرع مسبار `ZZZ` → الفرع يختفي، `DAM` يعود،
  وسجل `InitialPostgresBaseline` باقٍ.
- `DocGenerator.Api.Tests` كاملة على SQLite: **1529 ناجحًا + 5 `Sequential` ناجحة،
  19 متخطّاة** (المجموعة المنقولة وحدها) — 0 فشل.
- `DocGenerator.Application.Tests`: **2486/2486** · `Architecture.Tests`: **29/29**.
- الواجهة (بلا تغيير): `oxlint` نظيف · `tsc -b` · `npm run build` · `csp-guard` ·
  `vitest run` **1808/1808**.
- الحراس: `subhead` · `determinism` · `migration-watchlist` (5 هجرات مسجّلة) ·
  `migration-safety` · `secrets` · `dependency-audit` · `npm-audit` — كلها نظيفة.

## 5. الحدود

- **مجموعة واحدة فقط**؛ بقية مجموعات الـ`API` ما زالت على SQLite حتى نقلها في شرائح
  `P1-01b…`، ثم `P1-08` (إزالة SQLite من الاختبارات).
- **لا `Testcontainers`** في هذه الشريحة: `CI` يستخدم خدمة `postgres:15` القائمة،
  والتحقق المحلي احتاج Docker أو ثنائيات محمولة. إضافة `Testcontainers.PostgreSql`
  ممكنة لاحقًا لتحسين تجربة المطور، لكنها ليست شرطًا للـDoD الحالي.
- **`DocGenerator.Api.Tests` ما زالت تحمل مرجع `Microsoft.EntityFrameworkCore.Sqlite`**
  حتى `P1-08`.
- لا هجرة جديدة ولا تغيير عقد `API`/الواجهة.
