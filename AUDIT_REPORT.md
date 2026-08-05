# تقرير التدقيق الشامل — التحويل إلى React + .NET

- **التاريخ:** 2026-07-31
- **النطاق:** `react-dotnet-app` (الخلفية `backend` والواجهة `frontend`)
- **المراجع:** النسخة المكتبية (PyQt5) + نسخة Flask المرجعية (موجودة في `archive/flask-web-app`)
- **المنهجية:** مقارنة سلوكية مقابل الأصول البرمجية القياسية، مراجعة كود يدوية، اختبارات آلية، واختبار E2E عبر HTTP.

---

## 1. الملخص التنفيذي

جرى فحص التحويل كاملاً مقابل الأصول القياسية (Desktop وFlask). تم اكتشاف وإصلاح **8 عيوب** — منها **خلل حرج واحد** في صلاحيات الأدوار (JWT) كان سيعطّل الوصول للأدوار غير المدير عبر `[Authorize(Roles=...)]`، وعيبا ثغرة/أداء في عملية تسجيل الدخول والبحث، وثغرة صلاحية في الحذف. الحل يبني الآن بـ **0 تحذيرات**، وتمر **35 اختباراً**، وتُحققت الإصلاحات مباشرة عبر HTTP.

---

## 2. جدول المكتشفات

| # | الخطورة | المكتشف | الملف | الحالة |
|---|---------|---------|-------|--------|
| 1 | **حرجة** | قيمة دور JWT بأحرف كبيرة مقابل قيم `[Authorize(Roles="manager,admin")]` الصغيرة | `TokenService.cs` | ✅ مُصلح |
| 2 | **عالية** | البحث يحمّل كل المستندات في الذاكرة ثم يفلتر | `DocumentService.cs` | ✅ مُصلح |
| 3 | **عالية** | لا يوجد `LoginRateLimiter` (5 محاولات/5 دقائق) مقابل Flask | `AuthController.cs` | ✅ مُصلح |
| 4 | **عالية** | لا يوجد تدقيق (Audit) للأحداث الحساسة مقابل Flask | `AuthService.cs`/`DocumentService.cs` | ✅ مُصلح |
| 5 | **عالية** | الحذف متاح لأي مستخدم بدل «رئيس القسم فقط» مقابل الأصل | `DocumentsController.cs` | ✅ مُصلح |
| 6 | **متوسطة** | `UpdateStatusAsync` يقبل أي قيمة حالة | `DocumentService.cs` | ✅ مُصلح |
| 7 | **متوسطة** | لا يوجد حد أدنى لكلمة المرور عند تغييرها | `AuthService.cs` | ✅ مُصلح |
| 8 | **منخفضة** | عدم توافق التحقق من كلمات مرور مستخدمي Flask (صيغة werkzeug) | `PasswordHasher.cs` | ✅ مُصلح |
| 9 | **منخفضة** | زرّ «بدون حالة» المكرر في فلتر الواجهة | `DocumentsList.tsx` | ✅ مُصلح |
| 10 | **منخفضة** | نموذج التعديل يبثّ حقولاً غير صالحة في طلب التحديث | `DocumentForm.tsx` | ✅ مُصلح |
| 11 | **منخفضة** | إمكانية الحذف معروضة للمدير في الواجهة (خلاف الأصل) | `DocumentView.tsx` | ✅ مُصلح |

---

## 3. التفاصيل

### 3.1 حرجة — تطابق أدوار JWT

- **المشكلة:** `TokenService.CreateToken` كان يضيف `ClaimTypes.Role` بقيمة `UserRole.ToString()` (أي `Manager`, `Admin`, ...) بينما كل `[Authorize(Roles="manager,admin")]` في الـ Controllers يطلب أحرفاً صغيرة. النتيجة: حتى المديرون الصالحون لا يتجاوزون `Authorization`.
- **الجذر:** انعدام التوافق بين قيمة الـ claim المكتوبة وأسماء الأدوار في سمات التحكم.
- **الإصلاح:** `user.Role.ToString().ToLowerInvariant()` عند إصدار التوكن (سطر 28 من `TokenService.cs`)، ومطابقة `GetRole()` في `ClaimsPrincipalExtensions.cs` مع `ToLowerInvariant()`.
- **التحقق:** اختبار `TokenServiceTests.CreateToken_RoleClaim_IsLowercase` لكل الأدوار الأربعة + فحص JWT الحقيقي عبر HTTP (`role: "lawyer"`).

