# إصلاح ثغرة `ApplicantRegistryId = GroupId` (بدل `EntryId`) — خطة تفصيلية لمحادثة جديدة

> **الحالة:** المرحلة ب مكتملة ومدفوعة (`73e68cf` على `master` — 794+283 BE / 676 FE أخضر، هجرتان `AddExecutionApplicantRegistryId`).
> **هذه الخطة لإصلاح ثغرة قديمة معزولة** اكتُشفت أثناء المراجعة التحليلية الشاملة للمرحلة ب — تغيير منطقي صرف بلا هجرات، يُنفذ في محادثة جديدة وفق الأصول الصارمة.

---

## 1) السياق والثغرة

### التعريف المعياري للحقل

- **العمود:** `Document.ApplicantRegistryId` (`Document.cs:102` — `int?`) — **حقل تسريع بلا FK** (`Configurations.cs:123-124` مجرد `Property + HasIndex` — لا `HasOne/HasForeignKey`).
- **الدلالة الموثقة:** `DocumentService.Apply.cs:856-860`
  ```csharp
  // نسخة تسريع لفلترة جهة الطالب في البوابة: أول ربط سجلي غير فارغ بين صفوف الجهات
  doc.ApplicantRegistryId = doc.ApplicantPublicEntities
      .Select(a => a.RegistryId)          // ← EntryId (معرّف القيد = PublicEntity.Id)
      .FirstOrDefault(id => id.HasValue);
  ```
  أي القيمة المعيارية هي **معرّف القيد** (`PublicEntity.Id`) وليس معرّف الهوية الأم (`PublicEntity.GroupId`).
- **المصدر الثاني الوحيد الصحيح:** `DocumentDelegationService.cs:702-704` — نفس الاشتقاق عند نسخ المندوبية.
- **الاستهلاك الفعلي اليوم:** `PortalRepository.ScopePredicate:25-41` و `CountDocsPerEntryAsync:215-242` **لا يستهلكان `ApplicantRegistryId`** أصلًا — يفحصان `ApplicantPublicEntities.Any(a => a.RegistryId ... ids.Contains(a.RegistryId.Value))` مباشرة. الحقل يُعرض في `ToDto:806` ويُسجل تدقيقيًا (`DocumentChangeTracker.cs:87`) فقط.

### مواضع الثغرة (5 مواضع — كلها في `PublicEntityService.cs`)

| # | السطر الحالي | العملية | الكود الخاطئ | الصحيح |
|---|---|---|---|---|
| 1 | `1302` | `MoveEntry` — طي قيد إلى قيد مطابق | `doc.ApplicantRegistryId = toGroupId;` | `EntryId` |
| 2 | `1362` | `MoveEntry` — نقل قيد (فرع آخر) | `doc.ApplicantRegistryId = toGroupId;` | `EntryId` |
| 3 | `1487` | نقل هوية أم (`MoveAllEntries`) | `doc.ApplicantRegistryId = request.TargetGroupId;` | `EntryId` |
| 4 | `1755` | `CommitMerge` — دمج هويات | `doc.ApplicantRegistryId = targetEntry.GroupId;` | `EntryId` (`targetEntry.Id`) |
| 5 | `2480` | `AbolishAndReplace` — حلول | `doc.ApplicantRegistryId = newGroup.Id;` | `EntryId` (`newParentEntry.Id`) |

> **الأثر العملي الحالي:** لا يكسر الإنتاج (الفلترة لا تعتمد على الحقل، ويُعاد اشتقاقه تلقائيًا عند أول حفظ `Apply:858` فيزول الخطأ)، لكنه **خلط وحدات دلالية** (قيد ↔ هوية أم) و**باب خطأ خفي** إذا اعتمدت ميزة مستقبلية على الحقل كـ `EntryId`. **معزول عن المرحلة ب:** طالب التنفيذ (`ExecutionApplicants.RegistryId`) **لا يكتب في `ApplicantRegistryId` أبدًا** — فصل إلزامي موثق بـ `EntityRegistryLinkTests.cs:242` (`Assert.Null(loaded.ApplicantRegistryId)`).

