# تدقيق صحة الكود — الثغرات القائمة وخطة الإصلاح

> **حالة الوثيقة:** خطة إصلاح للثغرات **القائمة فقط** — لا تشمل أي ميزة جديدة.
> **تاريخ:** 2026-11-13
> **حالة التنفيذ (2026-11-13):** C-01..C-08 وC-11 منفّذة في فرع `fix/code-health` مع اختبارات. C-09 وثائقي (watchlist). C-10 خارج النطاق: لا يوجد مسار حالي يعرض شخصًا اعتباريًا بنوع `entity`؛ `ExecutedDebtors` يستخدم `legalPartyRows` أصلًا.
> **القاعدة:** لا حلول اسعافية؛ كل بند: الموقع، السبب الجذري، الأثر، الإصلاح القياسي، الاختبارات، النشر، الأولوية.
> **خارج النطاق:** أي إضافة/توسعة وظيفية جديدة (بما فيها أي صفة ملف جديدة أو جداول أطراف جديدة).

---

## 0. ملخّص تنفيذي

الكود الحالي فيه ثغرات بيانات صامتة لا تظهر في الاختبارات السريعة، أهمها:

1. `ApplyRequest` يخلق `ApplicantPublicEntity` وهميًا من `Applicant` لأي صفة غير `executed/deposit`.
2. `StruckOffDate` يُمحى عند تعديل ملف مشطوب غير `executed/deposit`.
3. `ExecutedHeirs` لا يُفرَّغ قبل إعادة بناء ورثة المنفذ عليهم.
4. حد المتدخلين (50) غير محمي من سباق الإضافة المتزامنة.
5. بحث بوابة المندوب يعتمد `SearchText` وحده، وهو يُقصّ عند 1000.
6. الإحصاءات تجمع `Amount*` و`InclusionAmount*` معًا في اللوحة/الفرع/البوابة.
7. لقطات الاستئناف تُحدَّث لكل الاستئنافات، بينما القرار المعتمد: المنظور فقط.
8. توليد قيد `CHECK` يعتمد ترتيب `HashSet` غير الحتمي.
9. SQLite عليه هجرات معلّقة و`G2`، وأي هجرة جديدة لا تُطبَّق منفردة.
10. `PartyDetailsModal` لا يعرض الشخص الاعتباري كاملًا.

**الخلاصة:** هذه إصلاحات مستقلة عن أي ميزة، ويمكن تنفيذها في PRs صغيرة متتابعة مع اختبارات انحدار.

---

## 1. منهجية

لكل بند:

1. الموقع الدقيق (`file:line`).
2. السلوك الحالي.
3. السبب الجذري.
4. الأثر.
5. الإصلاح القياسي.
6. الاختبارات.
7. الهجرة/النشر إن لزم.
8. الأولوية:
   - `P0`: بيانات/أموال/دليل قانوني/نشر — يمنع الإصدار.
   - `P1`: سلوك وظيفي خاطئ.
   - `P2`: جودة/وقاية.

---

## 2. الثغرات القائمة

### C-01 — `ApplyRequest` يخلق `ApplicantPublicEntity` وهميًا — P0

**الموقع:** `backend/src/DocGenerator.Application/Services/DocumentService.cs:776-786`.

**السلوك الحالي:**

```csharp
doc.ApplicantPublicEntities.Clear();
if (!GeneralEntitySideCatalog.IsExecutedLike(doc.GeneralEntitySide))
{
    var applicantList = r.ApplicantPublicEntities;
    if ((applicantList is null || applicantList.Count == 0) && !string.IsNullOrWhiteSpace(r.Applicant))
        applicantList = new List<ApplicantPublicEntityDto> { new(null, r.Applicant, null) };

    foreach (var a in DocumentText.NormalizeApplicantPublicEntities(applicantList))
        doc.ApplicantPublicEntities.Add(a);
}
```

**السبب الجذري:** الفرع مكتوب كـ«كل ما ليس منفذًا عليه»، وليس «طالب التنفيذ فقط». `Applicant` حقل توافق قديم.

