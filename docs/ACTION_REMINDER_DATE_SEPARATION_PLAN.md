# خطة فصل تاريخ التذكير عن تاريخ الإجراء/الملاحظة (إصلاح عيب «نقل التذكير يغيّر تاريخ الإجراء»)

> **الحالة:** **النسخة 2** — معتمدة في النهج (الخيار **١ + ٣** من النقاش)، ومُعدَّلة بعد مراجعة نقدية تحليلية (تقرير منفصل؛ انظر §15 لسجل التعديلات). **لا يبدأ التنفيذ قبل إشارة بدء صريحة.**
> **البيئة الحيّة:** `PostgreSQL/Supabase` (صفر هجرات معلّقة) — وبياناتها تجريبية، فلا تعبئة بيانات ولا إصلاح تواريخ سابق.
> **خارج هذه الخطة:** صفحة «تذكيرات الشطب» (مهمة تالية مستقلة)، وعرض «مستحق في …» في نافذة الإجراءات (مؤجَّل بقرار المالك).
> **قاعدة مبدأية:** لا حلول مؤقتة ولا أطر توافق زمنية (Compatibility shims) — العقد يُحدَّث على الطرفين في الإصدار نفسه، والخطر المتبقي يُدار بالنشر والتوثيق لا بكود انتقالي.

---

## 1) المشكلة (تشخيص مؤكد بالمراجع)

### 1.1 السلوك الحالي
`RescheduleReminderAsync` يكتب **تاريخ الإجراء نفسه** بقيمة مشتقة من التذكير:

```csharp
// backend/src/DocGenerator.Application/Services/ExecutionActionService.cs:194-197
var oldActionDate = action.ActionDate;
var newActionDate = due.AddDays(-ActionReminderCalculator.DurationDays(trimmedDuration))
                       .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
action.ActionDate = newActionDate;
```

ونفس السطر مكرر في `DocumentAppealService.RescheduleAppealReminderAsync` (السطور 938-940).

### 1.2 الأثر المزدوج
1. **قيمة**: تاريخ الواقعة القانونية يُستبدل بقيمة مشتقة من قرار جدولة.
2. **صيغة**: النص الذي أدخله المحامي (`10-11-2026`) يُعاد كتابته بصيغة `yyyy-MM-dd` (`2026-11-11`)، والنافذة تعرض النص المخزّن حرفيًا (`ExecutionActionsModal.tsx:324`).

### 1.3 تتبّع الحالة المُبلَّغ عنها
| الخطوة | المدخل | الناتج الحالي |
|---|---|---|
| إضافة إجراء | `10-11-2026` + «3 أيام» | استحقاق `13-11-2026` ✓ |
| نقل التذكير | `14-11-2026` | `ActionDate = 2026-11-14 − 3 = "2026-11-11"` ✗ |
| فتح النافذة | — | يظهر `2026-11-11` بدل `10-11-2026` ✗ |

### 1.4 لماذا لم تمنعه الاختبارات؟
لأنها **تؤكد العيب** صراحةً: `RescheduleReminderTests` (الأسطر 86، 110، 178-184، 194، 332) تُثبّت أن `ActionDate` يتغيّر. أي إصلاح مستقبلي كان سيُفشِلها.
⇒ **أول خطوة تنفيذية هي إثبات ذلك عمليًا**: تشغيل المجموعة كما هي (خضراء وهي تحمي العيب)، ثم إضافة الاختبار الأحمر الجديد (§8.2) قبل الإصلاح — فلا يُقبل أي تغيير بلا انتقالة أحمر→أخضر موثّقة.

---

## 2) النموذج المستهدف والثوابت (Invariants)

| # | الثابت | كيف يُضمن |
|---|---|---|
| **I1** | `ActionDate` (ملفات/استئنافات) **لا يكتبه أي مسار تذكير** | حذف الكتابة من مسارَي النقل + حذف حقول التذكير من عقد التعديل |
| **I2** | `ReminderBaseDate` يُضبط **مرة** عند إنشاء التذكير، ويعدّله **النقل فقط** | `??=` عند الإنشاء/التهيئة، وإسناد صريح في النقل |
| **I3** | `DueDate = (ReminderBaseDate ?? parse(ActionDate)) + DurationDays` | دالة واحدة `ActionReminderCalculator` |
| **I4** | حفظ الإجراء (تعديل) **لا يكتب** أي حقل تذكير | حذف الحقول من `UpdateExecutionActionRequest`/`UpdateAppealActionRequest` |
| **I5** | لا تذكير ⇒ الحقول الثلاثة `NULL` | مسار الإلغاء + `NormalizeReminder` |
| **I6** | تعديل تاريخ الإجراء **لا يحرّك** تذكيرًا قائمًا | `ReminderBaseDate` لا يُعاد ضبطه في مسار التعديل |
| **I7** | لا نص تاريخ يُعاد تنسيقه | كل حسابات التذكير على `DateTime` لا على `string` |

