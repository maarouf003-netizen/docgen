# تسليم جلسة — الحالة عند `6da9bfa` (2026-11-17)

> **الغرض:** مرجع البدء للمحادثة التالية. هذه وثيقة **حالة ولقطة**، لا خطة عمل جديدة:
> تصف ما دُمج، وما تحقّق، وما **لم يُقل** في سياق الجلسة السابقة (هجرات معلّقة، قرارات
> مالك، بنود نصف منجزة).
>
> **المصادر الملزمة بجانبها:** `AGENTS.md` (قواعد العمل) ·
> [`docs/PERFECTION_ROADMAP.md`](../PERFECTION_ROADMAP.md) (الحالة البندية التفصيلية) ·
> `RUN_GUIDE.md` §9 (النشر والهجرات).

---

## 1. نقطة الانطلاق

| البند | القيمة |
|---|---|
| الفرع الحي | `origin/master` = `6da9bfa` (9 PRs هذه الجلسة: `#181`–`#189`) |
| البوابات الست | خضراء على `6da9bfa`: `backend` · `frontend` · `guards` · `audit` · `backend-postgres` · `migration-guard` ([run 44046007997](https://github.com/maarouf003-netizen/docgen/actions/runs/44046007997)) |
| الهجرات المعلّقة | **4** (بلا تغيير هذه الجلسة) — التفاصيل في §4 |
| `master` محجوز | مأخوذ في `worktree` آخر عند `457d4ec` (**متأخر 9 commits** عن `origin/master`) ⇒ **لا تعمل عليه**، وابدأ كل فرع من `origin/master` |
| عدد وثائق `docs/audit` | آخرها `47-p4-06-format-gate-20261117.md`؛ هذه الوثيقة `48` |

### PRs المدموجة هذه الجلسة

| PR | البند | الخلاصة | التفاصيل |
|---|---|---|---|
| `#181` | `P2-02g` | مصفوفة كتالوج `RolePermissions` الذرّية: 38 قدرة × 6 أدوار = 228 خلية + 5 حراس | [`40-p2-02g-role-permissions-matrix-20261116.md`](40-p2-02g-role-permissions-matrix-20261116.md) |
| `#182` | `P2-01` | خطة السياسات الموردية + `ADR-0007` + جرد مقيس (22 متحكمًا/239 نقطة) | [`docs/adr/0007-resource-authorization-policies.md`](../adr/0007-resource-authorization-policies.md) |
| `#183` | `P2-01a` | متطلب/معالج موردي للمستندات + ترحيل `DocumentsController` (42 نقطة) | [`41-p2-01a-document-policies-20261116.md`](41-p2-01a-document-policies-20261116.md) |
| `#184` | `P2-01b` | تعميم جسر القدرات + `EntityRegistry`/`Delegates` + **حارس ratchet** للترحيل | [`42-p2-01b-portal-entity-20261116.md`](42-p2-01b-portal-entity-20261116.md) |
| `#185` | `P4-03 (scan)` | تطبيع دلالة `400↔403/404`: المفقود `404`، الصلاحية `403`؛ حذف 5 كتل `ArgumentException ⇒ NotFound` | [`43-p4-03-error-semantics-20261116.md`](43-p4-03-error-semantics-20261116.md) |
| `#186` | `P2-01c-1` | 7 متحكمات إدارة/قراءة ← الجسر + هويات `IsHead/IsSubHead/IsAdmin` | [`44-p2-01c1-admin-controllers-20261117.md`](44-p2-01c1-admin-controllers-20261117.md) |
| `#187` | `P2-01c-2` | الستة الأخيرة + `IsEntityManager`؛ **إفراغ قائمة انتظار الحارس** (صفر قرارات أدوار في المتحكمات) | [`45-p2-01c2-workflow-controllers-20261117.md`](45-p2-01c2-workflow-controllers-20261117.md) |
| `#188` | `P3-08c` | سقوف المسارات المتبقية: سجل التدقيق/تغييرات المستند/إحصاءات الدوائر/قائمة البوابة | [`46-p3-08c-query-budgets-20261117.md`](46-p3-08c-query-budgets-20261117.md) |
| `#189` | `P4-06c` | تطبيع أساس `dotnet format` مرة واحدة + بوابة `--verify-no-changes` في `CI` + `.gitattributes` (`eol=lf`) | [`47-p4-06-format-gate-20261117.md`](47-p4-06-format-gate-20261117.md) |

---

## 2. ما أُنجز بندًا بندًا (بدقّة الحدود)

### `P2-02` — مصفوفة صلاحيات مُختبَرة — **مغلق**

- الجولات `c/d/e/f` (#177–#180) أثبتت عزل البوابة والإنابة والاستئناف، وأصلحت دلالة
  `403/404` المكتشفة.
- `P2-02g` (#181) أضاف الجدول الذرّي: **38 × 6 = 228 خلية** مكتوبة من قرارات المالك،
  مع حراس الاكتمال والشكل والأدوار الستة و`SubHead == Head` وسطح `EntityManager`
  (البوابة فقط). أُزيلت نسخة محلية مكررة لـ`HasFullAccess`.

### `P2-01` — سياسات صلاحيات موردية — **مغلق**

- `ADR-0007` معتمد، والمراحل الأربع منفّذة: متطلب/معالج للمستندات (قرار قبل التقييم
  ليُحفظ `404` للمفقود و`403` لخارج النطاق)، ثم جسر قدرات تطبيقي، ثم إفراغ
  **قائمة انتظار ratchet** بالكامل.
- **النتيجة النهائية:** لا متحكم يحمل `RolePermissions` أو مقارنة دور؛ أي متحكم جديد
  بقرار دورّي يفشل البوابة فورًا. المصفوفة صارت **42 × 6 = 252 خلية**، والجسر
  **35 × 6 = 210**.
- **قرار مقصود (لا يُعاد فتحه):** لم يُنشأ `PortalAccessRequirement`/
  `EntityScopeRequirement` لأن النطاق بنيوي/خدمي ومختبَر أصلًا (ملحق `ADR-0007` §7).

### `P4-03` — تحقق مركزي و`ProblemDetails` — **جزئي (مسح فقط)**

- منفّذ (#185): تطبيع دلالة المطالعات/المراسلات/دوائر التنفيذ، وحارس
  `ApiErrorMessages` يسمح بـ**4** كتل `ArgumentException ⇒ NotFound` موثّقة فقط.
- **غير منفّذ:** `FluentValidation`/`IValidator<T>` موحّد، و`ProblemDetails` بشكل
  `code/field/message`، وربط أخطاء الواجهة. التفاصيل في §5.

### `P3-08` — حراسة N+1 — **مغلق**

- `QueryCounter` + سقوف لكل المسارات: قائمة المستندات **22** (القياس 18) وتفاصيل
  المستند **20**، التصدير **14** (10)، سجل التدقيق **12** (8)، تغييرات المستند **6** (4)،
  إحصاءات الدوائر **16** (12)، قائمة البوابة **18** (14) — مع تأكيد أن الاستجابة تحمل
  صفوفًا فعلية، وتحقّق سلبي بحقن N+1 (27 > 16).
- **متبقٍ (بند مستقل):** القياس الإنتاجي على `PostgreSQL`/Release ضمن `P3-01`.

### `P4-06` — أدوات الجودة — **جزئي**

- منفّذ: CPM (`Directory.Packages.props`)، `packages.lock.json` + `--locked-mode`،
  `global.json`، `.editorconfig`، ثم (#189) **بوابة التنسيق** بعد تطبيع 68 ملفًا
  (لا تغيير دلالي) و`eol=lf` موحّد.
- **متبقٍ:** `Roslynator`/`StyleCop` — **تقييم ضجيج مستقل** قبل التفعيل، لم يُضافا عمدًا
  كي لا يتحول PR التنسيق إلى تغيير قواعد/سلوك.

---

## 3. الأعداد والبوابات (تحقّق هذه الجولة على `origin/master`)

| الفحص | النتيجة |
|---|---|
| `dotnet build backend/DocGenerator.sln -warnaserror` | 0 تحذير / 0 خطأ |
| `dotnet format … --verify-no-changes` | `exit 0` |
| `DocGenerator.Application.Tests` | **2486/2486** |
| `DocGenerator.Api.Tests` (`Category!=Sequential`) | **1437/1437** |
| `DocGenerator.Api.Tests` (`Category=Sequential`) | **5/5** |
| `DocGenerator.Architecture.Tests` | **29/29** |
| الحلقة السريعة (`Category=Unit`) | **707** (Application) + **765** (Api) |
| الواجهة: `oxlint` · `tsc -b` · `vitest` · `build` · `guard` | **1804/1804** + البناء والحارس |
| الحراس الخمسة | `subhead` نظيف · `determinism` (0) · `migration-watchlist` (5 مسجّلة) · `migration-safety` (لا هدم) · `secrets` (20 خط أساس) |

> **ملاحظة توقيت:** `Application` كاملة ≈ 14–15 دقيقة؛ الحلقة السريعة (`--no-build`)
> ≈ 10 ثوانٍ للتنفيذ (≈30 ثانية مع البناء) — التفاصيل في `P1-07` (`docs/audit/23-…`).
> استخدم الحلقة السريعة أثناء التطوير، والكاملة قبل الدفع.
>
> كل الأرقام أعلاه **مقيسة على `origin/master` (`6da9bfa`)** عند إعداد هذه الوثيقة،
> وهي مطابقة لما سجّلته `docs/audit/47-…` في PR `#189`.

---

## 4. الهجرات المعلّقة — **4 (لم تُطبَّق على أي بيئة)**

المرجع الملزم: `RUN_GUIDE.md` §9 (القائمة التراكمية) + `AGENTS.md`
(تذكير الهجرات المعلّقة). كلها **إضافية غير كاسرة** إلا أن الأخيرة **تعتمد على البيانات**.

| # | الهجرة | المحتوى | ملاحظة التطبيق |
|---|---|---|---|
| 1 | `20261114140029_AddIdempotencyResponseMetadata` (`P0-04`) | `StatusCode`/`ContentType`/`ResponseLocation` في `IdempotencyKeys` | قبل نشر نسخة `P0-04` |
| 2 | `20261114160957_P0_06_OptimisticConcurrency` (`P0-06`) | `Version bigint NOT NULL DEFAULT 0` على 12 جدولًا | قبل نشر نسخة `P0-06` |
| 3 | `20261114175636_P0_07_SafeConstraints` (`P0-07`) | `CHECK ([Version] >= 0)` على 16 جدولًا + فهرس `(BranchId, SectionId)` | قبل نشر نسخة `P0-07` |
| 4 | `20261114211659_P0_07b_AmountConstraints` (`P0-07b`) | `CHECK` لعدم سلبية 15 عمود مبالغ في `Documents` | **شغّل `scripts/p0-07b-preflight.sql` على مشروع الإنتاج أولًا** |

```powershell
# التطبيق الوحيد المسموح (لا تستخدم --context DocGeneratorDbContext — لا هجرات له)
dotnet ef database update --context DocGeneratorPostgresDbContext
```

- `Database:AutoMigrate=false` في الإنتاج ⇒ **التطبيق يرفض الإقلاع** إن بقيت هجرة معلّقة.
  هذا رفض مقصود، لكنه يعني: **طبّق قبل نشر النسخة، لا بعدها**.
- الترتيب الإلزامي في النافذة: نسخة احتياطية مُستعادة تجريبيًا ← تطبيق الهجرات ←
  التحقق من `__EFMigrationsHistory` ← تشغيل النسخة.
- أي هجرة جديدة تُسجَّل في `RUN_GUIDE.md` §9 **و**`AGENTS.md` في التغيير نفسه، وإلا فشل
  حارس `migration-watchlist`.

---

## 5. الحدود المفتوحة وقرارات المالك (اقرأها قبل اختيار البند التالي)

| البند | الحالة | ما يلزم بالضبط |
|---|---|---|
| **`D11` — «رئيس بلا فرع» عبر النطاقات** | **قرار مالك معلّق** | `D11` يفرّق كتابة `400`/قراءة `403`، لكن **عدّاد طلبات الإنابة ما زال `400`** ومثبّت باختبار صريح (`DelegationPendingCountBranchGuardTests`). السؤال للمالك: توحيد العدّادات على `403` كما يقرر `D11`، أم توسيع الاستثناء الموثّق؟ |
| **بقايا دلالة `404` في متحكمين** | غير منفّذة | 4 كتل `catch (ArgumentException) ⇒ NotFound` باقية (`AppealsController`×3، `DelegationsController`×1) لأن خدماتها ما زالت ترمي `ArgumentException` للمفقود؛ `ApiErrorMessagesTests` يثبتها كقائمة مسموحة. إزالتها تتطلب فصل «مفقود» عن «تحقق» في الخدمة أولًا |
| **باقي `P4-03`** | غير منفّذ | `IValidator<T>`/`FluentValidation` موحّد + `ProblemDetails` (`code`/`field`/`message` عربي) + ربط الواجهة. **مرتبط بـ`D11`**: شكل الخطأ لا يُجمَّد قبل حسم دلالة «رئيس بلا فرع» |
| **`P2-03` — افتراضات أمنية في الإنتاج** | لم يبدأ (الإعدادات الحالية غير آمنة للإنتاج) | `appsettings.json`: `HstsEnabled=false`، `HttpsRedirectionEnabled=false`، `AllowedHosts="*"`، `KnownProxies=[]`، `CspReportOnly=true`. يلزم ضبطها + **رفض إقلاع في Production عند نقصها**، وفحص رؤوس/Cookies/CORS على `staging` — لا يُحسم من الإعدادات المحلية |
| **`P2-06` — SAST/DAST/SBOM** | جزئي | منفّذ: بوابة الاعتماديات + SBOM. **غير منفّذ:** `CodeQL`/`Semgrep` (SAST)، `DAST`، `Trivy` للصورة |
| **`P2-04` — تقوية الجلسات** | لم يبدأ | قرارات مالك: عمر JWT (15–30 دقيقة)، refresh rotation، MFA للمدير/المشرف، تدوير `Jwt:Secret` بفترة سماح |
| **`P1-01` — منصة اختبار PostgreSQL حقيقية** | لم يبدأ | `Testcontainers`/خدمة `postgres:16` في `CI` + `Respawn`؛ **شرط مسبق** لـ`P1-08` (إزالة `SQLite` من الاختبارات) وللقياس الإنتاجي `P3-01`. لا يبدأ دون Docker محلي أو خدمة CI |
| **`P3-08` قياس إنتاجي** | مؤجل | ضمن `P3-01` (Postgres/Release)، لا يُعاد فتح السقوف المحلية |
| **`Roslynator`/`StyleCop`** | تقييم | قياس ضجيج على الفرع أولًا؛ لا تفعيل داخل PR تنسيق |
| **إجراءات مالك (P2-05/P5)** | معلّقة | `gitleaks` على سجل Git، تدوير أسرار البيئة وتوثيق التاريخ، تنبيه على `/readyz`/outbox lag، بيئة `staging` |
| **`P4-05` (Modular Monolith)** | **مؤجل بقرار موثّق** | بعد `P4-02` + بوابة المعمارية الحالية؛ لا يُفتح الآن |

---

## 6. أول 3 خطوات مقترحة للمحادثة التالية

1. **قرار `D11` من المالك** (دقائق، لا كود): هل تُوحَّد عدّادات «رئيس بلا فرع» على
   `403` كما يقرر `D11`، أم تُبقى `400` موسومة كاستثناء صريح؟ **يُقدَّم أولًا** لأنه
   يجمّد شكل الخطأ في البند التالي.
2. **باقي `P4-03`** بعد القرار: `ProblemDetails` موحّد (`code`/`field`/`message`) +
   `IValidator<T>`، مع إبقاء كل `400` بشكل واحد وربط الواجهة.
3. **`P2-03`**: جرد الإعدادات الحالية مقابل القائمة، ثم فحص staging فعلي (رؤوس/Cookies/CORS)
   وتوثيق الفجوات — تنفيذ ما يمكن بلا قرارات، وتقديم ما يحتاج قيمة إنتاج للمالك.

> البديل إن أراد المالك تقديم المخاطر التشغيلية على المعمارية: ابدأ بـ`P2-03` ثم `D11`،
> ثم باقي `P4-03`.

---

## 7. أوامر التحقق الكاملة (قبل أي دفع)

```powershell
# الخلفية
dotnet build backend/DocGenerator.sln --nologo -v q -warnaserror
dotnet format backend/DocGenerator.sln --verify-no-changes --no-restore
dotnet test backend/tests/DocGenerator.Architecture.Tests/DocGenerator.Architecture.Tests.csproj --no-build --nologo -v q
dotnet test backend/tests/DocGenerator.Application.Tests/DocGenerator.Application.Tests.csproj --no-build --nologo -v q
dotnet test backend/tests/DocGenerator.Api.Tests/DocGenerator.Api.Tests.csproj --no-build --nologo -v q --filter "Category!=Sequential"
dotnet test backend/tests/DocGenerator.Api.Tests/DocGenerator.Api.Tests.csproj --no-build --nologo -v q --filter "Category=Sequential"
```

```powershell
# الحلقة السريعة أثناء التطوير
dotnet test backend/tests/DocGenerator.Application.Tests/DocGenerator.Application.Tests.csproj --no-build --filter "Category=Unit"
dotnet test backend/tests/DocGenerator.Api.Tests/DocGenerator.Api.Tests.csproj --no-build --filter "Category=Unit"
```

```powershell
# الواجهة + حراس المستودع
cd frontend
npx oxlint src scripts
npx tsc -b
npx vitest run
npm run build
npm run guard
cd ..
node scripts/subhead-guards.mjs
node scripts/determinism-guard.mjs
node scripts/migration-watchlist-guard.mjs
node scripts/migration-safety-guard.mjs
node scripts/secrets-guard.mjs
```

---

## 8. قواعد العمل التي تُوفّر وقتًا (من الجلسة السابقة)

- **لا دفع مباشر إلى `master`**: فرع من `origin/master` ← PR ← انتظار البوابات الست.
- **`master` مأخوذ في `worktree` آخر ومتأخر** عن `origin/master` ⇒ `git switch -c <branch> origin/master`.
- **`dotnet format` قبل الدفع**: بوابة `backend` تفشل على أي انحراف تنسيقي (`eol=lf`
  مفروض عبر `.gitattributes`؛ إن ظهرت فروق CRLF فالسبب checkout محلي لا الكود).
- **الحلقة السريعة أولًا، الكاملة قبل الدفع** — `Application` كاملة ≈ 14–15 دقيقة.
- **`docs/audit` لقطات**: لا تُحرَّر بعد التجميد؛ كل تحديث بوثيقة/ملحق جديد مؤرخ.
- **أي تغيير سلوكي = اختبار + تحقق سلبي موثّق** (حقن الانحراف وإثبات الفشل ثم الإرجاع).
- **`P2-01` مُغلق**: لا تُعِد إدخال قرارات أدوار في المتحكمات؛ الحارس يفشل فورًا.
