# مراجعة تحليلية وخطة تنفيذ: مرآة قرائية لصفحة المحامي في بوابة مندوب الجهة العامة

> **حالة الوثيقة:** مسودة بانتظار الاعتماد — **لا يبدأ أي تنفيذ قبل موافقة صاحب المشروع الصريحة.**
> **قاعدة العمل:** أي محادثة تنفيذ لاحقة تبدأ بقراءة هذا الملف كاملًا، وتنفّذ وفق بنوده حصرًا،
> ولا يُعدَّل أي قرار معتمد أدناه إلا بموافقة جديدة.
> **التاريخ:** 2026-09-24.

---

## 1) القرارات المعتمدة من النقاش (غير قابلة للتغيير دون موافقة)

| # | القرار |
|---|---|
| ق1 | صفحة المندوب `PortalFileDetail.tsx` تصبح **نسخة قرائية من صفحة المحامي `DocumentView.tsx`** بنفس البطاقات والنوافذ، بلا أي زر فعل **على الملف**. الاستثناء الوحيد الموثق: بطاقة «المراسلات» `DocumentCorrespondenceCard` تُمرَّر `canCreate=true` (إنشاء مراسلة مسموح به خلفيةً للمندوب عبر بنى المراسلات في البوابة) — توثيق دقيق لا ترخيص جديد. |
| ق2 | تبقى: `PartiesCard` + نافذة `PartyDetailsModal`، و`FileDataCard` + نافذتا أرقام الأساس (`BaseNumbersModal`) وسجل التعاقب (`TransferHistoryModal`) **دون سجل التعديلات** (`DocumentChangesModal` مستثنى). |
| ق3 | تبقى: `ExecutoryDocumentCard`، و`AssetsSection` **بما فيها الشارات** (الحجز + «مناب»)، و`DelegationsCard` **بلا زر «تسطير إنابة»** وبلا تعديل/حذف. |
| ق4 | تبقى: `SourceFileInfoCard` و`DelegationStatusCard` و`StatusCard` **قرائية بلا أي زر** (تسجيل أصولًا / إتمام / تغيير حالة). |
| ق5 | تبقى: `OccurrencesCard` + نافذة `OccurrencesModal` (عرض التفاصيل) + نافذة `AppealInfoModal` لكل استئناف. |
| ق6 | تُستثنى نهائيًا: زر/قائمة «استئناف ▾» ونافذة `AppealFormModal`، زر «توليد مستندات»، زر/نافذة «الإجراءات والملاحظات»، زر «تعديل» الملف، نقل/تنبيه، `DocumentReviewLettersCard` (كتب المطالعة)، `DocumentChangesModal`، نوافذ الإنابة الفعلية. |
| ق7 | تُضاف بطاقة **«الإجراءات التنفيذية»**: من جدول `ExecutionActions` نفسه بفلترة عرضية `type === 'action'` فقط (الملاحظات `note` محجوبة **نهائيًا وسلكيًا** — راجع ف6: التسقاط في الاستجابة قبل مغادرة الخادم، لا في الواجهة فقط)، كل إجراء يعرض **النص + التاريخ + المحامي الذي أدخله** (`Text` + `ActionDate` + `CreatedByName`)، **بلا شارة تذكير**، مرتبة **الأحدث أولًا** (وكسر تعادل `Id`)، والنص الغني عبر `sanitizeRichText` نفسها. |
| ق8 | الاستئنافات: **بطاقة التفاصيل الحالية فقط** (مفلترة بنطاق الجهة) — لا صفحة قائمة جديدة `‎/portal/appeals`. |
| ق9 | **إظهار اسم المحامي في كل البطاقات** حتى يعرف المندوب المحامي الذي يتابع الملف (قرار 2026-09-24 — يعكس إخفاء المحامي السابقة: `FileDataCard` تُمرَّر `showLawyer=true`، وتفاصيل الإنابة والاستئناف تُبقي `assignedLawyerName`). الفرع (`showBranch`) يبقى مخفيًا، وسجل التعديلات يبقى مستثنى. |
| ق10 | إبقاء سطر «سطّره: `createdByName`» في نافذة الاستئناف (اتساقًا مع بطاقة الإجراءات) — مع إخفاء **رأي المحامي** `defenseOpinion` وحده (رأي داخلي لا اسم). **ملحق معتمد (تقرير المراجعة النقدية):** حقل `Notes` الحر للاستئناف يُصفَّر خلفيًا مع الرأي — حقل حر من إدخال المحامي (2000 حرف) بلا ضمان بنيوي بأنه وقائع قضية، فيشمله حكم الفشل المغلق نفسه؛ راجع ف2. |

---

