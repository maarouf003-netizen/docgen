# دليل تنفيذ تسليم تنبيهات `HeadAlert` (الدمج ← النشر ← بقية المراحل)

> **وثيقة تشغيلية** (ليست لقطة تدقيق مجمَّدة): تُحدَّث بسطر «سجل التنفيذ» §11 عند كل خطوة تُنجز،
> وتُستبدل بوثيقة أحدث عند تغيّر الحالة. تاريخ الإعداد: **2026-11-01**.
> الغرض: تمكين جلسة جديدة (بلا ذاكرة هذه المحادثة) من تنفيذ التسليم خطوة بخطوة بأمان، مع بوابات
> قبول صريحة لكل خطوة وشروط توقف.

---

## 0. كيف تبدأ في محادثة جديدة

1. اقرأ بهذا الترتيب: `AGENTS.md` (قواعد ملزمة) ← هذه الوثيقة ← `RUN_GUIDE.md` §9 (النشر والهجرات)
   ← `docs/HEAD_ALERTS_PR4_REMEDIATION_PLAN.md` §0.3/§12 (مرساة الحالة وسجل الإغلاقات)
   ← `docs/HEAD_ALERTS_RESTRUCTURE_PLAN.md` §15 (مراحل `PR-1..PR-6`).
2. تحقّق من الحالة الفعلية **قبل أي فعل** بالخطوة «أ‑0» أدناه؛ لا تعتمد على أرقام هذه الوثيقة وحدها.
3. **قواعد غير قابلة للتفاوض:**
   - لا دفع مباشر إلى `master`؛ كل تغيير عبر فرع + `PR` + الفحوص المطلوبة.
   - لا نشر لـ`PR-4` وحده ولا لـ`PR-5` وحده؛ وحدة التراجع = **الزوج** (`D10=atomic`).
   - الإنتاج يعمل بـ`Database:AutoMigrate=false`: الهجرات تُطبَّق **قبل** نشر الكود وإلا يفشل الإقلاع
     (سلوك مقصود).
   - أي تغيير سلوكي يحتاج اختبارًا، واختبارًا مُتحقَّقًا بالطفر (mutation): أعد العيب ⇒ يجب أن يفشل الاختبار.
   - لا أسرار في الكود ولا في الوثائق.

> **منقول من الجلسة السابقة (تحقّق منه):** التغييرات الأخيرة في `PR-5` تخصّ `frontend/` + `docs/` فقط،
> ولا تغيير خلفي ولا هجرات جديدة.

---

## 1. الحالة المثبتة (Snapshot — محدَّثة 2026-11-01 بعد دمج الزوج)

| البند | القيمة |
|---|---|
| `master` | **`4e421f4`** (دمج `#55`) فوق **`97a4a93`** (دمج `#54`) — دُمج الزوج 2026-11-01 |
| فرع `PR-4` | `feature/head-alerts-pr4-api` — **محذوف بعد الدمج** (@ `3d38052`) |
| فرع `PR-5` | `feature/head-alerts-pr5-ui` — **محذوف بعد الدمج** (آخر رأس: `31c1153`؛ رأس الكود المختبَر `27dcb39`) |
| `PR` #54 | `PR-4 API` → `master` · **`MERGED`** في `97a4a93` · `CI` 12/12 على `3d38052` |
| `PR` #55 | `PR-5 UI` → `master` · **`MERGED`** في `4e421f4` · `CI` **12/12** على `31c1153` بعد إعادة التوجيه |
| الفحوص المطلوبة على `master` | `backend`، `frontend`، `guards`، `audit` (غير صارمة: `strict=false`) |
| فحوص `CI` العاملة | ستة: `backend`، `backend-postgres`، `frontend`، `guards`، `audit`، `migration-guard` × تشغيلان (`push` + `pull_request`) |
| بوابات `PR-5` المحلية | `oxlint`/`tsc -b` صفر · `vitest` **158 ملفًا/1666 اختبارًا** · `npm run build` ناجح · خلفي: `build` 0/0، `Application` 2149/2149، `Api` 724 + 2 تتابعية |
| الهجرات | `PostgreSQL`: 63، صفر معلّقة · `SQLite`: **4 معلّقة** (`HeadAlertTaxonomy_Expand`، `HeadAlertOutbox_Create`، `HeadAlertTaxonomy_Backfill`، `HeadAlertTaxonomy_Contract`) |
| ما تبقّى من المراحل | **نافذة النشر** (د‑1…د‑4) ثم بقية `PR-5` (صفحة `/alerts`، `alertKinds.ts`، شارات الأيقونات، حذف `AlertsPanel` = `PR-5b`) + `PR-6` (تنظيف) |