### 3.2 عالية — البحث يتحول إلى `SELECT` كامل في الذاكرة

- **المشكلة:** `DocumentService.SearchAsync` القديمة كانت تستدعي `ListAsync` ثم تفلتر في الذاكرة — انهيار أداء مع نمو البيانات.
- **الإصلاح:** واجهة `IDocumentRepository` مع `SearchAsync` ينفذ `Count + Skip/Take + Include` على مستوى قاعدة البيانات في `DocumentRepository.cs`، مع ترشيح بالفرع/المستخدم الظاهر قبل الترحيل.
- **التحقق:** اختبار بحث بالكفيل واختبار ترقيم صفحات + E2E (`q=ضامن` → `totalCount: 1`).

### 3.3 عالية — غياب تحديد محاولات الدخول

- **المشكلة:** نسخة Flask كان فيها `LoginRateLimiter` (5 محاولات / 5 دقائق)؛ النسخة المحوّلة لم تكن تحددها.
- **الإصلاح:** `ILoginRateLimiter` + تنفيذ `LoginRateLimiter` (يستخدم `ConcurrentDictionary`، آمن للخيوط) مسجّل كـ `Singleton` في `DependencyInjection.cs`، ومربوط في `AuthController.Login` بمفتاح `IP:username` (يسجّل الفشل عند `Unauthorized` ويرجّع `429 Too Many Requests`).
- **التحقق:** اختبارات `LoginRateLimiterTests` (حد أقصى، عزل المفاتيح، إعادة تعيين) + E2E: 5 محاولات خاطئة → `401`، السادسة → `429`.

### 3.4 عالية — غياب التدقيق (Audit Logging)

- **المشكلة:** الأصل يسجل عمليات دخول وإنشاء وتعديل وحذف وتغيير حالة؛ النسخة المحوّلة كانت صامتة.
- **الإصلاح:** `IAuditLogger` + `AuditLogService` (يكتب إلى جدول `AuditLogs`) وربطه في:
  - `AuthService`: `login`, `login_failed`, `change_password`.
  - `DocumentService`: `create`, `update`, `delete`, `status` (مع `documentId` و`documentType` و`details`).
  - جميع التوقيعات تلقت باراميتر `actorName`.
- **التحقق:** اختبارات `ServiceTests` تتحقق من سجل التدقيق عبر `FakeAuditLogger`، وسجل حقيقي يُكتب في DB (كل العمليات E2E عادت بـ `200/201` ما يعني نجاح `SaveChanges` على `AuditLogs`).

### 3.5 عالية — صلاحية الحذف

- **المشكلة:** الحذف كان متاحاً لأي مستخدم واصل.
- **الأصل:** `can_delete = role == 'head'` في نسخة Flask، وتطبيق المكتب يقصر الحذف على رئيس القسم.
- **الإصلاح:** `DocumentsController.Delete` يتحقق `IsHead` أولاً ثم `CanAccess`. كما حُفظت سلوكية الأصل بأن `SetStatus` يبقى لأي مستخدم يملك صلاحية الوصول للمستند.
- **التحقق:** E2E: حذف كمدير → `403`، كرئيس قسم → `204`.

### 3.6 متوسطة — تحقق من صحة الحالة

- **المشكلة:** `UpdateStatusAsync` كان يقبل أي نص كحالة.
- **الإصلاح:** `ValidStatuses = { "", "منفذ", "تريث" }` مع `ArgumentException("حالة غير صالحة")` يتحول إلى `400` في الـ Controller.
- **التحقق:** اختبار + E2E (`status=مزيف` → `400`، `status=تريث` → `200`).

### 3.7 متوسطة — حد أدنى لكلمة المرور

- **الإصلاح:** `ChangePasswordAsync` يرفض كلمة أقصر من 6 أحرف بـ `ArgumentException` → `400`.
- **التحقق:** اختبار + E2E (`newPassword="123"` → `400`).

