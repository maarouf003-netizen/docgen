# `P1-01f` — نقل الدوائر التنفيذية والمراسلات إلى `PostgreSQL` — 2026-11-18

> نطاق هذه الجولة: نقل `ExecutionCircuitsIntegrationTests` (16)
> و`CorrespondenceIntegrationTests` (16) و`ReviewCorrespondenceScopeTests` (5)
> إلى `PostgreSQL`. `ExecutionCircuitsBranchGuardTests` اختبار وحدة بلا قاعدة
> (متحكم + خدمة مزيفة) فبقي كما هو. لا تغيير إنتاجي ولا هجرة.

## 1. المشكلة المقيسة

- 16 فشلًا في المراسلات عند أول تشغيل: بذر `InitializeAsync` يُنشئ
  `Document` بـ`FileNumber` بلا `ExecutionCircuitId`، فيرفضه قيد
  `CK_Documents_FileNumberRequiresCircuit` (23514). على SQLite لم يكن القيد موجودًا.
- `ExecutionCircuitsIntegrationTests` كان مجموعته الخاصة بمصنع `ApiFactory` (SQLite)،
  وهي مجموعة يجب أن تبقى معزولة لأن سجل الدوائر يغيّر مطابقة «القفل من السجل».

## 2. الحل

1. `ExecutionCircuitsIntegrationTests`: مجموعة `ExecutionCircuitsCollection` صارت
   `ICollectionFixture<PostgresApiFactory>` + `IAsyncLifetime`/`ResetAsync` قبل كل اختبار.
2. `CorrespondenceIntegrationTests`: من `IsolatedApiTestBase` إلى
   `PostgresApiTestCollection` + إعادة ضبط في بداية `InitializeAsync` قبل البذر.
3. بذر المراسلات: إنشاء `ExecutionCircuit` في فرع دمشق وربط المستند به قبل الحفظ.
4. `ReviewCorrespondenceScopeTests`: من `IsolatedApiTestBase` إلى المجموعة المشتركة
   + إعادة ضبط.
5. حارس `CI` أُضيفت إليه أسماء المجموعات الثلاث.

## 3. المراجعة النقدية

- **القيد لا الخدمة:** الإصلاح في بيانات البذر (دائرة حقيقية) لا في القيد؛
  Postgres كشف بذرًا غير صالح كان SQLite يمرّره.
- **الدوائر معزولة:** المجموعة تحتفظ بقاعدة/مصنع مستقل لأن اختباراتها تفترض سجلًا
  فارغًا؛ `ResetAsync` قبل كل اختبار يضمن ذلك دون تداخل مع بقية المجموعات.
- **المراسلات:** إعادة الضبط تسبق البذر، فلا تتضاعف المستخدمون/المجموعات بين الاختبارات.
- **`ExecutionCircuitsBranchGuardTests`** لا يلمس قاعدة؛ نقله كان سيزيد زمنًا بلا قيمة.
- **حارس `npm audit`:** أثناء التحقق ظهر أن استثناء `GHSA-fc5q-39xc-vjpf` القديم لم يعد
  يظهر (0 ثغرات بعد `npm ci`)، فرفضه حارس الاستثناءات القديمة. حُذف من
  `scripts/npm-audit-allowlist.json` وحُدّثت الخريطة؛ اللقطة `56` التاريخية لم تُحرَّر.
- **لا تخفيف:** 37 حالة كما هي، لا حذف/Skip.

## 4. التحقق

- المجموعات الثلاث على `PostgreSQL 16.4`: **37/37**.
- كل مجموعات Postgres (السابقة + الجديدة): **227/227**.
- `Api.Tests` كاملة على `SQLite`: **1324 ناجحًا + 5 `Sequential`، 216 متخطّاة، 0 فشل**.
- `Application.Tests`: **2488/2488** · `Architecture.Tests`: **29/29**.
- الواجهة: `oxlint` نظيف · `tsc -b` · `npm run build` · `csp-guard` ·
  `vitest run` **1808/1808**.
- الحراس السبعة نظيفة (وحُذف استثناء `npm audit` الميت — 0 ثغرات) ·
  `dotnet build -warnaserror`: 0/0 · `dotnet format --verify-no-changes`: نظيف.

## 5. الحدود

- المتبقي: التنبيهات/إدارة المستخدمين/البوابة/الجهات، ثم `P1-08`.
- لا هجرة جديدة؛ القائمة المعلّقة تبقى 4.
