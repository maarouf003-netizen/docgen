# Runbook — اختبار الحمل وSLO (`P3-07`)

> **متى:** قبل كل إصدار كبير، وبعد أي تغيير في استعلامات القوائم/الإحصاءات/الفهارس.
> **الحكم:** `scripts/slo-check.mjs` على مخرجات منصة القياس (`P3-01`/`P3-02`) — لا أرقام يدوية.

## 1. التحضير

```powershell
$env:DOCGEN_BENCH_POSTGRES="Host=127.0.0.1;Port=55432;Database=postgres;Username=postgres"
```

- استخدم قاعدة **بذر اصطناعي حتمي**؛ لا بيانات إنتاج (سياسة `docs/audit/measurements/README.md`).
- شغّل بناء `Release` فقط (زمن `Debug` غير معتمد).

## 2. القياس

```powershell
dotnet run -c Release --project backend/tools/DocGenerator.Benchmarks -- `
  --scale 50000 --iterations 20 --warmup 5 --task P3-02 `
  --connection $env:DOCGEN_BENCH_POSTGRES --output "$env:TEMP\slo-run"
```

- تحقق من **زمن البذر** (مرجع `50k`: ~136–149s على بيئة خاملة). إن تضاعف، البيئة تحت حمل:
  **لا تعتمد الأرقام**، أعد التشغيل.
- تحقق أن الاستعلامات غير الممسوسة قريبة من خط الأساس قبل الحكم على تغيير محدّد.

## 3. الحكم (SLO)

```powershell
node scripts/slo-check.mjs "$env:TEMP\slo-run\20261121-p3-02-50000.json"
```

| المقياس | أهم السقوف (`p95`) |
|---|---|
| `10k` | `documents-list-page1` ≤250ms · `documents-list-text` ≤600ms · `portal-list-page1` ≤250ms |
| `50k` | `documents-list-page1` ≤500ms · `documents-list-text` ≤1600ms · `portal-list-page1` ≤500ms · `portal-list-text` ≤1000ms · إحصاءات ≤500ms |

- أي `FAIL` أو استعلام غائب = فشل صريح؛ لا يُخفَّض السقف بلا قرار موثّق في `docs/audit/`.
- **A/B عادل:** عند تغيير استعلام، شغّل الكود القديم والجديد على **نفس البصمة/البيئة**
  (كما في `docs/audit/94`)، لا مقارنة تشغيلين تحت حملين مختلفين.

## 4. التسجيل

- احفظ المخرجات في `%TEMP%` (تشغيل تحقّق) أو أضف خط أساس جديدًا في
  `docs/audit/measurements/` بقرار مبرّر (سياسة الاحتفاظ).
- اذكر في تقرير الإصدار: المقياس، البصمة، `p95` لكل استعلام، وأي انحراف.

## 5. مراجع

- منصة القياس: `docs/audit/81-p3-01-measurement-harness-20261121.md`.
- خط الأساس: `docs/audit/measurements/20261121-p3-0{1,2}-*.json`.
- سياسة الاحتفاظ: `docs/audit/measurements/README.md`.
