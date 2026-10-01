# تقرير الاكتشاف — PROMPT 1 (DISCOVER)

> نظام إدارة ملفات التنفيذ القضائي — قراءة فقط. كل ادعاء بدليل `path:line` وموسوم الثقة: VERIFIED (قُرئ الكود) / INFERRED (استدلال من كود مقروء) / NOT VERIFIED (تعذر الفحص).
> السياق: `docs/00-PROJECT-CONTEXT.md:1-88` قُرئ كبديل لأن `docs/audit/00-PROJECT-CONTEXT.md` غير موجود — NOT VERIFIED لتطابقهما (انظر `BQ-020` في `business-questions.md`).

## 1. خريطة المستودع (VERIFIED)

- الجذر: `AGENTS.md` + `Dockerfile` + `render.yaml` + `RUN_GUIDE.md` + `CHANGELOG.md` + `.github/workflows/ci.yml` + `.snyk` + `start-app.bat` + `stop-app.bat`.
- `backend/DocGenerator.sln`: أربعة مشاريع `src/` + مشروعا اختبار `tests/` — VERIFIED (`backend/DocGenerator.sln:7-19`).
  - `backend/src/DocGenerator.Api` — متحكمات + `Program.cs` + `WordTemplates/*.docx` + `Middleware/*` + `Security/*` + `Authorization/RolePermissions.cs`.
  - `backend/src/DocGenerator.Application` — خدمات + `DTOs/*` + `Common/*` + `DependencyInjection.cs`.
  - `backend/src/DocGenerator.Domain` — كيانات `Entities/*` + `Enums/*`.
  - `backend/src/DocGenerator.Infrastructure` — ثبات `Persistence/*` + `Security/TokenService.cs,DbLoginRateLimiter.cs`.
  - `backend/tests/DocGenerator.Api.Tests` + `backend/tests/DocGenerator.Application.Tests`.
- `frontend/`: تطبيق `Vite/React` — `src/App.tsx` (توجيه) + `src/pages/` + `src/components/` (`Layout`, `view/`, `form/`, `dashboard/`, `appeal/`, `correspondence/`, `delegation/`, `entity/`, `review/`, `portal/`) + `src/api/client.ts` + `src/auth/*` + `src/hooks/*` + `src/utils/*` + `src/types/index.ts`.
- `docs/`: السياق والخطط (`00-PROJECT-CONTEXT.md`, `01-DISCOVER.md` … `04-REFACTOR.md` + ~30 خطة `*-PLAN.md`).

## 2. مقاييس الحجم (VERIFIED)

الأوامر المستخدمة (PowerShell 5.1): `Get-ChildItem backend -Include *.cs -Recurse | Where-Object { $_.FullName -notmatch "\\bin\\|\\obj\\" }` وتصفية `Migrations` للإنتاجي، و`Get-ChildItem frontend/src -Include *.ts,*.tsx -Recurse` مع فصل `*.test.*`، و`Get-ChildItem .../Controllers -Filter *Controller.cs`.

| البند | القيمة |
|---|---|
| ملفات `backend` الكلية (`*.cs` بلا `bin/obj`) | 564 ملفًا، ~334359 سطرًا (يشمل الهجرات المولدة) |
| ملفات `backend/src` الإنتاجية بلا هجرات ولا `bin/obj` | 219 ملفًا |
| ملفات اختبارات الخلفية بلا `bin/obj` | 105 (43 `Api.Tests` + 62 `Application.Tests`) |
| ملفات `frontend/src` الكلية | 322 (194 إنتاجية + 128 اختبارًا) |
| سطور `frontend/src` الكلية | ~61315 |
| المتحكمات | 17 ملفًا / 18 صنفًا (`PortalController.cs:342-345` يحوي `DelegatesController`) |
| النقاط | 184 (انظر `01-endpoints.md`) |
| الكيانات | 40 صنفًا + واجهة `IDocumentExecutionState.cs:7` |
| ملفات `Enums` | 18 |
| خدمات `Application/Services/` | 33 ملفًا على القرص (منها `DocumentService` في 6 ملفات جزئية) |
| الهجرات SQLite | 72 منطقية + 72 `Designer` + 1 snapshot |
| الهجرات Postgres | 47 منطقية + 47 `Designer` + 1 snapshot |
| صفحات الواجهة الإنتاجية | ~36 (`pages/*.tsx` بلا اختبارات؛ 74 مع الاختبارات) |
| قوالب `Word` | 8 معلنة في `appsettings.json:58-70` (`001,002,003,004,005,006,007,PS`) و9 ملفات `*.docx` على القرص — INFERRED وجود ملف زائد يستحق المطابقة |

