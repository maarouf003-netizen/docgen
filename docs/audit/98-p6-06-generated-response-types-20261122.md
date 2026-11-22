# `P6-06` (الجزء العاشر) — أنواع استجابة مولَّدة (`ExecutionActionDto` وما بعدها) — 2026-11-22

> **النطاق:** بعد إظهار `DocumentResponse` ومخططاته المتداخلة في `P4-04b`، أُحيلت
> ستة أنواع يدوية إلى `components['schemas']`: `ExecutionActionDto`,
> `DocumentAssignmentDto`, `DocumentInterventionDto`, `DocumentOccurrenceDto`,
> `SaveBaseNumbersRequest`, `CorrectBaseNumberRequest`.
> **لا تغيير في الخلفية/`DTO`/`OpenAPI`** — استهلاك مخططات موجودة فقط.

## 1. ما نُفِّذ

| النوع | الشكل المعتمد | التضييق/التوافق |
|---|---|---|
| `ExecutionActionDto` | alias مباشر | `version` صار مطلوبًا (الخادم يرسله دائمًا) — حُدِّثت حمولة اختبار واحدة |
| `DocumentAssignmentDto` | `Omit<…, 'kind'> & { kind: 'create' \| 'transfer' }` | يحفظ الاتحاد المغلق |
| `DocumentInterventionDto` | `Omit<…, 'nature' \| 'heirs' \| 'version'> & { nature: InterventionNature; heirs?: HeirDto[]; version?: number }` | يحفظ الاتحاد ويُبقي `heirs`/`version` اختياريين للحمولات القائمة |
| `DocumentOccurrenceDto` | `Omit<…, 'occurrenceType' \| 'source'> & { occurrenceType: OccurrenceType; source?: 'system' \| 'manual' \| null }` | يحفظ الاتحادين |
| `SaveBaseNumbersRequest` | alias مباشر | `entries` مطلوب كما في الخلفية |
| `CorrectBaseNumberRequest` | `Omit<…, 'baseNumber'> & { baseNumber: string }` | يُبقي الإلزام الواجهي |

## 2. ما كشفته المراجعة النقدية

| # | الوجد | الدليل | الإصلاح |
|---|---|---|---|
| 1 | `ExecutionActionDto.version` صار مطلوبًا | خطأ `TS2741` في حمولة `DocumentsList.test` | أُضيف `version: 0` (الخادم يرسله دائمًا) |
| 2 | حقول `InterventionDetailsModal` (`subject`/`notes`) صارت `string \| null` | خطآ `TS2322` | توسيع `TextRow.value` إلى `string \| null` |
| 3 | `formatDate`/`formatDateTime` تقبل `string \| undefined` فقط | خطأ `TS2345` في `OccurrencesModal` بعد صيرورة `eventDate` قابلة للفراغ | توسيع التوقيعين (و`formatRelativeTime`) إلى `string \| null` — نمط `P6-06` القائم |
| 4 | `version` في `DocumentInterventionDto` مطلوب في المخطط | حمولات الواجهة تبني تدخلًا جديدًا قبل استجابة الخادم | أُبقي اختياريًا عبر `Omit` صريح (قرار توافق موثّق) |
| 5 | `heirs` في `DocumentInterventionDto` مطلوب في المخطط | نفس السبب | أُبقي اختياريًا (الخلفية تقبل الغياب) |

لا `as`/`any`/`!` جديدة. لم تُمسّ أي دلالة عرض: التغيير أنواع + توسيع توقيعات تاريخ.

## 3. ما تبقّى في `P6-06`

- `DocumentResponse` — مخططه متاح الآن، لكن الإحلال يحتاج قرارًا حول
  `displayFileNumber`/`needsRotation`/`hasAppeals` وتواريخ `DateTime` (`undefined`/`null`).
- `DocumentInterventionDto` أُحيل هنا؛ `DocumentFilterOptionsDto`/`RotationDocumentDto`/
  `BaseNumberHistoryDto` بلا مخططات مولَّدة؛ تبقى يدوية موثّقة.

## 4. التحقق

| البوابة | النتيجة |
|---|---|
| `npx oxlint src` | 0 |
| `npx tsc -b` | 0 |
| `npx vitest run` | **1808/1808** |
| `npm run build` | ناجح |
| الحراس الخفيفة | نظيفة |
| الخلفية/الهجرات | لم تُلمس (المعلّقة 6) |
