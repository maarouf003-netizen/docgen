/**
 * `P6-04` — حارس سلامة تشغيل `E2E`.
 *
 * القاعدتان غير القابلتين للتفاوض (انظر `docs/runbooks/e2e.md` و`AGENTS.md`):
 *  1. لا يشير `E2E_BASE_URL` إلى أي مضيف غير محلي إلا عند `E2E_ALLOW_STAGING=1`
 *     **و** كان المضيف يحتوي `staging` صراحةً. الإنتاج/Render/Supabase ممنوع عمليًا
 *     لغياب `staging` في اسمه.
 *  2. قاعدة البيانات المستخدمة **مخصّصة للتشغيل** (اسمها يحتوي `e2e`)، ومضيفها محلي
 *     (أو staging بالراية نفسها)، ولا يوحي اسمها/مضيفها بالإنتاج.
 *
 * الحارس **fail-closed**: أي غياب/غموض في السلسلة يُرفض، ولا تُطبع كلمة المرور أو
 * السلسلة الخام في أي رسالة خطأ (يُذكر المضيف واسم القاعدة فقط).
 *
 * التشغيل:
 *   node scripts/e2e-safety-guard.mjs --self-test
 */
import { fileURLToPath } from 'node:url';
import { resolve } from 'node:path';

const STAGING_FLAG = 'E2E_ALLOW_STAGING';
const LOCAL_HOSTS = new Set(['localhost', '127.0.0.1', '::1']);
const PRODUCTION_HINT = /(?:^|[.\-_/])(prod|production|live)(?:$|[.\-_/])/i;

/** @typedef {{ host: string, staging: boolean }} E2eTarget */
/** @typedef {{ host: string, port: string, database: string, format: 'uri' | 'keyword' }} E2eDatabaseTarget */

/**
 * هل سُمح ببيئة staging صراحةً؟ القيمة المقبولة `1` فقط — لا `true`/`yes`
 * (راية واحدة صريحة تُقرأ في CI ولا تُفعّل بالخطأ).
 * @param {Record<string, string | undefined>} env
 */
export function stagingAllowed(env = process.env) {
  return env[STAGING_FLAG] === '1';
}

/** @param {string} host */
function normalizeHost(host) {
  return host.trim().toLowerCase().replace(/^\[|\]$/g, '');
}

/**
 * يتحقق أن عنوان التطبيق المستهدف محلي أو staging معلن، ويعيد المضيف المطبّع.
 * @param {string} baseUrl
 * @param {Record<string, string | undefined>} env
 * @returns {E2eTarget}
 */
export function assertSafeE2eTarget(baseUrl, env = process.env) {
  if (typeof baseUrl !== 'string' || baseUrl.trim() === '') {
    throw new Error('E2E_BASE_URL is empty. Set it to http://127.0.0.1:5173 (local) or a declared staging URL.');
  }

  let url;
  try {
    url = new URL(baseUrl.trim());
  } catch {
    throw new Error('E2E_BASE_URL is not a valid absolute URL. Use an http(s) URL, for example http://127.0.0.1:5173.');
  }

  if (url.protocol !== 'http:' && url.protocol !== 'https:') {
    throw new Error(`E2E_BASE_URL must use http or https, got "${url.protocol}".`);
  }

  const host = normalizeHost(url.hostname);
  if (host === '') {
    throw new Error('E2E_BASE_URL has no host name.');
  }

  if (LOCAL_HOSTS.has(host)) return { host, staging: false };

  if (!stagingAllowed(env)) {
    throw new Error(
      `Refusing to run E2E against non-local host "${host}". ` +
        'Only localhost/127.0.0.1 are allowed; a staging host requires E2E_ALLOW_STAGING=1 and "staging" in its name.',
    );
  }

  if (!host.includes('staging')) {
    throw new Error(
      `E2E_ALLOW_STAGING=1 but host "${host}" does not contain "staging". Refusing to run against a production-looking host.`,
    );
  }

  return { host, staging: true };
}

/**
 * يحلّل سلسلة اتصال Npgsql (keyword-style) أو URI (`postgres://`).
 * لا يُرجع كلمة المرور إطلاقًا.
 * @param {string} connectionString
 * @returns {E2eDatabaseTarget}
 */
