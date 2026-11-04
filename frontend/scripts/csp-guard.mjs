/**
 * حارس `CSP` الثابت (W0-CSP): يفحص **مخرجات البناء** لا المصدر، لأن السياسة المفروضة في
 * الإنتاج تُطبَّق على ما يقدّمه `Vite` فعلًا. يكشف أربعة أصناف تُبيّض الصفحة أو تُبطل
 * الحماية عند فرض `Content-Security-Policy`:
 *
 *  1. سكربت مضمّن (`<script>` بلا `src`) — محجوب بـ`script-src 'self'` بلا `'unsafe-inline'`.
 *  2. معالج حدث مضمّن (`onclick=` ونحوه) — محجوب بالسياسة نفسها.
 *  3. أصل خارجي أو `javascript:` في وسوم تحميل الموارد — محجوب بـ`default-src 'self'`.
 *  4. `eval` / `new Function` / `WebAssembly` في حزمة `JS` — محجوب بـ`script-src 'self'`
 *     (لا `'unsafe-eval'` ولا `'wasm-unsafe-eval'`).
 *  5. `url(https://…)` أو `@import https://…` في `CSS` — محجوب بـ`style-src 'self'`
 *     (والخطوط الخارجية خرق مباشر لسياسة الخطوط المحلية).
 *
 * يُشغَّل بعد `npm run build` (محليًا، وفي صورة `Docker`، وفي وظيفة `frontend` بـ`CI`):
 *   `cd frontend && npm run guard`
 * الدوال التصديرية نقية لتُختبر في `csp-guard.test.mjs` بلا نظام ملفات.
 */
import { readFileSync, readdirSync, statSync, existsSync } from 'node:fs';
import { join, relative, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const FRONTEND_ROOT = join(import.meta.dirname, '..');
const DIST = join(FRONTEND_ROOT, 'dist');

/** وسوم تحمّل موارد؛ روابط `<a href>` الملاحية خارج هذا النطاق عمدًا. */
const RESOURCE_URL_ATTRIBUTES = new Map([
  ['script', ['src']],
  ['link', ['href']],
  ['img', ['src', 'srcset']],
  ['source', ['src', 'srcset']],
  ['video', ['src', 'poster']],
  ['audio', ['src']],
  ['track', ['src']],
  ['iframe', ['src']],
  ['embed', ['src']],
  ['object', ['data']],
]);

/** مخططات مسموحة لكل وسم بحسب السياسة: `img-src` وحدها تسمح `data:`/`blob:` (و`<source>` داخل `picture`). */
const DATA_SCHEME_ALLOWED_TAGS = new Set(['img', 'source']);

const TAG_PATTERN = /<([a-zA-Z][\w:-]*)((?:"[^"]*"|'[^']*'|[^>"'])*)>/g;
const ATTRIBUTE_PATTERN = /([^\s"'=<>`/]+)(?:\s*=\s*(?:"([^"]*)"|'([^']*)'|([^\s"'=<>`]+)))?/g;
const SCRIPT_PATTERN = /<script\b([^>]*)>([\s\S]*?)<\/script\s*>/gi;

/**
 * `@import` و`url(...)` في `CSS` في تعبير واحد حتى لا يُحتسب `@import url(https://…)` مرتين:
 * البديل الأول يبتلع `url(` التابع له، فيتقدّم المطابق إلى ما بعد العنوان.
 */
const CSS_URL_PATTERN =
  /@import\s+(?:url\(\s*)?(?:"([^"]*)"|'([^']*)'|([^'")\s;]+))|url\(\s*(?:"([^"]*)"|'([^']*)'|([^)'"\s]*))\s*\)/gi;

function parseAttributes(raw) {
  const attributes = new Map();
  for (const match of raw.matchAll(ATTRIBUTE_PATTERN)) {
    const name = match[1].toLowerCase();
    const value = match[2] ?? match[3] ?? match[4] ?? '';
    attributes.set(name, value);
  }
  return attributes;
}

function isExternalUrl(value) {
  const url = value.trim();
  return (
    /^[a-zA-Z][a-zA-Z\d+.-]*:/.test(url) || // http:, https:, javascript:, data:, ...
    url.startsWith('//') // protocol-relative
  );
}

function isRelativeOrSameOriginPath(value) {
  // أي قيمة بلا مخطط ولا `//` تُحلّ مقابل أصل المستند نفسه — بما فيها المسارات النسبية.
  return !isExternalUrl(value);
}

