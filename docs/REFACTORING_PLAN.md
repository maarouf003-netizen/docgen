# خطة التنظيف الاحترافي (Refactoring Plan)

> **الحالة:** مسودة بانتظار موافقة المالك — **يُمنع أي تنفيذ قبل الاعتماد الصريح**.
> **النطاق:** المرحلتان 0 و1 فقط — تنظيف بنيوي بلا كسر عقود (لا تغيير `DTOs`/نقاط نهاية/سلوك فروع إلا بمبرر واختبارات).
> **مستبعد بقرار المالك:** المرحلتان 2 و3 (استخراج سياسات المنتج والشطور الكبيرة) — عائدهما الحدي لا يبرر `churn` كودٍ مستقر ومُختبر؛ تُعادان فقط مع عمل وظيفي في مناطقهما (§4).
> **الأساس:** `master @ c44d75e` (بعد `PR #22` سياسة اللقطات). كل مرحلة على فرع مستقل من `master` ثم `PR`.
> **المرجعات السطرية** أُعيد تثبيتها على `master @ c44d75e` بتاريخ المراجعة النقدية (2026-10-13)؛ أي انزياح لاحق يُعاد تثبيته قبل التنفيذ عبر `git rev-parse HEAD` + إعادة `grep` للعدد والأسطر.

---

## 1. المبادئ الحاكمة (من `AGENTS.md` — إلزامية لكل بند)

1. **لا حلول مختصرة**: استخراج حقيقي ببنية نظيفة، لا ترقيع.
2. **لا تغييرات كاسرة**: العقود (الواجهات، `DTOs`، نقاط النهاية، سلوك الفروع) ثابتة إلا بتحديث شامل ومبرر.
3. **التحقق الكامل إلزامي** بعد كل بند: `dotnet test`، و`npx oxlint src`، و`npx tsc -b`، و`npx vitest run`، و`npm run build` — كلها خضراء.
4. **اختبارات مع كل تغيير سلوكي** بما فيها الحالات الحدّية (`null`، فراغ، تواريخ حدّية).
5. **مراجعة المسار الكامل** قبل الإنجاز: عقود `frontend ↔ backend` حقلًا بحقل، وبحث بقايا، ومطابقة الخطة.
6. **الأمان**: لا أسرار في الكود، واحترام دوال التحقق القائمة.
7. **قواعد التواريخ والزمن والجوال والتصميم** تبقى سارية على كل تغيير.

---

## 2. المرحلة 0 — نظافة آمنة (موصى بالبدء بها)

### R0-1 — إزالة الحجب المتزامن الوحيد في مسار الطلب

- **الملف:** `backend/src/DocGenerator.Application/Services/PublicEntityService.Branches.cs`
  (`ValidateMergeablePair` معرّفة `:183`، تُستدعى `:71` و`:107` و`:134` من `PreviewBranchActionAsync :16`؛ الحجب `:205-206`).
- **الخطوات:**
  1. تحويل `private void ValidateMergeablePair(...)` إلى `private async Task ValidateMergeablePairAsync(...)`.
  2. استبدال السطرين `:205-206` بـ `await EnsureHeadScopeAsync(...)` مع تمرير `ct` كما هو.
  3. تحويل الاستدعاءات الثلاثة إلى `await ValidateMergeablePairAsync(...)`.
  4. إبقاء التوقيع العام لـ `PreviewBranchActionAsync` و`BranchActionPreviewResponse` دون مساس.
- **التحقق:** `dotnet test` كامل + اختبار `PreviewBranchAction` الحالي يمر؛ لا اختبار جديد لازم (لا تغيير سلوكي)، لكن يُفضَّل اختبار إلغاء عبر `ct` ملغى.
- **المخاطر:** منخفضة — `ASP.NET Core` بلا `SynchronizationContext`، والتحويل `await` صرف.

### R0-2 — قراءة ساعة واحدة في `TokenService`

- **الملف:** `backend/src/DocGenerator.Infrastructure/Security/TokenService.cs:50-51`.
- **الخطوات:** `var now = _clock.GetUtcNow().UtcDateTime;` ثم استخدامها في `notBefore` و`expires` (بدل قراءتين).
- **التحقق:** اختبارات `TokenService`/الدخول الحالية خضراء.
- **المخاطر:** شبه معدومة (فرق `tick` واحد).

### R0-3 — ساعة قابلة للفحص في `IdempotencyStore`

- **الملف:** `backend/src/DocGenerator.Infrastructure/Persistence/IdempotencyStore.cs:24,89,130`.
- **الخطوات:**
  1. حقن `TimeProvider` (اختياري، افتراضي `TimeProvider.System`) بنفس نمط `DbLoginRateLimiter` (`DbLoginRateLimiter.cs:40-45`).
     `TimeProvider` مسجَّل `Singleton` في `Application/DependencyInjection.cs:17` فلا حاجة لتغيير تسجيل `DI` — المعامل الاختياري يُحقن تلقائيًا.
  2. توحيد قراءات `BeginAsync` على متغير `now` واحد، ومعالجة `:89` (`winner.ExpiresAt > DateTime.UtcNow`) ضمنه.
     ملاحظة: `CleanupExpiredAsync` تقرأ وقتها داخليًا (`:130`) — إما تمرير `now` إليها كبارامتر أو الإبقاء على قراءتها المستقلة (تنظيف أفضل-جهد، لا يؤثر على صحة الحجز).
  3. اختبار يثبت انتهاء الصلاحية بساعة مزيفة (`FakeTimeProvider`).
