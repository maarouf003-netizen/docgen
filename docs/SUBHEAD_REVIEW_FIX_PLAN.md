# خطة إصلاح مراجعة رئيس الشعبة — فرع التنفيذ `refactor/subhead-review-fixes`

> الغرض: تنفيذ بنود المراجعة `F1`–`F8` وفق قرارات المالك الأربعة المعتمدة، وبأقصى المعايير
> البرمجية القياسية الصارمة (`AGENTS.md` — تُطبق حرفيًا على كل تغيير).
>
> المرجع الأصلي: `docs/SUBHEAD_PLAN.md` — ودليل التشغيل: `RUN_GUIDE.md`.
> الفرع الحي `master` محمي — العمل هنا ثم `PR` إلى `feature/subhead`؛ لا دفع مباشر أبدًا.

---

## 1. قرارات المالك المعتمدة (نهائية — لا يُعاد فتحها بلا موافقة صريحة)

| البند | القرار |
|-------|--------|
| F3 | **(أ)** توسيع إشعارات الجهات العامة لتشمل رؤساء الشعب — بالنطاق المحافظي/الفرعي **حسب الاستعلام القائم** (كل رؤساء شعب المحافظة للبثّ المحافظي، وكل رؤساء شعب الفرع للبثّ الفرعي) |
| F6 | **يُنفَّذ الآن** ضمن هذه الخطة: النطاق الدائري لسجل التدقيق (`ownerSectionId` خلفيًا) ثم فتح المسار للشعبة |
| F7 | **(أ)** الإبقاء على سقوط الإشعار للقسم + توثيقه قرار توفّر صريحًا |
| F4/F5/F8 | اعتماد التصحيحات النقدية كما وردت في المراجعة العليا (مفصلة أدناه) |

---

## 2. الثغرات المؤكدة مرتبة التنفيذ (بعد المراجعة العليا — المراجع الدقيقة مصححة)

| # | الخطورة | البند | الشاهد المؤكد | الحكم |
|---|---------|-------|---------------|-------|
| 1 | 🔴 حرجة | القوائم الأربع (`GetDeleted`/`GetStruckOff`/`GetExecuted`/`GetReferredToStart`) تحسب `ownerSectionId` ولا تمرره — الشعبة ترى نطاق القسم | `DocumentsController.cs:227/230`، `:246/249`، `:262/265`، `:278/281`؛ التواقيع تقبل الوسيط أصلًا (`DocumentService.cs:28-44`، `DocumentService.Search.cs:14-71`) والمستودع يطبّق `ApplyOwnerScope` (`DocumentRepository.cs:511,902,981,1022`) | إصلاح 4 أسطر في المتحكم فقط — بلا تغيير عقود |
| 2 | 🟠 عالية | `InScope` يتجاهل المحال للقسم في **فرع الرئيس فقط** — فرع الشعبة صحيح أصلًا ومطابق للبحث | `AppealRepository.cs:131-135` مقابل البحث `:61-65`؛ المناداة `DocumentAppealService.cs:429,450` | نسخ محمول البحث حرفيًا إلى فرع الرئيس؛ عدم لمس فرع الشعبة |
| 3 | 🟡 متوسطة | إحصاءات الدوائر تهمل ملفات بلا دائرة — والعودة المبكرة `:983` تُفقد الصف الاصطناعي | `ExecutionCircuitService.cs:976-1003`؛ الوسم الصحيح `ExcelExportService.cs:98` (لا `:214` = تصدير الملفات `:215`)؛ `CircuitStatsDto` (`ExecutionCircuitDtos.cs:71`) بحقلي `int` غير قابلين للـnull | صف اصطناعي `Id=0` «بلا دائرة» بشروط §F4 أدناه |
| 4 | 🟡 متوسطة | `TransferAllAsync` يقرأ خارج `_tx` بلا حارس نسخة (المفرد مغطى) — **وليست «500»**: `GlobalExceptionHandler.cs:41` يترجم `DbUpdateConcurrencyException → 409` برسالة عربية | `DocumentAppealService.cs:408-443` (قراءة `:424` خارج `_tx` في `:427`) مقابل نمط `AssignAsync (:335-357)`؛ تصحيح `F5.2`: `TransferCircuitAsync` يقرأ داخل `_tx` (`:401`) و`RejectAsync` له فحص `Version` + `catch` (`:834,862`) — القصد مسارات الاستئناف فقط | تضييق النطاق على `TransferAllAsync` بنمط `AssignAsync` |
| 5 | 🟡 متوسطة | تنبيهات الجهات العامة لا تصل للشعب: 4 مواضع `Head`-فقط (قرار أ: توسيع بالنطاق حسب الاستعلام) | `PublicEntityRepository.cs:57,65`، `HeadAlertRepository.cs:119`، `HeadAlertRepository.cs:81` (عبر `HeadAlertService.cs:347`)؛ الاستهلاك: `Registry.cs:285,287,377-381`، `Moves.cs:186,310`، `Branches.cs:706`، `PublicEntityService.cs:618` | توسيع الدور مع توثيق تغيّر سلوك «غياب الرئيس» |
| 6 | 🔵 قرار معتمد | النطاق الدائري لسجل التدقيق — يُنفَّذ الآن (قرار 23 أصلًا يفرضه: «تأجيل معتمد غير منفَّذ») | `AuditLogsController.cs` + `AuditLogRepository.cs:34-44` (نمط الفرع القائم) + `App.tsx:421` + `Layout.tsx:135` (فرع البنود الأربعة) + `HeadIconRow.tsx:154` (`hideAudit`) | سلسلة 8 ملفات متزامنة (التفصيل في F6) |
| 7 | 🔵 قرار معتمد (أ) | سقوط إشعار الشعبة بلا رئيس على القسم — إبقاء + توثيق (ليس صامتًا: تدقيق + تنبيه `:1150-1153`) | `DocumentAppealService.cs:1145-1148` | توثيق فقط |
| 8 | 🔵 تصحيح | `ExecutedByDelegationId` غير موجود في الكود (4 مواضع توثيقية) لكنه **شرط بوابة في قرار 25 المعتمد** — يُصحَّح النص لا يُحذف | `SUBHEAD_PLAN.md:46,87` + `SUBHEAD_REVIEW_FIX_PLAN.md:71,285` (هذا الملف: لا ذكر للمعرّف) | تصحيح صياغة قرار 25 |

