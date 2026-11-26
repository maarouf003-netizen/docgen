# تدقيق ما بعد دفعة `P4`/`P5`/`P6` (Tier A) — 2026-11-26

> **النطاق:** تدقيق مستقل (وكيل لم ينفّذ الدفعة) لآخر **31** `PR` من `#337` إلى `#369`
> على `master` عند `08e96362` (شجرة نظيفة قبل التدقيق)، ثم إصلاح العيوب المؤكدة في
> فرع `audit/p4-post-batch-20261126` مع بوابة جديدة تمنع التكرار.
>
> **المراجع المقروءة:** `AGENTS.md`، `docs/PERFECTION_ROADMAP.md` (`P4-01` حتى
> `P4-01cw` و`P4-02`/`P4-04`/`P4-06`/`P5-01`/`P5-02`/`P5-06`/`P6-01`/`P6-03`/`P6-04`)،
> `docs/adr/0010-command-query-ownership.md`، `docs/adr/0011-api-versioning.md`،
> اللقطات `197…272` (وعلى الأخص `243` و`246…248` و`270…272`)،
> `docs/openapi/docgen-v1.json`، `docs/openapi/deprecations.json`،
> `.github/workflows/ci.yml`، و`scripts/*.mjs`.
>
> **لا تغيير سلوكي/عقود/هجرات:** الإصلاحات إزالة اعتماديات وحقول ميتة فقط، مع تقوية
> تأكيد اختباري قائم.

## 1. المنهج (قراءة فقط أولًا، ثم عيّنات، ثم إصلاح)

1. **خط أساس كامل** على `08e96362` قبل أي تعديل (النتائج في §3).
2. **سكربتات قراءة فقط** (خارج المستودع) لجرد:
   - تسجيلات `IQueryHandler`/`ICommandHandler` مقابل أصناف المعالجات وفحص التكرار.
   - كل حقل `private` (readonly وغيره) في عائلات `partial class` مقابل قراءاته الفعلية.
   - الملفات/الـ`partial` الفارغة، والجسور الاختبارية (`*Extensions`) بلا مستخدم.
   - أنماط `await _repo` داخل الحلقات في ملفات `UseCases/**/*Query.cs` (اشتباه `N+1`).
3. **عيّنة تكافؤ** (11 دالة) بمقارنة جسد الأصل (`git show <merge>^:<file>`) بجسد
   المعالج بعد تطبيع: تجريد تعليقات، إسقاط أسطر `var x = command.X;`، وتوحيد تأهيل
   `_rules.`/`Service.`/`Mapper.`، ثم `LCS diff` سطري.
4. **فحص أمني/واجهة/أداء** بأوامر فعلية (حراس، `E2E` guard، `budget`، `generate:api`).
5. **إصلاح الدفعة المصغّرة** + حارس جديد + إعادة كل البوابات.

## 2. جرد الملكية (المحور A) — الدليل

| الفحص | الأمر/الطريقة | النتيجة |
|---|---|---|
| أصناف المعالجات | مسح `class *Handler` في `Application` | **229** |
| التسجيلات | مسح `AddScoped<I(Query\|Command)Handler<…` في `DependencyInjection.cs` | **229** (**111** استعلامًا + **118** أمرًا) |
| غير مسجَّل / بلا صنف | مقارنة الأسماء | **0 / 0** |
| تسجيل مكرر / مفتاح مكرر | `Map` على اسم الصنف وعلى `(kind, generic)` | **0 / 0** |
| متحكمات بمعاملات | `Controllers/**` عن `ITransactionRunner\|RunAsync\|BeginTransaction\|SaveChanges\|CommitAsync` | **0** مطابقة |
| خدمات قراءة محذوفة مسجَّلة/مستدعاة | بحث `IStrikeOffReminderService\|IAuditLogService\|IForumService\|IAppSuggestionService\|IPersonalReminderService` في `backend/src` | **0** |
| استعلامات تكتب/تتدقق | مسح `UseCases/**/*Query*.cs` عن `SaveChanges\|AddAsync\|ITransactionRunner\|IAuditedRequest\|_audit` | **0** (استثناءا تصدير موثّقان: `ExportDocumentsQuery` و`ExportPublicEntityChangeEventsQuery` يكتبان تدقيق تصدير) |

## 3. خط الأساس (قبل الإصلاح، `08e96362`)

