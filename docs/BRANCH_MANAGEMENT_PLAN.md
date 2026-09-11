# خطة إدارة الفروع لرؤساء الأقسام + اقتراحات الجهة الأم (`BRANCH_MANAGEMENT_PLAN`)

> الحالة: خطة معتمدة — **لا تنفيذ حتى طلب صريح**.
> النطاق: رؤساء الأقسام في فروع الإدارة فقط (`head`) + تبويب اقتراحات للمدير/المشرف (`manager`/`admin`).
> المراجع: `ENTITY_REVIEW_RESTRUCTURE_PLAN.md`، `docs/ENTITY_MERGE_MOVE_SPEC.md`، `docs/ENTITY_UNIFY_SPEC.md`.

## 0. القرارات المعتمدة

| الرمز | القرار |
|---|---|
| `S1` | الجهة الأم (`IsParentEntity=true`): **منع + اقتراح فقط** لرئيس القسم |
| `S2` | عمليات الفروع: **بدون مرسوم + حدث** (`ChangeEvent` + وقوعات + تنبيه فرعي) |
| `S3` | الواجهة: **توسيع `BranchManagementModal`** (غير موصول حاليًا بأي صفحة — يُوصَل فعليًا) لا صفحة جديدة |
| `S4` | إلغاء فرع له روابط: **ممنوع بلا هدف**؛ مسموح مباشر فقط لصفر روابط |
| `S5` | التوحيد = **دمج N←1 + تصحيح اسم الناجي** في معاملة واحدة + تأكيد كتابة الاسم |
| `S6` | الرئيس يرى **أحداث محافظته** (جبر خادمي) + **تبويب اقتراحات** للمدير (قبول/رفض) |
| `S7` | تعديلات الفروع (رئيس/مدير/مشرف) **تُحدّث كل الملفات التنفيذية بلا استثناء**: النشطة والمشطوبة والتريث والاستئنافات |

## 1. حقائق الكود الحاكمة (مُتحقق منها سطرًا)

- `F1`: نص الطالب/البحث **لا يضم الفرع** (`ApplicantTextBuilder.cs:13`، `DocumentSearchTextBuilder.cs:45`)
  — الفرع لقطة نصية على صفوف الروابط (`ApplicantPublicEntity.Branch`، `ExecutedPublicEntity.EntityBranch`).
  الدليل: `DocumentForm.tsx:326,342` يأخذ أسماء الفروع من `entry.branchName`.
- `F2`: `POST /{id}/move {targetEntryId}` يشترط تطابق المحافظة **والفرع**
  (`PublicEntityService.cs:1403`) — دمج فرعين مختلفين **يفشل حتمًا** عبره، والمسار الجديد ضروري.
- `F3`: `UpdateAsync:586-592` يسمح للرئيس بتعديل الأم **إذا انحصرت المجموعة في محافظته** — ثغرة تُغلق.
- `F4`: `ListEntriesByGroupAsync:286-294` يستبعد الأم من قائمة الرئيس إذا اختلفت محافظتها المخزنة.
- `F5`: `BranchManagementModal` **غير مستورَد** في أي صفحة — كود ميت (مرجع وحيد: ملف اختباره).
- `F6`: ترحيل المندوبين القيدي نمطه في `MoveEntryAsync:1419-1424`
  (`ListEntityManagersByEntryIdAsync`) — لا `MigrateDelegatesAsync` (مجموعات).
- `F7`: `IsParentEntity` مكشوف في الـ `DTO` (`ToEntryDto:2464`) — البطاقة read-only ممكنة بلا عقد جديد.

## 2. الخلفية (بلا كسر عقود)

### 2.1 كيان جديد وحيد + هجرتان

- `ParentEditSuggestion { Id, GroupId, EntryId, ProposedCanonicalName?, ProposedEntityType?, ProposedCitationFormula?, Reason*, Status(pending/approved/rejected/withdrawn), CreatedById, CreatedBranchId, ReviewedById?, ReviewReason?, CreatedAtUtc, ReviewedAtUtc? }`
  عبر `IRepository<ParentEditSuggestion>` القائم.
- هجرتان: `Migrations/*_AddParentEditSuggestion` + `MigrationsPostgres/*_AddParentEditSuggestion`
  → تنبيه نشر `dotnet ef database update --context DocGeneratorDbContext` و`--context DocGeneratorPostgresDbContext` (`RUN_GUIDE.md` §9).

### 2.2 مساعدان مشتركان (يُبنيان أولًا)

- `SyncBranchLabelsAsync(entryId, newBranch, token)`: لكل مستندات
  `ListDocumentsLinkedToEntryAsync(entryId)` حدّث `Branch`/`EntityBranch` للصفوف ذات
  `RegistryId==entryId` — **بلا إعادة بناء `SearchText`** (`F1`) — ويشمل المشطوبة والتريث (`S7`)؛ ثم
  `SyncAppealsAfterEntityChangeAsync` للاستئنافات (`S7`).
- ترحيل مندوبي القيد بنمط `F6` (حلقة `PortalEntryId: source→target`).

### 2.3 أربع عمليات فروع

