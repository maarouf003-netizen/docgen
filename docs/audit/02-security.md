# تقرير الأمن — PROMPT 2 (SECURITY)

> تدقيق قراءة فقط لنظام إدارة ملفات التنفيذ القضائي (قراءة، `grep`، تاريخ `git`).
> لم يُعدَّل أي كود أو إعداد أو اعتماديات أو هجرات أو بيانات. لم يُتَّصَل بأي قاعدة بيانات.
> لم تُشغَّل اختبارات (عُرض عزلها من الكود: `ApiFactory.cs:21-36` قاعدة `SQLite` مؤقتة لكل مصنع،
> و`CoreTests.cs:10-15` ذاكرة `:memory:`).
> المهاجم المفترض يستدعي الـ `API` مباشرة ولا يستعمل الواجهة: فحوصات الواجهة ليست حدًا أمنيًا.
> التقارير بالعربية؛ المعرفات والمسارات والمصطلحات التقنية بالإنجليزية.
> الثقة: `VERIFIED` (قُرئ الكود) / `INFERRED` (استدلال من كود مقروء) / `NOT VERIFIED` (تعذر الفحص — يُذكر أين بُحث).
> السياق البديل: `docs/00-PROJECT-CONTEXT.md:1-88` (ملف `docs/audit/00-PROJECT-CONTEXT.md` غير موجود — انظر `BQ-020`).

## الملخص التنفيذي

- **العد**: `CRITICAL` صفر — `HIGH` نتيجتان — `MEDIUM` سبع نتائج — `LOW` ثماني نتائج. المجموع 17 (`SEC-001` … `SEC-017`).
- **أهم 5 مخاطر بلغة plain**:
  1. مميز داخلي (أو مخترِق بجلسته) يستطيع العمل ثم محو أثره: سجل التدقيق قابل للكتابة تقنيًا
     (`SEC-008`)، ومسارات كتابة صامتة بلا تدقيق (توليد `SEC-001`، تصدير `SEC-002`، قراءة `SEC-003`،
     خروج `SEC-009`، تعليم مقروء `SEC-015`)، ورفض التفويض بلا رصد (`SEC-007`).
  2. رئيس قسم يرى تدقيق **كل الفروع** (تفاصيل مالية وقضائية) لغياب ترشيح النطاق (`SEC-013`).
  3. استخراج جماعي بلا أثر: تصدير حتى 10 آلاف صف + توليد مستندات رسمية + تصفح ملفات — كلها بلا صف تدقيق واحد.
  4. مسح ممنهج للمعرفات لا يُرصد: كل `403` (بما فيها حارس البوابة) صامت (`SEC-007`)، ومحاولات الحساب المقفل صامتة (`SEC-006`).
  5. جلسة 8 ساعات (`SEC-011`) + فحوص الثغرات غير حاجبة في `CI` (`SEC-010`): نافذة استغلال طويلة ورقابة لاحقة لا تمنع.
- **الخبر السار (VERIFIED)**: لا حقن `SQL` (خام معامَل واحد فقط)، لا `SSRF` (صفر جلب خارجي)،
  `JWT` بحارس طول ومثبت الخوارزمية، كلمات `PBKDF2-SHA256` بـ 600 ألف تكرار، `CSRF` مزدوج،
  `CORS` للتطوير فقط، لا رفع ملفات أصلًا، القوالب بقائمة سماح، `XSS` خلف تعقيم مزدوج
  (`HtmlSanitizer` خادميًا + `DOMPurify` عرضًا)، لا سر إنتاجي ملتزم في التاريخ، ولا حزم خلفية ثغراتية.

## 1. سطح الهجوم

