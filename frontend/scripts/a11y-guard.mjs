#!/usr/bin/env node
/**
 * P6-03b — حارس الحركة والبؤرة (ثابت، بلا حزم).
 *
 * يمنع أنماطًا تُخالف قواعد الواجهة الملزمة في `AGENTS.md`:
 *  - `transition-all` / `transition: all` (يُذكر الخاصية صراحة).
 *  - حركة بلا `motion-safe:` (مثل `animate-pulse` بدل `motion-safe:animate-pulse`).
 *  - `focus:outline-none` بلا بديل تركيز مرئي في النص نفسه (`focus-visible:ring-*`
 *    أو `focus:ring-*` أو `focus-visible:outline-*`).
 *  - `type="date"` (حقول التواريخ نصية حرة بالصيغ الخمس).
 *  - تجاوزات viewport (`user-scalable=no`/`maximum-scale=1`).
 *  - غياب كتلة `prefers-reduced-motion` من `src/index.css`.
 *
 * لا يفحص الاختبارات ولا الملف المولَّد (ليست واجهة قابلة للتعديل بالمعنى المقصود).
 * كل قاعدة دالة نقية مُصدَّرة كي يثبتها `--self-test` قبل تشغيلها على المصدر.
 */
import { readdirSync, readFileSync } from 'node:fs';
import { join, relative, sep } from 'node:path';
import { fileURLToPath } from 'node:url';

export const REDUCED_MOTION_MEDIA = '@media (prefers-reduced-motion: reduce)';