- **التحقق:** اختبارات عدم التكرار (`RF011`) خضراء + اختبار انتهاء جديد.
- **المخاطر:** منخفضة (نوافذ انتهاء بالدقائق؛ فرق الميلي ثانية غير مؤثر).

### R0-4 — ثقافة حتمية للأعداد والسنوات

> **تنبيه هيكلي (2026-10-13):** الملفان `DocumentService.Search.cs` و`DocumentService.Status.cs` المذكوران في المسودة الأولى **لم يعودا موجودين** — انقسما إلى `DocumentSearchService.cs` و`DocumentStatusService.cs` و`DocumentService.cs`. القائمة أدناه مُعادة التثبيت على `c44d75e`.

- **المواضع (مُعادة التثبيت):** `ExcelExportService.cs:246` (`ViewCount`)، `EffectiveFileIdentity.cs:41` (`Year`)، `PortalService.cs:574`، `DocumentSearchService.cs:175` (`DisplayFileYear`)، `DocumentService.cs:1073,1108` (`year` الحالي)، `DocumentStatusService.cs:508,837-838`، `ExecutionCircuitService.cs:823`، `DocumentRepository.cs:930`، `TokenService.cs:29,37,41,44` (مطالب عددية)، `DocumentChangeTracker.cs:249,297` (احتياطي عام).
- **خارج النطاق عمدًا:** `TokenService.cs:33` (`Role.ToString().ToLowerInvariant()` — الـ`enum` رمزي غير حساس للثقافة أصلًا) و`ToLowerInvariant()` للأدوار عمومًا و`StringBuilder.ToString()` و`JsonElement.ToString()` و`RemoteIpAddress?.ToString()` — ليست تحويلات أعداد/سنوات ثقافية.
- **الخطوات:** إلحاق `CultureInfo.InvariantCulture` بكل `ToString()` لعدد/سنة/مطلب (`ToString(CultureInfo.InvariantCulture)`)؛ وتوسيع `switch` في `DocumentChangeTracker.Format (:255-274)` ليغطي `long`/`double` صراحة (الحالي يغطي `string`/`bool`/`DateTime`/`decimal`/`int` فقط، و`:249` و`:297` يسقطان على `ToString()` الثقافي قبل التحويل).
- **التحقق:** `grep` يؤكد صفر `ToString()` عارٍ في هذه المسارات + كامل البوابات.
- **المخاطر:** منخفضة اليوم (ثقافة الخادم غالبًا ثابتة) — إصلاح حتمية.

### R0-5 — صنف تمرير مشترك بدل 10 نسخ مضمّنة

- **المواضع (10 مواضع مؤكدة بـ`grep` على `c44d75e` — لا يوجد حادي عشر):** `style={{ overscrollBehavior: 'contain' }}` في `UnifyNamesModal.tsx:163`، `ReviewActionModal.tsx:21`، `PublicEntityPickerModal.tsx:261`، `ProposeEditModal.tsx:67`، `ParentSuggestionsTab.tsx:455`، `MergeModal.tsx:112`، `ImportModal.tsx:110`، `BranchManagementModal.tsx:328`، `EntityDelegates.tsx:288`، `EntityRegistryReview.tsx:438`.
- **الخطوات:** استبدالها بصنف `Tailwind` (`overscroll-contain` على العنصر نفسه)؛ إبقاء الحالات الديناميكية المبررة (`PortalStatsView.tsx:115,142` أبعاد محسوبة، `ReminderList.tsx:61,114` و`RichTextEditor.tsx:241` ألوان ديناميكية).
- **التحقق:** `oxlint` + `tsc` + `vitest` + فحص جوال بصري لعينة مودال (لا تمرير مقصود ضائع).
- **المخاطر:** صفر سلوك (خاصية `CSS` نفسها).

### R0-6 — إسقاط `catch → Forbid()` الزائد (بعد إثبات التكافؤ)

> **تصحيح نطاق (2026-10-13):** المسودة الأولى ذكرت `Portal (11)` و`ReviewLetters (15)` و`PersonalReminders (6)` — وهي **خاطئة على `c44d75e`**.
> الجرد الكامل بــ`grep` يعطي **51 موضعًا في 6 متحكمات**: `EntityRegistryController.cs (19)`، `PortalController.cs (10)`،
> `ForumController.cs (8)`، `CorrespondencesController.cs (7)`، `ReviewLettersController.cs (6)`، `UserManagementController.cs (1)`.
> و`PersonalRemindersController.cs` **خارج هذا البند أصلًا**: لا يحوي أي `catch (UnauthorizedAccessException)`، بل فحصًا
> استباقيًا `if (!CanManage) return Forbid();` (`:33-34` وما بعدها) — لا يُمسّ هنا.