### 2.1 القواعد السلوكية (مرجع المستخدم — تُنسخ في تعليق الكيان)
| الفعل | تاريخ الإجراء | موعد التذكير |
|---|---|---|
| تعديل **النص** | لا يتغير | لا يتحرك |
| تعديل **المدة/اللون** | لا يتغير | يتحرك (`Base + المدة`) |
| تعديل **تاريخ الإجراء** | يتغير (بإدخال المستخدم) | **لا يتحرك** (يُحرَّك من «نقل التذكير») |
| **نقل التذكير** | **لا يتغير** | يتحرك إلى اليوم المطلوب |
| **إلغاء التذكير** | لا يتغير | يُحذف (الحقول الثلاثة `NULL`) |

> **قرار منتجي مرتبط بـI6 (يُوثَّق صراحةً):** تصحيح تاريخ إجراء **خاطئ الإدخال** لن يحرّك تذكيره القائم
> (التذكير صار مستقلًا). هذا مقبول الآن لأن التذكير لا يُظهر تاريخه في النافذة، لكنه سيناريو واقعي.
> **المعالجة المؤجَّلة (خارج النطاق، §12):** عند تغيير تاريخ إجراء له تذكير، يُسأل المستخدم سؤالًا
> واعيًا «هل أنقل التذكير معه؟» — بدل قرار صامت في أي اتجاه. لا يُنفَّذ الآن.

### 2.2 قرارات تصميمية وبدائل مرفوضة (لماذا هذا النموذج تحديدًا)
| البديل | سبب الرفض |
|---|---|
| **استحقاق مطلق `ReminderDueDate`** (بلا مدة محسوبة) | يلغي `dueDateSuspect` والحساب عند القراءة، لكنه يجعل المدة بلا معنى بعد الإنشاء ⇒ يفرض تغيير عرض النافذة («مستحق في» بدل «3 أيام») ومساس `PortalService` والـDTOs — وهو ما أجّله المالك. يبقى الخطوة التالية الطبيعية. |
| **كيان تذكير مستقل (جدول)** | عزل بنيوي أقصى لكن جدولان (ملفات + استئنافات) + إسقاط عمودين + إعادة كتابة 18-25 ملفًا — غير مبرَّر لعيب واحد. الخيار ١ لا يمنعه لاحقًا. |
| **تحريك التذكير بفرق تعديل تاريخ الإجراء** | يعيد الربط الخفي بين الحقلين ويناقض I6، ويجعل السلوك غير متوقع عند التصحيح. |
| **`Compatibility shim` للعقد القديم** | يخالف قاعدة المشروع (لا حلول مؤقتة)؛ النافذة المتبقية (تبويب مفتوح قبل النشر) تُدار بالنشر المتزامن والتوثيق (§13). |
| **الاكتفاء بالخيار ١ وحده** (بلا فصل مسارات الكتابة) | كان سيقتل العيب أيضًا وبكلفة أقل، **لكنه يُبقي حقول التذكير داخل عقد تعديل الإجراء** ⇒ I4 غير قابل للتحقق آليًا (grep/عقد)، ويُبقي الباب مفتوحًا لإعادة الاقتران مستقبلًا. أُبقي الخيار ٣ لأنه يحوّل الثابت إلى **بنية** لا إلى انضباط، ويمنح مسارًا مستقلًا مشروعًا لتعديل التذكير — بكلفة معلومة ومحصورة (§5.2، §6، §7). |

---

## 3) نموذج البيانات والهجرات

### 3.1 الكيانات
```csharp
// backend/src/DocGenerator.Domain/Entities/ExecutionAction.cs
/// <summary>
/// تاريخ أساس التذكير (ليس تاريخ الإجراء): يُضبط مرة عند إنشاء التذكير من تاريخ الإجراء
/// المجلول، ويعدّله «نقل التذكير» وحده. تاريخ الإجراء واقعة قانونية لا تُشتق — I1/I2/I6.
/// </summary>
public DateTime? ReminderBaseDate { get; set; }
```
نفسه في `AppealAction.cs` (نسخة الاستئناف مستقلة، بنفس التعليق).

### 3.2 الإعدادات
- `Configurations/ExecutionActionConfiguration.cs` و`AppealActionConfiguration.cs`: لا ضبط نوع/طول/فهرس (العمود للقراءة/الحساب لا للفرز النطاقي).
- `DocGeneratorPostgresDbContext`: **لا حاجة لإضافة إسقاط** — مُتحقَّق من اللقطتين: `ExecutionAction.CreatedAt` (بلا `HasColumnType`) يُسقَط `TEXT` على SQLite و`timestamp with time zone` على Postgres، ولا يوجد في سياق Postgres إلا لتفادي `datetime2` المضبوط صراحةً في إعدادات أخرى. محوّل `UtcDateTimeConverter` يسري تلقائيًا على `DateTime?`.
- **لا فهرس جديد:** الاستعلامات تفلتر بـ`IS NOT NULL` على العمود (نفس صنف فلاتر `ReminderDuration/Color` القائمة التي بلا فهرس) — أي لا تراجع أداء مقارنةً بالحالي.

### 3.3 الهجرتان
| السياق | الاسم | العمود | النوع |
|---|---|---|---|
| SQLite (`DocGeneratorDbContext`) | `SeparateReminderBaseDate` | `ReminderBaseDate` في `ExecutionActions` و`AppealActions` | `TEXT` nullable |
| PostgreSQL (`DocGeneratorPostgresDbContext`) | `SeparateReminderBaseDate_Pg` | نفسهما | `timestamp with time zone` nullable |

