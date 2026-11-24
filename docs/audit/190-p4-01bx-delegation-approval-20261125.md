# `P4-01bx` — نقل توجيه/تراجع/رفض الإنابة إلى Commands — 2026-11-25

> **النطاق:** الشريحة الأولى من «دورة حياة/اعتماد الإنابة» في «متبقٍ» خريطة
> `P4-01b`: ملكية مسارات §7.3/§7.4 (`RedirectToSectionAsync`، `RecallRedirectAsync`،
> `RejectAsync`) داخل ثلاثة معالجات، مع الحفاظ الحرفي على السلوك، عبر الاستحقاق
> `P4-01bx` على فرع `refactor/p4-01bx-delegation-approval` من `master` (`dc48fa32`).

## ما نُفِّذ

| البند | التفصيل |
|---|---|
| الأوامر | `RedirectDocumentDelegationCommand` / `RecallDelegationRedirectCommand` / `RejectDocumentDelegationCommand` في `UseCases/Delegations/DocumentDelegationApprovalCommands.cs` |
| معالج الرفض | `RejectDocumentDelegationCommandHandler` يعيد استخدام حرّاس الاعتماد والنطاق نفسها |
| القواعد المشتركة | نُقلت `LoadHeadCallerAsync` / `ResolveApprovalScopeAsync` / `EnsureApprover` إلى `DocumentDelegationRules` بصيغة `static` مع تمرير المستودعات — مصدر واحد يستخدمه `AssignAsync` (الباقي في الخدمة) والمعالج الجديد |
| الإسقاط | `CourtWithExecutionPrefix` صار `internal` لإعادة استخدامه في رسالة الرفض (لا نسخة ثانية) |
| العقود | حُذفت التواقيع الثلاثة من `IDocumentDelegationService` (15 → 12 عضوًا، بلا تغيير آخر) |
| المتحكم | `DelegationsController` يحقن المعالجات الثلاثة؛ المسارات والحراسة وترجمة `DocumentConflictException` كما هي |
| `DI` | تسجيل المعالجات الثلاثة: 109 معالجًا / 109 تسجيلات / 0 غير مسجَّل |
| حدود المعاملة | لا وسم `ITransactionalRequest`؛ كل معالج يملك معاملته ونفس `catch` التزامن كما في الأصل |
| الاختبارات | جسر `DocumentDelegationApprovalCommandExtensions`، وتحويل 14 موضع نداء (6 توجيه + 2 تراجع + 6 رفض) إلى المعالجين |

## إثبات الحفاظ على السلوك

- **مقارنة سطرية لجسم كل دالة** بعد تطبيع بادئة `DocumentDelegationRules.`:
  - `RedirectToSectionAsync`: 63/63 سطرًا، `diffs = 0`.
  - `RecallRedirectAsync`: 54/54 سطرًا، `diffs = 0`.
  - `RejectAsync`: 67/67 سطرًا، وفرقان فقط هما تمرير المستودعين للحرّاس المنقولة
    (`_users` و`_circuits`) — لا تغيير منطقي.
- **مقارنة النصوص الحرفية (اتجاهان):** 35/35 نصًّا فريدًا، `missing = 0`، `added = 0`.
- **حدود المعاملة:** موضع `_tx.RunAsync` ونطاقه كما هما في كل مسار.
- **الحرّاس:** شروط الحالة/الإسناد/الرفض/التوجيه المسبق/النسخة (`Version`) بنفس الترتيب.
- **التنبيهات والتدقيق:** نفس الأنواع و`SupersedePlan` ونصوص `details`، ونفس أرشفة
  التنبيهات عند غياب فرع المصدر في الرفض.

## مراجعة نقدية أولى (قبل الإثبات النهائي)

| # | السؤال النقدي | الفحص | النتيجة |
|---|---|---|---|
| 1 | هل نُقلت الحرّاس المشتركة أم نُسخت؟ | فحص `AssignAsync` + المعالج الجديد | نسخة واحدة في `DocumentDelegationRules` |
| 2 | هل تغيّر ترتيب الحرّاس (403/400/409)؟ | مقارنة سطرية | لا؛ صفر فرق في التوجيه والتراجع |
| 3 | هل بقي `catch` التزامن بنفس النوع؟ | قراءة المقارنة | نعم — `IsConcurrencyViolation` فقط |
| 4 | هل تُرك حقل/معامل بلا استخدام بعد نقل التوجيه؟ | عدّ حقول الخدمة | **حقلان ميتان اكتُشفا وأُزيلا** (تفصيل الملحق `191`) |
| 5 | هل بقي عضو في الواجهة بلا مستدعٍ؟ | مقارنة سطح الواجهة | حُذفت الثلاثة بالضبط |
| 6 | هل كل معالج مسجَّل؟ | عدّ آلي مقابل `DependencyInjection.cs` | 109/109/0 |
| 7 | هل يضاعف التدقيق أو تُفتح معاملة متداخلة؟ | لا وسم `ITransactionalRequest`/`IAuditedRequest` | لا |
| 8 | هل تغيّر عقد `OpenAPI`؟ | المسارات والاستجابات لم تمسّ (`generate:api` لاحقًا) | لا تغيير |

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
| `DI` | عدّ التسجيلات | 109 معالجًا / 0 غير مسجَّل |

> **ملاحظة صدق:** اختبارات الإنابة (`PostgresFact`) تُخطّى محليًا لغياب خادم؛ بوابتا
> `backend-postgres` في `CI` هما المرجع القاطع قبل الدمج، ولا يُدّعى تشغيلهما هنا.

## الهجرات

**لا هجرة جديدة.** المعلّق يبقى **6**، ولا يُشغَّل
`dotnet ef database update --context DocGeneratorPostgresDbContext` ضمن هذه المرحلة.
