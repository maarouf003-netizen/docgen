# ADR-0008 — عقد خطأ HTTP موحّد (`ApiProblem`) بدل أجسام متفرقة (`P4-03`)

- **الحالة:** منفّذ — `P4-03` (تكملة)، 2026-11-17.
- **التاريخ:** 2026-11-17.
- **المرجع:** `docs/PERFECTION_ROADMAP.md` §`P4-03`، `docs/audit/43-p4-03-error-semantics-20261116.md`،
  `docs/audit/49-p4-03-error-contract-20261117.md`، `frontend/src/api/client.ts`.

## 1. السياق

قبل هذا القرار كانت أجسام الأخطاء تُبنى في **خمسة أماكن** بأشكال مختلفة:

1. متحكمات: `BadRequest(new { message })`، `NotFound(new { message })`،
   `Conflict(new { message })`، وبعضها `new { message, code }` (تحذير رقم الأساس).
2. `GlobalExceptionHandler`: `new { message, code, warningToken }`.
3. وسائط/مرشحات: `CsrfMiddleware`، `EntityManagerPortalGuard`، `RateLimitingSetup`،
   `SingleExportPerUserAttribute` — `new { message }` خام على الاستجابة.
4. ربط النموذج التلقائي (`[ApiController]`): `ValidationProblemDetails` إطاري بعنوان
   إنجليزي (`title`) وحقل `errors` بأسماء خصائص C#.
5. ردود فارغة عمدًا: `Forbid()` (403) و`NotFound()` (404) بلا جسم.

النتيجة: لا يستطيع العميل معرفة شكل موحّد، ولا يوجد رمز خطأ ثابت (`code`) يميّز
«تحقق» عن «تعارض رقم» عن «انشغال تصدير»، ولا حقل (`field`) لربط الخطأ بنموذج.
`P4-03` (مسح #185) وحّد **الدلالة** (`400/403/404`) لكن ليس **الشكل**.

## 2. القرار

1. **وثيقة واحدة على السلك** هي `ApiProblem` (متوافقة مع RFC 9457 في الحقول الأساسية):
   `type` (دائمًا `about:blank`)، `status`، `code`، `message`، و`traceId`؛
   واختياريًا `field`، `errors`، `warningToken`. تُكتب بنوع الوسائط
   `application/problem+json`، **ولا تُرسل مفاتيح فارغة** (`title`/`detail`/`instance`
   لا تظهر أصلًا — قرار المالك 2026-11-03 بجسم عربي بلا عنوان إنجليزي).
2. **`message` هو مصدر العرض الوحيد** عربيًا ومعقّمًا (`ApiErrorMessages`). `code` للقرار
   البرمجي، و`field`/`errors` لربط الحقول. الرسائل النصية غير العربية القادمة من ربط
   النموذج تُستبدل بالبديل العربي (`ApiErrorMessages.FromText`) ولا تصل للمستخدم.
3. **`code` ثابت ومشتق من الحالة افتراضيًا** (`ApiProblem.DefaultCodeFor`) مع تجاوزات
   دلالية: `number_reuse_warning`، `DocumentConflictException.Code`،
   `concurrency_conflict`، `unique_violation`، `branch_required`، `export_busy`…
   (القائمة في `ApiErrorCodes`). لا يُحلَّل النص العربي في العميل.
4. **كل منتج خطأ يمرّ عبر المصنع**: المتحكمات تستخدم `ApiProblem.Result`، والوسائط
   `ApiProblem.WriteAsync`، وربط النموذج `InvalidModelStateResponseFactory` واحدًا
   في `Program.cs`. **حارس مصدري** (`ErrorContractGuardTests`) يفشل عند أي
   `BadRequest/NotFound/Conflict/…(new {` أو `ValidationProblem(` جديد.
5. **التوافق الخلفي محفوظ:** `message` يبقى مفتاحًا أعلى المستوى (تقرأه `client.ts`)،
   و`code`/`warningToken` يبقيان حيث يعتمد عليهما العميل (`numberWarning.ts`).
   إضافة المفاتيح الجديدة غير كاسرة.
6. **أسماء الحقول camelCase:** مفاتيح `ModelState` (أسماء خصائص C#) تُطبَّع مقطعًا
   مقطعًا (`ApiProblem.NormalizeFieldName`) لتطابق حمولة JSON.

## 3. صراحة عن خارج النطاق

- **`Forbid()` (403) و`NotFound()` الفارغة تبقى بلا جسم** كما هي: عدد مواضع
  `Forbid()` كبير ومقصود (رفض استباقي بلا تسريب)، والواجهة تغطيه برسالة عربية ثابتة
  حسب الحالة. أي تحويل شامل لاحق يحتاج قرارًا مستقلًا.
- **`title` غائب عمدًا** رغم أن RFC 9457 يوصي به مع `about:blank` (عبارة حالة HTTP
  الإنجليزية): قرار المالك 2026-11-03 يمنع العنوان الإنجليزي، و`code` يؤدي وظيفة
  التمييز. استثناء موثّق لا سهو.
- **المدقق الموحّد (`IValidator<T>`/`FluentValidation`) مؤجل**: التحقق في هذا النظام
  **دلالة عمل** داخل الخدمات (مئات `ArgumentException` مرتبطة بقواعد المستند/الإنابة/
  الاستئناف) لا تحقق شكل طلب؛ نقله إلى مدقق موحّد تغيير بنيوي يخصّ `P4-01`
  (Command pipeline) لا عقد الخطأ. تسجيل ذلك هنا يمنع ادّعاء إنجاز `P4-03` كاملًا.
- **`AddProblemDetails()`** يبقى مسجّلًا (تستخدمه حالات إطارية نادرة)، لكن كل جسم
  خطأ يكتبه التطبيق لا يمرّ به.

## 4. العواقب

- **إيجابًا:** عقد واحد قابل للاختبار سلوكيًا ومصدريًا؛ رموز مستقرة؛ ربط حقول ممكن؛
  لا رسائل إنجليزية إطارية في أي 400؛ إضافة حالة HTTP جديدة لا تعني شكلًا جديدًا.
- **سلبًا/كلفة:** تغيير نوع الوسائط إلى `problem+json` لعملاء يفحصونها صراحةً (لا يوجد
  في الواجهة)، ومفاتيح إضافية في كل جسم خطأ، وحارس مصدري يجب تحديثه بوعي عند أي
  استثناء جديد.
- **المراقبة:** `traceId` ما زال يطابق `HttpContext.TraceIdentifier` وسجلات الخادم.
