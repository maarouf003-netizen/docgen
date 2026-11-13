# متابعة صحة الكود — الملاحظات الثلاث المؤجلة

> **الحالة:** ملاحظات **غير مانعة** رُصدت في المراجعة النقدية النهائية للـPR
> `#133` بعد دمجه في `master` بالـcommit `c58d10c`.
>
> **العلاقة:** `docs/CODE_HEALTH_REVIEW.md` يوثّق نطاق `C-01..C-11` المُغلق.
> هذا الملف هو **المرجع التنفيذي الوحيد** للملاحظات الثلاث التالية في محادثة جديدة.
>
> **تاريخ:** 2026-11-13
>
> **القاعدة:** كل بند يُنفَّذ في PR مستقل، بلا ميزات جديدة، وباختبارات انحدار
> وبوابات `CI` الست. لا تُخلط هذه البنود مع أي عمل وظيفي آخر.

---

## 0. ملخّص

| # | البند | النوع | الأولوية | الفرع المقترح |
|---|---|---|---|---|
| F-01 | ازدواج تحميل المستند لأداء فحص الوصول في مسارات المتدخلين | أداء/معمارية | `P2` | `perf/lightweight-document-access` |
| F-02 | حالة `DbContext` المتتبَّعة عند إعادة محاولة `TransactionRunner` | صحة تحت فشل عابر | `P2` | `fix/transaction-retry-state` |
| F-03 | غياب ميزانية زمنية رقمية لبحث بوابة المندوب | تحقق/أداء | `P3` | `test/portal-search-measurement` |

**الترتيب المقترح:** `F-02` ← `F-01` ← `F-03` (الصحة أولًا، ثم الأداء، ثم القياس).

**خارج النطاق:** أي تعديل على `C-01..C-11` المُغلقة، أو على صفات الملف، أو على
عقود الواجهة العامة، أو أي هجرة EF جديدة بلا مبرر.

---

## F-01 — ازدواج تحميل المستند في فحص الوصول

### الموقع الحالي

- `backend/src/DocGenerator.Api/Controllers/DocumentsController.cs`
  - `AddIntervention` (نحو السطر 471): `_documents.GetAsync(id, ct)` ثم `CanAccessAsync(doc)`.
  - `UpdateIntervention` (نحو 487) و`DeleteIntervention` (نحو 503): النمط نفسه.
  - `CanAccessAsync(DocumentResponse doc)` (نحو 131–148).
- `backend/src/DocGenerator.Application/Services/DocumentService.cs`
  - `GetAsync` (نحو 107–111) يستدعي `_documents.GetByIdAsync` بشجرة `Include` كاملة.
- `backend/src/DocGenerator.Application/Services/DocumentInterventionService.cs`
  - `AddAsync` (نحو 47–75): `_uow.ClearChanges()` ثم `GetByIdForUpdateAsync`
    (تحميل كامل جديد تحت القفل).
- `backend/src/DocGenerator.Infrastructure/Persistence/DocumentRepository.cs`
  - `GetByIdForUpdateAsync` (نحو 27–53): قفل صف ثم `GetByIdAsync` كامل.

### السلوك الحالي

طلب إضافة متدخل واحد يحمّل المستند الكامل **مرتين**:

1. مرة في المتحكم لفحص الوصول (`DocumentResponse` + كل الـ`Include`).
2. مرة في خدمة المتدخلين تحت القفل، بعد `ClearChanges`، لضمان قراءة طازجة
   للعدّ و`SearchText`.

### السبب الجذري

`CanAccessAsync` يحتاج خمسة حقول فقط (`Id`, `CreatedById`, `BranchId`,
`ExecutionCircuitId`, `SectionId`) + استعلام `HasForwardedAppealAsync` للرئيس،
لكنه يستقبل `DocumentResponse` الكامل لأنه مكتوب بذلك الشكل.

### الأثر

