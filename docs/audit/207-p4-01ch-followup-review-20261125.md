# `P4-01ch` ملحق — مراجعة هجومية ثانية وتنظيف — 2026-11-25

> **نطاق الملحق:** مراجعة عدائية بعد نقل «توحيد تسمية الجهات N←1» إلى Command وقبل الدمج:
> بقايا الاستدعاءات، الملفات الجزئية الفارغة، الحقول/المعاملات/الاستيرادات الميتة، مزيّفات
> `IPublicEntityService`، تكرار المساعدات، وتكافؤ الحمولة/الوقوعات/التنبيهات/التدقيق.

## ما فُحص

- بحث شامل في `backend/src` عن `.UnifyNamesAsync`/`registry.unify`/`UnifyNamesCoreAsync`:
  **0 مستدعٍ قديم** — المتحكم يحقن الأمر، والخدمة لا تعرّف الكتابة بعد الآن.
- عدّ أعضاء `IPublicEntityService` آليًا: **11 → 10** (حذف `UnifyNamesAsync` وحده؛ صفر عضو مضاف
  أو معدَّل). وأعضاء `IPublicEntityWriteRules`: **بلا أي عضو جديد**.
- عدّ حقول `PublicEntityService`: **13 → 12** بعد حذف `_idempotency` (أدنى تكرار متبقٍ 3،
  فلا حقل ميت). وعدّ partials: **12 → 11** ملفًا بعد `git rm` لـ`Unify.cs`، ولا partial < عضوين.
- عدّ `new PublicEntityService(` في `backend/`: **3 مواضع** (اختباران + خدمة) ولا أحدها يمرّر
  مخزن عدم تكرار، فحذف المعامل الاختياري غير كاسر.
- فحص مزيّفات `IPublicEntityService`: **لا وجود لأي مزيّف** (`: IPublicEntityService` أو
  `Mock<IPublicEntityService>`)؛ الاختبارات تستخدم الخدمة الحقيقية فقط.
- عدّ تسجيلات `DI` مقابل أصناف المعالجات (بما فيها الملف الجديد غير المتتبَّع): **124/124/0**.
- فحص تكرار المساعدات العشر المستخدمة: تعريف واحد لكل منها (`DocumentLinks.cs`/`Sync.cs`/
  `Guards.cs`) + عضو الواجهة + التطبيق الصريح — بلا نسخة ثانية.
- مطابقة النصوص الحرفية للحمولة/الحدث/الوقوعات/التنبيهات/التدقيق بين الوالد والمعالج (اتجاهان).
- فحص `generate:api` بعد التوليد (`git diff --exit-code`) واستعادة ضجيج `CRLF`.

## الملاحظات المكتشفة والإصلاح

