# P2-01c‑2 — إغلاق ترحيل المتحكمات وإفراغ قائمة الحارس — 2026-11-17

> نطاق هذه الجولة: الدفعة الأخيرة من `P2-01c` — **6 متحكمات** (استئنافات، مراسلات،
> إنابة، دوائر تنفيذ، مطالعات، مستخدمون). أُزيلت كل قرارات الأدوار المباشرة، وأُفرغت
> **قائمة انتظار الحارس**، فلا يبقى في أي متحكم `RolePermissions` ولا مقارنة دور.
> لا هجرات ولا تغيير في العقود أو السمات.

## 1. ما نُفّذ

| المتحكم | ما أُزيل |
|---|---|
| `AppealsController` | 6 خصائص/مقارنات دور + 6 مقارنات `SubHead` في اشتقاق النطاق |
| `CorrespondencesController` | 7 نداءات قدرة + 9 مقارنات (`SubHead`/`EntityManager`/`Lawyer or Head`) |
| `DelegationsController` | 4 خصائص قدرات + مقارنات `CanAccess`/`RequireApproverScope` |
| `ExecutionCircuitsController` | قدرة الإدارة + `SubHead`/`HeadOrSubHead` + `HasFullAccess` (وحُذفت خاصية `Role` الميتة في المتحكم الثاني) |
| `ReviewLettersController` | 5 نداءات قدرة + 3 مقارنات `SubHead` |
| `UserManagementController` | 3 خصائص قدرات |

**الكتالوج:** أُضيفت هوية `RolePermissions.IsEntityManager` (42 مسندًا).
**الجسر:** 10 قدرات جديدة → **35 × 6 = 210 خلية**، والمصفوفة **42 × 6 = 252 خلية**.
ثابت «المندوب لا يملك إلا سطح البوابة» استُثني منه `IsEntityManager` (هوية دور).

## 2. الحارس الراتشة — إغلاق

- المتحكمات المُرحَّلة: **16**؛ **قائمة الانتظار فارغة**.
- معنى ذلك: أي متحكم — قائم أو جديد — يحمل `RolePermissions` أو `Role ==/!=/is UserRole`
  يفشل فورًا، وأي إدخال في القائمة يفشل كـ«قديم» (لا قائمة متضخمة).
- **تحقق سلبي:** إضافة `RolePermissions.CanManageUsers(...)` إلى
  `UserManagementController` أفشلت **حارسين معًا** (حارس المتحكم + الجرد الفارغ)،
  ثم أُعيد وعادت 29/29 خضراء.
- `GetRoleEnum()` يبقى مسموحًا في المتحكمات التي تمرّر الدور **كبيان** إلى الخدمة
  (`ReviewLetters`, `Correspondences`, `EntityRegistry`)؛ المستندات وحدها تمنعه كليًا.

## 3. تكافؤ السلوك

- كل السمات `[Authorize(Roles=...)]` و`[Authorize(Policy="HeadOrSubHead")]` بقيت كما هي.
- اشتقاق النطاق في `Delegations`/`Appeals` و`ExecutionCircuits` نُقل حرفيًا إلى
  خصائص قدرات؛ الحالات `400` الموثّقة («رئيس بلا فرع») لم تُغيَّر.
- كل الاختبارات القائمة خضراء بلا تعديل توقعات، عدا تمرير `UserCapabilities` في
  اختباري حارس (`DelegationPendingCountBranchGuardTests`, `ExecutionCircuitsBranchGuardTests`).

## 4. التحقق الكامل

- `dotnet build backend/DocGenerator.sln --no-restore -warnaserror` ✅.
- `DocGenerator.Application.Tests`: **2486/2486** ✅.
- `DocGenerator.Api.Tests` (`Category!=Sequential`): **1433/1433** ✅.
- `DocGenerator.Api.Tests` (`Category=Sequential`): **5/5** ✅.
- `DocGenerator.Architecture.Tests`: **29/29** ✅ (16 مُرحَّلة + الجرد الفارغ + التحكم + 11 قاعدة).
- الحلقة السريعة (`Category=Unit`): **707 + 765**.
- حراس `CI` الخمسة: ✅ (`subhead` نظيف).
- الواجهة بلا تغيير: `oxlint` · `tsc` · `vitest` 1804 · `build` · `guard` ✅.

## 5. حدود (بدقة)

- **`P2-01` مغلق بمعنى «لا منطق صلاحية في المتحكمات»**: لا `RolePermissions` ولا مقارنة
  دور في أي متحكم. جسر القدرات خدمة تطبيقية (`IUserCapabilities` ← الكتالوج)، وليس
  `IAuthorizationService` لكل قدرة — قرار موثّق في `P2-01b` (متطلبات موردية حقيقية
  للمستندات، وجسر للقدرات) ولا يضيف تحويل كل قدرة إلى سياسة أي ضمان أمني.
- **متطلبا البوابة/نطاق الجهات مؤجلان** بقرار `ADR-0007` §7 (النطاق بنيوي/خدمي ومختبَر).
- اشتقاق النطاق داخل الخدمات (دور/فرع/شعبة) باقٍ مكانه الصحيح؛ المتحكم يمرّر بيانات
  فقط. توحيد «رئيس بلا فرع» (`D11`) ما زال بند قرار مالك.