- استعلامان ثقيلان بدل استعلام ثقيل واحد + إسقاط خفيف.
- مدة أطول خارج القفل، وضغط أعلى على القاعدة في مسار منخفض التكرار لكنه حساس.
- لا أثر على الصحة أو الصلاحيات ما دام الفحصان متطابقين.

### الإصلاح القياسي

1. إضافة سجل إسقاط خفيف (في `DocGenerator.Application/Common` أو `DTOs`):
   ```csharp
   public sealed record DocumentAccessInfo(
       int Id,
       int CreatedById,
       int? BranchId,
       int? ExecutionCircuitId,
       int? SectionId);
   ```
2. إضافة قراءة مخصصة:
   - `IDocumentRepository.GetAccessInfoAsync(int id, CancellationToken)`:
     `AsNoTracking()` + `Select` للحقول الخمسة فقط.
   - `IDocumentService.GetAccessInfoAsync(...)` يمررها.
3. إضافة حمل زائد لفحص الوصول:
   ```csharp
   private Task<bool> CanAccessAsync(DocumentAccessInfo doc) { ... }
   private Task<bool> CanAccessAsync(DocumentResponse doc) =>
       CanAccessAsync(new DocumentAccessInfo(doc.Id, doc.CreatedById, doc.BranchId,
           doc.ExecutionCircuitId, doc.SectionId));
   ```
   **قاعدة:** منطق الصلاحيات يبقى في مكان واحد؛ لا نسخ ثانٍ.
4. تحويل نقاط `AddIntervention`/`UpdateIntervention`/`DeleteIntervention`
   فقط إلى `GetAccessInfoAsync`؛ بقية النقاط تبقى كما هي في هذا الـPR.
5. إبقاء `GetByIdForUpdateAsync` كما هو؛ هو التحميل الكامل الوحيد في مسار الإضافة.

### الاختبارات المطلوبة

- مصفوفة وصول كاملة (قبل/بعد):
  - `lawyer` لملفه/ملف غيره.
  - `head` بفرع مختلف، وبلا دائرة/شعبة، وباستئناف محال.
  - `subhead` بنفس/غير شعبة.
  - `manager`/`admin` كامل الوصول.
- اختبار يثبت أن مسار الإضافة لا يستدعي `DocumentRepository.GetByIdAsync`
  من المتحكم (Spy/Count) — أو اختبار تكامل يعدّ الاستعلامات.
- اختبارات `DocumentInterventionIntegrationTests` الحالية تبقى خضراء.

### معايير القبول

- [ ] مصفوفة الصلاحيات مطابقة حرفيًا للسلوك السابق.
- [ ] `404` للملف غير الموجود، و`403` لغير المصرح، و`200` للمصرح.
- [ ] تحميل شجرة `Document` كامل مرة واحدة كحد أقصى في مسار الإضافة.
- [ ] لا تغيير في عقد الـAPI أو الواجهة.
- [ ] بناء + اختبارات + بوابات `CI` الست خضراء.

### المخاطر والتراجع

- خطر منخفض؛ التغيير محصور في قراءة الإسقاط وفحص الوصول.
- التراجع: إعادة النقاط الثلاث إلى `GetAsync` مع إبقاء `DocumentAccessInfo` (غير مستخدم)
  أو حذفه في PR عكسي.

### النشر

لا هجرة. لا تغيير قاعدة بيانات.

---

## F-02 — حالة `DbContext` المتتبَّعة عند إعادة محاولة `TransactionRunner`

### الموقع الحالي

- `backend/src/DocGenerator.Infrastructure/Persistence/TransactionRunner.cs`
  - `RunInTransactionAsync` (نحو 71–87): `CreateExecutionStrategy().ExecuteAsync`
    + `BeginTransactionAsync` + `action` + `CommitAsync`، مع `Rollback` عند الفشل.
- `backend/src/DocGenerator.Application/Common/Interfaces/IUnitOfWork.cs`
  - `ClearChanges()` موجود، لكنه ليس جزءًا من عقد `ITransactionRunner`.