| السطح | الحالة | الدليل |
|---|---|---|
| عام بلا مصادقة | 3 نقاط فقط: `POST api/auth/login` و`POST api/auth/logout` (`AuthController.cs:42-44,99-100`) و`GET api/meta/current-year` (`MetaController.cs:27-28`) | `grep AllowAnonymous` في `Controllers/` = 5 أسطر (3 سمات + تعليقان) — VERIFIED |
| مصادَق داخلي | 184 نقطة ناقص العامة؛ معظمها `[Authorize]` عارٍ + فحص برمجي (`RolePermissions.*`) | `01-endpoints.md` + `RolePermissions.cs:1-134` — VERIFIED |
| إداري | `users/*` و`branches/*` (مشرف فقط) + `audit-logs` (مدير/مشرف/رئيس) | `UserManagementController.cs:26-27`، `BranchesController.cs:25`، `AuditLogsController.cs:11` — VERIFIED |
| توليد/تصدير (مكلف) | `documents/export` + `documents/{id}/generate` + كل `StatisticsController` + `portal/stats/export` خلف `ExpensivePolicy` | `DocumentsController.cs:140,499`، `StatisticsController.cs:17,47,63,78,85,90,95,120,146,168`، `PortalController.cs:118,133` — VERIFIED |
| مصادقة/كلمات | `login` (حد `login-ip` + حد قاعدة) + `change-password` (حد `password`) | `AuthController.cs:44,111` — VERIFIED |
| استيراد/دمج سجل | `entity-registry/import-preview|import-commit|merge-*|unify*|abolish*` (مدير/مشرف؛ رئيس بقيد محافظة) | `EntityRegistryController.cs:193-604` + `RolePermissions.cs:107-122` — VERIFIED |
| مهام خلفية | لا أثر (`Hangfire/Quartz` صفر إصابة) | بُحث `Program.cs` والحزم — NOT VERIFIED وجود مخفي (يُنفى بالبحث فقط) |
| تكاملات خارجية | لا `SMS`/بريد/أنظمة حكومية في الكود | بُحث `backend/src` — NOT VERIFIED (انظر `BQ-018`) |
| `SignalR`/مراكز | لا أثر (`Hub` صفر إصابة إنتاجية) | بُحث `backend/src` — VERIFIED الغياب |
| صحة/تشخيص | لا `MapHealthChecks` (صفر إصابة في `Program.cs:310`) | VERIFIED الغياب |
| `Swagger` | مغلق افتراضيًا (`Swagger:Enabled=false` في `appsettings.json:49-51`)، يُفتح في التطوير أو براية صريحة (`Program.cs:97-98,286-290`) | VERIFIED |
| ملفات ثابتة | `wwwroot` + `MapFallbackToFile("index.html")` في غير التطوير فقط (`Program.cs:267-271,313-316`) | VERIFIED |
| تخزين مستندات | لا رفع (`IFormFile|Upload` صفر في `backend/src`)؛ لا تخزين مولّدات على الخادم (بث `File(bytes,...)`) | VERIFIED الغياب |
| توليد من قوالب | 8 رموز (`001,002,003,004,005,006,007,PS` في `appsettings.json:58-70`) من قرص `WordTemplates/` | VERIFIED |
| قاعدة البيانات | `SQLite` محليًا / `Postgres` مدارة عبر `DATABASE_URL` أو سلسلة مُحقونة (`Program.cs:43-50`) | VERIFIED |

## 2. المصادقة

- **الآلية**: `JWT` (`HmacSha256` فقط `Program.cs:166`) يُسلَّم في كوكي `HttpOnly` (`docgen_token`
  في `Auth/AuthCookie.cs:13`؛ `HttpOnly=true :19`، `Secure=!IsDevelopment :20`، `SameSite=Strict :21`)،
  ويُقبل أيضًا عبر ترويسة `Authorization` (تُحترم الترويسة أولًا `Program.cs:172-177`) — VERIFIED.
  التوكن لا يعود في جسم الدخول (`AuthController.cs:85-86` يعيد `{user}` فقط) — VERIFIED.
- **عمر الجلسة**: 480 دقيقة = 8 ساعات (`appsettings.json:56` + `JwtOptions.cs:8` + `TokenService.cs:47`)،
  و`ClockSkew=1min` (`Program.cs:164`) — VERIFIED. لا تجديد/تدوير (`refresh` صفر إصابة) — VERIFIED الغياب.
- **قوة المفتاح**: رفض إقلاعي ما لم تكن ≥32 بايت (`Program.cs:93-95`) — VERIFIED.
- **التجزئة**: `PBKDF2-SHA256` بـ 600 ألف تكرار وملح 16 بايت (`PasswordHasher.cs:16-29`)، والتحقق يقبل
  صيغًا انتقالية قديمة (`werkzeug pbkdf2:sha256` + `saltHex:hashHex` + `SHA256` بلا ملح) بترقية شفافة عند
  الدخول (`AuthService.cs:148-150`) — VERIFIED (الصيغ القديمة قابلة للكسر السريع حتى أول دخول — انظر T-8).
- **القفل**: 5 محاولات → قفل 15 دقيقة بتراجع أسّي حتى 120 (`appsettings.json:44-47` +
  `AuthService.cs:126-128`)؛ النجاح وحده يصفّر العداد (`:152`) — VERIFIED.