---

## 2) النطاق / خارج النطاق

**داخل النطاق:**
- إعادة اشتقاق `doc.ApplicantRegistryId` من `doc.ApplicantPublicEntities` في المواضع الخمسة أعلاه — داخل نفس المعاملة (بعد تحديث `RegistryId` نحو `targetEntry.Id` حيثما وُجد تحديث للصفوف — `Fold`/`Merge`/`Abolish` — وبإعادة اشتقاقه من الصفوف كما هي حيث لم يتغير معرّف القيد `entry.Id` — `MoveEntry ModeA`/`MoveAllEntries`).
- 6 اختبارات حارس تثبت أن القيمة بعد العملية = `EntryId` لا `GroupId`، وأنه يُعاد حسابه صحيحًا بعد إعادة حفظ.
- لا هجرات — تغيير منطقي صرف (نفس الاشتقاق الموحد لكل المواضع الخمسة — مصدر الحقيقة الواحد `Apply:858`).

**خارج النطاق (يُؤجل):**
- أي تغيير على سلوك `ExecutionApplicants` (مفصول تمامًا — لا يلمس `ApplicantRegistryId`).
- أي تغيير على `PortalRepository.ScopePredicate` أو `CountDocsPerEntryAsync` (سليمة — تستهلك الصفوف مباشرة).
- أي توحيد لاستعلامات الإحصاء أو الـ `DTO` — لا حاجة.

---

## 3) التنفيذ التفصيلي — خطوة بخطوة (وفق الأصول الصارمة)

### الخطوة 0 — القراءة الإلزامية قبل أي تعديل

اقرأ كاملة:
- `Document.cs:95-103` (تعريف الحقل وتعليق التسريع)
- `Configurations.cs:123-124` (بلا FK — تسريع)
- `DocumentService.Apply.cs:854-861` (المصدر المعياري الوحيد)
- `DocumentDelegationService.cs:695-704` (المصدر الثاني)
- `PublicEntityService.cs:1280-1310` (الموضع 1 — طي إلى قيد)
- `PublicEntityService.cs:1340-1390` (الموضع 2 — نقل قيد)
- `PublicEntityService.cs:1475-1495` (الموضع 3 — نقل هوية أم)
- `PublicEntityService.cs:1735-1765` (الموضع 4 — دمج)
- `PublicEntityService.cs:2450-2490` (الموضع 5 — حلول)
- `PublicEntityServiceTests.cs` — ابحث عن `MoveEntry` / `CommitMerge` / `Abolish` الحالية لفهم نمط الاختبار

### الخطوة 1 — الإصلاح الموحد (5 مواضع — نفس النمط)

**النمط المرجعي الوحيد (من `Apply:858`):**
```csharp
doc.ApplicantRegistryId = doc.ApplicantPublicEntities
    .Select(a => a.RegistryId)
    .FirstOrDefault(id => id.HasValue);
```

**التطبيق في كل موضع:** استبدل السطر الخاطئ `doc.ApplicantRegistryId = <GroupId>` بالاشتقاق أعلاه داخل نفس المعاملة — بعد حلقة تحديث `ApplicantPublicEntities.Where(a => a.RegistryId == oldId) { a.RegistryId = newId; }` حيثما وُجدت (`Fold`/`Merge`/`Abolish`)، أو مباشرة بعد `entry.GroupId = TargetGroupId` حيث لم يتغير معرّف القيد ولا الصفوف (`MoveEntry ModeA`/`MoveAllEntries`). في الحالتين يقرأ الاشتقاق `EntryId` الصحيح من الصفوف ويستبدل القيمة الخاطئة `GroupId`.

**الموضع 1 — `MoveEntry` (طي إلى قيد مطابق) — حوالي `1300`:**
```csharp
// قبل (خاطئ):
doc.ApplicantRegistryId = toGroupId;
// بعد (صحيح):
doc.ApplicantRegistryId = doc.ApplicantPublicEntities
    .Select(a => a.RegistryId)
    .FirstOrDefault(id => id.HasValue);
```

