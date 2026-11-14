# تسليم جلسة — إنجاز `P0-01` → `P0-05` (2026-11-14)

هذا مستند تسليم لمحادثة جديدة. يلخّص ما أُنجز، وما بقي، والفجوات المعروفة،
وأوامر التحقق. لا يحتوي أي أسرار أو سلاسل اتصال.

## 1. حالة الفرع والدمج

- الفرع الحي: `master` عند `af47958` (بعد دمج `P0-04`).
- `P0-01` — PR `#140` → `6d1bc3f`: PostgreSQL هو المزوّد الوحيد في التشغيل.
- `P0-02` — PR `#141` → `274228b`: Baseline واحد `20261114101737_InitialPostgresBaseline`،
  حذف 73 هجرة Postgres و97 SQLite، وانتقال اختبارات SQLite إلى `EnsureCreated`.
  نسخة `pg_dump` موثّقة في `docs/audit/17-postgres-baseline-20261114.md`
  (خارج المستودع)؛ القاعدة التجريبية أُعيد ضبطها وبقيت **بلا مستخدمين/بيانات أعمال**.
- `P0-03` — PR `#142` → `f8aba87`: `TransactionAwareExecutionStrategyFactory` +
  `IUnitOfWorkFactory` (نطاق DI جديد لكل محاولة)، `Database:RetryMaxCount=0` افتراضيًا،
  و`ADR-0002`.
- `P0-04` — PR `#143` → `af47958`: `IdempotencyMiddleware` عامة لكل كتابة مصادقة،
  هجرة `20261114140029_AddIdempotencyResponseMetadata`، و`ADR-0003`.
- `P0-05` — مُغلق بقرار `ADR-0004` (`docs/adr/0004-outbox-scope.md`): الـOutbox القائم
  (`HeadAlertOutbox`) هو الآلية المعتمدة؛ لا جدول عام حتى يظهر منتج ثانٍ.

## 2. نتائج التحقق الأخيرة (قبل هذا التسليم)

- `dotnet build backend/DocGenerator.sln -warnaserror`: 0/0.
- `Api` (parallel): 899، `Api` (sequential): 5.
- `Application`: 2392.
- الواجهة: `oxlint` + `tsc -b` + `vitest` 1803 + `build` + `guard` خضراء.
- `CI` على PR #143: البوابات الست خضراء، بما فيها `backend-postgres`
  (`IdempotencyStorePostgresTests` و`PostgresBaselineTests`).

## 3. الفجوات المعروفة التي تستحق الإصلاح

1. **`P0-03`: اختبار حقن الفشل عند `commit`** والمسار الكامل عبر
   `ITransactionRunner.RunAsync(..., retryable: true)` مع DI غير مغطّى؛ المغطّى حاليًا
   مصنع النطاق واختبار بعد `SaveChanges`.
2. **`P0-04`: تغطية اختيارية** — بلا `X-Idempotency-Key` لا إزالة تكرار؛ الواجهة
   تضيفه تلقائيًا لكل استدعاء، لكن نقرتين مزدوجتين في نماذج غير حساسة قد تولّدان
   مفتاحين مختلفين. النماذج الحرجة تمرر مفتاحًا ثابتًا.
3. **`P0-04`: عدم اتساق رمز الإعادة** — العمليات الست المستثناة تعيد `200` للإنشاء
   (عقد `RF-011`)، والعمليات المغطاة عامةً تعيد `201`. التوحيد يتطلب تغيير عقد مُختبَر.
4. **`P0-04`: حدود التخزين** — لا تُخزَّن استجابات > `1MiB` أو ثنائية، ولا تُعاد
   ترويسات مثل `Set-Cookie/ETag`؛ لا أثر حاليًا لأن الكتابات JSON وصغيرة.