## 2) الوضع الحالي (`as-is`) — بالأدلة

### 2-1) عرض المندوب اليوم: 3 مقاطع فقط

- `frontend/src/pages/PortalFileDetail.tsx` (141 سطرًا): يجلب نقطتين فقط —
  `GET /portal/files/{id}` ثم `GET /portal/files/{id}/appeals` (أسطر 31–34) —
  ويعرض 3 مقاطع `section`: «المعلومات الأساسية» (75–86)، «الأطراف» مختصرة (89–107:
  `applicant` + `executedPublicEntities` اسم/فرع/محافظة فقط)، «الاستئنافات» مختصرة (110–136).
- `frontend/src/App.tsx` (162–176): مسارا البوابة محصوران بدور `entitymanager`، ومسار
  `‎/documents/:id` الداخلي محجوب عنه أصلًا بالحارس والقائمة.
- `frontend/src/components/Layout.tsx` (105–112): قائمة المندوب مختصرة (`‎/portal` فقط).

### 2-2) العرض الداخلي: بطاقات منفصلة + نوافذ

- `frontend/src/pages/DocumentView.tsx` (654 سطرًا): أربع لوحات `infoPanel` (214–228:
  `PartiesCard` + `FileDataCard`)، `securityPanel` (229–244: `ExecutoryDocumentCard` +
  `DelegationStatusCard` + `AssetsSection` + `DocumentReviewLettersCard`)،
  `delegationsPanel` (245–288: `SourceFileInfoCard`/`DelegationsCard` + `OccurrencesCard`)،
  `statusPanel` (289–298: `StatusCard`). جوال تبويبات (300–305)، مكتبي 3 أعمدة (486–489).
- البطاقات في `frontend/src/components/view/` و`components/delegation/` و`components/appeal/`
  و`components/review/` — كلها موثقة أدناه مع نوافذها.

### 2-3) بنية «الإجراءات والملاحظات»: جدول واحد

- الكيان `backend/src/DocGenerator.Domain/Entities/ExecutionAction.cs` (3–17)، الجدول
  `ExecutionActions` — التفريق بحقل `Type`: ‏`action` مقابل `note`.
- الخلفية `Services/DocumentService.Actions.cs` (187–210 `NormalizeAction`: تاريخ `action`
  مطلوب، تاريخ `note` اختياري)، والـ `DTO` هو `ExecutionActionDto(Id, Type, Text,
  ActionDate, ReminderDuration, ReminderColor, CreatedByName, CreatedAt)` في
  `DTOs/DocumentDtos.cs` (97–105) ومرآته في `frontend/src/types/index.ts` (135–144).
- لا يوجد أي كيان/نقطة/مكوّن باسم «الإجراءات التنفيذية» — البحث يعيد صفر ملف. البطاقة
  الجديدة **فلترة عرضية** لا عمود جديد ولا هجرة.
- نقاط القراءة الداخلية `DocumentsController.cs` (620–629 `GET /documents/{id}/actions`
  بصلاحية `CanAccessOrFollowAsync`) — **محظورة على المندوب** بالحارس أدناه.

### 2-4) العزل البنيوي القائم

- `backend/src/DocGenerator.Api/Middleware/EntityManagerPortalGuard.cs` (18–40): أي طلب
  من `EntityManager` خارج `‎/api/portal` + `‎/api/auth/me|logout` + `‎/api/client-errors` +
  `‎/api/meta` يُرفض `403` فورًا. مسجّل في `Api/Program.cs:284`.
- `PortalController.cs` (19): `[Authorize(Roles = "entitymanager")]` — اليوم 5 نقاط قرائية
  فقط (`my-scope`, `files`, `files/{id}`, `files/{id}/appeals`, `stats`, `export`).
- `PortalService.GetFileAsync` (95–109): فحص `IsInScopeAsync` ثم `DocumentResponse.FromEntity`
  **الكاملة** + تدقيق `view_entity_portal_files` مرة واحدة عند فتح الملف.
- قاعدة النطاق `PortalRepository.cs` (25–41 `ScopePredicate`: أي تطابق طرفي بقيد نهائي
  غير قيد المراجعة). الخروج عن النطاق يُترجم `404` دون كشف الوجود.

---

## 3) المراجعة التحليلية التفصيلية — الفجوات والمخاطر

> كل بند أدناه وُثّق بالفحص المباشر للملفات، ويُذكر معه الحكم (يُحسم في التصميم §4)
> وما يتطلب انتباه المنفّذ.

### ف1 — `PortalAppealDto` المختصرة لا تكفي لنافذة `AppealInfoModal` (حرج)

