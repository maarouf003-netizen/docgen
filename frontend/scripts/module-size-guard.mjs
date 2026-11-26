#!/usr/bin/env node
/**
 * P6-02 — حارس حجم الوحدات (ratchet).
 *
 * قاعدة `P6-02`: لا ملف مصدر يتجاوز 600 سطرًا. الملفات القائمة الأكبر من الحد
 * مُدرجة في `FROZEN_MAX_LINES` بقيمها المقيسة، ويمكنها أن **تنقص فقط**؛ وأي ملف
 * جديد يتجاوز الحد يُفشل الحارس. عند تفكيك ملف مُجمَّد وإدخاله تحت الحد تُحذف
 * إضافته من القائمة في التغيير نفسه (وإلا يفشل الحارس كإضافة قديمة).
 *
 * لا يفحص ملفات الاختبار (`.test.`/`.spec.`) ولا التعريفات (`.d.ts`) ولا
 * العميل المولَّد (`api/generated.ts`) لأنها ليست وحدات واجهة قابلة للتفكيك
 * بالمعنى المقصود.
 */
import { readdirSync, readFileSync } from 'node:fs';
import { join, relative, sep } from 'node:path';
import { fileURLToPath } from 'node:url';

export const LIMIT = 600;

/**
 * الملفات القائمة فوق الحد (لقطة 2026-11-26/27، بعد تفكيك `DocumentForm`/`DocumentView`
 * وقسمَي الأطراف `ApplicantSideSections`/`ExecutedSideSections` و`DocumentsList`
 * وثوابت النموذج `documentFormConstants` و`StatusChangeModal` و`UsersManagement`
 * و`InterventionModal` و`EntityRegistryReviewManagement`).
 * القيمة = الحد الأقصى المسموح حاليًا؛ لا تُرفع يدويًا — تُحذف عند التفكيك.
 */
export const FROZEN_MAX_LINES = Object.freeze({
  'src/pages/BranchesManagement.tsx': 870,
  'src/components/entity/BranchManagementModal.tsx': 834,
  'src/components/dashboard/DayRemindersModal.tsx': 725,
});

/** ملفات لا تدخل القياس (اختبارات/تعريفات/مولَّد). */
export function isMeasuredSource(relativePath) {
  const normalized = relativePath.split(sep).join('/');
  if (normalized.endsWith('.d.ts')) return false;
  if (/\.(test|spec)\.(ts|tsx)$/.test(normalized)) return false;
  if (normalized === 'src/api/generated.ts') return false;
  return /\.(ts|tsx)$/.test(normalized);
}

/** عدد الأسطر بلا احتساب السطر الفارغ الأخير الناتج عن سطر جديد ختامي. */
export function countLines(text) {
  const normalized = text.replace(/\r\n/g, '\n');
  const raw = normalized.split('\n').length;
  return normalized.endsWith('\n') ? raw - 1 : raw;
}

/**
 * تقييم القياس — دالة نقية للاختبار الذاتي.
 * @param {{ files: Array<{ path: string, lines: number }>, frozen?: Record<string, number> }} input
 * @returns {{ errors: string[], measured: number }}
 */
export function evaluate({ files, frozen = FROZEN_MAX_LINES }) {
  const errors = [];
  const seen = new Set();
  for (const file of files) {
    const path = file.path.split(sep).join('/');
    if (Object.prototype.hasOwnProperty.call(frozen, path)) {
      seen.add(path);
      if (file.lines > frozen[path]) {
        errors.push(
          `${path}: ${file.lines} سطرًا > الحد المجمّد ${frozen[path]} — لا تُرفع القيمة؛ فكّك الملف.`,
        );
      }
      continue;
    }
    if (file.lines > LIMIT) {
      errors.push(`${path}: ${file.lines} سطرًا > ${LIMIT} — فكّك الملف إلى وحدات أصغر.`);
    }
  }
  for (const path of Object.keys(frozen)) {
    if (!seen.has(path)) {
      errors.push(`${path}: مُدرج في القائمة المجمّدة لكنه غير موجود — احذف الإدخال القديم.`);
    }
  }
  return { errors, measured: files.length };
}

