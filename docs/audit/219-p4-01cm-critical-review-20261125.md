# `P4-01cm` ملحق — المراجعة النقدية (تكافؤ وحدود العقد) — 2026-11-25

> **نطاق الملحق:** مراجعة نقدية بعد نقل قراءات الاستئنافات السبع إلى معالجات استعلام
> وقبل الدمج: مطابقة كل دالة سطرًا/رمزًا، دلتا سطح `IDocumentAppealService`، إثبات
> القراءة الصرفة، الحقول/المعاملات/الاستيرادات الميتة، وعدّ تسجيلات `DI`.

## 1. مطابقة الدوال المنقولة

| الدالة | موضعها الجديد | أسطر (قبل/بعد) | فرق الرموز | الاستثناء |
|---|---|---|---|---|
| `ListForDocumentAsync` | `ListAppealsForDocumentQueryHandler` | 4 / 5 | 0 | سطر ربط `documentId` |
| `SearchAsync` | `SearchAppealsQueryHandler.HandleCoreAsync` | 3 / 5 | 0 | `var` + لفّ `return` |
| `SearchCoreAsync` | `SearchAppealsQueryHandler.SearchCoreAsync` | 9 / 9 | 0 | تأهيل `ToDto` |
| `GetAsync` | `GetAppealQueryHandler` | 2 / 2 | 0 | تأهيل `ToDto` |
| `CountByAssigneeForHeadAsync` | `CountAssigneeAppealsForHeadQueryHandler` | 4 / 4 | 0 | خصائص `query` |
| `GetBaseNumberHistoryAsync` | `GetAppealBaseNumberHistoryQueryHandler` | 6 / 6 | 0 | — |
| `GetActionsAsync` | `GetAppealActionsQueryHandler` | 6 / 6 | 0 | تأهيل `ToActionDto` |
| `GetRemindersAsync` | `GetAppealRemindersQueryHandler` | 22 / 22 | 0 | — |
| `ListReminderRowsAsync` | `GetAppealRemindersQueryHandler` | 14 / 14 | 0 | `AppealTitleOf` محلية |
| **الإجمالي** | — | **70 / 73** | **0** | 3 أسطر ميكانيكية |

- **النصوص الحرفية:** 3 نصوص مميزة في الأجسام المنقولة، `missing = 0` و`added = 0`.
- **المساعدات المشتركة:** `ToDto` (17 سطرًا) و`ToActionDto` (9 أسطر) بقيا تعريفًا
  واحدًا `internal static` على الخدمة؛ لا نسخة ثانية في المعالجات.
- **الفاصل:** `Math.Max`/`Math.Clamp` و`ServerClock.CurrentYear` في الموضع نفسه قبل
  أي قراءة مستودع.

## 2. دلتا سطح الواجهة

| القياس | قبل | بعد |
|---|---|---|
| أعضاء `IDocumentAppealService` | 10 | 3 |
| المحذوف | — | `ListForDocumentAsync`، `SearchAsync`، `GetAsync`، `CountByAssigneeForHeadAsync`، `GetBaseNumberHistoryAsync`، `GetActionsAsync`، `GetRemindersAsync` |
| المبقى | — | `GetEntityAsync`، `IsAssignedFollowerAsync`، `HasForwardedAppealAsync` |

- الإبقاء مبرَّر: الأول كيان مرتبط لتحقق صلاحية العرض في المتحكم؛ والآخران حارسان
  يُستدعيان من `CorrespondenceService`/`DocumentAccessReader`/صلاحيات المتحكم داخل
  مسارات أخرى. لا أعضاء جديدة في الواجهة (حذف صرف).

## 3. إثبات القراءة الصرفة

| الملف | `SaveChanges`/`Add`/`Remove` | `_uow`/`ITransactionRunner`/`ITransactionalRequest` | `IAuditLogger`/`LogAsync` | `RunAsync` |
|---|---|---|---|---|
| `ListAppealsForDocumentQuery.cs` | 0 | 0 | 0 | 0 |
| `SearchAppealsQuery.cs` | 0 | 0 | 0 | 0 |
| `GetAppealQuery.cs` | 0 | 0 | 0 | 0 |
| `CountAssigneeAppealsForHeadQuery.cs` | 0 | 0 | 0 | 0 |
| `GetAppealBaseNumberHistoryQuery.cs` | 0 | 0 | 0 | 0 |
| `GetAppealActionsQuery.cs` | 0 | 0 | 0 | 0 |
| `GetAppealRemindersQuery.cs` | 0 | 0 | 0 | 0 |

