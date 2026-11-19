# `P1-01i/a` — نقل الجهات والبوابة إلى `PostgreSQL` — 2026-11-19

> نطاق هذه الجولة: نقل `EntityRegistryHeadScopeIntegrationTests` (5) و`PortalScopeMatrixTests` (1)
> إلى `PostgreSQL`، **وتثبيت مهلة إسقاط قاعدة الاختبار المؤقتة** بعد قياس سبب فشل بيئي.
> لا تغيير إنتاجي ولا هجرة.

## 1. المشكلة المقيسة

- `EntityRegistryHeadScopeIntegrationTests` كانت على `ApiTestCollection` (`SQLite` مشتركة
  بلا إعادة ضبط)، و`PortalScopeMatrixTests` تنشئ `new ApiFactory()` خاصة (قاعدة `SQLite`
  مؤقتة) لكل اختبار.
- الحالات تقيس: نطاق رئيس القسم على محافظته، قراءة سجل التغييرات، حراس اقتراحات الأم،
  وعزل مندوب الجهة على ملفات البوابة (`User.PortalEntryId` على طرفَي المستند).
  أي أن جوهرها **قيود وعلاقات** (`PublicEntityGroup`/`PublicEntity`/`ApplicantPublicEntity`)،
  وهي بالضبط ما لا يفرضه `SQLite`.
- **ملاحظة أُغلقت هنا:** تشغيلان كاملان سابقان على الخادم المحمول فشلا في التنظيف
  (`DROP DATABASE ... WITH (FORCE)`) لأن مهلة `Npgsql` الافتراضية `30s` أقل من الـcheckpoint
  المفروض على قرص بطيء (قياس 39–40s موثّق في `docs/audit/71-p1-01h-alerts-20261118.md`).

## 2. الحل

1. المجموعتان → `PostgresApiTestCollection` + `IAsyncLifetime`/`ResetAsync` قبل كل اختبار،
   والسمات `PostgresFact` (تخطٍّ محلي صريح، وفشل صريح في `CI`).
2. `PortalScopeMatrixTests`: مصنع المجموعة يُستخدم كبديل مباشر لـ`new ApiFactory()`
   (بلا `Dispose` للمصنع المشترك) — فيبقى جسم المصفوفة كما هو.
3. `PostgresTestDatabase.DropAsync`: `CommandTimeout = 120` مع تعليق يشرح أن
   `DROP DATABASE` يفرض checkpoint قبل الإسقاط، فلا يُفشل التنظيفُ اختبارًا ناجحًا.
4. حارس `trx` في وظيفة `backend-postgres` أُضيفت إليه المجموعتان.
5. حُذف متغيّر محلي ميت (`savedDamGov`) كان في بذرة الاختبار، بلا تغيير سلوك.

## 3. المراجعة النقدية

- **البذور:** كل الحقول المطلوبة على `Postgres` مضبوطة صراحة (`CanonicalName`،
  `Governorate`، `BranchName`، `Status = Final`، `CreatedById`)، والقيود الفريدة
  تعمل لصالح الاختبار لا ضده (‏`GUID` في الأسماء المتكررة).
- **جبر النطاق:** تعديل `Branches.Governorate` لم يعد يتسرّب بين الاختبارات، لأن
  `Respawn` يعيد البذر قبل كل حالة — وهذا أقوى مما كان على `SQLite`.
- **عزل البوابة:** `totalCount == 1` ما زال حاسمًا لأن المسح الكامل يجعل القاعدة
  تبدأ من الصفر، مطابقًا لدلالة القاعدة المعزولة السابقة.
- **لا تخفيف:** 6 حالات كما هي، بلا حذف/Skip ولا تعديل توقعات.

## 4. التحقق

- **المجموعتان على `PostgreSQL`:** **6/6** (5 + 1).
- **كل مجموعات `PostgreSQL`** (23 مجموعة): **342/342** (‏336 السابقة + 6 الجديدة)،
  مع `DOCGEN_REQUIRE_POSTGRES=1` وبلا أي تخطٍّ.
- **`Api.Tests` كاملًا على `PostgreSQL`** (`Category!=Sequential`): **1546/1546**،
  ثم `Sequential`: **5/5**.
- **`Api.Tests` على `SQLite`** (بلا `DOCGEN_TEST_POSTGRES`): **1204 ناجحًا + 5
  `Sequential`، 317 متخطّاة (بوابات Postgres)، 0 فشل** — أي 1210 − 6 و311 + 6 كما هو
  متوقّع بعد النقل.
- **`Application.Tests`:** **2488/2488** · **`Architecture.Tests`:** **29/29**.
- `dotnet build -warnaserror`: 0 تحذير/0 خطأ · `dotnet format --verify-no-changes`: نظيف.
- **الحراس:** `subhead` · `determinism` · `migration-watchlist` (5 هجرات) ·
  `migration-safety` · `secrets` — كلها نظيفة.
- **الواجهة:** `npm ci --legacy-peer-deps` (0 ثغرات) · `oxlint src` · `tsc -b` ·
  `vitest run` **1808/1808** · `npm run build` · `npm run guard` (`CSP`): 94 ملفًا — نظيفة.

## 5. الحدود

- بقية `P1-01i` (الإحصاءات/التدقيق ثم عائلة `RF`) في دفعات لاحقة.
- لا هجرة جديدة؛ قائمة الهجرات المعلّقة تبقى **4**.