### 3.8 منخفضة — توافق `PasswordHasher` مع مستخدمي Flask

- **المشكلة:** المستخدمون المخزّنون في قاعدة بيانات Flask يستخدمون صيغة werkzeug `pbkdf2:sha256:600000$salt$hash` (base64) — كانت التحويلات تفشل ضدهم.
- **الإصلاح:** `Verify` يتعرف على البادئة `pbkdf2:sha256:` ويحسب `Rfc2898DeriveBytes.Pbkdf2` بعدد التكرارات من السلسلة ويقارن بـ `CryptographicOperations.FixedTimeEquals`. تُركّ الصيغة الأصلية `saltHex:hashHex` وصيغة SHA-256 القديمة.
- **التحقق:** اختبارات `PasswordHasherLegacyTests`.

### 3.9–3.11 منخفضة — الواجهة

- إزالة القيمة المكررة `""` في فلتر الحالة بـ `DocumentsList.tsx`.
- وضع التعديل في `DocumentForm.tsx` يبني الآن `DocumentUpsertRequest` صراحةً عبر دالة `toUpsert()` بدل بثّ استجابة الواجهة كاملة (كانت قد تتضمن حقولاً للقراءة فقط ترفضها الخادم أو تلحق ضرراً).
- `DocumentView.tsx`: `canDelete = isHead` فقط.

---

## 4. توافق السلوكيات مقابل الأصل

| السلوك | Flask | النسخة المحوّلة | الحالة |
|--------|-------|----------------|--------|
| تسجيل دخول + JWT | جلسة `session` | JWT Bearer | ✅ |
| صلاحيات الدور | `manager/admin/head/lawyer` | نفسها (أحرف صغيرة) | ✅ |
| المدير لا يُدخل مستندات | ✅ | `Create` يرفض دور `manager` | ✅ |
| الحذف لرئيس القسم فقط | ✅ | `IsHead` + `CanAccess` | ✅ |
| تحديث الحالة لأي واصل | ✅ | `CanAccess` | ✅ |
| كفيل يؤمّن البحث | بحث نصي بالكفيل | `Guarantors.Any(...)` على مستوى DB | ✅ |
| محدد محاولات الدخول | 5 / 5 دقائق | 5 / 5 دقائق | ✅ |
| كلمات مرور Flask تعمل | — | دعم صيغة werkzeug | ✅ |

---

## 5. الفجوات المتبقية والتوصيات (غير عابرة للوظيفة)

### ⛔ قبل النشر — نقاط تحقّق إلزامية

1. **التحول إلى PostgreSQL (النقطة المحفوظة):** الترحيبات (`Migrations`) الحالية وُلدت مع SQLite. قبل النشر على PostgreSQL، جرّب الترحيل أولاً على خادم Postgres مؤقت وتحقّق من نجاح `db.Database.Migrate()` مع ضبط `ConnectionStrings__DefaultConnection` و`Database:UsePostgres=true` — ثم انشر.
2. **`Jwt:Secret`:** تأكد من ضبط `Jwt__Secret` في بيئة الإنتاج — وإلا يرفض التطبيق الإقلاع (عن قصد).

1. **`LoginRateLimiter` داخل العملية (منخفضة):** مناسب لتشغيل عقدة واحدة. عند نشر عدة نسخ خلف موازن يجب نقل العدّاد إلى مخزن مشترك (Redis/DB).
2. **حماية نقطة نهاية Swagger (منخفضة):** يوصى بتقييد `/swagger` في الإنتاج.
3. **`Jwt:Secret` (حرجة للإنتاج):** خارج بيئة التطوير يُرفض الإقلاع إن لم يكن معرّفاً — يجب تأمينه عبر المتغيرات البيئية/Secure Vault.

### إصلاحات إضافية أُنجزت بعد التقرير الأولي

| الفجوة | الحالة |
|--------|--------|
| `StatisticsService` (Dashboard/Monthly/Branches/Activity) كان يحمّل كل السجلات في الذاكرة | ✅ نُقل إلى `IStatisticsRepository` بتجميع `GroupBy/Count/Sum` على مستوى DB |
| `AuthService.LoginAsync` كان يجرّ كل المستخدمين ثم يفلتر | ✅ `IUserRepository.FindByUsernameAsync` يبحث في DB مباشرة |
| حد `decimal Sum` في SQLite (قيد EF Core) | ✅ مشروع `(double)` في التجميع ثم يُعاد للـ `decimal` — متوافق مع PostgreSQL أيضاً |

