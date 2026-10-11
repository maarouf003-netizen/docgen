# خطة تلميع تذكيرات التقويم: النقل بيوم الاستحقاق + تنظيف العرض + إخفاء الأرشفة

> **الحالة:** خطة معتمدة من المالك — **لا تنفيذ قبل الموافقة الصريحة**.
> **مراجعة نقدية:** طُبقت نتائج المراجعة (تناقض القيمة الابتدائية §6.1/§6.2 ← `yyyy-MM-dd` مباشرة؛
> تدقيق القديم←الجديد؛ حارس المدة الملتبسة؛ تصحيح `404/400` للمسارين؛ إسقاط بند «المؤرشف اليتيم»
> بعد تأكيد المالك بعدم وجود مؤرشف — مع بوابة تحقق `COUNT` قبل الدمج).
> **الفرع المقترح:** `feature/reminder-calendar-polish` ثم `PR` إلى `master` (ممنوع الدفع المباشر).
> **لا هجرات جديدة** في هذه الخطة — لا `dotnet ef database update` مطلوب عند النشر.

---

## 1) السياق والتشخيص

### 1.1 كيف يُحسب التذكير اليوم؟

التذكير المرتبط بملف أو استئناف **ليس تاريخًا مستقلًا**. القاعدة الوحيدة في `backend/src/DocGenerator.Application/Common/ActionReminderCalculator.cs`:

```
dueDate = actionDate + DurationDays(reminderDuration)
```

- `actionDate`: تاريخ الإجراء المدخل من `ExecutionActionsModal.tsx` أو `AppealActionsModal.tsx`.
- `reminderDuration`: واحدة من `3 أيام` / `أسبوع` / `أسبوعين` / `شهر` (`DurationDays` = `3` / `7` / `14` / `30`).
- `dueDate`: يوم ظهور التذكير في التقويم (`GET /reminders` عبر `StatisticsRepository.cs` و`GET /appeals/reminders` عبر `DocumentAppealService.cs`).
- التحليل عبر `ActionDateParser` (الصيغ الخمس + `InvariantCulture` + `ArabicDigitNormalizer`)، والرسالة المعتمدة `"{اسم الحقل} غير صالح — استخدم مثال: 1/8/2026"`.

### 1.2 الخلل الحالي في `DayRemindersModal`

الدالة `saveReschedule` في `frontend/src/components/dashboard/DayRemindersModal.tsx:134-160` ترسل المدخل كـ `actionDate` جديد عبر `PUT`:

- ملف: `PUT /documents/:id/actions/:actionId`
- استئناف: `PUT /appeals/:id/actions/:actionId`

مع بقاء `reminderDuration` و`reminderColor` نفسيهما (وتتطلب `GET actions` مسبقًا لحفظ `type/text`).

**النتيجة:** إدخال `10/8/2026` يجعل `actionDate=10/8` فيظهر التذكير `13/8` (مع `3 أيام`) — لا في `10/8` نفسه. وهذا سبب سؤال المالك.

### 1.3 عيوب عرض إضافية مؤكدة

| # | العيب | الموضع | الدليل |
|---|-------|--------|--------|
| 1 | نص التذكير يظهر بوسوم `<p>راسل المصرف…</p>` | `DayRemindersModal.tsx:457` يعرض `{r.actionText}` خامًا | المرجع الصحيح مطبق في `ReminderList.tsx:71,124` عبر `richToPlainText` |
| 2 | البحث لا يطبع الوسوم | `DayRemindersModal.tsx:119` يبني `haystack` من `actionText` الخام | يجب نفس `richToPlainText` |
| 3 | خيار `مؤرشف (يُخفى من التقويم)` زائد | `DayRemindersModal.tsx:545,575,693-705` (`PersonalForm`) | المالك طلب حذفه |
| 4 | رسالتا خلفية تحيلان لخيار محذوف | `PersonalReminderService.cs:75` (`أرشف القديم منها`) و`:185` (`أرشف التذكير`) | تصبحان طريقًا مسدودًا بعد حذف الخيار |

---

## 2) القرارات المعتمدة من المالك

