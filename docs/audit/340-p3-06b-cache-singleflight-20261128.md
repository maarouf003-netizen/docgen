# `P3-06b` — single-flight وعدّادات كاش البيانات المرجعية — 2026-11-28

> **لقطة مؤرخة** — تُجمَّد بعد الدمج. **الأساس:** `master ed048073` (بعد `P5-02b`).
> **الفرع:** `perf/p3-06b-cache-singleflight`.

## 1. التغيير

- `ReferenceDataCache`: بوابة `SemaphoreSlim` لكل `cacheKey` (مع فحص مزدوج بعدها)
  تمنع تكرار المصنع عند التزامن البارد، وتُزال البوابة بعد الاستخدام (بلا تسريب).
- `ReferenceDataDiagnostics`: متر `DocGenerator.ReferenceData` وعدّادا
  `docgen.refdata.cache.hits/misses` (بلا مفاتيح/قيم — لا PII)، مسجَّلان في
  `ObservabilitySetup.MetricMeters` بجانب `Npgsql`.
- لا تغيير في `IReferenceDataCache` أو TTL/الإبطال أو سلوك القيم؛ لا هجرة.

## 2. الاختبارات والطفرة

- `GetOrCreate_ConcurrentCalls_ExecuteFactoryOnce`: 10 طلبات متزامنة ⇒ المصنع **مرة**،
  وكلها تتشارك القيمة نفسها.
- `Metrics_CountOneMissThenHits`: `MeterListener` يثبت miss واحدة ثم hit واحدة.
- `Configured_SubscribesToNpgsqlMeter_ForPoolMetrics` (موسَّع): عدّاد على متر الكاش
  `Enabled=true` بعد الاشتراك.

| الطفرة | الاختبار | قبل | بعد |
|---|---|---|---|
| إلغاء البوابة المشتركة (بوابة جديدة لكل نداء) | `GetOrCreate_ConcurrentCalls…` | مرّ | **فشل** |
| بوابة عالمية واحدة لكل المفاتيح | `GetOrCreate_DoesNotBlockOtherKeys` | مرّ | **فشل** (مهلة 5s) |
| حذف `_locks.TryRemove` | `GetOrCreate_ConcurrentCalls…` (`InflightCount`) | مرّ | **فشل** |

## 2.1 الجولة الثانية (مراجع مستقل)

- `APPROVE WITH FOLLOW-UP` مع شروط إغلاق قبل الدمج: عزل اختبار المتر (مجموعة بلا توازٍ)،
  تغطية تفرد المفتاح، تغطية إزالة البوابة، وتوثيق ازدواج المصنع بعد الفشل.
- نُفّذ: `[Collection("ReferenceDataMetrics", DisableParallelization = true)]`،
  اختبار `DoesNotBlockOtherKeys` (يفشل تحت بوابة عالمية)، و`InflightCount` داخلي
  (`InternalsVisibleTo` للاختبارات) يُثبت عودة البوابات إلى الصفر، وتوثيق الحدّ في §4.
- الطفرتان `F` (بوابة عالمية) و`C` (حذف الإزالة) قُتلتا فعليًا بعد الإضافة.

أُعيد التغيير فورًا؛ الاختبارات خضراء (Application 4/4 للكاش، Observability 9/9).

## 3. البوابات (محليًا)

- `dotnet build -warnaserror` 0 تحذير/0 خطأ.
- `ReferenceDataCacheTests` 4/4، `Observability` 9/9، `observability-config-guard` نظيف.
- `CI` يشغّل الـ11 (بما فيها `backend-postgres`).

## 4. المتبقي والحدود المعلنة

- **دلالة العدّادات:** `hits` تشمل «انتظر البوابة ثم وجد القيمة»؛ `misses` تُحتسب مرة
  واحدة لكل تحميل ناجح بعد الفحص المزدوج. عند **فشل المصنع** يُحرَّر المنتظرون ويعيد
  كلٌّ منهم المحاولة (قد يتكرر المصنع بعد فشل) — تكرار أداء لا خطأ بيانات، معلن هنا؛
  الانتقال إلى `Lazy<Task<T>>` المشترك بلا هذا القيد مؤجل.
- **عزل الاختبار:** `ReferenceDataMetrics` مجموعة بلا توازٍ لتفادي تلوث عدّادات المتر
  العملية من أصناف أخرى تستخدم الكاش.
- عرض `hits/(hits+misses)` في `dashboard.json` بعد نشر المجمّع (خطوة بيئة).
- عند تعدد النسخ مستقبلًا: كاش موزّع/`single-flight` عبر العقد (خارج نطاق الجولة).
