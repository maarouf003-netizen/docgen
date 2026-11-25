# `P4-01cf` — نقل اقتراح تعديل قيد «الجهة الأم» إلى Command — 2026-11-25

> **النطاق:** المرحلة السابعة في الدفعة: ملكية `PublicEntityService.SuggestParentEditAsync`
> داخل `SuggestPublicEntityParentEditCommand` + handler في
> `backend/src/DocGenerator.Application/UseCases/PublicEntities/`، على الفرع
> `refactor/p4-01cf-cg-public-entity-rest` من `master` (`128938bd`)، بحفاظ حرفي على السلوك.
> آخِر كتابة عامة كانت لا تزال على `IPublicEntityService`.

## ما نُفِّذ

| البند | التفصيل |
|---|---|
| الأمر | `SuggestPublicEntityParentEditCommand(EntryId, Request, Actor)` في `UseCases/PublicEntities/SuggestPublicEntityParentEditCommand.cs` |
| المعالج | `SuggestPublicEntityParentEditCommandHandler` بمعاملاته السبعة: `IPublicEntityWriteRules` / `IPublicEntityRepository` / `IRepository<Branch>` / `IRepository<ParentEditSuggestion>` / `IUnitOfWork` / `ITransactionRunner` / `IAuditLogger` |
| العقود | حُذف `SuggestParentEditAsync` من `IPublicEntityService` (17 → 16 عضوًا، والحذف هو هذا العضو وحده) |
| الخدمة | حُذفت الطريقة من `PublicEntityService.Suggestions.cs`؛ الملف **لم يُحذف** لأنه بقي يحوي `ListParentEditSuggestionsAsync` و`ToParentEditSuggestionDto` |
| المعاملة | كل القراءات/التحقق/بناء `ParentEditSuggestion` قبل `_tx.RunAsync`؛ المعاملة تغطي `AddAsync` + `SaveChangesAsync` + حدث التغيير + `SaveChangesAsync` + التدقيق — بنفس الترتيب والموضع |
| عدم التكرار | لا `IdempotencyGuard` في هذه العملية (لم يكن في الأصل)، ولا وسم `ITransactionalRequest`/`IAuditedRequest` |
| الحرّاس | فحص `Head/SubHead` ثم وجود `BranchId` ثم القيد ثم `IsParentEntity` ثم نشاط المجموعة ثم نطاق محافظة الفرع ثم تفرّد الاسم ثم منع الاقتراح المعلّق المكرر — بلا تغيير رسالة أو ترتيب |
| `DI` | تسجيل `ICommandHandler<SuggestPublicEntityParentEditCommand, ParentEditSuggestionDto>`: 123 معالجًا / 123 تسجيلًا / 0 غير مسجَّل (بعد إضافة استعلامات `P4-01cg` الستة) |
| المتحكم | `EntityRegistryController.SuggestParentEdit` يستدعي `_suggestParentEdit.HandleAsync(...)`؛ المسار `entries/{entryId:int}/suggest-parent-edit` وفحص `IsHeadOrSubHead` و`Ok(...)` كما هي |
| الاختبارات | جسر `PublicEntityGroupWriteCommandExtensions.SuggestParentEditAsync`، وتحويل 24 موضع نداء في `PublicEntityServiceTests` إلى المعالج |

## إثبات الحفاظ على السلوك

- **مقارنة سطرية لجسم الدالة (بعد تطبيع تأهيل المساعدات):** 76 سطرًا في الوالد و76 في
  المعالج، `diffs = 0` بعد استثناء سطرَي `var request = command.Request;` و
  `var actor = command.Actor;` واستبدال `command.EntryId` بمعامل `entryId`، وتأهيل
  `NormalizeOptional`/`Required`/`ValidEntityType`/`ValidCitationFormula`/
  `EnsureCanonicalAvailableAsync`/`TrackChangeEventAsync`/`ToParentEditSuggestionDto`
  بـ`_rules.` (نفس التنفيذ الواحد عبر العقد الداخلي، بلا نسخ).
