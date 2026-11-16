# P4-03 (جزء الدلالة) — مسح `400↔403/404` في المطالعات والمراسلات والتنفيذ — 2026-11-16

> نطاق هذه الجولة: الجزء الأول من `P4-03` — **توحيد دلالة أخطاء الصلاحية/الوجود/التحقق**
> في ثلاثة نطاقات (مراجعات، مراسلات، دوائر تنفيذ) قبل ترحيلها إلى سياسات `P2-01c`.
> لا تغيير في عقود الـAPI عدا تصحيح حالتين موثّقتين أدناه. لا هجرات ولا مخطط.

## 1. القاعدة المثبتة

| الحالة | الرمز | المصدر |
|---|---|---|
| لا مصادقة | `401` | وسيط المصادقة |
| فاعل معروف بلا دور/نطاق/ملكية | `403` | `UnauthorizedAccessException` أو `Forbid()` |
| المورد غير موجود | `404` | `KeyNotFoundException` (أو إخفاء متعمد للوجود في البوابة) |
| الطلب نفسه غير صالح (حقل/قيمة) | `400` | `ArgumentException` |
| تعارض | `409` | `DocumentConflictException`/تفرد |

`D11` (قرار مالك) يخصّ «رئيس بلا فرع»: **الكتابة `400`** (حساب ناقص فرعًا = خطأ إدخال)
و**القراءة/العدّ `403`**. لم يُغيَّر أي مسار «رئيس بلا فرع» في هذه الجولة (§5).

## 2. ما كشفه المسح

| الموضع | الخلل | التصنيف |
|---|---|---|
| `ReviewLettersController` (5 أفعال) | `catch (ArgumentException) ⇒ NotFound` يحوّل **فشل تحقق** (نص لاحق/رد فارغ) إلى `404`، ويعتمد على استثناء عام للمفقود | خلط `400/404` |
| `ReviewLetterService` (4 مواضع) | كتاب مطالعة مفقود = `ArgumentException` بدل `KeyNotFoundException` | دلالة مورد |
| `ReviewLetterService` (3 مصائد دور) | رئيس بلا فرع/شعبة أو دور غير مخوّل = `ArgumentException` ⇒ `400` | صلاحية كـ`400` |
| `CorrespondenceService` (3 مصائد دور) | نفس النمط في بحث المراسلات | صلاحية كـ`400` |
| `ExecutionCircuitService.ResolveOwnerSection` (3 مواضع) | غير رئيس/بلا شعبة/شعبة خارج الفرع = `ArgumentException` | صلاحية كـ`400` |

## 3. الإصلاح

- **`ReviewLetterService`**: استُبدلت 5 `ArgumentException` بـ`KeyNotFoundException`
  (المفقود)، و3 بـ`UnauthorizedAccessException` (الدور/النطاق). أُبقيت استثناءات
  التحقق (`نص اللاحق/الرد مطلوب`) `ArgumentException`.
- **`ReviewLettersController`**: حُذفت الكتل الخمس `catch (ArgumentException) ⇒ NotFound`
  بالكامل — المفقود يمرّ إلى المعالج العام (`404`)، والتحقق يعود `400`، والصلاحية `403`.
- **`CorrespondenceService`**: مصائد الدور/النطاق الثلاث ⇒ `UnauthorizedAccessException`.
- **`ExecutionCircuitService`**: لا رئيس/بلا شعبة/شعبة خارج الفرع ⇒ `UnauthorizedAccessException`
  (المتحكم كان يمنع بـ`403` مسبقًا، فالتحويل يقوّي الدفاع العميق لا يغيّر عقد API).
- **حارس `ApiErrorMessagesTests`**: كان يثبّت **تسع** كتل `catch (ArgumentException)`
  مسموحة؛ أُعيد ضبطه إلى **أربع** (Appeals×3، Delegations×1) مع توثيق سبب الإزالة.
  الحارس يبقى يفشل على أي زيادة/نقص غير مقصود.

**التغيير الظاهري الوحيد:** إرسال لاحق/رد بنص فارغ كان يعيد `404` صار `400`
(وهو الصحيح: المورد موجود والطلب ناقص). كل حالات المفقود/الصلاحية بقيت بنفس الرمز.

## 4. التحقق والاختبارات

- **`ReviewCorrespondenceScopeTests`** (+2): مصفوفة دلالة للمطالعات —
  `GET`/لاحق/رد لمعرّف مفقود = `404` بجسم صالح؛ لاحق/رد فارغ على كتاب موجود = `400`؛
  والمراسلة المفقودة = `404`.
- **`ReviewLetterServiceTests`** (+2): رئيس بلا فرع ⇒ `Unauthorized`، وكتاب مفقود ⇒ `KeyNotFound`.
- **`CorrespondenceServiceTests`** (+1): رئيس بلا فرع ⇒ `Unauthorized`.
- حُدّثت توقعات `SubHeadReviewCorrespondenceTests` (شعبة بلا شعبة ⇒ `Unauthorized`)
  و`ExecutionCircuitServiceTests` (غير رئيس/بلا شعبة ⇒ `Unauthorized`).
- **تحقق سلبي موثّق:** إعادة `catch (ArgumentException) ⇒ NotFound` إلى `AddAddendum`
  أفشلت اختبار الدلالة (`Letter_ErrorSemantics…`) بمقارنة `400` المتوقعة بـ`404`؛
  ثم أُعيد الإصلاح وعاد أخضر.

## 5. حدود هذه الجولة (لا تُخفى)

- **«رئيس بلا فرع» لم يُوحَّد**: بقيت كتابة المطالعات/المراسلات ونقل الاستئنافات `400`
  (وفق `D11`)، وعدّاد طلبات الإنابة ما زال `400` ومثبّت باختبار صريح
  (`DelegationPendingCountBranchGuardTests`) رغم أن `D11` يرى العدّادات `403`.
  يُعرض على المالك كبند مستقل (توحيد `D11` عبر النطاقات) قبل تغييره.
- **بقيت 4 كتل `catch (ArgumentException) ⇒ NotFound`** في `AppealsController`×3
  و`DelegationsController`×1 — خدماتها ما زالت ترمي `ArgumentException` للمفقود؛
  حارس `ApiErrorMessagesTests` يثبتها كقائمة مسموحة ويُفشل أي تغيير غير مقصود.
- **لم يُنفَّذ** باقي `P4-03` (FluentValidation/`IValidator<T>` موحّد و`ProblemDetails`
  بشكل `code/field/message` وربط الواجهة) — بند لاحق.
- مصائد النطاق في `DocumentDelegationService`/`DocumentAppealService`/`HeadAlertService`
  (8 مواضع، `ArgumentException`) لم تُمسّ: `HeadAlertService` يخضع لـ`D11` صراحةً
  (كتابة `400`)، والبقية مرشحة لجولة مسح تالية مع قرار `D11` الموحّد.

## 6. التحقق الكامل

- `dotnet build backend/DocGenerator.sln --no-restore -warnaserror` ✅.
- `DocGenerator.Application.Tests`: **2486/2486** ✅.
- `DocGenerator.Api.Tests` (`Category!=Sequential`): **1301/1301** ✅.
- `DocGenerator.Api.Tests` (`Category=Sequential`): **5/5** ✅.
- `DocGenerator.Architecture.Tests`: **16/16** ✅ (بلا تغيير).
- حراس `CI` الخمسة: `subhead` · `determinism` (0) · `migration-watchlist` (5) ·
  `migration-safety` (لا هدم) · `secrets` (20) ✅.
- الواجهة بلا تغيير: `oxlint` · `tsc` · `vitest` 1804 · `build` · `guard` ✅.
