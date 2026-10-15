# خطة الإصلاح — `FIX-PLAN.md`

> مرافقة لـ `FINAL-REPORT.md`. مرتّبة بحسب الخطورة. **لا يُعدَّل أي كود دون موافقة المالك** (شرط هذه الجلسة).
> كل بند يذكر الموقع، الدليل، خطوات الإصلاح المقترحة، والاختبار المطلوب.
>
> ## سجل التنفيذ (2026-10-15 — بموافقة المالك، بلا دمج)
> | البند | الحالة | ملاحظة |
> |---|---|---|
> | `FR-001` | نُفذ | سطران فقط؛ سكربت `PG` المولَّد خالٍ من `datetime2` (مؤكَّد). يبقى التحقق على `PG` حية + التطبيق في نافذة النشر |
> | `FR-002` | نُفذ | مهمة `migration-guard` + مهمة `backend-postgres` في `ci.yml` (تُختبران عند أول تشغيل `CI`) |
> | `FR-003`/`FR-004` | نُفذ جزئيًا | حارس `NULL` + توثيق `TotalAmount` كموروث؛ الإزالة من عدمها قرار مالك معلَّق |
> | `FR-005` | نُفذ | `NormalizeDigitsOrNull` مركزي + المسار العام؛ مسارا الإنابة/الدوائر يتحققان أصلًا |
> | `FR-006` | نُفذ | حفظ واحد عبر التنقل |
> | `FR-007` | نُفذ | معاملة + حدث `lockout_released` + تسمية الواجهة |
> | `FR-009` | نُفذ مضيَّقًا | تبيَّن وجود `HtmlInputSanitizer` مركزي مطبَّق على المراسلات/الخطابات/الإجراءات — الناقص كان `AppealAction.Text` فقط (عُولج)؛ `DefenseOpinion` لا يُعرض `HTML` أصلًا |
> | `FR-010` | نُفذ جزئيًا | خيار `CspReportUri` + تغطية اختبارية؛ الفرض (`ReportOnly=false`) إعداد نشر |
> | `FR-011` | نُفذ | تضييق الواجهة (لا منادٍ للكتابة — مدقَّق) |
> | `FR-012` | نُفذ | `asyncUtilTimeout: 5000` على مستوى الملف؛ التشغيل الكامل أخضر `1562/1562` |
> | `FR-013` | نُفذ | صفر تحذيرات بناء |
> | `FR-014` | فرز فقط | عمل سابق غير ملتزم (`e2e`/`playwright`/حزم) لم يُمسّ — يُحسم بقرار مالك |
> | `FR-008` | مؤجل للتصميم | حسب مراجعة الخبير (تعارض مع دفتر التنقية) |
> | `FR-015` | سُحب — لا ثغرة | تحقق كامل: رسائل ثابتة + `false` (لا تسريب) |
> - البوابات بعد التنفيذ: بناء `0/0`، خلفية `2487/2487`، `oxlint` صفر، `tsc` نظيف، `vitest` `1562/1562`، بناء الواجهة ناجح، `dotnet vulnerable` نظيف، `npm audit` نظيف.
> - الحكم `NO-GO` قائم حتى التحقق على `PostgreSQL` حية والتطبيق في نافذة النشر.

---

## `FR-001` — `Blocker` : إصلاح `datetime2` في هجرة `PostgreSQL`

**الموقع:** `backend/src/DocGenerator.Infrastructure/Persistence/MigrationsPostgres/20260928082313_AddPersonalRemindersPg.cs:24,27`

**الدليل:**
```csharp
DueDate = table.Column<DateTime>(type: "datetime2", nullable: false),
RecurrenceEnd = table.Column<DateTime>(type: "datetime2", nullable: true),
```
وسكربت `PG` المُولَّد يُنتج `"DueDate" datetime2 NOT NULL` ⇒ فشل `CREATE TABLE`.

**خطوات الإصلاح:**
1. تغيير النوع إلى `"timestamp with time zone"` للعمودين (`DueDate`, `RecurrenceEnd`).
2. إعادة توليد لقطة النموذج (`DocGeneratorPostgresDbContextModelSnapshot.cs`) وملف `Designer` المقابل إن لزم، للتأكد من تطابق النوعين مع النموذج.
3. الإبقاء على `RF013_ReminderTimestamptzPg` (يصبح إعادة تثبيت آمنة للنوع) — لا يُحذف.
4. التحقق بإعادة توليد السكربت: يجب ألّا يظهر `datetime2` في مخرج `dotnet ef migrations script --context DocGeneratorPostgresDbContext`.
5. (موصى به) إضافة اختبار `PostgreSQL` فعلي إلى `CI` يمنع تكرار النوع (راجع `FR-002`).

