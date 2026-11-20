/**
 * `P1-08` §5 — مولّد كتلة «التحقق» للقطات `docs/audit` من ملف `trx`.
 *
 * المشكلة التي يغلقها: كانت أرقام (الإجمالي/المتخطّى/المدة/الفاشلة) تُنسخ يدويًا من
 * مخرجات `dotnet test` إلى اللقطة، فتضيع الدقة أو تُنقل أرقام تشغيل قديم.
 *
 * التشغيل:
 *   node scripts/test-report.mjs <path/to/results.trx> [--label "Application/PostgreSQL"]
 *   node scripts/test-report.mjs --self-test
 */
import { readFileSync } from 'node:fs';

/** يحلّل وسم `<UnitTestResult …>` إلى { testName, outcome, seconds }. */
export function parseResults(trxText) {
  const rows = [];
  const tagPattern = /<UnitTestResult\b[^>]*>/g;
  for (const tag of trxText.match(tagPattern) ?? []) {
    const name = /testName="([^"]*)"/.exec(tag)?.[1];
    const outcome = /outcome="([^"]*)"/.exec(tag)?.[1];
    const duration = /duration="([^"]*)"/.exec(tag)?.[1];
    if (!name || !outcome) continue;

    rows.push({
      testName: decodeXml(name),
      outcome,
      seconds: duration ? durationToSeconds(duration) : 0,
    });
  }

  return rows;
}

/** يبني ملخصًا رقميًا من صفوف النتائج (والمدة الجدارية إن توفرت في `Times`). */
export function summarize(rows, wallSeconds = null) {
  const count = (outcome) => rows.filter((row) => row.outcome === outcome).length;
  const passed = count('Passed');
  const failed = count('Failed');
  const skipped = count('NotExecuted');
  const sum = rows.reduce((total, row) => total + row.seconds, 0);
  const seconds = wallSeconds ?? sum;

  return {
    total: rows.length,
    passed,
    failed,
    skipped,
    seconds: Math.round(seconds * 10) / 10,
    failedTests: rows.filter((row) => row.outcome === 'Failed').map((row) => row.testName),
  };
}

/** مدة التشغيل الجدارية من وسم `<Times start=… finish=…>` (‏`null` إن غاب). */
export function wallClockSeconds(trxText) {
  const times = /<Times\b[^>]*>/.exec(trxText)?.[0];
  if (!times) return null;

  const start = /start="([^"]+)"/.exec(times)?.[1];
  const finish = /finish="([^"]+)"/.exec(times)?.[1];
  if (!start || !finish) return null;

  const elapsed = (Date.parse(finish) - Date.parse(start)) / 1000;
  return Number.isFinite(elapsed) && elapsed >= 0 ? elapsed : null;
}

/** كتلة Markdown جاهزة للصق في لقطة `docs/audit`. */
export function renderBlock(summary, label) {
  const duration = formatDuration(summary.seconds);
  const failed = summary.failedTests.length === 0
    ? 'لا شيء'
    : summary.failedTests.slice(0, 10).join(' · ')
      + (summary.failedTests.length > 10 ? ` … (+${summary.failedTests.length - 10})` : '');

  return [
    `- **${label}:** الإجمالي **${summary.total}** · ناجحة **${summary.passed}**`
      + ` · فاشلة **${summary.failed}** · متخطّاة **${summary.skipped}** · المدة **${duration}**`,
    `- **الفاشلة:** ${failed}`,
  ].join('\n');
}

function durationToSeconds(value) {
  // `00:01:02.3456789` → ثوانٍ عشرية.
  const match = /^(?:(\d+):)?(\d{1,2}):(\d{1,2}(?:\.\d+)?)$/.exec(value.trim());
  if (!match) return 0;
  const hours = Number(match[1] ?? 0);
  return hours * 3600 + Number(match[2]) * 60 + Number(match[3]);
}

function formatDuration(totalSeconds) {
  const minutes = Math.floor(totalSeconds / 60);
  const seconds = Math.round(totalSeconds - minutes * 60);
  return `${minutes}m ${seconds}s`;
}

function decodeXml(value) {
  return value
    .replaceAll('&quot;', '"')
    .replaceAll('&apos;', "'")
    .replaceAll('&lt;', '<')
    .replaceAll('&gt;', '>')
    .replaceAll('&amp;', '&');
}

