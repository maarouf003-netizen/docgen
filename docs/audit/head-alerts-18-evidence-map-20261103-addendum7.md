# ملحق 7 — خريطة أدلة §18: تصحيح إجراء نشر `ConflictRetries` (2026‑11‑03)

> ملحق سابع مؤرَّخ للوثيقة `head-alerts-18-evidence-map-20261103.md`. **الأصل والملاحق 1–6
> لا تُحرَّر** (سياسة `docs/audit`)؛ التصحيح هنا فقط.
>
> **الغرض:** تصحيح ادعاء «الهجرتان مستقلتان» في الملحق 6 §5، وتوثيق إعادة بناء جدول
> `SQLite`، وحدّ التعبئة، والأدلة الجديدة التي أُضيفت بعد مراجعة نقدية.

---

## 1) تصحيح: «مستقلتان» غير دقيق تشغيليًا

الملحق 6 §5 قال إن الهجرتين «مستقلتان عن ترتيب هجرات التنبيهات الأربع». الصحيح:
**مستقلتان دلاليًا فقط**. `dotnet ef database update` يطبّق **كل ما قبل الهدف ضمنًا**.

**الدليل (تجربة على قاعدة جديدة):** هجرة إلى `HeadAlertTaxonomy_Expand`، ثم إلى الهجرة
الجديدة وحدها:

```
dotnet ef database update 20261103172359_HeadAlertOutbox_ConflictRetries
```

**سجل `__EFMigrationsHistory` بعدها:**

```
Expand → Outbox_Create → Backfill → Contract → ConflictRetries
```

⇒ على `SQLite` (رأسه `Expand`) تطبيق الهجرة الجديدة **يسحب `Contract`** المحظورة قبل `G2`.
أما `PostgreSQL` (رأسه `Contract_Pg` المطبَّق حيًا) فتُطبَّق وحدها بلا سحب.

**الإجراءان الصحيحان (`SQLite`):**

1. استكمال السلسلة (`Expand` ← `Outbox_Create` ← `Backfill` ← `Contract` بعد `G2`) ثم تطبيق الجديدة.
2. على قاعدة عند `Contract` فعلًا: سكربت فرقي فقط:
   `dotnet ef migrations script 20261026151929_HeadAlertTaxonomy_Contract 20261103172359_HeadAlertOutbox_ConflictRetries`.

## 2) إعادة بناء جدول `SQLite` (تحذير تشغيلي)

`AddCheckConstraint` على `SQLite` يُجبر إعادة بناء `HeadAlertOutboxMessages`
(`ef_temp_…` ← نسخ ← `DROP` ← `RENAME`). أي كتابة من المشغّل بين النسخ والإسقاط **تُفقد**.
⇒ **أوقف المشغّل/الكتابة قبل التطبيق** (كما في `Contract`). على `PostgreSQL` لا إعادة بناء:
`ADD COLUMN … NOT NULL DEFAULT 0` ثم قيد يُتحقق مرة واحدة.

## 3) حدّ التعبئة (مُوثَّق صراحةً)

`UPDATE … SET "ConflictRetries" = 1 WHERE "LastError" = 'conflict_retry'` تُثبّت الحالة
**فقط** إذا كانت العلامة لا تزال في `LastError`. صفٌّ وقع له «تعارض ← خطأ عابر» **قبل**
النشر فقد علامته أصلًا ⇒ يبقى `0` ويُمنح إعادة تعارض واحدة إضافية. لا ضرر بيانات
(المعاملة تُرجع دائمًا)، والأثر محاولة إضافية لصفّ نادر.

## 4) أدلة أُضيفت بعد المراجعة

| الدليل | ما يثبته |
|---|---|
| `Migration_ConflictRetries_BackfillsConsumedMarkerOnly` | تعبئة `pending+marker` ⇒ 1، و`pending+transient` ⇒ 0، و`processing+marker` ⇒ 1 (نفس ما تحقّقت منه يدويًا) |
| `Outbox_ConflictRetries_NegativeRejected` | قيد `CK_HeadAlertOutbox_ConflictRetries` يرفض `-1` |
| `MaxConflictRetries` كثابت مسمّى | لا رقم سحري في `HandleConflictAsync` |
| تأكيد `Attempts = 3` في اختبار التداخل | يثبت أن كل محاولة (تعارض/عابر/تعارض) استُهلكت مرة واحدة |

## 5) ما لم يتغيّر

- سياسة «إعادة واحدة» نفسها، والكود، والهجرتان (لا تعديل عليهما).
- `LastError` يبقى تشخيصيًا؛ والقرار من `ConflictRetries` وحده.

## حدود هذا الملحق

- لا يعدّل الملاحق 1–6؛ يصحّح ما ورد في §5 منها فقط.
- لا هجرة جديدة هنا؛ التصحيح توثيقي واختباري.