- **الدليل:** `PortalAppealDto` (`EntityPortalDtos.cs` 36–46 و`types/index.ts` 1736–1746) تحمل
  9 حقول فقط (الاتجاه/الحالة/النوع/الأساس/السنة/القرار)، بينما `OccurrencesCard`
  (`OccurrencesCard.tsx` 18–28) يتوقع `appeals?: AppealDto[]` ويعرض `appeal.court`
  و`appealedDecisionDate` ويمرّر الكائن كاملًا إلى `onOpenAppeal(appeal: AppealDto)`،
  و`AppealInfoModal` يتوقع `AppealDto | null` (الكاملة: 1036–1087 في `types/index.ts`).
- **الحكم:** يلزم نقطة بوابة جديدة تُرجع `AppealDto` الكاملة scope-checked
  (`ListAppealDetailsAsync`)، مع معالجة ف2 أدناه. إبقاء `GET .../appeals` المختصرة كما هي
  للتوافق، أو توحيدهما — يُحسم عند التنفيذ بعد تدقيق المستهلكين.

### ف2 — نافذة الاستئناف الكاملة: يُخفى رأي المحامي وحده (متوسط — محسوم بق9/ق10)

- **الدليل:** `AppealDetailsBody.tsx:39` يعرض `المحامي المتابع: assignedLawyerName`،
  و`:75` يعرض `رأي المحامي المتابع بأسباب الاستئناف: defenseOpinion`، و`:125–131`
  تذييل «سطّره: `createdByName`».
- **الحكم (بعد قراري 2026-09-24):** يُبقي المندوب `assignedLawyerName` (ق9) وسطر «سطّره»
  (ق10)؛ ويُخفى **`defenseOpinion` وحده** (رأي داخلي لا اسم) طبقتين: (أ) خلفية — نقطة
  البوابة تُصفّره؛ (ب) واجهة — `AppealDetailsBody` خاصية `hideOpinion` تُسقط الخلية `:74–76`.
   لا يُكتفى بالإخفاء الأمامي وحده لأن البيانات تُرسل عبر الشبكة.
- **ملحق معتمد (تقرير المراجعة النقدية):** حقل `Notes` الحر (`AppealDto.Notes` — «الملاحظات»،
  2000 حرف من إدخال المحامي في `DocumentAppealService:872`) لم يُصنَّف أصلًا وكان يصل
  البوابة ويُعرض (`AppealDetailsBody:131`). صُنِّف الآن مع الرأي: **يُصفَّر خلفيًا** في
  `ListAppealDetailsAsync` (`a with { DefenseOpinion = null, Notes = null }`) — الفشل المغلق
  أولى من المرآة الكاملة للحقول الحرة. مُثبَت باختبار
  (`ListAppealDetails_HidesDefenseOpinionAndNotes_KeepsLawyerAndData`).

### ف3 — مدخل سجل التعاقب: محلول بقرار إظهار المحامي (كان حرجًا — أُغلق بق9)

- **الدليل:** `FileDataCard.tsx:44–72` — بلاطة «المحامي المختص» (وهي زر فتح
  `TransferHistoryModal` عبر `onOpenAssignments`) تُعرض عند `showLawyer=true` فقط.
- **الحكم (بعد ق9):** تُمرَّر `showLawyer=true` للمندوب، فتظهر البلاطة باسم المحامي
  وتفتح `TransferHistoryModal` من `doc.assignments` المضمّنة — **لا بلاطة بوابة مستقلة
  ولا نقطة جديدة ولا تغيير في `FileDataCard`**. (لو كان القرار إخفاءً لبقيت البلاطة
  المستقلة إلزامية.)

### ف4 — «منشئ المستند» يظهر للمندوب: مقبول بقرار الأسماء (ملغى كمشكلة — ق9)

- **الدليل:** `FileDataCard.tsx:155–159` — `{!isLawyer && <FieldCell label="منشئ المستند"
  value={doc.createdByName} />}`. بتمرير `isLawyer=false` للمندوب يظهر الاسم.
- **الحكم (بعد ق9):** الظهور **متسق** مع سياسة إظهار الأسماء (المحامي/المُدخل/الساطر) —
  **لا خاصية `showCreator` ولا أي تغيير في `FileDataCard`**. يُوثَّق كسلوك مقصود.

### ف5 — سجل التعاقب نفسه يحمل أسماء محامين داخلية (متوسط — مقبول ضمن ق2)

- **الدليل:** `DocumentAssignmentDto` (`types/index.ts` 229–235) تحمل
  `lawyerName` و`assignedByName` — و`TransferHistoryModal` يعرضهما.
