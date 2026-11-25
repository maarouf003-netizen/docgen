# `P4-01cw` — نقل دين القراءة المعلن: تنبيهات وتذكيرات شطب وتدقيق — 2026-11-25

> **النطاق:** إغلاق **دين القراءة الصرفة المعلن** في [`ADR-0010`](../adr/0010-command-query-ownership.md)
> §2.4: نقل **11** عضو قراءة باقيًا على ثلاث خدمات إلى استعلامات `IQuery<TResponse>`
> ومعالجات `QueryHandlerBase` تحت `backend/src/DocGenerator.Application/UseCases/`:
>
> - `IHeadAlertService` — ست قراءات → `UseCases/Alerts/` (مع **إبقاء سطح الكتابة النظامية الأربعة**).
> - `IStrikeOffReminderService` — ثلاث قراءات → `UseCases/Reminders/` (الواجهة **حُذفت كاملة**).
> - `IAuditLogService` — قراءتان → `UseCases/Audit/` (الواجهة **حُذفت كاملة** — لا كاتب/احتفاظ عليها).
>
> الفرع `refactor/p4-01cw-read-debt` من `master` (`b9bb1dbf`، بعد `P4-01cu`/`P4-01cv`)،
> بحفاظ حرفي على السلوك والصلاحيات والنطاق والرسائل ورموز الحالة وأشكال الاستجابة.
> **لا هجرة ولا تغيير في `OpenAPI`.**

## 1. ما نُفِّذ

| الاستعلام | المعالج | الاعتمادات | النتيجة |
|---|---|---|---|
| `ListHeadAlertsForActorQuery` | `ListHeadAlertsForActorQueryHandler` | `IHeadAlertRepository` | `PagedResult<HeadAlertSummaryDto>` |
| `ListSentHeadAlertsQuery` | `ListSentHeadAlertsQueryHandler` | `IHeadAlertRepository` | `PagedResult<HeadAlertSummaryDto>` |
| `ListHeadAlertsByDelegationQuery` | `ListHeadAlertsByDelegationQueryHandler` | `IHeadAlertRepository` + `IDelegationRepository` | `PagedResult<HeadAlertSummaryDto>` |
| `GetHeadAlertDetailsQuery` | `GetHeadAlertDetailsQueryHandler` | `IHeadAlertRepository` | `HeadAlertDto?` |
| `GetHeadAlertRecipientsQuery` | `GetHeadAlertRecipientsQueryHandler` | `IHeadAlertRepository` | `List<HeadAlertRecipientDto>?` |
| `CountUnreadHeadAlertsQuery` | `CountUnreadHeadAlertsQueryHandler` | `IHeadAlertRepository` | `int` |
| `GetStrikeOffRemindersQuery` | `GetStrikeOffRemindersQueryHandler` | `IDocumentRepository` + `TimeProvider` + `TimeZoneInfo` | `PagedResult<DocumentResponse>` |
| `GetStrikeOffReminderListQuery` | `GetStrikeOffReminderListQueryHandler` | `IDocumentRepository` + `TimeProvider` + `TimeZoneInfo` + `IKeysetCursorCodec` | `PagedResult<DocumentListItemDto>` |
| `CountStrikeOffRemindersQuery` | `CountStrikeOffRemindersQueryHandler` | `IDocumentRepository` + `TimeProvider` + `TimeZoneInfo` | `int` |
| `SearchAuditLogsQuery` | `SearchAuditLogsQueryHandler` | `IAuditLogRepository` | `PagedResult<AuditLogDto>` |
| `GetDocumentChangesQuery` | `GetDocumentChangesQueryHandler` | `IAuditLogRepository` | `PagedResult<DocumentChangeGroupDto>` |

