# تقرير جاهزية الإنتاج — نظام إدارة ملفات التنفيذ القضائي

> **النوع**: تدقيق جاهزية إنتاج (Production Readiness Review) — وضع قراءة فقط.
> **التاريخ**: 2026-10-16.
> **النطاق**: كامل المستودع (`backend/` + `frontend/`) — الخلفية `.NET 10`، الواجهة `React`/`Vite`.
> **الفرع عند التدقيق**: `feature/party-limit-50` (بتعديلات غير ملتزمة في `docs/audit/final-review/`).
> **قاعدة الأدلة**: كل بند يحمل مسار الملف والسطر؛ ما لم يُتحقَّق منه موسوم ⚠️.
> **تحذير منهجي**: لا يُعتمد على تقارير `docs/audit` السابقة كحقيقة — ما في هذا التقرير مبني على قراءة الكود والتنفيذ الفعلي.

---

## 0) القرار التنفيذي (GO / NO-GO)

**القرار: NO-GO مؤقتًا — جاهز مشروط.**
> **تحديث 2026-10-16**: أُغلقت بنود `H-01` (+`H-01b`) و`H-02` و`M-01` و`M-02` كودًا مع اختبارات
> (فرع `refactor/prod-readiness-fixes`). رفع القرار إلى **GO** يتطلب: فحص `Postgres` حي قبل النشر،
> واستيفاء `DEPLOY-CHECKLIST.md`، وقرارات حوكمة `M-03`.

النظام مبني بمعمارية أمنية قوية جدًا (مصادقة `Cookie`، `CSRF`، إبطال توكن، ترويسات أمن، تحديد معدل، تعقيم نص منسّق، فصل `EntityManager`)، والبوابات الآلية خضراء بالكامل. لكن توجد **مخاطر تشغيلية عالية** تخص **ضبط بيئة النشر** (خصوصًا خطر الوقوع الصامت على `SQLite` في الإنتاج، وتطبيق الهجرات تلقائيًا عند الإقلاع) ومسائل حوكمة بيانات (سياسة كلمة المرور، `PII` في السجلات). لا توجد ثغرة قاتلة صريحة، لكن يجب إغلاق بنود **High** قبل التشغيل الحقيقي.

شرط رفع القرار إلى **GO**: إغلاق `H-01` و`H-02`، وتنفيذ فحص `Postgres` قبل النشر (RUN_GUIDE §9)، واستيفاء قائمة النشر `DEPLOY-CHECKLIST.md`.

---

## 1) نتائج البوابات الآلية (منفّذة فعليًا)

| البوابة | الأمر | النتيجة | الوسم |
|---|---|---|---|
| بناء الخلفية `Release` | `dotnet build -c Release` | ✅ نجح — تحذيران CS8604 فقط في `backend/tests/DocGenerator.Application.Tests/PartyLimitsTests.cs:159,166` | ✅ |
| اختبارات التطبيق | `dotnet test` (Application.Tests) | ✅ `1856` نجح (`14m52s`) | ✅ |
| اختبارات الواجهة الخلفية | `dotnet test` (Api.Tests) | ✅ `649` نجح (غير تسلسلي) + `2` نجح (`Category=Sequential`) | ✅ |
| بناء الواجهة | `npm run build` | ✅ نجح (`7.37s`) | ✅ |
| TypeScript | `npx tsc -b` | ✅ نظيف | ✅ |
| Lint | `npx oxlint src` | ✅ `0` warning / `0` error (`377` ملفًا) | ✅ |
| اختبارات الواجهة | `npx vitest run` | ✅ نجحت (~`1576`، مع تحذيرات `act`) | ✅ |
| ثغرات npm | `npm audit` (من `frontend/`) | ✅ `0` ثغرة (`--omit=dev` = `0`) | ✅ |
| ثغرات NuGet | `dotnet list package --vulnerable --include-transitive` | ✅ لا حزم مصابة | ✅ |
| حزم قديمة | `dotnet list package --outdated` | ⚠️ عدة حزم قديمة (غير مصابة) — انظر `L-03` | ⚠️ |
| توليد سكربت `Postgres` | `dotnet ef migrations script` (اتصال وهمي) | ✅ `1394` سطرًا، **0** `datetime2` / `nvarchar` / `uniqueidentifier` / `rowversion` | ✅ |
| تطبيق هجرات على `Postgres` حيّ | — | ⚠️ **لم يُتحقَّق** — لا `docker` ولا `psql` في هذه البيئة | ⚠️ |