**نطاق `PR-5` الحالي (المنفَّذ):** عقود `HeadAlertSummaryDto`/`HeadAlertDto`، معاينات `AlertRow`/`AlertsPanel`،
`AlertDetails` عند الطلب (`D12`)، ترقيم `pageSize=100` + «تحميل المزيد» + `stalled`، نطاق الشارة
(`user`/`received`) في `Dashboard` و`AccountPage`، شريط الإنابة بإعلان فشله، تعليم المقروء للرئيس/الشعبة
في «مستلمة» (`ج‑5`)، وحارس عقد صارم يُعلن (`ContractError`).

---

## 2. المرحلة أ — تحقق ما قبل الدمج

### أ‑0) فحص الحالة الحيّة (إلزامي، لا تتجاوز عنه)

```powershell
git fetch origin --prune
git status --porcelain                              # يجب أن يكون فارغًا
git log --oneline -3 origin/master
git rev-list --count origin/master..origin/feature/head-alerts-pr4-api   # 26 (أو أكثر)
git rev-list --count origin/feature/head-alerts-pr4-api..origin/master   # يجب أن يكون 0
gh pr view 54 --json state,isDraft,mergeable,mergeStateStatus,headRefOid,baseRefName
gh pr view 55 --json state,isDraft,mergeable,mergeStateStatus,headRefOid,baseRefName
gh pr checks 54
gh pr checks 55
```

**معيار القبول:** `#54` و`#55` كلاهما `OPEN` + `MERGEABLE` + **`isDraft=false`**، و`#54` بلا انحراف عن
`master` (`ahead_of_master ≥ 26` و`behind = 0`)، وفحوصهما خضراء (12/12 لكلٍّ على رأسه الحالي).

- **`isDraft=true` = شرط توقف قبل ب‑1:** `GitHub` يرفض دمج المسودّة، فلا بد من تحويلها إلى
  `Ready for review` (`gh pr ready 54`) **بموافقة المالك** — لأنه فعل ظاهر على الـ`PR` لا يغطيه هذا الدليل.
- **تحقّق أن الرأس المختبَر هو الرأس الحالي** (لا تعتمد على `gh pr checks` وحدها بعد التزام توثيقي):
  `gh run list --branch <branch> --limit 4 --json headSha,conclusion` وقارن `headSha` بـ`headRefOid`.

**إن ظهر انحراف (`behind > 0`) أو تعارض:** أعد دمج `origin/master` في فرع `PR-4`، حلّ التعارضات،
شغّل البوابات كاملة (§8)، ثم انتظر `CI` قبل المتابعة.

### أ‑1) تأكيد نطاق `#55` (لا خلفية ولا هجرات)

**القاعدة:** قِس مقابل **قاعدة الـ`PR` نفسها** (`baseRefName`)، لا مقابل `master` دائمًا: قبل دمج `#54`
قاعدة `#55` هي `feature/head-alerts-pr4-api`، فمقارنتها بـ`master` تُدخل التزامات `PR‑4` كاملة
(≈74 ملف `backend/`) وتُنتج **إنذارًا كاذبًا**.

```powershell
$base55 = gh pr view 55 --json baseRefName -q .baseRefName
git diff --stat "origin/${base55}..origin/feature/head-alerts-pr5-ui"
git diff --name-only "origin/${base55}..origin/feature/head-alerts-pr5-ui" |
  Select-String "backend/|Migrations|MigrationsPostgres"
```

**معيار القبول:** لا سطر ناتج من الأمر الثالث (لا ملفات `backend/` ولا هجرات)، و`--stat` لا يُظهر إلا
`frontend/` + `docs/` + `RUN_GUIDE.md`. أي ناتج ⇒ **توقف**: النطاق خرج عن «واجهة + توثيق» ويجب مراجعة
الفرق قبل الدمج. (بعد ب‑1 تصبح `$base55` = `master` تلقائيًا، فيصبح الأمر هو المقارنة المعتادة.)

### أ‑2) تأكيد أن الزوج هو وحدة النشر

- `#54` يغيّر عقد القوائم إلى `PagedResult<HeadAlertSummaryDto>`، و`#55` يستهلكه.
- نشر `#54` وحده = قوائم فارغة/نصوص مفقودة بصمت في الواجهة القديمة (وإن كان الحارس الجديد يجعلها
  ظاهرة كخطأ، فهو ما زال عطلًا). **لا استثناء لهذا البند.**

---

## 3. المرحلة ب — الدمج (بالترتيب، لا بالعكس)

### ب‑1) دمج `#54`

```powershell
gh pr merge 54 --merge --delete-branch
```