| البوابة | النتيجة |
|---|---|
| `dotnet restore --locked-mode` | نجح |
| `dotnet build -warnaserror` | 0/0 |
| `dotnet format --verify-no-changes` | نظيف |
| `Architecture.Tests` | **41/41** |
| `Application.Tests` | **778** ناجح / 1661 متجاوز (Postgres-gated) / 0 فاشل |
| `Api.Tests` (`Category!=Sequential`) | **916** ناجح / 615 متجاوز / 0 فاشل |
| `Api.Tests` (`Category=Sequential`) | 0 ناجح / 5 متجاوز / 0 فاشل |
| `npx vitest run` | **1864/1864** في **175** ملفًا |
| `oxlint src scripts e2e playwright.config.ts` / `tsc -b` | نظيفان |
| `npm run build` / `npm run budget` | نجح · `JS=90` · إجمالي **1732.9kB** · أكبر **388.2kB** · دخول **260.4kB** |
| `npm run guard` (CSP) | نظيف — 93 ملفًا |
| `npm run generate:api` + `git diff` | لا انحراف |
| الحراس الثمانية + `dead-code` | نظيفة (التفاصيل §6) |
| `has-pending-model-changes` | «No changes» |
| `playwright test --list` | **15** اختبارًا |
| `docs/openapi/docgen-v1.json` | **206** مسارًا / **245** عملية / `info.version = v1` |
| الهجرات | **7** ملفات = Baseline + **6** معلّقة (مطابق `RUN_GUIDE.md` §9) |

## 4. عيّنة التكافؤ (المحور C) — 11 دالة

> التطبيع: تجريد تعليقات + إسقاط أسطر `var x = command.X;` + توحيد تأهيل المساعدات
> (`_rules.`/`Service.`/`Mapper.`/`Rules.`) + `LCS diff`. الفروق المتبقية كلها
> تأهيل مساعدات أو إسقاط أسماء مستعارة، **لا تغيير دلالي**.

| الدالة | المصدر (merge^) | الهدف | أسطر الأصل/الجديد | فروق | الحكم |
|---|---|---|---:|---:|---|
| `RegisterAsync` | `cf8126fe^:…DelegationService.Lifecycle.cs` | `RegisterDocumentDelegationCommand` | 176 / 176 | 2 | تمرير `_registrationDates` للمساعد المنقول (تحقق يدوي: الجسم حرفي) |
| `CompleteAsync` | نفسه | `CompleteDocumentDelegationCommand` | 186 / 186 | **0** | مكافئ حرفيًا |
| `CommitMergeCoreAsync` | `e1b99e3f^:…PublicEntityService.Merge.cs` | `CommitPublicEntityMergeCommand` | 182 / 182 | 24 | 12 سطرًا دلاليًا ظاهريًا كلها `_rules.` + ثابت `DefaultBranchName` = `ParentBranchName` |
| `RenameGroupAsync` | `e1b99e3f^:…PublicEntityService.Groups.cs` | `RenamePublicEntityGroupCommand` | 91 / 91 | 20 | تأهيل مساعدات فقط |
| `AbolishAndReplaceAsync` | `128938bd^:…PublicEntityService.Groups.cs` | `AbolishAndReplacePublicEntityGroupCommand` | 162 / 162 | 36 | تأهيل مساعدات + `ParentBranchName` (قيمته مطابقة) |
| `UnifyNamesCoreAsync` | `26d4fc63^:…PublicEntityService.Unify.cs` | `UnifyPublicEntityNamesCommand` | 176 / 176 | 22 | تأهيل مساعدات فقط |
| `ProposeEditAsync` | `128938bd^:…PublicEntityService.Registry.Writes.cs` | `ProposePublicEntityEditCommand` | — | 24 | تأهيل/`command.EntryId` فقط |
| `SuggestParentEditAsync` | `ccbac5a9^:…PublicEntityService.Suggestions.cs` | `SuggestPublicEntityParentEditCommand` | — | 18 | تأهيل/`command.EntryId` فقط |
| `CreateAsync` + `CreateCoreAsync` | `280ca26a^:…DocumentService.Crud.cs` | `CreateDocumentCommand` | 97 / 97 | 4 | `ApplyRequest`/`ApplyRegistrationDate` إلى `DocumentUpsertMapper` (تحقق حرفي للمساعد) |
| `SearchAsync` (تدقيق) | `f7306f52^:…AuditLogService.cs` | `SearchAuditLogsQuery` | 11 / 17 | 6 | أسطر إسقاط أسماء مستعارة فقط |
| `GetListAsync` (تذكيرات الشطب) | `f7306f52^:…StrikeOffReminderService.cs` | `GetStrikeOffReminderListQuery` | 33 / 39 | 8 | أسطر إسقاط + تأهيل `NormalizePaging` |

