# `P4-01cn` ملحق — مراجعة هجومية ثانية وتنظيف — 2026-11-25

> **نطاق الملحق:** مراجعة عدائية بعد تحويل قراءات المراسلات الست إلى Query Handlers
> وقبل الدمج: بقايا الاستدعاءات، الملفات الجزئية الفارغة، المزيّفات، تكرار المساعدات،
> الحقول/المعاملات/الاستيرادات الميتة، وتكافؤ الحمولة والرسائل والنطاق ورمز الحالة في
> المتحكمين والبوابة.

## ما فُحص

- بحث شامل في `backend/src` عن `ICorrespondenceService` وعلى أي مستدعٍ للمنهجيات الست
  على الخدمة: **0 مستدعٍ قديم** — `CorrespondencesController` يحقن ستة معالجات،
  و`PortalController` يحقن خمسة.
- عدّ `ICorrespondenceService` قبل/بعد: **6 → 0** (الواجهة والتسجيل حُذفا).
- عدّ حقول `CorrespondenceService`: **12 → 12**، وكلها مستخدمة (أدنى تكرار 3)،
  ومنشئها **12 → 12**.
- عدّ partials: **5 → 5**؛ لا ملف فارغ (ملف القراءات أُعيد تسميته `Mapping` وبقي
  يحوي الإسقاطات الأربعة).
- فحص المزيّفات: لا `FakeCorrespondenceService` في `src` أو الاختبارات؛
  `PortalServiceFactory` لم يتغيّر لأنه لا يبني `PortalService` بمراسلات (كان
  `PortalController` وحده يستهلك الخدمة).
- عدّ تسجيلات `DI` مقابل أصناف المعالجات: **156/156/0** بلا تكرار ولا صنف غير مسجَّل.
- فحص تكرار المساعدات: تعريف واحد لكل مساعد منقول أو مرقّى (بما فيها
  `NormalizeImportanceFilter` و`TargetsLimit` بعد نقلهما).
- مطابقة سطرية/نصية للدوال المنقولة (اتجاهان) وللمساعدات، وتتبّع النصوص الحرفية.
- فحص بقايا التسميات القديمة: لا ذكر لـ`ListByDocumentAsync`/`CountUnseenAsync`/
  `GetGovernoratesAsync`/`SearchTargetsAsync` على خدمة المراسلات في `src` أو الاختبارات.
- فحص `generate:api` بعد التوليد (`git diff --exit-code`) واستعادة ضجيج `CRLF`.
- فحص `has-pending-model-changes` بهوية اتصال وهمية و`--no-build`.

## الملاحظات المكتشفة والإصلاح

| # | الملاحظة | الدليل | الإصلاح |
|---|---|---|---|
| 1 | **اسم ملف مضلِّل**: `CorrespondenceService.Queries.cs` بقي بعد نقل كل القراءات ولم يبقَ فيه سوى الإسقاطات | لا `SearchAsync`/`GetByIdAsync` فيه بعد النقل | أُعيد تسميته `CorrespondenceService.Mapping.cs` (`git mv`) مع بقاء المحتوى حرفيًا |
| 2 | **`dotnet format` فشل** على `PortalController.CorrespondenceUnseenCount` بعد تحويلها إلى تعبير متعدد الأسطر | خطأ `WHITESPACE` عند السطرين 193/194 | حُوّلت إلى جسم كتلة صريح (`var count` ثم `return Ok`) |
| 3 | **BOM في ملفَي اختبار** بعد الاستبدال النصي الجماعي (`Set-Content -Encoding UTF8`) | خطأ `CHARSET` في `CorrespondenceServiceTests`/`SubHeadReviewCorrespondenceTests` | أُعيدت كتابتهما بترميز UTF-8 بلا BOM |
| 4 | **ضجيج `CRLF`** في `frontend/src/api/generated.ts` بعد `npm run generate:api` على Windows | `git status` يظهر الملف معدّلًا بلا فرق دلالي | أُعيد بـ`git restore`؛ `git diff --exit-code` = 0 |
| 5 | **`secrets-guard` توقف** على مسار محذوف بعد `git mv` لأن الفهرس كان نصف مرحَّلًا (add جديد + delete غير مرحَّل) | `ENOENT ... CorrespondenceService.Queries.cs` | `git add -A` ليعرف الفهرس إعادة التسمية؛ الحارس نظيف بعدها |
| 6 | احتمال أن يكون `_documents` حقلًا ميتًا بعد نقل القراءات | `_documents` يُستخدم في تفويض `ICorrespondenceWriteRules.CanViewAsync` المشترك مع مسار تأكيد المشاهدة | لا — الحقل حيّ؛ لم يُحذف أي حقل (12/12) |
| 7 | احتمال بقاء نسخة ثانية من حرّاس الوصول في المعالجات | مقارنة الأجسام المنقولة | لا — الحرّاس المرقّاة مصدر واحد، والمعالجات تستدعيها بتأهيل `CorrespondenceService.` |
| 8 | احتمال تغيّر شكل `unseen-count` أو `filter-options` | مقارنة أجسام الإجراءات | لا — نفس `{ count }` و`{ governorates }` ونفس النظام |
| 9 | احتمال أن `FileCorrespondence` في البوابة صار يفصح عن «غير مصرَّح» | مقارنة كتل `catch` | لا — `KeyNotFoundException` و`UnauthorizedAccessException` كلاهما `NotFound()` |
| 10 | احتمال انزياح السنة في `FileContextOf` بعد تمرير الساعة صراحةً | بناء المعالجات في الاختبارين | لا — `TestClock.Provider`/`TestClock.TimeZone` نفساهما |
| 11 | فرق عدّ التخطي بين تشغيل كامل وتشغيل CI المرحلي (620 مقابل 615) | `--filter Category!=Sequential` = 901/615، والـ`Sequential` = 0/5 | لا خلل: التشغيل الكامل يضم خمسة اختبارات تسلسلية متخطاة محليًا؛ أرقام CI المرحلية هي المرجع |

