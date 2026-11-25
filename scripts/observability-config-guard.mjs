/**
 * P5-02 — حارس إعدادات الرصد (Observability config guard).
 *
 * يتحقق من:
 *  - صلاحية YAML/JSON لملفات `ops/observability/*` (بمحلل حقيقي: PyYAML أو yq).
 *  - مراجع مكوّنات جامع OTLP (receivers/processors/exporters/connectors/pipelines).
 *  - وجود memory_limiter + batch + حذف PII + tail_sampling بنسبة معقولة.
 *  - تفرّد أسماء التنبيهات ووجود severity/for/runbook_url وتغطية الدليل.
 *  - أن كل تنبيه مذكور في docs/runbooks/observability.md وأن مرساة SLO موجودة.
 *  - أن أسماء المقاييس المطلوبة موثّقة في docs/SLO.md.
 *  - أن اللوحة JSON صالحة وتحوي اللوحات الإلزامية.
 *
 * التشغيل:
 *   node scripts/observability-config-guard.mjs
 *   node scripts/observability-config-guard.mjs --self-test
 *
 * لا يضيف حزمًا: يستخدم Python/PyYAML أو yq الموجودين في صورة CI (أو Python محليًا).
 */
import { execFileSync } from 'node:child_process';
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const PATHS = {
  collector: join(ROOT, 'ops/observability/otel-collector.yaml'),
  alerts: join(ROOT, 'ops/observability/alerts.yaml'),
  dashboard: join(ROOT, 'ops/observability/dashboard.json'),
  slo: join(ROOT, 'docs/SLO.md'),
  runbook: join(ROOT, 'docs/runbooks/observability.md'),
};

const RUNBOOK_BASE =
  'https://github.com/maarouf003-netizen/docgen/blob/master/docs/runbooks/observability.md';
const SEVERITIES = new Set(['critical', 'warning', 'info']);

const failures = [];
const notes = [];

function fail(message) {
  failures.push(message);
}

function read(file) {
  return readFileSync(file, 'utf8');
}

function assert(condition, message) {
  if (!condition) fail(message);
}

function isObject(value) {
  return value !== null && typeof value === 'object' && !Array.isArray(value);
}

function parseYaml(file) {
  const code = [
    'import sys, yaml, json',
    'with open(sys.argv[1], encoding="utf-8") as fh:',
    '    data = yaml.safe_load(fh)',
    'json.dump(data, sys.stdout, ensure_ascii=True)',
  ].join('\n');

  const candidates = [
    ['python', ['-c', code, file]],
    ['python3', ['-c', code, file]],
    ['yq', ['eval', '-o=json', '.', file]],
  ];
  const errors = [];
  for (const [command, args] of candidates) {
    try {
      const stdout = execFileSync(command, args, {
        encoding: 'utf8',
        stdio: ['ignore', 'pipe', 'pipe'],
      });
      return JSON.parse(stdout);
    } catch (error) {
      errors.push(`${command}: ${error.message.split('\n')[0]}`);
    }
  }
  throw new Error(
    `No YAML parser available (need python+PyYAML or yq). Tried: ${errors.join(' | ')}`,
  );
}

function hasBalanced(text) {
  const pairs = { '(': ')', '[': ']', '{': '}' };
  const stack = [];
  let quote = null;
  for (let i = 0; i < text.length; i += 1) {
    const ch = text[i];
    if (quote) {
      if (ch === '\\') i += 1;
      else if (ch === quote) quote = null;
      continue;
    }
    if (ch === '"' || ch === "'") {
      quote = ch;
      continue;
    }
    if (pairs[ch]) stack.push(pairs[ch]);
    else if (ch === ')' || ch === ']' || ch === '}') {
      if (stack.pop() !== ch) return false;
    }
  }
  return stack.length === 0;
}

