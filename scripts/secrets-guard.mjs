/**
 * P2-05 — حارس الأسرار في الملفات المتتبَّعة (نمط ratchet كحارس الحتمية).
 *
 * يفشل إذا ظهر في ملف متتبَّع نمط سرّي جديد (مفتاح خاص، رمز GitHub/AWS/Slack/OpenAI،
 * JWT، سلسلة اتصال بكلمات مرور، أو إسناد `Password=`/`Secret` ذي قيمة).
 * لا يطبع **قيمًا** أبدًا: المسار والقاعدة وأرقام الأسطر فقط.
 *
 * خط الأساس `scripts/secrets-baseline.json` يسمح بالمواضع القائمة (قيم اختبار/عناصر
 * نائبة موثّقة بسبب)، ويفشل عند:
 *   - أي مطابقة جديدة،
 *   - أو زيادة العدد داخل ملف/قاعدة معروفة،
 *   - أو قيد خط أساس لم يعد مطابقًا (انخفاض أو اختفاء) — فيُجبر على تقليصه.
 *
 * المراجعة إلزامية بعد `--update`: كل قيد جديد يولد بسبب مبدئي «راجع التصنيف»،
 * ويجب تعديله إلى سبب دقيق قبل الدمج.
 *
 * التشغيل: `node scripts/secrets-guard.mjs` / `node scripts/secrets-guard.mjs --update`
 */
import { execSync } from 'node:child_process';
import { existsSync, readFileSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';

const ROOT = join(import.meta.dirname, '..');
const BASELINE_PATH = join(ROOT, 'scripts', 'secrets-baseline.json');
const UPDATE = process.argv.includes('--update');
const PLACEHOLDER_REASON = 'راجع التصنيف: قيمة اختبار/عنصر نائب/بناء سلسلة';

const TEXT_FILE =
  /\.(cs|csproj|json|md|yml|yaml|mjs|js|ts|tsx|ps1|sh|sql|txt|config|props|targets|sln|xml|html|css)$/i;

const RULES = [
  { id: 'private-key', label: 'مفتاح خاص', regex: /-----BEGIN (?:RSA |EC |OPENSSH |PGP )?PRIVATE KEY-----/ },
  { id: 'aws-access-key', label: 'مفتاح AWS', regex: /\bAKIA[0-9A-Z]{16}\b/ },
  { id: 'github-token', label: 'رمز GitHub', regex: /\bgh[pousr]_[A-Za-z0-9]{30,}\b/ },
  { id: 'slack-token', label: 'رمز Slack', regex: /\bxox[baprs]-[A-Za-z0-9-]{10,}\b/ },
  { id: 'openai-key', label: 'مفتاح OpenAI', regex: /\bsk-[A-Za-z0-9]{20,}\b/ },
  { id: 'jwt-token', label: 'رمز JWT', regex: /\beyJ[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}\b/ },
  {
    id: 'uri-with-credentials',
    label: 'رابط اتصال ببيانات دخول',
    regex: /(?:postgres(?:ql)?|mysql|sqlserver):\/\/[^\s:@/]+:[^\s@/]+@/i,
  },
  { id: 'password-assignment', label: 'إسناد كلمة مرور', regex: /\bPassword\s*=\s*[^;\s"']{4,}/i },
  { id: 'secret-json', label: 'سرّ في إعداد JSON', regex: /"(?:Secret|JwtSecret|AdminPassword)"\s*:\s*"[^"]{8,}"/ },
];

/** @returns {{ file: string, id: string, count: number, lines: number[] }[]} */
function collectFindings() {
  const files = execSync('git ls-files -z', { cwd: ROOT, encoding: 'utf8' })
    .split('\u0000')
    .filter(Boolean);

  const findings = new Map();

  for (const file of files) {
    if (!TEXT_FILE.test(file)) continue;

    const text = readFileSync(join(ROOT, file), 'utf8');
    if (text.length > 2_000_000) continue;

    const lines = text.split(/\r?\n/);
    lines.forEach((line, index) => {
      for (const rule of RULES) {
        if (!rule.regex.test(line)) continue;
        const key = `${file}::${rule.id}`;
        const entry = findings.get(key) ?? { file, id: rule.id, count: 0, lines: [] };
        entry.count += 1;
        if (entry.lines.length < 3) entry.lines.push(index + 1);
        findings.set(key, entry);
      }
    });
  }

  return [...findings.values()].sort(
    (left, right) => left.file.localeCompare(right.file) || left.id.localeCompare(right.id),
  );
}

function readBaseline() {
  if (!existsSync(BASELINE_PATH)) {
    return { version: 1, entries: [] };
  }

  const parsed = JSON.parse(readFileSync(BASELINE_PATH, 'utf8'));
  return { version: parsed.version ?? 1, entries: Array.isArray(parsed.entries) ? parsed.entries : [] };
}

function writeBaseline(findings, previous) {
  const previousReasons = new Map(previous.entries.map((entry) => [`${entry.file}::${entry.rule}`, entry.reason]));
  const entries = findings.map((finding) => ({
    file: finding.file,
    rule: finding.id,
    count: finding.count,
    reason: previousReasons.get(`${finding.file}::${finding.id}`) ?? PLACEHOLDER_REASON,
  }));

  writeFileSync(BASELINE_PATH, `${JSON.stringify({ version: 1, entries }, null, 2)}\n`, 'utf8');
  console.log(`secrets-guard: حُدِّث خط الأساس — ${entries.length} قيدًا. راجع الأسباب قبل الدمج.`);
}

const findings = collectFindings();
const baseline = readBaseline();

if (UPDATE) {
  writeBaseline(findings, baseline);
  process.exit(0);
}

const baselineMap = new Map(baseline.entries.map((entry) => [`${entry.file}::${entry.rule}`, entry]));
const problems = [];

for (const finding of findings) {
  const known = baselineMap.get(`${finding.file}::${finding.id}`);

  if (!known) {
    problems.push(
      `جديد: ${finding.file} (${finding.id}) — ${finding.count} مطابقة، سطور: ${finding.lines.join(', ')}`,
    );
    continue;
  }

  if (finding.count > known.count) {
    problems.push(
      `زيادة: ${finding.file} (${finding.id}) — ${finding.count} > ${known.count}، سطور: ${finding.lines.join(', ')}`,
    );
  } else if (finding.count < known.count) {
    problems.push(
      `انخفاض غير مسجّل: ${finding.file} (${finding.id}) — ${finding.count} < ${known.count}؛ شغّل --update بعد المراجعة.`,
    );
  }
}

for (const entry of baseline.entries) {
  if (!findings.some((finding) => finding.file === entry.file && finding.id === entry.rule)) {
    problems.push(
      `قيد قديم لم يعد مطابقًا: ${entry.file} (${entry.rule})؛ شغّل --update بعد المراجعة.`,
    );
  }
}

if (problems.length > 0) {
  console.error('secrets-guard: فشل — أسرار محتملة في الملفات المتتبَّعة (لا تُطبع القيم):');
  for (const problem of problems) {
    console.error(`  - ${problem}`);
  }
  console.error('');
  console.error('القيم المخزّنة في المستودع ممنوعة. انقل القيمة إلى متغير بيئة/سرّ، ثم');
  console.error('راجع أي استثناء مقصود في scripts/secrets-baseline.json مع سبب دقيق.');
  process.exit(1);
}

console.log(
  `secrets-guard: نظيف — ${findings.length} مطابقة معروفة في خط الأساس، بلا أسرار جديدة أو غير مسجّلة.`,
);
