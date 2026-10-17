# تقرير جاهزية الإنتاج — النسخة النهائية (v2) — نظام إدارة ملفات التنفيذ القضائي

> **النوع**: تدقيق جاهزية إنتاج (Production Readiness Review) — وضع قراءة فقط.
> **التاريخ**: 2026-10-17. يُحلّ محل `READINESS-REPORT.md` المؤرّخ 2026-10-16 (يبقى محفوظًا كنسخة).
> **النطاق**: كامل المستودع (`backend/` + `frontend/`) — الخلفية `.NET 10`، الواجهة `React`/`Vite`.
> **حالة المستودع عند التدقيق**: فرع `master`، آخر commit `d549fa2` (Merge PR #41).
> **قاعدة الأدلة**: كل بند يحمل مسار الملف والسطر؛ ما لم يُتحقَّق منه موسوم ⚠️.
> **تحذير منهجي**: لا اعتماد على تقارير `docs/audit` السابقة كحقيقة — ما في هذا التقرير مبني على قراءة الكود والتنفيذ الفعلي في هذه الجلسة.

---

## 0) القرار التنفيذي (GO / NO-GO)

**القرار: GO مشروط — لا حواجز كود (Blockers = 0).**

الشروط كلها من نوع «ضبط نشر/حوكمة» وليست عيوب كود: استيفاء `DEPLOY-CHECKLIST-final.md` (ضبط بيئة النشر + فحص قاعدة البيانات قبل الدخول الفعلي) + إغلاق Medium النوعي `M-07` قبل فرض `CSP`.

> **التقييم**: المعمارية الأمنية قوية ومُثبتة باختبارات (مصادقة `Cookie` + `CSRF` + إبطال جلسات عبر `TokenVersion`، تفويض افتراضي إجباري، تحديد معدل، عزل `entitymanager` بنيويًا). كل البوابات خضراء: خلفية 1889 + 655 اختبارًا، واجهة 1584 اختبارًا، ومهام CI كاملة بما فيها `backend-postgres` على قاعدة `postgres:15` حية داخل CI.

### ملخص تنفيذي بلغة بسيطة (للمالك)

| النتيجة | العدد | البيان |
|---|---|---|
| Blockers | **0** | لا شيء |
| High | **0** (كانت 2 مغلقتا كودًا في 2026-10-16) | `H-01`/`H-02` مغلقتان باختبارات |
| Medium مفتوحة | **3** | `M-07` خط خارجي ضد CSP، `M-08` مندوب الجهة بلا تغيير كلمة سر، `M-06` فحص Postgres حي قبل الإطلاق |
| Low مفتوحة | **5** | `L-05` تحوّط سجل التدقيق، `L-06` وثيقة قديمة، `L-07` انحراف إصدار Node، `L-08` تعليقات عربية مشوّهة، `L-01` `AllowedHosts` |
| ضبط نشر (لا كود) | **3** | `M-04` `KnownProxies`، `M-05` `HSTS`/`HTTPS`/فرض `CSP`، `L-01` `AllowedHosts` |

**أخطر 5 مسائل (بترتيب الأثر)**:
1. `CSP` لا يُفرض افتراضيًا في الإنتاج (`Security:CspReportOnly=true`) مع خط خارجي مخالف للقاعدة في `frontend/src/index.css:1` — عند الفرض يتكسر الخط العربي صامتًا.
2. إغفال `Security__KnownProxies` على مخدّم ببروكسي عكسي = انهيار مفتاح تحديد محاولات الدخول (`IP:user`) وتشوّه سجلات التدقيق.
3. نسيان `Bootstrap__AdminPassword` أو `Jwt__Secret` = رفض إقلاع (سلوك مقصود) — يجب تجهيزهما مسبقًا عند النشر.
4. مندوب الجهة (`entitymanager`) لا يستطيع تغيير كلمة سرّه بنفسه (`EntityManagerPortalGuard.cs:29-36`) — عبء تشغيلي روتيني على المشرف.
5. وثيقة `RUN_GUIDE.md:47` تقول «كلمة السر `123456`» بينما البذر الفعلي يولّد كلمة عشوائية تُطبع مرة على وحدة التحكم (`DbSeeder.cs:26-30`) — تفسير قد يربك مشغّل النشر.

**أهم 5 أمور لا تُتحقق إلا في بيئة نشر حقيقية**:
1. سلوك المضيف النهائي فعليًا (TLS، زمن النشر، قاعدة بيانات مُدارة، فاتورة خطة الاستضافة).
2. أول إقلاع على قاعدة `Postgres` ببيانات حقيقية (الديناميك: القيود والفهارس وأنواع التوقيت) — CI يعمل على قاعدة فارغة فقط.
3. سلوك مجلد السجلات `logs/` في حاوية المضيف (أذونات الكتابة، الاحتفاظ، الحجم).
4. أداء الحمل الفعلي (~200 مستخدم، ~2000 ملف سنويًا — بعض الوثائق تذكر 300/10000 لأفق أوسع).
5. الفحص البصري على جوال حقيقي (تجاوز أفقي، أهداف لمس، تنقل سفلي) — لا أتمتة Playwright هنا.

---

## 1) نتائج البوابات الآلية (منفَّذة فعليًا في هذه الجلسة)

| البوابة | الأمر | النتيجة | الوسم |
|---|---|---|---|
| بناء الخلفية `Release` | `dotnet build -c Release` | ✅ نجح — 0 أخطاء / 0 تحذيرات | ✅ |
| اختبارات التطبيق | `dotnet test` (Application.Tests) | ✅ `1889` نجح (12 دقيقة) | ✅ |
| اختبارات الواجهة الخلفية | `dotnet test` (Api.Tests) | ✅ `655` نجح (4 دقائق) | ✅ |
| بناء الواجهة | `npm run build` (Vite) | ✅ نجح | ✅ |
| TypeScript | `npx tsc -b` | ✅ نظيف | ✅ |
| Lint | `npx oxlint src` | ✅ 0 خطأ / 0 تحذير | ✅ |
| اختبارات الواجهة | `npx vitest run` | ✅ `1584` نجح عبر 151 ملفًا | ✅ |
| ثغرات npm | `npm audit` | ✅ 0 ثغرات | ✅ |
| ثغرات NuGet | `dotnet list package --vulnerable --include-transitive` | ✅ لا حزم مصابة | ✅ |
| حزم قديمة | `dotnet list package --outdated` | ⚠️ عدة حزم قديمة غير مصابة (`L-03`) | ⚠️ |
| CI كاملة | `gh run view 38976397943` | ✅ كل jobs خضراء: `backend`, `frontend`, `guards`, `audit`, `migration-guard`, `backend-postgres` | ✅ |
| مهمة `backend-postgres` | اختبارات عبر `DOCGEN_TEST_POSTGRES` على `postgres:15` خدميًا في CI (`ci.yml:112-138`) | ✅ | ✅ |
| حارس الهجرات | سكربت هجرات Postgres offline + فحص "لا `datetime2`" (`ci.yml:87-110`) | ✅ نظيف | ✅ |
| تاريخ المستودع | `docgen.db` لم يُرفع يومًا؛ `appsettings.Development.json` غير مُتتبَّع؛ `frontend/package-lock.json` مُتتبَّع | ✅ | ✅ |

**ملاحظة بيئية**: `docker`/`psql` غير متوفرين محليًا؛ الاختبار الفعلي لـ`PostgreSQL` أنجز على مستوى الاختبارات عبر مهمة CI `backend-postgres`. فحص قاعدة الإنتاج نفسها قبل الإطلاق يبقى إجراءً تشغيليًا (شروط النشر).

---

## 2) الملاحظات (Findings)

### 2.1) المغلقة منذ الجلسة السابقة (تُتحقق إرشاديًا في هذه الجلسة)

| ID | الموضوع | الحالة | الدليل |
|---|---|---|---|
| `H-01`(+`H-01b`) | منع الوقوع الصامت على SQLite في الإنتاج | ✅ مغلق كودًا | `Program.cs:53-62` (الرفض خارج Development بلا UsePostgres/DATABASE_URL + استثناء `AllowSqliteInProduction`)؛ `Program.cs:42-46` (DATABASE_URL الفارغة = غائبة) |
| `H-02` | تطبيق الهجرات تلقائيًا | ✅ مغلق بقرار موثق | `DatabaseInitializer.cs:27` (MigrateAsync مع قفل الهجرات المدمج في EF Core) + اشتراط نسخة احتياطية قبل النشر (RUN_GUIDE §9) |
| `M-01` | تفويض افتراضي إجباري | ✅ مغلق كودًا | `app.MapControllers().RequireAuthorization()` + `[AllowAnonymous]` الموثقة؛ اختبار `AuthzDefaultsTests` لكل الإجراءات بالانعكاس |
| `M-02` | سياسة كلمة المرور | ✅ مغلق كودًا | `PasswordPolicy` (حد 10، قائمة ممنوعة، منع مطابقة اسم الدخول) موحدة في التغيير/الإنشاء/التأسيس (`AuthService.cs:198-204`, `DbSeeder.cs:88-89`) |
| `M-03` | حوكمة PII في السجلات | ✅ مغلق حوكمةً | احتفاظ 60 يومًا + وصول محصور (commit `5fb89c1`; RUN_GUIDE §9) |
| `L-02` | مسار npm audit | ✅ مغلق | `ci.yml:80` يشغّله من `frontend/` |

### 2.2) المفتوحة — بترتيب الخطورة

#### M-07 — خط `Cairo` مستورد خارجيًا يخالف قاعدة `CSP` (Medium)

- **الدليل**: `frontend/src/index.css:1` — `@import url('https://fonts.googleapis.com/css2?family=Cairo:...')`.
- **السيناريو الفاشل**: مع `Security:CspReportOnly=false` (الهدف النهائي الموثق في RUN_GUIDE §9) تُحجب الاستضافة الخارجية صامتًا ويعرض النظام بخط النظام الاحتياطي — لا إشارة ظاهرة للمستخدم. في الوضع الحالي (Report-Only) لا كسر وظيفي، وتظهر مخالفات القاعدة في console المطوّر فقط.
- **الإصلاح الموصى به**: استضافة الخطوط ذاتيًا (`frontend/public/fonts/` + `@font-face`) — يلغي طلبًا خارجيًا ويحسن الخصوصية والأداء. البديل الأقل تفضيلًا (ولو أسهل): إضافة `fonts.googleapis.com`/`fonts.gstatic.com` إلى `style-src`/`font-src` في `CSP`.
- **التحقق**: بعد الإصلاح لا توجد طلبات شبكة للاستضافة الخارجية عند `Content-Security-Policy` مفروض.

#### M-08 — مندوب الجهة لا يستطيع تغيير كلمة سرّه (Medium)

- **الدليل**: `backend/src/DocGenerator.Api/Middleware/EntityManagerPortalGuard.cs:29-36` — المسارات المسموحة: `/api/portal` و `/api/auth/me` و `/api/auth/logout` و `/api/client-errors` و `/api/meta` فقط، خارجها 403.
- **السيناريو**: مستخدم `entitymanager` يفشل في `/api/auth/change-password` بالرغم من أن النقطة نفسها تعمل وزيادة: `AuthService.ChangePasswordAsync` (`AuthService.cs:193-214`) تدعم كل الأدوار (تحقق كلمة السر القديمة + `PasswordPolicy` + رفع `TokenVersion` + تسجيل تدقيق). النتيجة: كل مندوب يحتاج المشرف لكل تغيير كلمة سر.
- **الإصلاح المقترح (قرار تصميمي واحد)**:
  - (أ) إضافة `path.StartsWithSegments("/api/auth/change-password")` إلى القائمة في `EntityManagerPortalGuard.cs` — آمن لأن النقطة تتطلب كلمة السر القديمة وتخضع لـ`PasswordPolicy` والتحديد المعدلي (المفضَّل)، أو
  - (ب) توثيق قرار «المشرف وحده يغيّر كلمات سر المناديّ» كعمد تعاقدي.
- **التحقق**: اختبار تكامل — مندوب يغيّر كلمة سره بنجاح بقية الأدوار دون انحدار.

#### M-06 — فحص `PostgreSQL` ببيانات حية قبل الإطلاق (Medium)

- **الحالة**: كودًا مكتمل في CI (`backend-postgres` على `postgres:15` + `migration-guard`)، لكن أول إقلاع على قاعدة نشر حقيقية (لا بيانات CI الفارغة) يبقى أول فحص فعلي للمخطط مع البيانات؛ العينات الحرجة: القيود الفريدة الجزئية (`RF009`)، قيود `Check` (`PB002`، `SUBHEAD`)، ومشغّلات سجل التدقيق إلحاق-فقط (`RF019`).
- **الإجراء (RUN_GUIDE §9)**: قاعدة مرحلية → `dotnet ef database update --context DocGeneratorPostgresDbContext` → فحص `__EFMigrationsHistory` → تجريب إدخال مكرر (المتوقع 409) + تعديل صف تدقيق (المتوقع فشل).
- **التحقق**: سجل خضرة مُرفق بالمهمة.

#### M-04 / M-05 / L-01 — ضبط نشر يعتمد المضيف (Medium/Low — لا كود)

- **الدليل**: `appsettings.json:16,27` — `AllowedHosts: "*"` و`KnownProxies: []`؛ و`appsettings.json:26-31` — `HstsEnabled=false`, `HttpsRedirectionEnabled=false`, `CspReportOnly=true`.
- **التقييم**: الافتراضيات مقصودة لمنصة تدير TLS (كـRender) وهي الأمان المشروط، لكنها يجب أن تُضبط على المخدّم الخاص: `HstsEnabled=true` (مع preload بعد التأكد HTTPS كامل)، `HttpsRedirectionEnabled=true`، `CspReportOnly=false` (بعد جولة مراقبة)، `KnownProxies` بالعناوين/CIDR، `AllowedHosts` بالنطاق الحقيقي.
- **التحقق**: `curl -I` للترويسات بعد النشر + ظهور عناوين العملاء الحقيقية في السجلات.

#### L-05 — نطاق `scopeBranchId` الفارغ في سجل التدقيق (Low — تحكيم دفاعي فقط)

- **الدليل**: `backend/src/DocGenerator.Api/Controllers/AuditLogsController.cs:33` — إن كان الدور `head`/`subhead` و`BranchId` فارغًا → النطاق `null` → رؤية كل السجلات.
- **تقييم الخطورة (لماذا Low)**: هذا لا يحدث عمليًا بسبب ثلاث طبقات:
  1. قيد القاعدة `CK_Users_BranchRequiredForBranchRoles` (`UserConfiguration.cs:19-20`؛ migrations `RequireBranchForBranchRoles*`) يمنع وجود `Lawyer/Head/SubHead` بلا فرع أصلًا.
  2. عند الدخول، `AuthService` يرفض حسابات `Head` بلا فرع (`login_branch_required`).
  3. أي تغيير دور/فرع يرفع `TokenVersion` وإبطال الجلسة القديمة في `OnTokenValidated`.
- **الإصلاح**: بند في `FIX-PLAN-final.md` — مستوى الإجراء: إن كان الدور `head`/`subhead` والنطاق فارغ → أعد نتيجة فارغة/404 بدل إجمالية (دفاع مواز للطبقات الثلاث).
- **التحقق**: اختبار يُنشئ حالة (عبر `DbContext` مباشرة بتجاوز القيد؟) أو وحدة موك — يجعل السلوك صريحًا.

#### L-06 — وثيقة `RUN_GUIDE` قديمة حول كلمة سر البذر (Low)

- **الدليل**: `RUN_GUIDE.md:47` — «كلمة السر للجميع `123456`»؛ بينما `DbSeeder.SeedAsync` (`DbSeeder.cs:26-30`) يولّد كلمة عشوائية ويطبعها مرة على وحدة التحكم فقط.
- **الإصلاح**: تحديث §4 للوصف الفعلي (كلمة عشوائية مرة واحدة).
- **التحقق**: مراجعة نصية.

#### L-07 — انحراف إصدار `Node` بين مسارات البناء (Low)

- **الدليل**: `Dockerfile:2` `node:20-alpine` مقابل CI Node 24 وفِعليًا `node v24.18.1`؛ لا حقل `engines` في `frontend/package.json`.
- **الإصلاح**: توحيد المعقول — ترقية `Dockerfile` إلى `node:24-alpine` أو تسجيل الحد الأدنى في `engines`. إجراء منخفض الأولوية لأن كل المسارات خضراء حاليًا.
- **التحقق**: بناء Docker نجاح.

#### L-08 — تعليقات عربية مشوّهة (mojibake) في أدلة الكود (Low — تجميلية)

- **الدليل**: موضعيًا في عدة ملفات (`AuthService.cs:214`, `DbSeeder.cs:12,73,83`, `RolePermissions.cs`, `SecurityOptions.cs`, `ClientErrorsController.cs`, `Program.cs`). لا يؤثر على السلوك.
- **الإصلاح**: إصلاح تدريجي مع ملامسة الملفات (باتش وحدات)، لا دفعة كبيرة.
- **التحقق**: كبير بصري.

#### L-03 — حزم قديمة غير مصابة (Low — دورية)

- **الدليل**: `dotnet list package --outdated` — `AngleSharp` 1.7.1→1.8.5، `DocumentFormat.OpenXml` 3.0.2→3.5.1، `HtmlSanitizer` 9.1.982→9.2.1039، `Microsoft.Extensions.*` 10.0.10→10.0.13.
- **الإصلاح**: دورة ترقية محدودة واعتيادية، ومُنفذة في نافذة نجاح بوابات كاملة.
- **التحقق**: البوابات الأربع بعد الترقية.

---

## 3) مصفوفة الصلاحيات (endpoint × role) — الدقيق

> **المصدر**: grep كامل لسمات `[Authorize]`/`[AllowAnonymous]` في `Controllers/` + `EntityManagerPortalGuard.cs` + ترتيب الميدل وير (`Program.cs:348-359`: Authentication → PortalGuard → Authorization → RateLimiter).
> **الأدوار الستة**: `lawyer`, `head`, `subhead`, `manager`, `admin`, `entitymanager`.
> **السياسة المعرفة**: `HeadOrSubHead` = `head` أو `subhead` (`Program.cs:231`).
> **القاعدة الفعّالة**: `app.MapControllers().RequireAuthorization()` — أي إجراء بلا سمة يبقى محميًا افتراضيًا.

| المتحكم | المسار الأساس | الأدوار المسموحة | ملاحظات |
|---|---|---|---|
| `AuthController` | `/api/auth` | مجهول: `login`(=46), `logout`(=103)؛ كل الـ6 أدوار: `me`(=117), `change-password`(=135) | login/logout مجهولان عمدًا؛ محدودة معدليًا |
| `MetaController` | `/api/meta` | مجهول على `current-year` فقط (=`27`) | سنة النظام العامة مقصودة |
| `MapHealthChecks` | `/healthz` | مجهول | شفافية التشاخص لا تعرض معلومات حساسة |
| `DocumentsController` | `/api/documents` | `lawyer,head,subhead,manager,admin` (صنف, =16) | النطاق الداخلي حسب الدور/الفرع/الملكية |
| `DelegationsController` | `/api/delegations` | صنفي `[Authorize]` (=18) + إجراءات لكل أمر: `lawyer,head,subhead,manager,admin` | — |
| `AppealsController` | `/api/appeals` | صنفي 5 أدوار (=17) + إجراء `lawyer` فقط (=554) | — |
| `ExecutionCircuitsController` | `/api/execution-circuits` | صنفي `[Authorize]` (=15) + إجراءات محددة: `HeadOrSubHead` أو `lawyer,head,subhead` أو `manager,admin` أو مجهول (275,298,313,320) | المسارات المجهولة مقصودة وقرائية (تصفح/تبويب) |
| `StatisticsController` | `/api/statistics` | صنفي `[Authorize]` (=16) + إجراءات دقيقة: `manager,admin,head,subhead,lawyer` أو `lawyer` فقط أو `manager,admin` | النطاق يمنع تسريب إحصائيات الفروع (RF-007) |
| `UserManagementController` | `/api/users` | `head,subhead,manager,admin` (=12) | قواعد الوكالة من `00-PROJECT-CONTEXT.md` §2-§8 |
| `AuditLogsController` | `/api/audit-logs` | `manager,admin,head,subhead` (=12) | نطاق `manager/admin` = إجمالي؛ `head/subhead` = فرعهم (`:33`) — والتحوّط `L-05` |
| `PortalController` | `/api/portal` | `entitymanager` (=20) + إدارة `manager,admin,head,subhead` (=306) | الحارس بنيوي فوقه (`Program.cs:353`) |
| `EntityRegistryController` | `/api/entities` | `lawyer,head,subhead,manager,admin` (=20) | — |
| `SectionsController` | `/api/sections` | `lawyer,head,subhead,manager,admin` (=13) | — |
| `ReviewLettersController` | `/api/review-letters` | `lawyer,head,subhead,manager,admin` (=18) | — |
| `CorrespondencesController` | `/api/correspondences` | `lawyer,head,subhead,manager,admin` (=19) | — |
| `HeadSuccessionController` | `/api/head-succession` | `manager,admin` (=9) | — |
| `AlertsController` | `/api/notifications/alerts` | `lawyer,head,subhead,manager,admin` (=14) | قراءة إشرافية للمدير/المشرف على القائمة والمفرد بقرار المالك |
| `PersonalRemindersController` | `/api/reminders` | `lawyer` (=14) | تذكيرات شخصية حصرًا |
| `ForumController` | `/api/forum` | `lawyer,head,subhead,manager,admin` (=19) | — |
| `AppSuggestionsController` | `/api/app-suggestions` | `lawyer,head,subhead,manager,admin` (=14) | — |
| `BranchesController` | `/api/branches` | `lawyer,head,subhead,manager,admin` (=12) | — |
| `ClientErrorsController` | `/api/client-errors` | كل الـ6 أدوار بالتساوي (=38) | نقطة الإبلاغ عن الأعطال؛ مخنوقة لكل مستخدم |

**الحارس الإضافي فوق الجدول** (ينفَّذ قبل `UseAuthorization` في `Program.cs:353`): مستخدم `entitymanager` خارج المسارات المسموحة (`/api/portal`, `/api/auth/me`, `/api/auth/logout`, `/api/client-errors`, `/api/meta`) → 403 فوري + سجل تدقيق مخنوق (5 دقائق/مستخدم) (`EntityManagerPortalGuard.cs:29-36,56-68`).

**المجهول (الوحيد المقصود)**: `login`, `logout`, `current-year`, `/healthz`, احتياطي SPA.

---

## 4) أوجه قوة مؤكَّدة (بأدلة الجلسة)

- **إبطال الكلمة يقتل الجلسات الأخرى**: `AuthService.ChangePasswordAsync` يرفع `TokenVersion++` بعد تحقق الكلمة القديمة والسياسة (`AuthService.cs:193-214`).
- **تفويض افتراضي**: `app.MapControllers().RequireAuthorization()` + اختبار `AuthzDefaultsTests` بالانعكاس يؤكد أن كل إجراء إما محمي بسمة أو مجهول صراحةً — لا تعتمد على الأعمدة الافتراضية الصامتة.
- **الجلسة المخبأة في Cookie فقط**: `HttpOnly` + `Secure`(غير التطوير) + `SameSite=Strict` (`Auth/AuthCookie.cs`)؛ لا توكن في `localStorage` (`frontend/src/api/client.ts:70-72`).
- **CSRF**: تبريح ثنائي `docgen_csrf` cookie + ترويسة `X-CSRF-Token` بمقارنة `FixedTimeEquals`؛ استثناء login/logout فقط (`CsrfMiddleware`; `client.ts:98-105`).
- **رفض حواف بيئة الإنتاج**: حراس الإقلاع صارمون (رفض المخطط SQLite في الإنتاج `Program.cs:53-62`؛ سر `Jwt:Secret` أقصر من 32 بايت يرفض الإقلاع).
- **سقف التصعيد**: `MaxRequestBodySize=10MB` (`Program.cs:35`) + `MaxDepth=32` (حماية من الفَرق العميق) + `X-Content-Type-Options`/`X-Frame-Options`/CSP.
- **حاوية بصلاحية غير جذرية**: `Dockerfile:25-27` — `mkdir /app/logs && chown -R app:app` + `USER app`.
- **وكيل عكسي بأمان**: لا تُحترم ترويسة `X-Forwarded-For` إلا من `KnownProxies` المعروفين (`ForwardedHeadersSetup`; `Program.cs:255-259,303`) — الافتراضي مغلق.
- **البذر الإنتاجي**: `BootstrapAsync` يستلزم `Bootstrap__AdminPassword` وفق `PasswordPolicy.EnsureValid` (`DbSeeder.cs:81-89`)؛ بذر التطوير يولّد كلمة عشوائية مطبوعة لمرة واحدة، ولا يُقبل `Bootstrap:DevSeedPassword` إلا للاختبارات (RF-006/SEC-004).

---

## 5) Phase 14 — الأسئلة التي لم نسألها (سيناريوهات)

| # | السؤال | الإجابة المُسنَدة من الواقع |
|---|---|---|
| 1 | مندوب جهة يحتاج تغيير كلمة سرّه؟ | محجوب حاليًا بحارس البوابة (`M-08`) — رغم أن مسار التغيير آمن ويعمل لبقية الأدوار (`AuthService.cs:193-214`). إصلاح مقترح في FIX-PLAN |
| 2 | رئيس قسم بلا فرع يفتح سجل التدقيق؟ | مستحيل عمليًا (قيد DB `UserConfiguration.cs:19` + رفض الدخول + إبطال التوكن) لكن يُصحَّح تحوطًا (`L-05`) |
| 3 | مستخدم مُعطَّل — هل تموت جلسته فورًا؟ | نعم — تغيير التعطيل/الدور/الفرع/الشعبة يرفع `TokenVersion` ويُبطل التوكن القديم (`Program.cs:192-223`) |
| 4 | تغيير كلمة سر من جهاز — هل تبقى الجلسات الأخرى حية؟ | لا — رفع `TokenVersion` يقتل كل الجلسات (`AuthService.cs:210`) |
| 5 | نقطة نهاية كتابة جديدة منسية التفويض؟ | الحماية الافتراضية `app.MapControllers().RequireAuthorization()` تُغطيها تلقائيًا + اختبار انعكاس يرفض أي إجراء بلا سمة (`AuthzDefaultsTests`) |
| 6 | تاريخ مستقبلي في بيانات الملف؟ | لم يُجرّب في هذه الجلسة (بند اختبار إضافي مطلوب في FIX-PLAN) |
| 7 | تدوير السنة مع رقم أساس مماثل؟ | الرقْم عبر RF-009 فريد داخل (الدائرة+الرقم+النوع+**السنة**) — سنة جديدة تفتح مساحة نظيفة دون تصادم |
| 8 | النقر المزدوج يكرر الطلب؟ | لا — ترويسة `X-Idempotency-Key` + جدول `IdempotencyKeys` (RF-011) يرجع النتيجة نفسها للطلب المكرر |
| 9 | لماذا عدم فرض CSP الآن أخطر من أي وقت؟ | `CspReportOnly=true` الحالي + الخط الخارجي (`M-07`) يقدّم انتهاكات مستمرة بلا كاسر وظيفي — قد يجعل بقاء وضع المراقبة دائمًا خيارًا مُفاجِعًا |
| 10 | تسريب سر عبر السجل خطأً؟ | `LogSanitizer` يقيّد ما يُمرَّر؛ ملفات السجلات لا تُعرَض عبر أي API — وصولها عبر المضيف لمشغّل النظام حصرًا (`M-03`)، وسجل التدقيق إلحاق-فقط (`RF019` مشغلات) |
| 11 | كلمات سر شائعة (`123456`/`password`)؟ | مرفوضة في `PasswordPolicy` (قائمة ممنوعة + حد 10) في كل مسارات الإنشاء/التغيير/التأسيس |
| 12 | تعديل/حذف سجل تدقيق باطنيًا؟ | مشغّلات RF-019 ترفض أي `UPDATE`/`DELETE` على `AuditLogs` و`DocumentFieldChanges` (فشل صريح عند الإلحاق المخالف) |

> بند `#6` (التاريخ المستقبلي) أُدرج كاختبار مطلوب في `FIX-PLAN-final.md` — لم يُتحقق سلوكه في هذه الجلسة، وإتمامه مراجعة صغيرة في منطق التحقق.

---

## 6) ما لم تتم مراجعته (صراحة)

- **نشر فعليّ على مضيف حقيقي** (التحقق من `TLS` وترويسات الوكلاء وسلوك المستخدم الفعلي) خارج نطاق هذه الجلسة.
- **`WordTemplates`**: وجود ملفات `.docx` الفعلية في مسار النشر ومُخرَجات الحاوية لم يُتحقَّق.
- **اختبار الحمل**: لا تحميل موجّه لسيناريو ~200 مستخدم / ~2000 ملف سنويًا موثَّق هنا.
- **فحص جوال بصري آلي** على مقاس `375px` (لم يُنفَّذ بأتمتة `Playwright`).
- **أول إقلاع على قاعدة `Postgres` حيّة ببيانات حقيقية** — محل الإجراء الإلزامي `M-06`.
- **استضافة الخطوط النهائية** بعد إصلاح `M-07`.
- **اختيار المضيف النهائي (والخطوة التعاقدية /** الفاتورة / النطاق) لم يُحسم بعد.

---

## 7) الخلاصة للمالك

- **الحالة**: لا blockers في الكود؛ المفتوح عمليًا = 3 Medium (`M-07`, `M-08`, `M-06` إجراء تشغيلي) + 5 Low أغلبها وثيقة/تنظيم.
- **قبل الإطلاق**: (1) استيفاء `DEPLOY-CHECKLIST-final.md` بندًا بندًا، (2) فحص `Postgres` حيّ (`M-06`)، (3) إصلاح `M-07` قبل فرض `CSP`، (4) قرار `M-08` (فتح المسار في الحارس أو توثيقه قرارًا مقصودًا)، (5) ضبط `M-04`/`M-05`/`L-01` على المضيف النهائي.
- **بعد الإطلاق**: مراقبة ردود `429` لضبط حدود المعدل مع الحمل الحقيقي، مراقبة مخالفات `CSP` قبل فرضه، دورية النسخ الاحتياطي (`RPO`=24h/`RTO`=4h)، وتنقيح الملاحظات في `docs/audit/production-readiness/` مع كل جلسة.

> **توصية**: الإطلاق مع القائمة بندًا بندًا — لا عائق تقني قائم، وما تبقى بنود ناضجة قابلة للحسم بأفعال محددة موثقةً في `FIX-PLAN-final.md`.
