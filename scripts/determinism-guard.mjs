import { readFileSync, readdirSync, statSync, writeFileSync } from 'node:fs';
import { join, relative, sep } from 'node:path';

/**
 * P1-02 — حارس الحتمية (ratchet):
 * يمنع إدخال مصادر زمن/عشوائية غير محقونة في الخلفية، ويُلزم تقليص الاستثناءات القائمة.
 *
 * - `node scripts/determinism-guard.mjs`         فحص (يفشل عند جديد أو زيادة أو خط أساس قديم).
 * - `node scripts/determinism-guard.mjs --update` يعيد توليد خط الأساس من الشجرة الحالية.
 *
 * لا تُضاف الاستثناءات يدويًا: شغّل `--update` بعد إصلاح المواضع، فينقص الخط تلقائيًا.
 */

const ROOT = process.cwd();
const BASELINE_PATH = join(ROOT, 'scripts', 'determinism-baseline.json');
const ROOTS = ['backend/src', 'backend/tests'];

const PATTERNS = [
  { id: 'dateTimeNow', regex: /\bDateTime\.Now\b/, label: 'DateTime.Now' },
  { id: 'dateTimeToday', regex: /\bDateTime\.Today\b/, label: 'DateTime.Today' },
  { id: 'dateTimeOffsetNow', regex: /\bDateTimeOffset\.Now\b/, label: 'DateTimeOffset.Now' },
  { id: 'localTimeZone', regex: /\bTimeZoneInfo\.Local\b/, label: 'TimeZoneInfo.Local' },
  { id: 'randomShared', regex: /\bRandom\.Shared\b/, label: 'Random.Shared' },
  { id: 'unseededRandom', regex: /\bnew\s+Random\s*\(\s*\)/, label: 'new Random()' },
  { id: 'getHashCode', regex: /\.GetHashCode\s*\(\s*\)/, label: 'GetHashCode()' },
  {
    id: 'timeProviderSystem',
    regex: /\bTimeProvider\.System\b/,
    label: 'TimeProvider.System (test)',
    testsOnly: true,
  },
  {
    id: 'utcNowParts',
    regex: /\bDateTime\.UtcNow\.(Year|Month|Day|Date)\b/,
    label: 'DateTime.UtcNow date parts (test)',
    testsOnly: true,
  },
];

// الاستثناء الدائم الوحيد: تطبيق `IEntropySource` نفسه هو الموضع المسموح لاستخدام
// `Random.Shared` في الإنتاج — كل الخدمات تمر عبر العقد المحقون.
const PERMANENT_ALLOW = new Set([
  'backend/src/DocGenerator.Application/Common/IEntropySource.cs::randomShared',
]);

function walk(dir) {
  const out = [];
  for (const entry of readdirSync(dir, { withFileTypes: true })) {
    if (entry.name === 'bin' || entry.name === 'obj' || entry.name === 'node_modules') continue;
    const full = join(dir, entry.name);
    if (entry.isDirectory()) out.push(...walk(full));
    else if (entry.isFile() && entry.name.endsWith('.cs')) out.push(full);
  }
  return out;
}

function isComment(line) {
  const t = line.trim();
  return t.startsWith('//') || t.startsWith('/*') || t.startsWith('*');
}

function scan() {
  /** @type {Map<string, number>} */
  const counts = new Map();
  for (const root of ROOTS) {
    const abs = join(ROOT, root);
    let files;
    try {
      files = walk(abs);
    } catch {
      continue;
    }
    for (const file of files) {
      const rel = relative(ROOT, file).split(sep).join('/');
      const lines = readFileSync(file, 'utf8').split(/\r?\n/);
      for (const line of lines) {
        if (isComment(line)) continue;
        for (const pattern of PATTERNS) {
          if (pattern.testsOnly && !rel.startsWith('backend/tests/')) continue;
          const matches = line.match(new RegExp(pattern.regex.source, 'g'));
          if (!matches) continue;
          const key = `${rel}::${pattern.id}`;
          if (PERMANENT_ALLOW.has(key)) continue;
          counts.set(key, (counts.get(key) ?? 0) + matches.length);
        }
      }
    }
  }
  return counts;
}

function loadBaseline() {
  try {
    const parsed = JSON.parse(readFileSync(BASELINE_PATH, 'utf8'));
    return new Map(Object.entries(parsed.entries ?? {}));
  } catch {
    return new Map();
  }
}

function writeBaseline(counts) {
  const entries = Object.fromEntries([...counts.entries()].sort(([a], [b]) => a.localeCompare(b)));
  writeFileSync(
    BASELINE_PATH,
    `${JSON.stringify({ version: 1, note: 'P1-02 ratchet — run --update only after fixing occurrences', entries }, null, 2)}\n`,
    'utf8',
  );
}

function labelFor(key) {
  const id = key.split('::')[1];
  return PATTERNS.find((p) => p.id === id)?.label ?? id;
}

const update = process.argv.includes('--update');
const current = scan();

if (update) {
  writeBaseline(current);
  console.log(`determinism-guard: baseline written (${current.size} entries)`);
  process.exit(0);
}

const baseline = loadBaseline();
const failures = [];
const stale = [];

for (const [key, count] of current) {
  const allowed = baseline.get(key) ?? 0;
  if (count > allowed) {
    failures.push(`${key}: ${count} occurrence(s) of ${labelFor(key)} (baseline ${allowed})`);
  }
}

for (const [key, allowed] of baseline) {
  const count = current.get(key) ?? 0;
  if (count < allowed) stale.push(`${key}: baseline ${allowed}, actual ${count}`);
}

if (failures.length === 0 && stale.length === 0) {
  console.log(`determinism-guard: clean (${current.size} allowlisted entries)`);
  process.exit(0);
}

if (failures.length > 0) {
  console.error('determinism-guard: new or increased nondeterminism:');
  for (const failure of failures) console.error(`  - ${failure}`);
  console.error('  Fix: inject TimeProvider / IEntropySource instead of ambient clock/random.');
}

if (stale.length > 0) {
  console.error('determinism-guard: stale baseline entries (fixes not recorded):');
  for (const entry of stale) console.error(`  - ${entry}`);
  console.error('  Run: node scripts/determinism-guard.mjs --update');
}

process.exit(1);