- **الحكم (مؤكد بق9):** ق2 اعتمدت بقاء النافذة، وق9 اعتمد إظهار الأسماء — لا تعارض أصلًا:
  أسماء `lawyerName/assignedByName` سجل إداري قرائي مقصود (النافذة لا تُفتح إلا بطلب صريح).
  البديل (تقليم الأسماء خلفيًا) مرفوض لأنه يُفرغ السجل من معناه ويخالف ق9.

### ف6 — `GET /portal/files/{id}` تُرجع `DocumentResponse` الكاملة (معلوم — الملاحظات أُغلق تسريبها)

- **الدليل:** `PortalService.GetFileAsync:100–108` تُرجع `DocumentResponse.FromEntity`
  الكاملة (فرع/محامٍ/عدّادات/`assignments`/`occurrences`/`assets`...). الإخفاء أول التنفيذ كان
  **عرضيًا فقط** (`showBranch/showLawyer=false`) وبيانات الملاحظات تصل المتصفح.
- **الحكم (التوصية 1 — نُفِّذت):** أُغلق تسريب الملاحظات الداخلية **سلكيًا** داخل
  `GetFileAsync` بذاتها: `Notes` و`ImmediateActions` تُصفَّران، و`ExecutionActions` تُرشَّح إلى
  نوع `action` فقط مع ترتيب حتمي (`CreatedAt` ثم `Id`) — قبل أن تغادر الاستجابة الخادم.
  بقية الحقول (فرع إداري/عدّادات/أرقام وطنية) سلوك مرآة مقصود تستهلكه البطاقات. التقليم
  الشمولي بعقد بوابة مقلّص (`PortalFileDetailDto`) يبقى **دَينًا تقنيًا اختياريًا مؤجلًا**
  (لا تُعاد هيكلة عقد مستقر من أجل الأثر المتبقي الصفري)، والضمانان الحقيقيان —
  الحارس + فحص النطاق — قائمان.

### ف7 — `BaseNumbersModal` تجلب من مسار محظور على المندوب (حرج — لها حل)

- **الدليل:** `BaseNumbersModal.tsx:24` تجلب `GET /documents/{id}/base-numbers` —
  الحارس يرفضه للمندوب `403`.
- **الحكم:** نقطة بوابة جديدة `GET /portal/files/{id}/base-numbers` (scope-checked)
  تُرجع `BaseNumberHistoryDto[]` نفسها. لا تغيير على المكوّن سوى حقن الدالة الجالبة
  أو ممرّ `baseUrl` — يُحسم عند التنفيذ بأقل لمس ممكن (خاصية `fetch` اختيارية).

### ف8 — `DelegationStatusCard` تتضمن شريطًا يجلب من مسار محظور لكنه يفشل بصمت (طفيف)

- **الدليل:** `DelegationStatusCard.tsx:29` تُضمّن `DelegationActivityStrip` الذي يجلب
  `GET /alerts/by-delegation/{id}` (`DelegationActivityStrip.tsx:14–20`) — محظور على
  المندوب، لكن المكوّن يُرجع `null` عند الخطأ/الفراغ (سطر 22) فلا يكسر الصفحة.
- **الحكم:** تُبقى البطاقة كما هي (ق4) ويُقبل صمت الشريط للمندوب. لا نقطة بوابة للتنبيهات
  في هذه المهمة (التنبيهات أداة رئيس القسم الداخلية).

### ف9 — `OccurrencesCard` تُرجع `null` عند الفراغ التام (طفيف)

- **الدليل:** `OccurrencesCard.tsx:45–51` — بلا شطوبات/تغيّرات/استئنافات تُرجع `null`
  (لا بطاقة أصلًا)، بينما بقية البطاقات تعرض حالة فراغ نصية.
- **الحكم:** في البوابة يُغلَّف `OccurrencesCard` ببطاقة فراغ بديلة «لا توجد وقوعات
  أو استئنافات على هذا الملف» عند `null` — حتى لا يظن المندوب أن البطاقة سقطت خطأً.

### ف10 — منطق إخفاء «تشعبات الملف» عند الفراغ (توضيح سلوك — لا تغيير)

- **الدليل:** `DocumentView.tsx:176` (`showDelegationsCard = length > 0 || canCreate`) —
  للمندوب `canCreate=false` دائمًا، فالبطاقة تظهر **فقط عند وجود إنابات**، وتختفي تمامًا
  عند عدمها (وليست حالة فراغ). `SourceFileInfoCard` تظهر فقط للملف المناب.
- **الحكم:** تُطابَق «كما هي» (ق3): نفس الشرط في البوابة. يُوثَّق للمستخدم حتى لا يُعدّ
  الاختفاء خللًا.

### ف11 — بطاقة «الإجراءات التنفيذية»: لا عمود ولا هجرة — فلترة فقط (تأكيد معماري)

