# `P4-01cf`/`P4-01cg` ملحق — مراجعة هجومية ثانية وتنظيف — 2026-11-25

> **نطاق الملحق:** مراجعة عدائية بعد نقل «اقتراح تعديل قيد الجهة الأم» (الأمر) وخمسة
> معاينات السجل (الاستعلامات) وقبل الدمج: بقايا الاستدعاءات، الملفات الجزئية، الحقول/
> الاستيرادات الميتة، مزيّفات الواجهة، تكرار المساعدات، تكافؤ الأخطاء/التحذيرات، وسطح
> العقود.

## ما فُحص

- بحث شامل في `backend/src` عن `PreviewMergeAsync`/`PreviewUnifyAsync`/
  `PreviewRenameGroupAsync`/`PreviewAbolishAndReplaceAsync`/`PreviewBranchActionAsync`/
  `SuggestParentEditAsync`: **0 مطابقة** — لا مستدعٍ قديم على الخدمة.
- عدّ أعضاء `IPublicEntityService` آليًا: **17 → 11** (ستة محذوفة: `SuggestParentEditAsync`
  + الخمسة معاينات)، بلا أي عضو مضاف أو معدَّل.
- مقارنة سطرية/نصية لأجسام الدوال الست + المساعدات الأربع (تفاصيلها في `203`/`204`).
- جرد كل حقول `PublicEntityService` الـ13 بعد النقل، وعدّ الأسطر/الأعضاء لكل partial.
- فحص الملفات الجزئية المحذوفة، ومزيّفات `IPublicEntityService`، وعدّ تسجيلات `DI`
  مقابل أصناف المعالجات، وفحص `generate:api`.

## الملاحظات المكتشفة والإصلاح

| # | الملاحظة | الدليل | الإصلاح |
|---|---|---|---|
| 1 | **5 استيرادات ميتة** في `PublicEntityService.Guards.cs` بعد رحيل المعاينات (`System.Globalization`/`Common.Audit`/`Common.Interfaces`/`DTOs`) | لا نوع منها مستخدم بعد نقل `ValidateMergeablePairAsync` | قُصّ الرأس إلى `Common` + `Domain.Entities` + `Domain.Enums` |
| 2 | **3 استيرادات ميتة** في `PublicEntityService.Suggestions.cs` بعد نقل الاقتراح | `System.Globalization`/`Common.Audit`/`Common.Interfaces` غير مستخدمة | قُصّ الرأس إلى `Common` + `DTOs` + `Domain.Entities` + `Domain.Enums` |
| 3 | **استيرادان ميتان** في `PublicEntityService.Unify.cs` | `Common.Audit`/`Common.Interfaces` غير مستخدمة بعد رحيل المعاينة | أُزيلا |
| 4 | **4 استيرادات ميتة** في `IPublicEntityService.cs` | `System.Globalization`/`Common.Audit`/`Common.Interfaces`/`Domain.Entities` لا تُستخدم في عقد صار قراءةً أشمل | قُصّ الرأس إلى `Common` + `DTOs` + `Domain.Enums` |
| 5 | **ملفان جزئيان أُفرغا بالكامل** `PublicEntityService.Merge.cs` و`PublicEntityService.Branches.cs` | عدّ الأعضاء قبل الحذف: 1 و2 (والثاني مُساعد نُقل إلى `Guards.cs`) | حُذفا بـ`git rm`؛ لا partial فارغ بين الـ13 المتبقية |
| 6 | **انحراف في فرضية المهمة:** «آخِر كتابة جهة عامة» ليست دقيقة — `UnifyNamesAsync` (توحيد التسمية N←1 بمفتاح `RF-011`) ما زالت كتابة على العقد | `EntityRegistryController.Unify` يستدعي `_registry.UnifyNamesAsync`؛ عضو رقم 9 من 11 | **لم تُنقل** (خارج نطاق `P4-01cf`/`cg` صراحةً وثقلها الجماعي يستحق مرحلة مستقلة)؛ وُثّقت كبقية صريحة أدناه |
| 7 | **انحراف في عدد السطح المتوقع:** المهمة توقّعت 19 → 13 | القياس الفعلي عند `HEAD` = 17 عضوًا (بعد `#343`/`#344`) | 17 − 6 = **11**؛ لا فرق دلالي — الفرق كله في خط الأساس المتقادم |
| 8 | احتمال بقاء partial «بلا محتوى مفيد» | `Groups.cs` صار 45 سطرًا (3 مساعدات عدّ مستخدمة) و`Suggestions.cs` 62 سطرًا (قائمة + مُحوِّل DTO) | لا حذف — كلا الملفين مستخدم |
| 9 | لا مزيّف `IPublicEntityService` في الاختبارات | بحث `: IPublicEntityService` و`Mock<IPublicEntityService>` | لا شيء — لا حاجة لتحديث مزيّف |
| 10 | احتمال تكرار مساعدات المعاينة بين الخدمة والمعالجات | بحث عن `ValidateMergeablePairAsync`/`CountDocumentsForGroupAsync`/`BranchNamesForGroupAsync`/`CountActiveEntriesForGroupsAsync` | تعريف واحد لكل منها في `Guards.cs`/`Groups.cs`، والمعالجات تناديها بـ`PublicEntityService.` |
| 11 | احتمال انحراف العقد المولَّد | `npm run generate:api` + `git diff --exit-code -- src/api/generated.ts` | لا فرق (`exit=0`) — المسارات وأنواع الاستجابة كما هي |
| 12 | حقل `_registry` في المتحكم قد يصبح ميتًا | عدّ `_registry.` = 17 نداءً لقراءات أخرى | ليس ميتًا — بقي كما هو |

