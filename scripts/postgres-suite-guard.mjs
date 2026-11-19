/**
 * P1-01i/c — حارس تكامل مجموعات `PostgreSQL` في وظيفة `backend-postgres`.
 *
 * المشكلة التي يغلقها: كانت قائمة المجموعات في `ci.yml` **يدوية**، فإضافة مجموعة
 * `PostgreSQL` جديدة بلا إضافتها إلى القائمة تعني «خضرة صامتة» بلا تحقق من تنفيذها.
 * الحارس هنا يكتشف المجموعات من المصدر نفسه، ثم يتحقق في ملف `trx` أن كل مجموعة:
 *   - نُفِّذت فعلًا (نتيجة ناجحة واحدة على الأقل)،
 *   - وبلا أي تخطٍّ (`NotExecuted`)،
 *   - وبلا فشل.
 * ويضيف شبكة أمان: أي `NotExecuted` خارج المجموعات المكتشفة يُفشل أيضًا، لأن وظيفة
 * `backend-postgres` تعمل بـ`DOCGEN_REQUIRE_POSTGRES=1` فلا يصحّ فيها تخطٍّ صامت
 * لاختبار `PostgreSQL` مستقل (‏`PostgresBaselineTests`، `IdempotencyStorePostgresTests`،
 * `RF006PostgresFactoryTests`…).
 *
 * الاكتشاف (لا يعتمد على أسماء مكتوبة هنا):
 *   1) صنف معلَّم `[Collection(PostgresApiTestCollection.Name)]` (المجموعة المشتركة).
 *   2) صنف `IClassFixture<PostgresApiFactory>` (مصنع مستقل لكل صنف).
 *   3) صنف عضو في تجميعة معرَّفة بـ`[CollectionDefinition("...")]` حامل
 *      `ICollectionFixture<PostgresApiFactory>`.
 *   4) أي صنف اختبار في ملف ينشئ `new PostgresApiFactory(...)` بنفسه.
 * ويُستثنى: أصناف `[CollectionDefinition]`، والمجردة/الساكنة.
 *
 * التشغيل:
 *   node scripts/postgres-suite-guard.mjs <path/to/api-postgres.trx>
 *   node scripts/postgres-suite-guard.mjs --self-test
 */
