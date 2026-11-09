# خطة تنفيذ: اسم الملف في التذكيرات، حرية الاسم الاعتباري، وتنبيه إعادة الإنابة للتصحيح

> **التاريخ:** 2026-11-09  
> **الحالة:** خطة معتمدة للتنفيذ — لم يُعدَّل أي كود بعد  
> **الهجرات:** **صفر** (لا تغيير مخطط قاعدة بيانات)

---

## 0) ملخص تنفيذي

تتناول الخطة ثلاث مشكلات منفصلة، وكلها قابلة للتنفيذ دون هجرات:

1. **تذكيرات الملفات والاستئنافات:** تذكير الاستئناف يعرض نوع الاستئناف واتجاهه فقط
   (`استئناف قرار رئيس التنفيذ — مستأنِفين`) ولا يعرض اسم الملف. الحل: إضافة `DocumentLabel`
   محسوب مركزيًا من أطراف الملف، وعرضه أولًا في الواجهة.
2. **الشخص الاعتباري في «الجهة العامة منفذ عليها» و«عرض وايداع»:** الفحص يثبت أن
   **المنفذ عليه الاعتباري** (`ExecutedPublicEntity` بطبيعة `legal`) حر بالفعل، لكن
   **طالب التنفيذ/طالب العرض الاعتباري** (`ExecutionApplicant` بطبيعة `legal`) مقفول
   على السجل المرجعي ويُمنع حفظه بلا `registryId`. الحل: جعله حقلًا حرًا مع ربط اختياري
   بالسجل، وإزالة منع الحفظ.
3. **رفض الإنابة وإعادتها للتصحيح:** التنبيه موجود لكن نصه لا يذكر اسم الملف ولا الدائرة،
   والمستلم يُحسب عبر `DocumentCreator` بدل تثبيت كاتب الإنابة صراحةً. الحل: توجيه التنبيه
   إلى `delegation.CreatedById`، وتغيير النص إلى صيغة «يرجى تصحيح الإنابة … في ملف …».

---

## 1) نتائج الفحص (حقائق من الكود الحالي)

### 1-1) تذكيرات الاستئناف

- `DocumentAppealService.GetRemindersAsync` يبني `AppealReminderDto` من
  `ListReminderRowsAsync`.
- `AppealTitleOf` في `DocumentAppealService.cs` (نحو السطر 1445) تُرجع حرفيًا:
  `استئناف قرار رئيس التنفيذ — {AppealDirectionCatalog.ToLabel(...)}` — **بلا اسم الملف**.
- `AppealReminderDto` (`AppealDtos.cs` نحو السطر 123) لا يحمل حقل اسم ملف.
- الواجهة `ReminderList.tsx` و`DayRemindersModal.tsx` تعرضان `appealTitle` كما هو.
- `DocumentTitle` الحالية في `DocumentAppealService.cs` تفضّل الاسم الثلاثي للمقترض، ثم
  أول منفذ عليه طبيعي، ثم نوع المستند — ولا تغطي المنفذ عليه الاعتباري (`ExecutedPublicEntity`)
  ولا طالب التنفيذ الاعتباري، كما أن `AppealRepository.WithIncludes` لا يحمّل مجموعات
  الأطراف أصلًا.
- تذكيرات الملفات (`ReminderDto`) تعتمد على `borrowerName/Father/Family` فقط؛ ولذلك
  ملفات «منفذ عليها» تظهر بنوع المستند (مثل «متداول») لا باسم الجهة/الشخص.

### 1-2) الشخص الاعتباري في وضعي «منفذ عليها» و«عرض وايداع»

- `ExecutedSideSections.tsx`:
  - قسم **المنفذ عليه** (`eIsLegal`) يعرض حقل `entityName` **حرًا** بالفعل (سطر 446 وما بعده).
  - قسم **طالب التنفيذ/طالب العرض** (`aIsLegal`) يعرض `name` بـ`readOnly` + رسالة
    «يجب اختيار هذا الطلب من السجل المرجعي» + زر اختيار من السجل (سطر 248 وما بعده).
- `DocumentForm.tsx`:
  - يمنع الحفظ إذا وُجد طالب تنفيذ اعتباري بلا `registryId` (سطر 851–856).
  - `setApplicant` يفكّ الربط تلقائيًا عند تعديل `name` (سطر 424–438)، لكن الحقل مقفول
    حاليًا فلا يحدث التحرير أصلًا.
