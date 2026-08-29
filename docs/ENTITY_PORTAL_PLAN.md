# خطة تنفيذ ميزة «بوابة الجهات العامة» (Entity Portal)

> **حالة الوثيقة:** مواصفة تنفيذية معتمدة — ناتجة نقاش 2026-08-24 مع صاحب المشروع.
> **قاعدة العمل:** أي محادثة تنفيذ تبدأ بقراءة هذا الملف كاملًا وتنفذ مرحلةً واحدة على الأقل مع
> اختباراتها وهجرتَيها قبل إعلان الإنجاز. لا يُعدَّل قرارٌ معتمد أدناه إلا بموافقة صاحب المشروع.

---

## 1) الهدف والنطاق

تمكين مندوبي/مدراء الدوائر القانونية في **الجهات العامة** من الاطلاع القرائي فقط على الملفات
التنفيذية العائدة لجهاتهم وفروعها، عبر:

1. **سجل مرجعي مركزي للجهات** يحل مشكلة الإدخال النصي الحر (اختلاف الكتابة / أسماء قديمة).
2. ربط كل جهة داخل ملف بهوية مرجعية بالمعرّف `RegistryId` (مع إبقاء الأعمدة النصية متزامنة للعرض).
3. **بوابة قراءة فقط** للمندوبين بتصدير Excel، منفصلة عن صلاحيات المحامين تمامًا.

### النطاق يشمل الجهات في الصفتين
- **طالبة تنفيذ** (`ApplicantPublicEntity` + حقل `Document.Applicant` النصي).
- **منفذ عليها** (`ExecutedPublicEntity` — وقد يتعدد في الملف الواحد؛ الرؤية بأي تطابق طرفي).

### خارج النطاق (مرحلة أولى)
- توليد المستندات أو أي كتابة من البوابة.
- دمج القيود المكررة (`Merge`) — مؤجل للمرحلة الثانية بقرار صريح.
- إحصاءات متقدمة للجهة — المرحلة الرابعة.

---

## 2) القرارات المعتمدة حرفيًا من النقاش (غير قابلة للتغيير دون موافقة)

| # | القرار |
|---|---|
| د1 | الرؤية تشمل الجهات **طالبة التنفيذ ومنفذ عليها** معًا، بقاعدة «أي تطابق طرفي». |
| د2 | بنية الهوية بمستويين: **Group** (الهوية الأم: وزارة/إدارة/هيئة/مؤسسة/شركة) ثم **Entry** (المحافظة + الفرع). لا شجرة عامة أعمق من ذلك. |
| د3 | إدارة السجل والاعتماد: **المدير ورؤساء الأقسام**. المشرف يملك صلاحيات المدير (وفق نمط `RolePermissions.HasFullAccess`). |
| د4 | اقتراح جهة جديدة من محامٍ يدخل بحالة `Pending` ولا يظهر لبوات المندوبين ولا يُربط نهائيًا حتى اعتماد رئيس قسم. |
| د5 | **إعادة تسمية جماعية فورية**: المدير/المشرف على كل السجل؛ رئيس القسم مقصورًا على قيود محافظة فرعه (يتطلب عمود `Governorate` على `Branch`). إعادة التسمية تُزامن الأعمدة النصية في كل الصفوف المرتبطة ضمن معاملة واحدة، وتُدوَّن في سجل تعديلات الحقول (قبل/بعد) للملفات المتأثرة. |
| د6 | تصحيح رابط الجهة في **ملف بعينه** يتم من المحامي المالك عبر نموذج التعديل (تغيير `RegistryId`) ويُدوَّن آليًا في سجل تعديلات الحقول. رئيس القسم لا يعدل بيانات ملف غيره؛ تصحيحه من مستوى السجل (د5) فقط. |
| د7 | **تحذير الإدخال** فوق اسم الجهة الجديدة بالنص الحرفي: «يرجى ادخال اسم الجهة العامة بدقة مع ممثلها القانوني بدون عبارة اضافة لوظيفته أو منصبه تمثله ادارة قضايا الدولة» — و`placeholder` الحقل: «مثال: المدير العام للمصرف التجاري السوري». |
| د8 | **قائمة منسدلة للصيغة** بين حقل الاسم وحقل الفرع بخيارين: `إضافة لوظيفته` / `إضافة لمنصبه` — قيمتها تُخزَّن على **قيد الجهة (Entry)** وتُعرض عند توليد/عرض ممثلها القانوني لاحقًا. |
| د9 | **بدون عدّاد ملفات** في نتائج بحث نافذة الإدخال (قرار صريح بإسقاطه). |
| د10 | بوابة المندوب: قراءة فقط + **تصدير Excel**، يرى **كل الحقول والبطاقات** وملفات جهته واستئنافاتها فقط (استثناءات حقول مستقبلية تُناقش لاحقًا). |
| د11 | حسابات المندوبين داخل نفس النظام (نفس شاشة الدخول، دور جديد)، يضيفهم المدير/المشرف/رئيس القسم. |
| د12 | البيانات التاريخية النصية تُرحَّل عبر أداة استيراد تجمع الكتابات المتشابهة بعدّاد ملفاتها، وتُعلَّم **نهائية مباشرة** (بلا انتظار اعتماد). |