**ملاحظة بيئية**: `docker` و`psql` غير متوفرين محليًا؛ لذلك مسار `PostgreSQL` الحقيقي (تطبيق + تشغيل + اختبار `timestamptz`) لم يُنفَّذ. جرى تعويض ذلك بتوليد سكربت الهجرات offline (نداء نظيف)، ويبقى **فحص `Postgres` قبل النشر إلزاميًا** (RUN_GUIDE §9، البند `H-02`/`M-06`).

---

## 2) الملاحظات (Findings)

### H-01 — الوقوع الصامت على `SQLite` في الإنتاج (High)

- **الدليل**:
  - `backend/src/DocGenerator.Api/appsettings.json:17-22` — `ConnectionStrings.DefaultConnection = "Data Source=docgen.db"` و`Database:UsePostgres = false`.
  - `backend/src/DocGenerator.Api/Program.cs:44-50` — `usePostgres` يُحسب من `Database:UsePostgres` **أو** وجود `DATABASE_URL`؛ عند غيابهما يُستخدم المسار الحرفي الاحتياطي `"Data Source=docgen.db"` بلا خطأ.
- **سيناريو الفشل الحقيقي**: النشر على أي مضيف لا يضبط `Database__UsePostgres=true` ولا يحقن `DATABASE_URL` (أو نُسي الضبط) → يقلع التطبيق بنجاح على `SQLite` في القرص الكتابي للحاوية. على مضيف بمخزن مؤقت (ephemeral) تفقد كل البيانات عند كل إعادة نشر، وتنشأ انحرافات نوعية (`SQLite` متسامح مع `datetime2`/الدقة) تظهر لاحقًا على `Postgres` كـ`timestamptz` صارم. لا شيء في السجلات ينبّه أن المحرك الفعلي ليس الإنتاجي.
- **الإصلاح (منفَّذ)**: في `Program.cs` بعد حساب `usePostgres`، إن كان `!builder.Environment.IsDevelopment()` و`usePostgres == false` → `throw new InvalidOperationException(...)` برسالة واضحة توجب ضبط `Database__UsePostgres`/`DATABASE_URL`، مع استثناء صريح `Database:AllowSqliteInProduction=true` لعقدة داخلية صغيرة بقرار مقصود. يجعل `SQLite` اختيارًا صريحًا للتطوير فقط (Fail-fast، على نمط حراس `Jwt:Secret` القائمين).
- **H-01b (اكتُشف أثناء الاختبار)**: `DATABASE_URL` الفارغة (`""`) كانت تفوز على سلسلة الاتصال عبر `??` فتُسقط التطبيق على قاعدة `SQLite` شبح بلا ملف (ترحيل ينجح ثم كل استعلام يفشل بـ`no such table`). صار الفراغ/البياض يُعامَل كغياب.
- **التحقق (منفَّذ)**: `ProductionDatabaseGuardTests` — اختبار `Production` بلا `Postgres` يفشل الإقلاع برسالة مطابقة؛ واختبار ب`AllowSqliteInProduction=true` يقلع وتظل نقطه البنيوية (`/healthz`) مجهولة؛ واختبار `Development` يقلع على `SQLite`.

### H-02 — تطبيق الهجرات تلقائيًا عند كل إقلاع في الإنتاج (High)

- **الدليل**:
  - `backend/src/DocGenerator.Infrastructure/Persistence/DatabaseInitializer.cs:27` — `await _db.Database.MigrateAsync(ct);` بلا شرط بيئة.
  - `backend/src/DocGenerator.Api/Program.cs:304-314` — يُنادى دائمًا عند الإقلاع قبل الاستماع.
  - `RUN_GUIDE.md:207` يقر صراحة أن «التهيئة تُطبّق الهجرات تلقائيًا عند الإقلاع».
