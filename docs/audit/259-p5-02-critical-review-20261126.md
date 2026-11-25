# `P5-02` — المراجعة النقدية — 2026-11-26

> **ملحق مستقل** بعد [`258`](258-p5-02-slo-alerts-20261126.md): يفحص وجود أسماء المقاييس
> فعلًا، واتساق العتبات مع `SLO`، وصلاحية `YAML`/`JSON`، وخلوّ الملفات من `PII`/الأسرار،
> وعدم وجود أي تغيير في وقت التشغيل. لا يُحرَّر `258` بعد التجميد؛ التصحيحات هنا وفي
> الملفات الحية.

## 1. جدول الحكم

| # | البند | ما فُحص فعلًا | الحكم |
|---|---|---|---|
| 1 | **أسماء مقاييس `AspNetCore`** | قراءة `README` حزمة `OpenTelemetry.Instrumentation.AspNetCore 1.21.0` (تؤكد `http.server.request.duration` وأن `net10.0` يفعّل المقاييس المدمجة) + وثائق `learn.microsoft.com` لمقاييس `HTTP`/`Security` | **سليم** |
| 2 | **`aspnetcore.rate_limiting.requests` وقيم النتيجة** | وثائق `ASP.NET Core` + `OpenTelemetry` semconv: القيم `acquired`/`endpoint_limiter`/`global_limiter`/`request_canceled` | **سليم** |
| 3 | **`aspnetcore.authentication.*`** | وثائق `ASP.NET Core 10` لمقاييس المصادقة (`authenticate.duration` بنتيجة `success/failure/none`، `challenges`، `forbids`) | **سليم** |
| 4 | **مقاييس `Runtime`** | وثائق `.NET` المدمجة: `dotnet.process.cpu.time`، `dotnet.process.memory.working_set`، `dotnet.thread_pool.*`… **لا** أسماء `process.runtime.dotnet.*` القديمة | **سليم** |
| 5 | **مقاييس `Npgsql`** | وثائق `Npgsql` 10: `db.client.connection.count` (`state=idle/used`)، `db.client.connection.max`، `db.client.connection.npgsql.timeouts` | **سليم كأسماء، لكن غير مُصدرة حاليًا** (المتر غير مسجّل) |
| 6 | **زمن `SQL` المشتق** | `spanmetricsconnector` README: `calls`/`duration` مع `namespace`، `unit: s`، `include_instrumentation_scope`؛ ينتج `docgen.db.duration` → `docgen_db_duration_seconds_bucket` | **سليم (مشتق، موثّق)** |
| 7 | **أسماء `Prometheus` المطبّعة** | `translation_strategy: UnderscoreEscapingWithSuffixes`: النقاط → `_`، `s` → `_seconds`، `By` → `_bytes`، `_total` للعدّادات؛ الدليل يوثّق الجدول كاملًا | **سليم** |
| 8 | **وجود مقاييس التنبيهات** | كل تعبير يستخدم مقياسًا من جدول `docs/SLO.md` §2، عدا `probe_success` (مُولَّد من `blackbox`) و`docgen_outbox_*` (اختياري موثّق) | **سليم** |
| 9 | **اتساق عتبات الإتاحة** | `99.5%` → ميزانية `0.5%`؛ `14.4x` = `7.2%` (`1h`)، `6x` = `3%` (`6h`)، `1x` = `0.5%` (`3d`)؛ الحساب مطابق | **سليم** |
| 10 | **اتساق عتبات الزمن** | `p95 ≤ 500ms` للقراءة و`≤ 1s` للكتابة؛ تنبيهات `30m/5m` و`2x`؛ لا خلط بين `p95` والمتوسط | **سليم** |
| 11 | **`Outbox`** | `P5-03` عتبة `15m`؛ فحص `503` = حرج، جسم `Degraded` = تحذير؛ لا ادعاء عدّاد دقائق أصلي | **سليم** |
| 12 | **صلاحية `YAML`/`JSON`** | `yaml.safe_load` للملفين + `JSON.parse` للوحة (والحارس يعيد الفحص في `CI`) | **سليم** |
| 13 | **مراجع مكوّنات الجامع** | كل `receiver`/`processor`/`exporter`/`connector` مذكور في مسار موجود في قسمه؛ لا مرجع dangling | **سليم** |
| 14 | **`PII`** | حذف `Authorization`/`Cookie`/`Set-Cookie`/`url.query`/`url.path`/`http.target`/نص `SQL`/المعاملات؛ لا أجسام/كلمات مرور | **سليم** |
| 15 | **الأسرار** | لا عنوان/رمز literal؛ `TEMPO_OTLP_ENDPOINT` من `${env:...}`؛ الحارس يرفض الأنماط الحساسة | **سليم** |
| 16 | **لا تغيير وقت تشغيل** | لم يُعدَّل أي ملف `.cs`/`.csproj`/`appsettings`؛ لا حزم؛ `has-pending-model-changes` نظيف | **سليم** |
| 17 | **لا هجرة** | لا ملف في `MigrationsPostgres`؛ لا تغيير كيان | **سليم** |
| 18 | **تغطية التنبيهات في الدليل** | الحارس يتحقق أن **كل** اسم تنبيه مذكور في `docs/runbooks/observability.md`، وكل مرساة `#slo-*` موجودة | **سليم** |