- الخلفية `NormalizeExecutionApplicants` تقبل `RegistryId = isLegal ? a.RegistryId : null`
  — أي أن **الخلفية تدعم الاسم الحر أصلًا**، والمشكلة واجهة أمامية فقط.
- الخلفية `NormalizeExecutedPublicEntities` تصفّر `RegistryId` عند `legal` — أي أن
  المنفذ عليه الاعتباري حر خلفيًا أيضًا.

> **تنبيه دقة:** إن كان المقصود حرفيًا «المنفذ عليه» فهو حر في الكود الحالي، وستُثبَّت
> هذه الحقيقة باختبار انحدار. القفل الفعلي الذي يطابق وصف «شركة خاصة وليست جهة عامة»
> هو على **طالب التنفيذ/طالب العرض الاعتباري**.
>
> **تعديل قرار المستخدم (2026-11-09):** لا ربط بالسجل إطلاقًا لحقل اسم الشخص الاعتباري؛
> لذلك لن يبقى «ربط اختياري» في الواجهة، وسيُزال `RegistryId` من عقد `ExecutionApplicantDto`
> ومن منطق الخلفية، مع إبقاء عمود `ExecutionApplicants.RegistryId` في قاعدة البيانات
> كعمود تاريخي غير مستخدم (بلا هجرة حذف).

### 1-3) رفض الإنابة وإعادتها للتصحيح

- `DocumentDelegationService.RejectAsync` (نحو السطر 835) ينشئ تنبيهًا من نوع
  `DelegationRejectedWrongCourt` بجمهور `HeadAlertAudience.DocumentCreator`.
- `HeadAlertMessages.DelegationRejectedWrongCourt` (سطر 96) نصه الحالي:
  `رُفضت الإنابة رقم {id} للدائرة الخطأ — {reason} — صحّح الدائرة من بطاقة تشعبات الملف`
  — **لا يذكر اسم الملف ولا الدائرة**.
- `delegation.CreatedById` هو كاتب الإنابة. `CreateAsync` يشترط أن يكون منشئ الملف
  (`source.CreatedById == userId`)، لكن التوجيه الحالي يعتمد على `DocumentCreator`
  (منشئ الملف) بدل تثبيت كاتب الإنابة صراحةً.
- بطاقة `DelegationDetails` التي يراها المحامي **لا تعرض** `rejectReason`؛ العرض الحالي
  موجود فقط في صفحة رئيس القسم `DelegationRequests.tsx`.

---

## 2) القرارات التصميمية

| الرمز | القرار | السبب |
|---|---|---|
| `D1` | إضافة `DocumentLabel` إلى `ReminderDto` و`AppealReminderDto` بدل حشره داخل `AppealTitle` | فصل «اسم الملف» عن «نوع الاستئناف/اتجاهه»، فيبقى `AppealTitle` دلاليًا وتتعدد طرق العرض بأمان |
| `D2` | إنشاء `DocumentDisplayName` مركزية في `Application/Common` | مصدر واحد لاسم الملف يعوّض تكرار المنطق في `DocumentTitle` و`StatisticsRepository` والواجهة |
| `D3` | **إزالة ربط طالب التنفيذ/طالب العرض الاعتباري بالسجل نهائيًا** (لا زر اختيار، لا `RegistryId` في العقد، لا منع حفظ)، مع إبقاء عمود قاعدة البيانات `legacy` فقط | قرار المستخدم الصريح: «لا أريد ربط حقل اسم الشخص الاعتباري بالسجل إطلاقًا»؛ إبقاء العمود يمنع هجرة حذف كاسرة ويحفظ البيانات التاريخية دون استخدامها |
| `D4` | توجيه تنبيه الرفض إلى `delegation.CreatedById` صراحةً بجمهور `SingleLawyer` | يطابق «المحامي الذي سطّر الإنابة» بدل الاعتماد على اشتقاق `DocumentCreator` |
| `D5` | إظهار سبب الإعادة داخل بطاقة تشعبات الملف أيضًا | التنبيه قد يُفوَّت؛ البطاقة تضمن رؤية السبب عند فتح الملف |

---

## 3) خطة التنفيذ التفصيلية

### المرحلة أ — اسم الملف في تذكيرات الملفات والاستئنافات

#### أ-1) الخلفية: مساعد اسم العرض المركزي

**ملف جديد:** `backend/src/DocGenerator.Application/Common/DocumentDisplayName.cs`

```csharp
public static class DocumentDisplayName
{
    public static string Build(Document doc);
}
```

قواعد البناء:

