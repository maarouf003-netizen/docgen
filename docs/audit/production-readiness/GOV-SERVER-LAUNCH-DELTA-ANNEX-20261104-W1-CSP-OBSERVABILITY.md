# ملحق `W1` — رصد `CSP` وتصلّب الجامع — 2026‑11‑04

> **الغرض:** إغلاق التحسينات الثلاثة المتبقية من مراجعة `W1`:
> تجميع سجلات `CSP` في سطر واحد لكل طلب، اختبار `413` والتقاط السجل، وترويسة
> `Report-To` القديمة. الملاحق السابقة مجنَّدة ولم تُحرَّر.
> **لا تغيير في عقود `DTO`، ولا هجرات `EF`.**

---

## 1. تجميع سجلات `CSP` — سطر واحد لكل طلب

**قبل:** كان كل تقرير يُسجَّل `Warning` منفصلًا (حتى 20 سطرًا لكل طلب)، فتتضخم السجلات
عند صفحة واحدة فيها عشرات المخالفات.

**بعد:** `CspReportsController.LogSummary` يكتب **سطرًا واحدًا** يحتوي:

| الحقل | المعنى |
|---|---|
| `Count` | عدد تقارير `CSP` المعالَجة (بسقف 20) |
| `Truncated` | هل تجاوزت الدفعة السقف |
| `Directives` | أول 5 توجيهات مختلفة (`effective/violated-directive`) |
| `FirstBlocked` | أول `blocked-uri` |
| `FirstDocument` | أول `document-uri` |
| `FirstDisposition` | أول `disposition` |
| `FirstStatus` | أول `status-code` |
| `FirstSource` | أول `source-file` |

- الحقول تمرّ عبر `LogSanitizer` (تسطيح/تجريد/قصّ) و`StripQueryAndFragment`، فلا
  يُسجَّل `?token=...` ولا `#...`.
- لا تُسجَّل الحمولة الخام ولا `script-sample` ولا بقية العيّنات؛ السطر الملخص للرصد،
  والتفاصيل الكاملة تبقى في المتصفح للمطوّر.
- التقارير من نوع `deprecation`/`intervention` لا تُسجَّل ولا تُحتسب.

**الاختبارات (`CspReportsControllerTests`):** تقريران في طلب واحد ← سطر واحد يحمل
`Count=2` و`Directives=script-src, img-src`؛ صيغة `report-uri` القديمة ← سطر واحد؛
25 تقريرًا ← `Count=20` و`Truncated=True` وسطر واحد؛ تقرير غير `CSP` ← لا سجل؛
ولا يظهر `token=secret` أو `tracking=1` في أي سطر.

## 2. اختبار `413` على `Kestrel` حقيقي

`TestServer` لا يفرض `RequestSizeLimit` (أعاد `204` لحمولة 20KB في القياس)، فلا يصلح
لإثبات السلوك. أُضيف `CspReportPayloadLimitTests`:

- يبني `WebApplication` مصغّرًا بـ`Kestrel` على `IPAddress.Loopback:0` (منفذ ديناميكي).
- يسجّل `CspReportsController` عبر `AddControllers().AddApplicationPart(...)`.
- يرسل تقريرًا صالحًا ← `204`، ثم حمولة `20KB` ← `413 RequestEntityTooLarge`.

هذا يثبت أن `RequestSizeLimit(16KB)` يُنفَّذ فعليًا في الخادم الحقيقي، لا في الاختبار
الوهمي فقط.

## 3. ترويسة `Report-To` القديمة

عند ضبط `Security:CspReportUri` يُضاف الآن، بجوار `Reporting-Endpoints` الحديثة:

```http
Report-To: {"group":"csp","max_age":31536000,"endpoints":[{"url":"/api/csp-report"}]}
```

- `report-to csp` في سياسة `CSP` يجد التعريف في `Reporting-Endpoints` (المتصفحات الحديثة)
  أو `Report-To` (المتصفحات الأقدم).
- `report-uri` يبقى احتياطًا ثالثًا.
- القيمة مبنية من المسار المدمج الثابت، فلا حقن ولا عنوان خارجي.

**الاختبارات:** `SecurityHeadersMiddlewareTests` (وجود/غياب `Report-To`)، و
`CspProductionIntegrationTests` (الترويسة على مستند إنتاجي).

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
- `Api.Tests`: **888** (غير متسلسلة) + **5** (متسلسلة) — صفر فشل.
- `Application.Tests`: **2195** — صفر فشل.
- `vitest`: **1712/1712** (`160` ملفًا). `csp-guard`: **88** ملفًا.
- `subhead-guards`: نظيف. `dotnet vulnerable`/`npm audit`: صفر ثغرات.
- EF: **صفر** تغييرات نموذج في السياقين ⇒ **صفر هجرات**.

## 5. أثر النشر

- **لا هجرات EF** ولا `database update`، وقائمة `RUN_GUIDE.md` §9 التراكمية لا تتغيّر.
- لا مفتاح إعداد جديد؛ `Security:CspReportUri` يبقى `/api/csp-report` حرفيًا أو فارغًا.
- السجل الآن أقل ضجيجًا: سطر واحد لكل طلب بدل حتى 20.
- `Report-To` القديمة تحسّن دعم المتصفحات الأقدم؛ لا تغيّر سلوك المتصفحات الحديثة.