export function parseE2eConnectionString(connectionString) {
  if (typeof connectionString !== 'string' || connectionString.trim() === '') {
    throw new Error('E2E_DB_CONNECTION is empty. Provide a disposable E2E database connection string.');
  }

  const raw = connectionString.trim();
  if (/^postgres(?:ql)?:\/\//i.test(raw)) {
    let url;
    try {
      url = new URL(raw);
    } catch {
      throw new Error('E2E_DB_CONNECTION is not a valid postgres:// URI (value hidden).');
    }
    // Npgsql يقرأ بعض معاملات الاستعلام كإعدادات اتصال؛ السماح بها يفتح تجاوزًا
    // لفحص المضيف/القاعدة (مثل ?Host=remote). نرفضها كليًا بدل تحليل جزئي.
    if (url.search !== '' || url.hash !== '') {
      throw new Error(
        'E2E_DB_CONNECTION URI must not carry query parameters or fragments (value hidden). ' +
          'Use a plain postgres:// URL or a keyword-style connection string.',
      );
    }
    const database = decodeURIComponent(url.pathname.replace(/^\//, ''));
    return {
      host: normalizeHost(url.hostname),
      port: url.port || '5432',
      database,
      format: 'uri',
    };
  }

  /** @type {Map<string, string>} */
  const values = new Map();
  for (const segment of raw.split(';')) {
    const separator = segment.indexOf('=');
    if (separator <= 0) continue;
    const key = segment.slice(0, separator).trim().toLowerCase();
    const value = segment.slice(separator + 1).trim();
    if (!key) continue;
    // مفاتيح مكررة غامضة: Npgsql قد يختار الأول ونحن نقرأ الأخير (أو العكس)،
    // فيصبح الفحص قابلًا للتجاوز. نرفض التكرار بدل اختيار تفسير.
    if (values.has(key)) {
      throw new Error(
        `E2E_DB_CONNECTION contains the key "${key}" more than once (value hidden). Refusing ambiguous input.`,
      );
    }
    values.set(key, value);
  }

  return {
    host: normalizeHost(values.get('host') ?? values.get('server') ?? ''),
    port: values.get('port') ?? '5432',
    database: values.get('database') ?? values.get('initial catalog') ?? '',
    format: 'keyword',
  };
}

/**
 * يرفض أي قاعدة ليست مخصّصة لـE2E (اسم يحتوي `e2e`، مضيف محلي/staging، ولا أثر إنتاجي).
 * @param {string} connectionString
 * @param {Record<string, string | undefined>} env
 * @returns {E2eDatabaseTarget}
 */
export function assertSafeE2eDatabase(connectionString, env = process.env) {
  const parsed = parseE2eConnectionString(connectionString);

  if (parsed.host === '') {
    throw new Error('E2E_DB_CONNECTION has no Host/Server (value hidden). Refusing to guess.');
  }
  if (parsed.database === '') {
    throw new Error('E2E_DB_CONNECTION has no Database (value hidden). Refusing to guess.');
  }

  const hostLower = normalizeHost(parsed.host);
  if (!LOCAL_HOSTS.has(hostLower)) {
    const stagingHost = stagingAllowed(env) && hostLower.includes('staging');
    if (!stagingHost) {
      throw new Error(
        `Refusing E2E database on non-local host "${parsed.host}". ` +
          'Use a local disposable database; a staging host requires E2E_ALLOW_STAGING=1 and "staging" in its name.',
      );
    }
  }

  if (PRODUCTION_HINT.test(parsed.database) || PRODUCTION_HINT.test(parsed.host)) {
    throw new Error(
      `Refusing E2E database that looks production ("${parsed.host}/${parsed.database}"). ` +
        'Use a disposable database named like docgen_e2e.',
    );
  }

  if (!/e2e/i.test(parsed.database)) {
    throw new Error(
      `Refusing E2E database "${parsed.database}" on "${parsed.host}": its name must contain "e2e" ` +
        '(for example docgen_e2e) so it cannot be a shared/production database.',
    );
  }

  return parsed;
}

/** حالات الاختبار الذاتي — تُستخدم من `--self-test` ومن اختبارات `vitest`. */
export function safetySelfTestCases() {
  return [
    {
      name: 'local base URL is accepted',
      run: () => assertSafeE2eTarget('http://127.0.0.1:5173', {}),
      expect: { host: '127.0.0.1', staging: false },
    },
    {
      name: 'IPv6 loopback is accepted',
      run: () => assertSafeE2eTarget('http://[::1]:5173', {}),
      expect: { host: '::1', staging: false },
    },
    {
      name: 'Render production URL is refused',
      run: () => assertSafeE2eTarget('https://docgen.onrender.com', {}),
      throws: /non-local host "docgen\.onrender\.com"/,
    },
    {
      name: 'staging host is refused without the explicit flag',
      run: () => assertSafeE2eTarget('https://docgen-staging.example.test', {}),
      throws: /E2E_ALLOW_STAGING=1/,
    },
    {
      name: 'staging host is accepted with the flag',
      run: () => assertSafeE2eTarget('https://docgen-staging.example.test', { E2E_ALLOW_STAGING: '1' }),
      expect: { host: 'docgen-staging.example.test', staging: true },
    },
    {
      name: 'flag without "staging" in the host is refused',
      run: () => assertSafeE2eTarget('https://docgen.example.test', { E2E_ALLOW_STAGING: '1' }),
      throws: /does not contain "staging"/,
    },
    {
      name: 'non-http scheme is refused',
      run: () => assertSafeE2eTarget('ftp://127.0.0.1:5173', {}),
      throws: /http or https/,
    },
    {
      name: 'userinfo cannot disguise a remote host',
      run: () => assertSafeE2eTarget('http://127.0.0.1@docgen.onrender.com', {}),
      throws: /non-local host/,
    },
    {
      name: 'local E2E database is accepted (keyword)',
      run: () => assertSafeE2eDatabase('Host=127.0.0.1;Port=5432;Database=docgen_e2e;Username=postgres', {}),
      expect: { host: '127.0.0.1', database: 'docgen_e2e', format: 'keyword' },
    },
    {
      name: 'local E2E database is accepted (URI)',
      run: () => assertSafeE2eDatabase('postgresql://postgres@localhost:5432/docgen_e2e', {}),
      expect: { host: 'localhost', database: 'docgen_e2e', format: 'uri' },
    },
    {
      name: 'production-looking database name is refused',
      run: () => assertSafeE2eDatabase('Host=127.0.0.1;Database=docgen_production_e2e;Username=postgres', {}),
      throws: /looks production/,
    },
    {
      name: 'database name without e2e is refused',
      run: () => assertSafeE2eDatabase('Host=127.0.0.1;Database=docgen_test;Username=postgres', {}),
      throws: /must contain "e2e"/,
    },
    {
      name: 'remote database host is refused',
      run: () => assertSafeE2eDatabase('Host=db.example.com;Database=docgen_e2e;Username=postgres', {}),
      throws: /non-local host/,
    },
    {
      name: 'remote database host needs staging in the name',
      run: () =>
        assertSafeE2eDatabase('Host=db.example.com;Database=docgen_e2e;Username=postgres', { E2E_ALLOW_STAGING: '1' }),
      throws: /non-local host/,
    },
    {
      name: 'missing database name is refused instead of guessed',
      run: () => assertSafeE2eDatabase('Host=127.0.0.1;Username=postgres', {}),
      throws: /no Database/,
    },
    {
      name: 'duplicate host key is refused (parser-confusion bypass)',
      run: () =>
        assertSafeE2eDatabase('Host=db.example.com;Database=docgen_e2e;Host=127.0.0.1;Username=postgres', {}),
      throws: /more than once/,
    },
    {
      name: 'URI query parameters are refused (Npgsql override bypass)',
      run: () =>
        assertSafeE2eDatabase('postgres://postgres@localhost:5432/docgen_e2e?Host=db.example.com', {}),
      throws: /query parameters/,
    },
  ];
}

/** يشغّل الحالات ويعيد رمز خروج (0 = نجاح). */
export function runSelfTest() {
  const failures = [];
  for (const testCase of safetySelfTestCases()) {
    try {
      const actual = testCase.run();
      if (testCase.throws) {
        failures.push(`${testCase.name}: expected a rejection, got success`);
        continue;
      }
      if (testCase.expect) {
        for (const [key, value] of Object.entries(testCase.expect)) {
          if (actual?.[key] !== value) {
            failures.push(
              `${testCase.name}: expected ${key}=${JSON.stringify(value)}, got ${JSON.stringify(actual?.[key])}`,
            );
          }
        }
      }
    } catch (error) {
      const message = error instanceof Error ? error.message : String(error);
      if (!testCase.throws) {
        failures.push(`${testCase.name}: unexpected rejection: ${message}`);
      } else if (!testCase.throws.test(message)) {
        failures.push(`${testCase.name}: rejection did not match ${testCase.throws}: ${message}`);
      }
    }
  }

  if (failures.length > 0) {
    for (const failure of failures) console.error(`FAIL ${failure}`);
    return 1;
  }

  console.log(`e2e-safety-guard: ${safetySelfTestCases().length}/${safetySelfTestCases().length} self-test cases passed.`);
  return 0;
}

// تشغيل CLI فقط (عند استدعاء الملف مباشرةً)، فلا تُنفَّذ الحالات عند الاستيراد.
const invokedDirectly = Boolean(process.argv[1]) && resolve(process.argv[1]) === fileURLToPath(import.meta.url);
if (invokedDirectly) {
  if (!process.argv.includes('--self-test')) {
    console.error('usage: node scripts/e2e-safety-guard.mjs --self-test');
    process.exitCode = 2;
  } else {
    process.exitCode = runSelfTest();
  }
}
