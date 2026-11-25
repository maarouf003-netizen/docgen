# `P4-01co` ملحق — المراجعة النقدية (تكافؤ وحدود العقد) — 2026-11-25

> **نطاق الملحق:** مراجعة نقدية بعد نقل قراءات البوابة الثماني إلى معالجات استعلام وقبل
> الدمج: مطابقة كل دالة سطرًا/رمزًا، دلتا سطح `IPortalService`، إثبات القراءة الصرفة،
> الحقول/المعاملات/الاستيرادات الميتة، وعدّ تسجيلات `DI`.

## 1. مطابقة الدوال المنقولة

المنهج: استخراج كل جسم من `git show HEAD` ومن الملف الجديد، ثم تطبيع أسماء المعاملات إلى
خصائص `query.…` وإسقاط تأهيل `PortalService.` وتوحيد `var page/perPage` المحليين، ومقارنة
الأسطر الفعّالة سطرًا بسطر (بلا فراغات/تعليقات).

| الدالة | موضعها الجديد | الأسطر (قبل/بعد) | فرق الرموز | الاستثناء |
|---|---|---|---|---|
| `GetMyScopeAsync` | `GetPortalScopeQueryHandler` | 6 / 6 | 0 | — |
| `ListFilesAsync` | `ListPortalFilesQueryHandler.HandleCoreAsync` | 8 / 8 | 0 | `var` محليان |
| `ToListItem` | `ListPortalFilesQueryHandler.ToListItem` | 23 / 23 | 0 | تأهيل `BuildMatchedEntries` |
| `ExecutedSummary` | `ListPortalFilesQueryHandler.ExecutedSummary` | 5 / 5 | 0 | — |
| `ListAppealsAsync` | `ListPortalAppealsQueryHandler` | 17 / 17 | 0 | تمرير `_portal` صراحةً |
| `ListExecutionActionsAsync` | `ListPortalExecutionActionsQueryHandler` | 9 / 9 | 0 | تمرير `_portal`/`_documents` |
| `ListDelegationsAsync` | `ListPortalDelegationsQueryHandler` | 4 / 4 | 0 | تمرير `_portal`/`_documents` |
| `ListAppealDetailsAsync` | `ListPortalAppealDetailsQueryHandler` | 5 / 5 | 0 | تمرير `_portal`/`_documents` |
| `ListBaseNumbersAsync` | `ListPortalBaseNumbersQueryHandler` | 5 / 5 | 0 | تمرير `_portal`/`_documents` |
| `GetStatsAsync` | `GetPortalStatsQueryHandler.HandleCoreAsync` | 61 / 61 | 0 | `query.*` فقط |
| **الإجمالي** | — | **143 / 143** | **0** | 10 أسطر ميكانيكية موثّقة |

- **المساعدات المرقّاة/المنقولة:** `ResolveEffectiveIds` 6/6، `IsInScopeAsync` 2/2،
  `GetScopedDocumentAsync` 3/3، `BuildMatchedEntries` (5/5 و16/16)، `NarrowScope` 6/6،
  ومساعدات الإحصاءات الخمس (9/9، 11/11، 11/11، 19/19، 19/19) — الإجمالي **107/107**
  و`diffs = 0`؛ لا نسخة ثانية لأي مساعد في المعالجات.
- **الإجمالي الكلي:** **250 / 250** سطرًا فعّالًا.
- **النصوص الحرفية (اتجاهان):** 13 نصًا / 13 مميزًا؛ `missing = 0` و`added = 0`.
- **الفاصل:** `Math.Max`/`Math.Clamp` و`ServerClock` و`PortalScopeOrdering` في مواضعها
  قبل أي قراءة مستودع كما كانت.

## 2. دلتا سطح الواجهة

| القياس | قبل | بعد |
|---|---|---|
| أعضاء `IPortalService` | 10 | **2** |
| المحذوف | — | `GetMyScopeAsync`، `ListFilesAsync`، `ListAppealsAsync`، `ListExecutionActionsAsync`، `ListDelegationsAsync`، `ListAppealDetailsAsync`، `ListBaseNumbersAsync`، `GetStatsAsync` |
| المبقى | — | `GetFileAsync`، `ExportWorkbookAsync` |

- **تبرير الإبقاء:** كلاهما يكتب سجل تدقيق (`view_entity_portal_files` عند فتح الملف،
  و`export_entity_portal_excel` عند التصدير) — أي ليسا «قراءة صرفة بلا تدقيق» كما يشترط
  التكليف. `ExportWorkbookAsync` يضيف بناء مصنّف Excel وسقف الصفوف. الواجهة لم تصر فارغة
  فلم تُحذف ولم يُحذف تسجيلها.

## 3. إثبات القراءة الصرفة

| الملف | `SaveChanges`/`Add` كيان | `_uow`/`ITransactionRunner`/`ITransactionalRequest`/`RunAsync` | `IAuditLogger`/`LogAsync`/`IAuditedRequest` |
|---|---|---|---|
| `GetPortalScopeQuery.cs` | 0 | 0 | 0 |
| `ListPortalFilesQuery.cs` | 0 | 0 | 0 |
| `ListPortalAppealsQuery.cs` | 0 | 0 | 0 |
| `ListPortalExecutionActionsQuery.cs` | 0 | 0 | 0 |
| `ListPortalDelegationsQuery.cs` | 0 | 0 | 0 |
| `ListPortalAppealDetailsQuery.cs` | 0 | 0 | 0 |
| `ListPortalBaseNumbersQuery.cs` | 0 | 0 | 0 |
| `GetPortalStatsQuery.cs` | 0 | 0 | 0 |

