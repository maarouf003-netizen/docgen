# `P6-02f` — تفكيك ثوابت النموذج `documentFormConstants` — 2026-11-26

> **النطاق:** استكمال دين حجم الوحدات: تفكيك
> `frontend/src/components/form/documentFormConstants.ts` من **618** سطرًا إلى
> **431** سطرًا + خمس وحدات متخصصة، بلا تغيير سلوكي (إعادة تصدير كاملة).

## 1. النتيجة

| الملف | قبل | بعد |
|---|---:|---:|
| `documentFormConstants.ts` | **618** | **431** |
| `interventionConstants.ts` | — | 89 |
| `documentFormAssets.ts` | — | 58 |
| `documentAmountKeys.ts` | — | 38 |
| `documentAnnexFields.ts` | — | 12 |
| `formLabelTypes.ts` | — | 2 |

حُذف `documentFormConstants` من قائمة الحارس: **8 ⇒ 7** ملفًا مجمّدًا.

## 2. التوزيع

| الوحدة | المحتوى |
|---|---|
| `formLabelTypes` | `LabelValueOption` |
| `interventionConstants` | صفات/حدود المتدخل + `emptyIntervention`/`interventionToRequest` |
| `documentFormAssets` | أنواع الأموال، الحق/الحصة، السقف، `emptyAsset` |
| `documentAmountKeys` | مفاتيح المبالغ/العملات الثلاثة في المواضع الثلاثة |
| `documentAnnexFields` | `emptyAnnex` + `legacyToAnnexes` |

## 3. التوافق

- `documentFormConstants.ts` يُعيد تصدير كل ما نُقل (`export *`), فكل الاستيرادات
  القائمة في الصفحات/المكوّنات/الاختبارات تعمل بلا تعديل.
- `toUpsert` يستورد `legacyToAnnexes` من وحدته الجديدة (بلا تغيير قيمة).
- `PARTY_NATURE_OPTIONS` وبقية خيارات الأطراف/الورثة/الممثل/الحدود/`toUpsert`
  بقيت في الملف الأصلي.

## 4. الاختبارات

| المجموعة | النتيجة |
|---|---|
| `documentFormConstants.test.ts` | **13/13** |
| `components/form` + أربعة ملفات `DocumentForm` | **162/162** |
| لا اختبار جديد | التفكيك ناقل بحت؛ الحزمة الكاملة 1904/1904 |

## 5. البوابات

`tsc`/`oxlint` نظيفان، و`module-size-guard` نظيف مع **7** ملفات مجمّدة.
البوابات الكاملة تُشغَّل قبل الـPR.

## 6. الهجرات

**لا هجرة.**