- **الدليل:** جدول واحد + حقل `Type` (§2-3). `ReminderDuration/ReminderColor` حقول
  داخلية للمحامي (تُدار من `ExecutionActionsModal` 197–246) ويجب **ألا تعبر الشبكة**
  للمندوب أصلًا (ق7: بلا تذكير) — الإسقاط في تعيين الـ `DTO` الخلفي لا في الواجهة فقط.
- **الحكم:** `PortalExecutionActionDto(Id, Text, ActionDate, CreatedByName, CreatedAt)` —
  بلا `Type` (مضمون) وبلا تذكير. الترتيب تنازلي (`CreatedAt` ثم `Id`)، والنص الغني يُعقَّم
  بـ `sanitizeRichText` نفسها، `ActionDate` نص حر يُعرض كما هو (قاعدة التواريخ الحرة).

### ف12 — عمود التصدير «الإجراءات والملاحظات» قد يسرّب ملاحظة للمندوب (حرج — مؤكد بالفحص)

- **الدليل:** `PortalRepository.ExportScopedAsync` يجلب `ExecutionActions` (`PortalRepository.cs:75`)
  لأن `ExcelExportService.BuildValues` يقرأ `doc.ExecutionActions.FirstOrDefault()?.Text`
  (`ExcelExportService.cs:113`) في عمود ثابت «الإجراءات والملاحظات» (`:88` — يُبنى دائمًا
  بلا شرط). و`DocumentResponse.FromEntity` يرتّب الإجراءات تنازليًا (`DocumentDtos.cs:911–915`)،
  فـ `FirstOrDefault()` = **الأحدث — وقد يكون `note`** (ملاحظة داخلية يجب حجبها عن المندوب
  بموجب ق7). أي ملفٍ أحدثُ سجل فيه ملاحظةٌ يُصدَّر نصُّها اليوم للمندوب.
- **الحكم:** إصلاح بوابة-فقط (بلا مساس بالتصدير الداخلي للمحامين): في
  `PortalService.ExportWorkbookAsync` تُرشَّح `ExecutionActions` لكل استجابة إلى
  `Type == "action"` **قبل** تمريرها إلى `BuildDocumentsWorkbook` (القائمة `settable` —
  `DocumentDtos.cs:732` — فيُبنى نسخة مُرشَّحة)، **وبكسر تعادل `Id` نفسه المعتمد في بطاقة
  التفاصيل** فيتطابق العمود والبطاقة حتميًا حتى عند تساوي اللحظة (مُثبَت باختبار
  `Export_TieBreakById_MatchesDetailCardOrder`). تغيير `ExcelExportService` المشترك
  **مرفوض** (سيغيّر سلوك التصدير الداخلي الذي يتوقع الملاحظات أيضًا). عنوان العمود يبقى
  كما هو (باني مشترك) ويُوثَّق أن محتواه في تصدير البوابة إجراءاتٌ فقط.
- **موثّق كسلوك قائم (تقرير المراجعة):** عمود «الفرع» (`BranchName`) يظهر في إكسل البوابة
  دائمًا بينما الواجهة تخفيه (`showBranch=false`) — سلوك التصدير السابق للمهمة، أُبقي كما
  هو بلا تغيير (أي محاذاة لاحقة قرار منتج مستقل).
- **الاختبار:** حالة `ExportWorkbookAsync` بملفٍ أحدثُ سجلاته `note` → خلية العمود = نص
  أحدث `action` (أو فارغة إن لا إجراءات)؛ وملف بلا إجراءات → خلية فارغة بلا خطأ.

### ف13 — الملف المشطوب: قائمةٌ تستبعده وتفاصيلٌ تفتحه (قرار منتج موثق — لا تغيير)

- **الدليل:** `PortalRepository.ScopedQuery` تستبعد المشطوب من القائمة/التصدير، بينما
  `IsDocumentInScopeAsync` (نطاق الأطراف ذو القيد النهائي وحده) **لا تستبعده** — فالمشطوب
  داخل النطاق يُفتح مباشرةً بالرابط رغم غيابه من القائمة.
- **الحكم (قرار منتج — التوصية 3):** الوصول المباشر للملف المشطوب داخل النطاق **مقصود**:
  المرآة تعرض الشطب أصلًا عبر «وقوعات الملف» وبطاقة الرأس، وحجبه إنما يحرم المندوب من ملفٍ
  صار رهنًا تاريخيًا يعنيه. ثُبّت هذا العقد باختبار
  (`GetFile_StruckOffInScope_ExcludedFromList_StillReturnsDetail`) — **مشدّد بعد المراجعة:**
  يضبط `ExecStatus = StateStruckOff` (الشطب الحقيقي الذي تستبعده القائمة، لا مجرد تاريخ
  الوقعة) ويثبت الغياب من `ListFilesAsync` مقابل بقاء `GetFileAsync`، مع ملفٍ شاهدٍ متداول.
  أي توحيد لاحق (استبعاد المشطوب من النطاق كليًا) قرار منتج بموافقة صريحة، لا تغيير
  بمبادرة مطوّر.

