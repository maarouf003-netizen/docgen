# `P4-01cq` — نقل قراءات المستند السبع إلى Query Handlers وحذف `IDocumentService` — 2026-11-25

> **النطاق:** المرحلة الثامنة عشرة في سلسلة نقل ملكية `use-case`: تحويل **كل** أسطح
> القراءة الصرفة السبعة على `IDocumentService` — ملف واحد، وإسقاط الوصول الخفيف، وملف
> محذوف، وعدد ملفات المحامي، وجدول التدوير، وتاريخ أرقام الأساس للملف الحالي والمحذوف —
> إلى استعلامات `IQuery<TResponse>` ومعالجات `QueryHandlerBase` تحت
> `backend/src/DocGenerator.Application/UseCases/Documents/`، **قراءة صرفة بلا كتابة
> ولا معاملة ولا تدقيق**. الفرع `refactor/p4-01cq-document-reads` من `master`
> (`e53cf51c`)، بحفاظ حرفي على السلوك والنطاق والرسائل ورموز الحالة.

## ما نُفِّذ

| الاستعلام | المعالج | الاعتمادات | النتيجة |
|---|---|---|---|
| `GetDocumentQuery(DocumentId)` (قائم، أُعيد تنفيذه) | `GetDocumentQueryHandler` | `IDocumentRepository` + `TimeProvider` + `TimeZoneInfo` | `DocumentResponse?` |
| `GetDocumentAccessInfoQuery(DocumentId)` | `GetDocumentAccessInfoQueryHandler` | `IDocumentRepository` | `DocumentAccessInfo?` |
| `GetDeletedDocumentQuery(DocumentId)` | `GetDeletedDocumentQueryHandler` | `IDocumentRepository` + `TimeProvider` + `TimeZoneInfo` | `DocumentResponse?` |
| `CountFilesByOwnerQuery(OwnerId, ScopeBranchId, OwnerSectionId?)` | `CountFilesByOwnerQueryHandler` | `IUserRepository` + `IDocumentRepository` | `int` |
| `GetDocumentRotationListQuery(UserId, Page, PerPage)` | `GetDocumentRotationListQueryHandler` | `IDocumentRepository` + `IDocumentNumberRegistry` + `TimeProvider` + `TimeZoneInfo` | `PagedResult<RotationDocumentDto>` |
| `GetDocumentBaseNumberHistoryQuery(DocumentId)` | `GetDocumentBaseNumberHistoryQueryHandler` | `IDocumentNumberRegistry` | `List<BaseNumberHistoryDto>` |
| `GetDeletedDocumentBaseNumberHistoryQuery(DocumentId)` | `GetDeletedDocumentBaseNumberHistoryQueryHandler` | `IDocumentNumberRegistry` | `List<BaseNumberHistoryDto>` |

| البند | التفصيل |
|---|---|
| العقود | `IDocumentService` حُذف نهائيًا (**7 → 0**) مع تسجيله في `DI`؛ لم تُنقل أي قراءة إلى `IDocumentSearchService` (خارج النطاق، كما هي) |
| قرار `IDocumentStatusService` | فُحصت واجهته الفعلية: عضوان فقط هما `ApplyRenewalAsync` و`AddStruckOffOccurrenceAsync`، وكلاهما **كاتب** يُستدعى داخل معاملة مستدعٍ خارجي (`CreateDocumentCommand`/`UpdateDocumentCommand`/معالجات الحالة) — فلا سطح قراءة فيه، وبقيا على عقدهما (**2 → 2**) بلا مساس |
| الخدمة | `DocumentService` فُكِّك: حُذفت ثلاثة partials (`DocumentService.cs` و`BaseNumbers` و`Transfer`) بـ`git rm` بعد إفراغها، وحُوِّل `DocumentService.Apply.cs` إلى `Services/DocumentUpsertMapper.cs` (`internal static`) لأنه منطق تطبيع كتابة خالص يستهلكه معالجَا الإنشاء/التعديل — **لا صنف خدمة باقٍ** ولا partial فارغ |
| المساعدات | `CurrentYear()` الخاصة بالخدمة أُزيلت: معالجات القراءة تستدعي `ServerClock.CurrentYear(_clock, _timeZone)` مباشرة (المصدر الواحد القائم)؛ لا نسخ لأي مساعد |
| `DI` | حُذف تسجيل خدمة واحد وأُضيفت ستة تسجيلات `IQueryHandler<Query, Response>` (سجل `GetDocumentQuery` كان قائمًا)؛ الإجمالي **173 → 179** معالجًا (**55 → 61** استعلامًا + **118** أمرًا)، و**179** صنف معالج، 0 غير مسجَّل، 0 مكرر |
| المتحكمون | `DocumentsController` يحقن خمسة معالجات جديدة بدل `IDocumentService` (منشئ **38 → 42** معاملًا)، و`DelegationsController`/`AppealsController` يحقنان `IQueryHandler<GetDocumentQuery, DocumentResponse?>` بدل `IDocumentService` (عدد المعاملات كما هو)؛ نفس المسارات و`[Authorize]` وحرّاس النطاق و`Ok(...)`/`Forbid()`/`ApiProblem` وأشكال الأجسام |
| الاختبارات | جسر جديد `DocumentReadQueryExtensions` (سبعة امتدادات بتوقيعات الخدمة القديمة) وتحويل **137 موضع نداء** بلا تغيير أي توقّع؛ ومصنع الاختبار صار يعيد سجل `DocumentReadHandlers` بدل `IDocumentService`، وحُدِّثت مواضع بناء `DocumentsController` في الاختبارات الأربعة (عدد المعاملات) واستُبدل `StubDocumentService` بـ`StubGetDocumentHandler`؛ وأُضيف اختبارا توصيل لمعالجَي القراءة المفردة |
| النهاية | لا تغيير في `OpenAPI` ولا في العميل المولَّد — العقود الخارجية كما هي |