## إثبات التكافؤ بعد الإصلاح

- **الأمر (اقتراح الجهة الأم):** 76/76 سطرًا، `diffs = 0`، والنصوص 12/12 (`added = 0`)؛
  النصان الغائبان هما ثابتا صيغة التاريخ داخل `ToParentEditSuggestionDto` الذي بقي في
  الخدمة ولم ينتقل.
- **المعاينات:** 54/54، 57/57، 8/8، 24/24، 152/152 — `diffs = 0` عدا تمرير الاعتماد
  (`_entities`/`_rules`) صريحًا في 8 مواضع نداء (توصيل لا سلوك).
- **المساعدات:** 14/14، 8/8، 8/8، 4/4 — `diffs = 0`.
- **العقود:** `IPublicEntityService` 17 → 11 بلا إضافة؛ `IPublicEntityWriteRules` **بلا أي
  عضو جديد** (كل ما استُخدم كان مُسجَّلًا سابقًا).
- **`DI`:** 123 معالجًا مسجَّلًا مقابل 123 صنفًا (كان 117 قبل الدفعة: +1 أمر و+5 استعلامات).
- **الخدمة:** 13 حقلًا كلها مستخدمة (أدنى تكرار 3)؛ 0 partial فارغ؛ 0 مستدعٍ قديم في `src`؛
  لا كتابة/معاملة في أي استعلام.
- **الحرّاس المشتركة:** `GuardNotParentEntry`/`GuardHeadCannotEditParent`/
  `EnsureHeadScopeAsync`/`EnsureCanonicalAvailableAsync` تُستهلك عبر العقد الداخلي نفسه —
  نفس التنفيذ الواحد في `Guards.cs`، بلا نسخة ثانية.

## متبقٍ بعد هذه الدفعة (صريح)

`IPublicEntityService` = 11 عضوًا: عشرة قراءات (`ListAsync`، `ListNeedsReviewAsync`،
`CountNeedsReviewAsync`، `ListChangeEventsAsync`، `ExportChangeEventsAsync`،
`PreviewImportAsync`، `ListGroupsAsync`، `FindSimilarToGroupAsync`، `ListEntriesByGroupAsync`،
`ListParentEditSuggestionsAsync`) + كتابة واحدة (`UnifyNamesAsync`) — الأخيرة مرشح
`P4-01ch` المقترح.

## التحقق

`build -warnaserror` 0/0 · `format` نظيف · Architecture 29/29 · Application 758/1659/0 ·
Api 901/615/0 و0/5/0 · frontend 1809/1809 + build/guard/`generate:api` بلا فرق ·
الحرّاس الستة نظيفة · `dependency-audit` 9 مشاريع · `npm audit` 0 ثغرة ·
`has-pending-model-changes` نظيف · `DI` 123/123/0 · 0 حقل ميت · 0 partial فارغ.

> **ملاحظة صدق:** اختبارات `PostgreSQL` تُخطّى محليًا (`DOCGEN_TEST_POSTGRES` غير مضبوط)؛
> الحكم النهائي للسلوك على القاعدة هو `backend-postgres` في `CI`.

## الهجرات

**لا هجرة جديدة.** المعلّق يبقى **6**.
