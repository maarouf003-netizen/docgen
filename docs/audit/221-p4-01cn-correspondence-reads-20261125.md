# `P4-01cn` — نقل قراءات المراسلات الست إلى Query Handlers — 2026-11-25

> **النطاق:** المرحلة الخامسة عشرة في سلسلة نقل ملكية `use-case`: تحويل **كل** أسطح
> القراءة الستة على `ICorrespondenceService` — قائمة المراسلات بحسب الدور، ومراسلة
> برسائلها، وعدّاد غير المؤكَّد المشاهدة، ومراسلات ملف محدد، ومحافظات فلتر الإدارة،
> ومرشحو الاستلام بالاسم — إلى استعلامات `IQuery<TResponse>` ومعالجات
> `QueryHandlerBase` تحت `backend/src/DocGenerator.Application/UseCases/Correspondences/`،
> **قراءة صرفة بلا كتابة ولا معاملة ولا تدقيق جديد**. الفرع
> `refactor/p4-01cn-correspondence-reads` من `master` (`47bc0dd3`)، بحفاظ حرفي على
> السلوك والنطاق والرسائل ورموز الحالة.

## ما نُفِّذ

| الاستعلام | المعالج | الاعتمادات | النتيجة |
|---|---|---|---|
| `SearchCorrespondencesQuery(ActorUserId, Role, ActorBranchId, Q, Governorate, Importance, Page, PerPage, ActorSectionId)` | `SearchCorrespondencesQueryHandler` | `ICorrespondenceRepository` + `IBranchRepository` + `TimeProvider` + `TimeZoneInfo` | `PagedResult<CorrespondenceListItemDto>` |
| `GetCorrespondenceQuery(Id, ActorUserId, Role, ActorBranchId, ActorSectionId)` | `GetCorrespondenceQueryHandler` | `ICorrespondenceRepository` + `IDocumentRepository` + `IRepository<ExecutionCircuit>` + `IBranchRepository` + `TimeProvider` + `TimeZoneInfo` | `CorrespondenceDto` |
| `CountUnseenCorrespondencesQuery(ActorUserId)` | `CountUnseenCorrespondencesQueryHandler` | `ICorrespondenceRepository` | `int` |
| `ListCorrespondencesByDocumentQuery(DocumentId, ActorUserId, Role, ActorBranchId, ActorSectionId)` | `ListCorrespondencesByDocumentQueryHandler` | `ICorrespondenceRepository` + `IDocumentRepository` + `IRepository<ExecutionCircuit>` + `IAppealRepository` + `IDelegationRepository` + `IPortalRepository` + `IBranchRepository` + `TimeProvider` + `TimeZoneInfo` | `List<CorrespondenceListItemDto>` |
| `GetCorrespondenceGovernoratesQuery()` | `GetCorrespondenceGovernoratesQueryHandler` | `ICorrespondenceRepository` | `List<string>` |
| `SearchCorrespondenceTargetsQuery(ActorUserId, Role, ActorBranchId, Q, DocumentId, ActorSectionId)` | `SearchCorrespondenceTargetsQueryHandler` | `IDocumentRepository` + `IUserRepository` + `IRepository<ExecutionCircuit>` + `IAppealRepository` + `IDelegationRepository` + `IPortalRepository` | `List<CorrespondenceTargetDto>` |

