#!/usr/bin/env bash
# P5-06 â€” ط§ط³طھط¹ط§ط¯ط© ظ†ط³ط®ط© PostgreSQL (ظƒط§ظ…ظ„ط© ط£ظˆ ط¬ط¯ظˆظ„ ظˆط§ط­ط¯) ظ…ط¹ ط­ظˆط§ط¬ط² ط£ظ…ط§ظ† طµط±ظٹط­ط©.
#
# ط§ظ„ظ…طھط·ظ„ط¨ط§طھ: bash 4+طŒ ط£ط¯ظˆط§طھ ط¹ظ…ظٹظ„ PostgreSQL 16+ (pg_restore/psql)طŒ ط£ط¯ط§ط© SHA-256.
#
# ط³ظ„ط³ظ„ط© ط§ظ„ط§طھطµط§ظ„: ظ…ظ† POSTGRES_CONNECTION ط­طµط±ظ‹ط§ (ط§ظ„ظ‡ط¯ظپ)طŒ ظˆ--target-db ظٹط­ط¯ظ‘ط¯ ط§ظ„ظ‚ط§ط¹ط¯ط© طµط±ط§ط­ط©ظ‹.
#   export POSTGRES_CONNECTION='postgresql://<user>@db.example.test:5432/postgres?sslmode=require'
#   ./pg-restore.sh --file /srv/backups/docgen/docgen-....dump --target-db docgen_staging --yes
#
# ط­ظˆط§ط¬ط² ط§ظ„ط£ظ…ط§ظ† (ظƒظ„ظ‡ط§ ط¥ظ„ط²ط§ظ…ظٹط©):
# - ط¨طµظ…ط© SHA-256 طھظڈطھط­ظ‚ظ‘ظ‚ ظ‚ط¨ظ„ ط£ظٹ ط§طھطµط§ظ„ ظƒطھط§ط¨ط©طŒ ظˆظ‚ط±ط§ط،ط© ط§ظ„ط£ط±ط´ظٹظپ طھظڈط®طھط¨ط± ظ‚ط¨ظ„ ط§ظ„ط§ط³طھط¹ط§ط¯ط©.
# - ظ„ط§ ط§ط³طھط¹ط§ط¯ط© ط¨ظ„ط§ --yes.
# - ظ‡ط¯ظپ ظٹط´ط¨ظ‡ ط§ظ„ط¥ظ†طھط§ط¬ (ط§ط³ظ… ظ‚ط§ط¹ط¯ط©/ظ…ط¶ظٹظپطŒ ط£ظˆ ظ†ظپط³ ظ…طµط¯ط± ط§ظ„ظ†ط³ط®ط©) ظٹظڈط±ظپط¶ ط¨ظ„ط§ --allow-production.
# - ط§ط³طھط¹ط§ط¯ط© ظƒط§ظ…ظ„ط© طھطھط·ظ„ط¨ ظ‚ط§ط¹ط¯ط© ظپط§ط±ط؛ط© (ط£ظˆ --allow-nonempty) ظˆطھظڈط¬ط±ظ‰ ظپظٹ ظ…ط¹ط§ظ…ظ„ط© ظˆط§ط­ط¯ط©.
# - ط§ط³طھط¹ط§ط¯ط© ط¬ط¯ظˆظ„ ظˆط§ط­ط¯ = --table ظ…ط¹ --data-onlyط› ط§ظ„ط­ط°ظپ ط§ظ„ظ…ط³ط¨ظ‚ ظٹطھط·ظ„ط¨ --truncate-table طµط±ظٹط­ظ‹ط§.
# - ظپط­طµ ط¯ط®ط§ظ†ظٹ ط¨ط¹ط¯ ط§ظ„ط§ط³طھط¹ط§ط¯ط©: ط¹ط¯ط¯ ط§ظ„ط¬ط¯ط§ظˆظ„طŒ طµظپظˆظپ __EFMigrationsHistoryطŒ ط±ط£ط³ ط§ظ„ظ‡ط¬ط±ط©طŒ ظˆDocuments.
#
# ط±ظ…ط² ط§ظ„ط®ط±ظˆط¬: 0 ظ†ط¬ط§ط­طŒ 1 ظپط´ظ„طŒ 2 ط®ط·ط£ ط§ط³طھط®ط¯ط§ظ….
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=scripts/backup/backup-common.sh
. "$SCRIPT_DIR/backup-common.sh"

