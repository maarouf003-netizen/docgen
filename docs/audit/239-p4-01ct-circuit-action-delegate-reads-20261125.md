# `P4-01ct` — نقل قراءات الدوائر/الإجراءات/المندوبين الثماني إلى Query Handlers وحذف واجهة المندوبين — 2026-11-25

> **النطاق:** المرحلة الحادية والعشرون في سلسلة نقل ملكية `use-case`: تحويل **كل**
> أسطح القراءة الصرفة المتبقية على خدمات الدوائر/الإجراءات/المندوبين إلى استعلامات
> `IQuery<TResponse>` ومعالجات `QueryHandlerBase` تحت
> `backend/src/DocGenerator.Application/UseCases/`:
> - `IExecutionCircuitService`: ستة قراءات (قوائم الدوائر الأربع، وملفات الدائرة،
>   والمعلقات، والإحصاءات) إلى `UseCases/ExecutionCircuits/`.
> - `IExecutionActionService`: قراءة إجراءات الملف الواحدة إلى `UseCases/ExecutionActions/`.
> - `IEntityDelegateService`: قائمة المندوبين الواحدة إلى `UseCases/PublicEntities/`
>   (اتساقًا مع `EntityDelegateCommands.cs` القائم هناك).
>
> **قراءة صرفة بلا كتابة ولا معاملة ولا تدقيق** في كل الاستعلامات الثمانية.
> **عضوان ليسا قراءة صرفة وبقيا في خدمتيهما بلا نقل:**
> `SyncPendingAlertsAfterTransferAsync` (يكتب تنبيهات ويحفظ ويبطل كاش الدوائر داخل
> معاملة النقل — عضو الواجهة السابع)، و`SeedInitialActionsAsync` (يزرع إجراءات داخل
> معاملة حفظ المستند). الفرع `refactor/p4-01ct-circuit-action-delegate-reads` من
> `master` (`cb2f145e`)، بحفاظ حرفي على السلوك والنطاق والرسائل ورموز الحالة وأشكال
> الاستجابة.

## ما نُفِّذ

| الاستعلام | المعالج | الاعتمادات | النتيجة |
|---|---|---|---|
| `ListMineExecutionCircuitsQuery` | `ListMineExecutionCircuitsQueryHandler` | `IRepository<Branch>` + `IRepository<ExecutionCircuit>` + `IDocumentRepository` + `IRepository<Section>` + `IReferenceDataCache?` | `List<ExecutionCircuitDto>` |
| `ListCircuitFilesQuery` | `ListCircuitFilesQueryHandler` | `IRepository<Branch>` + `IRepository<ExecutionCircuit>` + `IDocumentRepository` | `List<CircuitFileDto>` |
| `ListExecutionCircuitsForLawyerQuery` | `ListExecutionCircuitsForLawyerQueryHandler` | `IRepository<ExecutionCircuit>` + `IRepository<Section>` + `IReferenceDataCache?` | `List<ExecutionCircuitDto>` |
| `ListExecutionCircuitsForDelegationQuery` | `ListExecutionCircuitsForDelegationQueryHandler` | `IRepository<Branch>` + `IRepository<ExecutionCircuit>` + `IReferenceDataCache?` | `List<ExecutionCircuitDto>` |
| `ListMyPendingRegistrationsQuery` | `ListMyPendingRegistrationsQueryHandler` | `IDocumentRepository` + `IRepository<ExecutionCircuit>` + `IReferenceDataCache?` | `List<PendingRegistrationDto>` |
| `GetCircuitStatsQuery` | `GetCircuitStatsQueryHandler` | `IRepository<ExecutionCircuit>` + `IRepository<Branch>` + `IRepository<Section>` + `IDocumentRepository` + `IReferenceDataCache?` | `List<CircuitStatsDto>` |
| `GetExecutionActionsQuery` | `GetExecutionActionsQueryHandler` | `IDocumentRepository` | `List<ExecutionActionDto>` |
| `ListEntityDelegatesQuery` | `ListEntityDelegatesQueryHandler` | `IUserRepository` + `IPublicEntityRepository` + `IRepository<Branch>?` | `List<DelegateDto>` |

