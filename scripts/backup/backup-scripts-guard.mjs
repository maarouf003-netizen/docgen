/**
 * P5-06 — حارس سكربتات النسخ الاحتياطي/الاستعادة (Backup/restore scripts guard).
 *
 * يتحقق من:
 *  - وجود السكربتات الستة + المكتبتين المشتركتين، وصلاحية صياغة shell (`bash -n`).
 *  - الأعلام الأمنية الإلزامية: `--dry-run`/`-WhatIf`، `--yes`/`-Yes`، `--allow-production`،
 *    `--table`/`-Table`، البصمة `SHA-256`، وعدم وجود كلمات مرور مكتوبة أو مطبوعة.
 *  - ترتيب الحواجز: البصمة والتأكيد وحارس الإنتاج **قبل** أمر الاستعادة الهدّام.
 *  - `--retention-days` و`BACKUP_ENCRYPTION_KEY` و`POSTGRES_CONNECTION` في سكربت النسخ.
 *  - وجود دليل التشغيل `docs/runbooks/backup-restore.md` بمحتواه الإلزامي (RPO/RTO،
 *    الجدولة، التشفير، التمرين، PITR)، وتحديث الخريطة/الفهرس/السجل، وربط الحارس في `CI`.
 *  - `--self-test`: يشغّل اختبارات السكربتات الذاتية فعليًا (bash/pwsh)، ثم يجرّب تعديلات
 *    غير آمنة على نسخ في الذاكرة ويتأكد أن الحارس يرفضها (منع حارس أجوف).
 *
 * التشغيل:
 *   node scripts/backup/backup-scripts-guard.mjs
 *   node scripts/backup/backup-scripts-guard.mjs --self-test
 *
 * لا حزم جديدة، ولا اتصال بقاعدة بيانات، ولا أدوات PostgreSQL مطلوبة.
 */
import { spawnSync } from 'node:child_process';
import { existsSync, readFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '../..');

const SCRIPT_FILES = [
  'scripts/backup/backup-common.sh',
  'scripts/backup/backup-common.ps1',
  'scripts/backup/pg-backup.sh',
  'scripts/backup/pg-backup.ps1',
  'scripts/backup/pg-restore.sh',
  'scripts/backup/pg-restore.ps1',
  'scripts/backup/verify-backup.sh',
  'scripts/backup/verify-backup.ps1',
];

const SH_ENTRY_FILES = [
  'scripts/backup/pg-backup.sh',
  'scripts/backup/pg-restore.sh',
  'scripts/backup/verify-backup.sh',
];

const SELF_TEST_FILES = [
  ['scripts/backup/pg-backup.sh', 'sh'],
  ['scripts/backup/pg-restore.sh', 'sh'],
  ['scripts/backup/verify-backup.sh', 'sh'],
  ['scripts/backup/pg-backup.ps1', 'ps1'],
  ['scripts/backup/pg-restore.ps1', 'ps1'],
  ['scripts/backup/verify-backup.ps1', 'ps1'],
];