| سؤال | القرار |
|------|--------|
| معنى حقل اليوم الجديد | **(أ)** المدخل = يوم الاستحقاق `dueDate` نفسه؛ يُعاد حساب `actionDate = newDueDate - DurationDays` خلفيًا مع بقاء المدة |
| النطاق | الملف **+** الاستئناف معًا (توحيد سلوك زر `تغيير اليوم`) |
| أثر تاريخ الإجراء | **(أ)** مقبول أن يتغير `actionDate` المعروض داخل الملف (يصبح أبكر بمقدار المدة) — لا فصل معماري للتاريخين |
| التسمية | تبقى `اليوم الجديد` حصرًا في `label`؛ التوضيح ينتقل لسطر مساعدة |
| الوسوم | حذف `<p>` وعرض نص عادي عبر `richToPlainText` |
| الأرشفة | حذف `checkbox` من الواجهة؛ الخلفية تبقى دون هجرات للبيانات القديمة |
| الاحتفاظ | **لا تفريغ للسنوات القديمة ولا حذف تلقائي** (انظر §7) |

---

## 3) النطاق والأهداف غير المشمولة

### داخل النطاق

1. عمليتا إعادة جدولة خلفيتان (`documents` + `appeals`) بدلالة `dueDate`.
2. تبسيط التسمية إلى `اليوم الجديد` + سطر مساعدة.
3. تنظيف عرض `actionText` من الوسوم في نافذة اليوم + إصلاح البحث.
4. حذف خيار `مؤرشف` من `PersonalForm` + تصحيح رسالتي السقف والسجل.
5. اختبارات خلفية وأمامية + بوابات التحقق الأربع.

### خارج النطاق (لن يُنفذ)

- فصل `dueDate` كعمود مستقل (يتطلب هجرتين `SQLite` + `Postgres` وتغيير `ReminderDto` و`AppealReminderDto` و`StatisticsRepository`) — مرفوض لاختيار `3-أ`.
- أي حذف تلقائي للسنوات القديمة أو سياسة `Retention` جديدة للتذكيرات — مرفوض (§7).
- تغيير عرض `ExecutionActionsModal` / `AppealActionsModal` الغني (يبقى `sanitizeRichText` + `dangerouslySetInnerHTML` قصدًا).
- تغيير عقود `PUT` القائمة (تبقى لصالح محرري الإجراءات).

---

## 4) التصميم

### 4.1 المبدأ

نقطة حقيقة واحدة تبقى في الخلفية (`DurationDays` + `ActionDateParser`). الواجهة لا تحسب تواريخ — ترسل `newDueDate` مطبعًا فقط.

### 4.2 العقد الجديد (غير كاسر)

```
POST /documents/{id}/actions/{actionId}/reschedule
POST /appeals/{id}/actions/{actionId}/reschedule
Body: { "newDueDate": "10/8/2026" }
```

- `newDueDate`: نص حر بنفس عقد `Date Fields Rule` (يقبل `d/M/yyyy` و`yyyy-MM-dd`، مع `normalizeArabicDigits` في الواجهة و`ArabicDigitNormalizer` خلفيًا عبر `DocumentValidator.ParseDateTime` — المصدر الخلفي الوحيد للرسالة).
- النجاح: `200 OK` مع `DTO` الإجراء المحدث (الواجهة تعيد الجلب عبر `onChanged` المنادى في `CalendarPage.tsx:60-64`).
- الفشل:
  - `400 { message: "اليوم الجديد مطلوب — استخدم مثال: 1/8/2026" }` للفارغ.
  - `400 { message: "اليوم الجديد غير صالح — استخدم مثال: 1/8/2026" }` لغير القابل للتحليل.
  - `400 { message: "لا يوجد تذكير لإعادة جدولته" }` عندما `ReminderDuration` و`ReminderColor` فارغان معًا.
  - `400 { message: "مدة التذكير غير معروفة — عدّل الإجراء من نافذة الإجراءات" }` عندما المدة غير فارغة وخارج المدد الأربع (بيانات قديمة ملتبسة — تُرفض بدل إسكات `dueDateSuspect` بصمت).
  - ملفات: `404` لغياب الإجراء أو عدم تطابق الأب (`KeyNotFoundException` عبر المعالج العام)؛ استئناف: `400` بالعرف القائم (`ArgumentException("الإجراء غير موجود")` كما في `UpdateActionAsync:793-794`).
  - `403` لغير المصرح (محامٍ غير مالك / غير متابع).

### 4.3 البديل المرفوض