**الأثر:** أي طلب لصفة غير `applicant` (مستقبلًا أو صفة قائمة بسلوك مختلف) يحمل `Applicant` نصيًا يُنشئ صف جهة عامة بلا `RegistryId`، فيتلوث الجدول والبحث والبوابة.

**الإصلاح القياسي:**

```csharp
doc.ApplicantPublicEntities.Clear();

if (doc.GeneralEntitySide == GeneralEntitySideCatalog.Applicant)
{
    var applicantList = r.ApplicantPublicEntities;
    if ((applicantList is null || applicantList.Count == 0)
        && !string.IsNullOrWhiteSpace(r.Applicant))
    {
        applicantList = new List<ApplicantPublicEntityDto> { new(null, r.Applicant, null) };
    }

    foreach (var a in DocumentText.NormalizeApplicantPublicEntities(applicantList))
        doc.ApplicantPublicEntities.Add(a);
}
```

- لا تُستخدم `!IsExecutedLike` هنا.
- يبقى توافق `applicant` القديم كما هو.

**الاختبارات:**
- إنشاء `applicant` مع `Applicant` نصي ⇒ صف واحد (السلوك القديم).
- صفة أخرى مع `Applicant` نصي ⇒ `ApplicantPublicEntities` فارغة.
- تعديل لا يضيف صفًا وهميًا.

**النشر:** لا هجرة.

---

### C-02 — `StruckOffDate` يُمحى عند تعديل ملف مشطوب — P0

**الموقع:** `DocumentService.cs:741-759`.

**السلوك الحالي:** الفرع `else` لغير `executed/deposit` يضع `doc.StruckOffDate = null`.

**السبب الجذري:** `StruckOffDate` مشترك بين «مشطوب» في `ExecStatus` وحقول عائلة المنفذ عليه، لكنه يُصفَّر مع الحقول غير المستخدمة.

**الأثر:**
- تعديل ملف «طالبة تنفيذ» مشطوب يمحو تاريخ الشطب.
- صفحة المشطوبة/الترتيب/السجل التاريخي تتأثر.
- الأثر قائم لأي صفة تستخدم `ExecStatus` لاحقًا.

**الإصلاح القياسي:**

- **لا تضع `StruckOffDate = null` في `ApplyRequest` إطلاقًا** للصفات التي تستخدم `ExecStatus`.
- إدارة تاريخ الشطب تبقى في `DocumentStatusService` (هي التي تعرف متى يُضبط/يُبقى).
- إن أردت تصفيرًا دفاعيًا، اشترطه فقط عندما لا يوجد شطب تاريخي:
  ```csharp
  if (doc.ExecStatus != ExecutionStatusCatalog.StateStruckOff && doc.StruckOffDate is null)
      doc.StruckOffDate = null; // لا أثر عمليًا؛ الأفضل حذف السطر
  ```
  لكن الأفضل حذف السطر كليًا.

**الاختبارات:**
- ملف مشطوب ⇒ تعديل ملاحظة ⇒ `StruckOffDate` يبقى.
- ملف مشطوب ⇒ `RestoreStruckOff` ⇒ تعديل ⇒ يبقى (السلوك المعتمد: يبقى للعرض).
- ملف `executed/deposit` لا يتأثر.

**النشر:** لا هجرة.

---

### C-03 — `ExecutedHeirs` لا يُفرَّغ قبل إعادة البناء — P1

**الموقع:** `DocumentService.cs:762-794`.

**السلوك الحالي:** `ExecutionApplicants.Clear()` و`ExecutedNaturalPersons.Clear()` ثم إضافة الورثة الجدد إلى `doc.ExecutedHeirs` دون `Clear()`.

**السبب الجذري:** الاعتماد الضمني على Cascade/EF لحذف الأيتام.

**الأثر:** ورثة قدامى/مكررون، تلوث `DocumentChangeTracker` وسجل التعديلات.

**الإصلاح القياسي:**

```csharp
doc.ExecutedHeirs.Clear();
doc.ExecutionApplicants.Clear();
doc.ExecutedPublicEntities.Clear();
doc.ExecutedNaturalPersons.Clear();
```

