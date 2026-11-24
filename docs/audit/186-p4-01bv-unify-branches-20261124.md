# `P4-01bv` — نقل توحيد تسميات الفروع إلى Command — 2026-11-24

> **النطاق:** البند الثاني في «متبقٍ» خريطة `P4-01b` بعد الدخول: ملكية
> `PublicEntityService.UnifyBranchesAsync` كاملة داخل معالج أمر واحد، مع الحفاظ
> الحرفي على السلوك، عبر الاستحقاق `P4-01bv` على فرع
> `refactor/p4-01bv-unify-branches` من `master` (`d7565e88`).

## ما نُفِّذ

| البند | التفصيل |
|---|---|
| الأمر | `UnifyPublicEntityBranchesCommand(GroupId, Request, Actor)` في `UseCases/PublicEntities/UnifyPublicEntityBranchesCommand.cs` |
| المعالج | `UnifyPublicEntityBranchesCommandHandler` بمعاملاته الستة: `IPublicEntityWriteRules` / `IPublicEntityRepository` / `IUserRepository` / `IUnitOfWork` / `ITransactionRunner` / `IAuditLogger` |
| العقود | حُذف `UnifyBranchesAsync` من `IPublicEntityService` (24 → 23 عضوًا، بلا أي إضافة أو تعديل آخر)؛ بقيت معاينة الإجراء `PreviewBranchActionAsync` قراءةً |
| الخدمة | حُذفت الطريقة من `PublicEntityService.Branches.Lifecycle.cs`، وبقيت مساعدات عمليات الفروع المشتركة كما هي حرفيًا (0 سطر مضاف / 120 محذوفًا) |
| المتحكم | `EntityRegistryController` يحقن `ICommandHandler<UnifyPublicEntityBranchesCommand, UnifyBranchesResponse>` ويستدعي `HandleAsync`؛ الترويسة والمسار والترجمة كما هي |
| `DI` | تسجيل المعالج: 104 معالجًا / 104 تسجيلات / 0 غير مسجَّل |
| حدود المعاملة | لا وسم `ITransactionalRequest`؛ المعالج يملك معاملة واحدة بنفس المواضع الثلاثة لحفظ `SaveChangesAsync` والتدقيق كما في الأصل |
| الاختبارات | جسر `UnifyPublicEntityBranchesCommandExtensions.UnifyBranchesAsync`، وتحديث `PublicEntityServiceTests` (بناء المعالج + موضع النداء الوحيد) |

## إثبات الحفاظ على السلوك

- **مقارنة سطرية لجسم الدالة:** 82 سطرًا في الوالد و82 في المعالج، و4 فروق فقط
  كلها استبدال `groupId` ← `command.GroupId` (نداءان للمساعد + خاصية `groupId`
  في حمولة حدث التغيير + `GroupId` في الحدث) — لا تغيير منطقي.
- **مقارنة النصوص الحرفية (اتجاهان):** 7/7 في الجسم، `missing = 0`، `added = 0`
  (رسائل التوحيد والتحقق والتدقيق كما هي).
- **نداءات المساعدات:** كل مساعد مشترك بقي في الخدمة ويُستدعى عبر
  `IPublicEntityWriteRules` (`GetActiveGroupEntryAsync`، `EnsureNoDuplicateEntryAsync`،
  `RepointEntryLinks`، `AddFoldAliases`، `SyncBranchLabelsAsync`،
  `SyncAppealsAfterEntityChangeAsync`، `TrackChangeEventAsync`،
  `InsertBranchOccurrencesAsync`، `InsertBranchChangeAlertAsync`، `Required`) وكلها
  تعود إلى النسخة نفسها من `PublicEntityService` المسجَّلة في `DI`.
- **الملف الجزئي:** فرق `PublicEntityService.Branches.Lifecycle.cs` إضافات = 0؛
  المساعدات المتبقية لم تتغير بايتًا واحدًا.
- **حدود المعاملة:** معاملة واحدة حتى نهاية التدقيق، ونفس ترتيب الحفظ/التدقيق/التنبيه.

## مراجعة نقدية أولى (قبل الإثبات النهائي)

| # | السؤال النقدي | الفحص | النتيجة |
|---|---|---|---|
| 1 | هل يدخل المعالج في معاملة متداخلة؟ | لا وسم `ITransactionalRequest` | معاملة واحدة فقط |
| 2 | هل يضاعف `AuditBehavior` تدقيق التوحيد؟ | لا `IAuditedRequest` | لا تدقيق مزدوج |
| 3 | هل بقي في الملف الجزئي كود/استيراد بلا استخدام بعد النقل؟ | فحص الملف كاملًا + بناء | 3 استيرادات ميتة أُزيلت (تفصيل الملحق `187`) |
| 4 | هل أصبح أي مساعد مشترك ميتًا؟ | جرد نداءات المساعدات عبر المشاريع | لا — كلها مستخدمة من rename/merge/abolish/preview |
| 5 | هل صار أي حقل من حقول الخدمة الـ13 ميتًا؟ | عدّ آلي لكل حقل عبر كل الملفات الجزئية | لا — أدنى تكرار 3 (تصريح/إسناد/استخدام) |
| 6 | هل تغيّر عقد `IPublicEntityService` بأكثر من الحذف المقصود؟ | مقارنة سطح الواجهة آليًا | 24 → 23، والمحذوف هو `UnifyBranchesAsync` وحده |
| 7 | هل معاينة `unify` كانت تعتمد على الطريقة المحذوفة؟ | قراءة `PreviewBranchActionAsync` | مسار قراءة مستقل تمامًا |
| 8 | هل كل معالج مسجَّل؟ | عدّ آلي مقابل `DependencyInjection.cs` | 104/104/0 |

## التحقق

| البوابة | الأمر | النتيجة |
|---|---|---|
| backend | `build -warnaserror` · `format --verify-no-changes` | 0 تحذير/0 خطأ · نظيف |
| Architecture | `dotnet test …Architecture.Tests` | 29/29 |
| Application | `dotnet test …Application.Tests` | 758 ناجح / 1659 متخطّى (لا `PostgreSQL` محلي) / 0 فشل |
| Api | غير التسلسلي / التسلسلي | 901 / 615 / 0 · 0 / 5 / 0 |
| frontend | `oxlint` · `tsc -b` · `vitest run` · `build` · `guard` · `generate:api` | نظيف · نظيف · 1809/1809 (171 ملفًا) · ناجح · نظيف (94 ملفًا) · بلا فرق |
| guards | الستة | نظيفة (حتمية: 0 استثناءات) |
| audit | `dependency-audit` · `npm-audit-guard` | نظيف (9 مشاريع) · نظيف |
| migration-guard | `has-pending-model-changes` | لا تغييرات في الموديل |
| `DI` | عدّ التسجيلات | 104 معالجًا / 0 غير مسجَّل |

> **ملاحظة صدق:** اختبار `UnifyBranches_UnifiesIntoSurvivorWithCorrectedName` وبقية
> اختبارات `PostgreSQL` تُخطّى محليًا لغياب خادم؛ بوابتا `backend-postgres` في `CI`
> هما المرجع القاطع قبل الدمج، ولا يُدّعى تشغيلهما هنا.

## الهجرات

**لا هجرة جديدة.** المعلّق يبقى **6**، ولا يُشغَّل
`dotnet ef database update --context DocGeneratorPostgresDbContext` ضمن هذه المرحلة.