حساب `newDueDate - days` في الواجهة — مرفوض لأنه يكرر خريطة المدد ومحلل الصيغ الخمس وتطبيع الأرقام العربية، فينحرف عن الخادم لاحقًا ويكسر قاعدة المصدر الوحيد للحقيقة.

---

## 5) تغييرات الخلفية `backend/` (تفصيل)

### 5.1 `DTOs/DocumentDtos.cs` (بجانب `UpdateExecutionActionRequest:208-215`)

```csharp
public class RescheduleActionReminderRequest
{
    public string? NewDueDate { get; set; }
}
```

- `class` بمُهيّئ قابل للضبط (لا `record` positional) — اتساقًا مع `Add/UpdateExecutionActionRequest` المجاورتين، وقبول غياب الجسم (`null`) ليُردّ `400` برسالة ودية بدل `400` تقنية من `[ApiController]`.
- نظيرها للاستئناف في `AppealDtos.cs` (انظر §5.3) — لا كسر لعقود `PUT`.

### 5.2 `Services/DocumentService.Actions.cs` — دالة جديدة

`RescheduleReminderAsync(documentId, actionId, newDueDateRaw, actorName, ct)`:

1. جلب `action` عبر `_actions.GetByIdAsync` + التحقق `action.DocumentId == documentId` وإلا `KeyNotFoundException` (نفس عقد `Update`).
2. جلب `doc` عبر `_documents.GetByIdAsync` وإلا `KeyNotFoundException` (للسماح والتدقيق).
3. رفض الفارغ: `string.IsNullOrWhiteSpace` ← `throw new ArgumentException("اليوم الجديد مطلوب — استخدم مثال: 1/8/2026")`.
4. التفسير: `DocumentValidator.ParseDateTime(newDueDateRaw?.Trim(), "اليوم الجديد")` — يرث `ArabicDigitNormalizer` والصيغ الخمس والرسالة الودية. `null` مستحيل هنا لأن الفارغ رُفض أعلاه، ومع ذلك يُحرس دفاعيًا (`?? throw` برسالة المطلوب نفسها) فلا `NullReference` أبدًا.
5. اشتراط التذكير: إذا `string.IsNullOrWhiteSpace(ReminderDuration)` و`string.IsNullOrWhiteSpace(ReminderColor)` معًا ← `throw new ArgumentException("لا يوجد تذكير لإعادة جدولته")`.
5ب. حارس المدة الملتبسة (بيانات قديمة): إذا المدة غير فارغة بعد القصّ وخارج `ActionReminderCalculator.ValidDurations` (نفس تعريف `dueDateSuspect`) ← `throw new ArgumentException("مدة التذكير غير معروفة — عدّل الإجراء من نافذة الإجراءات")` — تُرفض بدل إسكات الاشتباه بصمت.
6. الحساب: `days = ActionReminderCalculator.DurationDays(action.ReminderDuration?.Trim())` (`0` عند الغياب المباح فقط بعد الحارس أعلاه)، ثم `newActionDate = due.AddDays(-days).ToString("yyyy-MM-dd", InvariantCulture)`.
7. التحديث: `action.ActionDate = newActionDate` فقط — **لا مساس** بـ `Type` / `Text` / `ReminderDuration` / `ReminderColor` / `CreatedById` / `CreatedAt`.
8. الحفظ: `_actions.Update` + `_uow.SaveChangesAsync` داخل `_tx.RunAsync` (نفس نمط `UpdateExecutionActionAsync`).
9. التدقيق: `_audit.LogAsync(actorName, "action", documentId, doc.DocumentType, AuditWithActor($"نقل التذكير إلى {due:yyyy-MM-dd} (تاريخ الإجراء: {oldActionDate} ← {newActionDate})", doc))` — التاريخ القديم موثق صراحةً فلا ضياع لسجل التغيير.
10. الإرجاع: `ExecutionActionDto` محدث (نفس مُنشئ السطر `87-88`).

قواعد ملزمة: أي كتابة `DateTime` للأعمدة = `DateTime.UtcNow` حصرًا (لا `Local`) احترامًا لـ `UtcDateTimeConverter`؛ `ServerClock.Now` للعرض فقط.

### 5.3 `Services/DocumentAppealService.cs` — مرآة (`UpdateActionAsync:782-819`)

`RescheduleAppealReminderAsync(appealId, actionId, newDueDateRaw, userId, actorName, ct)` بنفس الخطوات `3-7` أعلاه (بما فيها حارس المدة الملتبسة `5ب`)، مع:

- الجلب عبر `_appeals.GetByIdWithDetailsAsync(appealId)` + `EnsureAssignedFollower(appeal, userId)` (بدل `CanAccessAsync` للملفات).
- البحث `appeal.Actions.FirstOrDefault(a => a.Id == actionId)` وإلا `throw new ArgumentException("الإجراء غير موجود")` (عرف الاستئناف القائم — يُردّ `400` كما في `UpdateActionAsync`).
- التحقق `Bounded` غير لازم هنا لأننا لا نلمس `Type/Text` — فقط `ActionDate` المحسوب بصيغة `yyyy-MM-dd` (تجتاز `ValidateActionDate` حتمًا).
- التدقيق: حدث `"reschedule_appeal_reminder"` (مرآة عرف `"clear_appeal_reminder"` في `ClearReminderAsync`) ونص `نقل تذكير إجراء على الاستئناف (رقم {appeal.Id}) إلى {due} (تاريخ الإجراء: {old} ← {new})`.
- الطلب: سجل نظير `RescheduleAppealReminderRequest(string? NewDueDate)` في `AppealDtos.cs` (بجانب `UpdateAppealActionRequest:90-95`) — اتساقًا مع عرف الفصل بين `DocumentDtos` و`AppealDtos`، لا إعادة استخدام عبر النطاقين.

### 5.4 `Api/Controllers/DocumentsController.cs` (بجانب `UpdateAction:742`)

```csharp
[HttpPost("{id:int}/actions/{actionId:int}/reschedule")]
public async Task<IActionResult> RescheduleReminder(int id, int actionId, [FromBody] RescheduleActionReminderRequest? request, CancellationToken ct)
```

- الحراسة: `if (!CanManageActions) return Forbid();` + `GetAsync` + `CanAccessAsync` (نفس `UpdateAction:745-751`).
- المعامل قابل للفراغ (`?`) قصدًا: الجسم الغائب/الفارغ يصل `null` فيُردّ `400` برسالة `اليوم الجديد مطلوب…` الودية بدل رسالة `[ApiController]` التقنية.
- `try { var dto = await _documents.RescheduleReminderAsync(... request?.NewDueDate ...); return Ok(dto); }`
- `catch (ArgumentException e) { return BadRequest(new { message = e.Message }); }` — `KeyNotFoundException` تُترجم `404` عبر المعالج العام (نفس سلوك `Update`).

### 5.5 `Api/Controllers/AppealsController.cs` (بجانب `UpdateAction:484`)

```csharp
[HttpPost("appeals/{id:int}/actions/{actionId:int}/reschedule")]
public async Task<IActionResult> RescheduleReminder(int id, int actionId, [FromBody] RescheduleAppealReminderRequest? request, CancellationToken ct)
```

- الحراسة: `if (!CanManageAppeals) return Forbid();` ثم `try/catch ArgumentException → 400` (نفس `UpdateAction:486-499` — بما فيها `الإجراء غير موجود` ورفض غير المتابع من `EnsureAssignedFollower`).
- `var dto = await _appeals.RescheduleAppealReminderAsync(id, actionId, request?.NewDueDate, User.GetUserId(), ActorName, ct); return Ok(dto);`

### 5.6 تصحيح رسالتي الأرشفة (إلزامي بعد حذف الخيار)

- `PersonalReminderService.cs:75`: `تجاوزت الحد الأقصى للتذكيرات النشطة ({Max}) — أرشف القديم منها` ← `… — احذف القديم منها`.
- `PersonalReminderService.cs:185`: `تجاوز سجل التكرارات المنجزة حده — أرشف التذكير وأنشئ غيره` ← `… — حدّد تاريخ انتهاء للتكرار أو احذف التذكير`.
- لا تغيير على عمود `IsArchived` أو فلاتر `PersonalReminderRepository.cs:21,31` أو `personalReminders.ts:91,207` (تبقى للبيانات القديمة) — **لا هجرات**.

---

## 6) تغييرات الواجهة `frontend/` (تفصيل)

### 6.1 `components/dashboard/DayRemindersModal.tsx`

**أ) إعادة الجدولة بدلالة `dueDate`:**