```bash
dotnet ef migrations add SeparateReminderBaseDate    --context DocGeneratorDbContext          --project backend/src/DocGenerator.Infrastructure --startup-project backend/src/DocGenerator.Api
dotnet ef migrations add SeparateReminderBaseDate_Pg --context DocGeneratorPostgresDbContext --project backend/src/DocGenerator.Infrastructure --startup-project backend/src/DocGenerator.Api
```

- **لا تعبئة بيانات (Backfill):** الصفوف القديمة `ReminderBaseDate IS NULL` تسقط إلى `parse(ActionDate)` = **السلوك الحالي حرفيًا** ⇒ لا موعد تذكير يقفز يوم النشر، وصفر مخاطرة على البيانات.
- تحديث `RUN_GUIDE.md §9` (القائمة التراكمية) **في نفس التغيير**: سطران (`SQLite` غير مشطوب، `PostgreSQL` يُشطب بعد التطبيق الحيّ).

---

## 4) المحرك (مصدر الحقيقة الوحيد)

`backend/src/DocGenerator.Application/Common/ActionReminderCalculator.cs`

```csharp
public static (DateTime DueDate, bool IsSuspect) TryComputeDueDate(
    DateTime? reminderBaseDate, string? actionDate, string? duration, DateTime createdAt)
{
    var trimmedDuration = duration?.Trim();
    var parsedAction = ActionDateParser.TryParse(actionDate?.Trim());
    var baseDate = reminderBaseDate ?? parsedAction;          // I3

    var suspect = (baseDate is null && !string.IsNullOrWhiteSpace(actionDate))
        || (!string.IsNullOrWhiteSpace(trimmedDuration) && !ValidDurations.Contains(trimmedDuration));

    return ((baseDate ?? createdAt).Date.AddDays(DurationDays(trimmedDuration)), suspect);
}

public static DateTime ComputeDueDate(
    DateTime? reminderBaseDate, string? actionDate, string? duration, DateTime createdAt)
    => TryComputeDueDate(reminderBaseDate, actionDate, duration, createdAt).DueDate;
```

- دلالة `IsSuspect` لم تتغير: «سقط الحساب إلى احتياط». مع `ReminderBaseDate` لن يُرفع وسم كاذب على تذكير مؤكد.
- `DurationDays`/`ValidDurations`/`ValidColors` تبقى كما هي (تُستعمل للتحقق عند الكتابة وعرض المدة).

---

## 5) الخلفية — الملفات ونقاط التغيير

### 5.1 الملفات
| الملف | التغيير |
|---|---|
| `Domain/Entities/ExecutionAction.cs` | `ReminderBaseDate` + تعليق الثوابت |
| `Domain/Entities/AppealAction.cs` | مثله |
| `Infrastructure/Persistence/Configurations/ExecutionActionConfiguration.cs` | لا تغيير (نوع تلقائي) |
| `Infrastructure/Persistence/Configurations/AppealActionConfiguration.cs` | لا تغيير |
| `Infrastructure/Persistence/Migrations/*` + `MigrationsPostgres/*` | الهجرتان + اللقطتان |
| `Application/Common/ActionReminderCalculator.cs` | التوقيع الجديد + التوقيع القديم محذوف (لا Overload مزدوج) |
| `Application/DTOs/DocumentRequests.cs` | حذف حقلي التذكير من `UpdateExecutionActionRequest` + `SetActionReminderRequest` + تحديث وصف `RescheduleActionReminderRequest` |
| `Application/DTOs/AppealDtos.cs` | حذفهما من `UpdateAppealActionRequest` + `SetAppealReminderRequest` + تحديث وصف `RescheduleAppealReminderRequest` |
| `Application/Services/IExecutionActionService.cs` | `SetReminderAsync(...)` + تحديث وصف `RescheduleReminderAsync` |
| `Application/Services/ExecutionActionService.cs` | §5.2 |
| `Application/Services/IDocumentAppealService.cs` | `SetReminderAsync(...)` + وصف النقل |
| `Application/Services/DocumentAppealService.cs` | §5.3 |
| `Infrastructure/Persistence/StatisticsRepository.cs` | §5.4 |
| `Api/Controllers/DocumentsController.cs` | `PUT .../actions/{actionId}/reminder` |
| `Api/Controllers/AppealsController.cs` | `PUT appeals/{id}/actions/{actionId}/reminder` |

### 5.2 `ExecutionActionService`
- **`AddExecutionActionAsync`**: بعد `NormalizeAction`/`NormalizeReminder`:
  ```csharp
  ReminderBaseDate = reminderDuration is null && reminderColor is null
      ? null
      : ActionDateParser.TryParse(actionDate),   // actionDate مضمون (الإجراء إلزامي، والملاحظة تُؤرَّخ اليوم)
  ```
