# `P3-04d` — إسقاط تصدير بوابة الجهة + مراجعة `P3-04c` النهائية — 2026-11-22

> **النطاق:** تحويل تصدير بوابة المندوب (`PortalService.ExportWorkbookAsync` عبر
> `PortalRepository.ExportScopedAsync`) من تحميل شجرة `Include` كاملة إلى صفوف
> القائمة المُسقَطة نفسها، واستخراج إسقاط الصفوف المشترك، وإغلاق ملاحظات المراجعة
> النهائية لـ`P3-04c`. **لا تغيير في عقد API الخارجي ولا في الواجهة ولا هجرة.**

## 1. المراجعة النقدية النهائية لـ`P3-04c` (وجد/دليل/إصلاح/متبقٍ)

| # | الوجد | الدليل | الإصلاح | متبقٍ |
|---|---|---|---|---|
| 1 | الخريطة تقول «متبقٍ إسقاط التصدير»، لكن تصدير الملفات **مُسقَط بالفعل** (`ExportRowsAsync`/`DocumentExportRow`) | قراءة `DocumentSearchService.ExportAsync` | تصحيح الخريطة: المتبقي الحقيقي هو `ExportScopedAsync` (البوابة) فقط، وقد أُنجز هنا | لا |
| 2 | مسارات `…/list` الخمسة بلا اختبار تكافؤ صلاحيات مستقل | المتحكم يكرر الحارس | `ArchiveListEndpoints_ShareLegacyAuthorization` + `/list` في اختبار منع غير المحامي للتذكيرات | لا |
| 3 | `DocumentListItemDto.FromRow` يخلط بناء الكيان المؤقت بتحويل DTO، وإعادة استخدامه للتصدير تحتاج `DocumentResponse` كاملًا | قراءة الكود | استخراج `DocumentListRow.ToDocumentStub()` كمصدر واحد لبناء الكيان | لا |
| 4 | `DocumentDetailDto`/`PortalFileDetailDto` مؤجّلان صراحةً (`audit/94`) لأن التفاصيل 75–82ms غير ساخنة | `docs/audit/94...md` §4 | يبقيان دينًا تقنيًا اختياريًا، لا «خطوة أداء» | قرار مالك لاحقًا |
| 5 | تذكيرات الشطب ما زالت مقيّدة باستعلام الاختيار (282ms) لا بالتحميل | قياس `P3-04c` | يُسجَّل لبند فهرس/عمود آخر نشاط في `P3-03`/`P3-05` | خارج `P3-04` |
| 6 | المحذوفة/المحالة 0 صفوف في البذرة الحتمية | `P3-04c` fingerprint `total=0` | لا تغيير للبذرة (يكسر قابلية المقارنة)؛ يُوثَّق كقيد قياس | موثّق |

## 2. التصميم

1. **`DocumentListRowProjection`** (Infrastructure): تعبير `Selector` واحد
   + `LoadChildrenAsync(db, rows, ct, includeDeletedChildren)` — نُقلا من
   `DocumentRepository` ليستخدمهما مستودعا الملفات والبوابة، بلا نسخ.
2. **`DocumentListRow.ToDocumentStub()`** (Application): بناء الكيان المؤقت من
   الحقول المسقطة + العناصر الدفعية. `DocumentListItemDto.FromRow` والتصدير
   كلاهما يمرّان عبره ثم `DocumentResponse.FromEntity` — مصدر واحد للقواعد.
3. **`PortalRepository.ExportScopedAsync`**: يعيد `List<DocumentListRow>`:
   `ScopedQuery(...).OrderByDescending(CreatedAt).Select(Selector)` ثم تحميل
   الأبناء الدفعي. أُزيلت 16 `Include` + `AsSplitQuery`.
4. **`PortalService.ExportWorkbookAsync`**: يبني `DocumentResponse` من
   `row.ToDocumentStub()`، ثم `ScrubForPortal` (نفس سياسة الحجب)، ثم
   `BuildMatchedEntries` من `RegistryId` في `ApplicantPublicEntities`/
   `ExecutedPublicEntities` المُسقَطة. أُزيل تحميل `Document` الكامل.
5. **لا تغيير خارجي:** `PortalWorkbookRow` و`PortalColumns` و`ScrubForPortal`
   وترتيب التصدير (`CreatedAt DESC`) كما هي؛ `OpenAPI` لم يُلمس، والواجهة لم تُلمس.

## 3. قياس A/B — نفس التشغيل ونفس البصمة (`50k`)

