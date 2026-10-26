# PR‑3 — تطبيق `Contract_Pg` حيًا على Supabase ونشر الكود على Render — 2026‑10‑26

> **النوع:** سجل تطبيق ونشر حي — **لقطة مؤرخة**، لا تُحرَّر بعد اليوم؛ أي تصحيح لاحق يكون بملحق مؤرخ جديد.
> **الفرع:** `feature/head-alerts-pr3-contract` → `master` (PR #52)
> **الدمج:** `0acdfe3` — `2026‑10‑26T19:56:08Z` (squash، وحُذف الفرع البعيد)
> **البيئة:** Render `docgen-1-tt3j` + Supabase `qkscixevhitvmxpwtxke` (PostgreSQL 17.11)
> **المرجع:** `docs/audit/11-head-alerts-pr3-20261026.md` (تقرير الـPR) ·
> `docs/audit/12-head-alerts-pr3-g2-rehearsal-20261026.md` (تمرين G2 المحلي) · `RUN_GUIDE.md` §9.

---

## 0) الخلاصة

| البند | النتيجة |
|---|---|
| `HeadAlertTaxonomy_Contract_Pg` على Supabase | ✅ مطبَّقة في `2026‑10‑26` — `Done` في **22.1 ث** |
| إجمالي هجرات `PostgreSQL` | **63** (كانت 62) · **0** معلّقة فعليًا |
| التمرين الذرّي على الحي (`BEGIN … ROLLBACK`) | ✅ نجح بلا أثر |
| كود `master` على Render | ✅ `0acdfe3` — سطر الإقلاع `20:30:15Z` · `Live` عند `20:30:18Z` (**نشر يدوي**) |
| سجل الإقلاع | `Database provider active: postgres (auto-migrate at startup: False)` ⇒ حارس الهجرات مرّ |
| الفحص الدخاني (دخول ← إصدار ← قراءة ← `sent`) | ✅ كامل، مع اختبارين سلبيين (`400`/`403`) |
| هجرات `SQLite` الأربع | ⏳ **تبقى معلّقة** (سياق مستقل) |

**قرار النطاق:** المالك أقرّ أن هذه النسخة الحية **تجريبية بلا بيانات حقيقية**، فاعتُبرت بيئة `G2`
نفسها. سقطت بذلك اشتراطات حماية البيانات (نسخة احتياطية إلزامية + مشروع استعادة منفصل)، وبقيت
**فجوة معلنة** (§9): التحقق على **إنتاج حقيقي** لم يُنفَّذ بعد.

---

## 1) قياس ما قبل التطبيق (قراءة فقط)

| البند | القيمة |
|---|---|
| الإصدار | `PostgreSQL 17.11` · قاعدة `postgres` |
| الهجرات المطبَّقة | **62**، آخرها `20261021232825_HeadAlertTaxonomy_Backfill_Pg` |
| `HeadAlerts` | **33** صفًا |
| فحص الصفر | `0 / 0 / 0` (`Kind` فارغ · `LastContentChangedAt` فارغ · `Kind` أبيض) |
| `Kind` / `LastContentChangedAt` | `is_nullable = YES` (كما هو متوقع قبل M3) |
| `CK_HeadAlerts_Kind_NotEmpty` | غير موجود |

---

## 2) التمرين الذرّي على الحي (بدل G2 على نسخة مستعادة)

نُفِّذ **نفس ملف الهجرة المُراجَع** (`docs/audit/migrations/HEADALERT_PR3_postgres.sql`) داخل معاملة
تُلغى، مع `lock_timeout` صريح:

```sql
BEGIN;
SET LOCAL lock_timeout = '5s';
-- نفس جسم الهجرة: حارس DO $$ … $$ + SET NOT NULL × 2 + القيد + سجل الهجرة
ROLLBACK;
```

**النتيجة:** نجح بلا أخطاء، وبعد التراجع: السجل `62` · العمودان `nullable` · القيد غير موجود ·
`33` صفًا كما هي. ⇒ المخطط والبيانات والإصدار (`17.11`) تحمّلوا جسم الهجرة حرفيًا قبل التطبيق.

> هذا التمرين يحلّ محل `G2` على نسخة مستعادة **لهذه البيئة التجريبية فقط**؛ قيمته أنه يختبر
> `PostgreSQL 17.11` (CI يختبر على 15) وتسلسل النشر، لا حماية بيانات إنتاجية.

---

## 3) التطبيق الفعلي

```powershell
$env:POSTGRES_CONNECTION = "<session-pooler Supabase>"
dotnet ef database update 20261026152238_HeadAlertTaxonomy_Contract_Pg `
  --context DocGeneratorPostgresDbContext `
  --project src/DocGenerator.Infrastructure --startup-project src/DocGenerator.Api
# Applying migration '20261026152238_HeadAlertTaxonomy_Contract_Pg'.
# Done.  ELAPSED_SEC=22.1
```

> الترتيب الإلزامي المتبع: **هجرة ← تحقق ← نشر كود**. عكسه يُسقط الإقلاع عمدًا
> (`DatabaseInitializer.EnsureSchemaIsCurrentAsync` + `Database:AutoMigrate=false`).

---

## 4) التحقق بعد التطبيق

| الفحص | النتيجة |
|---|---|
| `__EFMigrationsHistory` | **63** — آخرها `20261026152238_HeadAlertTaxonomy_Contract_Pg` |
| `Kind` | `NOT NULL` · `column_default = NULL` (بلا Default دائم) |
| `LastContentChangedAt` | `NOT NULL` · `column_default = NULL` |
| `CK_HeadAlerts_Kind_NotEmpty` | `CHECK ((TRIM(BOTH FROM "Kind") <> ''::text))` |
| فهارس `HeadAlerts` | **16** (منها `PK_HeadAlerts` و`UX_HeadAlerts_DedupeKey_Active`) |
| فحص الصفر بعد التطبيق | `0 / 0 / 0` |
| تكرار `DedupeKey` نشط | `0` |
| إدراج `Kind = NULL` | ❌ `23502` (not-null violation) |
| إدراج `Kind = ' '` | ❌ `23514` (check violation) |
| إدراج `LastContentChangedAt = NULL` | ❌ `23502` |
| إدراج سليم داخل معاملة ثم `ROLLBACK` | ✅ ثم لا بقايا (`0` صفوف `g2-%`) |

---

## 5) نشر الكود على Render

| البند | النتيجة |
|---|---|
| الالتزام المنشور | `0acdfe3` (`feat(head-alerts): enforce taxonomy contract (PR-3) (#52)`) |
| CI على `master` | ✅ التشغيل `40391161821` — `completed success` |
| طريقة النشر | **يدوي** من اللوحة (`Manual Deploy` ← `Deploy latest commit`) |
| لحظة الإقلاع | `2026‑10‑26T20:30:15Z` — `Database provider active: postgres (auto-migrate at startup: False)` |
| `GET /healthz` | `200` |
| `GET /login` | `200` + `CSP-Report-Only` · `X-Frame-Options: DENY` · `X-Content-Type-Options: nosniff` · `Referrer-Policy: no-referrer` · `COOP/COEP` · `Permissions-Policy` |

### 5.1 لماذا لم يحدث النشر تلقائيًا

راقبتُ `/healthz` كل 15 ثانية لمدة **14 دقيقة** بعد الدمج (`22:57–23:11` محليًا) فبقي `200`
باستمرار — وهي مراقبة **لا تصلح للحكم** على حدوث النشر (السبب في §5.2). بعدها تبيّن أن النشر
التلقائي **لم يبدأ** أصلًا، فأُنجز النشر **يدويًا** من اللوحة بقرار المالك. لم أتحقق من إعداد
`Auto-Deploy` في اللوحة (لا وصول لي)؛ والمستودع **لا يحوي `webhook`** إلى `Render`، ولا مفتاح
`Render API`، فلا مسار آلي للنشر من هذه الجلسة.

### 5.2 إشارتان غير صالحتين لقياس «هل نُشر البناء؟» (درس مثبَّت)

| الإشارة | لماذا لا تصلح |
|---|---|
| `last-modified` لملفات الواجهة | الواجهة لم تتغيّر بايتًا في `PR‑3`، فطبقة `Docker` المخبّأة أعادت ملفات `dist` بزمن تعديل **2026‑10‑25** حتى بعد النشر الجديد. الاعتماد عليها أعطى **نتيجة سلبية خاطئة** في هذه الجلسة. |
| انخفاض `/healthz` | `Render` ينشر **بلا انقطاع**: النسخة القديمة تخدم حتى تجهز الجديدة، فلا تهبط الاستجابة أبدًا. |

⇒ الإشارة الصحيحة الوحيدة المتاحة خارجيًا: **سجل اللوحة** (بناء + `Live`) + **سجل إقلاع التطبيق**
(سطر مزوّد القاعدة) + نجاح الفحص الوظيفي.

### 5.3 تحذيرات غير حاجزة في سجل الإقلاع

| السطر | التقييم |
|---|---|
| `Cannot load library libgssapi_krb5.so.2` | معروف ومسبق توثيقه (`docs/audit/10-…`): صورة `aspnet:10.0` بلا حزمة Kerberos. الاتصال يعمل عبر `Supavisor` بلا تأثر — أثبته الفحص الوظيفي. |
| `Head alert outbox lock is unavailable; another worker may be active` | متوقع لحظة النشر بلا انقطاع (نسختان متداخلتان لدقيقة). لا أثر: `Outbox` بقي `6` صفوفًا. |
| `DataProtection` بمفتاح غير مشفّر في حاوية زائلة | سلوك معروف للخطة المجانية؛ يبطل التشفير عند كل إعادة نشر — لا يمسّ الجلسات (`JWT` بمفتاح بيئي). |

---

## 6) الفحص الدخاني (منفَّذ عبر الـAPI بعقود الواجهة نفسها)

> بيانات الدخول زوّدني بها المالك في ملف مؤقت **خارج المستودع**، وحُذف الملف بعد الاستخدام؛
> كلمة السر غير مذكورة هنا ولا في أي مخرج. الحسابان: رئيس قسم (`Id=4`) ومحامٍ مستلم (`Id=5`).

| # | الفحص | المتوقع | النتيجة |
|---|---|---|---|
| 1 | `GET /api/alerts` بلا جلسة | `401` | ✅ `401` |
| 2 | `POST /api/auth/login` ببيانات خاطئة | `401` | ✅ `401` + رسالة عربية (**يثبت قراءة `EF` على المخطط المشدّد**) |
| 3 | دخول رئيس القسم | `200` | ✅ `200` (Cookie + CSRF) |
| 4 | `POST /api/alerts` (هدف: محامٍ `Id=5`) | `201` | ✅ `201` — التنبيه **`#46`** · `recipientCount=1` · `unreadCount=1` |
| 5 | قائمة المحامي قبل القراءة | `isRead=false` | ✅ التنبيه `46` ظاهر وغير مقروء |
| 6 | `PATCH /api/alerts/46/read` | `204` | ✅ `204` |
| 7 | قائمة المحامي بعد القراءة | `isRead=true` | ✅ `true` |
| 8 | `GET /api/alerts/sent` (رئيس القسم) | يحتوي `46` | ✅ `unreadCount=0` · `recipientCount=1` |
| 9 | `GET /api/alerts/unread-count` (المحامي) | `count=1` | ✅ `1` — يعود إلى تنبيه **قديم غير مقروء (`#40`)**، لا إلى `46` (تحقق مباشر في القاعدة) |
| 10 | `POST /api/alerts` بنص فارغ/أبيض | `400` | ✅ `400` «نص التنبيه مطلوب» |
| 11 | `POST /api/alerts` بلا ترويسة `X-CSRF-Token` | `403` | ✅ `403` |

### 6.1 التحقق في القاعدة للتنبيه `#46`

| الحقل | القيمة |
|---|---|
| `Kind` | `manual_to_lawyer` (**لا `NULL` ولا فراغ** — القيد الجديد فعّال) |
| `LastContentChangedAt` | `2026‑10‑26 20:40:29` (غير فارغ) |
| `DedupeKey` | `NULL` (تنبيه يدوي — بالتصميم) |
| `CreatedById` / `TargetType` | `4` / `2` (محامٍ) |
| صف المستلم | `IsRead = true` · `ReadAt = 2026‑10‑26 20:40:36` |
| `HeadAlertOutboxMessages` | بقي **`6`** صفوفًا (التنبيه اليدوي **لا** يسجّل `Outbox` — بالتصميم) |

**أعداد القاعدة بعد الفحص:** `HeadAlerts = 34` · `HeadAlertRecipients = 42` ·
`HeadAlertOutboxMessages = 6` · `__EFMigrationsHistory = 63`.

---

## 7) الأثر المتبقي على البيانات

- التنبيه **`#46`** («فحص دخان PR‑3 (M3) — تنبيه يدوي لمحامٍ») يبقى في القاعدة؛ `AlertsController`
  لا يعرض نقطة أرشفة للتنبيهات اليدوية، فيُترك كسابقه `#35` (سجل PR‑2b) ويُوثَّق هنا بدل حذفه صامتًا.

---

## 8) حالة `SQLite` (خارج هذا النشر)

لا علاقة لهجرات `SQLite` بـ`Render` (الحي `PostgreSQL` فقط). تبقى **أربع هجرات معلّقة**:
`HeadAlertTaxonomy_Expand` ← `HeadAlertOutbox_Create` ← `HeadAlertTaxonomy_Backfill` ←
`HeadAlertTaxonomy_Contract` — تطبيقها يحتاج نافذة على قاعدة `SQLite` حقيقية، مع قيد
`sqlite_sequence` الموثّق (يبدأ التراجع من `Backfill` لا من `Contract`):
`docs/audit/12-head-alerts-pr3-g2-rehearsal-20261026.md` §6.

---

## 9) الفجوات المعلنة

1. **إنتاج حقيقي:** لم يُنفَّذ عليه فحص `0 | 0 | 0` ولا نسخة احتياطية ولا استعادة مجرَّبة قبل M3.
   نجاح هذه البيئة **لا يُلزم** بأن الإنتاج سيمرّ بلا فحص — عند توفّره تُكرَّر الخطوات 1–4 كاملة.
2. **النشر الآلي:** غير مفعّل/غير مؤكد على الخدمة؛ كل نشر حتى الآن يدوي. يُنصح بتفعيل
   `Auto-Deploy` على `master` في اللوحة، أو توفير `Render Deploy Hook`/مفتاح `API` للأتمتة.
3. **إصدار `PostgreSQL` في CI:** CI يختبر على `15` والحي `17.11`؛ التمرين الحي غطّى هذه الفجوة
   **لهذه الهجرة**، لكنها تبقى قائمة للهجرات المستقبلية.

---

## 10) المخرجات

| الملف/الأمر | الدور |
|---|---|
| `docs/audit/migrations/HEADALERT_PR3_postgres.sql` | جسم الهجرة المُراجَع (تمرين + تطبيق) |
| `docs/audit/migrations/HEADALERT_PR3_sqlite.sql` | دلتا M3 لـSQLite (لم تُطبَّق) |
| `backend/src/DocGenerator.Infrastructure/Persistence/MigrationsPostgres/20261026152238_HeadAlertTaxonomy_Contract_Pg.cs` | الهجرة المطبَّقة |
| `docs/audit/11-head-alerts-pr3-20261026.md` §6 | مراجعة PR‑3 والمخاطر |
| `docs/audit/12-head-alerts-pr3-g2-rehearsal-20261026.md` | تمرين G2 المحلي على SQLite + عيب `sqlite_sequence` |
| تعليق PR #52 (`issuecomment-6425907312`) | سجل التطبيق على الحي لحظة تنفيذه |
