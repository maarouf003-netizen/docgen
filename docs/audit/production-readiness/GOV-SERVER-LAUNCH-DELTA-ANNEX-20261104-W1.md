# ملحق `W1` — تشديد الحدود الأمنية المتبقية — 2026‑11‑04

> **الغرض:** تنفيذ البنود الخمسة غير المحجوبة من مراجعة دفعة `W0-CSP-followup`
> (`GOV-SERVER-LAUNCH-DELTA-ANNEX-20261104-W0CSP-FOLLOWUP.md` §10).
> الملاحق السابقة مجنَّدة ولم تُحرَّر (سياسة `docs/audit`).
>
> **النطاق:** كود + اختبارات + توثيق. **لا تغيير في عقود `DTO`، ولا هجرات `EF`.**
>
> **الفرع:** `feature/security-w1` مبني على `master` عند `6ea50a9`.

---

## 1. تقييد `CspReportUri` بمسار جذري-نسبي

**الخطر السابق:** `SecurityHeadersMiddleware.BuildCspValue` كان يقبل أي قيمة تحللها
`Uri.TryCreate(..., RelativeOrAbsolute)` — بما فيها `https://third-party.example/report`
و`//third-party.example/report`. فمن يضبط المفتاح (أو يلوّث الإعداد) يوجّه تقارير `CSP`،
وفيها مسارات الصفحات وأحداث المخالفات، إلى خادم خارجي.

**المعالجة:** يُلحَق `report-uri` فقط إذا كانت القيمة:

1. بلا بياض/محارف تحكم (كما كان — منع حقن الترويسة).
2. تبدأ بـ`/` وليست `//` (رفض المطلق والبروتوكول-النسبي).
3. تحلّها `Uri.TryCreate(..., UriKind.Relative)`.

**السلوك الجديد:** قيمة مثل `https://collector.example` أو `//collector.example` تُتجاهل
**بصمت** (لا `report-uri`)، كما كانت تُتجاهل القيم الملوثة سابقًا. تحديث تشغيلي في
`RUN_GUIDE.md` §9.

**الاختبارات:** `SecurityHeadersMiddlewareTests.ReportUri_Ignored_WhenUnsafe` توسّعت
بحالات المطلق والبروتوكول-النسبي وغياب `/`، و`ReportUri_Appended_WhenRootRelative`
تثبت قبول `/csp-report` و`/api/csp-report?source=csp` و`/`.

## 2. توحيد ردود `404` الخمسة في عمليات حالة المستند

**الخطر السابق:** المسارات `status` و`revert-status` و`return-referred-to-start` و
`consider-executed-by-delegation` و`executed-status` تقرأ المستند أولًا (فتنجح)، ثم إن
رفضت خدمة الحالات الانتقال (سباق حذف/تغيّر حالة) ترد `NotFound()` **بلا جسم** — فلا
تستطيع الواجهة عرض سبب.

**المعالجة:** ثابت `DocumentNotFoundMessage = "المستند غير موجود"` ودالة
`DocumentNotFound()` تُرجع `NotFound(new { message })`، واستُبدلت المواضع الخمسة.
رمز الحالة لم يتغيّر (`404`)؛ أُضيف الجسم العربي فقط، وهو عقد `{ message }` القائم في
بقية المتحكمات (`AlertsController` وغيره).

**الاختبار:** `DocumentsStatusNotFoundContractTests` يستبدل `IDocumentStatusService`
بخدمة ترفض كل انتقال، ويستدعي المسارات الخمسة على مستند حقيقي، ويثبت `404` + جسم
`{"message":"المستند غير موجود"}` لكل منها.

## 3. تطهير مسار الاستثناء في `Development`

**الخطر السابق:** فرع `_ when _environment.IsDevelopment() => exception.Message` كان
يعكس الرسالة **خامًا** (أسطر/بريد/اعتمادات/طول غير محدود)، و`DescribeDbUpdateException`
يعيد أعمق رسالة داخلية خامًا. تشغيل `Development` على خادم منشور — ولو بالخطأ — كان
يجعل هذه القيم تصل للمتصفح.

**المعالجة:** المساران يمرّان الآن بـ`LogSanitizer.SanitizeForResponse`
(جرد ذيول الإطار ← تسطيح ← تجريد الأسرار/`Bearer` ← قصّ `1000`)، مع الإبقاء على
الفائدة التشخيصية: رسالة `SQLite`/القيد تبقى ظاهرة في `Development` فقط، و`Production`
يبقى على الرسائل العربية العامة. لا تزال التفاصيل الكاملة (بلا قصّ) في السجل.