- **سيناريو الفشل الحقيقي**: مع أكثر من نسخة (instance) تتقلع معًا (التحجيم الأفقي أو إعادة تشغيل متزامنة) تتسابق نسختان على `MigrateAsync` → تعارض على جدول `__EFMigrationsHistory` أو تطبيق جزئي. كذلك يُطبَّق المخطط بلا بوابة نسخة احتياطية، وهجرة سيئة (كقيد فريد يتحقق من صفوف قائمة) تُسقط الإقلاع في الإنتاج مباشرة (انظر التحذيرات في `RUN_GUIDE.md:322,328,351,355`).
- **الإصلاح (منفَّذ)**: إبقاء التطبيق التلقائي عند الإقلاع مع الاعتماد على قفل الهجرات المدمج في `EF Core`
  دون قفل `pg_advisory_lock` مخصص (كان تكرارًا وسطح فشل إضافيًا) + اشتراط نسخة احتياطية موثّقة قبل النشر.
  التطبيق اليدوي المسبق عبر `dotnet ef database update` يبقى مساندًا للنوافذ الحرجة عند الحاجة.
- **التحقق**: `DatabaseInitializerTests` لتطبيق الهجرات والبذر/التأسيس؛ وفحص `Postgres` حي قبل النشر ما زال
  إلزاميًا (`M-06`).

### M-01 — لا توجد سياسة تفويض احتياطية عامة (Medium)

- **الدليل**: `AddAuthorization` في `Program.cs` يضيف فقط سياسة `HeadOrSubHead`؛ **لا** `FallbackPolicy`/`DefaultPolicy` تفرض مصادقة.
- **الحالة الراهنة**: كل المتحكمات الـ`21` تحمل `[Authorize]` على مستوى الصنف (تحقق `grep`)، والاستثناءات匿名ة مقصودة: `AuthController.login/logout` و`MetaController.current-year` فقط.
- **سيناريو الفشل**: أي متحكم/إجراء جديد مستقبلًا يُضاف بلا سمة `[Authorize]` يصبح **متاحًا للعموم افتراضيًا** بلا أي رفض — خطأ صامت خطير في نظام قضائي.
- **الإصلاح (منفَّذ بصيغة معدّلة)**: `app.MapControllers().RequireAuthorization();` — تفويض افتراضي على كل إجراءات المتحكمات، مع `[AllowAnonymous]` صريح على `login`/`logout`/`current-year` وعلى نقطتي البنية (`/healthz` واحتياطي `SPA`). **تعمّدنا عدم استخدام `FallbackPolicy`** لأنها تُطبَّق على الطلبات بلا نقطة مطابقة (كـ`/swagger/v1/swagger.json` الذي لا يطابق احتياطي `SPA` لقيد `:nonfile`) فتحوّل كل `404` إلى `401` — تغيير كاسر؛ الصيغة المختارة تحقق الحماية الافتراضية وتحفظ دلالة `404`.
- **التحقق (منفَّذ)**: `AuthzDefaultsTests` يفحص السمات الصريحة بالانعكاس ويؤكد أن كل إجراء متحكم إما محمي بسمة تفويض على الإجراء/الصنف أو مجهول صراحةً؛ واختبارات `SwaggerIntegrationTests`/`SecurityHeadersIntegrationTests` تؤكد بقاء `404` والترويسات في الإنتاج.

### M-02 — سياسة كلمة مرور ضعيفة (Medium)

- **الدليل السابق**: حد `6` بلا قيود تنوّع/قائمة كلمات ممنوعة، ولا كشف اختراق.
- **سيناريو الفشل**: مستخدم يختار `123456` (وهو فعلًا كلمة بذر التطوير في `RUN_GUIDE.md:47`) — قابل للتخمين.
- **الإصلاح (منفَّذ)**: رفع الحد الأدنى (`10`)، رفض كلمات شائعة/مساوية لاسم المستخدم، عبر `PasswordPolicy` الموحدة في كل مسارات الإنشاء/التغيير/التأسيس، مع قاعدة “ما يُفحَص مقصوصًا هو ما يُخزَّن”.
- **التحقق**: اختبارات وحدة لحدود الطول/القائمة الممنوعة، واختبار تكامل لرسالة `400` العربية.

### M-03 — بيانات شخصية حسّاسة في السجلات بلا سياسة تقادم/تعمية معلنة (Medium)

