# خطة تنفيذ إصلاحات حوكمة الجهة الأم (معتمدة — بانتظار إذن التنفيذ)

> **الحالة:** معتمدة النطاق من المستخدم. **ممنوع التنفيذ قبل موافقة صريحة ثانية.**
> **النطاق:** البنود 1 و2 و4 و5 + 6، والبند 3 بالخيار (أ): منع صريح للجميع — الأم محمية بنيًا.
> **المصدر:** مراجعة `docs/BRANCH_MANAGEMENT_PLAN.md` الشاملة + التعديلات المدمجة (الضمني في الإنشاء، تغطية المعاينة، الصقل).

## 0) مبادئ ملزمة (من `AGENTS.md`)

- لا حلول مختصرة؛ لا تغييرات كاسرة؛ لا أسرار في الكود.
- **لا عقود جديدة:** لا `DTOs` جديدة ولا حقول جديدة ولا هجرات `EF` — منطق داخلي + اختبارات فقط.
- **لا تنبيه نشر لقواعد البيانات** (لا تغيير مخطط).
- مع كل تغيير سلوكي: اختبارات تغطية + الحالات الحدية.
- التحقق الكامل بعد التنفيذ: `dotnet test` + `npx oxlint src` + `npx tsc -b` + `npx vitest run` + `npm run build` — كلها خضراء.
- مراجعة مسار كامل + `grep` بقايا قبل إعلان الإنجاز.

## 1) البند 1 — ظهور أحداث المجموعات في سجل المحافظة

**السبب:** `ListChangeEventsAsync` يحمّل `Include(c => c.Group)` دون `ThenInclude(g => g.Entries)` ولا تحميل كسول في المشروع، فـ `MatchesGovernorate` (`PublicEntityService.cs:826-832`) يرجع `false` دائمًا للأحداث بلا `EntryId` (عمليات المجموعات المركزية: `:2610`، `:2948`، `:3248`، `:3535`، `:3781`). سجل الرئيس (محافظته مجبورة خادميًا) لا يعرضها أبدًا — ثقب اكتمال S6.

**الملف:** `backend/src/DocGenerator.Infrastructure/Persistence/PublicEntityRepository.cs:193-199` — إضافة `ThenInclude(g => g.Entries)` على تضمين المجموعة فقط. يُصلح الفلترة وعمود `Governorate` في `ToChangeEventDto` (`PublicEntityService.cs:891`) معًا.

**ملاحظة تصميمية (توثَّق في تعليق الكود):** أي رئيس لفرعٍ في المجموعة سيرى أحداثها المركزية — اتساع مقصود بروح S6.

**اختبارات** (`backend/tests/DocGenerator.Application.Tests/PublicEntityServiceTests.cs`):
1. `ChangeEvents_GroupLevelEvent_VisibleToHeadOfMemberGovernorate` — حدث `rename` مركزي بلا `EntryId` على مجموعة لها فرع بمحافظة الرئيس → يظهر له.
2. `ChangeEvents_GroupLevelEvent_HiddenFromOtherGovernorateHead` — نفس الحدث → يُحجب عن رئيس محافظة أخرى، مع تثبيت عمود `Governorate` المعروض.

**حدّيات:** حدث بلا مجموعة؛ مجموعة بلا قيود؛ فلتر محافظة فارغ (الكل يرى الكل كما قبل).

## 2) البند 2 — إغلاق ترقية فرع→أم على الرئيس (صريح + ضمني)

**السبب:** الحارس `GuardHeadCannotEditParent` (`PublicEntityService.cs:2051-2056`) يفحص القيمة *الحالية* فقط، بينما المساران يقبلان العلم من الرئيس:
- `CreateAsync:490`: `entry.IsParentEntity = request.IsParentEntity ?? (branchName == DefaultBranchName);`
- `UpdateAsync:642-645`: الصيغة نفسها (صراحةً أو اشتقاقًا من الاسم).
رئيس يُنشئ قيدًا بالاسم الافتراضي دون إرسال الحقل → علم `true` تلقائيًا: التفاف كامل على S1.

**التغيير:**
- `request.IsParentEntity == true` صراحةً من `Head` → `UnauthorizedAccessException` مع إرشاد للاقتراح (في `CreateAsync` قبل المعاملة، وفي `UpdateAsync` بجوار الحراس `:615-626`).
- الاشتقاق الضمني (`null` + الاسم الافتراضي) عند الرئيس → يُثبَّت `false` بلا رفض (حماية صامتة لا تُعادي تسمية بريئة).
- المدير/المشرف: بلا تغيير (عقدهما يسمح).

