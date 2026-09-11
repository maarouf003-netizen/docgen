# خطة سجل الأخطاء — `DocGenerator`

> **الحالة: اعتماد قبل التنفيذ — لا يُنفَّذ أي بند قبل موافقة صريحة.**
> التاريخ: 2026-09-11 · النطاق: خلفية `backend/` + واجهة `frontend/` · لا هجرات، لا عقود قائمة متغيرة.

## 1. الهدف

تحويل تسجيل الأخطاء الحالي (كونسول فقط، يضيع فورًا) إلى سجل دائم منظم:
`Serilog` منظّم (`Structured Logging`) بوجهتين — `Console` (JSON في الإنتاج) وملف دوّار يومي —
مع ترابط الطلبات (`TraceIdentifier` + هوية المستخدم) ونقطة إبلاغ موثقة لأخطاء الواجهة.

## 2. القرارات المعتمدة قياسيًا (OWASP Logging + ممارسات ASP.NET)

1. **الحزم**: `Serilog.AspNetCore 9.x` + `Serilog.Sinks.File` + `Serilog.Enrichers.Environment`
   (غير موجودة في كاش NuGet — التنفيذ يتطلب شبكة لـ `restore`، ثم تُثبَّت الإصدارات الدقيقة في `csproj`).
2. **الوجهات (حيادية للاستضافة المجهولة)**: `Console` بتنسيق JSON في الإنتاج ونص مقروء في التطوير،
   + ملف دوّار يومي `logs/logs-.txt` (احتفاظ 31 يومًا، سقف 50MB للملف). `Seq` مؤجّل لحين التوسع.
3. **نقطة `POST /api/client-errors` موثقة فقط** (`[Authorize]`): لا استثناء في `CsrfMiddleware`
   (المصادَق يحمل الكوكي فيمرّ تلقائيًا)، لا خنق IP، خنق لكل مستخدم عبر `IMemoryCache`
   (`30/دقيقة ← 429`). المجهول يُرفض `401` ويُبتلع الفشل بصمت في الواجهة.
4. **الترابط**: `HttpContext.TraceIdentifier` المدمج (لا وسيط جديد) + وسيط إثراء `LogContext` دقيق
   يضغط `UserId`/`Role` للمعتمد وإلا `anonymous`.
5. **المستويات من `appsettings`** (تطوير `Information`، إنتاج `Warning` فما فوق) — لا قيم مثبتة بالكود.
6. **المحظورات (إلزامية)**: كلمات المرور، أسرار `JWT`، محتوى المستندات القانونية، البيانات الشخصية
   الكاملة. الحقول المسموحة بسقوف: رسالة ≤2KB، مكدس ≤8KB، مسار بلا استعلام، `UserAgent` مقصوص.

## 3. الملفات

### خلفية — جديدة

- `backend/src/DocGenerator.Application/Common/Options/LoggingOptions.cs`
  (على نمط `RateLimitOptions`: مسار الملف، حد الاحتفاظ، سقف الحجم، حد الإبلاغ/الدقيقة).
- `backend/src/DocGenerator.Api/Middleware/RequestLoggingEnricherMiddleware.cs`
  (ضغط `UserId`/`Role` في `LogContext` لكل طلب).
- `backend/src/DocGenerator.Api/Controllers/ClientErrorsController.cs`
  (DTO بسقوف طول عبر `DataAnnotations`، خنق `IMemoryCache`، تسجيل `Warning` منظّم، رد `202 Accepted` بلا صدى).
- `backend/tests/DocGenerator.Api.Tests/ClientErrorsIntegrationTests.cs`
  (مجهول←`401`، صالح←`202`، ضخم←`400`، فيض←`429`، مندوب جهة←`202`).
- `backend/tests/DocGenerator.Api.Tests/EnricherMiddlewareTests.cs`
  (وجود `TraceIdentifier`/الهوية في السجل).

### خلفية — تعديل

- `backend/src/DocGenerator.Api/Program.cs`
  (`AddMemoryCache` + `Configure<LoggingOptions>` + `UseSerilog` ملف+كونسول + ترتيب الوسطاء + `CloseAndFlush`).
- `backend/src/DocGenerator.Api/Middleware/GlobalExceptionHandler.cs`
  (إضافة `TraceIdentifier` لقالب `LogError` فقط — لا سلوك).
- `backend/src/DocGenerator.Api/Middleware/EntityManagerPortalGuard.cs`
  (سطر سماح واحد لـ `/api/client-errors` مع تعليل: الدور القرائي يجب أن يُبلغ عن الأعطال).
- `backend/src/DocGenerator.Api/appsettings.json` (قسم `Logging:File` + مستويات حسب البيئة).
- `backend/src/DocGenerator.Api/.gitignore` (إضافة `logs/`).
- `backend/tests/DocGenerator.Api.Tests/ApiFactory.cs` (توجيه مسار ملف السجل لمجلد مؤقت معزول).

### واجهة — جديدة

- `frontend/src/utils/errorReporting.ts`
  (`fetch` + `keepalive` — لا مثيل `api` لتفادي اعتراض `401` — إرفاق CSRF عبر `getCsrfToken`،
  إزالة تكرار الجلسة بالبصمة، سقف عام، صمت تام، لا رمي أبدًا).
- `frontend/src/utils/errorReporting.test.ts`
  (السقوف، إزالة التكرار، الخنق، الفشل الصامت، إرفاق CSRF، لا رمي على رسالة فارغة).

### واجهة — تعديل

- `frontend/src/components/ErrorBoundary.tsx:18-20` (إبلاغ نارٍ-وانسَ في `componentDidCatch`).
- `frontend/src/api/client.ts:55-65` (إبلاغ أخطاء `5xx` مع حارس منع الحلقة — دون مساس بمنطق `401`).

## 4. التحقق ومعيار القبول

- `dotnet test` (المشروعان) · `npx vitest run` · `npx tsc -b` · `npx oxlint src` · `npm run build` — كلها خضراء.
- قبول يدوي: توليد `500` في التطوير ← سطر في `logs/`/stdout يحمل `TraceIdentifier` وهوية المستخدم.
- قبول يدوي: عطل عرض في الواجهة كمستخدم مصادق ← `202` وسطر `Warning` منظم بالخصائص نفسها.

## 5. النشر والتراجع

- النشر: مجلد `logs/` قابل للكتابة على المضيف؛ في الحاويات ثبّته `mount` أو اعتمد `stdout`.
  لا هجرات — لا يلزم `dotnet ef database update`.
- التراجع: عكس الملفات + إزالة الحزم — لا أثر على البيانات.