### ملاحظة أمانة (خارج النطاق — جرد لاحق منفصل)

فحوص `Role == UserRole.Head` متناثرة في الخلفية لا تغطيها الحراس (تفحص الواجهة وسمات
`Authorize` فقط): `DocumentsController.cs:115`، `DelegationsController.cs:73`،
`AppealsController.cs:33,50`، `StatisticsController.cs:66`… — **لا تُمسّ في هذه المهمة**.

---

## 3. قواعد صارمة ملزمة لكل بند (من `AGENTS.md` — تُطبق حرفيًا)

1. **لا حلول مختصرة** — الحل الكامل الصحيح ببنية نظيفة.
2. **الالتزام بالبنية** — نفس أنماط المشروع (المجلدات، التسمية، مواضع الاختبارات).
3. **لا تغييرات كاسرة** — إلحاقي (`optional`/`nullable`) فقط.
4. **التحقق الكامل إلزامي** بعد كل بند: `dotnet test` + `npx oxlint src` + `npx tsc -b` +
   `npx vitest run` + `npm run build` + `node scripts/subhead-guards.mjs` — كلها خضراء.
5. **اختبارات مع كل تغيير سلوكي** تشمل الحالات الحدية.
6. **مراجعة المسار الكامل** قبل إغلاق البند: عقد حقلًا بحقل + بحث شامل عن البقايا +
   مطابقة بنود الخطة.
7. **الفرع والدمج**: هذا الفرع ثم `PR` إلى `feature/subhead` — لا دفع مباشر، ولا دمج
   قبل اخضرار `CI` (`backend` + `frontend` + `guards` + `audit`).
8. **الهجرات**: لا تغيير سكيما متوقع (كل البنود سلوكية/استعلامية بعقود قائمة)؛ إن ظهرت
   هجرة فيُطبَّق تنبيه النشر الإلزامي (السياقان + `RUN_GUIDE.md` §9).

### ترتيب التنفيذ (موجات)

- **الموجة 1 (حرجة):** F1.
- **الموجة 2 (سلوكية):** F2 ثم F4.
- **الموجة 3 (توسيع معتمد):** F3 ثم F6.
- **الموجة 4 (متانة):** F5.
- **الموجة 5 (توثيق):** F7 + F8.

---

### F1 — تمرير `ownerSectionId` في القوائم الأربع (🔴 حرجة — أولًا)

**الملف:** `backend/src/DocGenerator.Api/Controllers/DocumentsController.cs` (4 أسطر فقط):

