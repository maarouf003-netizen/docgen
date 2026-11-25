# `P4-01cn` ملحق — المراجعة النقدية (تكافؤ وحدود العقد) — 2026-11-25

> **نطاق الملحق:** مراجعة نقدية بعد نقل قراءات المراسلات الست إلى معالجات استعلام
> وقبل الدمج: مطابقة كل دالة سطرًا/رمزًا، دلتا سطح `ICorrespondenceService`، إثبات
> القراءة الصرفة، الحقول/المعاملات/الاستيرادات الميتة، وعدّ تسجيلات `DI`.

## 1. مطابقة الدوال المنقولة

| الدالة | موضعها الجديد | أسطر (قبل/بعد) | فرق الرموز | الاستثناء |
|---|---|---|---|---|
| `SearchAsync` | `SearchCorrespondencesQueryHandler.SearchCoreAsync` | 39 / 42 | 0 | لفّ استدعاءين (Governorate/Select) |
| `GetByIdAsync` | `GetCorrespondenceQueryHandler.HandleCoreAsync` | 5 / 7 | 0 | تمرير مستودعات الحرّاس + الساعة |
| `CountUnseenAsync` | `CountUnseenCorrespondencesQueryHandler` | 1 / 1 | 0 | — |
| `GetGovernoratesAsync` | `GetCorrespondenceGovernoratesQueryHandler` | 1 / 1 | 0 | — |
| `ListByDocumentAsync` | `ListCorrespondencesByDocumentQueryHandler` | 18 / 22 | 0 | لفظ استدعاءين |
| `MayListDocumentAsync` | `ListCorrespondencesByDocumentQueryHandler` | 23 / 25 | 0 | تمرير مستودعين |
| `SearchTargetsAsync` | `SearchCorrespondenceTargetsQueryHandler` | 11 / 13 | 0 | تمرير أربعة مستودعات |
| `ResolveEligibleTargetsAsync` | `SearchCorrespondenceTargetsQueryHandler` | 15 / 15 | 0 | — |
| `ToTargetDto` | `SearchCorrespondenceTargetsQueryHandler` | 6 / 6 | 0 | — |
| **الإجمالي** | — | **119 / 132** | **0** | 13 سطرًا ميكانيكيًا |

- **المساعدات المرقّاة على الخدمة:** **137 / 137** سطرًا، فرق **0** (تأهيل حقول↔بارامترات
  وتمرير ساعة/منطقة فقط) — التفصيل في `221`.
- **النصوص الحرفية:** 32 نصًا / 24 مميزًا، `missing = 0` و`added = 0` بعد احتساب نقل
  `فلتر الأهمية غير صالح` من `Helpers.cs` إلى معالج البحث.
- **المساعدات المشتركة:** تعريف واحد لكل من `ToDto`/`ToListItem`/`FileContextOf`/
  `ToMessageDto`/`CanViewAsync`/`OwnerSectionOfAsync`/`FollowsDocumentAsync`/
  `MayAttachAsync`/`IsDocumentInDelegateScopeAsync`/`ResolveHeadGovernorateAsync`/
  `CanWrite`؛ و`MayListDocumentAsync`/`ResolveEligibleTargetsAsync`/`ToTargetDto`/
  `NormalizeImportanceFilter`/`TargetsLimit` تعريف واحد في معالجها الوحيد.
- **الفاصل:** `Math.Max(1, page)` ← `Math.Clamp(perPage, 1, 100)` ←
  `NormalizeImportanceFilter` قبل حجب المدير، و`ServerClock.CurrentYear` في
  `FileContextOf` كما كان.

## 2. دلتا سطح الواجهة

| القياس | قبل | بعد |
|---|---|---|
| أعضاء `ICorrespondenceService` | 6 | 0 (الواجهة حُذفت) |
| المحذوف | — | `SearchAsync`، `GetByIdAsync`، `CountUnseenAsync`، `ListByDocumentAsync`، `GetGovernoratesAsync`، `SearchTargetsAsync` |
| `ICorrespondenceWriteRules` | 10 أعضاء | 10 (بلا تغيير حرفي) |
| إضافات إلى عقود الكتابة | — | 0 |

- الحذف صرف: لا أعضاء جديدة في أي واجهة، ولا تغيير في توقيعات عقود الكتابة.
- `GetGovernoratesAsync` على المستودع بقي كما هو؛ إنما انتقل سطح الخدمة فقط.

## 3. إثبات القراءة الصرفة

| الملف | `SaveChanges`/`Add`/`Remove` | `_uow`/`ITransactionRunner`/`ITransactionalRequest` | `IAuditLogger`/`LogAsync` | `RunAsync`/`ClearChanges` |
|---|---|---|---|---|
| `SearchCorrespondencesQuery.cs` | 0 | 0 | 0 | 0 |
| `GetCorrespondenceQuery.cs` | 0 | 0 | 0 | 0 |
| `CountUnseenCorrespondencesQuery.cs` | 0 | 0 | 0 | 0 |
| `ListCorrespondencesByDocumentQuery.cs` | 0 | 0 | 0 | 0 |
| `GetCorrespondenceGovernoratesQuery.cs` | 0 | 0 | 0 | 0 |
| `SearchCorrespondenceTargetsQuery.cs` | 0 | 0 | 0 | 0 |

