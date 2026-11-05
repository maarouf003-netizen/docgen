# خطة التنظيف اللاحق لخطة `HeadAlert` — البنود 1–2–3

> **النوع:** وثيقة تنفيذ عملية (ليست لقطة تدقيق مجمَّدة) — تُحدَّث بسطر في §9 عند إنجاز كل `PR`.
> **تاريخ الإعداد:** 2026-11-04 · **الأساس:** `master @ 46538d9`.
> **النطاق:** `backend/` + `frontend/` — **صفر هجرات، صفر تغيير عقود `API`، صفر تغيير سلوك ظاهر**.
> **المصدر:** مراجعة ما بعد إغلاق `HEAD_ALERTS_RESTRUCTURE_PLAN` (المتبقي: صفر بنود).
> **القاعدة:** ثلاثة `PRs` مستقلة، كل واحد قابل للتراجع وحده، ولا يُدمج أيٌّ منها قبل خضرة البوابات.

---

## 0. للمحادثة الجديدة — ابدأ من هنا

1. اقرأ بهذا الترتيب: `AGENTS.md` ← هذه الوثيقة ← `RUN_GUIDE.md` §9 (الهجرات).
2. تحقّق من الحالة **قبل أي فعل**:
   ```powershell
   git fetch origin --prune
   git status --porcelain          # يجب أن يكون فارغًا
   git branch --show-current       # master
   git log --oneline -3
   ```
   إن كانت الشجرة غير نظيفة: افهم مصدر التغييرات ولا تُودِعها تلقائيًا.
3. **سجّل خط الأساس الفعلي** (لا تعتمد على أرقام هذه الوثيقة وحدها):
   ```powershell
   dotnet build backend/DocGenerator.sln --nologo -v q -warnaserror
   dotnet test backend/tests/DocGenerator.Application.Tests/DocGenerator.Application.Tests.csproj --no-build --nologo -v q
   dotnet test backend/tests/DocGenerator.Api.Tests/DocGenerator.Api.Tests.csproj --no-build --nologo -v q --filter "Category!=Sequential"
   dotnet test backend/tests/DocGenerator.Api.Tests/DocGenerator.Api.Tests.csproj --no-build --nologo -v q --filter "Category=Sequential"
   cd frontend; npx oxlint src; npx tsc -b; npx vitest run; npm run build
   ```
4. نفّذ بالترتيب: **PR‑1 ← PR‑2 ← PR‑3**. لا تنتقل قبل خضرة البوابات وتسجيل النتيجة في §9.
5. **ممنوع:** الدفع إلى `master`، تعديل `docs/audit/*` (لقطات مجمَّدة)، أي هجرة، أي تغيير في
   العقود/الصلاحيات/الرسائل الظاهرة.

### 0.1 تعريف «منجز»

- الكود المطلوب محذوف/مستبدل، والبحث النصي المحدد في كل بند يعود **صفرًا**.
- البوابات كاملة خضراء، و`CI` **6/6** على رأس الفرع.
- لا اختبار عُدِّل ليمرّ؛ أي اختبار سقط = توقف وتحليل.
- §9 محدَّث بسطر التنفيذ (الفرع، `PR`، أرقام البوابات، `git diff --stat`).

---

## 1. السياق والثوابت

- خطة `HeadAlert` مغلقة (`PR‑1…PR‑6c`)، والكود لا يحمل بقايا `LegacyBridge`/`scope=received`/
  `hasFullMessage`/`hasNextPage` إلا في اختبارات سلبية.
- الكود مقسّم إلى طبقات: `Application` بلا EF Core، والعقود محروسة باختبارات ومخططات.
- **لا تغيير سلوكي مقصود في هذه الخطة.** أي فرق سلوكي = توقف وإعادة تقييم.

### الثوابت الملزمة

| الثابت | الشرط |
|---|---|
| الهجرات | **صفر**. لا `Migrations`/`MigrationsPostgres`، ولا `dotnet ef` |
| العقود | لا حقل يُحذف/يُعاد تسميته في `DTOs` أو `types/` |
| الرسائل | نصوص `4xx` العربية تبقى كما هي، بلا ذيول `(Parameter '…')` |
| الصلاحيات | لا تغيير في `RolePermissions` ولا في شروط الأدوار |
| الواجهة | لا تغيير تخطيط/بصري؛ القواعد `Mobile-first` لا تُمس |
| الفروع | `refactor/…` + `PR`؛ ممنوع الدفع المباشر إلى `master` |

