# `P1-01d` — نقل مجموعات الإنابة إلى `PostgreSQL` — 2026-11-18

> نطاق هذه الجولة: نقل `DelegationsIntegrationTests` (15) و`DelegationScopeMatrixTests` (1)
> و`DelegationScopeIsolationTests` (2) إلى `PostgreSQL` / مجموعة الاختبار المشتركة.
> لا تغيير في كود الإنتاج ولا هجرة جديدة.

## 1. المشكلة المقيسة

- `SQLite` يسمح بإنابة بنص محكمة بلا دائرة مسجّلة؛ `PostgreSQL` لا يغيّر العقد، لكن
  وجود دائرة في القاعدة يجعل `DocumentDelegationService.ValidateAndBuildAsync` يطلب
  `DelegatedCircuitId` صريحًا ويرفض النص وحده:
  «دائرة الإنابة مطلوبة — اختر من القائمة».
- الاختبارات كانت تُرسل `delegatedCourt` نصيًا، ففشلت 8 حالات عند أول تشغيل
  (أغلبها JSON يفكّ جسم خطأ 400 كأنه `DelegationDto`).

## 2. الحل

1. `DelegationsIntegrationTests`: مجموعة Postgres + `Respawn` قبل كل اختبار، ودائرة
   الهدف «دائرة تنفيذ حلب» تُنشأ في `InitializeAsync` وتُمرَّر عبر
   `DelegatedCircuitId` في `SampleBody` (صار instance بدل static).
2. مسار الإنابة **الخارجية** يبقى بلا `DelegatedCircuitId` (العقد يرفض الدائرة
   الداخلية للإنابة الخارجية)، فأُضيف `DelegatedCircuitId = null` في `with`.
3. `DelegationScopeMatrixTests`: مصنع Postgres مستقل لكل اختبار (كان `ApiFactory`)،
   ودائرة «محكمة دمشق» تُمرَّر صراحةً.
4. `DelegationScopeIsolationTests`: انتقل من `IsolatedApiTestBase` إلى
   `PostgresApiTestCollection` + إعادة ضبط؛ كان يمرّر `delegatedCircuitId` أصلًا.
5. حارس `CI` أُضيفت إليه أسماء المجموعات الثلاث.

## 3. المراجعة النقدية

- **العقد لا النص:** التصحيح في بيانات الاختبار لا في الخدمة؛ العقد الحالي يمنع
  الالتباس بين دوائر متشابهة الأسماء ويضمن FK صحيحًا.
- **الإنابة الخارجية:** لو بقي معرف الدائرة في `with` لرُفض الطلب؛ أُفرغ صراحةً
  وأُبقي مسارها مغطّى.
- **العزل:** `DelegationScopeIsolationTests` كان مصنعًا لكل اختبار؛ صار مضيفًا مشتركًا
  مع إعادة ضبط قاعدة. حالته الوحيدة المشتركة هي محدّد المعدل (سقوف عالية) والساعة
  الثابتة؛ لا اعتماد عليها.
- **`DelegationScopeMatrixTests`** يحتفظ بمصنع مستقل لكل اختبار (قاعدة/هجرات لكل
  اختبار) لأنه يبني مصفوفة صلاحيات كاملة؛ الكلفة مقبولة لاختبار واحد.
- **لا اختبارات حُذفت أو خُفّفت:** 18 حالة كما هي، والتغيير في التهيئة/التمرير فقط.

## 4. التحقق

- المجموعات الثلاث على `PostgreSQL 16.4`: **18/18** في ≈3 دقائق.
- كل مجموعات Postgres (السابقة + الجديدة): **173/173**.
- `Api.Tests` كاملة على `SQLite`: **1378 ناجحًا + 5 `Sequential`، 162 متخطّاة،
  0 فشل**.
- `Application.Tests`: **2488/2488** · `Architecture.Tests`: **29/29**.
- الواجهة: `oxlint` نظيف · `tsc -b` · `npm run build` · `csp-guard` ·
  `vitest run` **1808/1808**.
- الحراس السبعة نظيفة · `dotnet build -warnaserror`: 0/0 ·
  `dotnet format --verify-no-changes`: نظيف.

## 5. الحدود

- بقية المجموعات (الاستئناف/الحالة/البوابة/الجهات/التنبيهات) ما زالت على `SQLite`
  حتى `P1-01e…`، ثم `P1-08`.
- لا هجرة جديدة؛ القائمة المعلّقة تبقى 4.
