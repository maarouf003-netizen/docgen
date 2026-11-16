/**
 * P5-05a — حارس الهجرات غير القابلة للتراجع.
 *
 * يفحص **`Up` فقط** في هجرات `MigrationsPostgres` (لا `Down`: التراجع ليس خطرًا على
 * قاعدة حية أثناء الترقية)، ويفشل عند أي عملية هدم/إعادة تسمية/تعديل نوع:
 *   DropTable, DropColumn, DropSchema, RenameTable, RenameColumn, AlterColumn<T>, DeleteData.
 *
 * الاستثناء يحتاج سطرًا صريحًا في `scripts/migration-safety-allowlist.json` مع سبب
 * (قرار expand/contract موثّق)، لا مجرد `--update` آلي — الهدم قرار واعٍ.
 *
 * ما لا يغطيه: SQL الخام داخل `migrationBuilder.Sql(...)` (يُراجع يدويًا في المراجعة)،
 * لأن تحليله نصيًا عبر أسطر متعددة هشّ.
 *
 * التشغيل: `node scripts/migration-safety-guard.mjs`
 */
import { readFileSync, readdirSync, existsSync } from 'node:fs';
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
const ALLOWLIST_PATH = join(ROOT, 'scripts', 'migration-safety-allowlist.json');

const RULES = [
  { id: 'drop-table', label: 'حذف جدول', regex: /migrationBuilder\.DropTable\s*\(/ },
  { id: 'drop-column', label: 'حذف عمود', regex: /migrationBuilder\.DropColumn\s*[<(]/ },
  { id: 'drop-schema', label: 'حذف مخطط', regex: /migrationBuilder\.DropSchema\s*\(/ },
  { id: 'rename-table', label: 'إعادة تسمية جدول', regex: /migrationBuilder\.RenameTable\s*\(/ },
  { id: 'rename-column', label: 'إعادة تسمية عمود', regex: /migrationBuilder\.RenameColumn\s*[<(]/ },
  { id: 'alter-column', label: 'تغيير نوع عمود', regex: /migrationBuilder\.AlterColumn\s*</ },
  { id: 'delete-data', label: 'حذف بيانات', regex: /migrationBuilder\.DeleteData\s*\(/ },
];

const files = readdirSync(MIGRATIONS_DIR)
  .filter((name) => /^\d{14}_[A-Za-z0-9_]+\.cs$/.test(name))
  .sort();

const findings = [];

for (const file of files) {
  const text = readFileSync(join(MIGRATIONS_DIR, file), 'utf8');
  const upIndex = text.indexOf('protected override void Up(');
  if (upIndex < 0) continue;

  const downIndex = text.indexOf('protected override void Down(', upIndex);
  const upBody = text.slice(upIndex, downIndex > upIndex ? downIndex : undefined);
  const baseLine = text.slice(0, upIndex).split(/\r?\n/).length;

  upBody.split(/\r?\n/).forEach((line, index) => {
    const code = line.replace(/\/\/.*$/, '');
    for (const rule of RULES) {
      if (rule.regex.test(code)) {
        findings.push({
          file,
          rule: rule.id,
          label: rule.label,
          line: baseLine + index,
        });
      }
    }
  });
}

let allowlist = [];
if (existsSync(ALLOWLIST_PATH)) {
  let parsed;
  try {
    // بعض المحررات تكتب BOM؛ نتحمّله بدل الانهيار بمكدّس استثناءات غير مفهوم.
    parsed = JSON.parse(readFileSync(ALLOWLIST_PATH, 'utf8').replace(/^\uFEFF/, ''));
  } catch {
    console.error(
      'migration-safety-guard: ملف الاستثناءات scripts/migration-safety-allowlist.json ليس JSON صالحًا.',
    );
    process.exit(1);
  }

  allowlist = Array.isArray(parsed.entries) ? parsed.entries : [];
}

const allowed = new Set(allowlist.map((entry) => `${entry.file}::${entry.rule}`));
const problems = [];

for (const finding of findings) {
  if (!allowed.has(`${finding.file}::${finding.rule}`)) {
    problems.push(
      `${finding.file}:${finding.line} — ${finding.rule} (${finding.label}) في Up بلا استثناء موثّق.`,
    );
  }
}

for (const entry of allowlist) {
  if (!findings.some((finding) => finding.file === entry.file && finding.rule === entry.rule)) {
    problems.push(
      `قيد استثناء قديم لم يعد مطابقًا: ${entry.file} (${entry.rule}) — احذفه بعد المراجعة.`,
    );
  }
}

if (problems.length > 0) {
  console.error('migration-safety-guard: فشل — عمليات هدم في هجرات Up:');
  for (const problem of problems) {
    console.error(`  - ${problem}`);
  }
  console.error('');
  console.error('اتبع expand/contract: أضف الجديد أولًا، انقل البيانات، ثم احذف في إصدار لاحق.');
  console.error('الاستثناء المقصود يُسجَّل في scripts/migration-safety-allowlist.json مع سبب واضح.');
  process.exit(1);
}

console.log(
  `migration-safety-guard: نظيف — ${files.length} هجرة، لا عمليات هدم في Up` +
    (allowlist.length > 0 ? ` (${allowlist.length} استثناء موثّق).` : '.'),
);
