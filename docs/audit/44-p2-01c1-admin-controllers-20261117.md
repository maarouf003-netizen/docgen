# P2-01c‑1 — ترحيل متحكمات الإدارة/القراءة إلى جسر القدرات — 2026-11-17

> نطاق هذه الجولة: الدفعة الأولى من `P2-01c` — **7 متحكمات** بلا نطاق موردي معقّد
> (إحصاءات، تدقيق، فروع، شعب، تذكيرات شخصية، اقتراحات تطوير، منتدى). أُزيلت كل
> قرارات الأدوار المباشرة، وانخفضت قائمة انتظار الحارس من **13 إلى 6**. لا هجرات.

## 1. ما نُفّذ

| المتحكم | قبل | بعد |
|---|---|---|
| `StatisticsController` | 6 نداء `RolePermissions` + 7 مقارنات دور | خصائص قدرات (`HasFullAccess`, `IsHeadOrSubHead`, `IsHead`, `IsSubHead`, `IsLawyer`) عبر الجسر |
| `AuditLogsController` | 3 مقارنات (`Head`/`SubHead`) | `IsHead`/`IsSubHead` |
| `ForumController` | `Role == SubHead`، `Role != Admin` | `IsSubHead`، `IsAdmin` |
| `AppSuggestionsController` | `CanViewAppSuggestions`، `CanSuggestApp` | نفس القدرتين عبر الجسر |
| `BranchesController` / `SectionsController` | `CanManageBranches` | عبر الجسر |
| `PersonalRemindersController` | `CanManagePersonalReminders` | عبر الجسر |

**إضافات الكتالوج:** `RolePermissions.IsHead/IsSubHead/IsAdmin` — **هويات دور**
تُستخدم لاشتقاق النطاق لا لمنح قدرة، أُضيفت كي لا تحمل المتحكمات مقارنة دور.
أُضيفت مقابلها أربع قدرات فعلية إلى الجسر (`CanManageBranches`,
`CanViewAppSuggestions`, `CanSuggestApp`, `CanManagePersonalReminders`).

**مصفوفة `P2-02g`:** 41 صفًا × 6 أدوار = **246 خلية**، وجسر القدرات **25 × 6 = 150 خلية**.
ثابت «رئيس الشعبة = رئيس القسم» استُثني منه **هويات الدور** (`IsHead`/`IsSubHead`)
لأنها تصف الدور نفسه؛ كل صفوف القدرات تبقى مشمولة.

## 2. حراسة وتحقق سلبي

- حارس الترحيل يفحص الآن **10 متحكمات مُرحَّلة**، وقائمة الانتظار **6** فقط
  (`Appeals`, `Correspondences`, `Delegations`, `ExecutionCircuits`, `ReviewLetters`,
  `UserManagement`).
- خُفِّض حدّ منع الخضرة الكاذبة من 50 إلى **15 سطر كود** لأن أصغر متحكم مُرحَّل
  (`AuditLogs` ≈ 40 سطرًا) كان يفشل الحارس لسبب غير أمني.
- **تحقق سلبي:** إضافة `RolePermissions.HasFullAccess(User.GetRoleEnum())` إلى
  `StatisticsController` أفشلت **حارسين معًا** (حارس المتحكمات المُرحَّلة + حارس الجرد)،
  ثم أُعيد فورًا وعادت 23/23 خضراء.

## 3. تكافؤ السلوك

- كل السمات `[Authorize(Roles=...)]` بقيت كما هي؛ لم يُنقل أي قرار إلى الواجهة.
- نطاق الإحصاء والتدقيق و«رئيس بلا فرع» بقي حرفيًا (بما فيه `400` الموثّق لفرع
  الحساب في الإحصاءات — لم يُغيَّر في هذه الجولة).
- كل الاختبارات القائمة خضراء بلا تعديل توقعات، عدا تمرير `UserCapabilities` في
  اختباري الحارس (`AuditLogsBranchGuardTests`, `StatisticsBranchGuardTests`).

## 4. التحقق الكامل

- `dotnet build backend/DocGenerator.sln --no-restore -warnaserror` ✅.
- `DocGenerator.Application.Tests`: **2486/2486** ✅.
- `DocGenerator.Api.Tests` (`Category!=Sequential`): **1361/1361** ✅ (كانت 1301؛
  +60 خلية مصفوفة/جسر جديدة).
- `DocGenerator.Api.Tests` (`Category=Sequential`): **5/5** ✅.
- `DocGenerator.Architecture.Tests`: **23/23** ✅ (11 + 10 مُرحَّلة + الجرد + التحكم).
- الحلقة السريعة (`Category=Unit`): **707 + 693**.
- حراس `CI` الخمسة: ✅ (`subhead` نظيف).
- الواجهة بلا تغيير: `oxlint` · `tsc` · `vitest` 1804 · `build` · `guard` ✅.

## 5. الحدود

- **متبقٍ `P2-01c`:** 6 متحكمات (الأعمال: استئنافات، مراسلات، إنابة، دوائر تنفيذ،
  مطالعات، مستخدمون). إفراغ قائمة الحارس = إغلاق البند.
- مصائد النطاق داخل الخدمات (اشتقاق الدور) لا تُمسّ: مكانها الصحيح هناك، والمتحكم
  يمرّر الدور كبيان فقط عند الحاجة (`EntityRegistryActor` وما شابه).
- توحيد دلالة «رئيس بلا فرع» (`D11`) ما زال بند قرار مالك كما وثّق `P4-03`.
