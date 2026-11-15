/**
 * P2-06 — بوابة اعتماديات .NET: تُفشل الوظيفة عند ثغرة High/Critical.
 *
 * `dotnet list package --vulnerable` يُرجع رمز خروج 0 حتى مع ثغرات، فالخطوة النصية
 * السابقة كانت تقريرًا لا بوابة. هذا السكربت يقرأ `--format json` ويصنّف الشدّة:
 *   - High/Critical ⇒ فشل (بوابة).
 *   - Moderate/Low ⇒ تحذير فقط.
 *   - شدّة غير معروفة ⇒ فشل (fail-closed) حتى تُراجع الصيغة.
 *
 * التشغيل:
 *   node scripts/dependency-audit.mjs              # بوابة
 *   node scripts/dependency-audit.mjs --self-test  # تحقق من المحلّل بعيّنات ثابتة
 */
import { execFileSync } from 'node:child_process';
import { readFileSync } from 'node:fs';
import { join } from 'node:path';

const ROOT = join(import.meta.dirname, '..');
const SOLUTION = 'backend/DocGenerator.sln';
const FIXTURES = join(ROOT, 'scripts', 'fixtures');
const BLOCKING = new Set(['high', 'critical', 'unknown']);

const SEVERITY_BY_INDEX = ['low', 'moderate', 'high', 'critical'];

/**
 * @param {unknown} value
 * @returns {'low'|'moderate'|'high'|'critical'|'unknown'}
 */
export function normalizeSeverity(value) {
  if (typeof value === 'number' && Number.isFinite(value)) {
    return SEVERITY_BY_INDEX[Math.min(3, Math.max(0, Math.trunc(value)))] ?? 'unknown';
  }

  const text = String(value ?? '').toLowerCase();
  if (text.includes('critical')) return 'critical';
  if (text.includes('high')) return 'high';
  if (text.includes('moderate') || text.includes('medium')) return 'moderate';
  if (text.includes('low')) return 'low';
  return 'unknown';
}

/**
 * يستخرج الثغرات من تقرير `dotnet list package --format json` بلا افتراض وجود
 * `frameworks` (يُحذف من المخرجات حين لا توجد ثغرات).
 *
 * @param {any} report
 * @returns {{ project: string, package: string, version: string, severity: string, advisory: string }[]}
 */
export function collectVulnerabilities(report) {
  const findings = [];

  for (const project of report?.projects ?? []) {
    for (const framework of project?.frameworks ?? []) {
      for (const kind of ['topLevelPackages', 'transitivePackages']) {
        for (const pkg of framework?.[kind] ?? []) {
          for (const vulnerability of pkg?.vulnerabilities ?? []) {
            findings.push({
              project: String(project?.path ?? ''),
              package: String(pkg?.id ?? ''),
              version: String(pkg?.resolvedVersion ?? pkg?.requestedVersion ?? ''),
              severity: normalizeSeverity(vulnerability?.severity),
              advisory: String(vulnerability?.advisoryUrl ?? vulnerability?.advisoryurl ?? ''),
            });
          }
        }
      }
    }
  }

  return findings;
}

function readFixture(name) {
  return JSON.parse(readFileSync(join(FIXTURES, name), 'utf8'));
}

function selfTest() {
  const cases = [
    ['nuget-vulnerable-none.json', []],
    ['nuget-vulnerable-high.json', ['high']],
    ['nuget-vulnerable-critical.json', ['critical']],
    ['nuget-vulnerable-numeric.json', ['critical']],
    ['nuget-vulnerable-moderate.json', ['moderate']],
  ];

  for (const [fixture, expected] of cases) {
    const actual = collectVulnerabilities(readFixture(fixture)).map((finding) => finding.severity);
    if (actual.length !== expected.length || actual.some((severity, index) => severity !== expected[index])) {
      console.error(
        `dependency-audit: فشل الاختبار الذاتي في ${fixture} — متوقع [${expected}]، فعلي [${actual}].`,
      );
      process.exit(1);
    }
  }

  console.log(`dependency-audit: الاختبار الذاتي نجح (${cases.length} عيّنات).`);
}

function runAudit() {
  let raw;
  try {
    raw = execFileSync(
      'dotnet',
      ['list', SOLUTION, 'package', '--vulnerable', '--include-transitive', '--format', 'json'],
      { cwd: ROOT, encoding: 'utf8', stdio: ['ignore', 'pipe', 'pipe'] },
    );
  } catch (error) {
    console.error('dependency-audit: تعذّر تشغيل `dotnet list package` — لا تُعتبر النتيجة نظيفة.');
    console.error(String(error?.stdout ?? '').slice(0, 2000));
    process.exit(1);
  }

  let report;
  try {
    report = JSON.parse(raw.replace(/^\uFEFF/, ''));
  } catch {
    console.error('dependency-audit: مخرجات JSON غير صالحة من `dotnet list package`.');
    process.exit(1);
  }

  const findings = collectVulnerabilities(report);
  const blocking = findings.filter((finding) => BLOCKING.has(finding.severity));
  const advisory = findings.filter((finding) => !BLOCKING.has(finding.severity));

  for (const finding of advisory) {
    console.warn(
      `dependency-audit: تحذير — ${finding.package} ${finding.version} (${finding.severity}) في ${finding.project}`,
    );
  }

  if (blocking.length > 0) {
    console.error('dependency-audit: فشل — ثغرات High/Critical في الاعتماديات:');
    for (const finding of blocking) {
      console.error(
        `  - ${finding.package} ${finding.version} [${finding.severity}] في ${finding.project} — ${finding.advisory}`,
      );
    }
    process.exit(1);
  }

  console.log(
    `dependency-audit: نظيف — لا High/Critical في ${report?.projects?.length ?? 0} مشروعًا` +
      (advisory.length > 0 ? ` (${advisory.length} تحذير Moderate/Low).` : '.'),
  );
}

if (process.argv.includes('--self-test')) {
  selfTest();
} else {
  runAudit();
}
