# `P1-08/8a-4b` — بقية مجموعات التزامن على `PostgreSQL` + علّتا إنتاج مكتشفتان — 2026-11-20

> نطاق هذه الجولة: نقل بقية ملفات التزامن في `Application.Tests` من `SQLite` إلى
> `PostgreSQL` (‏`ADR-0009`) — **43 حالة** في 7 أصناف — مع إصلاح علّتين إنتاجيتين
> حقيقيتين كشفهما النقل. لا هجرة جديدة.

## 1. المشكلة المقيسة

- الملفات السبعة كانت تبني ملف/قاعدة `SQLite` مؤقتة واتصالات متعددة لتحاكي سباقًا.
  `SQLite` يُسلسل الكتابات ذاتيًا داخل العملية، فيمرّ السباق عبر «الحارس» قبل الكتابة
  ولا يصل إلى مسار التعارض الحقيقي (`23505`/`WHERE Version`) إلا نادرًا.
- أثر ذلك ليس أكاديميًا فقط: صار `Postgres` المزوّد الوحيد تشغيليًا (`ADR-0001`)،
  ودلالة `READ COMMITTED` تختلف جوهريًا عن تسلسل `SQLite` — فأي اختبار «خضراء»
  على `SQLite` لا يثبت شيئًا عن الإنتاج.

## 2. ما نُقل (43 حالة)

| الصنف | الحالات | ما يقيسه على `Postgres` |
|---|---|---|
| `DelegationConcurrencyTests` | 4 | فائز واحد على الأصل نفسه + ترجمة الفهرس الفريد إلى خطأ مجال، توكن نسخة صف الإنابة، ثلاثية عدّاد الإضافة المحلية، **وصيانة الحجوزات** |
| `AppealTransferAllConcurrencyTests` | 2 | مجموع المنقول = المنظور، والخاسر تعارض تزامن ودي حصرًا |
| `CorrespondenceConcurrencyTests` | 1 | 16 تسطيرًا متزامنًا على فرع واحد: أرقام مميزة + تدقيق واحد لكل نجاح |
| `HeadAlertSystemWriteTests` | 12 | نطاق مفتاح منع التكرار، خطة الاستبدال، التعاقب، حارس المعرّف المستثنى |
| `HeadAlertOutboxDispatcherTests` | 22 | الالتقاط (`FOR UPDATE SKIP LOCKED` + قفل استشاري)، التراجع، سياسة التعارض، التنظيف |
| `DocumentInterventionLimitStaleTrackingTests` | 1 | العدّ الطازج داخل القفل بعد حذفٍ من طلب آخر |
| `LoginRateLimiterTests.ConcurrentFailures_NeverExceedMax` | 1 | 12 محاولة دخول متزامنة لا تتجاوز الحد |

التحويل البنيوي: `PostgresTestDb` معزولة لكل حالة (‏`IAsyncLifetime`)، وسياقات
مستقلة عبر `_pg.Options` بدل اتصالات `SQLite`، وحذف `EnsureCreated` (القالب يوفّر
المخطط)، وكل الحالات `[PostgresFact]` (تخطٍّ ظاهر محليًا وفشل صريح في `CI`).
 كذلك صار لكل جولة سباق **سياقان جديدان** (كل طلب في الإنتاج نطاق `DI` مستقل):
الخاسر بعد ترجمة الاصطدام يبقى متتبَّعًا للكيان الفاشل، فلا يُعاد استعمال سياقه.

## 3. العلّتان الإنتاجيتان (وأصلحهما)

### (أ) المواءمة الذاتية للحجوزات تمحو حجز إنابةٍ التزمت للتوّ

`PruneStaleReservationsAsync` كانت تقرأ «الإنابات الحاجبة» من لقطة `source` سابقة،
ثم تقرأ الحجوزات من القاعدة وتحذف كل ما ليس في اللقطة. تحت `READ COMMITTED`:
طلبٌ B بدأ قبل التزام إنابة A (فلقطته لا تعرفها)، لكن قراءته للحجوزات تأتي **بعد**
التزام A فترى حجزه ⇒ تحذفه، ثم تُدرج حجزها هي؛ فيبقى للأصل إنابتان إحداهما بلا حجز،
وهو ما كان `SQLite` يمنعه بتسلسله. الأثر الإنتاجي: أصلٌ محجوز يصبح قابلاً لتسطير
متزامن لاحق (فجوة في حارس الفهرس الفريد).

**الإصلاح (بلا تكرار سياسة):** صار الحذف جملة `DELETE` واحدة تحكم على حالة الإنابة
**وقت التنفيذ** عبر `EXISTS` على `DocumentDelegations`، ومصدر الدلالة واحد:
`DelegationActivityPolicy.IsAssetBlockingExpression` (شجرة تعبير يستعملها `EF` في
الاستعلام، وتُترجم مرة واحدة للذاكرة في `IsAssetBlocking`). لم يعد هناك «لقطة أقدم
من الحجوزات»، وأي إنابة التزمت قبل بدء الجملة تبقى حاجزة.

