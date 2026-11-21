# `P6-06` (الجزء الثالث) — إحالة `HeirDto` ومعالجة nullability — 2026-11-21

> **النطاق:** إحالة `HeirDto` اليدوي في `frontend/src/types/document.ts` إلى النوع المولَّد
> `components['schemas']['HeirDto']`، ثم توسيع أنواع المساعدات المشتركة لتقبل `null`
> بدل `undefined` (المولَّد يصدّر `string | null`). **لا تغيير في `API`/`DTO`/`backend`.**
> **PR:** [#245](https://github.com/maarouf003-netizen/docgen/pull/245).

## 1. ما نُفِّذ

1. `frontend/src/types/document.ts`: حُذف تعريف `HeirDto` اليدوي (16 حقلًا) وصار:
   `export type HeirDto = components['schemas']['HeirDto'];`
2. توسيع أنواع الحقول الاختيارية في المساعدات إلى `string | null` بدل `string`:
   - `utils/documentDisplay.ts`: `tripleName`.
   - `components/view/viewTypes.ts`: `PersonFields`.
   - `components/view/viewFormat.ts`: `heirLineDetail`/`legalAddressLabel`/
     `heirRepresentativeDetail`/`representativeLine`/`representativeRows`/
     `representationSuffix`.
   - `components/form/documentFormConstants.ts`: `hasRepresentative`/`hasStoredRepresentative`/
     `hasHeirName`/`heirAddressLabelOf`/`representativeAddressLabelOf`.
   - `components/form/RepresentativeEditor.tsx`: `RepresentativeFields`.
   - `components/form/HeirsEditors.tsx`: `heirRowKey` و`drafts.isLocked` (قبل `number | null`).
3. `components/view/DocumentGenerationSection.tsx`: `heirId: h.id ?? undefined` عند تمرير
   معرّف الوريث إلى التوليد، لأن المولَّد يعلنه `number | null | undefined`.

## 2. المراجعة النقدية

- **`tsc -b` كشف 25 خطأ** كلها nullability: `string | null` من المولَّد يُمرَّر إلى مساعدات
  معرَّفة `string | undefined`. **لم يُستخدم `as` ولا `any` ولا `!`**؛ وُسِّعت أنواع
  المساعدات المشتركة مرة واحدة (حل جذري) وطُبِّق `?? undefined` عند حد `downloadOne`.
- **لا تغيير سلوكي:** كل المستهلكين كانوا يستخدمون `?? ''` أصلًا؛ `null` و`undefined`
  متساويان في `??` و`filter(Boolean)`. لم تتغير نصوص العرض أو القوائم.
- **لا تكرار تعريفات:** `HeirDto` الآن من `OpenAPI`؛ لم يُبقَ تعريف يدوي.
- **`viewFormat.ts`:** التغيير كله توقيعات أنواع (94 سطر diff لكن 0 تغيير منطقي)؛
  `oxlint` و`vitest` و`build` خضراء.
- **`DocumentGenerationSection`:** تمرير `undefined` بدل `null` يحافظ على العقد الحالي
  (`heirId?: number`) ولا يرسل `null` في query string.

## 3. التحقق

| البوابة | النتيجة |
|---|---|
| `npx oxlint src` | 0 |
| `npx tsc -b` | 0 |
| `npx vitest run` | **1808/1808** (171 ملفًا) |
| `npm run build` | ناجح |
| `npm run generate:api` + `git diff --exit-code` | لا انحراف (من الجزء السابق) |
| الخلفية | لم تُلمس (لا هجرة/لا اختبار خلفي مطلوب) |

## 4. الحدود

- بقي في `types/document.ts` نحو 760 سطرًا (`DocumentResponse`, `AssetDto`, `GuarantorDto`…)
  تحتاج نفس المعالجة. `P6-06` ما زال **جزئيًا** بقرار الخريطة.
- `GuarantorDto` يحمل `nature?: PartyNature` (اتحاد يدوي) بينما المولَّد `string | null`؛
  إحالته تحتاج قرارًا واعيًا (تحويل/تحقق) لا مجرد alias — تُترك لمرحلة تالية.
