# `P4-01cr` — نقل قراءات البحث/القوائم/التصدير الاثنتي عشرة إلى Query Handlers وحذف خدمة البحث — 2026-11-25

> **النطاق:** المرحلة التاسعة عشرة في سلسلة نقل ملكية `use-case`: تحويل **كل** أسطح
> القراءة الاثنتي عشرة على خدمة البحث (بحث/قوائم المحذوفات والمشطوبة والمنفذة
> والمحالة للبداية، وقوائمها المسقطة، والبحث/القائمة الرئيسية، وخيارات الفلترة،
> والتصدير) إلى استعلامات `IQuery<TResponse>` ومعالجات `QueryHandlerBase` تحت
> `backend/src/DocGenerator.Application/UseCases/Documents/`، **قراءة صرفة بلا كتابة
> ولا معاملة ولا تدقيق** — باستثناء واحد موثّق: التصدير يكتب سجل تدقيق
> `export_documents` كما في الأصل حرفيًا (RF-017/SEC-002). الفرع
> `refactor/p4-01cr-document-search-reads` من `master` (`ee43a71b`)، بحفاظ حرفي على
> السلوك والنطاق والرسائل ورموز الحالة.

## ما نُفِّذ

| الاستعلام | المعالج | الاعتمادات | النتيجة |
|---|---|---|---|
| `SearchDocumentsQuery` | `SearchDocumentsQueryHandler` | `IDocumentRepository` + `IAppealRepository` + `TimeProvider` + `TimeZoneInfo` | `PagedResult<DocumentResponse>` |
| `SearchDocumentListQuery` | `SearchDocumentListQueryHandler` | `IDocumentRepository` + `IAppealRepository` + `IKeysetCursorCodec` + `TimeProvider` + `TimeZoneInfo` | `PagedResult<DocumentListItemDto>` |
| `SearchDeletedDocumentsQuery` | `SearchDeletedDocumentsQueryHandler` | `IDocumentRepository` + `TimeProvider` + `TimeZoneInfo` | `PagedResult<DocumentResponse>` |
| `SearchDeletedDocumentListQuery` | `SearchDeletedDocumentListQueryHandler` | `IDocumentRepository` + `IKeysetCursorCodec` + `TimeProvider` + `TimeZoneInfo` | `PagedResult<DocumentListItemDto>` |
| `SearchStruckOffDocumentsQuery` | `SearchStruckOffDocumentsQueryHandler` | `IDocumentRepository` + `TimeProvider` + `TimeZoneInfo` | `PagedResult<DocumentResponse>` |
| `SearchStruckOffDocumentListQuery` | `SearchStruckOffDocumentListQueryHandler` | `IDocumentRepository` + `IKeysetCursorCodec` + `TimeProvider` + `TimeZoneInfo` | `PagedResult<DocumentListItemDto>` |
| `SearchExecutedDocumentsQuery` | `SearchExecutedDocumentsQueryHandler` | `IDocumentRepository` + `TimeProvider` + `TimeZoneInfo` | `PagedResult<DocumentResponse>` |
| `SearchExecutedDocumentListQuery` | `SearchExecutedDocumentListQueryHandler` | `IDocumentRepository` + `IKeysetCursorCodec` + `TimeProvider` + `TimeZoneInfo` | `PagedResult<DocumentListItemDto>` |
| `SearchReferredToStartDocumentsQuery` | `SearchReferredToStartDocumentsQueryHandler` | `IDocumentRepository` + `TimeProvider` + `TimeZoneInfo` | `PagedResult<DocumentResponse>` |
| `SearchReferredToStartDocumentListQuery` | `SearchReferredToStartDocumentListQueryHandler` | `IDocumentRepository` + `IKeysetCursorCodec` + `TimeProvider` + `TimeZoneInfo` | `PagedResult<DocumentListItemDto>` |
| `ExportDocumentsQuery` | `ExportDocumentsQueryHandler` | `IDocumentRepository` + `IAuditLogger` + `IOptions<ExportOptions>` + `TimeProvider` + `TimeZoneInfo` | `List<DocumentExportRow>` |
| `GetDocumentFilterOptionsQuery` | `GetDocumentFilterOptionsQueryHandler` | `IDocumentRepository` | `DocumentFilterOptions` |

