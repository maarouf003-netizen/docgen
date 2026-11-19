# `P1-08/8a-4a` — التزامن المتفائل والنقل على `PostgreSQL` — 2026-11-19

> نطاق هذه الجولة: نقل `OptimisticConcurrencyTests` (23 حالة) و`TransferConcurrencyTests`
> (2) إلى `PostgreSQL` — **25 حالة** — كدفعة أولى من ملفات التزامن. لا تغيير إنتاجي
> ولا هجرة.

## 1. المشكلة المقيسة

- الصنفان كانا يبنيان `SQLite` (في الذاكرة أو ملفًا) لمحاكاة جلستين متزامنتين.
- تزامن `SQLite` يختلف جوهريًا عن `Postgres`: قفل ملف/قاعدة مقابل أقفال صفوف، و
  `DbUpdateConcurrencyException` من `WHERE Version = …` تُختبر بدلالة المزوّد.
- الحارس `P0-06` يخصّ الإنتاج (`Postgres`)، فلا معنى لاختباره على مزوّد آخر.

## 2. الحل

1. قاعدة `Postgres` معزولة لكل اختبار (`PostgresTestDb`)، وجلسات متعددة عبر
   `_pg.Options` (سياقات مستقلة على القاعدة نفسها) بدل اتصالات `SQLite` مشتركة.
2. لا `EnsureCreated` في الاختبار: القالب المشترك يوفّر المخطط.
3. كل الحالات `PostgresFact`/`PostgresTheory` (تخطٍّ ظاهر محليًا، فشل صريح في `CI`).

## 3. المراجعة النقدية

- **السلوك المقيس صار الحقيقي:** تحديث الرابح يرفع `Version` إلى 1، والخاسر
  (نسخة قديمة 0 في `WHERE`) يرمي `DbUpdateConcurrencyException` على `Postgres` —
  نجحت الحالة بلا تعديل توقّع.
- **`TransferOwnerAsync`:** التحديث الشرطي `expectedCreatedById` يفشل فعلًا بعد تغيّر
  المالك، والتحقق اللاحق يثبت بقاء القيمة الأولى — على أقفال `Postgres` لا على
  تسلسل `SQLite`.
- **حدود مقصودة:** بقية ملفات التزامن العابرة للكيانات (‏`DelegationConcurrency`,
  `AppealTransferAllConcurrency`, `CorrespondenceConcurrency`, `HeadAlertSystemWrite`,
  `HeadAlertOutboxDispatcher`, `DocumentInterventionLimitStaleTracking`, و
  `SecurityTests.ConcurrentFailures…`) تُنقل في `8a/4b` لأنها تحاكي سباقات متعددة
  الاتصالات ويجب قياس سلوك الأقفال/الجمود على `Postgres` دفعةً واحدة قبل الحكم.
- **لا تخفيف:** كل التوكيدات كما هي؛ لم يُحذف اختبار ولم يُغيَّر توقّع.
- **مهلة أوامر قاعدة الاختبار:** تحت التوازي الكامل تجاوز استعلام البحث في
  `PortalSearchMeasurementTests` مهلة `30s` (`Timeout during reading attempt`)،
  فرُفعت مهلة أوامر **قاعدة الاختبار فقط** إلى `180s` في `PostgresTestDb`. ليست
  مهلة إنتاجية، وهي **دليل إضافي** على كلفة الاستعلام الحالية (‏`P3-02`).

## 4. التحقق

- **الحالات على `PostgreSQL`:** **25/25** (‏`OptimisticConcurrencyTests` 23 مع
  توسّع النظرية، و`TransferConcurrencyTests` 2).
- **`Application.Tests` كاملة مع `Postgres`:** **2488/2488**، 0 متخطّاة — منها
  **41** حالة على `Postgres` فعليًا (‏16 من `8a/1`–`8a/2` + 25 هنا).
- **بدون `DOCGEN_TEST_POSTGRES`:** الصنفان يُتخطّيان صراحةً (‏`Skipped: 10` سمات).
- `dotnet build -warnaserror`: 0/0 · `dotnet format --verify-no-changes`: نظيف.
- **الحراس:** الخمسة نظيفة + `postgres-suite-guard --self-test` نظيف.
- **الواجهة:** `npm ci --legacy-peer-deps` (0 ثغرات) · `oxlint src` · `tsc -b` ·
  `vitest run` **1808/1808** · `npm run build` · `npm run guard` (`CSP`): 94 ملفًا — نظيفة.

## 5. الحدود

- `8a/4b` ثم `8a/5` (`TestDb` المركزي) ثم `8b` (`Api.Tests`) ثم `8c` (حذف الحزم).
- لا هجرة جديدة؛ القائمة المعلّقة تبقى **4**.
