# الخريطة الذهنية — مولد المستندات التنفيذية (React + .NET)

> **الغرض:** مرجع دائم لأي وكيل ذكاء اصطناعي أو مطور لفهم المشروع كاملاً قبل أي تعديل — يُحدَّث بعد كل تغيير.
> **الحالة:** تحليل غير تدخلي مكتمل — لا كود تم تعديله أثناء الإنشاء
> **آخر تحديث:** 27 آب 2026
> **الإصدار:** 3.8 — `react-dotnet-app/`
> **المسار الجذر:** `C:\Users\pc\Desktop\document_generator\working 3.8 32b\react-dotnet-app\`
> **المراجع:** `AGENTS.md` + `RUN_GUIDE.md §9` + `README.md`
> **الوثيقة المقابلة (Word):** `التقرير_السياقي_الشامل.docx` على سطح المكتب

---

## كيف يستخدم هذا الملف

- **قبل أي تعديل:** اقرأ هذا الملف كاملاً + `AGENTS.md` (قواعد التواريخ، Mobile-first، الهجرات المزدوجة)
- **أثناء التعديل:** التزم بـ Clean Architecture + لا حلول مختصرة + لا تغييرات كاسرة للعقود
- **بعد أي تعديل:** حدّث القسم المتأثر أدناه + أضف سطراً في سجل التحديثات (آخر الصفحة) + شغّل `dotnet test` + `npx vitest run` + `npx oxlint src` + `npx tsc -b` + `npm run build`

---

## 1) الرؤية العامة (Big Picture)

### 1-1 الغرض الأساسي
إعادة بناء لتطبيق **مولد المستندات التنفيذية** (كان PyQt5 مكتبي + Flask) كتطبيق ويب:
- **الواجهة:** `Vite + React 19 + TypeScript 6 + Tailwind CSS 4 + Cairo RTL`
- **الخلفية:** `ASP.NET Core 8 Web API + EF Core 8 (SQLite محلي / PostgreSQL إنتاج)` + `JWT Bearer (PBKDF2 200k)` + `DocumentFormat.OpenXml` لتوليد Word خادمياً دون Word
- **القوالب:** `backend/src/DocGenerator.Api/WordTemplates/` (نسخ من `bin/*.docx`) — `001 summon`..`005 property` + `006/007/PS`
- **التوثيق:** `/swagger` (Development أو `Swagger:Enabled=true`)
- **المرفقات:** `frontend/src/index.css:1` خط Cairo + `index.html: lang="ar" dir="rtl"`

**الدورة الكاملة للملف القانوني:** إدخال مقترض/كفلاء/أموال (عقار/مركبة/متجر/كفالة رواتب) + تحديد الحالة التنفيذية + إنابات + استئنافات + مطالعات + تنبيهات + سجل جهات عامة + إحصاءات متعددة العملات + تصدير Excel + تدقيق حقلي كامل + وقوعات زمنية.

**قاعدة التواريخ الحرة (AGENTS.md:23-34):** كل حقل تاريخ `type="text" placeholder="مثال: 1/8/2026"` → `normalizeArabicDigits().trim()` في `DocumentForm.tsx:602` → `DTO string?` → `DocumentValidator.ParseDateTime → FreeDateParser/ActionDateParser` (سبع صيغ `d/M/yyyy`..`yyyy-MM-dd` + `TryParse` — يوم/شهر/سنة دائماً) → `Entity DateTime?` → استجابة `yyyy-MM-dd`.

### 1-2 الأدوار (Personas) — `Domain/Enums/Enums.cs` + `frontend/src/types/index.ts:1`

| الدور | `UserRole` | النطاق | الصلاحيات المركزية `RolePermissions.cs:9-117` | الوظيفة |
|---|---|---|---|---|
| **محامي** `lawyer` | 1 | ملفاته فقط `CreatedById==userId` | `CanEdit/CanChangeStatus/CanDelete/CanManageActions/CanRotate/CanManageDelegations/CanManageAppeals/CanCreateReviewLetters` | إنشاء/تعديل/حالة/إجراءات/تدوير/تسطير إنابات واستئنافات قبل الإسناد/تسطير مطالعات |
| **رئيس قسم** `head` | 2 | فرعه `BranchId` | `CanTransferDocuments/CanApproveDelegations/CanAssignAppeals/CanReplyReviewLetters/CanCreateAlerts/CanManageEntityRegistry` | نقل ملفات/استئنافات، اعتماد إنابات، إسناد استئنافات، رد مطالعات، تنبيهات، مراجعة سجل الجهات لمحافظته |
| **مدير** `manager` | 3 | عام (قراءة كل الفروع) | `HasFullAccess` (قراءة) + `CanViewCounters/CanSeeAdministrativeBranch` | قراءة فقط للملفات + إحصاءات + ملخص فروع + نشاط مستخدمين |
| **مشرف نظام** `admin` | 4 | عام | `HasFullAccess + CanManageUsers/CanManageBranches` | كـ المدير + إدارة مستخدمين/فروع، يرى المحذوفات |
| **مندوب جهة** `entitymanager` | 5 | مجموعته `PortalGroupId/PortalEntryId` (`User.cs:40`) | `CanUseDelegatePortal` معزول بـ `EntityManagerPortalGuard.cs:12-42` | بوابة قرائية فقط `GET /api/portal/*` + تصدير — `403` لكل `/api/*` آخر |

### 1-3 سير العمل السعيد (Happy Path)
```
Login.tsx → POST /api/auth/login (AuthController.cs:40) [يدعم اختيار الفرع عند تضارب الأسماء + Cookie HttpOnly + CSRF + RateLimit]
→ Dashboard.tsx [إحصاءات حسب الدور + تذكيرات + تنبيهات]
→ DocumentsList.tsx [بحث q + 7 فلاتر + ترقيم 20/صفحة + حفظ موضع listSession + تصدير Excel]
→ DocumentForm.tsx:544-710 [اختيار generalEntitySide ثابت ثم فرع ApplicantSideSections vs ExecutedSideSections → POST /api/documents]
→ DocumentView.tsx:70 [3 طلبات متوازية GET /documents/{id} + delegations + appeals + 9 مودالات]
→ StatusChangeModal/ExecutedStatusModal → PUT /documents/{id}/status (DocumentService.Status.cs:44)
→ توليد Word GET /api/documents/{id}/generate?template=001..005
→ تدقيق DocumentChangeTracker + AuditLogger → AuditLogs/DocumentChangesModal
```

---

## 2) الهيكل المعماري (Architecture)

### 2-1 Backend — Clean Architecture رباعي — `backend/DocGenerator.sln:1-64`

```
src/
 ├─ DocGenerator.Domain (بلا حزم — Domain.csproj:1-9)
 ├─ DocGenerator.Application (يعتمد Domain فقط — AngleSharp 1.7.1, OpenXml 3.0.2, HtmlSanitizer 9.1)
 ├─ DocGenerator.Infrastructure (يعتمد Application+Domain — EF Sqlite 10 + Npgsql 10.0.3, JwtBearer 10)
 └─ DocGenerator.Api (يعتمد الكل — Swashbuckle 6.6.2 + WordTemplates/*.docx)
tests/
 ├─ DocGenerator.Application.Tests (54 وحدة)
 └─ DocGenerator.Api.Tests (29 تكامل WebApplicationFactory — Program.cs:250 partial)
```

**نقطة الدخول `Program.cs:17-250`:**
- `21-53`: `DATABASE_URL` يغلب `ConnectionStrings` → `PostgresConnectionString.Normalize()` → حارس `NpgsqlConnectionStringBuilder`
- `69-80`: إلزام `Jwt:Secret` + تحويل `WordTemplates.Path` لمطلق
- `82-97`: `AddApplication() + AddInfrastructure(conn,usePostgres) + CORS Vite + AddControllers()`
- `99-144`: `JwtBearer` (`ClockSkew 1m`) — `OnMessageReceived` يقرأ `docgen_token` من `Cookie`، `OnTokenValidated` يتحقق `sub→userId + IsActive + TokenVersion`
- `154-179`: `ForwardedHeaders` موثوق فقط من `KnownProxies` (IP/CIDR) — بلا وكيل تُتجاهل
- `181-246`: الوسطاء بالترتيب الإلزامي `UseForwardedHeaders → ExceptionHandler → CsrfMiddleware → StaticFiles (إنتاج) → InitializeAsync → Swagger → Cors → Auth → EntityManagerPortalGuard → MapControllers → Fallback index.html`
- **هجرات مزدوجة إلزامية:** `Infrastructure/Persistence/Migrations/` (SQLite ~30) و `MigrationsPostgres/` (~60) — `AGENTS.md:68-75`

**Domain** (`Domain/Entities/`): ~30 كيان — المحوري `Document.cs:1-339` (50+ حقل: 3 عملات رئيسية + 3 شمول + 3 محصلة + 3 مطلوبة/مدفوعة لـ Executed، `IsDeleted/DeletedAt`، `SourceDelegationId` فريد 1:1، `ApplicantRegistryId`، `ReferredFromLawyer/ReferredAt`، `SoldAssetIds JSON`، `WasDepositExecuted`، `ForcibleTransferDate`، `StruckOffDate/Renewal*`، `Baraet/Tarith/Sayer`) + `User.cs:1-57` (`Username+BranchId` فريد `Configurations.cs:16`، `TokenVersion`، `Lockout`، `PortalGroupId`) + `Guarantor/Heir/Asset/ExecutionAction/DocumentBaseNumber/DocumentRegistrationDate/DocumentOccurrence/DocumentAssignment/DocumentDelegation/DelegationAsset/DocumentAppeal/HeadAlert/ReviewLetter/PublicEntityGroup` + `IDocumentExecutionState` + 16 تعداد (`ExecutionStatus=None/ExecutedForcibly/ExecutedBySettlement/Deferred/DelegationExecuted`، `ExecutedStatus=""=متداول/منفذ/مشطوب`، `DelegationStatus=PendingHead→Assigned→Registered→Executed`، `AppealStatus`...)

**Application** (`Application/`): واجهات `Common/Interfaces/` (`IRepository<T>`, `IDocumentRepository`, `IUserRepository`, `IHeadAlertRepository`, `IPublicEntityRepository`, `IPortalRepository`, `ITransactionRunner`...) + 13 خدمة (`DocumentService` مجزأ 5 partial: `.cs+.Apply.cs+.Actions.cs+.Status.cs+.Search.cs`، `DocumentDelegationService:903س`، `DocumentAppealService:1071س`، `AuthService`, `StatisticsService`, `PublicEntityService`, `WordDocumentGenerator`...) + أدوات `DocumentValidator`, `DocumentStatusResolver`, `FreeDateParser`, `DocumentChangeTracker` + DTOs (`DocumentDtos ~900س`)

**Infrastructure** (`Infrastructure/Persistence/`): `DocGeneratorDbContext:19-67` (22 DbSet) + `DocGeneratorPostgresDbContext:18-108` + `Configurations.cs:8-1100` (20 `IEntityTypeConfiguration` + `HasQueryFilter(!IsDeleted)`) + `Repository.cs:7-77` (17 Include لـ Document) + 8 مستودعات نوعية (`DocumentRepository:702س`، `StatisticsRepository:756س`...) + `TransactionRunner.cs:5-46` + `UnitOfWork` + `AuditLogger` + `TokenService:17-46`

**Api** (`Api/`): 11 متحكم:
- `AuthController:14-124` (`api/auth` — `login/logout` AllowAnonymous + `rateKey=ip:username` + `docgen_token+docgen_csrf HttpOnly+Strict`)
- `DocumentsController:10-719` (25 نقطة: `search/filter-options/{id}/status/revert/consider-executed/executed-status/restore/view/generate/transfer/actions/occurrences/base-numbers` — `CanAccess:61-66`)
- `AppealsController:10-415` / `DelegationsController:10-185` / `BranchesController:10-94` (Admin فقط) / `UserManagementController:10-163` / `StatisticsController:10-156` / `PortalController:16-82` / `EntityRegistryController:16-297` / `ReviewLettersController:16-207` / `AuditLogsController:10-26`
- وسطاء: `CsrfMiddleware:14-71` (`FixedTimeEquals` 32 بايت)، `EntityManagerPortalGuard:12-42` (403 إلا `portal/auth/me/logout`)، `GlobalExceptionHandler:12-56`

**الإعدادات `appsettings.json:1-51`:** `Data Source=docgen.db`، `UsePostgres=false`، `KnownProxies=[]`، `MaxRows=10000`، `RateLimit 5/5m`، `Lockout 5/15m`، `Jwt Expiry 480`

### 2-2 Frontend — `frontend/src/` (Vite + React 19 + TS 6 + Tailwind 4)

```
src/
 ├─ api/client.ts (+.test) — Axios baseURL /api + CSRF + interceptors
 ├─ auth/auth-context.ts + AuthContext.tsx — السياق الوحيد {user, loading, login, logout, hasFullAccess, isHead}
 ├─ components/ (34 — Layout, StatusChangeModal, RichTextEditor, BaseNumbersModal...)
 ├─ hooks/ useCancellableRequest.ts + useDebouncedValue + useMediaQuery (1023px) + useTimeout
 ├─ pages/ (23 + 46 .test — Login, Dashboard, DocumentsList 920س, DocumentForm 1034س, DocumentView 621س...)
 ├─ types/index.ts (1579س — العقد الأكبر)
 ├─ utils/ (13 — documentStatus, amountCurrencies, dates, arabicDigits, richText DOMPurify, governorate, listSession...)
 └─ App.tsx + main.tsx + index.css (Cairo)
```

**إدارة الحالة:** لا Redux/Zustand/React Query — سياق وحيد `AuthContext` + حالة محلية `useState/useMemo` + `useCancellableRequest.ts:17` (بديل SWR عبر `AbortController`) + `listSession.ts:1` (حفظ موضع القائمة في `sessionStorage`)

**الاتصال:** `axios.create({baseURL:'/api'})` — تطوير `vite.config.ts proxy /api → 5199`، إنتاج `UseStaticFiles + Fallback` من أصل واحد — كل `POST/PUT/PATCH/DELETE` يحمل `X-CSRF-Token`، كل `401` → `window.location.href='/login'` (`client.ts:55`)

**حماية المسارات `App.tsx:1`:** كل الصفحات `lazy + Suspense + ErrorBoundary` مع `RequireAuth` و `RequireRole((role,hasFullAccess,isHead)=>bool)` — 23 مسار ( `/login` عام، `/` Dashboard، `/documents` + `/new` + `/:id/edit` + `/deleted|struck-off|executed|rotate`، `/reviews/:id`، `/appeals/:id`، `/branch-lawyers` head|admin، `/delegations/requests` head، `/users/manage|branches/manage` admin، `/entities/registry|review` hasFullAccess||isHead، `/portal` entitymanager…)

**التنقل `Layout.tsx:1`:** شريط جانبي `aside.w-64` مكتبي + درج `dialog` + شريط سفلي جوال (`useIsMobile`) + شارة `unseenReplies` كل 60ث + مصيدة تركيز Tab

---

## 3) المنطق الأساسي والمعاملات

### 3-1 مسار المعاملة (إنشاء/تعديل ملف)
1. **الواجهة `DocumentForm.tsx:62-710`:** تهيئة `useState<DocumentUpsertRequest>` + `defaultGovernorateRef` من `governorateFromBranch` → تحميل `GET /documents/{id}` + `normalizeDocumentResponse` → تحقق أولي (executed/deposit يتطلب `fileNumber+FileYear`) → بناء `payload` مع `normalizeArabicDigits` لكل التواريخ + `slotDefaultCurrency` (لا تكرار) + تصفير حقول غير معنية → `PUT/POST`
2. **العقد `types/index.ts:536` ↔ `Dtos.cs:295`:** الوارد `DocumentUpsertRequest:RenewalRequest` (`string?/decimal?/List<Dto>` — التواريخ `string?` فقط) ↔ الصادر `DocumentResponse:747-944` (`DisplayFileNumber`, `DisplayStatus`, `NeedsRotation`)
3. **الخدمة `DocumentService.cs:173-258`:** `CreateAsync` → `ValidateSide/ValidateExecutedRequest` (`DocumentValidator.cs`) → `ApplyRequest→FillDerivedFields→ApplyRegistrationDate` → **معاملة** `RunAsync`: `Add+Save → AddAssignment(Create) → audit → AddStruckOffOccurrence? → SeedActions` — `UpdateAsync` يلتقط `Capture(before)` ثم داخل معاملة: `wasStruckOff&&Now==None → ApplyRenewalAsync` ثم `Update+SyncDelegationSnapshots→LogDocumentChangesAsync`
4. **التواريخ `FreeDateParser.cs:14`:** سبع صيغ `d/M/yyyy..yyyy-MM-dd` + `TryParse` — `1/8/2026=1 آب`، فراغ→`null`، غير صالح→`ArgumentException`
5. **التحقق `DocumentValidator.cs:10-103`:** `ValidateSide` (فارغ→`applicant`)، `ValidateExecutedRequest` (`executedLike` يجب أن يكون مقيد `FileNumber+FileYear` + عقد `عادي` فقط)، `ValidateRegistrationDate`
6. **التتبع `DocumentChangeTracker.cs:13-314`:** `Capture` عبر Reflection عدا `ExcludedFields` + 7 توقيعات `__Col_*` → `Diff` ينتج `FieldKey/FieldLabel عربي/OldValue/NewValue`
7. **التدقيق `AuditLogger.cs:9-80`:** داخل `TransactionRunner.Commit` يضم `AuditLog + DocumentFieldChanges` بنفس المعاملة — `LogManyAsync` للتدوير

### 3-2 سلامة المعاملات
- **`TransactionRunner.cs:5-46`:** `ExecutionStrategy + BeginTransactionAsync → action → Commit/Rollback` — تعليق `27-30` يؤكد التدقيق يلتحم (الكل أو لا شيء)
- **`Repository.cs:7-77` + `DocumentRepository.cs:12-702`:** `WithIncludes` 17 Include — `TransferOwnerAsync:406-420` + `TransferAllOwnerAsync:459-476` بـ `ExecuteUpdateAsync WHERE CreatedById==expected` (TOCTOU-safe → `409`) — `HasQueryFilter(!IsDeleted)` يحجب المحذوف
- **التنبيهات خارج المعاملة عمداً:** بعد `RunAsync` الناجح يُنشأ `HeadAlert` ببلوك `try/catch` يسجل `head_alert_failed` ولا يُفشل العملية (`DocumentDelegationService:105-121`)

### 3-3 نقاط القرار (Business Rules)

**أ. حالات طالبة تنفيذ `ExecutionStatusCatalog.cs:8-133` + `DocumentService.Status.cs:44-240`:**
```
تحت رفع (IsDraft) → متداول → { تريث (+Tarith*), منفذ بالتسوية (+baraet*+Collected*), منفذ جبريا (+ForcedExecutionDate+SoldAssetIds), مشطوب (+struckOffDate) }
تريث → منفذ بالتسوية
منفذ جبريا منفذ جزئياً (تلقائي بعد إنابة) → منفذ كاملاً عبر ConsiderExecutedByDelegationAsync (+ForcibleTransferDate)
التراجع من (تريث/تسوية/جبري) → متداول بكتاب Sayer* الأربعة (RevertStatusAsync:177)
```
- يسري على `applicant` فقط (`Status.cs:49-50` يرفض `executedLike`) — `IsAllowedStatusChange` يمنع الجبري/التسوية/المشطوب نهائياً

**ب. حالات منفذ عليه/عرض وايداع `ExecutedStatusCatalog.cs:9-42` + `Status.cs:314-504`:**
```
متداول ("") ⇄ منفذ ("منفذ" + ExecutedPaid* + WasDepositExecuted=true — نهائي في executed)
     ↓
  مشطوب (StruckOffDate) ←→ متداول مع تجديد (RenewalFileNumber إلزامي + RenewalYear في applicant + BaseNumber)
```
- كل شطب/تجديد يسجل `DocumentOccurrence` (`struck-off/renewal`) — يُسمح بالتكرار، `StruckOffDate` يبقى حتى بعد الإعادة

**ج. الإنابة `DelegationStatusCatalog.cs:7-25` + `DocumentDelegationService:14-903`:**
`PendingHead → Assigned → Registered → Executed` — `Create/Update/Delete` قبل الاعتماد وملك المنيب و`Source` غير منفذ/مشطوب و`Applicant` فقط — `Assign` ينشئ `Document` مناب `FileType=Delegation` بنسخ أطراف/سند/كتب — `Complete` يدخل `SalePrice` + `ReturnDate+ForcedExecutionDate` ويحوّل المنيب تلقائياً `منفذ جبريا جزئياً` + تنبيه للمنيب

**د. الاستئناف `AppealStatusCatalog.cs:8-31`:** `pending → decided/struck-off` — `Create` قبل الإسناد و`!IsDraft` — `Assign/Transfer` رئيس قسم — `Decide/Strike` متابع — لقطات `AppellantsJson` بـ `UnsafeRelaxedJsonEscaping`

**هـ. الحذف والتدوير والنقل:** `IsDeleted` يحجب تلقائياً — `SaveBaseNumbersAsync:202-300` (`!IsDraft && FileYear != currentYear && !HasBaseNumber(currentYear)`) — `TransferAsync:298-390` بتحقق داخل المعاملة + `AddAssignment(Transfer)`

---

## 4) التبعيات والمخاطر

### 4-1 المكتبات
**Frontend `package.json:13-40`:** `react 19.2.8`, `react-router-dom 7.18.2`, `axios 1.19.0`, `tailwindcss 4.3.3`, `Tiptap 3.29.2`, `dompurify 3.4.13`, `vite 8.2.0`, `typescript 6.0.2`, `vitest 4.1.10 + jsdom 30 + Testing Library`, `oxlint 1.75.0` — لا `date-fns` ولا `zod` ولا `Redux`

**Backend:** `EF Core Sqlite 10 + Npgsql 10.0.3`, `JwtBearer 10`, `OpenXml 3.0.2 + Packaging 10`, `AngleSharp 1.7.1 + HtmlSanitizer 9.1`, `Swashbuckle 6.6.2`

### 4-2 مناطق عالية المخاطر
| المنطقة | الملف | الخطر |
|---|---|---|
| **النموذج الموحد** | `DocumentForm.tsx:1-1034` | 10 useState + payload يدوي 20+ تاريخ؛ 3 صفات + مسارين؛ نسيان `normalizeArabicDigits` يكسر `ParseDateTime` |
| **عقد الأنواع** | `types/index.ts:1-1579` | 200+ حقل؛ تغيير DTO دون تحديث يكسر صامتاً |
| **شجرة التحميل** | `Repository.cs:41-69` (17 Include) | كل Search يحمّل كاملة؛ Export بسقف 10000 قد يثقل |
| **آلة الحالات** | `DocumentService.Status.cs + Catalogs` | حالات نهائية؛ خطأ في `IsAllowedStatusChange` يحبس ملفاً |
| **النقل المتزامن** | `DocumentRepository:397-476` | `ExecuteUpdate` ذري لكن `TransferAll` يقارن `rowsAffected → 409` |
| **الحذف المنطقي** | `Configurations.cs:64-66` | نسيان `IgnoreQueryFilters` يخفي بيانات |
| **عزل EntityManager** | `EntityManagerPortalGuard:25-31` | أي مسار `/api/*` جديد دون استثناء يحجبه 403 |
| **محرر النص** | `RichTextEditor.tsx + richText.ts` | قائمة بيضاء صارمة؛ توسيعها يتطلب مراجعة أمنية |

---

## 5) التأكيد والفجوات — القرارات النهائية (27 آب 2026)

> هذه القرارات **مُلزمة** — أي تعديل مستقبلي يجب أن يلتزم بها:

| # | الموضوع | القرار النهائي | الأثر على الكود |
|---|---|---|---|
| **1** | **تثبيت الصفة** `GeneralEntitySide` | **الحظر مطلق** — لا يمكن تغيير `applicant ↔ executed ↔ deposit` بعد الإنشاء (`ApplyRequest:203-205`) | عند تصحيح خطأ إدخال: حذف الملف وإعادة إنشائه بالصفة الصحيحة |
| **2** | **بقاء بيانات الشطب** | بعد التجديد يبقى **كل** معلومات الشطب (`StruckOffDate` + وقوعات `struck-off/renewal` في `DocumentOccurrence`) لأغراض الإحصاء (`Document.cs:228-229`) | `ApplyRenewalAsync:543-589` + `RestoreStruckOffAsync:464-503` لا يصفّر `StruckOffDate` |
| **3** | **منفذ → متداول والمحامي متوقف** | **السياسة (أ) معتمدة:** لا يحق لرئيس القسم إرجاع `منفذ→متداول` مباشرةً — يجب **نقل الملف** أولاً عبر `TransferAsync:298` إلى محامٍ نشط ثم يغيّر المالك الجديد الحالة | يحافظ على `Assignments` والتدقيق؛ يحل مشكلة الملفات اليتيمة |
| **4** | **تغطية بدل الإنابة** | محامي **الإنابة (المناب)** يعرف أولاً هل البدل غطى الدين — يُضاف `dropdown` في `CompleteDelegationRequest` (**غطى كامل المديونية / لم يغطِ**) + عند عودة الإنابة يظهر **تنبيه للمنيب** *"أُعيدت الإنابة إلى دائرة كذا في ملف فلان — منفذة (غطت/لم تغطِ) — يرجى تغيير حالة الملف"* والضغط يفتح المنيب | إضافة حقل `SaleCoversFullDebt?: bool` في `CompleteDelegationRequest:528` + `DelegationDto` + رسالة `alerts.CreateAsync:547` — لا تعارض مع `Occurrence` أو `ConsiderExecutedByDelegationAsync:242` (القرار النهائي يبقى بيد محامي المنيب) |
| **5** | **الإنابة الجغرافية** | أي فرع يتابع ملفاً يمكنه تسطير إنابة لفرع آخر إذا كان المال يتبع مكانياً له — يحدده محامي المنيب حسب موقع المال | `governorateFromBranch` تعبئة افتراضية فقط؛ لا حظر برمجي على `isExternal + externalBranchId` |
| **6** | **الاستئناف — نفس الطرف** | نفس الطرف **يمكن** أن يكون مستأنِفاً في استئناف ومستأنفاً عليه في آخر منفصل، لكن **ليس** في نفس الاستئناف | مطابق لـ `BuildSnapshots` (`Appellees = AllParties - Appellants`) داخل نفس `Appeal` |
| **7** | **`coverageLabel` + `NeedsReview`** | `CoverageLabel` أنشئ لأن جهات كـ *"إدارة نقل الكهرباء — المنطقة الشمالية/الساحلية"* تغطي أكثر من محافظة (`PublicEntity.cs:22-28` — عرض فقط `coverageLabel ?? governorate`، الحوكمة على `Governorate` حصراً). الجهة التي يدخلها محامٍ تظهر **فوراً للجميع** (حتى لمندوب البوابة `entitymanager`) لكن `NeedsReview=true` حتى يعتمدها رئيس القسم (`ApproveReviewAsync:481`) — **مؤكد: مندوب الجهة يراها حتى قبل الاعتماد** (`PortalRepository.ScopePredicate:25-34` يفحص `Status==Final` فقط) | لا مشكلة — التصميم مقصود؛ `PortalRepository` يطابق المطلوب |
| **8** | **قاعدة التدوير** | لا يُدوَّر إلا **متداول وتريث** أياً كانت الصفة والذي ليس لديه `BaseNumber` لسنة التدوير — `منفذ + مشطوب + تحت رفع` لا تُدوَّر | مطابق لـ `GetRotationCandidatesAsync:496-540` + `SaveBaseNumbersAsync:202-300` |

---

## ملحق أ — خريطة المسارات والملفات المفتاحية

| المسار | الوصف |
|---|---|
| `frontend/src/pages/DocumentForm.tsx:55/602/701` | النموذج — تطبيع أرقام وبناء payload |
| `frontend/src/utils/arabicDigits.ts:2` | تطبيع الأرقام العربية |
| `frontend/src/types/index.ts:536 + backend/DTOs/DocumentDtos.cs:295` | العقد — الطلب/الاستجابة |
| `backend/Services/DocumentService.cs:173/222 + Apply.cs:199/807 + Status.cs:44/177/242/314/464 + Actions.cs:17/135` | المنطق الأساسي |
| `backend/Common/FreeDateParser.cs:14 + ActionDateParser.cs:22 + DocumentValidator.cs:17/34/62` | التواريخ والتحقق |
| `backend/Audit/DocumentChangeTracker.cs:182/198 + AuditLogger.cs:15/50` | التدقيق |
| `backend/Domain/Enums/ExecutionStatusCatalog.cs:8 + ExecutedStatusCatalog.cs:9 + DelegationStatusCatalog.cs:7 + AppealStatusCatalog.cs:8` | كتالوجات الحالات |
| `backend/Api/Authorization/RolePermissions.cs:9` | مصفوفة الصلاحيات |
| `backend/Infrastructure/TransactionRunner.cs:27 + Repository.cs:18 + DocumentRepository.cs:91/340/397/459/496` | المعاملات |
| `backend/Services/DocumentDelegationService.cs:51/418 + DocumentAppealService.cs:54/241` | الإنابات والاستئنافات |
| `backend/Program.cs:17-250 + appsettings.json:1-51` | نقطة الدخول والإعدادات |
| `frontend/src/api/client.ts:1 + auth/AuthContext.tsx:6 + App.tsx:1 + Layout.tsx:1` | اتصال وحماية مسارات |
| `backend/Infrastructure/Persistence/PortalRepository.cs:25-34` | رؤية مندوب الجهة |
| `backend/Domain/Entities/PublicEntity.cs:22-28` | coverageLabel |

---

## ملحق ب — قائمة تحقق الالتزام (AGENTS.md)

- [ ] Mobile-first + أهداف لمس `44px` + جداول→بطاقات (`DocumentsList.tsx`)
- [ ] كل التواريخ `type="text" placeholder="مثال: 1/8/2026"` → `normalizeArabicDigits` → `string?` → `ParseDateTime` → `DateTime?` → `yyyy-MM-dd`
- [ ] قاعدتان: SQLite + Postgres — أي هجرة تتطلب `dotnet ef database update --context DocGeneratorDbContext` و `DocGeneratorPostgresDbContext`
- [ ] لا حلول مختصرة — Clean Architecture — لا كسر للعقود — التحقق `dotnet test` + `vitest` + `oxlint` + `tsc -b` + `build`
- [ ] وصول: `aria-label` + `focus-visible` + `labels` + منع `transition: all` + `outline-none` بلا بديل

---

## سجل التحديثات

> **قاعدة:** بعد كل تعديل — أضف سطراً هنا يصف ما تغيّر وأين، وحدّث القسم المتأثر أعلاه.

| التاريخ | الوكيل/المطور | الوصف | الملفات المتأثرة |
|---|---|---|---|
| **27 آب 2026** | `Muse Spark — Principal Analysis` | إنشاء أولي للخريطة الذهنية (تحليل غير تدخلي) — 5 أقسام + 8 قرارات مؤكدة | `ARCHITECTURE_MAP.md` + `التقرير_السياقي_الشامل.docx` |
| **28 آب 2026** | `opencode — Task` | تنفيذ قرار (4) — ميزة **تغطية البدل للديون** (`SaleCoversFullDebt`): كيان+DTO+خدمة (تحقق إلزامي + تخزين + رسالة تنبيه) + هجرات EF مزدوجة + قائمة منسدلة في نموذج الإتمام + شارة عرض في التفاصيل + اختبارات طرفية. تحقق كامل أخضر. | `DocumentDelegation.cs`, `DelegationDtos.cs`, `DocumentDelegationService.cs`, `Configurations.cs`, هجرتا `AddDelegationSaleCoversFullDebt` (SQLite+Postgres), `types/index.ts`, `CompleteDelegationModal.tsx`, `DelegationDetails.tsx`, `CompleteDelegationModal.test.tsx`, `DelegationsIntegrationTests.cs` |

---

> **تنبيه النشر (Deploy Reminder — AGENTS.md:68-75):** أي تغيير يضيف هجرات EF لا يُطبّق تلقائياً — يجب `dotnet ef database update --context DocGeneratorDbContext` و `DocGeneratorPostgresDbContext` في نافذة النشر، وإلا `no such column` رغم نجاح الاختبارات.

