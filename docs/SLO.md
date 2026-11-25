# تعريف `SLO` وحدود خدمة `docgen-api` — `P5-02`

> **الحالة:** تعريف وتوصيف قابل للتنفيذ (SLO + تنبيهات + لوحة كرمز). **لا تغيير في وقت التشغيل**
> في هذه الجولة: لا حزم جديدة، ولا تغيير في `Program.cs` أو `ObservabilitySetup`، ولا هجرة،
> ولا نقطة نهاية جديدة. التطبيق يبقى **معطَّل الرصد افتراضيًا** كما في `P5-01`.
>
> **المراجع:** [`docs/audit/255`](audit/255-p5-01-opentelemetry-20261126.md)،
> [`256`](audit/256-p5-01-critical-review-20261126.md)، [`257`](audit/257-p5-01-adversarial-review-20261126.md)،
> `docs/PERFECTION_ROADMAP.md` §`P5-01`/`P5-02`/`P5-03`، و`RUN_GUIDE.md` §9 «الرصد والتتبع».
>
> **ملفات التشغيل:** [`ops/observability/otel-collector.yaml`](../ops/observability/otel-collector.yaml)،
> [`ops/observability/alerts.yaml`](../ops/observability/alerts.yaml)،
> [`ops/observability/dashboard.json`](../ops/observability/dashboard.json)،
> والدليل [`docs/runbooks/observability.md`](runbooks/observability.md).

## 1. النطاق والمالك

| البند | القيمة |
|---|---|
| الخدمة | `docgen-api` (واجهة `ASP.NET Core` خلف `Render`) |
| المالك | مالك المشروع (`@maarouf003-netizen`) |
| بيئات القياس | `production` أساسًا، و`staging` عند توفره (`P5-04` لم يُنجز بعد) |
| مصدر التليمترية | `OpenTelemetry` من التطبيق → جامع `OTLP` → `Prometheus` (مقاييس) و`Tempo` (تتبّع) |
| نوافذ القياس | شهر تقويمي (`calendar month`, `UTC`) للـ`SLO`، ونوافذ متدحرجة (`5m`/`30m`/`1h`/`6h`/`3d`/`30d`) للتنبيهات |
| الاحتفاظ المطلوب | `Prometheus` ≥ **30 يومًا** (وإلا لا يمكن حساب `SLO` الشهري أو نافذة `30d`) |
| الجمهور | المالك/المراجع، وأي مسؤول نشر يقرأ `docs/runbooks/observability.md` |

**قاعدة أساسية:** كل رقم في هذا الملف إمّا (أ) مقياس يُصدره التطبيق فعلًا عبر `P5-01`، أو
(ب) مقياس مشتق في الجامع من تتبّع/فحص موجود، أو (ج) **فجوة معلنة صراحةً** لا يُدَّعى قياسها.
لا يوجد في هذا الملف مقياس «مفترض» بلا مصدر.

## 2. أسماء المقاييس والوسوم (العقد الفعلي)

### 2.1 ما يُصدره التطبيق عند التفعيل

عند ضبط `Observability:OtlpEndpoint` (أو `OTEL_EXPORTER_OTLP_ENDPOINT`) يسجّل `P5-01`
أدوات `AspNetCore` و`HttpClient` و`Runtime`، وعلى `net10.0` تُفعّل أداة `AspNetCore`
**كل مقاييس `ASP.NET Core` المدمجة** في الأمتار المسجّلة (`Microsoft.AspNetCore.Hosting`
و`RateLimiting` و`Authentication` و`Authorization` و`Routing` و`Server.Kestrel`…).
هذه أسماء `OTLP` (بنقاط) قبل تحويل الجامع:

