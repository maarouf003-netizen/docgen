# المساهمة في المشروع

> القواعد الملزمة الكاملة في [`AGENTS.md`](AGENTS.md). هذا الدليل العملي للدورة اليومية.

## الفرع والدمج

- الفرع الحي: **`master`** فقط؛ لا دفع مباشر إليه.
- ابدأ دائمًا من `origin/master` (فرع `master` المحلي قد يكون مأخوذًا في worktree آخر):
  ```bash
  git fetch origin
  git switch -c feat/<topic> origin/master
  ```
- `PR` واحد لكل بند/تغيير متماسك. لا دمج قبل **اخضرار البوابات الست**:
  `backend` · `frontend` · `guards` · `audit` · `backend-postgres` · `migration-guard`.
- **حدّ حجم الـ`PR`:** ≤ ~400 سطر منطقي و≤ 20 ملفًا. ما فوق ذلك يُقسَّم إلى مراحل، باستثناء
  الهجرات/الملفات المولَّدة/الوثائق الجماعية. الـ`PR` الأكبر يُطلب تقسيمه في المراجعة.
- رسائل الـ`commit` بصيغة Conventional (`feat|fix|refactor|test|docs|ci|chore(scope): …`)،
  وبالعنوان الإنجليزي كما في السجل الحالي.

## التحقق محليًا (إلزامي قبل الدفع)

```bash
# الخلفية — البناء وبوابة التنسيق
dotnet build backend/DocGenerator.sln --nologo -v q -warnaserror
dotnet format backend/DocGenerator.sln --verify-no-changes --no-restore

# الخلفية — الحلقة السريعة أثناء التطوير
dotnet test backend/tests/DocGenerator.Application.Tests/DocGenerator.Application.Tests.csproj --no-build --filter "Category=Unit"
dotnet test backend/tests/DocGenerator.Api.Tests/DocGenerator.Api.Tests.csproj --no-build --filter "Category=Unit"

# الخلفية — الكاملة قبل الدفع (Application ≈14–16 دقيقة)
dotnet test backend/tests/DocGenerator.Architecture.Tests/DocGenerator.Architecture.Tests.csproj --no-build
dotnet test backend/tests/DocGenerator.Application.Tests/DocGenerator.Application.Tests.csproj --no-build
dotnet test backend/tests/DocGenerator.Api.Tests/DocGenerator.Api.Tests.csproj --no-build --filter "Category!=Sequential"
dotnet test backend/tests/DocGenerator.Api.Tests/DocGenerator.Api.Tests.csproj --no-build --filter "Category=Sequential"
```

```bash
cd frontend
npx oxlint src scripts
npx tsc -b
npx vitest run
npm run build
npm run guard
```

```bash
# الحراس الخمسة
node scripts/subhead-guards.mjs
node scripts/determinism-guard.mjs
node scripts/migration-watchlist-guard.mjs
node scripts/migration-safety-guard.mjs
node scripts/secrets-guard.mjs
```

- أي تغيير سلوكي يحتاج اختبارًا، ويفضَّل **تحقق سلبي موثّق** (حقن الانحراف، إثبات الفشل، ثم الإرجاع).
- بعد تغيير واجهة: تحقّق بصريًا من الجوال (≈375px) بلا تجاوز أفقي، بجانب `vitest`/`build`.

## الهجرات

- كل هجرة جديدة تُسجَّل في `RUN_GUIDE.md` §9 **و**`AGENTS.md` **في نفس التغيير**.
- لا `--context DocGeneratorDbContext` (لا هجرات له). السياق الوحيد:
  `DocGeneratorPostgresDbContext`.
- تعديل هجرة مطبّقة ممنوع؛ أضف هجرة جديدة. العمليات الهدمية تحتاج استثناءً مبررًا في
  `scripts/migration-safety-allowlist.json`.
- عند النشر: `scripts/p0-07b-preflight.sql` قبل هجرة المبالغ، والترتيب في
  [`docs/runbooks/migrations.md`](docs/runbooks/migrations.md).

## الواجهة

- Mobile-first، بلا أبعاد ثابتة تُسبب تجاوزًا، جداول واسعة ⇒ بطاقات أو `overflow-x-auto`،
  أهداف لمس ≥44px، وتركيز مرئي، وقواعد `AGENTS.md` (الوصول/الحركة/الطباعة/الأرقام).
- حقول التواريخ **نص حر** بالصيغة المعتمدة (`1/8/2026`) — لا `type="date"`.

## الوثائق

- الحالة الحية في [`docs/PERFECTION_ROADMAP.md`](docs/PERFECTION_ROADMAP.md).
- كل تغيير سلوكي: لقطة مؤرخة في `docs/audit/` بالأرقام.
- قرار معماري: `ADR` مرقّم في `docs/adr/` ومربوط من [`docs/README.md`](docs/README.md).
- لقطات `audit` **لا تُحرَّر بعد التجميد**.

## المراجعة

استخدم قالب الـ`PR` ([`.github/pull_request_template.md`](.github/pull_request_template.md)):
ما الذي يُغلق، كيف تحققت، الهجرات، الأمان، أثر الواجهة/الجوال، والوثائق.