- لكل الصفات، وليس لصفة معينة.
- التفريغ قبل إضافة الورثة الجدد.

**الاختبارات:**
- ملف فيه طالب A ووريث H1؛ تعديله ليحذف A ويضيف B ⇒ لا H1.
- إضافة ورثة جدد ⇒ لا تكرار.
- سجل التعديلات يظهر تغييرًا واحدًا مفهومًا.

**النشر:** لا هجرة.

---

### C-04 — حد المتدخلين 50 غير محمي من السباق — P1

**الموقع:** `backend/src/DocGenerator.Application/Services/DocumentInterventionService.cs:47-63`.

**السلوك الحالي:**

```csharp
var doc = await _documents.GetByIdAsync(documentId, ct);
EnsureSupportedSide(doc);
if (doc.Interventions.Count >= InterventionLimitsCatalog.MaxPerDocument)
    throw new ArgumentException(...);
...
return await _tx.RunAsync(async token => { doc.Interventions.Add(...); ... });
```

**السبب الجذري:** العدّ خارج المعاملة، والمعاملة الافتراضية `ReadCommitted`.

**الأثر:** طلبان متزامنان يقرآن 49، كلاهما يضيف، فيصبح 51 — تجاوز الحد.

**الإصلاح القياسي:**
- إعادة العدّ **داخل** `_tx.RunAsync` بعد قراءة طازجة، مع قفل صف المستند:
  - PostgreSQL: `SELECT ... FOR UPDATE` على `Documents`.
  - SQLite: معاملة كتابة (BEGIN IMMEDIATE) عبر `TransactionRunner` أو نمط معاملة مخصص.
- أو عدّاد ذري على `Document` (`InterventionCount`) يُحدَّث بـ`UPDATE ... WHERE count < 50`.
- لا يُقبل «عدّ داخل المعاملة فقط» على PostgreSQL Read Committed بلا قفل/عدّاد.
- **تفريغ المتتبَّع قبل القراءة تحت القفل** (`IUnitOfWork.ClearChanges`): المستند قد يكون محمّلًا في الطلب نفسه (فحص الوصول) أو في محاولة سابقة أعادتها `ExecutionStrategy`؛ إعادة استخدام كيان متتبَّع تُبقي العدّ/`SearchText` على حالة قديمة رغم قفل الصف.

**الاختبارات (المنفذ):**
- `DocumentInterventionLimitPostgresTests.AddAsync_AtLimit_BlocksOnRowLock_AndRejectsFreshCount` (في `Api.Tests`، مُقيَّد بـ`DOCGEN_TEST_POSTGRES`): يمسك معاملة أولى قفل صف الملف، يتحقق من انتظار الطلب الثاني فعليًا عبر `pg_blocking_pids`، ثم يُدخل المتدخل الخمسين ويحرر القفل ⇒ الطلب الثاني يرى 50 ويرفض، والعدد النهائي 50 لا 51.
- `DocumentInterventionLimitStaleTrackingTests.Add_AfterConcurrentDelete_ReloadsFreshCount`: يثبت أن حذف متدخل في طلب آخر ثم إضافة متدخل جديد تنجح (49 ⇒ 50) رغم أن مجموعة المستند المتتبَّعة كانت 50 — يفشل قبل `ClearChanges`.
- `Add_AboveDocumentLimit_Throws`: الحد العادي 50.

**النشر:** لا هجرة إن استُخدم القفل؛ هجرة إن استُخدم عدّاد.

---

### C-05 — بحث البوابة يعتمد `SearchText` فقط — P1

**الموقع:** `backend/src/DocGenerator.Infrastructure/Persistence/PortalRepository.cs:202-205`.

**السلوك الحالي:**

```csharp
q = q.Where(d => d.SearchText != null && d.SearchText.Contains(ArabicNameNormalizer.Normalize(term)));
```

**السبب الجذري:** `SearchText` يُقصّ عند 1000، ويضم أسماء كثيرة.

**الأثر:** مندوب الجهة لا يجد ملفًا باسم كفيل/وريث/متدخل/جهة متأخرة في النص.