---

## 2. البند 1 — حذف سطح مستودع ميت (`PR‑1`)

### 2.1 الهدف

حذف خمس دوال بقيت من النموذج السابق للـOutbox، لا مستهلك لها في `backend/src` ولا في
`backend/tests` (كل واحدة لها **تعريف في الواجهة + تنفيذ في المستودع فقط**).

### 2.2 الأدلة (تحقّق منها قبل الحذف)

| الدالة | الواجهة | التنفيذ | مستهلكون |
|---|---|---|---|
| `FindLatestByDelegationAsync` | `IHeadAlertRepository.cs:132` | `HeadAlertRepository.cs:449-455` | 0 |
| `FindLatestByDelegationAndRecipientAsync` | `IHeadAlertRepository.cs:139-140` | `HeadAlertRepository.cs:457-466` | 0 |
| `ListByAppealAsync` | `IHeadAlertRepository.cs:143` | `HeadAlertRepository.cs:468-476` | 0 |
| `ListActiveByScopeAndKindsAsync` | `IHeadAlertRepository.cs:163-164` | `HeadAlertRepository.cs:561-576` | 0 |
| `ListActivePendingByLawyerAndKindAsync` | `IHeadAlertRepository.cs:178-179` | `HeadAlertRepository.cs:634-643` | 0 |

> الأرقام لحظة كتابة الخطة؛ **أعد التحقق** بـ`grep` قبل الحذف. لا تحذف دالة يظهر لها مستهلك
> جديد. `ListActivePendingByCircuitAndKindAsync` **تبقى** (مستهلكها `HeadAlertService`)،
> و`ArchiveByScopeAndKindsAsync` **تبقى** (مستهلكها `HeadAlertService`).

### 2.3 التغيير

1. احذف من `IHeadAlertRepository` كل توقيع مع كتلة `/// <summary>` الخاصة به (بلا ترك تعليق يتيم).
2. احذف التنفيذات الخمسة من `HeadAlertRepository` كاملة.
3. تحقق أن `HeadAlertScopeKind` ما يزال مستخدمًا (`ArchiveByScopeAndKindsAsync`) — لا تحذفه.
4. لا تلمس `docs/HEAD_ALERTS_RESTRUCTURE_PLAN.md` (تذكر الدوال كتاريخ تصميم) ولا `docs/audit/*`.

### 2.4 التحقق ومعايير القبول

```powershell
# يجب أن يعود صفرًا في src وtests
Get-ChildItem -Recurse -File backend -Include *.cs |
  Select-String "FindLatestByDelegationAsync|FindLatestByDelegationAndRecipientAsync|ListByAppealAsync|ListActiveByScopeAndKindsAsync|ListActivePendingByLawyerAndKindAsync"
```

- `dotnet build ... -warnaserror` = 0 تحذير/0 خطأ.
- `Application` + `Api` (متوازية وتتابعية) خضراء **بلا تعديل أي اختبار**.
- `git diff --stat` لا يمس أي ملف خارج `IHeadAlertRepository.cs` و`HeadAlertRepository.cs`.

**التقدير:** ~نصف يوم · **المخاطر:** شبه معدومة (الكود غير مستدعى) · **التراجع:** إعادة الملفين.

---

## 3. البند 2 — توحيد أعلام الأدوار + hook عدّاد «غير المقروء» (`PR‑2`)

### 3.1 الهدف

- إزالة اشتقاق `isHeadOrSubHead` المكرر في 15 ملفًا و`isManager` المكرر في 4 ملفات، عبر
  **مساعدات أدوار نقية** بدل تكرار الشروط.
- إزالة جلب `GET /alerts/unread-count?scope=user` المكرر في ثلاث صفحات عبر hook واحد.

### 3.2 القرار التصميمي: مساعدات نقية لا رايات سياق

`useAuth()` يعرض `isHeadOrSubHead` أصلًا، لكن **عشرات ملفات الاختبار** (40 ملفًا تذكر
`useAuth`، وكثير منها يموّكه جزئيًا) تُرجع `user`/`hasFullAccess`/`isHead` فقط، بلا
`isSubHead`/`isHeadOrSubHead`. الاعتماد على راية السياق سيُسقط سلوك رئيس الشعبة في الاختبارات.
لذلك:

**ملف جديد:** `frontend/src/auth/roles.ts`