- `backend/src/DocGenerator.Application/Services/DocumentInterventionService.cs`
  - `AddAsync` يفرّغ المتتبَّع داخل المعاملة (تحصين موضعي لهذا المسار).
- بقية مستدعي `_tx.RunAsync` (خدمات الحالة/الإنابة/الاستئناف/التصدير...) لم تُراجع
  بعد بهذا الخصوص.

### السلوك الحالي

عند تفعيل `EnableRetryOnFailure` (PostgreSQL)، قد يعيد `ExecutionStrategy` تنفيذ
الـdelegate كاملًا بعد فشل عابر. نفس `DbContext` يُعاد استخدامه، فقد تبقى كيانات
`Added`/`Modified`/مجموعات محمّلة من المحاولة الفاشلة. النتيجة المحتملة:

- إعادة إدراج كيان أُضيف في محاولة سابقة.
- عدّ/مجموعة قديمة تؤدي رفضًا خاطئًا (`false 400`).
- `SearchText` مبني على حالة قديمة.

`DocumentInterventionService.AddAsync` يحصّن مساره بـ`ClearChanges` في بداية
كل محاولة، لكن **هذا ليس عقدًا عامًا** لبقية الخدمات.

### السبب الجذري

`TransactionRunner` يعيد المحاولة على مستوى الـdelegate، بينما بعض الخدمات
تحمل كيانات/تغييرات خارج المعاملة أو تلتقطها قبلها؛ لا يوجد عقد صريح
«نظّف وأعد التحميل» لكل محاولة.

### الأثر

فشل عابر واحد (انقطاع اتصال/`serialization_failure`) قد يسبب:
- كتابة مكررة أو أثرًا مزدوجًا.
- رفضًا خاطئًا لعملية صحيحة.
- اختلاف `SearchText`/الوقائع عن الحالة الفعلية.

### الإصلاح القياسي (يحتاج قرارًا معماريًا في PR مستقل)

خيارات مرتبة من الأقل تدخلًا:

1. **عقد صريح لإعادة المحاولة:** إضافة `ClearChanges` إلى بداية كل delegate
   (أو إلى `RunInTransactionAsync` قبل كل محاولة) مع إلزام كل مستدعٍ بإعادة
   تحميل كياناته داخل الـdelegate. يتطلب تدقيق كل `_tx.RunAsync` والتأكد من
   عدم وجود كيانات ملتقطة خارج المعاملة.
2. **سياق/وحدة عمل جديدة لكل محاولة:** استخدام `IDbContextFactory` أو نطاق DI
   جديد لكل محاولة؛ أنظف لكنه أوسع (تغيير توقيع/حقن).
3. **تعطيل إعادة المحاولة للمعاملات التفاعلية:** إن لم يكن النظام يضمن
   الـidempotency؛ يعيد الفشل العابر كخطأ للمستخدم بدلًا من إعادة قد تكرر الأثر.
4. **عدّاد/مفتاح idempotency** على مستوى العملية إن كان مناسبًا.

**التوصية:** ابدأ باختبار حقن فشل عابر يثبت المشكلة، ثم اختر الخيار 1 إن كان
كل مستدعٍ قادرًا على إعادة التحميل، وإلا الخيار 2. لا تُطبّق تغييرًا عامًا
بلا تدقيق كل المستدعين.

### الاختبارات المطلوبة

- اختبار حقن فشل عابر في المحاولة الأولى (مثلاً `NpgsqlException` عابر)
  ثم نجاح الثانية:
  - إضافة متدخل واحدة ⇒ صف واحد فقط، لا تكرار.
  - تعديل/حالة ⇒ أثر واحد، لا كتابة مزدوجة.
  - لا `false 400` بسبب عدّ قديم.