### إغلاق الفجوات المتبقية (جولة «المعالجة وفق الأصول الصارمة»)

| الفجوة | الحل | الحالة |
|--------|------|--------|
| لا واجهة لعرض سجل التدقيق | `IAuditLogRepository` + `AuditLogService` + `GET /api/audit-logs` (manager/admin، ترقيم صفحات وترشيح) + صفحة `AuditLogs` في الواجهة | ✅ |
| عدّاد rate limiter داخل العملية فقط | `DbLoginRateLimiter` يعتمد جدول `LoginAttempts` (مشترك بين العقد) + migration `AddLoginAttempts` + خيارات قابلة للضبط `RateLimiting` | ✅ |
| `/swagger` غير مضبوط صراحةً | إعداد `Swagger:Enabled` + تفحص بيئة التطوير؛ اختبار يؤكد `404` في الإنتاج | ✅ |
| لا اختبارات تكامل آلية | مشروع `DocGenerator.Api.Tests` بـ `WebApplicationFactory` (قاعدة مؤقتة معزولة): 21 اختباراً تغطي الدخول، الأدوار، rate limit، الصلاحيات، الحالة، التدقيق، Swagger | ✅ |

---

## 6. أدلة التحقق

- **البناء:** `dotnet build DocGenerator.sln` → `Build succeeded. 0 Warning(s) 0 Error(s)`.
- **الاختبارات:** `dotnet test DocGenerator.sln` → `Passed: 60, Failed: 0`
  (39 وحدة + 21 تكامل بـ `WebApplicationFactory`).
- **الواجهة:** `npm run build` → `✓ built` (87 وحدة، بدون أخطاء TS).
- **E2E عبر HTTP** (المنفذ `5199`):
  - `DELETE` كمدير → `403`؛ كرئيس قسم → `204`.
  - `POST /documents/{id}/status` بحالة صالحة → `200`؛ بحالة غير صالحة → `400`.
  - 6 محاولات دخول خاطئة → `401 × 5` ثم `429`.
  - تغيير كلمة مرور → `200`، الدخول بالقديمة → `401`، بالجديدة → `200`، كلمة قصيرة → `400`.
  - بحث باسم كفيل → `totalCount: 1`.
  - إصدار JWT يحتوي `role` بأحرف صغيرة.

---

## 7. الملفات المتأثرة

