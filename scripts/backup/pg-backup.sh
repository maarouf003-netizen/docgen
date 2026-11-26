#!/usr/bin/env bash
# P5-06 â€” ظ†ط³ط®ط© ط§ط­طھظٹط§ط·ظٹط© ظٹظˆظ…ظٹط© ظ„ظ‚ط§ط¹ط¯ط© PostgreSQL (طµظٹط؛ط© custom + ط¨طµظ…ط© SHA-256 + طھط´ظپظٹط± ط§ط®طھظٹط§ط±ظٹ).
#
# ط§ظ„ظ…طھط·ظ„ط¨ط§طھ: bash 4+طŒ ط£ط¯ظˆط§طھ ط¹ظ…ظٹظ„ PostgreSQL 16+ (pg_dump/pg_restore)طŒ ظˆط£ط¯ط§ط© SHA-256.
#
# ط³ظ„ط³ظ„ط© ط§ظ„ط§طھطµط§ظ„: ظ…ظ† ظ…طھط؛ظٹط± ط§ظ„ط¨ظٹط¦ط© POSTGRES_CONNECTION ط­طµط±ظ‹ط§ (URI ط£ظˆ Npgsql ط£ظˆ libpq).
#   export POSTGRES_CONNECTION='postgresql://<user>@db.example.test:5432/docgen?sslmode=require'
# ظ„ط§ طھظڈظƒطھط¨ ط£ظٹ ظƒظ„ظ…ط© ظ…ط±ظˆط± ظپظٹ ظ‡ط°ط§ ط§ظ„ظ…ظ„ظپطŒ ظˆظ„ط§ طھظڈط·ط¨ط¹ ظپظٹ ط§ظ„ط³ط¬ظ„.
#
# ط§ظ„طھط´ظپظٹط± (ط§ط®طھظٹط§ط±ظٹ): ط¥ظ† ط¶ظڈط¨ط· BACKUP_ENCRYPTION_KEY ظٹظڈط´ظپظژظ‘ط± ط§ظ„ظ…ظ„ظپ ط¨ظ€ gpg (AES256) ط¥ظ† ظˆظڈط¬ط¯طŒ
# ظˆط¥ظ„ط§ openssl (AES-256-CBC/PBKDF2طŒ 200000 طھظƒط±ط§ط±). ط§ظ„ظ…ظپطھط§ط­ ظ„ط§ ظٹط¸ظ‡ط± ظپظٹ ظˆط³ط§ط¦ط· ط§ظ„ط£ظˆط§ظ…ط±.
#
# ط£ظ…ط«ظ„ط©:
#   ./pg-backup.sh --dry-run
#   ./pg-backup.sh --output-dir /srv/backups/docgen --retention-days 30 --compress-level 6
#
# ط±ظ…ط² ط§ظ„ط®ط±ظˆط¬: 0 ظ†ط¬ط§ط­طŒ 1 ظپط´ظ„طŒ 2 ط®ط·ط£ ط§ط³طھط®ط¯ط§ظ….
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=scripts/backup/backup-common.sh
. "$SCRIPT_DIR/backup-common.sh"

OUTPUT_DIR="${BACKUP_OUTPUT_DIR:-${HOME:-$PWD}/docgen-backups}"
RETENTION_DAYS="${BACKUP_RETENTION_DAYS:-30}"
COMPRESS_LEVEL="${BACKUP_COMPRESS_LEVEL:-6}"
SCHEMA="public"
DRY_RUN=0
SELF_TEST=0

usage() {
  cat <<'EOF'
Usage: pg-backup.sh [options]

Creates a PostgreSQL custom-format backup of POSTGRES_CONNECTION, verifies that
pg_restore can read it, writes a SHA-256 checksum, optional metadata, and prunes
expired artifacts. Secrets are never printed.

Options:
  -o, --output-dir DIR       Backup directory (default: $BACKUP_OUTPUT_DIR or ~/docgen-backups)
      --retention-days N     Delete artifacts older than N days; 0 keeps all (default: 30)
      --compress-level N     pg_dump compression 0-9 (default: 6)
      --all-schemas          Dump every schema (old behaviour). Default: app schema 'public'
                             only, so the artifact is portable to vanilla PostgreSQL and
                             does not capture Supabase platform internals.
      --dry-run              Validate connection/tools and print the plan; write nothing
      --self-test            Run offline parser/retention self-tests and exit
  -h, --help                 Show this help

Environment:
  POSTGRES_CONNECTION      Required. postgres:// URI or key=value connection string
  BACKUP_ENCRYPTION_KEY    Optional. Encrypts the artifact (gpg preferred, else openssl)
  BACKUP_OUTPUT_DIR        Default output directory
  BACKUP_RETENTION_DAYS    Default retention days
  BACKUP_COMPRESS_LEVEL    Default compression level
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
    -o | --output-dir)
      need_value "$@"
      OUTPUT_DIR="$2"
      shift 2
      ;;
    --retention-days)
      need_value "$@"
      RETENTION_DAYS="$2"
      shift 2
      ;;
    --compress-level)
      need_value "$@"
      COMPRESS_LEVEL="$2"
      shift 2
      ;;
    --all-schemas)
      SCHEMA=""
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

