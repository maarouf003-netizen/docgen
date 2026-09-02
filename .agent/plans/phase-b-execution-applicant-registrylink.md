# المرحلة ب — ربط طالب التنفيذ (ExecutionApplicant) بسجل الجهات العامة عبر `RegistryId`

> **الحالة:** المرحلة أ مكتملة ومُتحقق منها (784 BE / 283 BE-API / 674 FE أخضر، بلا هجرات).
> **هذه الخطة للتنفيذ في محادثة جديدة** — تغيير بنيوي بهجرتين (SQLite + Postgres).

---

## 1) السياق والغرض

### الفجوة الحالية (بعد المرحلة أ)
- في الملفات «منفذ عليها» (`GeneralEntitySide:executed/deposit` = `IsExecutedLike:34` في `Domain/Enums/GeneralEntitySideCatalog.cs:34`) طرف **الطالب** هو `ExecutionApplicant.cs:12` (`Name/Father/Family/ApplicantNature:natural|legal` + حقول الاعتباري) **بلا أي `RegistryId`** — لا رابط بـ `PublicEntity` (عكس `ApplicantPublicEntity.cs:18` و `ExecutedPublicEntity.cs:21` اللذين يحملان `int? RegistryId + Registry` مع `SetNull`).
- النتيجة: `PublicEntityService.cs:1709/2387 (RegistryId)` + `SyncTextsAfterRename:969` + `SyncAppealsAfterEntityChangeAsync:2070-2078` (بناء خريطة الأسماء) و `2082-2084` (جلب الاستئنافات) **لا تلمس `ExecutionApplicants` إطلاقًا**؛ لقطات `kind="execution-applicant"` (`DocumentAppealService.cs:741/781` `PartyId=ExecutionApplicant.Id` ببناء `TripleOr(Name,Father,Family,LegalRepresentative)`) تبقى قديمة بعد حلول/دمج/إعادة تسمية.
- مثال: ملف «منفذ عليها» طالبه `ExecutionApplicant{Id:901, Nature:legal, Name:المؤسسة السورية للتجارة}` بلا `RegistryId` — لو حُلّت تلك المؤسسة إلى «هيئة التجارة الموحدة» لا يمكن ترحيل `901` ولا مزامنة لقطته.

### هدف المرحلة ب (الحل الأصح هندسيًا)
إضافة ربط بنيوي `ExecutionApplicant.RegistryId → PublicEntity.Id` ليُعامل طالب التنفيذ **الاعتباري** (`legal`) **الذي هو جهة عامة** كجهة قابلة للحلول/الدمج/إعادة التسمية، مع مزامنة نصوصه ولقطاته كباقي الجهات.

> **نطاق دقيق:** يمس `legal + RegistryId != null` (جهة عامة مربوطة) فقط. `natural` (شخص بولي/وصي `Representative*:45`) و `legal + RegistryId==null` (شركة خاصة) **لا يُلمسان أبدًا** — يبقيان `null`. وهذا يميّز «إضافة جهة عامة» (اختيار من السجل) عن «إضافة شخص اعتباري خاص» (إدخال يدوي).

---

## 2) النطاق / خارج النطاق

**داخل النطاق:**
- `ExecutionApplicant.RegistryId` (Domain → EF → DTO → Normalize → ToDto → هجرتان → واجهة Picker → مزامنة `RegistryId`/نص/لقطة → بوابة/تدقيق اختياري).
- توسيع `IsPublicEntityKind` ليشمل `execution-applicant` ومزامنة لقطاته.

**خارج النطاق (يُؤجل):**
- `backfill` تلقائي للبيانات القديمة (`NULL` تبقى حتى يُعاد فتح الملف وربطه).
- توسيع بحث `AppealRepository` ليشمل `NULL` literal إضافي (مغطى بالمرحلة أ).

---

## 3) التنفيذ التفصيلي (B1 → B7) — سلوك + هجرة

