/**
 * P7-02b — التحقق من تطابق ملفات الهجرات مع تاريخ قاعدة البيانات الفعلي.
 *
 * يكمل `migration-watchlist-guard.mjs` (الذي يتحقق من توثيق الملفات فقط):
 *   1) يستخرج معرّفات الهجرات من `MigrationsPostgres` (بلا `.Designer.cs`).
 *   2) يشغّل `dotnet ef migrations list` على القاعدة الممرَّرة (يقرأ
 *      `__EFMigrationsHistory` فعليًا).
 *   3) يفشل إذا وُجدت هجرة معلّقة (Pending)، أو ملف بلا صف في التاريخ، أو
 *      صف في التاريخ بلا ملف (انحراف).
 *
 * القاعدة: `DOCGEN_MIGRATION_CONNECTION` أو `DOCGEN_TEST_POSTGRES`. غيابها = فشل
 * صريح لا تخطٍّ صامت. التشغيل المحلي:
 *   dotnet ef database update --context DocGeneratorPostgresDbContext \
 *     --connection "<conn>" --no-build
 *   node scripts/migration-history-check.mjs
 */
import { spawnSync } from 'node:child_process';
import { readdirSync } from 'node:fs';
import { join } from 'node:path';

const ROOT = join(import.meta.dirname, '..');
const MIGRATIONS_DIR = join(
  ROOT,
  'backend',
  'src',
  'DocGenerator.Infrastructure',
  'Persistence',
  'MigrationsPostgres',
);
const INFRA_PROJECT = join(
  ROOT,
  'backend',
  'src',
  'DocGenerator.Infrastructure',
  'DocGenerator.Infrastructure.csproj',
);
const API_PROJECT = join(ROOT, 'backend', 'src', 'DocGenerator.Api', 'DocGenerator.Api.csproj');

const MIGRATION_FILE = /^(\d{14}_[A-Za-z0-9_]+)\.cs$/;
const EF_LINE = /^(\d{14}_[A-Za-z0-9_]+)(\s+\(Pending\))?$/;

function fail(message) {
  console.error(`migration-history-check: ${message}`);
  process.exit(1);
}

function fileMigrations() {
  return readdirSync(MIGRATIONS_DIR)
    .map((name) => MIGRATION_FILE.exec(name)?.[1])
    .filter((id) => id !== undefined)
    .sort();
}

function runMigrationsList(connection) {
  const result = spawnSync(
    'dotnet',
    [
      'ef',
      'migrations',
      'list',
      '--context',
      'DocGeneratorPostgresDbContext',
      '--project',
      INFRA_PROJECT,
      '--startup-project',
      API_PROJECT,
      '--connection',
      connection,
      '--no-build',
    ],
    { cwd: ROOT, encoding: 'utf8', env: { ...process.env, POSTGRES_CONNECTION: connection } },
  );

  const output = `${result.stdout ?? ''}\n${result.stderr ?? ''}`.trim();
  if (result.error) fail(`تعذّر تشغيل dotnet ef: ${result.error.message}`);
  if (result.status !== 0) {
    fail(`dotnet ef migrations list فشل (exit ${result.status}):\n${output}`);
  }
  return output;
}

function parseListed(output) {
  const listed = new Map();
  for (const raw of output.split(/\r?\n/)) {
    const match = EF_LINE.exec(raw.trim());
    if (!match) continue;
    listed.set(match[1], { pending: Boolean(match[2]) });
  }
  return listed;
}

const connection =
  process.env.DOCGEN_MIGRATION_CONNECTION ?? process.env.DOCGEN_TEST_POSTGRES;
if (!connection || !connection.trim()) {
  fail(
    'سلسلة اتصال PostgreSQL إلزامية: عيّن DOCGEN_MIGRATION_CONNECTION أو DOCGEN_TEST_POSTGRES (فشل صريح لا تخطٍّ).',
  );
}

const files = fileMigrations();
if (files.length === 0) fail('لا توجد ملفات هجرة في MigrationsPostgres — فشل صريح.');

const output = runMigrationsList(connection.trim());
const listed = parseListed(output);
if (listed.size === 0) {
  fail(`dotnet ef لم يُعد أي هجرة. المخرجات:\n${output}`);
}

const pending = [...listed.entries()].filter(([, state]) => state.pending).map(([id]) => id);
const missingInHistory = files.filter((id) => !listed.has(id));
const orphanInHistory = [...listed.keys()].filter((id) => !files.includes(id));

if (pending.length > 0) fail(`هجرات معلّقة غير مطبقة:\n- ${pending.join('\n- ')}`);
if (missingInHistory.length > 0) {
  fail(`هجرات في الملفات بلا صف في __EFMigrationsHistory:\n- ${missingInHistory.join('\n- ')}`);
}
if (orphanInHistory.length > 0) {
  fail(`صفوف في __EFMigrationsHistory بلا ملف هجرة:\n- ${orphanInHistory.join('\n- ')}`);
}

console.log(
  `migration-history-check: نظيف — الملفات ${files.length}، المطبَّق ${listed.size}، المعلّق 0.`,
);