- المستودعات المستخدمة كلها قراءة: `ExistsAsync`، `ListByDocumentAsync`، `SearchAsync`،
  `GetByIdWithDetailsAsync`، `CountPendingByAssigneeInScopeAsync`، `ListByAssigneeAsync`.

## 4. الحقول والمعاملات والاستيرادات الميتة

| الموضع | قبل | بعد | المحذوف |
|---|---|---|---|
| `DocumentAppealService` | 11 حقلًا / 11 معاملًا | 1 / 1 | `_documents`، `_users`، `_uow`، `_tx`، `_audit`، `_alerts`، `_alertOutbox`، `_clock`، `_timeZone`، `_dbErrors` |
| `DocumentsController` | 39 معاملًا | 38 | `IDocumentAppealService _appeals` (ميت أصلًا) |
| `AppealsController` | 22 معاملًا | 29 | إضافة 7 معالجات استعلام وحذف صفر |
| `PortalService` | 10 معاملات | 10 | استبدال `IDocumentAppealService` بـ`IQueryHandler<ListAppealsForDocumentQuery, …>` |
| ملفات الخدمة | 8 partials | 6 | `Actions`/`Assignment`/`BaseNumbers` (أُفرغت ثم حُذفت)، وإضافة `Guards.cs` |

- **الاستيرادات:** حُذفت `Common.Security` و`Common.Interfaces` من `Mapping`،
  و`System.Globalization`/`Common.Interfaces`/`Common.Security`/`DTOs`/`Enums` من
  `Helpers`، و`System.Globalization`/`Common.Interfaces`/`Common.Security` من `Parties`،
  و`Common.Interfaces`/`Common.Security` من `Crud`؛ و`Common.Interfaces`/`Services`/
  `Infrastructure.Persistence` من اختبارات لم تعد تستخدمها.

## 5. عدّ `DI`

| القياس | القيمة |
|---|---|
| تسجيلات `IQueryHandler` | 32 |
| تسجيلات `ICommandHandler` | 118 |
| الإجمالي | **150** |
| أصناف المعالجات في `UseCases` | **150** |
| غير مسجَّل / مكرر | 0 / 0 |

## 6. المتحكم والاستجابة

- المسارات السبعة كما هي، و`[Authorize(Roles = "lawyer,head,subhead,manager,admin")]`
  على الصنف و`[Authorize(Roles = "lawyer")]` على `appeals/reminders` كما هي.
- نفس فحوص `HasFullAccess`/`IsHeadOrSubHead`/فرع الرئيس/شعبة رئيس الشعبة/`CanAssignAppeals`/
  `CanViewAppealAsync` بنفس الترتيب.
- نفس الأجسام: `List<AppealDto>`، `PagedResult<AppealDto>`، `AppealDto`/`NotFound()`،
  `{ count }`، `List<AppealBaseNumberHistoryDto>`، `List<AppealActionDto>`،
  `List<AppealReminderDto>`، ونفس `ApiProblem` عند `ArgumentException`.
- لا تغيير في `StatusCodes` ولا في نصوص الرسائل.

## 7. الاختبارات

- جسر `AppealReadQueryExtensions`: 7 امتدادات مطابقة لتوقيعات الواجهة المحذوفة
  (بما فيها الافتراضيات `ct = default` وترتيب `ownerSectionId`).
- 48 موضع نداء حُوِّل بلا تعديل توقّع واحد؛ 4 مواضع بناء قديمة للخدمة إما حُذفت
  (لأنها صارت بلا مستخدم) أو حُدّثت إلى منشئ معامل واحد في اختبار `GetEntityAsync`.
- `PortalServiceFactory` يبني `ListAppealsForDocumentQueryHandler` صراحةً، والحقول
  الميتة الثمانية حُذفت.
- لا تغيير في أعداد الاختبارات: Application 758/1659/0، Api 901/615/0 (والـSequential
  منفصلًا 0/5/0 محليًا).

## الهجرات

**لا هجرة جديدة.**