**الإصلاح القياسي:**
- إضافة فروع بحث مباشرة في `ScopedQuery` للأطراف القائمة:
  - `BorrowerName`/`BorrowerFamily`.
  - `Guarantors` و`Heirs`.
  - `ExecutionApplicants` و`ExecutedPublicEntities` و`ExecutedNaturalPersons` و`ExecutedHeirs`.
  - `Interventions` و`Interventions.Heirs`.
  - `ApplicantPublicEntities`/`ExecutedPublicEntities`.
- استخدام `EXISTS`/`Any` وليس تحميلًا في الذاكرة.
- قياس الأداء على بيانات كثيرة؛ إن لزم فهارس/`pg_trgm`.

**الاختبارات:**
- اسم في آخر `SearchText` أو خارج القص ⇒ البحث يجده.
- لا تسرب خارج النطاق.
- زمن استجابة مقبول (ميزانية رقمية).

**النشر:** لا هجرة (إلا فهرس اختياري).

---

### C-06 — ازدواج/نقص مبالغ الإحصاءات — P0 مالي

**المواقع:**
- `StatisticsRepository.cs:70-71` (`AmountNumeric + InclusionAmountNumeric`).
- `StatisticsRepository.cs:127-128` (الفرع).
- `PortalService.cs:507-518` (`DebtAmountPairs` يجمع المجموعتين).
- `StatisticsRepository.AccumulateContract` يختار واحدة حسب `ContractTypeSelector`.

**السبب الجذري:** اللوحة/الفرع/البوابة لا تطبق قاعدة «مجموعة واحدة حسب نوع السند» التي يطبقها `AccumulateContract`.

**الأثر:** ملف يحمل القيمتين (قديم/عميل مباشر) يتضاعف مبلغه في الإحصاءات المالية.

**الإصلاح القياسي:**
- `StatisticsRepository`:
  - `GetDashboardStatsAsync`: استخدم `IsBanking(selector) ? Amount* : Inclusion*`.
  - `GetBranchesSummaryAsync`: نفس القاعدة.
- `PortalAmountRow`: أضف `ContractTypeSelector`.
- `PortalService.DebtAmountPairs`: اختر مجموعة واحدة حسب `ContractTypeSelector`.
- لا تجمع المجموعتين لأي ملف.
- لا حاجة لهجرة بيانات (قاعدة البيانات تجريبية)، لكن أضف اختبارات للقيمتين.

**الاختبارات:**
- مصرفي: `Amount*` فقط.
- عادي: `Inclusion*` فقط.
- ملف بالقيمتين: لا يتضاعف؛ يختار حسب المحدد.
- لوحة/فرع/بوابة متطابقة.

**النشر:** لا هجرة.

---

### C-07 — لقطات الاستئناف تُحدَّث لكل الاستئنافات — P1

**الموقع:** `PublicEntityService.cs:505-564`.

**السلوك الحالي:** يحدّث أسماء كل الاستئنافات (منظورة/محسومة/مشطوبة).

**القرار المعتمد:** المنظور فقط يتغير؛ المحسوم/المشطوب يبقى تاريخيًا.

**الأثر:** تعديل لقطة محسومة يمحو دليلًا تاريخيًا.

**الإصلاح القياسي:**
- في حلقة `SyncAppealsAfterEntityChangeAsync`:
  ```csharp
  if (appeal.Status != AppealStatusCatalog.Pending) continue;
  ```
- للمنظور: تحديث الأسماء فقط (لا حاجة لإعادة توجيه IDs في نطاق هذه الثغرة؛ إعادة التوجيه تخص عمليات الدمج/الطيّ المستقبلية).
- للمحسوم/المشطوب: لا لمس.
- توثيق القاعدة في تعليق الخدمة.

**الاختبارات:**
- إعادة تسمية جهة: المنظور يتحدث، المحسوم لا.
- المحسوم يبقى كما هو بعد إعادة التسمية.
- اللقطة التالفة تُتخطى مع تدقيق (السلوك الحالي).

**النشر:** لا هجرة.

---

