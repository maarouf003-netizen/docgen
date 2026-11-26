#!/usr/bin/env bash
# P5-06 — دوال مشتركة لسكربتات النسخ الاحتياطي/الاستعادة/التحقق (PostgreSQL 16).
#
# تُقرأ بـ `.` من السكربتات الشقيقة (pg-backup.sh / pg-restore.sh / verify-backup.sh)
# ولا تُشغَّل مباشرة. السكربتات نفسها تضبط `set -euo pipefail`.
#
# قواعد الأمان الملزمة:
# - سلسلة الاتصال من `POSTGRES_CONNECTION` فقط؛ لا قيمة مكتوبة في الكود.
# - كلمة المرور تُحوَّل إلى `PGPASSWORD` ولا تُطبع ولا تُمرَّر في وسائط الأوامر.
# - أدوات عميل PostgreSQL يجب أن تكون 16 أو أحدث (مطابقة الخادم).
# - أي فشل = رمز خروج غير صفري، وبلا أثر ملفات جزئية عند الإمكان.

PG_BACKUP_MIN_CLIENT_MAJOR=16
PG_BACKUP_APPNAME="docgen-p5-06-backup"

log() {
  printf '[%s] %s\n' "$(date -u +%Y-%m-%dT%H:%M:%SZ)" "$*" >&2
}

warn() {
  log "WARN: $*"
}

die() {
  log "ERROR: $*"
  exit 1
}

trim() {
  local s="$1"
  s="${s#"${s%%[![:space:]]*}"}"
  s="${s%"${s##*[![:space:]]}"}"
  printf '%s' "$s"
}

lower() {
  printf '%s' "$1" | tr '[:upper:]' '[:lower:]'
}

# فك ترميز %XX في عنوان URI دون أدوات خارجية. يرفض الترميز المشوَّه بلا طباعة القيمة.
urldecode() {
  local s="$1" out="" head hex
  while [[ "$s" == *%* ]]; do
    head="${s%%\%*}"
    out+="$head"
    s="${s#*\%}"
    hex="${s:0:2}"
    if [[ ! "$hex" =~ ^[0-9A-Fa-f]{2}$ ]] || [ "$hex" = "00" ]; then
      die "Invalid percent-encoding in POSTGRES_CONNECTION (value is not echoed)."
    fi
    out+="$(printf "\\x${hex}")"
    s="${s:2}"
  done
  out+="$s"
  printf '%s' "$out"
}

# تقسيم نص مع احترام علامات الاقتباس المزدوجة (مثل Npgsql: Password="a;b").
_split_double_quoted() {
  local s="$1" sep="$2" out="" inq=0 i c
  for ((i = 0; i < ${#s}; i++)); do
    c="${s:i:1}"
    if [ "$c" = '"' ]; then
      inq=$((1 - inq))
      out+="$c"
      continue
    fi
    if [ "$c" = "$sep" ] && [ "$inq" -eq 0 ]; then
      printf '%s\n' "$out"
      out=""
      continue
    fi
    out+="$c"
  done
  printf '%s\n' "$out"
}

# تقسيم conninfo بصيغة libpq: مسافات تفصل، واقتباس مفرد/شرطة خلفية داخل القيم.
_split_libpq_tokens() {
  local s="$1" out="" inq=0 esc=0 i c
  for ((i = 0; i < ${#s}; i++)); do
    c="${s:i:1}"
    if [ "$esc" -eq 1 ]; then
      out+="$c"
      esc=0
      continue
    fi
    if [ "$c" = '\' ]; then
      esc=1
      continue
    fi
    if [ "$c" = "'" ]; then
      inq=$((1 - inq))
      continue
    fi
    if [[ "$c" =~ [[:space:]] ]] && [ "$inq" -eq 0 ]; then
      if [ -n "$out" ]; then
        printf '%s\n' "$out"
        out=""
      fi
      continue
    fi
    out+="$c"
  done
  if [ -n "$out" ]; then
    printf '%s\n' "$out"
  fi
}

# تعيين حقل اتصال من مفتاح (Npgsql/libpq) إلى متغيرات داخلية.
_set_pg_field() {
  local key="$1" value="$2" norm
  norm="$(printf '%s' "$key" | tr -d ' _-' | tr '[:upper:]' '[:lower:]')"
  case "$norm" in
    host | server | address | datasource) PG_HOST="$value" ;;
    port) PG_PORT="$value" ;;
    database | initialcatalog | dbname) PG_DB="$value" ;;
    username | userid | uid | user) PG_USER="$value" ;;
    password | pwd) PG_PASS="$value" ;;
    sslmode) PG_SSL="$value" ;;
    *) : ;; # مفاتيح التشغيل (Pooling/Timeout/Trust Server Certificate) لا تخصّ pg_dump.
  esac
}

_parse_pg_uri() {
  local raw="$1" rest authority pathpart query userinfo hostport host port pair key value
  rest="${raw#*://}"
  authority="${rest%%/*}"
  if [[ "$rest" == */* ]]; then
    pathpart="/${rest#*/}"
  else
    pathpart=""
  fi
  query=""
  if [[ "$pathpart" == *\?* ]]; then
    query="${pathpart#*\?}"
    pathpart="${pathpart%%\?*}"
  fi

  if [[ "$authority" == *@* ]]; then
    userinfo="${authority%@*}"
    hostport="${authority##*@}"
    if [[ "$userinfo" == *:* ]]; then
      PG_USER="$(urldecode "${userinfo%%:*}")"
      PG_PASS="$(urldecode "${userinfo#*:}")"
    else
      PG_USER="$(urldecode "$userinfo")"
    fi
  else
    hostport="$authority"
  fi

  if [[ "$hostport" == \[*\]* ]]; then
    host="${hostport#\[}"
    host="${host%%\]*}"
    port="${hostport##*\]:}"
    if [ "$port" = "$hostport" ]; then
      port=""
    fi
  elif [[ "$hostport" == *:* ]]; then
    host="${hostport%%:*}"
    port="${hostport##*:}"
  else
    host="$hostport"
    port=""
  fi
  PG_HOST="$(urldecode "$host")"
  if [ -n "$port" ]; then
    PG_PORT="$port"
  fi
  if [ -n "$pathpart" ] && [ "$pathpart" != "/" ]; then
    PG_DB="$(urldecode "${pathpart#/}")"
  fi

  if [ -n "$query" ]; then
    while IFS= read -r pair; do
      [ -z "$pair" ] && continue
      key="$(lower "$(urldecode "${pair%%=*}")")"
      value="$(urldecode "${pair#*=}")"
      case "$key" in
        dbname | database) PG_DB="$value" ;;
        host) PG_HOST="$value" ;;
        port) PG_PORT="$value" ;;
        user) PG_USER="$value" ;;
        password) PG_PASS="$value" ;;
        sslmode) PG_SSL="$value" ;;
        *) : ;;
      esac
    done < <(_split_double_quoted "$query" '&')
  fi
}

