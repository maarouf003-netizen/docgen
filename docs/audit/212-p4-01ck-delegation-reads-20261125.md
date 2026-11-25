# `P4-01ck` — نقل قراءات الإنابات الثلاث إلى Query Handlers — 2026-11-25

> **النطاق:** المرحلة الثانية عشرة في سلسلة نقل ملكية `use-case`: تحويل أسطح القراءة
> الثلاثة الباقية على `IDocumentDelegationService` — قائمة إنابات ملف، وطلبات الإنابة
> المعلّقة لنطاق الرئيس، وعدّها — إلى استعلامات `IQuery<TResponse>` ومعالجات
> `QueryHandlerBase` تحت `backend/src/DocGenerator.Application/UseCases/Delegations/`،
> **قراءة صرفة بلا كتابة ولا معاملة ولا تدقيق جديد**. الفرع
> `refactor/p4-01ck-delegation-reads` من `master` (`4256972b`)، بحفاظ حرفي على السلوك
> والنطاق والرسائل ورموز الحالة.

## ما نُفِّذ

| الاستعلام | المعالج | الاعتمادات | الملف |
|---|---|---|---|
| `GetDocumentDelegationsQuery(DocumentId)` | `GetDocumentDelegationsQueryHandler` | `IDocumentRepository` + `IDelegationRepository` + `TimeProvider` + `TimeZoneInfo` | `GetDocumentDelegationsQuery.cs` |
| `GetPendingDelegationsQuery(BranchId, OwnerSectionId, RejectedOnly)` | `GetPendingDelegationsQueryHandler` | `IDelegationRepository` + `TimeProvider` + `TimeZoneInfo` | `GetPendingDelegationsQuery.cs` |
| `CountPendingDelegationsQuery(BranchId, OwnerSectionId, RejectedOnly)` | `CountPendingDelegationsQueryHandler` | `IDelegationRepository` | `CountPendingDelegationsQuery.cs` |

| البند | التفصيل |
|---|---|
| العقود | حُذفت الأعضاء الثلاثة من `IDocumentDelegationService` (11 → 8 عضوًا، حذفًا صرفًا بلا إضافة أو تعديل آخر)؛ بقيت أعضاء المزامنة والحرّاس والمرآة القائمة على الخدمة حرفيًا |
| الخدمة | حُذفت الطرق الثلاث من `DocumentDelegationService.Crud.cs` (بقي `SyncTargetsTransferredAsync` وحده)، ونُقل `ListOfTargetAsync` إلى معالج الاستعلام الوحيد الذي يستخدمه؛ تقلّصت الحقول **9 → 8** والمنشئ **9 → 8**: حُذف `IDocumentRepository _documents` بعد نقل آخر مستخدميه |
| المساعدات | `ToDto` بقي `internal static` على الخدمة (مصدر واحد) ويستدعيه المعالجان بتأهيل صريح `DocumentDelegationService.ToDto`؛ `ListOfTargetAsync` تعريف واحد في معالج القائمة بلا نسخة ثانية على الخدمة |
| عدم الكتابة | لا `SaveChangesAsync`/`AddAsync`/`Add(`/`Remove`/`_uow`/`ITransactionRunner`/`ITransactionalRequest`/`IAuditedRequest`/`IAuditLogger`/`LogAsync` في ملفات الاستعلام الثلاثة (فحص نصي شامل: 0 مطابقة) |
| `DI` | ثلاثة تسجيلات `IQueryHandler<Query, Response>` جديدة في `DependencyInjection.cs`؛ الإجمالي **137 معالجًا / 137 تسجيلًا / 0 غير مسجَّل / 0 مكرر** |
| المتحكم | `DelegationsController` يحقن المعالجات الثلاثة بدل `IDocumentDelegationService` (12 → 14 معامل منشئ)، ويستدعيها؛ نفس المسارات (`documents/{id}/delegations`، `delegations/pending`، `delegations/pending-count`) ونفس `[Authorize]` وفحوص `CanApprove`/`RequireApproverScope` و`CanAccess` ونفس `Ok(...)`/`Forbid()`/`ApiProblem` ورموز الحالة وأسماء خصائص العدّاد (`count`/`rejectedCount`) |
| البوابة | `PortalService` صار يعتمد `IQueryHandler<GetDocumentDelegationsQuery, List<DelegationDto>>` بدل `IDocumentDelegationService` في `ListDelegationsAsync` (فحص النطاق `GetScopedDocumentAsync` كما هو)، وحُذف الاعتماد القديم |
| الاختبارات | جسر جديد `DelegationReadQueryExtensions` (ثلاثة امتدادات بنفس توقيعات الخدمة القديمة) وتحويل **49 موضع نداء** (33 في `DocumentDelegationServiceTests`، و15 في `SubHeadDelegationTests`، و1 في `DelegationConcurrencyTests`) بلا تغيير أي توقّع؛ وتحديث مصنعَي البناء (`DocumentServiceFactory`، `PortalServiceFactory`) ومصنع سياقات السباق، وحارس `Api` |
| تنظيف ميت | حُذف `FakeDelegationService` من حارس `Api` بعد أن صار بلا مستخدم، وحقل `_service` الميت في `DelegationDraftCircuitScopeTests` مع سلسلة بنائه، والمساعد `BuildService` غير المستخدم في `DelegationConcurrencyTests`، والمتغيران الميتان `registrationDates`/`occurrences` في `SubHeadDelegationTests`، وأربعة استيرادات ميتة في `DocumentDelegationService.Crud.cs` واستيرادان في `DelegationDraftCircuitScopeTests` |

