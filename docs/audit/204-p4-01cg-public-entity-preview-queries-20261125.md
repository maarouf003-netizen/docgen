# `P4-01cg` — نقل معاينات سجل الجهات إلى Query Handlers — 2026-11-25

> **النطاق:** المرحلة الثامنة في الدفعة: ملكية خمسة أسطح قراءة/معاينة كانت على
> `IPublicEntityService`، ونقلها إلى استعلامات `IQuery<TResponse>` ومعالجات
> `QueryHandlerBase` تحت `backend/src/DocGenerator.Application/UseCases/PublicEntities/`،
> **قراءة صرفة بلا كتابة ولا معاملة**. الفرع `refactor/p4-01cf-cg-public-entity-rest`
> من `master` (`128938bd`).

## ما نُفِّذ

| الاستعلام | المعالج | المعاملات | الملف |
|---|---|---|---|
| `PreviewMergeQuery(Request)` | `PreviewMergeQueryHandler` | `IPublicEntityRepository` | `PreviewMergeQuery.cs` |
| `PreviewUnifyQuery(Request)` | `PreviewUnifyQueryHandler` | `IPublicEntityRepository` | `PreviewUnifyQuery.cs` |
| `PreviewRenameGroupQuery(Request)` | `PreviewRenameGroupQueryHandler` | `IPublicEntityWriteRules` + `IPublicEntityRepository` | `PreviewRenameGroupQuery.cs` |
| `PreviewAbolishAndReplaceQuery(Request)` | `PreviewAbolishAndReplaceQueryHandler` | `IPublicEntityRepository` + `IUserRepository` | `PreviewAbolishAndReplaceQuery.cs` |
| `PreviewBranchActionQuery(GroupId, Request, Actor)` | `PreviewBranchActionQueryHandler` | `IPublicEntityWriteRules` + `IPublicEntityRepository` | `PreviewBranchActionQuery.cs` |

| البند | التفصيل |
|---|---|
| العقود | حُذفت الأعضاء الخمسة من `IPublicEntityService` (16 → 11 عضوًا، حذفًا صرفًا بلا إضافة أو تعديل آخر) |
| الخدمة | حُذفت `PreviewMergeAsync` (فأُفرغ `PublicEntityService.Merge.cs` وحُذف بـ`git rm`)، و`PreviewBranchActionAsync` (فأُفرغ `PublicEntityService.Branches.cs` وحُذف بـ`git rm`)، و`PreviewUnifyAsync` من `Unify.cs` (بقي `UnifyNamesAsync`/`UnifyNamesCoreAsync`)، و`PreviewRenameGroupAsync`/`PreviewAbolishAndReplaceAsync` من `Groups.cs` (بقيت مساعدات العدّ) |
| المساعدات | `CountDocumentsForGroupAsync`/`BranchNamesForGroupAsync`/`CountActiveEntriesForGroupsAsync` صارت `internal static` على الخدمة مع `IPublicEntityRepository` صريحًا؛ و`ValidateMergeablePairAsync` نُقل من `Branches.cs` إلى `Guards.cs` بنفس الجسم مع `IPublicEntityWriteRules` صريحًا — **مصدر واحد، بلا نسخ** |
| عدم الكتابة | لا `SaveChangesAsync`/`AddAsync`/`Remove`/`_uow`/`_tx`/`ITransactionalRequest`/`IAuditedRequest`/`IAuditLogger` في أي ملف استعلام (فحص نصي شامل) |
| `DI` | تسجيل كل استعلام كـ`IQueryHandler<Query, Response>` في `DependencyInjection.cs` |
| المتحكم | `EntityRegistryController` يحقن المعالجات الخمسة ويستدعيها؛ نفس المسارات (`groups/unify-preview`، `merge-preview`، `groups/{id}/rename-preview`، `groups/abolish-preview`، `groups/{id}/branches/preview`) ونفس فحوص القدرات ونفس `Ok(...)` — ومنها فحص تعارض `request.GroupId != groupId` قبل الاستعلام كما كان |
| الاختبارات | `PublicEntityPreviewQueryExtensions` (جسر لكل استعلام) وتحويل 18 موضع نداء في `PublicEntityServiceTests` من `_service.X` إلى `_handler.X` بلا تغيير أي توقع |

> **قرار موثّق:** استُخدم `IPublicEntityWriteRules` في استعلامَي التسمية/الفرع لأن أعضاءه
> المستخدمة (`Required`، `GuardNotParentEntry`، `GuardHeadCannotEditParent`،
> `EnsureHeadScopeAsync`، `EnsureNoDuplicateEntryAsync`) حرّاس/تحققات **قراءة** لا تكتب،
> وهي بالفعل تضمّ أعضاء قراءة (`BuildImportCandidatesAsync`، `GetActiveGroupEntryAsync`،
> `ToEntryDto`). البديل كان تكرار منطق الحرّاس — مرفوض.

