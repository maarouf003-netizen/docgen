# قائمة تحقق النشر (Deployment Checklist)

> **المرجع**: `README` الجاهزية `READINESS-REPORT.md` و`RUN_GUIDE.md` §9.
> **صلاحية**: لأي مضيف يحقق الشروط الأربعة (قاعدة `Postgres` مُدارة، نسخ + استعادة مختبَرة، `TLS` كامل، أسرار بيئية). `render.yaml` هدف مرجعي لا قيد.

---

## 0) قبل فتح نافذة النشر

- [ ] **الفرع والدمج**: كل العمل على فرع، والبوابات الأربع خضراء (`backend` + `frontend` + `guards` + `audit` في CI)، ولا دفع مباشر إلى `master`.
- [ ] **لا أسرار في المستودع**: `git ls-files` خالٍ من `.db`/`.env`/`appsettings.Development.json`/`bin`/`obj`/`logs`/`node_modules`/`dist` (تحقق: ✅ في هذا التدقيق).
- [ ] **`Jwt__Secret` مُضبوط**: ≥ `32` بايت عشوائي، في الأسرار البيئية فقط (`Bootstrap__AdminPassword` كذلك).
- [ ] **قرار المحرك**: `Database__UsePostgres=true` مُضبوط (و/أو `DATABASE_URL` محقون غير فارغ) — وإلا **يرفض التطبيق الإقلاع** خارج التطوير (`H-01`). تأكد أن `Database__AllowSqliteInProduction` **غير** مُضبوط على نشر `Postgres`.
- [ ] **نسخة احتياطية**: نسخة كاملة + `PITR` مُفعّل قبل أي نشر يمسّ المخطط (`RPO` 24س / `RTO` 4س).
- [ ] **سجل الهجرات الحي**: `SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY 1;` محفوظ للمقارنة.
- [ ] **فحص `Postgres` قبل النشر**: قاعدة مؤقتة، تطبيق هجرات `Postgres`، ثم:
  `$env:DOCGEN_TEST_POSTGRES="Host=...;Username=...;Password=..."` + `dotnet test backend/tests/DocGenerator.Api.Tests --filter "FullyQualifiedName~DatabaseInitializerTests"` — **يجب أن يخضرّ** (`M-06`).
- [ ] **استعلامات كشف الهجرات ذات القيود** (يجب أن تُرجع فارغًا) — بحسب `RUN_GUIDE.md`:
  - صفوف `Lawyer`/`Head` بلا فرع (`RequireBranchForBranchRoles`).
  - تكرار (الدائرة+الرقم+النوع+السنة) الفعّال (`RF009`).
  - رئيسا قسم مفعّلان في نفس الفرع (`AddSubHeadSections`).
  - أي تكرار في `CanonicalNameNorm`/`CourtNorm` (`PB001`) وصفوف حالة يتيمة (`PB002`).

## 1) أثناء النافذة

- [ ] إيقاف الكتابة/الإدخال أو إعلان صيانة.
- [ ] **تطبيق الهجرات** — القرار المنفَّذ: **تلقائي عند الإقلاع بقفل `EF Core` المدمج** (`H-02`)، بلا قفل استشاري مخصص:
  ```powershell
  dotnet ef database update --context DocGeneratorDbContext          # SQLite (local only)
  dotnet ef database update --context DocGeneratorPostgresDbContext   # PostgreSQL (production)
  ```
  التطبيق التلقائي متسلسَل بقفل الهجرات المدمج فيمنع تسابق نسخ عدة، لكنه **لا يغني عن النسخة الاحتياطية** (هجرة قد تسقط). الأوامر أعلاه مساندة اختيارية للتطبيق اليدوي المسبق في النوافذ الحرجة فقط.
- [ ] **فحص الانحراف** باتصال قراءة فقط: سجل `__EFMigrationsHistory` الحي = هجرات الحزمة؛ أي فرق = **إيقاف وتحقيق**.
- [ ] نشر التطبيق (خلفية + واجهة مبنية في `wwwroot` بنفس الأصل).
- [ ] **إصلاحات `H-01`/`H-02`** مطبَّقة في الإصدار المُنشَر.

## 2) بعد النافذة (فحص دخاني — أي فشل = تراجع)

