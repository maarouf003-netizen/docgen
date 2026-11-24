# `P4-01bt` — نقل كتابات المراسلات الأربع إلى Commands — 2026-11-24

> **النطاق:** إغلاق آخر عائلة كتابة معلّقة في `CorrespondenceService` بعد تأجيلها في
> دفعة `01bm`–`01bs`، عبر الاستحقاق `P4-01bt` على فرع
> `refactor/p4-01bt-correspondence-writes` من `master` (`fbf7d898`).

## ما نُفِّذ

| البند | التفصيل |
|---|---|
| الأوامر | `CreateCorrespondenceCommand` / `AddCorrespondenceAddendumCommand` / `ReplyCorrespondenceCommand` / `MarkCorrespondenceSeenCommand` في `UseCases/Correspondences/CorrespondenceCommands.cs` |
| العقود | حُذفت التواقيع الأربعة من `ICorrespondenceService` وبقيت قراءات المراسلات و`CountUnseenAsync` |
| القواعد المشتركة | `ICorrespondenceWriteRules` الداخلي (11 عضوًا): الأهلية، النطاق، الترقيم، التوثيق، الإسقاط — تنفّذه `CorrespondenceService` صريحًا بلا فتح معاملة |
| المتحكمات | `CorrespondencesController` و`PortalController` تستدعي المعالجات؛ بقي `ICorrespondenceService` للقراءات فقط |
| `DI` | تسجيل `CorrespondenceService` ملموسًا + `ICorrespondenceService` + `ICorrespondenceWriteRules` من النسخة نفسها، وتسجيل المعالجات الأربعة |
| حدود المعاملة | كل أمر يملك معاملته عبر `ITransactionRunner` كما في الأصل؛ لا وسم `ITransactionalRequest` (لا معاملة متداخلة) |
| الاختبارات | ثلاثة ملفات جسرت كتاباتها إلى المعالجات عبر `CorrespondenceWriteCommandExtensions` بنفس وسائط التوقيع القديمة، مع ساعة/مستودع/وحدة عمل مخصّصة عند الحاجة |

## إثبات الحفاظ على السلوك

- **مقارنة حرفية للنصوص (اتجاهان):** 35 نصًّا فريدًا في الوالد و35 في المعالجات؛
  `missing = 0` و`added = 0` — كل رسائل الخطأ والتدقيق والحرّاس كما هي.
- **ترتيب المنطق:** الحرّاس ثم التحقق ثم الأهلية/النطاق ثم الساعة المحقونة ثم
  المعاملة والتدقيق — نفس التسلسل في الأوامر الأربعة.
- **سباق الترقيم:** نفس حلقة `MaxNumberGenerationAttempts` ونفس
  `DocumentConflictException` عند الاستنفاد.
- **سباق التوثيق:** نفس إعادة المحاولة الواحدة في الرد، ونفس إرجاع التوثيق الفائز
  في تأكيد المشاهدة.
- **الحقول الميتة:** بعد النقل صارت `_uow`/`_tx`/`_audit`/`_errors` غير مستخدمة في
  الخدمة، فأُزيلت مع معاملات بانيها (تفصيل الملحق `183`).
- **الملف الفارغ:** `CorrespondenceService.Compose.cs` صار partial بلا أعضاء فحُذف.

## التحقق

| البوابة | الأمر | النتيجة |
|---|---|---|
| backend | `build -warnaserror` · `format --verify-no-changes` | 0 تحذير/0 خطأ · نظيف |
| Architecture | `dotnet test …Architecture.Tests` | 29/29 |
| Application | `dotnet test …Application.Tests` | 758 ناجح / 1659 متخطّى (لا `PostgreSQL` محلي) / 0 فشل |
| Api | غير التسلسلي / التسلسلي | 901 / 615 / 0 · 0 / 5 / 0 |
| frontend | `oxlint` · `tsc -b` · `vitest run` · `build` · `guard` · `generate:api` | نظيف · نظيف · 1809/1809 · ناجح · نظيف · بلا فرق |
| guards | الستة | نظيفة |
| audit | `dependency-audit` · `npm-audit-guard` | نظيف · نظيف |
| migration-guard | `has-pending-model-changes` | لا تغييرات في الموديل |
| `DI` | عدّ التسجيلات | 102 معالجًا / 0 غير مسجَّل، و8 عقود `*WriteRules` مسجَّلة |

> **ملاحظة صدق:** اختبارات `PostgreSQL` الحقيقية تُخطّى محليًا لغياب خادم؛ بوابتا
> `backend-postgres` في `CI` هما المرجع القاطع قبل الدمج، ولا يُدّعى تشغيلهما هنا.

## الهجرات

**لا هجرة جديدة.** المعلّق يبقى **6**، ولا يُشغَّل
`dotnet ef database update --context DocGeneratorPostgresDbContext` ضمن هذه المرحلة.
