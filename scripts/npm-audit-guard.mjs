/**
 * P2-06 — بوابة `npm audit` مع استثناءات موثّقة ومؤقتة.
 *
 * `npm audit --audit-level=high` كان يفشل على أي ثغرة High/Critical، لكنه لا يملك
 * آلية لثغرة **بلا إصدار مُصلَح** (مثل `GHSA-fc5q-39xc-vjpf` في `picomatch` حيث أحدث
 * إصدار 4.0.7 نفسه داخل المدى المتأثر). تعطيل البوابة أو `--force` (يخفض `vitest`
 * لإصدار أقدم) كلاهما خطأ. البديل: بوابة تفشل افتراضيًا، وتقبل استثناءً **مؤقتًا**
 * برقم الـadvisory وسبب مكتوب وتاريخ انتهاء، وتفشل إذا انتهى أو صار قديمًا.
 *
 * الاستخدام:
 *   node scripts/npm-audit-guard.mjs              # البوابة
 *   node scripts/npm-audit-guard.mjs --self-test  # اختبار منطق التصنيف بلا شبكة
 */
import { spawnSync } from 'node:child_process';
import { readFileSync } from 'node:fs';
import { join } from 'node:path';

const ROOT = join(import.meta.dirname, '..');
const FRONTEND = join(ROOT, 'frontend');
const ALLOWLIST_PATH = join(ROOT, 'scripts', 'npm-audit-allowlist.json');
const BLOCKING = new Set(['high', 'critical', 'unknown']);

/** @param {unknown} value */
export function normalizeSeverity(value) {
  const text = String(value ?? '').toLowerCase();
  if (text.includes('critical')) return 'critical';
  if (text.includes('high')) return 'high';
  if (text.includes('moderate') || text.includes('medium')) return 'moderate';
  if (text.includes('low')) return 'low';
  return 'unknown';
}

/**
 * يجمع الثغرات بحسب **رقم الـadvisory** لا اسم الحزمة: ثغرة واحدة تنتشر عبر سلسلة
 * اعتماديات كاملة (هنا 6 حزم) ويجب أن تكون استثناءً واحدًا مفهومًا.
 *
 * @param {any} report تقرير `npm audit --json`
 */
export function collectAdvisories(report) {
  /** @type {Map<string, {id: string, title: string, url: string, severity: string, packages: Set<string>}>} */
  const advisories = new Map();

  for (const vulnerability of Object.values(report?.vulnerabilities ?? {})) {
    const vuln = /** @type {any} */ (vulnerability);
    const severity = normalizeSeverity(vuln?.severity);

    for (const via of vuln?.via ?? []) {
      if (!via || typeof via !== 'object' || typeof via.url !== 'string') continue;
      const match = /GHSA-[0-9a-z-]+/i.exec(via.url);
      if (!match) continue;

      const id = match[0].toUpperCase();
      if (!advisories.has(id)) {
        advisories.set(id, {
          id,
          title: String(via.title ?? ''),
          url: via.url,
          severity,
          packages: new Set(),
        });
      }
      advisories.get(id).packages.add(String(vuln?.name ?? ''));
    }
  }

  return [...advisories.values()].map(advisory => ({
    ...advisory,
    packages: [...advisory.packages],
  }));
}

/**
 * منطق القرار النقي (قابل للاختبار):
 * - ثغرة High/Critical/unknown بلا استثناء ⇒ فشل.
 * - استثناء منتهٍ أو بلا سبب/تاريخ ⇒ فشل.
 * - استثناء لم تعد الثغرة موجودة ⇒ فشل (كي لا يبقى خط أساس ميتًا).
 *
 * @param {{advisories: {id: string, severity: string, title: string, packages: string[]}[], allowlist: any[], now: Date}} input
 */
export function evaluate({ advisories, allowlist, now }) {
  const violations = [];
  const allowed = new Map();

  for (const entry of allowlist ?? []) {
    const id = String(entry?.id ?? '').toUpperCase();
    const reason = String(entry?.reason ?? '').trim();
    const expires = String(entry?.expires ?? '').trim();

    if (!id || !reason || !expires) {
      violations.push(`استثناء غير مكتمل (id/سبب/تاريخ انتهاء): ${id || '(بلا id)'}`);
      continue;
    }

    const expiry = new Date(`${expires}T23:59:59Z`);
    if (Number.isNaN(expiry.getTime())) {
      violations.push(`تاريخ انتهاء غير صالح للاستثناء ${id}: ${expires}`);
      continue;
    }
    if (expiry < now) {
      violations.push(`انتهى الاستثناء ${id} في ${expires} — أصلح الاعتمادية أو جدّد السبب.`);
      continue;
    }

    allowed.set(id, entry);
  }

  const seen = new Set(advisories.map(advisory => advisory.id));

  for (const advisory of advisories) {
    if (!BLOCKING.has(advisory.severity)) continue;
    if (allowed.has(advisory.id)) continue;

    violations.push(
      `ثغرة ${advisory.severity} غير مسموحة: ${advisory.id} — ${advisory.title} ` +
        `(${advisory.packages.join(', ')}) ${advisory.url}`,
    );
  }

  for (const id of allowed.keys()) {
    if (!seen.has(id)) {
      violations.push(`استثناء قديم: ${id} لم تظهر في تقرير npm audit — احذفه لتحديث خط الأساس.`);
    }
  }

  return violations;
}

