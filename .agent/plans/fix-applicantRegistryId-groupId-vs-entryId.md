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
- إعادة اشتقاق `doc.ApplicantRegistryId` من `doc.ApplicantPublicEntities` في المواضع الخمسة أعلاه — داخل نفس المعاملة وبعد تحديث صفوف `ApplicantPublicEntities.RegistryId`.
- اختبار حارس يثبت أن القيمة بعد العملية = `EntryId` لا `GroupId`، وأنه يُعاد حسابه صحيحًا بعد إعادة حفظ.
- لا هجرات — تغيير منطقي صرف.

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

**التطبيق في كل موضع:** بعد حلقة تحديث `ApplicantPublicEntities.Where(a => a.RegistryId == oldId) { a.RegistryId = newId; }` وقبل `affectedDocs`/`entriesMoved`، استبدل السطر الخاطئ بالاشتقاق أعلاه.

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

> **تنبيه دقيق للموضعين 1 و 2:** في `MoveEntry` المتغير `toGroupId` يُشتق من `targetEntry.GroupId` لكن الهدف الحقيقي هو `targetEntryId` (`targetEntry.Id`). الاشتقاق الموحد يحلها تلقائيًا دون الحاجة لمعرفة `targetEntryId` يدويًا — لأنه يقرأ من الصفوف بعد تحديثها.

**الموضع 3 — `MoveAllEntries` (نقل هوية أم كاملة) — حوالي `1487`:**
```csharp
// قبل:
doc.ApplicantRegistryId = request.TargetGroupId;
// بعد: نفس الاشتقاق (يُقرأ من الصفوف التي حُدثت للتو إلى entry جديد في الهوية الهدف)
doc.ApplicantRegistryId = doc.ApplicantPublicEntities
    .Select(a => a.RegistryId)
    .FirstOrDefault(id => id.HasValue);
```
> **حافة:** في هذا المسار، قد يُنقل قيد ذو `RegistryId` يشير إلى قيد في الهوية المنقولة — بعد النقل يصبح `RegistryId` يشير إلى قيد جديد في `TargetGroupId`. الاشتقاق يلتقط القيمة الجديدة صحيحًا.

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
> **بديل مقبول إن رغبت بوضوح إضافي:** في الموضعين 4 و 5 يمكنك كتابة `doc.ApplicantRegistryId = targetEntry.Id` / `newParentEntry.Id` مباشرة (لأن الصفوف نُقلت إلى ذلك القيد تحديدًا)، لكن **النمط الموحد بالاشتقاق من الصفوف** مفضل — لأنه **مصدر الحقيقة الواحد** نفسه في `Apply:858`، ويبقى صحيحًا حتى لو تغيرت منطق اختيار `targetEntry` مستقبلًا، ولا يتطلب تتبع متغير إضافي.

**قاعدة ذهبية:** لا تكتب `GroupId` يدويًا في `ApplicantRegistryId` أبدًا — اشتقّه من الصفوف.

### الخطوة 2 — الاختبارات (إلزامي — مع كل تغيير سلوكي)

**اختبار 1 — حارس `EntryId` لا `GroupId` (الأساس):**
```csharp
[Fact]
public async Task MoveEntry_ApplicantRegistryId_IsEntryId_NotGroupId()
{
    // أنشئ مجموعتين: sourceGroup (قيد entryA) و targetGroup (قيد entryB)
    // أنشئ ملفًا `applicant` مرتبطًا بـ entryA (RegistryId = entryA.Id)
    // نفّذ MoveEntry(entryA.Id -> targetEntryId)
    // تحقق: doc.ApplicantRegistryId == targetEntry.Id (EntryId) لا targetGroup.Id
    // تحقق: ApplicantPublicEntities[0].RegistryId == targetEntry.Id
}
```

**اختبار 2 — `CommitMerge` — `ApplicantRegistryId` يُعاد اشتقاقه:**
```csharp
[Fact]
public async Task CommitMerge_ApplicantRegistryId_RederivedAsEntryId()
{
    // SeedThreeGroupsForMergeAsync() موجود — استخدمه
    // أنشئ ملفًا applicant مرتبطًا بقيد من الهوية الممتصة
    // CommitMerge(ag1 -> sg, NewCanonicalName: "اسم جديد")
    // تحقق: doc.ApplicantRegistryId == survivorEntry.Id (EntryId)
}
```

**اختبار 3 — `AbolishAndReplace` — `ApplicantRegistryId` يُعاد اشتقاقه:**
```csharp
[Fact]
public async Task AbolishAndReplace_ApplicantRegistryId_IsNewParentEntryId()
{
    // أنشئ هويتين ممتصتين + ملف applicant مرتبط بإحداهما
    // AbolishAndReplace([ag1, ag2] -> "هوية جديدة")
    // تحقق: doc.ApplicantRegistryId == newParentEntry.Id
}
```

**اختبار 4 — حالة حدية `null` (ملف بلا ربط):**
```csharp
[Fact]
public async Task MoveEntry_DocumentWithoutApplicantLink_ApplicantRegistryId_StaysNull()
{
    // ملف executed بلا ApplicantPublicEntities (أو بلا RegistryId)
    // MoveEntry على قيد غير مرتبط — لا يجب أن يضع GroupId
    // تحقق: doc.ApplicantRegistryId == null
}
```