| البند | التفصيل |
|---|---|
| الواجهات | `IHeadAlertService` **10 → 4** (بقيت الكتابة النظامية: `CreateSystemAsync`/`ApplyCircuitRenamedAsync`/`ArchiveByDelegationAsync`/`ArchiveByAppealAsync`)؛ `IStrikeOffReminderService` **3 → 0** ثم حُذفت مع ملفها؛ `IAuditLogService` **2 → 0** ثم حُذفت مع `AuditLogService` (لا كاتب/احتفاظ عليها) |
| الخدمات | `HeadAlertService` صار سطح كتابة صرفًا؛ حُذف `HeadAlertService.Reads.cs`، وحُذفت `StrikeOffReminderService.cs` و`AuditLogService.cs` نهائيًا. `HeadAlertActor`/`HeadAlertListQuery` بقيا في `Services` (يستهلكهما المتحكم والأوامر والمعالجات) |
| الحقول/المنشئ | منشئ `HeadAlertService` **9 → 7**: حُذف `IRepository<Branch> branches` (كان **حقلًا ميتًا قبل النقل**: لا استخدام له في أي partial) و`IDelegationRepository delegations` (صار ميتًا بعد نقل `ListByDelegationAsync`) مع حقليهما. لا حقول ميتة في الخدمات المتبقية |
| المساعدات | نُقلت حرفيًا إلى `HeadAlertReadHelpers` (`ToPagedResult`/`RequireBranch`/`CountUserScopeAsync`/`ToDetailDto`) و`StrikeOffReminderReadHelpers.NormalizePaging` — مصدر واحد تستهلكه المعالجات، بلا تكرار |
| `DI` | حُذف تسجيلا خدمة (`IAuditLogService` و`IStrikeOffReminderService`) وأُضيف **11** تسجيل `IQueryHandler<Query, Response>`؛ الإجمالي **218 → 229** معالجًا (**100 → 111** استعلامًا + **118** أمرًا)، و**229** صنف معالج، 0 غير مسجَّل، 0 مكرر |
| المتحكمون | `AlertsController` (منشئ **3 → 8**)، `AuditLogsController` (**2 → 2** بتبديل النوع)، `DocumentsController` (**53 → 55**) يحقنون معالجات الاستعلام بدل الخدمتين/الخدمة؛ نفس المسارات و`[Authorize]` وحرّاس القدرات/الجلسة و`Ok`/`NoContent`/`NotFound`/`Forbid`/`ApiProblem` وأشكال الأجسام (`{ count }`، `OkSanitized`، `CreatedAtAction`) |
| الاختبارات | جسران (`HeadAlertReadQueryExtensions`، `AuditLogReadQueryExtensions`) فوق حزمتي معالجات (`HeadAlertReadHandlers`، `AuditLogReadHandlers`) حوّلا **65** موضع نداء، وحُدِّثت **17** موضع إنشاء لـ`HeadAlertService` (إسقاط المعاملين المحذوفين)، وحُذفت قراءات البديل في `CreateFailureAlertService`، وأُضيف اختبار توصيل `HeadAlertReminderAuditReadHandlerRegistrationTests` للأحد عشر وغياب خدمتي القراءة وبقاء سطح كتابة التنبيهات |
| النهاية | لا تغيير في `OpenAPI` ولا في `src/api/generated.ts` — العقود الخارجية كما هي |

## 2. إثبات الحفاظ على السلوك (لكل دالة)

> التطبيع: إسقاط تعليقات، وتوحيد المسافات، واستبعاد أسطر التمرير الميكانيكية
> `var x = request.X;` (لازمة لأن خصائص `record` غير قابلة للإسناد)، وإسقاط تأهيل
> `HeadAlertReadHelpers.`/`StrikeOffReminderReadHelpers.`، ثم عدّ الجمل الفعّالة
> (`;`) ومقارنة النصوص العربية اتجاهين.

| الدالة | الجمل الأصلية/المعالج | `diffs` دلالية | الاستثناءات الموثّقة |
|---|---|---|---|
| `HeadAlertService.ListForActorAsync` | 13 / 13 | 0 | 2 سطر تمرير ميكانيكي (`actor`/`query`) |
| `HeadAlertService.ListSentAsync` | 8 / 8 | 0 | 4 أسطر تمرير ميكانيكي |
| `HeadAlertService.ListByDelegationAsync` | 8 / 8 | 0 | 3 أسطر تمرير ميكانيكي |
| `HeadAlertService.GetDetailsAsync` | 7 / 7 | 0 | 2 سطران ميكانيكيان |
| `HeadAlertService.GetRecipientsAsync` | 6 / 6 | 0 | 2 سطران ميكانيكيان |
| `HeadAlertService.CountUnreadAsync` | 2 / 2 | 0 | 2 سطران ميكانيكيان |
| `AuditLogService.SearchAsync` | 4 / 4 | 0 | 6 أسطر تمرير ميكانيكي |
| `AuditLogService.GetDocumentChangesAsync` | 4 / 4 | 0 | 3 أسطر تمرير ميكانيكي |
| `StrikeOffReminderService.GetAsync` | 9 / 9 | 0 | 5 أسطر تمرير ميكانيكي |
| `StrikeOffReminderService.GetListAsync` | 13 / 13 | 0 | 6 أسطر تمرير ميكانيكي |
| `StrikeOffReminderService.GetCountAsync` | 3 / 3 | 0 | 3 أسطر تمرير ميكانيكي |
| **الإجمالي** | **77 / 77** | **0** | 38 سطر تمرير ميكانيكي موثّق |
| `HeadAlertReadHelpers.ToPagedResult` | حرفي | 0 | بقي كما هو (نُقل إلى `internal static`) |
| `HeadAlertReadHelpers.RequireBranch` | حرفي | 0 | بقي كما هو |
| `HeadAlertReadHelpers.CountUserScopeAsync` | حرفي | 0 | استُبدل `_alerts` بمعامل `alerts` فقط |
| `HeadAlertReadHelpers.ToDetailDto` | حرفي | 0 | بقي كما هو |
| `StrikeOffReminderReadHelpers.NormalizePaging` | حرفي | 0 | بقي كما هو |
| **المساعدات** | **5 / 5** | **0** | — |