| البند | التفصيل |
|---|---|
| العقود | حُذفت الأعضاء الستة من `ICorrespondenceService`؛ صارت الواجهة **فارغة فحُذفت نهائيًا** (6 → 0) مع تسجيلها في `DI`. لم تُضف أعضاء جديدة إلى `ICorrespondenceWriteRules` (بقي أعضاؤه العشرة حرفيًا) |
| الخدمة | أُعيد تسمية `CorrespondenceService.Queries.cs` إلى `CorrespondenceService.Mapping.cs` بعد إفراغه من القراءات وبقاء الإسقاطات فيه؛ وبقيت الخدمة تنفيذًا وحيدًا لعقد مساعدات الكتابة. الحقول **12 → 12** والمنشئ **12 → 12**: لا حقل ميتًا — `_documents`/`_clock`/`_timeZone` ما زالت لازمة لحرّاس العرض والإسقاط المشتركين المستخدمين في مسار تأكيد المشاهدة/التسطير، و`_circuits`/`_appeals`/`_delegations`/`_portal` لحرّاس الكتابة |
| المساعدات | `MayAttachAsync` (حارس التسطير) و`FollowsDocumentAsync` و`CanViewAsync` و`OwnerSectionOfAsync` و`ResolveHeadGovernorateAsync` و`IsDocumentInDelegateScopeAsync` صارت `internal static` تستقبل مستودعاتها صراحةً وتفوّض إليها عقود الكتابة؛ و`ToDto`/`ToListItem`/`FileContextOf`/`ToMessageDto` صارت `internal static` (الأولان والثالث يستقبلان `TimeProvider`/`TimeZoneInfo` صراحةً)؛ و`CanWrite` صارت `internal static`؛ ونُقلت `MayListDocumentAsync` و`ResolveEligibleTargetsAsync` و`ToTargetDto` و`NormalizeImportanceFilter` و`TargetsLimit` إلى معالجيها الوحيدين (تعريف واحد بلا تكرار) |
| عدم الكتابة | لا `SaveChangesAsync`/`AddAsync`/`Add(`/`Remove`/`_uow`/`ITransactionRunner`/`ITransactionalRequest`/`IAuditedRequest`/`IAuditLogger`/`LogAsync`/`RunAsync`/`ClearChanges` في ملفات الاستعلام الستة (فحص نصي شامل: 0 مطابقة) |
| `DI` | حُذف تسجيل `ICorrespondenceService` وأُضيفت ستة تسجيلات `IQueryHandler<Query, Response>`؛ الإجمالي **150 → 156** تسجيلًا (38 استعلامًا + 118 أمرًا) مقابل **156** صنف معالج، 0 غير مسجَّل، 0 مكرر |
| المتحكم | `CorrespondencesController` يحقن المعالجات الستة بدل الخدمة (منشئ **6 → 11** معاملًا)، و`PortalController` يحقن خمسة منها (بلا المحافظات) بدل الخدمة (منشئ **8 → 12**)؛ نفس المسارات (`api/correspondence` و`api/portal/correspondence`) ونفس `[Authorize]` وحرّاس `IsSubHead && SectionId is null` و`CanCreateCorrespondences`/`CanSeeAdministrativeBranch`/`IsEntityManager`/`IsLawyerOrHead` ونفس `Ok(...)`/`Forbid()`/`ApiProblem`/`NotFound()` ورموز الحالة وأشكال الأجسام (`{ count }`، `{ governorates }`، `PagedResult` كما هو) |
| البوابة | `PortalController` صار يستدعي معالجات القراءة مباشرة بدل `ICorrespondenceService` (الخدمة لم تكن مستخدمة في `PortalService` أصلًا)؛ `FileCorrespondence` يحافظ على إخفاء 404 لكل من `KeyNotFoundException` و`UnauthorizedAccessException` |
| الاختبارات | جسر جديد `CorrespondenceReadQueryExtensions` (ستة امتدادات بنفس توقيعات الخدمة القديمة) وتحويل **46 موضع نداء** (38 في `CorrespondenceServiceTests`، و8 في `SubHeadReviewCorrespondenceTests`) بلا تغيير أي توقّع؛ وأُضيف بناء المعالجات في الملفين (ساعة `TestClock` والمنطقة نفسها) |
| تنظيف ميت | حُذفت استيرادات ميتة بعد نقل القراءات: `Common`/`DTOs` من `Access`، و`Common.Interfaces`/`DTOs` من `Helpers`، و`Common`/`Common.Interfaces`/`Common.Security`/`Entities`/`Enums` من `Queries` (الملف أُعيد تسميته `Mapping`، فبقي `Common`/`DTOs`/`Entities` فقط)، و`Common.Interfaces` من `Targets`، و`Services` من `CorrespondencesController` |