- **حد الدخول**: طبقتان — حد قاعدة لكل `IP:username` (5/5 دقائق `RateLimitOptions.cs:10-11` +
  `DbLoginRateLimiter.cs:30-51`) وحد `login-ip` (10/دقيقة `appsettings.json:38`) — VERIFIED.
  ملاحظة: `ExpensivePerMinute` الفعلي 60 (إعدادات) لا 10 (افتراضي الكود `RateLimitOptions.cs:23`) — VERIFIED.
- **أول مدير**: التطوير يبذر 4 حسابات معروفة (`DbSeeder.cs:37-40`)؛ الإنتاج يرفض الإقلاع بلا
  `Bootstrap__AdminPassword` وينشئ `admin` واحدًا فقط (`DbSeeder.cs:50-68` + `DatabaseInitializer.cs:28-31`
  + `Program.cs:273-280`) — VERIFIED.
- **إبطال الجلسات**: `TokenVersion` (`User.cs:19` + `claim token_version` في `TokenService.cs:40`) تُفحص عند كل
  تحقق (`Program.cs:189-196`) وتُزاد عند تغيير كلمة/دور/فرع/تعطيل (`AuthService.cs:181`،
  `UserManagementService.cs:123,137,167,259,271,278`) — VERIFIED النمط.
- **بلا**: `MFA` (صفر إصابة) ولا استرجاع كلمة (صفر مسار reset) ولا تعداد عبر رسائل الدخول
  (رسالة موحدة `AuthController.cs:96`) — VERIFIED.
- **ثغرات المصادقة**: تعداد الفروع قبل كلمة المرور (`BranchSelectionRequired` في `AuthController.cs:63-67`
  ← `AuthService.cs:52-76`)، ورفض `Head/Lawyer` بلا فرع قبل فحص الكلمة وبلا عدّ/قفل (`AuthService.cs:104-114`)،
  وإعادة المحاولة على مقفل صامتة (`:71-72,90-91`) — `SEC-006`.

## 3. التفويض (الأخطر)

- المصفوفة الكاملة: `docs/audit/02-authz-matrix.md` (كل خلية بدليل أو `UNKNOWN`).
- **النمط**: `[Authorize]` عارٍ على مستوى الصنف + فحص `RolePermissions.*` + `return Forbid()` داخل الأكشن.
  أي أكشن جديد يُنسى فحصه ينفتح افتراضيًا (فشل مفتوح) — VERIFIED هيكليًا من `01-endpoints.md` (184 نقطة).
- **«قراءة فقط» محترمة كتابةً**: المدير/المشرف بلا أي كتابة على الملفات (`IsReadOnlyOnDocuments :104-105`،
  وكل مسارات الكتابة محامٍ فقط `:12,15,18`) — VERIFIED.
- **عزل المندوب بنيوي**: `EntityManagerPortalGuard.cs:20-40` يحصره في
  `/api/portal|/api/auth/me|/api/auth/logout|/api/client-errors|/api/meta` — VERIFIED (دفاع عمق حقيقي).
- **الفروق عن السياق** (انظر `BQ-001` … `BQ-007`): المدير ممنوع من المحذوفات (`:24-25`)،
  واسم الكود `Admin` مقابل «مشرف»، وإدارة الحسابات للمشرف فقط لا المدير (`:46-47`).
- **الفجوات المشتبهة** (اختباراتها الدقيقة في «خطة الاختبار الأمني»): ملكية التعديل داخل خدمات
  الاستئناف/الإنابة (`SEC-016` NOT VERIFIED)، وقيد محافظة الرئيس في السجل (`SEC-017` NOT VERIFIED)،
  وتوسّع المتابِع (`CanAccessOrFollowAsync`)، ونافذة `branch_id` المخبوز بعد النقل.

## 4. أمن الـ `API`

- **التحقق**: `DocumentValidator.cs:10-103` نقي + `ParseDateTime` برسالة موحدة (`FreeDateParser.cs:14-24`)؛
  لا `DataAnnotations` في `DTOs` (التحقق يدوي → `ArgumentException` → `400` عبر
  `GlobalExceptionHandler.cs:33-68`؛ تفاصيل `DbUpdateException` للتطوير فقط `:60-63`) — VERIFIED.
- **إسناد جماعي**: `DocumentUpsertRequest` (`DocumentDtos.cs:310-479`) بلا `Role/BranchId/Counter/Hash`؛
  الهوية تُحقن خادميًا (`DocumentService.cs:203-204`)؛ `CreateUserRequest/UpdateUserRequest`
  (`UserManagementDtos.cs:39-52`) خلف `CanManageUsers` مع منع خفض الذات وإبطال جلسات (`:236-259,270-278`) — VERIFIED.
