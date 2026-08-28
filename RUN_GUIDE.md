# دليل التشغيل والتجربة

## 0) التشغيل بكبسة زر (الأسهل)

انقر نقراً مزدوجاً على `start-app.bat` في `react-dotnet-app`:

1. يفتح نافذة «DocGen API» (الخلفية على `http://localhost:5199`) ونافذة «DocGen Frontend» (الواجهة على `http://localhost:5173`).
2. ينتظر ~15 ثانية ثم يفتح المتصفح تلقائياً على `http://localhost:5173`.
3. بعد التجربة انقر `stop-app.bat` لإيقاف الخدمتين.

> يتطلب `.NET SDK 8` و`Node.js 20+` في المسارات الافتراضية (`C:\Program Files\dotnet` و`C:\Program Files\nodejs`).
> يمكنك إيقاف التشغيل يدوياً أيضاً بالطريقة اليدوية في القسم 8.

## 1) المتطلبات

| الأداة | الإصدار | ملاحظة |
|--------|---------|--------|
| .NET SDK | 8.x | مطلوب للخلفية |
| Node.js | 20+ | مطلوب للواجهة |

> ⚠️ في هذا الجهاز الأدوات ليست على `PATH`، لذا في كل نافذة PowerShell جديدة نفّذ أولاً:
> ```powershell
> $env:PATH="C:\Program Files\dotnet;C:\Program Files\nodejs;$env:PATH"
> ```

## 2) تشغيل الخلفية (API)

```powershell
cd "C:\Users\pc\Desktop\document_generator\working 3.8 32b\react-dotnet-app\backend"
dotnet run --project src/DocGenerator.Api --urls http://localhost:5199
```

- عند أول تشغيل تُنشأ قاعدة SQLite `docgen.db` تلقائياً (migrations + بذر المستخدمين والفروع).
- يظهر نص مثل `Now listening on: http://localhost:5199` — اترك النافذة مفتوحة.

## 3) تشغيل الواجهة (Vite) — نافذة جديدة

```powershell
cd "C:\Users\pc\Desktop\document_generator\working 3.8 32b\react-dotnet-app\frontend"
npm run dev
```

- تفتح على `http://localhost:5173` وتُمرر طلبات `/api` تلقائياً إلى الخلفية على المنفذ `5199`.

## 4) فتح المتصفح والتسجيل

افتح `http://localhost:5173` وسجّل الدخول. الحسابات (كلمة السر للجميع `123456`):

| المستخدم | الدور | ماذا يفعل |
|----------|-------|-----------|
| `admin` | مشرف نظام | كل الصلاحيات + سجل التدقيق + نشاط المستخدمين |
| `manager` | مدير | قراءة كل المستندات، لا يُدخل ولا يحذف، سجل التدقيق |
| `head1` | رئيس قسم دمشق | يدخل ويعدّل ويحذف مستندات فرعه، يغيّر الحالة |
| `lawyer1` | محامي دمشق | يدخل ويعدّل **مستنداته فقط**، يغيّر حالتها |

## 5) سيناريوهات تجربة جاهزة

### أ) إدارة المستندات (كـ `lawyer1`)
- «مستند جديد» → املأ اسم المقترض ونوع العقد والمبلغ → حفظ.
- المستند يظهر في «المستندات» كمسودة.
- افتحه → «تغيير الحالة» → اختر «منفذ» أو «تريث» وأدخل بيانات البراءة/التريث.

### ب) الصلاحيات (كـ `head1`)
- جرّب الحذف → ينجح. ثم ادخل بنفسك كـ `manager` وجرّب الحذف → مرفوض.
- كـ `manager` حاول «مستند جديد» → رسالة «المدير لا يمكنه إدخال مستندات».

### ج) سجل التدقيق (كـ `admin` أو `manager`)
- من القائمة الجانبية «سجل التدقيق» → تشاهد كل الأحداث (`login`, `create`, `status`, `delete`...).
- استخدم فلتر المستخدم ونوع الحدث، وترقيم الصفحات في الأسفل.