> **قرار موثّق:** لم يُحقن أي عقد كتابة (`ICorrespondenceWriteRules`) داخل معالج قراءة،
> ولم يُكرَّر منطق حرّاس الوصول؛ بدلًا من ذلك رُقّيت الحرّاس والإسقاطات المشتركة إلى
> `internal static` تستقبل مستودعاتها/ساعتها صراحةً (مصدر واحد)، فبقي كل معالج قراءة
> معتمدًا على مستودعاته فقط. و`SearchCorrespondenceTargetsQuery` هو الاستعلام الوحيد
> الذي يستدعي حارس الكتابة (`CanWrite`/`MayAttachAsync`) لأن بوابة البحث عن المستلمين
> هي صلاحية التسطير نفسها — كما كانت في الخدمة حرفيًا.
>
> **ملاحظة صدق:** `CorrespondenceServiceFactory` المذكور في التكليف **غير موجود** في
> المستودع؛ مواضع البناء كانت `new CorrespondenceService(...)` مباشرة في ثلاثة ملفات
> اختبار، ولم تتغير توقيعاتها (12 معاملًا) لأن أي حقل لم يُحذف.

## إثبات الحفاظ على السلوك (لكل دالة)

> التطبيع: استبدال أسماء المعاملات بخصائص `query.…`، وإعادة تسمية الحقول إلى
> بارامترات (`_letters`/`_documents`/`_circuits`/`_appeals`/`_delegations`/`_branches`/
> `_clock`/`_timeZone`)، وإسقاط تأهيل `CorrespondenceService.`، ثم مقارنة الأسطر
> الفعّالة (بلا فراغات/تعليقات).

| الدالة | الأسطر الفعّالة (قبل/بعد) | `diffs` | الاستثناءات الموثّقة |
|---|---|---|---|
| `SearchAsync` → `SearchCoreAsync` | 39 / 42 | 0 | لفّ `ResolveHeadGovernorateAsync` على سطرين (تمرير `_branches`)، ولفّ `items.Select(ToListItem)` على ثلاثة أسطر (تمرير `_clock`/`_timeZone`) |
| `GetByIdAsync` → `HandleCoreAsync` | 5 / 7 | 0 | تمرير `documents`/`circuits`/`branches` إلى `CanViewAsync`، و`_clock`/`_timeZone` إلى `ToDto` |
| `CountUnseenAsync` | 1 / 1 | 0 | — |
| `GetGovernoratesAsync` | 1 / 1 | 0 | — |
| `ListByDocumentAsync` → `HandleCoreAsync` | 18 / 22 | 0 | لفظ `MayListDocumentAsync` و`ResolveHeadGovernorateAsync` على سطرين، ولفّ `letters.Select(ToListItem)` على ثلاثة أسطر |
| `MayListDocumentAsync` (منقولة) | 23 / 25 | 0 | تمرير `_appeals`/`_delegations` إلى `FollowsDocumentAsync` و`_portal` إلى `IsDocumentInDelegateScopeAsync` |
| `SearchTargetsAsync` → `HandleCoreAsync` | 11 / 13 | 0 | تمرير `_circuits`/`_appeals`/`_delegations`/`_portal` إلى `MayAttachAsync` |
| `ResolveEligibleTargetsAsync` (منقولة) | 15 / 15 | 0 | — |
| `ToTargetDto` (منقولة) | 6 / 6 | 0 | — |
| **الإجمالي المنقول** | **119 / 132** | **0** | 13 سطرًا ميكانيكيًا فقط (لفّ وتأهيل استدعاءات) |