- **`UpdateExecutionActionAsync`**: حذف `NormalizeReminder` وإسنادي `ReminderDuration`/`ReminderColor` **بلا بديل** (I4). يبقى النوع/النص/التاريخ.
- **`ClearReminderAsync`**: `ReminderDuration = ReminderColor = ReminderBaseDate = null` + تدقيق كما هو.
- **`RescheduleReminderAsync`**: استبدال إسناد النص:
  ```csharp
  var oldBase = action.ReminderBaseDate;
  action.ReminderBaseDate = due.AddDays(-ActionReminderCalculator.DurationDays(trimmedDuration)); // DateTime خالص
  // ActionDate لا يُلمس
  ```
  والتدقيق: `"نقل التذكير إلى {due:yyyy-MM-dd} (تاريخ الأساس: {oldBase ?? "—"} ← {action.ReminderBaseDate:yyyy-MM-dd}) — تاريخ الإجراء لم يتغير"`.
- **حارس «لا تذكير» في النقل** يتسع ليشمل الأساس:
  ```csharp
  if (string.IsNullOrWhiteSpace(action.ReminderDuration)
      && string.IsNullOrWhiteSpace(action.ReminderColor)
      && action.ReminderBaseDate is null)
      throw new ArgumentException("لا يوجد تذكير لإعادة جدولته");
  ```
  (توافق الحارس مع فلتر القراءة الموسّع في §5.4 — فلا يختلف تعريف «له تذكير» بين المسارين.)
- **`SeedInitialActionsAsync`**: نفس قاعدة الإنشاء (`ReminderBaseDate = parse(actionDate)` عند وجود تذكير).
- **جديد `SetReminderAsync(int documentId, int actionId, SetActionReminderRequest request, string? actorName, CancellationToken ct)`**:
  1. جلب الإجراء؛ غير موجود ⇒ `KeyNotFoundException` (404 في المتحكم).
  2. `ValidateReminder` على المدة/اللون.
  3. الاثنان فارغان ⇒ إلغاء (نفس `ClearReminderAsync`، نقطة قرار موحّدة).
  4. وإلا: `ReminderDuration/Color = المطبَّع`، و`ReminderBaseDate ??= ActionDateParser.TryParse(action.ActionDate)` — **بلا سقوط إلى `CreatedAt`** (تهيئة مرة واحدة فقط — I2).
     - **سبب الحظر:** تجميد تاريخ الإنشاء في العمود يُطفئ وسم `dueDateSuspect` **بصمت** ويمنع التصحيح الذاتي عند إصلاح تاريخ الإجراء؛ أما إبقاء الأساس `null` فيجعل السقوط ديناميكيًا ومعلَنًا وفق I3.
     - إن كان تاريخ الإجراء غير قابل للتحليل (بيانات قديمة) يبقى الأساس `null` ويُحسب الاستحقاق احتياطيًا من `CreatedAt` **مع الوسم** — كما اليوم.
  5. تدقيق: `"عدّل تذكير {نوع الإجراء}: {نص مختصر} ({duration} — {color})"`.

### 5.3 `DocumentAppealService` (مرآة مطابقة)
- `AddActionAsync` (≈785): `ReminderBaseDate` عند وجود تذكير.
- `UpdateActionAsync` (≈811): حذف `ValidateReminder` وإسنادي المدة/اللون.
- `ClearReminderAsync` (≈878): تصفير الثلاثة.
- `RescheduleAppealReminderAsync` (≈911): إسناد `ReminderBaseDate` بدل `ActionDate` + التدقيق بالنص نفسه.
- جديد `SetReminderAsync(appealId, actionId, request, userId, actorName, ct)` بنفس قواعد §5.2 مع `EnsureAssignedFollower`.

### 5.4 مسارات القراءة
- `StatisticsRepository.GetRemindersAsync` (846-899): إضافة `a.ReminderBaseDate` إلى الإسقاط، وتوسيع الفلتر:
  ```csharp
  .Where(a => a.ReminderDuration != null || a.ReminderColor != null || a.ReminderBaseDate != null)
  ```
  ثم `TryComputeDueDate(r.ReminderBaseDate, r.ActionDate, r.ReminderDuration, r.CreatedAt)`.
- `DocumentAppealService.GetRemindersAsync` + `ListReminderRowsAsync` (953-995): نفسه (الفلتر في ≈988).

---

## 6) العقود ونقاط النهاية

### 6.1 العقود (خلفي + أمامي)
```csharp
// DocumentRequests.cs — بعد الإصلاح
public class UpdateExecutionActionRequest
{
    public string Type { get; set; } = "action";
    public string Text { get; set; } = string.Empty;
    public string? ActionDate { get; set; }
    // ReminderDuration / ReminderColor: محذوفان عمدًا — تُدار من نقطة التذكير
}

/// <summary>إنشاء/تعديل تذكير إجراء قائم (المدة/اللون فقط؛ الاستحقاق يُنقل بنقطة reschedule).</summary>
public class SetActionReminderRequest
{
    public string? ReminderDuration { get; set; }
    public string? ReminderColor { get; set; }
}
```
- `AddExecutionActionRequest` / `AddAppealActionRequest` / `InitialActionRequest` **تبقى كما هي** (الإنشاء فقط).
- `UpdateAppealActionRequest` يحذف الحقلين، و`SetAppealReminderRequest` جديد.
- **التوافق (مُدار بالنشر لا بكود انتقالي):** `System.Text.Json` يتجاهل الحقول المجهولة، فعميل قديم (حزمة `SPA` محمّلة قبل النشر) سيحفظ نص/تاريخ الإجراء ويُهمل تعديل التذكير بصمت. **مُتحقَّق:** `index.html` يُرسَل `no-cache, max-age=0, must-revalidate` (`SpaCacheHeaders.cs`) ⇒ لا حزمة قديمة **دائمة**؛ النافذة محصورة في **تبويب مفتوح قبل النشر** حتى إعادة التحميل.
  - **الضابط:** نشر الخلفي والأمامي في الإصدار نفسه، وإبلاغ المستخدمين بإعادة تحميل الصفحة بعد النشر (إجراء نشر مُوثَّق، لا `shim`).