**اختبار 5 — إعادة الحفظ لا تغير القيمة (تكافؤ مع Apply):**
```csharp
[Fact]
public async Task ApplicantRegistryId_AfterGovernance_EqualsAfterReSave()
{
    // بعد MoveEntry — اقرأ doc.ApplicantRegistryId
    // أعد حفظ الملف عبر DocumentService.UpdateAsync بلا تغيير
    // تحقق: القيمة قبل وبعد متطابقة (= EntryId)
}
```

> كل اختبار يجب أن يتحقق **قبل وبعد** أن القيمة ليست `GroupId` — مثلاً `Assert.NotEqual(targetGroup.Id, doc.ApplicantRegistryId)` حيث `targetGroup.Id != targetEntry.Id` (مضمون لأن `GroupId` ≠ `EntryId` دائمًا في البيانات المزروعة).

### الخطوة 3 — التحقق الإلزامي الكامل (قبل اعتبار المهمة منجزة)

```powershell
# من backend\
dotnet test                          # 794 + 283 + الجدد أخضر — لا فشل
dotnet build --no-restore            # 0 تحذير

# من frontend\ (حتى لو لم تُمس الواجهة — تحقق عدم كسر)
npx oxlint src                       # 0/0
npx tsc -b                           # EXIT 0
npm run build                        # ✓
npx vitest run                       # 676 + الجدد أخضر

# تدقيق بقايا/عقود
# grep: تأكد أن لا موضع بقي يكتب GroupId في ApplicantRegistryId
rg "ApplicantRegistryId\s*=\s*.*GroupId" backend/src
# يجب أن يعيد 0 نتائج بعد الإصلاح
```

**قائمة تحقق ذاتية قبل الإعلان:**
1. **تدقيق العقود:** تتبع `ApplicantRegistryId` من `Document.cs:102` → `Apply:858` → `PublicEntityService` الخمسة → `PortalRepository` (لا استهلاك) → `DTO:806` — كلها `int?` متطابقة.
2. **فحص بقايا:** `rg ApplicantRegistryId` يظهر فقط `Apply:858` و `Delegation:702` و الخمسة المواضع المُصلحة (كلها اشتقاق) — لا `GroupId` مباشر.
3. **مطابقة الخطة:** كل بند أعلاه مُنفذ — 5 مواضع + 4 اختبارات + تحقق كامل.
4. **الاصطلاحات:** لا تكرار — نمط واحد موحد، لا حلول مختصرة، لا أسرار.

---

## 4) المخاطر والالتزامات

- **لا هجرات** — تغيير منطقي صرف، لا migration، لا `dotnet ef database update`.
- **لا تغيير كاسر** — `ApplicantRegistryId` لم يكن مستهلكًا في الفلترة، فإصلاحه لا يكسر سلوكًا ظاهرًا؛ فائدته وقائية.
- **البيانات القائمة:** الملفات التي خضعت لحوكمة سابقة قد تحمل `GroupId` قديمًا في `ApplicantRegistryId` — سيُصحح تلقائيًا عند أول حفظ لاحق (`Apply:858`)، أو يمكن سكربت لمرة واحدة إن رغب المالك (خارج نطاق هذه الخطة — يُؤجل).
- **الأمان:** لا صلاحيات جديدة، لا أسرار، احترام `Date Fields Rule` و `mobile-first` (لا واجهة في هذه الخطة).

---

## 5) معايير التسليم (Definition of Done)

- [ ] الخمسة مواضع في `PublicEntityService.cs` تستخدم الاشتقاق الموحد — `rg "ApplicantRegistryId.*GroupId"` = 0
- [ ] 4 اختبارات جديدة خضراء (حارس EntryId + Merge + Abolish + حالة حدية null)
- [ ] `dotnet test` (794+283+4) + `dotnet build` + `npx oxlint` + `npx tsc -b` + `npx vitest run` كلها خضراء
- [ ] لا تغيير في `ExecutionApplicants` (مفصول — مُثبت)
- [ ] تقرير إنجاز يذكر المواضع الخمسة بالسطر ويؤكد عدم الحاجة لهجرات

---

## 6) ملاحظات ختامية للمحادثة الجديدة

1. اقرأ `Document.cs:102` + `Apply:858` + `PublicEntityService:1280-1310/1340-1390/1475-1495/1735-1765/2450-2490` كاملة قبل أي تعديل.
2. نفّذ الخطوة 1 (5 مواضع) في `commit` واحد صغير — لا تُجزئ بلا داعٍ (تغيير واحد متماسك).
3. أضف الاختبارات في نفس الـ `commit` أو تالٍ مباشر — لا تؤخر التغطية.
4. شغّل التحقق الكامل + `rg` قبل الدفع — لا تعتبر المهمة منجزة قبل الأخضر الكامل.
5. اذكر في تقرير الإنجاز: "لا هجرات — إصلاح منطقي صرف لتوحيد `ApplicantRegistryId` على `EntryId`".

> **المرجع:** هذه الخطة مبنية على مراجعة تحليلية شاملة للمرحلة ب (`73e68cf`) وفحص `diff` الكامل عن `HEAD` وتدقيق عقدي حقلًا بحقل.