- **مقارنة النصوص الحرفية (اتجاهان):** 12/12 في المعالج، `added = 0`؛ والنصان «المفقودان»
  هما `"yyyy-MM-ddTHH:mm:ssZ"` داخل `ToParentEditSuggestionDto` الذي **لم ينتقل** (بقي
  مساعدًا مشتركًا في الخدمة) — لا سلوك عرض تغيّر.
- **الحمولة والحدث:** نفس `ActionKindCatalog.Propose`، ونفس حمولة `JsonSerializer`
  (`actionKind`/`groupId`/`entryId`/`canonicalName`/`proposedCanonicalName`/
  `proposedEntityType`/`proposedCitationFormula`/`reason`/`createdBranchId`)، ونفس ربط
  `EntryId`/`GroupId`، ونفس `CreatedAtUtc = DateTime.UtcNow`، وبلا تنبيهات (كما الأصل).
- **التدقيق:** نفس `"suggest_parent_edit"` ونفس صيغة التفصيل
  (`«{canonical}» — {proposed} {reason}`) وداخل المعاملة كما كان.
- **الترتيب:** فحص الدور ← الفرع ← جلب القيد ← قيد الأم ← نشاط المجموعة ← نطاق المحافظة ←
  تفرّد الاسم المقترح ← نوع الجهة ← صيغة المناداة ← السبب ← منع المعلّق المكرر ← بناء
  الاقتراح ← المعاملة — بلا تغيير.

## مراجعة نقدية أولى

| # | السؤال النقدي | الفحص | النتيجة |
|---|---|---|---|
| 1 | هل انتقل أي تعديل كيان إلى داخل المعاملة؟ | مقارنة ترتيب السطور | لا — بناء `ParentEditSuggestion` قبل `_tx.RunAsync` كما في الأصل |
| 2 | هل يضاعف `AuditBehavior` التدقيق؟ | لا `IAuditedRequest` | لا تدقيق مزدوج |
| 3 | هل تحرّك موضع أي `SaveChangesAsync`؟ | مقارنة سطرية | لا — اثنان داخل المعاملة بنفس الموضع |
| 4 | هل بقي مستدعٍ للطريقة على الخدمة في `src`؟ | بحث شامل | لا — المتحكم وحده وكان يُحوَّل |
| 5 | هل حُذف partial غير فارغ؟ | عدّ أعضاء `Suggestions.cs` | لا — بقي عضوان (`List` + مُحوِّل DTO) |
| 6 | هل صار أي حقل من حقول الخدمة الـ13 ميتًا؟ | عدّ آلي لكل حقل | لا — أدنى تكرار 3 |
| 7 | هل تغيّر عقد `IPublicEntityService` بأكثر من الحذف المقصود؟ | مقارنة آلية | 17 → 16 والحذف هو `SuggestParentEditAsync` وحده |
| 8 | هل كل معالج مسجَّل؟ | عدّ آلي | 123/123/0 |
| 9 | هل بقي `_branches` مستخدمًا في الخدمة؟ | عدّ الحقل | نعم (8 استخدامات، منها `EnsureHeadScopeAsync`) |
| 10 | هل بقي `Propose` حدثًا على «اقتراح» لا على تعديل القيد؟ | قراءة الحدث | نعم — بلا أي كتابة على القيد، كما كان |

## التحقق

`build -warnaserror` 0/0 · `format` نظيف · Architecture 29/29 · Application 758/1659/0 ·
Api 901/615/0 و0/5/0 · frontend 1809/1809 + build/guard/`generate:api` بلا فرق ·
الحرّاس الستة نظيفة · `dependency-audit` 9 مشاريع نظيفة · `npm audit` 0 ثغرة ·
`has-pending-model-changes` نظيف · `DI` 123/123/0.

> اختبارات `PostgreSQL` (ومنها مسارات الاقتراح في `PublicEntityServiceTests`) تُخطّى
> محليًا لغياب `DOCGEN_TEST_POSTGRES`؛ `backend-postgres` في `CI` هو المرجع القاطع.

## الهجرات

**لا هجرة جديدة.** المعلّق يبقى **6**.