- استخدم **merge commit** (لا squash) ليبقى تاريخ الزوج واضحًا، على نمط الدمج القائم في المستودع.
- `--delete-branch` يحذف `feature/head-alerts-pr4-api` — وهي قاعدة `#55`. **`GitHub` لا يعيد التوجيه
  تلقائيًا كما كان مفترضًا سابقًا: بل يُغلق `#55` فورًا** (تحقُّق 2026-11-01: أُغلق بعد 3 ثوانٍ).
  الإصلاح في ب‑2، ولا يُعد هذا فشلًا ما دام الفرع ورأسه سليمين.

**معيار القبول:** `#54` أصبح `MERGED` (تحقّق: `gh pr view 54 --json state,mergeCommit`)، و`master` تقدّم.

### ب‑2) إعادة توجيه `#55` إلى `master` وانتظار فحوصه على القاعدة الجديدة

إن أُغلق `#55` (الحالة الفعلية): أعد إنشاء القاعدة مؤقتًا عند **التزام دمج `#54`** ثم أعد فتحه ثم أعد
توجيهه — `GitHub` يرفض `reopen`/`updateBase` على `PR` مغلق قاعدته غير موجودة:

```powershell
$merge54 = gh pr view 54 --json mergeCommit -q .mergeCommit.oid
git push origin "${merge54}:refs/heads/feature/head-alerts-pr4-api"   # قاعدة مؤقتة = رأس master الجديد
gh pr reopen 55
gh pr edit 55 --base master
git push origin --delete feature/head-alerts-pr4-api                  # تنظيف بعد التوجيه
git fetch origin --prune

gh pr view 55 --json state,baseRefName,mergeable,mergeStateStatus
gh pr checks 55                        # أو راقب التشغيل الجديد عبر gh run list --branch <head>
```

- **لا تعتمد على `gh pr checks` وحدها** لتحديد التشغيل الجديد: قارن `createdAt` / `databaseId`
  من `gh run list --branch feature/head-alerts-pr5-ui --limit 4` بأنه بعد إعادة التوجيه.
- تغيير القاعدة يُطلق تشغيل `pull_request` جديدًا. **لا تدمج قبل اخضرار الفحوص الستة على القاعدة الجديدة**؛
  هذا هو الفحص الذي يثبت سلامة الدمج مع `master` الجديد (لا تعتمد على تشغيل قديم).
- تحقّق أن `#55` ما زال يحتوي فقط: `frontend/` + `docs/` + `RUN_GUIDE.md` (أعد فحص أ‑1 بقاعدة `master` الآن).

### ب‑3) دمج `#55`

```powershell
gh pr merge 55 --merge --delete-branch
```

**معيار القبول:** `master` يحوي دمج `#54` ثم `#55` بالترتيب، و`CI` على رأس `master` أخضر **6/6** في
تشغيل `push` (أما «12/12» فهي لرأس الفرع قبل الدمج: ستة فحوص × `push`+`pull_request`) —
تحقّق: `gh run list --branch master --limit 2` ثم `gh run view <id> --json jobs`.

### ب‑4) ماذا لو فشل شيء أثناء الدمج؟

| العرض | التشخيص | الفعل |
|---|---|---|
| `#55` غير `MERGEABLE` بعد إعادة التوجيه | تعارض ناتج عن تقدّم `master` | أعد دمج `origin/master` في فرع `PR-5` محليًا، حلّ التعارض، شغّل البوابات، ادفع، انتظر `CI` |
| فحص `frontend` أحمر بعد إعادة التوجيه | فرق سلوكي في `master` لم يُغطَّ سابقًا | أصلح في `PR-5` (لا في `master`)، أضف/حدّث اختبارًا، أعد البوابات |
| فحص `backend` أحمر | نظريًا غير متوقع (`#55` لا يمسّ الخلفية) | تحقّق أن التشغيل يخصّ رأسًا قديمًا؛ إن تكرر، أعد تشغيل الفحص وسجّل الحالة |

**ممنوع:** دمج `#55` قبل `#54`، أو دفع أي إصلاح مباشرة إلى `master`.

---

## 4. المرحلة ج — تثبيت المرساة بعد الدمج (توثيق)

الخطة تلزم بأن تُثبَّت المرساة (`§0.3` في خطة الإصلاح) على رأس الدمج و`CI` الأخضر.

1. أنشئ فرعًا من `master`:
   ```powershell
   git switch master
   git pull --ff-only
   git switch -c docs/head-alerts-merge-anchor
   ```