/**
 * يفحص مستند `index.html` المبني. القيمة المعادة قائمة مخالفات نصية (فارغة = نظيف).
 * @param {string} html
 * @returns {string[]}
 */
export function analyzeHtml(html) {
  const violations = [];

  for (const match of html.matchAll(SCRIPT_PATTERN)) {
    const attributes = parseAttributes(match[1]);
    if (attributes.has('src'))
      continue;
    if (match[2].trim() !== '')
      violations.push('سكربت مضمّن بلا src محجوب بـ script-src \'self\'');
  }

  for (const tagMatch of html.matchAll(TAG_PATTERN)) {
    const tag = tagMatch[1].toLowerCase();
    const attributes = parseAttributes(tagMatch[2]);

    for (const [name, value] of attributes) {
      if (name.startsWith('on') && name.length > 2)
        violations.push(`معالج حدث مضمّن <${tag} ${name}> محجوب بـ script-src 'self'`);

      if (/^\s*javascript:/i.test(value))
        violations.push(`مخطط javascript: في <${tag} ${name}> مرفوض`);

      if (tag === 'base' && name === 'href' && !isRelativeOrSameOriginPath(value))
        violations.push(`<base href="${value}"> خارج الأصل يخالف base-uri 'self'`);
    }

    const resourceAttributes = RESOURCE_URL_ATTRIBUTES.get(tag);
    if (!resourceAttributes)
      continue;

    for (const attribute of resourceAttributes) {
      if (!attributes.has(attribute))
        continue;

      for (const candidate of attributeUrls(attribute, attributes.get(attribute))) {
        if (candidate === '')
          continue;
        if (DATA_SCHEME_ALLOWED_TAGS.has(tag) && /^\s*(data|blob):/i.test(candidate))
          continue;
        if (isExternalUrl(candidate)) {
          violations.push(
            `أصل غير محلي في <${tag} ${attribute}="${candidate}"> يخالف default-src 'self'`,
          );
        }
      }
    }
  }

  return dedupe(violations);
}

/** `srcset` قائمة مفصولة بفواصل، وكل عنصر قد ينتهي بوصف مقاس. */
function splitUrlList(value) {
  return value
    .split(',')
    .map((entry) => entry.trim().split(/\s+/)[0] ?? '')
    .filter((entry) => entry !== '');
}

/** عنوان واحد في السمات المفردة (`src`/`href`/`poster`)، وقائمة في `srcset` فقط. */
function attributeUrls(attribute, value) {
  return attribute === 'srcset' ? splitUrlList(value) : [value.trim()].filter((url) => url !== '');
}

/**
 * يجمع مراجع الأصول المحلية تحت `/assets/` من وسوم الموارد في المستند، بغضّ النظر عن نوع
 * علامات التنصيص — القراءة عبر محلّل السمات نفسه تمنع تفويت `src='/assets/…'`.
 * @param {string} html
 * @returns {string[]}
 */
function collectAssetReferences(html) {
  const references = new Set();
  for (const tagMatch of html.matchAll(TAG_PATTERN)) {
    const tag = tagMatch[1].toLowerCase();
    const resourceAttributes = RESOURCE_URL_ATTRIBUTES.get(tag);
    if (!resourceAttributes)
      continue;

    const attributes = parseAttributes(tagMatch[2]);
    for (const attribute of resourceAttributes) {
      if (!attributes.has(attribute))
        continue;
      for (const candidate of attributeUrls(attribute, attributes.get(attribute))) {
        if (candidate.startsWith('/assets/'))
          references.add(candidate);
      }
    }
  }
  return [...references];
}

/**
 * يفحص حزمة `JS` مبنية ضد تعليمات `CSP` التي تمنع التقييم الديناميكي.
 * @param {string} source
 * @returns {string[]}
 */