| # | الملاحظة | الدليل | الإصلاح |
|---|---|---|---|
| 1 | **حقل ومعامل ميتان** في `PublicEntityService`: `_idempotency` و`IIdempotencyStore? idempotency = null` | آخر مستخدميهما كان `UnifyNamesAsync` (المحذوفة)؛ `rg _idempotency` في الخدمة = 0 | حُذفا من الحقل والمُنشئ والإسناد؛ الاعتماد الاختياري انتقل للمعالج (اختباراته تمرّ `null` كما كانت) |
| 2 | **partial أُفرغ بالكامل** `PublicEntityService.Unify.cs` بعد النقل (261 سطرًا، عضوان) | عدّ أعضاء الملف بعد الحذف = 0 | حُذف بـ`git rm`؛ لا partial فارغ بين الـ11 المتبقية |
| 3 | **3 استيرادات ميتة** في `PublicEntityService.cs`: `System.Globalization`، `DocGenerator.Application.Common`، `DocGenerator.Application.Common.Audit` | لا `CultureInfo` ولا نوع من `Common`/`Common.Audit` مستخدم في الملف (الأخيران كانا ميتين قبل هذه الدفعة أيضًا) | قُصّ الرأس إلى `Common.Interfaces` + `DTOs` + `Domain.Entities` + `Domain.Enums` |
| 4 | **استيراد ميت قبل الدفعة** في `EntityRegistryController.cs`: `DocGenerator.Application.Common.Interfaces` | لا عضو من `Common.Interfaces` مستخدم (التحقق من قائمة أنواع المجلد كاملًا) | أُزيل؛ بقيت `Common`/`Common.Pipeline` المستخدمتان |
| 5 | **مرجعان نصيان قديمان** لاسم الطريقة في تعليقات معالجَي الدمج والمعاينة | `CommitPublicEntityMergeCommand` و`PreviewUnifyQuery` يذكران `UnifyNamesAsync` كمرجع سلوك | حُدِّثا إلى `UnifyPublicEntityNamesCommand` (تعليقات فقط، بلا سلوك) |
| 6 | احتمال بقاء `_registry` ميتًا في المتحكم بعد نقل آخر كتابة | عدّ `_registry.` = 11 نداءً لقراءات أخرى | ليس ميتًا — بقي كما هو |
| 7 | احتمال انحراف العقد المولَّد بعد تغيير توقيع الخدمة | `npm run generate:api` + `git diff --exit-code -- src/api/generated.ts` | `exit = 0` — لا فرق (السطح `HTTP` لم يتغير) |
| 8 | ضجيج `CRLF` في `generated.ts` بعد التوليد | `git ls-files --eol` بعد التوليد | أُعيد الملف بـ`git checkout --` فعاد `w/crlf` ونظيفًا (`hash` مطابق لـ`HEAD`) |
| 9 | احتمال تكرار جسم الدالة بين الخدمة والمعالج | بحث عن `UnifyNamesCoreAsync` | نسخة واحدة في المعالج؛ الخدمة بلا نسخة |
| 10 | احتمال نقل التحقق خارج المعاملة خطأً | مقارنة ترتيب السطور | لا — نفس الموضع (داخل `_tx.RunAsync`) كما كان عمدًا |

## إثبات التكافؤ بعد الإصلاح

- **جسم الدالة:** 178/178 سطرًا، `diffs = 0` (بعد تطبيع تأهيل `_rules.`/`PublicEntityService.`).
- **غلاف المفتاح:** 16/16، `diffs = 0`.
- **النصوص الحرفية (اتجاهان):** 16/16، `missing = 0`، `added = 0`.
- **`SaveChangesAsync`:** 2 (نفس الموضعين)، **`_tx.RunAsync`:** 1، **`Begin/Release/Complete`:** 1/1/1.
- **العقود:** `IPublicEntityService` 11 → 10 بلا إضافة؛ `IPublicEntityWriteRules` بلا تغيير.
- **`DI`:** 124 معالجًا مسجَّلًا مقابل 124 صنفًا (كان 123 قبل الدفعة: +1).
- **الخدمة:** 12 حقلًا كلها مستخدمة؛ 0 partial فارغ؛ 0 مستدعٍ قديم في `src`.
- **الاختبارات:** 12 موضع نداء حُوِّل إلى المعالج بنفس التوقيع عبر جسر الاختبار؛ لا حذف ولا
  تعديل توقع واحد.

## متبقٍ بعد هذه الدفعة (صريح)

- `IPublicEntityService` = **10 قراءات** فقط؛ لا كتابة عامة عليه.
- **متبقٍ من ملكية `use-case` الكاملة:** `CreateDocumentCommand` (أكبر مسار كتابة ما زال
  يعتمد على `IDocumentService`) ثم **مسح module-by-module** لبقية الوحدات (المطالعات/الإنابات/
  الاستئنافات/البوابة/الإحصاءات) للتأكد من أن كل use-case له معالج واحد بملكية واضحة، بلا
  كسر عقود خارجية.

## التحقق

`build -warnaserror` 0/0 · `format --verify-no-changes` نظيف · Architecture 29/29 ·
Application 758/1659/0 · Api 901/615/0 و0/5/0 · frontend 1809/1809 + build/guard/
`generate:api` بلا فرق · الحرّاس الستة نظيفة · `dependency-audit` 9 مشاريع ·
`npm-audit-guard` 0 ثغرة · `has-pending-model-changes` نظيف · `DI` 124/124/0 ·
0 حقل ميت · 0 partial فارغ.

> **ملاحظة صدق:** اختبارات `PostgreSQL` تُخطّى محليًا (`DOCGEN_TEST_POSTGRES` غير مضبوط)؛
> الحكم النهائي للسلوك على القاعدة هو `backend-postgres` في `CI`.

## الهجرات

**لا هجرة جديدة.** المعلّق يبقى **6**.