- **عائلة «منفذ عليه/عرض وايداع»** (`GeneralEntitySideCatalog.IsExecutedLike`):
  1. أول منفذ عليه طبيعي باسم ثلاثي غير فارغ (`Name/Father/Family`).
  2. أول منفذ عليه اعتباري باسم غير فارغ (`ExecutedPublicEntities.EntityName`).
  3. أول طالب تنفيذ/عرض: إن كانت طبيعته `legal` فاسمه `Name`، وإلا فالاسم الثلاثي.
  4. `doc.Applicant` إن وُجد.
  5. `GeneralEntitySideCatalog.ToLabel(doc.GeneralEntitySide)` ثم `ملف رقم {doc.Id}`.
- **عائلة «طالبة تنفيذ»**:
  1. الاسم الثلاثي للمقترض (`BorrowerName/Father/Family`).
  2. `doc.Applicant`.
  3. `doc.DocumentType` إن كان ذا معنى.
  4. `ملف رقم {doc.Id}`.

كل الأجزاء تُقصّ (`Trim`) وتُدمج بمسافة واحدة، والفارغ يُتجاهل.

#### أ-2) الخلفية: الاستئنافات

1. **`AppealRepository.WithIncludes`** (`AppealRepository.cs` نحو السطر 208):
   - إضافة تحميل مجموعات الأطراف:
     - `Document.ExecutedNaturalPersons`
     - `Document.ExecutedPublicEntities`
     - `Document.ExecutionApplicants`
   - إضافة `.AsSplitQuery()` لتجنب تضخم الضرب الديكارتي مع `Actions` و`BaseNumbers`.
   - يُطبَّق على كل مسارات `ToDto` (التفاصيل، القائمة، التذكيرات) ليتسق `DocumentLabel`.
2. **`DocumentAppealService.DocumentTitle`** (نحو السطر 1434):
   - استبدال الجسم بـ`DocumentDisplayName.Build(doc)`.
   - يبقى `AppealDto.DocumentLabel` كما هو في العقد لكن بقيمة صحيحة لكل الأوضاع.
3. **`AppealReminderDto`** (`AppealDtos.cs` نحو السطر 123):
   - إضافة `string? DocumentLabel` في نهاية السجل.
4. **`DocumentAppealService.GetRemindersAsync`**:
   - حساب `DocumentLabel` لكل صف عبر `DocumentDisplayName.Build(appeal.Document)`.
   - تمريره إلى `new AppealReminderDto(...)`.
   - يبقى `AppealTitle` كما هو (نوع الاستئناف + الاتجاه).

#### أ-3) الخلفية: تذكيرات الملفات

1. **`ReminderDto`** (`StatsDtos.cs` نحو السطر 40):
   - إضافة `string? DocumentLabel` في نهاية السجل.
2. **`StatisticsRepository.GetRemindersAsync`** (نحو السطر 844):
   - بعد جلب صفوف الإجراءات، جمع `DocumentId` الفريدة.
   - استعلام ثانٍ للوثائق:
     ```csharp
     _db.Documents.AsNoTracking()
        .Where(d => ids.Contains(d.Id))
        .Include(d => d.ExecutedNaturalPersons)
        .Include(d => d.ExecutedPublicEntities)
        .Include(d => d.ExecutionApplicants)
        .AsSplitQuery()
     ```
   - بناء قاموس `documentId → DocumentDisplayName.Build(doc)`.
   - تمرير `DocumentLabel` في `new ReminderDto(...)`.

#### أ-4) الواجهة

1. **`frontend/src/types/management.ts`**:
   - `ReminderDto`: إضافة `documentLabel?: string | null`.
   - `AppealReminderDto`: إضافة `documentLabel?: string | null`.
2. **`frontend/src/components/dashboard/dashboardFormat.ts`**:
   - تعديل `borrowerFullName` لتفضّل `r.documentLabel` ثم الاسم الثلاثي ثم `documentType`.
   - إضافة مساعد:
     ```ts
     export function appealReminderTitle(r: AppealReminderDto): string {
       const label = r.documentLabel?.trim();
       return label ? `${label} — ${r.appealTitle}` : r.appealTitle;
     }
     ```
3. **`ReminderList.tsx`**:
   - سطر الاستئناف يستخدم `appealReminderTitle(r)` بدل `r.appealTitle`.
   - سطر الملف يبقى `borrowerFullName(r)` بعد تعديله.