### د) تغيير كلمة المرور
- من القائمة الجانبية «تغيير كلمة المرور» → غيّرها، ثم سجّل خروجاً وأعد الدخول بالجديدة.

### هـ) تحديد المحاولات (Rate Limit)
- من شاشة الدخول أدخل كلمة سر خاطئة 6 مرات → يظهر «محاولات دخول كثيرة جداً» (429) لمدة 5 دقائق.

## 6) تشغيل الاختبارات (اختياري)

الخلفية:

```powershell
cd "C:\Users\pc\Desktop\document_generator\working 3.8 32b\react-dotnet-app\backend"
dotnet test DocGenerator.sln
```

النتيجة المتوقعة: `61` اختباراً (39 وحدة + 22 تكامل عبر `WebApplicationFactory` بقاعدة مؤقتة معزولة — لن تلمس `docgen.db`).

الواجهة:

```powershell
cd "C:\Users\pc\Desktop\document_generator\working 3.8 32b\react-dotnet-app\frontend"
npm test
```

النتيجة المتوقعة: `5` اختبارات (Vitest + Testing Library) تغطي منطق نموذج إدخال الملف (عادي/مصرفي، الحقول، ترقيم الكفلاء).

## 7) Swagger (توثيق API)

الخلفية شغّالة → افتح `http://localhost:5199/swagger` في المتصفح لاستعراض/تجربة كل نقاط النهاية
(يتطلب زر `Authorize` بإدخال التوكن). مغلق خارج بيئة التطوير ما لم تُفعّل `Swagger:Enabled`.

## 8) الإيقاف

- **الأسهل:** انقر `stop-app.bat` (يوقف الخدمتين على المنفذين `5199` و`5173`).
- أو يدوياً:
  - أوقف الواجهة: اضغط `Ctrl+C` في نافذة Vite.
  - أوقف الخلفية: اضغط `Ctrl+C` في نافذة `dotnet run`.

## 9) ملاحظات الإنتاج

- فعّل `Database:UsePostgres = true` واضبط `ConnectionStrings:DefaultConnection` على PostgreSQL.
- عيّن `Jwt:Secret` قوياً (إلزامي خارج بيئة التطوير).
- `Swagger:Enabled` مغلق افتراضياً خارج التطوير؛ فعّله صراحةً عند الحاجة.
- `RateLimiting:MaxLoginAttempts` / `RateLimiting:WindowMinutes` — إعدادات تحديد محاولات الدخول
  (مخزّنة في جدول `LoginAttempts` لتكون مشتركة بين عقد النشر).

### تطبيق هجرات قاعدة البيانات عند النشر (إلزامي)

