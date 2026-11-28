# دليل تشغيل الرصد و`SLO` (`P5-02`)

> **الجمهور:** من ينشر `docgen-api` أو يشغّل الحادث. **المرجع التعاقدي:** [`docs/SLO.md`](../SLO.md).
> **القاعدة:** لا تُغيَّر أي أسرار/عناوين حقيقية داخل هذا الملف؛ كل ما يخص البيئة يُمرَّر
> عبر متغيّرات بيئة في وقت النشر. التطبيق نفسه **لا يتغيّر** في هذه الجولة.

## 1. نظرة سريعة

```
docgen-api (OTLP gRPC :4317)
        |
        v
otel-collector-contrib
  - redaction + health filtering
  - traces:  tail_sampling 10% / 100% errors+slow  ->  Tempo
  - traces:  unsampled EF spans -> span_metrics    ->  Prometheus :8889/metrics
        |
        +--> Prometheus (scrape :8889) --> alert rules --> Alertmanager
        +--> Grafana (Prometheus + Tempo datasources)
        +--> blackbox_exporter probes (/readyz, /healthz/outbox)
```

- مقاييس التطبيق تُسحب من جامع `OTLP` عبر `prometheus` exporter على `:8889`.
- التتبّع يُرسل إلى `Tempo` عبر `otlp` exporter (عنوان `TEMPO_OTLP_ENDPOINT`).
- فحصا `/readyz` و`/healthz/outbox` يُدارَان بـ`blackbox_exporter` (لا يمرّان عبر الجامع).
- لوحة `dashboard.json` وتنبيهات `alerts.yaml` **كرمز** في `ops/observability/`.

## 2. تفعيل الرصد في التطبيق (من `P5-01`)

الرصد **معطَّل افتراضيًا**. للتفعيل في بيئة النشر:

```bash
# الخيار المفضّل (إعداد التطبيق)
Observability__OtlpEndpoint=http://otel-collector:4317
Observability__ServiceName=docgen-api

# أو المتغيّر القياسي (يُقرأ عند غياب/فراغ المفتاح أعلاه)
OTEL_EXPORTER_OTLP_ENDPOINT=http://otel-collector:4317
```

- العنوان يجب أن يكون `http`/`https` مطلقًا؛ أي قيمة أخرى **تُفشل الإقلاع** (سلوك `P5-01`).
- البروتوكول `gRPC` فقط (منفذ `4317`)؛ `OTLP/HTTP` (`4318`) غير مدعوم من التطبيق حاليًا.
- **للتعطيل:** اترك المفتاحين فارغين/غير معرّفين؛ لا مزوّد ولا مُصدِّر ولا اتصال.
- لا تُصدَّر سجلات التطبيق عبر `OTLP` (تبقى `Serilog`).
- **الخصوصية:** لا تُفعّل التقاط الأجسام/الترويسات في الحزم؛ الجامع يحذف الحساسية دفاعيًا.

**التحقق من التطبيق:**

```bash
curl -fsS https://<host>/readyz        # 200 Healthy (أو Degraded مع رسائل فاشلة)
curl -fsS https://<host>/healthz/outbox
```

## 3. نشر الجامع

1. استخدم صورة `otel/opentelemetry-collector-contrib` **بوسم مثبَّت** (لا `latest`) في
   بيئة النشر، ومرّر `TEMPO_OTLP_ENDPOINT` (افتراضي `http://tempo:4317`).
2. انسخ [`ops/observability/otel-collector.yaml`](../../ops/observability/otel-collector.yaml)
   كما هو (أو عبر `ConfigMap`). المنافذ:
   - `4317`/`4318`: استقبال `OTLP` (داخلي فقط، لا يُكشف للإنترنت).
   - `8889`: `/metrics` لسحب `Prometheus`.
   - `13133`: فحص صحة الجامع.
3. **لا تضع كلمات مرور أو رموزًا** في الملف؛ لا يحتوي الجامع مصادقة `OTLP` (حدّ `P5-01`)،
   لذا يجب أن يبقى على شبكة داخلية/محمية.
4. تحقّق قبل التشغيل:

```bash
otelcol-contrib validate --config=ops/observability/otel-collector.yaml
```

5. تحقّق بعد التشغيل:

```bash
curl -fsS http://otel-collector:13133/          # health extension
curl -fsS http://otel-collector:8889/metrics | head
```

