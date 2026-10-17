# متغيرات البيئة والإعدادات (Production)

> **المرجع**: `appsettings.json`، `Program.cs`، `Dockerfile`، `render.yaml` (2026-10-16).
> **الصيغة**: متغيرات البيئة تتبع نمط `.NET` (`Section__Key` بنقطتين مزدوجتين). مفاتيح هرمية تُضبط بـ`__`.
> **الوسم**: 🔴 إلزامي في الإنتاج | 🟠 موصى به بشدة | ⚪ اختياري/افتراضي مقبول.

---

## 1) أساسيات التشغيل

| المتغير | الوسم | الافتراضي | الوصف |
|---|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | 🔴 | — | `Production` للإنتاج (يفتح خدمة `wwwroot`، يغلق `Swagger`/`CORS`/`HSTS` حسب الرايات). |
| `PORT` | 🟠 | `8080` | منفذ الاستماع؛ `Dockerfile:20` يستخدم `${PORT:-8080}`. المضيف يضبطه. |
| `AllowedHosts` | 🟠 | `*` | على الإنتاج: النطاق الحقيقي فقط (`appsettings.json:16`؛ `L-01`). |

## 2) قاعدة البيانات

| المتغير | الوسم | الافتراضي | الوصف |
|---|---|---|---|
| `Database__UsePostgres` | 🔴 | `false` (`appsettings.json:21`) | **يجب `true` في الإنتاج** — وإلا **يُرفض الإقلاع** خارج التطوير (`H-01`). `render.yaml:14`. |
| `ConnectionStrings__DefaultConnection` | 🔴 | `Data Source=docgen.db` | سلسلة `Postgres` في الإنتاج (`Host=...;Port=...;Database=...;Username=...;Password=...`). `render.yaml:16-19`. |
| `DATABASE_URL` | ⚪ | — | بديل: رابط `postgres://` يحقنه بعض المضيفين تلقائيًا؛ يُفعّل `Postgres` ضمنيًا. **قيمة فارغة/بيضاء تُعامَل كغائبة** عمدًا (`H-01b`) فلا تُسقط التطبيق على `SQLite` شبح. |
| `Database__AllowSqliteInProduction` | ⚪ (استثناء صريح فقط) | `false` | `true` يسمح بالإقلاع على `SQLite` خارج التطوير — لعقدة داخلية صغيرة بقرار مقصود (`H-01`). **لا يُضبط في نشر `Postgres`.** |

> عند `UsePostgres=true` يجري التحقق من قابلية قراءة السلسلة، وفشل الإقلاع عند تشوّهها (`Program.cs:52-76`). لا تُسجَّل السلسلة.
> خارج التطوير بلا `Postgres` (ولا `DATABASE_URL`) ولا `AllowSqliteInProduction` → `InvalidOperationException` عند الإقلاع برسالة توجيهية.

## 3) المصادقة والأسرار

| المتغير | الوسم | الافتراضي | الوصف |
|---|---|---|---|
| `Jwt__Secret` | 🔴 | `""` | **يُرفض الإقلاع** إن قلّ عن `32` بايت (`Program.cs:92-95`). عشوائي قوي. `sync: false` (`render.yaml:20-21`). |
| `Bootstrap__AdminPassword` | 🔴 (أول نشرة) | — | كلمة مدير النظام الأول؛ غيابها مع قاعدة بلا مستخدمين = **فشل إقلاع صريح** (`DbSeeder.cs:80-83`). `sync: false` (`render.yaml:22-23`). |
| `Jwt__Issuer` | ⚪ | `DocGenerator` | يجب مطابقة العملاء. |
| `Jwt__Audience` | ⚪ | `DocGeneratorClients` | — |
| `Jwt__ExpiryMinutes` | ⚪ | `240` | عمر الجلسة. |
| `Bootstrap__DevSeedPassword` | ⚪ (تطوير/اختبار فقط) | — | لا يُضبط في الإنتاج (يُنشئ حسابات تطوير افتراضية). |

## 4) الأمن (تصلّب بحسب المضيف)