- `startReschedule(key, ...)`: القيمة الابتدائية `dayKeyOf(r.dueDate) ?? dayKey` مباشرةً (صيغة `yyyy-MM-dd` — يقبلها الخادم نصًا حرًا، فلا مُنسِّق جديد ولا منطق تواريخ في الواجهة، و§6.2 متسقة).
- `saveReschedule(o)`: حذف `GET actions` + `PUT`، واستبداله بـ:
  ```ts
  await api.post(`${base}/actions/${r.actionId}/reschedule`, {
    newDueDate: normalizeArabicDigits(rescheduleInput).trim(),
  });
  ```
  مع `import { normalizeArabicDigits } from '../../utils/arabicDigits'` (نفس عرف `DocumentForm`).
- `base` يبقى `/documents/${parentId}` أو `/appeals/${parentId}` حسب `o.kind` — التوحيد المطلوب.
- بعد النجاح: `setRescheduling(null); onChanged();` (يختفي الصف من اليوم القديم ويظهر في بلاطة اليوم الجديد بعد إعادة الجلب — سلوك صحيح).

**ب) التسمية:**

```tsx
<label htmlFor={...}>اليوم الجديد</label>
<input placeholder="مثال: 1/8/2026…" type="text" ... aria-describedby={helperId} />
<p id={helperId} className="text-[11px] text-gray-500">يوم الاستحقاق نفسه — المدة نفسها تُحفَظ، ويُعاد حساب تاريخ الإجراء تلقائيًا.</p>
```

- `label` مطابق لطلب المالك حرفيًا (`getByLabelText("اليوم الجديد")` يبقى أخضر).
- `type="text"` إلزامي (ممنوع `type="date"` حسب `Date Fields Rule`)، وبلا `inputMode="numeric"` (لوحة الأرقام على الجوال بلا مفتاح `/` — اتساقًا مع حقول التواريخ في `DocumentForm`)، و`name` و`autocomplete="off"` و`focus-visible:ring-*` تبقى.
- سطر المساعدة مربوط بالحقل عبر `aria-describedby` (إرشادات Vercel للوصول).

**ج) تنظيف الوسوم:**

- إضافة `import { richToPlainText } from '../../utils/richText';`
- السطر `:457`: `{richToPlainText(r.actionText)}` بدل `{r.actionText}` (مرآة `ReminderList.tsx:71,124`).
- البحث `:119`: `haystack` يستخدم `richToPlainText(o.reminder?.actionText ?? '')` بدل الخام.
- الفارغ بعد التجريد (مثل `<p><br></p>` → `''`): إخفاء سطر النص أو عرض `—` — لا واجهة مكسورة (`line-clamp-2` و`break-words` و`min-w-0` تبقى).

**د) حذف `مؤرشف` من `PersonalForm` (`:528-731`):**

- حذف `const [archived, setArchived] = useState(...)` (`:545`).
- حذف `isArchived: archived` من جسم `PUT /personal-reminders/:id` (`:575`) — يُترك `undefined` ليحفظ الخادم القيمة (`bool?` في `UpdatePersonalReminderRequest`).
- حذف كتلة `label checkbox` (`:693-705`) نهائيًا.
- لا `autoFocus` جديد؛ التركيز على العنوان (`titleRef`) يبقى.

**هـ) قواعد الجوال والوصول (إلزامية):**

- `min-h-11` لكل زر/حقل، `flex-wrap` و`overflow-y-auto overscroll-contain`، `aria-label` للأيقونية، `role="alert"` للأخطاء، احترام `prefers-reduced-motion`، أرقام `tabular-nums`.

### 6.2 `components/dashboard/personalReminders.ts`

لا منطق تواريخ جديد (لا نسخة واجهة من `DurationDays`). إعادة استخدام `dayKeyOf` / `toDayKey` / `dayLabelOf` للقيمة الابتدائية والمعاينة فقط.

### 6.3 `types/index.ts`

لا تغيير عقود (`ReminderDto:780-794` و`AppealReminderDto:1021-1030` تبقيان `dueDate: string` و`actionDate?: string`).

---

## 7) الاحتفاظ: لماذا لا تفريغ ولا حذف تلقائي؟

**الجواب المعتمد: التقويمات محفوظة دائمًا — لا حذف تلقائي.**