| المقياس (`OTLP`) | النوع | الوحدة | الوسوم المهمة | المصدر |
|---|---|---|---|---|
| `http.server.request.duration` | Histogram | `s` | `http.request.method`, `http.response.status_code`, `http.route`, `url.scheme`, `error.type` | `Microsoft.AspNetCore.Hosting` |
| `http.server.active_requests` | UpDownCounter | `{request}` | `http.request.method`, `url.scheme` | `Microsoft.AspNetCore.Hosting` |
| `http.client.request.duration` | Histogram | `s` | `http.request.method`, `http.response.status_code`, `server.address` | `HttpClient` |
| `aspnetcore.rate_limiting.requests` | Counter | `{request}` | `aspnetcore.rate_limiting.policy`, `aspnetcore.rate_limiting.result` (`acquired`/`endpoint_limiter`/`global_limiter`/`request_canceled`) | `Microsoft.AspNetCore.RateLimiting` |
| `aspnetcore.rate_limiting.request.time_in_queue` | Histogram | `s` | policy/result | `Microsoft.AspNetCore.RateLimiting` |
| `aspnetcore.authentication.authenticate.duration` | Histogram | `s` | `aspnetcore.authentication.result` (`success`/`failure`/`none`), `aspnetcore.authentication.scheme` | `Microsoft.AspNetCore.Authentication` |
| `aspnetcore.authentication.challenges` | Counter | `{challenge}` | scheme | `Microsoft.AspNetCore.Authentication` |
| `aspnetcore.authentication.forbids` | Counter | `{forbid}` | scheme | `Microsoft.AspNetCore.Authentication` |
| `aspnetcore.authorization.attempts` | Counter | `{attempt}` | `aspnetcore.authorization.result`, policy | `Microsoft.AspNetCore.Authorization` |
| `aspnetcore.diagnostics.exceptions` | Counter | `{exception}` | `aspnetcore.diagnostics.exception.result`, `error.type` | `Microsoft.AspNetCore.Diagnostics` |
| `kestrel.active_connections` | UpDownCounter | `{connection}` | `network.transport`, `server.address`, `server.port` | `Microsoft.AspNetCore.Server.Kestrel` |
| `kestrel.rejected_connections` | Counter | `{connection}` | transport/address | `Microsoft.AspNetCore.Server.Kestrel` |
| `dotnet.process.cpu.time` | Counter | `s` | `cpu.mode` (`user`/`system`) | `System.Runtime` |
| `dotnet.process.memory.working_set` | UpDownCounter | `By` | — | `System.Runtime` |
| `dotnet.gc.heap.total_allocated` | Counter | `By` | — | `System.Runtime` |
| `dotnet.thread_pool.thread.count` | UpDownCounter | `{thread}` | — | `System.Runtime` |
| `dotnet.thread_pool.queue.length` | UpDownCounter | `{work_item}` | — | `System.Runtime` |
| `dotnet.exceptions` | Counter | `{exception}` | `error.type` | `System.Runtime` |

> **مهم:** أسماء `dotnet.*` هي أسماء **المقاييس المدمجة في `.NET 9+`** التي تسجّلها حزمة
> `OpenTelemetry.Instrumentation.Runtime` عبر متر `System.Runtime` (وليست أسماء
> `process.runtime.dotnet.*` القديمة). لا يُستخدم أي اسم قديم في هذا الملف أو في التنبيهات.

### 2.2 ما يشتقه الجامع (بلا تغيير في التطبيق)

| المقياس المشتق | كيف يُشتق | لماذا |
|---|---|---|
| `docgen.db.duration` | موصّل `span_metrics` من أنشطة `OpenTelemetry.Instrumentation.EntityFrameworkCore` فقط (`include_instrumentation_scope`)، بوحدة `s`، مع استبعاد `span.name` عالي التعددية | زمن `SQL` كمقياس بلا إضافة عدّاد في التطبيق |
| `docgen.db.calls` | نفس الموصّل (عدّاد لكل مجموعة أبعاد) | معدّل أوامر القاعدة |

