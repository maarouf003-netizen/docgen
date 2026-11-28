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
import { join, relative, resolve, sep } from 'node:path';
import { fileURLToPath } from 'node:url';

export const REDUCED_MOTION_MEDIA = '@media (prefers-reduced-motion: reduce)';

const CLASS_LITERAL = /`(?:[^`\\]|\\.)*`|"(?:[^"\\]|\\.)*"|'(?:[^'\\]|\\.)*'/g;

/** بادئات تعبير صالح لبدء regex — تُستخدم لتجنّب قراءة `/*` داخل regex كتعليق. */
const REGEX_PREFIX_CHARS = '(,=:[!&|?{};+-*%<>~^';
const REGEX_PREFIX_WORDS = new Set([
  'return', 'typeof', 'instanceof', 'in', 'of', 'new', 'delete', 'void',
  'throw', 'case', 'do', 'else', 'yield', 'await',
]);

/** هل تكتمل قبل `//` بادئة رابط (`https:`…)؟ عندها `//` نصّ لا تعليق. */
const URL_SCHEME_TAIL = /(?:^|[^A-Za-z0-9+.-])(?:https?|wss?|ftp|file|blob|data|tel|mailto):$/i;

/** `animate-none` أو قيمته الاعتباطية `animate-[none…]` — ليست حركة. */
function isMotionNone(motionClass) {
  return motionClass === 'animate-none' || /^animate-\[none(?:[^\]]*)\]$/.test(motionClass);
}

/** هل البادئة تحمل variant `motion-safe:` حقيقيًا (بحدود `:` لا داخل `[…]`)؟ */
function hasMotionSafeVariant(token) {
  const prefix = token.slice(0, token.lastIndexOf('animate-'));
  return /(?:^|:)motion-safe:/.test(prefix);
}

function regexCanStart(out) {
  let index = out.length - 1;
  while (index >= 0 && /\s/.test(out[index])) index -= 1;
  if (index < 0) return true;
  if (REGEX_PREFIX_CHARS.includes(out[index])) return true;
  const word = /([A-Za-z_$][A-Za-z0-9_$]*)$/.exec(out.slice(0, index + 1));
  return word !== null && REGEX_PREFIX_WORDS.has(word[1]);
}

/**
 * يزيل تعليقات JS/TS/JSX (`//`، `/* *\/`، `{/* *\/}`) قبل المسح، فلا يُنشئ مثال
 * مشروح في تعليق إيجابيةً كاذبة. ماسح خطّي يحترم: النصوص، القوالب المتداخلة
 * (`${…}`)، الروابط (`https://`) في نص JSX، و`regex` (فلا تبتلع `/*` داخله بقية
 * الملف). بلا أي regex بتكرار متداخل (CodeQL ReDoS).
 */
