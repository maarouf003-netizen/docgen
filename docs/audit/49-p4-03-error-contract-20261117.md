# `P4-03` (تكملة) — عقد خطأ HTTP موحّد (`ApiProblem`) — 2026-11-17

> PR: [#191](https://github.com/maarouf003-netizen/docgen/pull/191)
>
> نطاق هذه الجولة: **شكل** استجابات الخطأ لا دلالتها (الدلالة وُحّدت في
> `docs/audit/43-p4-03-error-semantics-20261116.md`). لا تغيير في أي `status code`،
> ولا هجرات، ولا تغيير في منطق التحقق أو الصلاحيات.
> القرار المعماري: [`docs/adr/0008-api-error-contract.md`](../adr/0008-api-error-contract.md).

## 1. المشكلة المقيسة قبل التغيير

أجسام الخطأ كانت تُبنى في خمسة أماكن بأشكال مختلفة:

| المنتج | الشكل قبل | مثال |
|---|---|---|
| متحكمات (22 موضعًا) | `{ message }` أو `{ message, code }` | `BadRequest(new { message = … })` |
| `GlobalExceptionHandler` | `{ message, code, warningToken }` | كل استثناء غير ملتقط |
| وسائط/مرشحات | `{ message }` خام على `HttpResponse` | CSRF 403، حارس البوابة، حد المعدل، انشغال التصدير |
| ربط النموذج (`[ApiController]`) | `ValidationProblemDetails` إطاري | `title` إنجليزي + `errors` بأسماء C# |
| ردود فارغة (مقصودة) | بلا جسم | `Forbid()` 403، `NotFound()` 404 |

النتيجة: لا `code` ثابت يميّز نوع الخطأ، ولا `field` لربط النموذج، ولا ضمان أن كل
`400` بالشكل نفسه — وهو نصّ `DoD` للبند.

## 2. العقد المنفّذ

`ApiProblem` (RFC 9457-compatible، بنوع وسائط `application/problem+json`):

```json
{
  "type": "about:blank",
  "status": 400,
  "code": "validation_failed",
  "message": "سنة غير صالحة",
  "field": "year",
  "errors": { "year": ["سنة غير صالحة"] },
  "traceId": "0HN…"
}
```

- **دائمًا:** `type` · `status` · `code` · `message`.
- **عند الحاجة فقط:** `field` · `errors` · `warningToken` · `traceId` (لا مفاتيح `null`،
  ولا `title`/`detail`/`instance` — قرار المالك 2026-11-03).
- **`code`:** مشتق من الحالة افتراضيًا (`ApiProblem.DefaultCodeFor`) مع تجاوزات دلالية
  (`number_reuse_warning`، `DocumentConflictException.Code`، `concurrency_conflict`،
  `unique_violation`، `branch_required`، `export_busy`، `generation_disabled`…).
- **`message`:** عربي معقّم عبر `ApiErrorMessages`، وهو مصدر العرض الوحيد في
  `frontend/src/api/client.ts`. أي رسالة ربط نموذج بلا حرف عربي تُستبدل بالبديل
  (`ApiErrorMessages.FromText`) فلا تصل نصوص الإطار الإنجليزية للمستخدم.
- **`field`:** مسار camelCase مطبَّع من `ModelState` (`ApiProblem.NormalizeFieldName`).

## 3. التنفيذ (الملفات)

**جديد**

- `backend/src/DocGenerator.Api/Http/ApiProblem.cs`: الوثيقة + `Result` (MVC) +
  `WriteAsync` (وسائط) + `DefaultCodeFor` + تطبيع أسماء الحقول.
- `backend/src/DocGenerator.Api/Http/ApiErrorCodes.cs`: رموز مستقرة.
- `backend/tests/DocGenerator.Api.Tests/ApiProblemTests.cs`: شكل السلك والرموز والتطبيع
  والكتابة على الاستجابة.
- `backend/tests/DocGenerator.Api.Tests/ErrorContractGuardTests.cs`: **حارس مصدري** + 12
  حالة تحقق إيجابي/سلبي للمكشوف.
- `backend/tests/DocGenerator.Api.Tests/ErrorContractIntegrationTests.cs`: مسار كامل —
  فشل ربط نموذج، JSON غير صالح، 404 من متحكم.
- `docs/adr/0008-api-error-contract.md` + هذا التقرير.

**معدّل**

- `Program.cs`: `InvalidModelStateResponseFactory` موحّد (بدل `ValidationProblemDetails`).
- `GlobalExceptionHandler.cs`: يكتب `ApiProblem` ويضيف `code` لكل فرع.
- `CsrfMiddleware` · `EntityManagerPortalGuard` · `RateLimitingSetup` ·
  `SingleExportPerUserAttribute`: `ApiProblem.WriteAsync`/`Result`.
- 16 متحكمًا: كل `BadRequest/NotFound/Conflict/Unauthorized` و`StatusCode(…)` تحمل جسمًا
  صار عبر `ApiProblem.Result` (بحث آلي ثم مراجعة يدوية لكل استثناء).
- `ClientErrorsController`: حُذفت كتلة `ValidationProblem(ModelState)` الميتة (الفلتر
  التلقائي يسبقها)، فلم يبقَ إلا مصدر تحقق واحد.
- `frontend/src/api/client.ts`: نوع `ApiProblemBody` + `readApiProblemBody` +
  استخدامه في `getApiErrorMessage` و`getDownloadErrorMessage` (رسالة النزيل الـBlob)،
  و**رسالة الخادم صارت تسبق الرسائل الثابتة في 401/403 أيضًا** (كانتا تُقنَّعان برسالة
  الجلسة/الصلاحية؛ الردود الفارغة وحدها تسقط للثابت) — فيظهر سبب رفض CSRF أو رسالة
  الدخول الفعلية بدل «لا تملك صلاحية».
- 3 اختبارات وحدوية كانت تثبت **نوع النتيجة** الدقيق (`BadRequestObjectResult`): صارت
  تثبت `ObjectResult` + `StatusCode == 400` — النوع الدقيق تفصيل تنفيذي، والعقد هو
  الحالة والجسم.

## 4. التحقق

- `dotnet build … -warnaserror`: 0/0.
- `dotnet format … --verify-no-changes`: `exit 0`.
- `DocGenerator.Application.Tests`: **2486/2486** (بلا تغيير).
- `DocGenerator.Api.Tests` (`Category!=Sequential`): **1479/1479** (+42).
- `DocGenerator.Api.Tests` (`Category=Sequential`): **5/5**.
- `DocGenerator.Architecture.Tests`: **29/29**.
- الحلقة السريعة: `Application` 707، و`Api` **804** (+39).
- الواجهة: `oxlint` · `tsc -b` · `vitest` **1808/1808** · `build` · `guard`.
- الحراس الخمسة: نظيفة.

### تحقق سلبي موثّق (حارس العقد)

1. أُضيف سطر `return BadRequest(new { message = "x" });` فعلي (غير تعليق) إلى
   `MetaController.cs`.
2. `ErrorContractGuardTests.ApiSources_DoNotBuildAdHocErrorBodies` **فشل** وأشار إلى
   الملف.
3. أُعيد الملف بـ`git checkout` — الحارس عاد **أخضر 15/15**.

## 5. حدود هذه الجولة (لا تُخفى)

- **`Forbid()` (403) و`NotFound()` الفارغة** بقيت بلا جسم: `Forbid()` مئات المواضع
  ومقصودة (رفض استباقي)، والواجهة تغطي 403/404 برسائل ثابتة. تحويلها قرار مستقل.
- **`IValidator<T>`/`FluentValidation` لم يُنفّذ** — قرار موثّق في `ADR-0008` §3:
  التحقق في هذا النظام دلالة عمل داخل الخدمات (مئات `ArgumentException` لقواعد
  المستند/الإنابة/الاستئناف)، ونقله إلى مدقق طلب موحّد تغيير بنيوي يخصّ `P4-01`
  (Command/Query pipeline). تسجيله هنا يمنع اعتبار `P4-03` مكتملًا بنسبة 100%.
- **`code` على مسارات قديمة:** لم تُضف رموز خاصة لكل حالة عمل (فقط حيث يعالج العميل
  الرمز فعلًا)؛ الباقي يأخذ رمز الحالة الافتراضي. إضافة رمز لاحقًا غير كاسرة.
- **تغيير سلوك عرضي مقصود:** رسالة خادم 401/403 تظهر الآن بدل الرسالة الثابتة عند
  وجود جسم (كانت الرسالة الثابتة تسبق دائمًا). لا اختبار قائم يعتمد العكس (اختبارات
  403 الحالية تستخدم جسمًا فارغًا).
- **لا تغطية لـ`PostgreSQL` الحقيقي** لهذا العقد (يبقى ضمن `P1-01`).

## 6. الخطوة التالية

`P4-03b` (مدقق موحّد) بعد `P4-01`، أو البند التالي في الخطة (`P2-03`/`D11`).
