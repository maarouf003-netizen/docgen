/**
 * P3-07 — حارس SLO على مخرجات منصة القياس (`P3-01`/`P3-02`).
 *
 * يقرأ ملف JSON المُولَّد من `DocGenerator.Benchmarks` (يحوي `queries[].p95Ms`)
 * ويقارن كل استعلام بحدّه حسب المقياس (10000/50000). يفشل صراحةً عند أي تجاوز،
 * وعند غياب استعلام متوقع (لا نجاح كاذب).
 *
 * التشغيل:
 *   node scripts/slo-check.mjs docs/audit/measurements/20261121-p3-02-50000.json
 *   node scripts/slo-check.mjs <file> --scale 10000
 */
import { readFileSync } from 'node:fs';

const THRESHOLDS = {
  10000: {
    'documents-list-page1': 250,
    'documents-list-page100': 300,
    'documents-list-page100-projected': 300,
    'documents-list-page100-cursor': 150,
    'documents-list-text': 600,
    'portal-list-page1': 250,
    'portal-list-text': 400,
    'portal-file-detail': 200,
    'stats-dashboard': 300,
    'stats-monthly': 200,
    'stats-branches': 200,
    'head-alert-summaries': 50,
  },
  50000: {
    'documents-list-page1': 500,
    'documents-list-page100': 600,
    'documents-list-page100-projected': 600,
    'documents-list-page100-cursor': 300,
    'documents-list-text': 1600,
    'portal-list-page1': 500,
    'portal-list-text': 1000,
    'portal-file-detail': 300,
    'stats-dashboard': 500,
    'stats-monthly': 300,
    'stats-branches': 300,
    'head-alert-summaries': 100,
  },
};

function fail(message) {
  console.error(`slo-check: ${message}`);
  process.exit(1);
}

const args = process.argv.slice(2);
const file = args.find((a) => !a.startsWith('--'));
if (!file) fail('مرّر ملف قياس JSON (مثال: docs/audit/measurements/20261121-p3-02-50000.json).');

const scaleIndex = args.indexOf('--scale');
const scaleArg = scaleIndex >= 0 ? Number(args[scaleIndex + 1]) : undefined;

let report;
try {
  report = JSON.parse(readFileSync(file, 'utf8'));
} catch (error) {
  fail(`تعذّر قراءة/تحليل ${file}: ${error.message}`);
}

const scale = scaleArg ?? report.environment?.scale;
const thresholds = THRESHOLDS[scale];
if (!thresholds) fail(`لا حدود SLO للمقياس ${scale ?? '(غير محدد)'} — المسموح 10000/50000.`);
if (!Array.isArray(report.queries) || report.queries.length === 0) {
  fail('الملف لا يحوي queries — ليس تقرير قياس صالحًا.');
}

const rows = [];
const failures = [];
for (const [id, limit] of Object.entries(thresholds)) {
  const query = report.queries.find((q) => q.id === id);
  if (!query) {
    failures.push(`${id}: غائب من التقرير`);
    continue;
  }
  const p95 = Number(query.p95Ms);
  if (!Number.isFinite(p95)) {
    failures.push(`${id}: p95 غير رقمي`);
    continue;
  }
  rows.push({ id, p95, limit, ok: p95 <= limit });
  if (p95 > limit) failures.push(`${id}: p95=${p95.toFixed(1)}ms > ${limit}ms`);
}

console.log(`slo-check: ${file} (scale=${scale})`);
for (const row of rows) {
  console.log(
    `${row.ok ? 'PASS' : 'FAIL'}  ${row.id.padEnd(28)} p95=${row.p95.toFixed(1).padStart(8)}ms  ≤ ${row.limit}ms`,
  );
}

if (failures.length > 0) {
  console.error(`slo-check: تجاوزات/نواقص:\n- ${failures.join('\n- ')}`);
  process.exit(1);
}

console.log('slo-check: كل الاستعلامات ضمن SLO.');