function validateCollector(collector) {
  assert(isObject(collector), 'otel-collector.yaml: root must be a mapping');
  if (!isObject(collector)) return;

  const receivers = collector.receivers ?? {};
  const processors = collector.processors ?? {};
  const exporters = collector.exporters ?? {};
  const connectors = collector.connectors ?? {};
  const service = collector.service ?? {};
  const pipelines = service.pipelines ?? {};

  assert(isObject(receivers.otlp), 'collector: receivers.otlp is required');
  assert(
    isObject(receivers.otlp?.protocols?.grpc) && isObject(receivers.otlp?.protocols?.http),
    'collector: OTLP receiver must expose both grpc and http protocols',
  );
  assert(
    typeof receivers.otlp?.protocols?.grpc?.endpoint === 'string',
    'collector: receivers.otlp.protocols.grpc.endpoint is required',
  );

  assert(isObject(processors.memory_limiter), 'collector: processors.memory_limiter is required');
  assert(
    Number(processors.memory_limiter?.limit_mib) > 0,
    'collector: memory_limiter.limit_mib must be > 0',
  );

  const batchKeys = Object.keys(processors).filter((key) => key === 'batch' || key.startsWith('batch/'));
  assert(batchKeys.length >= 1, 'collector: at least one batch processor is required');

  const redactKeys = Object.keys(processors).filter(
    (key) => key.startsWith('attributes') || key.includes('redact'),
  );
  assert(redactKeys.length >= 1, 'collector: a redaction attributes processor is required');
  let hasDelete = false;
  let hasSensitiveDelete = false;
  for (const key of redactKeys) {
    const actions = processors[key]?.actions;
    if (!Array.isArray(actions)) continue;
    for (const action of actions) {
      if (action?.action !== 'delete') continue;
      hasDelete = true;
      const text = `${action.key ?? ''} ${action.pattern ?? ''}`.toLowerCase();
      if (text.includes('authorization') || text.includes('password') || text.includes('cookie')) {
        hasSensitiveDelete = true;
      }
    }
  }
  assert(hasDelete, 'collector: redaction processor must use action: delete');
  assert(
    hasSensitiveDelete,
    'collector: redaction must delete authorization/cookie/password-like attributes',
  );

  assert(isObject(connectors.span_metrics), 'collector: connectors.span_metrics is required');
  assert(isObject(pipelines), 'collector: service.pipelines is required');
  assert(isObject(pipelines.traces), 'collector: service.pipelines.traces is required');
  assert(isObject(pipelines.metrics), 'collector: service.pipelines.metrics is required');

  const spanMetricsPipeline = Object.entries(pipelines).find(
    ([name, pipeline]) =>
      name.startsWith('traces') && Array.isArray(pipeline?.exporters) &&
      pipeline.exporters.includes('span_metrics'),
  );
  assert(spanMetricsPipeline, 'collector: a traces pipeline must export to span_metrics');

  for (const [name, pipeline] of Object.entries(pipelines)) {
    assert(isObject(pipeline), `collector: pipeline ${name} must be a mapping`);
    if (!isObject(pipeline)) continue;
    const arrays = ['receivers', 'processors', 'exporters'];
    for (const field of arrays) {
      assert(
        Array.isArray(pipeline[field]) && pipeline[field].length > 0,
        `collector: pipeline ${name}.${field} must be a non-empty list`,
      );
    }
    for (const receiver of pipeline.receivers ?? []) {
      assert(
        Object.hasOwn(receivers, receiver) || Object.hasOwn(connectors, receiver),
        `collector: pipeline ${name} references unknown receiver ${receiver}`,
      );
    }
    for (const processor of pipeline.processors ?? []) {
      assert(
        Object.hasOwn(processors, processor),
        `collector: pipeline ${name} references unknown processor ${processor}`,
      );
    }
    for (const exporter of pipeline.exporters ?? []) {
      assert(
        Object.hasOwn(exporters, exporter) || Object.hasOwn(connectors, exporter),
        `collector: pipeline ${name} references unknown exporter ${exporter}`,
      );
    }
    const lastProcessor = pipeline.processors?.[pipeline.processors.length - 1];
    assert(
      typeof lastProcessor === 'string' &&
        (lastProcessor === 'batch' || lastProcessor.startsWith('batch/')),
      `collector: pipeline ${name} must end with a batch processor`,
    );
  }

  const traces = pipelines.traces;
  if (isObject(traces)) {
    assert(
      Array.isArray(traces.processors) && traces.processors.includes('tail_sampling'),
      'collector: traces pipeline must use tail_sampling',
    );
  }
  const percent = processors.tail_sampling?.policies?.find(
    (policy) => policy?.type === 'probabilistic',
  )?.probabilistic?.sampling_percentage;
  assert(
    Number(percent) > 0 && Number(percent) <= 25,
    'collector: probabilistic sampling_percentage must be in (0, 25]',
  );

  assert(
    Array.isArray(pipelines.metrics?.exporters) &&
      pipelines.metrics.exporters.includes('prometheus'),
    'collector: metrics pipeline must export to prometheus',
  );

  const raw = read(PATHS.collector);
  assert(
    !/(^|\s)(password|passwd|secret|api[_-]?key|bearer)\s*:\s*(?!["']?\$\{env:)/im.test(raw),
    'collector: possible literal secret in configuration (use ${env:...})',
  );
}

function collectRunbookAnchors(text) {
  const anchors = new Set();
  for (const line of text.split(/\r?\n/)) {
    const match = /^(#{2,6})\s+(.+?)\s*$/.exec(line);
    if (!match) continue;
    const anchor = match[2]
      .toLowerCase()
      .replace(/[^\p{L}\p{N}\s-]/gu, '')
      .trim()
      .replace(/\s+/g, '-');
    anchors.add(anchor);
  }
  return anchors;
}

function validateAlerts(alerts, runbookText) {
  assert(isObject(alerts), 'alerts.yaml: root must be a mapping');
  if (!isObject(alerts)) return { names: [] };
  assert(Array.isArray(alerts.groups) && alerts.groups.length > 0, 'alerts.yaml: groups is required');
  if (!Array.isArray(alerts.groups)) return { names: [] };

  const anchors = collectRunbookAnchors(runbookText);
  const names = new Set();
  const sloLabels = new Set();
  let ruleCount = 0;

  for (const group of alerts.groups) {
    assert(typeof group?.name === 'string' && group.name.length > 0, 'alerts: every group needs a name');
    assert(Array.isArray(group?.rules) && group.rules.length > 0, `alerts: group ${group?.name} has no rules`);
    for (const rule of group?.rules ?? []) {
      ruleCount += 1;
      const name = rule?.alert;
      assert(typeof name === 'string' && /^[A-Za-z][A-Za-z0-9]+$/.test(name), `alerts: invalid alert name ${name}`);
      if (typeof name === 'string') {
        assert(!names.has(name), `alerts: duplicate alert name ${name}`);
        names.add(name);
      }
      const expr = rule?.expr;
      assert(typeof expr === 'string' && expr.trim().length > 0, `alerts: ${name} has no expr`);
      if (typeof expr === 'string') {
        assert(!expr.includes('${'), `alerts: ${name} expr contains template substitution`);
        assert(!/TODO|FIXME/.test(expr), `alerts: ${name} expr contains TODO/FIXME`);
        assert(hasBalanced(expr), `alerts: ${name} expr has unbalanced brackets`);
      }
      assert(
        typeof rule?.for === 'string' && /^\d+(ms|s|m|h|d|w|y)$/.test(rule.for),
        `alerts: ${name} must have a valid for duration`,
      );
      assert(
        SEVERITIES.has(rule?.labels?.severity),
        `alerts: ${name} severity must be critical/warning/info`,
      );
      assert(
        typeof rule?.labels?.slo === 'string' && rule.labels.slo.length > 0,
        `alerts: ${name} must carry a slo label`,
      );
      if (typeof rule?.labels?.slo === 'string') sloLabels.add(rule.labels.slo);
      assert(
        rule?.labels?.service === 'docgen-api',
        `alerts: ${name} must set labels.service=docgen-api`,
      );

      const url = rule?.annotations?.runbook_url;
      assert(typeof rule?.annotations?.summary === 'string' && rule.annotations.summary.length > 0,
        `alerts: ${name} needs annotations.summary`);
      assert(
        typeof rule?.annotations?.description === 'string' &&
          rule.annotations.description.length > 0,
        `alerts: ${name} needs annotations.description`,
      );
      assert(typeof url === 'string', `alerts: ${name} needs annotations.runbook_url`);
      if (typeof url === 'string') {
        assert(url.startsWith(`${RUNBOOK_BASE}#`), `alerts: ${name} runbook_url must point to observability.md`);
        const anchor = url.slice(url.indexOf('#') + 1);
        assert(anchors.has(anchor), `alerts: ${name} references missing runbook anchor #${anchor}`);
        assert(
          runbookText.includes(name),
          `alerts: ${name} is not documented in docs/runbooks/observability.md`,
        );
      }
    }
  }

  const required = [
    'DocGenAvailabilityFastBurn',
    'DocGenReadP95Warning',
    'DocGenWriteP95Warning',
    'DocGenOutboxLagCritical',
    'DocGenDbPoolSaturationWarning',
    'DocGenAuthFailureWarning',
    'DocGenRateLimitRejectionWarning',
  ];
  for (const name of required) {
    assert(names.has(name), `alerts: required alert ${name} is missing`);
  }

  return { names: [...names], sloLabels: [...sloLabels], ruleCount };
}

function validateDashboard(raw) {
  let dashboard;
  try {
    dashboard = JSON.parse(raw);
  } catch (error) {
    fail(`dashboard.json: invalid JSON: ${error.message}`);
    return;
  }
  assert(typeof dashboard.title === 'string' && dashboard.title.length > 0, 'dashboard: title is required');
  assert(typeof dashboard.uid === 'string' && dashboard.uid.length > 0, 'dashboard: uid is required');
  assert(typeof dashboard.schemaVersion === 'number', 'dashboard: schemaVersion is required');
  assert(Array.isArray(dashboard.panels) && dashboard.panels.length >= 10, 'dashboard: at least 10 panels are required');

  const variables = dashboard.templating?.list ?? [];
  assert(
    variables.some((variable) => variable?.name === 'DS_PROMETHEUS' && variable?.type === 'datasource'),
    'dashboard: DS_PROMETHEUS datasource variable is required',
  );

  const ids = new Set();
  const titles = [];
  for (const panel of dashboard.panels ?? []) {
    assert(Number.isInteger(panel?.id), 'dashboard: every panel needs an integer id');
    if (Number.isInteger(panel?.id)) {
      assert(!ids.has(panel.id), `dashboard: duplicate panel id ${panel.id}`);
      ids.add(panel.id);
    }
    assert(typeof panel?.title === 'string' && panel.title.length > 0, 'dashboard: every panel needs a title');
    if (typeof panel?.title === 'string') titles.push(panel.title.toLowerCase());
    assert(Array.isArray(panel?.targets) && panel.targets.length > 0, `dashboard: panel ${panel?.title} has no targets`);
    for (const target of panel?.targets ?? []) {
      assert(typeof target?.expr === 'string' && target.expr.trim().length > 0, `dashboard: panel ${panel?.title} has an empty target`);
      assert(target?.datasource?.type === 'prometheus', `dashboard: panel ${panel?.title} target must use Prometheus`);
    }
    assert(
      ['timeseries', 'stat', 'table', 'bargauge', 'gauge', 'text'].includes(panel?.type),
      `dashboard: panel ${panel?.title} has an unexpected type ${panel?.type}`,
    );
  }

  const allTitles = titles.join(' | ');
  for (const needle of [
    'availability',
    'requests',
    'errors',
    'latency p50',
    'latency p95',
    'sql p95',
    'outbox',
    'db pool',
    'cpu',
    'ram',
    'auth failures',
  ]) {
    assert(allTitles.includes(needle), `dashboard: missing required panel keyword "${needle}"`);
  }

  assert(
    !/(api[_-]?key|password|secret|bearer)\s*[:=]\s*["'][^"']+/i.test(raw),
    'dashboard: possible literal secret in JSON',
  );
}

function validateSloDoc(text) {
  const required = [
    'http.server.request.duration',
    'aspnetcore.rate_limiting.requests',
    'aspnetcore.authentication.authenticate.duration',
    'dotnet.process.cpu.time',
    'dotnet.process.memory.working_set',
    'db.client.connection.count',
    'db.client.connection.max',
    'probe_success',
    'docgen.db.duration',
    'http_server_request_duration_seconds_bucket',
    'aspnetcore_rate_limiting_requests_total',
    'dotnet_process_cpu_time_seconds_total',
    'docgen_db_duration_seconds_bucket',
    '99.5',
    '500ms',
    '1000ms',
    '80%',
  ];
  for (const needle of required) {
    assert(text.includes(needle), `SLO.md: missing required documented token "${needle}"`);
  }
  assert(text.includes('ميزانية') || text.includes('budget'), 'SLO.md: error budget policy is missing');
  assert(text.includes('Npgsql'), 'SLO.md: Npgsql measurement gap must be stated');
}

function validateRunbook(text) {
  for (const needle of [
    'Observability__OtlpEndpoint',
    'OTEL_EXPORTER_OTLP_ENDPOINT',
    'TEMPO_OTLP_ENDPOINT',
    'node scripts/observability-config-guard.mjs',
    'promtool check rules',
    'otelcol-contrib validate',
    'postgres_exporter',
    'Npgsql',
  ]) {
    assert(text.includes(needle), `runbook: missing required token "${needle}"`);
  }
}

function runSelfTest() {
  const runbook = read(PATHS.runbook);
  const alerts = parseYaml(PATHS.alerts);
  const valid = validateAlerts(alerts, runbook);
  assert(valid.ruleCount >= 20, 'self-test: expected at least 20 rules');
  assert(valid.names.includes('DocGenAvailabilityFastBurn'), 'self-test: expected fast-burn alert');

  const broken = structuredClone(alerts);
  broken.groups[0].rules[0].labels.severity = 'page';
  const before = failures.length;
  validateAlerts(broken, runbook);
  const rejectedSeverity = failures.length > before;
  failures.length = before;
  assert(rejectedSeverity, 'self-test: invalid severity was accepted');

  const brokenRunbook = runbook.replaceAll('DocGenAvailabilityFastBurn', 'REMOVED_ALERT_NAME');
  const beforeMissing = failures.length;
  validateAlerts(alerts, brokenRunbook);
  const rejectedMissing = failures.length > beforeMissing;
  failures.length = beforeMissing;
  assert(rejectedMissing, 'self-test: missing runbook alert was accepted');

  try {
    JSON.parse('{ invalid json');
    fail('self-test: invalid JSON was accepted');
  } catch {
    notes.push('self-test: invalid JSON rejected');
  }

  const tempDir = mkdtempSync(join(tmpdir(), 'obs-guard-'));
  const invalidYaml = join(tempDir, 'invalid.yaml');
  writeFileSync(invalidYaml, 'foo: [unclosed\n', 'utf8');
  let yamlRejected = false;
  try {
    parseYaml(invalidYaml);
  } catch {
    yamlRejected = true;
  } finally {
    rmSync(tempDir, { recursive: true, force: true });
  }
  assert(yamlRejected, 'self-test: invalid YAML was accepted');
  notes.push('self-test: invalid YAML rejected');

  assert(!hasBalanced('sum(rate(x[5m])'), 'self-test: unbalanced expression was accepted');
  notes.push('self-test: alert severity/runbook/JSON/balance checks exercised');
}

function main() {
  const selfTest = process.argv.includes('--self-test');
  let collector;
  let alerts;
  let dashboardRaw;
  let slo;
  let runbook;

  try {
    collector = parseYaml(PATHS.collector);
    alerts = parseYaml(PATHS.alerts);
    dashboardRaw = read(PATHS.dashboard);
    slo = read(PATHS.slo);
    runbook = read(PATHS.runbook);
  } catch (error) {
    console.error(`observability-config-guard: ${error.message}`);
    process.exit(1);
  }

  validateCollector(collector);
  const alertInfo = validateAlerts(alerts, runbook);
  validateDashboard(dashboardRaw);
  validateSloDoc(slo);
  validateRunbook(runbook);

  if (selfTest) {
    try {
      runSelfTest();
    } catch (error) {
      fail(`self-test: ${error.message}`);
    }
  }

  if (failures.length > 0) {
    console.error('observability-config-guard: FAILED');
    for (const failure of failures) console.error(`  - ${failure}`);
    process.exit(1);
  }

  console.log('observability-config-guard: OK');
  console.log(`  collector pipelines: ${Object.keys(collector.service.pipelines).length}`);
  console.log(`  alert groups: ${alerts.groups.length}, rules: ${alertInfo.ruleCount}`);
  console.log(`  SLO labels: ${alertInfo.sloLabels.join(', ')}`);
  console.log(`  dashboard panels: ${JSON.parse(dashboardRaw).panels.length}`);
  for (const note of notes) console.log(`  ${note}`);
}

main();
