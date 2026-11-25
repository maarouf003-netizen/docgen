# `P4-01cp` — نقل قراءات الإحصاءات التسع إلى Query Handlers وحذف `IStatisticsService` — 2026-11-25

> **النطاق:** المرحلة السابعة عشرة في سلسلة نقل ملكية `use-case`: تحويل **كل** أسطح
> القراءة التسعة على `IStatisticsService` — بطاقة اللوحة، والسلسلة الشهرية، وملخّص
> الفروع، ونشاط المستخدمين، والتذكيرات، وبطاقات المدير، وجدول محامي الفرع، والإحصاءات
> الشخصية، والأشهر المتاحة — إلى استعلامات `IQuery<TResponse>` ومعالجات
> `QueryHandlerBase` تحت `backend/src/DocGenerator.Application/UseCases/Statistics/`،
> **قراءة صرفة بلا كتابة ولا معاملة ولا تدقيق**. الفرع
> `refactor/p4-01cp-statistics-reads` من `master` (`c32e841e`)، بحفاظ حرفي على السلوك
> والنطاق والرسائل ورموز الحالة.

## ما نُفِّذ

| الاستعلام | المعالج | الاعتمادات | النتيجة |
|---|---|---|---|
| `GetDashboardStatsQuery(BranchId, OwnerSectionId?, FullAccess)` | `GetDashboardStatsQueryHandler` | `IStatisticsRepository` | `DashboardStatsDto` |
| `GetMonthlyStatsQuery(BranchId, OwnerSectionId?, FullAccess)` | `GetMonthlyStatsQueryHandler` | `IStatisticsRepository` | `List<MonthlyStatDto>` |
| `GetBranchesSummaryQuery()` | `GetBranchesSummaryQueryHandler` | `IStatisticsRepository` | `List<BranchSummaryDto>` |
| `GetUserActivityQuery()` | `GetUserActivityQueryHandler` | `IStatisticsRepository` | `List<UserActivityDto>` |
| `GetRemindersQuery(UserId)` | `GetRemindersQueryHandler` | `IStatisticsRepository` | `List<ReminderDto>` |
| `GetManagerStatsQuery(Period, BranchId, Year?, Month?, Quarter?, OwnerSectionId?, FullAccess)` | `GetManagerStatsQueryHandler` | `IStatisticsRepository` | `ManagerStatsDto` |
| `GetManagerLawyerStatsQuery(Period, BranchId, Year?, Month?, Quarter?, OwnerSectionId?, FullAccess)` | `GetManagerLawyerStatsQueryHandler` | `IStatisticsRepository` | `List<ManagerLawyerStatDto>` |
| `GetPersonalStatsQuery(Period, UserId, Year?, Month?, Quarter?)` | `GetPersonalStatsQueryHandler` | `IStatisticsRepository` | `ManagerStatsDto` |
| `GetAvailablePeriodsQuery(BranchId, UserId, OwnerSectionId?, FullAccess)` | `GetAvailablePeriodsQueryHandler` | `IStatisticsRepository` | `List<MonthlyStatDto>` |

> **ملاحظة على العدّ:** التكليف ذكر «3 methods» لكن الواجهة الفعلية على `master`
> (`c32e841e`) تحوي **تسعة** أعضاء، وكلها قراءة صرفة تفرّغ إلى `IStatisticsRepository`.
> نُقلت التسعة كلها وفق قاعدة «انقل كل use-case قراءة صرف»، ولم يُترك أي عضو.

| البند | التفصيل |
|---|---|
| العقود | `IStatisticsService` حُذف نهائيًا (**9 → 0**) لأنه صار فارغًا، وحُذف تسجيله من `DI` |
| الخدمة | `StatisticsService.cs` (الواجهة + الصنف في ملف واحد) حُذف بـ`git rm`؛ لا partials ولا أعضاء متبقية |
| المساعدات | لا مساعدات مشتركة في الخدمة أصلًا — كل الأعضاء جملة تفويض واحدة إلى المستودع، فلا ترقية ولا نسخ |
| `DI` | حُذف تسجيل خدمة واحد وأُضيفت تسعة تسجيلات `IQueryHandler<Query, Response>`؛ الإجمالي **164 → 173** معالجًا (**46 → 55** استعلامًا + **118** أمرًا)، و**173** صنف معالج، 0 غير مسجَّل، 0 مكرر |
| المتحكم | `StatisticsController` يحقن تسعة معالجات بدل `IStatisticsService` (منشئ **3 → 10** معاملًا)، وحُذف حقل `_branches` (`IRepository<Branch>`) لأنه **ميت** (تعريف وإسناد بلا قراءة)؛ نفس المسارات و`[Authorize(Roles = …)]` و`[EnableRateLimiting]` وحرّاس النطاق و`Ok(...)`/`Forbid()`/`ApiProblem` وأشكال الأجسام |
| الاختبارات | لم يكن ثمة موضع نداء اختباري على الخدمة (اختبارات الإحصاءات تستهدف `IStatisticsRepository` مباشرة)؛ استُبدل مزيّفا الخدمة في `StatisticsBranchGuardTests` و`ExceptionHandlerIntegrationTests` بمعالجات استعلام، وأُضيف جسر `StatisticsReadQueryExtensions` (تسعة امتدادات بتوقيعات الخدمة القديمة) ومصنع `StatisticsReadHandlerFactory` وتسعة اختبارات تكافؤ تمرير |
| النهاية | لا تغيير في `OpenAPI` ولا في العميل المولَّد — العقود الخارجية كما هي |