## 3. أكبر الملفات (VERIFIED)

خلفية إنتاجية (بلا هجرات): `PublicEntityService.cs` (4068) ثم `Configurations.cs` (1383) ثم `DocumentDelegationService.cs` (~1190) ثم أجزاء `DocumentService*.cs` (~1104/1103/1043/942 — مجموعها الفعلي > 4000) ثم `DocumentDtos.cs` (1014) ثم `CorrespondenceService.cs` (900) ثم `StatisticsRepository.cs` (849) ثم `DocumentContextBuilder.cs` (832) ثم `DocumentRepository.cs` (814) ثم `DocumentsController.cs` (717) و`EntityRegistryController.cs` (702).
واجهة: `DocumentView.test.tsx` (2518، اختبار) و`types/index.ts` (2153) و`DocumentsList.test.tsx` (1663، اختبار) ثم `EntityRegistryReviewManagement.tsx` (1515، أضخم صفحة إنتاجية) و`DocumentForm.tsx` (1272) و`DocumentsList.tsx` (897) و`DocumentView.tsx` (863).

## 4. المخزون التقني (VERIFIED من ملفات المشروع)

- `React ^19.2.8` + `react-dom ^19.2.8` + `react-router-dom ^7.18.2`، بناء `vite ^8.2.0` + `tsc -b` (`frontend/package.json`).
- لا إدارة حالة مركزية — `useState/Context` محلي + `AuthContext` (`frontend/src/auth/AuthContext.tsx:6-58`) + `CurrentYearContext` (`hooks/useCurrentYear.ts:9-19`) — VERIFIED (لا `redux/zustand` في `package.json`).
- عميل `HTTP` وحيد `axios ^1.19.0` (`frontend/src/api/client.ts:29-112`) مع `CSRF` من كوكي `docgen_csrf` وإعادة محاولة واحدة عند `429` لـ `GET/HEAD`.
- `tailwindcss ^4.3.3` + `TipTap ^3.31.3` + `react-calendar ^6.0.1` + `dompurify ^3.4.13` — VERIFIED.
- `net10.0` في المشاريع الستة، `sdk:10.0`/`aspnet:10.0` في `Dockerfile`، `node:20-alpine` للواجهة — VERIFIED.
- `EF Core 10.0.10` (`Sqlite` + `Npgsql.EntityFrameworkCore.PostgreSQL 10.0.3`) — VERIFIED. نسخة خادم `PostgreSQL` غير معلنة — NOT VERIFIED.
- المصادقة `JwtBearer 10.0.10` + `Cookie HttpOnly` (`Program.cs:152-199`، القراءة `Program.cs:172-177`، الإبطال `Program.cs:194-196`، حارس السر `>=32 bytes` في `Program.cs:93-95`، `ValidAlgorithms=[HmacSha256]` فقط) — VERIFIED.
- السجلات `Serilog.AspNetCore 10.0.0` + `Sinks.File 7.0.0` — VERIFIED. لا مهام مجدولة (`Hangfire/Quartz`) — NOT VERIFIED وجودها (لا أثر في `Program.cs`).
- المستندات `DocumentFormat.OpenXml 3.0.2` + `HtmlSanitizer 9.1.982` + `AngleSharp 1.7.1` — VERIFIED. لا تكاملات `SMS`/بريد — NOT VERIFIED (انظر `BQ-018`).
- الاختبارات: `xunit 2.5.3` + `Mvc.Testing 10.0.10` + `TimeProvider.Testing 9.8.0`؛ واجهة `vitest ^4.1.10` + `jsdom` — VERIFIED.

## 5. البنية (INFERRED من كود مقروء)