- أي تغيير في الكود يضيف/يعدّل هجرات EF (`Persistence\Migrations\` لـ SQLite و`src\DocGenerator.Infrastructure\MigrationsPostgres\` لـ PostgreSQL) **لا يصل إلى قواعد البيانات الموجودة تلقائيًا** — الكود الجديد وحده لا يكفي.
- أثناء نافذة نشر خاضعة للتحكم نفّذ على الخادم (من مجلد `backend\`):
  ```powershell
  dotnet ef database update --context DocGeneratorDbContext          # قاعدة SQLite المحلية
  dotnet ef database update --context DocGeneratorPostgresDbContext   # قاعدة PostgreSQL في الإنتاج
  ```
- تتصل الأوامر بسلسلة الاتصال الموجودة في `appsettings.json` لبيئة الخادم (مع `Database:UsePostgres = true` لقاعدة الإنتاج). إن لم يكن `dotnet-ef` مثبتًا عالميًا: `dotnet tool install --global dotnet-ef`.
- **تذكير للجلسات القادمة**: بعد كل تعديل يضيف هجرات جديدة، يجب ذكرها بالاسم وعددها في تقرير الإنجاز وتنبيه النشر — انظر `AGENTS.md`.

### هجرات بانتظار التطبيق (قائمة تراكمية — تُشطب بعد التطبيق)

> ضع هنا أي هجرة جديدة لم تُطبَّق على قواعد البيانات بعد، واشطب السطر بعد `dotnet ef database update` الناجح في النشر.

- [ ] **2026-08-19 — `AddHeadAlertDelegationLink`** (ربط تنبيهات رؤساء الأقسام بالمندوبية):
  - `DocGeneratorDbContext` (SQLite): `Persistence\Migrations\20260819124219_AddHeadAlertDelegationLink.cs` — يضيف عمود `DelegationId` إلى `HeadAlerts` (FK SetNull + فهرس).
  - `DocGeneratorPostgresDbContext` (PostgreSQL): `MigrationsPostgres\20260819124309_AddHeadAlertDelegationLink.cs` — نفسه.
  - بدون التطبيق سيفشل ربط تنبيه المراجعة بالمندوبية برسالة `no such column: h.DelegationId`.
- [ ] **2026-08-20 — `AddForcibleTransferAndSnapshotAdjusted`** (ميزة «تاريخ قرار الإحالة القطعية»/«اعتبار الملف منفذًا كاملًا بهذا البيع»):
  - `DocGeneratorDbContext` (SQLite): `Persistence\Migrations\20260820180256_AddForcibleTransferAndSnapshotAdjusted.cs` — يضيف `ForcibleTransferDate` و`ForcibleTransferNoticeNumber` إلى `Documents` و`SnapshotAdjusted` إلى `DelegationAssets`.
  - `DocGeneratorPostgresDbContext` (PostgreSQL): `MigrationsPostgres\20260820180319_AddForcibleTransferAndSnapshotAdjusted.cs` — نفسه.
- [ ] **2026-08-20 — `RemoveSendBookColumns`** (حذف أعمدة كتاب الإرسال من المندوبية):
  - `DocGeneratorDbContext` (SQLite): `Persistence\Migrations\20260820131251_RemoveSendBookColumns.cs` — يحذف `SendBookDate` و`SendBookNumber` من `DocumentDelegations`.
  - `DocGeneratorPostgresDbContext` (PostgreSQL): `MigrationsPostgres\20260820131305_RemoveSendBookColumns.cs` — نفسه.
- [ ] **2026-08-20 — `AlignPostgresTimestampTypes`** (تنسيق أنواع التوقيت في PostgreSQL فقط):
  - `DocGeneratorPostgresDbContext` (PostgreSQL): `MigrationsPostgres\20260820211413_AlignPostgresTimestampTypes.cs` — يحوّل `ForcibleTransferDate` و`ExecutedExecutionDate` في `Documents` إلى `timestamp with time zone`.
  - ⚠️ هذا للـ PostgreSQL فقط — لا يوجد SQLite مقابل.
- [ ] **2026-08-22 — `AddAssetSeizureDate`** (تاريخ صك seizure على الأصول):
  - `DocGeneratorDbContext` (SQLite): `Persistence\Migrations\20260822215648_AddAssetSeizureDate.cs` — يضيف عمود `SeizureDate` إلى `Assets`.
  - `DocGeneratorPostgresDbContext` (PostgreSQL): `MigrationsPostgres\20260822215846_AddAssetSeizureDatePg.cs` — نفسه.
  - بدون التطبيق سيفشل عرض/حفظ تاريخ صك seizures برسالة `no such column: a.SeizureDate`.
- [ ] **2026-08-23 — `AddDocumentAppeals` / `AddDocumentAppealsPg`** (ميزة «الاستئنافات» — المرحلة 1):
  - `DocGeneratorDbContext` (SQLite): `Persistence\Migrations\20260823110935_AddDocumentAppeals.cs` — ينشئ جداول `DocumentAppeals` و`AppealActions` و`AppealBaseNumbers` ويضيف عمود `AppealId` إلى `HeadAlerts`.
  - `DocGeneratorPostgresDbContext` (PostgreSQL): `MigrationsPostgres\20260823110958_AddDocumentAppealsPg.cs` — نفسه.
  - بدون التطبيق سيفشل الإتمام/الاعتبار فعليًا برسالة `no such column: …` رغم نجاح الاختبارات محليًا.
- [ ] **2026-08-24 — `AddReviewLetters`** (ميزة خطابات المراجعة — المرحلة 1):
  - `DocGeneratorDbContext` (SQLite): `Persistence\Migrations\20260824060821_AddReviewLetters.cs` — ينشئ جداول `ReviewLetters` و`ReviewLetterMessages` مع FKs وفهارس.
  - `DocGeneratorPostgresDbContext` (PostgreSQL): `MigrationsPostgres\20260824064804_AddReviewLettersPg.cs` — نفسه.
  - بدون التطبيق سيفشل فتح شاشة خطابات المراجعة برسالة `no such table: ReviewLetters`.
- [ ] **2026-08-24 — `AddReviewLetterNotifications`** (إشعارات خطابات المراجعة):
  - `DocGeneratorDbContext` (SQLite): `Persistence\Migrations\20260824073441_AddReviewLetterNotifications.cs` — يضيف `IsSeenByLawyer` إلى `ReviewLetterMessages` و`ReviewLetterId` إلى `HeadAlerts` (FK SetNull + فهرس).
  - `DocGeneratorPostgresDbContext` (PostgreSQL): `MigrationsPostgres\20260824073458_AddReviewLetterNotificationsPg.cs` — نفسه.
  - بدون التطبيق سيفشل إرسال إشعار خطاب مراجعة للمحامي برسالة `no such column: h.ReviewLetterId`.
- [ ] **2026-08-24 — `AddDocumentFieldChanges`** (تغييرات حقول المستند في سجل التدقيق):
  - `DocGeneratorDbContext` (SQLite): `Persistence\Migrations\20260824104831_AddDocumentFieldChanges.cs` — ينشئ جدول `DocumentFieldChanges` مع FK إلى `AuditLogs` + فهارس مركبة.
  - `DocGeneratorPostgresDbContext` (PostgreSQL): `MigrationsPostgres\20260824104844_AddDocumentFieldChangesPg.cs` — نفسه.
  - بدون التطبيق سيفشل تسجيل تغييرات الحقول في سجل التدقيق برسالة `no such table: DocumentFieldChanges`.
- [ ] **2026-08-24 — `AddEntityRegistry` / `AddEntityRegistryPg`** (ميزة «بوابة الجهات العامة» — المرحلة 1):
  - `DocGeneratorDbContext` (SQLite): `Persistence\Migrations\20260824173935_AddEntityRegistry.cs` — ينشئ جداول `PublicEntityGroups` و`PublicEntities` و`PublicEntityAliases` و`PublicEntityProposals` ويضيف عمود `Governorate` إلى `Branches`.
  - `DocGeneratorPostgresDbContext` (PostgreSQL): `MigrationsPostgres\20260824174004_AddEntityRegistryPg.cs` — نفسه.
  - بدون التطبيق سيفشل فتح شاشة «سجل الجهات العامة» وأي عملية فيها برسالة `no such table: …`.
- [ ] **2026-08-24 — `AddEntityRegistryLinks` / `AddEntityRegistryLinksPg`** (بوابة الجهات العامة — المرحلة 2):
  - `DocGeneratorDbContext` (SQLite): `Persistence\Migrations\20260824190803_AddEntityRegistryLinks.cs` — يضيف `RegistryId` إلى `ApplicantPublicEntities` و`ExecutedPublicEntities` (FK SetNull + فهارس) و`ApplicantRegistryId` إلى `Documents` (فهرس).
  - `DocGeneratorPostgresDbContext` (PostgreSQL): `MigrationsPostgres\20260824190820_AddEntityRegistryLinksPg.cs` — نفسه.
  - بدون التطبيق سيفشل حفظ أي ملف برسالة `no such column: r.RegistryId`.
- [ ] **2026-08-24 — `AddEntityPortal` / `AddEntityPortalPg`** (بوابة الجهات العامة — المرحلة 3):
  - `DocGeneratorDbContext` (SQLite): `Persistence\Migrations\20260824202637_AddEntityPortal.cs` — يضيف `PortalGroupId` و`PortalEntryId` إلى `Users` (FK SetNull + فهارس) لنطاق حسابات المندوبين.
  - `DocGeneratorPostgresDbContext` (PostgreSQL): `MigrationsPostgres\20260824202706_AddEntityPortalPg.cs` — نفسه.
  - بدون التطبيق سيفشل إنشاء/قراءة أي حساب مندوب برسالة `no such column: u.PortalGroupId`.
- [ ] **2026-08-26 — `AddEntityRegistryReview` / `AddEntityRegistryReviewPg`** (بوابة الجهات — نموذج الحوكمة الجديد):
  - `DocGeneratorDbContext` (SQLite): `Persistence\Migrations\20260826010109_AddEntityRegistryReview.cs` — يضيف `NeedsReview/ReviewedAtUtc/ReviewedById` إلى `PublicEntities` (فهارس + FK SetNull) و**يُسقط جدول `PublicEntityProposals`**.
  - `DocGeneratorPostgresDbContext` (PostgreSQL): `MigrationsPostgres\20260826010122_AddEntityRegistryReviewPg.cs` — نفسه.
  - ⚠️ يسقط بيانات الاقتراحات القديمة نهائيًا — تأكد قبل التطبيق، وبعدده فشل شاشة المراجعة (`no such column`).
- [ ] **2026-08-26 — `AddCoverageLabel` / `AddCoverageLabelPg`** (تسمية التغطية + سجل تغييرات الهوية — المرحلة 1):
  - `DocGeneratorDbContext` (SQLite): `Persistence\Migrations\20260826152120_AddCoverageLabel.cs` — يضيف عمود `CoverageLabel` إلى `PublicEntities` (max 150) وينشئ جدول `PublicEntityChangeEvents` مع 3 FKs و5 فهارس.
  - `DocGeneratorPostgresDbContext` (PostgreSQL): `MigrationsPostgres\20260826152156_AddCoverageLabelPg.cs` — نفسه بأنواع Postgres (`character varying(150)` + `timestamp with time zone`).
  - بدون التطبيق سيفشل إنشاء قيد جديد بتسمية تغطية وأي عملية نقل/طيّ برسالة `no such column: e.CoverageLabel` أو `no such table: PublicEntityChangeEvents`.
- [ ] **2026-08-26 — `AddEntityEvents` / `AddEntityEventsPg`** (Snapshot فارغ — الجدول أُنشئ أعلاه):
  - `DocGeneratorDbContext` (SQLite): `Persistence\Migrations\20260826152236_AddEntityEvents.cs` — فارغ (لا إجراء).
  - `DocGeneratorPostgresDbContext` (PostgreSQL): `MigrationsPostgres\20260826152302_AddEntityEventsPg.cs` — نفسه.
- [ ] **2026-08-26 — `AddIsActiveIndex` / `AddIsActiveIndexPg`** (فهرس `IsActive` على `PublicEntities`):
  - `DocGeneratorDbContext` (SQLite): `Persistence\Migrations\20260826181557_AddIsActiveIndex.cs` — ينشئ `IX_PublicEntities_IsActive`.
  - `DocGeneratorPostgresDbContext` (PostgreSQL): `MigrationsPostgres\20260826181623_AddIsActiveIndexPg.cs` — نفسه.
- [ ] **2026-08-27 — `AddDelegationSaleCoversFullDebt`** (ميزة «تغطية بدل المبيع لكامل المديونية» عند إتمام الإنابة):
  - `DocGeneratorDbContext` (SQLite): `Persistence\Migrations\20260827131401_AddDelegationSaleCoversFullDebt.cs` — يضيف عمود `SaleCoversFullDebt` (nullable `bool`) إلى `DocumentDelegations`.
  - `DocGeneratorPostgresDbContext` (PostgreSQL): `MigrationsPostgres\20260827131448_AddDelegationSaleCoversFullDebt.cs` — نفسه (`boolean` nullable).
  - بدون التطبيق سيفشل إتمام الإنابة برسالة `no such column: d.SaleCoversFullDebt` رغم نجاح الاختبارات محليًا.