```ts
import type { Role } from '../types/common';

export function isLawyerRole(role: Role | null | undefined): boolean;
export function isHeadOrSubHeadRole(role: Role | null | undefined): boolean;
export function hasFullAccessRole(role: Role | null | undefined): boolean;
```

- التنفيذ مطابق حرفيًا للشروط الحالية (`=== 'lawyer'`، `=== 'head' || === 'subhead'`،
  `=== 'manager' || === 'admin'`).
- `AuthContext.tsx` يستخدمها لاشتقاق `hasFullAccess`/`isHead`/`isSubHead`/`isHeadOrSubHead`
  (مصدر حقيقة واحد، بلا تغيير في العقد).
- **لا تغيّر** `UsersManagement.tsx` (`me?.role === 'manager'` = مدير فقط، لا مدير+مشرف).

### 3.3 مواقع الاستبدال (15 ملفًا)

استبدل السطر المحلي بـ`isHeadOrSubHeadRole(role)` — مع تمرير `user?.role` أو `role` حسب الملف:

| الملف | السطر (لحظة الإعداد) | ملاحظة |
|---|---|---|
| `frontend/src/pages/AppealDetail.tsx` | 53 | `user` |
| `frontend/src/pages/AccountPage.tsx` | 30 | `user` |
| `frontend/src/pages/AppealsList.tsx` | 68 | `role` |
| `frontend/src/pages/AlertsPage.tsx` | 43 | `user` |
| `frontend/src/pages/BranchLawyers.tsx` | 12 | `user` |
| `frontend/src/pages/CircuitStatsPage.tsx` | 15 | `user` |
| `frontend/src/pages/CorrespondenceDetail.tsx` | 76 | `user` |
| `frontend/src/components/Layout.tsx` | 32 | `user` — **حدّث التعليق**: يبقى الاشتقاق من الدور عبر المساعد النقي (سبب بقاء السلوك مع `mock` القديم) |
| `frontend/src/pages/Dashboard.tsx` | 31 | `user` |
| `frontend/src/pages/CorrespondencesList.tsx` | 28 | `user` |
| `frontend/src/pages/DocumentsList.tsx` | 324 | `user` |
| `frontend/src/pages/EntityRegistryReview.tsx` | 28 | `user` |
| `frontend/src/pages/ReviewDetail.tsx` | 62 | `user` |
| `frontend/src/pages/ReviewsList.tsx` | 15 | `user` |
| `frontend/src/pages/StatsPage.tsx` | 25 | `user` |

كذلك استبدل `const isManager = user?.role === 'manager' || user?.role === 'admin';` بـ
`hasFullAccessRole(user?.role)` في: `Dashboard.tsx:30`، `AccountPage.tsx:31`،
`AlertsPage.tsx:42`، `Layout.tsx:39`. (`AppealsList.tsx:67` يحسب `hasFullAccess` من `role`
فيُستبدل بالمساعد نفسه أيضًا — اختياري لكنه متسق.)

### 3.4 hook عدّاد التنبيهات

**ملف جديد:** `frontend/src/hooks/useUnreadAlertCount.ts`

```ts
import { api } from '../api/client';
import { useCancellableRequest, type CancellableRequestResult } from './useCancellableRequest';

export const ALERTS_UNREAD_SCOPE = 'user' as const;
export interface UnreadAlertCount { count: number }

export function useUnreadAlertCount(enabled: boolean): CancellableRequestResult<UnreadAlertCount> {
  return useCancellableRequest<UnreadAlertCount>(
    (signal) => api.get('/alerts/unread-count', { signal, params: { scope: ALERTS_UNREAD_SCOPE } })
      .then((r) => r.data),
    [ALERTS_UNREAD_SCOPE],
    { enabled },
  );
}
```

- **يُبقي العقد نفسه** `CancellableRequestResult` لأن `AccountPage` يستخدم `unreadQuery.error`،
  و`AlertsPage` يستخدم `unreadQuery.setData` لإنقاص العدّاد بعد تعليم المقروء.
- انقل تعليق «النطاق الشخصي الموحّد `user`» من الصفحات إلى الـhook (مصدر واحد للتوثيق).
- استبدل في `Dashboard.tsx:74-82` و`AccountPage.tsx:56-64` و`AlertsPage.tsx:68-74` **مع الحفاظ
  على تعبير `enabled` الخاص بكل صفحة حرفيًا**:
  - `Dashboard`: `userReady && !isManager && (isLawyer || headAlertsScopeEnabled)`
  - `AccountPage`: `userReady && !isManager && (isLawyer || headUnreadEnabled)`
  - `AlertsPage`: `userReady && (isLawyer || headScopeEnabled)`