export function stripComments(text) {
  let out = '';
  let i = 0;
  const stack = [{ kind: 'code', braces: 0 }];
  while (i < text.length) {
    const frame = stack[stack.length - 1];
    const ch = text[i];
    const next = text[i + 1];
    if (frame.kind === 'template') {
      if (ch === '\\') {
        out += ch + (next ?? '');
        i += 2;
        continue;
      }
      if (ch === '`') {
        out += ch;
        i += 1;
        stack.pop();
        continue;
      }
      if (ch === '$' && next === '{') {
        out += '${';
        i += 2;
        stack.push({ kind: 'code', braces: 0 });
        continue;
      }
      out += ch;
      i += 1;
      continue;
    }
    if (ch === '/' && next === '/') {
      if (URL_SCHEME_TAIL.test(out)) {
        out += '//';
        i += 2;
        continue;
      }
      while (i < text.length && text[i] !== '\n') i += 1;
      continue;
    }
    if (ch === '/' && next === '*') {
      i += 2;
      while (i < text.length && !(text[i] === '*' && text[i + 1] === '/')) i += 1;
      i += 2;
      continue;
    }
    if (ch === '/' && regexCanStart(out)) {
      out += ch;
      i += 1;
      let inClass = false;
      while (i < text.length) {
        const current = text[i];
        out += current;
        if (current === '\\') {
          out += text[i + 1] ?? '';
          i += 2;
          continue;
        }
        i += 1;
        if (current === '[') inClass = true;
        else if (current === ']') inClass = false;
        else if (current === '/' && !inClass) break;
        else if (current === '\n') break;
      }
      while (i < text.length && /[a-z]/i.test(text[i])) {
        out += text[i];
        i += 1;
      }
      continue;
    }
    if (ch === '"' || ch === "'") {
      out += ch;
      i += 1;
      while (i < text.length) {
        const current = text[i];
        out += current;
        if (current === '\\') {
          out += text[i + 1] ?? '';
          i += 2;
          continue;
        }
        i += 1;
        if (current === ch) break;
      }
      continue;
    }
    if (ch === '`') {
      out += ch;
      i += 1;
      stack.push({ kind: 'template' });
      continue;
    }
    if (ch === '{') {
      frame.braces += 1;
    } else if (ch === '}') {
      // `}` يغلق تعبير `${…}` فقط عند عمق صفر داخل النص النمطي.
      if (frame.braces === 0 && stack.length > 1) {
        stack.pop();
        i += 1;
        continue;
      }
      frame.braces = Math.max(0, frame.braces - 1);
    }
    out += ch;
    i += 1;
  }
  return out;
}

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
  const code = stripComments(text);
  if (/transition-all/.test(code) || /transition:\s*all/.test(code)) {
    violations.push('transition-all ممنوع — اذكر الخاصية صراحة');
  }
  if (/\btype\s*=\s*["']date["']/.test(code)) {
    violations.push('type="date" ممنوع — حقل التاريخ نص حر بصيغة 1/8/2026');
  }

  for (const literal of classLiterals(code)) {
    const tokens = literal.slice(1, -1).split(/\s+/).filter(Boolean);
    for (const token of tokens) {
      // القوالب المتداخلة تُبقي علامات الاقتباس الداخلية ملتصقة بالكلمة — تُنزع قبل الفحص.
      const cleaned = token.replace(/^['"]+|['"]+$/g, '');
      // يلتقط `animate-*` مع أي بادئة variants (`sm:`/`hover:`/`md:hover:`) لأن
      // الحركة تبقى حركة، ويستثني `motion-safe:` (المسار الآمن) و`animate-none`.
      if (!/(?:^|:)animate-/.test(cleaned)) continue;
      const motionClass = cleaned.slice(cleaned.lastIndexOf('animate-'));
      if (isMotionNone(motionClass)) continue;
      if (!hasMotionSafeVariant(cleaned)) {
        violations.push(`حركة بلا motion-safe: (${motionClass})`);
      }
    }
    if (literal.includes('outline-none')) {
      const hasVisibleFocus = /(?:focus-visible|focus):ring-|(?:focus-visible|focus):outline-(?!none)/.test(literal);
      if (!hasVisibleFocus) {
        violations.push('outline-none بلا بديل تركيز مرئي في النص نفسه');
      }
    }
  }

  // مسح خام للقوالب المتداخلة التي لا يلتقطها استخراج النصوص المقتبسة
  // (مثل `` `a${x ? `sm:animate-pulse` : ''}` ``) — نفس القاعدة والاستثناءات.
  // تقسيم خطّي إلى «كلمات» بدل تعبير متداخل التكرار (تجنّب ReDoS).
  const rawTokens = (code.match(/[A-Za-z0-9:_[\]-]+/g) ?? []).filter((token) => token.includes('animate-'));
  for (const token of rawTokens) {
    const motionClass = token.slice(token.lastIndexOf('animate-'));
    if (isMotionNone(motionClass)) continue;
    if (!hasMotionSafeVariant(token)) {
      violations.push(`حركة بلا motion-safe: (${motionClass})`);
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
      name: 'animate-none مقبول',
      text: 'className="animate-none"',
      expected: [],
    },
    {
      name: 'حركة ببادئة breakpoint مرفوضة',
      text: 'className="sm:animate-pulse"',
      expected: ['حركة بلا motion-safe: (animate-pulse)'],
    },
    {
      name: 'حركة مع hover مرفوضة',
      text: 'className="md:hover:animate-spin"',
      expected: ['حركة بلا motion-safe: (animate-spin)'],
    },
    {
      name: 'motion-safe مع breakpoint مقبول',
      text: 'className="sm:motion-safe:animate-pulse"',
      expected: [],
    },
    {
      name: 'قالب متداخل بلا motion-safe مرفوض',
      text: "const x = `a${c ? `sm:animate-pulse` : ''}`",
      expected: ['حركة بلا motion-safe: (animate-pulse)'],
    },
    {
      name: 'قالب متداخل مع motion-safe مقبول',
      text: "const x = `a${c ? `motion-safe:animate-pulse` : ''}`",
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
    {
      name: 'تعليق سطري لا يُحتسب',
      text: '// className="animate-pulse"',
      expected: [],
    },
    {
      name: 'تعليق كتلي لا يُحتسب (و`transition-all` فيه)',
      text: '/* transition-all animate-spin */',
      expected: [],
    },
    {
      name: 'تعليق JSX لا يُحتسب',
      text: 'const x = <div>{/* <span className="animate-ping" /> */}</div>',
      expected: [],
    },
    {
      name: '`//` داخل نص لا يقصّ ما بعده',
      text: 'const u = "https://x.test"; const c = "animate-pulse";',
      expected: ['حركة بلا motion-safe: (animate-pulse)'],
    },
    {
      name: '`//` داخل قالب لا يقصّ ما بعده، و`animate-[…]` ملتقط',
      text: 'const u = `https://x.test`; const c = `animate-[pulse_2s_infinite]`;',
      expected: ['حركة بلا motion-safe: (animate-[pulse_2s_infinite])'],
    },
    {
      name: 'قيمة animate-[…] مع motion-safe مقبولة',
      text: 'className="motion-safe:animate-[pulse_2s_infinite]"',
      expected: [],
    },
    {
      name: 'motion-safe داخل تعليق لا يعفي الحركة الحقيقية',
      text: '// motion-safe:animate-pulse\nclassName="animate-pulse"',
      expected: ['حركة بلا motion-safe: (animate-pulse)'],
    },
    {
      name: '`/*` داخل regex لا يبتلع بقية الملف',
      text: 'const re = /a\\/*b/; const c = "animate-pulse";',
      expected: ['حركة بلا motion-safe: (animate-pulse)'],
    },
    {
      name: '`//` في رابط نص JSX لا يبتلع بقية السطر',
      text: 'const x = <p>https://y</p>; const c = "animate-pulse";',
      expected: ['حركة بلا motion-safe: (animate-pulse)'],
    },
    {
      name: 'animate-[none] مقبول (ليست حركة)',
      text: 'className="animate-[none]"',
      expected: [],
    },
    {
      name: 'motion-safe داخل variant اعتباطي لا يعفي الحركة',
      text: 'className="data-[motion-safe:x]:animate-pulse"',
      expected: ['حركة بلا motion-safe: (animate-pulse)'],
    },
    {
      name: 'variant آمن داخل قالب مقتبس داخليًا مقبول',
      text: "const c = `${x ? 'motion-safe:animate-pulse' : ''}`",
      expected: [],
    },
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
else if (process.argv[1] !== undefined && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) main();