- **النطاق الموصى:** الدفعة الأولى تغطي `PortalController` (9 حذف + 1 إبقاء) + `ReviewLettersController` (6) فقط — إثبات النمط على عينة صغيرة.
  الاستثناء الموثق: `PortalController.FileCorrespondence` (`catch → NotFound()`) **يُبقى عمدًا** — تمويه وجودي (404 خارج النطاق) وحذفه كان سيُسرّب الوجود عبر 403. راجع سجل التنفيذ (§11).
  التوسع إلى المتحكمات الأربعة الباقية (`EntityRegistry` الـ19 لوحدها دفعة مستقلة) **يتطلب قرار مالك لاحقًا** بعد نجاح العينة، لا ضمن هذه الدفعة.
- **تحذير التكافؤ (مُثبت):** `GlobalExceptionHandler.cs:45` يحوّل `UnauthorizedAccessException` إلى `403`، لكن **بجسم `{ message }` ورسالة مُنقّاة (`:67-68`)** بينما `Forbid()` فارغ — الشكلان **غير متطابقين** على مستوى الجسم.
- **أثر الواجهة (مُثبت — لا حاجب):** `frontend/src/api/client.ts:17` يترجم أي `403` إلى رسالة عربية ثابتة (`لا تملك صلاحية تنفيذ هذا الإجراء`) **دون قراءة الجسم إطلاقًا** — فتغيير الجسم لا يكسر أي شاشة. يبقى الأثر الوحيد على **اختبارات العقد** التي قد تثبّت شكل الجسم الحالي.
- **أثر السجل (مُثبت — يستوجب بندًا في `PR`):** `GlobalExceptionHandler.cs:52` يسجّل `LogError` مع `stack trace` لكل استثناء يفلت — واليوم الـ`catch` المحلي يبتلع رفض النطاق بصمت (لا سجل خطأ). بعد الحذف سيتحوّل كل رفض نطاق مشروع (سلوك متوقع، لا علة) إلى **سجل خطأ أحمر** قد يُغرق السجلات ويُنبّه المراقبة. التخفيف الموصى (ضمن دفعة `R0-6` نفسها): تخفيض سجل `403` الناتج عن `UnauthorizedAccessException` إلى `Warning` (أو `Information`) في المعالج العام، أو توثيق قبول حجم السجل الجديد — بقرار مالك صريح في الـ`PR`.
- **الخطوات (بالترتيب الإلزامي):**
  1. تثبيت الشكل الحالي باختبارات عقد (`403` + الجسم) لكل متحكم في الدفعة.
  2. اعتماد المالك للشكل المعتمد رسميًا (توصية المراجعة: شكل `{ message }` للمعالج العام — أوثق تشخيصيًا ومجاني بعد ثبوت حياد الواجهة) + قرار مستوى سجل `403` (بند السجل أعلاه).
  3. حذف `catch` دفعةً لكل متحكم على حدة + تحديث اختبارات العقد للشكل المعتمد + توثيق القرارين في `PR`.
- **التحقق:** اختبارات `Api` للمتحكمات المشمولة خضراء بعد كل دفعة.
- **المخاطر:** منخفضة-متوسطة (لا أثر واجهة مثبتًا؛ الأثر محصور باختبارات العقد وشكل الاستجابة الخام **وحجم/مستوى سجل `403`**).

---

## 3. المرحلة 1 — توحيد بنية الاختبارات (بلا مساس بالمنتج)

### R1-1 — مساعدات `Api` مركزية (`ApiFixtureExtensions`)

- **الحالة (مُعادة التثبيت على `c44d75e` بأوامر `grep` منفَّذة):** إصابات النمط (`GetBranchIdAsync`/`BranchIdOfHead`/`HeadBranchIdAsync`/`UniqueName`/`NewName`/`CreateSectionAsync`/`CreateSubHeadTokenAsync`/`CreateBranchAsync`/`UserIdAsync`) = **181 إصابة عبر 22 ملفًا** — يُعاد الحساب بنفس الأمر على الفرع قبل البدء.
- **الشواهد:** `AlertsIntegrationTests.cs:17,19,26,42`، `AuthIntegrationTests.cs:308,319-320`، `ExecutionCircuitsIntegrationTests.cs:34,37,45,303`، `DocumentScopeIsolationTests.cs:28,30,43,56,73`، `DelegationScopeIsolationTests.cs:27,29,42,55,72`، `ReviewCorrespondenceScopeTests.cs:27,29,42`، `StatsScopeTests.cs:29,31,44,76,283`، `AppealForwardFlowTests.cs:27,29,36,49,66`، `LockoutIntegrationTests.cs:22,178`، `StaleRoleRevocationTests.cs:22,32`.
- **الخطوات:**
  1. ملف جديد `backend/tests/DocGenerator.Api.Tests/TestHelpers/ApiFixtureExtensions.cs` (مجلد `TestHelpers` **يُنشأ** — المصنع الحالي `ApiFactory.cs` في جذر المشروع لا في `TestHelpers`) يحوي: `GetBranchIdAsync(code)`، `GetDamascusIdAsync()`، `UniqueName(prefix)` (اقتطاع واحد `[..24]` يحترم حد `Username` الأربعين والتطبيع العربي في `CreateUserAsync`)، `CreateSectionAsync`، `CreateSubHeadTokenAsync`، `CreateBranchAsync`، `UserIdAsync`، `NewLawyerClientAsync`.
  2. ترحيل ملفًا بملف (مجموعات صغيرة قابلة للمراجعة)، مع حذف التعريفات المحلية.
  3. **يُحظر**: توحيد `HeadBranchIdAsync` (يُغيّر `Governorate` كأثر جانبي) و`BranchIdOfHeadAsync` و`GetBranchIdAsync(code)` المخصصة — تُبقى أو تُوثَّق كاستثناءات.