**الاختبارات:** `GenericException_Development_IsSanitizedLike4xx`،
`DbUpdateException_Development_SanitizesDeepestMessage` (وحدة وتكامل)، مع بقاء
اختبارات الرسائل العربية النظيفة والرسالة الأعمق خضراء.

## 4. تشديد حارس مصدر رسائل الخطأ

**الحارس السابق:** `ApiErrorMessagesTests` كان يفحص `Controllers` فقط بنمط ضيق
`message = x.Message`، فتفلت `detail = ex.Message` والاستيفاء النصي و`Middleware`
(حيث يعيش معالج الاستثناءات نفسه).

**المعالجة:** الحارس صار `ApiSources_DoNotReflectUnsanitizedExceptionMessages`:

- يفحص `Controllers` و`Middleware` معًا (كل `.cs` متداخلًا).
- يكتشف `.Message` على مستقبِل يدل على استثناء (`e`/`ex`/`error`/`...Exception`/
  `...Error`/لاحقة `Ex` الشائعة) في أي سياق (إسناد، `detail`, استيفاء نصي).
- يستثني `request.Message`/`report.Message`/`suggestion.Message` (خصائص `DTO` مشروعة)،
  وتعليقات التوثيق.
- يقبل فقط ما يقع **على السطر نفسه** داخل `LogSanitizer.SanitizeForResponse(...)`؛
  تمرير الرسالة الخام عبر وسيط ثم تطهيرها لا يمر.

**الاختبارات:** `RawExceptionMessageDetector_ClassifiesSourceLines` (13 حالة) تثبت
التقاط الأنماط الخاطئة وعدم الالتقاط الخاطئ للأنماط المشروعة.

## 5. تحويل `OperationCanceledException` (انقطاع العميل) إلى `499`

**الخطر السابق:** `OperationCanceledException` كان يسقط في الفرع العام `500` ويُسجَّل
`Error` بمكدس كامل — انقطاع عميل متوقع يظهر كعطل خادم في السجلات ولوحات المراقبة.

**المعالجة:**

- يُعيَّن `499` **فقط** عند `httpContext.RequestAborted.IsCancellationRequested`
  (انقطاع عميل حقيقي)؛ `OperationCanceledException` من مهلة داخلية يبقى `500` كي لا
  يُخفى عطل.
- يُسجَّل `Information` بلا استثناء (لا `Error`).
- لا يُكتب جسم: القناة مغلقة، والكتابة عليها بـ`RequestAborted` قد ترمي استثناءً ثانيًا
  أثناء معالجة الأول.

**الاختبارات:** `ClientDisconnect_MapsTo499WithoutWritingBody`،
`ClientDisconnect_Returns499WithoutBody`، و`ServerSideCancellation_WithoutClientAbort_Stays500`.

## 6. `W1-7` — تفرد → `409` (لم يُنفَّذ)

ما زال معلّقًا على اعتماد المالك لأنه يغيّر عقد `500 → 409` المرصود. لم يُمسّ أي مسار
تفرد في هذه الدفعة.

---

## 7. التحقق المنفَّذ

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
- `Api.Tests`: **828** (غير متسلسلة) + **5** (متسلسلة) — صفر فشل.
- `Application.Tests`: **2185** — صفر فشل.
- `vitest`: **1700/1700** (`160` ملفًا) — صفر فشل.
- `oxlint`/`tsc`/`build`: نظيفة. `csp-guard`: **88** ملفًا متوافقًا.
- EF: **صفر** تغييرات نموذج في السياقين ⇒ **صفر هجرات**.

## 8. أثر النشر

- **لا هجرات EF**، ولا حاجة لـ`database update`، وقائمة `RUN_GUIDE.md` §9 التراكمية
  لا تتغيّر.
- **تغيير سلوكي وحيد في الإعداد:** `Security:CspReportUri` لم يعد يقبل عنوانًا مطلقًا
  أو `//host`؛ مَن كان يضبط عنوانًا خارجيًا (ولم يكن هناك جامع مركزي أصلًا) يجب أن
  ينقله إلى مسار على الأصل نفسه مثل `/api/csp-report`.
- `499` رمز غير قياسي لكنه متعارف عليه (nginx) وخاص بالسجلات/المراقبة؛ لا تعتمد عليه
  الواجهة (العميل منقطع أصلًا).
- ردود `404` الخمسة أضافت جسمًا عربيًا؛ رمز الحالة والعقد `{ message }` لم يتغيّرا.