- **الدليل**: `backend/src/DocGenerator.Api/Middleware/LogSanitizer.cs:9-11` — التعمية **مقصودة** ألّا_touch الأسماء والأرقام (تُشخَّص بها الأعطال)؛ و`AuditLog.Details` يخزّن تفاصيل حرّة. `GlobalExceptionHandler.cs:66` يسجّل الاستثناء كاملًا (قد يحوي مدخلات).
- **سيناريو الفشل**: ملفات `logs/logs-*.txt` وسجل التدقيق تحوي أسماء وأرقام هوية قضائية؛ تقادم/وصول/تسريب غير مضبوط = مخالفة حوكمة بيانات حساسة (بيانات قانونية حقيقية عند التشغيل).
- **الإصلاح**: توثيق سياسة تقادم السجلات والوصول إليها (من يقرؤوها، دورة الاحتفاظ، إخفاؤها عند عدم الحاجة)، وتقييم تعمية حقول محددة في `AuditLog.Details` أو تقييد عرضها لمن يملك صلاحية التدقيق.
- **التحقق**: مراجعة يدوية لسياسة الاحتفاظ + جرد أين تُطبع الحقول؛ لا اختبار آلي مباشر.

### M-04 — `Security:KnownProxies` فارغ افتراضيًا (Medium — اعتماد على المضيف)

- **الدليل**: `backend/src/DocGenerator.Api/appsettings.json:27` (`KnownProxies: []`) و`RUN_GUIDE.md:142-145`.
- **سيناريو الفشل**: على مخدّم خاص ببروكسي عكسي بلا ضبط `Security__KnownProxies` يُرى كل الزوار بعنوان البروكسي → ينهار مفتاح تحديد محاولات الدخول (`IP:user`) وتتشوّه سجلات التدقيق.
- **الإصلاح**: ضبط العناوين/CIDR على المخدّم الخاص (إلزامي حسب RUN_GUIDE). لا تغيير كود؛ ضبط نشر.
- **التحقق**: بعد النشر، تأكيد ظهور عناوين العملاء الحقيقية في السجل/مفتاح المحاولات.

### M-05 — افتراضيات التصلّب (`HSTS`/`HTTPS`/فرض `CSP`) مغلقة (Medium — اعتماد على المضيف)

- **الدليل**: `backend/src/DocGenerator.Api/appsettings.json:26-31` و`SecurityOptions.cs:13-20`.
- **السيناريو**: منصة تدير `TLS` بنفسها (كـ`Render`) → الافتراضي آمن ومقصود؛ لكن على مخدّم خاص بلا `TLS` منطبقة السلسلة غير مضمون.
- **الإصلاح**: على المخدّم الخاص ضبط `Security__HstsEnabled=true`، `Security__HttpsRedirectionEnabled=true`، `Security__CspReportOnly=false` بعد جولة اختبار واحدة (RUN_GUIDE:146-150).
- **التحقق**: فحص ترويسات `Strict-Transport-Security` و`Content-Security-Policy` الفعلية بعد النشر.

### M-06 — مسار `PostgreSQL` لم يُختبر محليًا (Medium — ⚠️ غير مُتحقَّق)

- **الدليل**: غياب `docker`/`psql`؛ `RUN_GUIDE.md:192-201` يفترض فحص `Postgres` قبل النشر.
- **الإصلاح**: تنفيذ فحص `Postgres` إلزامي قبل النشر (حاوية مؤقتة أو قاعدة مرحلية) + تشغيل `DatabaseInitializerTests` مع `DOCGEN_TEST_POSTGRES` (RUN_GUIDE:196-199).
- **التحقق**: خضرة ذلك الاختبار على `Postgres` حيّ.

### L-01 — `AllowedHosts: "*"` في الإعداد الأساسي (Low)

- **الدليل**: `backend/src/DocGenerator.Api/appsettings.json:16`.
- **الإصلاح**: على الإنتاج ضبط `AllowedHosts` على النطاق الحقيقي فقط (RUN_GUIDE:151).

### L-02 — `npm audit` من جذر المستودع يفشل (`ENOLOCK`) (Low)

- **الدليل**: لا `package-lock.json` في الجذر؛ الموجود في `frontend/` (نجح `npm audit` من هناك). CI يجب أن يشغّله من `frontend/`.
- **الإصلاح**: تثبيت مسار الفحص في CI/التوثيق إلى `frontend/`.

### L-03 — حزم قديمة (غير مصابة) (Low)

- **الدليل** (`dotnet list package --outdated`): `AngleSharp` `1.7.1→1.8.5`، `DocumentFormat.OpenXml` `3.0.2→3.5.1`، `HtmlSanitizer` `9.1.982→9.2.1039`، `Microsoft.Extensions.*` `10.0.10→10.0.13`.
- **الإصلاح**: ترقية دورية ضمن دورة تحديث محدودة، مع إعادة تشغيل البوابات — ليست عاجلة.