| البند | التفصيل |
|---|---|
| العقود | `IExecutionCircuitService` **7 → 1** عضوًا (بقي خطاف التنبيه الكاتب)، `IExecutionActionService` **2 → 1** (بقي الزرع الكاتب)، `IEntityDelegateService` أُفرغت ثم حُذفت نهائيًا (**1 → 0**) مع تسجيلها في `DI`؛ الإجمالي **10 → 2** عضوًا |
| الخدمات | `ExecutionCircuitService` تُقلِّص partials **4 → 2** (حُذفت `.Queries` و`.Helpers` و`.PendingRegistrations`، وأُضيف `.Alerts` لخطاف الكتابة) ومنشئها **11 → 7** وسائط بعد إسقاط `_circuits`/`_branches`/`_sections`/`_delegations` الميتة؛ `ExecutionActionService` منشئ **6 → 5** بعد إسقاط `_documents` الميت؛ `EntityDelegateService` منشئ **7 → 2** بعد إسقاط `_users` وأربعة اعتماديات ميتة سابقة (`_hasher`/`_uow`/`_tx`/`_audit`) |
| المساعدات | `Common/ReferenceDataQueries.cs` الجديد: مصدر واحد للقراءات المرجعية المخزّنة (الدوائر/الشعب/الفروع) يستهلكه استعلام الدوائر و`ListBranchesQuery` و`ListSectionsQuery`/`GetSectionQuery` — بلا نسخة ثانية؛ `ExecutionCircuitProjection` (بوابة فرع المحافظة + أسماء الشعب)؛ `EntityDelegateProjection` (محافظة الفاعل، نطاق المحافظة، الحراسة، الإسقاط) يستهلكها الاستعلام ومساعدات الكتابة معًا |
| `DI` | حُذف تسجيل خدمة واحد وأُضيفت ثمانية تسجيلات `IQueryHandler<Query, Response>`؛ الإجمالي **198 → 206** معالجًا (**88** استعلامًا + **118** أمرًا)، و**206** صنف معالج، 0 غير مسجَّل، 0 مكرر |
| المتحكمون | `ExecutionCircuitsController` (منشئ **12 → 16**) و`PendingRegistrationsController`/`DocumentsController`/`DelegatesController` (نفس عدد المعاملات بتبديل النوع) يحقنون المعالجات بدل الخدمات؛ نفس المسارات و`[Authorize]`/السياسات وحرّاس القدرات/النطاق و`Ok`/`Forbid`/`CreatedAtAction`/`NoContent`/`ApiProblem` وأشكال الأجسام |
| الاختبارات | ثلاثة جسور في `TestHelpers`: `ExecutionCircuitReadQueryExtensions` (سجل `ExecutionCircuitReadHandlers` بستة معالجات، غطّى **20** موضع نداء)، و`ExecutionActionReadQueryExtensions` (سجل `ExecutionActionHandlers`: كتابة + قراءة، غطّى **9**)، و`EntityDelegateReadQueryExtensions` (**6**) — بلا تغيير أي توقّع؛ وأُضيف اختبار توصيل واحد يثبت تسجيل الاستعلامات الثمانية مرة واحدة؛ وأُزيل مزيّف `FakeCircuitService` المتقادم |
| النهاية | لا تغيير في `OpenAPI` ولا في العميل المولَّد — العقود الخارجية كما هي |

## إثبات الحفاظ على السلوك (لكل دالة)

> التطبيع: استبدال أسماء المعاملات بخصائص `query.…`، وإسقاط تأهيل
> `_circuits`/`_branches`/`_sections`/`_documents`/`_users`/`_registry`، وإقحام
> `ReferenceDataQueries.`/`ExecutionCircuitProjection.`/`EntityDelegateProjection.` قبل
> المساعدات، ثم مقارنة **الأسطر الفعّالة** (لا الأسطر الملفوفة).

| الدالة | الأصلية/المعالج | `diffs` دلالية | الاستثناءات الموثّقة |
|---|---|---|---|
| `ListMineAsync` | 15 / 15 | 0 | — |
| `ListFilesAsync` | 5 / 6 | 0 | سطر لفّ ميكانيكي واحد لنداء `ExecutionCircuitRules.GetOwnedAsync` |
| `ListForLawyerAsync` | 7 / 8 | 0 | سطر لفّ ميكانيكي واحد لشرط `Where` |
| `ListForDelegationAsync` | 11 / 12 | 0 | سطر لفّ ميكانيكي واحد لشرط المحافظة |
| `CircuitStatsAsync` | 28 / 30 | 0 | سطرا لفّ ميكانيكيان (`query.BranchId is null` و`CountWithoutCircuitAsync`) |
| `MyPendingRegistrationsAsync` | 22 / 22 | 0 | — |
| `GetExecutionActionsAsync` | 7 / 7 | 0 | إحالة `ExecutionActionService.ToDto` |
| `ListAsync` (المندوبون) | 8 / 10 | 0 | سطرا لفّ ميكانيكيان لشرط المحافظة ونداء الإسقاط |
| **الإجمالي** | **103 / 110** | **0** | سبعة أسطر لفّ ميكانيكية فقط |