- **كشف مفرط**: كل الردود عبر `DocumentResponse.FromEntity` (لا `return Ok(entity)` بكيان `EF`)؛
  العدادات تُصفَّر لغير المصرح (`DocumentsController.cs:61-78`)؛ `Login` بلا توكن في الجسم — VERIFIED.
- **حقن `SQL`**: خام معامَل واحد فقط (`DbLoginRateLimiter.cs:29-33,59-62` بمعاملات `@p0..@p3` بلا تضمين نصي)؛
  لا `OrderBy` ديناميكي ولا `Linq.Dynamic` — VERIFIED (بُحث `backend/src`).
- **`SSRF`**: صفر جلب `URL` خارجي في `backend/src` — VERIFIED الغياب.
- **اجتياز المسار**: القوالب بقائمة سماح (`WordTemplateRenderer.cs:76-77` + `Path.Combine :80-81`)؛
  مدخل `template` لا يدخل المسار؛ اسم التنزيل يُعقَّم (`WordDocumentGenerator.cs:52-56`) — VERIFIED.
- **`JSON`**: `MaxDepth=32` (`Program.cs:38-39`)؛ لقطات الاستئناف `System.Text.Json` بلا تعددية أنواع
  (`AppealSnapshotSerializer.cs:16-49`) — VERIFIED.
- **`CORS`**: سياسة `Vite` (`localhost:5173`) للتطوير فقط (`Program.cs:145-147,292-295`)؛ الإنتاج أحادي الأصل — VERIFIED.
- **`CSRF`**: كل `POST/PUT/PATCH/DELETE` يتطلب `X-CSRF-Token == docgen_csrf` بمقارنة زمن ثابت
  (`CsrfMiddleware.cs:24-48`) عدا دخول/خروج (`:30-32`)؛ زرع الزوجين عند الدخول (`AuthController.cs:82-84`) — VERIFIED.
- **حد المعدل**: `GlobalLimiter` لكل `userId` (مصادَق) / `IP` (مجهول) + سياسات `login-ip/password/expensive`
  (`RateLimitingSetup.cs:25-52`)؛ `UseRateLimiter` بعد المصادقة عمدًا للتقسيم لكل مستخدم (`Program.cs:302`) — VERIFIED.
- **الحجم**: `Kestrel` + `Multipart` بسقف 10MB (`Program.cs:31-37`) — VERIFIED.
- **الترويسات**: `nosniff` + `DENY` + `no-referrer` + `Permissions-Policy` + `COOP/CORP` + `CSP`
  (`SecurityHeadersMiddleware.cs:37-45`)؛ إخفاء `Server` (`:33`)؛ `ForwardedHeaders` لوكلاء معروفين فقط
  (`Program.cs:209-210`) — VERIFIED. لكن `CSP` بوضع `Report-Only` افتراضيًا (`CspReportOnly=true`
  في `appsettings.json:30`) و`HSTS/HTTPS` معطّلان افتراضيًا (`:28-29` ← `Program.cs:238-262`) — انظر التسليم.
- **الأخطاء**: لا `DeveloperExceptionPage`؛ `Swagger` مقيد (`Program.cs:97-98,286-290`) — VERIFIED.
- **الترقيم**: `perPage` مقيد `1..100` في كل الخدمات؛ `Export.MaxRows=10000` يُفرض بالعدّ قبل الجلب
  (`DocumentService.Search.cs:137-140`) — VERIFIED (استثناء `PublicEntityService.cs:957` بسقف 5000 لسجل التغييرات).

## 5. الملفات والمستندات

- **لا رفع حاليًا**: `IFormFile|Upload|RequestForm` صفر في `backend/src` — VERIFIED (ما يجب قبل المسوحات: تسليم النشر).
- **توليد `Word`**: `GET .../generate?template&recipient&estateIds&heirId` (`DocumentsController.cs:498-499`
  `[Authorize]+ExpensivePolicy`) → فحص `CanAccess :511-513` → حظر عائلة `ExecutedLike :516-517` →
  `GenerateAsync :521` → `File(bytes, wordprocessingml, filename) :522` — VERIFIED.
  النص العادي يُهرَّب كنص `w:t` والخام `{{r key}}` يُقبل فقط إن بدأ `<w:` (`WordTemplateRenderer.cs:254-320`) — VERIFIED.
  **لكن**: `GET` يكتب (`PrintCount++; UpdatedAt` في `WordDocumentGenerator.cs:40-43`) بلا تدقيق — `SEC-001`.