ARCHIVE=""
CHECKSUM_FILE=""
TARGET_DB=""
TABLE=""
YES=0
ALLOW_PRODUCTION=0
ALLOW_NONEMPTY=0
TRUNCATE_TABLE=0
DRY_RUN=0
SELF_TEST=0

usage() {
  cat <<'EOF'
Usage: pg-restore.sh --file ARCHIVE --target-db NAME [options]

Restores a PostgreSQL custom-format backup produced by pg-backup.sh into an
explicit target database. Verifies the checksum and archive readability first.

Required:
  --file PATH               Backup artifact (.dump, .dump.gpg, or .dump.enc)
  --target-db NAME          Explicit target database name on POSTGRES_CONNECTION host

Safety:
  --yes                     Required to perform any restore
  --allow-production        Required when the target database/host looks like production,
                            or when it is the same host+database the backup came from
  --allow-nonempty          Permit a full restore into a non-empty database (atomic;
                            conflicts roll back the whole restore)
  --table NAME              Partial restore of one table (pg_restore --data-only -t)
  --truncate-table          With --table: TRUNCATE the target table first (destructive)

Other:
  --checksum-file PATH      Default: <archive>.sha256
  --dry-run                 Validate and print the plan; perform no writes
  --self-test               Run offline guard/self-tests and exit
  -h, --help                Show this help

Environment:
  POSTGRES_CONNECTION       Required. Target host/credentials (URI or key=value)
  BACKUP_ENCRYPTION_KEY     Required only for encrypted artifacts
EOF
}

need_value() {
  if [ "$#" -lt 2 ]; then
    log "ERROR: $1 requires a value."
    exit 2
  fi
}

while [ "$#" -gt 0 ]; do
  case "$1" in
    --file)
      need_value "$@"
      ARCHIVE="$2"
      shift 2
      ;;
    --checksum-file)
      need_value "$@"
      CHECKSUM_FILE="$2"
      shift 2
      ;;
    --target-db)
      need_value "$@"
      TARGET_DB="$2"
      shift 2
      ;;
    --table)
      need_value "$@"
      TABLE="$2"
      shift 2
      ;;
    --yes)
      YES=1
      shift
      ;;
    --allow-production)
      ALLOW_PRODUCTION=1
      shift
      ;;
    --allow-nonempty)
      ALLOW_NONEMPTY=1
      shift
      ;;
    --truncate-table)
      TRUNCATE_TABLE=1
      shift
      ;;
    --dry-run)
      DRY_RUN=1
      shift
      ;;
    --self-test)
      SELF_TEST=1
      shift
      ;;
    -h | --help)
      usage
      exit 0
      ;;
    *)
      log "ERROR: unknown option '$1' (see --help)."
      exit 2
      ;;
  esac
done

validate_target_db() {
  local name="$1"
  [ -n "$name" ] || {
    log "ERROR: --target-db is required (an explicit database name)."
    return 1
  }
  [ "${#name}" -le 63 ] || {
    log "ERROR: --target-db is longer than 63 characters."
    return 1
  }
  [[ "$name" =~ ^[A-Za-z0-9_][A-Za-z0-9_.-]*$ ]] || {
    log "ERROR: --target-db contains unsupported characters (allowed: letters, digits, _, ., -)."
    return 1
  }
  case "$(lower "$name")" in
    template0 | template1)
      log "ERROR: refusing to target a template database."
      return 1
      ;;
  esac
  return 0
}

validate_table_name() {
  local name="$1"
  [[ "$name" =~ ^[A-Za-z_][A-Za-z0-9_]*(\.[A-Za-z_][A-Za-z0-9_]*)?$ ]] || {
    log "ERROR: --table must be a simple identifier or schema.identifier."
    return 1
  }
  return 0
}

quote_table_sql() {
  local name="$1" schema table
  if [[ "$name" == *.* ]]; then
    schema="${name%%.*}"
    table="${name#*.}"
  else
    schema="public"
    table="$name"
  fi
  printf '"%s"."%s"' "$schema" "$table"
}

# ظٹظ‚ط±ط£ ظ‚ظٹظ…ط© ظ†طµظٹط© ظ…ظ† metadata (JSON ظ…ظƒطھظˆط¨ ط¨ظˆط§ط³ط·ط© pg-backup.sh) ط¨ظ„ط§ jq.
metadata_value() {
  local file="$1" key="$2"
  sed -n 's/.*"'"$key"'"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' "$file" | head -n 1
}