### 3.5 الاختبارات

- جديد: `frontend/src/auth/roles.test.ts` — جدول حقيقة للأدوار الستة + `null`/`undefined`.
- جديد: `frontend/src/hooks/useUnreadAlertCount.test.tsx` — بنمط `useBadgeCount.test.tsx`:
  - يطلب `/alerts/unread-count` بـ`params: { scope: 'user' }`;
  - `enabled=false` ⇒ صفر طلبات؛
  - الفشل يُظهر `error` ويُبقي آخر بيانات؛
  - `setData` يسمح بإنقاص `count` (سيناريو تعليم المقروء).
- **لا تعدّل** اختبارات الصفحات القائمة ما دامت تؤكد `params: { scope: 'user' }`؛ إن سقط
  اختبار فالسبب فرق سلوكي — توقف.

### 3.6 التحقق ومعايير القبول

```powershell
cd frontend
npx oxlint src
npx tsc -b
npx vitest run
npm run build
npm run guard
cd ..
node scripts/subhead-guards.mjs
```

- `grep "const isHeadOrSubHead ="` في `frontend/src` = **صفر**.
- `grep "'/alerts/unread-count'"` في `frontend/src` = **موضع واحد** (الـhook) + الاختبارات.
- لا تغيير في أي نص/تخطيط ظاهر؛ لقطات الاختبارات القائمة تُثبت ذلك.

**التقدير:** ~يوم · **المخاطر:** منخفضة (مساعدات نقية + hook يحافظ على العقد) ·
**التراجع:** إعادة الملفات الثلاثة + الصفحات.

---

## 4. البند 3 — إزالة ترجمة `ArgumentException` المكررة من المتحكمات (`PR‑3`)

> **هذا البند أعلى مخاطر الثلاثة.** لا تبدأه قبل خضرة `PR‑1` و`PR‑2`. إن ظهر أي فرق في
> جسم/رمز الاستجابة، **توقف** ولا تُكمل «بترقيع».

### 4.1 المشكلة

`135` كتلة `catch (ArgumentException e)` في `16` متحكمًا. **126** منها متطابقة حرفيًا:

```csharp
catch (ArgumentException e)
{
    return BadRequest(new { message = ApiErrorMessages.From(e) });
}
```

و**9** منها تُرجع `NotFound` عن قصد (تُستثنى):

| الملف | الأسطر |
|---|---|
| `AppealsController.cs` | 107, 426, 461 |
| `DelegationsController.cs` | 93 |
| `ReviewLettersController.cs` | 106, 121, 156, 175, 198 |

`GlobalExceptionHandler` يترجم أصلًا `ArgumentException → 400` و`KeyNotFoundException → 404`،
لكن هناك **فارقان يجب إغلاقهما قبل الحذف**:

1. **الرسالة:** المتحكم يستخدم `ApiErrorMessages.From` (تطهير + **رسالة بديلة** عند الفراغ)،
   والوسيط يستخدم `LogSanitizer.SanitizeForResponse(exception.Message)` بلا بديل.
2. **السجل:** الوسيط يسجّل `LogError` لكل استثناء غير مصنَّف، فحذف الـ`catch` سيحوّل كل `400`
   تحقّقي متوقع إلى **خطأ أحمر** يلوّث لوحات المراقبة.

### 4.2 التغييرات (بالترتيب)

**(أ) نقل المساعد إلى حيّ محايد**

- `git mv backend/src/DocGenerator.Api/Controllers/ApiErrorMessages.cs backend/src/DocGenerator.Api/Http/ApiErrorMessages.cs`
  (أنشئ المجلد `Http`)، و`namespace DocGenerator.Api.Http`.
- حدّث `using` في: `GlobalExceptionHandler.cs`، و`AppealsController`/`DelegationsController`/
  `ReviewLettersController` (الثلاثة التي تبقي فيها كتل `NotFound`)، و`ApiErrorMessagesTests.cs`.
- **حدّث الحارس المصدري القائم** `ApiSources_DoNotReflectUnsanitizedExceptionMessages`: يمسح حاليًا
  `Controllers` و`Middleware` فقط — أضف `"Http"` إلى القائمة، وإلا صار الملف المنقول خارج الفحص.

**(ب) تكافؤ الرسالة في الوسيط**