## إثبات التكافؤ بعد الإصلاح

- **الأجسام المنقولة:** **119/132** سطرًا فعّالًا (13 سطرًا ميكانيكيًا موثّقًا)؛
  **المساعدات المرقّاة:** **137/137**؛ الإجمالي **256/269** وفرق الرموز الدلالي **0**.
- **النصوص الحرفية (اتجاهان):** 32 نصًا / 24 مميزًا؛ `missing = 0` و`added = 0`.
- **المتحكمات:** نفس المسارات و`[Authorize]` وحرّاس الأدوار/النطاق ونفس
  `Ok(...)`/`Forbid()`/`ApiProblem`/`NotFound()` وأشكال الأجسام في
  `CorrespondencesController` و`PortalController`.
- **البوابة:** نفس دلالة `null`/خارج النطاق (`404` مخفي) ونفس `PagedResult` للقائمة.
- **الاختبارات:** **46** موضع نداء حُوِّل إلى المعالجات عبر جسر واحد؛ لا حذف ولا
  تعديل توقّع واحد. لا مزيّف قديم، ولا مصنع يحتاج حقلًا جديدًا.
- **الخدمة:** 12 حقلًا كلها مستخدمة؛ 0 partial فارغ؛ 0 مستدعٍ قديم؛
  `ICorrespondenceWriteRules` بلا أي تغيير (10 أعضاء).
- **`DI`:** 156 معالجًا مسجَّلًا مقابل 156 صنفًا (بلا تكرار).

## متبقٍ بعد هذه الدفعة (صريح)

- **اكتملت قراءات المراسلات:** لا يبقى أي سطح قراءة على خدمة المراسلات، وبقيت
  المساعدات المشتركة التي تُستدعى داخل معاملات الكتابة (خارج نطاق هذه الدفعة عمدًا)
  وعقد مساعدات الكتابة `ICorrespondenceWriteRules` كما هو.
- **متبقٍ في `P4-01`:** **مسح module-by-module** لبقية الوحدات (البوابة/الإحصاءات)
  للتأكد من أن كل `use-case` له معالج واحد بملكية واضحة، بلا كسر عقود خارجية — بند
  متابعة معماري لا يغيّر سلوكًا.
- **لا هجرة جديدة**؛ المعلّق يبقى **6**.

## التحقق

`build -warnaserror` 0/0 · `format --verify-no-changes` نظيف · Architecture 29/29 ·
Application 758/1659/0 · Api 901/615/0 (والـ`Sequential` منفصلًا: 0/5/0 تخطٍّ محلي) ·
frontend 1809/1809 + `oxlint`/`tsc -b`/`build`/`budget`/`guard`/`generate:api` بلا فرق ·
الحرّاس الستة نظيفة · `dependency-audit` 9 مشاريع · `npm-audit-guard` 0 ثغرة ·
`has-pending-model-changes` نظيف · `DI` 156/156/0 · 0 حقل ميت · 0 partial فارغ.

> **ملاحظة صدق:** اختبارات `PostgreSQL` تُخطّى محليًا (`DOCGEN_TEST_POSTGRES` غير مضبوط)؛
> الحكم النهائي للسلوك على القاعدة هو `backend-postgres` في `CI`.