### C-08 — ترتيب قيد `CHECK` غير حتمي — P1

**الموقع:** `GeneralEntitySideCatalog.ValidSides` (`HashSet`), `CheckConstraintLists.InList` (لا فرز).

**السبب:** `HashSet<string>` يعتمد على تجزئة النص، وقد يختلف ترتيبه بين العمليات.

**الأثر:** توليد هجرة قد يرى نص القيد مختلفًا عن اللقطة، فيُنشئ `Drop/Add` غير متوقع.

**الإصلاح القياسي:**
- **لا تغيّر `InList` عالميًا** (سيسبب Churn لكل القيود).
- في `DocumentConfiguration` استخدم قائمة مرتبة صريحة لـ`GeneralEntitySide`:
  ```csharp
  table.HasCheckConstraint(
      "CK_Documents_GeneralEntitySide",
      "\"GeneralEntitySide\" IN ('applicant', 'executed', 'deposit')");
  ```
  أو أضف `InListOrdered` تستخدمها هذه القيود فقط.
- أضف اختبارًا يثبت ثبات النص.

**الاختبارات (المنفذ):** `PB002StatusCheckTests.GeneralEntitySide_Constraint_UsesDeterministicOrder` يثبت نص القيد من `IDesignTimeModel` وترتيب `OrderedSides`؛ إضافة إلى `migration-guard`.

**النشر:** لا هجرة إن حافظت على الترتيب الحالي.

---

### C-09 — SQLite: هجرات معلّقة و`G2` — P1 نشر

**الموقع:** `RUN_GUIDE.md` §9.

**السلوك:** SQLite عليه سلسلة معلّقة، و`Contract` محظور قبل G2. أي `database update` جديد يسحب السلسلة كاملة.

**الأثر:** لا يمكن تطبيق أي هجرة SQLite منفردة، و`AutoMigrate=false` يرفض الإقلاع إن لم تُطبَّق.

**الإصلاح القياسي:**
- عدم تطبيق أي هجرة SQLite قبل G2.
- إضافة أي هجرة جديدة إلى watchlist فور إنشائها.
- سكربت فرقي/نسخة مستعادة + `Down/Up`.
- PostgreSQL هو مسار النشر الحالي.

**الاختبارات:** `database update` على نسخة SQLite اختبارية + `migration-guard`.

**النشر:** وثائقي.

---

### C-10 — `PartyDetailsModal` لا يعرض الشخص الاعتباري كاملًا — P2

**الموقع:** `frontend/src/components/view/PartyDetailsModal.tsx:44-49`, `viewTypes.ts:17-20`.

**السبب:** نوع `entity` مخصص للجهة العامة (اسم/فرع/محافظة)، ويُستخدم للشخص الاعتباري.

**الأثر:** تضيع `registrationNumber`/`representedBy`/`addressType`/`address`.

**الإصلاح القياسي:**
- للشخص الاعتباري: `kind: 'person'` + `legalPartyRows` (النمط القائم).
- للجهة العامة: `kind: 'entity'`.
- `InterventionDetailsModal` يعرضها بالفعل؛ نوحّد.

**الاختبارات:** فتح اعتباري في `PartiesCard` يعرض كل الحقول.

**النشر:** لا.

---

### C-11 — `Applicant` النصي قد يتجاوز 200 — P2

**الموقع:** `DocumentConfiguration.cs:88`, `ApplicantTextBuilder`.

**السبب:** `Applicant` يُبنى من كل الجهات الطالبة، وقد يتجاوز 200 مع تعدد الجهات.

**الأثر:** PostgreSQL يرفض الحفظ (500) بينما SQLite يقبل.

**الإصلاح القياسي:**
- قصّ `doc.Applicant` إلى 200 باستخدام `DocumentSearchTextBuilder.Truncate` أو دالة مخصصة، مع عدم قصّ الاسم الأول.
- أو رفض الطلب 400 إذا تجاوز الناتج 200، برسالة واضحة.
- الأفضل: `Applicant` نص عرض فقط؛ إذا تجاوز، اختصره مع «…» وسجّل الأسماء الكاملة في `ApplicantPublicEntities`.