- [ ] `GET /healthz` يرد صحّة قاعدة البيانات.
- [ ] دخول بحساب حقيقي (مدير النظام) ناجح؛ تغيير كلمة المرور يُبطل الجلسات القديمة.
- [ ] بحث في المستندات + فتح تفاصيل + توليد ملف `Word`.
- [ ] فحص ترويسات الأمن فعليًا: لا `Server`، ووجود `Content-Security-Policy`/`X-Content-Type-Options`/`X-Frame-Options` (وعلى المضيف الخاص: `Strict-Transport-Security`).
- [ ] ظهور عنوان العميل الحقيقي في السجل/مفتاح محاولات الدخول (`KnownProxies` — `M-04`).
- [ ] فتح شاشات الأدوار الأساسية: محامي/رئيس قسم/رئيس شعبة/مدير/مشرف/مندوب جهة.
- [ ] تأكيد أن `entitymanager` محجوب عن مسارات الـ`API` العامة.
- [ ] مراقبة ردود `429` بعد النشر لضبط حدود المعدل على الاستخدام الحقيقي.
- [ ] التأكد من كتابة مجلد `logs/` (صلاحيات الكتابة) وإلا بقيت الأخطاء على `stdout` فقط.

## 3) التراجع (Rollback)

- [ ] **استعادة النسخة حصرًا** — لا `Down` على قاعدة حيّة (`RUN_GUIDE.md:173-190`).
- [ ] تحذيرات `Down` الموثقة: `AllowMultipleBaseNumbersPerYear` (يفشل بتكرار)، `AddAppSuggestions`/`AddExecutionCircuitRegistry`/`AddEntityRegistryReview` (**خسارة بيانات**)، هجرتا التعبئة (`Down` فارغ عمدًا).

## 4) قائمة الهجرات المعلّقة (تراكمية — تُشطب بعد التطبيق الناجح)

> من `RUN_GUIDE.md:212-357`. **يجب** التأكد من تطبيقها جميعًا في النشر القادم؛ إهمالها = `no such column/table` رغم خضرة الاختبارات.
> التنبيه: هذا نظام بقاعدتَي `SQLite` و`Postgres`، ولكل هجرة نظير غالبًا — التأكد من تطبيق **السياقين**.

- [ ] `AddHeadAlertDelegationLink` (2026-08-19)
- [ ] `AddForcibleTransferAndSnapshotAdjusted` (2026-08-20)
- [ ] `RemoveSendBookColumns` (2026-08-20)
- [ ] `AlignPostgresTimestampTypes` (**Postgres فقط**)
- [ ] `AddAssetSeizureDate` (2026-08-22)
- [ ] `AddDocumentAppeals` (2026-08-23)
- [ ] `AddReviewLetters` / `AddReviewLetterNotifications` (2026-08-24)
- [ ] `AddDocumentFieldChanges` (2026-08-24)
- [ ] `AddEntityRegistry` / `AddEntityRegistryLinks` / `AddEntityPortal` (2026-08-24)
- [ ] `AddEntityRegistryReview` / `AddCoverageLabel` / `AddEntityEvents` / `AddIsActiveIndex` (2026-08-26)
- [ ] `AddDelegationSaleCoversFullDebt` (2026-08-27)
- [ ] `AddParentEntityFlag` / `AddHeadAlertPublicEntityLink` (2026-08-29)
- [ ] `AddExecutionApplicantRegistryId` (2026-09-02)
- [ ] `AllowMultipleBaseNumbersPerYear` / `AddOccurrenceSource` (2026-09-14)
- [ ] `BackfillMirrorLawyer` / `BackfillDelegationTargetCourt` (2026-09-19/20)
- [ ] `AddDelegationAssetReservations` (2026-09-20)
- [ ] `AddReferredToStartStatus` (2026-09-22)
- [ ] `AddCorrespondences` (2026-09-24)
- [ ] `RequireBranchForBranchRoles` (2026-10-01)
- [ ] `RF009_NumberingUniqueIndex` / `RF010_ConcurrencyVersion` / `RF013_ReminderTimestamptzPg` / `RF019_AuditAppendOnly` / `RF011_IdempotencyKeys` (2026-10-02)
- [ ] `PB001_Normalization` / `PB002_StatusChecks` (2026-10-04)
- [ ] `AddSubHeadSections` (2026-10-06)

**ملاحظة**: القاعدة الحالية فارغة/تجريبية وفق التوثيق، فأول إقلاع يطبّقها تلقائيًا **إن بقي التطبيق التلقائي**؛ لكن على قاعدة حيّة يجب المرور بالاستعلامات أعلاه أولًا.