- في `GlobalExceptionHandler.TryHandleAsync`، فرع الرسالة لـ`ArgumentException or KeyNotFoundException
  or DocumentConflictException or UnauthorizedAccessException` يستخدم `ApiErrorMessages.From(exception)`
  بدل `LogSanitizer.SanitizeForResponse(exception.Message)`.
- أضف اختبار وحدة: `ArgumentException("")` ⇒ جسم `{ message }` = `ApiErrorMessages.Fallback`.

**(ج) تصنيف السجل**

- أضف فرعًا: أي `statusCode` في `[400, 499]` (غير `499` الخاص بانقطاع العميل، وغير
  `UnauthorizedAccessException` الذي يبقى `Warning`) يُسجَّل `Information` لا `Error`.
- اختبار وحدة بـ`ILogger` ملتقط: `ArgumentException("نص")` ⇒ 400 + **لا** سطر `Error`.

**(د) الحذف الميكانيكي للـ126**

قواعد إلزامية لكل كتلة:

1. إن كانت `catch (ArgumentException …)` **الوحيدة** في `try`: احذف `try`/`catch` كاملين وأزل
   المسافة البادئة لجسم العملية (dedent) بلا أي تغيير آخر.
2. إن وُجدت `catch` أخرى (مثل `IdempotentReplayException` أو `KeyNotFoundException`): احذف كتلة
   `ArgumentException` وحدها وأبقِ `try` وبقية الـ`catch` كما هي.
3. لا تلمس كتل `NotFound` التسع، ولا تحذف `using` ما زال مستخدمًا.
4. نفّذ **ملفًا ملفًا** بالترتيب التالي (الأصغر أولًا): `AuthController`، `AppSuggestionsController`،
   `BranchesController`، `ForumController`، `PersonalRemindersController`، `SectionsController`،
   `UserManagementController`، `AlertsController`، `CorrespondencesController`،
   `ExecutionCircuitsController`، `DelegationsController`، `PortalController`، `DocumentsController`،
   `AppealsController`، `EntityRegistryController`، `ReviewLettersController`.
5. بعد كل ملف: `dotnet build` سريع + مراجعة `git diff` (يجب ألا يظهر أي سطر غير `try/catch/dedent`).

### 4.3 حارس مصدري جديد

أضف إلى `ApiErrorMessagesTests` اختبارًا يمسح `backend/src/DocGenerator.Api/Controllers`:

- أي `catch (ArgumentException` يجب ألا يعقبه `BadRequest(`؛
- الاستثناء المسموح: كتل `NotFound` التسع (يمكن حصرها بعدد/نمط صريح).

### 4.4 التحقق ومعايير القبول

```powershell
dotnet build backend/DocGenerator.sln --nologo -v q -warnaserror
dotnet test backend/tests/DocGenerator.Application.Tests/DocGenerator.Application.Tests.csproj --no-build --nologo -v q
dotnet test backend/tests/DocGenerator.Api.Tests/DocGenerator.Api.Tests.csproj --no-build --nologo -v q --filter "Category!=Sequential"
dotnet test backend/tests/DocGenerator.Api.Tests/DocGenerator.Api.Tests.csproj --no-build --nologo -v q --filter "Category=Sequential"
```

- `grep "catch (ArgumentException"` في المتحكمات = **9 فقط** (كلها `NotFound`).
- عيّنة تكامل (`Documents`, `Alerts`, `Delegations`): `400` بجسم `{ message }` عربي، بلا
  `(Parameter '…')` ولا `Actual value was`؛ و`403`/`404` كما كانت.
- لا سطر `Error` جديد في سجل اختبارات المسارات التحققية.
- `git diff --stat` ≈ **‑500 سطر** (المتوقع)، بلا تغيير في `Controllers` خارج كتل `catch`.

**التقدير:** ~2–3 أيام · **المخاطر:** متوسطة (حجم التغيير) · **التراجع:** عكس الـ`PR` (لا آثار
قاعدية).

---

## 5. ترتيب التنفيذ والبوابات

| # | الفرع | النطاق | التقدير | البوابة الإلزامية |
|---|---|---|---|---|
| `PR‑1` | `refactor/head-alerts-dead-repo-surface` | `backend` — مستودع التنبيهات | 0.5 يوم | بناء 0/0 + `Application` + `Api` |
| `PR‑2` | `refactor/frontend-role-and-unread-hooks` | `frontend` — مساعدات + hook | 1 يوم | `oxlint` + `tsc` + `vitest` + `build` + `guard` + `subhead-guards` |
| `PR‑3` | `refactor/api-central-validation-errors` | `backend` — وسيط + 16 متحكمًا | 2–3 أيام | كل بوابات الخلفية + `backend-postgres` |

