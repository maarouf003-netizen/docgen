# `P4-01cu` — نقل قراءات المنتدى والاقتراحات والتذكيرات الشخصية إلى Query Handlers — 2026-11-25

> **النطاق:** المرحلة الثانية والعشرون في سلسلة ملكية `use-case`: تحويل **كل** أسطح
> القراءة الصرفة المتبقية على الخدمات الثلاث إلى استعلامات `IQuery<TResponse>` ومعالجات
> `QueryHandlerBase` تحت `backend/src/DocGenerator.Application/UseCases/`:
> - `IForumService` (6 أعضاء) → `UseCases/Forum/`
> - `IAppSuggestionService` (4 أعضاء) → `UseCases/Suggestions/`
> - `IPersonalReminderService` (2 عضوان) → `UseCases/Reminders/`
>
> **قراءة صرفة بلا كتابة ولا معاملة ولا تدقيق** في الاستعلامات الاثني عشر. الفرع
> `refactor/p4-01cu-cv-reminders-forum-closure` من `master` (`e338e928`، بعد دمج
> `P4-01ct`)، بحفاظ حرفي على السلوك والنطاق والرسائل ورموز الحالة وأشكال الاستجابة.

## ما نُفِّذ

| الاستعلام | المعالج | الاعتمادات | النتيجة |
|---|---|---|---|
| `ListForumMessagesQuery` | `ListForumMessagesQueryHandler` | `IForumRepository` | `ForumMessagesPageDto` |
| `GetForumMessageQuery` | `GetForumMessageQueryHandler` | `IForumRepository` | `ForumMessageDto?` |
| `GetPinnedForumMessageQuery` | `GetPinnedForumMessageQueryHandler` | `IForumRepository` | `ForumMessageDto?` |
| `GetForumMessageReadersQuery` | `GetForumMessageReadersQueryHandler` | `IForumRepository` | `List<ForumReaderDto>` |
| `SearchForumUsersQuery` | `SearchForumUsersQueryHandler` | `IUserRepository` + `ForumMembership` | `List<ForumUserLookupDto>` |
| `CountUnreadForumMessagesQuery` | `CountUnreadForumMessagesQueryHandler` | `IForumRepository` + `IUserRepository` + `ForumMembership` | `int` |
| `ListMyAppSuggestionsQuery` | `ListMyAppSuggestionsQueryHandler` | `IAppSuggestionRepository` | `List<AppSuggestionDto>` |
| `GetAppSuggestionQuery` | `GetAppSuggestionQueryHandler` | `IAppSuggestionRepository` | `AppSuggestionDto?` |
| `ListAppSuggestionsForAdminQuery` | `ListAppSuggestionsForAdminQueryHandler` | `IAppSuggestionRepository` | `PagedResult<AppSuggestionDto>` |
| `CountUnreadAppSuggestionsQuery` | `CountUnreadAppSuggestionsQueryHandler` | `IAppSuggestionRepository` | `int` |
| `ListPersonalRemindersQuery` | `ListPersonalRemindersQueryHandler` | `IPersonalReminderRepository` | `List<PersonalReminderDto>` |
| `GetPersonalReminderQuery` | `GetPersonalReminderQueryHandler` | `IPersonalReminderRepository` | `PersonalReminderDto?` |

