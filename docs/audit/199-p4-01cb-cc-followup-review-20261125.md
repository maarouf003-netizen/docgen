# `P4-01cb`/`P4-01cc` ملحق — مراجعة هجومية ثانية وتنظيف — 2026-11-25

> **نطاق الملحق:** مراجعة عدائية بعد نقل اعتماد دمج الجهات وإعادة تسمية الهوية الأم
> وقبل الدمج: بقايا الاستدعاءات، الحقول/الاستيرادات الميتة، الملفات الجزئية، السطح
> العام، وتكافؤ الحمولات.

## ما فُحص

- بحث شامل في `backend/src` عن `CommitMergeAsync`/`CommitMergeCoreAsync`/`RenameGroupAsync`:
  لا استدعاء على خدمة؛ الباقي معاينة قراءة (`PreviewRenameGroupAsync`) وتعليق وصفي.
- مقارنة سطح `IPublicEntityService` آليًا: 21 → 19 بعد مرحلتي هذه الدفعة (عضوان محذوفان فقط).
- مقارنة سطرية/نصية لجسمَي العمليتين (207 + 23 سطرًا للدمج، 100 للإعادة تسمية):
  كلها `diffs = 0` بعد تطبيع تأهيل المساعدات.
- جرد كل حقول `PublicEntityService` الـ13 بعد النقل (أدنى تكرار 3: تصريح/إسناد/استخدام).
- فحص الملفين الجزئيين (`Merge.cs` و`Groups.cs`) بعد الحذف، ومزيّفات الواجهة في الاختبارات،
  وعدّ تسجيلات `DI` مقابل أصناف المعالجات.

## الملاحظات المكتشفة والإصلاح

| # | الملاحظة | الدليل | الإصلاح |
|---|---|---|---|
| 1 | **6 استيرادات ميتة** في `PublicEntityService.Merge.cs` بقيت بعد إفراغها من منطق الدمج (`System.Globalization`/`Common`/`Common.Audit`/`Common.Interfaces`/`Domain.Entities`/`Domain.Enums`) | الملف لم يعد يستخدم إلا `_entities` وDTOs | قُصّ رأس الملف إلى `using DocGenerator.Application.DTOs;` فقط |
| 2 | **استيرادان ميتان** في `PublicEntityService.Groups.cs` (`Common.Audit` و`Common.Interfaces`) | لا مرجع لهما بعد حذف الطريقة | أُزيلا وبقيت الاستيرادات المستخدمة فعلًا |
| 3 | تعليق وصفي في `EntityChangeLogSummary.RenameSummary` ما زال يسمّي الطريقة المحذوفة `RenameGroupAsync` | بحث عن المصطلح في `src` | حُدِّث إلى `` `RenamePublicEntityGroupCommand` `` (لا تغيير سلوكي) |
| 4 | لا مزيّف `IPublicEntityService` في الاختبارات | بحث `: IPublicEntityService` | لا شيء — لا حاجة لتحديث مزيّف |
| 5 | `PortalScopingTests` كان يستدعي الإعادة تسمية على الخدمة مباشرة | بحث نداءات | أُضيف حقل معالج + بناؤه فوق `(IPublicEntityWriteRules)_entities` ونفس سياق القاعدة |
| 6 | احتمال تكرار `MigrateDelegatesAsync`/`Broadcast*` في المعالج | جرد عبر `src` | لا تكرار — مصادر واحدة في `Sync.cs` تُستهلك عبر العقد الداخلي |
| 7 | احتمال بقاء partial فارغ | عدّ أعضاء `Merge.cs` = 1 و`Groups.cs` = 6 | لا حذف — الملفان غير فارغين |
| 8 | حقل `_registry` في المتحكم قد يصبح ميتًا | عدّ `_registry.` = 20 نداءً لقراءات أخرى | ليس ميتًا — بقي كما هو |
| 9 | لا اختبار API مباشر لمسارَي `/merge-commit` و`/groups/{id}/rename` عند غياب `PostgreSQL` محليًا | `RF011IdempotencyTests` يعتمد `PostgresFact` | فجوة بيئية سابقة موثّقة؛ `backend-postgres` في `CI` هو المرجع |

## إثبات التكافؤ بعد الإصلاح

- **الدمج (الغلاف):** 23/23 سطرًا، `diffs = 0`، النصوص 5/5 بلا فرق.
- **الدمج (الجسم):** 207/207 سطرًا، `diffs = 0`، النصوص 13/13 بفرق تأهيل واحد في نص التدقيق.
- **إعادة التسمية:** 100/100 سطرًا، `diffs = 0`، النصوص 13/13 بفرق تأهيل واحد في نص التدقيق.
- **العقود:** `IPublicEntityService` 21 → 19 بلا إضافة؛ `IPublicEntityWriteRules` +3 أعضاء
  بتنفيذات صريحة في `PublicEntityService.cs` (مصدر واحد).
- **`DI`:** 114 معالجًا مسجَّلًا مقابل 114 صنفًا (كان 112 قبل الدفعة).
- **الخدمة:** 13 حقلًا كلها مستخدمة (أدنى تكرار 3)؛ 0 partial فارغ؛ 0 مستدعٍ قديم في `src`.
- **المساعدات النقية:** `CountUniqueDocuments`/`BuildDecreeSuffix` صارت `internal static`
  بدل نسخ المنطق في المعالج.

## التحقق

`build -warnaserror` 0/0 · `format` نظيف · Architecture 29/29 · Application 758/1659/0 ·
Api 901/615/0 و0/5/0 · frontend 1809/1809 + build/guard/`generate:api` بلا فرق ·
الحرّاس الستة نظيفة · audit نظيف · migration-guard لا تغييرات · 0 حقل ميت · 0 partial فارغ.

> **ملاحظة صدق:** اختبارات `PostgreSQL` تُخطّى محليًا؛ الحكم النهائي لـ`backend-postgres` في `CI`.

## الهجرات

**لا هجرة جديدة.** المعلّق يبقى **6**.