const REQUIRED_TOKENS = {
  'scripts/backup/pg-backup.sh': [
    'POSTGRES_CONNECTION',
    '--dry-run',
    '--output-dir',
    '--retention-days',
    '--compress-level',
    '--all-schemas',
    'BACKUP_ENCRYPTION_KEY',
    'pg_dump "${DUMP_ARGS[@]}"',
    '--schema="$SCHEMA"',
    '--exclude-table-data',
    'public."DataProtectionKeys"',
    'pg_restore --list',
    '--no-password',
    'require_sha256_tool',
    'write_checksum',
    'prune_backups',
    '--self-test',
  ],
  'scripts/backup/pg-backup.ps1': [
    'POSTGRES_CONNECTION',
    '[switch]$WhatIf',
    'WhatIf',
    'OutputDir',
    'RetentionDays',
    'CompressLevel',
    '[switch]$AllSchemas',
    'BACKUP_ENCRYPTION_KEY',
    '--format=custom',
    '--schema=$schema',
    '--exclude-table-data',
    'public."DataProtectionKeys"',
    'pg_restore --list',
    '--no-password',
    'Write-ChecksumFile',
    'Invoke-BackupPrune',
    '-SelfTest',
  ],
  'scripts/backup/pg-restore.sh': [
    'POSTGRES_CONNECTION',
    '--target-db',
    '--yes',
    'require_confirmation',
    '--allow-production',
    'assert_safe_target',
    '--table',
    '--truncate-table',
    '--allow-nonempty',
    '--all-schemas',
    '--schema="$SCHEMA"',
    'CREATE EXTENSION IF NOT EXISTS pg_trgm',
    'verify_checksum',
    'pg_restore',
    '--single-transaction',
    '--exit-on-error',
    'psql',
    '__EFMigrationsHistory',
    '--self-test',
  ],
  'scripts/backup/pg-restore.ps1': [
    'POSTGRES_CONNECTION',
    '-TargetDb',
    '-Yes',
    'Assert-RestoreConfirmation',
    '-AllowProduction',
    'Assert-SafeTarget',
    '-Table',
    '-TruncateTable',
    '-AllowNonEmpty',
    '[switch]$AllSchemas',
    '--schema=$schema',
    'CREATE EXTENSION IF NOT EXISTS pg_trgm',
    '[switch]$WhatIf',
    'Test-Checksum',
    'pg_restore',
    '--single-transaction',
    '--exit-on-error',
    'psql',
    '__EFMigrationsHistory',
    '-SelfTest',
  ],
  'scripts/backup/verify-backup.sh': [
    '--file',
    '--checksum-file',
    '--checksum-only',
    '--expected-head',
    'verify_checksum',
    'pg_restore --list',
    '--data-only',
    'extract_migration_rows',
    '__EFMigrationsHistory',
    'BACKUP_ENCRYPTION_KEY',
    '--self-test',
  ],
  'scripts/backup/verify-backup.ps1': [
    '-File',
    '-ChecksumFile',
    '-ChecksumOnly',
    '-ExpectedHead',
    'Test-Checksum',
    'pg_restore --list',
    'ConvertFrom-CopyRows',
    '__EFMigrationsHistory',
    'BACKUP_ENCRYPTION_KEY',
    '-SelfTest',
  ],
  'scripts/backup/backup-common.sh': [
    'POSTGRES_CONNECTION',
    'PGPASSWORD',
    'sha256_file',
    'verify_checksum',
    'encrypt_file',
    'decrypt_archive',
    'extract_migration_rows',
    'is_production_like',
    'prune_backups',
  ],
  'scripts/backup/backup-common.ps1': [
    'POSTGRES_CONNECTION',
    'PGPASSWORD',
    'Get-Sha256',
    'Test-Checksum',
    'Protect-BackupFile',
    'Expand-BackupArchiveToTemp',
    'ConvertFrom-CopyRows',
    'Test-ProductionLike',
    'Invoke-BackupPrune',
  ],
};

const RUNBOOK = 'docs/runbooks/backup-restore.md';
const ROADMAP = 'docs/PERFECTION_ROADMAP.md';
const DOCS_README = 'docs/README.md';
const RUNBOOKS_README = 'docs/runbooks/README.md';
const CHANGELOG = 'CHANGELOG.md';
const CI = '.github/workflows/ci.yml';
const GITATTRIBUTES = '.gitattributes';

const RUNBOOK_TOKENS = [
  'RPO',
  'RTO',
  '24 ساعة',
  '4 ساعات',
  'PITR',
  'Supabase',
  '--dry-run',
  '-WhatIf',
  '--yes',
  '--allow-production',
  '--table',
  '--truncate-table',
  '--allow-nonempty',
  'verify-backup',
  'pg-backup',
  'pg-restore',
  '__EFMigrationsHistory',
  'gpg',
  'openssl',
  'PBKDF2',
  'offsite',
  'خارج الموقع',
  'PBKDF2',
  'يوميًا',
  'أسبوعيًا',
  'شهريًا',
  'توقيع',
  'المالك',
  'المزوّد',
  'cron',
  'Task Scheduler',
  '/readyz',
  'SHA-256',
];

const violations = [];
const notes = [];
let failureSink = null;

function fail(message) {
  (failureSink ?? violations).push(message);
}

function read(root, relative) {
  return readFileSync(join(root, relative), 'utf8');
}

function loadFiles(root, relativePaths) {
  const map = new Map();
  for (const relative of relativePaths) {
    const absolute = join(root, relative);
    if (!existsSync(absolute)) {
      fail(`Missing file: ${relative}`);
      continue;
    }
    map.set(relative, read(root, relative));
  }
  return map;
}

