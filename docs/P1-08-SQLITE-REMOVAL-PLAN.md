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
| `8a/4` | `SecurityTests` + مجموعات التزامن الثمانية (ملفات `SQLite` → نُسخ `Postgres`) | ✅ |
| `8a/4a` | `OptimisticConcurrencyTests` + `TransferConcurrencyTests` (25 حالة) | ✅ |
| `8a/4b` | بقية التزامن: `Delegation`/`AppealTransfer`/`Correspondence`/`HeadAlert`×2/`Intervention` + `SecurityTests` | ✅ |
| `8a/5` | `TestDb` المركزي (~80 ملفًا) → `PostgresTestDb`، وتخطّي صريح للاختبارات | ✅ |
| `8b+8c` | `ApiFactory` على `Postgres` فقط + المجموعات الـ29 والـ3 والمصنعان، ثم مصانع المضيف السبعة وحذف `TestDatabase`، ثم حذف حزم `SQLite` وتنظيف `packages.lock.json` و`CI` والوثائق | ✅ |

## 4. قواعد التنفيذ

- **لا `InMemory`** ولا استبدال مزوّد: كل اختبار قاعدة يعمل على `PostgreSQL` حقيقي.
- كل اختبار قاعدة يُعلَّم `PostgresFact`/`PostgresTheory` (تخطٍّ ظاهر لا صامت).
- لا يُحذف حارس (مثل `SchemaIntegrityTests`) قبل كتابة مكافئه على `Postgres`
  وتشغيله فعليًا.
- بعد كل مرحلة: `dotnet build -warnaserror` + الاختبارات الكاملة على المزوّدين
  + `format` + الحراس + بوابات الواجهة، ثم `PR` مستقل.
- `DoD` نهائي: لا `UseSqlite` ولا حزمة `SQLite` في `src`/`tests`، ولا
  `TestDb`/`TestDatabase`، و`grep -ri sqlite` لا يُرجع إلا سياقًا تاريخيًا موثّقًا.

## 5. بروتوكول التنفيذ الاقتصادي (معتمد 2026-11-20)

> الهدف: تقليص زمن الإنجاز مع **إبقاء** الهدف كما هو. لا يُخفَّف أي معيار قبول:
> البوابات الست، صفر تخطٍّ في `backend-postgres`، `format`/`warnaserror`، لقطات
> `docs/audit`، والمراجعة النقدية — كلها لازمة لكل مرحلة.

1. **تكرار محلي مرشَّح:** أثناء التطوير شغّل `dotnet test --filter` للأصناف/المناطق
   المتأثرة فقط (ثوانٍ)، ثم **مصفوفة كاملة واحدة** قبل `PR`. لا تُشغَّل المصفوفة
   الكاملة بعد كل تعديل تصميمي.
2. **بيئة قاعدة مطابقة لـ`CI`:** `PostgreSQL` عبر `Docker`/`WSL2` مع `fsync=off`
   و`shared_buffers` مضبوطة — تتجنّب حد الذاكرة المشتركة على `Windows` وفحص
   الملفات. القياس المحلي يقترب من زمن `CI` بدل أن يكون 2–4× أبطأ.
3. **دمج `8b`+`8c` في مرحلة واحدة** (فرع واحد بكومتين: بنية `ApiFactory`/المجموعات،
   ثم حذف الحزم/الأقفال/`CI`): كلتاهما بنية اختبار/`CI` ولا عمل إنتاجي بينهما،
   فدمجهما يوفّر دورة تصميم + مصفوفة + وثيقة + مراجعة كاملة.
4. **`P3-01` بالتوازي:** يبدأ `harness` القياس على فرع مستقل أثناء `8b+8c` لأنه لا
   يغيّر كود الإنتاج؛ ويُؤجَّل `P3-02` إلى ما بعد `8c` لتفادي إعادة كتابة اختبارات
   البحث مرتين. لا يُلمس `ci.yml` إلا في `8c`.
5. **أتمتة أرقام التحقق:** `node scripts/test-report.mjs <trx>` يبني كتلة «التحقق»
   (الإجماليات/المتخطّاة/المدة/الفاشلة) من ملف `trx` بدل النسخ اليدوي، مع
   `--self-test` في الحراس.
6. **موازاة المسارات المستقلة:** تُشغَّل بوابات الواجهة والحراس أثناء انتظار
   `backend-postgres` محليًا، دون تعديل نفس الملفات من مسارين.
7. **قالب قاعدة مهاجَر للمصانع (معتمد 2026-11-20):** `ApiFactory` يستنسخ قاعدة
   **مهاجَرة مرة واحدة** (`Migrate` على قالب) بدل تشغيل الهجرات في كل مصنع
   (~1s استنساخ بدل 5–8s هجرات). يبقى **مصنع/اختبار واحد على مسار الهجرات الكامل**
   (`DatabaseInitializerTests` + `PostgresBaselineTests`) لإثبات تطابق المخطط، فلا
   يُستبدل التحقق — يُسرَّع فقط.
   **شرط بنيوي مرافق:** كل تنظيف في المصانع (`Create`/`Dispose` والقالب) **متزامن**؛
   `await` داخل `Dispose` يلتقط سياق xUnit المحجوب فيُجمِد الجولة كاملة
   (السبب الجذري والتشخيص في `docs/audit/80-…` §3).
8. **مشغّل خلفية محلي متوازٍ (معتمد 2026-11-20):** `scripts/test-backend-local.sh`
   يشغّل `Application` و`Api` (المتوازية) **في الوقت نفسه** على `PostgreSQL` محلي،
   ثم يجمّع النتائج ويولّد كتلتَي `test-report`. يُستخدم عبر `WSL2`/Linux تفاديًا
   لحدّ الذاكرة المشتركة على `Windows`، ويُبقي `Sequential` بعد البقية.
   **شرط سعة:** يحتاج الخادم المحلي `max_connections` ≥ 300 (عشرات قواعد الأصناف ×
   تجميع الاتصالات)؛ وإلا فشل بـ`53300 too many clients` — والبوابة الرسمية تبقى
   **تسلسلية** كما في `CI`. مضبوط `MaxPoolSize=2` لكل قاعدة لتقليل البصمة.