| البند | التفصيل |
|---|---|
| العقود | الواجهة `IDocumentSearchService` أُفرغت ثم حُذفت نهائيًا (**12 → 0**) مع تسجيلها في `DI`؛ لا عضو قراءة متبقٍ |
| الخدمة | `DocumentSearchService` حُذف بالكامل (**1 → 0**) بلا partial باقٍ |
| المساعدات | `BuildListResult` و`NextCursor` وإثراء شارة الاستئناف (نسختا القائمة/الاستجابة) نُقلت كما هي إلى `UseCases/Documents/DocumentSearchHelpers.cs` (`internal static`) — مصدر واحد بلا تكرار، والمعالجات تستدعيها صراحةً بتمرير `_clock`/`_timeZone`/`_cursorCodec`/`_appeals` |
| `DI` | حُذف تسجيل خدمة واحد وأُضيفت اثنا عشر تسجيلًا `IQueryHandler<Query, Response>`؛ الإجمالي **179 → 191** معالجًا (**61 → 73** استعلامًا + **118** أمرًا)، و**191** صنف معالج، 0 غير مسجَّل، 0 مكرر |
| المتحكمون | `DocumentsController` يحقن اثني عشر معالجًا بدل الخدمة (منشئ **42 → 53** معاملًا)؛ نفس المسارات و`[Authorize]` وحرّاس القدرات/النطاق و`Ok(...)`/`Forbid()`/`ApiProblem` وأشكال الأجسام |
| الاختبارات | جسر جديد `DocumentSearchQueryExtensions` (اثنا عشر امتدادًا بتوقيعات الخدمة القديمة فوق سجل `DocumentSearchHandlers`) حوّل **82** موضع نداء بلا تغيير أي توقّع؛ ومصنع الاختبار يعيد سجل المعالجات الاثني عشر بدل بناء الخدمة؛ وأُضيف اختبار توصيل واحد يثبت تسجيل كل استعلام مرة واحدة |
| النهاية | لا تغيير في `OpenAPI` ولا في العميل المولَّد — العقود الخارجية كما هي |

> **الاستثناء الموثّق الوحيد للقراءة الصرفة:** `ExportDocumentsQueryHandler` يستدعي
> `IAuditLogger.LogAsync(...)` بسجل `export_documents` بعد جلب الصفوف، بنفس النص
> والوسائط والعدد والفلاتر — حفظة واحدة بلا معاملة، كما في الخدمة الأصلية حرفيًا.
> لا استعلام آخر يحقن `IAuditLogger` ولا يكتب شيئًا.

## إثبات الحفاظ على السلوك (لكل دالة)

> التطبيع: استبدال أسماء المعاملات بخصائص `query.…`، وإسقاط تأهيل
> `_documents`/`_appeals`/`_cursorCodec`، وإقحام `DocumentSearchHelpers.` قبل المساعدات،
> ثم مقارنة **الجمل الفعّالة** (لا الأسطر الملفوفة).

| الدالة | الجمل الأصلية/المعالج | `diffs` دلالية | الاستثناءات الموثّقة |
|---|---|---|---|
| `SearchDeletedAsync` | 9 / 9 | 0 | — |
| `SearchDeletedListAsync` | 6 / 7 | 0 | سطر ميكانيكي واحد: تمرير `_clock`/`_timeZone` صراحةً إلى `BuildListResult` |
| `SearchStruckOffAsync` | 9 / 9 | 0 | — (الفلاتر الستة `null` كما هي) |
| `SearchStruckOffListAsync` | 6 / 7 | 0 | سطر ميكانيكي واحد (تمرير الساعة/المنطقة) |
| `SearchExecutedAsync` | 9 / 9 | 0 | — |
| `SearchExecutedListAsync` | 6 / 7 | 0 | سطر ميكانيكي واحد (تمرير الساعة/المنطقة) |
| `SearchReferredToStartAsync` | 9 / 9 | 0 | — |
| `SearchReferredToStartListAsync` | 6 / 7 | 0 | سطر ميكانيكي واحد (تمرير الساعة/المنطقة) |
| `SearchAsync` | 12 / 12 | 0 | — |
| `SearchListAsync` | 9 / 10 | 0 | سطر ميكانيكي واحد (تمرير الساعة/المنطقة)، مع إبقاء `perPage <= 0 ? 20` حرفيًا |
| `GetFilterOptionsAsync` | 1 / 1 | 0 | — (تعبير واحد يفوّض للمستودع) |
| `ExportAsync` | 16 / 16 | 0 | — (يُبقي سقف التصدير، والرقم الفعّال، وسجل التدقيق بنفس النص) |
| **الإجمالي** | **98 / 103** | **0** | خمسة أسطر ميكانيكية فقط |
| `BuildListResult` | 8 / 8 | 0 | — |
| `NextCursor` | 12 / 12 | 0 | — |
| إثراء القائمة (DTO) | 10 / 10 | 0 | — |
| إثراء الاستجابة | 10 / 10 | 0 | — |
| **المساعدات** | **40 / 40** | **0** | — |