**قبل كل دمج:** `CI` **6/6** على رأس الفرع (`backend` · `frontend` · `guards` · `audit` ·
`backend-postgres` · `migration-guard`)، ومراجعة `git diff` كاملة (لا تغييرات جانبية).

---

## 6. خارج النطاق (صريح)

- تفكيك `HeadAlertService` أو `DocumentDelegationService` أو أي خدمة كبيرة.
- أي `refactoring` شامل/إعادة كتابة.
- أي هجرة أو تغيير مخطط (`SQLite`/`PostgreSQL`).
- أي تغيير في `docs/audit/*` أو في `HEAD_ALERTS_RESTRUCTURE_PLAN.md` (تاريخ مجمَّد/تصميم).
- أي تغيير بصري أو في تخطيط الواجهة.

---

## 7. المخاطر وشروط التوقف (No‑Go)

| الخطر | العلامة | الإجراء |
|---|---|---|
| دالة «ميتة» لها مستهلك جديد | نتيجة `grep` غير صفرية | **توقف** — لا تحذف |
| اختبار صفحة يسقط بعد الـhook | فرق في `params`/`error`/`setData` | **توقف** — الـhook خالف العقد |
| فرق رسالة/رمز بعد حذف `catch` | اختبار تكامل أحمر أو جسم مختلف | **توقف** — لا تُعدّل الاختبار ليمرّ |
| ظهور `LogError` لطلبات `400` | سجل الاختبارات | أصلح تصنيف السجل قبل المتابعة |
| `CI` أحمر على `backend-postgres` | `gh pr checks` | أصلح على الفرع، لا تُدمج |

---

## 8. الهجرات (تذكير نشر — لا يخصّ هذه الخطة)

هذه الخطة **لا تضيف هجرات**. لكن قبل أي نشر على بيئة `SQLite` تبقى القائمة التراكمية في
`RUN_GUIDE.md` §9 (**5 معلّقة**): `HeadAlertTaxonomy_Expand` ← `HeadAlertOutbox_Create` ←
`HeadAlertTaxonomy_Backfill` ← `HeadAlertTaxonomy_Contract` ← `HeadAlertOutbox_ConflictRetries`.
`PostgreSQL` مكتمل (64 هجرة، صفر معلّقة). لا تنسَ `dotnet ef database update --context …` في
نافذة النشر، ولا تعتبر المهمة «منجزة» قبل تأكيد ذلك في التقرير.

---

## 9. سجل التنفيذ (يُعبأ في المحادثة المنفّذة)

| التاريخ | `PR` | الفرع | النتيجة | البوابات | `diff --stat` |
|---|---|---|---|---|---|
| 2026-11-05 | `PR‑1` | `refactor/head-alerts-dead-repo-surface` | تم — حُذفت الدوال الخمس الميتة (تعريف + تنفيذ) بلا مستهلك، و`ListActivePendingByCircuitAndKindAsync`/`ArchiveByScopeAndKindsAsync` باقيتان. | بناء `0/0` · `Application` 2195 · `Api` 888 + 5 | 2 ملف · ‑85 سطرًا |
| 2026-11-05 | `PR‑2` | `refactor/frontend-role-and-unread-hooks` | تم — `roles.ts` (ثلاثة مساعدات نقية) + `useUnreadAlertCount`، واستبدال الاشتقاق في 15 صفحة و`AuthContext`، واختبارات الجدول والـhook. | `oxlint` · `tsc -b` · `vitest` 1720 · `build` · `guard` · `subhead-guards` | 20 ملفًا · +217/‑60 |
| 2026-11-05 | `PR‑3` | `refactor/api-central-validation-errors` | تم — نقل `ApiErrorMessages` إلى `Api/Http`، تكافؤ الرسالة والتصنيف في الوسيط، حذف 126 كتلة `catch` ميكانيكيًا (بقيت 9 `NotFound`). | بناء `0/0` · `Application` 2195 · `Api` 892 + 5 · حارسا المصدر | 20 ملفًا · +316/‑946 |

> **ملاحظة البوابات:** نُفِّذت محليًا على `SQLite` (لا خادم `PostgreSQL` محلي)؛ بوابة `backend-postgres` تُشغَّل في `CI` على رأس كل فرع قبل الدمج. لا هجرات في هذه الخطة.