- اختبار SQLite للمسارات المكافئة إن أمكن، واختبار `PostgreSQL` مُقيَّد
  بـ`DOCGEN_TEST_POSTGRES` للسلوك الحقيقي.
- تدقيق ثابت (`grep`) لكل `_tx.RunAsync` مع تعليق يوضح مصدر الكيانات.

### معايير القبول

- [ ] عقد إعادة المحاولة موثَّق في `ITransactionRunner`/`TransactionRunner`.
- [ ] كل مستدعٍ مُدقَّق ومُختبَر.
- [ ] لا تكرار/فقدان أثر تحت فشل عابر محقون.
- [ ] لا تراجع في `document intervention` أو الحالة/الإنابة/الاستئناف.
- [ ] بناء + اختبارات + بوابات `CI` الست خضراء.

### المخاطر والتراجع

- خطر متوسط–مرتفع (تغيير معاملاتي عام). يُنفَّذ في PR منفصل مع مراجعة كود مستقلة.
- لا هجرة قاعدة بيانات إن لم يُضف عدّاد/idempotency جديد.

---

## F-03 — ميزانية زمنية رقمية لبحث بوابة المندوب

### الموقع الحالي

- `backend/src/DocGenerator.Infrastructure/Persistence/DocumentPartySearch.cs`
  (الباني الموحد بعد `#133`).
- `backend/src/DocGenerator.Infrastructure/Persistence/PortalRepository.cs`
  (`ApplyScopedPersonNameSearch` + `ScopedQuery`).
- الاختبارات الحالية:
  - `backend/tests/DocGenerator.Application.Tests/PortalScopingTests.cs`
    (صحة/عزل).
  - `backend/tests/DocGenerator.Application.Tests/SearchMeasurementTests.cs`
    (10k على بحث المحامين، وليس البوابة).
  - `backend/tests/DocGenerator.Application.Tests/PortalStatsMeasurementTests.cs`
    (إحصاءات، وليس بحثًا).
  - `backend/tests/DocGenerator.Api.Tests/PortalSearchPostgresTests.cs`
    (ترجمة/صحة على `Npgsql`، وليس زمنًا).

### السلوك الحالي

لا يوجد قياس زمني رقمي لمسار `PortalService.ListFilesAsync`/`SearchScopedAsync`
مع بحث اسمي يتجاوز `SearchText`، ولا تحقق من خطة الاستعلام على نطاق مندوب كبير.

### الأثر

- لا نعرف إن كانت فروع `EXISTS` الجديدة تحافظ على زمن مقبول مع 10k مستند.
- لا يوجد حارس انحدار أداء عند إضافة فروع بحث جديدة مستقبلًا.

### الإصلاح القياسي

1. إضافة `PortalSearchMeasurementTests` (أو توسيع `PortalStatsMeasurementTests`):
   - Seed نحو 5k–10k مستند مرتبط بـ`PublicEntity` نهائي لمندوب واحد.
   - مستند هدف واحد عليه كفيل باسم فريد، و`SearchText` فارغ/لا يحويه.
   - `ListFilesAsync`/`SearchScopedAsync` بالاسم ⇒ نتيجة واحدة صحيحة.
   - `Stopwatch` + `ITestOutputHelper` لطباعة الزمن.
   - تحقق خطة `EXPLAIN QUERY PLAN` (SQLite) من `EXISTS` واستخدام فهرس
     `IX_Documents...` أو `IX_Guarantors_DocumentId`، وليس `SCAN Documents`.
2. ميزانية رقمية واقعية غير هشّة، مثل:
   - `Assert.True(elapsed < 5_000)` على CI (سقف أمان، لا معيار دقيق)، أو
   - اختبار قياس منفصل غير حاجب مع تسجيل النتيجة، أو
   - `BenchmarkDotNet` خارج حزمة الوحدة إن أردت دقة.
