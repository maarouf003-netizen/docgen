# `P4-01cq` ملحق — مراجعة هجومية ثانية وتنظيف — 2026-11-25

> **نطاق الملحق:** مراجعة عدائية بعد تحويل قراءات المستند السبع إلى Query Handlers
> وقبل الدمج: بقايا الاستدعاءات، والملفات الجزئية الفارغة، والمزيّفات/المصانع القديمة،
> وتكرار المساعدات، والحقول/المعاملات/الاستيرادات الميتة، وتكافؤ الحمولة والرسائل
> والنطاق ورمز الحالة في المتحكمات الثلاثة.

## ما فُحص

- بحث شامل في `backend/src` عن `IDocumentService`/`DocumentService` وعلى أي مستدعٍ
  للمنهجيات السبع على خدمة: **0 مستدعٍ قديم** — `DocumentsController` يحقن خمسة معالجات
  إضافية و`DelegationsController`/`AppealsController` يحقنان `GetDocumentQuery`،
  والمنهجيات لم تعد موجودة إلا على `IDocumentRepository`/`IDocumentNumberRegistry`/`IUserRepository`.
- عدّ أعضاء `IDocumentService`: **7 → 0**، وحُذف الملف وتسجيله في `DI`.
- عدّ partials: **4 → 1** (`DocumentUpsertMapper` فقط، غير فارغ)؛ حُذفت ثلاثة partials
  بعد إفراغها فعليًا بـ`git rm`.
- فحص المزيّفات: لا `StubDocumentService` بعد استبداله بـ`StubGetDocumentHandler`؛
  ولا `IDocumentService` في أي اختبار.
- عدّ تسجيلات `DI` مقابل أصناف المعالجات: **179/179/0** (61 استعلامًا + 118 أمرًا)
  بلا تكرار ولا صنف غير مسجَّل.
- فحص تكرار المساعدات: لا مساعدات في `DocumentService` أصلًا عدا `CurrentYear()` التي
  أُزيلت واستُبدلت بالمصدر الواحد `ServerClock.CurrentYear`؛ لم تُنسخ أي دالة.
- مطابقة كل دالة أصلية بمعالجها (19/21 جملة، 0 فرق دلالي، سطران لفّ ميكانيكيان) وتتبّع
  النصوص الحرفية (2 نص / 0 فرق).
- فحص بقايا التسميات القديمة: لا ذكر لـ`IDocumentService`/`DocumentService` في
  `backend/src` أو الاختبارات بعد تحديث تعليقات الاستخراج التاريخية إلى «خدمة المستندات
  القديمة» و`DocumentUpsertMapper`.
- فحص أن سلوكيات الخط لا تضيف أثرًا للاستعلامات: لا استعلام من السبعة يطبّق
  `ITransactionalRequest`/`IAuditedRequest`، و`AuditBehavior` لا يعمل إلا للثاني،
  ولا مدقق مسجَّل لأي منها — فالقراءة الصرفة محفوظة.
- مطابقة إجراءات المتحكمات: الفرق كله في سطور الحقن والنداء؛ المسارات والأدوار وحرّاس
  القدرات/النطاق والرسائل ورموز الحالة وأشكال الأجسام كما هي.
- فحص `generate:api` بعد التوليد (`git diff --exit-code`) واستعادة ضجيج `CRLF`.
- فحص `has-pending-model-changes` بهوية اتصال وهمية و`--no-build`.

## الملاحظات المكتشفة والإصلاح