**الاختبار:**
- `dotnet ef migrations script --context DocGeneratorPostgresDbContext` ثم التأكد من غياب `datetime2`.
- تشغيل البوابة الخلفية `dotnet test backend/DocGenerator.sln`.

**شرط قبول:** لا `datetime2` في سكربت `PG`، وتُطبَّق السلسلة على قاعدة `PostgreSQL` جديدة بلا خطأ.

---

## `FR-002` — `High` : تغطية هجرات `PostgreSQL` في `CI`

**الموقع:** `.github/workflows/ci.yml` + `backend/tests/DocGenerator.Api.Tests/DatabaseInitializerTests.cs:430-432` + `backend/tests/DocGenerator.Application.Tests/UtcDateTimeConverterTests.cs:167-169`

**الدليل:** الاختبارات تُرجع مبكرًا ما لم يُضبط `DOCGEN_TEST_POSTGRES`، و`ci.yml` لا يضبطه ولا يشغّل خدمة `Postgres`.

**خطوات الإصلاح:**
0. (فوري، بلا خدمة) اختبار حارس غير متصل: توليد سكربت `PG` في `CI` والتأكد من خلوّه من `datetime2` — يمنع هذا الصنف من الأخطاء مباشرةً.
1. إضافة خدمة `postgres` إلى مهمة `backend` في `ci.yml` (أو مهمة مستقلة).
2. ضبط `DOCGEN_TEST_POSTGRES` ببيانات الخدمة.
3. تشغيل اختبارات الخدمة/الهجرات ضدها.

**الاختبار:** ملاحظة تشغيل اختبارات `PostgreSQL` فعليًا (بعدد غير صفري) في `CI`.

---

## `FR-003` + `FR-004` — `Low (كامن)` : إحصاء `dashboard` القديم (غير مستهلك)

**الموقع:** `StatisticsRepository.cs:47-55` و`:67-70` (+ `DTOs/StatsDtos.cs`)

**خطوات الإصلاح (خياران):**
- **إزالة:** بحث شامل عن المستهلكين أولًا (واجهة، سكربتات، توثيق — تغيير كاسر محتمل)، ثم حذف نقطة `GET /api/dashboard` و`DashboardStatsDto` ومستهلكيها إن أُقرّ أنها غير مستخدمة (الواجهة تستخدم `/stats/manager`).
- **إصلاح:** فصل العملات في `totalAmount`/`totalCollectedAmount` عبر بنية `CurrencyAmountDto` نفسها المستخدمة في `ManagerStats`، وحرس `ExecSubStatus == null` في عدّادي `Executed`/`Active` ليطابق `DocumentRepository.cs:232-259`.

**الاختبار:** إضافة/تحديث اختبارات `StatisticsRepository` لحالتَي: تعدد العملات، و`ExecSubStatus == null`.

---

## `FR-005` — `Low` : الرقم الفارغ `''` داخل نطاق الفهرس الفريد

**تصحيح:** ادّعاء التفاوت السابق سُحب — هجرة `RF009` الأحادية انتقالية مستبدَلة؛ المخطط الفعلي (النموذج + السلسلتان + اللقطة) فهرسان جزئيان يطابقان فرعي الفحص الخدمي.

**الموقع:** `DocumentService.cs:606-608` + `DigitNormalizer.cs:12-17` + فلترا `IX_Documents_ExecutionCircuitId_FileNumber_FileType_FileYear` و`IX_Documents_CourtNorm_FileNumber_FileType_FileYear`

**الدليل:** `NormalizeDigits('')` تُعيد `''` كما هو، و`ApplyRequest` يحفظه بلا توحيد إلى `NULL`، بينما الفلتران يستبعدان `NULL` فقط ⇒ صفّان بلا رقم (`''`) في نفس الدائرة/النوع/السنة يصطدمان قاعديًا (`409` كاذب؛ الفحص الخدمي يتجاوز الفراغ).

**الإصلاح (خياران):** توحيد `''` إلى `NULL` عند الكتابة، أو توسيع الفلترين بـ`AND "FileNumber" <> ''` (هجرتان).

**الاختبار:** اختبار تكامل ينشئ ملفين بلا رقم في نفس الدائرة ويثبت القبول.

---

## `FR-006` — `Low` : حفظان في `AuditLogger`

**الموقع:** `AuditLogger.cs:60-78`
**الإصلاح:** إدراج السجل وتغييرات الحقول ثم `SaveChangesAsync` واحد (العلاقة تُعبّأ عبر التنقل أو معرف مؤقت)، أو ضمان لفّ كل المستدعين بمعاملة صريحة.

---

## `FR-007` — `Low` : تحرير القفل بلا تدقيق/خارج معاملة