| الملف | التغيير |
|-------|---------|
| `backend/src/DocGenerator.Infrastructure/Security/TokenService.cs` | أدوار صغيرة في claim |
| `backend/src/DocGenerator.Api/ClaimsPrincipalExtensions.cs` | `GetRole()` بأحرف صغيرة |
| `backend/src/DocGenerator.Application/Common/Interfaces/IAuditAndRateLimit.cs` | واجهات `IDocumentRepository`, `ILoginRateLimiter`, `IAuditLogger` |
| `backend/src/DocGenerator.Infrastructure/Persistence/DocumentRepository.cs` | بحث DB-مستوى ترحّلي |
| `backend/src/DocGenerator.Infrastructure/Security/LoginRateLimiter.cs` | محدد محاولات الدخول |
| `backend/src/DocGenerator.Infrastructure/Persistence/AuditLogService.cs` | كتابة `AuditLogs` |
| `backend/src/DocGenerator.Application/Services/DocumentService.cs` | توقيعات `actorName`, حالات صالحة, بحث عبر المستودع |
| `backend/src/DocGenerator.Application/Services/AuthService.cs` | تدقيق + حد أدنى لكلمة المرور |
| `backend/src/DocGenerator.Application/Services/PasswordHasher.cs` | توافق werkzeug |
| `backend/src/DocGenerator.Api/Controllers/AuthController.cs` | rate limiter + `429` + `400` |
| `backend/src/DocGenerator.Api/Controllers/DocumentsController.cs` | حذف لرئيس القسم فقط + `ActorName` + `400` |
| `backend/src/DocGenerator.Infrastructure/DependencyInjection.cs` | تسجيل الواجهات الجديدة |
| `backend/tests/DocGenerator.Application.Tests/ServiceTests.cs` | توقيعات محدّثة + اختبارات جديدة |
| `backend/tests/DocGenerator.Application.Tests/SecurityTests.cs` | جديد: JWT / werkzeug / rate limiter |
| `backend/src/DocGenerator.Application/Common/Interfaces/IUserRepository.cs` | جديد: بحث مستخدم في DB |
| `backend/src/DocGenerator.Application/Common/Interfaces/IStatisticsRepository.cs` | جديد: تجميع إحصائيات في DB |
| `backend/src/DocGenerator.Infrastructure/Persistence/UserRepository.cs` | جديد: `FindByUsernameAsync` |
| `backend/src/DocGenerator.Infrastructure/Persistence/StatisticsRepository.cs` | جديد: `GroupBy/Count/Sum` على مستوى DB |
| `backend/src/DocGenerator.Application/Services/AuthService.cs` | `FindByUsernameAsync` بدل جلب الكل |
| `backend/src/DocGenerator.Application/Services/StatisticsService.cs` | تفويض إلى `IStatisticsRepository` |
| `backend/src/DocGenerator.Infrastructure/DependencyInjection.cs` | تسجيل `IUserRepository` + `IStatisticsRepository` |
| `backend/tests/DocGenerator.Application.Tests/StatisticsTests.cs` | جديد: اختبارات الإحصائيات (DB) |
| `backend/tests/DocGenerator.Application.Tests/ServiceTests.cs` | تحديث إنشاء `AuthService` |
| `backend/tests/DocGenerator.Api.Tests/` | جديد: مشروع اختبارات التكامل (`ApiFactory`, Auth, Documents, Audit/Statistics, Swagger) |
| `backend/src/DocGenerator.Infrastructure/Persistence/Migrations/*_AddLoginAttempts.cs` | جديد: جدول `LoginAttempts` |
| `backend/src/DocGenerator.Application/DTOs/AuditLogDto.cs` | جديد |
| `backend/src/DocGenerator.Application/Common/Interfaces/IAuditLogRepository.cs` | جديد |
| `backend/src/DocGenerator.Application/Services/AuditLogService.cs` | جديد: استعلام التدقيق |
| `backend/src/DocGenerator.Api/Controllers/AuditLogsController.cs` | جديد: `GET /api/audit-logs` |
| `backend/src/DocGenerator.Infrastructure/Persistence/AuditLogRepository.cs` | جديد |
| `backend/src/DocGenerator.Infrastructure/Security/DbLoginRateLimiter.cs` | جديد: عدّاد مشترك عبر `LoginAttempts` |
| `backend/src/DocGenerator.Infrastructure/Persistence/AuditLogger.cs` | إعادة تسمية من `AuditLogService` |
| `backend/src/DocGenerator.Infrastructure/DependencyInjection.cs` | تسجيل `IUserRepository` + `IStatisticsRepository` + `IAuditLogRepository` + `DbLoginRateLimiter` |
| `backend/src/DocGenerator.Application/Common/RateLimitOptions.cs` | جديد: إعدادات `RateLimiting` |
| `backend/src/DocGenerator.Api/Program.cs` | `public partial class Program` + خيار `Swagger:Enabled` + ربط `RateLimiting` |
| `backend/src/DocGenerator.Api/appsettings.json` | قسم `RateLimiting` + `Swagger:Enabled` |
| `frontend/src/pages/AuditLogs.tsx` | جديد: صفحة سجل التدقيق |
| `frontend/src/pages/DocumentsList.tsx` | إزالة الخيار المكرر |
| `frontend/src/pages/DocumentForm.tsx` | `toUpsert()` في وضع التعديل |
| `frontend/src/pages/DocumentView.tsx` | `canDelete = isHead` |
| `frontend/src/pages/ChangePassword.tsx` | جديد: صفحة تغيير كلمة المرور |
| `frontend/src/App.tsx` + `frontend/src/components/Layout.tsx` | مسار ورابط تغيير كلمة المرور |