- **التحقق:** `dotnet test` كامل بعد كل مجموعة (أي ترتيب متأثر بالتسلسل يُكتشف فورًا).
- **المخاطر:** متوسطة — تغيير ~22 ملفًا؛ التخفيف: دفعات صغيرة + عدم المساس بالتجميعات (`api-integration` التسلسلية مقابل المعزولة).

### R1-2 — توحيد مسار التوكن الاختباري

- **الملفات:** `ApiFactory.cs:130` (`ClientWithToken` — يُبقى)، `AuthIntegrationTests.cs:319-320` (غلاف `WithToken` — يُحذف)، كل مستخدمي `.WithToken(` (= **120 استعمالًا عبر 16 ملفًا** على `c44d75e` بأمر `grep` منفَّذ — الثقل: `DocumentsIntegrationTests.cs` وحده 82، ثم `RF*.cs` و`Transfer*`، و`ManagerStatsIntegrationTests.cs:132`، `ExecutionCircuitsIntegrationTests.cs:42,333` — يُعاد الحساب على الفرع)، `MissingRoleClaimTests.cs:42-68` (سر `JWT`/المُصدِر في `MintToken`).
- **الخطوات:**
  1. استبدال ميكانيكي `WithToken(` ← `ClientWithToken(` عبر الملفات، وحذف الغلاف.
  2. نقل ثابت سر/مُصدِر/جمهور الاختبار إلى `ApiFactory` واستخدامه في `MintToken`.
  3. **يُحظر** استبدال `AuthorizedClient` (دخول حقيقي) بـ `ClientWithToken` (حقن) في اختبارات المصادقة/`CSRF` — مساران غير متكافئين.
- **التحقق:** `dotnet test` لمشروع `Api` كاملًا.
- **المخاطر:** منخفضة (ميكانيكي)، مع شرط المسارين أعلاه.

### R1-3 — قاعدة الفئات المعزولة

- **الملفات:** الفئات التسع ذات `private readonly ApiFactory _factory = new();` + `IAsyncLifetime` (`AppealForwardFlowTests.cs:15-24`، `CorrespondenceIntegrationTests.cs:19-21`، `DelegationScopeIsolationTests.cs:15-23`، `DocumentScopeIsolationTests.cs:16-24`، `MissingRoleClaimTests.cs:17-34`، `ReviewCorrespondenceScopeTests.cs:15-23`، `SectionsIntegrationTests.cs:15`، `StatsScopeTests.cs:17-19`، `UserBranchInvariantTests.cs:14-16`).
- **الخطوات:** قاعدة `IsolatedApiTestBase : IAsyncLifetime` تملك `Factory` وتُهيّئ/تُصرّف؛ السبع ذات التهيئة الفارغة ترثها مباشرة، و`Correspondence`/`MissingRole` تُبقيان `InitializeAsync` عبر تجاوز.
- **يُحظر**: المساس بالتجميعة المخصصة `execution-circuits-integration` ومصانع `RF016`/`SecurityHeaders`/`Swagger`/`DatabaseInitializer` و`Lockout:22` — عزلها مقصود.
- **التحقق:** `dotnet test` كامل + تشغيل محاكاة نواتين (`DOTNET_PROCESSOR_COUNT=2`) للفئات المعزولة (أثبتنا به القفل سابقًا).
- **المخاطر:** منخفضة-متوسطة (خطر مشاركة حالة عبر الوراثة — تمنعه القاعدة بتصميمها: مصنع لكل نسخة).

### R1-4 — مساعدو `Application.Tests`

- **الملفات:** `TestHelpers/TestDb.cs` + `ServiceBuilders.cs` + `EntityBuilders.cs` (جديدة — مجلد `TestHelpers` **يُنشأ** تحت `Application.Tests`)؛ الاستهلاك بأوامر `grep` منفَّذة على `c44d75e`: `new DbContextOptionsBuilder` (**17 موضعًا**)، `UseSqlite` (14 بثلاث نكهات)، هياكل التزامن الأربعة (`AppealTransferAllConcurrencyTests.cs:22-44`، `CorrespondenceConcurrencyTests.cs:19-41`، `DelegationConcurrencyTests.cs:23-44`، `TransferRedirectConcurrencyTests.cs:25-46`)، `FakeAuditLogger` (**تعريف واحد** في `ServiceTests.cs:12` و**48 استعمالًا عبر 42 ملفًا** — يُعاد الحساب على الفرع)، بانو `BuildService` (~10 ملفات) — والمرجع `PortalServiceFactory.cs` (70 سطرًا).
- **الخطوات:** نقل `FakeAuditLogger` إلى مشترك، وتوحيد `Options(conn)`/فتح الاتصال، وبناة `NewUser/NewDocument/NewBranch` و`BuildCorrespondence/Delegation/Auth`.
- **يُحظر**: تغيير بذور الاختبارات الدلالية (أسماء، أدوار، كلمات) — التوحيد شكلي فقط.
- **التحقق:** `dotnet test` لمشروع `Application` (1727 اختبارًا — لقطة، يُعاد العدّ على الفرع) أخضر.
- **المخاطر:** منخفضة (اختبارات فقط).