- **تصدير `Excel`**: خلايا `InlineString` لا تُفسَّر صيغًا (`ExcelExportService.cs:273-285`) — محايد حاليًا
  بلا تطهير بادئة صريح (أي تحول لخلايا صيغ يعيد فتح حقن الصيغ) — VERIFIED البنية / INFERRED الأمان.
  التنزيل `attachment` ضمني + `nosniff` شامل — VERIFIED. التصدير الداخلي بلا تدقيق — `SEC-002`.

## 6. قاعدة البيانات

- لا `SQL` خام/ديناميكي عدا المحدد المعامَل أعلاه؛ الباقي `LINQ` — VERIFIED.
- سلسلة الاتصال: `DATABASE_URL > ConnectionStrings:DefaultConnection > docgen.db` (`Program.cs:43-50`)؛
  التطبيع يقتبس ويمرر `sslmode` (`PostgresConnectionString.cs:13,53-55`)؛ الإقلاع يطبع المحرك فقط (`:284`)؛
  وصف الفشل مقنَّع (`:78-89`) — VERIFIED. `TLS` للقاعدة **غير مفروض كوديًا** (يعتمد على سلسلة المنصة) — NOT VERIFIED.
- امتيازات مستخدم القاعدة: `render.yaml:1-5` قاعدة مُدارة بلا `GRANT` مخصص؛ لا فصل قارئ/كاتب تدقيق — VERIFIED.
- حماية التدقيق برمجية فقط (لا كتابة عبر `API` — `AuditLogsController` قراءة فقط) بلا منع على مستوى القاعدة — `SEC-008`.

## 7. الواجهة

- **`XSS`**: 6 إصابات `dangerouslySetInnerHTML` كلها خلف `sanitizeRichText` (`DOMPurify` بقائمة 12 وسمًا +
  `style` فقط + منع `script/iframe/object` + مرشح أنماط في `richText.ts:3-61`)؛ الخلفية تعقم مزدوجًا
  (`HtmlSanitizer` في `HtmlInputSanitizer.cs:17-43` عند الحفظ) — VERIFIED.
- **التوكن**: لا `localStorage` للتوكن (فقط مفتاح تحية + مواضع قوائم `sessionStorage` غير حساسة)؛
  الجلسة `HttpOnly` تُجلب عبر `GET /auth/me` (`AuthContext.tsx:10-17`) — VERIFIED.
- لا أسرار في الحزمة (صفر `VITE_|import.meta.env` في `frontend/src` عدا وكيل التطوير `vite.config.ts:11`)؛
  بلا خرائط مصدر إنتاجية (الافتراضي `false` ولا `--sourcemap` في `Dockerfile:7`)؛ لا إعادة توجيه بدخل مستخدم
  (مسارات ثابتة `roleHome.ts:11-12`) — VERIFIED.
- حراس `RequireAuth/RequireRole` (`App.tsx:62-85`) للتجربة فقط؛ الإنفاذ خادمي — VERIFIED.

## 8. الأسرار والإعدادات

- `Jwt:Secret` فارغ افتراضيًا (`appsettings.json:52-57`) والحارس يرفض الإقلاع بدونه — VERIFIED؛
  الإنتاج عبر `Jwt__Secret (sync:false)` + `Bootstrap__AdminPassword (sync:false)` في `render.yaml:20-23` — VERIFIED.
- لا سلسلة إنتاج ملتزمة (`docgen.db` محليًا `:17-19`؛ الإنتاج مُحقون `render.yaml:16-19`) — VERIFIED.
- `DataProtection`: غياب VERIFIED (بلا `AddDataProtection` في الشجرة) — وهو سليم (جلسات `JWT` عديمة الحالة
  و`CSRF` بلا حالة خادمية)؛ سيلزم مخزن مفاتيح دائم إن أُضيف تشفير خادمي لاحقًا — INFERRED.
- فصل `dev/prod` سليم (`appsettings.Development.json:9-11` بلا أسرار)؛ `Dockerfile:23-26` بمستخدم غير جذري — VERIFIED.
- التاريخ: `git log -p -S "Secret"` — الإصابة الوحيدة إنشاء الملف بقيمة `""` (لم يُلتزم سر `JWT` حقيقي قط)؛
  الاستثناء: كلمة بذر التطوير ملتزمة منذ الأساس — `SEC-004`.
