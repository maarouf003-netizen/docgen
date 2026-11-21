# `P6-06` (الجزء السابع) — إحالة `AssetDto` و`ExecutionApplicantDto` — 2026-11-21

> **النطاق:** الدفعة الأولى من إكمال `types/document.ts`: إحالة `AssetDto` و`ExecutionApplicantDto`
> إلى المخططات المولَّدة من `OpenAPI`، مع تضييق اتحاد `nature` ومعالجة `nullability`
> من الجذر (بلا `as`/`any`). لا تغيير في عقود الخلفية ولا في `DTO` ولا في قاعدة البيانات.

## 1. ما نُفِّذ

1. في `frontend/src/types/document.ts`:
   - `AssetDto = components['schemas']['AssetDto']` — النوع المولَّد يطابق
     `AssetDto` الخلفي حقلًا بحقل (`AssetDto.Owners` هي `List<string>?` ⇒ `string[] | null`،
     و`Id` هي `int?` ⇒ `number | null`). أُبقيت وثيقة المجال أعلى النوع، وأُحيلت تفاصيل
     الحقول إلى `DocumentPartyDtos.cs`.
   - `ExecutionApplicantDto = Omit<components['schemas']['ExecutionApplicantDto'], 'nature'> & { nature?: PartyNature | null }`
     — يحفظ الاتحاد المغلق `'natural' | 'legal'` بدل `string | null` المولَّد، ويعيد
     استخدام بقية الحقول (بما فيها `heirs?: ExecutedHeirDto[] | null`).
2. إصلاح `nullability` الناتج من الجذر (9 مواضع، صفر `as`/`any` جديدة):
   - `assetDisplayName(a: AssetDto)` — أُزيل تقاطع `& { id?: number }` الذي كان يضيّق
     `id` بلا داعٍ بعد صيرورته `number | null`.
   - `assetKindLabel` و`isAuctionableKind` يقبلان `string | null | undefined`.
   - `Row` و`Rows` في `AssetsSection` يقبلان `string | null | undefined`.
   - `EstateSelection` و`DocumentGenerationSection`: فحص المعرّف `r.id != null` بدل
     `r.id !== undefined` — **إصلاح سلوكي حقيقي**: `null` معرّف «غير محفوظ» يجب ألا يُدرج
     في `selected` ولا يُمرَّر إلى `onToggle`.
3. لا هجرات ولا تغيير في `OpenAPI`؛ `npm run generate:api` لا انحراف.

## 2. ما كشفته المراجعة النقدية

| # | الوجد | الدليل | الإصلاح |
|---|---|---|---|
| 1 | اليدوي كان يدّعي `owners?: string[]` فقط، والخلفية تُرجع `null` فعلًا | `AssetDto(..., List<string>? Owners, ...)` في `DocumentPartyDtos.cs` ⇒ `owners?: string[] \| null` | استُخدم المولَّد؛ كل المستهلكين يقرأون `owners ?? []` مسبقًا |
| 2 | اليدوي كان يدّعي `id?: number` بينما `int?` تُرجع `null` | `AssetDto(int? Id, ...)` و`ExecutionApplicantDto` | `id != null` بدل `id !== undefined` في مساري الاختيار |
| 3 | تقاطع `AssetDto & { id?: number }` في `assetDisplayName` صار متناقضًا مع المولَّد | 39 خطأ `tsc` أوليًا كلها من هذا التقاطع | أُزيل التقاطع؛ التوقيع الآن `AssetDto` الصريح |
| 4 | دوال العرض تقبل `string \| undefined` فقط | أخطاء `TS2322` في `AssetsSection` و`TS2345` في `StatusChangeModal` | توسيع التوقيعات إلى `string \| null \| undefined` (نمط `P6-06` القائم) |
| 5 | `ExecutionApplicantDto.heirs` صار `ExecutedHeirDto[] \| null` | المخطط المولَّد | كل مواضع القراءة تستخدم `(heirs ?? [])` مسبقًا؛ لا تعديل سلوكي |

**لم تُمسّ الدلالات:** `Omit` لا يغيّر أي حقل مولَّد غير `nature`، و`PartyNature` يبقى
اتحادًا مغلقًا. لا `as`/`any`/`!` جديدة في المستهلكين.

## 3. ما تبقّى في `P6-06`

- `DocumentInterventionDto`/`DocumentInterventionUpsertRequest` (المولَّد موجود للأخير).
- `DocumentUpsertRequest` (المولَّد موجود، مع `borrowerNature: string` المولَّد).
- `DocumentResponse` (219 سطرًا/225 موضعًا) — **لا مخطط مولَّد له** في لقطة `OpenAPI`
  (الصنف `DocumentResponse` غير مُصدَّر كمخطط)؛ يحتاج قرارًا قبل الإحالة.
- `InitialActionRequest` / `DocumentFilterOptionsDto` / `RotationDocumentDto` /
  `BaseNumberHistoryDto` / `CorrectBaseNumberRequest` — بلا مخططات مولَّدة مقابلة، تبقى يدوية موثّقة.

## 4. التحقق

| البوابة | الأمر | النتيجة |
|---|---|---|
| `oxlint` | `npx oxlint src` | 0 |
| `tsc` | `npx tsc -b` | 0 |
| `vitest` | `npx vitest run` | **1808/1808** (171 ملفًا) |
| `build` | `npm run build` | ناجح (`✓ built in 2.52s`) |
| الخلفية/الهجرات | لم تُلمس | المعلّقة 6 كما هي |