- طبقات نظيفة معلنة: `Api -> Application -> Domain` و`Infrastructure -> Application+Domain` — VERIFIED التسجيل (`Program.cs:143-144` و`Application/DependencyInjection.cs:11-42` و`Infrastructure/DependencyInjection.cs:12-54`).
- المتحكمات نحيفة (مصادقة/تفويض + تحويل `HTTP`) — VERIFIED (`DocumentsController.cs:16,50-78`).
- القواعد في الخدمات (`DocumentService*.cs` + `DocumentValidator.cs` + `DocumentStatusResolver.cs` + `DelegationActivityPolicy.cs` + `ActionDateParser.cs`) — VERIFIED؛ الكيانات `POCO` فقيرة (`Document.cs:1-60` بلا سلوك، `IsDraft=true:24`) — VERIFIED.
- المستودعات تمريرات `EF` رقيقة + `IUnitOfWork` + `ITransactionRunner` — INFERRED (تحتاج PROMPT 3 لتقييم الجدوى).
- الشذوذات: إعدادات `EF` كلها في ملف واحد (`Configurations.cs:1-1383`)، و`DocumentService` موزع على 6 ملفات، و`PortalController.cs` يحوي صنفين، ومنطق عرض مكرر في الواجهة (`viewFormat.ts` يعاكس `EffectiveFileIdentity`) — VERIFIED المواقع، INFERRED التقييم.

## 6. خريطة الاعتماديات (VERIFIED المواقع)

- واجهة: `pages/*` → `api/*` + `client.ts` → `/api` → متحكمات. لا مخزن مركزي؛ `useCancellableRequest` (`hooks/useCancellableRequest.ts:59-108`) للإلغاء.
- خلفية: متحكمات → خدمات (`AddScoped` ~19 في `DependencyInjection.cs:22-41`) → مستودعات (`IRepository<>` + ~15 مستودعًا) → `DbContext` → `SQLite/Postgres`؛ `WordDocumentGenerator` → `IDocumentContextBuilder` + `IDocumentRenderer` (`WordDocumentGenerator.cs:6-23`)؛ التدقيق `AuditLogger` (`Infrastructure/Persistence/AuditLogger.cs:9-28`).
- لا اعتماديات دائرية مثبتة — NOT VERIFIED (تحتاج تحليلًا آليًا في PROMPT 4). لا كشف شبكي لـ `EF` عبر `API` مباشرة (الردود `DTOs`) — INFERRED من `DocumentResponse.FromEntity`.

## 7. خريطة الدومينات (INFERRED من كيانات + خدمات + نقاط مقروءة)

- ملفات (`Document` + `DocumentBaseNumber` + `DocumentRegistrationDate` + `DocumentAssignment` + `DocumentOccurrence`) — المركز.
- أطراف (مقترض/كفلاء/ورثة/أموال/مالكون: `Guarantor, Asset, AssetOwner, Heir`) وطالبو التنفيذ والمنفذ عليهم (`ExecutionApplicant, ExecutedNaturalPerson, ExecutedPublicEntity, ExecutedHeir, ApplicantPublicEntity`).
- إجراءات (`ExecutionAction` + `ActionDateParser`) وتذكيرات (`PersonalReminder` + تقويم المحامي).
- إنابات (`DocumentDelegation` + `DelegationAsset` + `DelegationAssetReservation`) واستئنافات (`DocumentAppeal` + `AppealAction` + `AppealBaseNumber`).
- مراسلات (`Correspondence` + رسائل + إيصالات) وكتب مطالعة (`ReviewLetter`) وتنبيهات (`HeadAlert`) وسجل جهات (`PublicEntityGroup/Entry/Alias/ChangeEvent` + اقتراحات).
- مستخدمون/فروع (`User, Branch, LoginAttempt`) وتدقيق (`AuditLog` + `DocumentFieldChange`) واقتراحات تطوير (`AppSuggestion`).

## 8. خريطة سير العمل (INFERRED — التفصيل الأمني في PROMPT 2)