> **أسماء المقاييس:** `prometheus.translation_strategy: UnderscoreEscapingWithSuffixes`،
> و`transform/resource_labels` ينسخ `service.name` إلى `service_name` في كل سلسلة. لا تعتمد
> على `resource_to_telemetry_conversion` (مهجور في إصدارات الجامع الحديثة).

### 3.1 ماذا يحذف الجامع؟

`attributes/redact` يحذف: `http.request.header.authorization`، `http.request.header.cookie`،
`http.response.header.set-cookie`، `url.query`، `http.target`، `db.query.text`،
`db.statement`، `db.query.parameter`، وأي مفتاح يطابق
`password|passwd|secret|token|api_key|apikey|authorization`. النتيجة: لا نصوص `SQL` ولا
استعلامات URL ولا بيانات اعتماد في `Tempo`/`Prometheus`. **لا تُضف** `db.query.text` إلى
أبعاد `span_metrics`؛ القائمة تستبعد `span.name` أيضًا.

## 4. استيراد التنبيهات

الملف [`ops/observability/alerts.yaml`](../../ops/observability/alerts.yaml) هو
`PrometheusRule` عادي (`groups[].rules[]`).

```bash
# 1) تحقق محلي إلزامي
promtool check rules ops/observability/alerts.yaml

# 2) Prometheus تقليدي: ضعه في rule_files ثم أعد التحميل
#    rule_files:
#      - /etc/prometheus/rules/docgen/*.yaml
curl -X POST http://prometheus:9090/-/reload

# 3) Kubernetes Prometheus Operator: غلّفه كـ PrometheusRule
#    metadata.labels.release: <اسم إصدار kube-prometheus-stack>
kubectl apply -f ops/observability/alerts.yaml
```

- التنبيهات تحمل `labels.slo` و`annotations.runbook_url`؛ لا تغيّر الاسم (`alert`) بلا
  تحديث هذا الدليل (الحارس يتحقق من التغطية).
- اضبط `Prometheus` على احتفاظ **≥ 30 يومًا** (`--storage.tsdb.retention.time=30d` أو أكثر)
  كي تعمل نوافذ `SLO` الشهرية و`30d`؛ وإلا تصبح اللوحة مضللة بلا بيانات.
- القواعد الموسومة `optional: "true"` لا تُطلق حتى يظهر مقياسها (لا `absent()` مصطنع):
  `db.client.*` (متر `Npgsql`) و`docgen_outbox_*` (postgres_exporter).

## 5. فحوص `blackbox_exporter`

أنشئ الوحدات والوظائف التالية (أسماء `job` إلزامية لتطابق `alerts.yaml`):

```yaml
# blackbox.yml
modules:
  docgen_http_2xx:
    prober: http
    timeout: 5s
    http:
      valid_http_versions: ["HTTP/1.1", "HTTP/2.0"]
      preferred_ip_protocol: ip4
  docgen_outbox_healthy:
    prober: http
    timeout: 5s
    http:
      valid_http_versions: ["HTTP/1.1", "HTTP/2.0"]
      preferred_ip_protocol: ip4
      fail_if_body_not_matches_regexp: ["^Healthy"]
```

```yaml
# prometheus.yml (مقتطف)
scrape_configs:
  # مقاييس التطبيق تُسحب من الجامع، لا من التطبيق مباشرة.
  - job_name: docgen-collector
    scrape_interval: 30s
    static_configs:
      - targets: ["otel-collector:8889"]

  - job_name: docgen-ready
    scrape_interval: 30s
    metrics_path: /probe
    params:
      module: [docgen_http_2xx]
    static_configs:
      - targets: ["https://<host>/readyz"]
    relabel_configs: &blackbox_relabel
      - source_labels: [__address__]
        target_label: __param_target
      - source_labels: [__param_target]
        target_label: instance
      - target_label: __address__
        replacement: blackbox-exporter:9115

  - job_name: docgen-outbox-ready
    scrape_interval: 30s
    metrics_path: /probe
    params:
      module: [docgen_http_2xx]
    static_configs:
      - targets: ["https://<host>/healthz/outbox"]
    relabel_configs: *blackbox_relabel

  - job_name: docgen-outbox-health
    scrape_interval: 30s
    metrics_path: /probe
    params:
      module: [docgen_outbox_healthy]
    static_configs:
      - targets: ["https://<host>/healthz/outbox"]
    relabel_configs: *blackbox_relabel
```