case "$COMPRESS_LEVEL" in
  '' | *[!0-9]*) log "ERROR: --compress-level must be an integer 0-9."; exit 2 ;;
esac
if [ "$COMPRESS_LEVEL" -gt 9 ]; then
  log "ERROR: --compress-level must be an integer 0-9."
  exit 2
fi
case "$RETENTION_DAYS" in
  '' | *[!0-9]*) log "ERROR: --retention-days must be a non-negative integer."; exit 2 ;;
esac
[ -n "$OUTPUT_DIR" ] || {
  log "ERROR: --output-dir must not be empty."
  exit 2
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

self_assert_true() {
  local label="$1"
  shift
  if ! "$@" >/dev/null 2>&1; then
    printf 'SELF-TEST FAIL: %s\n' "$label" >&2
    SELF_TEST_FAILURES=$((SELF_TEST_FAILURES + 1))
  fi
}

run_self_test() {
  local tmp desc uri npgsql libpq
  tmp="$(mktemp -d)" || {
    printf 'SELF-TEST FAIL: cannot create a temp directory\n' >&2
    return 1
  }

  self_assert_eq "artifact name" "docgen-20260102T030405Z.dump" "$(artifact_base 20260102T030405Z)"
  self_assert_eq "gpg suffix" ".gpg" "$(artifact_suffix gpg)"
  self_assert_eq "openssl suffix" ".enc" "$(artifact_suffix openssl)"
  self_assert_eq "plain suffix" "" "$(artifact_suffix none)"

  # URI: percent-encoded user/password, IPv4 host, sslmode.
  uri='postgresql://backup%40user:p%40ss%3Aword@db.example.test:6543/docgen?sslmode=require&application_name=x'
  POSTGRES_CONNECTION="$uri"
  load_connection
  self_assert_eq "URI host" "db.example.test" "$PG_HOST"
  self_assert_eq "URI port" "6543" "$PG_PORT"
  self_assert_eq "URI database" "docgen" "$PG_DB"
  self_assert_eq "URI user" "backup@user" "$PG_USER"
  self_assert_eq "URI password decoding" "p@ss:word" "$PGPASSWORD"
  self_assert_eq "URI sslmode" "require" "$PG_SSL"
  desc="$(redacted_target)"
  case "$desc" in
    *'p@ss'* | *'s3mi'*) self_assert_eq "redaction hides password" "hidden" "leaked" ;;
  esac
  self_assert_true "redaction marker" grep -q 'password=\*\*\*' <<<"$desc"

  # Npgsql: quoted password with a semicolon, spaced keys.
  npgsql='Host=db.example.test;Port=5432;Database=docgen;Username=docgen_app;Password="s3mi;colon";SSL Mode=Require'
  POSTGRES_CONNECTION="$npgsql"
  load_connection
  self_assert_eq "Npgsql password with semicolon" "s3mi;colon" "$PGPASSWORD"
  self_assert_eq "Npgsql ssl mode" "require" "$PG_SSL"
  self_assert_eq "Npgsql default port" "5432" "$PG_PORT"

  # libpq: single-quoted value with a space.
  libpq="host=db.example.test port=5433 dbname=docgen user=docgen_app password='a b' sslmode=verify-full"
  POSTGRES_CONNECTION="$libpq"
  load_connection
  self_assert_eq "libpq password with space" "a b" "$PGPASSWORD"
  self_assert_eq "libpq port" "5433" "$PG_PORT"
  self_assert_eq "libpq verify-full" "verify-full" "$PG_SSL"

  # Missing/blank connection must fail closed.
  if (
    unset POSTGRES_CONNECTION
    load_connection
  ) >/dev/null 2>&1; then
    printf 'SELF-TEST FAIL: missing POSTGRES_CONNECTION was accepted\n' >&2
    SELF_TEST_FAILURES=$((SELF_TEST_FAILURES + 1))
  fi

  # Production-like detection truth table.
  self_assert_true "prod db" is_production_like db.example.test docgen_prod
  self_assert_true "production db" is_production_like db.example.test production
  self_assert_true "postgres default db" is_production_like db.example.test postgres
  self_assert_true "prod host" is_production_like db.prod.internal docgen
  self_assert_true "live db suffix" is_production_like db.example.test docgen-live
  if is_production_like staging.example.test docgen_staging; then
    printf 'SELF-TEST FAIL: staging was classified as production\n' >&2
    SELF_TEST_FAILURES=$((SELF_TEST_FAILURES + 1))
  fi
  if is_production_like db.example.test mylive; then
    printf 'SELF-TEST FAIL: mylive was classified as production\n' >&2
    SELF_TEST_FAILURES=$((SELF_TEST_FAILURES + 1))
  fi

  # Retention: one old artifact is a candidate; a fresh one and sidecars are not.
  touch -t 202001010000 "$tmp/docgen-old.dump"
  touch -t 202001010000 "$tmp/docgen-old.dump.sha256"
  touch "$tmp/docgen-new.dump"
  local candidates
  candidates="$(retention_candidates "$tmp" 30)"
  self_assert_true "old artifact selected" grep -q 'docgen-old.dump$' <<<"$candidates"
  if grep -q 'docgen-new.dump' <<<"$candidates"; then
    printf 'SELF-TEST FAIL: fresh artifact selected by retention\n' >&2
    SELF_TEST_FAILURES=$((SELF_TEST_FAILURES + 1))
  fi
  if grep -q '\.sha256' <<<"$candidates"; then
    printf 'SELF-TEST FAIL: checksum sidecar selected as an artifact\n' >&2
    SELF_TEST_FAILURES=$((SELF_TEST_FAILURES + 1))
  fi

  if [ "$SELF_TEST_FAILURES" -ne 0 ]; then
    rm -rf -- "$tmp"
    printf 'pg-backup.sh self-test: FAILED (%d)\n' "$SELF_TEST_FAILURES" >&2
    return 1
  fi
  rm -rf -- "$tmp"
  printf 'pg-backup.sh self-test: OK\n'
  return 0
}

