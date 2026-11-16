# P2-02g — المصفوفة الذرّية لكتالوج الصلاحيات — 2026-11-16

> نطاق هذه الجولة: إغلاق الشرط الأخير في `DoD` البند `P2-02` — **تغطية 100% لمصفوفة
> `RolePermissions`** باختبار وحدوي واحد لكل (`قدرة × دور`). لا تغيير سلوكي في التشغيل؛
> إزالة تكرار واحد مطابق لكتالوج مركزي موجود.

## 1. المشكلة التي تُغلق

مصفوفات `P2-02` السابقة (`#172`، `#177`–`#180`) ثبّتت **نقاط الموارد** (مستندات/بوابة/
إنابة/استئناف) عبر طلبات HTTP حقيقية، لكن **الكتالوج المركزي** — 38 قدرة في
`Application/Common/RolePermissions.cs` — كان مغطّى جزئيًا فقط: `SubHeadRolePermissionsTests`
يغطي 15 قدرة وراثية + 5 سلبية، و`EntityRegistryPermissionsTests` خمس قدرات، وبقية
القدرات بلا تثبيت ذرّي. أي تعديل في سطر `role is …` كان يمكن أن يمرّ بلا كشف.

## 2. مصدر الحقيقة

الجدول مكتوب يدويًا من **قرارات المالك** لا من الكود:

- `SUBHEAD_PLAN.md` §2.1/§2.21 (رئيس الشعبة = رئيس القسم، والمجموعة السلبية الخمس).
- `BQ-001`/`BQ-001د` (رؤية المدير للمحذوفات والاقتراحات، وإدارة المستخدمين عدا المشرف).
- `BQ-002`/`BQ-005` (رئيس القسم يدير محامي فرعه ومندوبي محافظته، والمندوب جهته فقط).
- §2.15 (إدارة الفروع والشعب للمدير والمشرف معًا).
- د3/د5/د11 (سجل الجهات والدمج ومندوبو الجهات).

ملاحظة مقصودة: كتابة التوقعات من الكود ثم تثبيتها تُنتج اختبارًا **طوطولوجيًا** يحمي
الانحدار فقط. الجدول هنا ذرّي في الاتجاه المعاكس أيضًا: تغيير الكتالوج بلا تحديث الصفّ
يفشل، وتحديث الصفّ بلا قرار مالك مرفوض في المراجعة.

## 3. كيف ثُبِّت — `RolePermissionsMatrixTests`

`backend/tests/DocGenerator.Api.Tests/RolePermissionsMatrixTests.cs`،
`[Trait("Category", "Unit")]`، خمسة أجزاء:

| الحارس | ماذا يمنع |
|---|---|
| `Capability_MatchesDecidedMatrix` (228 خلية عبر `TheoryData`) | كل قدرة × دور مع توقع صريح؛ فشل خلية واحدة يسمّي القدرة والدور والقيمتين. |
| `Catalog_CoversEveryCapabilityExactlyOnce` | قدرة جديدة في الكتالوج بلا صفٍّ (تغطية ناقصة بصمت)، أو صفّ بلا قدرة (بقايا حذف)، أو صفّ مكرر. |
| `Catalog_HasSingleUniformShape` | توقيع غير `public static bool M(UserRole)`؛ أي قدرة موردية (تحتاج فاعلًا/موردًا) تُفشل الحارس بدل أن تُتجاهل. |
| `DeclaredRoles_AreTheSixDecidedRoles` | دور سابع يدخل `UserRole` بقيمة افتراضية `false` بلا قرار مالك. |
| `SubHead_HoldsExactlyTheHeadCapabilitySet` / `EntityManager_HoldsOnlyThePortalSurface` | ثابتان قرارِيان: رئيس الشعبة = رئيس القسم في كل صف، والمندوب لا يملك إلا سطح البوابة (`CanUseDelegatePortal` + `CanCreateCorrespondences`). |

العدد: **38 قدرة × 6 أدوار = 228 خلية + 5 حراس = 233 اختبارًا**، زمنه < ثانية.

## 4. تحقق سلبي موثّق (مُطبَّق ثم أُعيد)

