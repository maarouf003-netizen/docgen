# P2-01b — جسر القدرات للبوابة وسجل الجهات + حارس راتشة الترحيل — 2026-11-16

> PR: [#184](https://github.com/maarouf003-netizen/docgen/pull/184)
>
> نطاق هذه الجولة: المرحلة `b` من `P2-01` بعد `P2-01a`. أُزيلت كل قرارات الأدوار
> المباشرة من `EntityRegistryController` و`DelegatesController`، وأُضيف حارس يمنع أي
> متحكم جديد من حمل قرار دورّي بصمت ويُلزم بترحيل البقية. **لا هجرات ولا تغيير عقود.**

## 1. لماذا لم يُنشأ `PortalAccessRequirement`/`EntityScopeRequirement`

`ADR-0007` §7 (ملحق اليوم) يوثّق القرار: البوابة عزلُها بنيوي (سمة الصنف + حارس
`EntityManagerPortalGuard` + كل استعلام مقيّد بـ`userId` في `IPortalService`، ودلالة
404 لإخفاء الوجود مثبتة في `P2-02c`)، ونطاق محافظة رئيس القسم مفروض داخل
`PublicEntityService` عبر `EntityRegistryActor` (fail-closed) ومغطى باختبارات.

إضافة متطلبين الآن = تكرار قاعدة موجودة في مكان آخر بلا تقليص سطح فشل مفتوح. لذلك
اقتصرت المرحلة على **إزالة منطق الأدوار من المتحكمين** (الغاية الحقيقية) + الحارس
الراتشيت، وتأجّل المتطلبان إلى أن تُنقل قواعد النطاق من الخدمة (بند مستقل).

## 2. ما نُفّذ

- **جسر القدرات عمّم**: نُقل `UserCapability`/`UserCapabilities` من
  `Authorization/Documents` إلى `Authorization` (همّ عام)، وأُضيفت أربع قدرات:
  `IsLawyer`، `CanManageEntityRegistry`، `CanMergeEntities`، `CanManageDelegates`.
- **`EntityRegistryController` (30 نقطة/37 موضع قرار)**: كل
  `RolePermissions.CanManageEntityRegistry/HasFullAccess/CanMergeEntities(Role)` ←
  `_capabilities.Has(User, …)`؛ `Role != Lawyer` ← `IsLawyer`؛
  `Role != Head && Role != SubHead` ← `IsHeadOrSubHead`؛ وفحصا `Role == EntityManager`
  الزائدان أُزيلا (السمة الصفية + حارس البوابة يمنعان المندوب بنيويًا).
  بقي `UserRole Role => User.GetRoleEnum()` **لتمرير الدور كبيان** إلى
  `EntityRegistryActor` (عقد الخدمة)، لا لقرار.
- **`DelegatesController`** (ثلاث نقاط في `PortalController.cs`): `CanManageDelegates`
  عبر الجسر.
- **حارس راتشة** (`AuthorizationMigrationGuardTests`، يحل محل حارس المستندات المفرد):
  - (أ) المتحكمات المُرحَّلة (المستندات/السجل/البوابة) بلا `RolePermissions` ولا مقارنة
    دور (والمستندات بلا `GetRoleEnum`)، مع منع خضرة كاذبة (ملف > 50 سطرًا).
  - (ب) **قائمة انتظار** بالملفات الـ13 المتبقية ومرحلتها (`P2-01c`)؛ أي ملف جديد بقرار
    دورّي يفشل فورًا، وأي ملف أُفرغ ولم تُحذف قائمته يفشل (لا قائمة متضخمة).
  - (ج) تحكم إيجابي يثبت أن المنقّح يرى المخالفة في الكود لا التعليق.

## 3. تحقق سلبي موثّق (حقن ثم إعادة)

| الحقن المؤقت | النتيجة المرصودة |
|---|---|
| `RolePermissions.HasFullAccess` في `MetaController` (خارج القائمة) | فشل حارس الجرد: «بقرار دورّي خارج القائمة: MetaController.cs» |
| إزالة القرار الوحيد من `PersonalRemindersController` (داخل القائمة) | فشل حارس الجرد: «لم يبقَ فيها قرار دورّي — احذفها» |
| `Role == UserRole.Head` في `PortalController` (مُرحَّل) | فشل حارس المتحكمات المُرحَّلة على `PortalController.cs` |

بعد كل إعادة: البناء والحراس 16/16 خضراء.

## 4. التحقق الكامل

- `dotnet build backend/DocGenerator.sln --no-restore -warnaserror` ✅.
- `DocGenerator.Application.Tests`: **2483/2483** ✅.
- `DocGenerator.Api.Tests` (`Category!=Sequential`): **1299/1299** ✅.
- `DocGenerator.Api.Tests` (`Category=Sequential`): **5/5** ✅.
- `DocGenerator.Architecture.Tests`: **16/16** ✅ (11 + 3 ملفات مُرحَّلة + الجرد + التحكم).
- الحلقة السريعة (`Category=Unit`): **707 + 633** (18 قدرة × 6 أدوار = 108 خلية جسر).
- حراس `CI` الخمسة: `subhead` · `determinism` (0) · `migration-watchlist` (5) ·
  `migration-safety` (لا هدم) · `secrets` (20) ✅.
- الواجهة بلا تغيير: `oxlint` · `tsc` · `vitest` 1804 · `build` · `guard` ✅.

## 5. الحدود

- **`P2-01c`**: 13 متحكمًا ما زالت تحمل قرارات أدوار (القائمة في الحارس) — الحارس
  يمنع زيادتها ويجبر تقليصها. إفراغ القائمة = إغلاق البند.
- **`PortalAccessRequirement`/`EntityScopeRequirement` مؤجلان** بقرار موثّق (§1).
- `P4-03` (دلالة `400↔403/404` في المراجعات/المراسلات/التنفيذ) ما زال مستحسنًا **قبل**
  `P2-01c` كي لا يُنقل سلوك خاطئ إلى السياسات.