### B1 — Domain — `ExecutionApplicant.cs:12-54`
```csharp
// بعد السطر 31 (ApplicantAddress) وقبل RepresentationType:34
public int? RegistryId { get; set; }
public PublicEntity? Registry { get; set; }
```
- النمط المرجعي: `ApplicantPublicEntity.cs:18-21` (`int? RegistryId` + `PublicEntity? Registry` nullable، تعليق «اختياري — يُفكّ الارتباط بحذف القيد»).

### B2 — EF Configuration — `Configurations.cs:509-543` (كتلة `ExecutionApplicantConfiguration`)
```csharp
builder.HasIndex(a => a.RegistryId);
builder.Property(a => a.RegistryId);
builder.HasOne(a => a.Registry).WithMany().HasForeignKey(a => a.RegistryId).OnDelete(DeleteBehavior.SetNull);
```
- النمط: `ExecutedPublicEntityConfiguration:564-575` و `ApplicantPublicEntityConfiguration:690-701` (`HasIndex` + `Property` + `HasOne.WithMany.HasForeignKey(SetNull)`).
- لا `CheckConstraint` (مثل `ExecutedPublicEntity` يصفّر `RegistryId` للـ `legal` على مستوى الخدمة فقط).

### B3 — الهجرتان (إلزامي) — `Migrations/` + `MigrationsPostgres/` + `ModelSnapshot:1503`
```powershell
# من backend\
dotnet ef migrations add AddExecutionApplicantRegistryId --context DocGeneratorDbContext --output-dir Persistence\Migrations
dotnet ef migrations add AddExecutionApplicantRegistryId --context DocGeneratorPostgresDbContext --output-dir Persistence\MigrationsPostgres
# مصنع Postgres موجود: `Api/PostgresDbContextFactory.cs:11`
# SQLite بلا مصنع — يستخدم `Program.cs:22` و `appsettings.json`
```
- كل هجرة تولّد ملفين: `*.cs` + `*.Designer.cs` + تحديث `DocGeneratorDbContextModelSnapshot.cs:1503-1574` (أنواع `INTEGER` vs `integer`).
- أمثلة مرجعية: `20260814093700_AddPartyNature.cs` (إضافة أعمدة لـ `ExecutionApplicants`) و `20260824190803_AddEntityRegistryLinks:13-60` (إضافة `RegistryId` + فهرس + FK SetNull).
- **التحقق الحارس:** `tests/SchemaIntegrityTests.cs:10-27` يطبّق هجرات SQLite ويقارن الأعمدة بحقول النموذج — أي حقل بلا عمود يفشل فورًا.
- **Deploy Reminder (إلزامي):** ذكر الهجرتين بالاسم في تقرير الإنجاز + تنبيه `dotnet ef database update --context DocGeneratorDbContext` و `--context DocGeneratorPostgresDbContext` (`RUN_GUIDE.md:117` §9).

### B4 — العقود — `DocumentDtos.cs:122-142` + `906-915` + `DocumentService.Apply.cs:544-597`
- **DTO:** إضافة `int? RegistryId = null` نهاية `ExecutionApplicantDto` (قارن `ExecutedPublicEntityDto:156` و `ApplicantPublicEntityDto:165`).
- **ToDto (معكوس):** `DocumentDtos.cs:906-915` إضافة `a.RegistryId` عند البناء.
- **NormalizeExecutionApplicants:544-597** (Clear+Rebuild 370-378): تمرير `RegistryId = dto.RegistryId` بنمط `624` (`RegistryId = isLegal ? null : dto.RegistryId` للمنفذ عليه — لكن هنا للطالب: `legal` فقط يحمل `RegistryId`؛ `natural` يُصفّر). الفرع الاعتباري `571-577` يُضاف `ApplicantRegistrationNumber/RepresentedBy/Address` + `RegistryId`; الطبيعي `575-577` يُصفّر الاعتباري.
- **Frontend عقد:** `types/index.ts:166-192` (`nature?: PartyNature` + `registryId?: number|null`) و `emptyExecutionApplicant:159-176` (بدون `registryId` حاليًا → يضاف `registryId: null`).