```csharp
// :230
var result = await _documents.SearchDeletedAsync(q, page, perPage, visibleBranch, visibleUser, ct, ownerSectionId);
// :249
var result = await _documents.SearchStruckOffAsync(q, page, perPage, visibleBranch, visibleUser, ct, ownerSectionId);
// :265
var result = await _documents.SearchExecutedAsync(q, page, perPage, visibleBranch, visibleUser, ct, ownerSectionId);
// :281
var result = await _documents.SearchReferredToStartAsync(q, page, perPage, visibleBranch, visibleUser, ct, ownerSectionId);
```

**الاختبارات (خلفية):** الشعبة ترى ملفات شعبته فقط في الأربع (شعبة أخرى محجوبة، بلا دائرة
محجوب)؛ الرئيس يرى نطاق القسم كاملًا (منع انحدار)؛ مدير/مشرف بلا تغيير.

**الحراس:** فحص في `scripts/subhead-guards.mjs` يرفض استدعاء القوائم الأربع من طبقة
`Api` بدون `ownerSectionId`.

**التحقق:** `dotnet test` + بحث بصفر نتائج عن الاستدعاءات بلا الوسيط.

---

### F2 — استثناء `ForwardedToHead` في فرع الرئيس من `InScope` (🟠 عالية)

**الملف:** `backend/src/DocGenerator.Infrastructure/Persistence/AppealRepository.cs:131-132`
(فرع الشعبة `:129-130` **لا يُمسّ** — مطابق للبحث أصلًا):

```csharp
: q.Where(a => a.Document.ExecutionCircuitId == null
    || a.Document.ExecutionCircuit!.SectionId == null
    || (a.Status == AppealStatusCatalog.Pending
        && a.ForwardState == AppealForwardCatalog.ForwardedToHead));
```

مع تعليق يربطه بقرار §2.22. العدّ والنقل يشتركان في `InScope` — التطابق بالبنية.

**الاختبارات (خلفية):** عدّاد القسم يتضمن المحال المنظور؛ النقل ينقله ويرفع `Version`؛
المحال المحسوم/المشطوب مستبعد؛ عدّاد الشعبة لا يتضمن المحال خارجها؛ تطابق العدّ والمنقول.

**فحص البقايا:** `ForwardedToHead` (البحث، `HasForwardedAppealAsync`، `InScope`).

---

### F4 — صف «بلا دائرة» في إحصاءات الدوائر (🟡 متوسطة)

**القواعد الذهبية (من المراجعة العليا):**

1. العدّ **صارم** `ExecutionCircuitId == null` فقط — دوائر `SectionId == null` لها صفوفها
   (وإلا ازدوج العدّ)؛ مع `!IsDeleted` مرآةً لـ`CountByCircuitsAsync`.
2. الصف الاصطناعي **فقط عندما** `ownerSectionId is null` (رئيس قسم/إدارة) **و** `FileCount > 0`
   — الشعبة بلا صف أصلًا (§5.6)، والصف الصفري ضجيج.
3. إسقاط العودة المبكرة `:983` كليًا (المسارات الفارغة آمنة)؛ الصف: `Id=0`، «بلا دائرة»،
   `BranchId = branchId ?? 0`، `IsActive=true`، `Version=0`، `SectionId/SectionName=null`
   (الواجهة تعرض «القسم» احتياطيًا — صحيح دلاليًا لملفات القسم).
4. الواجهة (`CircuitStatsPage.tsx`) قرائية بلا إجراءات — لا تغيير لازم؛ التصدير يستوعب
   الصف تلقائيًا بنفس الوسم (`ExcelExportService.cs:98`).

**الملفات:** ميثود مستودع جديدة `CountWithoutCircuitAsync(int? branchId, ct)` في
`IDocumentRepository` + `DocumentRepository` (استعلام تجميعي واحد يعيد
`(FileCount, PendingCount, LawyerCount)`)؛ `ExecutionCircuitService.CircuitStatsAsync`.

**الاختبارات (خلفية):** قسم بلا دوائر + ملفات يتيمة ⇒ صف واحد صحيح؛ بلا ملفات يتيمة ⇒
لا صف (مطابقة السابق)؛ شعبة ⇒ لا صف؛ مدير ⇒ تجميع الكل. القائمة:
`CircuitStats_ReturnsLawyerCounts` و`CircuitStats_HeadSeesDivisionOnly_ManagerSeesAll`
و`CircuitStats_CarriesSectionColumns` تبقى خضراء **بلا تعديل** (مثبت سطرًا بسطر).