/** اختبار ذاتي معزول (لا يقرأ المستودع). */
export function selfTest() {
  const trx = `<TestRun><Times start="2026-01-01T00:00:00.0000000Z" finish="2026-01-01T00:00:10.0000000Z" /><Results>
    <UnitTestResult testName="A.Tests.One" outcome="Passed" duration="00:00:02.5000000" />
    <UnitTestResult testName="A.Tests.Two &amp; More" outcome="Passed" duration="00:00:01" />
    <UnitTestResult testName="A.Tests.Failed" outcome="Failed" duration="00:00:00.2500000" />
    <UnitTestResult testName="A.Tests.Skipped" outcome="NotExecuted" duration="00:00:00.0000001" />
  </Results></TestRun>`;

  const summary = summarize(parseResults(trx), wallClockSeconds(trx));
  const expected = { total: 4, passed: 2, failed: 1, skipped: 1, seconds: 10 };
  for (const [key, value] of Object.entries(expected)) {
    if (summary[key] !== value) {
      throw new Error(`الاختبار الذاتي فشل: ${key}=${summary[key]} ≠ ${value}`);
    }
  }
  if (!summary.failedTests.includes('A.Tests.Failed')) {
    throw new Error('الاختبار الذاتي فشل: لم تُجمع أسماء الفاشلة.');
  }
  if (!parseResults(trx).some((row) => row.testName === 'A.Tests.Two & More')) {
    throw new Error('الاختبار الذاتي فشل: فك ترميز XML غير صحيح.');
  }

  const empty = summarize(parseResults('<TestRun><Results /></TestRun>'));
  if (empty.total !== 0) {
    throw new Error('الاختبار الذاتي فشل: ملف فارغ يجب أن يُرجع 0.');
  }

  const block = renderBlock(summary, 'Test/Label');
  if (!block.includes('**4**') || !block.includes('Test/Label')) {
    throw new Error(`الاختبار الذاتي فشل: الكتلة غير متوقعة:\n${block}`);
  }

  // ترتيب الوسائط: يجب ألا يُلتقط عنوان `--label` كمسار ملف.
  const before = parseArgs(['--label', 'App/PG', 'x.trx']);
  if (before.file !== 'x.trx' || before.label !== 'App/PG') {
    throw new Error(`الاختبار الذاتي فشل: تحليل الوسائط (قبل) خاطئ: ${JSON.stringify(before)}`);
  }
  const after = parseArgs(['x.trx', '--label', 'App/PG']);
  if (after.file !== 'x.trx' || after.label !== 'App/PG') {
    throw new Error(`الاختبار الذاتي فشل: تحليل الوسائط (بعد) خاطئ: ${JSON.stringify(after)}`);
  }
  const bare = parseArgs(['x.trx']);
  if (bare.file !== 'x.trx' || bare.label !== 'Application/PostgreSQL') {
    throw new Error(`الاختبار الذاتي فشل: الافتراضي غير صحيح: ${JSON.stringify(bare)}`);
  }

  console.log('test-report: self-test نظيف (تحليل/تلخيص/مدة/وسائط).');
}

/** يحلّل وسائط سطر الأوامر (المسار أولًا أو بعد `--label` — بلا التباس). */
export function parseArgs(args) {
  let file = null;
  let label = 'Application/PostgreSQL';

  for (let i = 0; i < args.length; i += 1) {
    const arg = args[i];
    if (arg === '--self-test') return { selfTest: true, file: null, label };
    if (arg === '--label') {
      label = args[i + 1] ?? label;
      i += 1;
      continue;
    }
    if (arg.startsWith('--')) continue;
    file ??= arg;
  }

  return { selfTest: false, file, label };
}

function main() {
  const args = parseArgs(process.argv.slice(2));
  if (args.selfTest) {
    selfTest();
    return;
  }

  if (!args.file) {
    throw new Error('مرّر مسار ملف trx أو استخدم --self-test.');
  }
  const trxPath = args.file;
  const trxPathText = readFileSync(trxPath, 'utf8');

  const summary = summarize(parseResults(trxPathText), wallClockSeconds(trxPathText));
  if (summary.total === 0) {
    throw new Error('لم تُقرأ أي نتيجة من ملف trx — تحقق من المسار.');
  }
  if (summary.failed > 0) {
    console.error(`تحذير: ${summary.failed} حالة فاشلة في ${trxPath}.`);
  }

  console.log(renderBlock(summary, args.label));
}

if (process.argv[1] && process.argv[1].endsWith('test-report.mjs')) {
  try {
    main();
  } catch (error) {
    console.error(`test-report: ${error.message}`);
    process.exitCode = 1;
  }
}