### B5 — الواجهة — ربط اختيار من السجل (تمييز «جهة عامة» عن «شخص اعتباري خاص»)
- **المكوّن:** `ExecutedSideSections.tsx:182-219` (قسم «👤 طالب التنفيذ» — `select` لطبيعة `natural/legal` + حقول الاعتباري `name/registrationNumber/representedBy/addressType/address`). **لا يوجد `registryId` ولا زر اختيار حاليًا** (عكس كتلة المنفذ عليه `371-386` التي تملك شارة «مرتبطة ✓» وزر «اختيار من السجل…» مشروط بـ `onPickRegistry`).
- **النافذة القابلة لإعادة الاستخدام:** `PublicEntityPickerModal.tsx:29` (`{onClose, onPick(entry: PublicEntityEntryDto)}`) — تُستخدم في `DocumentForm.tsx:77-79` (`registryPicker: {side:'applicant'|'executed', index}` + `applyRegistryPick:306-327` + `EXECUTED_IDENTITY_KEYS:263` تفك ربط عند تحرير نصي).
- **التغيير:**
  - إضافة صنف `execution-applicant` أو إعادة استخدام `side:'executed'` مع فهرس مخصص + حالة `registryPicker` موسعة.
  - زر «اختيار من السجل…» في كتلة الاعتباري داخل `ExecutedSideSections` (عند 193-219) **ظاهر فقط عند `nature==legal`** يستدعي `onPickRegistry` + `applyRegistryPick` يملأ `canonicalName/branchName/governorate` + `registryId: entry.id` + شارة خضراء «مرتبطة ✓ من السجل» + زر «فك الربط».
  - **تمييز واضح (يمنع الالتباس):** بدون اختيار يبقى `registryId==null` = شخص اعتباري خاص (إدخال يدوي) لا تلمسه حلول/دمج؛ مع اختيار يصبح جهة عامة مربوطة (`RegistryId != null` هو التعريف الوحيد — بلا حقل `EntityNature` للطالب). حقول `RegistrationNumber/RepresentedBy` تُملأ/تُقفل للجهة المربوطة حسب بيانات السجل.
  - فك ربط تلقائي عند تحرير `Name` يدويًا (نمط `263-278` يصفّر `registryId`) + زر فك يدوي.
  - `utils/apiNormalization.ts:18-27` لا حاجة لتعديل (مصفوفات فقط).

### B6 — المزامنة الفعلية (لب المرحلة ب) — عبر `RegistryId` (مع فصل `ApplicantRegistryId`)
- **`PublicEntityService.cs:1258-1261` (طي) / `1709-1712` (دمج) / `2387-2398` (حلول):** إضافة حلقة **مفصولة عن `ApplicantRegistryId`**
  ```csharp
  foreach (var a in doc.ExecutionApplicants.Where(a => a.RegistryId == entry.Id))
  {
      a.RegistryId = targetEntry.Id; // أو newParentEntry.Id
      a.Name = targetCanonicalName; // للاتساق: تحديث Name الاعتباري مع RegistryId (مثل Applicant/ExecutedPublic)
  }
  // فصل إلزامي: Document.ApplicantRegistryId:857 (المشتق من ApplicantPublicEntities فقط) لا يُمس هنا — ExecutionApplicant جهة في وضع «منفذ عليه» وليست الجهة الطالبة الكلاسية (ApplicantPublicEntity)
  ```
  + `ListDocumentsLinkedToEntryAsync:99,112,143` و `IPublicEntityRepository:64` يجب توسيعه ليشمل `ExecutionApplicants` (إضافة استعلام `Where ExecutionApplicants.Any(a => a.RegistryId == entryId)` + `Include` موجود 143).
- **`SyncTextsAfterRenameAsync:969-1051` و `SyncTextsAfterFoldAsync:2048-2061`:** إعادة بناء `SearchText/FullData` و `RotationDisplayName` (`DocumentService.Search.cs:170-172` التي تقرأ أول `ExecutionApplicant`) التي لم تُلتقط بالمزامنة الاسمية.
- **`SyncAppealsAfterEntityChangeAsync:2070-2078` (بناء خريطة الأسماء) + `2082-2084` (جلب الاستئنافات):** إضافة داخل حلقة `2070-2078`
  ```csharp
  foreach (var a in doc.ExecutionApplicants.Where(a => a.RegistryId.HasValue))
      newNames[("execution-applicant", a.Id)] = a.Name!; // صحيح لأن النطاق legal فقط → TripleOr(Name,null,null,null) == Name — للـ natural لا يطبق (RegistryId==null ضمنيًا)
  // isLegal ضمنيًا عبر RegistryId != null — لا يوجد EntityNature للطالب، التعريف الوحيد للجهة العامة هو RegistryId != null (عكس ExecutedPublicEntity.EntityNature:public/legal:624)
  ```
