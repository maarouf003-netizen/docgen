---
name: Release Gate (CI Parity)
description: Use before committing, pushing, or opening a PR. Reproduces the six CI gates locally (backend, frontend, guards, audit, backend-postgres, migration-guard), reports pass/fail per gate, and enforces branch/PR rules. استخدمها قبل أي دفع أو PR.
---

# بوابة الإصدار — مطابقة `CI`

## القاعدة الحاكمة

- لا دفع مباشر إلى `master`: فرع `feature/…` أو `refactor/…` أو `docs/…` ثم `PR`، ولا دمج قبل
  اخضرار البوابات الست.
- لا تتجاوز بوابة بـ `--no-verify` ولا بتعديل `allowlist`/`baseline` بلا مبرر وموافقة.
- شغّل البوابات على التغيير النهائي، لا على نسخة وسيطة.

## المتطلبات المسبقة

- `.NET SDK 10` و`dotnet-ef 10.0.10`
  (`dotnet tool install --global dotnet-ef --version 10.0.10`).
- `Node 24` + تبعيات واجهة كاملة: `cd frontend; npm ci --legacy-peer-deps` (بدونها تفشل
  `generate:api` و`vitest`).
- `PostgreSQL` محلي للبوابة الخامسة، أو صرّح بتخطّيها.
- الأوامر مكتوبة لـ `PowerShell` (بيئة العمل هنا). على `bash` استبدل `$env:X = '…'` بـ
  `export X='…'`.
- **إدارة الوقت**: `dotnet build` قد يستغرق دقائق و`Application.Tests` نحو ربع ساعة على جهاز
  محدود. شغّل الطويل في الخلفية وانتظر إشعار الانتهاء قبل التقرير؛ لا تعتبر انتهاء المهلة فشلًا.
- **لا تشغّل `vitest` و`dotnet test` معًا** على جهاز محدود: المهلات تُنتج فشلًا وهميًا. شغّلهما
  بالتتابع، وأعد الاختبار الفاشل منفردًا قبل الحكم عليه.

## الترتيب الموصى به (أسرع فشل أولًا)

1. الحرّاس (ثوانٍ) → 2. `oxlint`/`tsc` → 3. `vitest` → 4. `build` + `guard` → 5. الخلفية
(`build`/`format`/الاختبارات) → 6. `backend-postgres` → 7. `migration-guard`.

## 1) `backend`

```powershell
dotnet restore backend/DocGenerator.sln --locked-mode
dotnet build backend/DocGenerator.sln --no-restore --nologo -v q -warnaserror
dotnet format backend/DocGenerator.sln --verify-no-changes --no-restore
dotnet test backend/tests/DocGenerator.Architecture.Tests/DocGenerator.Architecture.Tests.csproj --no-build --nologo -v q
dotnet test backend/tests/DocGenerator.Application.Tests/DocGenerator.Application.Tests.csproj --no-build --nologo -v q
dotnet test backend/tests/DocGenerator.Api.Tests/DocGenerator.Api.Tests.csproj --no-build --nologo -v q --filter "Category!=Sequential"
dotnet test backend/tests/DocGenerator.Api.Tests/DocGenerator.Api.Tests.csproj --no-build --nologo -v q --filter "Category=Sequential"
```

- `-warnaserror`: تحذير = خطأ.
- `--locked-mode` يفشل إن تغيّرت التبعيات بلا `packages.lock.json` مطابق.
- لا تدمج اختبارات `Sequential` مع المتوازية في تشغيل واحد (حالة عامة مشتركة).

## 2) `frontend`

```powershell
cd frontend
npx oxlint src scripts
npx tsc -b
npx vitest run          # أو: npm run test:ci (4 عمّال) عند بطء الجهاز
npm run build
npm run generate:api
git diff --exit-code -- src/api/generated.ts
npm run guard           # حارس CSP على dist — بعد build فقط
cd ..
```

- `npm run build` يشمل `tsc -b`؛ تكراره مقصود لمطابقة `CI`.
- أي فرق في `generated.ts` = عقد `OpenAPI` غير محدّث. وقد يُظهر `git status` الملف «معدّلًا»
  بسبب `CRLF` فقط؛ احكم بـ `git diff` الفعلي لا بالحالة.

## 3) `guards`

```powershell
node scripts/subhead-guards.mjs
node scripts/determinism-guard.mjs
node scripts/migration-watchlist-guard.mjs
node scripts/migration-safety-guard.mjs
node scripts/secrets-guard.mjs
node scripts/postgres-suite-guard.mjs --self-test
```

- `determinism-guard`: يمنع `DateTime.Now`/`Today` و`Random` غير المحكوم.
- `secrets-guard`: يفشل على أي سرّ جديد خارج `scripts/secrets-baseline.json`. تحديث الأساس
  (`--update`) يحتاج مبررًا في التقرير، ولا يُستخدم لإخفاء سرّ حقيقي.

## 4) `audit`

```powershell
node scripts/dependency-audit.mjs --self-test
node scripts/dependency-audit.mjs
node scripts/npm-audit-guard.mjs --self-test
node scripts/npm-audit-guard.mjs
```