_parse_pg_key_value() {
  local raw="$1" item key value
  while IFS= read -r item; do
    item="$(trim "$item")"
    [ -z "$item" ] && continue
    case "$item" in
      *=*) ;;
      *) die "Malformed POSTGRES_CONNECTION entry (expected key=value; value is not echoed)." ;;
    esac
    key="$(trim "${item%%=*}")"
    value="$(trim "${item#*=}")"
    if [[ "$value" == \"*\" ]] && [ "${#value}" -ge 2 ]; then
      value="${value:1:${#value}-2}"
    fi
    _set_pg_field "$key" "$value"
  done < <(_split_double_quoted "$raw" ';')
}

_parse_pg_libpq() {
  local raw="$1" token key value
  while IFS= read -r token; do
    [ -z "$token" ] && continue
    case "$token" in
      *=*) ;;
      *) die "Malformed POSTGRES_CONNECTION entry (expected key=value; value is not echoed)." ;;
    esac
    key="$(trim "${token%%=*}")"
    value="${token#*=}"
    _set_pg_field "$key" "$value"
  done < <(_split_libpq_tokens "$raw")
}

# يقرأ POSTGRES_CONNECTION (URI أو Npgsql أو libpq)، يتحقق، ويصدّر PG* فقط.
# يحذف النص الخام من البيئة بعد التحليل كي لا يظهر لعملية تابعة أو في سجل.
load_connection() {
  local raw="${POSTGRES_CONNECTION:-}"
  if [ -z "$(trim "$raw")" ]; then
    die "POSTGRES_CONNECTION is not set. Export a PostgreSQL URI (postgres://...) or a libpq/Npgsql connection string; never hardcode it."
  fi
  PG_HOST=""
  PG_PORT="5432"
  PG_DB=""
  PG_USER=""
  PG_PASS=""
  PG_SSL=""

  case "$raw" in
    postgres://* | postgresql://*) _parse_pg_uri "$raw" ;;
    *';'*) _parse_pg_key_value "$raw" ;;
    *'='*) _parse_pg_libpq "$raw" ;;
    *) die "POSTGRES_CONNECTION format is not recognized (expected postgres:// URI or key=value string; value is not echoed)." ;;
  esac

  [ -n "$PG_HOST" ] || die "POSTGRES_CONNECTION must include a host (or Host=)."
  [ -n "$PG_DB" ] || die "POSTGRES_CONNECTION must include a database name (or Database=)."
  [ -n "$PG_USER" ] || die "POSTGRES_CONNECTION must include a username (or Username=)."
  [[ "$PG_PORT" =~ ^[0-9]+$ ]] && [ "$PG_PORT" -ge 1 ] && [ "$PG_PORT" -le 65535 ] ||
    die "POSTGRES_CONNECTION has an invalid port."
  case "$(lower "$PG_SSL")" in
    '') ;;
    verifyca) PG_SSL="verify-ca" ;;
    verifyfull) PG_SSL="verify-full" ;;
    *) PG_SSL="$(lower "$PG_SSL")" ;;
  esac

  export PGHOST="$PG_HOST"
  export PGPORT="$PG_PORT"
  export PGDATABASE="$PG_DB"
  export PGUSER="$PG_USER"
  export PGCONNECT_TIMEOUT="${PGCONNECT_TIMEOUT:-15}"
  export PGAPPNAME="$PG_BACKUP_APPNAME"
  if [ -n "$PG_PASS" ]; then
    export PGPASSWORD="$PG_PASS"
  else
    unset PGPASSWORD || true
  fi
  if [ -n "$PG_SSL" ]; then
    export PGSSLMODE="$PG_SSL"
  else
    unset PGSSLMODE || true
  fi
  PG_PASS=""
  unset POSTGRES_CONNECTION || true
}