3. نسخة `PostgreSQL` اختيارية مُقيَّدة بـ`DOCGEN_TEST_POSTGRES` تقيس
   `PortalRepository.SearchScopedAsync` على نفس البيانات (يمكن البدء بالصحة/
   `EXPLAIN` فقط لتجنب هشاشة الزمن على CI).
4. توثيق النتيجة المتوقعة في `docs/CODE_HEALTH_REVIEW.md` أو ملحق مستقل.

### الاختبارات المطلوبة

- [ ] بحث يجد الكفيل خارج `SearchText` على 5k–10k مستند.
- [ ] لا تسرب خارج نطاق المندوب.
- [ ] خطة الاستعلام تُظهر `EXISTS`/فهرسًا مناسبًا (SQLite).
- [ ] زمن مسجّل ضمن سقف الأمان المتفق عليه.
- [ ] اختبار `PostgreSQL` للصحة/الترجمة إن أضيف.

### معايير القبول

- [ ] قياس موثَّق وقابل للتكرار.
- [ ] لا تغيير سلوكي في البحث أو النطاق.
- [ ] لا هجرة قاعدة بيانات (إلا فهرس اختياري بمبرر قياس).
- [ ] بناء + اختبارات + بوابات `CI` الست خضراء.

### المخاطر والتراجع

- خطر منخفض؛ اختبار/قياس أساسًا.
- تجنّب حدود زمنية ضيقة تسبب هشاشة على مراوح CI مختلفة.

> **حالة F-03 — ✅ منفّذة (2026-11-13):** أُضيف `PortalSearchMeasurementTests`
> (10,002 مستند، منها ملف هدف بكفيل فريد غير موجود في `SearchText` وملف خارج
> النطاق بنفس الاسم): يثبت النتيجة الواحدة والعزل عبر `SearchScopedAsync` و
> `ListFilesAsync` معًا، ويسجّل الزمن وخطة `EXPLAIN QUERY PLAN` من **نص الاستعلام
> الفعلي الملتقط** عبر `DbCommandInterceptor` (لا استعلام يدوي مكافئ).
> الخطة تُظهر `SEARCH g USING INDEX IX_Guarantors_DocumentId`؛ المسح الخارجي
> لـ`Documents` متوقع لأن البحث `OR` عبر عدة جداول أطراف، فلا يصحّ اشتراط
> «بلا `SCAN Documents`» كما اقترحت المسودة. **لا حدّ زمني حاجب** التزامًا
> بنهج `M6`/`M24` (الزمن الجداري هشّ)؛ القياس المرجعي في بيئة التطوير
> (Debug + SQLite ذاكرة) ≈ 26.8 ثانية للاستعلام و≈ 26.2 ثانية للمسار الكامل —
> رقم يستحق متابعة أداء منفصلة على PostgreSQL/Release، ولا يُحوَّل إلى فشل CI.
> لا تغيير سلوكي ولا هجرة. اختبار PostgreSQL الزمني يبقى اختياريًا خارج هذا الـPR.

---

## Definition of Done المشترك

- [ ] اختبار فاشل قبل الإصلاح حيث ينطبق (F-01/F-02).
- [ ] اختبارات وحدة + تكامل، واختبار `PostgreSQL` للسلوك المعتمد على المزوّد.
- [ ] `dotnet build` بلا تحذيرات/أخطاء.
- [ ] `DocGenerator.Application.Tests` + `DocGenerator.Api.Tests`.
- [ ] بوابات `CI` الست: `backend` + `frontend` + `guards` + `audit`
      + `backend-postgres` + `migration-guard`.
- [ ] لا هجرات جديدة؛ وفحصا `has-pending-model-changes` نظيفان إن لمس العمل النموذج.
- [ ] تحديث هذا الملف/`CODE_HEALTH_REVIEW.md` بحالة التنفيذ.
- [ ] مراجعة كود مستقلة، وبلا خلط مع أي ميزة.

---

**نهاية الملف — ثلاث ملاحظات غير مانعة، كل واحدة في PR مستقل.**
