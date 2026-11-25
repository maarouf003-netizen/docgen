# `P4-01cs` — نقل قراءات الهيكل التنظيمي (المستخدمون/المصادقة/الفروع/الشعب) إلى Query Handlers وحذف الخدمات الأربع — 2026-11-25

> **النطاق:** المرحلة العشرون في سلسلة نقل ملكية `use-case`: تحويل **كل** أسطح
> القراءة الصرفة على خدمات الهيكل التنظيمي الأربع إلى استعلامات `IQuery<TResponse>`
> ومعالجات `QueryHandlerBase` تحت `backend/src/DocGenerator.Application/UseCases/`:
> - `IUserManagementService`: قائمتا المحامين (اختصار + كامل)، وقائمة المستخدمين، وسجل التعاقب.
> - `IAuthService`: ملف المستخدم (`GetUserAsync`).
> - `IBranchManagementService`: قائمة الفروع.
> - `ISectionService`: قائمة الشعب، وشعبة واحدة.
>
> **قراءة صرفة بلا كتابة ولا معاملة ولا تدقيق** في كل الاستعلامات السبعة.
> الفرع `refactor/p4-01cs-org-structure-reads` من `master` (`605294bb`)، بحفاظ حرفي
> على السلوك والنطاق والرسائل ورموز الحالة وأشكال الاستجابة.

## ما نُفِّذ

| الاستعلام | المعالج | الاعتمادات | النتيجة |
|---|---|---|---|
| `ListLawyersQuery` | `ListLawyersQueryHandler` | `IUserRepository` + `IRepository<ExecutionCircuit>` + `IDocumentRepository` | `List<LawyerListItemDto>` |
| `ListUsersQuery` | `ListUsersQueryHandler` | `IUserRepository` | `List<UserListItemDto>` |
| `ListSuccessionQuery` | `ListSuccessionQueryHandler` | `IRepository<HeadSuccession>` + `IRepository<Branch>` + `IRepository<Section>` + `IUserRepository` | `List<HeadSuccessionDto>` |
| `GetUserQuery` | `GetUserQueryHandler` | `IUserRepository` | `UserDto?` |
| `ListBranchesQuery` | `ListBranchesQueryHandler` | `IBranchRepository` + `IReferenceDataCache?` | `List<BranchDto>` |
| `ListSectionsQuery` | `ListSectionsQueryHandler` | `IRepository<Section>` + `IRepository<Branch>` + `IRepository<ExecutionCircuit>` + `IUserRepository` + `IReferenceDataCache?` | `List<SectionDto>` |
| `GetSectionQuery` | `GetSectionQueryHandler` | نفس اعتماديات `ListSectionsQuery` | `SectionDto?` |