| العملية | المسار | الشروط الصلبة | الأثر |
|---|---|---|---|
| تعديل تسمية فرع | `POST groups/{g}/branches/{e}/rename-branch {newBranchName, coverageLabel?}` | غير أم (حارس §2.5)؛ ضمن نطاق الرئيس؛ لا تصادم (`EntryExistsAsync`)؛ تُغلق المراجعة كـ `Update:636` | `SyncBranchLabelsAsync` + `Alias` للقديم + حدث `rename` + وقوعات + تنبيه فرعي |
| دمج فرعين | `POST groups/{g}/branches/merge {sourceId, targetId}` | نفس `Group`+المحافظة؛ فرعان مختلفان؛ نشطان؛ بلا `NeedsReview`؛ كلاهما ضمن النطاق (حلقة `EnsureHeadScopeAsync`) | `RepointEntryLinks` + `AddFoldAliases` + ترحيل مندوبين + `IsActive=false` للمصدر + حدث `merge` + وقوعات + تنبيه فرعي |
| إلغاء فرع | `POST groups/{g}/branches/{e}/abolish {targetId?}` | روابط>0 بلا هدف → رفض (`S4`)؛ ممنوع الأم/آخر نشط؛ بلا `NeedsReview` | مع هدف = دمج ضمني (حدث `merge`)؛ بلا هدف = تعطيل + حدث `abolish` + وقوعات + تنبيه فرعي |
| توحيد تسميات | `POST groups/{g}/branches/unify {targetId, absorbedIds[], correctedName?}` + `preview` موحد | N≥1؛ تصحيح الاسم بلا تصادم؛ نفس شروط الدمج | دمج جماعي + تصحيح الناجي عبر `SyncBranchLabelsAsync` + `Alias` لكل كتابة + حدث `unify` + وقوعات + تأكيد كتابة الاسم |

كلها داخل `_tx.RunAsync` + حدث في نفس المعاملة، وإعادة استخدام `ActionKindCatalog` الحالي بلا قيم جديدة.
الحراسة في المتحكم: `CanManageEntityRegistry` (لا `HasFullAccess` ولا `CanMergeEntities` —
دمج الفروع `Entry→Entry` داخل نفس الهوية وليس `N Groups→1`).

### 2.4 اقتراح الأم + الأحداث

- `POST /{id}/suggest-parent-edit {proposedCanonicalName?, proposedEntityType?, proposedCitationFormula?, reason*}`
  → فحص: القيد أم + المجموعة نشطة + للمجموعة فرع نشط في محافظة الرئيس + لا معلّق مكرر
  (`GroupId`×فرع الرئيس) + الاسم المقترح مختلف وفريد — **بلا أي كتابة على الكيان**
  (صف + حدث `propose` فقط — بلا تنبيه للمديرين، التبويب يكفي) + سحب ذاتي ما دام معلّقًا.
- `POST suggestions/{id}/review` = مراجعة من الإدارة (الاسم يجب أن يكون قد طُبّق مسبقًا) ثم
  تعليم `approved`؛ `reject {reason*}` → `rejected` + تنبيه للرئيس.
- `GET /change-events` (+ التصدير): التوقيع يضيف `Actor`؛ الرئيس تُجبَر محافظته خادميًا ويُتجاهَل پارامتر
  العميل؛ المدير بلا تغيير. ملاحظة: `PayloadJson` لأحداث المجموعات قد يذكر فروع محافظات أخرى —
  عرض قراءة فقط (مقبول).

### 2.5 حراسان إلزاميان

- حارس الأم: رئيس + `IsParentEntity` → `UnauthorizedAccessException` مع إرشاد للاقتراح —
  في `UpdateAsync` وكل عمليات الفروع الجديدة (`F3`).
- `ListEntriesByGroupAsync` تُدرج الأم دائمًا للرئيس
  (`e.IsParentEntity || e.Governorate == gov`) (`F4`).

## 3. الواجهة

- `EntityRegistryReview.tsx`: زر «إدارة فروع جهة في محافظتي» → بحث `GET /groups` (النطاق خادمي) →
  فتح `BranchManagementModal` الموسّع (بطاقة أم read-only + زر اقتراح + حالة المعلّق + أقسام
  تعديل/دمج/إلغاء/توحيد بمعاينة وتأكيد كتابة) + زر «سجل تغييرات محافظتي» (قراءة).
- `EntityRegistryReviewManagement.tsx`: تبويب خامس «اقتراحات الأم» (قبول→rename مسبق/رفض بسبب)
  بجانب `edit/add/unify/log`.
- أنواع `types/index.ts` الجديدة بجانب القائمة؛ التزام `AGENTS.md`: `mobile-first`، `min-h-11`،
  `focus-visible`، `aria-label`، بطاقات جوال، `Intl.NumberFormat`، `…`.

## 4. الاختبارات والتحقق الإلزامي (تعريف «منجز»)

- `PublicEntityServiceTests`: حارس الأم؛ دمج فرعين (نجاح + رفض عبر مجموعتين/محافظتين + رفض ذات +
  رفض `NeedsReview`)؛ توحيد N←1 مع تصحيح؛ إلغاء (صفر روابط مباشر / روابط بلا هدف مرفوض / بهدف مدمج)؛
  تحديث لقطات `Branch` في المشطوبة والتريث والاستئنافات (`S7`)؛ اقتراح (بلا مساس بالكيان + منع
  المكرر + سحب)؛ عزل البوابة.
- `EntityRegistryPermissionsTests`: فروع للرئيس ضمن محافظته فقط + `change-events` مجبورة + عمليات
  المجموعات تبقى `HasFullAccess`.
- `BranchManagementModal.test` (توسيع): أم read-only + اقتراح + منع هدف=مصدر + ملخص معاينة + تأكيد
  كتابة + تبويب اقتراحات المدير.
- أوامر: `dotnet test` + `npx oxlint src` + `npx tsc -b` + `npx vitest run` + `npm run build` —
  كلها خضراء + مراجعة مسار كامل + `grep` بقايا أسماء.

## 5. مخاطر معلنة

- `R1`: تكرار نصي إذا كان المصدران مربوطين بنفس الملف (موروث من مسار الطيّ — مقبول).
- `R2`: قرار `S7` (تحديث التاريخي) فلسفة المشروع القائمة — أي تغيير لاحق يتطلب نقاشًا قانونيًا منفصلًا.
