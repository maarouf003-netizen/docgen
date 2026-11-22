/**
 * P6-05 — حارس ميزانية حزمة الواجهة.
 *
 * يقرأ `frontend/dist/bundle-report.json` (يُولّده `vite.config.ts` أثناء البناء)
 * ويفشل إذا تجاوزت الحزمة السقوف المعتمدة. الأرقام **بايت خام** قبل gzip (كما في
 * تقرير Vite)، ولا تُقاس أثناء `render` ولا تعتمد على الشبكة.
 *
 * التشغيل: `npm run budget` بعد `npm run build`.
 */
import { readFileSync, existsSync } from 'node:fs';
import { join } from 'node:path';

const ROOT = join(import.meta.dirname, '..');
const REPORT = join(ROOT, 'dist', 'bundle-report.json');

/** السقوف المعتمدة (بايت). تُرفع فقط بتغيير موثّق في `docs/audit/`. */
const BUDGETS = {
  totalBytes: 2_000_000,
  maxChunkBytes: 460_000,
  entryBytes: 300_000,
};

if (!existsSync(REPORT)) {
  console.error('bundle-budget: dist/bundle-report.json غير موجود — شغّل `npm run build` أولًا.');
  process.exit(1);
}

const report = JSON.parse(readFileSync(REPORT, 'utf8'));
if (!Array.isArray(report.files) || report.files.length === 0) {
  console.error('bundle-budget: تقرير الحزمة فارغ أو غير صالح.');
  process.exit(1);
}

const jsFiles = report.files.filter((f) => f.file.endsWith('.js'));
if (jsFiles.length === 0) {
  console.error('bundle-budget: لا ملفات JS في التقرير.');
  process.exit(1);
}

const total = jsFiles.reduce((sum, f) => sum + f.bytes, 0);
const largest = [...jsFiles].sort((a, b) => b.bytes - a.bytes)[0];
const entry = jsFiles.find((f) => /^assets\/index-[^/]+\.js$/.test(f.file));

const failures = [];
if (total > BUDGETS.totalBytes) {
  failures.push(`الإجمالي ${total} > ${BUDGETS.totalBytes} بايت`);
}
if (largest.bytes > BUDGETS.maxChunkBytes) {
  failures.push(`أكبر حزمة ${largest.file} = ${largest.bytes} > ${BUDGETS.maxChunkBytes} بايت`);
}
if (entry && entry.bytes > BUDGETS.entryBytes) {
  failures.push(`حزمة الدخول ${entry.file} = ${entry.bytes} > ${BUDGETS.entryBytes} بايت`);
}

console.log(
  `bundle-budget: JS=${jsFiles.length} · إجمالي=${(total / 1024).toFixed(1)}kB` +
    ` · أكبر=${largest.file} ${(largest.bytes / 1024).toFixed(1)}kB` +
    (entry ? ` · دخول=${(entry.bytes / 1024).toFixed(1)}kB` : ''),
);

if (failures.length > 0) {
  console.error(`bundle-budget: تجاوز السقوف:\n- ${failures.join('\n- ')}`);
  process.exit(1);
}

console.log('bundle-budget: ضمن الميزانية.');
