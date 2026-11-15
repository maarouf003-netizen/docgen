/**
 * P7-02a — حارس قائمة هجرات النشر (الجزء الثابت من P7-02).
 *
 * يفشل إذا وُجدت هجرة في
 * `backend/src/DocGenerator.Infrastructure/Persistence/MigrationsPostgres`
 * غير مذكورة بالاسم في **كلا** الموضعين الملزِمين:
 *   1) `RUN_GUIDE.md` §9 — «هجرات بانتظار التطبيق (قائمة تراكمية)».
 *   2) `AGENTS.md` — «تذكير الهجرات المعلّقة».
 *
 * السبب: `AGENTS.md` ينص أن كل هجرة جديدة تُضاف إلى القائمتين **في نفس التغيير**،
 * لأن نسيان التطبيق يؤدي إلى رفض إقلاع مقصود في الإنتاج (`AutoMigrate=false`).
 * الحارس يمنع هذا النسيان بدل الاعتماد على المراجعة اليدوية.
 *
 * ما لا يغطيه (يبقى في P7-02 الكامل): مقارنة الهجرات بـ`__EFMigrationsHistory`
 * على staging — تحتاج قاعدة حقيقية ولا تُنفَّذ في CI بلا بيئة.
 *
 * التشغيل: `node scripts/migration-watchlist-guard.mjs`
 */
import { readFileSync, readdirSync } from 'node:fs';
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

const TARGETS = [
  { file: 'RUN_GUIDE.md', heading: 'هجرات بانتظار التطبيق', level: 3 },
  { file: 'AGENTS.md', heading: 'تذكير الهجرات المعلّقة', level: 2 },
];

/**
 * يستخرج نص قسم Markdown بعنوان يحتوي `headingFragment`، حتى أول عنوان بمستوى
 * مساوٍ أو أعلى.
 *
 * @param {string} text
 * @param {string} headingFragment
 * @param {number} level
 * @returns {string | null}
 */
function extractSection(text, headingFragment, level) {
  const lines = text.split(/\r?\n/);
  const marker = `${'#'.repeat(level)} `;
  const start = lines.findIndex((line) => line.startsWith(marker) && line.includes(headingFragment));
  if (start === -1) return null;

  let end = lines.length;
  for (let i = start + 1; i < lines.length; i += 1) {
    const heading = /^(#{1,6}) /.exec(lines[i]);
    if (heading && heading[1].length <= level) {
      end = i;
      break;
    }
  }

  return lines.slice(start, end).join('\n');
}

/** @returns {{ file: string, className: string }[]} */
function listMigrations() {
  return readdirSync(MIGRATIONS_DIR)
    .filter((name) => /^\d{14}_[A-Za-z0-9_]+\.cs$/.test(name))
    .map((name) => ({
      file: name,
      className: name.replace(/^\d{14}_/, '').replace(/\.cs$/, ''),
    }))
    .sort((left, right) => left.file.localeCompare(right.file));
}

const migrations = listMigrations();
if (migrations.length === 0) {
  console.error(`migration-watchlist-guard: لم يُعثر على أي هجرة في ${MIGRATIONS_DIR}`);
  process.exit(1);
}

const problems = [];
const sections = new Map();

for (const target of TARGETS) {
  const text = readFileSync(join(ROOT, target.file), 'utf8');
  const section = extractSection(text, target.heading, target.level);

  if (section === null) {
    problems.push(
      `${target.file}: تعذّر العثور على قسم «${target.heading}» — إن أُعيدت تسمية القسم فحدّث الحارس.`,
    );
    continue;
  }

  sections.set(target.file, section);

  // المطابقة داخل بنود القائمة فقط (`- ...`)، لا في أي نص حر داخل القسم: ذكر الاسم
  // في شرح لا يُعدّ تسجيلًا للهجرة. البنود هي عقد القائمة في الملفين.
  const bulletLines = section.split(/\r?\n/).filter((line) => /^\s*[-*]\s/.test(line));

  for (const migration of migrations) {
    // الهجرة قد تُذكر باسم صنفها وحده (`P0_06_OptimisticConcurrency`) أو داخل
    // معرّفها الكامل (`20261114160957_P0_06_OptimisticConcurrency`)، فنسمح بشرطة
    // سفلية قبل الاسم ونمنع أي حرف آخر ملتصق به.
    const pattern = new RegExp(
      `(?<![A-Za-z0-9])${migration.className.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')}(?![A-Za-z0-9_])`,
    );
    if (!bulletLines.some((line) => pattern.test(line))) {
      problems.push(
        `${target.file}: الهجرة \`${migration.className}\` (${migration.file}) ليست بندًا في قسم «${target.heading}».`,
      );
    }
  }
}

if (problems.length > 0) {
  console.error('migration-watchlist-guard: فشل — هجرات غير مسجّلة في قوائم النشر:');
  for (const problem of problems) {
    console.error(`  - ${problem}`);
  }
  console.error('');
  console.error('أضف كل هجرة جديدة إلى RUN_GUIDE.md §9 وAGENTS.md في نفس التغيير (قاعدة إلزامية).');
  process.exit(1);
}

const pending = (sections.get('RUN_GUIDE.md') ?? '')
  .split(/\r?\n/)
  .filter((line) => /^- \[ \]/.test(line.trim())).length;

console.log(
  `migration-watchlist-guard: نظيف — ${migrations.length} هجرة مسجّلة في RUN_GUIDE.md وAGENTS.md` +
    ` (${pending} سطرًا غير مُشطوب في قائمة RUN_GUIDE).`,
);