| المساعدة | الأصلية/النسخة المشتركة | `diffs` دلالية |
|---|---|---|
| `RequireBranchWithGovernorateAsync` | 3 / 4 | 0 (سطر لفّ ميكانيكي للتوقيع) |
| `SectionNamesAsync` | 2 / 2 | 0 |
| `HeadGovernorateAsync` | 5 / 5 | 0 |
| `EnsureHeadGovernorateAsync` | 5 / 5 | 0 |
| `IsScopeInGovernorateAsync` | 11 / 11 | 0 |
| `BuildDtoWithScopeAsync` | 15 / 15 | 0 |
| **الإجمالي** | **41 / 42** | **0** |

- **النصوص الحرفية (اتجاهان):** **2/2** نص منقول (`"بلا دائرة"` في الإحصاءات،
  و`"نطاق المندوب خارج محافظة فرعك"` في حراسة الكتابة)؛ `missing = 0` و`added = 0`.
  بقية الرسائل (`"الدائرة غير موجودة"` ونطاق الملكية) بقيت في
  `ExecutionCircuitRules` كما هي بلا مساس.
- **عدم الكتابة:** فحص نصي لكل ملفات `UseCases/ExecutionCircuits/List*Query.cs`
  و`GetCircuitStatsQuery.cs` و`UseCases/ExecutionActions/GetExecutionActionsQuery.cs`
  و`UseCases/PublicEntities/ListEntityDelegatesQuery.cs` والمساعدات المشتركة:
  `SaveChanges`/`AddAsync`/`Remove`/`Update`/`_uow`/`ITransactionRunner`/
  `ITransactionalRequest`/`RunAsync`/`BeginTransaction`/`ExecuteUpdate`/`ExecuteDelete`/
  `IAuditLogger`/`IAuditedRequest` = **0 مطابقة**.
- **النطاق/الصلاحيات:** الحرّاس بقوا في المتحكمات وقبل الاستعلام بنفس الترتيب —
  `RequireOwnerScope` (`CanManageExecutionCircuits` + claim الفرع + شعبة رئيس الشعبة)
  لمسارات `mine`/`files`/`stats`/`stats/export`، و`[Authorize(Roles = "lawyer,head,subhead")]`
  مع محور `limitToOwner` ورفض رئيس شعبة بلا شعبة لـ`for-lawyer`، و
  `[Authorize(Roles = "manager,admin,head,subhead")]` لـ`stats` مع
  `HasFullAccess`/النطاق، و`[Authorize(Roles = "lawyer")]` للمعلقات، و`CanManageDelegates`
  لقائمة المندوبين؛ المعالجات لا تكرر فحص صلاحية ولا تضيّق النطاق.
- **الفاصل الحتمي:** نفس ترتيب `OrderBy(Name)`، و`ThenBy` في الإحصاءات
  (`BranchId` ثم `Name`)، ونفس تطبيع/تحقق `GovernorateCatalog`، ونفس صف «بلا دائرة»
  بشرط `FileCount > 0` و`OwnerSectionId is null`، ونفس `OrderBy(d.Id)` و
  `OrderByDescending(CreatedAt)`.
- **الكاش:** نفس مفاتيح `ReferenceDataGroups` والمجموعة `"all"` ونفس
  `ListNoTrackingAsync`؛ العدّادات الحيّة (`CountByCircuits`/`CountLawyersByCircuits`/
  `CountWithoutCircuit`) خارج الكاش كما كانت.

## التحقق

`build -warnaserror` 0/0 · `format --verify-no-changes` نظيف · Architecture 29/29 ·
Application 770/1661/0 (منها اختبار التوصيل الجديد) · Api 901/620/0 (والـ`Sequential`
منفصلًا: 0/5/0 تخطٍّ محلي) · frontend 1809/1809 (171 ملفًا) +
`oxlint`/`tsc -b`/`build`/`guard`/`generate:api` بلا فرق
(`git diff --ignore-cr-at-eol --exit-code -- src/api/generated.ts` = 0 مع استعادة ضجيج
`CRLF`) · الحرّاس الستة نظيفة · `dependency-audit` 9 مشاريع · `npm-audit-guard` 0 ثغرة ·
`has-pending-model-changes` نظيف · `DI` 206/206/0 · 0 حقل ميت · 0 partial فارغ.

> اختبارات `PostgreSQL` (ومنها كل مسارات الدوائر/الإجراءات/المندوبين) تُخطّى محليًا
> لغياب `DOCGEN_TEST_POSTGRES`؛ `backend-postgres` في `CI` هو المرجع القاطع للسلوك
> على القاعدة.

## الهجرات

**لا هجرة جديدة** (لم يُمسّ أي كيان أو تعيين). المعلّق يبقى **6**.