**الحدود:** `span_metrics` مكوّن `alpha` في جامع `contrib`؛ لذلك يُشغَّل في مسار تتبّع
**منفصل غير معاين** (انظر `otel-collector.yaml`) كي لا تتأثر دقة زمن `SQL` بعيّنة التتبّع.
يبقى زمن `SQL` **مشتقًا من التتبّع** لا من عدّاد أصلي، ويُوثَّق ذلك في اللوحة.

### 2.3 التحويل إلى أسماء `Prometheus`

جامع `OTLP` يُصدّر المقاييس عبر `prometheus` exporter مع
`translation_strategy: UnderscoreEscapingWithSuffixes` (السياسة الموثّقة). القواعد: النقاط → `_`، وإضافة لاحقة الوحدة (`s` → `_seconds`، `By` → `_bytes`)،
و`_total` للمجاميع الرتيبة (`Counter`)، ولا لاحقة للوحدات التعليقية (`{request}`…).
هذه أسماء الاستعلام الفعلية:

| `OTLP` | `Prometheus` |
|---|---|
| `http.server.request.duration` | `http_server_request_duration_seconds_{bucket,sum,count}` |
| `http.server.active_requests` | `http_server_active_requests` |
| `http.client.request.duration` | `http_client_request_duration_seconds_{bucket,sum,count}` |
| `aspnetcore.rate_limiting.requests` | `aspnetcore_rate_limiting_requests_total` |
| `aspnetcore.rate_limiting.request.time_in_queue` | `aspnetcore_rate_limiting_request_time_in_queue_seconds_{bucket,sum,count}` |
| `aspnetcore.authentication.authenticate.duration` | `aspnetcore_authentication_authenticate_duration_seconds_{bucket,sum,count}` |
| `aspnetcore.authentication.challenges` | `aspnetcore_authentication_challenges_total` |
| `aspnetcore.authentication.forbids` | `aspnetcore_authentication_forbids_total` |
| `aspnetcore.authorization.attempts` | `aspnetcore_authorization_attempts_total` |
| `aspnetcore.diagnostics.exceptions` | `aspnetcore_diagnostics_exceptions_total` |
| `kestrel.active_connections` | `kestrel_active_connections` |
| `kestrel.rejected_connections` | `kestrel_rejected_connections_total` |
| `dotnet.process.cpu.time` | `dotnet_process_cpu_time_seconds_total` |
| `dotnet.process.memory.working_set` | `dotnet_process_memory_working_set_bytes` |
| `dotnet.gc.heap.total_allocated` | `dotnet_gc_heap_total_allocated_bytes_total` |
| `dotnet.thread_pool.thread.count` | `dotnet_thread_pool_thread_count` |
| `dotnet.thread_pool.queue.length` | `dotnet_thread_pool_queue_length` |
| `dotnet.exceptions` | `dotnet_exceptions_total` |
| `docgen.db.duration` (مشتق) | `docgen_db_duration_seconds_{bucket,sum,count}` (`docgen_db_duration_seconds_bucket` للاستعلام) |
| `docgen.db.calls` (مشتق) | `docgen_db_calls_total` |

**الوسوم:** `http.request.method` → `http_request_method`، `http.response.status_code` →
`http_response_status_code`، `http.route` → `http_route`، `aspnetcore.authentication.result` →
`aspnetcore_authentication_result`، `aspnetcore.rate_limiting.result` →
`aspnetcore_rate_limiting_result`، `cpu.mode` → `cpu_mode`.
`transform/resource_labels` في الجامع ينسخ `service.name`/`service.namespace`/
`deployment.environment` إلى وسوم كل نقطة (`service_name`, `service_namespace`,
`deployment_environment`) بدل `resource_to_telemetry_conversion` المهجور. **لا** نستخدم `job`
للدلالة على التطبيق: `job` يضبطه `Prometheus` لوظيفة سحب الجامع؛ التصفية الصحيحة
`service_name="docgen-api"`.