### R1-5 — تقسيم ملفات العقود الكبيرة (بإعادة تصدير)

- **الملفات:** `frontend/src/types/index.ts` (2181 سطرًا على `c44d75e`) ← `types/document.ts`، `appeal.ts`، `delegation.ts`، `entity.ts`، `portal.ts` (+ إبقاء `index.ts` مُعيد تصدير)؛ و`backend/src/DocGenerator.Application/DTOs/DocumentDtos.cs` (970 سطرًا، `namespace DocGenerator.Application.DTOs;` — نطاق ملفي واحد) حسب المجموع **داخل نفس الـ`namespace`**.
- **الخطوات:** نقل حرفي بلا إعادة تسمية؛ كل الاستيرادات تبقى صالحة.
- **التحقق:** `npx tsc -b` + `dotnet build` + كامل البوابات (صفر كسر متوقع).
- **المخاطر:** شبه معدومة (نفس الأسماء والمسارات المنطقية).

---

## 4. مستبعد من النطاق (بقرار المالك)

- **المرحلة 2 — استخراج سياسات المنتج** (`R2-1` سياسة الملكية/الوصول، `R2-2` خط البحث/التصدير الموحد، `R2-3` هيكل الواجهة المشترك): مستبعدة — الكود مستقر ومُختبر (~3900 اختبار أخضر — لقطة) والعائد الحدي لا يبرر مخاطرة التفويض/الأداء/الانزلاق البصري الآن. تُعاد فقط مع أول عمل وظيفي يمسّ تلك المناطق.
- **المرحلة 3 — الشطور الكبيرة** (شطر الخدمات العملاقة، توحيد `SQLite/Postgres`، دمج صفحات المراجعة): مستبعدة — تُفتح فقط عند أول علة حقيقية أو قرار منتج صريح. ملاحظة: توحيد `SQLite/Postgres` مرفوض أصلًا (التخصيصات لازمة لـ`timestamptz` والتوحيد يعيد توليد اللقطات ⇒ هجرات نشر جديدة).

---

## 5. ممنوعات صريحة (لا تُفتح في أي مرحلة دون قرار مالك مستقل)

1. ملفات `Migrations*/Designer/Snapshot` المولّدة — لا تُلمس يدويًا أبدًا.
2. عقود `DTO`/نقاط النهاية/سلوك الفروع — ثابتة.
3. حجب الإقلاع `Program.cs:313` (كان `:303` قبل انزياح الأسطر) — حميد ومقبول.
4. بديل `court` النصي (حال المساس: منطق الحل بالنص في `DocumentWriteGuard.ResolveCircuitForWriteAsync` — `DocumentWriteGuard.cs:163` — المستدعى في `DocumentService.cs:134-135,240-241` بعد شطر الملف القديم `DocumentService.Circuits.cs` الذي لم يعد موجودًا) — يُحذف فقط بعد اكتمال التعبئة وبراية صريحة.
5. احتياطات `Unconfigured*` (`DocumentDelegationService.cs:66-67,70`، `DocumentService.cs:76`، `DocumentWriteGuard.cs:16,25,60,173,192` — المسارات القديمة `DocumentService.cs:161-162` و`DocumentService.Circuits.cs` لم تعد صالحة بعد الشطر) — تحويلها لفشل صريح **قرار سلوكي** يحتاج اعتمادًا (يُكسر اختبارات تعتمد الصمت) — **خارج هذه الخطة** حتى يُطلب.
6. قواعد التواريخ (`text` + `1/8/2026`) والزمن (`UtcNow` للكتابة) والجوال — ملزمة دائمًا.

---

## 6. خطة الفروع والدمج والتحقق

- فرع لكل مرحلة من `master`: `refactor/cleanup-p0`، ثم `refactor/test-harness-p1` (يُبنى على الأول بعد دمجه إن تداخلا).
- كل مرحلة `PR` مستقل + `CI` أخضر (المهام الأربع) + منع `force-push`.
- بوابات كل بند: `dotnet test`، `npx oxlint src`، `npx tsc -b`، `npx vitest run`، `npm run build`.
- تقرير إنجاز كل مرحلة يذكر الهجرات (المتوقع: لا شيء — وأي انحراف يُعلن فورًا مع تنبيه `database update`).
- لا دفع مباشر إلى `master` أبدًا.

---

## 7. تقدير الجهد (تقريبي، لشخص واحد)

| المرحلة | الجهد | الخطر |
|---|---|---|
| 0 (R0-1…R0-5 + عينة R0-6) | أيام | منخفض (R0-6 عينة `Portal`+`ReviewLetters`: منخفضة-متوسطة — لا أثر واجهة مثبتًا حسب `client.ts:17`) |
| 1 (R1-1…R1-5) | ~أسبوع | متوسط (حجم التغيير في الاختبارات) |
| توسع R0-6 لاحقًا (`EntityRegistry` الـ19 + الباقي) | دفعة مستقلة | يتطلب قرار مالك بعد نجاح العينة — خارج هذه الدفعة |

---

## 8. قائمة التحقق قبل اعتماد الخطة