# وصف آمن للسجل: بلا كلمة مرور ولا سلسلة اتصال خام.
redacted_target() {
  printf 'host=%s port=%s dbname=%s user=%s sslmode=%s password=***' \
    "${PG_HOST:-?}" "${PG_PORT:-?}" "${PG_DB:-?}" "${PG_USER:-?}" "${PG_SSL:-default}"
}

require_command() {
  local name="$1"
  command -v "$name" >/dev/null 2>&1 || die "Required tool '$name' not found in PATH."
}

require_sha256_tool() {
  local tool
  for tool in sha256sum shasum openssl; do
    if command -v "$tool" >/dev/null 2>&1; then
      return 0
    fi
  done
  die "No SHA-256 tool found (need sha256sum, shasum, or openssl)."
}

sha256_file() {
  local file="$1"
  if command -v sha256sum >/dev/null 2>&1; then
    # GNU coreutils prefixes the line with "\" when the path contains backslashes
    # (e.g. a Windows path passed to Git Bash). The digest itself is the first field.
    sha256sum "$file" | awk '{print $1}' | sed 's/^\\//'
  elif command -v shasum >/dev/null 2>&1; then
    shasum -a 256 "$file" | awk '{print $1}'
  elif command -v openssl >/dev/null 2>&1; then
    openssl dgst -sha256 "$file" | awk '{print $NF}'
  else
    die "No SHA-256 tool found (need sha256sum, shasum, or openssl)."
  fi
}

# يكتب ملف بصمة بصيغة `sha256sum -c` المتوافقة: "<hash>  <basename>".
write_checksum() {
  local file="$1" sums="$2" hash name
  hash="$(sha256_file "$file")"
  name="$(basename "$file")"
  printf '%s  %s\n' "$hash" "$name" >"$sums.tmp" || die "Cannot write checksum file next to $file."
  mv -f -- "$sums.tmp" "$sums" || die "Cannot finalize checksum file: $sums"
  printf '%s' "$hash"
}

verify_checksum() {
  local file="$1" sums="$2" expected actual listed
  [ -f "$file" ] || {
    log "ERROR: artifact not found: $file"
    return 1
  }
  [ -f "$sums" ] || {
    log "ERROR: checksum file missing: $sums (refusing to continue)"
    return 1
  }
  expected="$(awk 'NF {print $1; exit}' "$sums")"
  if [[ ! "$expected" =~ ^[0-9A-Fa-f]{64}$ ]]; then
    log "ERROR: checksum file has an invalid format: $sums"
    return 1
  fi
  listed="$(awk 'NF >= 2 {print $2; exit}' "$sums")"
  listed="${listed#\*}"
  if [ -n "$listed" ] && [ "$(basename "$file")" != "$(basename "$listed")" ]; then
    log "ERROR: checksum file names a different artifact ($(basename "$listed")); refusing to verify $(basename "$file")."
    return 1
  fi
  actual="$(sha256_file "$file")"
  if [ "$(lower "$expected")" != "$(lower "$actual")" ]; then
    log "ERROR: checksum mismatch for $(basename "$file") (expected $expected, got $actual)"
    return 1
  fi
  log "Checksum OK (sha256 $actual)"
  return 0
}

