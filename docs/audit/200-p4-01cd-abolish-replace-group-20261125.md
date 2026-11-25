# `P4-01cd` — نقل «الحلول» (إلغاء واستبدال هوية أم) إلى Command — 2026-11-25

> **النطاق:** المرحلة الخامسة بعد `P4-01cb`/`P4-01cc`: ملكية
> `PublicEntityService.AbolishAndReplaceAsync` داخل
> `AbolishAndReplacePublicEntityGroupCommand` + handler في
> `backend/src/DocGenerator.Application/UseCases/PublicEntities/`، على الفرع
> `refactor/p4-01cd-ce-public-entity-group-writes` من `master` (`e1b99e3f`)،
> بحفاظ حرفي على السلوك.

## ما نُفِّذ

| البند | التفصيل |
|---|---|
| الأمر | `AbolishAndReplacePublicEntityGroupCommand(Request, Actor)` في `UseCases/PublicEntities/AbolishAndReplacePublicEntityGroupCommand.cs` |
| المعالج | `AbolishAndReplacePublicEntityGroupCommandHandler` بمعاملاته الستة: `IPublicEntityWriteRules` / `IPublicEntityRepository` / `IRepository<DocumentOccurrence>` / `IUnitOfWork` / `ITransactionRunner` / `IAuditLogger` |
| العقود | حُذف `AbolishAndReplaceAsync` من `IPublicEntityService` (19 → 18 عضوًا بعد هذه المرحلة، والحذف هو هذا العضو وحده)؛ بقيت `PreviewAbolishAndReplaceAsync` قراءةً |
| الخدمة | حُذفت الطريقة من `PublicEntityService.Groups.cs`؛ بقيت المعاينتان ومساعدات العدّ الثلاثة (5 أعضاء)، فلم يُفرَّغ الملف |
| المساعدات | لا مساعد جديد في `IPublicEntityWriteRules`: كل ما تحتاجه الطريقة كان مُسجَّلًا سابقًا (`Required`/`ValidEntityType`/`ValidCitationFormula`/`ValidateCoverageLabel`/`CleanAliases`/`SetGroupNorm`/`EnsureCanonicalAvailableAsync`/`SyncTextsAfterFoldAsync`/`SyncAppealsAfterEntityChangeAsync`/`MigrateDelegatesAsync`/`TrackChangeEventAsync`/`Broadcast*`) |
| الثابت المشترك | `DefaultBranchName` (خاص في الخدمة = `PublicEntityBranchCatalog.ParentBranchName`) استُبدل في المعالج بالمصدر نفسه `PublicEntityBranchCatalog.ParentBranchName` بلا تغيير قيمة |
| المعاملة | كل التحقق/التطبيع قبل `_tx.RunAsync` كما في الأصل (الصلاحية، الفراغ، الاسم، المحافظة، المرسوم، التاريخ)؛ ومعاملة واحدة تحوي الإنشاء والترحيل والمزامنة والحدث والوقوعات والتنبيهات والتدقيق |
| عدم التكرار | لا `IdempotencyGuard` في هذه العملية (لم يكن في الأصل) |
| `DI` | تسجيل المعالج في `DependencyInjection.cs`: 116 معالجًا / 116 تسجيلًا / 0 غير مسجَّل |
| المتحكم | `EntityRegistryController.AbolishAndReplace` يستدعي `_abolishAndReplace.HandleAsync(...)`؛ المسار `groups/abolish-and-replace` وفحص `HasFullAccess` و`Ok(...)` كما هي |
| الاختبارات | جسر `PublicEntityGroupWriteCommandExtensions.AbolishAndReplaceAsync`، وتحويل 12 موضع نداء في `PublicEntityServiceTests` إلى المعالج |

## إثبات الحفاظ على السلوك

- **مقارنة سطرية لجسم الدالة (بعد تطبيع تأهيل المساعدات):** 193 سطرًا في الوالد
  و193 في المعالج، `diffs = 0` بعد استثناء سطرَي `var request = command.Request;`
  و`var actor = command.Actor;` اللذين يحلّان محل معاملَي الطريقة.