### 6.2 نقاط النهاية
| الطريقة | المسار | الصلاحية | الرد |
|---|---|---|---|
| `PUT` | `/api/documents/{id}/actions/{actionId}/reminder` | `CanManageActions` + `CanAccessAsync` | `200 ExecutionActionDto` / `404` / `400` / `403` |
| `PUT` | `/api/appeals/{id}/actions/{actionId}/reminder` | المحامي المتابع (`EnsureAssignedFollower`) | `200 AppealActionDto` / `400` / `403` / `404` |
| `DELETE` | `.../reminder` (قائم) | كما هو | `204` |
| `POST` | `.../reschedule` (قائم) | كما هو | `200` |

- **دلالة `PUT /reminder`:** إنشاء-أو-تعديل؛ وإرسال المدة واللون فارغين = **إلغاء** (نفس أثر `DELETE`). القاعدة الواحدة تُوثَّق في تعليق الـDTO، والواجهة تستخدم `DELETE` عند إلغاء التفعيل.
- **الأخطاء موحّدة:** `400` رسائل تحقق عربية (§5.2)، `403` صلاحية/نطاق، `404` إجراء غير موجود أو لا يخص الملف.

---

## 7) الواجهة الأمامية

### 7.1 `ExecutionActionsModal.tsx` و`AppealActionsModal.tsx`
- **وضع الإضافة**: كما هو — جسم `POST` يحمل `reminderDuration/Color` (إنشاء ذرّي).
- **وضع التعديل**: تسلسل صريح:
  1. `PUT /actions/{id}` بـ`{ type, text, actionDate }` **فقط**.
  2. إن تغيّرت حالة التذكير مقابل القيم المحمّلة (`reminderDirty`) ⇒ `PUT /reminder` (إن وُجد تذكير) أو `DELETE /reminder` (إن أُلغي).
  ```ts
  const reminderDirty =
    remind !== hadReminder || (remind && (reminderDuration !== loaded.duration || reminderColor !== loaded.color));
  ```
  `reminderDirty` ضروري **لأن العقد لا يحمل حقول التذكير** (لا `shim`)؛ وهو تتبّع صريح في الواجهة لا مقارنة خادمية.
- **لا طلب زائد:** تعديل النص/التاريخ فقط ⇒ طلب واحد بلا أي نداء تذكير.
- **فشل جزئي**: إذا نجح (1) وفشل (2) ⇒ رسالة صريحة «حُفظ الإجراء، وتعذّر حفظ التذكير: {السبب}»، مع `load()` وإبقاء النموذج مفتوحًا لإعادة المحاولة (لا حفظ صامت ناقص).
- **أخطاء التحقق**: تظهر بجانب النموذج كما هي اليوم (`role="alert"`).
- **إتاحة/جوال**: لا تغيير بنيوي — الأزرار والحقول الحالية `min-h-11` وتسمياتها قائمة.

### 7.2 نصوص
- `DayRemindersModal.tsx:483`: «يوم الاستحقاق نفسه — المدة نفسها تُحفَظ، ويُعاد حساب تاريخ الإجراء تلقائيًا» ⇒ **«يوم الاستحقاق نفسه — المدة تبقى، ولا يتغيّر تاريخ الإجراء»**.
- `ExecutionActionsModal`/`AppealActionsModal`: نص مساعدة صغير أسفل كتلة التذكير في وضع التعديل: «تعديل التذكير لا يغيّر تاريخ الإجراء».
- لا تغيير في `ReminderList`/`LawyerCalendar`/`ReminderViewModal`.

### 7.3 الأنواع
- لا حذف أنواع مطلوبة في TS (النماذج تُبنى inline)، ويُضاف نوعان اختياريان للوضوح: `SetActionReminderRequest`/`SetAppealReminderRequest` إن استُخدما في المكوّنات.

---

## 8) الاختبارات (إلزامية)

### 8.1 وحدة — `ActionReminderCalculatorTests`
- تحديث التوقيع في الحالات القائمة (تمرير `null` كأساس) — النتائج كما هي.
- جديد: `BasePresent_WinsOverUnparsableActionDate_NotSuspect` · `BasePresent_AddsDuration` · `BaseNull_FallsBackToActionDate` (سلوك اليوم) · `BaseNull_UnparsableDate_Suspect` (قائم).