| الدليل | الموضع |
|--------|--------|
| لا خدمة احتفاظ للتذكيرات ولا `DeleteOlderThan` لها | بحث شامل في `backend/src` — الوحيدة `ForumRetentionService` للمنتدى |
| التقويم عرض محسوب لا مخزن | `groupRemindersByDay(reminders, personal, from, to)` — خارج النافذة يُسقط بصمت |
| `LawyerCalendar` يعرض شهرًا واحدًا + هامش `10` أيام | `LawyerCalendar.tsx:45-58` عبر `useMemo` |
| الجلب لمرة واحدة مع إلغاء عند التنقل | `CalendarPage.tsx` عبر `useCancellableRequest` (`AbortSignal`) |
| الحمولة صغيرة (`id + dueDate + actionText`) — مئات الصفوف < `200KB` | `GET /reminders` + `GET /appeals/reminders` + `GET /personal-reminders` |
| الشخصي مسقوف `200` نشطًا + مفهرس | `PersonalReminderCatalog.MaxActivePerUser` + `IX_PersonalReminders_IsArchived` |
| الاحتفاظ الوحيد في المشروع لغير التذكيرات | منتدى `Forum:RetentionMonths=6` (`appsettings.json:36`، `ForumOptions.cs:9`) وسجلات `RetainedDays=31` (`LoggingOptions.cs:9`) |
| الزوال الوحيد للتذكير | حذف يدوي صريح (`إلغاء التذكير` / `DELETE`) أو انتهاء `recurrenceEnd` (يتوقف عن الظهور دون حذف الصف) |

**اعتقاد المالك ("كثرة التقويمات قد تثقل التطبيق") — الرد:** القديم يبقى مخزنًا (مطلوب قانونيًا لسجل الملف والتدقيق والإحصاءات) لكنه لا يُرسم ولا يُلون ولا يُقرع جرسًا خارج النافذة. لا مبرر أداء لحذف جماعي عند هذه الأحجام. عند التضخم الحقيقي (آلاف النشطة) الحل هو جلب بنافذة `?from&to` وترقيم القائمة — لا `DELETE` شامل.

**ملاحظة الأرشفة (مؤكدة من المالك — لا تذكيرات مؤرشفة سابقًا):** لا صفوف `IsArchived=1` قائمة، وحذف الـ`checkbox` يمنع نشوء أي صف مؤرشف مستقبلًا (هو الكاتب الوحيد عبر `PUT`)، فيصبح العمود ميتًا كتابةً وقراءةً بلا يتامى. يُتحقق باستعلام واحد على القاعدتين قبل الدمج (بوابة §10-10) ويُترك العمود والفلاتر للتوافق — **لا هجرات**. تنظيف العمود نفسه خارج النطاق (مهمة هجرتين مستقبلة).

---

## 8) الحالات الحدية (مصفوفة القبول)

| الحالة | السلوك المطلوب |
|--------|----------------|
| مدخل فارغ/أبيض (أو جسم غائب) | `400 اليوم الجديد مطلوب — استخدم مثال: 1/8/2026` (لا سقوط إلى `CreatedAt`) |
| أرقام عربية `١/٨/٢٠٢٦` | تُطبع في الطرفين (`normalizeArabicDigits` + `ArabicDigitNormalizer`) ثم تُقبل |
| `31/2/2026` أو صيغة خارج الخمس | `400 اليوم الجديد غير صالح — استخدم مثال: 1/8/2026` |
| مدة فارغة مباحة (`days=0`) | `actionDate = dueDate` نفسه |
| مدة غير فارغة خارج الأربع (قديمة) | `400 مدة التذكير غير معروفة — عدّل الإجراء من نافذة الإجراءات` (لا إسكات لـ`dueDateSuspect`) |
| `note` بلا تاريخ أصلي | يُزرع تاريخ محسوب صريح (مسموح) |
| لا تذكير على الإجراء | `400 لا يوجد تذكير لإعادة جدولته` |
| غياب الإجراء/عدم تطابق الأب | ملفات `404` (`KeyNotFoundException`)؛ استئناف `400` (`الإجراء غير موجود` — عرف المسار) |
| سباق: أُلغي التذكير concurrently | `404` (ملفات) أو `400` (استئناف) برسالة ودية (لا كسر) |
| `<p><br></p>` بعد التجريد | سطر النص يُخفى أو `—` (لا وسوم ولا فراغ مكسور) |
| بعد النقل | الصف يختفي من اليوم القديم ويظهر في الجديد بعد `onChanged` (لا إبقاء وهمي) |

---