psql_query() {
  local sql="$1"
  psql --no-password --no-psqlrc --set=ON_ERROR_STOP=1 \
    --tuples-only --no-align --dbname="$TARGET_DB" --command="$sql"
}

smoke_full_restore() {
  local archive_head="$1" tables history head documents_present documents_rows
  tables="$(psql_query "SELECT count(*) FROM information_schema.tables WHERE table_schema='public' AND table_type='BASE TABLE';")" ||
    die "Post-restore smoke query failed (table count)."
  tables="$(trim "$tables")"
  [ "$tables" -gt 0 ] || die "Post-restore smoke check failed: no tables in public schema."

  history="$(psql_query 'SELECT count(*) FROM public."__EFMigrationsHistory";')" ||
    die "Post-restore smoke query failed (__EFMigrationsHistory missing)."
  history="$(trim "$history")"
  [ "$history" -gt 0 ] || die "Post-restore smoke check failed: __EFMigrationsHistory is empty."

  head="$(psql_query 'SELECT "MigrationId" FROM public."__EFMigrationsHistory" ORDER BY "MigrationId" DESC LIMIT 1;')" ||
    die "Post-restore smoke query failed (migration head)."
  head="$(trim "$head")"
  if [ "$head" != "$archive_head" ]; then
    die "Post-restore smoke check failed: migration head '$head' != archive head '${archive_head:-<none>}'."
  fi

  documents_present="$(psql_query "SELECT to_regclass('public.\"Documents\"') IS NOT NULL;")" ||
    die "Post-restore smoke query failed (Documents table)."
  documents_present="$(trim "$documents_present")"
  [ "$documents_present" = "t" ] || die "Post-restore smoke check failed: Documents table is missing."

  documents_rows="$(psql_query 'SELECT count(*) FROM public."Documents";')" ||
    die "Post-restore smoke query failed (Documents row count)."
  documents_rows="$(trim "$documents_rows")"

  log "Smoke OK: tables=$tables history=$history head=$head documents=$documents_rows"
}

smoke_partial_restore() {
  local table="$1" quoted history head rows exists
  quoted="$(quote_table_sql "$table")"
  exists="$(psql_query "SELECT to_regclass('$quoted') IS NOT NULL;")" ||
    die "Post-restore smoke query failed (restored table)."
  exists="$(trim "$exists")"
  [ "$exists" = "t" ] || die "Post-restore smoke check failed: table '$table' is missing."

  rows="$(psql_query "SELECT count(*) FROM $quoted;")" ||
    die "Post-restore smoke query failed (row count for $table)."

  history="$(psql_query 'SELECT count(*) FROM public."__EFMigrationsHistory";')" ||
    die "Post-restore smoke query failed (__EFMigrationsHistory missing)."
  history="$(trim "$history")"
  [ "$history" -gt 0 ] || die "Partial restore target must be an initialized DocGen database (empty __EFMigrationsHistory)."

  head="$(psql_query 'SELECT "MigrationId" FROM public."__EFMigrationsHistory" ORDER BY "MigrationId" DESC LIMIT 1;')" ||
    die "Post-restore smoke query failed (migration head)."
  head="$(trim "$head")"
  log "Partial smoke OK: table=$table rows=$(trim "$rows") history=$history head=$head"
}

# ---------------------------------------------------------------------------
# Self-test (offline; no database, no PostgreSQL tools required)
# ---------------------------------------------------------------------------
SELF_TEST_FAILURES=0

self_assert_eq() {
  local label="$1" expected="$2" actual="$3"
  if [ "$expected" != "$actual" ]; then
    printf 'SELF-TEST FAIL: %s (expected %q, got %q)\n' "$label" "$expected" "$actual" >&2
    SELF_TEST_FAILURES=$((SELF_TEST_FAILURES + 1))
  fi
}