### 8.2 وحدة — `RescheduleReminderTests` (ملفات واستئنافات) — تُقلب رأسًا على عقب
- `Reschedule_DoesNotTouchActionDate`: `ActionDate` **نفسه حرفيًا** قبل/بعد (`"1/8/2026"`).
- `Reschedule_MovesReminderBaseDate`: `ReminderBaseDate == 2026-08-07` والاستحقاق `2026-08-10` والمدة/اللون/`CreatedAt` بلا مساس.
- `Reschedule_ArabicDigits_Accepted`: الأساس `2026-08-07`.
- `Reschedule_ColorOnly_UsesDueDateAsBase`: الأساس = الاستحقاق، `ActionDate` بلا مساس.
- `Reschedule_DoesNotRewriteDateFormat`: إدخال `10-11-2026` ونقل إلى `14-11-2026` ⇒ `ActionDate` يبقى `10-11-2026` والأساس `2026-11-11`.
- التدقيق: يحتوي «تاريخ الإجراء لم يتغير».

### 8.3 وحدة — `SetReminderTests` (جديد)
- إجراء بلا تذكير + `PUT reminder` ⇒ تهيئة الأساس من تاريخ الإجراء.
- تعديل المدة ⇒ الأساس ثابت والاستحقاق يتحرك.
- أساس موجود + تعديل تاريخ الإجراء (`UpdateExecutionActionAsync`) ⇒ الأساس **لا يتغير** (I6).
- مدة/لون فارغان ⇒ تصفير الثلاثة.
- مدة مجهولة ⇒ `ArgumentException`.
- إجراء غير موجود/غير تابع للملف ⇒ `KeyNotFoundException`.
- **بيانات قديمة:** إجراء `ActionDate` غير قابل للتحليل + `PUT reminder` ⇒ الأساس يبقى `null` (لا يُجمَّد `CreatedAt`)، والاستحقاق احتياطي **مع** `dueDateSuspect = true`.

### 8.4 تكامل — `ActionReminderSeparationIntegrationTests` (جديد)
1. السيناريو الكامل: `POST` إجراء `10-11-2026` + «3 أيام» ← `POST reschedule {14-11-2026}` ← `GET /actions` يعيد `actionDate = "10-11-2026"` ← `GET /reminders` يعيد `dueDate = 2026-11-14`.
2. `PUT /actions/{id}` مع حقلي تذكير في الجسم ⇒ **لا يغيّر** التذكير (العقد لا يحملهما) — توثيق صريح للتوافق.
3. `PUT /reminder` يعدّل المدة ويُبقي الأساس؛ `DELETE /reminder` يصفّر الثلاثة.
4. الصلاحيات: غير المحامي `403` على كل نقاط التذكير؛ محامٍ لا يملك الملف `403`؛ مندوب الجهة `403`.
5. `404` لمعرّف إجراء لا يخص الملف.
6. **فلتر القراءة الموسّع:** صف بـ`ReminderBaseDate` وحده (بلا مدة/لون) يظهر في `GET /reminders`.
7. **مرآة الاستئناف:** `PUT /appeals/{id}/actions/{actionId}/reminder` يعمل لمحامٍ متابع، و`403` لمن ليس متابعًا، ولا يغيّر `AppealAction.ActionDate`.

### 8.5 واجهة
- `ExecutionActionsModal.test.tsx`: تعديل النص فقط ⇒ **لا** طلب تذكير؛ تغيير المدة ⇒ `PUT /reminder`؛ إلغاء ⇒ `DELETE`؛ فشل الخطوة الثانية ⇒ رسالة الفشل الجزئي؛ الإضافة ما زالت ترسل التذكير في `POST`.
- `AppealActionsModal.test.tsx`: المرآة.
- `DayRemindersModal.test.tsx`: النص الجديد (إن كان مُختبَرًا).
- لا تغيير مطلوب في `ReminderList`/`CalendarPage`/`Dashboard` (لا تغيير سلوك قراءة).

---

## 9) ترتيب التنفيذ الإلزامي

0. **إثبات الحالة القائمة (قبل أي تعديل):** تشغيل `RescheduleReminderTests` كما هي ⇒ خضراء (وهي تحمي العيب)؛ ثم إضافة **الاختبار الأحمر** `Reschedule_DoesNotTouchActionDate` والتأكد أنه يفشل ⇒ يُثبِت العيب عمليًا.
1. الكيانات + الهجرتان (SQLite ثم Postgres) + فحص `has-pending-model-changes` فورًا + تحديث `RUN_GUIDE §9`.
2. المحرك (`ActionReminderCalculator`) وتحديث مواضع الاستهلاك الثلاثة.
3. مسارات الكتابة (ملفات ثم استئنافات) + إزالة حقول التذكير من عقود التعديل.
4. نقطتا `PUT /reminder` في المتحكمين + `SetReminderAsync` في الخدمتين.
5. مسارات القراءة (فلتر + إسقاط + حساب).
6. الواجهة الأمامية (نافذتا الإجراءات + نص المساعدة).
7. الاختبارات (وحدة → تكامل → واجهة) حتى الأخضر، ثم البوابات الكاملة.
8. **التوثيق:** `CHANGELOG.md` + ملاحظة تأريخية في `docs/audit/` (اختياري وفق عرف المستودع) + تقرير الإنجاز (الهجرتان وأمرا `database update`).

---

## 10) التحقق والبوابات (لا إنجاز قبل خضرتها)

