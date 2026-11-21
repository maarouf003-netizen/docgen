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

> **قاعدة `P1-08` (‏ADR-0009):** كل اختبار قاعدة يعمل على `PostgreSQL` حقيقي. بدون
> `DOCGEN_TEST_POSTGRES` تُتخطّى اختبارات القاعدة **صراحةً** (`[PostgresFact]`)، فلا
> تعتبر تشغيلًا بلا المتغيّر دليلًا على خضرة الخلفية. عيّن `DOCGEN_REQUIRE_POSTGRES=1`
> عند لزوم الفشل الصريح بدل التخطي.

```bash
# قاعدة اختبار محلية مطابقة لـ CI (Linux/Container؛ على Windows يُفضَّل WSL2/Docker
# لتفادي حد الذاكرة المشتركة وفحص الملفات). الحاوية محلية ومؤقتة، فمصادقة trust كافية
# ولا تُخزَّن أي كلمة مرور في المستودع.
docker run --rm -d --name docgen-pg -e POSTGRES_HOST_AUTH_METHOD=trust -p 5432:5432 \
  postgres:16 -c fsync=off -c synchronous_commit=off

export DOCGEN_TEST_POSTGRES="Host=127.0.0.1;Port=5432;Database=postgres;Username=postgres"
export DOCGEN_REQUIRE_POSTGRES=1   # مفيد في CI/التحقق النهائي
```

```bash
# الخلفية — البناء وبوابة التنسيق
dotnet build backend/DocGenerator.sln --nologo -v q -warnaserror
dotnet format backend/DocGenerator.sln --verify-no-changes --no-restore

# أثناء التطوير: مرشَّح فقط (ثوانٍ) لاختبارات القاعدة على Postgres
dotnet test backend/tests/DocGenerator.Application.Tests/DocGenerator.Application.Tests.csproj \
  --no-build --filter "FullyQualifiedName~<Area>"

# قبل الدفع: مصفوفة كاملة واحدة (Application ≈19 دقيقة محليًا/أقل في CI)
dotnet test backend/tests/DocGenerator.Architecture.Tests/DocGenerator.Architecture.Tests.csproj --no-build
dotnet test backend/tests/DocGenerator.Application.Tests/DocGenerator.Application.Tests.csproj --no-build \
  --logger "trx;LogFileName=app-postgres.trx"
dotnet test backend/tests/DocGenerator.Api.Tests/DocGenerator.Api.Tests.csproj --no-build \
  --filter "Category!=Sequential" --logger "trx;LogFileName=api-postgres.trx"
dotnet test backend/tests/DocGenerator.Api.Tests/DocGenerator.Api.Tests.csproj --no-build \
  --filter "Category=Sequential"

# أرقام التحقق للقطة docs/audit (بدل النسخ اليدوي)
node scripts/test-report.mjs backend/tests/DocGenerator.Application.Tests/TestResults/app-postgres.trx
```

```bash
# أو: مصفوفة الخلفية المتوازية (Application + Api معًا) — يُفضَّل WSL2/Linux
# لتجنّب حد الذاكرة المشتركة على Windows. تفترض قاعدة مطابقة لـ CI أعلاه.
bash scripts/test-backend-local.sh
```


```bash
cd frontend
# P6-06: قفل الواجهة يتضمن openapi-typescript (peer TS ^5.x) — ثبّت بالعلم الصريح.
npm ci --legacy-peer-deps
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
