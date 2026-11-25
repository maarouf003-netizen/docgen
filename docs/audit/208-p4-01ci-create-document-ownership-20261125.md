# `P4-01ci` — نقل ملكية إنشاء المستند كاملة إلى `CreateDocumentCommandHandler` — 2026-11-25

> **النطاق:** المرحلة العاشرة والأخيرة في سلسلة نقل ملكية `use-case`: أكبر مسار كتابة
> كان لا يزال يعتمد على `IDocumentService.CreateAsync` — نُقل جسم
> `DocumentService.CreateAsync` ومعاونه الحصري `CreateCoreAsync` إلى
> `CreateDocumentCommandHandler` في `backend/src/DocGenerator.Application/UseCases/Documents/`،
> على الفرع `refactor/p4-01ci-create-document-ownership` من `master` (`26d4fc63`)،
> بحفاظ حرفي على السلوك: كل الحرّاس/السياسات، معالجة المسودة، الترقيم وسجل الأرقام،
> خرائط الأصول/الورثة/الأطراف، والمعاملة والتدقيق و`RF-011` والرسائل العربية.

## ما نُفِّذ

| البند | التفصيل |
|---|---|
| المعالج | `CreateDocumentCommandHandler` صار يملك 13 اعتمادًا: `IDocumentRepository` / `IUserRepository` / `IRepository<DocumentRegistrationDate>` / `IUnitOfWork` / `ITransactionRunner` / `IAuditLogger` / `IExecutionActionService` / `IDocumentStatusService` / `IDocumentWriteGuard` / `IDocumentNumberRegistry` / `TimeProvider` / `TimeZoneInfo` / `IIdempotencyStore?` |
| الأمر | `CreateDocumentCommand(Request, UserId, ActorName, BranchId, IdempotencyKey)` بلا تغيير في توقيعه أو وسومه |
| العقود | حُذف `CreateAsync` من `IDocumentService` (8 → 7 أعضاء، والحذف هو هذا العضو وحده)؛ بقيت سبع قراءات |
| الخدمة | `DocumentService` صار قراءةً فقط؛ عضوان `CreateAsync`/`CreateCoreAsync` نُقلا، وحُذف `DocumentService.Crud.cs` (98 سطرًا) بـ`git rm` بعد إفراغه |
| الحقول/المنشئ | `DocumentService`: **17 حقلًا → 5** (`_documents`/`_users`/`_numberRegistry`/`_clock`/`_timeZone`) و**17 معامل منشئ → 5**؛ حُذف 12 حقلًا ميتًا بعد النقل (`_baseNumbers`، `_registrationDates`، `_uow`، `_tx`، `_audit`، `_actionService`، `_delegationService`، `_statusService`، `_writeGuard`، `_idempotency`، `_circuits`، `_executionCircuits`) |
| المساعدات المشتركة | `DocumentService.ApplyRequest` و`DocumentService.ApplyRegistrationDate(doc, value, registrationDates)` بقيتا `internal static` كما هي (تستخدمهما أيضًا `UpdateDocumentCommandHandler` والإنابة)؛ حُذف الغلاف المثيلي `ApplyRegistrationDate(doc, value)` لأنه صار بلا مستدعٍ |
| `DI` | تسجيل `ICommandHandler<CreateDocumentCommand, DocumentResponse>` قائم مسبقًا؛ العدد **124 معالجًا / 124 تسجيلًا / 0 غير مسجَّل** |
| المتحكم | `DocumentsController.Create` لم يتغير: نفس `POST api/documents`، ونفس `[SkipIdempotency]`، ونفس فحص `CanEditDocuments`، ونفس `CreatedAtAction`/`catch (IdempotentReplayException)` |
| الاختبارات | جسر `DocumentCreateCommandExtensions.CreateAsync` (بنفس توقيع الخدمة القديم)، وتحويل **356 موضع نداء** في 13 ملف اختبار إلى مستقبِل المعالج؛ وتحديث `DocumentServiceFactory` (عضو `CreateDocument` في الصفّ) وتنظيف مزيّف `StubDocumentService` من **8 أعضاء غير معرَّفين** في العقد (بينها `CreateAsync`) |

## إثبات الحفاظ على السلوك

- **جسم الدالة الأساسية (بعد تطبيع التأهيل):** 50 سطرًا في الوالد و50 في المعالج، `diffs = 0`،
  بعد استثناء:
  - أسطر ربط `var request/userId/actorName/branchId/idempotencyKey = command.…` (تحلّ محل معاملات الطريقة)،
  - وتأهيل `DocumentService.ApplyRequest` و`DocumentService.ApplyRegistrationDate(..., _registrationDates)`.
- **نواة الإنشاء:** 22 سطرًا في الوالد و22 في المعالج، `diffs = 0` (لا استثناءات).
- **مقارنة النصوص الحرفية (اتجاهان):** 5/5، `missing = 0` و`added = 0`؛ تشمل:
  - رسالتَي التحقق `"اسم المقترض مطلوب"` و`"المستخدم الذي ينشئ الملف غير موجود"`،
  - مفتاح عدم التكرار `"documents.create"`،
  - حدث التدقيق `"create"` ونصه `"أنشأ المستند (رقم {doc.Id})"`.
