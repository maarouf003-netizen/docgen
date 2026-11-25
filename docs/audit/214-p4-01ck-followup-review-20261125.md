# `P4-01ck` ملحق — مراجعة هجومية ثانية وتنظيف — 2026-11-25

> **نطاق الملحق:** مراجعة عدائية بعد تحويل قراءات الإنابات الثلاث إلى Query Handlers
> وقبل الدمج: بقايا الاستدعاءات، الملفات الجزئية الفارغة، المزيّفات، تكرار المساعدات،
> الحقول/المعاملات/الاستيرادات الميتة، وتكافؤ الحمولة والرسائل والنطاق ورمز الحالة في
> المتحكم والبوابة.

## ما فُحص

- بحث شامل في `backend/src` عن الطرق الثلاث على أي خدمة/واجهة: **0 مستدعٍ قديم** —
  المتحكم يحقن ثلاثة معالجات، و`PortalService` يحقن معالج القائمة.
- عدّ `IDocumentDelegationService` قبل/بعد: **11 → 8**؛ وبحث عن أي حذف إضافي: **0**.
- عدّ حقول `DocumentDelegationService`: **9 → 8** بعد حذف `_documents`، وكلها مستخدمة
  (أدنى تكرار 3)، ومنشئها **9 → 8**.
- عدّ partials: بلا حذف هذه الدفعة؛ `DocumentDelegationService.Crud.cs` بقي غير فارغ
  (`SyncTargetsTransferredAsync`)، وبقية الملفات الجزئية غير فارغة.
- فحص المزيّفات: `FakeDelegationService` في اختبار حارس `Api` صار بلا مستخدم فأُزيل.
- عدّ تسجيلات `DI` مقابل أصناف المعالجات: **137/137/0** بلا تكرار ولا صنف غير مسجَّل.
- فحص تكرار المساعدات: تعريف واحد لكل من `ToDto` (الخدمة) و`ListOfTargetAsync`
  (معالج القائمة).
- مطابقة سطرية/نصية للدوال المنقولة (اتجاهان) وللمساعد المنقول.
- فحص بقايا التسميات القديمة: لا ذكر لـ`ListForDocumentAsync`/`ListPendingForHeadAsync`/
  `CountPendingForHeadAsync` على خدمة الإنابات في `src` أو الاختبارات.
- فحص `generate:api` بعد التوليد (`git diff --exit-code`) واستعادة ضجيج `CRLF`.
- فحص `has-pending-model-changes` بهوية اتصال وهمية و`--no-build`.

## الملاحظات المكتشفة والإصلاح

| # | الملاحظة | الدليل | الإصلاح |
|---|---|---|---|
| 1 | **`FakeDelegationService` صار ميتًا** بعد أن حُذف اعتماد الخدمة من `DelegationsController` | لا بناء للصنف في أي اختبار بعد تحديث المتحكم | حُذف الصنف كاملًا مع استيرادات `DTOs`/`Services`/`Entities` التي كان يستهلكها |
| 2 | **حقل `_service` ميت أصلًا** في `DelegationDraftCircuitScopeTests` (تعريف وبناء بلا أي استخدام) | بحث `_service` داخل الملف: مطابقتان فقط (تعريف/إسناد) | حُذف الحقل وسلسلة بناء الخدمة والاعتماديات التي لم تبقَ لها مستخدمة |
| 3 | **مساعد `BuildService` غير مستخدم** في `DelegationConcurrencyTests` (نسخة مفردة ميتة بجانب `BuildServices`) | لا نداء له في الملف | حُذف، وبقي `BuildServices` وحده بعد تحويله لبناء معالج القائمة |
| 4 | **متغيران ميتان** `registrationDates`/`occurrences` في `SubHeadDelegationTests` بعد حذف بناء الخدمة | لا استخدام لهما | حُذفا |
| 5 | **استيرادات ميتة** في `DocumentDelegationService.Crud.cs` بعد نقل القراءات | `System.Globalization`/`System.Text.Json`/`Common`/`Common.Interfaces` بلا استخدام | قُصّت إلى `DTOs`/`Entities`/`Enums` المستخدمة فقط |
| 6 | **استيرادان ميتان** في `DelegationDraftCircuitScopeTests` بعد حذف بناء الخدمة | `Common.Interfaces`/`Infrastructure.Persistence` بلا استخدام | حُذفا |
| 7 | احتمال أن يكون `ListOfTargetAsync` بقي على الخدمة بلا مستخدم | عدّ التعريفات والنداءات | نُقل إلى معالج القائمة (تعريف واحد)، وحُذف من الخدمة |
| 8 | احتمال أن يكون استعلام البوابة تجاوز فحص النطاق | مقارنة `PortalService.ListDelegationsAsync` | لا — `GetScopedDocumentAsync` أولًا كما كان، ثم نفس استعلام الملف |
| 9 | ضجيج `CRLF` في `generated.ts` بعد التوليد على Windows | `git diff --exit-code` بلا فرق دلالي | أُعيد بـ`git checkout --` عند الحاجة؛ الملف نظيف |
| 10 | احتمال تغيّر شكل عدّاد الطلبات (`count`/`rejectedCount`) بعد الاستدعاء المزدوج | مقارنة جسم `PendingCount` | لا — نفس أسماء الخصائص والترتيب ونفس الاستعلامين (مقبول/مرفوض) |