4. **`DayRemindersModal.tsx`**:
   - عنوان الاستئناف يستخدم `appealReminderTitle(r)`.
   - إضافة `documentLabel` إلى نص البحث (`haystack`).

#### أ-5) الاختبارات

**خلفية:**

- ملف جديد `DocumentDisplayNameTests.cs`:
  - طالبة تنفيذ باسم ثلاثي.
  - منفذ عليه طبيعي.
  - منفذ عليه اعتباري (`EntityName = "شركة بركات التجارية"`).
  - طالب تنفيذ اعتباري حر.
  - احتياط اسم الصفة/رقم الملف.
- `DocumentAppealServiceTests`:
  - استئناف على ملف «منفذ عليه» باسم جهة اعتبارية ⇒ `AppealReminderDto.DocumentLabel` يحتوي الاسم.
  - استئناف على ملف «طالبة تنفيذ» ⇒ الاسم الثلاثي.
- `StatisticsTests` أو اختبار مستودع مخصص:
  - `ReminderDto.DocumentLabel` لملف «منفذ عليه» لا يساوي نوع المستند.

**واجهة:**

- `ReminderList.test.tsx`: تذكير استئناف بـ`documentLabel` يعرض اسم الملف أولًا.
- `DayRemindersModal.test.tsx`: البحث باسم الملف يجد تذكير الاستئناف.
- `AppealIntegration.test.tsx`: تحديث الـmock وإضافة تأكيد ظهور الاسم.

---

### المرحلة ب — إزالة ربط اسم الشخص الاعتباري بالسجل نهائيًا

#### ب-1) الواجهة: `ExecutedSideSections.tsx`

في فرع `aIsLegal` (طالب التنفيذ/طالب العرض):

1. جعل حقل الاسم **حرًا بالكامل**:
   - إزالة `readOnly`.
   - إزالة `bg-gray-50` و`cursor-not-allowed`.
   - `placeholder="اسم الشخص الاعتباري (شركة خاصة)…"`.
2. حذف كل ما يخص السجل من هذا الفرع:
   - رسالة `يجب اختيار هذا الطلب من السجل المرجعي`.
   - شارة `مرتبطة بالسجل ✓`.
   - زر `اختيار من السجل…` / `تغيير من السجل…`.
   - زر `فك الربط` وزر `مسح`.
3. حذف الخاصية `onPickExecutionApplicantRegistry` و`onApplicantUnlink` من
   `ExecutedSideSectionsProps` ومن نداء المكوّن في `DocumentForm.tsx`.
4. إبقاء حقل `nature` كما هو؛ لا يلزم تعطيله بسبب `registryId` بعد إزالة الربط.

#### ب-2) الواجهة: `DocumentForm.tsx`

1. حذف كتلة منع الحفظ الخاصة بطالب التنفيذ الاعتباري (نحو السطر 851–856).
2. حذف `'execution-applicant'` من نوع `registryPicker` ومن فرع `applyRegistryPick`.
3. حذف `EXECUTION_APPLICANT_IDENTITY_KEYS` ومنطق `registryId` من `setApplicant`
   (يصبح `setApplicant` تحديثًا مباشرًا للحقل).
4. حذف `unlinkExecutionApplicant` ومن تمريره إلى `ExecutedSideSections`.
5. لا تغيير في بناء الحمولة: `...a` يرسل الاسم والحقول الاعتبارية فقط.

#### ب-3) العقد والخلفية (إزالة الربط من الجذر)

1. **`ExecutionApplicantDto`** (`DocumentPartyDtos.cs`):
   - حذف `int? RegistryId = null` نهائيًا.
   - تحديث التعليق: «الاسم حر؛ لا ربط سجلي لطالب التنفيذ/العرض الاعتباري».
2. **`DocumentDtos.cs`**:
   - حذف تمرير `a.RegistryId` في تحويل `ExecutionApplicantDto`.
3. **`DocumentService.NormalizeExecutionApplicants`**:
   - حذف `RegistryId = isLegal ? a.RegistryId : null` (يبقى `null` افتراضيًا).
   - تعليق صريح: «الربط السجلي لطالب التنفيذ الاعتباري أُلغي بقرار المالك».
4. **`ExecutionApplicant`** (الكيان):
   - إبقاء الخاصية `RegistryId` مع تعليق `legacy` لأن العمود قائم في القاعدة
     (لا هجرة حذف)، لكن لا يكتبها/يقرأها منطق التطبيق.