| المتغير | الوسم | الافتراضي | الوصف |
|---|---|---|---|
| `Security__KnownProxies` | 🔴 على المخدّم الخاص | `[]` | صفيف عناوين `IP`/`CIDR` للبروكسي (`["10.0.0.5"]`). بدونها يتشوّه `IP` العميل (`M-04`). |
| `Security__HstsEnabled` | 🔴 على المخدّم الخاص | `false` | `true` بعد تأكيد `HTTPS` كامل (`M-05`). |
| `Security__HttpsRedirectionEnabled` | 🔴 على المخدّم الخاص | `false` | `true` على المخدّم الخاص. |
| `Security__CspReportOnly` | 🟠 | `true` | `false` بعد جولة مراقبة واحدة (فرض `CSP` فعليًا). |
| `Security__CspReportUri` | ⚪ | — | عنوان استقبال تقارير `CSP`. |
| `Swagger__Enabled` | ⚪ | `false` | مغلق افتراضيًا خارج التطوير. |

> على منصة تدير `TLS` (كـ`Render`) تُترك رايات `Security` على الافتراضي الآمن.

## 5) تحديد المعدل والقفل

| المتغير | الافتراضي | الوصف |
|---|---|---|
| `RateLimiting__MaxLoginAttempts` | `5` | محاولات فاشلة لكل `IP:user` قبل القفل. |
| `RateLimiting__WindowMinutes` | `5` | نافذة العد. |
| `RateLimiting__LoginIpPerMinute` | `10` | حد لكل `IP` لتوليد التوكن. |
| `RateLimiting__GeneralAnonPerMinute` | `100` | عام للزوار. |
| `RateLimiting__GeneralAuthPerMinute` | `300` | عام لكل مستخدم مصادق. |
| `RateLimiting__ExpensivePerMinute` | `60` | تصدير/إحصاءات. |
| `RateLimiting__PasswordPerMinute` | `5` | دخول/تغيير كلمة المرور. |
| `Lockout__MaxFailedAttempts` | `5` | حد القفل المتصاعد. |
| `Lockout__LockoutMinutes` | `15` | مدة القفل الأول. |
| `Lockout__MaxLockoutMinutes` | `120` | سقف القفل. |

## 6) متنوّع

| المتغير | الافتراضي | الوصف |
|---|---|---|
| `TimeZone__Id` | `Asia/Damascus` | توقيت العرض/الحساب (التخزين `UTC` دائمًا). |
| `Export__MaxRows` | `10000` | سقف صفوف التصدير. |
| `Forum__RetentionMonths` | `6` | `0` معطّل، السالب **يُفشل الإقلاع** (`Program.cs:156-160`). |
| `WordTemplates__Path` | `WordTemplates` | مسار قوالب `Word` — يجب أن تحوي ملفات `.docx` فعليًا عند النشر. |
| `Logging__LogLevel__Default` | `Warning` | مستوى Serilog. |
| `Logging__File__Path` | `logs/logs-.txt` | **يجب أن يكون قابلًا للكتابة** (`Dockerfile:25` يهيّئ `/app/logs` ومالكه `app`). |
| `Logging__File__RetainedDays` | `60` | أيام احتفاظ ملفات التشخيص (قرار المالك 2026-10-16). |
| `Logging__File__FileSizeLimitBytes` | `52428800` | سقف الملف (`50MB`). |

## 7) متغيرات الاختبار (CI/فحص `Postgres` — ليست للنشر)

| المتغير | الوصف |
|---|---|
| `DOCGEN_TEST_POSTGRES` | سلسلة `Postgres` مرحلية تُفعّل `DatabaseInitializerTests` على محرك حقيقي (`RUN_GUIDE.md:196-199`). |

---

## تحذيرات حرجة

1. **`Database__UsePostgres` + `ConnectionStrings__DefaultConnection`** هما أخطر إعدادين: نسيانهما = **فشل إقلاع** (`H-01`) — لا وقوع صامت على `SQLite`. لا تُضبط `Database__AllowSqliteInProduction` على نشر `Postgres`.
2. **`Jwt__Secret` و`Bootstrap__AdminPassword`** يجب أن يكونا في أسرار المنصة (`sync: false`) لا في المستودع.
3. **`WordTemplates`** ملفات `.docx` ليست في `git` بالضرورة — تأكد من وجودها في صورة النشر وإلا فشل التوليد.