### L-04 — انحراف التوثيق عن الواقع (Low)

- **الدليل**:
  - `RUN_GUIDE.md:11,18` يذكر `.NET SDK 8` بينما الحل يستهدف `.NET 10` (CI يستخدم `10.0.x`).
  - `RUN_GUIDE.md:86,104` يذكر «`61` اختبارًا» و«`5` اختبارات» للواجهة بينما الواقع `1856`+`649`+`2` و~`1576` على الترتيب.
  - `RUN_GUIDE.md:212-357` قائمة «هجرات بانتظار التطبيق» طويلة جدًا (منها ما هو مُطبَّق محليًا) — يلزم تدقيق مطابقتها.
- **الإصلاح**: تحديث `RUN_GUIDE.md` ليطابق الحالة الفعلية.

---

## 3) مصفوفة الصلاحيات (ملخّص — من سمات `[Authorize]`)

> المصدر: سمات `[Authorize]` على كل متحكم. السياسة الموحدة `HeadOrSubHead` = `head` أو `subhead` (`Program.cs:231`). حارس `EntityManagerPortalGuard` يقصر دور `entitymanager` على البوابة.

| المتحكم | الحماية على مستوى الصنف | ملاحظات |
|---|---|---|
| `AuthController` | إجراءات الدخول/الخروج `[AllowAnonymous]`؛ الباقي (`me`/`change-password`) `[Authorize(Roles=...)]` | بوابة الدخول + تدوير توكن |
| `MetaController` | `[AllowAnonymous]` على `current-year` فقط | بيانات عامة مقصودة |
| `DocumentsController` | `lawyer,head,subhead,manager,admin` | — |
| `DelegationsController` | `[Authorize]` صنفي + أدوار لكل إجراء | — |
| `AppealsController` | `lawyer,head,subhead,manager,admin` (+ إجراء `lawyer` فقط) | — |
| `ExecutionCircuitsController` | `[Authorize]` + `HeadOrSubHead`/أدوار لكل إجراء | — |
| `StatisticsController` | `[Authorize]` + أدوار دقيقة لكل إجراء (بينها `manager,admin` للإحصاءات الحسّاسة) | — |
| `UserManagementController` | `head,subhead,manager,admin` | — |
| `AuditLogsController` | `manager,admin,head,subhead` | — |
| `PortalController` | `entitymanager` (صنفان: دخول + إدارة `manager,admin,head,subhead`) | مقصور بالحارس |
| `EntityRegistryController` | `lawyer,head,subhead,manager,admin` | — |
| `SectionsController` | `lawyer,head,subhead,manager,admin` | — |
| `ReviewLettersController` | `lawyer,head,subhead,manager,admin` | — |
| `CorrespondencesController` | `lawyer,head,subhead,manager,admin` | — |
| `HeadSuccessionController` | `manager,admin` | — |
| `AlertsController` | `lawyer,head,subhead,manager,admin` | قراءة إشرافية للمدير/المشرف (القائمة والمفرد فقط) بقرار المالك |
| `PersonalRemindersController` | `lawyer` | — |
| `ForumController` | `lawyer,head,subhead,manager,admin` | — |
| `AppSuggestionsController` | `lawyer,head,subhead,manager,admin` | — |
| `BranchesController` | `lawyer,head,subhead,manager,admin` | — |
| `ClientErrorsController` | `lawyer,head,subhead,manager,admin,entitymanager` | إبلاغ أخطاء |

**تحديث**: أُضيفت قراءة إشرافية للمدير/المشرف على القائمة والمفرد فقط بقرار المالك؛ وبقيت الكتابة والعدّادات والتعليم مقيدة بالمستلم.

---

## 4) أوجه قوة مؤكَّدة (بأدلة)