| البند | التفصيل |
|---|---|
| العقود | أُفرغت الواجهات الثلاث ثم حُذفت نهائيًا (`IForumService` **6 → 0**، `IAppSuggestionService` **4 → 0**، `IPersonalReminderService` **2 → 0**) مع تسجيلاتها في `DI` |
| الخدمات | صارت الأنواع الثلاثة `internal static` بلا حالة ولا منشئ: `ForumService` (ثوابت + `ToDto`)، `AppSuggestionService` (ثوابت + `ToDto`)، `PersonalReminderService` (تحقق + `ToDto`) — مصدر واحد يستهلكه المعالجون والأوامر بلا تكرار |
| الحقول/المنشئ | حُذفت **7** حقول و**7** معاملات منشئ: `ForumService` (`_forum`/`_users` + منشئ 2)، `AppSuggestionService` (`_suggestions` + `_uow`/`_tx`/`_audit` الميتة + منشئ 4)، `PersonalReminderService` (`_reminders` + منشئ 1) |
| المساعدات | `ForumMembership.RequireAsync` و`ForumIdentityParts` (الاسم/الموقع/الشعبة) و`ForumService.ToDto` بقيت مصدرًا واحدًا؛ `AppSuggestionService.ToDto` و`PersonalReminderService.ToDto`/`Validate*` رُقّيت إلى `internal static` للمعالجين |
| `DI` | حُذفت 3 تسجيلات خدمة وأُضيفت 12 تسجيل `IQueryHandler<Query, Response>`؛ الإجمالي **206 → 218** معالجًا (**88 → 100** استعلامًا + **118** أمرًا)، و**218** صنف معالج، 0 غير مسجَّل، 0 مكرر |
| المتحكمون | `ForumController` (منشئ **7 → 12**)، `AppSuggestionsController` (**4 → 7**)، `PersonalRemindersController` (**6 → 7**) يحقنون معالجات الاستعلام بدل الخدمة؛ نفس المسارات و`[Authorize]` وحرّاس القدرات/الجلسة و`Ok`/`NoContent`/`NotFound`/`Forbid`/`ApiProblem` وأشكال الأجسام (بما فيها `CreatedAtAction` و`{ count }`) |
| الاختبارات | ثلاثة جسور (`ForumReadQueryExtensions`، `AppSuggestionReadQueryExtensions`، `PersonalReminderReadQueryExtensions`) فوق سجلات/مصانع (`ForumReadHandlers`، `AppSuggestionReadHandlers`، `PersonalReminderReadHandlers`) حوّلت **54** موضع نداء بلا تغيير أي توقّع؛ وأُضيف اختبار توصيل `ForumSuggestionReminderReadHandlerRegistrationTests` يثبت التسجيلات الاثني عشر وغياب الخدمات الثلاث |
| النهاية | لا تغيير في `OpenAPI` ولا في `src/api/generated.ts` — العقود الخارجية كما هي |

## إثبات الحفاظ على السلوك (لكل دالة)

> التطبيع: استبدال أسماء المعاملات بخصائص `query.…`، وإسقاط تأهيل المستودعات، وإقحام
> `ForumService.`/`AppSuggestionService.`/`PersonalReminderService.` قبل المساعدات، ثم
> مقارنة **الجمل الفعّالة** (لا الأسطر الملفوفة).

| الدالة | الجمل الأصلية/المعالج | `diffs` دلالية | الاستثناءات الموثّقة |
|---|---|---|---|
| `ForumService.ListAsync` | 6 / 7 | 0 | سطر واحد ميكانيكي: `var limit = query.Limit` قبل تقويم المدى |
| `ForumService.GetByIdAsync` | 4 / 4 | 0 | — |
| `ForumService.GetPinnedAsync` | 4 / 4 | 0 | — |
| `ForumService.GetReadersAsync` | 4 / 4 | 0 | — |
| `ForumService.SearchUsersAsync` | 3 / 3 | 0 | استدعاء `ForumMembership.RequireAsync` نفسه |
| `ForumService.CountUnreadAsync` | 3 / 3 | 0 | استدعاء `ForumMembership.RequireAsync` نفسه |
| `AppSuggestionService.ListMineAsync` | 2 / 2 | 0 | إحالة `ToDto` |
| `AppSuggestionService.GetByIdAsync` | 6 / 6 | 0 | خصائص `query.SenderId` فقط |
| `AppSuggestionService.ListForAdminAsync` | 4 / 6 | 0 | سطران ميكانيكيان: `var page`/`var perPage` قبل التقويم |
| `AppSuggestionService.CountUnreadAsync` | 1 / 1 | 0 | — |
| `PersonalReminderService.ListAsync` | 2 / 2 | 0 | إحالة `ToDto` |
| `PersonalReminderService.GetByIdAsync` | 4 / 4 | 0 | — |
| **الإجمالي** | **43 / 46** | **0** | 3 أسطر ميكانيكية موثّقة |
| `ForumService.ToDto` | 1 / 1 | 0 | بقي حرفيًا (ثابتة) |
| `ForumIdentityParts.DisplayName` | 1 / 1 | 0 | بقي حرفيًا |
| `ForumIdentityParts.ResolveLocation` | 5 / 5 | 0 | بقي حرفيًا |
| `ForumIdentityParts.ResolveSection` | 4 / 4 | 0 | بقي حرفيًا |
| `AppSuggestionService.ToDto` | 1 / 1 | 0 | رُقّي إلى `internal static` |
| `PersonalReminderService.ToDto` | 1 / 1 | 0 | بقي حرفيًا |
| `PersonalReminderService.Validate*` | 15 / 15 | 0 | بقيت حرفيًا |
| **المساعدات** | **28 / 28** | **0** | — |