**الموضع 2 — `MoveEntry` (نقل قيد عام) — حوالي `1362`:**
```csharp
// قبل:
doc.ApplicantRegistryId = toGroupId;
// بعد: نفس الاشتقاق
doc.ApplicantRegistryId = doc.ApplicantPublicEntities
    .Select(a => a.RegistryId)
    .FirstOrDefault(id => id.HasValue);
```

> **تنبيه دقيق للموضعين 1 و 2:** في `MoveEntry` المتغير `toGroupId` يُشتق من `targetEntry.GroupId` لكن الهدف الحقيقي هو `targetEntryId` (`targetEntry.Id`).
> - **الموضع 1 — `Fold` (`1302`):** الصفوف حُدّثت فعلًا إلى `targetEntry.Id` (السطر `1297`)، والاشتقاق يقرأ القيمة الجديدة `targetEntry.Id` ويستبدل `toGroupId` الخاطئ — صحيح.
> - **الموضع 2 — `ModeA` (`1362`):** القيد **لم يتغير معرّفه** (`targetEntryId == entryId == entry.Id`، فقط `entry.GroupId = toGroupId` في `1354`)، فالصفوف **لم تُحدّث**، والاشتقاق يقرأ `entry.Id` كما هو من الصفوف ويستبدل `toGroupId` الخاطئ — يعمل لأنه يعيد القيمة الصحيحة الثابتة، لا لأنه "انتقل إلى entry جديد". لا تكتب `entry.Id` مباشرة — حافظ على الاشتقاق الموحد لأنه يحترم ترتيب "أول `RegistryId` غير فارغ" كما في `Apply:858` ويبقى صحيحًا لو كان للملف صفوف متعددة.

**الموضع 3 — `MoveAllEntries` (نقل هوية أم كاملة) — حوالي `1487`:**
```csharp
// قبل:
doc.ApplicantRegistryId = request.TargetGroupId;
// بعد: نفس الاشتقاق — القيد لم يتغير معرّفه (entry.Id ثابت، فقط entry.GroupId = TargetGroupId في 1480)
// فالصفوف لم تُحدّث، والاشتقاق يقرأ entry.Id كما هو من الصفوف ويستبدل TargetGroupId الخاطئ:
doc.ApplicantRegistryId = doc.ApplicantPublicEntities
    .Select(a => a.RegistryId)
    .FirstOrDefault(id => id.HasValue);
```
> **حقيقة هذا المسار — تصحيح لتعليل سابق:** لا يوجد تحديث لـ `ApplicantPublicEntities.RegistryId` في `MoveAllEntries` (الحلقة `1470-1490` تغيّر `entry.GroupId` فقط، ولا تلمس الصفوف، ولا تتلوها `SyncTextsAfterFoldAsync` بل `SaveChangesAsync:1521` مباشرة)، ومعرّف القيد `entry.Id` **لا يتغير**. القيمة الصحيحة قبل وبعد هي `entry.Id`، والسطر الخاطئ `request.TargetGroupId` يضع `GroupId` مكان `EntryId` ويستقر في القاعدة. الاشتقاق الموحد **يُصلح** لأنه يستبدل `TargetGroupId` بـ `entry.Id` المقروء من الصفوف كما هي — لا تكتب `entry.Id` مباشرة (الاشتقاق أدق لأنه يحترم ترتيب الصفوف المتعددة ويوحّد مصدر الحقيقة مع `Apply:858`).

**الموضع 4 — `CommitMerge` — حوالي `1755`:**
```csharp
// قبل:
doc.ApplicantRegistryId = targetEntry.GroupId;
// بعد:
doc.ApplicantRegistryId = doc.ApplicantPublicEntities
    .Select(a => a.RegistryId)
    .FirstOrDefault(id => id.HasValue);
```

