# `P4-01cd`/`P4-01ce` ملحق — مراجعة هجومية ثانية وتنظيف — 2026-11-25

> **نطاق الملحق:** مراجعة عدائية بعد نقل «الحلول» (إلغاء واستبدال هوية أم) و«اقتراح
> تعديل قيد الجهة» وقبل الدمج: بقايا الاستدعاءات، الحقول/الاستيرادات الميتة، الملفات
> الجزئية، السطح العام، وتكافؤ الحمولات.

## ما فُحص

- بحث شامل في `backend/src` عن `AbolishAndReplaceAsync`/`ProposeEditAsync`: الباقي هو
  `PreviewAbolishAndReplaceAsync` (قراءة في الواجهة/المتحكم/الخدمة) فقط — 0 مستدعٍ كتابي
  على الخدمة.
- مقارنة سطح `IPublicEntityService` آليًا: 19 → 17 بعد مرحلتي هذه الدفعة (عضوان محذوفان فقط).
- مقارنة سطرية/نصية لجسمَي العمليتين (193 سطرًا للحلول، 82 للاقتراح): كلها `diffs = 0`
  بعد تطبيع تأهيل المساعدات والثابت المشترك.
- جرد كل حقول `PublicEntityService` الـ13 بعد النقل (أدنى تكرار 3:
  `_changeEvents`/`_appeals`).
- فحص الملفات الجزئية بعد الحذف، ومزيّفات الواجهة في الاختبارات، وعدّ تسجيلات `DI`
  مقابل أصناف المعالجات.

## الملاحظات المكتشفة والإصلاح

| # | الملاحظة | الدليل | الإصلاح |
|---|---|---|---|
| 1 | **استيراد ميت** `DocGenerator.Application.Common` في `ProposePublicEntityEditCommand.cs` | لا نوع من `Common` مُستخدم بعد نقل المساعدات إلى العقد | أُزيل؛ بقي `Common.Interfaces`/`Common.Pipeline` |
| 2 | **4 استيرادات ميتة** في `PublicEntityService.Groups.cs` بعد حذف منطق الحلول (`System.Globalization`/`Common`/`Domain.Entities`/`Domain.Enums`) | الملف لم يعد يستخدم إلا DTOs و`_entities`/`_users` و`Required` الخاصة | قُصّ رأس الملف إلى `using DocGenerator.Application.DTOs;` فقط |
| 3 | **ملف جزئي أُفرغ بالكامل** `PublicEntityService.Registry.Writes.cs` (كان يحوي `ProposeEditAsync` وحده) | عدّ الأعضاء = 1 قبل الحذف | حُذف بـ`git rm` |
| 4 | لا مزيّف `IPublicEntityService` في الاختبارات | بحث `: IPublicEntityService` و`Mock<IPublicEntityService>` | لا شيء — لا حاجة لتحديث مزيّف |
| 5 | احتمال بقاء partial فارغ | عدّ أعضاء `Groups.cs` = 5 (معاينتان + 3 مساعدات عدّ) | لا حذف — الملف غير فارغ |
| 6 | احتمال تكرار `MigrateDelegatesAsync`/`Broadcast*`/`Sync*` في المعالج | جرد عبر `src` | لا تكرار — مصدر واحد في `Sync.cs`/`Merge.cs` يُستهلك عبر العقد الداخلي |
| 7 | حقل `_registry` في المتحكم قد يصبح ميتًا | عدّ `_registry.` = 18 نداءً لقراءات أخرى | ليس ميتًا — بقي كما هو |
| 8 | احتمال انحراف العقد المولَّد بعد نقل الملكية | `npm run generate:api` + `git diff --exit-code -- src/api/generated.ts` | لا فرق (`exit=0`)؛ المسارات وأنواع الاستجابة كما هي |
| 9 | لا اختبار API مباشر لمسارَي `/propose-edit` و`/groups/abolish-and-replace` عند غياب `PostgreSQL` محليًا | الحزمة تعتمد `PostgresFact` | فجوة بيئية سابقة موثّقة؛ `backend-postgres` في `CI` هو المرجع |

## إثبات التكافؤ بعد الإصلاح

- **الحلول:** 193/193 سطرًا، `diffs = 0`، النصوص 15/15 بلا فرق (`missing = 0`، `added = 0`).
- **الاقتراح:** 82/82 سطرًا، `diffs = 0`، النصوص 6/6 بلا فرق (`missing = 0`، `added = 0`).
- **العقود:** `IPublicEntityService` 19 → 17 بلا إضافة؛ لا أعضاء جديدة في
  `IPublicEntityWriteRules` (كل المساعدات كانت مُسجَّلة سابقًا).
- **`DI`:** 116 معالجًا مسجَّلًا مقابل 116 صنفًا (كان 114 قبل الدفعة).
- **الخدمة:** 13 حقلًا كلها مستخدمة (أدنى تكرار 3)؛ 0 partial فارغ؛ 0 مستدعٍ قديم في `src`.
- **الثابت المشترك:** `PublicEntityBranchCatalog.ParentBranchName` بدل الثابت الخاص
  `DefaultBranchName` — نفس المصدر والقيمة، بلا نسخ حرفي.

## التحقق

`build -warnaserror` 0/0 · `format` نظيف · Architecture 29/29 · Application 758/1659/0 ·
Api 901/615/0 و0/5/0 · frontend 1809/1809 + build/guard/`generate:api` بلا فرق ·
الحرّاس الستة نظيفة · audit نظيف · migration-guard لا تغييرات · 0 حقل ميت · 0 partial فارغ.

> **ملاحظة صدق:** اختبارات `PostgreSQL` تُخطّى محليًا؛ الحكم النهائي لـ`backend-postgres` في `CI`.

## الهجرات

**لا هجرة جديدة.** المعلّق يبقى **6**.