- **المصادقة**: جلسة `Cookie` بـ`HttpOnly`/`Secure`(غير التطوير)/`SameSite=Strict` (`backend/src/DocGenerator.Api/Auth/AuthCookie.cs`)؛ لا توكن في `localStorage` (`frontend/src/api/client.ts:70-72`).
- **`JWT`**: فشل إقلاع عند سر أقصر من `32` بايت، تثبيت `HmacSha256`، `ClockSkew` دقيقة (`Program.cs:92-95,168-180`).
- **إبطال الجلسة**: `TokenVersion` + `IsActive` + تطابق `section_id` (`Program.cs:192-223`) — أي تغيير كلمة مرور/دور/فرع/شعبة يُبطل الرمز.
- **`CSRF`**: ازدواج `Cookie`(`docgen_csrf`) + ترويسة(`X-CSRF-Token`) بـ`FixedTimeEquals`، واستثناء الدخول/الخروج فقط (`CsrfMiddleware`، `client.ts:98-105`).
- **فصل `EntityManager`**: `EntityManagerPortalGuard` يقصره على `/api/portal` ومسارات الهوية/الإبلاغ (`Program.cs:331-335`).
- **تعقيم النص المنسّق**: `DOMPurify` بقائمة بيضاء صارمة + ترشيح خصائص CSS (`frontend/src/utils/richText.ts`)، ومنتدى/إشارات بلا `dangerouslySetInnerHTML` (نص `React` فقط).
- **ترويسات أمن**: بلا بصمة `Server`، `CSP`/`X-Content-Type-Options`/`X-Frame-Options`، سقف طلب `10MB`، `MaxDepth=32` (`Program.cs:29-39،287`؛ `SecurityHeadersMiddleware`).
- **تحديد المعدل**: عام لكل مستخدم/`IP` + سياسات دخول/مكلف/كلمة مرور (`RateLimitingSetup`)، ومحدد دخول مدعوم بقاعدة `INSERT` ذرّي يمنع `TOCTOU` (`DbLoginRateLimiter.cs:29-33`).
- **معالج استثناءات آمن**: بلا تفاصيل داخلية في الإنتاج، رسائل `4xx` مُعقَّمة (`GlobalExceptionHandler.cs:74-90`؛ `LogSanitizer`).
- **`ForwardedHeaders`** لا تُحترم إلا من وكلاء معروفين (مغلق افتراضيًا) (`ForwardedHeadersSetup`).
- **بيانات نظيفة التسريب**: لا متحكم يعيد كيان `EF` مباشرة، ولا أسرار في الاستجابات (تدقيق مسار الاستجابة).
- **النمط الزمني**: كل كتابة `DateTime` بـ`UtcNow` عبر `UtcDateTimeConverter`؛ تحويل `datetime2→timestamptz` في سياق `Postgres` (`DocGeneratorPostgresDbContext`).
- **سكربت هجرات `Postgres` نظيف**: `0` occurrence لأنواع `SQL Server` الممنوعة (تحقق offline فعلي).

---

## 5) أمور تحتاج قرار المالك

1. **استراتيجية تطبيق الهجرات** (`H-02`): حُسمت — إبقاء التلقائي مع قفل `EF Core` المدمج ونسخة إلزامية؛ واليدوي مساند للنوافذ الحرجة فقط.
2. **سياسة كلمة المرور** (`M-02`): حُسمت — حد `10` + قائمة ممنوعة + منع مطابقة اسم الدخول، بلا استثناء للتأسيس.
3. **حوكمة سجلات `PII`** (`M-03`): من يملك الوصول، ومدة الاحتفاظ، وهل تُعمّى حقول معينة في `AuditLog.Details`.
4. **`AlertsController` مع `manager/admin`** (§3): نُفّذت قراءة إشرافية للقائمة والمفرد فقط بقرار المالك.
5. **دور `entitymanager`**: تأكيد أن نطاقه مقصور بحسب التصميم.

---

## 6) ما لم تتم مراجعته (صراحة)

- **`PostgreSQL` حيّ**: تطبيق هجرات/تشغيل/اختبارات `timestamptz` (لا `docker`/`psql`). ⚠️
- **تغطية اختبار الواجهة لكل الشاشات**: نُفّذت الاختبارات الآلية، لكن الفحص البصري على مقاس جوال (`375px`) لكل الشاشات لم يُؤمَت.
- **سلوك مقدمي الاستضافة**: لم يُختبر نشر فعلي على `Render` أو غيره.
- **أداء تحت حمل `300` مستخدم / `10000` ملف سنويًا**: لا اختبار حمل موثّق هنا.
- **`WordTemplates`**: وجود القوالب الفعلية في مسار النشر لم يُتحقَّق (ملفات `.docx`).