2. حدّث في `docs/HEAD_ALERTS_PR4_REMEDIATION_PLAN.md`:
   - سطر **الالتزامات**: أضف هاش دمج `#54` ثم هاش دمج `#55` (وأي التزام توثيقي لاحق).
   - سطر **`CI`**: ثبّته على رأس `master` المدموج وبعدد `12/12`.
   - سطر **البوابات**: أبقِ الأرقام الفعلية من آخر تشغيل محلي (§8).
   - سطر **الشارة/القوائم** إن تغيّر سلوك ظاهر بعد الدمج.
3. إن كانت `docs/HEAD_ALERTS_RESTRUCTURE_PLAN.md` §15 تحتوي «غير مدموج» فحدّثها إلى «مدموج في `<hash>`».
4. `git commit` برسالة `docs(head-alerts): pin anchor to the merged PR-4/PR-5 heads` ثم:
   ```powershell
   git push -u origin docs/head-alerts-merge-anchor
   gh pr create --base master --title "docs(head-alerts): pin anchor to merged PR-4/PR-5" --body "..."
   ```
   وانتظر الفحوص ثم ادمج.

**معيار القبول:** لا عبارة «غير مدموج» تبقى لهذين الـ`PR`، والمرساة تطابق `git log` على `master`.

---

## 5. المرحلة د — نافذة النشر (أعلى مخاطرة)

> المرجع التفصيلي: `RUN_GUIDE.md` §9 — «اقتران نشر `PR-4` + `PR-5`»، «تطبيق هجرات قاعدة البيانات
> عند النشر»، «قائمة النشر الملزمة»، «هجرات بانتظار التطبيق (قائمة تراكمية)».

### د‑0) قبل النافذة (Pre-flight)

1. **نسخة احتياطية + استعادة مختبَرة** من قاعدة الإنتاج (SQLite: نسخ الملف؛ PostgreSQL: `pg_dump`).
2. سجّل الهجرات المطبّقة حيًا لكل سياق:
   ```sql
   SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY 1;
   ```
   وقارنها بهجرات الحزمة — **أي فرق غير متوقع = توقف وتحقيق**.
3. راجع القائمة التراكمية في `RUN_GUIDE.md` §9 واشطب فقط ما ثبت تطبيقه.
4. جهّز نافذة صيانة قصيرة (لا نشر متزامن لمخطط آخر، ولا `seed`/مهمة صيانة أثناءها).

### د‑1) بوابة `G2` الإلزامية (قبل `HeadAlertTaxonomy_Contract` على `SQLite`)

- المرجع: `AGENTS.md` (تذكير الهجرات المعلّقة) + `docs/audit/11-head-alerts-pr3-20261026.md`
  + `docs/audit/13-head-alerts-pr3-live-apply-20261026.md`.
- نفّذ **dry-run على نسخة إنتاجية مستعادة**: طبّق `Expand`/`Outbox_Create`/`Backfill`/`Contract`
  بالترتيب، ثم `PRAGMA integrity_check` و`PRAGMA foreign_key_check`، وفحص الفهارس وأعداد الصفوف، ثم
  إقلاع التطبيق وفحص دخان مصغّر.
- تأكّد أن **كل النسخ** تعمل بكود `PR‑2` (أو أحدث) وأنه لا توجد قيم `NULL`/`Kind` فارغة قبل `Contract`؛
  الهجرة ترفض التطبيق صراحةً عند أي مخالفة — وهذا سلوك مقصود لا خطأ.

**شرط التوقف:** أي فشل في `G2` ⇒ **لا تطبيق `Contract`** ولا نشر. عالج المخالفة على نسخة اختبار أولًا.

### د‑2) تطبيق هجرات `SQLite` الأربع — **بالترتيب الإلزامي**

