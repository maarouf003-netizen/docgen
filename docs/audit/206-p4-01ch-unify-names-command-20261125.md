# `P4-01ch` — نقل توحيد تسمية الجهات (N←1) إلى Command — 2026-11-25

> **النطاق:** المرحلة التاسعة في السلسلة: آخر كتابة عامة كانت لا تزال على
> `IPublicEntityService` — `PublicEntityService.UnifyNamesAsync` (وتحتها `UnifyNamesCoreAsync`)
> — نُقلت إلى `UnifyPublicEntityNamesCommand` + handler في
> `backend/src/DocGenerator.Application/UseCases/PublicEntities/`، على الفرع
> `refactor/p4-01ch-unify-names-command` من `master` (`ccbac5a9`)، بحفاظ حرفي على السلوك
> (مفتاح `RF-011`، معاملة واحدة، الحرّاس، الحمولة/الحدث/الوقوعات/التنبيهات/التدقيق).

## ما نُفِّذ

| البند | التفصيل |
|---|---|
| الأمر | `UnifyPublicEntityNamesCommand(Request, Actor, IdempotencyKey = null)` في `UseCases/PublicEntities/UnifyPublicEntityNamesCommand.cs` |
| المعالج | `UnifyPublicEntityNamesCommandHandler` بمعاملاته السبعة: `IPublicEntityWriteRules` / `IPublicEntityRepository` / `IRepository<DocumentOccurrence>` / `IUnitOfWork` / `ITransactionRunner` / `IAuditLogger` / `IIdempotencyStore?` |
| العقود | حُذف `UnifyNamesAsync` من `IPublicEntityService` (11 → 10 أعضاء، والحذف هو هذا العضو وحده؛ بقيت عشرة قراءات) |
| الخدمة | حُذفت `UnifyNamesAsync` و`UnifyNamesCoreAsync` من `PublicEntityService.Unify.cs`، فأُفرغ الملف بالكامل وحُذف بـ`git rm` (261 سطرًا، عضوان) |
| الحقل الميت | حُذف الحقل `_idempotency` ومعامل المُنشئ الاختياري `IIdempotencyStore? idempotency = null` من `PublicEntityService` (آخر مستخدميه كان `UnifyNamesAsync`)؛ بقي في المعالج الجديد كاعتماد اختياري (`null` في الاختبارات كما كان) |
| المساعدات | **لا عضو جديد** في `IPublicEntityWriteRules`: كل ما تحتاجه الطريقة كان مُسجَّلًا (`NormalizeOptional`/`RepointEntryLinks`/`AddFoldAliases`/`SyncTextsAfterRenameAsync`/`SyncTextsAfterFoldAsync`/`SyncAppealsAfterEntityChangeAsync`/`MigrateDelegatesAsync`/`TrackChangeEventAsync`/`BroadcastEntityChangeToAllLawyersAsync`/`BroadcastToAllHeadsAsync`)، و`CountUniqueDocuments` بقيت `internal static` على الخدمة (مصدر واحد، بلا نسخ) |
| عدم التكرار | `IdempotencyGuard.BeginAsync("registry.unify")` ثم `CompleteAsync(Snapshot(result))` عند النجاح و`ReleaseAsync` في `catch` — نفس الترتيب ونفس العملية ونفس بصمة `Fingerprint(request)` ونفس الوسيط `command.IdempotencyKey` |
| المعاملة | `IdempotencyGuard.BeginAsync` **قبل** `_tx.RunAsync` عمدًا (كما في الأصل: التحققات داخل المعاملة)، ومعاملة واحدة تحوي كل القراءات والتحققات والترحيل والطيّ والمزامنة والحدث والوقوعات والتنبيهات والتدقيق، مع موضعَي `SaveChangesAsync` كما هما |
| `DI` | تسجيل `ICommandHandler<UnifyPublicEntityNamesCommand, UnifyNamesResponse>`: **124 معالجًا / 124 تسجيلًا / 0 غير مسجَّل** |
| المتحكم | `EntityRegistryController.Unify` يستدعي `_unifyNames.HandleAsync(...)`؛ نفس المسار `groups/unify`، ونفس `[SkipIdempotency]`، ونفس فحص `HasFullAccess`، ونفس `Ok(...)` و`catch (IdempotentReplayException)`، ونفس نقل الترويسة عبر `IdempotencyKeyHeader()` |
| الاختبارات | جسر `PublicEntityGroupWriteCommandExtensions.UnifyNamesAsync` (بنفس توقيع الخدمة القديم)، وتحويل **12** موضع نداء في `PublicEntityServiceTests` من `_service.UnifyNamesAsync` إلى `_unifyNames.UnifyNamesAsync` بلا تغيير أي توقع |

## إثبات الحفاظ على السلوك

- **جسم الدالة الأساسية (بعد تطبيع التأهيل):** 178 سطرًا في الوالد و178 في المعالج،
  `diffs = 0` بعد استثناء:
  - `var request = command.Request;` و`var actor = command.Actor;` (يحلّان محل معاملَي الطريقة)،
  - وتأهيل تسع نداءات مساعدات بـ`_rules.` (`NormalizeOptional` ×2، `SyncTextsAfterRenameAsync`،
    `SyncTextsAfterFoldAsync`، `SyncAppealsAfterEntityChangeAsync`، `MigrateDelegatesAsync`،
    `TrackChangeEventAsync`، `BroadcastEntityChangeToAllLawyersAsync`، `BroadcastToAllHeadsAsync`)،
  - وتأهيل `CountUniqueDocuments` بـ`PublicEntityService.` (تعريف واحد كما كان).
- **غلاف مفتاح عدم التكرار:** 16/16 سطرًا، `diffs = 0` (بعد إسقاط سطرَي ربط `request`/`actor`
  وتطبيع `idempotencyKey` ← `command.IdempotencyKey`).