if [ "$SELF_TEST" -eq 1 ]; then
  run_self_test || exit 1
  exit 0
fi

# ---------------------------------------------------------------------------
# Main flow
# ---------------------------------------------------------------------------
require_command date
load_connection
log "Backup source: $(redacted_target)"
require_pg_tools pg_dump pg_restore
require_sha256_tool

ENC_MODE="$(encryption_mode)" || die "BACKUP_ENCRYPTION_KEY is set but neither gpg nor openssl is available."
if [ "$ENC_MODE" != "none" ]; then
  log "Encryption: $ENC_MODE (key supplied via environment; never logged)."
else
  log "Encryption: disabled (set BACKUP_ENCRYPTION_KEY to enable)."
fi
if [ -n "$SCHEMA" ]; then
  log "Scope: app schema '$SCHEMA' only (portable; excludes platform internals)."
else
  log "Scope: ALL schemas (full database; may include platform internals and may not restore on vanilla PostgreSQL)."
fi

PG_DUMP_VERSION="$(pg_dump --version 2>/dev/null | head -n 1)"
TIMESTAMP="$(date -u +%Y%m%dT%H%M%SZ)"
ARTIFACT="$OUTPUT_DIR/$(artifact_base "$TIMESTAMP")$(artifact_suffix "$ENC_MODE")"
CHECKSUM="$ARTIFACT.sha256"
METADATA="$ARTIFACT.meta.json"

if [ "$DRY_RUN" -eq 1 ]; then
  log "DRY-RUN: no backup will be written."
  log "Planned artifact: $ARTIFACT"
  log "Planned checksum: $CHECKSUM"
  log "Planned metadata: $METADATA"
  log "Compression level: $COMPRESS_LEVEL (custom format)"
  if [ -n "$SCHEMA" ]; then
    log "Scope: app schema '$SCHEMA' only."
  else
    log "Scope: ALL schemas (full database)."
  fi
  log "Client: $PG_DUMP_VERSION"
  if [ -d "$OUTPUT_DIR" ]; then
    log "Output directory exists: $OUTPUT_DIR"
    if [ "$RETENTION_DAYS" -gt 0 ]; then
      expired_count="$(retention_candidates "$OUTPUT_DIR" "$RETENTION_DAYS" | wc -l | tr -d ' ')"
      log "Retention: would delete $expired_count expired artifact(s) older than ${RETENTION_DAYS} day(s)."
    else
      log "Retention: disabled."
    fi
  else
    log "Output directory does not exist; it would be created: $OUTPUT_DIR"
  fi
  log "DRY-RUN: connection and tools validated; exiting 0."
  exit 0
fi

mkdir -p -- "$OUTPUT_DIR" || die "Cannot create output directory: $OUTPUT_DIR"
chmod 700 -- "$OUTPUT_DIR" 2>/dev/null || warn "Could not tighten permissions on $OUTPUT_DIR (check manually)."

if [ -e "$ARTIFACT" ]; then
  die "Artifact already exists, refusing to overwrite: $ARTIFACT"