---

## 3) نموذج البيانات (Domain)

```csharp
// DocGenerator.Domain/Entities/PublicEntityGroup.cs
public class PublicEntityGroup
{
    public int Id { get; set; }
    public string CanonicalName { get; set; }        // مطلوب، max 200، فهرس فريد
    public string EntityType { get; set; }           // كتالوج نصي: ministry/administration/authority/foundation/company
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public ICollection<PublicEntity> Entries { get; set; }
}

// DocGenerator.Domain/Entities/PublicEntity.cs
public class PublicEntity
{
    public int Id { get; set; }
    public int GroupId { get; set; }                 // FK Cascade
    public string Governorate { get; set; }          // مطلوب، max 100 (كتالوج المحافظات الموجود في FE utils/governorate.ts)
    public string BranchName { get; set; }           // مطلوب، max 200 («الفرع الرئيسي» قيمة افتراضية مسموحة)
    public string CitationFormula { get; set; }      // "add-to-job" | "add-to-position"  ← د8
    public string Status { get; set; }               // EntityStatusCatalog.Final | Pending  ← د4
    public int CreatedById { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; } = true;
    PublicEntityGroup Group { get; set; }
    User CreatedBy { get; set; }
    ICollection<PublicEntityAlias> Aliases { get; set; }
}

// PublicEntityAlias.cs : Id, PublicEntityId FK, AliasText(500) — فهرس على AliasText للبحث

// روابط الملفات:
//  - ApplicantPublicEntity += int? RegistryId FK(SetNull) + فهرس
//  - ExecutedPublicEntity  += int? RegistryId FK(SetNull) + فهرس
//  - Document              += int? ApplicantRegistryId (نسخة تسريع لفلترة الطالب، تُحدَّث عند الحفظ)

// DocGenerator.Domain/Entities/PublicEntityProposal.cs  (اقتراح المحامي — د4/د7/د8)
public class PublicEntityProposal
{
    public int Id { get; set; }
    public string ProposedName { get; set; }         // كما أدخلها المحامي
    public string EntityType { get; set; }
    public string Governorate { get; set; }
    public string BranchName { get; set; }
    public string CitationFormula { get; set; }
    public int ProposedById { get; set; }
    public int? SourceDocumentId { get; set; }        // الملف الذي قُدِّر منه (اختياري للسياق)
    public ProposalStatus Status { get; set; }        // Pending | Approved | Rejected
    public int? RejectedById { get; set; }
    public string? RejectionReason { get; set; }
    public int? CreatedPublicEntityId { get; set; }   // يُملأ عند الاعتماد
    public DateTime CreatedAt { get; set; }
}

// Enums جديدة: PublicEntityTypeCatalog, EntityStatusCatalog(Final/Pending),
// CitationFormulaCatalog(AddToJob/AddToPosition), ProposalStatusCatalog(Pending/Approved/Rejected)

// تعديلات على كيانات قائمة:
//  - Branch += string? Governorate (max 100)  ← لد5
//  - UserRole enum += EntityManager
```

### مزامنة الأعمدة النصية (شرط ثابت)
عند إنشاء/اعتماد/إعادة تسمية أي Entry تُحدَّث فورًا (بنفس المعاملة):
- `ExecutedPublicEntity.EntityName = Group.CanonicalName (+ BranchName حسب سياق العرض الحالي)` —
  يُحافظ على الشكل النصي الحالي للعرض، والتغيير الجوهري هو ضبط الاسم المعتمد.