- المقياس: `50000`، البذرة `20261121`، البذر **150.2s**، البصمة
  **`a3f7e28ace7f054f2c74aa8c558f72d6`** (مطابقة لـ`P3-04c`).
- المسار القديم ظلّ مؤقتًا داخل `PortalRepository` **للقياس فقط** ثم حُذف قبل
  الدمج — لا نسخ SQL في الكود النهائي.

| الاستعلام | قديم p50/p95 | جديد p50/p95 | التغيّر p50 | الصفوف | البصمة | الخطط |
|---|---:|---:|---:|---:|---|---:|
| `portal-export` | 5187.83 / 5256.89ms | **679.55 / 719.55ms** | **−86.9%** | 9882 | `rows=9882;first=49920` (متطابقة) | 14 → 8 |

**ضوابط لم تُمسّ (نفس التشغيل):** `portal-list-page1` 116.82ms،
`portal-list-text` 296.98ms، `portal-file-detail` 79.87ms، `stats-dashboard`
244.15ms، `documents-list-page1` 122.33ms — لا تدهور.

**ملاحظة صدق:** القياس غير مقيَّد بسقف التصدير (9882 صفًا) لإظهار أثر الذاكرة/الزمن؛
في التشغيل الفعلي السقف `MaxRows` يحكم قبل الجلب.

## 4. تدقيق العقد

- **لا حقل جديد في أي DTO خارجي.** `IPortalRepository.ExportScopedAsync` عقد داخلي
  فقط تغيّر نوعه (`Document` → `DocumentListRow`)، و`OpenAPI`/`generated.ts` بلا فرق.
- الحقول التي يستهلكها المصنّف تُبنى حرفيًا من `DocumentResponse.FromEntity`:
  `displayStatus`/`applicant`/`executionApplicants`/`executedPublicEntities`/
  `borrower*`/`court`/`displayFileNumber`/`fileType`/`fileYear`/`executionActions`.
- مسار `null` لكل حقل محجوب يبقى عبر `ScrubForPortal` (سياسة واحدة للتفاصيل
  والتصدير).
- اختبار `Export_PortalHeadersMatchDeclaredAllowlist` واختبارات الحجب/الفروع/
  الإجراءات العلنية بقيت ناجحة، وأُضيف اختبار طرف منفذ عليه.

## 5. الاختبارات (قبل/بعد)

| البوابة | `P3-04c` | `P3-04d` | الفرق |
|---|---:|---:|---:|
| `Application` | 2493 | **2494** | +1 (`Export_ProjectsExecutedSidePartiesAndStatus_FromListRows`) |
| `Api` غير تسلسلي | 1551 | **1552** | +1 (`ArchiveListEndpoints_ShareLegacyAuthorization`) |
| `Api` تسلسلي | 5 | **5** | 0 |
| `vitest` | 1808 | **1808** | 0 (لا تغيير واجهة) |

الاختبارات الجديدة/المعدّلة:

- `PortalFileDetailTests.Export_ProjectsExecutedSidePartiesAndStatus_FromListRows`.
- `DocumentsIntegrationTests.ArchiveListEndpoints_ShareLegacyAuthorization`.
- `StrikeOffRemindersIntegrationTests` — إضافة `/list` إلى فحص منع غير المحامي.

## 6. الاصطلاحات والبقايا

- لا `TestDb`/`TestDatabase`/`UseSqlite`/`SqliteException`/`usePostgres` في
  `backend/src` أو `backend/tools` (إشارات `SQLite` تعليقات تاريخية فقط).
- فروع `ProviderName` الحارسة (رفض غير Npgsql) لم تُمس.
- `[PostgresFact]`/`PostgresTestDb`، و`test-report.mjs`، وRF-014، و`Intl.NumberFormat`
  — بلا تغيير.

## 7. الهجرات

**لا هجرة جديدة.** القائمة المعلّقة تبقى **6**، و`has-pending-model-changes` نظيف.

## 8. التحقق النهائي

| البوابة | النتيجة |
|---|---|
| `dotnet build -warnaserror` | 0/0 |
| `dotnet format --verify-no-changes` | نظيف |
| `Architecture` | **29/29** |
| `Application` | **2494/2494** |
| `Api` غير تسلسلي | **1552/1552** |
| `Api` تسلسلي | **5/5** |
| `oxlint` / `tsc -b` / `vitest` / `build` / `guard` | نظيفة / **1808** / ناجح |
| الحراس (subhead/determinism/migration-watchlist/migration-safety/secrets/postgres-suite/test-report/npm-audit/dependency-audit) | نظيفة |
| `migration-guard` (`has-pending-model-changes` + `datetime2`) | نظيف |