- إنشاء/تعديل ملف: `POST/PUT api/documents` (محامٍ فقط `CanEditDocuments` في `RolePermissions.cs:12`) → `DocumentValidator` + `ParseDateTime` → حفظ + تدقيق. القواعد الافتراضية (عملات، صفة الملف) بعضها في الواجهة فقط (`documentFormConstants.ts` + `DocumentForm.tsx:67-76`) — INFERRED.
- تغيير الحالة: `POST .../status` + `revert-status` + `return-referred-to-start` + `consider-executed-by-delegation` + `executed-status` → آلة `ExecutionStatusCatalog.cs:149-167` (`AllowedStatusChanges/CanRevert`) → حقول تواريخ/مبالغ → تدقيق. الحالات النهائية (`ExecutedBySettlement`, `DelegationExecuted`, `Recovered`) بلا مخارج عبر الآلة — VERIFIED.
- شطب/تجديد: `struck-off` + `restore-struck-off` (`RenewalRequest`) → `StruckOffDate` + رقم جديد لسنة الإعادة (`DocumentService.Status.cs:820-844`) — INFERRED الذرية تحتاج PROMPT 3.
- حذف/استعادة منطقية: `DELETE` + `restore` (محامٍ فقط `CanDeleteDocuments` في `RolePermissions.cs:18`) مع فلتر `HasQueryFilter(!IsDeleted)` (`Configurations.cs:72`) — VERIFIED.
- نقل: `transfer` + `transfer-all` (رئيس قسم فقط `CanTransferDocuments` في `RolePermissions.cs:41`) — VERIFIED التصريح، INFERRED الذرية.
- إنابة: تسطير (محامٍ `CanManageDelegations:59`) → اعتماد/إسناد (رئيس `CanApproveDelegations:65`) → تسجيل → إتمام/استرداد (حالات `DelegationExecuted/Recovered` لا تُختار يدويًا) — VERIFIED من الكتالوج والصلاحيات.
- استئناف: تسطير/حسم/شطب (محامٍ `CanManageAppeals:71`) وإسناد/نقل (رئيس `CanAssignAppeals:76`) — VERIFIED.
- مراسلات/مطالعة: إنشاء (محامٍ/رئيس/مندوب عبر بوابته) وردود — VERIFIED (`CanCreateCorrespondences:92-93`, `CanCreateReviewLetters:83`, `CanReplyReviewLetters:86`).
- تصدير/توليد: `GET .../export` + `GET .../generate?template&recipient&heirId&estateIds` (مكلفان خلف `ExpensivePolicy`) → `WordDocumentGenerator.GenerateAsync` يرفع `PrintCount` (`WordDocumentGenerator.cs:40-43`) — VERIFIED؛ لا تخزين ملفات مولدة على الخادم (بث `blob` + `downloadBlob`) — INFERRED.
- تدوير سنوي: `GET/PUT api/documents/rotate` (محامٍ فقط `CanRotate:31`) → `DocumentBaseNumber` متعدد الصفوف (`Configurations.cs:404-405`) — VERIFIED الهيكل، NOT VERIFIED الوحدانية على مستوى القاعدة (انظر `BQ-008`).

## 9. الواجهة — نظرة (VERIFIED المواقع)

- توجيه `lazy` + حراس `RequireAuth/RequireRole` (`App.tsx:11-108`)، حوالي 40 مسارًا (ملفات/استئنافات/مراسلات/إنابات/بوابة/إدارة/تقارير/تقويم) — VERIFIED.
- `RTL` عربي (`index.html:2` و`Layout.tsx:316`)، خط `Cairo`، أرقام معزولة `dir=ltr+tabular-nums`، بلا `i18n` (نصوص مثبتة) — VERIFIED.
- تواريخ نص حرة `type=text` + `placeholder="مثال: 1/8/2026"` + `normalizeArabicDigits().trim()` عند الإرسال — VERIFIED (`ApplicantSideSections`/`DelegationFormModal.tsx:106-112`).
- عرض الأرقام `Intl.NumberFormat('en-US')` فقط (`viewFormat.ts:24`, `dashboardFormat.ts:66`) — VERIFIED.
- مكونات عملاقة: `EntityRegistryReviewManagement.tsx` (1439) و`DocumentForm.tsx` (1272) و`DocumentsList.tsx` (862) و`DocumentView.tsx` (829)؛ `useEffect` ~100 موضع بأنماط جلب/إلغاء/اقتراع — VERIFIED.
- منطق متسرب للواجهة: `viewFormat.ts:282-460` (ملخص الحالة/الهوية) و`DocumentGenerationSection.tsx:169-245` (اختيار العقارات/المخاطبين) — INFERRED الازدواج مع الخلفية.

## 10. الخلفية — نظرة (VERIFIED المواقع)