| البند | التفصيل |
|---|---|
| العقود | أُفرغت الواجهات الأربع ثم حُذفت نهائيًا (`IUserManagementService` **4 → 0**، `IAuthService` **1 → 0**، `IBranchManagementService` **1 → 0**، `ISectionService` **2 → 0**) مع تسجيلاتها في `DI` |
| الخدمات | `AuthService` و`BranchManagementService` و`SectionService` حُذفت (**3 → 0**)؛ و`UserManagementService` بقي **مساعدات كتابة فقط** (`IUserManagementLawyerWriteRules` + `IUserManagementUserWriteRules`) بعد حذف `UserManagementService.Lawyers.cs` و`UserManagementService.Users.cs` وإخراج `ListSuccessionAsync` من `.Succession.cs` |
| حقول ميتة | حُذف `_circuits` (كان يخدم قائمة المحامين فقط) و`_hasher`/`_tx` (ميتان قبل النقل) من `UserManagementService`، فتقاصّ المنشئ **12 → 10** وسيطًا |
| المساعدات | أُبقيت على مصدر واحد بلا تكرار: `UserProjection` (`ToLawyerDto`/`ToUserDto`) يستهلكها المعالجان ومساعدات الكتابة؛ `BranchProjection` (`NormalizeRequired`/`NormalizeOptional`/`ToDto`) و`SectionProjection` (قراءة الكاش + `NormalizeName`/`NameExists`/`ToDto`) يستهلكها المعالجان ومعالجات الإنشاء/التسمية/التفعيل |
| الكاش المرجعي | `IReferenceDataCache` بقي على عقده كما هو (واجهة وتسجيل `singleton` بلا تغيير)؛ المعالجان يحقنان `IReferenceDataCache?` بنفس نمط معالجات الأوامر، والقراءة المخزّنة نُقلت حرفيًا إلى `SectionProjection` |
| `DI` | حُذفت 4 تسجيلات خدمة وأُضيفت 7 تسجيلات `IQueryHandler<Query, Response>`؛ الإجمالي **191 → 198** معالجًا (**73 → 80** استعلامًا + **118** أمرًا)، و**198** صنف معالج، 0 غير مسجَّل، 0 مكرر |
| المتحكمون | `AuthController` و`BranchesController` و`HeadSuccessionController` بلا تغيير عدد معاملات المنشئ؛ `SectionsController` (منشئ **6 → 7**) و`UserManagementController` (**7 → 8**) يحقنان معالجين بدل خدمة واحدة؛ نفس المسارات و`[Authorize]` وحرّاس القدرات/النطاق و`Ok`/`Forbid`/`NotFound`/`ApiProblem` وأشكال الأجسام |
| الاختبارات | جسر جديد `UserReadQueryExtensions` بتوقيعات الخدمة القديمة فوق سجل `UserReadHandlers` (مصنع `UserReadHandlerFactory`) حوّل مواضع النداء بلا تغيير أي توقّع؛ وأُضيف اختبار توصيل `OrgStructureReadHandlerRegistrationTests` يثبت تسجيل كل استعلام مرة واحدة |
| النهاية | لا تغيير في `OpenAPI` ولا في العميل المولَّد — العقود الخارجية كما هي |

## إثبات الحفاظ على السلوك (لكل دالة)

> التطبيع: استبدال أسماء المعاملات بخصائص `query.…`، وإسقاط تأهيل
> `_users`/`_branches`/`_sections`/`_circuits`/`_documents`/`_successions`، وإقحام
> `UserProjection.`/`SectionProjection.`/`BranchProjection.` قبل المساعدات، ثم مقارنة
> **الجمل الفعّالة** (لا الأسطر الملفوفة).

| الدالة | الجمل الأصلية/المعالج | `diffs` دلالية | الاستثناءات الموثّقة |
|---|---|---|---|
| `ListLawyersAsync(branchId, ct)` | 1 / 1 | 0 | بقيت كتكوين اختصاري في جسر الاختبارات (المتحكم يمرّر الوسائط الستة صراحةً كما كان) |
| `ListLawyersAsync(6 وسائط)` | 22 / 22 | 0 | خصائص `query` فقط |
| `BranchLawyerListAsync` | 6 / 6 | 0 | إحالة `UserProjection.ToLawyerDto` |
| `ListUsersAsync` | 2 / 2 | 0 | إحالة `UserProjection.ToUserDto` |
| `ListSuccessionAsync` | 25 / 25 | 0 | خصائص `query` فقط |
| `GetUserAsync` | 2 / 2 | 0 | — |
| `ListBranchesAsync` | 10 / 10 | 0 | إحالة `BranchProjection.ToDto` |
| `ListBranchesTrackedAsync` | 3 / 3 | 0 | — (الكاش كما هو) |
| `ListSectionsAsync(branchId)` | 27 / 27 | 0 | قراءة الكاش عبر `SectionProjection` |
| `ListSectionsAsync(ct)` (خاصة) | 3 / 3 | 0 | نُقلت إلى `SectionProjection` بوسائط `(repo, cache, ct)` |
| `ListCircuitsAsync` (خاصة) | 3 / 3 | 0 | نُقلت إلى `SectionProjection` بوسائط `(repo, cache, ct)` |
| `GetSectionAsync` | 2 / 2 | 0 | — |
| `ToDtoAsync` (الشعبة) | 6 / 6 | 0 | الإسقاط عبر `SectionProjection.ToDto` |
| **الإجمالي** | **112 / 112** | **0** | — |
| `ToLawyerDto` | 9 / 9 | 0 | نُقل إلى `UserProjection` |
| `ToUserDto` | 11 / 11 | 0 | نُقل إلى `UserProjection` |
| `BranchProjection.NormalizeRequired` | 4 / 4 | 0 | — |
| `BranchProjection.NormalizeOptional` | 2 / 2 | 0 | — |
| `BranchProjection.ToDto` | 11 / 11 | 0 | — |
| `SectionProjection.NormalizeName` | 6 / 6 | 0 | — |
| `SectionProjection.NameExists` | 3 / 3 | 0 | — |
| `SectionProjection.ToDto` | 13 / 13 | 0 | — |
| **المساعدات** | **59 / 59** | **0** | — |