**الخلاصة:** **0** فرق دلالي في العيّنة؛ لا نص عربي محذوف/مضاف في الأجسام المنقولة
(تحقق بصري للنصوص ذات الصلة).

## 5. العيوب المؤكدة (قبل الإصلاح)

| # | الشدّة | العيب | الدليل (عند `08e96362`) | الأثر |
|---|---|---|---|---|
| D-01 | متوسط | `UserManagementService._audit` حقل `private readonly` لا يُقرأ، ومعامل منشئه كذلك | `Services/UserManagementService.cs:28,40,51` | اعتمادية DI معلّقة تُوهم أن الخدمة تُدقّق، بينما التدقيق في أوامر `P4-01bh/bs` |
| D-02 | متوسط | `UpdateDocumentCommandHandler._audit` حقل ميت | `UseCases/Documents/UpdateDocumentCommand.cs:33,47,62` | التدقيق الفعلي عبر `IDocumentWriteGuard.LogDocumentChangesAsync`؛ الحقل مضلّل |
| D-03 | متوسط | `CreateExecutionCircuitCommandHandler._documents` حقل ميت | `UseCases/ExecutionCircuits/CreateExecutionCircuitCommand.cs:26,37,49` | اعتمادية مستودع غير مستخدمة |
| D-04 | منخفض | `DocumentDelegationServiceTests._pendingDelegationsCount` يُنشأ ولا يُستخدم | `DocumentDelegationServiceTests.cs:25,97` | توصيل اختباري ميت |
| D-05 | منخفض | `CorrespondenceIntegrationTests._branchId` يُسند ولا يُقرأ | `CorrespondenceIntegrationTests.cs:32,131` | بيانات اختبار ميتة |
| D-06 | منخفض | `HeadAlertServicePermissionTests._alertHeadInB` يُبذر ويُذكر في تعليق فقط | `HeadAlertServicePermissionTests.cs:39,74,475` | اختبار يعتمد على العدّ وحده بدل نفي الصف صراحةً |
| D-07 | منخفض (وثائقي) | ادّعاء «0 حقل ميت» في `docs/audit/246` و`243` غير دقيق وقت التدقيق | `docs/audit/246` §1 | **لا تُحرَّر اللقطة المجمّدة**؛ التصحيح هنا |

### 5.1 ما ليس عيبًا (فُحص وأُغلق)

- **`N+1` في المعاينات** (`PreviewAbolishAndReplaceQuery`/`PreviewMergeQuery`/
  `PreviewUnifyQuery`) و`ListSectionsQuery` (نداء مستخدمي كل شعبة): **قائم قبل الدفعة
  ومنقول حرفيًا** (`cb2c…`/`ccbac5a9^`)، وليس انحدارًا. المعاينات قراءة نادرة،
  و`ListSectionsQuery` محروس ضمن مسار مرجعي؛ لم يُغيَّر سلوكه.
- **تكرار `ListDocumentsLinkedToEntryAsync` مرتين لنفس القيد** في
  `PreviewBranchActionQuery:152,155`: قائم حرفيًا في `PublicEntityService.Branches.cs:136,139`
  قبل النقل؛ لا يمسّ مسار الكتابة. يُسجَّل كدين أداء معلن لا كعيب دفعة.
- **مراجع `DocumentInterventionService`/`IStrikeOffReminderService` في
  `docs/CODE_HEALTH_*` وخطط 2026-11-13**: وثائق تاريخية خارج نطاق الدفعة وليست
  `docs/audit`؛ لا تُحرَّر بأثر رجعي.
- **`_LogSanitizer` ثوابت** (`RedactedEmail`/…): مستخدمة داخل `LogSanitizer` نفسه
  (خلل أداة الجرد الأولى لا خلل كود).

## 6. أمن/واجهة/أداء (المحاور E/F/G) — أدلة

