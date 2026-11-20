---
name: Release Gate (CI Parity)
description: Use before committing, pushing, or opening a PR. Reproduce the six CI gates locally (backend, frontend, guards, audit, backend-postgres, migration-guard), report pass/fail per gate, and enforce the branch/PR rules.
---

# بوابة الإصدار — مطابقة `CI`

## القاعدة الحاكمة

- لا دفع مباشر إلى `master`. كل عمل على `feature/…` أو `refactor/…` أو `docs/…` ثم `PR`.
- لا دمج قبل اخضرار البوابات الست. لا تتجاوز بوابة بـ `--no-verify` أو بتعديل `allowlist`
  بلا مبرر مكتوب.
- نفّذ البوابات على التغيير نفسه قبل `commit` النهائي، وأعد تشغيل المتأثر فقط بعد الإصلاح.

## المتطلبات المسبقة

- `.NET SDK 10` و`dotnet-ef 10.0.10` (`dotnet tool install --global dotnet-ef --version 10.0.10`).
- `Node 24` + تبعيات الواجهة كاملة: `cd frontend; npm ci --legacy-peer-deps` (بدونها تفشل
  `npm run generate:api` و`vitest`).
- `PostgreSQL` محلي للبوابة الخامسة (أو تخطَّها بصراحة في التقرير).

## البوابات الست وأوامرها

### 1) `backend`

```powershell
dotnet restore backend/DocGenerator.sln --locked-mode
dotnet build backend/DocGenerator.sln --no-restore --nologo -v q -warnaserror
dotnet format backend/DocGenerator.sln --verify-no-changes --no-restore
dotnet test backend/tests/DocGenerator.Architecture.Tests/DocGenerator.Architecture.Tests.csproj --no-build --nologo -v q
dotnet test backend/tests/DocGenerator.Application.Tests/DocGenerator.Application.Tests.csproj --no-build --nologo -v q
dotnet test backend/tests/DocGenerator.Api.Tests/DocGenerator.Api.Tests.csproj --no-build --nologo -v q --filter "Category!=Sequential"
dotnet test backend/tests/DocGenerator.Api.Tests/DocGenerator.Api.Tests.csproj --no-build --nologo -v q --filter "Category=Sequential"
```

- `-warnaserror` إلزامي: تحذير = خطأ.
- لا تُشغّل الاختبارات المتوازية والمتسلسلة في عملية واحدة؛ اختبارات `Sequential` تعتمد على حالة عامة.

### 2) `frontend`

```powershell
cd frontend
npx oxlint src scripts
npx tsc -b
npx vitest run
npm run build
npm run generate:api
git diff --exit-code -- src/api/generated.ts
npm run guard
cd ..
```

- `npm run guard` = حارس `CSP` على `dist` بعد البناء، فلا تخطّ `build` قبله.
- أي فرق في `generated.ts` = عقد `OpenAPI` غير محدّث.

### 3) `guards`

```powershell
node scripts/subhead-guards.mjs
node scripts/determinism-guard.mjs
node scripts/migration-watchlist-guard.mjs
node scripts/migration-safety-guard.mjs
node scripts/secrets-guard.mjs
node scripts/postgres-suite-guard.mjs --self-test
```

- `determinism-guard`: يمنع `DateTime.Now`/`Today` و`Random` غير المحكوم.
- `secrets-guard`: لا تضع أسرارًا ولا مفاتيح في أي ملف — حتى في الأمثلة.

### 4) `audit`

```powershell
node scripts/dependency-audit.mjs --self-test
node scripts/dependency-audit.mjs
node scripts/npm-audit-guard.mjs --self-test
node scripts/npm-audit-guard.mjs
```

- يفشل عند `High`/`Critical` غير موثّق في `scripts/npm-audit-allowlist.json`.

### 5) `backend-postgres`

يتطلب قاعدة `PostgreSQL` متاحة محليًا (أو حاوية):

```powershell
$env:DOCGEN_TEST_POSTGRES = 'Host=localhost;Port=5432;Database=docgen_test;Username=postgres;Password=postgres'
$env:DOCGEN_REQUIRE_POSTGRES = '1'
dotnet test backend/tests/DocGenerator.Application.Tests/DocGenerator.Application.Tests.csproj --nologo -v q `
  --logger "trx;LogFileName=app-postgres.trx"
dotnet test backend/tests/DocGenerator.Api.Tests/DocGenerator.Api.Tests.csproj --nologo -v q `
  --filter "Category!=Sequential" --logger "trx;LogFileName=api-postgres.trx"
dotnet test backend/tests/DocGenerator.Api.Tests/DocGenerator.Api.Tests.csproj --no-build --nologo -v q `
  --filter "Category=Sequential"
node scripts/postgres-suite-guard.mjs "api-postgres.trx"
```

- `DOCGEN_REQUIRE_POSTGRES=1` يجعل غياب `PostgreSQL` فشلًا لا تخطّيًا صامتًا.
- عدد الاختبارات المتخطّاة في `trx` يجب أن يكون صفرًا.

### 6) `migration-guard`

```powershell
$env:POSTGRES_CONNECTION = 'Host=localhost;Port=5432;Database=docgen;Username=x;Password=x'
dotnet ef migrations has-pending-model-changes --context DocGeneratorPostgresDbContext `
  --project backend/src/DocGenerator.Infrastructure --startup-project backend/src/DocGenerator.Api
dotnet ef migrations script --context DocGeneratorPostgresDbContext `
  --project backend/src/DocGenerator.Infrastructure --startup-project backend/src/DocGenerator.Api `
  -o artifacts/migrations/pg.sql
Select-String -Path artifacts/migrations/pg.sql -Pattern 'datetime2' -Quiet
```

- أي نتيجة `True` لـ `datetime2` = فشل (`FR-001`: نوع خاص بـ `SQL Server`).
- إن أضفت هجرة: طبّق مهارة `db-migration` أولًا (حارس القائمة + `RUN_GUIDE` §9).

## سياسة الفشل

- فشل بوابة = لا `commit` نهائي ولا `PR`. أصلح السبب، لا الحارس.
- إن كان الفشل بيئيًا (لا `PostgreSQL` محليًا) فصرّح بذلك في التقرير ولا تدّعِ نجاح البوابة.
- لا تُعدّل `scripts/*-allowlist.json` أو `secrets-baseline.json` لتجاوز فشل بلا مبرر وموافقة.

## قالب التقرير

| البوابة | الأمر المختصر | النتيجة |
|---|---|---|
| backend | `dotnet build -warnaserror` + الاختبارات | ✅/❌ |
| frontend | `oxlint` · `tsc -b` · `vitest` · `build` · `generate:api` · `guard` | ✅/❌ |
| guards | القائمة الستة | ✅/❌ |
| audit | `dependency-audit` · `npm-audit-guard` | ✅/❌ |
| backend-postgres | `DOCGEN_REQUIRE_POSTGRES=1` + `postgres-suite-guard` | ✅/❌/غير متاح |
| migration-guard | `has-pending-model-changes` + فحص `datetime2` | ✅/❌ |

وأضف: الفرع، رسالة `commit`، الهجرات الجديدة (إن وجدت) مع تنبيه `dotnet ef database update
--context DocGeneratorPostgresDbContext`، وما لم يُشغّل ولماذا.