### 2.4 ما **لا** يُقاس في هذه الجولة (فجوات صريحة)

| الفجوة | السبب | الأثر على `SLO` | المعالجة |
|---|---|---|---|
| `db.client.connection.count` / `db.client.connection.max` / `db.client.connection.npgsql.timeouts` | `Npgsql` يصدرها على متر `Npgsql`، لكن `P5-01` لا يسجّل هذا المتر (`.AddMeter("Npgsql")` غير موجود) | `SLO-5` (تشبّع المجمّع) **غير مقيس** حاليًا | تفعيل المتر في تغيير لاحق (`P5-02b`)؛ القواعد موجودة في `alerts.yaml` وتصبح فعّالة تلقائيًا عند ظهور المقياس |
| تأخّر `Outbox` بالدقائق | `P5-03` يفحصه ويعيد **حالة صحية** فقط (`/healthz/outbox`)، لا عدّادًا عدديًا | يُقاس كـ**نسبة نجاح فحص** لا كدقائق | `SLO-4` يعرّف على `probe_success`؛ الدقائق العددية اختيارية عبر `postgres_exporter` |
| عدد إعادات `Outbox` / الرسائل الفاشلة | لا عدّاد تطبيقي؛ الحالة تظهر في جسم `/healthz/outbox` فقط | لوحة «retries» اختيارية | استعلام `postgres_exporter` موثّق في الدليل؛ ليست جزءًا من `SLO` |
| تتبّع من المتصفح | الواجهة لا تحمل `traceparent` بعد (`P5-01` §10) | لا يؤثر على `SLO` الخادم | `P6`/تغيير لاحق |
| فحوص `/readyz` و`/healthz/outbox` | تتطلب `blackbox_exporter` (غير منشور) | `SLO-4` وفحص جهوزية `SLO-1` يحتاجان نشر `blackbox` | دليل التشغيل §5 |

## 3. تعريفات `SLI` و`SLO`

> جميع النوافذ بالـ`UTC`. «الشهر» = شهر تقويمي (مثل `2026-11-01T00:00Z` إلى
> `2026-12-01T00:00Z`). «الجيد» و«الإجمالي» يُحسبان على كل نسخ الخدمة مجموعًا.

### 3.1 `SLO-1` — إتاحة `API` (99.5% شهريًا)

**`SLI`:** نسبة الطلبات التي أنجزها الخادم بردّ غير `5xx` من كل طلبات `API` التي لها رمز
حالة، في الشهر.

```
availability_ratio =
  sum(rate(http_server_request_duration_seconds_count{
        service_name="docgen-api", http_route=~"/api/.*",
        http_response_status_code=~"[1-4][0-9][0-9]"}[30d]))
  /
  sum(rate(http_server_request_duration_seconds_count{
        service_name="docgen-api", http_route=~"/api/.*",
        http_response_status_code=~"[1-5][0-9][0-9]"}[30d]))
```

**الهدف:** `≥ 99.5%`. **ميزانية الخطأ:** `0.5%` من الطلبات (تعادل 216 دقيقة شهريًا فقط
إذا حُوّلت إلى زمن عند معدل ثابت؛ الميزانية الفعلية بالطلبات).

**الاستثناءات:**
- استثناء مسارات الصحة `"/healthz"`, `"/readyz"`, `"/healthz/outbox"`، و`/metrics`، والملفات
  الثابتة/`SPA` (لا تطابق `http_route=~"/api/.*"`).
- `4xx` (بما فيها `401`/`403`/`404`/`429`) **ليست** فشل إتاحة: الخادم أجاب. تُراقب `429`
  و`401/403` كحارس منفصل (`SLO-6`/`SLO-7`).
- الطلب بلا رمز حالة (انقطع قبل الرد) مستبعد من البسط والمقام (المقام يشترط `[1-5][0-9][0-9]`).
- طلبات لا تطابق مسارًا (`404` قبل التوجيه) لا تحمل `http.route` فتُستبعد؛ لا تُخفى كعطل عام
  لأن فحص الجهوزية يغطي انقطاع الخدمة الكلي.