run_self_test() {
  local tmp desc meta
  tmp="$(mktemp -d)" || {
    printf 'SELF-TEST FAIL: cannot create a temp directory\n' >&2
    return 1
  }
  require_sha256_tool

  # Production heuristic truth table.
  if ! is_production_like db.example.test docgen_prod; then
    printf 'SELF-TEST FAIL: docgen_prod was not classified as production\n' >&2
    SELF_TEST_FAILURES=$((SELF_TEST_FAILURES + 1))
  fi
  if ! is_production_like db.prod.internal docgen; then
    printf 'SELF-TEST FAIL: prod host was not classified as production\n' >&2
    SELF_TEST_FAILURES=$((SELF_TEST_FAILURES + 1))
  fi
  if ! is_production_like db.example.test postgres; then
    printf 'SELF-TEST FAIL: default postgres database was not classified as production-like\n' >&2
    SELF_TEST_FAILURES=$((SELF_TEST_FAILURES + 1))
  fi
  if is_production_like staging.example.test docgen_staging; then
    printf 'SELF-TEST FAIL: staging was classified as production\n' >&2
    SELF_TEST_FAILURES=$((SELF_TEST_FAILURES + 1))
  fi

  # Confirmation gate: absent --yes must fail, explicit --yes must pass.
  if (require_confirmation 0) >/dev/null 2>&1; then
    printf 'SELF-TEST FAIL: restore proceeded without confirmation\n' >&2
    SELF_TEST_FAILURES=$((SELF_TEST_FAILURES + 1))
  fi
  if ! (require_confirmation 1) >/dev/null 2>&1; then
    printf 'SELF-TEST FAIL: explicit confirmation was rejected\n' >&2
    SELF_TEST_FAILURES=$((SELF_TEST_FAILURES + 1))
  fi

  # Target/table validation.
  validate_target_db docgen_staging || {
    printf 'SELF-TEST FAIL: valid target database rejected\n' >&2
    SELF_TEST_FAILURES=$((SELF_TEST_FAILURES + 1))
  }
  if (validate_target_db 'bad name;drop') >/dev/null 2>&1; then
    printf 'SELF-TEST FAIL: invalid target database accepted\n' >&2
    SELF_TEST_FAILURES=$((SELF_TEST_FAILURES + 1))
  fi
  if (validate_target_db template0) >/dev/null 2>&1; then
    printf 'SELF-TEST FAIL: template0 target accepted\n' >&2
    SELF_TEST_FAILURES=$((SELF_TEST_FAILURES + 1))
  fi
  if (validate_table_name 'Documents; DROP TABLE x') >/dev/null 2>&1; then
    printf 'SELF-TEST FAIL: unsafe table name accepted\n' >&2
    SELF_TEST_FAILURES=$((SELF_TEST_FAILURES + 1))
  fi
  self_assert_eq "quoted public table" '"public"."Documents"' "$(quote_table_sql Documents)"
  self_assert_eq "quoted schema table" '"audit"."Log"' "$(quote_table_sql audit.Log)"

  # Metadata extraction (used for source-overwrite guard).
  meta="$tmp/meta.json"
  cat >"$meta" <<'EOF'
{
  "source": {
    "host": "db.example.test",
    "database": "docgen"
  }
}
EOF
  self_assert_eq "metadata host" "db.example.test" "$(metadata_value "$meta" host)"
  self_assert_eq "metadata database" "docgen" "$(metadata_value "$meta" database)"
  self_assert_eq "metadata missing key" "" "$(metadata_value "$meta" absent)"

  # Checksum round-trip: valid passes, corruption fails, missing checksum fails.
  printf 'backup-bytes' >"$tmp/sample.dump"
  write_checksum "$tmp/sample.dump" "$tmp/sample.dump.sha256" >/dev/null
  if ! verify_checksum "$tmp/sample.dump" "$tmp/sample.dump.sha256"; then
    printf 'SELF-TEST FAIL: valid checksum rejected\n' >&2
    SELF_TEST_FAILURES=$((SELF_TEST_FAILURES + 1))
  fi
  printf 'corrupt' >>"$tmp/sample.dump"
  if (verify_checksum "$tmp/sample.dump" "$tmp/sample.dump.sha256") >/dev/null 2>&1; then
    printf 'SELF-TEST FAIL: corrupted artifact passed checksum verification\n' >&2
    SELF_TEST_FAILURES=$((SELF_TEST_FAILURES + 1))
  fi
  if (verify_checksum "$tmp/sample.dump" "$tmp/absent.sha256") >/dev/null 2>&1; then
    printf 'SELF-TEST FAIL: missing checksum file was accepted\n' >&2
    SELF_TEST_FAILURES=$((SELF_TEST_FAILURES + 1))
  fi

  # Encryption suffix detection.
  self_assert_eq "gpg detection" "gpg" "$(artifact_encryption_mode x.dump.gpg)"
  self_assert_eq "openssl detection" "openssl" "$(artifact_encryption_mode x.dump.enc)"
  self_assert_eq "plain detection" "none" "$(artifact_encryption_mode x.dump)"

  rm -rf -- "$tmp"
  if [ "$SELF_TEST_FAILURES" -ne 0 ]; then
    printf 'pg-restore.sh self-test: FAILED (%d)\n' "$SELF_TEST_FAILURES" >&2
    return 1
  fi
  printf 'pg-restore.sh self-test: OK\n'
  return 0
}