من مجلد `backend\` وبسلسلة اتصال بيئة الخادم:

```powershell
dotnet ef database update HeadAlertTaxonomy_Expand      --context DocGeneratorDbContext
dotnet ef database update HeadAlertOutbox_Create        --context DocGeneratorDbContext
dotnet ef database update HeadAlertTaxonomy_Backfill    --context DocGeneratorDbContext
dotnet ef database update HeadAlertTaxonomy_Contract    --context DocGeneratorDbContext
```

- الترتيب ليس اختياريًا: `Contract` **أخيرًا دائمًا** (يشدّد `Kind`/`LastContentChangedAt` ويضيف القيود).
- `PostgreSQL`: **لا خطوة** (63 هجرة، صفر معلّقة). تحقّق فقط من عدم وجود فرق في `__EFMigrationsHistory`.
- **سياسة «صفر تحذيرات»** تسري على أوامر `dotnet ef` (تبني الخلفية): أي تحذير يفشلها.

**معيار القبول:** الأوامر الأربعة تنجح، والتحقق من `__EFMigrationsHistory` يطابق هجرات الحزمة للسياقين.

### د‑3) النشر الذرّي

1. انشر الخلفية (كود `master` المدموج).
2. انشر الواجهة **فورًا** في النافذة نفسها (لا فاصل زمني بينهما).
3. **التراجع = الزوج**: تراجع أحدهما وحده يكسر القوائم (عقد `PagedResult` مقابل المصفوفة القديمة).

> الإنتاج بـ`Database:AutoMigrate=false`: إن نسيت د‑2 فالخلفية **ترفض الإقلاع** (مقصود).

### د‑4) فحص الدخان بعد النافذة (إلزامي)

| الفحص | المتوقع |
|---|---|
| `GET /alerts` و`/alerts/sent` و`/alerts/by-delegation/{id}` | `{items,page,perPage,totalCount,totalPages}` |
| `POST /alerts` | `201` بجسم ملخص + `Location` |
| `PATCH /alerts/{id}/read` | `204` بلا جسم |
| شارة غير المقروء | المحامي `?scope=user`، الرئيس/الشعبة `?scope=received` — نفس الرقم في `Dashboard` و`AccountPage` |
| قائمة > 100 | «تحميل المزيد» يُلحق بلا تكرار ويختفي عند الاكتمال؛ الفشل يعطي «إعادة المحاولة» ويُبقي المعروض |
| إزاحة الترقيم | يظهر «تغيّرت القائمة أثناء التصفّح…» + «تحديث»، ويختفي «تحميل المزيد» (لا طلبات عقيمة) |
| **تعليم المقروء للرئيس/الشعبة** | زر «تمت القراءة» في «مستلمة» يُنقص شارة `received` وعدّاد الصف بواحد؛ **لا زر في «أرسلتها»** |
| **خرق العقد** | جسم غير مطابق (مصفوفة قديمة) ⇒ لافتة خطأ عربية + «إعادة المحاولة» في اللوحة **وشريط الإنابة** (لا صفحة فارغة) |
| رئيس/شعبة بلا فرع | حالة صريحة («حسابك غير مرتبط بفرع…») بلا طلبات ولا `403` متوقع |
| `GET /alerts/{id}` | `message` كامل، والقوائم لا تحمله |
| بصري 375px | لا تجاوز أفقي، والدرج/الشريط السفلي سليمان، والأزرار ≥ 44px |
| `GET /healthz` | `200` |

**شرط التراجع:** أي فشل في الفحص ⇒ تراجع للزوج (أو استعادة النسخة إذا لزم).

---

## 6. المرحلة هـ — `PR-5b`: بقية واجهة المرحلة 5 (وظيفية)

> **لماذا مرحلة منفصلة عن `PR-6`؟** قواعد تقسيم `PRs` في خطة إعادة الهيكلة §15 تنص أن `PR-6`
> «تنظيف بلا إضافات وظيفية». فإضافة صفحة جديدة + حذف كود قديم يجب أن تكون في تغيير مستقل (سطح تراجع
> واحد لكل تغيير).

### هـ‑0) الفرع والنطاق

```powershell
git switch master
git pull --ff-only
git switch -c feature/head-alerts-pr5b-alerts-page
```

- النطاق: `frontend/` + `docs/` فقط. **لا** تغيير خلفي ولا هجرات.
- اتبع قواعد `AGENTS.md`: `Mobile-first`، `min-h-11`، `useIsMobile` عند اختلاف جوهري،
  `aria-*` و`focus-visible`، `Intl.NumberFormat`، `…` بدل `...`، `tabular-nums`، و`truncate`/`min-w-0`.

### هـ‑1) البنود المطلوبة

| # | البند | تفصيل القبول |
|---|---|---|
| 1 | `alertKinds.ts` | كتالوج الرموز الدلالية للتسمية/التوجيه مع `fallback` للرمز المجهول (لا استنتاج من النص) |
| 2 | `AlertsPage` + `AlertList` | صفحة `/alerts` للمحامي ورئيس القسم/الشعبة، تبويبا «مستلمة/أرسلتها»، تصفية «نشط/الكل/المؤرشف»، ترقيم عبر `useAlertsFeed` نفسه |
| 3 | `AlertDetailsSheet` | عرض النص الكامل عبر `GET /alerts/{id}` عند الطلب فقط (نمط `AlertDetails` القائم)، حوار قابل للتمرير على الجوال بلا اقتصاص |
| 4 | `RecipientsModal` | نافذة مستلمي التنبيه في «أرسلتها» (حالة القراءة لكل مستلم) عبر العقد القائم فقط — إن لم يوجد عقد مستلمين فلا تُخترع نقطة نهاية هنا، بل يُسجَّل البند كقرار |
| 5 | مسار `/alerts` | في `App.tsx` + رابط من `Layout`/الشارات، مع `aria-current` وحالة تركيز |
| 6 | شارات صفوف الأيقونات | `HeadIconRow`/`LawyerIconRow` تقرأ عدّادات التنبيهات من المصادر القائمة (بلا استعلامات ثقيلة مكررة) |
| 7 | حذف `AlertsPanel` | **آخر خطوة** بعد أن تغطي الصفحة كل وظائف اللوحة (استيراداته، اختباراته، وأي مرجع في `Dashboard`) |

### هـ‑2) البوابات والقبول

```powershell
cd frontend
npx oxlint src
npx tsc -b
npx vitest run
npm run build
```

- اختبارات جديدة: تنقّل إلى `/alerts` لكل دور، فتح التفاصيل عند الطلب فقط، عدم تحميل النص الكامل في
  القوائم، حذف `AlertsPanel` لا يكسر `Dashboard`، وسلوك 375px (وجود/غياب عناصر لا الاثنان).
- **تحقّق بالطفر** لأي اختبار يثبت إصلاحًا سلوكيًا (أعد العيب ⇒ يفشل الاختبار).
- `CI` 12/12 على رأس الفرع، ثم `PR` إلى `master`.

---

## 7. المرحلة و — `PR-6`: التنظيف (بلا إضافات وظيفية)

> بنود ملزمة صراحةً في خطة إعادة الهيكلة §15 (مصدرها `D5`/`D6`/`D9`/`P3-12`) — لا تُترك مفتوحة.

### و‑1) البنود

1. **إزالة العدّادين من عقد التفاصيل:** حذف `RecipientCount`/`UnreadCount` من `HeadAlertDto`
   (ووسم «محتفظ مؤقتًا»)، وحذف الكتلة `retainedHeadAlertDetail` من
   `frontend/src/test/contracts/head-alert-contracts.json`، وتبسيط
   `DetailContract_MatchesManifest_PlusExplicitlyRetainedFields`، واستبدال
   `Detail_RetainsCounters_…_UntilPR6` باختبار يثبت غيابهما.
2. **حسم نطاق شارة الرئيس:** هل تبقى تجميعية (`scope=received`) أم تصير `user` مع `unreadOnly=true`؟
   عند التحوّل: حذف النطاق `received` من `AlertUnreadScope` والمتحكم والخدمة والمستودع + اختبار يثبت
   رفضه (`400`)، وتحديث `Dashboard`/`AccountPage`/`RUN_GUIDE.md` §9.
3. **إزالة الجسر الانتقالي** بعد أن يعيد استعلام §16.2 صفرًا خلال نافذة 14 يومًا:
   ```sql
   SELECT COUNT(*) FROM "HeadAlerts"
   WHERE "Kind" = 'legacy' AND "IsArchived" = 0
     AND ("DelegationId" IS NOT NULL OR "AppealId" IS NOT NULL OR "ReviewLetterId" IS NOT NULL
          OR "PublicEntityId" IS NOT NULL OR "ExecutionCircuitId" IS NOT NULL
          OR ("DocumentId" IS NOT NULL AND "TargetType" = 1
              AND "Message" NOT LIKE 'تنبيه من رئيس القسم:%'
              AND "Message" NOT LIKE 'تنبيه من رئيس الشعبة:%'));
   ```
   (إطفاء أي `Flag` أولًا ثم حذف الكود، مع إبقاء `legacy` للصفوف التاريخية غير القابلة للتصنيف.)
4. **مؤشر `cursor` جذريًا** لإزاحة `offset` (جذر `stalled`): مؤشر `(LastContentChangedAt, Id)` + ترقيم
   بلا إزاحة، مع إبقاء `stalled`/«تحديث» كشبكة أمان حتى إثبات الاستقرار، ثم إزالتهما إن لم يبقَ لهما معنى.
5. **تحققات نصية إلزامية (§15):**
   - لا `hasFullMessage`/`hasNextPage` في أي استجابة `PagedResult`.
   - لا تحديث لـ`CreatedAt` بعد الإنشاء (كتابة `DateTime.UtcNow` حصرًا).
   - لا فهرس فريد على `(DedupeKey, IsArchived)` بدل الجزئي.

### و‑2) البوابات

كما في §8 كاملة (خلفي + واجهي)، مع `dotnet test` للخلفية لأن هذا هو التغيير الخلفي الوحيد بعد `PR-4`.
إن أضفت هجرة (نحو فهرس جزئي) فأضفها إلى قائمة `RUN_GUIDE.md` §9 التراكمية **في نفس التغيير**.

---

## 8. مصفوفة البوابات (تُشغَّل قبل كل إعلان إنجاز)

| البوابة | الأمر | المتوقع |
|---|---|---|
| بناء الخلفية | `dotnet build backend/DocGenerator.sln --nologo -v q -warnaserror` | 0 تحذير/0 خطأ |
| اختبارات التطبيق | `dotnet test backend/tests/DocGenerator.Application.Tests/DocGenerator.Application.Tests.csproj --no-build --nologo -v q` | 2149/2149 (أو أكثر) |
| اختبارات `API` المتوازية | `dotnet test backend/tests/DocGenerator.Api.Tests/DocGenerator.Api.Tests.csproj --no-build --nologo -v q --filter "Category!=Sequential"` | 724 (أو أكثر) |
| اختبارات `API` التتابعية | نفس الأمر بـ`--filter "Category=Sequential"` | 2 |
| `lint` واجهة | `cd frontend; npx oxlint src` | صفر |
| أنواع واجهة | `npx tsc -b` | صفر |
| اختبارات واجهة | `npx vitest run` | 158 ملفًا/1666 اختبارًا (أو أكثر) |
| بناء واجهة | `npm run build` | ناجح |
| `CI` | `gh run list --branch <branch> --limit 4` | 12/12 `success` على رأس الفرع |

**قاعدة:** أي بوابة حمراء = المهمة غير منجزة؛ لا «نجاح جزئي»، ولا تعطيل اختبار لتجاوزه.

---

## 9. قرارات مفتوحة (تحتاج المالك قبل النشر)

| # | القرار | الأثر | الافتراضي المقترح |
|---|---|---|---|
| 1 | تثبيت سلوك `ج‑5` (تعليم المقروء للرئيس/الشعبة) | ظاهر للمستخدم في لوحة الرئيس | **الإبقاء** (تنفيذًا لبند §3.1/§196)، والتراجع بند واحد إن رغب المالك |
| 2 | نطاق شارة الرئيس لاحقًا (`received` أم `user`+`unreadOnly`) | يحدّد بقاء `AlertUnreadScope.Received` | يُحسم في `PR-6` بأرقام استخدام حقيقية بعد النشر |
| 3 | تاريخ نافذة النشر ومسؤول تنفيذ `G2` | يحدّد ترتيب المراحل هـ/و | النشر أولًا ثم `PR-5b` ثم `PR-6` |
| 4 | هل يُنفَّذ `RecipientsModal` بعقد جديد؟ | إن لم يوجد عقد مستلمين فهو تغيير خلفي = خارج نطاق `PR-5b` | يُسجَّل بندًا مستقلًا بعد التحقق من العقد |

---

## 10. مخاطر وشروط توقف (No-Go)

| الخطر | العلامة | الإجراء |
|---|---|---|
| نشر بلا هجرات `SQLite` | الخلفية لا تُقلع (`no such column`) | نفّذ د‑2 قبل النشر؛ لا تتجاوز |
| تطبيق `Contract` قبل `G2` | فشل الهجرة أو بيانات غير مهيّأة | **توقف**: أعد `G2` على نسخة مستعادة |
| نشر أحدهما بلا الآخر | قوائم فارغة/أخطاء عقد في الواجهة | تراجع للزوج |
| فرق انحراف غير مفسَّر في `__EFMigrationsHistory` | هجرة غير متوقعة | **توقف** وتحقيق قبل أي كتابة |
| اختبار «أخضر» بعد تعطيل تأكيد | ضمانة زائفة | أعد التأكيد؛ لا تُعدّل اختبارًا ليمرّ |
| نص عربي مُشوَّه في ملفات/تعليقات `PR` | `PowerShell` لا يقرأ/يكتب UTF‑8 بشكل موثوق | استخدم أدوات `edit`/`write`، و`node` لبناء JSON UTF‑8 (`gh api --input`) |
| `git push` يُرجع رمز خروج `1` في `PowerShell` رغم النجاح | مخرجات التقدّم على `stderr` | تحقّق من سطر `old..new  HEAD -> branch` |
| `#54` مسودّة عند بدء ب‑1 | `gh pr merge 54` يرفض الدمج | `gh pr ready 54` **بموافقة المالك**، ثم أعد أ‑0 |