5. **`PublicEntityService.cs`**:
   - حذف `RepointEntryLinks` الخاص بـ`ExecutionApplicants` (السطر 415).
   - حذف كتلة مزامنة إعادة التسمية الخاصة بـ`ExecutionApplicants` (نحو 197–232)
     وكل استدعاءات `ListDistinctExecutionApplicantTextsAsync` /
     `ListExecutionApplicantRowsByNamesAsync`.
   - حذف حلقة `ExecutionApplicants.Where(RegistryId.HasValue)` من
     `SyncAppealsAfterEntityChangeAsync` (نحو 519–523).
6. **`PublicEntityService.Groups.cs`**:
   - حذف حلقة `ExecutionApplicants.Where(RegistryId == entry.Id)` (نحو 282).
7. **`PublicEntityRepository` + `IPublicEntityRepository`**:
   - حذف `ListDistinctExecutionApplicantTextsAsync`.
   - حذف `ListExecutionApplicantRowsByNamesAsync`.
   - حذف فرع `ExecutionApplicants` من `ListDocumentsLinkedToEntryAsync`.
8. **`PortalRepository`**:
   - حذف شرط `ExecutionApplicants.RegistryId` من `ScopePredicate` (نحو 38–42).
   - حذف `executionApplicantPairs` من إحصاءات الربط (نحو 323–335).
   - الإبقاء على استخدامات `ExecutionApplicants` الأخرى للعرض/الأسماء فقط.
9. **`DocumentChangeTracker`**:
   - حذف `e.RegistryId?.ToString(...)` من توقيع `__Col_ExecutionApplicants`
     (لأن الربط لم يعد جزءًا من هوية الصف).

#### ب-4) الاختبارات

- `DocumentForm.execution.test.tsx`:
  - استبدال اختبار «يمنع الحفظ عند بقاء طالب تنفيذ اعتباري نصّي…» باختبار
    «يحفظ طالب تنفيذ اعتباري حر النص ويسمح بالحفظ».
  - حذف اختبارات زر «مسح»/«فك الربط» الخاصة بطالب التنفيذ الاعتباري.
  - إضافة نسخة لـ`generalEntitySide: 'deposit'`.
- `DocumentForm.entityParties.test.tsx`:
  - حذف اختبار ربط طالب التنفيذ الاعتباري بالسجل.
  - حذف اختبار قفل اسمه.
  - إضافة تأكيد أن الاسم يُرسل في `payload.executionApplicants` بلا `registryId`.
- `EntityRegistryLinkTests`:
  - حذف `ExecutionApplicant_LegalWithRegistry_KeepsRegistryLink_AcceleratorStaysNull`.
  - حذف `ExecutionApplicant_Natural_DropsRegistryLink`.
  - إضافة `ExecutionApplicant_LegalWithoutRegistry_KeepsFreeName`.
- `PortalScopingTests`:
  - استبدال `ExecutionApplicantLink_EntersDelegateScope_LegalRegistryIdOnly` باختبار
    يثبت أن `ExecutionApplicant.RegistryId` **لا يدخل** نطاق المندوب بعد الإلغاء
    (قيمة تاريخية متجاهلة).
- `PublicEntityServiceTests`:
  - حذف اختبارات `AbolishAndReplace_MigratesExecutionApplicantRegistryIdAndName`
    و`CommitMerge_MigratesExecutionApplicantRegistryId`
    و`RenameGroup_ExecutionApplicant_UpdatesSnapshotAndTexts`.
  - حذف `ExecutionApplicant` من اختبار `UnifyNames` المرتبط بالأنواع الثلاثة.
  - إضافة اختبار `RenameGroup_DoesNotRenameFreeLegalExecutionApplicant` يثبت أن
    الاسم الحر لا يُزامَن مع تغيير السجل.
- `DocumentChangeTrackerTests`:
  - حذف `RegistryId = 123` وتأكيداته من اختبار الثقافة.
- إضافة اختبار انحدار للمنفذ عليه الاعتباري (`executedPublicEntities` طبيعة `legal`)
  يثبت أن الحقل حر ويُحفظ بلا `registryId`.

---

### المرحلة ج — تنبيه المحامي عند إعادة الإنابة للتصحيح

#### ج-1) الكتالوج والجمهور

**`backend/src/DocGenerator.Domain/Enums/HeadAlertKindCatalog.cs`:**

- تغيير `DelegationRejectedWrongCourt` من `DocumentCreator` إلى `SingleLawyer`:
  ```csharp
  [DelegationRejectedWrongCourt] = Set(HeadAlertAudience.SingleLawyer),
  ```