- كلمات الاختبارات طويلة غير إنتاجية (`ApiFactory.cs:36` ونظراؤها) — مقبول.

## 9. الاعتماديات (قراءة فقط)

- **خلفية**: `dotnet list backend/DocGenerator.sln package --vulnerable --include-transitive` نُفّذ فعلًا:
  المشاريع الستة **`has no vulnerable packages`** — VERIFIED. (`net10.0`؛ `JwtBearer 10.0.10`؛
  `EF Core 10.0.10` + `Npgsql 10.0.3`؛ `Serilog.AspNetCore 10.0.0`؛ `OpenXml 3.0.2` + `HtmlSanitizer 9.1.982`.)
- **واجهة** (من `package.json` + `lock`): `axios 1.19.0` و`react 19.2.8` و`vite 8.2.0` و`dompurify 3.4.13` —
  خطوط حديثة مدعومة — VERIFIED.
- **لكن الرقابة غير حاجبة**: مهمة `audit` في `ci.yml:42-57` مضبوطة `continue-on-error: true` وفحص `Snyk`
  الكامل معطّل بانتظار `SNYK_TOKEN` (`ci.yml:40-41`) — `SEC-010`.

## 10. السجلات

- `Serilog` (كونسول + ملف دوّار `logs/logs-.txt` بحد 50MB واحتفاظ 31 يومًا `appsettings.json:7-14`) مع
  `LogContext(TraceId/UserId/UserRole)` (`RequestLoggingEnricherMiddleware.cs:20-22`) — **بلا أجساد طلبات
  ولا رؤوس ولا كلمات ولا توكنات** — VERIFIED.
- `GlobalExceptionHandler.cs:48-64`: قالب ثابت + `Method/Path/TraceId/UserId`؛ رسائل `4xx` تعكس مدخلًا
  (رقم أساس/اسم فرع — مقصود للرسائل العربية)؛ جذر `DbUpdateException` للتطوير فقط — VERIFIED.
- `ClientErrorsController.cs:36-72`: موثّق + مخنوق لكل مستخدم + تسطيح `\r\n` ضد تزوير السطور + تجريد
  `query/fragment` + قصّ من الإعدادات — VERIFIED. لكن `Message/Stack` نص حر قد يحمل `PII` — `SEC-012`.
- **الفجوات**: إعادة المحاولة على مقفل صامتة + طلب اختيار فرع بلا تدقيق (`AuthService.cs:71-76,90-91`)،
  وكل `403` بلا تدقيق (الحارس بلا `ILogger` + `Forbid` مباشر)، ولا تدقيق خروج — `SEC-006/007/009`.

## 11. مسار التدقيق

- الهيكل: `AuditLog.cs:3-15` + `DocumentFieldChange.cs:8-29` + `Configurations.cs:344-356,1131-1154`
  (فهارس + **حذف متتابع** `:1148-1153`)؛ الكاتب `AuditLogger.cs:15-79` بطابع `UtcNow` خادمي — VERIFIED.
- **التغطية**: إنشاء/تعديل/حذف/استعادة/حالة/شطب/تجديد/تدوير/إجراءات/إنابات/استئنافات/مراسلات/مطالعة/
  مستخدمون/تذكيرات/اقتراحات — كلها مدققة داخل نفس المعاملة (`_tx.RunAsync` في `TransactionRunner.cs:22-49`) — VERIFIED.
  **الصامت**: توليد `Word` (`SEC-001`)، تصدير داخلي (`SEC-002`)، قراءة داخلية (`SEC-003`)،
  خروج (`SEC-009`)، تعليم اقتراح مقروءًا (`SEC-015`).
  **الإيجابي المقارن**: قراءة/تصدير البوابة الخارجية مدققان (`PortalService.cs:141,316`) وتصدير سجل الجهات
  مدقق (`PublicEntityService.cs:959`) — ازدواج المعاملة غير مبرر للداخلي.
- **العبث**: لا كتابة عبر `API` (`AuditLogsController` قراءة فقط) — لكن `Repository<T>.Update/Remove`
  موروثة في `AuditLogRepository` + لا إلحاق-فقط على مستوى القاعدة — `SEC-008`.
- **نطاق القراءة**: `AuditLogRepository.SearchAsync` (`:14-43`) بلا ترشيح فرع/جهة — `SEC-013`.
- **ذرية الدخول**: حفظا مسار الفشل متتاليان خارج معاملة (`AuthService.cs:117-134`) — `SEC-014`.

