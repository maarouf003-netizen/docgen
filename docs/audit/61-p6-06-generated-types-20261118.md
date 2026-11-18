# `P6-06` (الجزء الأول) — أنواع مولَّدة من `OpenAPI` وحارس انحراف — 2026-11-18

> نطاق هذه الجولة: جعل المخطط صالحًا للتوليد (`required` من nullability)، توليد أنواع
> `TS` من لقطة `OpenAPI`، حارس انحراف في `CI`، وإحلال أول نوع. لا تغيير في سلوك
> الواجهة أو الـ`API`.

## 1. المشكلة المقيسة

- `Swashbuckle` كان يصدّر **كل** خصائص الـ`DTO` اختيارية: أنواع القيم (`int`/`bool`/`long`)
  وسجلات `record` الموضعية لا تُوسم `required`، ومراجع `string` كانت `string | null`.
- النتيجة: توليد `TS` يعطي `name?: string | null` بينما العقد الفعلي `name: string` ⇒
  **إحلال الأنواع المولَّدة محل اليدوية يُضعف الأمان** ولا يصلح كبديل.

## 2. الحل

1. **`RequiredByNullabilitySchemaFilter`** (Api/Swagger): يقرأ `NullabilityInfoContext`
   لكل خاصية، ويضيف غير القابلة للفراغ (قيم ومراجع) إلى `required`، بأسماء camelCase.
2. **`SupportNonNullableReferenceTypes()`** بجانبه: يمنع `nullable: true` على مراجع
   `string` غير القابلة للفراغ.
3. **إعادة توليد اللقطة** (`UPDATE_OPENAPI_SNAPSHOT=1`) — 235 مجموعة `required` بدل 152
   (معظمها جديد)، و`DashboardStatsDto` صار 8 حقول مطلوبة.
4. **`openapi-typescript@7.13.0`** كـdevDependency + `npm run generate:api` ←
   `frontend/src/api/generated.ts` (239 عملية، ≈271KB).
5. **حارس `CI`**: خطوة في وظيفة `frontend`: `npm run generate:api` ثم
   `git diff --exit-code -- src/api/generated.ts` — تغيير API بلا توليد يفشل.
6. **أول إحلال**: `DashboardStatsDto` في `types/management.ts` =
   `components['schemas']['DashboardStatsDto']` (تطابق 8/8).

## 3. المراجعة النقدية (عقبات حقيقية)

- **تعارض peer:** `openapi-typescript@7` يعلن `typescript ^5.x` والمشروع على **6.0.3**،
  ففشل `npm install` بـ`ERESOLVE`. لم أخفض `TypeScript` ولم أستخدم `--force` عشوائيًا:
  أُضيف `frontend/.npmrc` بـ`legacy-peer-deps=true` **مع تعليق**، والدليل على التوافق هو
  `tsc -b` على الناتج (يمر). المولّد ينتج أنواعًا صرفة لا تعتمد على إصدار TS.
- **`npm ci` يحذف peer اختياريًا:** بعد التثبيت، `@testing-library/dom` (peer لـ
  `@testing-library/react`) اختفى ففشل `tsc` في **ملفات اختبار قائمة**. أُضيف
  `@testing-library/dom` صراحةً كـdevDependency — لا نفترض أن legacy-peer يثبّت الأقران.
- **أنواع `undefined` مقابل `null`:** المولّد يصدّر الخصائص الاختيارية `?` و`| null`
  معًا؛ أي إحلال لاحق يجب أن يراجع المستهلكين (مثل بناء كائن بلا حقل اختياري).
- **حجم الناتج:** 271KB مولَّد داخل `src` — لا يُحرَّر يدويًا، ويُستثنى فعليًا من المراجعة
  (حارس الانحراف يحمي).
- **`required` ليس اختياريًا:** الفلتر يوسم كل غير قابل للفراغ؛ نموذج C# الذي يستخدم
  `null!` أو `default!` سيظهر مطلوبًا وقد يكذب — لكنه يعكس إعلان C# الحالي.

## 4. التحقق

- `dotnet build -warnaserror`: 0/0.
- `OpenApiContractTests`: اللقطة الجديدة + ثابت `DashboardStatsDto` = 8 حقول مطلوبة.
- `npx tsc -b` (يشمل الأنواع المولَّدة والمحالة): نجح.
- `npx oxlint src scripts`: نظيف · `npm run build` · `npm run guard`: ناجحان.
- `npx vitest run`: **1808/1808** (لا تغيير سلوك).
- `DocGenerator.Api.Tests` بالكامل: **1541/1541** + `Sequential` 5/5 (لا اختبارات جديدة،
  الثابت داخل اختبار قائم).

## 5. الحدود

- **إحلال نوع واحد فقط**؛ بقية `types/document.ts` (797 سطرًا) تحتاج مراجعة مستهلكين
  لكل نوع — مراحل لاحقة.
- **ترقية كبرى للمولّد** قد تغيّر شكل الناتج؛ التثبيت في `package-lock` هو الحماية.
- **`@testing-library/dom`** صار تبعية صريحة بسبب `.npmrc` — لا مشكلة وظيفية، لكنه أثر
  جانبي موثّق.
- لا يغيّر `P2-06`/`P4-04`؛ لقطة العقد صارت أوسع (`required`) وهذا مقصود.