- [ ] اعتمد المالك النطاق (0 / 0+1) — المرحلتان 2 و3 مستبعدتان (§4).
- [ ] اعتمد قرار شكل `403` في R0-6 (توصية المراجعة: شكل `{ message }` للمعالج العام — حياد الواجهة مثبت في `client.ts:17`) **+ قرار مستوى سجل `403`** (تخفيض `LogError` إلى `Warning` للرفض المتوقع أم قبول الحجم الجديد)، واعتُمد أن الدفعة الأولى عينة (`Portal`+`ReviewLetters`) والتوسع (`EntityRegistry` الـ19 + الباقي) دفعة لاحقة بقرار مستقل.
- [ ] ثُبّتت المراسي السطرية على الفرع المنفَّذ عليه قبل البدء (`git rev-parse HEAD` + إعادة `grep` للأعداد: `catch (UnauthorizedAccessException)` الـ51، و`overscrollBehavior` الـ10، و`.WithToken(` الـ120، و`FakeAuditLogger` الـ48).
- [ ] لا عمل موازٍ على نفس الملفات أثناء التنفيذ.

## 9. سجل المراجعة النقدية (2026-10-13 — على `master @ c44d75e`)

- أُعيد تثبيت كل المراسي بعد انزياحها عن أساس المسودة (`e74e33d`): `ExcelExportService 226←246`، `DocumentRepository 880←930`، `Program.cs 303←313`، `index.ts 2134←2181 سطرًا`، `DocumentDtos 954←970 سطرًا`.
- صُحّح هيكل منقرض: `DocumentService.Search.cs` / `DocumentService.Status.cs` / `DocumentService.Circuits.cs` لم تعد موجودة — أُحيلت إلى `DocumentSearchService.cs` و`DocumentStatusService.cs` و`DocumentService.cs` و`DocumentWriteGuard.cs`.
- صُحّح نطاق `R0-6`: الأعداد القديمة (`11/15/6`) استُبدلت بالجرد الكامل (51 في 6 متحكمات)، وأُخرج `PersonalRemindersController` (فحص استباقي لا `catch`)، وأُثبت حياد الواجهة (`client.ts:17`) فخُفّض الخطر.
- صُحّح عدد `R0-5` (`11 (+1 غامض)` ← 10 مؤكدة) وعدد `FakeAuditLogger` (36 ← 48 استعمالًا عبر 42 ملفًا بتعريف واحد) ومسارا `TestHelpers` الجديدين (يُنشآن — غير موجودين).
- لم يُنفَّذ أي كود — الوثيقة فقط عُدّلت، بانتظار اعتماد المالك.

## 10. سجل المراجعة النقدية الثانية (2026-10-13 — تصحيح ذاتي على `c44d75e`)

- اصطادت المراجعة الثانية أخطاء زرعتها الأولى نفسها: `R1-1` («~70 ملفًا» ← **22 ملفًا** بأمر `Group-Object`، و«~40» في المخاطر ← ~22)، و`R1-2` («~146» كان يخلط `ClientWithToken(` ← **120 لـ`.WithToken(` عبر 16 ملفًا**، الثقل `DocumentsIntegrationTests.cs` وحده 82)، و`R1-4` («3 تعريفات» ← **تعريف واحد** — الالتباس كان مع `FakeUnitOfWork`/`FakeTransactionRunner`؛ و`DbContextOptionsBuilder` «13» ← **17**).
- أُضيف أثر تشغيلي غاب عن الجولتين: حذف `catch` في `R0-6` يحوّل رفض النطاق الصامت إلى `LogError` مع `stack trace` (`GlobalExceptionHandler.cs:52`) — أُضيف بند تخفيف مستوى السجل + قراره في قائمة الاعتماد (§8).
- القاعدة المستخلصة: كل رقم في هذه الوثيقة يجب أن يكون ناتج أمر `grep` منفَّذ، لا حصيلة قراءة تقريبية.

## 11. سجل التنفيذ (المرحلة 0 — فرع `refactor/cleanup-p0`، بمراجعة سطر-بسطر لكل بند)

- `R0-1`: تحويل `ValidateMergeablePair → ValidateMergeablePairAsync` + حذف الحجب (`:205-206`) + اختبار إلغاء جديد — `PublicEntityServiceTests` 231/231.
- `R0-2`: قراءة ساعة واحدة في `TokenService` — `TokenServiceTests` 9/9.
- `R0-3`: حقن `TimeProvider` في `IdempotencyStore` (بلا تغيير `DI` — `Singleton` موجود) + 4 اختبارات ساعة مزيفة جديدة — `RF011` 5/5 عبر الحاوية الحقيقية.
- `R0-4`: `InvariantCulture` في 10 ملفات إنتاج + ذراعا `long`/`double` في `ChangeTracker` (غير قابلين للوصول عبر `Diff` اليوم — `IsTrackedType` لا يشملهما؛ لم يُوسَّع عمدًا لتجنب تتبع `Version` الجديد) + اختبار ثقافة `ar-EG` (حارس العشرية حقيقي؛ جزء الصحيح توصيفي — تنسيق الصحيح لاتيني أصلًا على .NET؛ ومقارنات `xUnit` النصية الافتراضية حساسة للثقافة فثُبّتت `Ordinal` صراحةً).
- `R0-5`: `overscroll-contain` بدل المضمّنة في 10 مواضع (مطابق لعرف 30+ موضعًا قائمًا) — `oxlint` و`tsc` و`vitest` (1554) و`build` خضراء.
- `R0-6` (العينة، بقراري المالك: شكل `{ message }` + سجل `Warning`): حذف 9+6، إبقاء تمويه `FileCorrespondence`، تبسيط `Stats` (كان `catch`-وحيدًا فصار `try` يتيمًا — كشفه البناء)، تخفيض سجل الرفض، واختبارات عقد `ForbiddenShapeContractTests` (2) — كامل الخلفية خضراء (`Application` 1821 + `Api` 641).
- الهجرات: **لا شيء** — لم يُمسّ أي كيان (لا `database update` عند النشر).