- **مقارنة النصوص الحرفية (اتجاهان):** 16/16، `missing = 0` و`added = 0`؛ تشمل رسائل
  التحقق الخمس عشرة، ومفاتيح حمولة `JsonSerializer`
  (`targetGroupId`/`targetGroup`/`absorbedGroupIds`/`oldCanonicalNames`/`entriesMoved`/
  `entriesFolded`/`folds`/`groupsUnified`/`aliasesAdded`/`totalAffectedDocs`/`decreeKind`/
  `decreeNumber`/`decreeDate` بصيغة `yyyy-MM-dd`)، ومفتاح التدقيق
  `"unify_entity_names"` وصياغة تفصيله، و`EntityChangeMessages.UnifyOccurrence`،
  و`HeadAlertMessages.EntityUnifyLawyers`/`EntityUnifyHeads`.
- **الحدث:** نفس `ActionKindCatalog.Unify`، ونفس `GroupId`/`DecreeKind`/`DecreeNumber`/
  `DecreeDate`/`PayloadJson`/`ActorUserId`/`CreatedAtUtc = DateTime.UtcNow`، وبلا `EntryId` (حدث مجموعة).
- **الطيّ والنقل:** نفس بركة الناجين، ونفس مطابقة `(Governorate, BranchName)` الخام `Ordinal`،
  ونفس `entryTargetByAbsorbed` ونفس `AddFoldAliases` ونفس حفظ الاسم الممتصّ اسمًا بديلًا
  «للبحث فقط» على القيد المنقول، ونفس ترتيب `Distinct()` للممتصات (حتمية الاختبارات).
- **الترتيب:** حجز المفتاح ← المعاملة ← تحقق الهدف ← قائمة الممتصات ← النقل/الطيّ ←
  مزامنة الأسماء ← إعادة بناء نصوص الملفات المربوطة ← اتحاد الملفات ← مزامنة الاستئنافات ←
  ترحيل المندوبين ← الحدث ← الوقوعات ← `SaveChangesAsync` ← التدقيق ← التنبيهان ←
  `SaveChangesAsync` — بلا تغيير.

## مراجعة نقدية أولى

| # | السؤال النقدي | الفحص | النتيجة |
|---|---|---|---|
| 1 | هل انزاح موضع `IdempotencyGuard.BeginAsync` إلى داخل المعاملة؟ | مقارنة سطرية للغلاف | لا — قبل `_tx.RunAsync` كما كان، والتحققات داخل المعاملة عمدًا |
| 2 | هل تغيّر موضع `Release`/`Complete` أو ترتيبهما؟ | مقارنة الغلاف | لا — `Release` في `catch` ثم `Complete` بعد النجاح، ونفس `Snapshot(result)` |
| 3 | هل تغيّر عدد/موضع `_tx.RunAsync` أو `SaveChangesAsync`؟ | عدّ آلي | `RunAsync = 1`، `SaveChangesAsync = 2` بنفس الموضعين |
| 4 | هل أُضيف حارس/تحقق أو حُذف؟ | مقارنة سطرية | لا — نفس الشروط والرسائل والترتيب (`Groups` ثم `Entries` ثم `NeedsReview` ثم الطول) |
| 5 | هل تكرار مساعدات بين الخدمة والمعالج؟ | بحث عن كل مساعد | تعريف واحد لكل مساعد؛ المعالج يناديها عبر `IPublicEntityWriteRules`/`PublicEntityService.` |
| 6 | هل تغيّر سطح `IPublicEntityService` بأكثر من الحذف المقصود؟ | مقارنة آلية | 11 → 10، والحذف هو `UnifyNamesAsync` وحده |
| 7 | هل صار أي حقل من حقول الخدمة ميتًا؟ | عدّ كل حقل بعد النقل | 13 → 12 (حُذف `_idempotency`)؛ أدنى تكرار متبقٍ 3 (`_changeEvents`) |
| 8 | هل بقي مستدعٍ للطريقة على الخدمة في `src`؟ | بحث شامل | 0 — المتحكم وحده وكان يُحوَّل |
| 9 | هل كل معالج مسجَّل في `DI`؟ | عدّ آلي مقابل الأصناف | 124/124/0 بلا تكرار |
| 10 | هل تغيّر مسار/صلاحية/شكل استجابة `groups/unify`؟ | مقارنة الأكشن + `generate:api` | لا — نفس المسار والوسوم والفحص و`Ok`/`catch`، والعميل المولَّد بلا فرق |
| 11 | هل صار partial فارغًا أو بقي ملف ميت؟ | عدّ أعضاء كل partial | `Unify.cs` أُفرغ فعلًا فحُذف بـ`git rm`؛ 11 partial متبقية كلها مستخدمة (أدنى عضوين) |

## التحقق

`build -warnaserror` 0/0 · `format --verify-no-changes` نظيف · Architecture 29/29 ·
Application 758/1659/0 · Api 901/615/0 و0/5/0 · frontend 1809/1809 + `oxlint`/`tsc -b`/`build`/`guard`/
`generate:api` بلا فرق · الحرّاس الستة نظيفة · `dependency-audit` 9 مشاريع ·
`npm-audit-guard` 0 ثغرة · `has-pending-model-changes` نظيف · `DI` 124/124/0 ·
0 حقل ميت · 0 partial فارغ.

> اختبارات `PostgreSQL` (ومنها كل مسارات التوحيد في `PublicEntityServiceTests`) تُخطّى
> محليًا لغياب `DOCGEN_TEST_POSTGRES`؛ `backend-postgres` في `CI` هو المرجع القاطع.

## الهجرات

**لا هجرة جديدة.** المعلّق يبقى **6**.