| الحقن المؤقت | النتيجة المرصودة | أُعيد؟ |
|---|---|---|
| `CanTransferDocuments` يمنح `Manager` | فشل خلية واحدة بالضبط: `CanTransferDocuments × Manager` (232 ناجحة/1 فاشلة) | نعم |
| قدرة جديدة `CanAuditEverything` في الكتالوج | فشل `Catalog_CoversEveryCapabilityExactlyOnce` وحدها برسالة «في الكتالوج وحدها: [CanAuditEverything]» | نعم |
| قيمة `Ghost = 7` في `UserRole` | فشل `DeclaredRoles_AreTheSixDecidedRoles` وحدها | نعم |
| منح `EntityManager` قدرة `CanManageEntityRegistry` في **جدول التوقعات** | فشل `EntityManager_HoldsOnlyThePortalSurface` | نعم |

بعد كل إعادة: `git status` نظيف من الملفات المؤقتة، والاختبارات 233/233 خضراء.

## 5. إزالة تكرار مكتشَف

`PublicEntityService.Groups.cs` كان يحمل نسخة محلية باسم `RolePermissions_IsFullAccess`
(المدير/المشرف) مستخدمة في `RenameGroupAsync` و`AbolishAndReplaceAsync` — أي قدرة ثانية
يمكن أن تنحرف عن `HasFullAccess` بصمت. استُبدلت بالكتالوج المركزي نفسه وحُذفت النسخة.
لا تغيير سلوكي (التعبير متطابق)، وتغطيها اختبارات إعادة التسمية القائمة
(`RenameGroup_ByHead_Throws` ثم مسار `Manager`).

**لم أُمسّ** المقارنات الدوريّة المشروعة الباقية التي تشتق **نطاق** الفاعل
(`CorrespondenceService`/`ReviewLetterService`: قسم مقابل شعبة) — ليست قدرات كتالوج،
بل منطق نطاق، ومكانها `P2-01` (سياسات موردية) كما هو مخطّط.

## 6. التحقق الكامل

- `dotnet build backend/DocGenerator.sln --no-restore -warnaserror` ✅ 0 تحذير.
- `DocGenerator.Application.Tests`: **2483/2483** ✅ (14m33s).
- `DocGenerator.Api.Tests` (`Category!=Sequential`): **1146/1146** ✅ (2m27s) — كانت 913 قبل الجولة (+233).
- `DocGenerator.Api.Tests` (`Category=Sequential`): **5/5** ✅.
- `DocGenerator.Architecture.Tests`: **11/11** ✅.
- الحلقة السريعة (`Category=Unit`): بلا انحدار؛ الاختبارات الجديدة كلها `Unit`.
- حراس: `determinism` نظيف (0 استثناءات)، `migration-watchlist` (5 هجرات)،
  `migration-safety` (لا هدم)، `secrets` (20 مطابقة أساس معروفة).
- الواجهة بلا تغيير: `oxlint`/`tsc`/`vitest`/`build`/`guard` خضراء على الفرع.

## 7. الحدود المتبقية (لا تُخفى)

- **`P2-01`**: ما زالت نقاط النهاية تفحص الأدوار داخل الأجسام (`RolePermissions.*` +
  `Forbid()`)، وبعض الخدمات تقارن الأدوار لاشتقاق النطاق. المصفوفة تضمن **اتساق
  الكتالوج**، لا **إعلانية الإنفاذ** على كل نقطة. الخطة: سياسات موردية + `ADR-0007`.
- **الحد الموضوعي (ملكية/فرع/شعبة)** يبقى في الخدمات كما هو؛ `P2-02` يثبت دلالة
  الدور، و`P2-02c/d/e/f` تثبت النطاق على المسارات الحرجة.
- `Catalog_HasSingleUniformShape` يفشل عند إضافة قدرة موردية إلى `RolePermissions`؛
  القرار المعماري المتوقع أن تنتقل هذه القدرات إلى `IAuthorizationHandler` في `P2-01`،
  وعندها تُحدَّث المصفوفة بوعي (لا تُحذف القدرة بصمت).