- متحكمات نحيفة + خدمات سمينة؛ `DocumentService` وحده > 4000 سطر موزعة + `PublicEntityService` (4068) — VERIFIED.
- تحقق `DocumentValidator` نقي بلا تخزين (`DocumentValidator.cs:10-103`)؛ تواريخ عبر `ParseDateTime` → `FreeDateParser` (`DocumentValidator.cs:95-96`) بصيغ `d/M/yyyy…` + بديل مرن (`ActionDateParser.cs:16-38`) — VERIFIED.
- حالة العرض من `DocumentStatusResolver.Resolve` (`DocumentStatusResolver.cs:13-37`) + آلة `ExecutionStatusCatalog` (`ExecutionStatusCatalog.cs:128-168`) — VERIFIED.
- استثناءات مركزية (`GlobalExceptionHandler`) + تعقيم `HtmlSanitizer` + حدود `Kestrel 10MB` (`Program.cs:31-39`) — VERIFIED.
- `DI` و`async` سليمة ظاهريًا (`AddScoped` + `TransactionRunner`) — INFERRED (التدقيق العميق في PROMPT 4).

## 11. قاعدة البيانات — نظرة (VERIFIED المواقع)

- سياقان: `DocGeneratorDbContext` (40 `DbSet` في `DocGeneratorDbContext.cs:19-58`) و`DocGeneratorPostgresDbContext : DocGeneratorDbContext` (تحويل `datetime2` → `timestamptz` في `DocGeneratorPostgresDbContext.cs:24-128`) — VERIFIED.
- إعدادات ملف واحد (`Configurations.cs:1-1383`) — VERIFIED؛ 11 قيدًا فريدًا (منها 2 جزئية) ولا قيد فريد لثلاثية (رقم الأساس + النوع + السنة + الدائرة) — VERIFIED الغياب الظاهر (انظر `BQ-008`).
- علاقات `Document` نجمية (`Cascade` للتوابع، `Restrict/SetNull` للمراجع) + حذف منطقي `QueryFilter` — VERIFIED (`Configurations.cs:72,214-217,254-257`).
- نقود `decimal(20,2)` حصرًا (`Document.cs:73-90,135-137,176-206` + `Configurations.cs:107-112,147-149,166-176` + `DelegationAsset.cs:23`) — VERIFIED؛ لا `double/float` — VERIFIED.
- تواريخ هجينة: `DateTime?` مخزنة زمنيًا + `string?` حرة (12 حقلًا في `Document.cs` + إجراءات) — VERIFIED (`Configurations.cs:101-104,139-142,181-194`).
- تدقيق `AuditLog` + `DocumentFieldChange` (`AuditLog.cs:3-15` + `Configurations.cs:344-356,1131-1154`) — VERIFIED الهيكل، NOT VERIFIED الشمولية والمنع من العبث (PROMPT 2–3).

## 12. الاختبارات (VERIFIED المواقع — لم تُشغَّل)

- خلفية 105 ملفات: `Api.Tests` (42 + `FastTestPasswordHasher.cs`) على `SQLite` مؤقت معزول (`ApiFactory.cs:21-34` + `Database:UsePostgres=false` + سر اختبار + تنظيف `Dispose`)، و`Application.Tests` على `:memory:`/ملف مؤقت (`CoreTests.cs:10-15`) — VERIFIED العزل من الكود.
- واجهة 128 ملف اختبار (`vitest.config.ts:6-14` + `test/setup.ts`) — VERIFIED الإعداد.
- التغطية المدعاة: مصادقة/فروع/نقل/إنابات/مراسلات/تدقيق/حد معدلي/توليد `Word`/إحصاءات — INFERRED من أسماء الملفات فقط (لم تُقرأ كل حالة).
- لم تُشغَّل الاختبارات في هذه الجلسة — NOT VERIFIED نجاحها الحالي (العزل مثبت كوديًا لكن التشغيل لم يتم توفيرًا للزمن).

## 13. الإعدادات (VERIFIED — القيم masked `****`)