**فحص الجهوزية المرافق (لا يدخل الميزانية):** `probe_success{job="docgen-ready"}` على
`/readyz` يجب أن يبقى `1`؛ انقطاع كلي بلا طلبات لا يظهر في نسبة الطلبات، لذلك تنبيه
`DocGenApiReadinessDown` إلزامي.

### 3.2 `SLO-2` — زمن قراءة `p95` ≤ 500ms

**`SLI`:** `p95` لمدد طلبات `GET`/`HEAD` على `/api/*` (بلا مسارات الصحة)، من هيستوغرام
`http.server.request.duration`.

```
read_p95 =
  histogram_quantile(0.95,
    sum by (le) (rate(http_server_request_duration_seconds_bucket{
      service_name="docgen-api", http_route=~"/api/.*",
      http_request_method=~"GET|HEAD"}[30m])))
```

**الهدف:** `read_p95 ≤ 0.5s` لكل نافذة `30m`، و`≤ 0.5s` عند تجميع الشهر.
**ميزانية الخطأ:** `5%` من طلبات القراءة مسموح أن تتجاوز `500ms` (تعريف `p95`).
**تنبيهات:** تحذير عند `p95 > 0.5s` لـ`30m`، وحرج عند `p95 > 1.0s` لـ`5m`.

### 3.3 `SLO-3` — زمن كتابة `p95` ≤ 1000ms

**`SLI`:** `p95` لمدد طلبات `POST`/`PUT`/`PATCH`/`DELETE` على `/api/*`.

```
write_p95 =
  histogram_quantile(0.95,
    sum by (le) (rate(http_server_request_duration_seconds_bucket{
      service_name="docgen-api", http_route=~"/api/.*",
      http_request_method=~"POST|PUT|PATCH|DELETE"}[30m])))
```

**الهدف:** `≤ 1.0s` (حدّ `DoD` في الخريطة للكتابة على `staging`). **الميزانية:** `5%` من
طلبات الكتابة فوق `1s`. **تنبيهات:** تحذير `>1s` لـ`30m`، وحرج `>2s` لـ`5m`.

> **ملاحظة:** الكتابات المحمية بـ`Idempotency-Key` قد تُعاد من ذاكرة الاستجابة؛ تُحتسب كما
> هي (زمن الخادم الفعلي). لا تُستثنى من `p95`.

### 3.4 `SLO-4` — تأخّر صندوق الصادر (`Outbox`) ≤ 15 دقيقة

**`SLI`:** نسبة نجاح فحص `/healthz/outbox` (يعيد `200` عندما لا يتجاوز تأخّر أقدم رسالة
معلّقة `HeadAlerts:Outbox:Health:LagThresholdMinutes` الافتراضي **15 دقيقة**، و`503` عند
التجاوز — `P5-03`/`OutboxHealthPolicy`).

```
outbox_healthy_ratio = avg_over_time(probe_success{job="docgen-outbox-ready"}[30d])
```

**الهدف:** `≥ 99.5%` من فحوص النافذة (فحص كل `30s`). **الميزانية:** `0.5%` ≈ 3.6 ساعة/شهر.

**حالة `Degraded`:** رسائل فاشلة (`FailedCount > 0`) تعيد `200` مع جسم `Degraded`، لذلك
فحص `job="docgen-outbox-health"` (يقبل جسم `^Healthy` فقط) يكشفها كتحذير لا كحرج.
تأخّر `>15m` يعيد `503` فيسقط `job="docgen-outbox-ready"` (حرج).

**قياس عددي اختياري:** `docgen_outbox_lag_minutes` من `postgres_exporter` (استعلام في الدليل)
— ليس جزءًا من `SLO` الأساسي، ولا تفشل اللوحة بغيابه.

