# ADR-0009 — قاعدة اختبار واحدة: `PostgreSQL` فقط

- **التاريخ:** 2026-11-19
- **الحالة:** معتمد (سيُطبَّق على مراحل ضمن `P1-08`)
- **يعتمد على:** `ADR-0001` (Postgres-only في التشغيل)
- **يلغي:** السماح المؤقت بـ`SQLite` في الاختبارات (المذكور في `ADR-0001` §3)

## السياق

`ADR-0001` أزال `SQLite` من مسار التشغيل وأبقاه **مؤقتًا** في الاختبارات، ثم نصّت
`P1-08` على إزالته نهائيًا. الواقع المقيس (‏`1ab3c51`):

- `backend/tests/DocGenerator.Api.Tests`: 29 مجموعة على `ApiTestCollection` (‏`SQLite`)،
  3 على `IsolatedApiTestBase`، ومصنعان خاصان (`QueryBudgetTests`, `RF016HealthTests`)،
  إضافة إلى `ApiFactory` الافتراضي على `SQLite` و`TestDatabase.UseSqlite`.
- `backend/tests/DocGenerator.Application.Tests`: 16 موضع `UseSqlite` مباشرًا،
  و`TestDb` المركزي (‏`CoreTests.cs`) يخدم ~80 ملفًا، و9 مجموعات تزامن تعتمد ملفات
  `SQLite`، وحارسان يقرآن `sqlite_master`/`PRAGMA`.
- **`src` نفسه ما زال يحمل حزم `Microsoft.EntityFrameworkCore.Sqlite` و
  `SQLitePCLRaw.bundle_e_sqlite3`** في `DocGenerator.Infrastructure` رغم أن لا كود
  إنتاجي يستخدمها (بقايا تاريخية في `packages.lock.json`).

المشكلة ليست تنظيفًا شكليًا: `SQLite` يتسامح فيما يرفضه `Postgres` (`timestamptz`
بلا `Kind`, `numeric`/`decimal`, قيود `CHECK`, الفهارس الجزئية، دلالات الاقتران في
التزامن). ولذلك كانت كل شريحة من `P1-01` تكشف انحرافات حقيقية عند النقل.

## القرار

1. **قاعدة الاختبارات = `PostgreSQL` حقيقي فقط** في المشروعين (`Api.Tests`,
   `Application.Tests`) وفي أي مجموعة مستقبلية.
2. **لا `EF InMemory`** كبديل: لا يفرض قيودًا ولا SQL، ويعيد بالضبط نوع التسامح
   الذي أُزيل.
3. **بنية مشتركة**: مشروع `backend/tests/DocGenerator.TestSupport` يحوي:
   - `PostgresTestEnvironment` (قراءة `DOCGEN_TEST_POSTGRES`/`DOCGEN_REQUIRE_POSTGRES`).
   - `PostgresTestDatabase` (إنشاء/إسقاط قاعدة باسم فريد، `Pooling=false`، مهلة
     إسقاط `120s` لأن `DROP DATABASE` يفرض checkpoint).
   - `PostgresFact`/`PostgresTheory` (تخطٍّ صريح محليًا، وفشل صريح في `CI`).
   - `PostgresTestDb` لحالات الوحدات التي تحتاج سياقًا: **قالب واحد لكل تشغيل**
     يُبنى بـ`EnsureCreated` من `DocGeneratorPostgresDbContext`، ثم كل اختبار
     **نسخة مستنسخة** بـ`CREATE DATABASE … TEMPLATE` (‏Kلفة إنشاء منخفضة)، مع
     إسقاط النسخ عند الخروج.
4. **الترحيل على مراحل** (‏PR لكل مرحلة، لا دفعة واحدة):
   - `8a` — `Application.Tests`: الدفعات: (1) اختبارات السياق/المعاملة/الوحدة،
     (2) حارسا المخطط يُعاد كتابتهما على `information_schema`/`pg_indexes`،
     (3) اختبارات القياس، (4) مجموعات التزامن، (5) `TestDb` المركزي.
   - `8b` — `Api.Tests`: نقل المجموعات المتبقية وجعل `ApiFactory` على `Postgres` فقط.
   - `8c` — حذف الحزم (`Microsoft.EntityFrameworkCore.Sqlite`,
     `SQLitePCLRaw.bundle_e_sqlite3`) من `src` و`tests`، وحذف `TestDatabase`،
     وتنظيف `packages.lock.json`، وتحديث `CI` (لا حاجة لتشغيل `Application.Tests`
     على مزوّدين).
5. **`DoD` نهائي:** `grep -ri sqlite backend/src backend/tests` لا يُرجع إلا تعليقات
   تاريخية في هجرة/`ADR` حيث يجب بقاؤها، ولا `UseSqlite` ولا حزمة.

## البدائل المرفوضة

- **إبقاء `SQLite` للوحدات**: يناقض `ADR-0001` و`P1-08`، ويُبقي حزمًا إنتاجية ميتة.
- **`EF InMemory`**: لا يحاكي القيود ولا أنواع الأعمدة، فيُنتج «أخضر كاذبًا».
- **قاعدة واحدة مشتركة + `Respawn` لكل اختبار وحدات**: `Respawn` يعتمد ترتيب
  العلاقات وقد يمسح بذورًا يحتاجها اختبار؛ الاستنساخ من قالب أبسط وأكثر حتمية،
  و`Respawn` يبقى في `Api.Tests` حيث المجموعة المشتركة مبنية عليه.

## الأثر

- زمن التشغيل يزيد قليلًا (إنشاء قاعدة مستنسخة لكل اختبار سياق) مقابل حتمية أعلى.
- الاختبارات تحتاج `DOCGEN_TEST_POSTGRES` محليًا؛ بدونه تُتخطّى صراحةً (وليس بصمت).
- `CI`: وظيفة `backend` تُبقي الوحدات النقية سريعة، و`backend-postgres` تُنفّذ كل
  اختبارات القاعدة فعليًا، وبوابة `postgres-suite-guard` تمنع التخطي الصامت.
