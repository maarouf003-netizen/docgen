# خطة «تذكيرات الشطب» (الملفات المقاربة للشطب — 5 أشهر بلا إجراء)

> **الحالة:** معتمدة للتنفيذ. **الفرع:** `feature/strike-off-reminders` متشعّب من
> `fix/action-reminder-date-separation` (Stacked — لا دفع مباشر إلى `master`).
> **يستبدل:** `STALE_FILES_REMINDERS_PLAN.md` (لم تُنفَّذ).
> **قرارات المالك:** المحامي فقط · ملفات «طالبة تنفيذ» فقط · الإجراءات فقط (لا الملاحظات) ·
> العتبة **5 أشهر** · الأساس **`ActionDate`** · **لا Backfill** لـ`ActionDateParsed` (بيانات تجريبية) ·
> الإلغاء عمود على الملف (يُبطَل منطقيًا عند أي إجراء أحدث).

---

## 1) القاعدة (مصدر واحد)

```
آخر نشاط = MAX(ActionDateParsed ?? CreatedAt) على الإجراءات (Type == "action") فقط
            ?? RegistrationDate.DateParsed
            ?? Document.CreatedAt

يظهر الملف إذا:  آخر نشاط <= (الآن بتوقيت دمشق) − 5 أشهر
                 AND (DismissedAt IS NULL OR DismissedAt < آخر نشاط)
```

- **الملاحظات (`note`) لا تُحتسب** — ملف بملاحظات فقط يُحسب من تاريخ القيد (قرار المالك).
- **الاستبعادات:** مسودة/تحت الرفع · محذوف (فلتر EF) · مشطوب · المنفذة بالتسوية/الإنابة/المسترد/
  الجبري الكامل · «محال الى البداية». تُدرَج «منفذ جزئيا» و«تريث» (متداولان فعلًا).
- **الطرف:** `GeneralEntitySide == applicant` فقط.
- **الترتيب:** الأقدم ركودًا أولًا.
- **`ActionDateParsed`:** عمود يُكتب في إضافة/تعديل/زرع الإجراءات فقط (`TryParse(ActionDate)`)،
  ولا يمسّه أي مسار تذكير. الصفوف القديمة `NULL` ⇒ السقوط إلى `CreatedAt` (مقبول صراحةً).

## 2) البيانات والهجرات

| الكيان | العمود | SQLite | PostgreSQL |
|---|---|---|---|
| `ExecutionAction` | `ActionDateParsed` (`DateTime?`) | `TEXT` | `timestamp with time zone` |
| `Document` | `StrikeOffReminderDismissedAt` (`DateTime?`) | `TEXT` | `timestamp with time zone` |

- هجرتان: `AddStrikeOffReminders` / `AddStrikeOffReminders_Pg` — عمودان `NULL`، **بلا** Backfill/فهارس.
- `RUN_GUIDE.md §9` يُحدَّث بسطرين في نفس التغيير.

## 3) الخلفية

- `StrikeOffReminderCalculator` (Application/Common): `Months = 5`، `CutoffUtc(now, tz)`، وثابتا الترقيم.
- `IDocumentRepository.SearchStrikeOffReminderIdsAsync(...)` + `Count...`: استعلام خطوتين —
  (1) تصفية/عدّ/ترتيب/ترقيم وإرجاع المعرّفات + تاريخ آخر نشاط من SQL؛ (2) تحميل الكتبة
  بـ`WithStandardIncludes` بالترتيب نفسه.
- `DocumentResponse`: حقلان محسوبان مضافان `LastActivityDate` (DateTime?) و`LastActivitySource`
  (`action`/`registration`/`created`) — يُحسبان في `FromEntity` بالقاعدة نفسها (اختبار تطابق يلزمهما).
- `IStrikeOffReminderService` + `StrikeOffReminderService`: ساعة/منطقة محقونتان، نطاق `ApplyOwnerScope`،
  وإلغاء يكتب `DateTime.UtcNow` + تدقيق `strike_off_reminder_dismiss`.
- `RolePermissions.CanViewStrikeOffReminders = IsLawyer` (إلغاء أيضًا للمالك فقط).
- نقاط النهاية في `DocumentsController`:
  - `GET /api/documents/strike-off-reminders?page&perPage` ⇒ `PagedResult<DocumentResponse>`
  - `GET /api/documents/strike-off-reminders/count` ⇒ `{ count }`
  - `POST /api/documents/{id}/strike-off-reminder/dismiss` ⇒ `204` (محامٍ مالك فقط)

## 4) الواجهة

- صفحة `⁦/documents/strike-off-reminders⁩` (محامٍ فقط): جدول مكتبي + بطاقات جوال، ترقيم،
  حالات تحميل/خطأ/فراغ، وشعار «آخر إجراء» مع وسم مصدره عند كونه تاريخ قيد/إدخال.
- زرّا كل صف: «عرض الإجراءات والملاحظات» (`ExecutionActionsModal` كما هي) و«إلغاء التذكير»
  (تأكيد داخلي بخطوتين، ثم حذف الصف + رسالة).
- بطاقة «تذكيرات الشطب» في `LawyerIconRow` بشارة عدّاد من `/count` + `useBadgeCount`.
- لا بند في الشريط الجانبي (نمط اللوحة).

## 5) الاختبارات والبوابات

- وحدة: الحدود (5 أشهر تمامًا/أقل/أكثر)، سلسلة السقوط، الإلغاء المنطقي (إجراء أحدث يُبطل الإلغاء).
- تكامل: العزل (محامٍ آخر لا يرى)، كل استبعاد، `Type == note` لا يُحتسب، الإلغاء يُخفي، العدّاد،
  الصلاحيات (`403` لغير المحامي، `403` لمحامٍ غير مالك)، تطابق `lastActivityDate` المعروض مع ترتيب SQL.
- واجهة: الصفحة (جدول/بطاقات/إلغاء/خطأ/فراغ)، البطاقة في اللوحة، توجيه lawyer-only.
- البوابات الكاملة (خلفية/واجهة/هجرات) كما في §10 من خطة الإصلاح.

## 6) النشر

- `dotnet ef database update --context DocGeneratorPostgresDbContext` قبل نشر النسخة (حارس الإقلاع)،
  و`--context DocGeneratorDbContext` ضمن سلسلة `G2`.
- لا Backfill ⇒ لا إصلاح بيانات؛ الملفات القديمة قد تتأخر في الظهور (تاريخ الإدخال أحدث من الإجراء).

## 7) خارج النطاق

أدوار غير المحامي · استئنافات · إشعارات/بريد · تعديل قاعدة الستة أشهر · إصلاح التواريخ المشوّهة سابقًا.