- (اختياري موصى به) تغيير التسمية إلى `إعادة إنابة للتصحيح` مع مزامنة
  `frontend/src/components/dashboard/alertKinds.ts`.

#### ج-2) نص الرسالة

**`HeadAlertMessages.cs`:**

- توسيع السياق:
  ```csharp
  public sealed record DelegationRejectedWrongCourtContext(
      int DelegationId, string Court, string SourceLabel, string Reason);
  ```
- النص الجديد:
  ```csharp
  => $"يرجى تصحيح الإنابة رقم {context.DelegationId} المسطرة إلى {context.Court} " +
     $"في ملف {context.SourceLabel} وإعادة تسطيرها — سبب الإعادة: {context.Reason}";
  ```
- إضافة مساعد عرض دائرة في `DocumentDelegationService` يمنع «دائرة دائرة تنفيذ»:
  ```csharp
  private static string CourtWithExecutionPrefix(string? court)
  {
      var value = Normalize(court);
      if (value is null) return "الدائرة المنابة";
      if (value == "دائرة" || value.StartsWith("دائرة ") ||
          value == "محكمة" || value.StartsWith("محكمة ") ||
          value == "تنفيذ" || value.StartsWith("تنفيذ "))
          return value;
      return $"دائرة تنفيذ {value}";
  }
  ```

#### ج-3) `DocumentDelegationService.RejectAsync`

- بناء الرسالة بـ`SourceLabel(delegation.SourceDocument)` و`CourtWithExecutionPrefix(delegation.DelegatedCourt)`.
- تغيير الطلب:
  ```csharp
  Audience: HeadAlertAudience.SingleLawyer,
  RecipientUserId: delegation.CreatedById,
  TargetLawyerId: delegation.CreatedById,
  ```
- الإبقاء على:
  - `DocumentId: delegation.SourceDocumentId`
  - `DelegationId: delegation.Id`
  - `SupersedePlan` الحالي (`DelegationKinds` + `delegation_rejected`).
- لا تغيير في `RejectReason` ولا في حالة الإنابة.

#### ج-4) الواجهة

**`DelegationDetails.tsx`:**

- إضافة عرض سبب الإعادة قبل الأموال:
  ```tsx
  {d.rejectReason && (
    <p role="alert"
       className="text-xs font-medium text-red-800 bg-red-50 border border-red-200 rounded-lg px-3 py-2 break-words">
      أُعيدت الإنابة للتصحيح: {d.rejectReason}
    </p>
  )}
  ```
- إزالة التكرار من `DelegationRequests.tsx` (السطر 145–147) أو إبقاؤه إن أردنا
  تكرارًا مقصودًا لرئيس القسم؛ الموصى به إزالته لأن `DelegationDetails` تعرضه الآن.

#### ج-5) الاختبارات

**خلفية:**

- `HeadAlertMessageTests`: تحديث النص المتوقع ليشمل الدائرة واسم الملف والسبب.
- `HeadAlertKindCatalogTests`: تحديث `InlineData` لجمهور `DelegationRejectedWrongCourt`
  إلى `SingleLawyer`.
- `SubHeadDelegationTests.Reject_WrongCircuit_BlocksAssign_LawyerCorrectionReleases`:
  - بعد `RejectAsync` و`FlushAlertsAsync`:
    - التنبيه النشط `TargetType == Lawyer`.
    - المستلم الوحيد هو `_lawyer1` (كاتب الإنابة).
    - النص يحتوي `أحمد خالد الخطيب` و`دائرة تنفيذ دمشق` و`سبب الإعادة`.
- `HeadAlertSchemaMigrationTests`: لا تغيير (اختبار رسائل تاريخية لا يمس النص الجديد).

**واجهة:**

- `DelegationsCard.test.tsx`: إضافة اختبار يعرض `rejectReason` داخل البطاقة.
- `DelegationRequests.test.tsx`: إن أُزيل العرض المكرر، تعديل الاختبار إن وُجد.

---

## 4) الملفات المتأثرة (قائمة مرجعية)

### خلفية

