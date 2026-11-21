# `P6-06` (الجزء الثاني) — إحالة `DocumentAnnexDto` إلى النوع المولَّد — 2026-11-21

> **النطاق:** إحلال نوع `DocumentAnnexDto` اليدوي في `frontend/src/types/document.ts`
> بنظيره المولَّد من `OpenAPI` (`components['schemas']['DocumentAnnexDto']`)، مع الحفاظ
> على السلوك. **لا تغيير في `API`/`DTO`/`backend`.**
> **PR:** [#244](https://github.com/maarouf003-netizen/docgen/pull/244).

## 1. لماذا هذا النوع؟

- `DocumentAnnexDto` هو أحد أصغر أنواع `document.ts` وأقلها ارتباطًا (8 مواضع)، ويطابق
  حقوله المولَّدة حقلًا بحقل: `annexType`/`annexNumber`/`annexDate` (نصية اختيارية)
  و`sequence` (قيمة مطلوبة في العقد).
- إحالته خطوة حقيقية في مسار `P6-06` (إزالة تعريف يدوي مكرر) دون المخاطرة بإحالة
  `DocumentResponse` (225 موضعًا) قبل مراجعة nullability كاملة.

## 2. ما نُفِّذ

1. في `frontend/src/types/document.ts`:
   - `import type { components } from '../api/generated';`
   - `export type DocumentAnnexDto = components['schemas']['DocumentAnnexDto'];`
   - حُذف التعريف اليدوي (5 حقول) وبقي تعليق دلالي واحد.
2. في `frontend/src/components/form/documentFormConstants.ts`:
   - `emptyAnnex()` و`legacyToAnnexes()` تعيدان `sequence: 0` (مطابق للافتراضي في
     `DocumentAnnexDto` الخلفي `Sequence = 0`)، لأن النوع المولَّد يعلن `sequence` مطلوبًا
     (نوع قيمة).
3. في `frontend/src/pages/DocumentForm.tsx`:
   - خريطة الملاحق عند الحفظ تحفظ `sequence: a.sequence ?? 0` بدل إسقاطه.
4. تحديث توقعات اختبارات `DocumentForm.annexAndLayout.test.tsx` (3 توقعات) لتشمل
   `sequence: 0`.

## 3. المراجعة النقدية

- **`tsc -b` كشف خطأً واحدًا:** بناء الملاحق في `DocumentForm.tsx` كان ينتج كائنًا بلا
  `sequence`؛ أُصلح بإمرار القيمة الافتراضية (لا `as` ولا `any`).
- **3 اختبارات `vitest`** كانت تثبّت شكل الحمولة بلا `sequence`؛ حُدِّثت التوقعات، ثم
  مرّت **1808/1808** كاملة.
- **nullability:** المولَّد يعطي `string | null` بدل `string | undefined`؛ المستهلكون
  الحاليون يستخدمون `?? ''` أصلًا، فلا تغيير سلوكي. لم يُضف أي `!` أو `as`.
- **حقل `sequence`:** النوع المولَّد يفرضه لأن `int` قيمة غير قابلة للفراغ؛ القيمة `0`
  هي نفسها الافتراضية في العقد الخلفي، فلا تغيير ترتيب فعلي (الخادم يعيد الترتيب).
- **حارس التوليد:** `npm run generate:api` أُعيد تشغيله بعد `npm ci --legacy-peer-deps`،
  و`git diff --exit-code -- src/api/generated.ts` = `0` (لا انحراف).
- **لا مساس بالخلفية** ولا بملفات `DTO` ولا بلقطة `OpenAPI`.

## 4. التحقق

| البوابة | النتيجة |
|---|---|
| `npx oxlint src` | 0 |
| `npx tsc -b` | 0 |
| `npx vitest run` | **1808/1808** (171 ملفًا) |
| `npm run build` | ناجح |
| `npm run generate:api` + `git diff` | لا انحراف |
| `backend` | لم يُلمس (لا اختبارات خلفية مطلوبة) |

## 5. الحدود والخطوة التالية

- بقي في `types/document.ts` نحو 780 سطرًا من الأنواع اليدوية؛ الإحالة الكاملة تحتاج
  مراجعة مستهلك لكل نوع (خصوصًا `DocumentResponse` و`AssetDto` و`HeirDto`) وnullability.
- الخطوة التالية المقترحة: `ExecutionApplicantDto` أو `DocumentInterventionHeirDto`
  (مستهلكون محدودون)، ثم `DocumentResponse` بعد جرد حقوله.
- لا تغيير في `DoD` الكلي (`P6-06` ما زال **جزئيًا** بقرار الخريطة).