- **النصوص الحرفية (اتجاهان):** **7** نص في الجانبين (ستة في التصدير: رسالة السقف،
  فاصل الفلاتر `،`، مفتاح `export_documents`، ونصوص التفاصيل الثلاثة) + نص
  `"نوع مؤشر غير معروف"` في `NextCursor`؛ `missing = 0` و`added = 0`.
- **عدم الكتابة:** فحص نصي لكل ملفات
  `UseCases/Documents/{Search*,GetDocumentFilterOptions}Query.cs` والمساعدات:
  `SaveChanges`/`AddAsync`/`Remove`/`_uow`/`ITransactionRunner`/`ITransactionalRequest`/
  `RunAsync`/`BeginTransaction`/`ExecuteUpdate`/`ExecuteDelete`/`IAuditLogger`/
  `IAuditedRequest` = **0 مطابقة**؛ `ExportDocumentsQuery.cs` وحده يحقن `IAuditLogger`
  ويكتب سجلًا واحدًا (الاستثناء الموثّق أعلاه).
- **النطاق/الصلاحيات:** الحرّاس كلهم في المتحكمات كما كانوا
  (`CanSearchByLawyer`، `CanViewDeletedDocuments`، `ResolveListScope`،
  `CanSeeAdministrativeBranch`، `CanSeeAssignedLawyer`) وقبل الاستعلام بنفس الترتيب؛
  المعالجات لا تكرر فحص صلاحية.
- **الفاصل الحتمي/الترتيب:** `Math.Max(1, Page)` و`Math.Clamp(PerPage, 1, 100)` بنفس
  الترتيب (مع استثناء `SearchListAsync` المحفوظ `perPage <= 0 ? 20`)، وأنواع مؤشرات
  المفاتيح الخمسة كما هي (`CreatedAt`/`DeletedAt`/`StruckOffDate`/`UpdatedAt`/
  `StartReferralDate`)، و`ServerClock.CurrentYear` بنفس الساعة/المنطقة.
- **الفلاتر الثابتة:** `SearchStruckOff*` تمرّر `null` للفلاتر الستة كما في الأصل؛
  `Export` يعدّ أولًا (`CountExportAsync`) ثم يفحص السقف ثم يجلب الصفوف.

## التحقق

`build -warnaserror` 0/0 · `format --verify-no-changes` نظيف · Architecture 29/29 ·
Application 768/1661/0 (منها اختبار التوصيل الجديد) · Api 901/615/0 (والـ`Sequential`
منفصلًا: 0/5/0 تخطٍّ محلي) · frontend 1809/1809 (171 ملفًا) +
`oxlint`/`tsc -b`/`build`/`guard`/`generate:api` بلا فرق
(`git diff --ignore-cr-at-eol --exit-code -- src/api/generated.ts` = 0 مع استعادة ضجيج
`CRLF`) · الحرّاس الستة نظيفة · `dependency-audit` 9 مشاريع · `npm-audit-guard` 0 ثغرة ·
`has-pending-model-changes` نظيف · `DI` 191/191/0 · 0 حقل ميت · 0 partial فارغ.

> اختبارات `PostgreSQL` (ومنها كل مسارات البحث/التصدير) تُخطّى محليًا لغياب
> `DOCGEN_TEST_POSTGRES`؛ `backend-postgres` في `CI` هو المرجع القاطع للسلوك على القاعدة.

## الهجرات

**لا هجرة جديدة** (لم يُمسّ أي كيان أو تعيين). المعلّق يبقى **6**.