---

## 11. سجل التنفيذ (يُعبأ في الجلسة المنفّذة)

| التاريخ | المرحلة | النتيجة | الهاش/الرابط |
|---|---|---|---|
| 2026-11-01 | أ‑0 | ✅ مطابق للقطة: `master` = `e0d8384`، `PR-4` ahead 26 / behind 0، شجرة العمل نظيفة، `#55` رأسه `648b7d7` و`CI` 12/12. **انحراف واحد:** `#54` مسودّة (`isDraft=true`) ⇒ يمنع ب‑1 حتى `Ready for review` | `3d38052` / `648b7d7` |
| 2026-11-01 | أ‑1 | ✅ لا خلفية ولا هجرات: مقابل قاعدة `#55` الفعلية (`feature/head-alerts-pr4-api`) = 28 `frontend/` + 3 `docs/` + `RUN_GUIDE.md`، صفر ملفات `backend/`. (أمر الدليل القديم كان يقيس مقابل `master` فيُظهر 74 ملف `backend/` — صُحّح ليقيس مقابل `baseRefName`) | — |
| 2026-11-01 | أ‑2 | ✅ الزوج وحدة واحدة: الخلفية تُرجع `HeadAlertSummaryPage`، والواجهة تقرأ `{items,page,perPage,totalCount}` بحارس صارم يُعلن `ContractError` عند الانحراف (لا فشل صامت). الهجرات الأربع المعلّقة موجودة على `master` ولا هجرة جديدة في الزوج | — |
| 2026-11-01 | أ (توثيق) | ✅ صُحّح أمر أ‑1 ليَقيس مقابل `baseRefName` (كان يُنتج 74 نتيجة `backend/` كاذبة)، وأُضيف `isDraft` كشرط قبول وكشرط توقف في §10، وعُبِّئ هذا السجل. `CI` أخضر **12/12** على رأس الفرع بعد التصحيح | `8d045d0` |
| 2026-11-01 | ب‑1 دمج `#54` | ✅ حُوّل `#54` إلى جاهز (`gh pr ready 54`) ثم دُمج بـ`--merge --delete-branch` (Merge commit، لا squash). **انحراف:** حذف القاعدة **أغلق `#55`** بدل إعادة توجيهه — عُولج فورًا في ب‑2 | `97a4a93` |
| 2026-11-01 | ب‑2 إعادة توجيه `#55` | ✅ أُعيد إنشاء `feature/head-alerts-pr4-api` عند `97a4a93` ← `gh pr reopen 55` ← `gh pr edit 55 --base master` ← حُذف الفرع المؤقت. النطاق بعد التوجيه: 28 `frontend/` + 3 `docs/` + `RUN_GUIDE.md`، صفر `backend/`. `CI` الجديد (تشغيل `41338240077`) 6/6 `success` على `31c1153` | `31c1153` |
| 2026-11-01 | ب‑3 دمج `#55` | ✅ دُمج بـ`--merge --delete-branch`. `CI` على `master` @ `4e421f4`: الفحوص الستة `success`، و`CI` على `97a4a93` (رأس `#54` المدموج) 6/6 أيضًا | `4e421f4` |
| 2026-11-01 | ج تثبيت المرساة | ✅ فرع `docs/head-alerts-merge-anchor` ← [#56](https://github.com/maarouf003-netizen/docgen/pull/56) (`CI` 12/12) ← **مدموج في `355462f`**. §0.3 محدَّثة (الفرع/الالتزامات/`CI`/`PR`ان/المتبقي)، وإشارات «محدَّث» في §0.1/§0.2، وترويسة خطة إعادة الهيكلة (§0) صارت «`PR-1`–`PR-5` مدموجة»، وتصحيح ب‑1/ب‑2 في هذا الدليل، وسجل §12 في خطة الإصلاح | `355462f` |
|  | د‑1 `G2` |  |  |
|  | د‑2 هجرات `SQLite` |  |  |
|  | د‑3 النشر |  |  |
|  | د‑4 فحص الدخان |  |  |
|  | هـ `PR-5b` |  |  |
|  | و `PR-6` |  |  |

---

## 12. مراجع

- `AGENTS.md` — القواعد الملزمة (الهجرات، الواجهة/الجوال، إرشادات الويب، حماية الفرع، سياسة الوثائق).
- `RUN_GUIDE.md` §9 — اقتران نشر `PR-4`+`PR-5`، تطبيق الهجرات، قائمة الهجرات التراكمية، فحص الانحراف.
- `docs/HEAD_ALERTS_PR4_REMEDIATION_PLAN.md` — §0.3 (المرساة) و§12 (سجل الإغلاقات `خ‑1`…`خ‑5`، `ج‑1`…`ج‑5`).
- `docs/HEAD_ALERTS_RESTRUCTURE_PLAN.md` — §3.1 (المشاكل المؤكدة)، §15 (المراحل وقواعد تقسيم `PRs`)، §16.2 (فحوص النشر).
- `docs/audit/11-head-alerts-pr3-20261026.md` و`docs/audit/13-head-alerts-pr3-live-apply-20261026.md` — `G2` والتطبيق الحي.
- `frontend/src/api/alerts.ts` + `frontend/src/api/contractError.ts` — حارس العقد الصارم (سلوك ما بعد `ج‑3`).