| # | الملاحظة | الدليل | الإصلاح |
|---|---|---|---|
| 1 | **تعليقات باقية على صنف محذوف**: سبعة ملفات خدمة/مشتركة و4 ملفات اختبار تذكر `DocumentService` أو `DocumentService.Apply.cs` | `grep` في `backend/src` و`backend/tests` | حُدِّثت إلى `DocumentUpsertMapper`/«خدمة المستندات القديمة»/`DeleteDocumentCommand`/`CreateDocumentCommand` بحسب السياق |
| 2 | **حارس الطبقات معلَّق على نوع محذوف**: `typeof(DocumentService).Assembly` | `LayerBoundaryTests` | صار `typeof(GetDocumentQuery).Assembly` (نوع عام مستقر في `Application`) |
| 3 | **مصنع الاختبار** كان يبني `new DocumentService(...)` ويعيد `IDocumentService` | `DocumentServiceFactory` | أُعيد بناء قسم القراءة إلى سجل `DocumentReadHandlers` (7 معالجات) وأُزيل بناء الخدمة |
| 4 | **مزيّف متحكم قديم**: `StubDocumentService : IDocumentService` في اختبار إسقاط الوصول | `DocumentAccessProjectionTests` | صار `StubGetDocumentHandler : IQueryHandler<GetDocumentQuery, DocumentResponse?>` بنفس اسم الحقل/التوقّع `GetAsyncCalled` |
| 5 | **أعداد معاملات منشئ `DocumentsController`** تغيّرت (38 → 42) في ثلاثة اختبارات | `DocumentAccessProjectionTests`/`TransferBranchGuardTests`/`DocumentGenerationDisabledTests` | حُدِّثت وسائط `null!` بدقة؛ كل الاختبارات خضراء |
| 6 | **استيراد ميت** في `DelegationsController` (`Application.Services`) بعد إزالة الخدمة | `grep` | حُذف |
| 7 | **عضو منقول بلا مستدعٍ**: `GetAccessInfoAsync` كان بلا مستدعٍ قبل النقل وبعده؛ قناة F-01 هي `IDocumentAccessReader` | بحث شامل | نُقل كاستعلام صريح بدل إسقاطه صامتًا، ووُثّق، وأُضيف له اختبار توصيل (المفقود ← `null`) |
| 8 | **اختبارا التوصيل الجديدان فشلا محليًا** لأن `[Fact]` يبني قاعدة `PostgreSQL` بلا `DOCGEN_TEST_POSTGRES` | تشغيل Application | حُوِّلا إلى `[PostgresFact]` (تخطٍّ صريح محليًا وتشغيل في CI) |
| 9 | **ضجيج ترميز في ملف اختبار جديد** بعد كتابة PowerShell (BOM/CRLF) | `git diff` | أُعيد كتابة الملف `UTF-8` بلا `BOM` بسطر جديد واحد؛ `dotnet format --verify-no-changes` نظيف |
| 10 | احتمال بقاء نسخة ثانية من منطق النطاق/الرسائل في المعالجات | مقارنة الأجسام | لا — الحرّاس والرسائل كلها في المتحكمات والمعالجات لا تكرر فحص صلاحية |
| 11 | احتمال نسخ مساعد التدوير/الإسقاط | بحث عن تعريف كل مساعد | تعريف واحد لكل من `EffectiveFileIdentity`/`DocumentText.RotationDisplayName`/`ServerClock.CurrentYear` |
| 12 | احتمال بقاء partial فارغ أو حقل ميت | عدّ أعضاء كل ملف وحقل | **0 partial فارغ**، **0 حقل ميت** في المتحكمات والمصنع |

## إثبات التكافؤ بعد الإصلاح

- **الأجسام المنقولة:** **7/7** معالجًا، **19/21** جملة فعّالة، **0** فرق دلالي بعد
  الاستثناءات الموثّقة (خصائص `query`، و`var page`/`var perPage`، ولفّ شرط الحذف).
- **النصوص الحرفية (اتجاهان):** **2** نص / **0** مميز؛ `missing = 0` و`added = 0`.
- **المتحكمات:** نفس المسارات والأدوار وحرّاس النطاق والقدرات ورسائل الرفض ورموز الحالة
  وأشكال الأجسام؛ لم يتغيّر أي توقيع نقطة نهاية.
- **`IDocumentStatusService`:** لم يُمسّ (**2 → 2**) لأن عضويه كاتبان داخل معاملة
  مستدعٍ خارجي، وليسا سطح قراءة.
- **الاختبارات:** لا حذف ولا تعديل توقّع واحد؛ **137** موضع نداء عبر جسر
  `DocumentReadQueryExtensions`، وسجل `DocumentReadHandlers`، واختبارا توصيل جديدان.
- **الخدمة:** 0 ملف خدمة، 0 حقل، 0 partial، 0 مستدعٍ قديم.
- **`DI`:** 179 معالجًا مسجَّلًا مقابل 179 صنفًا (61 استعلامًا + 118 أمرًا) بلا تكرار.

## متبقٍ بعد هذه الدفعة (صريح)

- **اكتملت قراءات المستند:** لا يبقى أي سطح قراءة صرف على `IDocumentService` (حُذفت).
- **`IDocumentSearchService`** ما زال يحمل أسطح قراءة/تصدير — **خارج نطاق هذه المرحلة**
  ومُعلَن للمرحلة التالية.
- **`IDocumentStatusService`** يبقى كاتبي وقوعات/تجديد داخل معاملات المستدعين — ليس سطح
  قراءة ولا مرشحًا للنقل.
- **خارج نطاق المسح:** بقية أوامر `P4-01` وتفكيك `P4-02` الجاري — كما في الخريطة.
- **لا هجرة جديدة**؛ المعلّق يبقى **6**.