```bash
dotnet build backend/DocGenerator.sln
dotnet test  backend/tests/DocGenerator.Application.Tests/DocGenerator.Application.Tests.csproj
dotnet test  backend/tests/DocGenerator.Api.Tests/DocGenerator.Api.Tests.csproj
cd frontend; npx oxlint src scripts; npx tsc -b; npx vitest run; npm run build; npm run guard
```

> `npm run guard` هو حارس `CSP` على `dist` المبني، ويشغّله CI — إغفاله محليًا يعني اكتشاف كسره في CI فقط.

- **حارس الهجرات (CI):**
  ```bash
  dotnet ef migrations has-pending-model-changes --context DocGeneratorDbContext          --project backend/src/DocGenerator.Infrastructure --startup-project backend/src/DocGenerator.Api
  dotnet ef migrations has-pending-model-changes --context DocGeneratorPostgresDbContext --project backend/src/DocGenerator.Infrastructure --startup-project backend/src/DocGenerator.Api
  dotnet ef migrations script --context DocGeneratorPostgresDbContext \
    --project backend/src/DocGenerator.Infrastructure --startup-project backend/src/DocGenerator.Api \
    -o /tmp/pg_migrations.sql   # ثم: لا 'datetime2' في الناتج
  ```
- **بوابات المستودع:** `backend` + `frontend` + `guards` + `audit` + `backend-postgres` + `migration-guard` (CI الستة) — على فرع، ثم `PR` إلى `master` (لا دفع مباشر).

### 10.1 تحقق يدوي (قبول العيب المُبلَّغ)
1. إجراء بتاريخ `10-11-2026` + «3 أيام» ⇒ التقويم يعرض `13-11-2026`.
2. نقل التذكير إلى `14-11-2026`.
3. نافذة الإجراءات ⇒ التاريخ **`10-11-2026` بالحرف**.
4. التقويم ⇒ `14-11-2026`.
5. تعديل نص الإجراء ⇒ التاريخ والاستحقاق بلا تغيير.
6. تعديل المدة إلى «شهر» ⇒ الاستحقاق = الأساس + 30 يومًا.

---

## 11) النشر والتراجع

**ترتيب النشر (إلزامي):**
1. `dotnet ef database update --context DocGeneratorPostgresDbContext` على الحيّة **قبل** تشغيل النسخة (وإلا رفض الإقلاع: `DatabaseInitializer.EnsureSchemaIsCurrentAsync`).
2. نشر الخلفي والأمامي **في الإصدار نفسه** (العقد تغيّر على الطرفين — لا `shim`).
3. إبلاغ المستخدمين بإعادة تحميل الصفحة: أي تبويب فُتح قبل النشر يبقى على الحزمة القديمة حتى التحديث، وفيه تُهمَل تعديلات التذكير بصمت (§6.1).

- `SQLite`: تطوير محلي (`AutoMigrate=true`) أو `dotnet ef database update --context DocGeneratorDbContext` عند دفعة `G2` (السلسلة معلّقة أصلًا ولا تُطبَّق وحدها).
- **التراجع:** إرجاع الكود فقط يَكفي — العمود `nullable` يُهمَل، و`ActionDate` سليم في كل الصفوف. الأثر الوحيد: تذكير نُقل تحت الكود الجديد يعود (بعد التراجع) إلى `ActionDate + المدة` ⇒ إعادة نقله بعد الإصلاح.
- **لا سكربت إصلاح بيانات:** البيانات تجريبية؛ والتواريخ المشوّهة سابقًا لا تُستعاد آليًا (سطر التدقيق لا يحمل معرّف الإجراء).

---

## 12) خارج النطاق (صراحةً)

1. صفحة «تذكيرات الشطب» — مهمة تالية، ستعتمد `ActionDate` المحصّن + عمودًا محلولًا للفرز.
2. عرض «مستحق في …» في نافذة الإجراءات.
3. كيان تذكير مستقل (جدول) أو أكثر من تذكير على الإجراء.
4. إصلاح آلي للتواريخ المشوّهة سابقًا أو تحويل صيغها.
5. أي تغيير في آلة حالات الشطب/القوائم/الإحصاءات/البوابة.
6. سؤال «هل أنقل التذكير معه؟» عند تصحيح تاريخ إجراء له تذكير (المعالجة الكاملة لأثر I6) — مؤجَّل بقرار واعٍ (§2.1).

---

## 13) المخاطر ومضاداتها

| الخطر | الأثر | المضاد |
|---|---|---|
| عميل SPA قديم بعد النشر | تعديل التذكير يُهمَل بصمت (العقد لا يحمله) | النافذة محصورة في تبويب مفتوح قبل النشر (`index.html` بلا كاش) ⇒ نشر متزامن + إبلاغ بإعادة التحميل (§6.1، §11) |
| صفوف قديمة بتاريخ إجراء غير قابل للتحليل | أساس احتياطي `CreatedAt` + وسم «تاريخ مشتبه» | سلوك مطابق لليوم (لا انحدار) + **يُمنع تجميد الاحتياط** في العمود (§5.2/4) |
| نسيان تحديث أحد مسارَي القراءة | موعد خاطئ في التقويم | اختبارات التكامل + البحث الشامل `TryComputeDueDate` |
| نسيان تصفير الأساس عند الإلغاء | صف بلا تذكير وأساس معلّق | اختبار `DELETE` يفحص الثلاثة |
| ترتيب الهجرتين | فشل `migration-guard` | توليد SQLite ثم Postgres + تحديث اللقطتين تلقائيًا |
| تصحيح تاريخ إجراء له تذكير لا يحرّك التذكير (I6) | تذكير معلّق على تاريخ قديم بلا إشارة مرئية | قاعدة موثّقة + توجيه للنقل + سؤال واعٍ مؤجَّل (§2.1/§12) |
| تسابق `PUT /reminder` مع `POST /reschedule` (لا `Version` على الإجراء) | آخر كاتب يفوز | خارج النطاق (سلوك قائم، مستخدم واحد لكل ملف) — يُوثَّق ولا يُعالَج هنا |