- **الترتيب:** التحققات الثماني ← جلب الفاعل ← بناء الكيان ← `ApplyRequest` ← اشتقاق الدائرة
  ← `FillDerivedFields` ← تاريخ التسجيل ← حجز `RF-011` ← `EnsureNumberUniqueByCircuitAsync`
  ← `WithNumberGuardAsync` ← `_tx.RunAsync` ← `AddAsync` ← `SaveChangesAsync` ← مزامنة القيد
  ← سجل التعاقب ← التدقيق ← وقعة الشطب (للمشطوب) ← `SaveChangesAsync` ← زرع الإجراءات
  ← `DocumentResponse.FromEntity` — بلا تغيير.
- **عدم التكرار:** `IdempotencyGuard.BeginAsync` قبل `_tx.RunAsync` عمدًا، و`ReleaseAsync` في
  `catch`، و`CompleteAsync(Snapshot(result))` بعد النجاح، ونفس `Fingerprint(request)` ونفس العملية.
- **المعاملة:** `WithNumberGuardAsync` تحيط بـ`_tx.RunAsync` واحدة كما الأصل؛ عدد
  `SaveChangesAsync = 2` بنفس الموضعين، و`_tx.RunAsync = 1`.

## مراجعة نقدية أولى

| # | السؤال النقدي | الفحص | النتيجة |
|---|---|---|---|
| 1 | هل انزاح `IdempotencyGuard.BeginAsync` إلى داخل المعاملة؟ | مقارنة سطرية | لا — قبل `_tx.RunAsync` كما كان |
| 2 | هل تغيّر موضع `Release`/`Complete`؟ | مقارنة الغلاف | لا — `catch` ثم بعد النجاح، ونفس `Snapshot` |
| 3 | هل تغيّر عدد/موضع `_tx.RunAsync` أو `SaveChangesAsync`؟ | عدّ آلي | `RunAsync = 1`، `SaveChangesAsync = 2` بنفس الموضعين |
| 4 | هل أُضيف حارس/تحقق أو حُذف؟ | مقارنة سطرية | لا — نفس الثماني ونفس الرسائل والترتيب |
| 5 | هل تكرار مساعدات بين الخدمة والمعالج؟ | بحث عن `ApplyRequest`/`ApplyRegistrationDate` | تعريف واحد لكل مساعد (`internal static` على الخدمة)، والمعالج يناديهما مؤهَّلين |
| 6 | هل تغيّر سطح `IDocumentService` بأكثر من الحذف المقصود؟ | عدّ آلية | 8 → 7، والحذف هو `CreateAsync` وحده |
| 7 | هل صار أي حقل في الخدمة أو المعالج ميتًا؟ | عدّ كل حقل بعد النقل | الخدمة: 5/5 مستخدمة؛ المعالج: 13/13 مستخدمة |
| 8 | هل بقي مستدعٍ للطريقة على الخدمة في `src`؟ | بحث شامل | 0 — المتحكم يستدعي المعالج، والخدمة لا تعرّف الكتابة |
| 9 | هل كل معالج مسجَّل في `DI`؟ | عدّ آلي مقابل الأصناف | 124/124/0 بلا تكرار |
| 10 | هل تغيّر مسار/صلاحية/شكل استجابة `POST api/documents`؟ | مقارنة الأكشن + `generate:api` | لا — نفس المسار والوسم والفحص و`CreatedAtAction`، والعميل المولَّد بلا فرق |
| 11 | هل صار partial فارغًا أو بقي ملف ميت؟ | عدّ أعضاء كل partial | `Crud.cs` أُفرغ فعلًا فحُذف بـ`git rm`؛ 3 partials متبقية كلها مستخدمة |
| 12 | هل تأثرت مسارات المسودة/الترقيم؟ | مقارنة جسم الدالة ونواة الإنشاء | لا — `diffs = 0` في الجسمين، ورسائل/مواضع الحجز نفسها |

## التحقق

`build -warnaserror` 0/0 · `format --verify-no-changes` نظيف · Architecture 29/29 ·
Application 758/1659/0 · Api 901/620/0 (والـ`Sequential` منفصلًا: 0/5/0 تخطٍّ محلي) ·
frontend 1809/1809 + `oxlint`/`tsc -b`/`build`/`guard`/`generate:api` بلا فرق ·
الحرّاس الستة نظيفة · `dependency-audit` 9 مشاريع · `npm-audit-guard` 0 ثغرة ·
`has-pending-model-changes` نظيف · `DI` 124/124/0 · 0 حقل ميت · 0 partial فارغ.

> اختبارات `PostgreSQL` (ومنها كل مسارات الإنشاء في `ServiceTests`) تُخطّى محليًا لغياب
> `DOCGEN_TEST_POSTGRES`؛ `backend-postgres` في `CI` هو المرجع القاطع للسلوك على القاعدة.

## الهجرات

**لا هجرة جديدة.** المعلّق يبقى **6**.