## 12. نموذج التهديد

| # | الفاعل | الأصل | السطح | التهديد | الضابط القائم | الضابط الغائب | الأثر | التخفيف |
|---|---|---|---|---|---|---|---|---|
| T-1 | غير موثَّق (إنترنت عام) | حسابات/جلسات | `POST api/auth/login` | تخمين/رش | حد طبقي + قفل أسّي + رسالة موحدة | لا `CAPTCHA`/تنبيه رش؛ المقفل صامت | متوسط | تنبيه عتبة رش + تدقيق الصامت (كود) |
| T-2 | مندوب خارجي | ملفات خارج جهته | كل `api/*` | هروب نطاق بتلاعب `id` | حارس بنيوي + نطاق جهة | رفض `403` بلا تدقيق — مسح لا يُرصد | عالٍ | سجل `portal_forbidden` بمعدل (كود) |
| T-3 | محامٍ فضولي | ملفات فرع آخر | `documents/*` + `export` | قراءة خارج النطاق + استخراج 10k | نطاق فرع/مالك + سقف تصدير | قراءة/تصدير/توليد بلا أي تدقيق | عالٍ | تدقيق الثلاثي (كود) |
| T-4 | مميز داخلي | أي ملف + السجل نفسه | الخدمات + `DB` مباشر | فعل بلا أثر ثم محو السجل | لا حذف عبر `API` | لا إلحاق-فقط + قراءة تدقيق بلا نطاق | حرج | إلحاق-فقط `DB` + تضييق القراءة (كود) |
| T-5 | جلسة `admin` مخترَقة | كل المستخدمين | `users/*` | حساب موالٍ/رفع صلاحيات | إبطال `TokenVersion` + تدقيق | جلسة 8 ساعات؛ لا تنبيه مدير جديد | عالٍ | تقصير الجلسة + تنبيه (قرار + كود) |
| T-6 | عميل `API` بتوكن مسروق | مورد الضحية | `api/*` + كوكي | إعادة تشغيل طلبات | `HttpOnly+Strict` + `CSRF` مزدوج + لا توكن مخزّن | `CSRF` مقروء `JS` بالتصميم؛ لا ربط قناة | عالٍ | `CSP` صارمة + مراجعة العرض |
| T-7 | قالب `Word` مسموم | الخادم/المستخدمون | `WordTemplates/*.docx` | `XXE`/ماكرو | ملفات ثابتة + `OpenXml 3.0.2` + تعقيم | لا بصمة تكامل؛ التوليد بلا تدقيق | متوسط-عالٍ | `hash` عند الإقلاع + تدقيق (كود) |
| T-8 | خدمة خارجية مخترَقة | كل البيانات | سلسلة الاتصال + الأسرار | تسرب لقطة `DB` (تشمل هاشات) | أسرار خارج المستودع + `PBKDF2` 600k | هاشات تاريخية ضعيفة حتى أول دخول؛ سجلات 31 يومًا | عالٍ | تدوير كلمات الخاملة + تقليل الاحتفاظ (قرار + إعداد) |

## 13. النتائج `SEC-001` … `SEC-017`

### SEC-001 — توليد `Word` يرفع `PrintCount` كتابةً بلا أي سجل تدقيق
- **الشدة**: MEDIUM | **الثقة**: VERIFIED | **الموقع**: `WordDocumentGenerator.cs:40-43` (الكتابة)
  مقابل `DocumentsController.cs:498-537` (بلا أي `Log*`).
- **الوصف**: كل `GET api/documents/{id}/generate?template=...` يولّد مستندًا قضائيًا (استدعاء/إخطار/محضر حجز)
  ويرفع عدّاد الطباعة في القاعدة دون إدخال `AuditLog` (فاعل/زمن/قالب/مستلم).
- **سيناريو الهجوم**: محامٍ (أو مخترِق بجلسته) يستدعي `GET /api/documents/123/generate?template=001&recipient=5`
  عشرات المرات لطباعة إخطارات رسمية خارج الإجراء — لا أثر في `GET /api/audit-logs` ولا سجل الملف.
- **الأثر**: سلامة إجرائية — وثائق رسمية بلا أثر قانوني (المالك اشترط «التدقيق يسجّل كل عملية»، السياق §8).
- **التوصية**: حقن `IAuditLogger` وتسجيل `generate_document` (القالب + المستلم + `heirId/estateIds`) داخل نفس المعاملة.
- **اختبار الانحدار**: `Generate_Then_AuditContainsTemplateAndRecipient` + `Generate_Failure_DoesNotBumpPrintCount`.
- **وضوح عمل**: NO | **الإصلاح**: كود فقط.

