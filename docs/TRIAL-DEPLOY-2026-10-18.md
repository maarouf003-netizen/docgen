# النشرة التجريبية — Render + Supabase (2026-10-18)

**الحالة:** نشرة تجريبية عاملة — بيانات تجريبية فقط، وليست بديلًا عن النشرة الحكومية.
**المراجع:** `docs/audit/production-readiness/DEPLOY-CHECKLIST-final.md`، `docs/audit/production-readiness/FIX-PLAN-final.md`، `RUN_GUIDE.md` §9.

## 1. الطوبولوجيا

| المكوّن | القيمة |
|---|---|
| التطبيق | `https://docgen-1-tt3j.onrender.com` — Render، Docker، Frankfurt، Free |
| فحص الصحة | `GET /healthz` ← `200 Healthy` |
| قاعدة البيانات | Supabase Postgres — project ref: `qkscixevhitvmxpwtxke` — Frankfurt (`eu-central-1`) |
| الاتصال | Pooler — `aws-1-eu-central-1.pooler.supabase.com:5432` — **Session mode** (لا Transaction `6543` ولا Direct — الأخير IPv6 فقط) |
| الهجرات | 59/59 مطبّقة، آخرها `20261014181050_AddInclusionNotesPg` |

> **تحديث 2026‑10‑25:** بند «الهجرات» أعلاه أصبح **62/62** بعد نشر PR‑2b — انظر الملحق §7.

## 2. متغيرات بيئة Render (أسماء فقط)

| المتغير | القيمة/الغرض |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `Database__UsePostgres` | `true` |
| `DATABASE_URL` | URI لـ Session pooler + `?sslmode=require` |
| `Jwt__Secret` | 64 حرفًا hex — سر توقيع الجلسات |
| `Bootstrap__AdminPassword` | كلمة مدير أول — تُستهلك فقط عند قاعدة بلا مستخدمين |

**سياسة الأسرار:** لا تُخزَّن أي قيمة سرية في المستودع أو في هذه الوثيقة. القيم في Render ← `Environment` فقط، وقيم قاعدة Supabase تُدار من لوحة Supabase (SQL Editor / Reset).

## 3. أوامر مرجعية (من مجلد `backend`)

### تطبيق الهجرات على Supabase يدويًا

```powershell
$env:POSTGRES_CONNECTION="Host=aws-1-eu-central-1.pooler.supabase.com;Port=5432;Database=postgres;Username=postgres.qkscixevhitvmxpwtxke;Password=<DB_PASSWORD>;SSL Mode=Require;Timeout=120;Command Timeout=300"
dotnet ef database update --context DocGeneratorPostgresDbContext --project src/DocGenerator.Infrastructure --startup-project src/DocGenerator.Api
```

- `Command Timeout=300` ضروري عمليًا عبر الـ pooler: المهلة الافتراضية `30s` سببت فشلًا عابرًا أثناء `LOCK TABLE` و`DROP INDEX` (أعيد التشغيل فواصل من حيث توقف).
- التحقق: `dotnet ef migrations list --context DocGeneratorPostgresDbContext --project src/DocGenerator.Infrastructure --startup-project src/DocGenerator.Api` — لا سطر `(Pending)`.

### استرجاع مدير النظام عند فقدان كلمة السر

1. Supabase ← `SQL Editor`:

```sql
DELETE FROM "Users";
```

2. Render ← `Manual Deploy` ← `Restart` — يعيد الإقلاع `DbSeeder.BootstrapAsync` إنشاء `admin` بكلمة `Bootstrap__AdminPassword`.
3. تغيير الكلمة بعد الدخول: تُكتب في `Notepad` أولًا (إنجليزية، حروف+أرقام، بلا مسافات)، ثم تُلصق في الحقلين.

> بديل لإعادة تعيين كلمة سر القاعدة نفسها (عند فشل المصادقة `28P01`): Supabase ← `SQL Editor` ← `ALTER USER postgres WITH PASSWORD '<NEW_PASSWORD>';` ثم تحديث `DATABASE_URL` في Render.

### ملاحظات تشخيص اتصال Supabase

- `tenant/user ... not found` = project ref/مضيف قديم (مشروع محذوف) — أعد أخذ سلسلة `Session pooler` من المشروع الحالي.
- `28P01 password authentication failed` = كلمة سر القاعدة لا تطابق — أعد التعيين (راجع أعلاه).
- `Timeout during reading attempt` = مهلة الأوامر `30s` عبر الـ pooler — أضف `Command Timeout=300`.

