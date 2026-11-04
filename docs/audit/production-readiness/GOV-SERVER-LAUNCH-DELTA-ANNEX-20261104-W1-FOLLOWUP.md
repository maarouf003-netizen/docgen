# ملحق `W1-followup` — إغلاق `W1-7` وتشغيل جامع تقارير `CSP` — 2026‑11‑04

> **الغرض:** استكمال ما أُجّل في `GOV-SERVER-LAUNCH-DELTA-ANNEX-20261104-W1.md` §6:

- `W1-7`: تفرد غير ملتقط ← `409`.
- تحقق إقلاع صارم لـ`Security:CspReportUri` بدل التجاهل الصامت.
- جامع تقارير `CSP` داخلي + `report-to`/`Reporting-Endpoints`.

> الملحق السابق مجنَّد ولم يُحرَّر (سياسة `docs/audit`). **لا تغيير في عقود `DTO` ولا هجرات `EF`.**

---

## 1. `W1-7` — تفرد ← `409` (شبكة أمان عامة)

**الوضع السابق:** كل `DbUpdateException` غير ملتقط = `500`؛ الخدمات تغطي معظم المسارات
بـ`DocumentWriteGuard` وغيره، لكن أي مسار منسي يظهر كعطل خادم.

**المعالجة:** حُقن `IDbExceptionClassifier` في `GlobalExceptionHandler` (المصنّف القائم
الذي يميّز SQLite `19`/`2067` وPostgreSQL `23505` عبر سلسلة الأسباب). الفرع الجديد:

```csharp
var isUniqueViolation = exception is DbUpdateException dbFailure
    && _dbErrors.IsUniqueViolation(dbFailure);

DbUpdateConcurrencyException => StatusCodes.Status409Conflict,
DbUpdateException when isUniqueViolation => StatusCodes.Status409Conflict,
DbUpdateException => StatusCodes.Status500InternalServerError,
```

- الرسالة **عامة في كل البيئات** (حتى `Development`): «تعارض في البيانات — القيمة مكررة،
  حدّث الصفحة وحاول مجددًا»؛ اسم القيد/الجدول/المزود يبقى في السجل فقط.
- التسجيل `Warning` لا `Error` (سباق متوقع)، مع `TraceId`/`UserId` والتفاصيل الكاملة.
- **لا تغيير** في الرسائل الدقيقة التي ترميها الخدمات (`DocumentConflictException` تبقى كما هي)،
  ولا في `DbUpdateException` غير الفريد (يبقى `500`).
- وُحّد توثيق `IDbExceptionClassifier` على `409` بدل `400` المتقادم.

**الاختبارات:** `GlobalExceptionHandlerTests.UniqueViolation_MapsTo409WithGenericMessage`،
`NonUniqueDbUpdateException_Stays500`،
`ExceptionHandlerIntegrationTests.DbUpdateException_UniqueViolation_MapsTo409WithGenericMessage`
(تثبت عدم كشف `UNIQUE`/`SQLite` حتى في `Development`).

## 2. تحقق إقلاع `Security:CspReportUri`

**المشكلة السابقة:** القيمة الخاطئة (مطلق/`//host`/محارف تكسر الترويسة) كانت تُتجاهل بصمت،
فتظل التقارير بلا جامع دون أي إشارة.

**المعالجة:**

- `CspReportUriPolicy` — مصدر الحقيقة الوحيد: مسار جذري-نسبي يبدأ بـ`/` وليس `//`، بلا
  بياض/تحكم، وبلا `"` `\` `,` `;` (تكسر `report-uri` أو قيمة `Reporting-Endpoints` المقتبسة)،
  وبمحارف `ASCII` فقط، وبتحليل `UriKind.Relative`.
- `CspSetup.Validate(security)` يُستدعى **دائمًا** في `Program.cs` بعد `HstsSetup.Validate`،
  فالقيمة الخاطئة **تمنع الإقلاع** برسالة تسمّي `Security:CspReportUri` والمثال
  `/api/csp-report`، بدل التدهور الصامت. القيمة الفارغة/`null` = الجمع معطّل بلا خطأ.
- `SecurityHeadersMiddleware` يستهلك التطبيع نفسه (دفاع ثانٍ في اختبارات الوحدة).

**الاختبارات:** `CspSetupTests` (قيم صالحة/فارغة + 12 قيمة مرفوضة + `null` options)،
و`SecurityHeadersMiddlewareTests` توسّعت للمحارف المكسِرة وغير `ASCII`.

## 3. جامع تقارير `CSP` داخلي

نقطة جديدة `POST /api/csp-report` في `CspReportsController`:

| البند | السلوك |
|---|---|
| الصيغ | `application/csp-report` (القديمة) و`application/reports+json` (Reporting API) و`application/json` |
| المصادقة/CSRF | عامة `[AllowAnonymous]`، ومستثناة **بمسارها الدقيق** في `CsrfMiddleware` (المتصفح لا يملك جلسة/كوكي عند الإرسال) |
| الحماية من الإغراق | `RequestSizeLimit(16KB)` + `RateLimiting:CspReportPerMinute` (60/دقيقة لكل `IP`) + سقف 20 تقريرًا/طلب |
| التسجيل | حقول تشخيصية فقط (`effective/violated-directive`، `blocked-uri`، `document-uri`، `disposition`، `status-code`، `source-file`) بعد `LogSanitizer` وقصّ 500 وحذف الاستعلام/المقطع |
| ما لا يُسجَّل | الحمولة الخام و`script-sample` (قد يحمل كودًا/بيانات) |
| الرد | `204` للتقرير الصالح، `400` لـ`JSON` غير صالح، `415` لنوع محتوى غير مدعوم |

**الربط بالسياسة:** عند ضبط `Security:CspReportUri` يُلحق بالسياسة:

- `report-uri <path>` (احتياط المتصفحات الأقدم).
- `report-to csp` + ترويسة `Reporting-Endpoints: csp="<path>"` (الحديث).

**الاختبارات:** `CspReportEndpointTests` (صيغة قديمة بلا مصادقة/CSRF، دفعة Reporting API،
`JSON` تالف، نوع محتوى غير مدعوم)، و`SecurityHeadersMiddlewareTests` (وجود/غياب
`report-to` و`Reporting-Endpoints`).

## 4. التحقق المنفَّذ

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
- `Api.Tests`: **857** (غير متسلسلة) + **5** (متسلسلة) — صفر فشل.
- `Application.Tests`: **2185** — صفر فشل.
- `vitest`: **1700/1700** (`160` ملفًا).
- `oxlint`/`tsc`/`build`: نظيفة. `csp-guard`: **88** ملفًا.
- `subhead-guards`: نظيف. `dotnet vulnerable`/`npm audit`: صفر ثغرات.
- EF: **صفر** تغييرات نموذج في السياقين ⇒ **صفر هجرات**.

## 5. أثر النشر

- **لا هجرات EF** ولا `database update`، وقائمة `RUN_GUIDE.md` §9 التراكمية لا تتغيّر.
- **تغيير سلوكي مقصود:** `Security:CspReportUri` غير الصالح يمنع الإقلاع الآن (كان يُتجاهل
  بصمت). افحص متغيرات البيئة قبل النشر؛ القيمة الموصى بها `/api/csp-report`.
- **مفتاح جديد:** `RateLimiting:CspReportPerMinute` (افتراضي `60`).
- **عقد جديد:** تفرد غير ملتقط يرد `409` برسالة عربية عامة بدل `500`؛ الواجهة تعرض
  `message` كما في بقية تعارضات `409`. الأعطال غير الفريدة تبقى `500`.
