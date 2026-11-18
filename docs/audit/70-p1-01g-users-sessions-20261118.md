# `P1-01g` — نقل إدارة المستخدمين والجلسات إلى `PostgreSQL` — 2026-11-18

> نطاق هذه الجولة: نقل `UserManagementIntegrationTests` (34 سمة → 36 حالة مع النظريات)
> و`LockoutIntegrationTests` (8) و`TokenRevocationIntegrationTests` (2) إلى
> `PostgreSQL`. لا تغيير إنتاجي ولا هجرة.

## 1. المشكلة المقيسة

- المجموعات الثلاث كانت على `ApiTestCollection` (SQLite مشتركة بلا إعادة ضبط).
- قيود الإنتاج (فرع إلزامي لأدوار الفروع، وحدانية اسم المستخدم، `Version`،
  `LoginAttempts`) لا تُختبر على المزوّد الحقيقي.

## 2. الحل

1. الثلاثة → `PostgresApiTestCollection` + `IAsyncLifetime`/`ResetAsync` قبل كل اختبار.
2. السمات → `PostgresFact`/`PostgresTheory` (لا Skip صامت في CI).
3. حارس `CI` أُضيفت إليه الأسماء الثلاثة.

## 3. المراجعة النقدية

- **`LockoutIntegrationTests` يستخدم `WithWebHostBuilder`:** مصنع Postgres يعيد استخدام
  قاعدة المجموعة (`_connectionString` محفوظ)، فلا تُنشأ قاعدة جديدة لكل اختبار قفل؛
  إعادة الضبط قبل كل اختبار تمسح `LoginAttempts`.
- **إدارة المستخدمين:** قيود Postgres (فرع إلزامي، وحدانية) طُبّقت كما هي؛ نجحت كل
  الحالات بلا تعديل سلوكي، ما يثبت أن بيانات الاختبار صالحة للإنتاج.
- **إبطال التوكن:** يعتمد على `TokenVersion` المخزَّن؛ نفس السلوك على المزوّدين.
- **لا تخفيف:** 46 حالة كما هي، بلا حذف/Skip، ولا تعديل توقعات.

## 4. التحقق

- المجموعات الثلاث على `PostgreSQL 16.4`: **46/46**.
- كل مجموعات Postgres (السابقة + الجديدة): **273/273**.
- `Api.Tests` على SQLite (تقسيم CI): **1273 ناجحًا (غير متسلسل) + 5 `Sequential`،
  260 متخطّاة، 0 فشل**. (تشغيل الكل دفعة واحدة محليًا يفشل في
  `EnricherMiddlewareTests` لأنها تستبدل `Log.Logger` العام ويجب أن تُشغَّل وحدها —
  وهي تمرّ 5/5 منفردة، وهذا سبب تقسيم CI المعروف لا انحدار من هذه الشريحة.)
- `Application.Tests`: **2488/2488** · `Architecture.Tests`: **29/29**.
- الواجهة: `oxlint` نظيف · `tsc -b` · `npm run build` · `csp-guard` ·
  `vitest run` **1808/1808**.
- الحراس السبعة نظيفة · `dotnet build -warnaserror`: 0/0 ·
  `dotnet format --verify-no-changes`: نظيف.

## 5. الحدود

- المتبقي: التنبيهات (51) والبوابة/الجهات، ثم `P1-08` لإزالة `SQLite`.
- لا هجرة جديدة؛ القائمة المعلّقة تبقى 4.