- **النصوص الحرفية (اتجاهان):** **2/2** نص (`"الرسالة غير موجودة"`، `"نافذة «شوهدت
  بواسطة» متاحة لكاتب الرسالة فقط"`)؛ `missing = 0` و`added = 0`. لا نصوص في
  الاقتراحات/التذكيرات المنقولة.
- **عدم الكتابة:** فحص نصي لكل ملفات `UseCases/{Forum,Suggestions,Reminders}/*Query.cs`:
  `SaveChanges|AddAsync|.Add(|Remove(|ITransactionRunner|ITransactionalRequest|RunAsync|
  BeginTransaction|ExecuteUpdate|ExecuteDelete|IAuditLogger|IAuditedRequest|_uow|_tx` =
  **0 مطابقة** في **12** ملفًا.
- **النطاق/الصلاحيات:** الحرّاس بقوا في المتحكمات وقبل الاستعلام بنفس الترتيب —
  `[Authorize(Roles = ...)]`، و`SubHeadSessionBroken` في المنتدى، و`CanViewAppSuggestions`/
  `IsAdmin` في الاقتراحات، و`CanManagePersonalReminders`/`CanManage` في التذكيرات؛
  والمعالجات لا تكرر فحص صلاحية ولا تضيّق النطاق.
- **الفاصل الحتمي:** تقويم `limit` (1…100 وإلا 30) كما هو؛ شريحة المنتدى بنفس `before`/
  `after` و`HasOlder`؛ عدّاد القراءة للرسائل الذاتية فقط؛ ترتيب المرشحين و`MentionLookupLimit`
  (20) كما هو؛ صفحة المشرف (صفحة ≥1، حجم 1…50 وإلا 20) وترتيبها من المستودع كما هو؛
  ملكية التذكير (غير المالك = `null`) كما هي.
- **الاستجابة:** نفس أشكال `ForumMessagesPageDto`/`ForumMessageDto`/`ForumReaderDto`/
  `ForumUserLookupDto`/`AppSuggestionDto`/`PagedResult<AppSuggestionDto>`/
  `PersonalReminderDto` ونفس `{ count }`/`{ maxReadMessageId }`.

## التحقق

`build -warnaserror` 0/0 · `format --verify-no-changes` نظيف · Architecture 29/29 ·
Application 771/1661/0 (منها اختبار التوصيل الجديد) · Api 901/615/0 (والـ`Sequential`
منفصلًا: 0/5/0 تخطٍّ محلي) · frontend 1809/1809 (171 ملفًا) +
`oxlint`/`tsc -b`/`build`/`budget`/`guard`/`generate:api` بلا فرق
(`git diff --ignore-cr-at-eol --exit-code -- src/api/generated.ts` = 0) · الحرّاس الستة
نظيفة · `dependency-audit` 9 مشاريع · `npm-audit-guard` 0 ثغرة ·
`has-pending-model-changes` نظيف · `DI` 218/218/0 · 0 حقل ميت · 0 partial فارغ ·
0 معاملة في المتحكمات.

> اختبارات `PostgreSQL` (ومنها مسارات المنتدى/الاقتراحات/التذكيرات) تُخطّى محليًا لغياب
> `DOCGEN_TEST_POSTGRES`؛ `backend-postgres` في `CI` هو المرجع القاطع للسلوك على القاعدة.

## الهجرات

**لا هجرة جديدة** (لم يُمسّ أي كيان أو تعيين). المعلّق يبقى **6**.