- `dependency-audit` يحتاج شبكة (`dotnet list package --vulnerable`)؛ إن تعذّر فصرّح بذلك.
- يفشل عند `High`/`Critical` غير موثّق في `scripts/npm-audit-allowlist.json`.

## 5) `backend-postgres`

```powershell
# اختياري: docker run --rm -d -p 5432:5432 -e POSTGRES_PASSWORD=postgres postgres:15
# لا تكتب كلمة المرور في المستودع — متغير بيئة محلي فقط (في حاوية CI هي postgres)
$env:PGPASSWORD = '<كلمة-مرور-Postgres-المحلية>'
$env:DOCGEN_TEST_POSTGRES  = 'Host=localhost;Port=5432;Database=docgen_test;Username=postgres;Password=' + $env:PGPASSWORD
$env:DOCGEN_REQUIRE_POSTGRES = '1'

dotnet test backend/tests/DocGenerator.Application.Tests/DocGenerator.Application.Tests.csproj `
  --nologo -v q --logger "trx;LogFileName=app-postgres.trx"
dotnet test backend/tests/DocGenerator.Api.Tests/DocGenerator.Api.Tests.csproj `
  --nologo -v q --filter "Category!=Sequential" --logger "trx;LogFileName=api-postgres.trx"
dotnet test backend/tests/DocGenerator.Api.Tests/DocGenerator.Api.Tests.csproj `
  --no-build --nologo -v q --filter "Category=Sequential"

node scripts/postgres-suite-guard.mjs   # يلتقط api-postgres.trx من TestResults تلقائيًا
```

- `DOCGEN_REQUIRE_POSTGRES=1` يجعل غياب `PostgreSQL` فشلًا لا تخطّيًا صامتًا.
- احذف `TestResults` القديمة قبل التشغيل، وإلا قرأ الحارس `trx` سابقًا:
  `Remove-Item -Recurse -Force backend/tests/*/TestResults -ErrorAction SilentlyContinue`
- عدد المتخطّى في `app-postgres.trx` يجب أن يكون **صفرًا**؛ تحقق منه بـ:

  ```powershell
  $trx = Get-ChildItem backend/tests/DocGenerator.Application.Tests/TestResults -Recurse -Filter app-postgres.trx |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1
  @(Select-String -Path $trx.FullName -Pattern 'outcome="NotExecuted"').Count
  ```

- `postgres-suite-guard` يلتقط `api-postgres.trx` تلقائيًا؛ إن مرّرتَ مسارًا فاجعله مسارًا فعليًا
  لا اسم ملف مجردًا.

## 6) `migration-guard`

```powershell
$env:POSTGRES_CONNECTION = 'Host=localhost;Port=5432;Database=docgen;Username=x;Password=x'
New-Item -ItemType Directory -Force artifacts/migrations | Out-Null

dotnet ef migrations has-pending-model-changes --context DocGeneratorPostgresDbContext `
  --project backend/src/DocGenerator.Infrastructure --startup-project backend/src/DocGenerator.Api

dotnet ef migrations script --context DocGeneratorPostgresDbContext `
  --project backend/src/DocGenerator.Infrastructure --startup-project backend/src/DocGenerator.Api `
  -o artifacts/migrations/pg.sql

if (Select-String -Path artifacts/migrations/pg.sql -Pattern 'datetime2' -Quiet) {
  throw "FORBIDDEN: 'datetime2' in Postgres DDL (FR-001)"
}
```

- نتائج مقبولة: `No changes have been made…` + بلا `datetime2`.
- إن أضفت هجرة: نفّذ مهارة `db-migration` أولًا (حرّاس القائمة والتسجيل في الوثيقتين).

## سياسة الفشل

- فشل بوابة = لا `commit` نهائي ولا `PR`. أصلح السبب لا الحارس.
- فشل بيئي (لا `PostgreSQL`، لا شبكة) يُصرَّح به؛ لا يُدّعى نجاح.
- اختبار فاشل تحت حمل ثقيل: أعده منفردًا قبل تصنيفه عطلًا حقيقيًا.

## قالب التقرير

| البوابة | الأمر المختصر | النتيجة |
|---|---|---|
| backend | `build -warnaserror` · `format` · الاختبارات | ✅/❌ |
| frontend | `oxlint` · `tsc -b` · `vitest` · `build` · `generate:api` · `guard` | ✅/❌ |
| guards | القائمة الستة | ✅/❌ |
| audit | `dependency-audit` · `npm-audit-guard` | ✅/❌/بلا شبكة |
| backend-postgres | `DOCGEN_REQUIRE_POSTGRES=1` + الحارس | ✅/❌/غير متاح |
| migration-guard | `has-pending-model-changes` + فحص `datetime2` | ✅/❌ |

وأضف: الفرع، رسالة `commit`، الهجرات الجديدة (إن وجدت) مع تنبيه
`dotnet ef database update --context DocGeneratorPostgresDbContext`، وما لم يُشغّل ولماذا.
