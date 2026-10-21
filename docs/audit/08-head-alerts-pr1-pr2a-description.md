# PR — Head alerts: Phase 0 (G0) + PR‑1 (Expand/Outbox schema) + PR‑2a (Outbox core)

> **الفرع:** `feature/head-alerts-pr1-expand-outbox` — 8 commits، 65 ملفًا.
> **الهدف:** إعادة هيكلة نظام التنبيهات `HeadAlert` وفق `docs/HEAD_ALERTS_RESTRUCTURE_PLAN.md`.
> **الحالة:** المرحلة 0 + PR‑1 + **PR‑2a (نواة التسليم)**. ترحيل مواقع الكتابة (PR‑2b) والواجهة (PR‑5) لم يبدأ.

---

## ⚠️ قبل الدمج — أطول ثلاثة أسطر في هذا الـPR

1. **⁧4 هجرات معلّقة** تُطبَّق **قبل** نشر النسخة:
   ```powershell
   dotnet ef database update --context DocGeneratorDbContext
   dotnet ef database update --context DocGeneratorPostgresDbContext
   ```
   بالترتيب: `HeadAlertTaxonomy_Expand` ← `HeadAlertOutbox_Create` ← (لاحقًا) `HeadAlertTaxonomy_Backfill` ← `HeadAlertTaxonomy_Contract`.
2. **⁧`Database:AutoMigrate=false` في الإنتاج ⇒ التطبيق يرفض الإقلاع** إن لم تُطبَّق الهجرات.
   **طبّق الهجرات قبل نشر النسخة، وإلا انقطاع إقلاع مقصود** (أفضل من `no such column` عند أول طلب).
3. **نسخة احتياطية + استعادة مجرَّبة** قبل النافذة (قاعدة `SQLite` تُعاد بناؤها في `Expand`).

---

## ما الذي يغيّره هذا الـPR؟

| | قبل | بعد |
|---|---|---|
| **سلوك التنبيهات** | كما هو | **كما هو** (لا مُنتِج يستعمل Outbox بعد) |
| **مخطط القاعدة** | — | أعمدة nullable + جدول Outbox (إضافة بحتة) |
| **نصوص التنبيهات** | مبثوثة في الخدمات | مركز واحد `HeadAlertMessages` |
| **مفتاح منع التكرار** | غير موجود | `HeadAlertDedupe` مركزي (غير مُفعَّل بعد) |
| **قصّ 2000/80 محرفًا** | قائم | **لا يزال قائمًا** (يزول في PR‑2b) |

**⇒ لا تغيير في عقد الـAPI، ولا في الواجهة، ولا في سلوك المستخدم.**

---

## البنية

### المرحلة 0 — بوابة G0 (docs)
- `scripts/head-alert-g0-audit.py` — تدقيق **قراءة فقط** (`mode=ro`).
- `docs/audit/06-head-alert-g0-20261020.md` — تقرير مؤرخ. كل أرقام المسودة المحلية في §2.3 تحقّقت حرفيًا (51 تنبيهًا، `open_by_status=2` مقابل `closed_linked=19`، صفر تكرارات، أطول نص 266).
- **G0 الإنتاجي لم يُنفَّذ** ⇒ الجسر الانتقالي `HeadAlerts:LegacyBridge:Enabled=false` ولا أرشفة تلقائية.

### PR‑1 — التوسّع (Expand) بلا كسر للتوافق
- **النطاق الدلالي**: `HeadAlertKindCatalog` (39 رمزًا + `legacy` المحجوز) + مصفوفة الجمهور + مجموعات العائلات.
- **مركز النصوص**: `HeadAlertMessages` (الصيغ 1–31 + التعميمات الثمانية + البادئة حسب الدور).
- **مفتاح منع التكرار**: `HeadAlertDedupe` (نطاق الحدث + نوع إلزامي + بديل SHA‑256 بدل القصّ).
- **الحدود**: `HeadAlertTextLimits` (معاينة بنقاط Unicode، بلا قطع `surrogate pair`، بلا علامة تجميع معلّقة).
- **المخطط**: `HeadAlertTaxonomy_Expand(_Pg)` (إضافة بحتة، بلا backfill، بلا فهرس فريد، بلا قيد `Kind`) + `HeadAlertOutbox_Create(_Pg)` (جدول + 4 فهارس + 5 قيود، و`Down` يرفض إسقاط جدول فيه رسائل غير محسومة).
- **التشغيل**: `Database:AutoMigrate` (خطأ في الإنتاج ⇒ رفض إقلاع بدل فشل صامت) + فحص `foreign_key_check`/`integrity_check` بعد ترحيل SQLite.