**اختبارات:**
1. `Update_HeadExplicitIsParentEntity_ThrowsUnauthorized`.
2. `Update_HeadRenameToDefaultBranchName_KeepsIsParentEntityFalse`.
3. `Create_HeadExplicitIsParentEntity_ThrowsUnauthorized`.
4. `Create_HeadDefaultBranchName_DerivesIsParentEntityFalse` (المسار الضمني في الإنشاء — التعديل المدمج).
5. ضمني: مدير يرقّي → مسموح (لا كسر عقد).

**تدقيق مسار:** `DocumentForm.tsx → DTO → CreateAsync/UpdateAsync` للتأكد أن الواجهة لا ترسل الحقل للرئيس أصلًا (فلا كسر سلوكي أمامي).

## 3) البند 3 — منع استهداف الأم في العمليات الأربع (أ)

**القرار المعتمد:** منع صريح للجميع — الأم محمية بنيًا من الجميع، وطريقها الوحيد المسارات المركزية (`Update` / شاشة `rename` بمرسوم / `AbolishAndReplace` المجموعي للاستعادة).

**النقطة المشتركة (مُتحقق من حصريتها):** مناديات `GetActiveGroupEntryAsync` كلها عمليات الفروع الأربع فقط (1390 تسمية، 1473-1474 دمج، 1558/1575 إلغاء، 1695/1704 توحيد). لا مسار اقتراحات ولا `AbolishAndReplace` يمرّ عبرها — مثبت بـ `grep`.

**التغيير:** `backend/src/DocGenerator.Application/Services/PublicEntityService.cs:GetActiveGroupEntryAsync:2064-2083` — بعد الجلب والتحققات القائمة: إن كان `entry.IsParentEntity` → `ArgumentException` («عمليات الفروع للفروع فقط — الأم تُدار عبر التعديل/التسمية المركزية»). يغطي التنفيذ والمعاينة معًا (كلاهما يمرّ عبر النقطة).

**نطاق مثبت كتابيًا:** `AbolishAndReplace` المجموعي (طريق استعادة الأم المسنود) خارج النطاق لأنه لا يمرّ عبر النقطة — يُذكر في التقرير حتى لا يعبثه تنفيذ لاحق.

**اختبارات:**
1. `BranchOp_OnParentEntry_Throws_ForManager` (يكفي اختبار عملية واحدة لتغطية النقطة المشتركة — يُقترح الإلغاء).
2. `Preview_BranchOp_OnParentEntry_Throws` (تغطية المعاينة — التعديل المدمج).

**تحقق مسبق (جزء من التنفيذ):** `grep` كل مناديات العمليات الأربع + عدم اعتماد أي اختبار قائم على استهداف الأم (مُتحقق مسبقًا: أسطر 2902-3110 خالية من `IsParentEntity`؛ يُعاد التأكيد بعد التعديل بتشغيل الحزمة).

## 4) البند 4 — حارس خادمي: لا اعتماد اسم قبل تطبيقه

**السبب:** `ReviewParentEditSuggestionAsync:1926-1962` يعتمد دون التحقق أن الاسم المقترح طُبّق (الواجهة تفرضه عبر `needsRename`، لكن `API` المباشر يتجاوزه).

**التغيير:** عند `approved` وكان `ProposedCanonicalName` غير فارغ ومختلفًا (عبر `ArabicNameNormalizer.Normalize` كما في باقي الكود) عن المعياري الحي → `ArgumentException` («طبّق التسمية عبر `rename` أولًا»).

**اختبارات:**
1. `ReviewApprove_WithoutAppliedRename_Throws`.
2. `ReviewApprove_AfterAppliedRename_Accepts`.
3. `ReviewApprove_SuggestionWithoutName_Accepts` (نوع/صيغة فقط — غير متأثر).

**توافقية:** الواجهة تطبّق مسبقًا (`needsRename` + اختبارا `ParentSuggestionsTab`) — يُتحقق باختبارات التبويب القائمة.

## 5) البند 5 — تصحيح ملخص معاينة الإلغاء بهدف

**السبب:** `PublicEntityService.cs:1284-1289` — `targetBranch = entry.BranchName` (المصدر) قبل معرفة الهدف، فيظهر «بدمجه في X» حيث X اسم الفرع الملغى نفسه، وقيمة `targetBranchName` في الاستجابة خطأ وتُعرض حرفيًا في `BranchManagementModal.tsx:808-809`.