### 3.5 `SLO-5` — تشبّع مجمّع اتصالات القاعدة ≤ 80%

**`SLI` (عند تفعيل متر `Npgsql`):**

```
db_pool_used_ratio =
  db_client_connection_count{state="used"}
  /
  clamp_min(db_client_connection_max, 1)
```

**الهدف:** `≤ 0.80` في `99%` من الدقائق شهريًا. **الميزانية:** `1%` من الدقائق.
**الحالة الحالية:** **غير مقيس** — متر `Npgsql` غير مسجّل في `P5-01`؛ القواعد موجودة في
`alerts.yaml` لكنها لا تُطلق حتى يظهر المقياس (لا `absent()` يفبرك تنبيهًا دائمًا).
**الحدّ الآمن:** `20` هو `Database:Pool:MaxPoolSize` الافتراضي، لكن الاستعلام يستخدم
`db_client_connection_max` الفعلي لا رقمًا مثبتًا.

### 3.6 `SLO-6` — فشل المصادقة (حارس أمني/تشغيلي)

**`SLI`:** نسبة عمليات المصادقة المنتهية `failure` من عمليات `success + failure`
(`none` مستبعد لأنه طلب بلا بيانات اعتماد، وليس فشلًا):

```
auth_failure_ratio =
  sum(rate(aspnetcore_authentication_authenticate_duration_seconds_count{
        service_name="docgen-api", aspnetcore_authentication_result="failure"}[30d]))
  /
  clamp_min(sum(rate(aspnetcore_authentication_authenticate_duration_seconds_count{
        service_name="docgen-api",
        aspnetcore_authentication_result=~"success|failure"}[30d])), 0.001)
```

**الهدف التشغيلي:** `≤ 25%` شهريًا (خط أساس مبدئي قابل للتعديل بقرار المالك). **ليس**
ميزانية إتاحة: هذا حارس أمني/تشغيلي، لا يجمّد الإصدارات وحده. **تنبيهات:** تحذير `>25%`
لـ`15m`، وحرج `>50%` لـ`5m` (هجوم تخمين أو انقطاع مزوّد هوية).
**إضافة:** أي `5xx` على `/api/auth/*` يُنبَّه عليه منفصلًا (`DocGenAuthServerErrors`).

### 3.7 `SLO-7` — رفض حدّ المعدل (حارس سعة/أمان)

**`SLI`:** نسبة الطلبات المرفوضة من محدّد المعدل إلى كل محاولات الحصول على تصريح:

```
rate_limit_rejection_ratio =
  sum(rate(aspnetcore_rate_limiting_requests_total{
        service_name="docgen-api",
        aspnetcore_rate_limiting_result=~"endpoint_limiter|global_limiter"}[30d]))
  /
  clamp_min(sum(rate(aspnetcore_rate_limiting_requests_total{
        service_name="docgen-api"}[30d])), 0.001)
```

**الهدف:** `≤ 5%` شهريًا. **تنبيهات:** تحذير `>5%` لـ`15m`، وحرج `>20%` لـ`5m`
(هجوم أو حدّ ضيّق يمنع مستخدمين شرعيين). لا يدخل ميزانية الإتاحة.

## 4. سياسة ميزانية الخطأ

| `SLO` | الهدف | الميزانية/شهر | قرار عند استهلاك 100% |
|---|---|---|---|
| الإتاحة | 99.5% | 0.5% من الطلبات | تجميد الإصدارات غير العاجلة حتى العودة تحت الحد |
| قراءة `p95` | ≤ 500ms | 5% طلبات قراءة بطيئة | مراجعة أداء إلزامية قبل أي ميزة جديدة |
| كتابة `p95` | ≤ 1000ms | 5% طلبات كتابة بطيئة | مراجعة أداء/فهارس إلزامية |
| `Outbox` | 99.5% | 0.5% من الفحوص | إصلاح تشغيلي عاجل، لا تجميد كامل |
| مجمّع القاعدة | 99% ≤ 80% | 1% من الدقائق | (غير مقيس حاليًا) |
| المصادقة/حدّ المعدل | حراس | لا ميزانية | تحقيق أمني/سعة، لا تجميد تلقائي |