- `backend/src/DocGenerator.Application/Common/DocumentDisplayName.cs` (جديد)
- `backend/src/DocGenerator.Application/DTOs/AppealDtos.cs`
- `backend/src/DocGenerator.Application/DTOs/StatsDtos.cs`
- `backend/src/DocGenerator.Application/Services/DocumentAppealService.cs`
- `backend/src/DocGenerator.Application/Services/DocumentDelegationService.cs`
- `backend/src/DocGenerator.Application/Services/HeadAlertMessages.cs`
- `backend/src/DocGenerator.Application/DTOs/DocumentPartyDtos.cs`
- `backend/src/DocGenerator.Application/DTOs/DocumentDtos.cs`
- `backend/src/DocGenerator.Application/Services/PublicEntityService.cs`
- `backend/src/DocGenerator.Application/Services/PublicEntityService.Groups.cs`
- `backend/src/DocGenerator.Application/Common/Audit/DocumentChangeTracker.cs`
- `backend/src/DocGenerator.Application/Common/Interfaces/IPublicEntityRepository.cs`
- `backend/src/DocGenerator.Domain/Enums/HeadAlertKindCatalog.cs`
- `backend/src/DocGenerator.Domain/Entities/ExecutionApplicant.cs` (تعليق legacy)
- `backend/src/DocGenerator.Infrastructure/Persistence/AppealRepository.cs`
- `backend/src/DocGenerator.Infrastructure/Persistence/StatisticsRepository.cs`
- `backend/src/DocGenerator.Infrastructure/Persistence/PublicEntityRepository.cs`
- `backend/src/DocGenerator.Infrastructure/Persistence/PortalRepository.cs`

### واجهة

- `frontend/src/types/management.ts`
- `frontend/src/components/dashboard/dashboardFormat.ts`
- `frontend/src/components/dashboard/ReminderList.tsx`
- `frontend/src/components/dashboard/DayRemindersModal.tsx`
- `frontend/src/components/form/ExecutedSideSections.tsx`
- `frontend/src/pages/DocumentForm.tsx`
- `frontend/src/components/delegation/DelegationDetails.tsx`
- `frontend/src/pages/DelegationRequests.tsx`
- `frontend/src/components/dashboard/alertKinds.ts` (اختياري للتسمية)

### اختبارات

- `backend/tests/DocGenerator.Application.Tests/DocumentDisplayNameTests.cs` (جديد)
- `backend/tests/DocGenerator.Application.Tests/DocumentAppealServiceTests.cs`
- `backend/tests/DocGenerator.Application.Tests/EntityRegistryLinkTests.cs`
- `backend/tests/DocGenerator.Application.Tests/PortalScopingTests.cs`
- `backend/tests/DocGenerator.Application.Tests/PublicEntityServiceTests.cs`
- `backend/tests/DocGenerator.Application.Tests/DocumentChangeTrackerTests.cs`
- `backend/tests/DocGenerator.Application.Tests/HeadAlertMessageTests.cs`
- `backend/tests/DocGenerator.Application.Tests/HeadAlertKindCatalogTests.cs`
- `backend/tests/DocGenerator.Application.Tests/SubHeadDelegationTests.cs`
- `backend/tests/DocGenerator.Application.Tests/StatisticsTests.cs` (أو اختبار مستودع مكافئ)
- `frontend/src/components/dashboard/ReminderList.test.tsx`
- `frontend/src/components/dashboard/DayRemindersModal.test.tsx`
- `frontend/src/components/dashboard/AppealIntegration.test.tsx`
- `frontend/src/pages/DocumentForm.execution.test.tsx`
- `frontend/src/pages/DocumentForm.entityParties.test.tsx`
- `frontend/src/components/delegation/DelegationsCard.test.tsx`
- `frontend/src/pages/DelegationRequests.test.tsx` (إن لزم)

---

## 5) الاختبارات والتحقق (إلزامي بعد التنفيذ)

### خلفية

```powershell
dotnet test backend/DocGenerator.sln
```

- التأكد من نجاح اختبارات SQLite وPostgreSQL.
- تشغيل اختبارات `DocumentDisplayName` والاستئناف والإنابة والتنبيهات.

### واجهة

```powershell
cd frontend
npx oxlint src
npx tsc -b
npx vitest run
npm run build
```

### حراس إضافية

```powershell
node scripts/subhead-guards.mjs
```

(لأن `HeadAlertKindCatalog` والواجهة `alertKinds.ts` قد يتأثران بتغيير التسمية/الجمهور.)

### فحص بصري للجوال

- فتح نموذج «منفذ عليها» و«عرض وايداع» على عرض ~375px:
  - بطاقة طالب التنفيذ الاعتباري: حقل الاسم قابل للكتابة، لا تجاوز أفقي.
  - زر الربط الاختياري لا يزاحم الحقل.
- فتح بطاقة التذكيرات: اسم الملف يظهر أولًا ولا يُقطع.

---