---

## 14) قائمة تحقق نهائية (مراجعة المسار الكامل)

- [ ] `rg "\.ActionDate\s*=" backend/src` ⇒ لا نتيجة في أي مسار تذكير (فقط إسناد نموذج الإضافة/التعديل).
- [ ] `rg "ReminderDuration|ReminderColor" backend/src` ⇒ الكتابة فقط في: الإضافة، `SetReminder*`، `ClearReminder*`.
- [ ] `rg "TryComputeDueDate|ComputeDueDate" backend` ⇒ كل المواضع بالتوقيع الجديد.
- [ ] كل `SetReminder`/`Reschedule`/`Clear` تحمل تدقيقًا يحمل اسم الفاعل ومعرّف الملف.
- [ ] عقود: `UpdateExecution*` بلا حقول تذكير (خلفي + أمامي)، و`Add*` كما هي.
- [ ] الهجرتان موجودتان للسياقين، واللقطتان محدَّثتان، و`has-pending-model-changes` نظيف للسياقين.
- [ ] `RUN_GUIDE §9` محدَّث بسطرَي الهجرتين.
- [ ] الاختبارات المقلوبة (التي كانت تُثبّت العيب) مؤكِّدة للثوابت I1/I2/I6.
- [ ] جوال: نافذتا الإجراءات تعملان بلا تجاوز أفقي (فحص بصري 375px) — لا تغيير بنيوي متوقع.
- [ ] `rg "CreatedAt\.Date" backend/src` ⇒ لا يظهر في أي تهيئة لـ`ReminderBaseDate` (منع تجميد الاحتياط).
- [ ] دليل الانتقال **أحمر → أخضر** موثّق في تقرير الإنجاز (لا دعوى إصلاح بلا دليل).
- [ ] `npm run guard` مع `oxlint src scripts` أخضران محليًا (لا في CI فقط).
- [ ] `CHANGELOG.md` محدَّث بسطر يصف الإصلاح والعيب.
- [ ] التقرير النهائي يذكر الهجرتين و**تنبيه تطبيقهما** وأمرَي `database update` و**تزامن نشر الواجهة والخلفية**.

---

## 15) سجل تعديلات الخطة (v1 → v2) — بعد المراجعة النقدية

| # | التعديل | السبب |
|---|---|---|
| 1 | حذف `?? action.CreatedAt.Date` من تهيئة `ReminderBaseDate` في `SetReminderAsync` | كان **يجمّد احتياطًا** ويُطفئ وسم `dueDateSuspect` بصمت ويمنع التصحيح الذاتي (§5.2). |
| 2 | إلغاء اقتراح `Compatibility shim` نهائيًا واستبداله بضابط نشر | يتعارض مع قاعدة «لا حلول مؤقتة»؛ والنافذة محصورة في تبويب مفتوح قبل النشر (§6.1، §11، §13). |
| 3 | إضافة حارس «لا تذكير» إلى `ReminderBaseDate` في النقل | يوحّد تعريف «له تذكير» بين الحارس وفلتر القراءة (§5.2). |
| 4 | تسجيل أثر **I6** كقرار منتجي + معالجة مؤجَّلة صريحة | كان أثرًا غير مرئي للمستخدم في سيناريو تصحيح التاريخ الخطأ (§2.1، §12). |
| 5 | توسيع البوابات (`oxlint scripts`، `npm run guard`، `CHANGELOG`، `docs/audit` اختياري) | كانت ناقصة مقابل ما يشغّله CI/العرف (§9، §10، §14). |
| 6 | إضافة اختبارات: فلتر القراءة الموسّع، مرآة صلاحيات الاستئناف، الاحتياط غير المجمَّد | سدّ فجوات تحقق كانت مكشوفة (§8.3، §8.4). |
| 7 | إضافة خطوة 0 (إثبات أحمر→أخضر) وترتيب نشر صريح | تحويل «الاختبارات تحمي العيب» من ملاحظة إلى إجراء مُلزِم (§1.4، §9، §11). |
| 8 | إضافة §2.2 «بدائل مرفوضة» وتحديث §3.2 ببرهان الإسقاط | تثبيت مسار القرار ومنع إعادة فتحه بلا معطيات (§2.2، §3.2). |

---

**الخلاصة:** عمود واحد للتاريخ المستقل + مسار كتابة مستقل للتذكير ⇒ يُزال العيب بنيويًا (لا اتفاقيًا) بأقل مساحة تغيير، بلا تعبئة بيانات، وبقابلية تراجع كاملة.