- **`AppealSnapshotSerializer.cs:77-78` (`IsPublicEntityKind`):** إضافة `|| kind == "execution-applicant"` (ثابت جديد `KindExecutionApplicant = "execution-applicant"` مع `const string` مثل `KindApplicantEntity:23`).
- **ملاحظة طبيعة:** لا تلمس `natural` إطلاقًا (`Where ApplicantNature == "legal"` ضمنيًا عبر `RegistryId != null`).

### B7 — وعي إضافي (قرار يُحسم قبل التنفيذ — ليس تجميليًا)
- **بوابة (قرار منتج — توصية إلزامي):** `PortalRepository.cs:25-36` (`ScopePredicate`) حاليًا يفحص `ApplicantPublicEntities` + `ExecutedPublicEntities(public)` فقط. **يجب تقرير قبل التنفيذ:** هل يرى مندوب جهة عامة ملفات «منفذ عليها» التي هو طالبها (`ExecutionApplicants.RegistryId` ضمن `ids`)؟ منطقيًا نعم (قاعدة الرؤية الموحدة)، لكنه تغيير سلوكي للبوابة. **التوصية:** توسيع `ScopePredicate:25-36` ليشمل `d.ExecutionApplicants.Any(a => a.RegistryId != null && ids.Contains(a.RegistryId.Value))` + توسيع العداد `PortalRepository:212-227`. إن رُفض، يُوثق القرار صراحةً أن ملفات طالب التنفيذ الجهة ستبقى خارج نطاق مندوبها حتى بعد الربط (تعارض محتمل مع § «مصدر حقيقة واحد» 20-23).
- **تدقيق (إلزامي جزئي):** `DocumentChangeTracker.cs:158,290-291` — توقيع `__Col_ExecutionApplicants` حاليًا `Join(Name,Father,Family)` فقط؛ **إضافة `RegistryId` للتوقيع** (`Join(..., RegistryId?.ToString())`) حتى يظهر تغير الربط في سجل التدقيق/الوقوعات — إلزامي لاتساق المراجعة.
- **بحث/توقيع:** `PublicEntityRepository.cs:99,112` — توسيع `ListApplicantRows...` إن لزم لعد النطاق.
- **إنابة:** `DocumentDelegationService.cs:693-705` — إن وُسّعت لعائلة «منفذ عليها» لاحقًا.

---

## 4) الاختبارات (إلزامي — يُضاف/يُحدّث مع كل تغيير سلوكي)

- **Backend:**
  - `AppealSnapshotSerializerTests` — حالات `execution-applicant` (مطابقة `PartyId`، عدم مس `natural`).
  - `PublicEntityServiceTests` — حلول/دمج/إعادة تسمية لملف «منفذ عليها» طالبه `legal + RegistryId` (تأكيد ترحيل `RegistryId` + تحديث `Name` + لقطة `execution-applicant` + وقوع `entity-change`).
  - `SchemaIntegrityTests` + `EntityRegistryLinkTests` — هجرات + ربط RegistryId.
  - حالات حدّية: `RegistryId==null` لا يُرحّل، `natural` لا يُلمس، `legal` خاص بلا رابط لا يُلمس، وقوعات لا تُنشأ لغير المتأثر.
- **Frontend:**
  - `ExecutedSideSections.test.tsx` / `DocumentForm.test.tsx` — اختيار جهة من السجل لطالب التنفيذ الاعتباري، شارة «مرتبطة ✓»، فك ربط عند تحرير نصي، إرسال `registryId` في payload.
  - `vitest` يبقى `type="text"` + `placeholder="مثال: 1/8/2026"` للتواريخ (لا `type="date"`).