### SEC-002 — التصدير الداخلي `Excel` (حتى 10k صف) بلا تدقيق
- **الشدة**: MEDIUM | **الثقة**: VERIFIED | **الموقع**: `DocumentService.Search.cs:131-143` (بلا `Log*`)
  عبر `DocumentsController.cs:150-164` (مقابل تصدير البوابة المدقق `PortalService.cs:316` وتصدير السجل المدقق `PublicEntityService.cs:959`).
- **الوصف**: `GET api/documents/export?...` يصدّر كامل نتائج البحث (أسماء/مبالغ/هويات) بلا صف تدقيق واحد
  (من صدّر؟ متى؟ كم صفًا؟ بأي فلتر؟).
- **سيناريو الهجوم**: مستخدم مصرح له بحثيًا يصدّر بفلتر واسع دوريًا — استخراج ضخم مشروع-شكليًا بلا أثر.
- **التوصية**: `LogAsync(actor,"export_documents", الفلاتر + عدد الصفوف)` في نفس مسار `ExportAsync`.
- **اختبار الانحدار**: `Export_Then_AuditHasRowCountAndFilters` + `Export_OverCap_StillAudited`.
- **وضوح عمل**: NO | **الإصلاح**: كود فقط.

### SEC-003 — قراءة الملفات الداخلية غير مسجلة (عداد أصم فقط)
- **الشدة**: MEDIUM | **الثقة**: VERIFIED | **الموقع**: `DocumentsController.cs:488-496` +
  `DocumentService.cs:472-474` (رفع `ViewCount` ذري بلا تدقيق)؛ `GET` التفصيل/القائمة بلا أثر
  (مقابل قراءة البوابة المدققة `PortalService.cs:141`).
- **الوصف**: فضولي داخلي يتصفح `GET /api/documents/456` خارج فرعه — العداد يرتفع رقمًا بلا هوية.
- **الأثر**: سرية + عدم إنكار الاطلاع. **يتطلب قرار عمل** (السياق §8 ترك تجريم القراءة `UNKNOWN`؛
  وحجم الحركة يجعل تدقيق كل قراءة مكلفًا تخزينيًا).
- **التوصية**: كحد أدنى تدقيق قراءة التفصيل الكامل (قارئ + زمن + `id`) أو عداد مُسنَد بهوية آخر قارئ.
- **اختبار الانحدار**: `ViewDetail_Then_AuditHasReaderAndDocId` (أو حسب القرار).
- **وضوح عمل**: YES (`BQ-021`) | **الإصلاح**: كود فقط (+ قرار احتفاظ).

### SEC-004 — كلمة بذر التطوير الافتراضية ملتزمة في المستودع
- **الشدة**: LOW | **الثقة**: VERIFIED | **الموقع**: `DbSeeder.cs:8,18,37-40` (القيمة `****` — لا تُنسخ)؛
  التخفيف `DatabaseInitializer.cs:28-31` + `DbSeeder.cs:50-68`.
- **الوصف**: كلمة تطوير معروفة ملتزمة منذ الأساس، محصورة بمسار `IsDevelopment`، والإنتاج يرفض البذر الافتراضي.
- **المتبقي**: نشر تجريبي خطأً بـ`Development` (أو نسخ `docgen.db` تطويري) يفتح 4 حسابات معروفة.
- **التوصية**: توليد كلمة البذر عشوائيًا عند كل بذر وطباعتها لمرة واحدة + رفض إقلاعي لـ`Development + UsePostgres=true`.
- **اختبار الانحدار**: `Seed_UsesRandomPasswordEachTime` + `Bootstrap_RefusesEmptyPassword`.
- **وضوح عمل**: NO | **الإصلاح**: كود فقط.

### SEC-005 — سلسلة `Postgres` احتياطية باعتماد افتراضي في مصنع زمن-التصميم
- **الشدة**: LOW | **الثقة**: INFERRED | **الموقع**: `PostgresDbContextFactory.cs:15-16` (قيم `****` — لا تُنسخ).
- **الوصف**: مصنع توليد الهجرات محليًا يحمل اعتمادًا افتراضيًا؛ لا يصل الإنتاج (مسار `design-time` فقط).
- **التوصية**: قراءته من متغير بيئة أو إزالة القيمة المض
...[truncated 10916 chars]