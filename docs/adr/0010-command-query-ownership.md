# ADR-0010 — ملكية الأوامر/الاستعلامات (Command/Query Ownership)

- **الحالة:** معتمد
- **التاريخ:** 2026-11-25
- **المرجع:** `P4-01b` (إغلاق نموذج الملكية) — بعد `P4-01a` (الأساس) و`P4-01c…P4-01cw`
  (نقل use-cases كتابةً وقراءةً).
- **يرتبط بـ:** [`0002-transactions-retry.md`](0002-transactions-retry.md)،
  [`0003-idempotency-all-writes.md`](0003-idempotency-all-writes.md)،
  [`0004-outbox-scope.md`](0004-outbox-scope.md)،
  [`0005-optimistic-concurrency-scope.md`](0005-optimistic-concurrency-scope.md).

## 1. السياق

كانت الخدمات (`*Service`) تملك القراءة والكتابة والمعاملة والتدقيق والحرّاس معًا، فيصعب
معرفة نقطة استخدام واحدة (use-case) وملكها. أدخل `P4-01a` خط أوامر/استعلامات ضيقًا
(`ICommandHandler<,>`/`IQueryHandler<,>` + `QueryHandlerBase`/`CommandHandlerBase` +
`IPipelineBehavior`)، ثم نقلت المراحل `P4-01b…P4-01cu` كل use-case كتابة ثم كل سطح
قراءة صرف من الوحدات المعلنة إلى معالج واحد لكل use-case.

هذا القرار يثبّت القاعدة، ويوثّق **ما يبقى على الخدمات عمدًا** لأنه ليس use-case
مستقلًا بل مساعد داخل معاملة مستدعٍ أو حارس/مزامنة/مرآة/`Outbox`/بيانات مرجعية.

## 2. القرار

### 2.1 معالج واحد لكل use-case

- كل نقطة كتابة في `API` = **`CommandHandler` واحد** يملك: التحقق، الحرّاس، الآثار
  الجانبية داخل المعاملة (الوقوعات/المرايا/`Outbox`/التدقيق)، وقرار الحفظ.
  المعاملة تُدار عبر `ITransactionalRequest`/`TransactionBehavior` أو
  `ITransactionRunner` عندما يفرض المسار `catch` حول المعاملة (كما في
  `TransferExecutionCircuitCommand`).
- كل سطح قراءة صرف = **`QueryHandler` واحد** بلا كتابة ولا معاملة ولا تدقيق
  (`IPortalService.GetFileAsync`/`ExportWorkbookAsync` استثناء موثّق لأنهما يكتبان تدقيقًا).
- لا يُكرَّر منطق بين معالج وخدمة: المساعدات النقية تُرقّى إلى `internal static`
  (أو إلى نوع إسقاط مشترك) ويستهلكها الجانبان.

### 2.2 المتحكمات رقيقة

المتحكم مسؤول عن: التوجيه (`[Route]`)، المصادقة/التفويض (`[Authorize]`، حرّاس القدرات
والنطاق)، تحويل الطلب إلى `Query`/`Command`، وترجمة الاستثناءات إلى
`ApiProblem`/رموز الحالة. **ممنوع** في المتحكم: `ITransactionRunner`، `SaveChanges`،
بدء معاملة، أو أوركسترا عمل. الحارس الآلي: **0** مطابقة لـ
`ITransactionRunner|RunAsync|BeginTransaction|SaveChanges|CommitAsync` في
`backend/src/DocGenerator.Api/Controllers` (تحقق 2026-11-25).

### 2.3 ما يبقى على الخدمات **عمدًا**

هذه الأعضاء ليست use-cases مستقلة؛ تُستدعى **داخل معاملة/سياق مستدعٍ** أو تخدم
السياسات والبنية التحتية. إبقاؤها على واجهة/خدمة مقصود ومثبّت هنا:

| العقد | العدد | لماذا تبقى |
|---|---:|---|
| `IDocumentDelegationService` | 8 | `SyncTargetsTransferredAsync` + حرّاس التعديل (`ValidateTargetEdit`/`ValidateSeizureLockAsync`) + مزامنة اللقطات/المرايا وإطلاق إشعاراتها + تثبيت الإضافات المحلية + تنبيهات تغيّر حالة المنيب — كلها داخل معاملة تعديل/نقل المستدعي أو بعد تثبيتها |
| `IDocumentAppealService` | 3 | `GetEntityAsync` (تحقق صلاحية عرض في المتحكم) + `IsAssignedFollowerAsync`/`HasForwardedAppealAsync` (سؤالا وصول يستهلكهما معالج السياسة والمراسلات/الإحالة) |
| `IDocumentWriteGuard` | 8 | حرّاس التزامن/الترقيم/الدائرة ووقوعات تبديل الدائرة وتدقيق الحقول — تُركَّب حول نواة الأمر ولا تملك use-case |
| `IDocumentAccessReader` | 3 | إسقاط `F-01` الضيق وسؤالا الاستئناف لسياسات الوصول (`P2-01`) — قراءة بلا كشف كيان |
| `IDocumentStatusService` | 2 | `ApplyRenewalAsync`/`AddStruckOffOccurrenceAsync`: كتابة حقول/وقوعات داخل معاملة أمر الحالة (`P4-01bd`) |
| `IHeadAlertService` (كتابة نظامية) | 4 | `CreateSystemAsync`/`ApplyCircuitRenamedAsync`/`ArchiveByDelegationAsync`/`ArchiveByAppealAsync`: منتج/مؤرشف تنبيهات يُستدعى داخل معاملة الأمر (والـ`Outbox` يرفض بلا معاملة) |
| `IHeadAlertOutbox` + `IHeadAlertOutboxStore` | 2 + 12 | تسجيل الرسائل داخل معاملة المنتج، ودورة حياة العامل (التقاط/lease/إتمام) — بنية تحتية لا endpoint |
| `IForumRetentionService` | 1 | `ExecuteOnceAsync`: مهمة احتفاظ دورية (حذف الرسائل القديمة غير المثبّتة) |
| `IHeadAlertSuccessionService` | 1 | `ApplyAsync`: نقل آثار التنبيهات ضمن معاملة تعطيل/تعاقب الرئيس |
| `IExecutionCircuitService` | 1 | `SyncPendingAlertsAfterTransferAsync`: مزامنة تنبيهات معلّقة داخل معاملة نقل الملكية |
| `IExecutionActionService` | 1 | `SeedInitialActionsAsync`: زرع الإجراءات ضمن معاملة إنشاء/تعديل المستند |
| `IDocumentNumberRegistry` | 9 | الكاتب الوحيد لأرقام الملف/الأساس: كل عملية داخل معاملة المستدعي + قفل (دائرة، سنة) + فحص تفرد |
| `IDocumentContextBuilder` | 1 | بناء قاموس سياق القالب لمسار الطباعة — مساعد توليد لا use-case |
| `INumberWarningTokenService` | 2 | إصدار/تحقق رمز تحذير إعادة استخدام الرقم (R7) — مساعد تحقق |
| `IPortalService` | 2 | `GetFileAsync`/`ExportWorkbookAsync`: قراءتان تكتبان تدقيقًا (`view_entity_portal_files`/`export_entity_portal_excel`) فليستا قراءة صرفة (`P4-01co`) |
| `IReferenceDataCache` | 2 | `GetOrCreateAsync`/`Invalidate`: كاش البيانات المرجعية البحتة مع إبطال بعد الكتابة |
| `IExcelExportService` | 4 | مصنّفات `xlsx` نقية من صفوف جاهزة — تنسيق لا قراءة قاعدة ولا use-case |

### 2.4 سطح القراءة المتبقي: **صفر** (`P4-01cw` أغلق الدين)

`DoD` الخاص بـ`P4-01` هو **ملكية الكتابة**: كل نقطة كتابة = handler واحد، ولا معاملة
مكررة في المتحكم. وكانت هناك أسطح **قراءة صرفة** معلنة؛ أغلقها `P4-01cw` (2026-11-25):

| العقد | القراءات المنقولة | المصير |
|---|---:|---|
| `IHeadAlertService` | 6 (`ListForActorAsync`/`ListSentAsync`/`ListByDelegationAsync`/`GetDetailsAsync`/`GetRecipientsAsync`/`CountUnreadAsync`) | `UseCases/Alerts/` — وبقي على الواجهة **4** أعضاء كتابة نظامية |
| `IStrikeOffReminderService` | 3 (`GetAsync`/`GetListAsync`/`GetCountAsync`) | `UseCases/Reminders/` — وحُذفت الواجهة والخدمة |
| `IAuditLogService` | 2 (`SearchAsync`/`GetDocumentChangesAsync`) | `UseCases/Audit/` — وحُذفت الواجهة والخدمة (لا كاتب/احتفاظ عليهما) |
| `IExcelExportService` | 4 مصنّفات | ليس قراءة قاعدة — بقي كمساعد تنسيق ولا يُحتسب دينًا |

> **تصحيح موثّق:** ادّعاء «لا يبقى سطح قراءة صرف معلن» في خريطة `P4-01ct` كان غير دقيق
> وقتها؛ صُحّح في §2.4 من هذه الوثيقة، ثم أُغلق فعليًا في `P4-01cw`. الأعداد الفعلية
> بعد `P4-01cw`: **11 → 0** قراءة على الخدمات، والدليل `docs/audit/246…248`.

## 3. الإنفاذ والتحقق

1. **عدّ التسجيلات:** كل `IQueryHandler<,>`/`ICommandHandler<,>` مسجَّل مرة واحدة،
   وعدد التسجيلات = عدد أصناف المعالجات = **229** (**111** استعلامًا + **118** أمرًا) في
   2026-11-25، بلا غير مسجَّل وبلا مكرر (تحقق آلي في لقطة `P4-01cw`).
2. **لا خدمة قراءة ميتة:** الواجهات المحذوفة (`IForumService`/`IAppSuggestionService`/
   `IPersonalReminderService` وغيرها) لا تُسجَّل في `DI`، ولا موضع نداء لها في
   `backend/src` أو `backend/tests`.
3. **متحكمات بلا معاملات:** الحارس النصي أعلاه يفشل عند أي `RunAsync`/`SaveChanges` في
   `Controllers`.
4. **اختبار توصيل** لكل دفعة استعلامات جديدة يثبت التسجيل مرة واحدة ونوع الاستجابة.
5. **حرّاس الاختبارات** (`Architecture` 29/29) تمنع كسر الحدود بين الطبقات.

## 4. النتائج

- الملكية صريحة: من يقرأ يجد `UseCases/<Module>`، ومن يكتب يجد `CommandHandler` واحدًا
  يحمل معاملته وآثاره.
- الخدمات المتبقية صارت إما بنية تحتية/حرّاسًا أو مساعدات تنسيق — لا use-cases كتابة.
- المتحكمات لا تعرف `DbContext` ولا `ITransactionRunner`؛ اختبارات المسار الكامل تبقى
  المرجع السلوكي.
- الدين المعلن (قراءات §2.4) كان بندًا مستقلًا ولم يُخفَ داخل «مغلق»، ثم أُغلق في
  `P4-01cw`: لا سطح قراءة صرف على الخدمات، والدليل `docs/audit/246…248`.