### ف14 — تجريد التذكير سلكيًا + إعادة محاولة الملف الرئيسي + إدارة التركيز (تقرير المراجعة)

- **تجريد التذكير:** مسار `GetFileAsync` كان يعبر بحقول `ReminderDuration/ReminderColor`
  الداخلية (عبر `ExecutionActionDto`) رغم أن ق7 «بلا شارة تذكير» — الواجهة تتجاهلها لكن
  السلك يحملها. تُجرَّد الآن (`a with { ReminderDuration = null, ReminderColor = null }`)
  بعد الترشيح والترتيب، ومُثبَتة باختبار (`GetFile_WireTrimmed_...`).
- **إعادة محاولة الملف الرئيسي:** شاشة خطأ الملف كانت رابط رجوع فقط بلا إعادة محاولة،
  بينما الأقسام الفرعية لها أشرطة — أُضيف زر «إعادة المحاولة» (`docQuery.refetch`) بجانب
  الرابط، ومُثبَت باختبار (فشل ثم نجاح يعرض البطاقات).
- **إدارة التركيز:** عند نجاح إعادة محاولة يدوية لقسم فرعي ينتقل التركيز إلى مجموعة
  القسم (`role="group"` + `tabIndex={-1}`) ليُعلن محتواه لقارئ الشاشة — يعمل فقط بعد نقرة
  (`retryingSection`) فلا يسرق التركيز عند التحميل الأولي؛ والشريط يبقى مثبتًا أثناء إعادة
  الجلب فلا يضيع التركيز عند فشلٍ متكرر. مُثبَت باختبار تركيز.

---

## 4) التصميم المستهدف (`to-be`)

### 4-1) الخلفية — 4 نقاط بوابة جديدة (قراءة فقط، scope-checked، `404` خارج النطاق)

| النقطة | الخدمة | الإرجاع | الملاحظة |
|---|---|---|---|
| `GET /portal/files/{id}/execution-actions` | `ListExecutionActionsAsync` | `PortalExecutionActionDto[]` (الأحدث أولًا، `action` فقط، بلا تذكير) | ف11 |
| `GET /portal/files/{id}/delegations` | `ListDelegationsAsync` | `DelegationDto[]` القائم (قراءة فقط) — بإعادة استعمال `IDocumentDelegationService.ListForDocumentAsync` (يتعامل مع المنيب/المناب تلقائيًا)، بلا تصفير (الأسماء ظاهرة بق9) | حقن الخدمة في `PortalService` (مسجلة في DI أصلًا) |
| `GET /portal/files/{id}/appeals/details` | `ListAppealDetailsAsync` | `AppealDto[]` بإعادة استعمال `IDocumentAppealService.ListForDocumentAsync` ثم تصفير `DefenseOpinion` وحده (ق10) — تُبقي `assignedLawyerName` وسطر «سطّره» (ق9/ق10) | تبقى `GET .../appeals` المختصرة للتوافق |
| `GET /portal/files/{id}/base-numbers` | `ListBaseNumbersAsync` | `BaseNumberHistoryDto[]` نفسها | ف7 |

- كل نقطة: فحص `IsInScopeAsync` أولًا → `null` يُترجم `404` (نمط `File`/`Appeals` القائم
  في `PortalController.cs:51–64`). لا `POST/PUT/DELETE`. لا تدقيق جديد (التدقيق مرة عند
  `GetFileAsync` — التعليق المرجعي `PortalService.cs:104`).
- الحارس بلا تغيير (يسمح بكل `‎/api/portal`). لا هجرات (لا تغيير أعمدة) — **لا تنبيه نشر
  لقواعد البيانات**؛ التنبيه الوحيد: نشر خلفية وواجهة معًا (عقود جديدة).
- التصدير: ترشيح `ExecutionActions` إلى `action` فقط داخل `ExportWorkbookAsync` قبل البناء (ف12).

### 4-2) الواجهة — خريطة إعادة الاستخدام (قرائي فقط)