export function analyzeScript(source) {
  const violations = [];
  if (/\beval\s*\(/.test(source))
    violations.push("eval(...) محجوب بـ script-src 'self' (لا 'unsafe-eval')");
  if (/\bnew\s+Function\s*\(/.test(source))
    violations.push("new Function(...) محجوب بـ script-src 'self'");
  else if (/(?<![\w.$])Function\s*\(/.test(source))
    violations.push("استدعاء Function(...) كمُنشئ محجوب بـ script-src 'self'");
  if (/\bWebAssembly\s*\./.test(source))
    violations.push("WebAssembly محجوب بـ script-src 'self' (لا 'wasm-unsafe-eval')");
  return dedupe(violations);
}

/**
 * يفحص ورقة أنماط مبنية ضد `style-src 'self'`: أي `url(...)` أو `@import` إلى أصل خارجي
 * يمنعه المتصفح (وقد يُبيّض أجزاء من الواجهة)، ما عدا `data:`/`blob:` المسموحين للصور والخطوط.
 * @param {string} source
 * @returns {string[]}
 */
export function analyzeCss(source) {
  const violations = [];

  for (const match of source.matchAll(CSS_URL_PATTERN)) {
    const isImport = match[1] !== undefined || match[2] !== undefined || match[3] !== undefined;
    const candidate = (isImport ? match[1] ?? match[2] ?? match[3] : match[4] ?? match[5] ?? match[6]) ?? '';
    const url = candidate.trim();
    if (url === '' || /^\s*(data|blob):/i.test(url))
      continue;
    if (!isExternalUrl(url))
      continue;

    violations.push(
      isImport
        ? `استيراد غير محلي في @import "${url}" يخالف style-src 'self'`
        : `أصل غير محلي في url(...) "${url}" يخالف style-src 'self'`,
    );
  }

  return dedupe(violations);
}

function walkFiles(dir, out = []) {
  for (const entry of readdirSync(dir)) {
    const full = join(dir, entry);
    if (statSync(full).isDirectory()) walkFiles(full, out);
    else out.push(full);
  }
  return out;
}

/**
 * يفحص مجلد `dist` كاملًا: المستند + حزم `JS` + وجود الملفات المشار إليها تحت `/assets/`.
 * @param {string} [distDir]
 * @returns {{ violations: string[], filesChecked: number }}
 */
export function analyzeDist(distDir = DIST) {
  const indexPath = join(distDir, 'index.html');
  if (!existsSync(indexPath)) {
    return {
      violations: [`لا يوجد ${relative(FRONTEND_ROOT, indexPath)} — شغّل npm run build قبل الحارس`],
      filesChecked: 0,
    };
  }

  const html = readFileSync(indexPath, 'utf8');
  const violations = analyzeHtml(html).map((violation) => `dist/index.html: ${violation}`);

  // أصول `/assets/...` المشار إليها يجب أن تكون موجودة فعلًا: مرجع مكسور يعني صفحة بيضاء
  // لا علاقة لها بـ`CSP`، لكنه يفشل في نفس اللحظة ويستحق نفس الحارس.
  for (const reference of collectAssetReferences(html)) {
    const assetPath = join(distDir, reference.replace(/^\/+/, '').split(/[?#]/)[0]);
    if (!existsSync(assetPath))
      violations.push(`dist/index.html: الملف المشار إليه ${reference} غير موجود في dist`);
  }

  const assetsDir = join(distDir, 'assets');
  const assetFiles = existsSync(assetsDir) ? walkFiles(assetsDir) : [];
  const scriptFiles = assetFiles.filter((file) => file.endsWith('.js'));
  const styleFiles = assetFiles.filter((file) => file.endsWith('.css'));

  for (const file of scriptFiles) {
    for (const violation of analyzeScript(readFileSync(file, 'utf8'))) {
      violations.push(`${relative(FRONTEND_ROOT, file)}: ${violation}`);
    }
  }

  for (const file of styleFiles) {
    for (const violation of analyzeCss(readFileSync(file, 'utf8'))) {
      violations.push(`${relative(FRONTEND_ROOT, file)}: ${violation}`);
    }
  }

  return {
    violations: dedupe(violations),
    filesChecked: scriptFiles.length + styleFiles.length + 1,
  };
}

function dedupe(items) {
  return [...new Set(items)];
}

const isMain =
  process.argv[1] !== undefined &&
  resolve(process.argv[1]) === fileURLToPath(import.meta.url);

if (isMain) {
  const { violations, filesChecked } = analyzeDist();
  if (violations.length > 0) {
    console.error(
      `csp-guard: ${violations.length} مخالفة (فُحص ${filesChecked} ملفًا):\n- ${violations.join('\n- ')}`,
    );
    process.exit(1);
  }
  console.log(`csp-guard: نظيف — ${filesChecked} ملفًا متوافقًا مع سياسة CSP المفروضة.`);
}