# gpg مفضّل (AES256 + integrity) ثم openssl (AES-256-CBC/PBKDF2). لا مفاتيح في وسائط الأوامر.
encryption_mode() {
  if [ -n "${BACKUP_ENCRYPTION_KEY:-}" ]; then
    case "$BACKUP_ENCRYPTION_KEY" in
      *$'\n'* | *$'\r'*) die "BACKUP_ENCRYPTION_KEY must not contain newlines." ;;
    esac
    if command -v gpg >/dev/null 2>&1; then
      printf 'gpg'
      return 0
    fi
    if command -v openssl >/dev/null 2>&1; then
      printf 'openssl'
      return 0
    fi
    return 1
  fi
  printf 'none'
}

artifact_suffix() {
  case "$1" in
    gpg) printf '.gpg' ;;
    openssl) printf '.enc' ;;
    none) printf '' ;;
    *) die "internal: unknown encryption mode '$1'" ;;
  esac
}

artifact_encryption_mode() {
  case "$1" in
    *.gpg) printf 'gpg' ;;
    *.enc) printf 'openssl' ;;
    *) printf 'none' ;;
  esac
}

encrypt_file() {
  local src="$1" dest="$2" mode="$3"
  case "$mode" in
    gpg)
      if ! printf '%s' "$BACKUP_ENCRYPTION_KEY" |
        gpg --batch --yes --quiet --pinentry-mode loopback --passphrase-fd 0 \
          --symmetric --cipher-algo AES256 --output "$dest" "$src"; then
        rm -f -- "$dest"
        die "gpg encryption failed (artifact not kept)."
      fi
      ;;
    openssl)
      if ! BACKUP_ENCRYPTION_KEY="$BACKUP_ENCRYPTION_KEY" \
        openssl enc -aes-256-cbc -salt -pbkdf2 -iter 200000 \
        -in "$src" -out "$dest" -pass env:BACKUP_ENCRYPTION_KEY; then
        rm -f -- "$dest"
        die "openssl encryption failed (artifact not kept)."
      fi
      ;;
    *)
      die "internal: unsupported encryption mode '$mode'"
      ;;
  esac
}

# يبثّ الأرشيف المفكوك إلى stdout (لـ pg_restore عبر أنبوب، بلا ملف نصّي مؤقت).
decrypt_archive() {
  local file="$1" mode="$2"
  case "$mode" in
    gpg)
      [ -n "${BACKUP_ENCRYPTION_KEY:-}" ] || die "Artifact is gpg-encrypted but BACKUP_ENCRYPTION_KEY is not set."
      printf '%s' "$BACKUP_ENCRYPTION_KEY" |
        gpg --batch --quiet --yes --pinentry-mode loopback --passphrase-fd 0 --decrypt "$file"
      ;;
    openssl)
      [ -n "${BACKUP_ENCRYPTION_KEY:-}" ] || die "Artifact is openssl-encrypted but BACKUP_ENCRYPTION_KEY is not set."
      BACKUP_ENCRYPTION_KEY="$BACKUP_ENCRYPTION_KEY" \
        openssl enc -d -aes-256-cbc -pbkdf2 -iter 200000 \
        -in "$file" -pass env:BACKUP_ENCRYPTION_KEY
      ;;
    none)
      cat -- "$file"
      ;;
    *)
      die "internal: unsupported encryption mode '$mode'"
      ;;
  esac
}

# يستخرج معرّفات الهجرات من مخرجات COPY لجدول __EFMigrationsHistory (من الأرشيف، بلا استعادة).
extract_migration_rows() {
  awk '
    /^COPY .* FROM stdin;$/ { inside = 1; next }
    inside && /^\\\.$/ { inside = 0; next }
    inside {
      n = split($0, fields, "\t")
      if (n >= 1 && fields[1] != "" && fields[1] != "\\N") print fields[1]
    }
  '
}

migration_head() {
  awk 'NF { if ($0 > head) head = $0 } END { print head }'
}

migration_count() {
  sed '/^[[:space:]]*$/d' | wc -l | tr -d ' '
}