## إثبات الحفاظ على السلوك (لكل دالة)

> التطبيع: أسماء المعاملات إلى خصائص `query.…`، وإلغاء لفّ الأسطر، ومقارنة الجمل
> الفعّالة. كل دالة أصلية كانت **جملة تعبيرية واحدة**، وكل معالج **جملة تعبيرية واحدة**.

| الدالة | الجملة الأصلية/المعالج | `diffs` دلالية | الاستثناءات الموثّقة |
|---|---|---|---|
| `GetDashboardStatsAsync` | 1 / 1 | 0 | `query.BranchId`/`query.OwnerSectionId`/`query.FullAccess` |
| `GetMonthlyStatsAsync` | 1 / 1 | 0 | المثل |
| `GetBranchesSummaryAsync` | 1 / 1 | 0 | — |
| `GetUserActivityAsync` | 1 / 1 | 0 | — |
| `GetRemindersAsync` | 1 / 1 | 0 | `query.UserId` |
| `GetManagerStatsAsync` | 1 / 1 | 0 | خصائص `query` لثمانية وسائط |
| `GetManagerLawyerStatsAsync` | 1 / 1 | 0 | المثل |
| `GetPersonalStatsAsync` | 1 / 1 | 0 | خصائص `query` لخمسة وسائط |
| `GetAvailablePeriodsAsync` | 1 / 1 | 0 | خصائص `query` لخمسة وسائط |
| **الإجمالي** | **9 / 9** | **0** | — |

- **الأسطر الفعّالة بعد التطبيع:** **26 / 27** — الفرق سطر واحد ميكانيكي: تواقيع
  `HandleCoreAsync` في المعالجات تُكتب على سطرين، بينما كانت ثلاثة تواقيع قديمة في
  سطر واحد (وثلاثة منها كانت في ثلاثة أسطر فقصرت)، فالمحصلة صافي +1 بلا أي تغيير دلالي.
- **النصوص الحرفية (اتجاهان):** **0** نص حرفي في الدوال التسع قبل وبعد؛ `missing = 0`
  و`added = 0`. رسائل الحرّاس (`"الحساب غير مرتبط بفرع"`، `"سنة غير صالحة"`…) بقيت في
  المتحكم حرفيًا بلا مساس.
- **عدم الكتابة:** فحص نصي لكل ملفات `UseCases/Statistics/` عن
  `SaveChanges`/`AddAsync`/`Remove`/`_uow`/`ITransactionRunner`/`ITransactionalRequest`/
  `RunAsync`/`BeginTransaction`/`ExecuteUpdate`/`ExecuteDelete`/`IAuditLogger`/`LogAsync`/
  `IAuditedRequest` = **0/0/0**.
- **النطاق/الصلاحيات:** `HasFullAccess`/`IsHeadOrSubHead`/`RequireOwnerScope`/
  `RequireOwnBranch` وترتيبها قبل أي استعلام كما هو؛ القيم الممرَّرة مطابقة
  (`null` للوصول الكامل، `ownerSectionId`/`false` للرئيس/الشعبة، فرع المحامي لفرعه)،
  وقيم `fullAccess` الافتراضية في السجلات تحاكي الوسائط الاختيارية القديمة.
- **الفاصل الحتمي والترتيب:** كله داخل `StatisticsRepository` ولم يُمسّ؛ المعالجات لا
  تعيد ترتيبًا ولا تصفية ولا حسابًا.

## التحقق

`build -warnaserror` 0/0 · `format --verify-no-changes` نظيف · Architecture 29/29 ·
Application 767/1659/0 (منها 9 اختبارات تكافؤ جديدة) · Api 901/615/0 (والـ`Sequential`
منفصلًا: 0/5/0 تخطٍّ محلي) · frontend 1809/1809 (171 ملفًا) +
`oxlint`/`tsc -b`/`build`/`guard`/`generate:api` بلا فرق
(`git diff --exit-code -- src/api/generated.ts` = 0 مع استعادة ضجيج `CRLF`) ·
الحرّاس الستة نظيفة · `dependency-audit` 9 مشاريع · `npm-audit-guard` 0 ثغرة ·
`has-pending-model-changes` نظيف · `DI` 173/173/0 · 0 حقل ميت · 0 partial فارغ.

> اختبارات `PostgreSQL` (ومنها كل مسارات الإحصاءات) تُخطّى محليًا لغياب
> `DOCGEN_TEST_POSTGRES`؛ `backend-postgres` في `CI` هو المرجع القاطع للسلوك على القاعدة.

## الهجرات

**لا هجرة جديدة** (لم يُمسّ أي كيان أو تعيين). المعلّق يبقى **6**.