## 6) معايير القبول

- [ ] تذكير الاستئناف يعرض اسم الملف (مثل «شركة بركات التجارية») قبل نوع الاستئناف/اتجاهه.
- [ ] تذكير الملف لملف «منفذ عليه» يعرض اسم الجهة/الشخص لا نوع المستند.
- [ ] طالب التنفيذ/طالب العرض الاعتباري في وضعي «منفذ عليها» و«عرض وايداع» يُحفظ باسم حر
      بلا `registryId`، ويظهر خطأ فقط عند الاسم الفارغ (إن كان إلزاميًا).
- [ ] **لا يوجد أي زر أو شارة أو تحقق يربط اسم الشخص الاعتباري بالسجل**، ولا يُقرأ
      `ExecutionApplicant.RegistryId` في أي مسار تطبيقي (يبقى عمودًا تاريخيًا فقط).
- [ ] المنفذ عليه الاعتباري يبقى حقلًا حرًا (اختبار انحدار).
- [ ] عند رفض رئيس القسم الإنابة، يصل تنبيه للمحامي كاتب الإنابة (`delegation.CreatedById`)
      نصه يذكر الدائرة واسم الملف والسبب و«إعادة تسطيرها».
- [ ] بطاقة تشعبات الملف تعرض سبب الإعادة للمحامي.
- [ ] كل الاختبارات والفحوصات خضراء، ولا هجرات جديدة.

---

## 7) المخاطر والتراجع

| الخطر | التخفيف |
|---|---|
| إضافة `Include` لمجموعات الأطراف في الاستئنافات تزيد استعلامات القوائم | `AsSplitQuery()` + قياس عدد الاستعلامات في اختبارات التكامل |
| تغيير جمهور `DelegationRejectedWrongCourt` يؤثر على تنبيهات قديمة | الجمهور لا يُخزَّن كمرجع للقراءة (القراءة عبر `Recipients`)؛ الصفوف القديمة تبقى صالحة |
| إلغاء الربط قد يُخفي بيانات تاريخية مرتبطة | العمود `ExecutionApplicants.RegistryId` يبقى في القاعدة بلا حذف، لكن لا يقرأه/يكتبه أي مسار تطبيقي |
| تكرار «استئناف» في سطر التذكير | `appealReminderTitle` يجمع الاسم والنوع مرة واحدة، والشارة الحمراء تبقى للتصنيف البصري |

**التراجع:** كل التغييرات واجهة/خدمة بلا هجرات؛ يمكن الرجوع عنها بـ`git revert` دون
أثر على قاعدة البيانات.

---

## 8) خارج النطاق

- لا تغيير في منطق حساب تواريخ التذكير أو `ActionReminderCalculator`.
- لا تغيير في صلاحيات رفض الإنابة أو حالاتها.
- لا تغيير في `ApplicantSideSections` (ملفات «طالبة تنفيذ») — تبقى الجهات العامة هناك
  مرتبطة بالسجل كما هي.
- لا إعادة تصميم لصفحة التنبيهات أو تصنيفاتها.

---

## 9) حالة التنفيذ (2026-11-09)

- **المرحلة أ:** نُفذت — `DocumentDisplayName` مركزي، `DocumentLabel` في
  `ReminderDto`/`AppealReminderDto`، تحميل أطراف الاستئناف، وعرض اسم الملف في
  `ReminderList` و`DayRemindersModal`.
- **المرحلة ب:** نُفذت — أُزيل ربط اسم الشخص الاعتباري بالسجل من الواجهة والعقد
  (`ExecutionApplicantDto`)، ومن منطق الخلفية (`NormalizeExecutionApplicants`,
  `PublicEntityService`, `PublicEntityRepository`, `PortalRepository`, `PortalService`)،
  مع إبقاء عمود `ExecutionApplicants.RegistryId` كعمود تاريخي فقط (بلا هجرة حذف).
- **المرحلة ج:** نُفذت — تنبيه `DelegationRejectedWrongCourt` يذهب إلى
  `delegation.CreatedById` بجمهور `SingleLawyer`، ونصه يذكر الدائرة واسم الملف والسبب،
  وبطاقة `DelegationDetails` تعرض سبب الإعادة.
- **التحقق:** `dotnet test` (Application: 2320 ناجح، API: 905 ناجح)،
  `npx oxlint src`، `npx tsc -b`، `npx vitest run` (1775 ناجح)،
  `npm run build`، `node scripts/subhead-guards.mjs`.
- **الهجرات:** صفر.
