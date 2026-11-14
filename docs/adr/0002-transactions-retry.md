# ADR-0002 — إستراتيجية معاملات واعية وإعادة محاولة بنطاق جديد

- **الحالة:** معتمد (`P0-03`).
- **التاريخ:** 2026-11-14.
- **السياق:** `docs/PERFECTION_ROADMAP.md` §4 `P0-03`، و`docs/adr/0001-postgres-only.md`.
- **يعالج:** `F-02` — `SaveChanges` الداخلي وإعادة المحاولة، ونطاق DI لكل محاولة.

## 1. السياق

`TransactionRunner` يمنع إعادة تنفيذ الـdelegate كاملًا (`at-most-once`)، لكن
`SaveChangesAsync` الداخلي يستخدم إستراتيجية السياق العامة. عند تفعيل
`EnableRetryOnFailure` قد يعيد EF تنفيذ دفعة كتابة **داخل معاملة تفاعلية**، وقد
تبقى كيانات/تغييرات المحاولة الفاشلة في `DbContext` المشترك في نطاق الطلب.
إعادة المحاولة الآمنة تتطلب:

1. ألا يعيد EF `SaveChanges` داخل معاملة نشطة.
2. نطاق DI/سياق EF جديدًا لكل محاولة، مع إعادة تحميل الكيانات داخل الـdelegate.

## 2. القرار

### 2.1 `TransactionAwareExecutionStrategyFactory` (`P0-03a`)

- تُستبدل `IExecutionStrategyFactory` في مزوّد EF عبر
  `options.ReplaceService<IExecutionStrategyFactory, TransactionAwareExecutionStrategyFactory>()`.
- تُرجع إستراتيجية مغلّفة تختار **عند التنفيذ**:
  - `NonRetryingExecutionStrategy` إذا كان `Database.CurrentTransaction` نشطًا.
  - الإستراتيجية المعيدة (عند `Database:RetryMaxCount > 0`) خارج المعاملة.
- **لا تُفحص المعاملة داخل `Create()`**: قراءة `Database.CurrentTransaction` هناك
  تعيد حلّ `DatabaseFacadeDependencies` الذي يطلب المصنع نفسه (دورة حلّ لانهائية).
- الافتراضي `RetryMaxCount = 0` (معطّل)؛ التفعيل قرار تشغيلي لاحق.

### 2.2 `IUnitOfWorkFactory` ونطاق جديد لكل محاولة (`P0-03b`)

- `UnitOfWorkFactory.ExecuteAsync(action, retryable, ct)`:
  - ينشئ **نطاق DI جديدًا** لكل محاولة، ويحلّ `IUnitOfWork` و`DocGeneratorDbContext`
    منه، ثم يفتح معاملة ويُثبّتها أو يتراجع عنها.
  - عند `retryable=true` تُنشأ الإستراتيجية من نطاق منفصل، ويُعاد تنفيذ العملية
    بنطاق جديد عند الفشل العابر.
  - الـdelegate يتلقى `IUnitOfWork` الجديد، ويجب أن يعيد تحميل الكيانات منه لا من
    نطاق الطلب.
- `ITransactionRunner.RunAsync(Func<IUnitOfWork,...>, retryable, ct)` هو المسار
  الصريح للعمليات القابلة لإعادة المحاولة.
- المسار القديم `RunAsync(Func<CancellationToken,...>)` يبقى **at-most-once**
  وغير معيد، للعمليات غير idempotent أو التي تعتمد على كيانات محمّلة مسبقًا.
- `HeadAlertOutboxStore` يبقى at-most-once أيضًا (إستراتيجية غير معيدة صريحة).

## 3. الأثر

- لا تغيير سلوكي عند `RetryMaxCount=0`: كل شيء غير معيد كما كان.
- عند التفعيل لاحقًا: `SaveChanges` داخل معاملة لا يُعاد؛ العمليات المعلنة
  `retryable` فقط تُعاد، وكل محاولة في نطاق/معاملة جديدة.
- لا تكرار كتابة عند فشل عابر محقون بعد `SaveChanges` (اختبار `UnitOfWorkFactoryTests`).
- العمليات القديمة لا تتحول تلقائيًا إلى retryable؛ الانتقال تدريجي وواعٍ.

## 4. الاختبارات

- `TransactionAwareExecutionStrategyTests`: الإستراتيجية المعيدة تُستخدم خارج
  المعاملة، ولا تُستخدم داخلها (الاختيار عند التنفيذ على SQLite).
- `TransactionAwareExecutionStrategyFactoryTests`: التهيئة الافتراضية غير معيدة.
- `UnitOfWorkFactoryTests`: فشل عابر بعد `SaveChanges` مرتين لا يترك صفوفًا مكررة،
  والمحاولة الثالثة وحدها تُثبَّت؛ والمسار غير القابل للإعادة لا يعيد.
- `TransactionAwareExecutionStrategyPostgresTests` (CI): إنشاء الإستراتيجية على
  PostgreSQL داخل/خارج معاملة لا يرمي ولا يدخل دورة حلّ.

## 5. التراجع

- إعادة `RetryMaxCount=0` تعيد السلوك الآمن فورًا (لا حاجة لتغيير كود).
- إزالة استبدال `IExecutionStrategyFactory` تعيد إستراتيجية EF الافتراضية؛
  لكنها لا تُفعّل مع `retryable` في `UnitOfWorkFactory`.
- لا تغيير مخطط/بيانات في هذا القرار.