> **قرار موثّق:** `PortalService` (خدمة قراءة تجميعية للبوابة) تستدعي الاستعلام عبر
> `IQueryHandler` بدل الإبقاء على اعتماد الخدمة الذي صار بلا أي استخدام آخر — البديل
> (تكرار استعلام المستودع وتحويل `ToDto` داخل البوابة) كان سيكرر منطق قراءة قائمًا،
> وهو مرفوض. لا دائرة اعتماد: المعالج يعتمد مستودعات فقط.

## إثبات الحفاظ على السلوك (لكل دالة)

| الدالة | أسطر الوالد/المعالج بعد التطبيع | `diffs` | الاستثناءات |
|---|---|---|---|
| `ListForDocumentAsync` | 12 / 12 | 0 | سطر ربط `var documentId = query.DocumentId;` مستثنى؛ وتأهيل `ToDto` صار `DocumentDelegationService.ToDto` (نص واحد) |
| `ListPendingForHeadAsync` | 2 / 2 | 0 | استبدال أسماء المعاملات بـ`query.BranchId`/`query.OwnerSectionId`/`query.RejectedOnly` |
| `CountPendingForHeadAsync` | 1 / 1 | 0 | نفس استبدال أسماء المعاملات |
| `ListOfTargetAsync` (منقول) | 2 / 2 | 0 | — |
| **الإجمالي** | **17 / 17** | **0** | مع سطر ربط واحد خارج العدّ ونص تأهيل واحد |

- **مقارنة النصوص الحرفية (اتجاهان):** المقطع المنقول كاملًا يحوي نصًا حرفيًا واحدًا
  (`"الملف غير موجود"`)، والنتيجة `missing = 0` و`added = 0`.
- **النطاق/الصلاحيات:** شروط `RequireApproverScope` (فرع الرئيس، شعبة رئيس الشعبة،
  `403`/`branch_required`) و`CanAccess` (مدير/مشرف، محامٍ مالك، رئيس قسم/شعبة) بقيت في
  المتحكم بنفس المواضع والترتيب؛ ولم يُمسّ `CanApprove`/`CanManage`.
- **بلا كتابة:** الاستعلامات الثلاثة تقرأ فقط عبر `GetByIdAsync`/`ListBySourceAsync`/
  `FindByTargetAsync`/`ListPendingByBranchAsync`/`CountPendingByBranchAsync`؛ لا حفظ
  ولا معاملة ولا تدقيق ولا أثر جانبي.
- **الفاصل الحتمي/الترتيب:** ترتيب المستودع و`Select` كما هو، ويُبنى `currentYear` مرة
  واحدة لكل استعلام كما في الأصل.

## التحقق

`build -warnaserror` 0/0 · `format --verify-no-changes` نظيف · Architecture 29/29 ·
Application 758/1659/0 · Api 901/615/0 (والـ`Sequential` منفصلًا: 0/5/0 تخطٍّ محلي) ·
frontend 1809/1809 (171 ملفًا) + `oxlint`/`tsc -b`/`build`/`budget`/`guard`/`generate:api`
بلا فرق · الحرّاس الستة نظيفة · `dependency-audit` 9 مشاريع · `npm-audit-guard` 0 ثغرة ·
`has-pending-model-changes` نظيف · `DI` 137/137/0 · 0 حقل ميت · 0 partial فارغ.

> اختبارات `PostgreSQL` (ومنها كل مسارات قراءات الإنابات المعدَّلة) تُخطّى محليًا لغياب
> `DOCGEN_TEST_POSTGRES`؛ `backend-postgres` في `CI` هو المرجع القاطع للسلوك على القاعدة.

## الهجرات

**لا هجرة جديدة.** المعلّق يبقى **6**.