> **ملاحظة صدق:** `GetDocumentAccessInfoQuery` كان — وما زال — **بلا مستدعٍ داخل
> المستودع**: قناة F-01 الفعلية هي `IDocumentAccessReader` المحفوظة على عقدها. نُقل
> العضو ضمن قاعدة «كل قراءة صرفة» بدل إسقاطه صامتًا، وأُضيف له اختبار توصيل صريح
> (المفقود ← `null`)؛ لا يستدعيه أي مسار إنتاجي.

## إثبات الحفاظ على السلوك (لكل دالة)

> التطبيع: استبدال أسماء المعاملات بخصائص `query.…`، وإسقاط تأهيل `_documents`/`_users`/
> `_numberRegistry`، ثم مقارنة **الجمل الفعّالة** (لا الأسطر الملفوفة).

| الدالة | الجمل الأصلية/المعالج | `diffs` دلالية | الاستثناءات الموثّقة |
|---|---|---|---|
| `GetAsync` | 2 / 2 | 0 | — |
| `GetAccessInfoAsync` | 1 / 1 | 0 | — |
| `GetDeletedAsync` | 2 / 4 | 0 | لفّ شرط `doc is null \|\| !doc.IsDeleted` على ثلاثة أسطر (سطران ميكانيكيان) |
| `CountFilesByOwnerAsync` | 4 / 4 | 0 | خصائص `query.…` |
| `GetRotationListAsync` | 6 / 6 | 0 | `var page`/`var perPage` بدل إعادة إسناد المعاملين، و`currentYear` محلي بدل `CurrentYear()` |
| `GetBaseNumberHistoryAsync` | 2 / 2 | 0 | — |
| `GetDeletedBaseNumberHistoryAsync` | 2 / 2 | 0 | — |
| **الإجمالي** | **19 / 21** | **0** | سطران ميكانيكيان فقط (لفّ شرط) |

- **النصوص الحرفية (اتجاهان):** **2** نص في الجانبين (`"المحامي غير موجود"`،
  `"لا يمكن عرض ملفات محامٍ من فرع آخر"`)؛ `missing = 0` و`added = 0`.
- **عدم الكتابة:** فحص نصي لكل ملفات `UseCases/Documents/*Query.cs`: لا
  `SaveChanges`/`AddAsync`/`Remove`/`_uow`/`ITransactionRunner`/`ITransactionalRequest`/
  `RunAsync`/`BeginTransaction`/`ExecuteUpdate`/`ExecuteDelete`/`IAuditLogger`/
  `IAuditedRequest` = **0 مطابقة**.
- **النطاق/الصلاحيات:** الحرّاس كلهم في المتحكمات كما كانوا
  (`RequireDocumentAccessAsync`، `CanRotate`، `CanTransferDocuments`،
  `ResolveListScope`، `CanAccess`/`CanAccessOrFollowAsync`) وقبل الاستعلام بنفس الترتيب؛
  المعالجات لا تكرر فحص صلاحية.
- **الفاصل الحتمي/الترتيب:** `Math.Max(1, page)` و`Math.Clamp(perPage, 1, 100)` بنفس
  الترتيب، و`ServerClock.CurrentYear` يُحسب مرة واحدة لكل طلب، و`EffectiveFileIdentity`
  و`DocumentText.RotationDisplayName` بلا تغيير.
- **`DocumentUpsertMapper`:** نقل حرفي لـ471 سطرًا (تطبيع الطلبات وتواريخ التسجيل)؛
  الفرق كله في ترويسة الصنف (`internal static` بدل `sealed partial class : IDocumentService`).

## التحقق

`build -warnaserror` 0/0 · `format --verify-no-changes` نظيف · Architecture 29/29 ·
Application 767/1661/0 (منها اختبارا توصيل جديدان) · Api 901/615/0 (والـ`Sequential`
منفصلًا: 0/5/0 تخطٍّ محلي) · frontend 1809/1809 (171 ملفًا) +
`oxlint`/`tsc -b`/`build`/`guard`/`generate:api` بلا فرق
(`git diff --exit-code -- src/api/generated.ts` = 0 مع استعادة ضجيج `CRLF`) ·
الحرّاس الستة نظيفة · `dependency-audit` 9 مشاريع · `npm-audit-guard` 0 ثغرة ·
`has-pending-model-changes` نظيف · `DI` 179/179/0 · 0 حقل ميت · 0 partial فارغ.

> اختبارات `PostgreSQL` (ومنها كل مسارات المستند) تُخطّى محليًا لغياب
> `DOCGEN_TEST_POSTGRES`؛ `backend-postgres` في `CI` هو المرجع القاطع للسلوك على القاعدة.

## الهجرات

**لا هجرة جديدة** (لم يُمسّ أي كيان أو تعيين). المعلّق يبقى **6**.