if [ "$SELF_TEST" -eq 1 ]; then
  run_self_test || exit 1
  exit 0
fi

# ---------------------------------------------------------------------------
# Main flow
# ---------------------------------------------------------------------------
[ -n "$ARCHIVE" ] || {
  log "ERROR: --file is required."
  exit 2
}
validate_target_db "$TARGET_DB" || exit 2
if [ -n "$TABLE" ]; then
  validate_table_name "$TABLE" || exit 2
fi
if [ "$TRUNCATE_TABLE" -eq 1 ] && [ -z "$TABLE" ]; then
  log "ERROR: --truncate-table requires --table."
  exit 2
fi
[ -f "$ARCHIVE" ] || die "Backup artifact not found: $ARCHIVE"
if [ -z "$CHECKSUM_FILE" ]; then
  CHECKSUM_FILE="$ARCHIVE.sha256"
fi
[ -f "$CHECKSUM_FILE" ] || die "Checksum file not found: $CHECKSUM_FILE (refusing to restore an unverified backup)."

load_connection
PG_DB="$TARGET_DB"
export PGDATABASE
log "Restore target: $(redacted_target)"
log "Archive: $(basename "$ARCHIVE")"
require_pg_tools pg_restore psql
require_sha256_tool

# ط§ظ„طھط­ظ‚ظ‚ ظ…ظ† ط§ظ„ط¨طµظ…ط© ظˆط§ظ„ط£ط±ط´ظٹظپ ظ‚ط¨ظ„ ط£ظٹ ط§طھطµط§ظ„ ظƒطھط§ط¨ط©.
verify_checksum "$ARCHIVE" "$CHECKSUM_FILE" || die "Checksum verification failed; refusing to restore."

ENC_MODE="$(artifact_encryption_mode "$ARCHIVE")"
if [ "$ENC_MODE" != "none" ] && [ -z "${BACKUP_ENCRYPTION_KEY:-}" ]; then
  die "Artifact is encrypted ($ENC_MODE) but BACKUP_ENCRYPTION_KEY is not set."
fi
if ! decrypt_archive "$ARCHIVE" "$ENC_MODE" | pg_restore --list - >/dev/null 2>&1; then
  die "Archive is not readable by pg_restore (corrupt, wrong key, or not a custom-format dump)."
fi
ARCHIVE_ROWS="$(decrypt_archive "$ARCHIVE" "$ENC_MODE" | pg_restore --data-only --table=__EFMigrationsHistory --file=- - 2>/dev/null | extract_migration_rows || true)"
ARCHIVE_MIGRATION_COUNT="$(printf '%s\n' "$ARCHIVE_ROWS" | migration_count)"
ARCHIVE_MIGRATION_HEAD="$(printf '%s\n' "$ARCHIVE_ROWS" | migration_head)"
if [ "$ARCHIVE_MIGRATION_COUNT" -eq 0 ]; then
  warn "Archive contains no __EFMigrationsHistory rows; a full-restore smoke check will fail."
else
  log "Archive migrations: count=$ARCHIVE_MIGRATION_COUNT head=$ARCHIVE_MIGRATION_HEAD"
fi

# ط­ط§ط±ط³ ط§ظ„ط¥ظ†طھط§ط¬: ط§ظ„ط§ط³ظ…/ط§ظ„ظ…ط¶ظٹظپ + ظ…ط·ط§ط¨ظ‚ط© ظ…طµط¯ط± ط§ظ„ظ†ط³ط®ط© (metadata).
assert_safe_target "$PG_HOST" "$TARGET_DB" "$ALLOW_PRODUCTION"
METADATA="$ARCHIVE.meta.json"
if [ -f "$METADATA" ]; then
  SOURCE_HOST="$(metadata_value "$METADATA" host | tr '[:upper:]' '[:lower:]')"
  SOURCE_DB="$(metadata_value "$METADATA" database)"
  TARGET_HOST_LOWER="$(lower "$PG_HOST")"
  if [ -n "$SOURCE_HOST" ] && [ "$SOURCE_HOST" = "$TARGET_HOST_LOWER" ] && [ "$SOURCE_DB" = "$TARGET_DB" ]; then
    if [ "$ALLOW_PRODUCTION" -ne 1 ]; then
      die "Target is the same host+database the backup came from ($TARGET_DB@$PG_HOST). Refusing to overwrite the source without --allow-production."
    fi
    warn "Restoring over the backup source ($TARGET_DB@$PG_HOST) because --allow-production was supplied."
  fi