| الفحص | النتيجة |
|---|---|
| حارس `E2E` يرفض الإنتاج **فعليًا** | `E2E_BASE_URL=https://docgen.onrender.com npx playwright test --list` ⇒ `Error: Refusing to run E2E against non-local host…` (exit 1) |
| اختبارات الحارس الذاتية | **17/17** |
| `user-scalable=no`/`maximum-scale` في الواجهة | **0** |
| إعادة المحاولة في العميل | قراءات `GET/HEAD` فقط، سقف **3** محاولات، احترام `Retry-After`؛ لا `POST/PUT/PATCH/DELETE` |
| CSRF | ترويسة `X-CSRF-Token` تُضاف فقط لغير `GET/HEAD/OPTIONS` |
| حدود الخطأ | `Layout` يلفّ `Outlet` بـ`variant="content"` (التنقل يبقى)، والجذر `variant="screen"` + `resetKeys` على المسار |
| تنقية السجلات | `redactSensitiveText` (كوكي/`Bearer`/JWT/بريد/هاتف/رموز طويلة) قبل `console`/`reportClientError`؛ `ErrorBoundary` يجرّد `stack` أيضًا |
| `axe`/E2E | 15 اختبارًا (11 desktop + 4 mobile) كما في `270`؛ أُعيد التحقق بـ`--list` فقط محليًا (لا قاعدة) |
| OpenTelemetry | وسوم النشاط: اسم النوع/المعالج والحصيلة فقط؛ لا استثناء/طلب/معاملات SQL؛ `TraceContextEnricher` معرّفات فقط |
| ميزانية استعلامات `P3-08` | `QueryBudgetTests` تغطي القوائم/التفاصيل/التصدير/التدقيق/البوابة/تذكيرات الشطب (Postgres-gated ⇒ مرجعها `backend-postgres`) |
| الأسرار | `secrets-guard` نظيف (23 مطابقة معروفة)؛ سكربتات النسخ لا تطبع كلمة المرور وتستخدم `PGPASSWORD`/`--no-password` |

## 7. الإصلاحات المنفّذة

1. حُذف `_audit` (الحقل + معامل المُنشئ + الإسناد) من `UserManagementService`.
2. حُذف `_audit` من `UpdateDocumentCommandHandler`.
3. حُذف `_documents` من `CreateExecutionCircuitCommandHandler`.
4. حُذف `_pendingDelegationsCount` وبناؤه من `DocumentDelegationServiceTests`.
5. حُذف `_branchId` وإسناده من `CorrespondenceIntegrationTests`.
6. **تقوية** `CountUnread_ArchivedAndOtherBranch_AreExcluded`: أُضيف نفي صريح
   `Assert.DoesNotContain(page.Items, a => a.Id == _alertHeadInB.Id)` بدل الاعتماد على
   العدّ وحده (يستخدم الحقل ويشدّد الدليل السلوكي).
7. حُدّثت مواضع البناء في الاختبارات (`DocumentServiceFactory`,
   `ExecutionCircuitServiceTests` ×2, `UserManagementServiceTests`,
   `SubHeadFoundationTests`) بإسقاط المعاملات المحذوفة — **بلا تغيير أي قيمة/سلوك**.

### 7.1 الحارس الجديد: `scripts/dead-code-guard.mjs`

- يفحص **`backend/src` + `backend/tests`** لكل حقل `private` (readonly أو متغيّر)
  في عائلة `partial class`، ويعدّ القراءات الفعلية بعد:
  تجريد التعليقات، وإسقاط سطر التصريح، وإسقاط الطرف الأيسر من الإسناد المباشر.
- **صفر قراءة = فشل**؛ الرسالة تحمل `file:line — Class.Field`.
- `--self-test`: **10/10** حالات (حقل ميت، مهيّئ فقط، `nameof`، تعليق، partial آخر،
  القراءة على يمين إسناد، تشابه الأسماء `_dep`/`_depot`، خاصية معبّرة ليست حقلًا).
- أُضيف إلى وظيفة `guards` في `.github/workflows/ci.yml` باسم **Dead field guard**.
- عند تشغيله على الشجرة **بعد** الإصلاح: `dead-code-guard: نظيف — لا حقل private بلا استخدام.`

## 8. إعادة البوابات بعد الإصلاح