function runAudit() {
  // Windows لا يشغّل `npm`/`npm.cmd` بلا shell؛ نستخدم cmd صراحةً بلا تمرير وسائط
  // غير مضمونة (تحذير Node DEP0190). الأمر ثابت لا يحتوي مدخلات مستخدم.
  const result = process.platform === 'win32'
    ? spawnSync(process.env.ComSpec ?? 'cmd.exe', ['/d', '/s', '/c', 'npm audit --json'], {
        cwd: FRONTEND,
        encoding: 'utf8',
      })
    : spawnSync('npm', ['audit', '--json'], { cwd: FRONTEND, encoding: 'utf8' });

  if (result.error) {
    throw new Error(`تعذّر تشغيل npm audit: ${result.error.message}`);
  }

  if (!result.stdout) {
    throw new Error(`npm audit لم يُنتج تقريرًا (exit=${result.status}). ${result.stderr ?? ''}`);
  }

  try {
    return JSON.parse(result.stdout);
  } catch {
    throw new Error('تعذّر تحليل JSON من npm audit.');
  }
}

function selfTest() {
  const report = {
    vulnerabilities: {
      picomatch: {
        name: 'picomatch',
        severity: 'high',
        via: [
          {
            title: 'ReDoS',
            url: 'https://github.com/advisories/GHSA-fc5q-39xc-vjpf',
          },
        ],
      },
      lowpkg: {
        name: 'lowpkg',
        severity: 'low',
        via: [{ title: 'low issue', url: 'https://github.com/advisories/GHSA-aaaa-bbbb-cccc' }],
      },
    },
  };

  const advisories = collectAdvisories(report);
  if (advisories.length !== 2) throw new Error('self-test: expected 2 advisories');
  if (advisories.find(a => a.id === 'GHSA-FC5Q-39XC-VJPF')?.packages[0] !== 'picomatch') {
    throw new Error('self-test: advisory package mapping failed');
  }

  const now = new Date('2026-11-17T00:00:00Z');
  const cases = [
    { allowlist: [], expected: 1, name: 'unallowlisted high blocks' },
    {
      allowlist: [
        {
          id: 'GHSA-fc5q-39xc-vjpf',
          reason: 'dev only',
          expires: '2026-12-17',
        },
      ],
      expected: 0,
      name: 'valid allowlist passes',
    },
    {
      allowlist: [
        {
          id: 'GHSA-fc5q-39xc-vjpf',
          reason: 'dev only',
          expires: '2026-11-01',
        },
      ],
      expected: 2,
      name: 'expired allowlist blocks (expired + unallowlisted)',
    },
    {
      allowlist: [
        {
          id: 'GHSA-zzzz-yyyy-xxxx',
          reason: 'stale',
          expires: '2026-12-17',
        },
      ],
      expected: 2,
      name: 'stale allowlist blocks (missing + stale)',
    },
    {
      allowlist: [
        { id: 'GHSA-FC5Q-39XC-VJPF', reason: 'dev only', expires: '2026-12-17' },
      ],
      expected: 0,
      name: 'case-insensitive id',
    },
  ];

  for (const testCase of cases) {
    const violations = evaluate({ advisories, allowlist: testCase.allowlist, now });
    if (violations.length !== testCase.expected) {
      throw new Error(
        `self-test failed: ${testCase.name} — expected ${testCase.expected}, got ${violations.length}: ${violations.join(' | ')}`,
      );
    }
  }

  process.stdout.write('npm-audit-guard: self-test نجح.\n');
}

function main() {
  if (process.argv.includes('--self-test')) {
    selfTest();
    return;
  }

  const allowlist = JSON.parse(readFileSync(ALLOWLIST_PATH, 'utf8'));
  const advisories = collectAdvisories(runAudit());
  const violations = evaluate({ advisories, allowlist, now: new Date() });

  if (violations.length > 0) {
    process.stderr.write(`npm-audit-guard: فشل — ${violations.length} مخالفة:\n`);
    for (const violation of violations) process.stderr.write(`  - ${violation}\n`);
    process.exitCode = 1;
    return;
  }

  const allowedCount = advisories.filter(a => BLOCKING.has(a.severity)).length;
  process.stdout.write(
    `npm-audit-guard: نظيف — ${advisories.length} ثغرة معروفة، ` +
      `منها ${allowedCount} High/Critical مستثناة بمبرّر وتاريخ انتهاء.\n`,
  );
}

main();