## 4. سلوكيات تشغيلية مهمة

- التأسيس الإنتاجي ينشئ `admin` فقط بلا فروع — أنشئ الفرع من الواجهة بعد أول دخول.
- قفل الحساب: 5 محاولات فاشلة ← 15 دقيقة (تتضاعف ×2 حتى 120) — رمز `423` برسالة مميزة عن `401`.
- تحديد المعدل: 5 محاولات/5 دقائق لكل `IP:username` ← `429`.
- الدخول لا يقصّ كلمة المرور، والتخزين يقصّ المسافات الطرفية — تجنب المسافات عند الإدخال.
- الخطة المجانية: Render ينام بعد ~15 دقيقة خمول (~50s للإيقاظ)؛ Supabase يتوقف بعد ~7 أيام خمول؛ بلا نسخ احتياطي تلقائي؛ سقف ~500MB.
- `Strict-Transport-Security` معطّل عمدًا على Render (تدير TLS بنفسها) و`CSP` بوضع `Report-Only` حتى إصلاح `M-07` (خط `Cairo` الخارجي) — راجع `FIX-PLAN-final.md` السطر 40.

## 5. ما تم التحقق منه (2026-10-18)

- `dotnet ef database update` ← 59 هجرة مطبّقة، صفر معلّقة.
- `GET /healthz` ← `200 Healthy` (0.28s).
- `GET /login` ← `200` مع رؤوس أمن: `CSP-Report-Only`، `X-Frame-Options: DENY`، `X-Content-Type-Options: nosniff`، `Referrer-Policy: no-referrer`.
- `GET /` ← `200` (الواجهة تُقدَّم من نفس الخدمة).

## 6. المتبقي

- تأكيد الدخول بعد تغيير كلمة `admin` (إن استمر الفشل: مسار الاسترجاع في §3).
- إنشاء فرع + ملف تجريبي والتحقق من دورة (حفظ ← تعديل ← طباعة).
- قبل أي استخدام حقيقي: استيفاء `DEPLOY-CHECKLIST-final.md` بندًا بندًا، وإصلاح `M-07` قبل فرض `CSP`، وترقية البنية إلى خطة مدفوعة/خادم حكومي مع نسخ احتياطي.

## 7. ملحق 2026‑10‑25 — نشر PR‑2b وهجرات التنبيهات

> §1–§6 أعلاه **محفوظة كما هي بتاريخ 2026‑10‑18**. هذا الملحق يسجّل الوضع بعد النشر اللاحق.

| البند | الحالة في 2026‑10‑25 |
|---|---|
| الهجرات على Supabase | **62/62** · آخرها `20261021232825_HeadAlertTaxonomy_Backfill_Pg` · **0** معلّقة فعليًا (تطابق الاسم `AddPostgresPendingSchema` ليس معلّقة) |
| الكود المنشور | `ec278c0` (دمج PR #50) — Render `docgen-1-tt3j` · `auto-migrate at startup: False` |
| `HeadAlertOutboxMessages` | موجود · أول دورة كاملة `pending → processed` في `2026‑10‑25T18:38:48Z` · `Attempts = 1` |
| `HeadAlerts` / `HeadAlertRecipients` | 28 / 28 بعد الفحص الدخاني (كانا 26/26) |
| `HeadAlerts:LegacyBridge:Enabled` | `false` (لم يُمسّ) |

- **التفاصيل والأدلة الكاملة:** `docs/audit/10-head-alerts-pr2b-deploy-20261025.md`.
- ⚠️ **ما زال معلّقًا:** ثلاث هجرات `SQLite` (`HeadAlertTaxonomy_Expand` ← `HeadAlertOutbox_Create`
  ← `HeadAlertTaxonomy_Backfill`) تُطبَّق قبل تشغيل الكود الحالي على سياق `SQLite`،
  و`HeadAlertTaxonomy_Contract(_Pg)` في PR‑3.
- 🔐 **أمني:** رابط قاعدة البيانات وُضع في ملف مؤقت خارج المستودع وحُذف بعد التحقق؛
  يُنصح بتدوير كلمة مرور القاعدة.