| بطاقة البوابة | المكوّن المُعاد استخدامه | الممرّات | النافذة |
|---|---|---|---|
| أطراف الملف التنفيذي | `PartiesCard doc onOpen` | كما هي | `PartyDetailsModal` (من `doc` نفسها) |
| بيانات الملف | `FileDataCard` | `showBranch=false, showLawyer=true, isLawyer=false, canViewChanges=false` (المحامي ظاهر بق9؛ بلاطته تفتح التعاقب — ف3 محلولة بلا تغيير) | `BaseNumbersModal` (نقطة البوابة + خاصية `fetchUrl?`، ف7) + `TransferHistoryModal` من `doc.assignments` (ف5) |
| بيانات السند التنفيذي | `ExecutoryDocumentCard doc` | كما هي | — |
| الأموال المنقولة وغير المنقولة | `AssetsSection doc delegations` | الشارات تُحسب من الإنابات (تُجلب من نقطة البوابة) | — |
| تشعبات الملف | `DelegationsCard delegations canCreate=false currentUserId=undefined` | تُخفي التسطير/التعديل/الحذف آليًا (`DelegationsCard.tsx:72–84, 94`) | — (لا `DelegationFormModal`) |
| معلومات الملف المنيب | `SourceFileInfoCard delegation canRegister=undefined` | تُخفي «تسجيل أصولًا» آليًا (`:68`) | — (لا `RegisterDelegationModal`) |
| حالة الإنابة | `DelegationStatusCard doc delegationId` | كما هي؛ شريط التنبيهات يصمت للمندوب (ف8) | — |
| وقوعات الملف | `OccurrencesCard doc appeals onOpen onOpenAppeal` | `appeals` من نقطة التفاصيل (ف1)؛ تغليف فراغ بديل عند `null` (ف9) | `OccurrencesModal` (من `doc.occurrences`) + `AppealInfoModal` (المحامي ظاهر بق9؛ يُخفى رأيه `hideOpinion` — ف2) |
| الحالة | `StatusCard canChangeStatus=false canCompleteDelegation=false` | تُخفي الزرين آليًا (`StatusCard.tsx:33`) وتبقي الملخص | — (لا `StatusChangeModal/ExecutedStatusModal`) |
| **الإجراءات التنفيذية (جديدة)** | `components/portal/PortalExecutionActionsCard.tsx` (جديد) | `SectionCard` + نص معقّم + `actionDate` + `createdByName` + `tabular-nums`؛ فراغ «لا توجد إجراءات تنفيذية» | — |

**مستثنى صراحةً (لا يُبنى ولا يُستورد في صفحة البوابة):** `AppealFormModal` وقائمة «استئناف ▾»،
`DocumentGenerationModal`، `ExecutionActionsModal`، `StatusChangeModal`/`ExecutedStatusModal`،
`TransferDocumentModal`، `FileAlertModal`، `DocumentReviewLettersCard`، `DocumentChangesModal`،
`DelegationFormModal`/`RegisterDelegationModal`/`CompleteDelegationModal` وحوار حذف الإنابة،
رابط `‎/documents/:id/edit`.

**التخطيط:** مرآة `DocumentView` — تبويبات (`useIsMobile`) على الجوال وأعمدة
`grid md:grid-cols-3` على المكتبي، حاوية `max-w-6xl` (بدل `max-w-3xl` الحالية)، `min-h-11`
لكل تفاعلي، `aria-label` للحوارات، `focus-visible:ring-*`، `overscroll-behavior: contain`،
`truncate/break-words` للنصوص الطويلة، أرقام `tabular-nums`.

### 4-3) البدائل المدروسة والمرفوضة (ولماذا)

1. **نقطة بوابة مركّبة واحدة تُرجع كل شيء:** مرفوضة — تكسر نمط النقاط المنفصلة القائم
   (`appeals` منفصلة أصلًا)، وتُضخّم استجابة الملف، وتُصعّب التخزين المؤقت والاختبار.
2. **تقليم `DocumentResponse` خلفيًا لعقد بوابة مقلّص:** مرفوض لهذه المهمة — عقد مستقر
   تستهلكه 4 نقاط (`files`, `export`, قائمة، تفاصيل)؛ التقليم الشامل مشروع مستقل (دَين ف6).
3. **إعادة استخدام `ExecutionActionDto` مباشرة للمندوب:** مرفوض — تُسرّب `ReminderDuration/
   ReminderColor` و`Type=note` عبر الشبكة (ف11)؛ الـ `DTO` المقلّص إلزامي.
4. **إظهار سجل التعديلات للمندوب:** مرفوض (ق2) — أداة مراجعة داخلية (`FileDataCard:24–25`
   تحصرها بصاحب الملف ورئيس القسم والإدارة).
5. **صفحة قائمة استئنافات مستقلة:** مرفوضة (ق8) — بطاقة التفاصيل فقط.

---

## 5) خطة التنفيذ (بعد الاعتماد — بالترتيب)

