# ملحق 6 — خريطة أدلة §18: حالة تعارض صريحة في `Outbox` (2026‑11‑03)

> ملحق سادس مؤرَّخ للوثيقة `head-alerts-18-evidence-map-20261103.md`. **الأصل والملاحق 1–5
> لا تُحرَّر** (سياسة `docs/audit`)؛ كل ما يُسجَّل هنا جديد ومؤرَّخ.
>
> **الغرض:** توثيق إغلاق `§18:3115` (سياسة «إعادة واحدة» عند تعارض `DedupeKey`) بعمود حالة
> صريح بدل استخدام `LastError` التشخيصي ذاكرةً للحالة، مع هجرتَي `SQLite` و`PostgreSQL`
> وتعبئة الصفوف القائمة.

---

## 1) الخلل قبل الإصلاح

كان `HandleConflictAsync` يقرأ **نص** `LastError`:

```csharp
if (string.Equals(message.LastError, "conflict_retry", StringComparison.Ordinal))
    await MarkFailedAsync(...);            // تعارض ثانٍ
else
    await MarkRetryAsync(..., "conflict_retry");  // تعارض أول
```

و`HandleTransientAsync` يكتب `"<ExceptionType>: <message>"` في العمود نفسه. فالتسلسل
**تعارض ← خطأ عابر ← تعارض** يطمس العلامة، فيُعامَل التعارض الثاني كأنه الأول ⇒ **إعادة
ثانية**، والحد الفعلي يصير تقريبًا `MaxAttempts / 2` (افتراضيه في الإنتاج **10**) بدل واحدة.
و`LastError` عمود **تشخيصي** بحسب تعليق الكيان، فلا يصلح آلة حالة.

## 2) الحل: عمود `ConflictRetries`

| العنصر | التفصيل |
|---|---|
| الكيان | `HeadAlertOutboxMessage.ConflictRetries` (`int`) — تعليق صريح بأنه حالة سياسة لا تشخيص |
| التهيئة | `IsRequired()` + قيد `CK_HeadAlertOutbox_ConflictRetries` (`>= 0`) |
| المخزن | `IHeadAlertOutboxStore.MarkConflictRetryAsync`: `pending` + `NextAttemptAt = UtcNow` + `ConflictRetries += 1` + العلامة في `LastError` |
| المشغّل | `HandleConflictAsync`: `ConflictRetries >= 1` ⇒ `failed` ببادئة `conflict_retry:`، وإلا ⇒ إعادة واحدة |
| التشخيص | `LastError` يبقى للسبب/العلامة، ولا يُقرأ لقرار السياسة |

**الهجرتان** (لم تُطبَّقا على أي قاعدة بعد):

- `Persistence\Migrations\20261103172359_HeadAlertOutbox_ConflictRetries.cs` — `INTEGER NOT NULL DEFAULT 0` + القيد + **تعبئة** `ConflictRetries = 1` حيث `LastError = 'conflict_retry'`.
- `Persistence\MigrationsPostgres\20261103172448_HeadAlertOutbox_ConflictRetries_Pg.cs` — `integer NOT NULL DEFAULT 0` + القيد + التعبئة نفسها.

**لماذا التعبئة؟** صفٌّ معلّق قبل النشر وقد استهلك إعادته الواحدة (علامته في `LastError`)
كان سيحصل على إعادة ثانية بعد الهجرة لو بدأ العدّ من صفر. التعبئة تُثبّت الحالة لحظة النشر.

## 3) السلوك بعد الإصلاح

| التسلسل | قبل | بعد |
|---|---|---|
| تعارض ← نجاح | `processed` بمحاولتين | نفسه (بلا تغيير) |
| تعارض ← تعارض | `failed` ببادئة العلامة | نفسه |
| **تعارض ← خطأ عابر ← تعارض** | `pending` **مرة أخرى** (خرق السياسة) | **`failed`** — العدّ محفوظ في عموده |
| تعارض ← إعادة تشغيل العملية ← تعارض | `failed` (العلامة مخزّنة) | `failed` (العدّ مخزّن) |

`Attempts` لم يتغيّر دوره: يزيد عند **كل** التقاط ويُستخدم للتباعد التصاعدي وحدّ
`MaxAttempts` للعابر، وليس لقرار التعارض.

## 4) الأدلة (اختبارات)

| الاختبار | ما يثبته |
|---|---|
| `ProcessNext_DedupeConflict_RetriesOnceWithMarkerThenSucceeds` | `ConflictRetries = 1` بعد التعارض، ويبقى 1 بعد النجاح، والعلامة في `LastError` |
| `ProcessNext_RepeatedDedupeConflict_FailsWithConflictRetryPrefix` | `ConflictRetries = 1` مع `failed` وبادئة العلامة |
| `ProcessNext_DedupeKeyUniqueViolation_UsesConflictPolicyNotTransientBackoff` | الفرع الإنتاجي (`DbUpdateException` حقيقي) يزيد العدّ ويعطي `conflict_retry` لا `backoff` |
| **`ProcessNext_ConflictThenTransientThenConflict_StillFailsAfterOneRetry`** | **جوهر الإصلاح:** الخطأ العابر يطمس `LastError` لكن `ConflictRetries` يبقى 1 ⇒ التعارض الثاني `failed` |
| `ProcessNext_BusinessTransactionFails_…` | تراجع المعاملة (مُدقِّق يتحقق من الإدراج قبل الفشل) — بلا تغيير |

## 5) التشغيل عند النشر

```
dotnet ef database update --context DocGeneratorDbContext
dotnet ef database update --context DocGeneratorPostgresDbContext
```

- الهجرتان **مستقلتان** عن ترتيب هجرات التنبيهات الأربع المعلّقة في `SQLite`، لكن `database update`
  يطبّق ما قبلهما ضمنًا — فطبّق الترتيب المعتمد في `RUN_GUIDE.md` §9 كما هو.
- الإنتاج يعمل بـ`Database:AutoMigrate=false` ⇒ **التطبيق قبل النشر**، وإلا يرفض التطبيق الإقلاع
  (سلوك مقصود) أو يفشل المشغّل بـ`no such column: m.ConflictRetries`.
- تم التحقق محليًا: `has-pending-model-changes` = لا تغييرات (السياقان)، وتطبيق الهجرة على قاعدة
  `SQLite` جديدة نجح، وسكربت `PostgreSQL` لا يحوي أنواع `SQL Server` المحظورة.

## 6) الحدود

- لا تغيير في الواجهة ولا في العقود السلكية؛ التغيير داخلي في جدول `Outbox` فقط.
- لا يمسّ `Attempts` ولا `MaxAttempts` ولا تباعد المحاولات العابرة.
- `LastError` يبقى عمود تشخيص/علامة، ويُمنع مستقبلًا استخدامه لقرار سياسة (المرجع: تعليق الكيان).