| المساعد (بقي على الخدمة) | قبل/بعد | `diffs` |
|---|---|---|
| `MayAttachAsync` | 21 / 21 | 0 (مستودعات صراحةً) |
| `FollowsDocumentAsync` | 8 / 8 | 0 (مستودعان صراحةً) |
| `CanViewAsync` | 17 / 17 | 0 (ثلاثة مستودعات صراحةً) |
| `OwnerSectionOfAsync` | 7 / 7 | 0 (مستودعان صراحةً) |
| `ResolveHeadGovernorateAsync` | 3 / 3 | 0 (مستودع صراحةً) |
| `IsDocumentInDelegateScopeAsync` | 4 / 4 | 0 (مستودع صراحةً) |
| `ToMessageDto` | 9 / 9 | 0 |
| `FileContextOf` | 14 / 14 | 0 (ساعة/منطقة صراحةً) |
| `ToDto` | 29 / 29 | 0 (ساعة/منطقة صراحةً) |
| `ToListItem` | 25 / 25 | 0 (ساعة/منطقة صراحةً) |
| **الإجمالي** | **137 / 137** | **0** |

- **الإجمالي الكلي المنقول والمرقّى:** **256 / 269** سطرًا فعّالًا، وفرق الرموز الدلالي **0**.
- **مقارنة النصوص الحرفية (اتجاهان):** المقطع الكامل (القراءات + المساعدات، مع نقل
  `فلتر الأهمية غير صالح` من `Helpers` إلى معالج البحث) يحوي **32** نصًا حرفيًا و**24**
  نصًا مميزًا، والنتيجة `missing = 0` و`added = 0`.
- **النطاق/الصلاحيات:** شروط `SearchAsync` (المدير/المشرف بمحافظة إجبارية، رئيس بلا فرع،
  شعبة بلا شعبة، الدور غير المخوّل)، و`CanViewAsync` (الطرفان، محافظة الرئيس، فرع
  الشعبة + ملك دائرتها، المدير/المشرف)، و`MayListDocumentAsync` (حارس `null` الصريح
  للفرع الإرثي) بقيت بنفس المواضع والترتيب؛ ولم يتغيّر أي سطر من حرّاس المتحكم.
- **بلا كتابة:** الاستعلامات الستة تقرأ فقط عبر
  `SearchForPartyAsync`/`SearchForBranchAsync`/`SearchForScopeAsync`/`SearchAllAsync`/
  `GetByIdWithDetailsAsync`/`CountUnseenForTargetAsync`/`ListByDocumentAsync`/
  `GetGovernoratesAsync`/`SearchCorrespondenceTargetsAsync`/`SearchDocumentLawyerTargetsAsync`/
  `SearchScopeDelegateTargetsAsync`/`GetDocumentScopeKeysAsync` ومستودعات القراءة المساعدة؛
  لا حفظ ولا معاملة ولا تدقيق.
- **الفاصل الحتمي/الترتيب:** `Math.Max(1, page)` ثم `Math.Clamp(perPage, 1, 100)` ثم
  `NormalizeImportanceFilter` بنفس الترتيب قبل حجب المدير/المشرف، و`ServerClock.CurrentYear`
  يُحسب داخل `FileContextOf` كما كان، وترتيب الرسائل/التوثيق في الإسقاطات كما هو
  (`OrderBy(m => m.Id)` و`OrderBy(r => r.SeenAt)`).

## التحقق

`build -warnaserror` 0/0 · `format --verify-no-changes` نظيف · Architecture 29/29 ·
Application 758/1659/0 · Api 901/615/0 (والـ`Sequential` منفصلًا: 0/5/0 تخطٍّ محلي) ·
frontend 1809/1809 (171 ملفًا) + `oxlint`/`tsc -b`/`build`/`budget`/`guard`/`generate:api`
بلا فرق (`git diff --exit-code -- src/api/generated.ts` = 0 مع استعادة ضجيج `CRLF`) ·
الحرّاس الستة نظيفة · `dependency-audit` 9 مشاريع · `npm-audit-guard` 0 ثغرة ·
`has-pending-model-changes` نظيف · `DI` 156/156/0 · 0 حقل ميت · 0 partial فارغ.

> اختبارات `PostgreSQL` (ومنها كل مسارات قراءات المراسلات المعدَّلة) تُخطّى محليًا لغياب
> `DOCGEN_TEST_POSTGRES`؛ `backend-postgres` في `CI` هو المرجع القاطع للسلوك على القاعدة.

## الهجرات

**لا هجرة جديدة.** المعلّق يبقى **6**.