- **الاستثناء الوحيد داخل الذاكرة:** `series.Add(...)` في بناء السلسلة الشهرية (قائمة
  محلية) — ليس كتابة مستودع.
- **مستودعات القراءة المستخدمة:** `ResolveForUserAsync`، `IsDocumentInScopeAsync`،
  `SearchScopedAsync`، `ListAmountRowsAsync`، `ListCreatedDatesAsync`،
  `CountDocsPerEntryAsync`، `AppealsBreakdownAsync`، `GetByIdAsync`، `ListByDocumentAsync`،
  ومعالجا قراءة الإنابة/الاستئناف الموجودان أصلًا (`GetDocumentDelegationsQuery`,
  `ListAppealsForDocumentQuery`).

## 4. الحقول والمعاملات والاستيرادات الميتة

| الموضع | قبل | بعد | المحذوف |
|---|---|---|---|
| `PortalService` | 10 حقولًا / 10 معاملًا | **7 / 7** | `_appeals`، `_appealReads`، `_delegationReads` (آخر مستخدميها انتقلوا إلى المعالجات) |
| `PortalController` | 12 معاملًا | 20 | حُذف صفر؛ أُضيفت ثمانية معالجات |
| ملفات `Services/PortalService*` | 4 partials | **3** | `PortalService.Stats.cs` (أُفرغ ثم `git rm`) |
| `PortalServiceFactory` (اختبار) | 10 معاملات خدمة | 7 | حُذف بناء `ListAppealsForDocumentQueryHandler`/`GetDocumentDelegationsQueryHandler` من المصنع (انتقل إلى المصنع الجديد) |

- **الاستيرادات المحذوفة:** من `PortalService.cs` (`Common.Audit`، `Common.Pipeline`،
  `UseCases.Appeals`، `UseCases.Delegations`، `Domain.Enums`، `System.Globalization`)؛
  ومن `Reads.cs`/`Export.cs` (`System.Globalization`، `Common.Audit`، `Common.Pipeline`،
  `UseCases.*`، `Domain.Entities`/`Enums`، `Options` حسب الملف)؛ ومن الاختبارات
  (`Common.Audit`، `Common.Interfaces`، `Services`، `Options`، `Infrastructure.Persistence`
  حيث لم تبقَ مستخدمة) — كل حذف مُثبت بالبناء الخالي من الأخطاء.

## 5. عدّ `DI`

| القياس | قبل (`c8562047`) | بعد |
|---|---|---|
| تسجيلات `IQueryHandler` | 38 | **46** |
| تسجيلات `ICommandHandler` | 118 | 118 |
| الإجمالي | 156 | **164** |
| أصناف المعالجات في `UseCases` | 156 | **164** |
| غير مسجَّل / مكرر | 0 / 0 | **0 / 0** |

## 6. المتحكم والاستجابة

- المسارات الثمانية كما هي (`my-scope`، `files`، `files/{id}/appeals`،
  `files/{id}/execution-actions`، `files/{id}/delegations`، `files/{id}/appeals/details`،
  `files/{id}/base-numbers`، `stats`)، ونفس `[Authorize(Roles = "entitymanager")]` على
  الصنف ونفس `[EnableRateLimiting(ExpensivePolicy)]` على `stats`/`export`.
- نفس الأجسام: `PortalScopeDto?`، `PagedResult<PortalFileListItemDto>`،
  `IReadOnlyList<PortalAppealDto>`، `IReadOnlyList<PortalExecutionActionDto>`،
  `IReadOnlyList<DelegationDto>`، `IReadOnlyList<AppealDto>`،
  `IReadOnlyList<BaseNumberHistoryDto>`، `PortalStatsDto` — وكلها `Ok(...)` أو `NotFound()`
  عند `null` كما كان؛ لا تغيير في `StatusCodes` ولا في نصوص الرسائل.
- `File` و`Export` بقيا على `IPortalService` بلا أي تغيير سلوكي، مع بقاء `_clock`/`_timeZone`
  لاسم ملف التصدير.

## 7. الاختبارات

- مصنع واحد جديد `PortalReadHandlerFactory` (حزمة `PortalReadHandlers` بالثمانية، مع
  `clock` اختياري) — لا بناءات مبعثرة للخدمة.
- `PortalServiceFactory` حُدِّث إلى منشئ الخدمة الجديد (7 معاملات) وبقي مستخدمًا لمسارات
  `GetFile`/`Export` فقط.
- جسر `PortalReadQueryExtensions`: 8 امتدادات مطابقة لتوقيعات الواجهة المحذوفة
  (بما فيها الافتراضيات `ct = default` و`entryId = null` وترتيب المعاملات).
- **76** موضع نداء حُوِّل بلا تعديل توقّع واحد؛ 17 موضعًا لـ`GetFile`/`Export` لم تُمسّ.
- لا تغيير في أعداد الاختبارات: Application 758/1659/0، Api 901/615/0 (والـ`Sequential`
  منفصلًا 0/5/0 محليًا).

## الهجرات

**لا هجرة جديدة.** المعلّق يبقى **6**.