- **النصوص الحرفية (اتجاهان):** **8/8** نص في الجانبين (`وضع غير صالح — mine أو branch`،
  ورسائل فحص الفرع/الشعبة في قائمة المحامين، `"all"` لمجموعة الكاش، ورسائل الشعبة الثلاث
  و`"الفرع غير موجود"`)؛ `missing = 0` و`added = 0`.
- **عدم الكتابة:** فحص نصي لكل ملفات
  `UseCases/{Users,Auth,Branches,Sections}/{List*,Get*}Query.cs` والمساعدات:
  `SaveChanges`/`AddAsync`/`Remove`/`_uow`/`ITransactionRunner`/`ITransactionalRequest`/
  `RunAsync`/`BeginTransaction`/`ExecuteUpdate`/`ExecuteDelete`/`IAuditLogger`/
  `IAuditedRequest` = **0 مطابقة**.
- **النطاق/الصلاحيات:** الحرّاس بقوا في المتحكمات كما كانوا وقبل الاستعلام بنفس
  الترتيب — `CanManageUsers` (قائمة/تفاصيل المستخدم)، و`CanManageBranchLawyers` +
  `IsHeadOrSubHead`/`User.GetBranchId()` (قائمة المحامين ومحور `effectiveBranchId`)،
  و`CanManageBranches`، و`[Authorize(Roles = "manager,admin")]` لسجل التعاقب، و
  `[Authorize]` لملف المستخدم؛ المعالجات لا تكرر فحص صلاحية ولا تضيّق النطاق.
- **الفاصل الحتمي:** قائمة المحامين تُبقي تطبيع الوضع (`"mine"` افتراضيًا) ورفض غيره
  بنفس الرسالة، وترتيب `OrderBy(FullName)`، وقاعدة القدامى بلا منشئ، ونفس ترتيب
  `includeInactive || IsActive`.
- **الفروع:** `ListBranchesQueryHandler` يُبقي `_branches.ListAsync` المتتبَّع عند غياب
  الكاش، و`OrderBy(Name)`، والعدّادات الحيّة بعد القراءة.
- **الشعب:** نفس تحقق وجود الفرع (`ArgumentException("الفرع غير موجود")`)، وقصر
  النتائج على الفرع، وإرجاع قائمة فارغة قبل حساب الدوائر/الرؤساء، وإسقاط الرئيس الشاغل
  بالأقدم معرّفًا.

## التحقق

`build -warnaserror` 0/0 · `format --verify-no-changes` نظيف · Architecture 29/29 ·
Application 769/1661/0 (منها اختبار التوصيل الجديد) · Api 901/615/0 (والـ`Sequential`
منفصلًا: 0/5/0 تخطٍّ محلي) · frontend 1809/1809 (171 ملفًا) +
`oxlint`/`tsc -b`/`build`/`guard`/`generate:api` بلا فرق
(`git diff --ignore-cr-at-eol --exit-code -- src/api/generated.ts` = 0 مع استعادة ضجيج
`CRLF`) · الحرّاس الستة نظيفة · `dependency-audit` 9 مشاريع · `npm-audit-guard` 0 ثغرة ·
`has-pending-model-changes` نظيف · `DI` 198/198/0 · 0 حقل ميت · 0 partial فارغ.

> اختبارات `PostgreSQL` (ومنها كل مسارات الهيكل التنظيمي) تُخطّى محليًا لغياب
> `DOCGEN_TEST_POSTGRES`؛ `backend-postgres` في `CI` هو المرجع القاطع للسلوك على القاعدة.

## الهجرات

**لا هجرة جديدة** (لم يُمسّ أي كيان أو تعيين). المعلّق يبقى **6**.