**معدلات الاستهلاك (Burn Rate) وقراراتها:**

| المعدل | النافذة الطويلة | النافذة القصيرة | `for` | الإجراء |
|---|---|---|---|---|
| `14.4x` | `1h` | `5m` | `2m` | صفحة فورية (critical)، فتح حادث، تجميد تغييرات غير عاجلة |
| `6x` | `6h` | `30m` | `15m` | تذكرة (warning)، تحقيق خلال يوم عمل |
| `1x` | `3d` | `6h` | `1h` | بند في قائمة العمل (info)، مراجعة أسبوعية |

- **تعريف:** `burn_rate = error_ratio / (1 - SLO)`. لحد `99.5%`، `error_ratio` المسموح
  `0.005`؛ `14.4x` ⇒ `7.2%` أخطاء في الساعة.
- **الاستثناءات:** لا تُستهلك الميزانية بسبب صيانة معلنة مسبقًا فقط إذا عُطّل الفحص/السحب
  خلالها وسُجّل الاستثناء في الحادث؛ لا يوجد وسم `maintenance` آلي اليوم.
- **إعادة الضبط:** أول الشهر (`UTC`)؛ النوافذ المتدحرجة للتنبيهات لا تُعاد ضبطها.
- **الصلاحية:** تجاوز التجميد بقرار المالك الموثّق في الحادث، بمدة محدودة.

## 5. التضمين والاستبعاد (ملخص تنفيذي)

| البند | القرار |
|---|---|
| المسارات المحتسبة | `http_route=~"/api/.*"` فقط |
| مسارات الصحة/المقاييس/الملفات الثابتة | مستبعدة |
| `4xx` | جيدة للإتاحة؛ `401/403/429` لها حراس منفصلة |
| `5xx` | سيئة للإتاحة |
| طلب بلا رمز حالة | مستبعد من المقامين |
| الطلبات غير المطابقة لمسار | مستبعدة (لا `http.route`) |
| تعدد النسخ | مجموع كل السلاسل (`sum`) |
| القياس المتأخر/المفقود | لا يُعوَّض بتخمين؛ تنبيه `DocGenTelemetryAbsent` |
| عيّنة التتبّع | `10%` للنجاح، `100%` للأخطاء/البطيء؛ **لا تؤثر على مقاييس `HTTP`**؛ زمن `SQL` من مسار غير معاين |

## 6. حدود ومعايير القبول

- **لا ادعاء نشر حي:** هذا التعريف + الملفات جاهزة للاستيراد، لكن `Prometheus`/`Grafana`/
  `Tempo`/`blackbox_exporter` ليست منشورة في هذا المستودع؛ النشر مسؤولية المالك/البنية
  التحتية (`P5-04` مستقبلًا).
- **لا أسرار:** كل نقاط النهاية/الاعتمادات عبر `${env:...}` في الجامع، ولا كلمات مرور في
  الملفات.
- **لا `PII`:** الجامع يحذف ترويسات المصادقة/الكوكيز و`url.query`/`url.path` ونص `SQL`
  (`db.query.text`/`db.statement`) وقيم المعاملات؛ راجع `otel-collector.yaml` §`attributes/redact`.
- **التحقق الآلي:** `node scripts/observability-config-guard.mjs` يفحص صلاحية `YAML`/`JSON`،
  ووجود كل تنبيه في الدليل، وروابط `runbook`، واتساق أسماء المقاييس المطلوبة في هذا الملف.
- **المراجعات:** `docs/audit/258` (التنفيذ)، `259` (النقدية)، `260` (العدائية والملحق).