---

## 5) التحقق الإلزامي (قبل اعتبار المهمة منجزة)

```
backend:  dotnet test                          # 784 + جديد (Application) + 283 (Api) أخضر
          dotnet build                         # 0 تحذير
frontend: npx oxlint src                       # 0/0
          npx tsc -b                           # EXIT 0
          npx vitest run                       # 674 + جديد أخضر
          npm run build                        # ✓
grep:     UpdateEntityName = 0 (باستثناء Migrations/Designer)
          SnapshotJsonOptions خارج Serializer = 0
```

---

## 6) مخاطر والتزامات نشر

- **هجرتان جديدتان** (`AddExecutionApplicantRegistryId` بـ SQLite + Postgres) — **لا تُطبّق تلقائيًا** عند النشر؛ يجب `dotnet ef database update --context DocGeneratorDbContext` و `--context DocGeneratorPostgresDbContext` في نافذة النشر (`RUN_GUIDE.md §9`) وإلا `no such column: RegistryId`.
- **بيانات قديمة:** `RegistryId` يبقى `NULL` حتى يُعاد فتح الملف وربطه يدويًا — لا `backfill` تلقائي في هذه الخطة (مقبول).
- **أداء:** `MigrateDelegates` + `ListDocumentsLinkedToEntryAsync` الموسع قد يحمّل ملفات أكثر — يبقى ضمن معاملة واحدة (حذر `SaveChanges` المتعدد مسموح).
- **لا أسرار/صلاحيات واسعة؛ احترام `Date Fields Rule` و `mobile-first` (44px `min-h-11`, `focus-visible:ring-*`).**

---

## 7) قائمة تحقق للمحادثة الجديدة

1. قراءة `ExecutionApplicant.cs:12` + `Configurations.cs:509` + `DocumentDtos.cs:122` + `ExecutedSideSections.tsx:182` كاملة.
2. تنفيذ B1 → B2 → B3 (هجرتان) → B4 → B5 → B6 → B7 بالترتيب، commit صغير لكل خطوة.
3. إضافة/تحديث اختبارات مع كل خطوة (لا تُهمل `null`/فارغ/تالف).
4. تشغيل التحقق الإلزامي كاملًا + تدقيق عقود حقلًا بحقل + `grep` بقايا.
5. تقرير إنجاز يذكر الهجرتين بالاسم والعدد + تنبيه تطبيق `dotnet ef database update` للسياقين.

---

## 8) ملاحظات ختامية

- **لا تغييرات كاسرة** للعقود الحالية (`ExecutionApplicantDto` يضيف حقلًا اختياريًا `RegistryId?` فقط).
- **المرحلة ب لا تمس الشخص الطبيعي إطلاقًا** — فقط `legal + RegistryId` (جهة عامة مربوطة).
- **البديل الاسمي السريع (مطابقة `Normalize` بلا `RegistryId`)** استُبعد لصالح الحل البنيوي الأصح رغم ثقله — قرار مالك المشروع.

> **المرجع:** هذه الخطة مبنية على فحص شامل لـ `GeneralEntitySideCatalog:34`, `ExecutionApplicant:12`, `ApplicantPublicEntity:18`, `ExecutedPublicEntity:21`, `Configurations:509/545/677`, `DocumentService.Apply:544/370/857`, `DocumentDtos:122/906`, `DocumentAppealService:741/781`, `PublicEntityService:2070/2387`, `Serializer:77`, `types:166`, `ExecutedSideSections:182/371`, `PortalRepository:25`, `DocumentChangeTracker:290`, `RUN_GUIDE:117`.
> **تحديث ما بعد المراجعة التحليلية (2026-09-02):** صُححت أرقام `SyncAppeals:2084→2070-2078/2082-2084`، ووُضح فصل `ApplicantRegistryId:857`، وتمييز `legal:RegistryId!=null` كتعريف وحيد للجهة العامة للطالب، وتوضيح `TripleOr==Name` للنطاق `legal`، وترقية `B7` من «اختياري» إلى «قرار منتج يُحسم» (انظر B7).