- المستودعات المستخدمة كلها قراءة: `SearchForPartyAsync`، `SearchForBranchAsync`،
  `SearchForScopeAsync`، `SearchAllAsync`، `GetByIdWithDetailsAsync`،
  `CountUnseenForTargetAsync`، `ListByDocumentAsync`، `GetGovernoratesAsync`،
  `SearchCorrespondenceTargetsAsync`، `SearchDocumentLawyerTargetsAsync`،
  `SearchScopeDelegateTargetsAsync`، `GetDocumentScopeKeysAsync`،
  `IsAssignedFollowerAsync`، `ListBySourceAsync`، `FindByTargetAsync`،
  `ResolveForUserAsync`، `IsDocumentInScopeAsync` — كلها `AsNoTracking` أو عدّ/وجود.

## 4. الحقول والمعاملات والاستيرادات الميتة

| الموضع | قبل | بعد | المحذوف |
|---|---|---|---|
| `CorrespondenceService` | 12 حقلًا / 12 معاملًا | 12 / 12 | لا شيء — كل حقل مستخدم (أدنى تكرار 3) |
| `CorrespondencesController` | 6 معاملات | 11 | `ICorrespondenceService` (وحيد) |
| `PortalController` | 8 معاملات | 12 | `ICorrespondenceService` (وحيد) |
| ملفات الخدمة | 5 partials | 5 | `Queries.cs` أُعيد تسميته `Mapping.cs` (لم يُحذف محتوى إسقاط) |

- **لماذا لم يُحذف أي حقل؟** `_documents`/`_clock`/`_timeZone` تستخدمها حرّاس/إسقاطات
  مشتركة يستدعيها مسار تأكيد المشاهدة (عبر `ICorrespondenceWriteRules`) ومسار التسطير؛
  و`_circuits`/`_appeals`/`_delegations`/`_portal` تستخدمها `MayAttachAsync` المشتركة
  وعقد الكتابة. لذا لا يوجد حقل أو معامل ميت، والمنشئ بقي 12 معاملًا.
- **الاستيرادات:** حُذفت `Common`/`DTOs` من `Access`، و`Common.Interfaces`/`DTOs` من
  `Helpers`، و`Common`/`Common.Interfaces`/`Common.Security`/`Entities`/`Enums` من
  ملف الإسقاط بعد إعادة التسمية، و`Common.Interfaces` من `Targets`، و
  `DocGenerator.Application.Services` من `CorrespondencesController`.

## 5. عدّ `DI`

| القياس | القيمة |
|---|---|
| تسجيلات `IQueryHandler` | 38 |
| تسجيلات `ICommandHandler` | 118 |
| الإجمالي | **156** |
| أصناف المعالجات في `UseCases` | **156** |
| غير مسجَّل / مكرر | 0 / 0 |

- حُذف سطر `services.AddScoped<ICorrespondenceService>(...)` وبقي
  `CorrespondenceService` + `ICorrespondenceWriteRules` كما هما.

## 6. المتحكم والاستجابة

- `CorrespondencesController`: المسارات كما هي (`GET api/correspondence`،
  `filter-options`، `targets`، `unseen-count`، `document/{documentId:int}`، `{id:int}`،
  و`POST`/`addenda`/`replies`/`mark-seen`)، و`[Authorize(Roles = "lawyer,head,subhead,manager,admin")]`
  على الصنف، وحرّاس `IsSubHead && SectionId is null` و
  `CanCreateCorrespondences`/`CanSeeAdministrativeBranch`/`IsEntityManager`/`IsLawyerOrHead`
  بنفس الترتيب.
- نفس الأجسام: `PagedResult<CorrespondenceListItemDto>`، `{ governorates }`،
  `List<CorrespondenceTargetDto>`، `{ count }`، `List<CorrespondenceListItemDto>`،
  `CorrespondenceDto`، ونفس `ApiProblem` عند `KeyNotFoundException` (404).
- `PortalController`: نفس المسارات الخمسة، و`FileCorrespondence` يبقي إخفاء `404`
  لكل من المفقود وغير المصرَّح (لا تمييز)، و`Create` يبقي
  `CreatedAtAction(nameof(CorrespondenceGet), ...)`.
- لا تغيير في `StatusCodes` ولا في نصوص الرسائل.
- `CreatedAtAction` في المتحكمين ما زال يشير إلى إجراء `Get`/`CorrespondenceGet`
  نفسه (لم يُغيَّر اسم الإجراء).

## 7. الاختبارات

- جسر `CorrespondenceReadQueryExtensions`: 6 امتدادات مطابقة لتوقيعات الواجهة المحذوفة
  (بما فيها الافتراضيات `ct = default` وترتيب `actorSectionId` الأخير).
- **46** موضع نداء حُوِّل (38 في `CorrespondenceServiceTests` + 8 في
  `SubHeadReviewCorrespondenceTests`) بلا تعديل توقّع واحد.
- بناء المعالجات في الاختبارين يستخدم `TestClock.Provider`/`TestClock.TimeZone` نفسيهما
  اللذين كانت تستخدمهما الخدمة، فلا ينزاح `ServerClock.CurrentYear`.
- لا تغيير في أعداد الاختبارات: Application 758/1659/0، Api 901/615/0
  (والـ`Sequential` منفصلًا 0/5/0 محليًا).

## الهجرات

**لا هجرة جديدة.** المعلّق يبقى **6**.
