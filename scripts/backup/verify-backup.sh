#!/usr/bin/env bash
# P5-06 — التحقق من نسخة PostgreSQL بلا استعادة: البصمة، قابلية قراءة الأرشيف، ورأس الهجرة.
#
# المتطلبات: bash 4+، pg_restore من عميل PostgreSQL 16+، أداة SHA-256.
# لا يتصل بأي قاعدة بيانات. الملف المشفّر يُفكّ إلى أنبوب (بلا ملف نصّي مؤقت).
#
# أمثلة:
#   ./verify-backup.sh --file /srv/backups/docgen/docgen-20261126T020000Z.dump
#   ./verify-backup.sh --file ... --expected-head 20261121034750_P3_02_PortalSearchIndex
#   ./verify-backup.sh --file ... --checksum-only
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=scripts/backup/backup-common.sh
. "$SCRIPT_DIR/backup-common.sh"

ARCHIVE=""
CHECKSUM_FILE=""
EXPECTED_HEAD=""
CHECKSUM_ONLY=0
SELF_TEST=0

usage() {
  cat <<'EOF'
Usage: verify-backup.sh --file ARCHIVE [options]

Verifies a backup without restoring it:
  1. SHA-256 checksum matches <archive>.sha256,
  2. pg_restore can read the archive table of contents,
  3. prints the migration count and head recorded in __EFMigrationsHistory,
     extracted from the archive data (no database connection, no restore).

Options:
  --file PATH             Backup artifact (.dump, .dump.gpg, or .dump.enc)
  --checksum-file PATH    Default: <archive>.sha256
  --expected-head ID      Fail if the recorded migration head differs (drift check)
  --checksum-only         Verify the checksum only; skip archive inspection
  --self-test             Run offline checksum/parser self-tests and exit
  -h, --help              Show this help

Environment:
  BACKUP_ENCRYPTION_KEY   Required only for encrypted artifacts
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
    --expected-head)
      need_value "$@"
      EXPECTED_HEAD="$2"
      shift 2
      ;;
    --checksum-only)
      CHECKSUM_ONLY=1
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

# ---------------------------------------------------------------------------
# Self-test (offline)
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
  local tmp copy rows
  tmp="$(mktemp -d)" || {
    printf 'SELF-TEST FAIL: cannot create a temp directory\n' >&2
    return 1
  }
  require_sha256_tool

  printf 'archive-bytes' >"$tmp/sample.dump"
  write_checksum "$tmp/sample.dump" "$tmp/sample.dump.sha256" >/dev/null
  if ! verify_checksum "$tmp/sample.dump" "$tmp/sample.dump.sha256" >/dev/null 2>&1; then
    printf 'SELF-TEST FAIL: valid checksum rejected\n' >&2
    SELF_TEST_FAILURES=$((SELF_TEST_FAILURES + 1))
  fi
  printf 'x' >>"$tmp/sample.dump"
  if (verify_checksum "$tmp/sample.dump" "$tmp/sample.dump.sha256") >/dev/null 2>&1; then
    printf 'SELF-TEST FAIL: corrupted artifact passed checksum verification\n' >&2
    SELF_TEST_FAILURES=$((SELF_TEST_FAILURES + 1))
  fi
  printf 'archive-bytes' >"$tmp/other.dump"
  if (verify_checksum "$tmp/other.dump" "$tmp/sample.dump.sha256") >/dev/null 2>&1; then
    printf 'SELF-TEST FAIL: checksum for a different artifact was accepted\n' >&2
    SELF_TEST_FAILURES=$((SELF_TEST_FAILURES + 1))
  fi
  if (verify_checksum "$tmp/sample.dump" "$tmp/missing.sha256") >/dev/null 2>&1; then
    printf 'SELF-TEST FAIL: missing checksum file was accepted\n' >&2
    SELF_TEST_FAILURES=$((SELF_TEST_FAILURES + 1))
  fi

  # COPY parser: headers/settings ignored, \N ignored, ids extracted.
  copy="$(cat <<'EOF'