- `docgen-outbox-ready`: `probe_success=0` عند `503` (تأخّر > 15 دقيقة).
- `docgen-outbox-health`: `probe_success=0` عند جسم `Degraded` (رسائل فاشلة) أو `503`.
- لا تُستخدم `fail_if_body_not_matches_regexp` على `/readyz` (قد يعيد `Degraded` مع `200`).

## 6. استيراد اللوحة

1. `Grafana → Dashboards → New → Import → Upload JSON` واختر
   [`ops/observability/dashboard.json`](../../ops/observability/dashboard.json).
2. اختر متغيّر `DS_PROMETHEUS` (مصدر `Prometheus` الذي يسحب الجامع).
3. أضف `Tempo` كمصدر `Traces` إن رغبت في الانتقال من اللوحة إلى التتبّع (غير مطلوب للوحات).
4. للحفظ كـ provisioning: ضع الملف تحت `provisioning/dashboards` مع مزوّد `file`؛
   اللوحة تحمل `uid: docgen-slo-p5-02` فتحديث الملف يحدّث النسخة نفسها.

## 7. التحقق الآلي (قبل كل دمج)

```bash
node scripts/observability-config-guard.mjs
node scripts/observability-config-guard.mjs --self-test
promtool check rules ops/observability/alerts.yaml
otelcol-contrib validate --config=ops/observability/otel-collector.yaml
jq empty ops/observability/dashboard.json
```

الحارس يفحص: صلاحية `YAML`/`JSON`، مراجع مكوّنات الجامع، وجود `batch`/`memory_limiter`/حذف
`PII`، تفرّد أسماء التنبيهات، `severity`/`for`/`runbook_url`، تغطية كل تنبيه في هذا الدليل،
ووجود أسماء المقاييس المطلوبة في `docs/SLO.md`.

## 8. إجراءات التنبيهات (Alert → Action)

| التنبيه | ماذا تفعل فورًا | التصعيد |
|---|---|---|
| `DocGenApiReadinessDown` | `curl /readyz`، افحص القاعدة و`/healthz/outbox`، لا تعِد التشغيل قبل معرفة السبب | صفحة للمالك |
| `DocGenTelemetryAbsent` | تحقق من الجامع و`Prometheus` و`Observability__OtlpEndpoint` | تذكرة بنية تحتية |
| `DocGenCollectorDown` | أعد تشغيل حاوية الجامع، تحقق من `:13133`/`:8889`، لا تعِد تشغيل التطبيق | تذكرة |
| `DocGenAvailabilityFastBurn` | افحص `5xx` حسب `http_route`، السجلات، `SQL`، النشر الأخير؛ جمّد الإصدارات غير العاجلة | صفحة + حادث |
| `DocGenAvailabilitySlowBurn` | نفس الفحص بنافذة أوسع؛ افتح تذكرة | تذكرة |
| `DocGenAvailabilityBudgetBurning` | أضف بندًا في المراجعة الأسبوعية، لا تجميد فوري | قائمة عمل |
| `DocGenReadP95Warning` | افحص `SQL p95`، `DB pool`، `CPU`، أخطاء `N+1`/فهارس | تذكرة |
| `DocGenReadP95Critical` | نفس الفحص مع أولوية عالية؛ قد يكون تدهورًا مرئيًا للمستخدم | صفحة |
| `DocGenWriteP95Warning` | افحص الأقفال/المعاملات/`Outbox`/`SQL`، وحجم الطلبات | تذكرة |
| `DocGenWriteP95Critical` | نفس الفحص مع أولوية عالية؛ تحقق من عدم فشل الكتابات | صفحة |
| `DocGenSqlP95Warning` | `pg_stat_activity`، استعلامات بطيئة، فهارس، أقفال؛ راجع آخر هجرة/استعلام | تذكرة |
| `DocGenSqlP95Critical` | نفس الفحص مع أولوية عالية؛ راجع خطة الاستعلام فورًا | صفحة |
| `DocGenOutboxLagCritical` | اتبع [`outbox-stuck.md`](outbox-stuck.md)؛ افحص `HeadAlertOutboxMessages` و`LastError` | صفحة |
| `DocGenOutboxDegraded` | راجع الرسائل `failed` وأعد المحاولة بعد الإصلاح؛ لا تُسقط الجهوزية | تذكرة |
| `DocGenOutboxLagNumeric` | تحقق من تأخّر التنبيهات الفعلي (اختياري عبر `postgres_exporter`) | تذكرة |
| `DocGenOutboxRetriesHigh` | راجع سبب الإعادات و`LastError` (اختياري) | تذكرة |
| `DocGenDbPoolSaturationWarning` | (عند تفعيل متر `Npgsql`) افحص تسريب `DbContext`، استعلامات طويلة، `MaxPoolSize` | تذكرة |
| `DocGenDbPoolSaturationCritical` | نفس الفحص مع أولوية عالية؛ الطلبات ستصطف وتنتهي مهلتها | صفحة |
| `DocGenDbPoolTimeouts` | نفس الفحص + `Database:Pool`؛ تحقق من صحة القاعدة | تذكرة |
| `DocGenAuthFailureWarning` | تحقق من `Jwt`/الساعة/التوكنات، وابحث عن نمط تخمين من IP واحد؛ لا تسجّل كلمات المرور | تحقيق |
| `DocGenAuthFailureCritical` | تعامل كحادث أمني/انقطاع مصادقة فورًا | أمن/صفحة |
| `DocGenAuthServerErrors` | افحص `5xx` على `/api/auth/*` في السجلات/التتبّع، دون طباعة أسرار | تذكرة |
| `DocGenRateLimitRejectionWarning` | راجع السياسة (`RateLimiting`) وأعلى IP/مستخدم؛ ميّز هجومًا من حدّ ضيّق | تذكرة |
| `DocGenRateLimitRejectionCritical` | تعامل كحادث أمني/سعة؛ قد يكون هجومًا أو حدًّا يمنع المستخدمين | أمن/صفحة |