**الموقع:** `AuthService.cs:110-116`
**الإصلاح:** نقل التحرير داخل معاملة (`_tx.RunAsync`) وإضافة حدث تدقيق (`lockout_released`) بنفس أسلوب `login_locked`.

---

## `FR-008` — `Low` : `TRUNCATE` يتجاوز إلحاق-فقط

**الموقع:** `20261002162231_RF019_AuditAppendOnlyPg.cs`
**الإصلاح (خيارات):** `REVOKE TRUNCATE ON "AuditLogs", "DocumentFieldChanges" FROM <أدوار التطبيق>` في دليل النشر (لا مشغّل صف لـ`TRUNCATE` في `PostgreSQL`)، و/أو مشغّل حدث `ddl_command_start` يمنعه صراحةً، مع إبقاء إجراء التنقية الموثّق.

---

## `FR-009` — `Low` : تعقيم النص الغني عند التخزين (نُفذ مضيَّقًا)

**تصحيح النطاق (بعد الجرد):** تبين وجود معقِّم خادمي مركزي (`HtmlInputSanitizer`, `Ganss.Xss` بقائمة
مطابقة للواجهة) مطبَّق أصلًا على المراسلات (`CorrespondenceService` ×3) والخطابات
(`ReviewLetterService` ×3) والإجراءات (`ExecutionActionService.NormalizeAction`) — الناقص الوحيد
المؤكد كان `AppealAction.Text` (إضافة/تعديل)؛ و`DefenseOpinion`/`AppealedDecisionText` لا يُعرضان `HTML`
أصلًا فخرجا من النطاق. لا تبعية جديدة ولا سياسة جديدة.

**الموقع:** `DocumentAppealService.cs` (مسارا `AddAction`/`UpdateAction`)
**الإصلاح (المطبَّق):** `RequireText(HtmlInputSanitizer.Sanitize(request.Text), …)` — الخبيث الخالص يُرفض
لفراغه بعد التعقيم، والتنسيق المباح يُحفَظ كما هو، وتعقيم العرض يبقى طبقة ثانية.

**الاختبار (المطبَّق):** اختباران في `DocumentAppealServiceTests` (تجريد الوسوم المرفوضة + عدم تخزين خام أبدًا).

---

## `FR-010` — `Low` : ترويسات الأمان

**الموقع:** `appsettings.json`
**الإجراء:** تقرير إعداد الإنتاج الفعلي؛ تفعيل `HSTS` و`HTTPS Redirection`، وجعل `CSP` حاجزًا (لا `Report-Only`) — أو توثيق القرار المخالف صراحةً.

---

## `FR-011` — `Low` : `Repository<T>` وسجل التدقيق

**الإصلاح:** تقييد واجهة المستودع الخاصة بـ `AuditLog` (قراءة/إضافة فقط) بدل كشف `Update`/`Remove`.

---

## `FR-012` — `Low` : هشاشة اختبارات الواجهة

**الموقع:** `frontend/src/App.test.tsx:179`
**الإصلاح:** تثبيت انتظار العنوان بدل الاعتماد على زمن التحميل (تثبيت `waitFor`/معرّفات صريحة)، أو عزل الاختبار الهش.

---

## `FR-013` — `Info` : تحذيرات `nullable`

**الموقع:** `DocumentAppealService.cs:805`, `DocumentRepository.cs:98,101`
**الإصلاح:** معالجة الاحتمال الصفري صراحةً.

---

## `FR-014` — `Info` : شجرة العمل غير نظيفة

**الإجراء:** مراجعة/حسم تغييرات `frontend/e2e/`, `playwright.config.ts`, `package.json`, `package-lock.json`, `tsconfig.node.json`, `.gitignore` قبل النشر (التزامًا بنظام `PR` على `master`).

---

## `FR-015` (`NR-03`) — سُحب: لا ثغرة (مؤكَّد بالقراءة الكاملة)

**الدليل:** `ChangePasswordAsync:195-214` لا يُرجع أي `e.Message` — أخطاء التحقق رسائل عربية ثابتة
(`ArgumentException` برسالة ثابتة)، وكلمة المرور الخاطئة تُرجع `false`. والمتحكم (`AuthController:119-130`)
يتبع النمط المعمّم المقصود (`catch ArgumentException → BadRequest`) برسائل مُصاغة للمستخدم بلا آثار داخلية.
لا إجراء مطلوب.

---

## ترتيب التنفيذ

1. `FR-001` (حاجب) ثم `FR-002` (حماية من التكرار).
2. `FR-003`/`FR-004` (قرار نقطة قديمة: إزالة أم إصلاح).
3. `FR-005` → `FR-015` (تحسينات ومتانة).
