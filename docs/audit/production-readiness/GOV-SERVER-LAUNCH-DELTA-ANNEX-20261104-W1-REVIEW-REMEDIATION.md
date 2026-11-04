# ملحق `W1` — معالجة المراجعة النقدية (H‑1/M‑1/M‑2) — 2026‑11‑04

> **الغرض:** استكمال ما كشفته المراجعة النقدية بعد دمج `PR #94` (`94cef80`):
> تصنيف `SQLite` الفضفاض للتفرد (`H‑1`)، حجب حارس مندوب الجهة لجامع `CSP` (`M‑1`)،
> والتحقق الشكلي من `CspReportUri` (`M‑2`)، مع إغلاق فجوات الاختبار.
>
> الملاحق السابقة مجنَّدة ولم تُحرَّر (سياسة `docs/audit`).
> **لا تغيير في عقود `DTO`، ولا هجرات `EF`.**

---

## 1. `H‑1` — تضييق تصنيف التفرد في `SQLite`

**العيب:** `IsUniqueViolation` كان يقبل `SqliteErrorCode == 19` (وهو
`SQLITE_CONSTRAINT` العام) أو `2067`. الرمز `19` يشمل أيضًا:

| الرمز الموسّع | المعنى |
|---|---|
| `787` | خرق مفتاح أجنبي |
| `1299` | `NOT NULL` |
| `275` | `CHECK` |
| `1555` | تكرار مفتاح أساسي (تفرد فعلي) |
| `2579` | تكرار `ROWID` (تفرد فعلي) |

بعد `W1‑7` كان أي منها يُرد `409` «القيمة مكررة» ويُسجَّل `Warning`، فيُخفي عطل بيانات
حقيقي ويضلل العميل.

**المعالجة:** `IsUniqueViolation` يعتمد الرموز الموسّعة فقط:
`2067` (UNIQUE) أو `1555` (PRIMARYKEY) أو `2579` (ROWID). غياب الرمز الموسّع
(`0`) يبقى **`500` — fail‑safe** بدل تصنيف خاطئ. فرع `PostgreSQL` (`23505`) لم يتغيّر.

**الأثر السلوكي المقصود:** خرق `FK`/`NOT NULL`/`CHECK` عاد إلى `500` (عطل خادم حقيقي)
بدل `409` مضللة. هذا يصحّح أيضًا سلوك حراس الخدمات القائمة التي تستخدم المصنّف نفسه.

**الاختبارات:** `DbExceptionClassifierTests` — 3 رموز تفرد موجبة، 3 رموز غير تفرد سالبة،
ورمز أساسي بلا امتداد سالب. و`GlobalExceptionHandlerTests` — تفرد `SQLite` حقيقي عبر
المصنّف الحقيقي ← `409`، و`FK` حقيقي ← `500`.

## 2. `M‑1` — حارس مندوب الجهة وجامع `CSP`

**العيب:** `EntityManagerPortalGuard` كان يسمح بمسارات البوابة/`auth`/`client-errors`/`meta`
فقط، فيحجب `/api/csp-report` عن أي طلب مصادَق بدور `entitymanager` (`403` قبل التفويض).

**المعالجة:** أُضيف `/api/csp-report` إلى قائمة المسارات المسموحة، مع تعليل أن النقطة
عامة/محدودة المعدل ولا تغيّر حالة.

**الاختبارات:** `EntityManagerPortalGuardTests` (مسار جديد)، و
`CspReportEndpointTests.AuthenticatedEntityManager_CanPostReport` (تكامل حقيقي بدور
مندوب جهة ← `204`).

## 3. `M‑2` — ربط `CspReportUri` بالجامع المدمج

**العيب:** التحقق كان شكليًا؛ قيمة مثل `/api/csp-reports` تمرّ عند الإقلاع ثم تُفقد
التقارير بصمت (`404`). وكان يُقبل أي مسار same‑origin، بما فيه مسارات حساسة.

**المعالجة:** `CspReportUri` صار **المسار المدمج `/api/csp-report` حرفيًا فقط** (أو فارغًا
للتعطيل)، بسقف طول `512` لمنع تضخيم `Reporting-Endpoints`. أي قيمة أخرى تمنع الإقلاع
برسالة تسمّي المفتاح والمثال. هذا يطابق مسار `CspReportsController` ومسار استثناء
`CSRF` الدقيق.

**الاختبارات:** `CspSetupTests` (قيم صالحة/فارغة + 17 قيمة مرفوضة + الطول)،
`SecurityHeadersMiddlewareTests` (المسار الوحيد المقبول)، و
`CspProductionIntegrationTests` (فشل إقلاع حقيقي لقيمة مطلقة وأخرى بخطأ إملائي،
ووصول `report-uri`/`report-to`/`Reporting-Endpoints` إلى مستند إنتاجي).

## 4. تحسينات هيكلية واختبارية

- `LogSanitizer.StripQueryAndFragment` صار سياسة مشتركة بين `ClientErrorsController`
  و`CspReportsController` (إزالة تكرار)، مع اختبارات `?`/`#`/كلاهما/الفارغ.
- `RateLimitingIntegrationTests.CspReportPolicy_BlocksFloodWith429` يثبت حد `60`/دقيقة
  لكل `IP` فعليًا.
- `CspReportEndpointTests` يثبت أن استثناء `CSRF` **لا يتوسّع** إلى مسارات أخرى
  (`/api/csp-reportXYZ`, `/api/csp-report/extra`, `/api/other` ← `403`).

## 5. التحقق المنفَّذ

```powershell
dotnet build backend/DocGenerator.sln --nologo -v q -warnaserror
dotnet test backend/tests/DocGenerator.Api.Tests --filter "Category!=Sequential"
dotnet test backend/tests/DocGenerator.Api.Tests --filter "Category=Sequential"
dotnet test backend/tests/DocGenerator.Application.Tests
dotnet ef migrations has-pending-model-changes --context DocGeneratorDbContext
dotnet ef migrations has-pending-model-changes --context DocGeneratorPostgresDbContext
cd frontend
npx oxlint src scripts
npx tsc -b
npx vitest run
npm run build
npm run guard
```

- البناء: صفر تحذيرات/أخطاء (`-warnaserror`).
- `Api.Tests`: **882** (غير متسلسلة) + **5** (متسلسلة) — صفر فشل.
- `Application.Tests`: **2192** — صفر فشل.
- `vitest`: **1700/1700** (`160` ملفًا). `csp-guard`: **88** ملفًا.
- `subhead-guards`: نظيف. `dotnet vulnerable`/`npm audit`: صفر ثغرات.
- EF: **صفر** تغييرات نموذج في السياقين ⇒ **صفر هجرات**.

## 6. أثر النشر

- **لا هجرات EF** ولا `database update`، وقائمة `RUN_GUIDE.md` §9 التراكمية لا تتغيّر.
- `Security:CspReportUri` يجب أن يكون **`/api/csp-report` حرفيًا** أو فارغًا؛ أي قيمة أخرى
  تمنع الإقلاع الآن.
- `RateLimiting:CspReportPerMinute` (افتراضي `60`) يحكم الجامع لكل `IP`.
- تصحيح `H‑1` يعني أن أعطال `FK`/`NOT NULL`/`CHECK` تعود `500` (لا `409`)، وهو التصنيف
  الصحيح؛ تفردات `UNIQUE`/`PRIMARYKEY`/`ROWID` تبقى `409`.
- `report-to` يتطلب `HTTPS`؛ على `HTTP` الداخلي يبقى `report-uri` هو المسار الفعلي.