## إثبات الحفاظ على السلوك (لكل دالة)

| الدالة | أسطر الوالد/المعالج بعد التطبيع | `diffs` | الاستثناءات |
|---|---|---|---|
| `PreviewMergeAsync` | 54 / 54 | 0 | — |
| `PreviewUnifyAsync` | 57 / 57 | 0 | — |
| `PreviewRenameGroupAsync` | 8 / 8 | 0 | تمرير `_entities` صريحًا في نداءَي العدّ |
| `PreviewAbolishAndReplaceAsync` | 24 / 24 | 0 | تمرير `_entities` صريحًا في ثلاثة نداءات عدّ |
| `PreviewBranchActionAsync` | 152 / 152 | 0 | تمرير `_rules` صريحًا في ثلاثة نداءات `ValidateMergeablePairAsync` + تأهيل الحرّاس بـ`_rules.` |

- **المساعدات المسحوبة:** `ValidateMergeablePairAsync` 14/14، `CountDocumentsForGroupAsync`
  8/8، `BranchNamesForGroupAsync` 8/8، `CountActiveEntriesForGroupsAsync` 4/4 — كلها
  `diffs = 0` بعد تمرير الاعتماد صريحًا.
- **مقارنة النصوص الحرفية (اتجاهان):** `PreviewMerge` 9/9، `PreviewUnify` 9/9،
  `Rename+Abolish` 7/7، `PreviewBranchAction` 19/19 — `missing = 0` و`added = 0`.
- **الأخطاء/التحذيرات:** نفس ترتيب الإضافة ونفس الرسائل ونفس شروط `errors.Add` مقابل
  `throw`؛ `UnauthorizedAccessException` تظل تُرمى (فلا تُحوَّل إلى `errors`) و
  `ArgumentException` يظل يُلتقط في مواضعه الثلاثة الحرفية.
- **بلا كتابة:** لا استعلام يعدّل كيانًا أو يضيف صفًا؛ كل المكالمات `GetGroupAsync`/
  `ListEntriesByGroupAsync`/`ListDocumentsLinkedToEntryAsync`/`CountLinkedDocumentsByEntryIdsAsync`/
  `ListEntityManagersByGroupIdsAsync` قراءة.

## مراجعة نقدية أولى

| # | السؤال النقدي | الفحص | النتيجة |
|---|---|---|---|
| 1 | هل أُضيف أي وسم معاملة/تدقيق لاستعلام؟ | فحص نصي للملفات الخمسة | لا — صفر مطابقة |
| 2 | هل أضيفت كتابة داخل استعلام؟ | فحص `AddAsync`/`SaveChangesAsync`/`Remove` | لا |
| 3 | هل تكرار منطق `ValidateMergeablePairAsync`؟ | بحث شامل | تعريف واحد في `Guards.cs` |
| 4 | هل حُذف partial غير فارغ؟ | عدّ الأعضاء | `Merge.cs` (طريقة واحدة) و`Branches.cs` (طريقة + مساعد نُقل) فقط — كلاهما أُفرغ فعلًا |
| 5 | هل صار أي حقل ميتًا بعد نقل آخر مستخدميه؟ | عدّ 13 حقلًا | لا — أدنى تكرار 3 |
| 6 | هل تغيّر سطح `IPublicEntityService` بأكثر من الحذف المقصود؟ | مقارنة آلية | 16 → 11، والحذف هو الأعضاء الخمسة وحدها |
| 7 | هل كل استعلام مسجَّل في `DI`؟ | عدّ آلي | 123/123/0 و0 مكرر |
| 8 | هل تغيّر مسار/صلاحية/شكل استجابة أي معاينة؟ | مقارنة المتحكم + `generate:api` | لا — نفس المسارات والفحوص و`Ok`، والعميل المولَّد بلا فرق |
| 9 | هل بقي مستدعٍ للخدمة في `src`؟ | بحث شامل | 0 |
| 10 | هل استعلام `PreviewBranchAction` يحفظ سلوك `groupId` في المسار؟ | قراءة المتحكم | نعم — يُمرَّر إلى الاستعلام كهيئة ويُستخدم في كل الحرّاس |

## التحقق

`build -warnaserror` 0/0 · `format` نظيف · Architecture 29/29 · Application 758/1659/0 ·
Api 901/615/0 و0/5/0 · frontend 1809/1809 + build/guard/`generate:api` بلا فرق ·
الحرّاس الستة نظيفة · `dependency-audit` 9 مشاريع نظيفة · `npm audit` 0 ثغرة ·
`has-pending-model-changes` نظيف.

> اختبارات `PostgreSQL` (ومنها كل معاينات `PublicEntityServiceTests`) تُخطّى محليًا
> لغياب `DOCGEN_TEST_POSTGRES`؛ `backend-postgres` في `CI` هو المرجع القاطع.

## الهجرات

**لا هجرة جديدة.** المعلّق يبقى **6**.
