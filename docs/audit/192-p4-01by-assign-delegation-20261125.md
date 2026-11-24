# `P4-01by` — نقل اعتماد الإنابة إلى Command — 2026-11-25

> **النطاق:** الشريحة الثانية من «اعتماد الإنابة» في «متبقٍ» خريطة `P4-01b`:
> ملكية `DocumentDelegationService.AssignAsync` (§7.1) كاملة داخل معالج أمر،
> مع الحفاظ الحرفي على السلوك، عبر الاستحقاق `P4-01by` على فرع
> `refactor/p4-01by-assign-delegation` من `master` (`e584cbd9`).

## ما نُفِّذ

| البند | التفصيل |
|---|---|
| الأمر | `AssignDocumentDelegationCommand(DelegationId, Request, UserId, HeadBranchId, ActorName)` في `UseCases/Delegations/AssignDocumentDelegationCommand.cs` |
| المعالج | `AssignDocumentDelegationCommandHandler` (public على نمط معالجَي الإنابة السابقين) بمعاملاته الاثني عشر: مستودعات الإنابة/المستخدمين/الفروع/الدوائر/الملفات، الـOutbox، وحدة العمل، المعاملة، التدقيق، مصنّف أخطاء القاعدة، الساعة والمنطقة الزمنية |
| الحرّاس | إعادة استخدام `DocumentDelegationRules.LoadHeadCallerAsync/ResolveApprovalScopeAsync/EnsureApprover` (مصدر واحد) |
| النسخ | `CopySourcePartiesAndBond` صار `internal static` لإعادة استخدامه في المعالج (لا نسخة ثانية من منطق نسخ الأطراف/السند) |
| العقود | حُذف `AssignAsync` من `IDocumentDelegationService` (12 → 11 عضوًا)؛ بقيت القراءات و`Register`/`Complete` للحزمة التالية |
| الملفات الجزئية | حُذف `DocumentDelegationService.Approval.cs` كاملًا بعد إفراغه |
| المتحكم | `DelegationsController` يحقن المعالج؛ المسار والحراسة وترجمة `DocumentConflictException` كما هي |
| `DI` | تسجيل المعالج: 110 معالجًا / 110 تسجيلات / 0 غير مسجَّل |
| الاختبارات | مصنع `DelegationWriteHandlerFactory.Assign` + جسر `AssignAsync`، وتحويل 88 موضع نداء (75 + 13 + 1) إلى المعالج |

## إثبات الحفاظ على السلوك

- **مقارنة سطرية لجسم الدالة:** 136/136 سطرًا، `diffs = 0` بعد تطبيع بادئة
  `DocumentDelegationRules.` و`DocumentDelegationService.` وأسطر الأسماء المستعارة.
- **مقارنة النصوص الحرفية (اتجاهان):** 17/17، `missing = 0`، `added = 0`.
- **حدود المعاملة:** `_tx.RunAsync` يبدأ بعد كل الحرّاس (كما في الأصل)، و`catch`
  وحدانية `SourceDelegationId` يبقى **خارج** المعاملة ليترجم إلى 409.
- **إنشاء الملف المناب:** نفس الحقول (الفرع/الدائرة/المحكمة/`GeneralEntitySide`/
  `IsDraft`/`FileType`/`SourceDelegationId`)، ونفس `CopySourcePartiesAndBond`،
  ونفس تنبيه الإسناد و`SupersedePlan` والوثيقة والتدقيق.

## مراجعة نقدية أولى (قبل الإثبات النهائي)

| # | السؤال النقدي | الفحص | النتيجة |
|---|---|---|---|
| 1 | هل نُسخ منطق نسخ الأطراف في المعالج؟ | فحص `Helpers.cs` + المعالج | لا؛ صار `internal static` ويُستدعى نفسه |
| 2 | هل تحرّك `catch` الودّي خارج نطاقه؟ | مقارنة سطرية | لا؛ ما زال يغلّف `_tx.RunAsync` فقط |
| 3 | هل بقي مستخدم لـ`AssignAsync` على الخدمة؟ | بحث شامل | لا؛ 88 موضعًا على المعالج |
| 4 | هل صار أي حقل من حقول الخدمة ميتًا؟ | جرد آلي | **`_branches` اكتُشف ميتًا وأُزيل** (تفصيل `193`) |
| 5 | هل سطح الواجهة تغيّر بأكثر من الحذف المقصود؟ | مقارنة آلية | 12 → 11، عضو واحد فقط |
| 6 | هل كل معالج مسجَّل؟ | عدّ آلي مقابل `DependencyInjection.cs` | 110/110/0 |
| 7 | هل يضاعف التدقيق أو تُفتح معاملة متداخلة؟ | لا وسم `ITransactionalRequest`/`IAuditedRequest` | لا |
| 8 | هل بقي partial فارغ بعد النقل؟ | فحص الملف | حُذف `DocumentDelegationService.Approval.cs` |

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
| `DI` | عدّ التسجيلات | 110 معالجًا / 0 غير مسجَّل |

> **ملاحظة صدق:** اختبارات اعتماد الإنابة (`PostgresFact`) تُخطّى محليًا لغياب خادم؛
> بوابتا `backend-postgres` في `CI` هما المرجع القاطع قبل الدمج.

## الهجرات

**لا هجرة جديدة.** المعلّق يبقى **6**، ولا يُشغَّل
`dotnet ef database update --context DocGeneratorPostgresDbContext` ضمن هذه المرحلة.
