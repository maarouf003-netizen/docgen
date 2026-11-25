# `P4-01cq` — المراجعة النقدية الأولى (تفعيل/تكافؤ/عقود) — 2026-11-25

> **نطاق المراجعة:** بعد نقل قراءات المستند السبع إلى Query Handlers وحذف
> `IDocumentService` وقبل الدمج. المنهج: مقارنة لكل دالة أصلية بمعالجها بعد التطبيع،
> وعدّ سطح الواجهة والخدمة والتسجيلات، وفحص نصي لعدم الكتابة، ومسح شامل لبقايا
> المستدعين والاستيرادات والحقول الميتة، ومقارنة إجراءات المتحكمات سطرًا بسطر.

## أسئلة المراجعة والنتائج

| # | السؤال النقدي | الفحص | النتيجة |
|---|---|---|---|
| 1 | هل نُقلت كل أعضاء `IDocumentService`؟ | عدّ الواجهة قبل الحذف | نعم — **7/7**؛ لا عضو متبقٍ |
| 2 | هل عضوا `IDocumentStatusService` قراءة صرفة؟ | قراءة الواجهة والتنفيذ والمستدعين | لا — كلاهما كاتب داخل معاملة مستدعٍ (`ApplyRenewalAsync`/`AddStruckOffOccurrenceAsync`)؛ بقيا **2 → 2** بقاعدة الاستثناء المنصوصة |
| 3 | هل تكافئ كل دالة معالجها دلاليًا؟ | مقارنة الجمل الفعّالة بعد التطبيع | نعم — **19/21** جملة، **0** فرق دلالي، سطران ميكانيكيان موثّقان |
| 4 | هل أُضيفت كتابة أو معاملة أو تدقيق؟ | فحص نصي لكل ملفات `UseCases/Documents/*Query.cs` | لا — **0/0/0** |
| 5 | هل تغيّر سطح الواجهة بأكثر من الحذف المقصود؟ | `IDocumentService` قبل/بعد | **7 → 0** ثم حُذفت الواجهة وتسجيلها؛ `IDocumentSearchService` بلا مساس |
| 6 | هل بقيت الخدمة أو أي partial منها؟ | وجود `DocumentService*` في `src` | **0 ملف خدمة**؛ حُذفت ثلاثة partials، وحُوِّل ملف التطبيع إلى `DocumentUpsertMapper` |
| 7 | هل صار أي حقل أو معامل منشئ ميتًا؟ | عدّ كل حقل مقابل استخدامه | `DocumentService` حُذف بكامل حقوله الخمسة؛ المتحكمات لا حقول ميتة؛ `DelegationsController` نُظّف استيراد `Services` |
| 8 | هل بقي مستدعٍ للخدمة أو لإحدى المنهجيات السبع على خدمة؟ | بحث شامل في `backend/src` | **0** — المتحكمات تستدعي المعالجات |
| 9 | هل كل معالج مسجَّل في `DI`؟ | عدّ آلي للتسجيلات مقابل الأصناف | **179/179/0** (61 استعلامًا + 118 أمرًا) |
| 10 | هل تغيّر مسار/صلاحية/شكل استجابة أي نقطة؟ | مقارنة `DocumentsController`/`DelegationsController`/`AppealsController` | لا — نفس المسارات والأدوار وحرّاس النطاق و`Ok`/`NotFound`/`Forbid`/`ApiProblem` والأجسام |
| 11 | هل انزاح ترتيب الحرّاس أو نطاق المالك؟ | مقارنة كتل `GetBaseNumberHistory`/`GetDeletedBaseNumberHistory`/`Rotation`/`CountFilesByOwner` | لا — الحرّاس قبل الاستعلام، ونفس الوسائط (`scopeBranchId`/`ownerSectionId`) |
| 12 | هل تغيّر سلوك الملف المحذوف (`!IsDeleted` شرطًا)؟ | مقارنة `GetDeletedAsync` | لا — نفس الشرط ونفس الإسقاط `DocumentResponse.FromEntity` |
| 13 | هل تغيّرت رسالة أي رفض؟ | تتبّع النصوص الحرفية | لا — رسالتا `CountFilesByOwner` كما هما، ورسائل الرفض في المتحكمات بلا مساس |
| 14 | هل أُضيفت هجرة أو تغيّر النموذج؟ | `has-pending-model-changes` | لا — نظيف، المعلّق يبقى 6 |
| 15 | هل غُطّيت عملية النقل باختبارات؟ | عدّ مواضع النداء والاختبارات | **137** موضع نداء محوَّل عبر الجسر + اختبارا توصيل جديدان + تحديث 4 مواضع بناء متحكم |
| 16 | هل تحوّل أي اختبار فعلي إلى تخطٍّ جديد؟ | أرقام Application/Api قبل/بعد | لا — التخطي ارتفع 2 فقط لأن الاختبارين الجديدين `PostgresFact` (يُشغَّلان في CI) |

## تدقيق التكافؤ الحقل بحقل (عيّنة المسار الكامل)

مسار `GET /api/documents/deleted/{id}/base-numbers` (المحذوفات):

`id` (Route) ← `[Authorize(Roles = …)]` ← `GetDeletedDocumentQuery` ←
`IDocumentRepository.GetDeletedByIdAsync` (IgnoreQueryFilters) ← `!doc.IsDeleted`
← `RequireDocumentAccessAsync(doc, Read)` ← `GetDeletedDocumentBaseNumberHistoryQuery`
← `IDocumentNumberRegistry.GetHistoryAsync(id, includeDeletedDocument: true, ct)` ←
`Ok(List<BaseNumberHistoryDto>)` كما هو.

- الترتيب والوسائط مطابقة: `includeDeletedDocument: true` في المحذوف و`false` في الحالي.
- `DocumentResponse.FromEntity(doc, ServerClock.CurrentYear(...))` بنفس الساعة/المنطقة.
- الصلاحية تُقيَّم على الاستجابة الكاملة المحمَّلة كما كان (لا تحويل إلى إسقاط خفيف هنا).

## تدقيق عدّ المعالجات وعدم التكرار

- 61 تسجيل `IQueryHandler<,>` + 118 تسجيل `ICommandHandler<,>` = **179**، مقابل **179**
  صنف معالج في `Application` → 0 غير مسجَّل و0 مكرر.
- `GetDocumentQuery` بقي مسجَّلًا مرة واحدة؛ أُضيفت ستة أسطر متجاورة بلا تكرار.
- لم يُسجَّل أي معالج في طبقة `Api`؛ خط الأنابيب في `Application` فقط.

## التحقق المصاحب

`build -warnaserror` و`format --verify-no-changes` وArchitecture 29/29 والاختبارات
والحرّاس الستة و`dependency-audit`/`npm-audit-guard`/`has-pending-model-changes`
كلها خضراء كما في `230`.