# عملاء PostgreSQL: pg_dump/pg_restore يجب ألا يكونوا أقدم من الخادم (16).
pg_client_major() {
  local tool="$1"
  "$tool" --version 2>/dev/null | grep -oE '[0-9]+\.[0-9]+' | head -n 1 | cut -d. -f1
}

require_pg_tools() {
  local tool major
  for tool in "$@"; do
    require_command "$tool"
  done
  for tool in "$@"; do
    major="$(pg_client_major "$tool")"
    [ -n "$major" ] || die "Cannot parse '$tool --version' output."
    if [ "$major" -lt "$PG_BACKUP_MIN_CLIENT_MAJOR" ]; then
      die "'$tool' major version $major is older than the PostgreSQL server (requires >= $PG_BACKUP_MIN_CLIENT_MAJOR)."
    fi
  done
}

# اسم الملف: طابع زمني UTC + صيغة مخصّصة. لا يحمل اسم قاعدة/مستخدم.
artifact_base() {
  printf 'docgen-%s.dump' "$1"
}

# حارس الإنتاج: اسم قاعدة/مضيف يوحي بالإنتاج، أو قائمة رفض صريحة في BACKUP_PRODUCTION_DENYLIST.
is_production_like() {
  local host db denylist item
  host="$(lower "$1")"
  db="$(lower "$2")"
  # قاعدة `postgres` هي قاعدة الإدارة/الافتراضية لدى كثير من المزوّدين (Supabase)؛
  # استعادتها فوق الإنتاج خطأ شائع، فتتطلب --allow-production صريحًا.
  if [ "$db" = "postgres" ]; then
    return 0
  fi
  if [[ "$db" =~ (^|[-_])(prod|production|prd|live)([-_]|$) ]]; then
    return 0
  fi
  if [[ "$host" =~ (^|[.-])(prod|production|prd|live)([.-]|$) ]]; then
    return 0
  fi
  denylist="${BACKUP_PRODUCTION_DENYLIST:-}"
  if [ -n "$denylist" ]; then
    local IFS=','
    for item in $denylist; do
      item="$(lower "$(trim "$item")")"
      [ -z "$item" ] && continue
      if [ "$db" = "$item" ]; then
        return 0
      fi
    done
  fi
  return 1
}

assert_safe_target() {
  local host="$1" db="$2" allow="$3"
  case "$(lower "$db")" in
    template0 | template1)
      die "Refusing to restore into template database '$db'."
      ;;
  esac
  if is_production_like "$host" "$db"; then
    if [ "$allow" != "1" ]; then
      die "Target database '$db' on host '$host' looks like PRODUCTION. Refusing without --allow-production and an approved change window."
    fi
    warn "PRODUCTION override accepted (--allow-production) for db '$db' on host '$host'."
  fi
}

# بوابة التأكيد: لا استعادة بلا --yes صريح.
require_confirmation() {
  if [ "${1:-0}" != "1" ]; then
    die "Refusing to restore without --yes. Re-run with --yes after verifying the target and the backup checksum."
  fi
}

# قائمة النسخ المنتهية (أقدم من N يومًا) — لا تشمل ملفات البصمة/البيانات الوصفية.
retention_candidates() {
  local dir="$1" days="$2"
  [ "$days" -gt 0 ] || return 0
  [ -d "$dir" ] || return 0
  find "$dir" -maxdepth 1 -type f \
    \( -name 'docgen-*.dump' -o -name 'docgen-*.dump.gpg' -o -name 'docgen-*.dump.enc' \) \
    -mtime +"$days" -print
}

prune_backups() {
  local dir="$1" days="$2" keep="$3" f count=0
  if [ "$days" -le 0 ]; then
    log "Retention disabled (retention-days=0 keeps every artifact)."
    return 0
  fi
  while IFS= read -r f; do
    [ -z "$f" ] && continue
    [ "$f" = "$keep" ] && continue
    rm -f -- "$f" "$f.sha256" "$f.meta.json" || die "Failed to prune expired backup: $(basename "$f")"
    log "Pruned expired backup: $(basename "$f")"
    count=$((count + 1))
  done < <(retention_candidates "$dir" "$days")
  log "Retention: removed $count expired backup(s) older than ${days} day(s)."
}

json_escape() {
  printf '%s' "$1" | sed -e 's/\\/\\\\/g' -e 's/"/\\"/g'
}

file_size_bytes() {
  wc -c <"$1" | tr -d ' '
}