## 2. ثغرات وُجدت أثناء المراجعة وأُصلحت

| # | الثغرة | الأثر لو بقيت | الإصلاح |
|---|---|---|---|
| 1 | **`increase()` على مقياس إعادات غير رتيب** (مجموع `Attempts` ينقص عند معالجة الرسائل) | تنبيهات كاذبة/سالبة عند تغيّر المجموع | أُعيدت التسمية إلى `docgen_outbox_retry_attempts` (gauge) واستُخدم `delta()` في التنبيه واللوحة |
| 2 | **فلترة فحوص الصحة بـ`url.path` ثم حذفه في التنقيح** | فقدان الفلترة بعد الحذف (ضجيج فحوص في التتبّع/زمن `SQL`) | الفلترة تستخدم `http.route` (لا يُحذف)، وأُضيف حذف `url.path` لحماية `PII` |
| 3 | **`batch` مشترك بين ثلاثة مسارات** | مشاركة حالة تجميع بين مسارات مختلفة (سلوك غير محدد) | `batch/spanmetrics` + `batch/traces` + `batch/metrics` مستقلة |
| 4 | **حارس الاختبار الذاتي يترك أخطاءه المقصودة** | فشل `--self-test` رغم نجاح الفحوص | حفظ/استعادة طول `failures` بعد كل حالة سلبية |
| 5 | **`replace` مرة واحدة في اختبار انقطاع الدليل** | اختبار ذاتي يمرّ كاذبًا لوجود ذكر ثانٍ للتنبيه | `replaceAll` لكل ذكر |
| 6 | **عناوين الدليل مرقّمة (`## 11. SLO: …`)** | مراسي `#slo-*` غير مطابقة، وروابط `runbook_url` مكسورة | إزالة الترقيم من عناوين `SLO:` |
| 7 | **جدول الدليل يجمع أسماء التنبيهات (`Warning / Critical`)** | الحارس لا يجد الاسم الدقيق؛ تغطية ناقصة | صفوف صريحة لكل اسم |
| 8 | **لوحة `RAM` تخلط `bytes` وعدّ عددي في محور واحد** | قراءة مضللة | أُزيل هدف `thread-pool` من اللوحة؛ أُبقيت `RAM` بوحدة `bytes` |
| 9 | **اسم `docgen_db_duration_seconds_bucket` غير مذكور حرفيًا** | حارس الاتساق يفشل/توثيق ناقص | أُضيف الاسم الكامل في جدول التحويل |
| 10 | **`span.name` بعدّاد `span_metrics`** | تعددية عالية/تسريب نص استعلام | استُبعد `span.name` صراحةً، ونص `SQL` يُحذف قبل الاشتقاق |

## 3. التحقق من الحساب (أمثلة)

- `SLO-1`: `0.5%` ميزانية؛ `14.4 × 0.005 = 0.072` → تنبيه عند `>7.2%` في `1h` و`5m`.
- `SLO-1` بطيء: `6 × 0.005 = 0.03` → `>3%` في `6h` و`30m`.
- `SLO-1` بند: `1 × 0.005 = 0.005` → `>0.5%` في `3d` و`6h`.
- `SLO-2`: `p95 ≤ 0.5s`؛ تنبيه حرج عند `>1.0s` (ضعف الهدف) — ليس «ميزانية 1%».
- `SLO-4`: `0.5%` من فحوص `30s` ≈ `3.6h/شهر`؛ الفحص `503` يعني تأخرًا `>15m`.
- `SLO-5`: `used/max > 0.8` تحذير و`>0.95` حرج؛ لا رقم `20` مثبت في الاستعلام.

## 4. البوابات (على الفرع)

| البوابة | النتيجة |
|---|---|
| `node scripts/observability-config-guard.mjs --self-test` | **OK** — 3 مسارات، 24 قاعدة، 13 لوحة |
| `python yaml.safe_load` + `JSON.parse` | **OK** |
| بناء `-warnaserror` | **0/0** |
| `dotnet format --verify-no-changes --no-restore` | `exit 0` |
| `Architecture` / `Application` | **41/41** · **778 passed** (1661 skipped) |
| `Api` غير تسلسلي / تسلسلي | **916 passed** (615 skipped) · 0/5 skipped محليًا |
| الحراس الستة + حارس الرصد | نظيفة |
| تدقيق الاعتماديات + `npm-audit-guard` | 0 High/Critical · 0 ثغرة |
| `ef has-pending-model-changes` | «No changes…» |
| الواجهة | **1809/1809** في 171 ملفًا، `build`/`guard`/`generate:api` نظيفة (غير معدّلة) |

## 5. الحكم

`258` صالح للدمج: أسماء المقاييس إمّا مُصدرة فعليًا في `P5-01` أو مشتقة/اختيارية موثّقة،
والعتبات متسقة مع `SLO`، والملفات صالحة نحويًا، ولا `PII`/أسرار، ولا تغيير في وقت التشغيل
أو هجرة. الفجوة الوحيدة المؤثرة هي `SLO-5` (متر `Npgsql`) وهي **معلنة** بقواعد نائمة لا
تطلق إنذارات كاذبة.