import { readdirSync, readFileSync, mkdtempSync, writeFileSync, mkdirSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';

const ROOT = join(fileURLToPath(new URL('.', import.meta.url)), '..');
const DEFAULT_TESTS_DIR = join(ROOT, 'backend', 'tests', 'DocGenerator.Api.Tests');

const POSTGRES_FIXTURE = 'PostgresApiFactory';
const SHARED_COLLECTION = 'PostgresApiTestCollection.Name';

/**
 * يقرأ أصناف الاختبار من مجلد ويصنّف أيها يعمل على `PostgreSQL`.
 *
 * @param {string} testsDir
 * @returns {{ name: string, file: string }[]}
 */
export function discoverSuites(testsDir) {
  /** @type {Map<string, string>} */
  const collectionFixtures = new Map();
  /** @type {{ name: string, file: string }[]} */
  const suites = [];

  const files = readdirSync(testsDir).filter((name) => name.endsWith('.cs')).sort();

  for (const file of files) {
    const text = readFileSync(join(testsDir, file), 'utf8');

    // تعريفات التجميعات: اسم التجميعة → نوع الـfixture.
    const definition = /\[CollectionDefinition\((?:[^)\]]*)\)\][\s\S]{0,400}?class\s+\w+[^{]*ICollectionFixture<(\w+)>/g;
    for (const match of text.matchAll(definition)) {
      const named = /\[CollectionDefinition\(\s*"([^"]+)"\s*\)\]/.exec(match[0]);
      if (named) collectionFixtures.set(named[1], match[1]);
    }

    const ownsFactory = /new\s+PostgresApiFactory\s*\(/.test(text);

    const classPattern = /((?:\[[^\]]*\]\s*\r?\n\s*)*)(?:public|internal)\s+(?:sealed\s+|abstract\s+|static\s+)*class\s+(\w+)([^{]*)\{/g;
    for (const match of text.matchAll(classPattern)) {
      const attributes = match[1] ?? '';
      const className = match[2];
      const bases = match[3] ?? '';

      if (/\[CollectionDefinition\(/.test(attributes)) continue;
      if (/\b(abstract|static)\b/.test(bases)) continue;

      const shared = attributes.includes(SHARED_COLLECTION);
      const classFixture = new RegExp(`IClassFixture\\s*<\\s*${POSTGRES_FIXTURE}\\s*>`).test(bases);
      const customCollection = [...attributes.matchAll(/\[Collection\(\s*"([^"]+)"\s*\)\]/g)]
        .some((m) => collectionFixtures.get(m[1]) === POSTGRES_FIXTURE);

      if (shared || classFixture || customCollection || ownsFactory) {
        suites.push({ name: className, file });
      }
    }
  }

  return suites.sort((a, b) => a.name.localeCompare(b.name));
}

/**
 * يتحقق من ملف `trx` أن كل مجموعة اكتُشفت نُفِّذت بلا تخطٍّ ولا فشل.
 * يرمي خطأً يسرد كل انحراف بدل الاكتفاء بأول واحد.
 *
 * @param {string} trxText
 * @param {{ name: string, file: string }[]} suites
 * @returns {{ name: string, passed: number }[]}
 */
export function verifyTrx(trxText, suites) {
  if (suites.length === 0) {
    throw new Error('لم تُكتشف أي مجموعة PostgreSQL — تحقق من مسار مشروع الاختبارات.');
  }

  /** @type {Map<string, { passed: number, failed: number, skipped: number }>} */
  const results = new Map();
  const resultPattern = /<UnitTestResult[^>]*testName="([^"]*)"[^>]*outcome="([^"]*)"/g;
  for (const match of trxText.matchAll(resultPattern)) {
    results.set(match[1], {
      passed: match[2] === 'Passed' ? 1 : 0,
      failed: match[2] === 'Failed' ? 1 : 0,
      skipped: match[2] === 'NotExecuted' ? 1 : 0,
    });
  }

  const problems = [];
  const verified = [];
  for (const suite of suites) {
    let passed = 0;
    let failed = 0;
    let skipped = 0;
    for (const [testName, outcome] of results) {
      if (!testName.includes(suite.name)) continue;
      passed += outcome.passed;
      failed += outcome.failed;
      skipped += outcome.skipped;
    }

    if (passed === 0) {
      problems.push(`${suite.name} (${suite.file}): لم تُنفَّذ أي حالة ناجحة.`);
    }
    if (skipped > 0) {
      problems.push(`${suite.name} (${suite.file}): ${skipped} حالة متخطّاة (NotExecuted).`);
    }
    if (failed > 0) {
      problems.push(`${suite.name} (${suite.file}): ${failed} حالة فاشلة.`);
    }
    verified.push({ name: suite.name, passed });
  }

  if (problems.length > 0) {
    throw new Error(`انحراف في تشغيل مجموعات PostgreSQL:\n- ${problems.join('\n- ')}`);
  }

  // شبكة أمان لما لا يُكتشف كمجموعة (اختبارات Postgres مستقلة مثل
  // PostgresBaseline/IdempotencyStore/RF006): في وظيفة backend-postgres يجب
  // ألا يوجد أي NotExecuted إطلاقًا، لأن DOCGEN_REQUIRE_POSTGRES=1 يمنع التخطي.
  const skippedOutside = [...results]
    .filter(([, outcome]) => outcome.skipped > 0)
    .map(([testName]) => testName)
    .filter((testName) => !suites.some((suite) => testName.includes(suite.name)));
  if (skippedOutside.length > 0) {
    throw new Error(
      `تخطٍّ صامت خارج المجموعات المكتشفة (${skippedOutside.length}): ${skippedOutside
        .slice(0, 5)
        .join(', ')}`,
    );
  }

  return verified;
}

/** اختبار ذاتي معزول (لا يقرأ المستودع ولا يكتب فيه). */
function selfTest() {
  const dir = mkdtempSync(join(tmpdir(), 'pg-suite-guard-'));
  const write = (name, body) => writeFileSync(join(dir, name), body);

  write('SharedSuite.cs', '[Collection(PostgresApiTestCollection.Name)]\npublic class SharedSuite\n{\n}\n');
  write(
    'FixtureSuite.cs',
    'public class FixtureSuite\n    : IClassFixture<PostgresApiFactory>, IAsyncLifetime\n{\n}\n',
  );
  write(
    'CustomCollectionSuite.cs',
    '[CollectionDefinition("custom-pg")]\npublic sealed class CustomCollection : ICollectionFixture<PostgresApiFactory>\n{\n}\n\n[Collection("custom-pg")]\npublic class CustomCollectionSuite\n{\n}\n',
  );
  write('OwnFactorySuite.cs', 'public class OwnFactorySuite\n{\n    void Seed() { using var f = new PostgresApiFactory(); }\n}\n');
  write(
    'SqliteSuite.cs',
    '[Collection(ApiTestCollection.Name)]\npublic class SqliteSuite\n{\n    readonly ApiFactory _f = new();\n}\n',
  );
  write('Helper.cs', 'public sealed class PostgresApiFactory : ApiFactory\n{\n}\n');

  const suites = discoverSuites(dir);
  const names = suites.map((s) => s.name);
  const expected = ['CustomCollectionSuite', 'FixtureSuite', 'OwnFactorySuite', 'SharedSuite'];
  if (JSON.stringify(names) !== JSON.stringify(expected)) {
    throw new Error(`الاكتشاف خاطئ: ${JSON.stringify(names)} ≠ ${JSON.stringify(expected)}`);
  }

  const trx = (rows) =>
    `<TestRun><Results>${rows
      .map(
        ([name, outcome]) =>
          `<UnitTestResult testName="DocGenerator.Api.Tests.${name}.Case" outcome="${outcome}" />`,
      )
      .join('')}</Results></TestRun>`;

  verifyTrx(
    trx(expected.map((name) => [name, 'Passed'])),
    suites,
  );

  const expectThrow = (text, label) => {
    try {
      verifyTrx(text, suites);
    } catch {
      return;
    }
    throw new Error(`لم يفشل الحارس في الحالة المتوقعة: ${label}`);
  };

  expectThrow(trx([['SharedSuite', 'Passed']]), 'مجموعة غائبة');
  expectThrow(
    trx([
      ...expected.filter((n) => n !== 'FixtureSuite').map((n) => [n, 'Passed']),
      ['FixtureSuite', 'NotExecuted'],
    ]),
    'مجموعة متخطّاة',
  );
  expectThrow(
    trx([
      ...expected.filter((n) => n !== 'OwnFactorySuite').map((n) => [n, 'Passed']),
      ['OwnFactorySuite', 'Failed'],
    ]),
    'مجموعة فاشلة',
  );
  expectThrow(
    trx([
      ...expected.map((name) => [name, 'Passed']),
      ['UnrelatedPostgresProbe', 'NotExecuted'],
    ]),
    'تخطٍّ خارج المجموعات المكتشفة',
  );

  console.log(`postgres-suite-guard: self-test نظيف (${expected.length + 1} حالات اكتشاف/تحقق).`);
}

/** @returns {string} مسار trx من الوسيط أو من `TestResults` داخل المستودع. */
function locateTrx(arg) {
  if (arg) return arg;
  const base = join(ROOT, 'backend', 'tests', 'DocGenerator.Api.Tests', 'TestResults');
  const found = readdirSync(base, { recursive: true }).filter((name) => String(name).endsWith('api-postgres.trx'));
  if (found.length === 0) throw new Error('لم يُعثر على api-postgres.trx — مرّر المسار صريحًا.');
  return join(base, String(found[found.length - 1]));
}

function main() {
  const arg = process.argv[2];
  if (arg === '--self-test') {
    selfTest();
    return;
  }

  const testsDir = process.argv[3] ?? DEFAULT_TESTS_DIR;
  const suites = discoverSuites(testsDir);
  const verified = verifyTrx(readFileSync(locateTrx(arg), 'utf8'), suites);
  console.log(`postgres-suite-guard: نظيف — ${verified.length} مجموعة على PostgreSQL نُفِّذت بلا تخطٍّ.`);
  for (const suite of verified) {
    console.log(`  - ${suite.name}: ${suite.passed}`);
  }
}

if (process.argv[1] && process.argv[1].endsWith('postgres-suite-guard.mjs')) {
  try {
    main();
  } catch (error) {
    console.error(`postgres-suite-guard: ${error.message}`);
    process.exitCode = 1;
  }
}