- **النصوص الحرفية (اتجاهان):** **6/6** نص عربي (`missing = 0`، `added = 0`) في الأجسام
  المنقولة: رسائل رفض الدور/الفرع/الطرف/النطاق ورسالة 400 — بلا أي نص جديد أو محذوف.
- **عدم الكتابة:** فحص نصي لكل ملفات `UseCases/{Alerts,Reminders,Audit}/*Query.cs` (**13** ملفًا)
  عن `SaveChanges|AddAsync|ITransactionRunner|ITransactionalRequest|RunAsync|BeginTransaction|
  ExecuteUpdate|ExecuteDelete|IAuditLogger|IAuditedRequest|_uow|_tx` = **0 مطابقة**؛
  المطابقتان الوحيدتان لكلمة `.Add(` هما `items.Add(...)` على قوائم في الذاكرة في
  استعلامَي تذكيرات الشطب (بناء الاستجابة، لا كتابة مستودع).
- **النطاق/الصلاحيات:** الحرّاس بقوا في المتحكمات وقبل الاستعلام بنفس الترتيب —
  `[Authorize(Roles = "lawyer,head,subhead,manager,admin")]` للتنبيهات، وحرّاس
  `scopes` في التدقيق، و`CanViewStrikeOffReminders` في الشطب؛ وداخل المعالجات بقي
  فحص الدور/الفرع/الطرف/النطاق نفسه حرفيًا (`UnauthorizedAccessException` ⇒ 403،
  `ArgumentException`/`ArgumentOutOfRangeException` ⇒ 400) بلا توسيع أو تضييق.
- **الاستجابة:** نفس أشكال `PagedResult<HeadAlertSummaryDto>`/`HeadAlertDto`/
  `HeadAlertRecipientDto`/`PagedResult<DocumentResponse>`/`PagedResult<DocumentListItemDto>`/
  `PagedResult<AuditLogDto>`/`PagedResult<DocumentChangeGroupDto>` ونفس `{ count }`.
- **الفاصل الحتمي:** ترقيم التنبيهات والمؤشر (`AlertCursor`) و«403 قبل 400» كما هو؛
  ترقيم تذكيرات الشطب (1…100 وإلا 20) وkeyset `ReminderLastActivity` كما هو؛
  تدقيق (صفحة ≥1 وحجم 1…100) وترتيب تغييرات الحقول `OrderBy(c => c.Id)` كما هو.

## 3. التحقق

`build -warnaserror` 0/0 · `format --verify-no-changes` نظيف · Architecture 29/29 ·
Application 772/1661/0 (منها اختبار التوصيل الجديد) · Api 901/615/0 (والـ`Sequential`
منفصلًا: 0/5/0 تخطٍّ محلي) · frontend 1809/1809 (171 ملفًا) +
`oxlint`/`tsc -b`/`build`/`guard`/`generate:api` بلا فرق
(`git diff --exit-code -- src/api/generated.ts` = 0) · الحرّاس الستة نظيفة ·
`dependency-audit` 9 مشاريع · `npm-audit-guard` 0 ثغرة ·
`has-pending-model-changes` نظيف · `DI` 229/229/0 · 0 حقل ميت · 0 partial فارغ ·
0 معاملة في المتحكمات.

> اختبارات `PostgreSQL` تُخطّى محليًا لغياب `DOCGEN_TEST_POSTGRES`؛ `backend-postgres`
> في `CI` هو المرجع القاطع لسلوك المسارات على القاعدة.

## 4. الهجرات

**لا هجرة جديدة** (لم يُمسّ أي كيان أو تعيين). المعلّق يبقى **6**.