**الاختبارات (المنفذ):** `ApplicantAndAssignmentTests.Create_WithManyLongApplicantEntities_TruncatesApplicantTextToColumnLimit` — 3 جهات بأسماء 100 حرف ⇒ النص ≤ 200 وينتهي بـ«…».

**النشر:** لا.

---

## 3. ترتيب PRs المستقل (لا يعتمد على أي ميزة)

| PR | الفرع | الثغرة | الاعتماد |
|---|---|---|---|
| PR-01 | `fix/applicant-public-entities-fallback` | C-01 | لا |
| PR-02 | `fix/struckoff-date-preservation` | C-02 | لا |
| PR-03 | `fix/executed-heirs-update` | C-03 | لا |
| PR-04 | `fix/intervention-limit-race` | C-04 | لا |
| PR-05 | `fix/portal-search-completeness` | C-05 | لا |
| PR-06 | `fix/statistics-amount-source` | C-06 | لا |
| PR-07 | `fix/appeal-snapshot-pending-only` | C-07 | لا |
| PR-08 | `fix/check-constraint-determinism` | C-08 | لا |
| PR-09 | `docs/sqlite-watchlist` | C-09 | لا |
| PR-10 | `fix/party-details-legal` | C-10 | لا |
| PR-11 | `fix/applicant-length-guard` | C-11 | لا |

- كل PR مستقل، من `master`، مع اختباراته.
- لا PR ميزة.
- لا تغيير في الصفات القائمة إلا المقصود.
- **المنفذ في `fix/code-health`:** C-01..C-08 وC-11 (مع C-04 قفل/معاملة). المتبقي: C-09 وثائقي، وC-10 خارج النطاق.

---

## 4. DoD لكل PR

- [ ] اختبار فاشل قبل الإصلاح (سلوكيًا).
- [ ] اختبارات وحدة + تكامل.
- [ ] `dotnet build` / `dotnet test`.
- [ ] `backend-postgres` / `migration-guard` / `guards` / `audit`.
- [ ] للواجهة: `npx oxlint src` / `npx tsc -b` / `npx vitest run` / `npm run build`.
- [ ] لا انحدار في `applicant/executed/deposit`.
- [ ] تحديث `RUN_GUIDE`/watchlist إن لزم.
- [ ] خطة تراجع/نسخة احتياطية إن لزم.
- [ ] مراجعة كود مستقلة.

---

## 5. بوابات القبول

- [x] C-01: لا صف وهمي (تحصين دفاعي منفذ).
- [x] C-02: `StruckOffDate` يبقى بعد التعديل/الإعادة.
- [x] C-03: لا ورثة قدامى.
- [x] C-04: لا تجاوز 50 تحت التزامن (قفل صف PostgreSQL + معاملة SQLite).
- [x] C-05: بحث البوابة يجد اسمًا خارج `SearchText`.
- [x] C-06: لا تضاعف مبالغ في اللوحة/الفرع/البوابة.
- [x] C-07: المحسوم تاريخي، المنظور يتحدث.
- [x] C-08: نص القيد ثابت (`OrderedSides`).
- [ ] C-09: watchlist محدث (وثائقي — لا هجرة جديدة في هذا النطاق).
- [-] C-10: خارج النطاق (لا مسار حالي يعرض اعتباريًا بنوع `entity`).
- [x] C-11: لا 500 بسبب طول `Applicant`.

---

## 6. قرارات مثبّتة

- لا ميزات جديدة في هذا النطاق.
- لا بيانات حقيقية في القاعدة؛ لا backfill مطلوب، لكن لا نكسر الصفوف التجريبية.
- `StruckOffDate` يبقى تاريخيًا بعد الإعادة.
- `Applicant` نص عرض/توافق، لا مصدر أطراف.
- الإحصاء يقرأ مجموعة واحدة حسب نوع السند.
- الاستئناف: المنظور فقط يتحدث.

---

**نهاية الوثيقة — نطاق الثغرات القائمة فقط.**
**لا يبدأ أي إصلاح قبل اعتماد ترتيب PRs أعلاه.**
