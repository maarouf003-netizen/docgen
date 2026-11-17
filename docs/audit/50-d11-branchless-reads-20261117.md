# `D11` — توحيد «رئيس بلا فرع»: قراءة/عدّ `403` وكتابة `400` — 2026-11-17

> قرار المالك (2026-11-17): **الخيار (أ)** — توحيد أسطح القراءة والعدّ على `403`
> بدل `400`، مع إبقاء الكتابة `400`. لا هجرات، ولا تغيير في منطق الصلاحيات نفسه —
> تغيير في **حالة HTTP** المتوقعة للحالة الشاذة نفسها.

## 1. المشكلة

`D11` يقرر: «رئيس بلا فرع» **يكتب** `400` (طلبه ناقص: لا فرع) و**يقرأ/يعدّ** `403`
(لا نطاق رؤية = لا صلاحية)، و`MarkRead` مستثنى عمدًا. لكن عدة أسطح قراءة بقيت `400`:

| السطح | قبل | بعد |
|---|---|---|
| `GET /api/delegations/pending` | `400` من `RequireApproverScope` | **`403`** |
| `GET /api/delegations/pending-count` | `400` من نفس الحارس | **`403`** |
| `GET /api/review-letters/pending-count` | `400` | **`403`** |
| `GET /api/appeals` (بحث/قائمة) | `400` | **`403`** |
| `GET /api/dashboard` · `monthly-stats` · `available-periods` | `400` من `RequireOwnBranch` | **`403`** |

## 2. ما بقي `400` عمدًا

- **كل كتابة** تحتاج فرعًا: `POST /api/alerts` (من الخدمة)، `POST /api/correspondences`،
  `POST /api/review-letters` والرد عليها، ونقل الاستئنافات (`transfer`/`transfer-all`).
- **`POST /api/auth/login`** عند `LoginStatus.BranchRequired`: سياق مصادقة لا قراءة مورد.
- **`MarkRead`** لتنبيهات الرئيس: يخصّ صف استلام شخصيًا لا نطاقًا فرعيًا (استثناء `D11`
  القائم، غير ممسوس).

## 3. التنفيذ

- `DelegationsController.RequireApproverScope`: `403` + تعليق يربط القرار؛ الحارس يخدم
  القائمة والعدّاد معًا فتوحّدا.
- `ReviewLettersController.PendingCount`: `403`.
- `AppealsController.Search`: `403` (نقل/كتابة الاستئنافات بقيت `400`).
- `StatisticsController.RequireOwnBranch`: `403` (dashboard/monthly/available-periods)؛
  الرسالة العربية نفسها في الجسم، فالواجهة لا تتغير (تقرأ `message` قبل رمز الحالة).
- رمز العقد الموحّد في الحالات الخمس: `code = "branch_required"` مع `message` العربي
  و`status = 403` (`ApiProblem`، PR `#191`).

## 4. الاختبارات

- `DelegationPendingCountBranchGuardTests.PendingCount_HeadWithoutBranch_Forbidden`:
  كان `400` → صار `403` مع التحقق من `code`/`message`.
- `StatisticsBranchGuardTests`: الثلاثة (`Dashboard`/`MonthlyStats`/`AvailablePeriods`)
  صارت `*_Forbidden` وتتحقق من `403` + `branch_required` + نص الرسالة.
- **جديد:** `D11BranchlessReadContractTests` — جلسة رئيس بلا فرع مسكوكة من كيان حقيقي
  (قيد القاعدة يمنع التجسيد عبر EF)، وتغطي **خمسة أسطح قراءة** عبر HTTP: `403` +
  `application/problem+json` + `status/code/message`. (الكتابة `400` تبقى مغطاة في
  `AlertsIntegrationTests.BranchlessHead_CreateIs400_AndReadsAre403`.)
- تعليقات الاختبارات/التوثيق التي كانت تصف العدّاد `400` حُدّثت.

## 5. التحقق

- `dotnet build … -warnaserror`: 0/0.
- `dotnet format … --verify-no-changes`: `exit 0`.
- `DocGenerator.Api.Tests` (`Category!=Sequential`): **1484/1484** (+5 اختبار تكامل جديد).
- `DocGenerator.Api.Tests` (`Category=Sequential`): **5/5**.
- `DocGenerator.Application.Tests`: **2486/2486** (بلا تغيير).
- `DocGenerator.Architecture.Tests`: **29/29**.
- الحلقة السريعة: `Application` 707، و`Api` 804 (بلا تغيير — اختبارات D11 تكاملية).
- الحراس الخمسة والواجهة (`oxlint`/`tsc`/`vitest` 1808/`build`/`guard`): بلا تغيير ونظيفة.

## 6. الأثر والحدود

- **أثر على العميل:** لا تغيير في النص المعروض (الواجهة تفضّل `message`)، لكن أي
  مستهلك يعتمد `400` لهذه الأسطح سيرى `403` — وهو التغيير المقصود.
- **لا يمسّ:** قيد القاعدة `CK_Users_BranchRequiredForBranchRoles` (الحالة تبقى مستحيلة
  الإنشاء عبر التطبيق؛ هذه حراسة دفاعية لبيانات شاذة/قديمة).
- **لا هجرات.**