- `Document.Applicant` لصفوف الطالب المرتبطة.
- كل فرق يُسجَّل عبر `DocumentChangeTracker.Diff` في سجل تعديلات الحقول (قبل/بعد) للملفات المتأثرة
  (دفعة واحدة عبر `LogManyAsync` أو صفوف مجمعة — يقرر المنفذ بين النمطين بما يحمي الأداء).

---

## 4) الصلاحيات وقواعد الرؤية

### إضافات `RolePermissions.cs`
```csharp
CanManageEntityRegistry(role)  => Manager || Admin || Head   // د3 (رئيس القسم مقيّد بمحافظته عند التنفيذ)
CanApproveEntityProposals(role)=> Head                        // د4 (ضمن نطاق محافظته)
CanUseDelegatePortal(role)     => EntityManager               // د10/د11
```

### مصفوفة الرؤية
| الفاعل | النطاق |
|---|---|
| محامٍ مالك / رئيس قسم / مدير / مشرف | كما هو اليوم بلا تغيير |
| EntityManager مربوط بـ **GroupId** | كل الملفات التي فيها أي طرف (طالب أو منفذ) بقيد ينتمي لمجموعته |
| EntityManager مربوط بـ **EntryId** | الملفات ذات القيد نفسه حصرًا |
| البوابة دائمًا | قراءة + تصدير إكسل فقط؛ تُمنع نقاط الإنشاء/التعديل/الحالة/التوليد بنيويًا (مسارات منفصلة) |

### سجل تدقيق جديد
`view_entity_portal_files` عند كل دخول جلسة عرض (وليس كل صفحة)، و`export_entity_portal_excel`.

---

## 5) تجربة الإدخال (Frontend — د9/د7/د8)

1. زر «اختيار الجهة العامة» (بديل الحقل النصي الحر) يفتح نافذة `PublicEntityPickerModal`:
   - حقل بحث واحد؛ النتائج مجمّعة/قابلة للتبديل حسب **المحافظة** (كتالوج `utils/governorate.ts`)،
     وتحتها اقتراحات **الفرع** المستخلصة من القيود المطابقة. **بلا عدّاد ملفات** (د9).
   - زر «جهة غير موجودة؟ اقترح إضافة» يبدّل إلى نموذج الاقتراح.
2. نموذج الاقتراح (د7/د8) — ترتيب الحقول:
   - تحذير أحمر/عنابي بنص د7 (فوق الاسم).
   - اسم الجهة (placeholder د7).
   - **Dropdown الصيغة** (د8): `إضافة لوظيفته` / `إضافة لمنصبه`.
   - نوع الجهة (الكتالوج الخمسة) · المحافظة · الفرع.
   - حفظ ⇒ `POST /api/entity-registry/proposals` (Pending) + رسالة نجاح توضح أنها بانتظار الاعتماد.
3. اختيار قيد `Pending` من نتائج البحث **ممكن للمحامي على ملفه** لكن يُعلَّم بصريًا «بانتظار الاعتماد»،
   ولا يظهر لأي مندوب حتى الاعتماد (تطابق د4).

---

## 6) واجهات API المقترحة (تتبع نمط Controllers القائمة)

```
# السجل (رئيس قسم/مدير/مشرف — نطاق الرئيس بمحافظته عند الكتابة)
GET    /api/entity-registry?governorate=&q=&status=
POST   /api/entity-registry                      (إنشاء قيد نهائي مباشر للإدارة/الرئيس)
PUT    /api/entity-registry/{id}                  (تعديل/إعادة تسمية → مزامنة نصوص + تدقيق حقول)
POST   /api/entity-registry/{id}/aliases
GET    /api/entity-registry/search?q=&governorate=   (لنافذة الإدخال — متاح للمحامي)

# الاقتراحات
POST   /api/entity-registry/proposals             (محامٍ)
GET    /api/entity-registry/proposals/pending     (رئيس القسم — نطاق محافظته)
POST   /api/entity-registry/proposals/{id}/approve | /reject{reason}

# بوابة المندوب (EntityManager فقط — قراءة)
GET    /api/portal/my-scope                       (ما يُسمح له برؤيته: Group/Entry)
GET    /api/portal/files?q=&status=&page=         (قائمة ملفاته — نفس غنى DocumentsList قرائيًا)
GET    /api/portal/files/{id}                     (تفاصيل كاملة قراءةً)
GET    /api/portal/files/{id}/appeals
GET    /api/portal/export?...(نفس فلاتر القائمة)   (Excel — سقف ExportOptions.MaxRows)
```