### PR‑2a — نواة التسليم (لا مُنتِج بعد)
- `IHeadAlertOutbox` (منتج) + `IHeadAlertOutboxStore` (مخزن) + حمولة/خيارات.
- `HeadAlertService.CreateSystemAsync` (معالج داخلي): مفتاح مركزي، حارس حداثة، إعادة ضبط القراءة **عند تغيّر النص فقط**، `CreatedAt` ثابت، أرشفة مقيدة بالنوع.
- `HeadAlertOutboxStore` + `HeadAlertOutboxLock` (قفل نسخة واحدة) + `HeadAlertOutboxDispatcher` (عامل واحد مرتّب، صف واحد لكل دورة، إعادة واحدة عند التعارض، `skipped`/`failed` صريحة).
- استثناءات داخلية بلا HTTP: `HeadAlertConflictException`، `HeadAlertNoRecipientsException` (**لا 409**).

---

## المراجعة النقدية (سطرًا بسطر)

أُجرت مراجعة كاملة للـ65 ملفًا: منطق، ذرّية، تزامن، صلاحيات، **تسريب بيانات**، تبعيات DI، فصل طبقات. **11 عيبًا** رُصدت وأُغلقت باختبارات رجعية:

| الخطورة | العيب |
|---|---|
| 🔴 | نطاق `DedupeKey` من الطلب الخام ⇒ جمهور «منشئ الملف» يفشل دائمًا |
| 🔴 | البديل غير محفوظ قبل `SupersedePlan` ⇒ كان يمكن أن يؤرشف نفسه |
| 🔴 **تسريب** | `LastError` يخزن رسالة استثناء قد تحوي **نص التنبيه** (الحمولة) |
| 🔴 | المشغّل (singleton) يبتلع مصنِّفًا (scoped) ⇒ **التطبيق لا يقلع** (496 اختبارًا) |
| 🟠 | السلف يبقى مستلمًا وفي `TargetLawyerId` بعد التعاقب |
| 🟠 | سياسة تعارض الفهرس الفريد كانت **غير قابلة للوصول** |
| 🟠 | `circuit_renamed` بلا صفوف فعلية كان يُوسم «معالَج» |
| 🟡 | `excludeAlertId` مجهول يُتجاهل ⇒ أرشفة جماعية صامتة |
| 🟡 | معاملة كتابة كل 2 ثانية على قائمة فارغة (تنافس قفل SQLite) |
| 🟡 | `PRAGMA foreign_keys` لا يُعاد عند الفشل إذا كان الاتصال مفتوحًا |

> عيبان كانا **ممنوعين أصلًا في الخطة** وظهرا بالاختبارات: قيمة `Operation` كانت تُكتب
> `circuitrenamed` (‏`Enum.ToString()` يُسقط الشرطة السفلية) فترفضها قيد `CHECK`؛
> والمقارنة النصية العربية considered `إ`≈`ا` — فصارت كل المقارنات الحرجة `Ordinal`.

---

## البوابات (كلها خضراء)

| البوابة | النتيجة |
|---|---|
| `dotnet test` (خلفية) | ✔ **2030 + 673** |
| `npx oxlint src` · `npx tsc -b` | ✔ 0/0 |
| `npx vitest run` | ✔ **1599** (152 ملفًا) |
| `npm run build` | ✔ |
| بناء + اختبار **كل commit معزولًا** (worktree مستقل) | ✔ 8/8 |
| `ModelSnapshot` مقابل النموذج الحيّ (SQLite + PG) | ✔ «No changes» |
| فحص ترميز 64 ملفًا | ✔ 0 سطر تالف |

---

## ما **ليس** في هذا الـPR (متوقّع)

- **PR‑2b**: ترحيل الـ26 موقعًا إلى Outbox + حذف 31 `catch head_alert_failed` + تنظيف
  `CreateHeadAlertRequest` + `M2` (التصنيف + حل التصادم + دمج المستلمين + الفهرس الفريد الجزئي).
  المصفوفة الإلزامية جاهزة في `docs/audit/07-head-alert-migration-matrix-20261020.md`.
- **PR‑3** `Contract` · **PR‑4** API · **PR‑5** الواجهة (تُنشر مع PR‑4 في نافذة واحدة) · **PR‑6** إزالة الجسر.

## انحرافات موثّقة عن الخطة

1. تنظيف `CreateHeadAlertRequest` من حقوله الداخلية: **PR‑2b** لا PR‑1 (الذرتية مع نداءات النظام الـ13).
2. عقد التفاصيل النهائي `HeadAlertDto`: **PR‑4** لا PR‑1 (PR‑1 لا يمسّ العقد الخارجي).
3. `Database:AutoMigrate` + رفض الإقلاع: **إضافة** تتجاوز نص الخطة (سلامة تشغيلية).