fi

TMP_PLAIN="$OUTPUT_DIR/.$(basename "$ARTIFACT").partial"
TMP_META="$METADATA.tmp"
cleanup() {
  rm -f -- "$TMP_PLAIN" "$TMP_META" 2>/dev/null || true
}
trap cleanup EXIT

log "Dumping database '$PG_DB' to a temporary artifact..."
DUMP_ARGS=(--format=custom --compress="$COMPRESS_LEVEL" --no-password --file="$TMP_PLAIN")
if [ -n "$SCHEMA" ]; then
  DUMP_ARGS+=(--schema="$SCHEMA")
fi
if ! pg_dump "${DUMP_ARGS[@]}"; then
  die "pg_dump failed; no artifact was published (temporary file removed)."
fi

if ! pg_restore --list "$TMP_PLAIN" >/dev/null 2>&1; then
  die "Dumped archive is not readable by pg_restore; no artifact was published."
fi

if ! MIGRATION_ROWS="$(pg_restore --data-only --table=__EFMigrationsHistory --file=- "$TMP_PLAIN" 2>/dev/null | extract_migration_rows)"; then
  warn "Could not extract __EFMigrationsHistory from the archive; metadata will record count=0."
  MIGRATION_ROWS=""
fi
MIGRATION_COUNT="$(printf '%s\n' "$MIGRATION_ROWS" | migration_count)"
MIGRATION_HEAD="$(printf '%s\n' "$MIGRATION_ROWS" | migration_head)"
if [ "$MIGRATION_COUNT" -eq 0 ]; then
  warn "No rows found in __EFMigrationsHistory inside the archive; verify-backup.sh will reject it as unexpected for DocGen."
fi

if [ "$ENC_MODE" = "none" ]; then
  mv -f -- "$TMP_PLAIN" "$ARTIFACT" || die "Cannot publish artifact: $ARTIFACT"
else
  log "Encrypting artifact with $ENC_MODE..."
  encrypt_file "$TMP_PLAIN" "$ARTIFACT" "$ENC_MODE"
  if ! decrypt_archive "$ARTIFACT" "$ENC_MODE" | pg_restore --list - >/dev/null 2>&1; then
    rm -f -- "$ARTIFACT"
    die "Encrypted artifact failed its decrypt/readability round-trip; removed."
  fi
  rm -f -- "$TMP_PLAIN"
fi

ARTIFACT_HASH="$(write_checksum "$ARTIFACT" "$CHECKSUM")"
ARTIFACT_BYTES="$(file_size_bytes "$ARTIFACT")"

SOURCE_HOST_JSON="$(json_escape "$PG_HOST")"
SOURCE_DB_JSON="$(json_escape "$PG_DB")"
SOURCE_USER_JSON="$(json_escape "$PG_USER")"
ARTIFACT_NAME_JSON="$(json_escape "$(basename "$ARTIFACT")")"
MIGRATION_HEAD_JSON="$(json_escape "$MIGRATION_HEAD")"
PG_DUMP_JSON="$(json_escape "$PG_DUMP_VERSION")"
SCHEMA_JSON="$(json_escape "$SCHEMA")"
SCOPE_JSON="app"
if [ -z "$SCHEMA" ]; then
  SCOPE_JSON="all"
fi

cat >"$TMP_META" <<EOF
{
  "schemaVersion": 1,
  "createdAtUtc": "$(date -u +%Y-%m-%dT%H:%M:%SZ)",
  "artifact": "$ARTIFACT_NAME_JSON",
  "bytes": $ARTIFACT_BYTES,
  "sha256": "$ARTIFACT_HASH",
  "compressionLevel": $COMPRESS_LEVEL,
  "encryption": "$ENC_MODE",
  "scope": "$SCOPE_JSON",
  "schema": "$SCHEMA_JSON",
  "pgDumpVersion": "$PG_DUMP_JSON",
  "source": {
    "host": "$SOURCE_HOST_JSON",
    "port": $PG_PORT,
    "database": "$SOURCE_DB_JSON",
    "user": "$SOURCE_USER_JSON"
  },
  "migrations": {
    "count": $MIGRATION_COUNT,
    "head": "$MIGRATION_HEAD_JSON"
  }
}
EOF
mv -f -- "$TMP_META" "$METADATA" || die "Cannot finalize metadata file: $METADATA"
trap - EXIT

log "Backup written: $(basename "$ARTIFACT") (${ARTIFACT_BYTES} bytes)"
log "SHA-256: $ARTIFACT_HASH"
log "Migrations recorded in archive: count=$MIGRATION_COUNT head=${MIGRATION_HEAD:-<none>}"
prune_backups "$OUTPUT_DIR" "$RETENTION_DAYS" "$ARTIFACT"
log "Backup completed successfully."