ملاحظات تنفيذية: مصادقة Cookie+CSRF القائمة تكفي؛ دور `EntityManager` يُضاف إلى `UserRole` ويُمنع
من مسارات الكتابة كلها بنيويًا (لا يمر عبر `RolePermissions` الحالية لأنها تمنحه افتراضيًا لا شيء).

---

## 6bis) قرار المواءمة السلوكية (2026-08-28)

**المشكلة الرصدية:** التنفيذ الحالي ينشئ قيد المحامي بـ `Status=Final` فورًا ويُعلِّمه
`NeedsReview=true` (د4)، مما يجعله يظهر خطأً في نطاق بوابة المندوبين قبل اعتماد رئيس القسم
لأن بوابة المندوب ترشِّح بقاعدة `Status == Final` فقط (انظر `PortalRepository`) دون مراعاة
`NeedsReview`.

**القرار المعتمد (موافقة صاحب المشروع — "المعالجة السلوكية"):**
- نحتفظ ببنية `NeedsReview` القائمة (لا كيان `PublicEntityProposal` منفصل، ولا هجرة جديدة).
- يُعرَّف «المعتمد النهائي الظاهر لبوات المندوبين» بأنه: `Status == Final && !NeedsReview`.
- يُطبَّق هذا التعريف على **كافة المستهلكين النهائيين** (بوابة المندوب تحديدًا: نطاق الرؤية،
  التصدير، الإحصاءات) بحيث لا يظهر قيد المحامي غير المعتمد لأي مندوب قبل اعتماد رئيس القسم،
  بينما يبقى ظهوره للمحامي نفسه ولبطاقة مراجعة رئيس القسم كالمعتاد (نافذة الإدخال ولوحة المراجعة).
- الاعتماد (`approve-review`) أو التعديل المراجِع (`update`) يعطيان القيد الظهور النهائي فعلًا.

**الأثر:** DoD#5 (اقتراح محامٍ لا يظهر في نتائج بوابة المندوبين قبل الاعتماد) تتحقق سلوكيًا
بإصلاح المستهلكين دون تغيير مخطط البيانات.

---

## 7) المراحل والتنفيذ

### المرحلة 1 — السجل والحوكمة والاستيراد *(هذه المواصفة تُنفَّذ أولًا)*
- كيانات Domain + Configurations + DbSets + كتالوجات Enums.
- `Branch.Governorate` (هجرة) + شاشة إدارة الفروع تُحدّثه.
- `PublicEntityService`: CRUD + بحث + اعتماد/رفض + **RenameEntry** (مزامنة النصوص + تدقيق حقول
  مجمّع + حدود محافظة الرئيس) + Aliases.
- `PublicEntityRepository` + استعلامات البحث/الصفحات.
- `EntityRegistryController` + `RolePermissions` + DI.
- شاشات FE: إدارة السجل (لرئيس/مدير) + قائمة انتظار الاقتراحات لرئيس القسم (بطاقة في Dashboard
  بنمط التنبيهات + صفحة إدارة).
- أداة الاستيراد: endpoint إداري `POST /api/entity-registry/import-preview` ثم `/import-commit`
  يجمعان `DISTINCT` النصوص (طالب/منفذ) بعد تطبيع `ArabicNameNormalizer` مع عدّادات، ويعتمدان
  الربط الجماعي — الحالة الناتجة **Final** مباشرة (د12).
- هجرتان: SQLite/Postgres باسمَي `AddEntityRegistry` / `AddEntityRegistryPg`.
- اختبارات: خدمة السجل (CRUD/نطاق الرئيس/المزامنة/التدقيق)، الاعتماد، الاستيراد، الصلاحيات.

