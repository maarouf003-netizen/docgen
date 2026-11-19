# خطة `P1-08` — إزالة `SQLite` من الاختبارات (والحزم من `src`)

> القرار المعماري في [`docs/adr/0009-postgres-only-tests.md`](adr/0009-postgres-only-tests.md).
> هذه الخطة تثبّت الجرد والمراحل وترتيبها؛ الحالة الحيّة في `PERFECTION_ROADMAP.md`.

## 1. الجرد المقيس (‏`1ab3c51`)

| الموضع | الحجم | الملاحظة |
|---|---|---|
| `Api.Tests` — مجموعات على `ApiTestCollection` | 29 ملفًا | `ApiFactory` الافتراضي على `SQLite` |
| `Api.Tests` — `IsolatedApiTestBase` | 3 | مصنع `SQLite` لكل صنف |
| `Api.Tests` — مصنع خاص | 2 | `QueryBudgetTests`, `RF016HealthTests` |
| `Api.Tests` — مصانع مضيف (`TestDatabase.UseSqlite`) | 7 | `AllowedHosts`, `SecurityHeaders`, `Swagger`, `DatabaseInitializer`, `ProductionDatabaseGuard`, `DevelopmentEnvironmentGuard`, `ProductionSpaFactory` |
| `Application.Tests` — `UseSqlite` مباشر | 16 | 9 منها مجموعات تزامن تعتمد ملفات `SQLite` |
| `Application.Tests` — `TestDb` المركزي | ~80 ملفًا | `CoreTests.TestDb` يخدم أغلب الاختبارات |
| `Application.Tests` — حارسا مخطط على `sqlite_master`/`PRAGMA` | 2 | `SchemaIntegrityTests`, `DocumentOwnerSchemaTests` |
| `src` — حزم `SQLite` | `DocGenerator.Infrastructure` | `Microsoft.EntityFrameworkCore.Sqlite` + `SQLitePCLRaw.bundle_e_sqlite3` بلا أي استخدام كودي |

## 2. البنية المشتركة (منجزة في `8a/1`)

`backend/tests/DocGenerator.TestSupport`:

- `PostgresTestEnvironment` — قراءة `DOCGEN_TEST_POSTGRES`/`DOCGEN_REQUIRE_POSTGRES`.
- `PostgresTestDatabase` — إنشاء/استنساخ/إسقاط قاعدة، `Pooling=false`، مهلة إسقاط `120s`.
- `PostgresFact`/`PostgresTheory` — تخطٍّ صريح محليًا وفشل صريح في `CI`.
- `PostgresTestDb` — **قالب واحد لكل تشغيل** يُبنى بـ`EnsureCreated` من
  `DocGeneratorPostgresDbContext`، ثم كل اختبار **نسخة مستنسخة**
  (`CREATE DATABASE … TEMPLATE`) مع إسقاط عند الخروج.

قُدِّم المشروع إلى `DocGenerator.sln`، ويستهلكه `Api.Tests` و`Application.Tests`
عبر `<ProjectReference>` و`<Using Include="DocGenerator.Testing" />`، وأُزيلت النسخ
الثلاث المحلية من `Api.Tests/TestHelpers`.

## 3. المراحل

| المرحلة | النطاق | الحالة |
|---|---|---|
| `8a/1` | البنية المشتركة + دفعة المعاملات/الوحدة/مالك المستند في `Application.Tests` | ✅ |
| `8a/2` | إعادة كتابة `SchemaIntegrityTests` على `information_schema`/`pg_indexes` | ✅ |
| `8a/3` | `PortalSearchMeasurementTests` على `Postgres` (‏`EXPLAIN`)؛ `SecurityTests` مع دفعة التزامن | ✅ (القياس) |
| `8a/4` | `SecurityTests` + مجموعات التزامن الثمانية (ملفات `SQLite` → نُسخ `Postgres`) | ⏳ |
| `8a/5` | `TestDb` المركزي (~80 ملفًا) → `PostgresTestDb`، وتخطّي صريح للاختبارات | ⏳ |
| `8b/1` | `ApiFactory` على `Postgres` فقط + المجموعات الـ29 والـ3 والمصنعان | ⏳ |
| `8b/2` | مصانع المضيف السبعة (`TestDatabase`) + حذف `TestDatabase` | ⏳ |
| `8c` | حذف الحزم من `src`/`tests`، تنظيف `packages.lock.json`، تحديث `CI` والوثائق | ⏳ |

## 4. قواعد التنفيذ

- **لا `InMemory`** ولا استبدال مزوّد: كل اختبار قاعدة يعمل على `PostgreSQL` حقيقي.
- كل اختبار قاعدة يُعلَّم `PostgresFact`/`PostgresTheory` (تخطٍّ ظاهر لا صامت).
- لا يُحذف حارس (مثل `SchemaIntegrityTests`) قبل كتابة مكافئه على `Postgres`
  وتشغيله فعليًا.
- بعد كل مرحلة: `dotnet build -warnaserror` + الاختبارات الكاملة على المزوّدين
  + `format` + الحراس + بوابات الواجهة، ثم `PR` مستقل.
- `DoD` نهائي: لا `UseSqlite` ولا حزمة `SQLite` في `src`/`tests`، ولا
  `TestDb`/`TestDatabase`، و`grep -ri sqlite` لا يُرجع إلا سياقًا تاريخيًا موثّقًا.