## 12. سجل التنفيذ (المرحلة 1 — فرع `refactor/test-harness-p1` فوق `P0`، بمراجعة سطر-بسطر لكل بند)

- `R1-1`: النطاق الحقيقي أكبر مما قُدّر (17 ملفًا إضافيًا بنسخ `BranchIdAsync(code)` اكتُشفت أثناء التنفيذ — الإجمالي ~35+ ملفًا لا 22). `ApiFixtureExtensions` مركزية + ترحيل كامل بدفعات (B1–B4) — كامل `Api` أخضر (641/641). انحرافات موثقة بلا انزلاق دلالي: نسخة `Appeal` بلا-إصلاح → `GetBranchIdAsync("DAM")` (البذر يضمن المحافظة)؛ `CreateSubHeadAsync` المميز (`Token,Id`) بقي محليًا في `Review`/`Stats`؛ `CreateBranchAsync` أخذ بارامتر محافظة اختيارية (حلب/حمص/حماة محفوظة)؛ `NewLawyerClientAsync` أُضيفت للمركزي؛ الاستثناءات المحظورة (`HeadBranchIdAsync` بأثره الجانبي و`BranchIdOfHeadAsync`) بقيت موثقة. درس: الاستدعاء المجرد للدوال الساكنة يحتاج `using static` لا `using` عاديًا (أمسكه البناء).
- `R1-2`: حذف غلاف `WithToken` + استبدال ميكانيكي (120 عبر 16 ملفًا) + ثوابت `JWT` الاختبارية في `ApiFactory` — `AuthorizedClient` لم يُمسّ (مسار الدخول الحقيقي محفوظ) — 641/641.
- `R1-3`: `IsolatedApiTestBase` للفئات التسع (`Factory` + تهيئة/تصريف، وتجاوز `Correspondence`/`MissingRole`) — 69/69 في الوضع الطبيعي ومحاكاة النواتين.
- `R1-4` (نطاق مُنقّح بقرار هندسي): `TestDb.CreateOptions` (مدّد الموجود في `CoreTests.cs` — لا ملف جديد منعًا للتصادم) + `EntityBuilders.NewUser` (المتطابقان فقط؛ نسخة المنتدى بقيت لاختلاف الآلية) + نقل `FakeAuditLogger` إلى `TestHelpers/Fakes.cs`. **مرفوض عمدًا**: توحيد هياكل السباق (حساسية توقيت — الخطر يفوق العائد التجميلي) وتوحيد بانو الخدمات (وسائط بناء مختلفة) — 1821/1821.
- `R1-5` (انحراف مبرر عن قائمة الملفات): التقسيم الحرفي إلى 5 ملفات كان سيُشوّه التماسك (منتدى/مراجعة/إحصاءات في `portal.ts`؟) — فقسّمت إلى 12 وحدة متماسكة تعكس ملفات `DTOs` الخلفية (`common/management/document/delegation/appeal/correspondence/forum/review/entity/portal/personal/circuit`) مع `index.ts` مُعيد تصدير؛ `DocumentDtos.cs` (1062 سطرًا فعليًا) إلى 4 ملفات بنفس النطاق. سلامة البايت مثبتة (204=204 تصديرًا؛ مسح UTF-8 نظيف) والتبعيات البينية حُلّت بـ4 `import type` (كشفها `tsc`) — `tsc` و`oxlint` و`vitest` (1554) و`build` خضراء.
- الهجرات: **لا شيء** — تغييرات اختبارات وعقود مُعاد تصديرها فقط (لا `database update` عند النشر).

## 13. سجل تنفيذ توسع `R0-6` — الدفعة A (`EntityRegistry` 19، فرع `refactor/r06-entityregistry`)