- **مقارنة النصوص الحرفية (اتجاهان):** 15/15، `missing = 0` و`added = 0`.
- **فروق التأهيل المطبَّعة فقط:** `_rules.` قبل نداءات المساعدات المشتركة،
  و`PublicEntityBranchCatalog.ParentBranchName` مكان الثابت الخاص `DefaultBranchName`
  (نفس المصدر والقيمة).
- **الحدث/الوقوعات/التنبيهات:** نفس `ActionKindCatalog.Abolish`، ونفس حمولة
  `JsonSerializer` (`abolishedGroupIds`/`oldCanonicalNames`/`newCanonical`/`entityType`/
  `governorate`/`branchName`/`entriesMoved`/`affectedDocuments`/`delegatesReassigned`/
  `decreeKind`/`decreeNumber`/`decreeDate` بصيغة `yyyy-MM-dd`)، ونفس
  `EntityChangeMessages.AbolishOccurrence` و`HeadAlertMessages.EntityAbolishLawyers`/
  `EntityAbolishHeads`.
- **الترتيب:** التحقق ← الألقاب/القيد الأم ← ترحيل الروابط والوقوعات ← مزامنة النصوص ←
  مزامنة الاستئنافات ← الحفظ ← المندوبون ← حدث التغيير ← الحفظ ← التنبيهان ← الحفظ ←
  التدقيق — بلا تغيير، وكذلك اشتقاق `ApplicantRegistryId` وإعادة كتابة اسم الطالب/المنفذ.
- **نص التدقيق:** `abolish_and_replace_entity` بنفس الصياغة والعدادات.

## مراجعة نقدية أولى

| # | السؤال النقدي | الفحص | النتيجة |
|---|---|---|---|
| 1 | هل انتقل التحقق إلى داخل المعاملة؟ | مقارنة ترتيب السطور | لا — التحقق والتطبيع قبل `_tx.RunAsync` كما في الأصل |
| 2 | هل يدخل المعالج في معاملة متداخلة؟ | لا وسم `ITransactionalRequest`/`IAuditedRequest` | معاملة واحدة فقط |
| 3 | هل تضاعف التدقيق عبر `AuditBehavior`؟ | لا `IAuditedRequest` | لا تدقيق مزدوج |
| 4 | هل تغيّر عدد/موضع `SaveChangesAsync`؟ | مقارنة سطرية | خمسة مواضع بنفس الترتيب |
| 5 | هل بقيت نسخة من الطريقة في الخدمة؟ | بحث شامل في `src` | لا — نسخة واحدة في المعالج، والباقي معاينة قراءة فقط |
| 6 | هل صار أي حقل من حقول الخدمة الـ13 ميتًا؟ | عدّ آلي لكل حقل | لا — أدنى تكرار 3 (`_changeEvents`/`_appeals`) |
| 7 | هل تغيّر عقد `IPublicEntityService` بأكثر من الحذف المقصود؟ | مقارنة سطح الواجهة آليًا | 19 → 18 والحذف هو `AbolishAndReplaceAsync` وحده |
| 8 | هل كل معالج مسجَّل؟ | عدّ آلي مقابل `DependencyInjection.cs` | 116/116/0 |
| 9 | هل تغيّر مسار/صلاحية/ترجمة المتحكم؟ | مقارنة الأكشن | المسار و`HasFullAccess` و`Ok` كما هي |

## التحقق

`build -warnaserror` 0/0 · `format` نظيف · Architecture 29/29 · Application 758/1659/0 ·
Api 901/615/0 و0/5/0 · frontend 1809/1809 + build/guard/`generate:api` بلا فرق ·
الحرّاس الستة · audit · migration-guard لا تغييرات · `DI` 116/116/0.

> اختبارات `PostgreSQL` (ومنها مسارات الحلول في `PublicEntityServiceTests`) تُخطّى
> محليًا؛ `backend-postgres` في `CI` هو المرجع القاطع.

## الهجرات

**لا هجرة جديدة.** المعلّق يبقى **6**.