- `appsettings.json:1-71`: سجلات ملفية دوارة + `ConnectionStrings:DefaultConnection=****` (افتراضي `sqlite`) + `Database:UsePostgres=false` + `TimeZone=Asia/Damascus` + `Security(Hsts/Https=false, CspReportOnly=true)` + `Export.MaxRows=10000` + `RateLimiting` + `Lockout` + `Swagger.Enabled=false` + `Jwt(Secret=****, Issuer/Audience, Expiry 480min)` + `WordTemplates(Path + 8 قوالب)`.
- الطبقات: `DATABASE_URL` > سلسلة الاتصال > `docgen.db` (`Program.cs:43-50`)، وأسرار التطوير عبر `user-secrets` (`appsettings.Development.json:9-11`)، والإنتاج عبر `render.yaml` (`Jwt__Secret sync:false`, `Bootstrap__AdminPassword sync:false`, `Database__UsePostgres=true`).
- النشر: `Dockerfile` (واجهة مبنية في `wwwroot` + `USER app` + `PORT 8080`) و`CI` (`ci.yml:8-57`: خلفية `dotnet test` + واجهة `oxlint + tsc -b + vitest + build` + تدقيق `continue-on-error`) — VERIFIED.

## 14. المراقبة (VERIFIED المواقع)

- `Serilog` (كونسول + ملف يومي دوار `logs/logs-.txt`، `RetainedDays 31`، حد 50MB) مع `LogContext` (`TraceId/UserId/UserRole` في `RequestLoggingEnricherMiddleware.cs:20-22`) — VERIFIED (`Program.cs:105-129,303-306`).
- لا فحوص صحة (`AddHealthChecks/MapHealthChecks` = صفر إصابة) ولا `Seq`/مقاييس/تتبع — VERIFIED الغياب (يُصعّب تشخيص أعطال الإنتاج).
- تدقيق كتابات (`AuditLogger` + `GET api/audit-logs` لرؤساء/مدراء فقط) + قناة `POST api/client-errors` (بحد `IMemoryCache` لكل مستخدم في `ClientErrorsController.cs:56-62`) — VERIFIED الهيكل.
- قراءة الملف تُتتبع عدادًا (`POST .../view` → `ViewCount` في `DocumentsController.cs:488`) لا سجل تدقيق قراءة — INFERRED (انظر `BQ-016`).

## 15. جودة الكود (INFERRED — يحتاج PROMPT 4 للقياس الآلي)

- خدمات سمينة (`PublicEntityService` 4068، `DocumentService` موزع > 4000) و`DTO` ضخم (`DocumentDtos` 1014، `types/index.ts` 2151) — VERIFIED الأحجام.
- ازدواج منطق العرض بين `DocumentStatusResolver`/`EffectiveFileIdentity` و`viewFormat.ts` — INFERRED.
- `Configurations.cs` ملف واحد (1383 سطرًا لكل الكيانات) — VERIFIED؛ يُصعّب المراجعة المتوازية.
- لا `TODO/FIXME` ممنهج — NOT VERIFIED (لم يُمسَح شاملًا).

## 16. خريطة المخاطر (أولية — التفصيل الأمني في PROMPT 2 والتكاملي في PROMPT 3)

- ARC-001 — HIGH — غياب قيد وحدانية ظاهر لثلاثية (رقم الأساس + النوع + السنة + الدائرة) على مستوى القاعدة — `Configurations.cs` (فحص `HasIndex/IsUnique` كاملًا، لا أثر للثلاثية) — الأثر: تكاملية بيانات/ازدواج أرقام قضائية — الثقة: INFERRED (يحتاج تأكيد PROMPT 3 ضد الهجرات).
- ARC-002 — HIGH — صلاحيات برمجية لا إعلانية (معظم النقاط `[Authorize]` عارٍ + `RolePermissions` داخلي) — `RolePermissions.cs:1-134` + متحكمات — الأثر: أمن/صيانة (أي omission = تجاوز) — الثقة: VERIFIED.
- ARC-003 — HIGH — الدور الخارجي (`entitymanager`) على نفس سطح `API` مع عزل بوسيط (`EntityManagerPortalGuard` في `Program.cs:307-308`) — الأثر: أمن (تسرب نطاق) — الثقة: INFERRED (يحتاج PROMPT 2).
- ARC-004 — MEDIUM — تواريخ هجينة (`string?` حرة + `DateTime?` مفسر) مع بديل مرن `TryParse` غير قابل للمحاكاة `SQL` (`ActionDateParser.cs:10-12`) — الأثر: صحة/فرز/إحصاءات — الثقة: VERIFIED.
- ARC-005 — MEDIUM — مبالغ متعددة العملات في سجل واحد بلا سعر صرف معلن (3 مبالغ + 3 شمول + 3 محصلة + 3 مطلوبة + 3 مدفوعة) — `Document.cs:73-90,135-209` — الأثر: صحة مالية/إحصاءات — الثقة: VERIFIED الهيكل، NOT VERIFIED الخلط (PROMPT 3).
- ARC-006 — MEDIUM — توليد `Word` من قوالب قرصية (`WordTemplates/`) مع رفع `PrintCount` كتابةً في مسار قراءة ظاهريًا (`WordDocumentGenerator.cs:40-43`) — الأثر: سلامة/تدقيق — الثقة: VERIFIED.
- ARC-007 — MEDIUM — منطقة زمنية واحدة مثبتة (`Asia/Damascus` + احتياطي `+03` ثابت في `ServerClock.cs:29-56`) تتحكم بسنة التدوير — الأثر: صحة (حدود السنوات) — الثقة: VERIFIED.
- ARC-008 — LOW — لا فحوص صحة ولا مقاييس (`Program.cs:310` بلا `MapHealthChecks`) — الأثر: موثوقية/تشغيل — الثقة: VERIFIED.
- ARC-009 — LOW — مكونات واجهة عملاقة + منطق مكرر (`EntityRegistryReviewManagement` 1439، `types/index.ts` 2151) — الأثر: صيانة/اختبار — الثقة: VERIFIED الأحجام.

