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

/** يبني ملخصًا رقميًا من صفوف النتائج. */
export function summarize(rows) {
  const count = (outcome) => rows.filter((row) => row.outcome === outcome).length;
  const passed = count('Passed');
  const failed = count('Failed');
  const skipped = count('NotExecuted');
  const seconds = rows.reduce((total, row) => total + row.seconds, 0);

  return {
    total: rows.length,
    passed,
    failed,
    skipped,
    seconds: Math.round(seconds * 10) / 10,
    failedTests: rows.filter((row) => row.outcome === 'Failed').map((row) => row.testName),
  };
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
  const trx = `<TestRun><Results>
    <UnitTestResult testName="A.Tests.One" outcome="Passed" duration="00:00:02.5000000" />
    <UnitTestResult testName="A.Tests.Two &amp; More" outcome="Passed" duration="00:00:01" />
    <UnitTestResult testName="A.Tests.Failed" outcome="Failed" duration="00:00:00.2500000" />
    <UnitTestResult testName="A.Tests.Skipped" outcome="NotExecuted" duration="00:00:00.0000001" />
  </Results></TestRun>`;

  const summary = summarize(parseResults(trx));
  const expected = { total: 4, passed: 2, failed: 1, skipped: 1 };
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

  console.log('test-report: self-test نظيف (4 حالات تحليل/تلخيص).');
}

function main() {
  const args = process.argv.slice(2);
  if (args.includes('--self-test')) {
    selfTest();
    return;
  }

  const trxPath = args.find((arg) => !arg.startsWith('--'));
  if (!trxPath) {
    throw new Error('مرّر مسار ملف trx أو استخدم --self-test.');
  }
  const labelIndex = args.indexOf('--label');
  const label = labelIndex >= 0 ? args[labelIndex + 1] : 'Application/PostgreSQL';

  const summary = summarize(parseResults(readFileSync(trxPath, 'utf8')));
  if (summary.total === 0) {
    throw new Error('لم تُقرأ أي نتيجة من ملف trx — تحقق من المسار.');
  }
  if (summary.failed > 0) {
    console.error(`تحذير: ${summary.failed} حالة فاشلة في ${trxPath}.`);
  }

  console.log(renderBlock(summary, label));
}

if (process.argv[1] && process.argv[1].endsWith('test-report.mjs')) {
  try {
    main();
  } catch (error) {
    console.error(`test-report: ${error.message}`);
    process.exitCode = 1;
  }
}