| البوابة | النتيجة |
|---|---|
| `dotnet build -warnaserror` | 0/0 |
| `dotnet format --verify-no-changes` | نظيف |
| `Architecture.Tests` | **41/41** |
| `Application.Tests` | **778** / 1661 متجاوز / 0 فاشل |
| `Api.Tests` (`!=Sequential`) | **916** / 615 متجاوز / 0 فاشل |
| `dead-code-guard --self-test` | **10/10** |
| `dead-code-guard` | نظيف |
| `subhead`/`determinism`/`migration-watchlist`/`migration-safety` | نظيفة |
| `has-pending-model-changes` | «No changes» |
| `vitest`/`oxlint`/`tsc`/`build`/`budget`/`guard`/`generate:api` | كما في §3 (لا تغيير واجهة) |

> **PostgreSQL محليًا غير متاح** (`DOCGEN_TEST_POSTGRES` غائب): 1661+615 اختبارًا
> متجاوزة محليًا، و`HeadAlertServicePermissionTests` المعدَّل منها. المرجع القاطع هو
> وظيفة `backend-postgres` في `CI` (تشغيل فعلي + `DOCGEN_REQUIRE_POSTGRES=1` + منع
> التخطي). لم أُشغّل `Playwright` محليًا (لا قاعدة)؛ تحققت من العدّ والحارس فقط.

## 9. التصحيحات الوثائقية (بلقطة جديدة، لا تحرير مجمّد)

- `docs/audit/243` ذكر «0 حقل بتكرار واحد» و`docs/audit/246` ذكر «0 حقل ميت»؛
  القياس المستقل عند `08e96362` وجد **6** حقول ميتة (D-01…D-06). أُصلحت جميعها
  وأُضيف حارس. الادعاءان يبقيان كما هما (لقطتان مجمّدتان) **بتصحيح صريح هنا**.
- `docs/README.md` أُضيف إليه مؤشر هذه اللقطة (`273`).
- `docs/PERFECTION_ROADMAP.md`: أُضيف سطر حالة مؤرخ تحت `P4-01` يشير إلى هذا التدقيق
  والحارس الجديد فقط (لا إعادة كتابة لادعاءات سابقة).

## 10. الهجرات

**لا هجرة جديدة.** `has-pending-model-changes` = «No changes». المعلّق يبقى **6**
(`AddIdempotencyResponseMetadata`…`P3_02_PortalSearchIndex`)؛ لا يُطبَّق شيء في هذا الـPR.

## 11. ما لم يُفحص ولماذا (إفصاح)

1. **قاعدة PostgreSQL حقيقية محليًا**: غير متاحة؛ كل ما هو `PostgresFact` متجاوز
   محليًا. لم أُعِد إثبات `P0-P3` التاريخي (خارج النطاق)، ولا اختبارات ميزانية
   الاستعلام فعليًا (تُشغَّل في `backend-postgres`).
2. **E2E/`axe`/375px**: لم تُشغَّل هنا (تحتاج قاعدة + خادمين)؛ اعتمدت لقطة `270`
   وأعدت التحقق من العدّ (`15`) ومن رفض حارس الإنتاج بأمر فعلي.
3. **staging/لقطات بصرية/توسيع تدفقات E2E**: ما زالت دينًا معلنًا في `P6-04` (§10
   من `270`)؛ لا تغيير.
4. **P4-05 (حدود modular monolith)**: مؤجل صراحةً، لم يُفتح.
5. **`N+1` في المعاينات/الشعب**: قائم قبل الدفعة؛ لم أُغيّره حفاظًا على «لا تغيير
   سلوكي»، وسُجّل كدين أداء.

## 12. الحكم

- **المحور A (العقود/DI):** سليم — 229/229/0، لا معاملات في المتحكمات، لا سطح قراءة
  مكتوب على خدمة.
- **المحور C (التكافؤ):** 11/11 دالة بعيّنة موثّقة، **0** فرق دلالي.
- **المحور B (البقايا):** وُجد **6** حقول ميتة (3 في `src`، 3 في الاختبارات) —
  أُصلحت كلها، وحارس `CI` جديد يمنع تكرارها.
- **المحور D (الوثائق):** أعداد `DI`/الاختبارات/`OpenAPI`/الهجرات مطابقة؛ ادّعاء
  «0 حقل ميت» صُحّح بلقطة جديدة (D-07).
- **المحور E/F/G/H:** لا أسرار/PII، الإعادة للقراءات بسقف 3، CSRF للكتابة، حدود
  الخطأ تحفظ التنقل، ولا هجرة/انحراف نموذج.

**لا ادعاء «نظيف» بلا برهان:** ما أُثبت أعلاه بأوامر/مخرجات، وما تُخطّي مُعلن في §11.