## 17. الاتجاه المستهدف (تدريجي فقط — لا إعادة كتابة)

1. تثبيت الوحدانية على مستوى القاعدة للثلاثية + اختبار تزامن — يعالج ARC-001 — يمس `Configurations.cs` + هجرتين (`SQLite/Postgres`) — الفائدة: منع ازدواج قضائي — الخطر: هجرة على بيانات قائمة (تتطلب `database update` مزدوجًا عند النشر).
2. تضييق التفويض إعلانيًا (`[Authorize(Roles=...)]` على كل أكشن) مع إبقاء `RolePermissions` مصدر الحقيقة — يعالج ARC-002/ARC-003 — الفائدة: فشل مغلق — الخطر: كسر مسارات شرعية إن أُخطئ الدور (يُكشَف بالاختبارات).
3. توحيد التواريخ (مفسر واحد + `DateParsed` للفرز + رفض الغامض) — يعالج ARC-004 — الفائدة: إحصاءات صحيحة — الخطر: تغيير سلوك إدخال (يحتاج موافقة المالك).
4. فصل توليد `Word` عن عدادات المشاهدة/الطباعة بسجل تدقيق صريح — يعالج ARC-006 — الفائدة: أثر قانوني نظيف — الخطر: ضئيل.
5. إضافة `/healthz` + مقاييس أساسية — يعالج ARC-008 — الفائدة: تشخيص إنتاج — الخطر: ضئيل.
6. تقسيم `PublicEntityService` و`Configurations.cs` تدريجيًا — يعالج ARC-009 — الفائدة: صيانة — الخطر: انزلاق سلوكي (يُحاصَر باختبارات التكامل القائمة).

## 18. ترتيب مرشح أولي (خشن — الخطة الحقيقية بعد PROMPT 2 و3)

1. PROMPT 2 (أمن) → تثبيت التفويض والنطاقات. 2. PROMPT 3 (تكاملية) → الوحدانية والذرية والتدقيق. 3. ثم البنود 1–6 أعلاه بهذا الترتيب.

## 19. الأسئلة والتغطية

- الأسئلة: `business-questions.md` (`BQ-001` … `BQ-020`) — مدمجة هنا بالمرجع لا بالنص الكامل.
- التغطية: فُحص كاملًا: خريطة المستودع، المخزون من ملفات المشروع، المتحكمات والنقاط (184)، الصلاحيات (`RolePermissions.cs`)، `Program.cs`، الكيانات والكتالوجات، `Configurations.cs`، الخدمات الكبرى، الواجهة (توجيه/مكونات/تواريخ/توليد)، الإعدادات والنشر، السجلات. فُحص جزئيًا: سير العمل الداخلية لكل خدمة (قراءة رؤوس + مسارات مختارة)، الاختبارات (أسماء + عزل فقط). لم يُفحص: تشغيل الاختبارات/البناء، محتوى `src/` الجذري، تطابق `DTO` حقلًا بحقل، الهجرات سطرًا بسطر (تُدرَس في PROMPT 3).