## 9) الاختبارات

### 9.1 خلفية — ملف جديد `backend/tests/DocGenerator.Application.Tests/RescheduleReminderTests.cs`

1. ملف: إنشاء إجراء `actionDate=1/8/2026` + `3 أيام` ثم `Reschedule(newDueDate=10/8/2026)` ← `ActionDate == "2026-08-07"` وإعادة حساب `TryComputeDueDate == 10/8/2026` غير مشتبه.
2. استئناف: `due=15/8/2026` + `أسبوع` ← `ActionDate == "2026-08-08"` (عبر `CreateAppealWithAssigneeAsync` + متابع).
3. رفض الفارغ وغير الصالح بنفس رسالتي `اليوم الجديد` أعلاه + رفض المدة الملتبسة (`سنة` قديمة ← `مدة التذكير غير معروفة`) + رفض غياب التذكير.
4. بقاء `Type/Text/Duration/Color` دون مساس + وجود سجل تدقيق يحوي `نقل التذكير` **والتاريخ القديم** (`←`).
5. صلاحيات: غير المالك / غير المتابع ← `Forbid` على مستوى المتحكم؛ خدميًا: `KeyNotFoundException` (ملفات) / `ArgumentException("الإجراء غير موجود")` (استئناف).
6. رسالتا السقف الجديدتان (`احذف` بدل `أرشف`) مغطاتان بنص مطابق.
7. تكامل (`DocumentsIntegrationTests` — عرف المسار): `POST reschedule` كمحامٍ ينقل اليوم فعلًا عبر الخط الكامل، وكمدير `403`، والتاريخ الفاسد `400` برسالة `اليوم الجديد` (ربط `JSON` والحراس والتوجيه — لا منطق مكرر).

### 9.2 واجهة — `DayRemindersModal.test.tsx` + `personalReminders.test.ts` + `richText.test.ts` (قائم)

1. تحديث اختباري `يغيّر يوم تذكير الملف/الاستئناف`: توقع `POST …/reschedule` مع `{ newDueDate: '10/8/2026' }` بدل `GET+PUT`، والقيمة الابتدائية `2026-08-08` (من `dueDate` مباشرةً) لا `actionDate`.
2. إدخال `١٠/٨/٢٠٢٦` يُرسَل مطبعًا `10/8/2026`.
3. فشل الخلفية `اليوم الجديد غير صالح` يظهر كـ `role="alert"`.
4. `onChanged` مرة واحدة وإغلاق نموذج إعادة الجدولة.
5. وسوم: `actionText="<p>راسل المصرف ببيانات رواتب</p>"` يظهر `راسل المصرف ببيانات رواتب` ولا `<p>` في `container.innerHTML`؛ والبحث بـ `راسل` يجده.
6. أرشفة: لا `checkbox مؤرشف` في DOM؛ جسم `PUT` التعديل لا يحوي `isArchived` (`toBeUndefined`).
7. `richToPlainText('<p>إجراء <strong>هام</strong></p>') === 'إجراء هام'` (مغطى فعلًا — يُعاد التأكيد).

---

## 10) بوابات التحقق قبل الدمج (إلزامية — لا تُتجاوز)

على الفرع `feature/reminder-calendar-polish`:

1. `dotnet test` للخلفية — أخضر.
2. `npx oxlint src` للواجهة — أخضر.
3. `npx tsc -b` — أخضر.
4. `npx vitest run` — أخضر.
5. `npm run build` — أخضر.
6. مراجعة المسار الكامل: واجهة → `DTO` → `Service` → `dueDate` → تقويم/جرس/إحصاءات، حقلًا بحقل (`DateTime?` / `decimal?` / `string?`).
7. بحث بقايا: `rg "api\.get.*\/actions|api\.put.*\/actions\/|\{r\.actionText\}|مؤرشف|personal-archived|isArchived: archived" frontend/src/components/dashboard` للتأكد من زوال مسار `GET+PUT` والـ `checkbox` والعرض الخام (تبقى `isArchived` في الأنواع والفلاتر والاختبارات القائمة قصدًا — خلفي للتوافق فقط).
8. فحص جوال بصري `375px`: لا تجاوز أفقي، لا عناصر مقطوعة، `Drawer` + الشريط السفلي سليمان (`Layout.tsx`).
9. مطابقة هذه الخطة بندًا ببند وتسجيل أي انحراف.
10. تحقق `SELECT COUNT(*) FROM PersonalReminders WHERE IsArchived = 1` = `0` على القاعدتين (`SQLite` + `Postgres`) — إثبات فرضية «لا مؤرشف سابقًا» قبل الدمج.