### (ب) تجاوز حد محاولات الدخول على `Postgres`

`INSERT … SELECT … WHERE (COUNT(*) …) < Max` جملة واحدة، لكنها ليست ذرّية على
`Postgres`: تحت `READ COMMITTED` يأخذ الإدراج لقطةً لكل جملة، فمحاولتان متزامنتان
تقرآن `4` وتُدرجان معًا ⇒ `6` صفوف بحدِّ `5`. أُثبت عمليًا: **فشل 1 من 8** تشغيلات
للاختبار المنقول (كان يمرّ على `SQLite` لتسلسل كتابته).

**الإصلاح:** على `Postgres` يُسلسل الإدراج بمفتاحٍ واحد عبر قفل استشاري معاملاتي
(`pg_advisory_xact_lock(hashtextextended(@key, 0))`) داخل معاملة قصيرة (أو المعاملة
المحيطة إن وُجدت)؛ المفاتيح المختلفة لا تتزاحم، والقفل يُحرَّر تلقائيًا بانتهاء
المعاملة. `10/10` تشغيلات بعد الإصلاح.

## 4. التحقق

- **`Application.Tests` على `PostgreSQL`** (‏`DOCGEN_TEST_POSTGRES` + `DOCGEN_REQUIRE_POSTGRES=1`):
  **2489/2489**، 0 متخطّاة، 0 فاشلة (13m 5s). منها **43 حالة** منقولة في هذه الجولة
  (‏22 `HeadAlertOutboxDispatcher`، 12 `HeadAlertSystemWrite`، 4 `DelegationConcurrency`،
  2 `AppealTransferAll`، 1 `Correspondence`، 1 `Intervention`، 1 `LoginRateLimiter`).
- **`Application.Tests` على `SQLite`** (بلا المتغيّر): **2405 ناجحة، 69 متخطّاة صراحةً،
  0 فاشلة** — الحالات المنقولة تُتخطّى ظاهرًا ولا تفشل، ومسار `DeleteStaleAsync`
  يُترجم ويُشغَّل على `SQLite` أيضًا (ومن هنا مرّ `Reservations_FollowDelegationLifecycle`
  و`DuplicateLabels_OneToOneBlocking_KeepsHealthyTwin`).
- **`Api.Tests` على `PostgreSQL`:** **1546/1546** (0 متخطّاة) + **5/5** لـ`Category=Sequential`،
  و`postgres-suite-guard` على `api-postgres.trx`: كل المجموعات نُفِّذت بلا تخطٍّ.
- **`Api.Tests` على `SQLite`:** **1107 ناجحة + 412 متخطّاة** + **5/5** `Sequential`.
- **`Architecture.Tests`:** **29/29**.
- **ثبات النتيجة للسباقين الحاسمين:** `DelegationConcurrencyTests` **4/4** في ثلاث
  تشغيلات متتالية؛ و`ConcurrentFailures_NeverExceedMax` **10/10** بعد إصلاح القفل
  (قبله **7/8** — الفشل هو نفسه الذي كشف العلّة).
- `dotnet build -warnaserror`: 0/0 · `dotnet format --verify-no-changes`: نظيف.
- **الحراس:** `subhead` · `determinism` · `migration-watchlist` (5 هجرات) ·
  `migration-safety` · `secrets` (20 مطابقة خط أساس) · `postgres-suite-guard --self-test`
  (5 حالات) · `npm-audit-guard` (0 ثغرة) + `--self-test` · `dependency-audit --self-test`.
- **الواجهة** (`npm ci --legacy-peer-deps`: 0 ثغرات): `oxlint src scripts` 0 ملاحظة ·
  `tsc -b` نظيف · `vitest run` **1808/1808** في **171** ملفًا · `npm run build` ناجح ·
  `npm run generate:api` بلا تغيير في `generated.ts` · `npm run guard` (`CSP`):
  **94** ملفًا نظيفًا.
- لا هجرة جديدة؛ القائمة المعلّقة تبقى **4**.

## 5. الحدود

- التالي: `8a/5` (`TestDb` المركزي ~80 ملفًا) ثم `8b` (‏`Api.Tests`/`ApiFactory`)
  ثم `8c` (حذف حزم `SQLite` وتنظيف الأقفال و`CI`).
- **لا هجرة جديدة** في هذه الجولة؛ لم يُمسّ مخطط `PostgreSQL` ولا `EnsureCreated`
  (كهرباء التشغيل).