else
  warn "Metadata file not found ($METADATA); source-overwrite detection is limited to name/host heuristics."
fi

# ظپط­طµ ط§ظ„ظ‡ط¯ظپ ظ‚ط¨ظ„ ط£ظٹ ظƒطھط§ط¨ط©.
CURRENT_DB="$(psql_query 'SELECT current_database();')" || die "Cannot connect to target database '$TARGET_DB'."
CURRENT_DB="$(trim "$CURRENT_DB")"
[ "$CURRENT_DB" = "$TARGET_DB" ] || die "Connected database '$CURRENT_DB' is not the requested target '$TARGET_DB'."

TARGET_TABLES="$(psql_query "SELECT count(*) FROM information_schema.tables WHERE table_schema='public' AND table_type='BASE TABLE';")" ||
  die "Target preflight query failed."
TARGET_TABLES="$(trim "$TARGET_TABLES")"

if [ -n "$TABLE" ]; then
  QUOTED_TABLE="$(quote_table_sql "$TABLE")"
  TABLE_PRESENT="$(psql_query "SELECT to_regclass('$QUOTED_TABLE') IS NOT NULL;")" ||
    die "Target preflight query failed (table existence)."
  TABLE_PRESENT="$(trim "$TABLE_PRESENT")"
  [ "$TABLE_PRESENT" = "t" ] || die "Partial-restore target table '$TABLE' does not exist (create the schema first or use a full restore)."
  log "Partial restore: table=$TABLE target_tables=$TARGET_TABLES truncate=$TRUNCATE_TABLE"
else
  if [ "$TARGET_TABLES" -gt 0 ] && [ "$ALLOW_NONEMPTY" -ne 1 ]; then
    die "Target database '$TARGET_DB' is not empty ($TARGET_TABLES tables). Full restore requires an empty database or --allow-nonempty."
  fi
  log "Full restore: target_tables=$TARGET_TABLES allow_nonempty=$ALLOW_NONEMPTY"
fi

if [ "$DRY_RUN" -eq 1 ]; then
  log "DRY-RUN: no restore will be performed."
  log "Planned action: $([ -n "$TABLE" ] && printf 'partial data-only restore of %s' "$TABLE" || printf 'full restore') into $TARGET_DB@$PG_HOST"
  [ "$TRUNCATE_TABLE" -eq 1 ] && log "Planned pre-step: TRUNCATE $(quote_table_sql "$TABLE")"
  log "Checksum and archive readability verified; dry-run exiting 0."
  exit 0
fi

require_confirmation "$YES"

RESTORE_OPTS=(--exit-on-error --single-transaction --no-owner --no-privileges --dbname="$TARGET_DB")
if [ -n "$TABLE" ]; then
  RESTORE_OPTS+=(--data-only --table="$TABLE")
fi

if [ "$TRUNCATE_TABLE" -eq 1 ]; then
  log "Truncating target table $(quote_table_sql "$TABLE") as explicitly requested..."
  psql_query "TRUNCATE TABLE $(quote_table_sql "$TABLE");" >/dev/null ||
    die "Failed to truncate table '$TABLE'; restore aborted."
fi

log "Restoring (single transaction, no owner/privilege replay)..."
if [ "$ENC_MODE" = "none" ]; then
  if ! pg_restore "${RESTORE_OPTS[@]}" "$ARCHIVE"; then
    die "pg_restore failed; the single transaction was rolled back (no partial restore)."
  fi
else
  if ! decrypt_archive "$ARCHIVE" "$ENC_MODE" | pg_restore "${RESTORE_OPTS[@]}" -; then
    die "pg_restore failed; the single transaction was rolled back (no partial restore)."
  fi
fi

if [ -n "$TABLE" ]; then
  smoke_partial_restore "$TABLE"
else
  smoke_full_restore "$ARCHIVE_MIGRATION_HEAD"
fi

log "Restore completed successfully."