SET statement_timeout = 0;
COPY "public"."__EFMigrationsHistory" ("MigrationId", "ProductVersion") FROM stdin;
20261114101737_InitialPostgresBaseline	10.0.10
20261114140029_AddIdempotencyResponseMetadata	10.0.10
20261121034750_P3_02_PortalSearchIndex	10.0.10
\.
EOF
)"
  rows="$(printf '%s\n' "$copy" | extract_migration_rows)"
  self_assert_eq "COPY rows" "3" "$(printf '%s\n' "$rows" | migration_count)"
  self_assert_eq "COPY head" "20261121034750_P3_02_PortalSearchIndex" "$(printf '%s\n' "$rows" | migration_head)"

  # .gpg/.enc/plain detection.
  self_assert_eq "gpg detection" "gpg" "$(artifact_encryption_mode x.dump.gpg)"
  self_assert_eq "openssl detection" "openssl" "$(artifact_encryption_mode x.dump.enc)"
  self_assert_eq "plain detection" "none" "$(artifact_encryption_mode x.dump)"

  rm -rf -- "$tmp"
  if [ "$SELF_TEST_FAILURES" -ne 0 ]; then
    printf 'verify-backup.sh self-test: FAILED (%d)\n' "$SELF_TEST_FAILURES" >&2
    return 1
  fi
  printf 'verify-backup.sh self-test: OK\n'
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
[ -f "$ARCHIVE" ] || die "Backup artifact not found: $ARCHIVE"
if [ -z "$CHECKSUM_FILE" ]; then
  CHECKSUM_FILE="$ARCHIVE.sha256"
fi
require_sha256_tool
verify_checksum "$ARCHIVE" "$CHECKSUM_FILE" || die "Checksum verification failed."

if [ "$CHECKSUM_ONLY" -eq 1 ]; then
  log "Checksum-only verification passed."
  exit 0
fi

require_pg_tools pg_restore
ENC_MODE="$(artifact_encryption_mode "$ARCHIVE")"
if [ "$ENC_MODE" != "none" ] && [ -z "${BACKUP_ENCRYPTION_KEY:-}" ]; then
  die "Artifact is encrypted ($ENC_MODE) but BACKUP_ENCRYPTION_KEY is not set; cannot verify archive readability."
fi

if ! TOC="$(decrypt_archive "$ARCHIVE" "$ENC_MODE" | pg_restore --list -)"; then
  die "pg_restore cannot read the archive (corrupt, wrong key, or not a custom-format dump)."
fi
[ -n "$(printf '%s' "$TOC" | sed '/^[[:space:]]*$/d')" ] || die "Archive table of contents is empty."

ENTRY_COUNT="$(printf '%s\n' "$TOC" | grep -cvE '^;|^$' || true)"
[ -n "$ENTRY_COUNT" ] && [ "$ENTRY_COUNT" -gt 0 ] || die "Archive table of contents has no restorable entries."
ARCHIVE_DB="$(printf '%s\n' "$TOC" | sed -n 's/^;[[:space:]]*dbname:[[:space:]]*//p' | head -n 1)"
log "Archive readable: entries=$ENTRY_COUNT dbname=${ARCHIVE_DB:-unknown} encryption=$ENC_MODE"

MIGRATION_ROWS="$(decrypt_archive "$ARCHIVE" "$ENC_MODE" | pg_restore --data-only --table=__EFMigrationsHistory --file=- - 2>/dev/null | extract_migration_rows || true)"
MIGRATION_COUNT="$(printf '%s\n' "$MIGRATION_ROWS" | migration_count)"
MIGRATION_HEAD="$(printf '%s\n' "$MIGRATION_ROWS" | migration_head)"
if [ "$MIGRATION_COUNT" -eq 0 ]; then
  die "No migrations found in __EFMigrationsHistory inside the archive; this is not a usable DocGen backup."
fi

log "Migrations in archive: count=$MIGRATION_COUNT head=$MIGRATION_HEAD"
if [ -n "$EXPECTED_HEAD" ] && [ "$MIGRATION_HEAD" != "$EXPECTED_HEAD" ]; then
  die "Migration head drift: archive has '$MIGRATION_HEAD' but '$EXPECTED_HEAD' was expected."
fi

log "Backup verification passed (no restore performed)."