**حماية الفرع:** `master` محمي (`PR` + نجاح فحوصات `CI` الثلاثة `backend` + `frontend` + `audit` + منع `force-push`/الحذف). سلوك موصوف في `docs/audit/00-PROJECT-CONTEXT.md §2–§8` لا يُغيَّر بلا موافقة المالك — هذه الخطة تحملها.

---

## 11) خطة التنفيذ خطوة بخطوة (بعد الموافقة فقط)

1. إنشاء الفرع من `master` المحدث: `git checkout -b feature/reminder-calendar-polish`.
2. خلفية: إضافة `RescheduleActionReminderRequest` + `RescheduleReminderAsync` (ملفات) + `RescheduleAppealReminderAsync` (استئناف) + المساران في `Controllers` + تصحيح رسالتي `PersonalReminderService`.
3. `dotnet test` (بوابة 1).
4. واجهة: تعديل `DayRemindersModal.tsx` (إعادة الجدولة + التسمية + `richToPlainText` + حذف `مؤرشف`) — لا ملفات جديدة إلا لزم.
5. تحديث/إضافة اختبارات `DayRemindersModal.test.tsx`.
6. `npx oxlint src` + `npx tsc -b` + `npx vitest run` + `npm run build` (بوابات 2-5).
7. مراجعة المسار + بحث البقايا + فحص الجوال (بوابات 6-8).
8. `git status` + `git diff` + `git log --oneline -10` ثم `commit` + `push` + `PR` مع تقرير يذكر: **لا هجرات جديدة — لا `dotnet ef database update` مطلوب**.
9. انتظار اخضرار `CI` ثم الدمج عبر `PR` فقط.

---

## 12) المخاطر والتراجعات

| الخطر | التخفيف |
|-------|---------|
| تغير `actionDate` المعروض يفاجئ المستخدم (دَين قائم — مسار `PUT` الحالي يفعله أيضًا) | سطر المساعدة تحت الحقل + نص التدقيق يوثق التاريخين (قديم ← جديد) |
| سباق بين نافذتين على نفس الإجراء | آخر كتابة تفوز (نفس عقد `PUT`؛ لا عمود `Version` على الإجراءات)؛ لا قفل متفائل جديد في هذه الخطة |
| مدة قديمة ملتبسة (`dueDateSuspect`) | تُرفض برسالة `مدة التذكير غير معروفة` — لا إسكات صامت للاشتباه |
| سقف `200` بلا زر أرشفة | الرسالة الجديدة توجه للحذف أو `recurrenceEnd`؛ الحذف بتأكيد `modal`؛ ولا مؤرشف قائم أصلًا (§7) |
| انحراف محلل الواجهة عن الخادم | ممنوع أي حساب تواريخ في الواجهة — الخادم وحده يحسب؛ القيمة الابتدائية `yyyy-MM-dd` تُرسَل كما هي |

---

## 13) قائمة تحقق مقابل المطلوب

- [ ] المدخل في `اليوم الجديد` ينقل التذكير إلى اليوم نفسه (ملف + استئناف) مع بقاء المدة.
- [ ] التسمية `اليوم الجديد` حرفيًا + `placeholder` حر + تطبيع الأرقام العربية + `aria-describedby` للمساعدة.
- [ ] لا `<p>` في نافذة اليوم (نص عادي) والبحث يجده.
- [ ] لا خيار `مؤرشف (يُخفى من التقويم)` في الواجهة؛ الرسالتان الخلفيتان بلا إحالة للأرشفة؛ `COUNT(IsArchived=1)=0` على القاعدتين.
- [ ] المدة الملتبسة تُرفض (لا إسكات لـ`dueDateSuspect`)؛ التدقيق يوثق القديم والجديد.
- [ ] إجابة الاحتفاظ موثقة: دائم + لا تفريغ + لا حذف تلقائي.
- [ ] البوابات الخمس خضراء + مراجعة المسار + بحث البقايا + فحص الجوال.
- [ ] التقرير النهائي يذكر: صفر هجرات، صفر عقود مكسورة لـ `PUT`، صفر أسرار جديدة.