function collectSourceFiles(srcDir) {
  const out = [];
  const walk = (dir) => {
    for (const entry of readdirSync(dir, { withFileTypes: true })) {
      const full = join(dir, entry.name);
      if (entry.isDirectory()) {
        walk(full);
        continue;
      }
      const rel = relative(join(srcDir, '..'), full);
      if (!isMeasuredSource(rel)) continue;
      out.push({ path: rel, lines: countLines(readFileSync(full, 'utf8')) });
    }
  };
  walk(srcDir);
  return out;
}

function selfTest() {
  const cases = [
    {
      name: 'حد 600 مقبول',
      input: { files: [{ path: 'src/a.tsx', lines: 600 }], frozen: {} },
      expected: [],
    },
    {
      name: '601 غير مجمّد مرفوض',
      input: { files: [{ path: 'src/a.tsx', lines: 601 }], frozen: {} },
      expected: ['src/a.tsx: 601 سطرًا > 600 — فكّك الملف إلى وحدات أصغر.'],
    },
    {
      name: 'ملف مجمّد ضمن حده مقبول',
      input: { files: [{ path: 'src/big.tsx', lines: 900 }], frozen: { 'src/big.tsx': 900 } },
      expected: [],
    },
    {
      name: 'نمو ملف مجمّد مرفوض',
      input: { files: [{ path: 'src/big.tsx', lines: 901 }], frozen: { 'src/big.tsx': 900 } },
      expected: ['src/big.tsx: 901 سطرًا > الحد المجمّد 900 — لا تُرفع القيمة؛ فكّك الملف.'],
    },
    {
      name: 'انكماش ملف مجمّد مقبول (يُحذف الإدخال لاحقًا)',
      input: { files: [{ path: 'src/big.tsx', lines: 120 }], frozen: { 'src/big.tsx': 900 } },
      expected: [],
    },
    {
      name: 'إدخال مجمّد بلا ملف مرفوض',
      input: { files: [], frozen: { 'src/gone.tsx': 700 } },
      expected: ['src/gone.tsx: مُدرج في القائمة المجمّدة لكنه غير موجود — احذف الإدخال القديم.'],
    },
  ];

  const exclusions = [
    ['src/a.test.tsx', false],
    ['src/a.spec.ts', false],
    ['src/types.d.ts', false],
    ['src/api/generated.ts', false],
    ['src/a.ts', true],
    ['src/a.tsx', true],
  ];

  let failures = 0;
  for (const testCase of cases) {
    const { errors } = evaluate(testCase.input);
    const ok = JSON.stringify(errors) === JSON.stringify(testCase.expected);
    if (!ok) {
      failures += 1;
      console.error(`✖ ${testCase.name}`);
      console.error(`  متوقع: ${JSON.stringify(testCase.expected)}`);
      console.error(`  فعلي:  ${JSON.stringify(errors)}`);
    } else {
      console.log(`✔ ${testCase.name}`);
    }
  }
  for (const [path, expected] of exclusions) {
    if (isMeasuredSource(path) !== expected) {
      failures += 1;
      console.error(`✖ استثناء القياس: ${path} (متوقع ${expected})`);
    }
  }
  if (failures > 0) {
    console.error(`module-size-guard self-test: ${failures} فشل.`);
    process.exit(1);
  }
  console.log(
    `module-size-guard self-test: ${cases.length + exclusions.length} حالة ناجحة.`,
  );
}

function main() {
  const scriptDir = fileURLToPath(new URL('.', import.meta.url));
  const srcDir = join(scriptDir, '..', 'src');
  const files = collectSourceFiles(srcDir);
  const { errors, measured } = evaluate({ files });
  if (errors.length > 0) {
    console.error('module-size-guard: فشل.');
    for (const error of errors) console.error(`  - ${error}`);
    process.exit(1);
  }
  const frozenCount = Object.keys(FROZEN_MAX_LINES).length;
  console.log(
    `module-size-guard: نظيف — ${measured} ملفًا مصدريًا، ${frozenCount} ملفًا مجمّدًا فوق ${LIMIT} سطرًا.`,
  );
}

if (process.argv.includes('--self-test')) selfTest();
else main();