- **الأساس المعاد تثبيته:** `master @ 6a57678` — الجرد `36 = 35 حذف + 1 إبقاء` (`Portal:296` تمويه `404` الوجودي) مؤكد بأمر `grep` منفذ؛ `EntityRegistry` صفر بعد التنفيذ (الباقي 16 + 1 إبقاء).
- **العقد أولًا:** `ForbiddenShapeContractTests.EntityRegistry_PreviewBranch_OutOfScope_Returns403WithMessage` (رئيس دمشق يعاين `rename` على قيد حلب أدخله محامي حلب — يبلغ `EnsureHeadScopeAsync` لا الحارس الاستباقي): فشل قبل الحذف بجسم فارغ (`JsonException` — `Forbid()` بلا `JSON`)، ونجح بعده (`403` + `{ message }` غير فارغ) — الإجمالي `3/3` مع عينة `R0-6`.
- **تدقيق الرسائل (خفيف):** رسائل `UnauthorizedAccessException` في مسارات السجل وصفية للسياسة (`الرئيس مقصور على ما أدخله محامو فرعه…`، `الجهة الأم تُدار عبر الاقتراح فقط…` ونحوها) ولا تكشف بيانات جديدة؛ التمييز `403` مقابل `404` قائم أصلًا (`KeyNotFound` محفوظ) فالفرق إغناء النص فقط — لا مسار كاشف يستوجب استثناء تمويهًا (§4.2 من خطة التوسع).
- **الحذف:** `18` كتلة ميكانيكية (`catch Unauthorized → Forbid` بعد `Argument`/`IdempotentReplay`) + `1` فكّ `try` (`ApproveReview` — كان `catch`-وحيدًا فصار مباشرًا بلا `try`، درس `Stats` في العينة) — الإجمالي `19`.
- **تعليق المعالج:** حُدّث `GlobalExceptionHandler.cs:42-46` (لم يعد «المتحكمات نفسها تحوّله إلى `Forbid()`» صحيحًا لهذا المتحكم — الخدمة تفلت إلى `403` بجسم، والحراس الاستباقية وحدها ترد `403` فارغًا، مقبول صراحة والواجهة تحجب الشكلين `client.ts:17`).
- **العائد (صراحة استشارية):** لا تحسين مرئي — الرسالة الخادمية لا تبلغ الواجهة أبدًا؛ القيمة اتساق خلفي (شكل `403` موحد) + سجل مركزي بمستوى صحيح (`Warning` مطبق مسبقًا).
- **البوابات:** `Application` `1821/1821` + `Api` `642/642` (إعادة ثانية بعد فشل عابر وحيد غير مرتبط `EnricherMiddlewareTests.Anonymous` — يمر منفردًا؛ `CI` يقسم `Category=Sequential` فيمنع تكراره) + `oxlint` صفر + `tsc` نظيف + `vitest` `1554/1554` + `build` ناجح.
- **الهجرات:** **لا شيء** — لم يُمسّ أي كيان (لا `database update` عند النشر).

## 14. سجل تنفيذ توسع `R0-6` — الدفعة B (`Forum` 8 + `Correspondences` 7 + `UserManagement` 1، فرع `refactor/r06-forum-correspondence-users`، سُوّي على `master` بعد دمج A)

- **الأساس:** أُعيد التثبيت على `master` بعد دمج الدفعة A (`PR #25`) — الجرد النهائي `36 = 35 حذف + 1 إبقاء` (`Portal:296` تمويه `404` الوجودي)؛ بعد الدمجين: `EntityRegistry` صفر + `Forum`/`Correspondences`/`UserManagement` صفر.
- **العقود أولًا (3 — واحد لكل متحكم):** `Forum_Update_ByNonAuthor` (تعديل غير الكاتب)،
  `Correspondence_Get_ByThirdParty` (اطلاع ثالث)، `UserManagement_ManagerCreateAdmin` (حد `BQ-001د`):
  الثلاثة فشلت قبل الحذف بجسم فارغ (`JsonException` — `Forbid()` بلا `JSON`) ونجحت بعده
  (`403` + `{ message }` غير فارغ) — الإجمالي `5/5` مع عينة `R0-6`.
- **تدقيق الرسائل (خفيف):** رسائل الرفض وصفية للسياسة (`تعديل الرسالة متاح لكاتبها فقط`،
  `لا تملك صلاحية الاطلاع على هذه المراسلة`، `حسابات المشرف يديرها مشرف فقط` ونحوها) ولا تكشف
  بيانات جديدة؛ التمييز `403` مقابل `404` قائم أصلًا (`KeyNotFound` محفوظ في كل المسارات) — لا مسار
  كاشف يستوجب استثناء تمويهًا.
- **الحذف:** `16` موضعًا — `Forum`: `6` ميكانيكية (منها `TogglePin` الوسطية بين `KeyNotFound`
  و`Conflict`) + `2` فكّ `try` (`SearchUsers` و`UnreadCount` كانا `catch`-وحيدين)؛ `Correspondences`: `7`
  ميكانيكية؛ `UserManagement`: `1` (الأولى من كتلتين — `Unauthorized` قبل `Argument`).
- **تعليق المعالج:** وُحّد عند التسوية ليذكر المتحكمات الخمسة (`Portal`+`ReviewLetters`+`EntityRegistry`+`Forum`+`Correspondences`+`UserManagement` — الإبقاء الوحيد تمويه `Portal:296`).
- **العائد (صراحة استشارية):** لا تحسين مرئي — الواجهة تحجب الشكلين (`client.ts:17`)؛ القيمة اتساق خلفي + سجل `Warning`.
- **البوابات على شجرة التسوية:** تُعاد كاملة أدناه (المتوقع `Api` `645/645` = `641+4` + `Application` `1821/1821` + الرباعية الأمامية).
- **الهجرات:** **لا شيء** — لم يُمسّ أي كيان (لا `database update` عند النشر).
- **الختام بعد الدمجين:** `35/35` محذوفة + `1` إبقاء مقصود (`Portal:296`) — التوسع مغلق.