## 9. لوحة القياس العددي لصندوق الصادر (اختياري)

لا يصدر التطبيق عدّاد تأخّر عدديًا؛ `P5-03` يعيد حالة صحية فقط. لقياس الدقائق/الإعادات
أضف استعلامات `postgres_exporter` (لا تغيير في التطبيق):

```yaml
# postgres_exporter queries.yaml (مقتطف)
- query:
    name: docgen_outbox_lag_minutes
    help: "Minutes since the oldest pending/processing outbox message"
    sql: |
      SELECT COALESCE(EXTRACT(EPOCH FROM (now() - MIN("CreatedAt"))) / 60.0, 0)::float8
      FROM "HeadAlertOutboxMessages"
      WHERE "Status" IN ('pending', 'processing')
- query:
    name: docgen_outbox_failed_messages
    help: "Outbox messages in failed status"
    sql: |
      SELECT COUNT(*)::float8
      FROM "HeadAlertOutboxMessages"
      WHERE "Status" = 'failed'
- query:
    name: docgen_outbox_retry_attempts
    help: "Accumulated retry attempts for pending/processing/failed outbox messages (gauge; use delta)"
    sql: |
      SELECT COALESCE(SUM(GREATEST("Attempts" - 1, 0)), 0)::float8
      FROM "HeadAlertOutboxMessages"
      WHERE "Status" IN ('pending', 'processing', 'failed')
```

- استخدم حساب قاعدة **قراءة فقط** لهذه الاستعلامات، ولا تعرض سلسلة الاتصال.
- عند تفعيلها تصبح `DocGenOutboxLagNumeric`/`DocGenOutboxRetriesHigh` فعّالة تلقائيًا.

## 10. فجوات معروفة (لا تُخفى)

1. **`Npgsql` meter مسجَّل (P5-02b):** `db.client.*` يُصدَر الآن بعد `.AddMeter("Npgsql")`؛
   تبقى خطوة النشر: ظهور `db_client_connection_*` في `Prometheus` وتغذية `SLO-5` واللوحة.
2. **لا نشر حي:** `Prometheus`/`Grafana`/`Tempo`/`blackbox_exporter` خارج المستودع؛
   الملفات جاهزة لكن النشر مسؤولية المالك/البنية (`P5-04` مستقبلًا).
3. **لا مصادقة `OTLP`:** الجامع يجب أن يبقى داخليًا (حدّ `P5-01` §10).
4. **زمن `SQL` مشتق من التتبّع:** مسار `span_metrics` غير معاين، لكنه ليس عدّادًا أصليًا؛
   استخدمه للاتجاه لا للمحاسبة الدقيقة.
5. **لا `traceparent` من المتصفح:** سلسلة الواجهة→الخادم غير مكتملة بعد.

## SLO: availability