### المرحلة أ — الخلفية

1. `EntityPortalDtos.cs`: إضافة `PortalExecutionActionDto` (توثيق: بلا تذكير، `action` فقط).
2. `IPortalService` + `PortalService`: أربع دوال §4-1 (حقن `IDocumentAppealService`
   و`IDocumentDelegationService` المسجلتين في DI أصلًا + `IRepository<Document>` القائم للإجراءات
   وأرقام الأساس)؛ كل دالة تبدأ بفحص النطاق؛ تصفير `DefenseOpinion` وحده في `AppealDto` (ف2/ق10)؛
   فلترة `action` وترتيب تنازلي (ف11)؛ ترشيح `ExecutionActions` في `ExportWorkbookAsync` (ف12).
3. `PortalController`: أربع نقاط `GET` (توثيق `404` خارج النطاق؛ لا كتابة).
4. اختبارات خلفية: `PortalServiceTests` (النطاق، فلترة `action`، غياب التذكير، الترتيب، تصفير
   `DefenseOpinion` مع بقاء `assignedLawyerName`، ترشيح التصدير ف12، `404` خارج النطاق لكل نقطة)
   + `PortalControllerTests` + تحديث `EntityManagerPortalGuardTests` (النقاط الجديدة تمر، والداخلية `403`).

### المرحلة ب — الواجهة

5. `types/index.ts`: `PortalExecutionActionDto` + أنواع النقاط الجديدة.
6. `components/portal/PortalExecutionActionsCard.tsx` (جديد) + اختباره (فلترة الملاحظات
   ضمنيًا، غياب التذكير، الترتيب، الفراغ، التعقيم).
7. `PortalFileDetail.tsx`: إعادة كتابة العرض (§4-2) + `normalizeDocumentResponse` للاستجابة
   + خاصية `hideOpinion` في `AppealDetailsBody` (ف2) + خاصية `fetchUrl?` في `BaseNumbersModal`
   (ف7) + تغليف فراغ الوقوعات (ف9). بلا تغيير في `FileDataCard` (ف3/ف4 محلولتان بق9).
8. اختبارات واجهة: توسيع `PortalFileDetail.test.tsx` (البطاقات السبع تظهر؛ وغياب تام لنصوص:
   «تسطير إنابة»، «استئناف ▾»، «توليد مستندات»، «الإجراءات والملاحظات»، «تغيير الحالة»،
   «كتب المطالعة»، «سجل التعديلات»، «تسجيل أصولًا»، «إتمام الإنابة») + اختبار المكوّن الجديد.

### المرحلة ج — التحقق الإلزامي (قبل إعلان الإنجاز)

9. `dotnet test` (خلفية) ثم `npx oxlint src` و`npx tsc -b` و`npx vitest run` و`npm run build`
   (واجهة) — كلها خضراء.
10. مراجعة المسار الكامل: `PortalFileDetail → Portal*Dto → PortalService → ScopePredicate →
    DocumentResponse/AppealDto/DelegationDto` حقلًا بحقل (تواريخ حرة `string?`، `CreatedByName`)،
    + `grep` لبقايا الأسماء، + فحص جوال بصري 375px (تجاوز أفقي/قص/تنقّل).
11. تقرير الإنجاز يذكر: لا هجرات (لا تنبيه `database update`)، الاستثناء المقصود ف5، والدَين
    الاختياري المتبقي ف6 (تقليم شمولي مؤجل — الملاحظات مغلقة سلكيًا في `GetFileAsync` الآن)،
    وقرار ف13 (المشطوب) — وينشر خلفية وواجهة معًا.

---

## 6) معايير القبول

- [ ] المندوب يرى البطاقات السبع + «الإجراءات التنفيذية» (نص + تاريخ + محامي، `action` فقط،
  بلا تذكير، الأحدث أولًا) + نوافذ (المنفذ عليهم، أرقام الأساس، التعاقب، تفاصيل الوقوعات،
  تفاصيل كل استئناف مع اسم المحامي وسطر «سطّره» وبلا رأي المحامي — ق9/ق10).
- [ ] غياب تام لكل الأزرار/البطاقات المستثناة (ق6) — يُثبَت باختبار نصوص سالبة.
- [ ] خارج النطاق → `404` لكل نقطة جديدة؛ ودور المندوب خارج `‎/api/portal` → `403`.
- [ ] تصدير البوابة: ملفٌ أحدثُ سجلاته `note` يُصدَّر بنص أحدث `action` (لا الملاحظة) — مُثبَت باختبار (ف12).
- [ ] كل أوامر التحقق خضراء (§5-9) وفحص الجوال 375px سليم.