### المرحلة 2 — نافذة الإدخال وربط الملفات
- `PublicEntityPickerModal` (د9/د7/د8) + ربط `RegistryId` في نموذجَي الطالب والمنفذ + مزامنة
  `Document.ApplicantRegistryId` عند الحفظ + ظهورها في سجل تعديلات الحقول تلقائيًا (المحرك القائم
  يتتبع الحقول الجديدة بمجرد إضافتها للكيانات — تأكد من تحديث قاموس `FieldLabels`).
- هجرتان (أعمدة RegistryId الثلاثة).
- اختبارات: اختيار/اقتراح/اعتماد يُظهر القيد، تغيير الربط يُسجل قبل/بعد.

### المرحلة 3 — بوابة المندوب
- دور `EntityManager` + شاشة إضافته (من مدير/مشرف/رئيس قسم) مربوطة بـ Group أو Entry.
- مسارات `portal/*` القرائية + تصدير Excel + بطاقة استئنافات قرائية.
- Layout: عند دور المندوب تُعرض قائمة بوابة مختصرة فقط (ملفاتي/تصدير) دون باقي البنود.
- اختبارات تكامل: عزل النطاق (ملف بلا تطابق = 404/Forbid)، التصدير بسقف الصفوف، منع الكتابة.

### المرحلة 4 — إحصاءات الجهة (لاحقًا بطلب صريح).

---

## 8) أنماط مرجعية إلزامية من الكود القائم (للمحادثة المنفذة)

| الحاجة | المرجع |
|---|---|
| خدمة + مستودع + متصل + DI | `HeadAlertService` / `HeadAlertRepository` / `AlertsController` |
| مصفوفة الصلاحيات | `Api/Authorization/RolePermissions.cs` + `ClaimsPrincipalExtensions.GetRoleEnum()` |
| تواريخ/أسماء عربية | `Common/ActionDateParser.cs`, `Common/ArabicNameNormalizer.cs`, `FreeDateParser.Parse(value, fieldName)` |
| تدقيق حقول قبل/بعد | `Application/Common/Audit/DocumentChangeTracker.cs` + `IAuditLogger.LogDocumentChangeAsync` |
| هجرات مزدوجة | أوامر `dotnet ef migrations add … --context … --output-dir Persistence\Migrations` و `MigrationsPostgres` + تنبيه `RUN_GUIDE.md §9` |
| اختبارات DB | `TestDb.Create()` (SQLite in-memory) + `FakeAuditLogger` |
| FE: axios/CSRF | `src/api/client.ts` (`api`, `getApiErrorMessage`) |
| FE: نافذة بحث منسدلة | نمط `ColumnFilter` في `DocumentsList.tsx` (Portal + Escape + خارج النقر) |
| FE: بطاقة tile تفاعلية | نمط `interactiveTile` في `components/view/FileDataCard.tsx` |
| FE: تصدير إكسل | نمط `responseType: 'blob'` في `DocumentsList.tsx` MoreMenu |
| FE: محافظات | `utils/governorate.ts` |

---

## 9) معايير قبول المرحلة 1 (Definition of Done)
1. `dotnet test` أخضر بالكامل مع اختبارات المرحلة الجديدة (خدمة/نطاق/مزامنة/استيراد/صلاحيات ≥ 12 اختبارًا).
2. `npx oxlint src && npx tsc -b && npx vitest run && npm run build` خضراء مع اختبارَي شاشة الإدارة والاقتراحات.
3. سكربت سلسلة الهجرتين كاملًا من قاعدة فارغة بلا أخطاء (`dotnet ef migrations script` للسياقين).
4. إعادة تسمية قيد من رئيس قسم بمحافظة أخرى = 403، ومن مدير = تنفيذ + صفوف تدقيق حقول للملفات المتأثرة.
5. اقتراح محامٍ جديد لا يظهر في نتائج بوابة المندوبين قبل الاعتماد (اختبار عزل).
6. تحديث `CHANGELOG.md` بقسم Added + جدول الهجرات، وكومِت بنمط `feat(entities): …` ثم push.

## 10) تنبيهات النشر الثابتة
كل مرحلة تتضمن هجرات: يجب تنفيذ `dotnet ef database update` للسياقين (`DocGeneratorDbContext`
و`DocGeneratorPostgresDbContext`) في نافذة النشر، وإلا فشل التشغيل (`no such table/column`).