---

### F3 — توسيع البثّ لرؤساء الشعب (🟡 متوسطة — قرار أ بالنطاق حسب الاستعلام)

**التغيير (توسيع محمول الدور فقط — بلا تغيير تواقيع/أسماء):**

- `PublicEntityRepository.cs:57` و`:65`: `u.Role == UserRole.Head` ←
  `u.Role is UserRole.Head or UserRole.SubHead`.
- `HeadAlertRepository.cs:119` (`ListAllActiveHeadsGroupedByBranchAsync`): نفس التوسيع.
- `HeadAlertRepository.cs:81` (`ListActiveHeadsAsync` — عبر `HeadAlertService.cs:347`
  لمسار `HeadAlertTargetType.Head`): نفس التوسيع، مع توثيق أن «غياب الرئيس ⇒ رفض الإنشاء»
  صار «غياب الرئيس والشعبة ⇒ رفض».

**يُستثنى صراحةً:** مناديات `FindActiveHeadAsync(UserRole.Head, …)` للتوجيه
(`DocumentAppealService.cs:1152-1153`، `ExecutionCircuitService.cs:445`،
`DocumentDelegationService.cs:995/1334/1341/1342/1346`) — توجيه موافقة لا بثّ (قرار F7).

**الضمان:** كل مستهلك ينشئ تنبيهًا لكل مستلم بفرعه، والقراءة `ListByRecipientInBranchAsync`
(فرع + مستلم) — لا تسريب عبر الفروع. الحجم الأكبر للبثّ مقبول صراحةً بقرار (أ).

**الاختبارات (خلفية):** شعبة فرع الجهة يستلم تنبيه الاقتراح/الإدخال/النقل؛ شعبة فرع آخر
لا يستلم؛ الرئيس يستلم كالسابق.

---

### F6 — النطاق الدائري لسجل التدقيق (🔵 قرار معتمد — سلسلة متزامنة واحدة)

**الخلفية (إلحاقي `optional` — المنادي الوحيد هو السلسلة نفسها):**

1. `IAuditLogRepository.cs:16` + `AuditLogRepository.cs`: وسيط `int? scopeSectionId = null`؛
   فرع `else if (scopeSectionId.HasValue)` مرآةً لفرع `scopeBranchId` (`:34-44`):
   الصف مرئي إن نُسِب لمستند دائرته في الشعبة (`IgnoreQueryFilters` — تاريخ الشعبة يبقى)
   أو لفاعل `Users.SectionId == section` (دلالة `UserName` = اسم الدخول مثبتة:
   `TokenService.cs:31`).
2. `IAuditLogService` + `AuditLogService.cs`: تمرير الوسيط.
3. `AuditLogsController.cs`: `[Authorize(Roles = "manager,admin,head,subhead")]`؛
   `scopeSectionId = Role == SubHead ? User.GetSectionId() : null`؛ شعبة بلا شعبة ⇒ `Forbid`
   (قرار §2.16).

**الواجهة (الوصول عبر بطاقة اللوحة — لا بند جانبي في الفرع الخاطئ):**

4. `App.tsx:419-425`: إضافة `isSubHead` وتحديث التعليق (الشرط الخلفي تحقق).
5. بطاقة «سجل التدقيق» للشعبة في اللوحة (إظهار ما يخفيه `hideAudit` عن `subhead`).
6. `Layout.tsx`: **لا** بند جانبي جديد (الرئيس والشعبة في فرع البنود الأربعة `:135` عمدًا).

**الشبكات (نفس الالتزام — وإلا يفشل `CI`):**

7. `RF004AuthzTests.cs:301-303`: إسقاط استثناء `AuditLogsController`.
8. `subhead-guards.mjs`: تحديث مدخلي `App.tsx` و`AuditLogsController.cs`.
9. `App.test.tsx:206-212` (ارتداد ⇒ سماح)، `:249` (إضافة `'subhead'`)،
   `HeadIconRow.test.tsx:65` (ظهور البطاقة للشعبة).

**الاختبارات (خلفية):** شعبة ترى أحداث مستندات دوائر شعبتها وأحداث حسابها فقط؛ أحداث
شعبة أخرى/بلا دائرة محجوبة؛ الرئيس/الإدارة بلا تغيير.

---

### F5 — تحصين `TransferAllAsync` بنمط `AssignAsync` (🟡 متوسطة — مضيّق)