**الهدف:** 99.5% شهريًا؛ **الميزانية:** 0.5%. راجع [`docs/SLO.md`](../SLO.md) §3.1.
**الفحص:** `1 - 5xx/all` عبر `http_route=~"/api/.*"` (بلا مسارات الصحة/الملفات الثابتة).
**الأعراض:** `DocGenAvailabilityFastBurn` (7.2% أخطاء/ساعة) أو `DocGenApiReadinessDown`.
**الخطوات:** حدّد المسار/النسخة/آخر نشر، افحص `5xx` حسب `http_route`، ثم القاعدة/`SQL`.
**الخروج:** عودة النسبة تحت `0.5%` وعدم تكرار النمط، مع تسجيل الحادث.

## SLO: read-latency

**الهدف:** `p95 ≤ 500ms` لقراءات `GET/HEAD`؛ **الميزانية:** 5% من الطلبات البطيئة.
**الخطوات:** رتّب المسارات حسب `p95`، ثم `SQL p95`، ثم `DB pool`/`CPU`؛ ابحث عن `Seq Scan`
أو استعلام جديد أو تغيّر فهرس. لا ترفع الفهارس في الإنتاج بلا مراجعة هجرة.
**الخروج:** `p95 ≤ 500ms` لنافذة `30m` على الأقل.

## SLO: write-latency

**الهدف:** `p95 ≤ 1s` لكتابات `POST/PUT/PATCH/DELETE`؛ **الميزانية:** 5%.
**الخطوات:** افحص الأقفال/المعاملات الطويلة، `Outbox`، `SQL`، وحجم الحمولة؛ راجع `Version`
وإعادة المحاولة (`409`/`Concurrency`). لا تعِد إرسال الكتابة بلا `Idempotency-Key`.
**الخروج:** `p95 ≤ 1s` وعدم تصاعد `5xx`.

## SLO: outbox

**الهدف:** 99.5% من فحوص `/healthz/outbox` سليمة؛ العتبة **15 دقيقة**.
**الخطوات:** اتبع [`outbox-stuck.md`](outbox-stuck.md): افحص `pending/processing/failed`،
`NextAttemptAt`، `LockedUntil`، `LastError`، ثم أصلح السبب (اعتماد خارجي/بيانات) وأعد المحاولة.
**الخروج:** `probe_success=1` للفحصين، ولا رسائل `failed` جديدة.

## SLO: db-pool

**الهدف (عند التفعيل):** `used/max ≤ 80%` في 99% من الدقائق.
**الخطوات:** افحص تسريب `DbContext` (عدم `await using`)، استعلامات طويلة، و`MaxPoolSize`.
لا ترفع `MaxPoolSize` كحل أول بلا قياس سعة القاعدة.
**الخروج:** النسبة تحت `0.8` مع اختفاء `timeouts`.

## SLO: auth

**الهدف التشغيلي:** فشل المصادقة `≤ 25%` (حارس، لا ميزانية إتاحة).
**الخطوات:** تحقق من `Jwt:Secret`/`Issuer`/`Audience`/`ClockSkew`، صلاحية التوكنات، وساعة
الخادم؛ راجع أنماط `401` من IP واحد (تخمين) دون تسجيل بيانات اعتماد. للحرج `>50%` تعامل
كحادث أمني/انقطاع مصادقة.
**الخروج:** النسبة تحت الحد، ولا `5xx` على `/api/auth/*`.

## SLO: rate-limit

**الهدف:** رفض حدّ المعدل `≤ 5%` (حارس سعة/أمان).
**الخطوات:** راجع `RateLimiting` (الحدود العامة/الدخول/الغالي)، وأعلى المستخدمين/IP؛ ميّز
هجومًا من حدّ ضيّق يمنع الجوال. لا ترفع الحدود بلا مراجعة أمنية.
**الخروج:** النسبة تحت `5%` دون فتح الباب لهجوم.

## SLO: telemetry

**الهدف:** وجود التليمترية نفسها (لا `SLO` رقمي): `DocGenTelemetryAbsent`/`DocGenCollectorDown`.
**الخطوات:** تحقق من `Observability__OtlpEndpoint`، حاوية الجامع، `:8889`، هدف سحب
`Prometheus` `job="docgen-collector"`، و`blackbox` `job="docgen-ready"`.
**الخروج:** ظهور `http_server_request_duration_seconds_count` و`probe_success` من جديد.
