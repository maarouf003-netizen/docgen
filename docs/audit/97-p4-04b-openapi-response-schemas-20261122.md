# `P4-04b` — إظهار عقد استجابة القوائم في `OpenAPI` — 2026-11-22

> **النطاق:** تحويل ستة إجراءات قوائم في `DocumentsController` من `IActionResult`
> إلى `ActionResult<PagedResult<DocumentResponse>>`، فيصدر مخطط `DocumentResponse`
> (وأنواعه المتداخلة) في لقطة `OpenAPI` ويُولَّد في عميل `TS`.
> **لا تغيير سلوكي ولا `DTO` ولا هجرة؛ الإضافة عقدية فقط.**

## 1. ما نُفِّذ

1. `DocumentsController`:
   - `Search`, `GetDeleted`, `GetStruckOff`, `GetExecuted`, `GetReferredToStart`,
     `GetStrikeOffReminders` صارت `Task<ActionResult<PagedResult<DocumentResponse>>>`.
   - `OkSanitized` صار `ActionResult<PagedResult<DocumentResponse>>` مع **نفس** تصفير
     `PrintCount` لمن لا يملك `CanViewPrintCounter`.
2. أُعيد توليد لقطة `docs/openapi/docgen-v1.json` واعتمادًا:
   - مخططات جديدة: `DocumentResponse`, `DocumentResponsePagedResult`,
     `ExecutionActionDto`, `DocumentAssignmentDto`, `DocumentInterventionDto`,
     `DocumentOccurrenceDto`.
   - **لم يُحذف أي مخطط**، والتغيير محصور في مسارات القوائم الستة.
3. `npm run generate:api` ⇒ `frontend/src/api/generated.ts` يحوي الأنواع الجديدة
   (+320 سطرًا)، بلا أي استخدام إجباري بعد (إحلال تدريجي في `P6-06`).

## 2. ما كشفته المراجعة النقدية

| # | الوجد | الدليل | القرار |
|---|---|---|---|
| 1 | `IActionResult` يخفي عقد الاستجابة عن `OpenAPI` | لقطة `OpenAPI` لم تكن تحوي `DocumentResponse` قبل التغيير | `ActionResult<T>` في الإجراءات الستة فقط |
| 2 | `DocumentInterventionDto` لم يكن مخططًا صادرًا | أُضيف تلقائيًا عبر `DocumentResponse.Interventions` | مكسب جانبي: يفتح إحالة `P6-06` لاحقًا |
| 3 | خطر كسر سلوك التصفير `PrintCount` | `OkSanitized` أُعيد كتابته | نفس المنطق حرفيًا؛ اختبارات `PrintCount` القائمة تمر عبر `CI` |
| 4 | خطر انحراف اللقطة | `git diff` أظهر إضافات فقط بلا حذف مخططات | المسارات المتغيرة = القوائم الستة فقط (تحقق آلي) |
| 5 | خطر انحراف العميل المولَّد | `npm run generate:api` ثم `git diff` | الفرق هو الأنواع المضافة فقط؛ حارس `CI` يمنع أي انحراف لاحق |
| 6 | لا تغيير إصدار `API` | المسارات والأسماء والحمولة كما هي | إضافة مخطط لا كسر — الإصدار يبقى `P4-04`/`P4-05` |

## 3. التحقق

| البوابة | النتيجة |
|---|---|
| `OpenApiContractTests` مع `UPDATE_OPENAPI_SNAPSHOT=1` | 2/2 |
| `OpenApiContractTests` بلا متغيّر التحديث | 2/2 (اللقطة مطابقة) |
| `dotnet build -warnaserror` | 0/0 |
| `dotnet format --verify-no-changes` | نظيف |
| `Architecture` | 29/29 |
| `npx oxlint src` / `npx tsc -b` / `npm run build` | 0 / 0 / ناجح |
| البوابات الكاملة | على `CI` (الـ`PR`) |

## 4. الهجرات

**لا هجرة جديدة.** القائمة المعلّقة تبقى **6**.
