# PR‑2b — نشر التنبيهات على البيئة الحية (PostgreSQL / Supabase) — 2026‑10‑25

> **النوع:** سجل نشر حي — **لقطة مؤرخة**، لا تُحرَّر بعد اليوم؛ أي تصحيح لاحق يكون بملحق مؤرخ جديد.
> **الفرع:** `feature/head-alerts-pr2b-migrate-sites` → `master` (PR #50)
> **الدمج:** `ec278c0` — `2026‑10‑25T17:41:37Z`
> **البيئة:** Render `docgen-1-tt3j` + Supabase `qkscixevhitvmxpwtxke` (PostgreSQL 17.11)
> **المرجع:** `docs/audit/09-head-alerts-pr2b-m2-20261025.md` (تقرير الـPR) و`RUN_GUIDE.md` §9.

---

## 0) الخلاصة

| البند | النتيجة |
|---|---|
| هجرات PostgreSQL الثلاث | ✅ مطبَّقة على Supabase بالترتيب الموثّق |
| إجمالي الهجرات | **62** (كانت 59) · **0** معلّقة فعليًا |
| كود `master` على Render | ✅ `ec278c0` (بعد نجاح CI) |
| دورة `Outbox` كاملة | ✅ `pending → processed` خلال **3 ثوانٍ** · `Attempts = 1` |
| سلامة البيانات | ✅ 28 تنبيهًا / 28 مستلمًا · 0 تكرار · 0 تنبيه بلا مستلم · 0 `Kind` فارغ |
| الجسر الانتقالي | ✅ ما زال مغلقًا (`HeadAlerts:LegacyBridge:Enabled = false`) |

**لم تُطبَّق** هجرات `SQLite` الثلاث بعد (سياق التشغيل المكتبي/المحلي) — انظر §8.

---

## 1) حالة ما قبل التنفيذ

قياس حي على `Supabase` قبل أي كتابة:

| البند | القيمة |
|---|---|
| الإصدار | `PostgreSQL 17.11` · قاعدة `postgres` · الحجم `16 MB` |
| الهجرات المطبَّقة | **59**، آخرها `20261014181050_AddInclusionNotesPg` |
| `HeadAlertTaxonomy_Expand_Pg` | ❌ غير مطبَّقة (`Kind` غير موجود) |
| `HeadAlertOutbox_Create_Pg` | ❌ غير مطبَّقة (`HeadAlertOutboxMessages` غير موجود) |
| `HeadAlertTaxonomy_Backfill_Pg` | ❌ غير مطبَّقة |
| `HeadAlerts` / `HeadAlertRecipients` | 26 / 26 · 0 مستلم يتيم · 0 تكرار `(HeadAlertId, UserId)` |
| الكتابات النشطة | 0 استعلام عامل، 12 اتصالًا خاملًا |
| `Database:AutoMigrate` | `false` (حارس إقلاع مقصود) |

---

## 2) النسخة الاحتياطية قبل الكتابة

خطة `Supabase` المجانية **بلا نسخ احتياطي تلقائي**، فأُخذت لقطة منطقية كاملة قبل الهجرات:

| البند | القيمة |
|---|---|
| المجلد | `%TEMP%\opencode\backups\backup-20261025-182038` (خارج المستودع) |
| المحتوى | **47 جدولًا** · **1,110 صفًا** · **351 KB** · 48 ملفًا |
| المانيفست | `manifest.txt` بعدد الصفوف وبصمة `SHA256` لكل جدول |
| الجداول الحرجة | `HeadAlerts = 26` · `HeadAlertRecipients = 26` · `Users = 11` · `Branches = 1` |

> **حدّ صريح:** هذه لقطة بيانات `JSONL` وليست `pg_dump` كاملًا بمخططه؛ تكفي لهذه الهجرات الثلاث
> (لا تمسّ إلا جدولي التنبيهات وتنشئ جدولًا جديدًا)، **ولا تكفي** كنشر إنتاجي حقيقي.

---

## 3) الهجرات المطبَّقة

نُفِّذت واحدةً تلو الأخرى (`dotnet ef database update <name>` لكل هجرة، لا `update` على آخر اسم)،
بين لقطة `18:20:38Z` وإقلاع النسخة `18:30:12Z`، وكل خطوة أعادت `Done`.

| # | الهجرة | ما فعلته | التحقق بعدها |
|---|---|---|---|
| 1 | `20261020202452_HeadAlertTaxonomy_Expand_Pg` | أضافت إلى `HeadAlerts`: `Kind` (64, NULL)، `DedupeKey` (200, NULL)، `ExecutionCircuitId` (NULL)، `LastContentChangedAt` (NULL)، `ArchivedAt` (NULL)، `IsArchived` (`NOT NULL DEFAULT false`)، ووسّعت `Message` إلى `text` | الهجرات **60** · الأعمدة الستة موجودة بأنواعها · `IsArchived IS NULL = 0` |
| 2 | `20261020202700_HeadAlertOutbox_Create_Pg` | أنشأت `HeadAlertOutboxMessages` (`bigint identity` + `PK`) مع 5 قيود `CHECK` و4 فهارس | الجدول والفهارس والقيود موجودة |
| 3 | `20261021232825_HeadAlertTaxonomy_Backfill_Pg` | تصنيف الصفوف القديمة/التدفقات المفتوحة، بناء `DedupeKey`، أرشفة المراجعة المتقاعدة، حلّ تصادم المفاتيح ودمج المستلمين، ثم `DROP INDEX IX_HeadAlerts_TargetLawyerId` + FK الدائرة + الفهارس الفريدة | الهجرات **62** · `Kind/LastContentChangedAt` بلا NULL · 0 تكرار مفاتيح/مستلمين |

### القيود والفهارس الحاسمة (موجودة حيًا)

- `UX_HeadAlerts_DedupeKey_Active` — فريد جزئي:
  `("DedupeKey") WHERE "DedupeKey" IS NOT NULL AND "IsArchived" = false`
- `UX_HeadAlertRecipients_HeadAlertId_UserId` — فريد على `("HeadAlertId", "UserId")`
- `FK_HeadAlerts_ExecutionCircuits_ExecutionCircuitId` — `ON DELETE SET NULL`
- `HeadAlertOutboxMessages`: `CK_HeadAlertOutbox_Operation`، `CK_HeadAlertOutbox_Status`،
  `CK_HeadAlertOutbox_Kind_Operation`، `CK_HeadAlertOutbox_PayloadVersion`، `CK_HeadAlertOutbox_Attempts`
  + `IX_…_Status_NextAttemptAt_Id`، `IX_…_Status_LockedUntil`، `IX_…_Status_TargetUserId`، `IX_…_Status_ProcessedAt`.

### توزيع `Kind`

**بعد الـBackfill مباشرة:** 26 صفًا = **13 `legacy`** + **13 مصنّفة**. ثم أُضيف تنبيهان في الفحص
الدخاني (`manual_to_lawyer` و`appeal_pending_to_owner`)، فصار الإجمالي 28:

| `Kind` | العدد | متى أُضيف |
|---|---|---|
| `legacy` | 13 | نتيجة الـBackfill |
| `delegation_assigned_to_lawyer` | 3 | تصنيف الـBackfill |
| `review_letter_reply` | 3 | تصنيف الـBackfill |
| `appeal_assigned_to_lawyer` | 2 | تصنيف الـBackfill |
| `appeal_final_to_base_lawyer` | 1 | تصنيف الـBackfill |
| `delegation_completed_returned` | 1 | تصنيف الـBackfill |
| `delegation_mirror_local_addition` | 1 | تصنيف الـBackfill |
| `delegation_mirror_updated` | 1 | تصنيف الـBackfill |
| `delegation_pending_completion` | 1 | تصنيف الـBackfill |
| `manual_to_lawyer` | 1 | الفحص الدخاني — يدوي (`#35`) |
| `appeal_pending_to_owner` | 1 | الفحص الدخاني — نظامي (`#36`) |
| **الإجمالي** | **28** | |

---

## 4) نشر الكود على Render

| البند | النتيجة |
|---|---|
| الالتزام المنشور | `ec278c0` (دمج PR #50) |
| CI بعد الدمج | ✅ التشغيل `40217691203` — **6/6** وظائف ناجحة |
| سجل الإقلاع | `Database provider active: postgres (auto-migrate at startup: False)` ⇒ حارس الهجرات مرّ |
| `GET /healthz` | `200 Healthy` |
| `GET /login` | `200` مع `CSP-Report-Only` و`X-Frame-Options: DENY` و`X-Content-Type-Options: nosniff` و`Referrer-Policy: no-referrer` |

### إثبات أن البناء الجديد هو العامل (لا مجرد `healthz`)

1. **استعلام المشغّل الدوري** ظهر في `pg_stat_activity`:
   `SELECT EXISTS (… FROM "HeadAlertOutboxMessages" WHERE "Status" = 'pending' …)` —
   لا وجود له في البناء القديم (قبل PR‑2b).
2. **كتابة `Kind`** من تنبيه أُنشئ بعد النشر — العمود لا يعرفه البناء القديم أصلًا.

---

## 5) الفحص الدخاني — المساران (اليدوي والنظامي)

| المسار | التنبيه | `Outbox` | الاستنتاج |
|---|---|---|---|
| يدوي (`POST /api/alerts`) | `#35` · `manual_to_lawyer` · مستلم واحد · `18:34:13Z` | **0 صفوف** | متوقّع بالتصميم: `HeadAlertService.CreateAsync` يكتب التنبيه مباشرة |
| نظامي (استئناف) | `#36` · `appeal_pending_to_owner` · مستلم واحد · `18:38:45Z` | **صف واحد** | يثبت أن الكتابة النظامية تمرّ بالصندوق |

### دورة الصادر الكاملة

| الحقل | القيمة |
|---|---|
| `Id` | `1` |
| `Operation` / `Status` | `upsert` / **`processed`** |
| `Kind` | `appeal_pending_to_owner` |
| `Attempts` | **1** |
| `TargetUserId` | `4` |
| `CreatedAt` → `ProcessedAt` | `18:38:45Z` → `18:38:48Z` (**3 ثوانٍ**) |
| `LastError` | فارغ · لا صفوف `failed`/`skipped` |

### التنبيه الناتج

| الحقل | القيمة |
|---|---|
| `Id` | `36` |
| `Kind` | `appeal_pending_to_owner` |
| `DedupeKey` | `appeal:3:recipient:4:appeal_pending_to_owner` |
| `AppealId` / `DocumentId` | `3` / `6` |
| `CreatedById` | `5` |
| المستلمون | **1** (`UserId = 4`) · `IsRead = false` |
| `CreatedAt` | `18:38:45Z` (مطابق لـ`OccurredAt` كما يصمّم المشغّل) |

### سلامة البيانات بعد الاختبارين

```
HeadAlerts = 28 · HeadAlertRecipients = 28 · HeadAlertOutboxMessages = 1
DedupeKey نشط مكرر = 0 · (HeadAlertId, UserId) مكرر = 0
تنبيهات بلا مستلمين = 0 · Kind IS NULL = 0
```

> **التفسير:** 26 صفًا قبل النشر + التنبيه اليدوي `#35` + تنبيه الاستئناف `#36` = 28، ولكلٍّ مستلمه.

---

## 6) تحذيرات التشغيل المرصودة

1. **`Cannot load library libgssapi_krb5.so.2`** — تحذير **تجميلي**: صورة `aspnet:10.0` بلا حزمة
   `libgssapi-krb5-2`، وهي مطلوبة لمصادقة Kerberos فقط؛ الاتصال بكلمة المرور يعمل. الإصلاح
   الاختياري لاحقًا في `Dockerfile` (خارج نطاق هذا الـPR).
2. **تحذير `DataProtection`** (مفاتيح على قرص مؤقت غير مشفّرة) — قائم من قبل، ويخص فقدان الجلسات
   بعد إعادة النشر، وليس هذا التغيير.
3. **الخطة المجانية:** Render ينام بعد ~15 دقيقة خمول (أول طلب قد يعيد `503` أثناء الإيقاظ)،
   وSupabase بلا نسخ تلقائي وبسقف تخزين — ما تم هنا لقطة يدوية وليس بديلًا عن نسخ مُدارة.
4. **المشغّل يفترض نسخة تطبيق واحدة** (الخطة §16.1): `WEB_CONCURRENCY=1`. أي توسّع أفقي يتطلب
   إعادة مراجعة آلية الالتقاط/الحجز.
5. **خطر ترتيب الرسائل المختلفة المفتاح** الموثّق في `docs/audit/09-head-alerts-pr2b-m2-20261025.md` §7.6
   ما زال قائمًا، ومعالجته في PR‑3 (هوية تجمّع للرسائل + إسقاط الرسائل المعلّقة الأقدم).
6. **نافذة قصيرة بين `M2` والنشر** كان يمكن أن يُنشئ فيها الكود القديم مستلمًا مكررًا؛ لم تحدث
   (التطبيق كان نائمًا)، والفهرس الفريد يحمي بعدها.

---

## 7) حالة الهجرات النهائية

| السياق | العدد | المعلّق |
|---|---|---|
| `PostgreSQL` (Supabase) | **62** | **0** فعليًا — مطابقة الاسم `20260814161218_AddPostgresPendingSchema` ليست معلّقة |
| `SQLite` | — | **3**: `HeadAlertTaxonomy_Expand` ← `HeadAlertOutbox_Create` ← `HeadAlertTaxonomy_Backfill` |

> أمر التحقق المستخدم (بلا أسرار):
> `dotnet ef migrations list --context DocGeneratorPostgresDbContext --project src/DocGenerator.Infrastructure --startup-project src/DocGenerator.Api`

---

## 8) ما تبقّى (خارج نطاق هذا السجل)

1. **هجرات `SQLite` الثلاث** — تُطبَّق قبل تشغيل الكود الحالي على سياق `SQLite`
   (`dotnet ef database update --context DocGeneratorDbContext`).
2. **PR‑3 / M3:** `HeadAlertTaxonomy_Contract(_Pg)` — فرض `Kind` غير فارغ + قيد `CHECK`،
   مشروطٌ بأن تكون كل النسخ على PR‑2 وبصفر قيم `NULL` (متحقق حاليًا على Supabase) واستيفاء `G1/G2`.
3. **PR‑4/PR‑5:** عقد الـAPI النهائي والواجهة. **PR‑6:** إزالة الجسر الانتقالي (يبقى مغلقًا الآن).
4. **مراقبة أول 24 ساعة:** عدّادات `processed` / `skipped` / `failed`، وخصوصًا
   `missing_reference` و`recipient_inactive`.
5. **أمني:** رابط قاعدة البيانات وُضع في ملف مؤقت خارج المستودع وحُذف بعد التحقق؛
   **يُنصح بتدوير كلمة مرور القاعدة** لأنها لُصقت في محادثة.

---

## 9) المراجع

- `docs/HEAD_ALERTS_RESTRUCTURE_PLAN.md` — §6.2، §6.5، §8.1، §14.1، §16.
- `docs/audit/07-head-alert-migration-matrix-20261020.md` — مصفوفة مواقع الكتابة.
- `docs/audit/09-head-alerts-pr2b-m2-20261025.md` — تقرير PR‑2b/M2 (لقطة مجمّدة).
- `RUN_GUIDE.md` §9 — قائمة الهجرات المعلّقة وأوامر التطبيق.
- `docs/TRIAL-DEPLOY-2026-10-18.md` §7 — ملحق النشر الحالي.