**الملف:** `DocumentAppealService.cs:408-443` — نقل القراءة (`:429-430`) **داخل** `_tx.RunAsync`
+ إعادة فحص `Pending` لكل عنصر + `Version++` (الرمز `IsConcurrencyToken` يرمي، والمعالج
العام يترجم `409` عربية — بلا `catch` جديد، مرآةً لـ`AssignAsync :335-357`).

**خارج النطاق صراحةً:** `TransferCircuitAsync` (يقرأ داخل `_tx` أصلًا `:401`) و`RejectAsync`
(فحص `Version` + `catch` `:834,862`) — لا يُمسّان.

**الاختبارات (خلفية):** سباق متزامن ⇒ ناجح واحد + `409` ودية للآخر (على غرار
`TransferRedirectConcurrencyTests`)؛ مصدر فارغ ⇒ صفر ناجح بلا استثناء؛ محسوم بين
المعاينة والتنفيذ ⇒ مستبعد والعدّ مطابق. (قيد موثق: سباق `SQLite` تقريبي.)

---

### F7/F8 — توثيق (قرار أ + تصحيح)

- **F7:** توثيق «شعبة بلا رئيس → قسم» قرار توفّر في `SUBHEAD_PLAN.md` (قرب §6.2/قرار 22)
  مع الإشارة للتدقيق والتنبيه (`:1150-1153`) — بلا تغيير كود.
- **F8:** تصحيح صياغة قرار 25 في `SUBHEAD_PLAN.md:46,87` (توضيح أن الشرط توثيقي-تعاقدي
  يُطبَّق عند إدخال الحقل مستقبلًا) — **لا حذف** من قرار معتمد.

---

## 4. شبكات الانحدار الجديدة (مع الإصلاحات)

1. **حارس F1** في `scripts/subhead-guards.mjs`: استدعاء القوائم الأربع من `Api` بدون
   `ownerSectionId` ⇒ فشل `CI`.
2. **تحديثات F6** في نفس الحارس (إسقاط/تعديل مدخلي السماح) + `RF004AuthzTests`.
3. تشغيل الشبكات محليًا قبل الدفع (نفس أمر `CI`).

---

## 5. قائمة التحقق النهائية قبل `PR`

1. [ ] F1–F5 مغلقة بتغييرها + اختباراتها + حدّياتها خضراء.
2. [ ] البوابات خضراء: `dotnet test`، `npx oxlint src`، `npx tsc -b`، `npx vitest run`،
   `npm run build`، `node scripts/subhead-guards.mjs`.
3. [ ] مراجعة المسار الكامل لكل عقد ممسوس (حقلًا بحقل).
4. [ ] بحث شامل عن البقايا — لا شيء إلا في الهجرات التاريخية.
5. [ ] مطابقة بنود هذه الخطة مع المنفَّذ، وتوثيق أي انحراف.
6. [ ] تقرير الإنجاز يذكر الهجرات (المتوقع: لا شيء).
7. [ ] `PR` من `refactor/subhead-review-fixes` إلى `feature/subhead` — لا دفع مباشر،
   ولا دمج قبل اخضرار `CI`.

---

## 6. ملحق الشواهد (نقاط الدخول)

| البند | الملف | الأسطر |
|-------|-------|--------|
| F1 | `Controllers/DocumentsController.cs` | `216-283` (الأربع)، التواقيع `DocumentService.cs:28-44` |
| F2 | `Persistence/AppealRepository.cs` | `61-65` (النمط)، `98-133` (العدّ/النقل و`InScope`) |
| F3 | `Persistence/PublicEntityRepository.cs` / `Persistence/HeadAlertRepository.cs` | `54-68` / `78-84` و`115-142` |
| F4 | `Services/ExecutionCircuitService.cs` / `Services/ExcelExportService.cs` / `DTOs/ExecutionCircuitDtos.cs` | `976-1003` / `:98` / `:71-83` |
| F5 | `Services/DocumentAppealService.cs` / `Middleware/GlobalExceptionHandler.cs` | `335-357` (النمط)، `408-443` (الهدف) / `:37,41,63-64` |
| F6 | `Controllers/AuditLogsController.cs` / `Persistence/AuditLogRepository.cs` / `Services/AuditLogService.cs` / `App.tsx` / `HeadIconRow.tsx` | `12,31` / `14-44` / `7-34` / `419-425` / `:154` |
| F7 | `Services/DocumentAppealService.cs` | `1150-1153` |
| الحراس/CI | `scripts/subhead-guards.mjs` + `.github/workflows/ci.yml` | — |
