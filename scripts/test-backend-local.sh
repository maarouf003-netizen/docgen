#!/usr/bin/env bash
# P1-08 §5.8 — مصفوفة الخلفية المحلية المتوازية على PostgreSQL.
#
# يشغّل Application وApi (المتوازية) في الوقت نفسه على قاعدة محلية، ثم Api
# التسلسلية وArchitecture، ويولّد كتلتَي «التحقق» من ملفَي trx.
#
# متطلبات: bash + dotnet + PostgreSQL محلي (يُفضّل WSL2/Docker مطابقًا لـCI).
#   export DOCGEN_TEST_POSTGRES="Host=127.0.0.1;Port=5432;Database=postgres;Username=postgres"
#   bash scripts/test-backend-local.sh
#
# ملاحظة: لا يستبدل بوابة CI؛ هو تكرار محلي أسرع. الفشل يُرجع رمز خروج غير صفري.
# شرط سعة: خادم PostgreSQL المحلي يحتاج max_connections ≥ 300 (عشرات قواعد
# الأصناف × تجميع الاتصالات)، وإلا فشل بـ`53300 too many clients`. البوابة
# الرسمية في CI تبقى تسلسلية (Application ثم Api) على خدمة max_connections=100.

set -u -o pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

: "${DOCGEN_TEST_POSTGRES:=Host=127.0.0.1;Port=5432;Database=postgres;Username=postgres}"
export DOCGEN_TEST_POSTGRES
export DOCGEN_REQUIRE_POSTGRES=1

APP="backend/tests/DocGenerator.Application.Tests/DocGenerator.Application.Tests.csproj"
API="backend/tests/DocGenerator.Api.Tests/DocGenerator.Api.Tests.csproj"
ARCH="backend/tests/DocGenerator.Architecture.Tests/DocGenerator.Architecture.Tests.csproj"
RESULTS="backend/tests"
APP_TRX="$RESULTS/DocGenerator.Application.Tests/TestResults/app-local.trx"
API_TRX="$RESULTS/DocGenerator.Api.Tests/TestResults/api-local.trx"

echo "== build =="
dotnet build backend/DocGenerator.sln --nologo -v q -warnaserror || exit 1

echo "== parallel: Application + Api (Category!=Sequential) =="
dotnet test "$APP" --no-build --nologo -v q \
  --logger "trx;LogFileName=app-local.trx" \
  --blame-hang --blame-hang-timeout 40m --blame-hang-dump-type none &
APP_PID=$!

dotnet test "$API" --no-build --nologo -v q \
  --filter "Category!=Sequential" \
  --logger "trx;LogFileName=api-local.trx" \
  --blame-hang --blame-hang-timeout 30m --blame-hang-dump-type none &
API_PID=$!

APP_STATUS=0; wait "$APP_PID" || APP_STATUS=$?
API_STATUS=0; wait "$API_PID" || API_STATUS=$?

echo "== sequential: Api (Category=Sequential) =="
SEQ_STATUS=0
dotnet test "$API" --no-build --nologo -v q --filter "Category=Sequential" || SEQ_STATUS=$?

echo "== architecture =="
ARCH_STATUS=0
dotnet test "$ARCH" --no-build --nologo -v q || ARCH_STATUS=$?

echo "== results =="
if [ -f "$APP_TRX" ]; then
  node scripts/test-report.mjs "$APP_TRX" --label "Application/PostgreSQL (local)"
fi
if [ -f "$API_TRX" ]; then
  node scripts/test-report.mjs "$API_TRX" --label "Api/PostgreSQL (local)"
fi

if [ "$APP_STATUS" -ne 0 ] || [ "$API_STATUS" -ne 0 ] || [ "$SEQ_STATUS" -ne 0 ] || [ "$ARCH_STATUS" -ne 0 ]; then
  echo "FAIL: app=$APP_STATUS api=$API_STATUS sequential=$SEQ_STATUS architecture=$ARCH_STATUS"
  exit 1
fi

echo "OK: all backend projects passed on PostgreSQL."