**الموضع 5 — `AbolishAndReplace` — حوالي `2480`:**
```csharp
// قبل:
doc.ApplicantRegistryId = newGroup.Id;
// بعد:
doc.ApplicantRegistryId = doc.ApplicantPublicEntities
    .Select(a => a.RegistryId)
    .FirstOrDefault(id => id.HasValue);
```
> **لا تكتب `targetEntry.Id` / `newParentEntry.Id` مباشرة في الموضعين 4 و 5** رغم أنه يساوي النتيجة حاليًا — **التزم بالاشتقاق الموحد** لأنه مصدر الحقيقة الواحد نفسه في `Apply:858`، ويحترم ترتيب "أول `RegistryId` غير فارغ" لو كان للملف صفوف متعددة، ولا يتطلب تتبع متغير إضافي.

**قاعدة ذهبية:** لا تكتب `GroupId` يدويًا في `ApplicantRegistryId` أبدًا — اشتقّه دومًا من `doc.ApplicantPublicEntities.Select(a => a.RegistryId).FirstOrDefault(id => id.HasValue)`.

### الخطوة 2 — الاختبارات (إلزامي — مع كل تغيير سلوكي — 6 اختبارات)

**اختبار 1 — حارس `EntryId` لا `GroupId` — `MoveEntry Fold` (يغطي `1302`):**
```csharp
[Fact]
public async Task MoveEntry_Fold_ApplicantRegistryId_IsEntryId_NotGroupId()
{
    // أنشئ مجموعتين: sourceGroup (قيد entryA) و targetGroup (قيد entryB)
    // أنشئ ملفًا applicant مرتبطًا بـ entryA (RegistryId = entryA.Id)
    // نفّذ MoveEntry(entryA.Id -> targetEntryId = entryB.Id) — وضع الطيّ
    // تحقق: doc.ApplicantRegistryId == entryB.Id (EntryId) لا targetGroup.Id
    // تحقق: ApplicantPublicEntities.Single(a => a.RegistryId == entryB.Id) موجود
    // تحقق: Assert.NotEqual(targetGroup.Id, doc.ApplicantRegistryId) — GroupId != EntryId مضمون
}
```

**اختبار 2 — حارس `EntryId` لا `GroupId` — `MoveEntry ModeA` (يغطي `1362` — القيد لم يتغير):**
```csharp
[Fact]
public async Task MoveEntry_ModeA_ApplicantRegistryId_IsEntryId_NotGroupId()
{
    // أنشئ مجموعتين g1 (entryA) و g2 فارغة
    // أنشئ ملفًا applicant مرتبطًا بـ entryA (RegistryId = entryA.Id)
    // نفّذ MoveEntry(entryA.Id -> TargetGroupId = g2.Id) — تغيير هوية أم بلا طيّ
    // تحقق: doc.ApplicantRegistryId == entryA.Id (بقي كما هو — EntryId لم يتغير)
    // تحقق: Assert.NotEqual(g2.Id, doc.ApplicantRegistryId) — GroupId الجديد ليس EntryId
    // تحقق: ApplicantPublicEntities[0].RegistryId == entryA.Id (الصفوف لم تُمس)
}
```

**اختبار 3 — حارس `EntryId` لا `GroupId` — `MoveAllEntries` (يغطي `1487` — الأخطر، يستقر خاطئًا بلا مزامنة):**
```csharp
[Fact]
public async Task MoveAllEntries_ApplicantRegistryId_IsEntryId_NotGroupId()
{
    // أنشئ g1 بقيد entryA (حلب/فرع حلب) و g2 فارغة
    // أنشئ ملفًا applicant مرتبطًا بـ entryA (RegistryId = entryA.Id)
    // نفّذ MoveAllEntries(g1 -> g2)
    // تحقق: doc.ApplicantRegistryId == entryA.Id (القيد نفسه — Id ثابت، فقط GroupId تغير)
    // تحقق: Assert.NotEqual(g2.Id, doc.ApplicantRegistryId) — لو كان GroupId لانكشف
    // تحقق: entryA.GroupId == g2.Id لكن ApplicantRegistryId != g2.Id
}
```