## إثبات التكافؤ بعد الإصلاح

- **الأجسام المنقولة:** **17/17** سطرًا بعد التطبيع؛ `diffs = 0` في الأربعة (مع استثناء
  سطر ربط واحد ونص تأهيل واحد موثّقين).
- **النصوص الحرفية (اتجاهان):** `missing = 0` و`added = 0` للمقطع المنقول.
- **المتحكم:** نفس المسارات (`documents/{id}/delegations`، `delegations/pending`،
  `delegations/pending-count`)، ونفس `[Authorize(Roles = "lawyer,head,subhead,manager,admin")]`،
  ونفس `CanApprove`/`RequireApproverScope`/`CanAccess`، ونفس `Ok(...)`/`Forbid()`/`ApiProblem`.
- **البوابة:** نفس عقد `ListDelegationsAsync` (`IReadOnlyList<DelegationDto>?`) ونفس دلالة
  `null` لخارج النطاق.
- **الاختبارات:** **49** موضع نداء حُوِّل إلى المعالجات عبر جسر واحد؛ لا حذف ولا تعديل
  توقّع واحد. مصنعا البناء ومصنع السباق وحارس `Api` محدَّثة.
- **الخدمة:** 8 حقول كلها مستخدمة؛ 0 partial فارغ؛ 0 مستدعٍ قديم؛ 0 مزيّف قديم.
- **`DI`:** 137 معالجًا مسجَّلًا مقابل 137 صنفًا (بلا تكرار).

## متبقٍ بعد هذه الدفعة (صريح)

- **اكتملت قراءات الإنابات:** لا يبقى أي سطح قراءة على `IDocumentDelegationService`،
  وبقيت عليه أعضاء المزامنة/الحرّاس/المرآة/التنبيهات التي تُستدعى داخل معاملات أخرى
  (خارج نطاق هذه الدفعة عمدًا).
- **متبقٍ في `P4-01`:** **مسح module-by-module** لبقية الوحدات (المطالعات/الاستئنافات/
  البوابة/الإحصاءات) للتأكد من أن كل `use-case` له معالج واحد بملكية واضحة، بلا كسر
  عقود خارجية — بند متابعة معماري لا يغيّر سلوكًا.
- **لا هجرة جديدة**؛ المعلّق يبقى **6**.

## التحقق

`build -warnaserror` 0/0 · `format --verify-no-changes` نظيف · Architecture 29/29 ·
Application 758/1659/0 · Api 901/615/0 (والـ`Sequential` منفصلًا: 0/5/0 تخطٍّ محلي) ·
frontend 1809/1809 + `oxlint`/`tsc -b`/`build`/`budget`/`guard`/`generate:api` بلا فرق ·
الحرّاس الستة نظيفة · `dependency-audit` 9 مشاريع · `npm-audit-guard` 0 ثغرة ·
`has-pending-model-changes` نظيف · `DI` 137/137/0 · 0 حقل ميت · 0 partial فارغ.

> **ملاحظة صدق:** اختبارات `PostgreSQL` تُخطّى محليًا (`DOCGEN_TEST_POSTGRES` غير مضبوط)؛
> الحكم النهائي للسلوك على القاعدة هو `backend-postgres` في `CI`.