**التغيير:** تعيين `targetBranch = target.BranchName` داخل فرع الهدف.

**اختبار:** `PreviewAbolish_WithTarget_ShowsTargetBranchName` — الملخص والحقل يحملان اسم الهدف لا المصدر.

## 6) البند 6 — تحديث توثيقي فقط

**الملف:** `docs/BRANCH_MANAGEMENT_PLAN.md:§2.4` — حذف «تنبيه للمديرين» (القرار المعتمد: تبويب فقط)، وتصحيح `accept` إلى `review`. بلا كود وبلا اختبارات.

## 7) معايير القبول

- ≈ 12 اختبارًا جديدًا (2 + 5 + 2 + 3 + 1 للبنود 1/2/3/4/5) — كلها خضراء.
- `dotnet test` (حزمتا API + Application، صفر فشل) ← `npx oxlint src` (0/0) ← `npx tsc -b` ← `npx vitest run` ← `npm run build`.
- مراجعة مسار العقود للاقتراحات + `grep` بقايا (`ThenInclude` / `IsParentEntity` / `targetBranch`) + مطابقة كل بند مع ما نُفذ.
- التراجع عند الحاجة: `revert` نظيف (لا هجرات ولا عقود).

## 8) المخاطر

- البند 3 (أ): يُستبعد انحدار المدير بالتحقق المسبق (لا مسار واجهة يستهدف الأم؛ لا اختبار قائم يفعل).
- البند 1: تحميل إضافي محدود للقيود مع الأحداث — مقبول (القائمة تُحمَّل كلها أصلًا).
- البند 2: صياغة رسالة الرفض إرشادية (توجيه للاقتراح) لا عدائية.

## 9) ملحق المراجعة التحليلية الشاملة (منفَّذ ضمن نطاق البنود نفسها — بلا عقود/هجرات)

1. **تشديد `MatchesGovernorate` (البند 1):** بعد `ThenInclude` تبيّن أن الفرع البديل للمجموعة كان
   سيكشف أحداث مستوى القيد لرؤساء محافظات الأعضاء الأخرى (تسريب نطاق). أُعيدت الصياغة:
   حدث ذو `Entry` → محافظة القيد حصرًا؛ حدث مجموعي بلا `EntryId` → محافظات الأعضاء.
   أُعيدت كتابة اختباري البند 1 على حدث مجموعي حقيقي (`EntryId = null`) مع تثبيت أن `Governorate`
   و`CanonicalName` غير فارغين في `DTO`، وأُضيف اختبار ثالث
   (`ChangeEvents_EntryLevelEvent_HiddenFromOtherMemberGovernorate`) يغلق التسريب.
2. **تناسق معاينة الدمج (البند 3):** استدعاء `ValidateMergeablePair` في حالة `Merge` كان بلا
   `try/catch` فيُرمى `ArgumentException` (‏400) بدل إرجاعه ضمن `Errors` (‏200) كما في باقي
   العمليات — والواجهة تعرض `Errors` داخل اللوحة. أُحيط بـ `try/catch (ArgumentException)` مع
   بقاء `UnauthorizedAccessException` مُرمًا (تصميم موثّق)، وأُضيف اختبار
   (`PreviewMerge_OnParentEntry_ReportsError`).
3. **الجرد النهائي للاختبارات: 24** (15 سابقة + 9 جديدة) — `dotnet test`: 287 API + 892 Application خضراء.
4. **بقايا مُغلقة بخطة الإغلاق (`docs/PARENT_RESIDUAL_CLOSURE_PLAN.md`):**
   - **(أ) ProposeEditAsync:** تجميد العلم بالكامل (لا ترقية ولا تخفيض) + رفض `true` صريح
     بـ `UnauthorizedAccessException` — اختباران + حدّية.
   - **(ب) CreateAsync للمحامي:** رفض `IsParentEntity=true` صريح + تعطيل الاشتقاق الضمني
     للمحامي (يُنشئ فرعًا بدل أمّ مع بقاء `NeedsReview`) — اختباران + حدّية.
   - **(ج) MoveEntryAsync:** `GuardNotParentEntry(entry)` على المصدر فقط (لا أمّ تُنقَل ولا تُطوى
     أصلًا) — الطيّ باتجاه أمّ هدف يبقى مباحًا — اختباران.
   - جميعها مُنفَّذة ومضمونة (287 + 892، صفر فشل، لا واجهة مسّت).