**اختبار 4 — `CommitMerge` — `ApplicantRegistryId` يُعاد اشتقاقه (يغطي `1755`):**
```csharp
[Fact]
public async Task CommitMerge_ApplicantRegistryId_RederivedAsEntryId()
{
    // SeedThreeGroupsForMergeAsync() موجود — استخدمه (sg ناجية، ag1 ممتصة)
    // أنشئ ملفًا applicant مرتبطًا بقيد من ag1 (absorbedEntry.Id)
    // CommitMerge(ag1 -> sg, NewCanonicalName: "اسم جديد")
    // تحقق: doc.ApplicantRegistryId == survivorEntry.Id (EntryId) لا sg.Id
    // تحقق: Assert.NotEqual(sg.Id, doc.ApplicantRegistryId)
}
```

**اختبار 5 — `AbolishAndReplace` — `ApplicantRegistryId` يُعاد اشتقاقه (يغطي `2480`):**
```csharp
[Fact]
public async Task AbolishAndReplace_ApplicantRegistryId_IsNewParentEntryId()
{
    // أنشئ هويتين ممتصتين ag1 (دمشق) + ag2 (حلب) + ملف applicant مرتبط بـ ag1
    // AbolishAndReplace([ag1, ag2] -> "هوية جديدة")
    // تحقق: doc.ApplicantRegistryId == newParentEntry.Id (EntryId) لا newGroup.Id
    // تحقق: Assert.NotEqual(newGroup.Id, doc.ApplicantRegistryId)
}
```

**اختبار 6 — حالة حدية `null` + تكافؤ إعادة الحفظ:**
```csharp
[Fact]
public async Task MoveEntry_DocumentWithoutApplicantLink_ApplicantRegistryId_StaysNull()
{
    // ملف executed بلا ApplicantPublicEntities (أو بلا RegistryId) + ملف applicant سليم للمقارنة
    // MoveEntry على قيد غير مرتبط — لا يجب أن يضع GroupId
    // تحقق: docNull.ApplicantRegistryId == null (بقي null، لم يُكتب GroupId)
    // تحقق ثانٍ (تكافؤ Apply): بعد MoveEntry — اقرأ doc.ApplicantRegistryId ثم أعد حفظ الملف عبر DocumentService.UpdateAsync بلا تغيير — تحقق القيمة قبل وبعد متطابقة (= EntryId)
}
```

> كل اختبار يجب أن يتحقق **أن القيمة ليست `GroupId`** — مثلاً `Assert.NotEqual(targetGroup.Id, doc.ApplicantRegistryId)` حيث `targetGroup.Id != targetEntry.Id` و `newGroup.Id != newParentEntry.Id` (مضمون لأن `GroupId` ≠ `EntryId` دائمًا في البيانات المزروعة — كل `CreateAsync` ينشئ `Group` + `Entry` بمعرّفين مختلفين). الاختبارات 2 و 3 هما الأهم لأنهما يغطيان المسارين اللذين لا يُحدّثان الصفوف ويتركان القيمة الخاطئة مستقرة في القاعدة بلا `SyncTextsAfterFoldAsync`.

### الخطوة 3 — التحقق الإلزامي الكامل (قبل اعتبار المهمة منجزة)

```powershell
# من backend\
dotnet test                          # 794 + 283 + 6 الجدد أخضر — لا فشل
dotnet build --no-restore            # 0 تحذير

# من frontend\ (حتى لو لم تُمس الواجهة — تحقق عدم كسر)
npx oxlint src                       # 0/0
npx tsc -b                           # EXIT 0
npm run build                        # ✓
npx vitest run                       # 676 + الجدد أخضر

# تدقيق بقايا/عقود
# grep: تأكد أن لا موضع بقي يكتب GroupId في ApplicantRegistryId
Select-String -Pattern "ApplicantRegistryId\s*=\s*.*GroupId" -Path backend/src -Recurse
# يجب أن يعيد 0 نتائج بعد الإصلاح — إن عاد >0 فهناك موضع بقي يكتب GroupId
```