function lineNumber(content, index) {
  if (index < 0) return 0;
  return content.slice(0, index).split('\n').length;
}

function requireOrder(file, content, firstNeedle, secondNeedle) {
  const first = content.indexOf(firstNeedle);
  const second = content.indexOf(secondNeedle);
  if (first < 0 || second < 0) {
    return; // missing-token checks report this separately.
  }
  if (first > second) {
    fail(
      `${file}: safety order violated — "${firstNeedle}" (line ${lineNumber(content, first)}) must appear before "${secondNeedle}" (line ${lineNumber(content, second)}).`,
    );
  }
}

function scanForSecrets(file, content) {
  const lines = content.split(/\r?\n/);
  lines.forEach((line, index) => {
    const lineNumberValue = index + 1;
    const isFixture = /example\.test|SELFTEST|self-test fixture/i.test(line);
    const isPlaceholder = /<user>|<password>|<host>|<database>|\buser:pass\b/i.test(line);

    if (!isFixture && !isPlaceholder && /postgres(?:ql)?:\/\/[^\s"'/@]+:[^\s"'@]+@/.test(line)) {
      fail(`${file}:${lineNumberValue}: possible inline password in a PostgreSQL URI.`);
    }
    if (!isFixture && !isPlaceholder && /\bPGPASSWORD\s*=\s*["'](?!\$)[^"']+["']/.test(line)) {
      fail(`${file}:${lineNumberValue}: literal PGPASSWORD assignment.`);
    }
    if (!isFixture && !isPlaceholder && /\b(?:password|passwd|pwd)\s*=\s*["'](?!\$)[^"'\s]{3,}["']/.test(line)) {
      fail(`${file}:${lineNumberValue}: possible literal password assignment.`);
    }
    if (/(?<!no-)--password\b/.test(line)) {
      fail(`${file}:${lineNumberValue}: pg tools must not be invoked with --password (use PGPASSWORD/PGPASSFILE).`);
    }
    if (/(?:^|\s)(?:echo|printf|Write-Host|Write-Output|Write-BackupLog)\b[^\n]*(?:\$env:PGPASSWORD|\$PGPASSWORD|\bPGPASSWORD\b)/.test(line)) {
      fail(`${file}:${lineNumberValue}: password may be printed to logs.`);
    }
    if (/(?:^|\s)(?:echo|printf|Write-Host|Write-Output|Write-BackupLog)\b[^\n]*\$(?:env:)?POSTGRES_CONNECTION\b/.test(line)) {
      fail(`${file}:${lineNumberValue}: raw POSTGRES_CONNECTION may be printed.`);
    }
  });
}

function validateScripts(root, files) {
  const localViolations = [];
  failureSink = localViolations;
  try {
    for (const [file, tokens] of Object.entries(REQUIRED_TOKENS)) {
      if (!files.has(file)) {
        continue;
      }
      const content = files.get(file);
      for (const token of tokens) {
        if (!content.includes(token)) {
          fail(`${file}: missing required safety token "${token}".`);
        }
      }
      scanForSecrets(file, content);
    }

    for (const file of SH_ENTRY_FILES) {
      if (files.has(file) && !/set -euo pipefail/.test(files.get(file))) {
        fail(`${file}: must enable "set -euo pipefail".`);
      }
    }

    if (files.has('scripts/backup/pg-backup.sh')) {
      const content = files.get('scripts/backup/pg-backup.sh');
      requireOrder('pg-backup.sh', content, 'if [ "$DRY_RUN" -eq 1 ]', 'pg_dump "${DUMP_ARGS[@]}"');
      requireOrder('pg-backup.sh', content, 'write_checksum', 'prune_backups');
    }
    if (files.has('scripts/backup/pg-backup.ps1')) {
      const content = files.get('scripts/backup/pg-backup.ps1');
      requireOrder('pg-backup.ps1', content, 'if ($WhatIf)', '& pg_dump @dumpArguments');
      requireOrder('pg-backup.ps1', content, 'Write-ChecksumFile', 'Invoke-BackupPrune');
    }
    if (files.has('scripts/backup/pg-restore.sh')) {
      const content = files.get('scripts/backup/pg-restore.sh');
      const main = content.slice(content.indexOf('# Main flow'));
      requireOrder('pg-restore.sh', main, 'verify_checksum "$ARCHIVE"', 'pg_restore "${RESTORE_OPTS[@]}"');
      requireOrder('pg-restore.sh', main, 'assert_safe_target', 'pg_restore "${RESTORE_OPTS[@]}"');
      requireOrder('pg-restore.sh', main, 'require_confirmation "$YES"', 'pg_restore "${RESTORE_OPTS[@]}"');
      requireOrder('pg-restore.sh', main, 'require_confirmation "$YES"', 'TRUNCATE TABLE');
      requireOrder('pg-restore.sh', main, 'require_confirmation "$YES"', 'CREATE EXTENSION IF NOT EXISTS pg_trgm');
      requireOrder('pg-restore.sh', main, 'CREATE EXTENSION IF NOT EXISTS pg_trgm', 'pg_restore "${RESTORE_OPTS[@]}"');
    }
    if (files.has('scripts/backup/pg-restore.ps1')) {
      const content = files.get('scripts/backup/pg-restore.ps1');
      const main = content.slice(content.indexOf('$plainArchive = $null'));
      requireOrder('pg-restore.ps1', main, 'Test-Checksum -File $File', '& pg_restore @restoreArguments');
      requireOrder('pg-restore.ps1', main, 'Assert-SafeTarget', '& pg_restore @restoreArguments');
      requireOrder('pg-restore.ps1', main, 'Assert-RestoreConfirmation', '& pg_restore @restoreArguments');
      requireOrder('pg-restore.ps1', main, 'Assert-RestoreConfirmation', 'TRUNCATE TABLE');
      requireOrder('pg-restore.ps1', main, 'Assert-RestoreConfirmation', 'CREATE EXTENSION IF NOT EXISTS pg_trgm');
      requireOrder('pg-restore.ps1', main, 'CREATE EXTENSION IF NOT EXISTS pg_trgm', '& pg_restore @restoreArguments');
    }
    if (files.has('scripts/backup/verify-backup.sh')) {
      const content = files.get('scripts/backup/verify-backup.sh');
      const main = content.slice(content.indexOf('# Main flow'));
      requireOrder('verify-backup.sh', main, 'verify_checksum "$ARCHIVE"', 'pg_restore --list');
    }
    if (files.has('scripts/backup/verify-backup.ps1')) {
      const content = files.get('scripts/backup/verify-backup.ps1');
      const main = content.slice(content.indexOf('$plainArchive = $null'));
      requireOrder('verify-backup.ps1', main, 'Test-Checksum -File $File', '& pg_restore --list');
    }

    for (const file of ['scripts/backup/pg-restore.sh', 'scripts/backup/pg-restore.ps1']) {
      if (!files.has(file)) continue;
      const content = files.get(file);
      if (!/template0|template1/.test(content)) {
        fail(`${file}: must refuse template0/template1 targets.`);
      }
      if (!/__EFMigrationsHistory/.test(content)) {
        fail(`${file}: post-restore smoke must inspect __EFMigrationsHistory.`);
      }
    }
  } finally {
    failureSink = null;
  }
  return localViolations;
}

function validateDocs(root) {
  if (!existsSync(join(root, RUNBOOK))) {
    fail(`Missing runbook: ${RUNBOOK}`);
    return;
  }
  const runbook = read(root, RUNBOOK);
  for (const token of RUNBOOK_TOKENS) {
    if (!runbook.includes(token)) {
      fail(`${RUNBOOK}: missing required content "${token}".`);
    }
  }
  if (runbook.includes('TODO') || runbook.includes('TBD')) {
    fail(`${RUNBOOK}: contains TODO/TBD instead of an actionable procedure.`);
  }

  if (!existsSync(join(root, ROADMAP))) {
    fail(`Missing roadmap: ${ROADMAP}`);
  } else {
    const roadmap = read(root, ROADMAP);
    const start = roadmap.indexOf('#### `P5-06`');
    if (start < 0) {
      fail(`${ROADMAP}: P5-06 section not found.`);
    } else {
      const next = roadmap.indexOf('\n#### ', start + 10);
      const section = roadmap.slice(start, next < 0 ? undefined : next);
      for (const token of ['2026-11-26', 'backup', 'PITR', 'المالك', 'scripts/backup']) {
        if (!section.includes(token)) {
          fail(`${ROADMAP}: P5-06 status must mention "${token}".`);
        }
      }
    }
  }

  if (!existsSync(join(root, DOCS_README))) {
    fail(`Missing docs index: ${DOCS_README}`);
  } else {
    const index = read(root, DOCS_README);
    for (const token of ['runbooks/backup-restore.md', '261', 'backup']) {
      if (!index.includes(token)) {
        fail(`${DOCS_README}: index must reference "${token}".`);
      }
    }
  }

  if (!existsSync(join(root, RUNBOOKS_README))) {
    fail(`Missing runbooks index: ${RUNBOOKS_README}`);
  } else if (!read(root, RUNBOOKS_README).includes('backup-restore.md')) {
    fail(`${RUNBOOKS_README}: must link backup-restore.md.`);
  }

  if (!existsSync(join(root, CHANGELOG))) {
    fail(`Missing changelog: ${CHANGELOG}`);
  } else {
    const changelog = read(root, CHANGELOG);
    if (!changelog.includes('P5-06') || !changelog.includes('backup')) {
      fail(`${CHANGELOG}: must record the P5-06 delivery.`);
    }
  }

  if (!existsSync(join(root, CI))) {
    fail(`Missing CI workflow: ${CI}`);
  } else {
    const ci = read(root, CI);
    if (!ci.includes('node scripts/backup/backup-scripts-guard.mjs --self-test')) {
      fail(`${CI}: guards job must run backup-scripts-guard.mjs --self-test.`);
    }
  }

  if (existsSync(join(root, GITATTRIBUTES))) {
    const gitattributes = read(root, GITATTRIBUTES);
    if (!/\*\.sh text eol=lf/.test(gitattributes)) {
      fail(`${GITATTRIBUTES}: shell scripts must keep eol=lf.`);
    }
  }
}

function findBash() {
  const candidates = [
    process.env.BASH,
    'bash',
    'C:\\Program Files\\Git\\bin\\bash.exe',
    'C:\\Program Files\\Git\\usr\\bin\\bash.exe',
  ].filter(Boolean);
  for (const candidate of candidates) {
    const result = spawnSync(candidate, ['-c', 'exit 0'], {
      encoding: 'utf8',
      timeout: 15000,
      windowsHide: true,
    });
    if (result.status === 0) return candidate;
  }
  return null;
}

function findPowerShell() {
  const candidates = [
    process.env.PWSH,
    'pwsh',
    'powershell',
    'C:\\Windows\\System32\\WindowsPowerShell\\v1.0\\powershell.exe',
  ].filter(Boolean);
  for (const candidate of candidates) {
    const result = spawnSync(candidate, ['-NoProfile', '-Command', 'exit 0'], {
      encoding: 'utf8',
      timeout: 30000,
      windowsHide: true,
    });
    if (result.status === 0) return candidate;
  }
  return null;
}

function runNative(command, args, timeout = 120000) {
  const env = { ...process.env };
  for (const key of ['POSTGRES_CONNECTION', 'BACKUP_ENCRYPTION_KEY', 'PGPASSWORD', 'PGHOST', 'PGPORT', 'PGDATABASE', 'PGUSER']) {
    delete env[key];
  }
  return spawnSync(command, args, {
    cwd: ROOT,
    env,
    encoding: 'utf8',
    timeout,
    windowsHide: true,
  });
}

function runFunctionalSelfTests(bash, powershell) {
  if (bash) {
    for (const file of SH_ENTRY_FILES) {
      const syntax = runNative(bash, ['-n', file], 30000);
      if (syntax.status !== 0) {
        fail(`${file}: bash -n failed: ${(syntax.stderr || '').trim().split('\n')[0]}`);
      }
      const common = runNative(bash, ['-n', 'scripts/backup/backup-common.sh'], 30000);
      if (common.status !== 0) {
        fail(`scripts/backup/backup-common.sh: bash -n failed: ${(common.stderr || '').trim().split('\n')[0]}`);
      }
    }
  } else {
    fail('bash interpreter not found; cannot run shell self-tests (CI requires bash).');
  }

  if (!powershell) {
    fail('PowerShell interpreter not found; cannot run PowerShell self-tests (CI requires pwsh).');
  }

  for (const [file, kind] of SELF_TEST_FILES) {
    if (kind === 'sh' && bash) {
      const result = runNative(bash, [file, '--self-test']);
      if (result.status !== 0) {
        fail(`${file} --self-test failed (exit ${result.status}): ${(result.stderr || result.stdout || '').trim().split('\n').slice(-3).join(' | ')}`);
      } else {
        notes.push(`${file} --self-test OK`);
      }
    }
    if (kind === 'ps1' && powershell) {
      const result = runNative(powershell, ['-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', file, '-SelfTest']);
      if (result.status !== 0) {
        fail(`${file} -SelfTest failed (exit ${result.status}): ${(result.stderr || result.stdout || '').trim().split('\n').slice(-3).join(' | ')}`);
      } else {
        notes.push(`${file} -SelfTest OK`);
      }
    }
  }
}

function runMutationSelfTest(root, files) {
  const mutations = [
    {
      name: 'backup shell without --dry-run',
      file: 'scripts/backup/pg-backup.sh',
      transform: (content) => content.replaceAll('--dry-run', '--plan-only'),
    },
    {
      name: 'backup PowerShell without WhatIf',
      file: 'scripts/backup/pg-backup.ps1',
      transform: (content) => content.replaceAll('[switch]$WhatIf', '[switch]$PlanOnly'),
    },
    {
      name: 'restore shell without --yes',
      file: 'scripts/backup/pg-restore.sh',
      transform: (content) => content.replaceAll('--yes', '--proceed'),
    },
    {
      name: 'restore shell without --allow-production',
      file: 'scripts/backup/pg-restore.sh',
      transform: (content) => content.replaceAll('--allow-production', '--allow-anything'),
    },
    {
      name: 'restore PowerShell without -AllowProduction',
      file: 'scripts/backup/pg-restore.ps1',
      transform: (content) => content.replaceAll('-AllowProduction', '-AllowAnything'),
    },
    {
      name: 'verify shell without checksum verification',
      file: 'scripts/backup/verify-backup.sh',
      transform: (content) => content.replaceAll('verify_checksum', 'skip_checksum'),
    },
    {
      name: 'backup shell with an inline password',
      file: 'scripts/backup/pg-backup.sh',
      transform: (content) => `${content}\nPGPASSWORD="hunter2-hardcoded"\n`,
    },
    {
      name: 'restore shell without production refusal',
      file: 'scripts/backup/pg-restore.sh',
      transform: (content) => content.replaceAll('assert_safe_target', 'allow_any_target'),
    },
  ];

  for (const mutation of mutations) {
    const content = files.get(mutation.file);
    if (!content) {
      fail(`self-test: cannot mutate missing file ${mutation.file}`);
      continue;
    }
    const mutated = mutation.transform(content);
    if (mutated === content) {
      fail(`self-test: mutation "${mutation.name}" did not change ${mutation.file} (stale guard).`);
      continue;
    }
    const mutatedFiles = new Map(files);
    mutatedFiles.set(mutation.file, mutated);
    const found = validateScripts(root, mutatedFiles);
    if (found.length === 0) {
      fail(`self-test: unsafe mutation "${mutation.name}" was accepted by the guard.`);
    } else {
      notes.push(`self-test: rejected "${mutation.name}"`);
    }
  }
}

function main() {
  const selfTest = process.argv.includes('--self-test');
  const files = loadFiles(ROOT, SCRIPT_FILES);
  violations.push(...validateScripts(ROOT, files));
  validateDocs(ROOT);

  const bash = findBash();
  const powershell = findPowerShell();
  runFunctionalSelfTests(bash, powershell);

  if (selfTest) {
    runMutationSelfTest(ROOT, files);
  }

  if (violations.length > 0) {
    console.error('backup-scripts-guard: FAILED');
    for (const violation of violations) console.error(`  - ${violation}`);
    process.exit(1);
  }

  console.log('backup-scripts-guard: OK');
  console.log(`  files checked: ${files.size}`);
  console.log(`  shell interpreter: ${bash || 'missing'}`);
  console.log(`  PowerShell interpreter: ${powershell || 'missing'}`);
  console.log(`  script self-tests: ${SELF_TEST_FILES.length}`);
  for (const note of notes) console.log(`  ${note}`);
}

main();
