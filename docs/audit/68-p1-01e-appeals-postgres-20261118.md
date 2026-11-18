# `P1-01e` — نقل الاستئناف/المتدخلين/التصدير إلى `PostgreSQL` — 2026-11-18

> نطاق هذه الجولة: نقل `AppealForwardFlowTests` (3) و`AppealScopeMatrixTests` (1)
> و`DocumentInterventionIntegrationTests` (5) و`DocumentsExportIntegrationTests` (8)
> إلى `PostgreSQL`. لا تغيير في كود الإنتاج ولا هجرة جديدة.

## 1. المشكلة المقيسة

- الاستئناف والمتدخلون كانا على `SQLite`؛ `AppealForwardFlowTests` كان
  `IsolatedApiTestBase` (مصنع لكل اختبار)، والباقي في `ApiTestCollection` المشتركة.
- التصدير يُنشئ مستندًا مباشرة بلا `court` في اختبار الملحق؛ عند وجود دائرة في
  Postgres صار الطلب غير صالح (400 «دائرة التنفيذ مطلوبة»).

## 2. الحل

1. `AppealForwardFlowTests` → `PostgresApiTestCollection` + `Respawn` قبل كل اختبار.
2. `AppealScopeMatrixTests` → `PostgresApiFactory` مستقل + `[PostgresFact]`.
3. `DocumentInterventionIntegrationTests` → المجموعة المشتركة + إعادة ضبط.
4. `DocumentsExportIntegrationTests` → المجموعة المشتركة + إعادة ضبط + تهيئة دائرة
   «دمشق»، وإضافة `court` لاختبار تصدير الملحق.
5. حارس `CI` أُضيفت إليه أسماء المجموعات الأربع.

## 3. المراجعة النقدية

- **لماذا بقيت اختبارات حالة المستند على SQLite؟** `DocumentsStatusNotFoundContractTests`
  تستبدل `IDocumentStatusService` وتستخدم `WithWebHostBuilder`، و
  `RF001ConcurrentStatusCharacterizationTests` توصيف لتزامن قديم؛ كلاهما لا يضيف
  تغطية مزوّد جديدة، ونقلهما يخلط مخاطر `WithWebHostBuilder` مع منصة Postgres.
- **العزل:** `AppealForwardFlowTests` كان معزولًا لتجنّب تلوث تدقيق `head1`؛ إعادة
  الضبط تفرّغ `AuditLogs` قبل كل اختبار، فالعزل محفوظ.
- **الاستئناف:** لا يمرّر دائرة نصية؛ ينشئ دائرة عبر API ويستخدم معرفها، فلا يحتاج
  تعديل عقد.
- **دائرة التصدير:** التهيئة تُنشئ «دمشق» مرة لكل اختبار؛ أي مسار مباشر بلا `court`
  صار غير صالح، وأُضيف `court` للاختبار المتأثر.
- **لا تخفيف:** 17 حالة كما هي، بلا حذف/Skip.

## 4. التحقق

- المجموعات الأربع على `PostgreSQL 16.4`: **17/17**.
- كل مجموعات Postgres (السابقة + الجديدة): **190/190**.
- `Api.Tests` كاملة على `SQLite`: **1361 ناجحًا + 5 `Sequential`، 179 متخطّاة، 0 فشل**.
- `Application.Tests`: **2488/2488** · `Architecture.Tests`: **29/29**.
- الواجهة: `oxlint` نظيف · `tsc -b` · `npm run build` · `csp-guard` ·
  `vitest run` **1808/1808**.
- الحراس السبعة نظيفة · `dotnet build -warnaserror`: 0/0 ·
  `dotnet format --verify-no-changes`: نظيف.

## 5. الحدود

- المتبقي: التنبيهات/المراسلات/الدوائر/إدارة المستخدمين/البوابة/الجهات، ثم `P1-08`.
- لا هجرة جديدة؛ القائمة المعلّقة تبقى 4.
- اختبارات عقد الحالة/التزامن تبقى على SQLite بقرار موثّق أعلاه.