**قائمة تحقق ذاتية قبل الإعلان:**
1. **تدقيق العقود:** تتبع `ApplicantRegistryId` من `Document.cs:102` → `Apply:858` → `PublicEntityService` الخمسة (`1302` Fold / `1362` ModeA / `1487` MoveAll / `1755` Merge / `2480` Abolish) → `PortalRepository` (لا استهلاك) → `DTO:806` — كلها `int?` متطابقة.
2. **فحص بقايا:** `Select-String ApplicantRegistryId` يظهر فقط `Apply:858` و `Delegation:702` و الخمسة المواضع المُصلحة (كلها اشتقاق `Select(a => a.RegistryId).FirstOrDefault`) — لا `GroupId` مباشر.
3. **مطابقة الخطة:** كل بند أعلاه مُنفذ — 5 مواضع (نفس الاشتقاق الموحد) + 6 اختبارات (Fold + ModeA + MoveAll + Merge + Abolish + حدية null/تكافؤ) + تحقق كامل.
4. **الاصطلاحات:** لا تكرار — نمط واحد موحد، لا حلول مختصرة، لا `entry.Id` مباشر، لا أسرار.

---

## 4) المخاطر والالتزامات

- **لا هجرات** — تغيير منطقي صرف، لا migration، لا `dotnet ef database update`.
- **لا تغيير كاسر** — `ApplicantRegistryId` لم يكن مستهلكًا في الفلترة، فإصلاحه لا يكسر سلوكًا ظاهرًا؛ فائدته وقائية.
- **البيانات القائمة:** الملفات التي خضعت لحوكمة سابقة قد تحمل `GroupId` قديمًا في `ApplicantRegistryId` — سيُصحح تلقائيًا عند أول حفظ لاحق (`Apply:858`)، أو يمكن سكربت لمرة واحدة إن رغب المالك (خارج نطاق هذه الخطة — يُؤجل).
- **الأمان:** لا صلاحيات جديدة، لا أسرار، احترام `Date Fields Rule` و `mobile-first` (لا واجهة في هذه الخطة).

---

## 5) معايير التسليم (Definition of Done)

- [ ] الخمسة مواضع في `PublicEntityService.cs` (`1302` Fold / `1362` ModeA / `1487` MoveAll / `1755` Merge / `2480` Abolish) تستخدم الاشتقاق الموحد — `Select-String "ApplicantRegistryId.*GroupId"` = 0
- [ ] 6 اختبارات جديدة خضراء (Fold EntryId + ModeA EntryId + MoveAll EntryId + Merge + Abolish + حدية null/تكافؤ) — كلها تتحقق `NotEqual(GroupId, ApplicantRegistryId)`
- [ ] `dotnet test` (794+283+6) + `dotnet build` + `npx oxlint` + `npx tsc -b` + `npx vitest run` كلها خضراء
- [ ] لا تغيير في `ExecutionApplicants` (مفصول — مُثبت بـ `EntityRegistryLinkTests.cs:242`)
- [ ] تقرير إنجاز يذكر المواضع الخمسة بالسطر ويؤكد عدم الحاجة لهجرات (تغيير منطقي صرف)

---

## 6) ملاحظات ختامية للمحادثة الجديدة

1. اقرأ `Document.cs:102` + `Apply:858` + `PublicEntityService:1280-1310/1340-1390/1475-1495/1735-1765/2450-2490` كاملة قبل أي تعديل.
2. نفّذ الخطوة 1 (5 مواضع) في `commit` واحد صغير — لا تُجزئ بلا داعٍ (تغيير واحد متماسك).
3. أضف الاختبارات في نفس الـ `commit` أو تالٍ مباشر — لا تؤخر التغطية.
4. شغّل التحقق الكامل + `Select-String` قبل الدفع — لا تعتبر المهمة منجزة قبل الأخضر الكامل.
5. اذكر في تقرير الإنجاز: "لا هجرات — إصلاح منطقي صرف لتوحيد `ApplicantRegistryId` على `EntryId`".

> **المرجع:** هذه الخطة مبنية على مراجعة تحليلية شاملة للمرحلة ب (`73e68cf`) وفحص `diff` الكامل عن `HEAD` وتدقيق عقدي حقلًا بحقل.