const CLASS_LITERAL = /`(?:[^`\\]|\\.)*`|"(?:[^"\\]|\\.)*"|'(?:[^'\\]|\\.)*'/g;

/** يستخرج نصوص الأصناف المحتملة (بين علامات اقتباس) من ملف مصدر. */
export function classLiterals(text) {
  return text.match(CLASS_LITERAL) ?? [];
}

/**
 * انتهاكات ملف مصدر واحد — دالة نقية.
 * @returns {string[]} رسائل قصيرة (بلا مسارات) ليثبتها الاختبار الذاتي.
 */
export function sourceViolations(text) {
  const violations = [];
  if (/transition-all/.test(text) || /transition:\s*all/.test(text)) {
    violations.push('transition-all ممنوع — اذكر الخاصية صراحة');
  }
  if (/\btype\s*=\s*["']date["']/.test(text)) {
    violations.push('type="date" ممنوع — حقل التاريخ نص حر بصيغة 1/8/2026');
  }

  for (const literal of classLiterals(text)) {
    const tokens = literal.slice(1, -1).split(/\s+/).filter(Boolean);
    for (const token of tokens) {
      if (/^animate-/.test(token)) {
        violations.push(`حركة بلا motion-safe: (${token})`);
      }
    }
    if (literal.includes('outline-none')) {
      const hasVisibleFocus = /(?:focus-visible|focus):ring-|(?:focus-visible|focus):outline-(?!none)/.test(literal);
      if (!hasVisibleFocus) {
        violations.push('outline-none بلا بديل تركيز مرئي في النص نفسه');
      }
    }
  }

  return [...new Set(violations)];
}

/** انتهاكات CSS: كتلة تقليل الحركة إلزامية في ملف الأنماط الجذري. */
export function cssViolations(text) {
  return text.includes(REDUCED_MOTION_MEDIA)
    ? []
    : [`src/index.css يفتقد ${REDUCED_MOTION_MEDIA}`];
}

/** انتهاكات HTML: منع تعطيل التكبير. */
export function htmlViolations(text) {
  const violations = [];
  if (/user-scalable\s*=\s*["']?no/i.test(text)) {
    violations.push('user-scalable=no ممنوع');
  }
  if (/maximum-scale\s*=\s*["']?1(?:\.0)?/i.test(text)) {
    violations.push('maximum-scale=1 ممنوع');
  }
  return violations;
}

function isMeasuredSource(relativePath) {
  const normalized = relativePath.split(sep).join('/');
  if (/\.(test|spec)\.(ts|tsx)$/.test(normalized)) return false;
  if (normalized === 'src/api/generated.ts') return false;
  return /\.(ts|tsx)$/.test(normalized);
}

function collectSources(srcDir) {
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
      out.push({ path: rel.split(sep).join('/'), text: readFileSync(full, 'utf8') });
    }
  };
  walk(srcDir);
  return out;
}

function selfTest() {
  const cases = [
    { name: 'transition-colors مقبول', text: 'className="transition-colors"', expected: [] },
    {
      name: 'transition-all مرفوض',
      text: 'className="transition-all"',
      expected: ['transition-all ممنوع — اذكر الخاصية صراحة'],
    },
    {
      name: 'حركة بلا motion-safe مرفوضة',
      text: 'className="animate-pulse"',
      expected: ['حركة بلا motion-safe: (animate-pulse)'],
    },
    {
      name: 'motion-safe animate مقبول',
      text: 'className="motion-safe:animate-pulse"',
      expected: [],
    },
    {
      name: 'outline-none بلا بديل مرفوض',
      text: 'className="focus:outline-none"',
      expected: ['outline-none بلا بديل تركيز مرئي في النص نفسه'],
    },
    {
      name: 'outline-none مع حلقة مقبول',
      text: 'className="focus:outline-none focus-visible:ring-2"',
      expected: [],
    },
    {
      name: 'type=date مرفوض',
      text: '<input type="date" />',
      expected: ['type="date" ممنوع — حقل التاريخ نص حر بصيغة 1/8/2026'],
    },
    { name: 'type=text مقبول', text: '<input type="text" />', expected: [] },
  ];

  let failures = 0;
  for (const testCase of cases) {
    const actual = sourceViolations(testCase.text);
    const ok = JSON.stringify(actual) === JSON.stringify(testCase.expected);
    if (!ok) {
      failures += 1;
      console.error(`✖ ${testCase.name}`);
      console.error(`  متوقع: ${JSON.stringify(testCase.expected)}`);
      console.error(`  فعلي:  ${JSON.stringify(actual)}`);
    } else {
      console.log(`✔ ${testCase.name}`);
    }
  }

  const cssChecks = [
    ['كتلة reduced-motion مقبولة', `${REDUCED_MOTION_MEDIA} { * { animation: none } }`, []],
    ['غياب كتلة reduced-motion مرفوض', 'body { margin: 0 }', [`src/index.css يفتقد ${REDUCED_MOTION_MEDIA}`]],
  ];
  for (const [name, text, expected] of cssChecks) {
    const ok = JSON.stringify(cssViolations(text)) === JSON.stringify(expected);
    if (!ok) {
      failures += 1;
      console.error(`✖ ${name}`);
    } else {
      console.log(`✔ ${name}`);
    }
  }

  const htmlChecks = [
    ['viewport عادي مقبول', 'content="width=device-width, initial-scale=1"', []],
    ['user-scalable=no مرفوض', 'content="width=device-width, user-scalable=no"', ['user-scalable=no ممنوع']],
    ['maximum-scale=1 مرفوض', 'content="width=device-width, maximum-scale=1.0"', ['maximum-scale=1 ممنوع']],
  ];
  for (const [name, text, expected] of htmlChecks) {
    const ok = JSON.stringify(htmlViolations(text)) === JSON.stringify(expected);
    if (!ok) {
      failures += 1;
      console.error(`✖ ${name}`);
    } else {
      console.log(`✔ ${name}`);
    }
  }

  const exclusions = [
    ['src/a.test.tsx', false],
    ['src/a.spec.ts', false],
    ['src/api/generated.ts', false],
    ['src/a.tsx', true],
  ];
  for (const [path, expected] of exclusions) {
    if (isMeasuredSource(path) !== expected) {
      failures += 1;
      console.error(`✖ استثناء القياس: ${path}`);
    }
  }

  if (failures > 0) {
    console.error(`a11y-guard self-test: ${failures} فشل.`);
    process.exit(1);
  }
  console.log(
    `a11y-guard self-test: ${cases.length + cssChecks.length + htmlChecks.length + exclusions.length} حالة ناجحة.`,
  );
}

function main() {
  const scriptDir = fileURLToPath(new URL('.', import.meta.url));
  const frontendDir = join(scriptDir, '..');
  const sources = collectSources(join(frontendDir, 'src'));
  const errors = [];

  for (const file of sources) {
    for (const violation of sourceViolations(file.text)) {
      errors.push(`${file.path}: ${violation}`);
    }
  }
  const css = readFileSync(join(frontendDir, 'src', 'index.css'), 'utf8');
  for (const violation of cssViolations(css)) errors.push(violation);

  const html = readFileSync(join(frontendDir, 'index.html'), 'utf8');
  for (const violation of htmlViolations(html)) errors.push(`index.html: ${violation}`);

  if (errors.length > 0) {
    console.error('a11y-guard: فشل.');
    for (const error of errors) console.error(`  - ${error}`);
    process.exit(1);
  }
  console.log(`a11y-guard: نظيف — ${sources.length} ملفًا مصدريًا + index.css + index.html.`);
}

if (process.argv.includes('--self-test')) selfTest();
else main();
