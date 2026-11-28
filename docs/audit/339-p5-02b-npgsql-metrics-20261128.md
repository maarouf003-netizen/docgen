# `P5-02b` — تسجيل متر `Npgsql` لـ`SLO-5` — 2026-11-28

> **لقطة مؤرخة** — تُجمَّد بعد الدمج. **الأساس:** `master f9f190b0` (بعد `W8a`).
> **الفرع:** `perf/p5-02b-npgsql-metrics`. **المرجع:** `docs/PERFECTION_ROADMAP.md` §`P5-02b`،
> `docs/SLO.md` §3.5، ومهارة `independent-review`.

## 1. التغيير

- `ObservabilitySetup.WithMetrics` صار يسجّل متر `Npgsql` عبر ثابت `NpgsqlMetricMeter`
  و`.AddMeter(MetricMeters)` — يغذّي `db.client.connection.count/max/timeouts` لـ`SLO-5`.
- لا تغيير في التفعيل: عند غياب `Observability:OtlpEndpoint` لا يُسجَّل شيء (اختبار قائم).
- تصحيح تسمية الوسم في التوثيق والتنبيهات واللوحة: `state` ← `db_client_connection_state`
  (وسم Npgsql الأصلي `db.client.connection.state` يُطبَّع في Prometheus بشرطة سفلية).
- تحديث المواضع التي كانت تعلن «المتر غير مسجَّل»: `docs/SLO.md`، `ops/observability/alerts.yaml`،
  `ops/observability/dashboard.json`، `docs/runbooks/observability.md`، `docs/PERFECTION_ROADMAP.md`.

**بلا هجرة، بلا تغيير عقد/إعداد/حزم.**

## 2. المراجعة المستقلة (الجولة الأولى)

- **`BLOCK`:** (1) الاختبار الأول كان جوفًا (يفحص ثابتًا نصيًا؛ حذف `.AddMeter` لا يُفشله)،
  (2) ادعاء «`SLO-5` قابل للقياس» غير محقّق لأن استعلامات `state` لا تطابق وسم Npgsql
  بعد تطبيعه في Prometheus.
- **الإصلاح:** استُبدل الاختبار باختبار اشتراك فعلي (`Meter` + `UpDownCounter.Enabled`)،
  وصُحّح الوسم في `SLO.md`/`alerts.yaml`/`dashboard.json`، وحُدّثت الوثائق المتقادمة.
- **تحقق المراجع:** `AddMeter("Npgsql")` مطابق لتطبيق `Npgsql.OpenTelemetry`، والبناء بلا تحذير،
  و`observability-config-guard` أخضر. أُعيد الاختبار بعد الإصلاح.

## 3. الطفرة

| الطفرة | الاختبار | قبل | بعد |
|---|---|---|---|
| حذف `.AddMeter(MetricMeters)` | `Configured_SubscribesToNpgsqlMeter_ForPoolMetrics` | مرّ | **فشل** (`Instrument.Enabled=false`) |

أُعيد السطر فورًا، والاختبار أخضر (9/9 Observability).

## 4. البوابات (محليًا)

- `dotnet build -warnaserror` 0 تحذير، `Observability` **9/9**،
  `observability-config-guard --self-test` + الحارس نظيفان.
- `npm`/الواجهة بلا تغيير؛ `CI` يشغّل الـ11.

## 5. المتبقي

- ظهور `db_client_connection_*` في `Prometheus` بعد نشر المجمّع/`staging` (خطوة بيئة، لا كود).
- `Outbox` بالدقائق والإعادات اختياري عبر `postgres_exporter` (كما هو معلن).