5. **`P0-04`: TTL 72 ساعة** (قرار `BQ-032`) والتنظيف انتهازي بلا مهمة خلفية.
6. **`P0-04`: fail-open** عند تعذّر مخزن المفاتيح (توافر على حساب منع التكرار).
7. **`P0-05`: outbox lag** لم يُربط بـ`/readyz`؛ مؤجّل إلى `P5-03` بقرار `ADR-0004`.
8. **`P1-08`**: `SQLite` لا يزال في الاختبارات فقط، وبقايا `datetime2` في تكوين
   `IdempotencyKeyConfiguration` يصحّحها سياق Postgres.
9. **`P7-05`**: تحذيرات `CI` من `actions/*@v4` (Node 20) و`ubuntu-latest` → Ubuntu 26.

## 4. النشر (مهم)

- **هجرة معلّقة واحدة:** `20261114140029_AddIdempotencyResponseMetadata`
  **لم تُطبّق على أي بيئة بعد**.
- قبل نشر نسخة `P0-04`:
  ```powershell
  dotnet ef database update --context DocGeneratorPostgresDbContext
  ```
  ثم تحقق من `__EFMigrationsHistory` (صفان) ومن أعمدة
  `IdempotencyKeys.StatusCode/ContentType/ResponseLocation`.
- لا تُشغّل `--context DocGeneratorDbContext` (لا هجرات له).
- `Database:AutoMigrate=false` في الإنتاج ⇒ الإقلاع يُرفض إن لم تُطبّق.
- القائمة التراكمية في `RUN_GUIDE.md` §9 و`AGENTS.md` محدّثة.

## 5. الخطوات التالية المقترحة

1. `P0-06` — تزامن تفاؤلي على كل كيان قابل للتعديل (`Version`/`xmin` + `409`).
2. `P0-07` — تدقيق قيود القاعدة (unique/check/FK/partial indexes).
3. `P0-08` — تدقيق غير قابل للتعديل + تنقيح `PII` + صلاحيات DB للقراءة فقط.
4. بالتوازي: `P1-01` (منصة Postgres حقيقية/Respawn) و`P1-02` (حتمية `TimeProvider`)
   ثم `P1-08` لإزالة SQLite نهائيًا.
5. `P7-05` كتغيير صغير مستقل لتنظيف تحذيرات CI.

## 6. أوامر التحقق القياسية

```powershell
dotnet build backend/DocGenerator.sln --nologo -v q -warnaserror
dotnet test backend/tests/DocGenerator.Api.Tests/DocGenerator.Api.Tests.csproj --nologo -v q --filter "Category!=Sequential"
dotnet test backend/tests/DocGenerator.Api.Tests/DocGenerator.Api.Tests.csproj --nologo -v q --filter "Category=Sequential"
dotnet test backend/tests/DocGenerator.Application.Tests/DocGenerator.Application.Tests.csproj --nologo -v q
```

```powershell
cd frontend
npx oxlint src scripts
npx tsc -b
npx vitest run
npm run build
npm run guard
```

- `backend-postgres` في CI هو المرجع لاختبارات Postgres الحقيقية؛ خادم pg المحلي
  ليس موثوقًا بعد تلف عنقود سابق.
- اختبارات Postgres المحلية تتطلب `DOCGEN_TEST_POSTGRES`، وتُتخطّى بدونه
  (وفي CI مع `DOCGEN_REQUIRE_POSTGRES=1` تفشل صراحةً عند غيابه).

## 7. قواعد الجلسة التالية

- كل بند خريطة في PR مستقل، ولا دمج قبل خضرة البوابات الست.
- لا دفع مباشر إلى `master`.
- لا أسرار في الكود/السجلات/المحادثة. `001.txt` لا يُقرأ/يُنشر إلا بطلب صريح.
- أي هجرة جديدة تُضاف إلى `RUN_GUIDE.md` §9 و`AGENTS.md` في نفس التغيير.
- بعد كل بند: مراجعة نقدية قبل الانتقال للبند التالي.
