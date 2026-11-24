# خارطة الوصول إلى المثالية — DocGenerator

> **الحالة:** خطة تنفيذية معتمدة للتنفيذ في محادثة جديدة.
> **التاريخ:** 2026-11-14.
> **الأساس:** `master` عند `457d4ec` (بعد دمج `#135`–`#139`).
> **قاعدة البيانات الفعلية:** `PostgreSQL` فقط (نسخة تجريبية على Render/Supabase، بلا بيانات حقيقية).
> **قاعدة `SQLite`:** موجودة في الكود والاختبارات لكنها **غير مستخدمة في التشغيل**؛ الخطة تعالج إزالتها تدريجيًا.
>
> **تحذير أمني:** ملف `001.txt` على سطح المكتب يحتوي معلومات اتصال. **لا يُقرأ محتواه في المحادثة، ولا يُنسخ إلى المستودع، ولا يُرفع في PR.** إذا كان قد شارك أو التُقط في سجل Git فيجب تدوير بيانات Render/Supabase فورًا. تفاصيل الإدارة في `P2-05`.

---

## 0. كيف تُستخدم هذه الخطة

- كل بند له معرّف ثابت (`P0-01`، `P1-03`…) وأولوية واعتماديات و`DoD` وأوامر تحقق.
- **PR واحد لكل بند** (أو لكل بندين متلازمين بموافقة صريحة)، بلا خلط مع ميزة وظيفية.
- لا يبدأ بند يعتمد على آخر قبل اكتمال اعتماديته.
- البنود التي تغيّر العقد أو المعمارية تحتاج `ADR` في `docs/adr/` قبل الكود.
- لا تُغلق أي مهمة قبل: بناء `-warnaserror`، الاختبارات المحلية، بوابات `CI` الست، وتحديث هذا الملف/`docs/CODE_HEALTH_REVIEW.md`.
- **هذا الملف هو المرجع التنفيذي الوحيد** للرحلة؛ أي خطة قديمة تُعتبر لقطة تاريخية.

---

## 1. الملخص التنفيذي

### 1.1 أخطر الفجوات الحالية

| # | الفجوة | الأثر | الأولوية |
|---|---|---|---|
| 1 | `EnableRetryOnFailure` معطّل، و`SaveChanges` الداخلي يستخدم إستراتيجية السياق العامة | إما لا مرونة عند الفشل العابر، أو تكرار كتابة إذا فُعّل بلا idempotency | `P0` |
| 2 | 72 موضع `SaveChangesAsync` محتمل خارج `_tx.RunAsync` (201 إجمالًا، 129 داخل معاملة) | كتابة جزئية/تأثير جانبي بلا ذرّية | `P0` |
| 3 | 97 هجرة `SQLite` + 73 هجرة `PostgreSQL` (بعد استثناء `Designer`/`Snapshot`)، وقائمة هجرات معلّقة | تعقيد نشر، خطر `no such column`، ونسخ مزدوج من الحقيقة | `P0` |
| 4 | idempotency مطبّق في 3 متحكمات فقط (~12 موضعًا) من 239 نقطة | تكرار مستندات/حالات عند إعادة الإرسال أو الـretry | `P0` |
| 5 | اختبارات التكامل على `SQLite` في الذاكرة بينما الإنتاج `PostgreSQL` | اختلافات مخطط/قيود/خطط استعلام لا تُكتشف | `P1` |
| 6 | لا `OpenTelemetry` ولا metrics/SLOs/تنبيهات؛ الصحة تفحص القاعدة فقط | أعطال صامتة، لا قياس لزمن الاستجابة أو outbox lag | `P5` |
| 7 | صلاحيات موزعة في 22 متحكمًا و60 `[Authorize]` وفحوص أدوار يدوية | ثغرة سلطة محتملة عند إضافة مسار/مورد | `P2` |
| 8 | `DocumentResponse` ضخم (566 سطرًا) و`WithStandardIncludes` ~20 `Include` | تحميل زائد، N+1، صعوبة تغيير | `P3/P4` |
| 9 | بحث البوابة `OR + Contains` ≈ 26.8s على `SQLite/Debug` | خطر أداء إنتاجي غير مقيس على `PostgreSQL` | `P3` |
| 10 | ملفات ضخمة: `DocumentDelegationService` 1904، `Appeal` 1598، `Status` 1312، `DocumentForm.tsx` 1521 | صيانة ومراجعة ومخاطر انحدار | `P4/P6` |
| 11 | لا E2E/axe/visual/bundle budget للواجهة | كسر تجربة الجوال/الوصول لا يُكتشف قبل النشر | `P6` |
| 12 | لا staging/canary/rollback/DR drills؛ Render مجاني ونسخة واحدة | تعطّل كامل بلا مسار تعافٍ مُختبَر | `P5` |
| 13 | لا SAST/DAST/SBOM؛ `.snyk` غير مفعّل في CI | ثغرات اعتماديات/كود لا تُحجب | `P2` |
| 14 | لا OpenAPI مُفعّل ولا عميل TS مولّد؛ `types/document.ts` يدوي 797 سطرًا | انحراف عقد بين الواجهة والخلفية | `P4/P6` |
| 15 | لا `.editorconfig`/CPM/lock files (اختبارات المعمارية أُغلقت في `P1-06`) | انحراف تنسيق/إصدارات | `P4` |

### 1.2 القرارات المعمارية الكبرى (تحتاج موافقة المالك)

1. **`PostgreSQL` فقط في التشغيل**، وإزالة مسار `SQLite` الإنتاجي وهجراته.
2. **سحق الهجرات إلى Baseline واحد** بعد نسخة احتياطية، لأن القاعدة تجريبية بلا بيانات حقيقية.
3. **Command/Query pipeline** لمسارات الكتابة: تحقق ← صلاحية ← idempotency ← معاملة ← تدقيق ← outbox.
4. **سياق/نطاق جديد لكل محاولة** قبل تفعيل أي retry تلقائي.
5. **سياسات صلاحيات موردية** بدل فحوص الأدوار في المتحكمات.
6. **Read models وإسقاطات** لكل شاشة بدل `DocumentResponse` الشامل.
7. **`PostgreSQL` هو بيئة الاختبار التكاملية**؛ `SQLite` للوحدات السريعة فقط ثم يُزال.
8. **`OpenTelemetry` + SLOs** قبل أي تحسين أداء.
9. **بيئة staging + canary + rollback** قبل أي ميزة جديدة.
10. **عقد API مولّد** (OpenAPI → TypeScript) ومنع الأنواع اليدوية المتوازية.

### 1.3 ما لا نفعله

- لا إعادة كتابة شاملة من الصفر.
- لا microservices مبكرًا؛ `modular monolith` أولًا.
- لا تفعيل `EnableRetryOnFailure` بلا idempotency/verify.
- لا 100% coverage كهدف؛ نستهدف الـ`invariants` والمسارات الحدّية.
- لا `SQLite` كبديل للإنتاج في اختبارات السلوك المعتمد على المزوّد.
- لا تخزين أسرار في المستودع أو المحادثة.

---

## 2. خط الأساس الحالي (Baseline)

| المقياس | القيمة |
|---|---|
| الحل | 4 مشاريع: `Domain` / `Application` / `Infrastructure` / `Api` |
| الكيانات | 51 كيانًا في `DocGenerator.Domain/Entities` |
| المتحكمات/النقاط | 22 متحكمًا، 239 نقطة `[Http*]` |
| `[Authorize]` | 60 سمة |
| `SaveChangesAsync` | 201 موضعًا؛ 129 `_tx.RunAsync` |
| الهجرات | 97 ملف `SQLite` و73 `PostgreSQL` (بما فيها Designer/Snapshot) |
| اختبارات | 2384 `Application` (على `master`) + 931 `Api`، وتزيد مع PRs الجارية |
| زمن الاختبارات | `Application` ≈ 13 دقيقة محليًا؛ `Api` ≈ 13 دقيقة محليًا |
| أكبر الخدمات | `DocumentDelegationService` 1904، `DocumentAppealService` 1598، `DocumentStatusService` 1312، `ExecutionCircuitService` 1264، `DocumentRepository` 1232، `DocumentService` 1103 |
| أكبر ملفات الواجهة | `DocumentForm.tsx` 1521، `DocumentsList.tsx` 965، `ApplicantSideSections.tsx` 952، `DocumentView.tsx` 947 |
| `Include` | `DelegationRepository` 46، `PublicEntityRepository` 35، `DocumentRepository` 28، `Repository<T>` 27 |
| الرصد | `Serilog` فقط؛ لا `OpenTelemetry`، لا metrics، لا tracing |
| الصحة | `/healthz` يفحص القاعدة فقط |
| الواجهة | اختبارات RTL فقط؛ لا Playwright/axe/visual/coverage |
| CI | 6 بوابات؛ `backend` 40 دقيقة؛ `Application` 13 دقيقة |
| البيانات | `PostgreSQL` تجريبية على Render/Supabase، بلا بيانات حقيقية |

### 2.1 نقاط القوة القائمة

- فصل طبقات واضح، و`TreatWarningsAsErrors`.
- تغطية اختبارات كبيرة (≈3.3k) وست بوابات CI.
- `IdempotencyStore`، `outbox` للتنبيهات، `concurrency tokens`، تدقيق، وحراس هجرات.
- `Program.cs` به حراسات إقلاع أمنية (JWT، بيئة التطوير، SQLite في الإنتاج، HSTS/CSP).
- `CsrfMiddleware`، `SecurityHeadersMiddleware`، rate limiting، وForwardedHeaders مضبوط.
- Dockerfile يعمل بمستخدم غير جذر.

---

## 3. المبادئ غير القابلة للتفاوض

1. **الصحة قبل الميزات:** أي كتابة غير ذرّية أو غير idempotent = عيب `P0`.
2. **القاعدة هي الحارس الأخير:** القيود والفهارس والـFK في `PostgreSQL`، لا الثقة بالخدمة فقط.
3. **لا retry بلا idempotency:** retry أعمى = تكرار بيانات.
4. **الاختبار الحتمي:** لا `GetHashCode`/`DateTime.Now`/`Random` غير محقون في الاختبارات.
5. **`PostgreSQL` هو الحقيقة:** أي سلوك يعتمد على المزوّد يُختبر عليه.
6. **الأمان افتراضيًا:** رفض الإقلاع إذا نقص إعداد أمني في الإنتاج.
7. **الرصد قبل التحسين:** لا تحسين بلا قياس.
8. **تغييرات صغيرة وقابلة للتراجع:** `expand/contract`، feature flags، canary.
9. **عقد واحد:** OpenAPI/TypeScript مولّد، لا نسخ يدوية.
10. **التوثيق قرار:** `ADR` لكل قرار معماري، و`runbook` لكل حادثة محتملة.

---

## 4. خارطة العمل

### P0 — سلامة البيانات والمعاملات (حاجب)

#### `P0-01` — `PostgreSQL` فقط في التشغيل

- **الحالة (2026-11-14):** منفّذ — `ADR-0001` (`docs/adr/0001-postgres-only.md`)، وإزالة تسجيل
  `UseSqlite` و`AllowSqliteInProduction` و`Database:UsePostgres` من التشغيل. حذف هجرات
  `SQLite` وتوحيد النموذج على صياغة `PostgreSQL` مؤجّلان إلى `P0-02` لتلازمهما مع سحق الهجرات
  وإعادة ضبط القاعدة.
- **المشكلة:** مسار `SQLite` الإنتاجي و`Database:AllowSqliteInProduction` وهجرات مزدوجة.
- **العمل:**
  - حذف فرع `UseSqlite` من `DependencyInjection` و`Program.cs` و`DatabaseInitializer`.
  - حذف `Database:AllowSqliteInProduction` ورفض أي إقلاع غير `PostgreSQL` خارج Development.
  - إزالة `ConfigureHeadAlertProviderSpecific` وSQL الخاص بـ`SQLite` من `DocGeneratorDbContext`؛ استخدم صيغة `PostgreSQL` فقط.
  - إبقاء `SQLite` مؤقتًا في مشروع اختبارات الوحدات فقط، ثم إزالته (`P1-08`).
- **الملفات:** `Program.cs`، `DependencyInjection.cs`، `DocGeneratorDbContext.cs`، `DatabaseInitializer.cs`، `appsettings.json`، `render.yaml`، `RUN_GUIDE.md`.
- **الاختبارات:** إقلاع Production بلا Postgres → يفشل؛ اختبارات الوحدات لا تتأثر.
- **DoD:** لا ذكر لـ`UseSqlite` في `src`؛ لا هجرات SQLite (تُنجز في `P0-02` مع سحق الهجرات)؛ بناء/اختبارات خضراء.
- **الاعتمادية:** `P0-02`.

#### `P0-02` — سحق الهجرات إلى Baseline واحد

- **الحالة (2026-11-14):** منفّذ — `20261114101737_InitialPostgresBaseline` واحد،
  بعد نسخة `pg_dump` موثّقة في `docs/audit/17-postgres-baseline-20261114.md`؛ حُذفت
  هجرات `PostgreSQL` و`SQLite` القديمة، وانتقلت اختبارات SQLite إلى `EnsureCreated`.
  أُعيد ضبط القاعدة التجريبية وطُبّق عليها الـBaseline (51 جدولًا، صف هجرة واحد،
  4 مشغّلات، قيدان)، ونجح دخان الإقلاع ضدها.
- **المشكلة:** 97 + 73 هجرة، وقائمة معلّقة، وتعقيد نشر.
- **العمل:**
  - أخذ نسخة منطقية (`pg_dump`) وتوثيق أعداد الجداول.
  - حذف هجرات `PostgreSQL` القديمة، وتوليد `InitialPostgresBaseline` من النموذج الحالي.
  - حذف هجرات `SQLite` نهائيًا (الاختبارات تستخدم `EnsureCreated` مؤقتًا).
  - إعادة ضبط القاعدة التجريبية (`__EFMigrationsHistory` + schema) وتطبيق Baseline.
  - توثيق سياسة `expand/contract` لكل تغيير مستقبلي، وتحديث `RUN_GUIDE.md` §9 وقائمة المعلّق.
- **الاختبارات:** قاعدة فارغة + `database update` + إقلاع + اختبار دخان شامل.
- **DoD:** Baseline واحد لكل سياق (Postgres فقط)؛ `has-pending-model-changes` نظيف؛ لا هجرات معلّقة.
- **الاعتمادية:** `P0-01` (و`P1-01` للتحقق الآلي بعد توفره، لا كحاجز بدء).

#### `P0-03` — إستراتيجية تنفيذ واعية بالمعاملة + نطاق جديد لكل محاولة

- **الحالة (2026-11-14):** منفّذ — `ADR-0002` (`docs/adr/0002-transactions-retry.md`):
  `TransactionAwareExecutionStrategyFactory` تختار غير المعيدة عند وجود
  `CurrentTransaction`، و`IUnitOfWorkFactory` ينشئ نطاق DI/سياقًا جديدًا لكل محاولة.
  `Database:RetryMaxCount` معطّل افتراضيًا (`0`)؛ التفعيل لاحقًا آمن للعمليات المعلنة
  `retryable` فقط. المسار القديم يبقى at-most-once.
- **المشكلة:** `TransactionRunner` يمنع إعادة الـdelegate، لكن `SaveChanges` الداخلي يستخدم إستراتيجية السياق العامة؛ `EnableRetryOnFailure` قد يعيد دفعة كتابة.
- **العمل (على مرحلتين):**
  - `P0-03a`: `TransactionAwareExecutionStrategyFactory` في `Infrastructure` يختار `NonRetryingExecutionStrategy` عند وجود `CurrentTransaction`، والإستراتيجية المعيدة خارجه.
  - `P0-03b`: مسار جديد `IUnitOfWorkFactory`/`IDbContextFactory` لإنشاء نطاق DI جديد لكل محاولة، مع إعادة تحميل الكيانات داخل الـdelegate.
  - تحديث `ITransactionRunner` بعقد صريح: هل العملية قابلة لإعادة المحاولة؟ وما مصدر الكيانات؟
  - اختبار حقن `NpgsqlException` عابر في: البداية، `SaveChanges`، والـcommit.
- **الملفات:** `TransactionRunner.cs`، `HeadAlertOutboxStore.cs`، `IUnitOfWork.cs`، `DependencyInjection.cs`، `CommandBehavior` الجديد.
- **DoD:** لا تكرار/فقدان أثر تحت فشل عابر محقون؛ `EnableRetryOnFailure` يمكن تفعيله لاحقًا بأمان.
- **الاعتمادية:** `P0-01` و`P0-02`؛ ولا يعتمد على `P0-04`.

#### `P0-04` — idempotency لكل عملية كتابة

- **الحالة (2026-11-14):** منفّذ — `IdempotencyMiddleware` عامة تغطي كل
  `POST/PUT/PATCH/DELETE` مصادَق يحمل `X-Idempotency-Key`، وتعيد الحالة والجسم
  و`Location` الأصليين عند التكرار (`ADR-0003`). الإجراءات ذات الحجز الخدمي
  القائم (`documents.create`, `transfer-all`, `circuits.refer-files`,
  `registry.import-commit/unify/merge-commit`) معلّمة `[SkipIdempotency]`
  حفاظًا على عقدها (إعادة `200` للإنشاء)، والدخول/الخروج/التليمترية مستثناة.
  الواجهة تضيف المفتاح تلقائيًا لكل طلب كتابة.
- **المشكلة:** 3 متحكمات فقط تستخدم `Idempotency-Key`.
- **العمل:**
  - `IdempotencyFilter`/middleware لكل `POST/PUT/PATCH/DELETE` (عدا `login`/`logout` إن لزم).
  - مفتاح = `userId + method + route + hash(body)`؛ تخزين الاستجابة والحالة؛ `409` عند تعارض؛ `replay` عند التكرار.
  - TTL 24 ساعة + مهمة تنظيف؛ دعم `Idempotency-Key` من الواجهة في كل طلب كتابة.
  - توسيع `IdempotencyKeys` (أو جدول جديد) بحقول `RequestHash`، `StatusCode`، `ResponseBody`، `ExpiresAt`.
- **الاختبارات:** إعادة الطلب بنفس المفتاح = نفس الاستجابة؛ طلبان متزامنان = واحد ينفّذ؛ مفتاح مختلف = تنفيذ جديد.
- **DoD:** كل نقطة كتابة مغطاة، واختبارات تكامل على Postgres.
- **الاعتمادية:** `P0-03`.

#### `P0-05` — Outbox عام لكل أثر جانبي

- **الحالة (2026-11-14):** مغلق بقرار `ADR-0004` (`docs/adr/0004-outbox-scope.md`):
  `HeadAlertOutbox` القائم هو الـOutbox المعتمد (enqueue داخل المعاملة، lease،
  retries/NextAttemptAt/LastError، مشغّل خلفي، صحة واختبارات). لا جدول عام جديد
  حتى يظهر منتج ثانٍ مستقل؛ أي أثر جديد يمرّ عبره أو عبر `adapter`، وربط lag
  بـ`/readyz` ضمن `P5-03`.
- **المشكلة:** `HeadAlertOutbox` خاص بالتنبيهات فقط؛ بعض الآثار الجانبية داخل الخدمات.
- **العمل:**
  - `OutboxMessage` عام (`Type`, `Payload`, `Status`, `Attempts`, `NextAttemptAt`, `LockedUntil`, `LastError`).
  - `IOutboxHandler` + registry؛ `OutboxDispatcher` يعالج بالـtype.
  - ترحيل تنبيهات الرأس الحالية إلى العام تدريجيًا، أو الإبقاء مع `adapter`.
  - قياس `outbox lag` في `/readyz` وmetrics.
- **الاختبارات:** enqueue داخل معاملة، crash بعد commit، retry، idempotent handler.
- **DoD:** لا أثر جانبي خارج المعاملة؛ معالجة at-least-once مع idempotency.
- **الاعتمادية:** `P0-04`.

#### `P0-06` — تزامن وتفاؤل على كل كيان قابل للتعديل

- **المشكلة:** `Version` على `Document` فقط (يُتحقق).
- **العمل:** جرد الكيانات القابلة للتعديل (`User`, `PublicEntity`, `Delegation`, `Appeal`, `Correspondence`, `ExecutionCircuit`, `Section`, `ReviewLetter`, `PersonalReminder`…) وإضافة `Version`/`xmin` وسلوك `409`.
- **الاختبارات:** تحديث قديم = `409`؛ دمج متزامن لا يضيع تحديثًا.
- **DoD:** لا lost updates على أي aggregate.
- **الاعتمادية:** `P0-02`.

#### `P0-07` — تدقيق قيود القاعدة

- **العمل:** مراجعة كل `IEntityTypeConfiguration`: unique/check/FK/partial indexes للحذف المنطقي؛ إضافة قيود للنطاقات (`amount >= 0`، `year`، `status`)، وفهارس `(BranchId, SectionId)` و`(RegistryId)`.
- **الاختبارات:** إدخال خام يخالف القيد → يفشل؛ هجرة تولّد القيود.
- **DoD:** `invariants` الحرجة مفروضة في القاعدة، لا في الخدمة فقط.

#### `P0-08` — تدقيق غير قابل للتعديل + تنقيح السجلات

- **العمل:** منع تحديث/حذف `AuditLog` من التطبيق؛ صلاحيات DB للقراءة فقط؛ إخفاء الهويات الوطنية/العناوين/الأسرار من السجلات؛ سياسة احتفاظ.
- **DoD:** لا PII في stdout/الملفات؛ سجل تدقيق غير قابل للعبث من التطبيق.

---

### P1 — الاختبارات والحتمية

#### `P1-01` — منصة اختبار `PostgreSQL` حقيقية

- **الحالة (2026-11-19):** **جزئيًا — الشرائح `1a`–`1h` و`1i/a`–`1i/c` منفذة.**
  - `ApiFactory` صار قابلًا للتوسّع (`ConfigureDatabaseSettings`/`ConfigureDatabaseServices`)
    مع بقاء `SQLite` افتراضيًا لكل الاختبارات القائمة.
  - `PostgresApiFactory`: قاعدة فريدة لكل مصنع، هجرات `EF` الحقيقية
    (`Database:AutoMigrate=true` لا `EnsureCreated`)، وبذر التطوير نفسه.
  - `Respawn 7.0.0`: `ResetAsync` يفرّغ الجداول مع إبقاء `__EFMigrationsHistory`
    ويعيد البذر؛ اختبار حارس يثبت المحو وإعادة البذر وبقاء سجل الهجرات.
  - **المجموعات المنقولة:** `AuthIntegrationTests` (17) + حارس
    `PostgresApiFactoryConfigurationTests` (2) عبر `IClassFixture`؛ و
    `SectionsIntegrationTests` (20) + `BranchManagementIntegrationTests` (11) +
    `DocumentsIntegrationTests` (105) + `DelegationsIntegrationTests` (15) +
    `DelegationScopeMatrixTests` (1) + `DelegationScopeIsolationTests` (2) +
    `AppealForwardFlowTests` (3) + `AppealScopeMatrixTests` (1) +
    `DocumentInterventionIntegrationTests` (5) + `DocumentsExportIntegrationTests` (8) +
    `ExecutionCircuitsIntegrationTests` (16) + `CorrespondenceIntegrationTests` (16) +
    `ReviewCorrespondenceScopeTests` (5) + `UserManagementIntegrationTests` (34) +
    `LockoutIntegrationTests` (8) + `TokenRevocationIntegrationTests` (2) +
    `AlertsIntegrationTests` (35) + `AlertsCursorPagingTests` (13) +
    `AlertsUnreadScopeIntegrationTests` (3) + `EntityRegistryHeadScopeIntegrationTests` (5) +
    `PortalScopeMatrixTests` (1) + `AuditIntegrationTests` (6) + `StatisticsIntegrationTests` (5) +
    `ManagerStatsIntegrationTests` (13) + `StatisticsHeadScopeIntegrationTests` (6) +
    `StatsScopeTests` (5) + `ForbiddenShapeContractTests` (6) + `RF001ConcurrentStatusCharacterizationTests` (2) +
    `RF001DuplicateRequestCharacterizationTests` (2) + `RF001NumberingCharacterizationTests` (2) +
    `RF003AuthzProofTests` (5) + `RF004AuthzTests` (12) + `RF005SecurityLoggingTests` (3) +
    `RF007AuditScopeTests` (2) + `RF008SessionLifetimeTests` (1) + `RF009NumberingTests` (2) +
    `RF010ConcurrencyTests` (6) + `RF011IdempotencyTests` (5) + `RF017SilentWriteAuditTests` (5) +
    `RF018ReadAuditTests` (2) + `RF019AppendOnlyTests` (5)
    عبر `PostgresApiTestCollection` المشتركة/مصانع مستقلة. كلها في وظيفة
    `backend-postgres`، مع حارس `trx` يمنع تخطيها الصامت.
  - **حارس `trx` مولَّد لا يدوي (‏`1i/c`):** `scripts/postgres-suite-guard.mjs`
    يكتشف المجموعات من المصدر — خاصية المجموعة المشتركة، أو `IClassFixture<PostgresApiFactory>`،
    أو تجميعة معرَّفة بمصنع `PostgresApiFactory`، أو صنف ينشئ المصنع بنفسه — ثم يتحقق
    من `trx` أن كل مجموعة نُفِّذت بلا تخطٍّ ولا فشل، **وأنه لا `NotExecuted` خارج
    المجموعات المكتشفة** (شبكة أمان للاختبارات المستقلة). له اختبار ذاتي في وظيفة `guards`،
    **وأُلغيت القائمة اليدوية** (‏28 اسمًا سابقًا) لئلا تدخل مجموعة جديدة بلا حارس.
  - **متانة تنظيف قواعد الاختبار:** `PostgresTestDatabase.DropAsync` صار بمهلة `120s`
    لأن `DROP DATABASE ... WITH (FORCE)` يفرض checkpoint قد يتجاوز المهلة الافتراضية
    (‏`30s`) على قرص بطيء — بلا هذا كان اختبار ناجح يُفشل الجولة في التنظيف
    (`docs/audit/72-p1-01ia-entities-portal-20261119.md`).
  - **عقد الإنابة:** عند وجود دوائر، `DelegatedCircuitId` مطلوب صريحًا ولا يُحل
    النص وحده؛ عُدّلت بيانات اختبارات الإنابة، مع إبقاء المسار الخارجي بلا معرف
    (`docs/audit/67-p1-01d-delegations-postgres-20261118.md`).
  - **عيب إنتاجي أُغلق:** رقم ملف بلا دائرة تنفيذ كان يُنتج `500` من قيد القاعدة؛
    صار يُصنَّف في `IDbExceptionClassifier` ويُترجم إلى `400` في المعالج العام، مع
    اختبار وحدة للمعالج واختبار تكامل سلبي على Postgres
    (`docs/audit/66-p1-01c-documents-postgres-20261118.md`).
  - تخطٍّ محلي نظيف عند غياب `DOCGEN_TEST_POSTGRES`، وفشل صريح عند
    `DOCGEN_REQUIRE_POSTGRES=1` (تحقق سلبي موثّق).
  - التفاصيل: `docs/audit/64-p1-01-postgres-harness-20261118.md`،
    `docs/audit/65-p1-01b-admin-suites-20261118.md`،
    `docs/audit/66-p1-01c-documents-postgres-20261118.md`،
    `docs/audit/67-p1-01d-delegations-postgres-20261118.md`،
    `docs/audit/68-p1-01e-appeals-postgres-20261118.md`،
    `docs/audit/69-p1-01f-circuits-correspondence-20261118.md`،
    `docs/audit/70-p1-01g-users-sessions-20261118.md`،
    `docs/audit/71-p1-01h-alerts-20261118.md`،
    `docs/audit/72-p1-01ia-entities-portal-20261119.md`،
    `docs/audit/73-p1-01ib-stats-audit-20261119.md`،
    `docs/audit/74-p1-01ic-rf-contracts-20261119.md`.
  - **متبقٍ:** `P1-08` لإزالة `SQLite` من الاختبارات. `CI` الحالي على `postgres:15`.
- **العمل:** `Testcontainers.PostgreSql` محليًا، وخدمة `postgres:16` في CI؛ `Respawn` لإعادة الضبط؛ `WebApplicationFactory` بسلسلة اتصال Postgres؛ تطبيق الهجرات لا `EnsureCreated`.
- **الاختبارات:** نقل اختبارات المستندات/المتدخلين/الإنابة/الاستئناف/الحالة/البوابة/المصادقة.
- **DoD:** الاختبارات الحرجة تعمل على Postgres في CI؛ `SQLite` للوحدات فقط.
- **الاعتمادية:** `P0-01` (بيئة Postgres)؛ ويمكن أن يتوازى مع `P0-02`.

#### `P1-02` — حزمة الحتمية

- **الحالة (2026-11-15):** مغلق — `IEntropySource` محقون ومسجّل، `TimeProvider` في قرارات
  الزمن، ساعة اختبار ثابتة، حارس `CI` بخط أساس صفر، و**20/20 تشغيلًا بلا flake**
  ([run 43694989802](https://github.com/maarouf003-netizen/docgen/actions/runs/43694989802)).
  التفاصيل في `docs/adr/0006-determinism-policy.md`.
- **العمل:** `TimeProvider` في كل خدمة؛ حقن `Guid`/`Random`؛ منع `GetHashCode` في الـfixtures؛ محلل/سكربت CI يرفض `DateTime.Now`/`Random` غير الم seeded في الاختبارات.
- **DoD:** 20 تشغيلًا متتاليًا بلا flake؛ صفر اختبار معطّل.

#### `P1-03` — Fixtures وBuilders

- **العمل:** `DocumentBuilder`، `UserBuilder`، `PublicEntityBuilder`؛ `IClassFixture`/`CollectionFixture`؛ إعادة استخدام قاعدة اختبار واحدة مع `Respawn` بدل `EnsureCreated` لكل اختبار.
- **DoD:** زمن `Application` < 5 دقائق للوحدات، والاختبارات التكاملية منفصلة.

#### `P1-04` — Property-based tests

- **الحالة (2026-11-15):** مغلق — `FsCheck 3.6` مع **1000 حالة لكل خاصية** للدوال السبع:
  `ArabicNameNormalizer` (idempotent/مسافات)، `ActionDateParser`/`FreeDateParser`
  (round-trip والصيغ العربية وعدم الرمي غير المتوقع)، `NumberToWords` (بما فيها
  `long.MinValue` — كشف تكرارًا لا نهائيًا وأُصلح)، `DocumentSearchTextBuilder`
  (الحدود و`surrogate pairs`)، `DocumentInterventionText.Normalize`، و`ApplicantTextBuilder`.
  الاختبارات في `PropertyBasedTests.cs`.
- **العمل:** `FsCheck` لـ`ArabicNameNormalizer`، `ActionDateParser`/`FreeDateParser`، `NumberToWords`، `DocumentSearchTextBuilder`، `DocumentInterventionText`، `ApplicantTextBuilder`.
- **الـinvariants:** تطبيع idempotent، تواريخ round-trip، لا استثناء على مدخلات عشوائية، احترام الحدود.
- **DoD:** 1000 حالة لكل دالة حرجة.

#### `P1-05` — Mutation testing

- **الحالة (2026-11-17):** **متجاوز بقرار نطاق** — التغطية الحالية (وحدات + تكامل + حراس
  مصدرية) تغطي المخاطر العملية، و`Stryker.NET` كلفته عالية بلا أثر إنتاجي مباشر.
  يُعاد النظر فقط إن انخفضت جودة الاختبارات.
- **العمل:** `Stryker.NET` على `Domain` و`Application` للوحدات الحرجة فقط؛ تقرير أسبوعي، ليس بوابة أولية.
- **DoD:** ≥60% mutation score للوحدات الحرجة.

#### `P1-06` — Architecture tests

- **الحالة (2026-11-15):** مغلق — PR
  [`#158`](https://github.com/maarouf003-netizen/docgen/pull/158) (squash `af766f7`): مشروع مستقل
  `backend/tests/DocGenerator.Architecture.Tests`
  على `NetArchTest.Rules 1.3.2`، مضاف إلى `DocGenerator.sln` وإلى وظيفة `backend` في `CI`
  (خطوة حاجبة بعد البناء)، و**11 اختبارًا**:
  - `Application` لا يعتمد على `Infrastructure`/`Api`، ولا على `Microsoft.EntityFrameworkCore`.
  - `Domain` بلا EF Core وبلا `Application`/`Infrastructure`/`Api`.
  - `Infrastructure` لا يعتمد على `Api`.
  - المتحكمات (كل من يرث `ControllerBase` بلا حصر بمساحة أسماء، لأن ملفات المتحكمات قد
    تحمل أكثر من متحكم) لا تعتمد على EF Core ولا على `Infrastructure`؛ واختبار يطابق
    المجموعة المكتشفة بجميع أبناء `ControllerBase` كي لا يصير الاختيار ناقصًا بصمت.
  - لا `raw SQL` (`FromSql*`/`ExecuteSql*`) في مصادر `Application`: فحص نصي يتجاهل
    التعليقات، مع اختبار مضاد يثبت أن المنقّح يميّز الكود من التوثيق.
  - تحكم إيجابي دائم (`ArchitectureRuleEngineTests`) يثبت أن المحرك يرصد اعتمادًا موجودًا
    في جسم دالة فقط وليس في التوقيع — فلا تكون خضرة بقية القواعد كاذبة.
  - **تحقق سلبي يدوي موثّق:** إدخال `ExecuteSql` في `Application` و`DbContext` في متحكم
    أفشل الحارسين وأظهر اسم النوع المخالف، ثم حُذف الملفان وعادت النتيجة خضراء (11/11).
- **العمل:** `NetArchTest.Rules`: `Application` لا يعتمد على `Infrastructure/Api`؛ `Domain` بلا EF؛ المتحكمات بلا `DbContext`؛ لا `raw SQL` في Application.
- **DoD:** بوابة CI تفشل عند كسر الحدود.

#### `P1-07` — تقسيم fast/slow وقياس التغطية

- **الحالة (2026-11-15):** مغلق — PR
  [`#160`](https://github.com/maarouf003-netizen/docgen/pull/160) (squash `1d19efe`)، والتقرير الكامل في
  [`docs/audit/23-p1-07-fast-slow-20261115.md`](audit/23-p1-07-fast-slow-20261115.md):
  - **الاتفاقية:** `[Trait("Category", "Unit")]` صريح على الأصناف التي لا تلمس قاعدة/مضيف،
    وما عداه **تكاملي ضمنيًا** (الغالب في المشروع)؛ `CI` لا يستخدم أي مرشّح فئات، فكل
    التشغيلات تُنفَّذ دائمًا.
  - **الوسم:** 74 صنفًا (54 `Application` + 20 `Api`) وُسمت آليًا بتحليل مصادر موثّق
    (علامات قاعدة/مضيف + مصانع مضيف + إغلاق متعدٍّ للوراثة)، فراجعه 931 اختبارًا وحدويًا.
  - **القياس:** الحلقة السريعة `684 + 247` اختبارًا في ≈8 ثوانٍ (≈30 ثانية جدارية)،
    مقابل ≈19 دقيقة لـ`Application` كاملة.
  - **`CI`:** `--blame-hang --blame-hang-timeout 25m/15m --blame-hang-dump-type none`
    على خطوات الخلفية الطويلة، وتغطية `XPlat Code Coverage` تُرفع كأثر
    `backend-coverage` (إعلامية بلا بوابة).
- **العمل:** `Category=Unit`/`Integration`؛ وحدات <5 دقائق؛ تقرير تغطية (لا بوابة 100%)؛ `--blame-hang` و`--blame-hang-timeout`.
- **DoD:** تغذية راجعة سريعة ونتائج حتمية.

#### `P1-08` — إزالة `SQLite` من الاختبارات

- **الحالة (2026-11-20):** **مكتملة — `8a/1`–`8a/5` و`8b+8c` منفذة؛ لا مسار `SQLite` في `src`/`tests`.**
  - القرار: [ADR-0009](adr/0009-postgres-only-tests.md) — قاعدة اختبار واحدة
    (`PostgreSQL`)، بلا `EF InMemory`، والخطة المرحلية في
    [`P1-08-SQLITE-REMOVAL-PLAN.md`](P1-08-SQLITE-REMOVAL-PLAN.md).
  - **بنية مشتركة:** `backend/tests/DocGenerator.TestSupport` يحوي
    `PostgresTestEnvironment`/`PostgresTestDatabase`/`PostgresFact`/`PostgresTheory`
    و`PostgresTestDb` (قالب واحد لكل تشغيل + استنساخ لكل اختبار)، ويستهلكه
    `Api.Tests` (حُذفت نسخه المحلية) و`Application.Tests`.
  - **`8a/1`:** 4 أصناف (10 حالات) في `Application.Tests` تعمل الآن على
    `PostgreSQL` بدل `SQLite`: المعاملات/إعادة المحاولة/الوحدة/مالك المستند
    (`docs/audit/75-p1-08a1-test-support-20261119.md`).
  - **`8a/2` + قياس `8a/3`:** حارسا المخطط (`SchemaIntegrityTests`) صارا على
    `information_schema`/`pg_indexes` وبـEF بدل SQL خام، و`PortalSearchMeasurementTests`
    على `Postgres` مع `EXPLAIN`؛ وأول قياس حقيقي لبحث البوابة: ~11.8s بارد/~467ms دافئ
    على 10k مع `Seq Scan` — دليل رقمي لـ`P3-02`
    (`docs/audit/76-p1-08a2-schema-search-20261119.md`).
  - **`8a/4a`:** `OptimisticConcurrencyTests` + `TransferConcurrencyTests` (25 حالة)
    على `Postgres` بجلسات سياقات متعددة على قاعدة واحدة
    (`docs/audit/77-p1-08a4a-optimistic-transfer-20261119.md`).
  - **`8a/4b`:** بقية مجموعات التزامن (`Delegation`/`AppealTransferAll`/`Correspondence`/
    `HeadAlertSystemWrite`/`HeadAlertOutboxDispatcher`/`DocumentInterventionLimitStaleTracking`)
    + `SecurityTests.ConcurrentFailures` على `Postgres`. وكشف النقل علّتين إنتاجيتين
    كانت `SQLite` تخفيهما فأُصلحتا: مواءمة الحجوزات الذاتية كانت تمحو حجز إنابةٍ التزمت
    للتوّ تحت `READ COMMITTED` (صار الحكم داخل جملة `DELETE` نفسها)، وتجاوز حد محاولات
    الدخول المتزامنة على `Postgres` (قفل استشاري معاملاتي على المفتاح:
    `pg_advisory_xact_lock`). (`docs/audit/78-p1-08a4b-concurrency-suites-20261120.md`).
  - **`8a/5`:** حُذف `TestDb` المركزي ونُقلت كل أصناف القاعدة إلى `PostgresDbTest`:
    قاعدة معزولة **لكل صنف** + تصفير `DELETE`/`replica` مع إعادة تسلسل (~15ms) بدل
    استنساخ قاعدة لكل اختبار (~2s)، واتصالات السياقات مُجمَّعة. التحويل كشف:
    إفراط أطوال `character varying` كان `SQLite` يتجاهله، ودلالة «قاعدة مستقلة» في
    اختبارين، ومهلة `CREATE DATABASE` تحت التوازي. تدقيق المشغّلات: لا اختبار في
    `Application.Tests` يعتمد على مشغّلات/قيود الهجرة الخام؛ مغطاة في `Api.Tests` عبر
    المهاجر. (`docs/audit/79-p1-08a5-central-test-db-20261120.md`).
  - **مكتشف يحتاج `8c`:** `DocGenerator.Infrastructure` ما زال يعرّف حزم
    `Microsoft.EntityFrameworkCore.Sqlite` و`SQLitePCLRaw.bundle_e_sqlite3` بلا
    أي استخدام كودي — يجب حذفهما مع إعادة توليد الأقفال.
  - **`8b+8c`:** `ApiFactory` صار `Postgres` فقط (قاعدة فريدة + هجرات + `Respawn`)،
    و37 ملف اختبار API مُبوَّب بـ`[PostgresFact]`, ومصانع المضيف السبعة على قاعدة
    `Postgres` مهاجَرة، وحُذف `TestDatabase`/`EnsureCreatedDatabaseInitializer` وحزم
    `Microsoft.EntityFrameworkCore.Sqlite` و`SQLitePCLRaw.bundle_e_sqlite3`، وصار
    `DbExceptionClassifier` على `Postgres` وحده. أُعيدت موازنة اعتماديات EF صراحةً
    (‏`Microsoft.EntityFrameworkCore`/`.Relational` 10.0.10) بعد سقوط الحزمة التي كانت
    ترفعها، وأُعيد تنظيم `CI`: `backend` = بناء/تنسيق/معمارية، و`backend-postgres` =
    كل اختبارات القاعدة على `Postgres`. (`docs/audit/80-p1-08bc-api-postgres-sqlite-removal-20261120.md`).
  - **المتبقي:** لا شيء في `P1-08`؛ التالي في الخريطة `P3-01` ثم `P3-02`.
- **العمل:** بعد `P1-01`، حذف `Microsoft.EntityFrameworkCore.Sqlite`، `TestDb`، وكل `UseSqlite` من الاختبارات.
- **DoD:** لا مرجع لـSQLite في `src` أو `tests`.

---

### P2 — الأمان والصلاحيات

#### `P2-01` — سياسات صلاحيات موردية

- **الحالة (2026-11-17):** **مغلق** — `ADR-0007`
  (`docs/adr/0007-resource-authorization-policies.md`) معتمد، والمراحل الثلاث منفّذة:
  - **`P2-01a`** (PR [`#183`](https://github.com/maarouf003-netizen/docgen/pull/183)):
    بنية متطلب/معالج موردي للمستندات + بوابة قراءة ضيقة + `IDocumentAuthorization`،
    وترحيل `DocumentsController` (42 نقطة) بلا أي `RolePermissions`/`GetRoleEnum`.
    التطبيع المقصود: المفقود ⇒ `404` قبل التقييم، خارج النطاق ⇒ `403`.
    التفاصيل: `docs/audit/41-p2-01a-document-policies-20261116.md`.
  - **`P2-01b`** (PR [`#184`](https://github.com/maarouf003-netizen/docgen/pull/184)):
    جسر القدرات عُمّم (`IUserCapabilities` ← `RolePermissions`)، و`EntityRegistry`
    (30 نقطة/37 موضع) و`Delegates` بلا قرارات أدوار. **قرار موثّق:** لم يُنشأ
    `PortalAccessRequirement`/`EntityScopeRequirement` لأن النطاق بنيوي/خدمي ومختبَر
    (`ADR-0007` §7 ملحق). التفاصيل: `docs/audit/42-p2-01b-portal-entity-20261116.md`.
  - **`P2-01c‑1`** (PR [`#186`](https://github.com/maarouf003-netizen/docgen/pull/186)):
    7 متحكمات إدارة/قراءة، وهوية `IsHead/IsSubHead/IsAdmin`. التفاصيل:
    `docs/audit/44-p2-01c1-admin-controllers-20261117.md`.
  - **`P2-01c‑2`** (PR
    [`#187`](https://github.com/maarouf003-netizen/docgen/pull/187)، 2026-11-17): الستة الأخيرة (`Appeals`, `Correspondences`,
    `Delegations`, `ExecutionCircuits`, `ReviewLetters`, `UserManagement`) + هوية
    `IsEntityManager`. الجسر **35×6 = 210 خلية**، والمصفوفة **42×6 = 252**.
    **قائمة انتظار الحارس أُفرغت** — لا متحكم يحمل `RolePermissions` أو مقارنة دور،
    وأي متحكم جديد بقرار دورّي يفشل فورًا. التفاصيل:
    `docs/audit/45-p2-01c2-workflow-controllers-20261117.md`.
- **العمل:** `DocumentAccessRequirement` (`Owner`, `Branch`, `Section`, `ForwardedAppeal`)، `PortalAccessRequirement`، `EntityScopeRequirement`؛ `IAuthorizationHandler` لكل منها؛ إزالة `Role ==` من المتحكمات.
- **DoD:** كل نقطة تستخدم `[Authorize(Policy=...)]` أو `IAuthorizationService`؛ لا منطق صلاحية في المتحكم — متحقق: متطلبات موردية للمستندات، وجسر قدرات تطبيقي لبقية الفحوص، وصفر قرارات أدوار في المتحكمات.

#### `P2-02` — مصفوفة صلاحيات مُختبَرة

- **الحالة (2026-11-16):** مغلق — PR
  [`#172`](https://github.com/maarouf003-netizen/docgen/pull/172) (squash `cd024b8`)، منفّذ لمورد **المستندات**:
  `AuthorizationMatrixTests` تثبّت 6 فاعلين × (تفاصيل/قائمة/حذف) + حالة `404`:
  المالك/الرئيس/المدير/المشرف = `200` والمستند في القائمة؛ محامٍ آخر (نفس الفرع أو
  فرع آخر) = `403` للتفاصيل والحذف و`200` لقائمة لا تحتويه؛ معرّف غير موجود = `404`.
  العقد مقيس فعليًا أولًا ثم ثُبّت (القياس المؤقت حُذف). التفاصيل:
  `docs/audit/32-p2-02-authz-matrix-20261116.md`.
  - **P2-02c (2026-11-16):** `PortalScopeMatrixTests` تثبّت عزل نطاق المندوبين:
    `my-scope` لمدخل واحد، قوائم مصفّاة لكل جهة، تفاصيل الغير **`404`** (البوابة تُخفي
    الوجود خلافًا لـ`403` الداخلي)، `entryId` أجنبي `403`، مندوب بلا ربط `200/0`،
    محامٍ `403`، زائر `401`. التفاصيل:
    `docs/audit/36-p2-02c-portal-scope-matrix-20261116.md`.
  - **P2-02d (2026-11-16):** `DelegationScopeMatrixTests` — قائمة إنابات المستند:
    المالك/الرئيس/المدير/المشرف `200`، محامٍ آخر (نفس الفرع أو فرعه) `403`؛
    و`pending` للرئيس `200` وغيره `403`. التفاصيل:
    `docs/audit/37-p2-02d-delegation-matrix-20261116.md`.
  - **P2-02e (2026-11-16):** إصلاح دلالة الأخطاء المكتشفة: ملكية الإنابة/المصدر ⇒
    `403` (`UnauthorizedAccessException`)، والإنابة/المصدر المفقود ⇒ `404`
    (`KeyNotFoundException`)، وفحص الملكية قبل حالة الإنابة. حُدّثت التوقعات في
    `DocumentDelegationServiceTests`/`DelegationsIntegrationTests`/`RF003`، وأُضيفت
    خلايا الكتابة إلى المصفوفة. التفاصيل:
    `docs/audit/38-p2-02e-delegation-error-semantics-20261116.md`.
  - **P2-02f (2026-11-16):** `AppealScopeMatrixTests` — مالك/رئيس/مدير/مشرف `200`،
    محامٍ آخر `403` (تفاصيل/قائمة/تعديل/حذف)، مفقود `404`؛ مع **إصلاح نفس ثغرة الدلالة**
    في الاستئناف: `EnsurePendingByCreator`/`EnsureAssignedFollower` ⇒ `403`، والمفقود
    ⇒ `404`. التفاصيل: `docs/audit/39-p2-02f-appeal-matrix-20261116.md`.
  - **P2-02g (2026-11-16):** PR
    [`#181`](https://github.com/maarouf003-netizen/docgen/pull/181) — المصفوفة الذرّية للكتالوج المركزي — `RolePermissionsMatrixTests`
    تغطي **38 قدرة × 6 أدوار = 228 خلية** بجدول حقيقة مكتوب من قرارات المالك لا من الكود،
    مع أربعة حراس: اكتمال الكتالوج (قدرة جديدة بلا صفّ تفشل)، الشكل الموحّد
    `bool(UserRole)`، الأدوار الستة المقررة (دور سابع يفشل)، وثابتا «رئيس الشعبة = رئيس
    القسم» و«المندوب لا يملك إلا سطح البوابة». أُزيلت نسخة محلية مكررة لـ`HasFullAccess`
    من `PublicEntityService.Groups.cs`. تحقق سلبي موثّق بأربع حقن. التفاصيل:
    `docs/audit/40-p2-02g-role-permissions-matrix-20261116.md`.
- **العمل:** `[Theory]` لكل (`role`, `resource`, `owner`, `branch`, `section`, `forwarded`)؛ اختبارات 200/403/404 لكل مسار.
- **DoD:** تغطية 100% لمصفوفة `RolePermissions` ونقاط الموارد — متحقق لهذه الجولة؛
  الإنفاذ الإعلاني لكل نقطة (سياسات موردية) هو `P2-01`.

#### `P2-03` — افتراضات أمنية آمنة في الإنتاج

- **الحالة (2026-11-17):** **منفّذ آليًا — بروفة `staging` متبقية (إجراء مالك).** PR
  [`#193`](https://github.com/maarouf003-netizen/docgen/pull/193) — حارس `SecurityProductionPosture` يرفض إقلاع `Production` (fail-closed) ما لم تُضبط:
  `AllowedHosts` صريح بلا `*`، `Security:HstsEnabled=true`،
  `Security:HttpsRedirectionEnabled=true`، `Security:CspReportOnly=false`،
  `Swagger:Enabled=false`، و**إما** `Security:KnownProxies` صالح (يرفض `0.0.0.0/0`
  والإدخال غير الصالح) **أو** `Security:TlsTerminatedByApp=true`. أُضيفت اختبارات
  سياسة الكوكيز (`AuthCookie`/CSRF: `Secure`/`HttpOnly`/`SameSite=Strict`) واختبار
  أن `Production` لا يُصدر أي ترويسة CORS لـ`localhost`. `render.yaml` و`RUN_GUIDE.md`
  §9 يوثّقان المتغيّرات الإلزامية. التفاصيل: `docs/audit/51-p2-03-production-security-20261117.md`.
  **متبقٍ (لا يُغلق آليًا):** فحص رؤوس `HSTS`/`CSP`/Cookies/CORS على `staging` فعلي،
  وقيمة `AllowedHosts`/`KnownProxies` الحقيقية عند المالك، وقرار `HstsPreload`.
- **العمل:** `HstsEnabled=true`، `HstsPreload` بقرار، `CspReportOnly=false` بعد مراجعة التقارير، `HttpsRedirectionEnabled=true`، `AllowedHosts` صريح، `KnownProxies` مضبوط، CORS إنتاجي صريح (لا localhost)، ومراجعة `CsrfMiddleware`/`SameSite`/`Secure`/`HttpOnly`؛ رفض الإقلاع عند نقص أي منها في Production.
- **DoD:** فحص رؤوس الاستجابة/Cookies/CORS في staging.

#### `P2-04` — تقوية الجلسات والهوية

- **العمل:** JWT قصير (15–30 دقيقة) + refresh token rotation + إبطال؛ MFA TOTP للمدير/المشرف؛ سجل جلسات؛ إبطال عند تغيير الدور/الفرع؛ تدوير مفتاح التوقيع (`Jwt:Secret`) بفترة سماح.
- **DoD:** لا رمز طويل العمر؛ إبطال فوري؛ تدوير مفتاح موثّق؛ اختبارات.

#### `P2-05` — إدارة الأسرار وتدويرها

- **الحالة (2026-11-15):** جزئيًا — PR
  [`#167`](https://github.com/maarouf003-netizen/docgen/pull/167): الجزء الآلي منفّذ: `scripts/secrets-guard.mjs` في
  بوابة `guards` يفحص الملفات المتتبَّعة (مفاتيح/رموز/JWT/سلاسل اتصال/إسناد كلمات
  مرور) بخط أساس موثّق (20 قيدًا كلها عناصر نائبة أو قيم اختبار)، ولا يطبع القيم؛
  و`.gitignore` وُسّع (`.env.*`، `001.txt`، `*.publishsettings`، `id_rsa*`، `.npmrc`…).
  **مراجعة مسار المشرف الأولي:** `DbSeeder` يمنع حسابات افتراضية في الإنتاج، ويشترط
  `Bootstrap__AdminPassword` مع سياسة كلمة المرور، ويطبع كلمة التطوير العشوائية على
  الكونسول المحلي فقط عند البذر. **المتبقي (إجراء مالك):** فحص سجل Git بـ`gitleaks`،
  تدوير أسرار البيئة مرة واحدة وتوثيق التاريخ، وإجبار تغيير كلمة المرور الأولية
  (يحتاج حقلًا/هجرة — بند مستقل). التفاصيل: `docs/audit/27-p2-05-secrets-guard-20261115.md`.
- **العمل:** أسرار Render/Supabase في متغيرات بيئة فقط؛ `gitleaks` في CI/pre-commit؛ فحص سجل Git؛ تدوير أي سر كُشف؛ `.gitignore` لملفات `.env` و`001.txt`؛ مراجعة `Bootstrap__AdminPassword`/`DbSeeder` (كلمة مرور لمرة واحدة، لا طباعة في الإنتاج، وإجبار التغيير).
- **DoD:** لا سر في المستودع/السجلات؛ آخر تدوير موثّق؛ لا كلمة مرور افتراضية صالحة.

#### `P2-06` — SAST/DAST/SBOM

- **الحالة (2026-11-15):** جزئيًا — PR
  [`#168`](https://github.com/maarouf003-netizen/docgen/pull/168): بوابة الاعتماديات وSBOM منفّذتان:
  - `scripts/dependency-audit.mjs` يحوّل `dotnet list package --vulnerable` (كان يُرجع
    `0` دائمًا) إلى بوابة فعلية: `High`/`Critical` ⇒ فشل، وشدّة مجهولة ⇒ fail-closed،
    مع اختبار ذاتي على 5 عيّنات ثابتة.
  - `npm audit --audit-level=high` باقٍ (بوابة حقيقية أصلًا).
  - **`P2-06` (2026-11-17):** PR
    [`#198`](https://github.com/maarouf003-netizen/docgen/pull/198) — بوابة `npm audit` صارت `scripts/npm-audit-guard.mjs`:
    تفشل على أي `High/Critical` غير مستثنى، وتقبل استثناءً **مبرّرًا بتاريخ انتهاء** فقط
    (`scripts/npm-audit-allowlist.json`)، وتفشل إذا انتهى/صار قديمًا، مع `--self-test`.
    السبب: `GHSA-fc5q-39xc-vjpf` (`picomatch` ReDoS عبر سلسلة `Vite`/`Vitest` التطويرية)
    بلا إصدار مُصلَح (أحدث إصدار 4.0.7 داخل المدى)، وإصلاح `npm` المقترح يخفض `vitest`
    لإصدار أقدم. الاستثناء ينتهي 2026-12-17. التفاصيل:
    `docs/audit/56-npm-audit-allowlist-20261117.md`.
    **تحديث (2026-11-18):** لم يعد الاستثناء مطلوبًا — `npm audit` بعد `npm ci` يعيد
    **0 ثغرات**، فحُذف من `scripts/npm-audit-allowlist.json` (حارس الاستثناءات القديمة
    يرفض بقاءه). لم تُحرَّر اللقطة التاريخية `56`.
  - SBOM (CycloneDX 6.3.1 للخلفية، `@cyclonedx/cyclonedx-npm` للواجهة) يُرفع كأثر
    `sbom` في وظيفة `audit` (30 يومًا).
  - **`P2-06` (تكملة، 2026-11-17):** PR
    [`#199`](https://github.com/maarouf003-netizen/docgen/pull/199) — `.github/workflows/security.yml`:
    - **CodeQL** للـ`C#` والـ`JavaScript/TypeScript` (بناء فعلي للخلفية مع
      `--locked-mode`، SARIF إلى Code scanning، أسبوعيًا أيضًا). منذ 2026-11-18:
      `security-extended` + استثناء `obj/bin` (`security.yml` و`codeql-config.yml`)،
      وفُرزت تنبيهات `cs/log-forging` الأولى (14) كإيجابيات كاذبة موثّقة
      (`docs/audit/62-codeql-triage-20261118.md`). وأُصلح تنبيه `js/bad-tag-filter` الحقيقي
      في `csp-guard.mjs` (نهاية `</script …>` بسمات/مسافات) — `docs/audit/63-…`.
    - **`dependency-review`** مُفعّل الآن بعد تفعيل المالك لـGitHub Dependency graph
      (كان معطّلًا بإعداد المستودع): `fail-on-severity: high` على كل `PR`.
    - **`P2-06` (تكملة، 2026-11-17):** PR
      [`#200`](https://github.com/maarouf003-netizen/docgen/pull/200) — **`CodeQL` صار بوابة
      مطلوبة في `master`**: أُضيف السياقان `CodeQL (csharp)` و`CodeQL (javascript-typescript)`
      إلى حماية الفرع مع الستة القائمة (8 سياقات، `strict=false`، `enforce_admins=true`).
      أُزيل `paths-ignore` كي تُنتَج الفحوص على الـPR الوثائقي أيضًا. التفاصيل:
      `docs/audit/58-codeql-branch-protection-20261117.md`.
  **المتبقي (البند الكامل):** `Semgrep` (يتطلب ضبط قواعد/توكن)، `DAST` (يحتاج `staging`)،
  `Trivy` للصورة (يحتاج بناء صورة في CI)، وبوابة `Snyk`/OSS Index إن رغب المالك
  بـ`SNYK_TOKEN`. التفاصيل: `docs/audit/28-p2-06-dependency-gate-20261115.md` و
  `docs/audit/57-p2-06-codeql-20261117.md`.
- **العمل:** CodeQL/Semgrep، Snyk/OSS Index، `dotnet list package --vulnerable`، `npm audit`، Trivy للصورة، CycloneDX SBOM؛ بوابات حاجبة للحرج.
- **DoD:** كل PR يفحص الكود والاعتماديات والصورة.

#### `P2-07` — تصنيف PII وتنقيح السجلات

- **الحالة (2026-11-17):** **جزئيًا — الجرد والضوابط موثّقة، وتنقيح السجلات مكتمل كودًا.** PR
  [`#201`](https://github.com/maarouf003-netizen/docgen/pull/201) — أُضيف [`docs/PII.md`](PII.md): تصنيف الفئات (هوية/رقم وطني/اتصال/عنوان/اعتماد/أخطاء)،
  وأين تظهر (واجهة/بوابة/تصدير/سجلات/تدقيق)، وضوابط التنقيح (`LogSanitizer`: بريد،
  اعتمادات، `Bearer`، **JWT عارٍ** — أُضيف هذه الجولة — وبيانات موسومة، وحذف الاستعلام)،
  والاحتفاظ (سجلات 60 يومًا) والوصول (تدقيق مقيّد بالأدوار/النطاق) والتشفير (TLS مفروض،
  عند الراحة مسؤولية المزوّد)، والحدود. التفاصيل:
  `docs/audit/59-p2-07-pii-20261117.md`.
  **متبقٍ (بنية تحتية/مالك):** تأكيد تشفير القاعدة عند الراحة، سياسة احتفاظ سجل التدقيق،
  وأي حجب/تنقيح لحقول `PII` داخل التصدير (قرار منتج).
- **العمل:** جرد PII (الاسم/الرقم الوطني/العنوان/الهاتف)؛ تنقيح السجلات؛ صلاحيات وصول؛ احتفاظ؛ تشفير عند الراحة (Supabase) وعبر TLS.
- **DoD:** لا PII في السجلات العامة؛ وصول مدقق.

#### `P2-08` — حدود المعدل ومكافحة التخمين

- **الحالة (2026-11-17):** **منفّذ آليًا — حمل فعلي متبقٍ.** PR
  [`#195`](https://github.com/maarouf003-netizen/docgen/pull/195) — أُغلق الثقب المكتشف:
  استيراد الجهات (`import-preview`/`import-commit`) وتصدير سجل التغييرات كانت بلا
  سياسة `expensive`؛ أُضيفت، مع **حارس انعكاسي** يمنع أي تصدير `SingleExportPerUser`
  جديد بلا السياسة. وأُضيف: تحقق إعدادات `RateLimiting` عند الإقلاع (بدل
  `Math.Max(1, …)` الذي كان يخفي صفرًا/سالبًا)، و`Retry-After` على كل 429 (RFC 6585)
  بما فيه محدّد الدخول المعتمد على القاعدة. قفل الدخول التدريجي (`Lockout`)
  ومفاتيح التقسيم (IP/User) كانا قائمين ومثبتين. التفاصيل:
  `docs/audit/53-p2-08-rate-limit-hardening-20261117.md`.
  **متبقٍ:** قياس 429 تحت حمل حقيقي/`staging` (لا أرقام محلية كحكم).
- **العمل:** مراجعة `RateLimitingSetup` لكل نقطة (login, export, password, CSP, عام)؛ partition حسب IP/User؛ قفل تدريجي.
- **DoD:** اختبارات 429/قفل.

#### `P2-09` — أمان التصدير/Word/Excel

- **الحالة (2026-11-15):** مغلق — PR
  [`#162`](https://github.com/maarouf003-netizen/docgen/pull/162) (squash `af7ef43`)، والتفاصيل في
  [`docs/audit/24-p2-09-export-security-20261115.md`](audit/24-p2-09-export-security-20261115.md):
  - **Excel (Formula Injection):** كل الخلايا `inlineString` بلا أي `<f>`؛ أُضيف حارس
    اختباري بخمس حِمول خبيثة يمنع الانحدار إلى خلية صيغة. **لم تُضف بادئة `'`** لأنها
    تفسد عرض الأرقام السالبة/الموجبة بلا فائدة (الخلية نصية أصلًا)؛ البادئة تلزم فقط
    لو أُضيف تصدير `CSV` (غير موجود).
  - **Word (Path Traversal):** `ResolveTemplatePath` يرفض الأسماء المطلقة ويتحقق من
    احتواء المسار الكامل في مجلد القوالب قبل الفتح.
  - **اسم الملف / `Content-Disposition`:** `ExportFileName.Sanitize` مستقل عن المنصة
    (يحظر `"` وCR/LF ومحارف تحكّم الاتجاه، ويقصّ الطول بلا بتر `surrogate`).
  - **سقف الصفوف:** موجود مسبقًا (`ExportOptions.MaxRows` للمستندات والبوابة، `5000`
    لسجل التغييرات، وإحصاءات الدوائر محدودة بعدد الدوائر)؛ وثُّق بلا تغيير.
  - 14 اختبارًا جديدًا (Excel 6 + أسماء 8)، كلها `Category=Unit`.
- **العمل:** منع Formula Injection في Excel (`'` قبل `= + - @`)، منع Path Traversal في قوالب Word، سقف صفوف/حجم، `Content-Disposition` آمن.
- **DoD:** اختبارات مدخلات خبيثة.

---

### P3 — الأداء وقابلية التوسع

#### `P3-01` — منصة قياس `PostgreSQL/Release`

- **الحالة (2026-11-21):** **منفَّذ** — PR
  [`#242`](https://github.com/maarouf003-netizen/docgen/pull/242) على فرع
  `perf/p3-01-measurement-harness` — مشروع
  `backend/tools/DocGenerator.Benchmarks` (خارج `src`/`tests`، لا يمسّ كود الإنتاج ولا `ci.yml`):
  قاعدة معزولة + هجرات حقيقية + بذر حتمي (`10k`/`50k`) + `VACUUM (ANALYZE)` قبل القياس +
  `p50`/`p95` بعد إحماء + `EXPLAIN (ANALYZE, BUFFERS, FORMAT JSON)` لكل أمر فعلي.
  `pg_stat_statements` فُحصت: **غير متوفرة** في توزيعة `PostgreSQL` المحمولة وسُجّل ذلك صراحةً.
  - النتائج: `docs/audit/measurements/20261121-p3-01-10000.{json,md}` و
    `20261121-p3-01-50000.{json,md}` (بصمة بذر متطابقة بين تشغيلين مستقلين).
  - **أبرز نتيجة:** `documents-list-text` عند `10k`: `p95=462.9ms`، وعند `50k`:
    `p95=1317ms`؛ وبحث البوابة `194ms → 661ms`. هذا يثبت أن `P3-02` هو الهدف التالي.
  - **فخ أُصلح:** أول استعلام بعد بذر كثيف كان يتجاوز `30s` لغياب إحصاءات حديثة؛
    `VACUUM (ANALYZE)` قبل القياس أعاد الخطة المستقرة.
  - التفاصيل والمراجعة النقدية: `docs/audit/81-p3-01-measurement-harness-20261121.md`.
  - **الاحتفاظ:** خط أساس واحد لكل `(task, scale)`؛ إعادات التشغيل إلى `%TEMP%`/`artifacts`.
    سياسة الخصوصية/الحجم: `docs/audit/measurements/README.md`.
- **العمل:** harness على Postgres/Release مع 10k/50k؛ `pg_stat_statements`، `EXPLAIN (ANALYZE, BUFFERS, FORMAT JSON)`، p50/p95؛ تخزين النتائج.
- **DoD:** قياس موثّق لكل استعلام ساخن؛ لا أرقام من `SQLite/Debug` كحكم إنتاجي. **متحقّق**
  للاستعلامات العشرة المطلوبة على `10k`/`50k`؛ ويبقى قياس `staging` عند توفره.

#### `P3-02` — إعادة كتابة بحث البوابة

- **الحالة (2026-11-21):** **منفَّذ** — PR
  [`#243`](https://github.com/maarouf003-netizen/docgen/pull/243) على فرع
  `perf/p3-02-portal-search`:
  - هجرتان: `20261121025258_P3_02_PortalSearchIndexes` (`pg_trgm`) و
    `20261121034750_P3_02_PortalSearchIndex` (فهرس GIN جزئي على `SearchText`
    داخل معاملة الهجرة؛ لا `CONCURRENTLY` بسبب سكربت الهجرات الـidempotent).
  - `UNION ALL` لمجموعات مرشّحي جداول الأطراف في `DocumentPartySearch`، مع إبقاء مسند
    `SearchText`/المقترض واحدًا (المحاولة الشاملة رُفضت بالقياس: تدهور إلى `1040ms`).
  - **النتيجة (p95):** `documents-list-text` عند `10k`: `462.9ms → 401.7ms` (−13.2%)؛
    وعند `50k`: `1317ms → 1245ms`. البوابة عند `10k`: `194.3ms → 185.1ms`.
  - **DoD متحقّق** عند `10k` (`<500ms`) مع نتيجة صحيحة وعزل تام (اختبار تكرار/عزل جديد).
  - التفاصيل: `docs/audit/82-p3-02-portal-search-20261121.md`، والنتائج:
    `docs/audit/measurements/20261121-p3-02-{10000,50000}.{json,md}`.
- **خط الأساس (`P3-01`، 2026-11-21):** `documents-list-text` `p95 = 462.9ms` عند `10k`
  و`1317ms` عند `50k`؛ `portal-list-text` `p95 = 194.3ms` عند `10k` و`661.3ms` عند `50k`
  (`docs/audit/measurements/20261121-p3-01-*.json`). القياس هو مرجع «قبل».
- **العمل:** `pg_trgm` GIN على `SearchText`/الأسماء، أو `tsvector` مولّد؛ `UNION ALL` لمجموعات المرشحين بدل `OR`؛ قياس قبل/بعد.
- **DoD:** `p95 < 500ms` على 10k في staging، مع نتيجة صحيحة وعزل تام.

#### `P3-03` — تدقيق الفهارس

- **الحالة (2026-11-21):** **مكتمل — لا هجرة فهارس.** تدقيق على قاعدة بذر `10k` حتمية
  (`pg_stat_user_indexes`/`pg_stat_user_tables`) + خطط `P3-02` عند `50k`:
  - كل `Seq Scan` على جدول كبير إما انتقائية عالية/تجميع إحصائي مقصود، أو جدول أبعاد
    صغير (5–100 صف)، أو عيب شكل استعلام مؤجّل إلى `P3-04` (تكرار `EF` لـ`Documents` داخل
    `EXISTS` في البوابة).
  - `Documents`: 15 فهرسًا، 5 مستخدمة في الحمل؛ **لا حذف** بلا حمل تمثيلي أوسع.
    مرشح موثّق للمالك: `IX_Documents_ApplicantRegistryId` بلا قارئ في الكود.
  - **0** مفاتيح أجنبية بلا فهرس بادئ.
  - `pg_stat_statements` غائبة في التوزيعة المحمولة وسُجّل الغياب.
  - التفاصيل: `docs/audit/92-p3-03-index-audit-20261121.md`.
- **العمل:** `pg_stat_user_indexes`/`pg_stat_statements`؛ إضافة الناقص، حذف غير المستخدم، `CREATE INDEX CONCURRENTLY`.
- **`DoD`:** لا `Seq Scan` على جدول كبير في استعلام ساخن — **متحقّق مع توثيق المقصود**
  (سجل §2 في تدقيق `P3-03`).

#### `P3-04` — Read models وإسقاطات

- **الحالة (2026-11-22):** **قوائم الملفات كلها وتصدير البوابة مُسقَطة؛ يتبقى فقط تفاصيل الملف/البوابة كدين تقني مؤجّل.**
  - `PortalRepository.SearchScopedAsync` صار يُسقط `PortalListRow` (بلا `Include` كيان
    كامل)، مع ثلاث قراءات دفعية مفهرسة لمعرّفات الصفحة (أرقام أساس/أطراف).
  - `P3-04b`: مسار `GET /documents/list` + `DocumentListItemDto` للقائمة الرئيسية،
    بإسقاط صف وبلا `WithStandardIncludes`، مع إعادة استخدام `FromEntity` على كيان
    مُجمَّع. **A/B في نفس التشغيل:** `documents-list-page1` 145.9→81.6ms (−44.1%)،
    `documents-list-text` 1408.1→329.4ms (−76.6%)، الخطط 18→9.
  - `P3-04c`: القوائم الخمس المتبقية (محذوفة/مشطوبة/منفذة/محالة للبداية/تذكيرات
    الشطب) إلى `DocumentListItemDto` عبر مسارات `…/list` إضافية، مع بناة استعلام
    مشتركة و`LoadListRowChildrenAsync` (مع تجاوز فلاتر الأبناء للمحذوف فقط).
    **A/B في نفس التشغيل (بصمة `a3f7e28a…`، `50k`):** المشطوبة 596.6→70.2ms
    (−88.2%)، المنفذة 816.1→89.9ms (−89.0%)، تذكيرات الشطب 355.7→275.9ms
    (−22.4%)؛ الخطط 17→9 و18→9 و19→10. (المحذوفة/المحالة بلا صفوف في البذرة:
    تطابق النتيجة والشكل فقط.)
  - `P3-04d`: تصدير بوابة الجهة (`ExportScopedAsync`) إلى صفوف القائمة المُسقَطة نفسها
    عبر `DocumentListRowProjection` المشترك و`DocumentListRow.ToDocumentStub`، بلا
    شجرة `Include` كاملة. **A/B في نفس التشغيل (بصمة `a3f7e28a…`، `50k`):**
    `portal-export` 5187.8→679.6ms (−86.9%)، الخطط 14→8، والصفوف/البصمة متطابقة
    (`9882`).
  - **قياس بوابة `50k`:** `portal-list-page1` `325.7→125.9ms` (−61.3%)،
    `portal-list-text` `733.3→303.9ms` (−58.5%).
  - **لا تغيير عقد قائم** (المسار القديم باقٍ؛ الجديد إضافي) ولا هجرة.
  - **متبقٍ:** `DocumentDetailDto`/`PortalFileDetailDto` — **دين تقني اختياري مؤجّل**
    بقرار `audit/94` (مسار التفاصيل 75–82ms غير ساخن)، وليس بند أداء مفتوحًا.
  - التفاصيل: `docs/audit/94-p3-04-portal-read-model-20261122.md` و
    `docs/audit/102-p3-04b-document-list-dto-20261122.md` و
    `docs/audit/103-p3-04c-archive-lists-read-model-20261122.md` و
    `docs/audit/104-p3-04d-portal-export-projection-20261122.md`.
- **العمل:** `DocumentListItemDto`، `DocumentDetailDto`، `PortalFileDetailDto`؛ إزالة `WithStandardIncludes` من القوائم؛ `AsNoTracking` و`Select` فقط.
- **`DoD`:** 20 `Include` تصبح ≤5، وزمن القائمة يتحسن — **متحقّق لقائمة البوابة
  والقوائم الست وتصدير البوابة** (0 `Include` في مسارات القراءة/التصدير)، ويتبقى
  التفاصيل كدين تقني اختياري.
- **الاعتمادية:** `P3-01` (قياس قبل/بعد) و`P1-01` (اختبارات Postgres).

#### `P3-05` — Keyset pagination

- **الحالة (2026-11-22):** **مكتملة للمسارات الساخنة:** `P3-05a` أضاف `cursor`
  موقّعًا + `nextCursor` على `/documents/list` والقوائم الخمس مع إبقاء
  `page`/`perPage`؛ و`P3-05b` حوّل واجهة القائمة الرئيسية إلى «تحميل المزيد»
  مع استعادة سلسلة المؤشرات. القوائم الإدارية الصغيرة تبقى على offset.
- **العمل:** `(CreatedAt, Id)` للقوائم الكبيرة؛ cursor موقّع؛ إبقاء offset للإداريات الصغيرة.
- **DoD:** لا `Skip/Take` على الجداول الكبيرة — متحقّق للقائمة الرئيسية عبر الواجهة،
  والـAPI يدعمه للقوائم الخمس.
- **التفاصيل:** `docs/audit/105-p3-05a-keyset-cursor-api-20261122.md` و
  `docs/audit/106-p3-05b-load-more-ui-20261122.md`.

#### `P3-06` — Caching

- **الحالة (2026-11-22):** **المرحلة 1أ منجزة — مرجعي بحت فقط.** `IMemoryCache`
  لأسماء/معرّفات الفروع والشعب والدوائر مع إبطال محلي بعد كل كتابة وTTL 3 دقائق؛
  **العدّادات الحيّة لا تُخزَّن**، و`ListNoTrackingAsync` يمنع تخزين كيانات متتبَّعة.
  **متبقٍ:** قياس hit-ratio في التشغيل، و(Single-flight/موزّع) عند تعدد النسخ.
  التفاصيل: `docs/audit/110-p3-06a-reference-data-cache-20261122.md`.
- **العمل:** `HybridCache` للبيانات المرجعية (الفروع/الشعب/الجهات)؛ invalidation عند الكتابة؛ لا caching لبيانات الصلاحيات بلا مفتاح.
- **DoD:** hit ratio مرصود، ولا بيانات قديمة.

#### `P3-07` — Load tests وSLOs

- **الحالة (2026-11-22):** **جزئيًا — الأداة والـrunbook جاهزان، وتشغيل staging متبقٍ.**
  - `scripts/slo-check.mjs` يحكم على مخرجات `P3-01/P3-02` بسقوف `p95` لـ`10k/50k`،
    ويفشل عند التجاوز أو غياب استعلام متوقع (تحقق سلبي موثّق).
  - `docs/runbooks/load-testing.md`: خطوات القياس، التحقق من زمن البذر/الحمل، A/B العادل.
  - **`P3-07a` (2026-11-22):** A/B لصفحة 100 داخل نفس التشغيل: إزاحة عميقة
    (`documents-list-page100-projected`) مقابل مؤشر keyset
    (`documents-list-page100-cursor`)، مع حفظ مؤشر حدود الصفحة 99 في
    `SeedManifest` وحدّي SLO جديدين. التفاصيل:
    `docs/audit/107-p3-07a-cursor-pagination-benchmark-20261122.md`.
  - **متبقٍ:** تشغيل الحمل على staging/بيانات قريبة من الواقع وتسجيل `RTO/SLO` التشغيلي.
  - التفاصيل: `docs/audit/100-p3-07-slo-gate-20261122.md`.
- **العمل:** k6/NBomber على staging؛ سيناريوهات القراءة/الكتابة/البوابة؛ SLO `p95` و`error budget`.
- **`DoD`:** تقرير حمل قبل كل إصدار كبير — **أداة الحكم والrunbook جاهزان**؛ تنفيذ staging مسؤولية نافذة الإصدار.

#### `P3-08` — حراسة N+1

- **الحالة (2026-11-16):** جزئيًا — PR
  [`#171`](https://github.com/maarouf003-netizen/docgen/pull/171) (squash `fc2224b`): منفّذ:
  - `QueryCounter` (DbCommandInterceptor) في `Api.Tests` يُحقن في `DbContext` الاختباري
    عبر `TestDatabase.UseSqlite`، ويعدّ كل الأوامر لكل طلب.
  - `QueryBudgetTests`: قائمة 15 صفًا بسقف 22 (القياس 18) وتفاصيل صف واحد بسقف 20 —
    العدد لا ينمو مع الصفوف، فأي N+1 يضيف ≥15 استعلامًا ويفشل فورًا.
  - **P3-08b (2026-11-16):** تصدير Excel لـ15 صفًا بسقف **14** (القياس 10) — يغطي مسار
    `WithStandardIncludes` الأثقل. التفاصيل: `docs/audit/35-p3-08b-export-budget-20261116.md`.
  - **P3-08c (2026-11-17):** PR
    [`#188`](https://github.com/maarouf003-netizen/docgen/pull/188) — سقوف المسارات المتبقية: سجل التدقيق 15 صفًا **12**
    (القياس 8)، سجل تغييرات المستند **6** (القياس 4)، إحصاءات 15 دائرة **16**
    (القياس 12)، قائمة البوابة 15 صفًا **18** (القياس 14) — مع تأكيد أن الاستجابة
    تحمل صفوفًا فعلية، وتحقّق سلبي بحقن N+1 (27 > 16). التفاصيل:
    `docs/audit/46-p3-08c-query-budgets-20261117.md`.
- **`P3-08d/e` (2026-11-22):** ميزانيات استعلامات للمسارات المُسقَطة/المؤشر
  (قائمة الملفات، صفحة المؤشر، الأرشيف الأربع، تذكيرات الشطب، تصدير البوابة) —
  الحارس يمنع أي N+1 في مسارات `P3-04`/`P3-05`. التفاصيل:
  `docs/audit/108-p3-08de-list-query-budgets-20261122.md`.
  **مغلق** — سقوف كل المسارات المذكورة مثبتة؛ يبقى القياس الإنتاجي على
  `PostgreSQL`/Release ضمن `P3-01` (البند المستقل).
- **العمل:** `DbCommandInterceptor` في الاختبارات يعدّ الاستعلامات لكل طلب؛ سقف لكل نقطة؛ `AsSplitQuery` عند الحاجة.
- **DoD:** لا انحدار استعلامات.

#### `P3-09` — ضبط الاتصال والـPool

- **الحالة (2026-11-17):** **منفّذ آليًا — قياس الحمل متبقٍ.** PR
  [`#194`](https://github.com/maarouf003-netizen/docgen/pull/194): أُضيف `Database:Pool`
  (`MaxPoolSize=20` · `MinPoolSize=0` · `ConnectionTimeoutSeconds=15` ·
  `CommandTimeoutSeconds=30` · `IdleLifetimeSeconds=300` · `HealthCheckTimeoutSeconds=5`)
  ويُطبَّق على سلسلة `Npgsql` **كافتراضات فقط**: أي مفتاح صريح في السلسلة (ومنها
  `connect_timeout` من URI) يبقى محترمًا. القيم تُتحقق عند الإقلاع، و`/readyz` مقيَّد
  بمهلة صريحة كي لا ينتظر اتصالًا من مجمّع مستنزف. التفاصيل:
  `docs/audit/52-p3-09-db-pool-20261117.md`.
  **متبقٍ (لا يُغلق آليًا):** قياس `pool utilization` تحت حمل فعلي على `PostgreSQL`
  (ضمن `P3-01`)، وضبط `MaxPoolSize` النهائي حسب عدد النسخ وحدّ المزوّد.
- **العمل:** مراجعة `Npgsql` (`MaxPoolSize`، `Timeout`، `CommandTimeout`، `KeepAlive`، `Maximum Pool Size` مع حدود Render/Supabase)؛ health check بمهلة؛ منع استنزاف الـpool أثناء التقارير/التصدير.
- **DoD:** لا `Timeout` عشوائي تحت الحمل؛ قياس pool utilization.

---

### P4 — المعمارية والصيانة

#### `P4-01` — Command/Query pipeline

- **الحالة (2026-11-24):** **جزئيًا — الأساس مثبت بشريحة رأسية في `Documents`.**
  أُضيفت عقود `ICommandHandler`/`IQueryHandler`/`IPipelineBehavior` وأساسا التركيب،
  وثلاثة سلوكيات (`Validation`/`Transaction`/`Audit`) مسجّلة كـ open generics،
  وحُوِّلت نقطتا `Get`/`Create` في `DocumentsController` إلى معالجين **بلا تغيير سلوكي**.
  المعاملة/التدقيق لا يعملان إلا للطلبات الموسومة (`ITransactionalRequest`/`IAuditedRequest`)
  لأن الخدمات الحالية تملكهما. **`P4-01b`:** أول use-case مكتمل الملكية —
  `DismissStrikeOffReminderCommand` يملك المعاملة (عبر الوسم) والتدقيق، وأُزيلت الكتابة
  من `StrikeOffReminderService`. **`P4-01c/d`:** `MarkReviewRepliesSeenCommand` و
  `SetLawyerActiveCommand` يملكان معاملتهما (والثاني تدقيقه أيضًا)، وأُزيلت الطريقتان
  من عقدي خدمتيهما. **`P4-01e/f`:** `MarkHeadAlertReadCommand` (تحديث ذرّي بلا معاملة)
  و`MarkAppSuggestionReadCommand` (معاملة + تدقيق) أُزيلا من عقدي خدمتيهما.
  **`P4-01g/h`:** `DeletePersonalReminderCommand` و`DeleteBranchCommand` يملكان
  المعاملة والتدقيق (والثاني إبطال الكاش)، وأُزيلا من عقدي خدمتيهما.
  **`P4-01i/j`:** `CreateAppSuggestionCommand` و`DeleteForumMessageCommand` يملكان
  المعاملة والتدقيق (المنتدى بلا تدقيق كالأصل)، وأُزيلا من عقدي خدمتيهما.
  **`P4-01k/l`:** `SetPersonalReminderOccurrenceCommand` و`CreateBranchCommand` يملكان
  المعاملة (والثاني التدقيق وإبطال الكاش)، وأُزيلا من عقدي خدمتيهما. **`P4-01m/n`:**
  `DeleteExecutionActionCommand` و`DeleteDocumentInterventionCommand` يملكان المعاملة
  والتدقيق (`action`/`intervention`؛ الثاني يعيد بناء `SearchText` ويستخدم
  `EnsureSupportedSide` المشتركة)، وأُزيلا من عقدي خدمتيهما. **`P4-01o/p/q`:**
  `UpdateBranchCommand` و`CreatePersonalReminderCommand` و`UpdatePersonalReminderCommand`
  يملكون المعاملة والتدقيق (الأول إبطال الكاش أيضًا)، وأُزيلت الثلاثة من عقدي خدمتيهما.
  **`P4-01r/s/t/u`:** `CreateSectionCommand` و`DeleteSectionCommand` و`RenameSectionCommand`
  و`SetSectionActiveCommand` يملكون المعاملة والتدقيق وإبطال كاش الشعب (والأول يحوّل
  انتهاك الوحدانية إلى `DocumentConflictException`، والتسمية تسجّل `HeadSuccession`)،
  وأُزيلت الأربعة من عقد `ISectionService` الذي صار قراءةً فقط، مع `NormalizeName`/
  `NameExists`/`ToDto` مساعدين مشتركين.
  **`P4-01v/w`:** `AddExecutionActionCommand` و`UpdateExecutionActionCommand` يملكان
  المعاملة والتدقيق (`action`)، وأُزيلا من `IExecutionActionService` مع إتاحة
  `NormalizeAction`/`NormalizeReminder`/`ToDto` مساعدات مشتركة (بلا تغيير سلوك التذكير).
  **`P4-01x/y/z`:** `ToggleForumPinCommand` (مع استخراج `ForumMembership` وإزالة نسخة
  العضوية المكررة)، `ClearActionReminderCommand` (مع `ClearReminderWriteAsync` المشتركة)،
  و`AddDocumentInterventionCommand`/`UpdateDocumentInterventionCommand` (مع
  `DocumentInterventionRules`) — وأُزيلت خدمة المتدخلين وعقدها نهائيًا.
  **`P4-01aa/ab`:** `RestoreDocumentCommand` و`SetActionReminderCommand`/
  `RescheduleActionReminderCommand` يملكون المعاملة والتدقيق (`restore`/`action`)،
  وأُزيلت الثلاثة من عقدي خدمتيهما (بقي `IExecutionActionService` للقراءة والزرع).
  **`P4-01ac/ad/ae`:** `PostForumMessageCommand` و`UpdateForumMessageCommand` و
  `MarkForumReadCommand` (الأخير يملك معاملته عبر `ITransactionRunner` عمدًا لابتلاع
  سباق الإدراج بعد التراجع)، مع `ForumMessageContent` مشتركًا — و`IForumService` صار قراءةً فقط.
  **`P4-01af/ag`:** `CreateExecutionCircuitCommand` و`SetExecutionCircuitActiveCommand`
  يملكان المعاملة والتدقيق وإبطال الكاش، مع `ExecutionCircuitRules` مشتركًا؛
  وتسمية الدائرة (`RenameAsync`) بقيت لمرحلة مستقلة لثقلها الجماعي.
  **`P4-01ah`:** `RenameExecutionCircuitCommand` يملك معاملة الجماعية كاملة (ملفات +
  وقوعات + إنابات + ترحيل `Outbox` + تدقيق حقلي)، مع `ExecutionCircuitRules.LogDocumentChangeAsync`
  مشتركة بين التسمية وإعادة القيد والإحالة.
  **`P4-01ai`:** `CompletePendingRegistrationsCommand` يملك معاملة إعادة القيد الجماعية
  (وحدانية السجل + وقوعات + تدقيق حقلي + تنظيف تنبيهات المعلقات)، مع
  `ExecutionCircuitRules.CleanupPendingAlertsAsync` مشتركة.
  **`P4-01aj`:** `DeleteExecutionCircuitCommand` يملك الحذف الفيزيائي/التطهير النهائي
  (فصل الإنابات، أرقام الأساس، أرشفة التنبيهات)؛ وحُذف `ExecutionCircuitService.Crud.cs`
  مع حقلي `DocumentBaseNumber` الميتين.
  **`P4-01ak`:** `TransferExecutionCircuitCommand` يملك نقل الملكية كاملًا (فحص النسخة،
  سجل التعاقب، والتنبيه البديل عبر `Outbox` بلا تعديل القديم) ومعاملته الخاصة عبر
  `ITransactionRunner` — بلا وسم `ITransactionalRequest` — كي يلتف `catch` تعارض التزامن
  حولها كما الأصل؛ وبقيت `SyncPendingAlertsAfterTransferAsync` عند مستدعيها في
  `DocumentService` (نقل الملفات) ولا تدخل مسار نقل الدائرة.
  **`P4-01al`:** `ReferCircuitFilesCommand` يملك مفتاح عدم التكرار (`RF-011`) ومعاملة
  الإفراغ كاملة (تعطيل المصدر، الهوية الجديدة، الوقوعات الثلاث، إعادة توجيه الواردة،
  والتدقيق الحقلي/الجماعي والتنبيه عبر `Outbox`)، مع `ExecutionCircuitRules.GetOwnedAsync`
  و`EnqueuePendingAlertAsync` مشتركتين؛ وأُزيل من الخدمة سبعة حقول ميتة (`_occurrences`,
  `_audit`, `_dbErrors`, `_idempotency`, `_clock`, `_timeZone`, `_numberRegistry`).
  **`P4-01am`:** `UpdateDocumentCommand` يملك تعديل المستند كاملًا (التحققات، حارسا
  الانتقال/الإنابة، تبديل الدائرة، شبكتا التزامن/الترقيم، المرآة والتنبيهات والوقوعات)،
  مع `DocumentService.ApplyRequest`/`ApplyRegistrationDate` مشتركتين بين الإنشاء والتعديل.
  **`P4-01an`:** `DeleteDocumentCommand` يملك الحذف المنطقي (حرّاس الإنابة/الاستئناف،
  إلغاء صفوف الأرقام قبل العلم، والتدقيق) في معاملة واحدة؛ وأُزيل `IAppealRepository`
  من حقول `DocumentService`.
  **`P4-01ao`:** `TransferDocumentCommand` يملك نقل الملف المفرد (تحققات `TOCTOU`،
  التعاقب، مزامنة المناب، التدقيق، وتنبيه المالك الجديد `H2`) داخل معاملة واحدة.
  **`P4-01ap`:** `TransferAllDocumentsCommand` يملك النقل الجماعي بمفتاح `RF-011`
  (بصمة مطابقة، تحققات داخل المعاملة، تدقيق لكل ملف، مزامنة المناب، وتنبيه `H2`).
  **`P4-01aq`:** ثلاث نقاط كتابة أرقام الأساس (`Save`/`Correct`/`Undo`) صارت أوامر
  تفوّض `IDocumentNumberRegistry` بنفس الحدود، مع اختبار وحدة يثبّت رسالة التحقق.
  **`P4-01ar`:** `DeleteDocumentDelegationCommand` يملك حذف الإنابة (الملكية قبل الحالة،
  أرشفة التنبيهات، والتدقيق)؛ ونُقل `EnsurePendingByOwner` إلى `DocumentDelegationRules`.
  (قُسّمت مرحلة الإنابة عمدًا: `⁦01as⁩` للإنشاء/التعديل.)
  **`P4-01as`:** `DeleteDocumentAppealCommand` يملك حذف الاستئناف بنفس بنية الحرّاس
  والأرشفة والتدقيق، مع `DocumentAppealRules.EnsurePendingByCreator` المشتركة.
  **`P4-01at`:** نقل صرف لمساعدات الإنابة المشتركة إلى `DocumentDelegationRules`
  (تحقق/استهداف/تطهير حجوزات) وتوسيع رؤية المساعدات النقية — بلا أي تغيير سلوكي
  أو عقدي، تمهيدًا لنقل `Create/Update` بتغيير صغير.
  **`P4-01au`:** `CreateDocumentDelegationCommand`/`UpdateDocumentDelegationCommand`
  يملكان أوركسترا الإنابة كاملة (الحرّاس، الاستهداف، الحجوزات، `Outbox`، التدقيق)،
  مع تصحيح خطأ حذف `SyncTargetsTransferredAsync` الذي كشفه البناء وإعادته حرفيًا.
  **`P4-01av`:** `CreateDocumentAppealCommand`/`UpdateDocumentAppealCommand` يملكان
  تسطير الاستئناف وتعديله (الحرّاس، اللقطات، الحقول، تنبيه المالك، التدقيق)، مع
  `DocumentAppealRules.ValidateSourceForAppeal` و`AppealDocumentNumberText` المشتركتين
  وإزالة `EnqueueAppealPendingAsync` الميتة.
  **`P4-01aw`:** `UpdateAppealRegistrationCommand` يملك قيد الاستئناف (سياسة رقم الأساس
  الواحدة، حارس المتابع، والتدقيق) داخل معاملة واحدة.
  **`P4-01ax`:** `AssignDocumentAppealCommand`/`TransferDocumentAppealCommand`/
  `TransferAllDocumentAppealsCommand` تملك الإسناد والنقل المفرد والجماعي، مع نقل
  `LoadForHeadActionAsync`/`ResolveTargetLawyerAsync`/`EnqueueAppealAssignedAsync`
  إلى `DocumentAppealRules` وإزالة الكود الميت.
  **`P4-01ay`:** `DecideDocumentAppealCommand`/`StrikeDocumentAppealCommand` يملكان
  إغلاق الاستئناف (الحسم/الشطب، تنبيه صاحب الملف، والتدقيق)، مع
  `DocumentAppealRules.EnqueueAppealFinalAsync` وحذف `Registration.cs` بعد إفراغه.
  **`P4-01az`:** `ForwardDocumentAppealCommand`/`RecallAppealForwardCommand` يملكان
  الإحالة والتراجع (تنبيه الرئيس/الأرشفة، و`catch` التزامن حول المعاملة)، وحُذف
  `Forward.cs`/`Alerts.cs` بعد إفراغهما مع الكود الميت (`AppealDocumentNumber`).
  **`P4-01ba`:** `AddAppealActionCommand`/`UpdateAppealActionCommand`/
  `DeleteAppealActionCommand` تملك CRUD إجراءات الاستئناف (التعقيم، قواعد التاريخ،
  وحارس المتابع)؛ ومسارات التذكيرات بقيت لمرحلة تالية.
  **`P4-01bb`:** `ClearAppealReminderCommand`/`SetAppealReminderCommand`/
  `RescheduleAppealReminderCommand` تملك تذكيرات إجراءات الاستئناف، مع
  `DocumentAppealRules.ClearReminderCoreAsync` المشتركة وقاعدة «تاريخ الإجراء لا يُكتب».
  **`P4-01bc`:** `SaveAppealBaseNumbersCommand`/`CorrectAppealBaseNumberCommand` يملكان
  كتابات رقم الأساس الاستئنافي (التدوير للمسند المنظور، والتصحيح الموضعي في أي حالة)
  تحت `AppealBaseNumberPolicy`، مع بقاء `GetBaseNumberHistoryAsync` قراءة.
  **`P4-01bd`:** ستة أوامر لانتقالات حالة المستند (`UpdateStatus`/`RevertStatus`/
  `ReturnFromReferredToStart`/`ConsiderExecutedByDelegation`/`UpdateExecutedStatus`/
  `RestoreStruckOff`) تملك معاملاتها وتدقيقها، مع `DocumentStatusRules` لمساعدات
  الحقول/التجديد/توريث المناب وبقاء كاتبَي الوقوعات في الخدمة لمعاملة المستدعي.
  **`P4-01be`:** `CreatePublicEntityCommand`/`UpdatePublicEntityCommand`/
  `AddPublicEntityAliasCommand`/`ApprovePublicEntityReviewCommand` تملك كتابات السجل
  الأساسية مع `IPublicEntityWriteRules` لمساعدات الحرّاس/التكرار/أحداث التغيير.
  **`P4-01bf`:** `CreateEntityDelegateCommand`/`UpdateEntityDelegateCommand` تملك
  كتابات مندوبي الجهات مع `IEntityDelegateWriteRules`.
  **`P4-01bg`:** `CreateLawyerCommand`/`UpdateLawyerCommand` تملك كتابات المحامين.
  **`P4-01bh`:** `CreateUserCommand`/`UpdateUserCommand` تملك كتابات المستخدمين
  (الدور/الفرع/الشعبة/الخلف الإجباري).
  **`P4-01bi`:** `CreateManualHeadAlertCommand`/`RemoveAppealAlertRecipientCommand`
  تملك الإصدار اليدوي للتنبيهات، مع الحفاظ على حارس البناء المركزي.
  **`P4-01bj`:** `ChangePasswordCommand` يملك تغيير كلمة المرور (يبقى الدخول لمرحلة تالية).
  **`P4-01bk`:** `DeletePendingRegistrationAlertsCommand` يملك خطاف تنظيف تنبيه إعادة القيد.
  **`P4-01bl`:** `AddReviewLetterAddendumCommand`/`ReplyReviewLetterCommand` يملكان
  لاحق/رد كتاب المطالعة، مع `IReviewLetterReplyWriteRules`.
  **`P4-01bm`:** `CreateReviewLetterCommand` يملك تسطير كتاب المطالعة، مع
  `IReviewLetterCreateWriteRules` (المتابعة/المستلم/الترقيم).
  **`P4-01bn`:** `CommitPublicEntityImportCommand` يملك اعتماد الاستيراد التاريخي
  (مع مفتاح `RF-011`)، وبقيت المعاينة قراءة.
  **`P4-01bo`/`P4-01bp`:** `WithdrawParentEditSuggestionCommand`/
  `ReviewParentEditSuggestionCommand` يملكان سحب/مراجعة اقتراح الجهة الأم.
  **`P4-01bq`/`P4-01br`/`P4-01bs`:** `RenamePublicEntityBranchCommand`/
  `MergePublicEntityBranchesCommand`/`AbolishPublicEntityBranchCommand` تملك عمليات
  الفروع الثلاث مع `IPublicEntityWriteRules` المشترك.
  **`P4-01bt`:** `CreateCorrespondenceCommand`/`AddCorrespondenceAddendumCommand`/
  `ReplyCorrespondenceCommand`/`MarkCorrespondenceSeenCommand` تملك كتابات المراسلات
  الأربع مع `ICorrespondenceWriteRules` المشترك؛ حُذفت التواقيع الأربعة من
  `ICorrespondenceService` وبقيت القراءات، وأُزيلت الحقول الأربعة الميتة
  (`_uow`/`_tx`/`_audit`/`_errors`) من `CorrespondenceService` بعد نقل الكتابات.
  **`P4-01bu`:** `LoginCommand` يملك مسار الدخول كاملًا (اختيار الحساب/الفرع، القفل
  والتراجع الأسّي، ترقية صيغة الهاش، والتدقيق) داخل معالج واحد بلا معاملة متداخلة؛
  `AuthService` صارت قراءةً فقط (`GetUserAsync`) وحُذفت حقولها الستة الميتة.
  **`P4-01bv`:** `UnifyPublicEntityBranchesCommand` يملك توحيد تسميات الفروع (الدخول
  النشط، تفرّد الاسم المصحّح، إعادة توجيه الروابط والمناديب، الألقاب، تغيير الفرع،
  الوقوعات والتنبيه، والتدقيق) داخل معاملة واحدة، وبقيت معاينة الإجراء قراءةً.
  **`P4-01bw`:** `MovePublicEntityEntryCommand`/`MoveAllPublicEntityEntriesCommand` يملكان
  نقل القيد (وضع الطيّ أو تغيير الهوية الأم) والنقل الجماعي: الحرّاس والنطاق، إعادة
  توجيه الروابط والمناديب، الألقاب والمزامنة النصية، أحداث التغيير، الوقوعات والتنبيهات،
  والتدقيق — بمعاملاتها كما في الأصل؛ وحُذف الملف الجزئي `PublicEntityService.Moves.cs` بعد إفراغه.
  **`P4-01bx`:** `RedirectDocumentDelegationCommand`/`RecallDelegationRedirectCommand`/
  `RejectDocumentDelegationCommand` تملك توجيه الإنابة الخارجية لشعبة والتراجع عنه
  ورفض الدائرة الخطأ (الحرّاس والنطاق والتنبيهات والتدقيق)؛ ونُقلت حرّاس الاعتماد
  المشتركة إلى `DocumentDelegationRules`، وحُذف حقلان ميتان (`_reservations`/`_sections`)
  من الخدمة بعد نقل آخر مستخدميهما.
  **`P4-01by`:** `AssignDocumentDelegationCommand` يملك اعتماد الإنابة كاملًا (نطاق
  الاعتماد، المحامي المختص بفرع الدائرة، إنشاء الملف المناب ونسخ الأطراف/السند،
  تنبيه الإسناد، الوثيقة، والتدقيق) مع ترجمة سباق `SourceDelegationId` إلى 409؛
  وحُذف `DocumentDelegationService.Approval.cs` بعد إفراغه وحقل `_branches` الميت.
  **`P4-01bz`:** `RegisterDocumentDelegationCommand` يملك تسجيل الإنابة أصولًا
  (فحص المنيب، الرقم/السنة/الدائرة، التفرّد، لقطة القيد، استهداف رئيس الإتمام،
  التنبيه، والتدقيق) مع ترجمة سباق الرقم إلى 409.
  **`P4-01ca`:** `CompleteDocumentDelegationCommand` يملك إتمام الإنابة (بدلات البيع،
  تحويل المنيب إلى «منفذ جبريا/جزئيا»، الملف المناب «منفذ إنابة»، الوقعة، التنبيه،
  والتدقيق)؛ وحُذف `DocumentDelegationService.Lifecycle.cs` بعد إفراغه.
  **`P4-01cb`:** `CommitPublicEntityMergeCommand` يملك اعتماد دمج الجهات (الحرّاس،
  الاسم النهائي والألقاب، ترحيل الروابط والأسماء، مزامنة النصوص والاستئنافات،
  مندوبو الجهات، حدث الدمج، الوقوعات، التنبيهات، والتدقيق) داخل معاملة واحدة مع
  حجز `RF-011` كما هو.
  **`P4-01cc`:** `RenamePublicEntityGroupCommand` يملك إعادة تسمية الهوية الأم
  (فحص الصلاحية والمرسوم، تفرّد الاسم، الألقاب البديلة، مزامنة النصوص والاستئنافات،
  حدث التغيير، الوقوعات، التنبيهات، والتدقيق) بلا تغيير سلوكي.
  **ملحق `P4-01bs`:** مراجعة نقدية ثانية بعد الدمج أزالت تعليقَي `XML` يتيمين،
  وحقل `_dbErrors` الميت في `ReviewLetterService`، وثبّتت ثابت محاولات الترقيم (20).
  التفاصيل:
  `docs/audit/128-p4-01a-command-query-slice-20261123.md` و
  `docs/audit/129-p4-01b-dismiss-command-20261123.md` و
  `docs/audit/130-p4-01cd-batched-use-cases-20261123.md` و
  `docs/audit/131-p4-01ef-batched-read-commands-20261123.md` و
  `docs/audit/132-p4-01gh-batched-delete-commands-20261123.md` و
  `docs/audit/133-p4-01ij-batched-create-delete-commands-20261123.md` و
  `docs/audit/134-p4-01kl-batched-occurrence-branch-commands-20261123.md` و
  `docs/audit/135-p4-01mn-batched-delete-commands-20261123.md` و
  `docs/audit/136-p4-01opq-branch-reminder-commands-20261123.md` و
  `docs/audit/137-p4-01rstu-sections-commands-20261123.md` و
  `docs/audit/138-p4-01vw-execution-action-writes-20261123.md` و
  `docs/audit/139-p4-01xyz-forum-reminder-intervention-20261123.md` و
  `docs/audit/140-p4-01aaab-restore-reminder-commands-20261123.md` و
  `docs/audit/141-p4-01acadae-forum-writes-20261123.md` و
  `docs/audit/142-p4-01afag-circuit-create-active-20261123.md` و
  `docs/audit/143-p4-01ah-circuit-rename-20261123.md` و
  `docs/audit/144-p4-01ai-complete-registrations-20261123.md` و
  `docs/audit/145-p4-01aj-circuit-delete-20261123.md` و
  `docs/audit/146-p4-01ak-circuit-transfer-20261124.md` و
  `docs/audit/147-p4-01al-circuit-refer-files-20261124.md` و
  `docs/audit/148-p4-01am-document-update-20261124.md` و
  `docs/audit/149-p4-01an-document-delete-20261124.md` و
  `docs/audit/150-p4-01ao-document-transfer-20261124.md` و
  `docs/audit/151-p4-01ap-document-transfer-all-20261124.md` و
  `docs/audit/152-p4-01aq-document-base-numbers-20261124.md` و
  `docs/audit/153-p4-01ar-delegation-delete-20261124.md` و
  `docs/audit/154-p4-01as-appeal-delete-20261124.md` و
  `docs/audit/155-p4-01at-delegation-rules-extraction-20261124.md` و
  `docs/audit/156-p4-01au-delegation-write-commands-20261124.md` و
  `docs/audit/157-p4-01av-appeal-write-commands-20261124.md` و
  `docs/audit/158-p4-01aw-appeal-registration-20261124.md` و
  `docs/audit/159-p4-01ax-appeal-assignment-20261124.md` و
  `docs/audit/160-p4-01ay-appeal-decision-20261124.md` و
  `docs/audit/161-p4-01az-appeal-forward-20261124.md` و
  `docs/audit/162-p4-01ba-appeal-actions-20261124.md` و
  `docs/audit/163-p4-01bb-appeal-reminders-20261124.md` و
  `docs/audit/164-p4-01bc-appeal-base-numbers-20261124.md` و
  `docs/audit/165-p4-01bd-document-status-transitions-20261124.md` و
  `docs/audit/166-p4-01be-public-entity-writes-20261124.md` و
  `docs/audit/167-p4-01bf-entity-delegate-writes-20261124.md` و
  `docs/audit/168-p4-01bg-lawyer-writes-20261124.md` و
  `docs/audit/169-p4-01bh-user-writes-20261124.md` و
  `docs/audit/170-p4-01bi-head-alert-manual-20261124.md` و
  `docs/audit/171-p4-01bj-change-password-20261124.md` و
  `docs/audit/172-p4-01bk-pending-registration-hook-20261124.md` و
  `docs/audit/173-p4-01bl-review-letter-replies-20261124.md` و
  `docs/audit/174-p4-01bm-review-letter-create-20261124.md` و
  `docs/audit/175-p4-01bn-public-entity-import-20261124.md` و
  `docs/audit/176-p4-01bo-withdraw-parent-suggestion-20261124.md` و
  `docs/audit/177-p4-01bp-review-parent-suggestion-20261124.md` و
  `docs/audit/178-p4-01bq-rename-branch-20261124.md` و
  `docs/audit/179-p4-01br-merge-branches-20261124.md` و
  `docs/audit/180-p4-01bs-abolish-branch-20261124.md` و
  `docs/audit/181-p4-01bs-followup-review-20261124.md` و
  `docs/audit/182-p4-01bt-correspondence-writes-20261124.md` و
  `docs/audit/183-p4-01bt-followup-review-20261124.md` و
  `docs/audit/184-p4-01bu-login-command-20261124.md` و
  `docs/audit/185-p4-01bu-followup-review-20261124.md` و
  `docs/audit/186-p4-01bv-unify-branches-20261124.md` و
  `docs/audit/187-p4-01bv-followup-review-20261124.md` و
  `docs/audit/188-p4-01bw-move-entries-20261125.md` و
  `docs/audit/189-p4-01bw-followup-review-20261125.md` و
  `docs/audit/190-p4-01bx-delegation-approval-20261125.md` و
  `docs/audit/191-p4-01bx-followup-review-20261125.md` و
  `docs/audit/192-p4-01by-assign-delegation-20261125.md` و
  `docs/audit/193-p4-01by-followup-review-20261125.md` و
  `docs/audit/194-p4-01bz-register-delegation-20261125.md` و
  `docs/audit/195-p4-01bz-complete-delegation-20261125.md` و
  `docs/audit/196-p4-01bz-ca-followup-review-20261125.md` و
  `docs/audit/197-p4-01cb-merge-commit-20261125.md` و
  `docs/audit/198-p4-01cc-rename-group-20261125.md` و
  `docs/audit/199-p4-01cb-cc-followup-review-20261125.md`.
  **متبقٍ:** توسيع تدريجي module-by-module ونقل ملكية use-case كاملة إلى المعالجات
  (ملكية `CreateDocumentCommand`، وبقايا كتابات الجهات العامة:
  `AbolishAndReplaceAsync` و`ProposeEditAsync` و`SuggestParentEditAsync`).
- **العمل:** `ICommandHandler<TCommand,TResult>` و`IQueryHandler`؛ Behaviors: Validation, Authorization, Idempotency, Transaction, Audit, Outbox؛ المتحكمات رقيقة.
- **DoD:** كل نقطة كتابة = handler واحد؛ لا `_tx.RunAsync` مكرر في المتحكم.
- **الاعتمادية:** `P0-03`، `P0-04`، `P2-01`.

#### `P4-02` — تفكيك الخدمات الضخمة

- **الحالة (2026-11-23):** **النطاق المعلن مكتمل + توسيع النطاق جارٍ — `DoD` العام ما زال مفتوحًا.**
  `DocumentRepository`: 1564 → خمسة ملفات بأقصى 534 سطرًا؛ `DocumentStatusService`:
  1312 → خمسة ملفات بأقصى 382 سطرًا؛ `DocumentDelegationService`: 3167 (1904 + 1263)
  → اثنا عشر ملفًا بأقصى 510 أسطر؛ `DocumentAppealService`: 1600 → أحد عشر ملفًا
  بأقصى 334 سطرًا؛ `ExecutionCircuitService`: 1296 → سبعة ملفات بأقصى 317 سطرًا؛
  `HeadAlertService`: 1137 → خمسة ملفات بأقصى 413 سطرًا؛ `DocumentService`: 1110 →
  خمسة ملفات بأقصى 465 سطرًا؛ `CorrespondenceService`: 1049 → ستة ملفات بأقصى
  302 سطرًا؛ `DocumentContextBuilder`: 912 → خمسة ملفات بأقصى 356 سطرًا؛
  `DocumentNumberRegistry`: 742 → أربعة ملفات بأقصى 325 سطرًا؛
  `PublicEntityService.Branches`: 738 → ملفان بأقصى 375 سطرًا؛
  `ReviewLetterService`: 726 → خمسة ملفات بأقصى 243 سطرًا؛
  `UserManagementService`: 710 → خمسة ملفات بأقصى 220 سطرًا؛
  `PublicEntityService` (الرئيسي): 674 → أربعة ملفات بأقصى 372 سطرًا؛
  `PortalService`: 665 → أربعة ملفات بأقصى 328 سطرًا؛ `PublicEntityService.Registry`:
  629 → ثلاثة ملفات بأقصى 363 سطرًا، بلا تغيير سلوكي.
  **`Application`: `DoD` متحقق — لا ملف >600 (أقصى 566).** في `Infrastructure`:
  `StatisticsRepository` 902 → خمسة ملفات بأقصى 259 سطرًا؛ `HeadAlertRepository`
  630 → أربعة ملفات بأقصى 288 سطرًا. **`DoD` العام مغلق: لا ملف >600 في الطبقتين.**
  التفاصيل: `docs/audit/109-p4-02a-document-repository-split-20261122.md` و
  `docs/audit/111-p4-02b-document-status-split-20261122.md` و
  `docs/audit/112-p4-02c-document-delegation-split-20261122.md` و
  `docs/audit/113-p4-02d-document-appeal-split-20261122.md` و
  `docs/audit/114-p4-02e-execution-circuit-split-20261122.md` و
  `docs/audit/115-p4-02f-head-alert-split-20261122.md` و
  `docs/audit/116-p4-02g-document-service-split-20261123.md` و
  `docs/audit/117-p4-02h-correspondence-split-20261123.md` و
  `docs/audit/118-p4-02i-document-context-builder-split-20261123.md` و
  `docs/audit/119-p4-02j-number-registry-split-20261123.md`.
- **العمل:** `DocumentDelegationService` (1904)، `DocumentAppealService` (1598)، `DocumentStatusService` (1312)، `ExecutionCircuitService` (1264)، `DocumentRepository` (1232) إلى وحدات use-case ≤500 سطر.
- **DoD:** لا ملف >600 سطر في Application/Infrastructure (باستثناء الهجرات/الـsnapshots).
- **الاعتمادية:** `P1-03`/`P1-06` (اختبارات حدود) و`P4-01` (pipeline) لتفادي تفكيك بلا شبكة أمان.

#### `P4-03` — تحقق مركزي وProblemDetails

- **الحالة (2026-11-16):** **جزئيًا** — PR
  [`#185`](https://github.com/maarouf003-netizen/docgen/pull/185): الجزء الأول منفّذ: مسح دلالة
  `400↔403/404` في المطالعات والمراسلات ودوائر التنفيذ. صُحّح خلط «فشل التحقق ⇒ 404»
  في `ReviewLettersController` (حُذفت 5 كتل `catch (ArgumentException) ⇒ NotFound`)،
  ونُقل المفقود إلى `KeyNotFoundException` (404) والصلاحية/النطاق إلى
  `UnauthorizedAccessException` (403) في الخدمات الثلاث. حارس `ApiErrorMessagesTests`
  أُعيد ضبطه من 9 إلى 4 كتل مسموحة بتوثيق. التفاصيل:
  `docs/audit/43-p4-03-error-semantics-20261116.md`.
  **متبقٍ:** `FluentValidation`/`IValidator<T>` موحّد، و`ProblemDetails` بشكل
  `code/field/message`، وربط الواجهة؛ وتوحيد «رئيس بلا فرع» (`D11` يفرّق كتابة `400`/
  قراءة `403` وبعض العدّادات ما زالت `400`) — قرار مالك قبل أي تغيير.
- **`P4-03` (تكملة، 2026-11-17):** PR
  [`#191`](https://github.com/maarouf003-netizen/docgen/pull/191) — عقد الخطأ الموحّد منفّذ — `ApiProblem`
  (`application/problem+json`) بمفاتيح `type/status/code/message/traceId` وامتدادات
  `field/errors/warningToken`، في **كل** منتج خطأ: المتحكمات، الوسائط والمرشحات،
  معالج الاستثناءات، وربط النموذج الإطاري (`InvalidModelStateResponseFactory`)، مع
  حارس مصدري `ErrorContractGuardTests` (تحقق سلبي موثّق). `message` عربي معقّم دائمًا،
  ورسائل ربط النموذج الإنجليزية تُستبدل بالبديل العربي. التفاصيل:
  [`docs/adr/0008-api-error-contract.md`](adr/0008-api-error-contract.md) و
  `docs/audit/49-p4-03-error-contract-20261117.md`.
  **متبقٍ:** `IValidator<T>`/`FluentValidation` موحّد — **أُجّل بقرار موثّق** إلى `P4-01`
  (التحقق هنا دلالة عمل في الخدمات لا شكل طلب).
- **`D11` (2026-11-17):** **مغلق** بقرار المالك — PR
  [`#192`](https://github.com/maarouf003-netizen/docgen/pull/192): «رئيس بلا فرع» قراءة/عدّ `403`
  وكتابة `400`، بعد توحيد خمسة أسطح قراءة (الإنابة قائمةً وعدّادًا، مطالعات، استئنافات،
  إحصاءات) كانت تُرجع `400`. التفاصيل: `docs/audit/50-d11-branchless-reads-20261117.md`.
- **العمل:** `FluentValidation` أو `IValidator<T>` موحّد؛ `ProblemDetails` مع `code`, `field`, `message` عربي؛ ربط أخطاء الواجهة.
- **DoD:** كل 400 بنفس الشكل؛ لا رسائل متفرقة — **متحقق للشكل** (حارس مصدري + اختبارات
  مسار كامل)؛ المدقق الموحّد بند مستقل.

#### `P4-04` — OpenAPI + عميل TS مولّد

- **الحالة (2026-11-17):** **جزئيًا — أساس العقد مثبت.** PR
  [`#196`](https://github.com/maarouf003-netizen/docgen/pull/196): أُضيف `operationId` حتمي
  (`Controller_Action`) لكل عملية، ولقطة عقد مُطبَّعة في
  `docs/openapi/docgen-v1.json` (239 عملية) يتحقق منها اختبار `OpenApiContractTests`
  عند كل تشغيل (تطابق اللقطة + `operationId` فريد + معاملات مسار معلنة + مخطط
  `Bearer`). أي تغيير في سطح `API` يفشل حتى يُحدَّث العقد واعيًا بأمر موثّق. التفاصيل:
  `docs/audit/54-p4-04-openapi-contract-20261117.md`.
  **متبقٍ:** توليد `frontend/src/api/generated.ts` من اللقطة (`openapi-typescript`)،
  إحلاله تدريجيًا محل `types/document.ts`، وإصدار `API` (`/api/v1` أو
  `Accept-Version` + سياسة إهمال) — `P6-06`/`P4-05`.
- **الجزء الثاني (2026-11-22):** إظهار عقد استجابة القوائم — ستة إجراءات
  (`Search`/`Deleted`/`StruckOff`/`Executed`/`ReferredToStart`/`StrikeOffReminders`) صارت
  `ActionResult<PagedResult<DocumentResponse>>`، فأُضيفت مخططات `DocumentResponse`
  و`DocumentResponsePagedResult` و`ExecutionActionDto` و`DocumentAssignmentDto`
  و`DocumentInterventionDto` و`DocumentOccurrenceDto` إلى اللقطة والعميل المولَّد —
  **إضافة بلا حذف ولا تغيير سلوك**. التفاصيل:
  `docs/audit/97-p4-04b-openapi-response-schemas-20261122.md`.
- **العمل:** تفعيل OpenAPI في staging/prod محمي؛ `openapi-typescript`/NSwag لتوليد `frontend/src/api/generated.ts`؛ حذف `types/document.ts` اليدوي تدريجيًا؛ contract tests؛ **إصدار API** (`/api/v1` أو `Accept-Version` + سياسة إهمال).
- **DoD:** تغيير API يكسر التوليد/الاختبارات، لا الواجهة بصمت؛ لا كسر بلا إصدار.
- **الاعتمادية:** `P4-03` (شكل أخطاء/عقود) و`P6-06`.

#### `P4-05` — حدود Modular Monolith

- **الحالة (2026-11-16):** **مؤجل** — كما كُتب يتطلب إعادة هيكلة مساحات أسماء إلى وحدات
  (`Documents`/`Interventions`/… ) عبر مئات الملفات، وهو تغيير أكبر من جولة آمنة واحدة
  ولا يمكن اختباره بحدود مفيدة قبل الانتهاء. الخطة: فرع `refactor/modules-*` مستقل بعد
  تثبيت `P4-02` (تفكيك الخدمات) وبوابة المعمارية الحالية (`P1-06`) كشبكة أمان.
- **العمل:** مساحات أسماء `Documents`, `Interventions`, `Delegations`, `Appeals`, `Entities`, `Alerts`, `Identity`, `Portal`؛ منع cross-module `DbSet`؛ architecture tests.
- **DoD:** حدود واضحة قابلة للفصل مستقبلًا.

#### `P4-06` — أدوات الجودة

- **الحالة (2026-11-17):** **مغلق** — PR
  [`#170`](https://github.com/maarouf003-netizen/docgen/pull/170) (squash `1884aef`): منفّذ:
  - `backend/Directory.Packages.props` (CPM): 24 حزمة بإصدار مركزي، وأُزيل `Version` من
    35 مرجعًا في 6 مشاريع؛ CPM يفرض نفسه (`NU1008` عند إصدار محلي).
  - `global.json`: SDK `10.0.100` مع `rollForward: latestFeature`.
  - `.editorconfig`: تنسيق موحّد.
  - **`P4-06b` (2026-11-16):** `packages.lock.json` لسبعة مشاريع + `--locked-mode` في
    وظائف `backend`/`backend-postgres`/`migration-guard`/`audit`/`flake-sentinel`؛ أي
    انحراف يفشل بـ`NU1004` (تحقق سلبي موثّق). التفاصيل:
    `docs/audit/30-p4-06b-lockfiles-20261116.md`.
  - **`P4-06c` (2026-11-17):** PR
    [`#189`](https://github.com/maarouf003-netizen/docgen/pull/189) — بوابة `dotnet format --verify-no-changes` مثبتة في وظيفة
    `backend` بعد البناء. القياس الأول: 69/721 ملفًا (BOM/سطر أخير/تقسيم تهيئات/إزاحة
    عبارات المستوى الأعلى، بلا تغيير دلالي) ⇒ طُبِّع الأساس مرة واحدة (68 ملفًا تنسيقيًا)
    وأُضيف `.gitattributes` (`eol=lf`) و`end_of_line = lf` لثبات النتيجة بين المنصّات.
    التفاصيل: `docs/audit/47-p4-06-format-gate-20261117.md`.
  **متبقٍ:** Roslynator/StyleCop — تقييم ضجيج مستقل قبل تفعيلهما (لم يُضافا هنا عمدًا).
- **العمل:** `Directory.Packages.props` (CPM)، `packages.lock.json`، `global.json`، `.editorconfig`، `dotnet format --verify-no-changes`، Roslynator/StyleCop (بعد تقييم الضجيج).
- **DoD:** إصدارات موحدة ونمط تنسيق حاجب — متحقق (البوابة ترفض أي انحراف).

#### `P4-07` — ADRs/CODEOWNERS/PR template

- **الحالة (2026-11-17):** **مغلق** — PR
  [`#197`](https://github.com/maarouf003-netizen/docgen/pull/197): `ADR-0001`…`ADR-0008` موثّقة ومربوطة من
  [`docs/README.md`](README.md)، وأُضيف `.github/CODEOWNERS` و
  `.github/pull_request_template.md` (قائمة بوابات + هجرات + أمان + واجهة + وثائق)،
  وسياسة حدّ حجم الـ`PR` (≤~400 سطر/20 ملفًا) في `CONTRIBUTING.md`. التفاصيل:
  `docs/audit/55-p7-01-governance-20261117.md`.
- **العمل:** `docs/adr/0001-postgres-only.md`، `0002-transactions-idempotency.md`، `0003-authz-policies.md`…؛ `CODEOWNERS`؛ قالب PR؛ حد لحجم PR.
- **DoD:** كل قرار كبير موثّق — متحقق.

---

### P5 — الرصد والتشغيل

#### `P5-01` — OpenTelemetry

- **العمل:** `OpenTelemetry.Extensions.Hosting` + AspNetCore/EFCore/Http/Runtime؛ OTLP exporter (Grafana/Tempo)؛ `ActivitySource` للأوامر؛ correlation id؛ Serilog enricher.
- **DoD:** trace واحد من الواجهة إلى SQL، وmetrics لكل نقطة.

#### `P5-02` — لوحات وتنبيهات وSLOs

- **العمل:** طلبات/أخطاء/زمن، زمن SQL، outbox lag، retry count، pool، CPU/RAM؛ SLO 99.5%؛ تنبيهات بريد/Telegram.
- **DoD:** لوحة واحدة + تنبيهات مفعّلة.

#### `P5-03` — Health checks

- **الحالة (2026-11-15):** مغلق — PR
  [`#165`](https://github.com/maarouf003-netizen/docgen/pull/165) (squash `6e70795`)، والتفاصيل في
  [`docs/audit/26-p5-03-health-checks-20261115.md`](audit/26-p5-03-health-checks-20261115.md):
  فصل liveness/readiness:
  - `GET /healthz` → حياة العملية فقط (`Predicate = _ => false`، بلا قاعدة).
  - `GET /readyz` → القاعدة + صندوق الصادر (وسم `ready`)؛ `503` عند تأخر أقدم رسالة
    معلّقة فوق `HeadAlerts:Outbox:Health:LagThresholdMinutes` (افتراضي 15).
  - `GET /healthz/outbox` → صندوق الصادر وحده (وسمان `ready`+`outbox`؛ `Degraded` عند
    رسائل فاشلة، بلا إسقاط الجهوزية).
  - سياسة التقييم في `OutboxHealthPolicy` (نقية، 7 اختبارات وحدوية)، والمهايئ في
    `HeadAlertOutboxHealthCheck`؛ والردّ النصّي الافتراضي لا يكشف تفاصيل استثناء.
  - `render.yaml`: `healthCheckPath: /readyz` (كان `/healthz`)، ومزامنة `RUN_GUIDE.md §9`
    مع تنبيه أن `⁦/healthz⁩` ليس فحص جهوزية.
- **العمل:** `/healthz` liveness بلا DB؛ `/readyz` DB + outbox lag؛ `/healthz/outbox`؛ فشل readiness عند lag > عتبة.
- **DoD:** Render health check يستخدم `/readyz`.

#### `P5-04` — Staging/Canary/Rollback

- **العمل:** خدمة staging + قاعدة منفصلة؛ canary 10% → 50% → 100%؛ feature flags؛ rollback مُختبَر.
- **DoD:** نشر بلا توقف وتراجع خلال دقائق.

#### `P5-05` — خط أنابيب الهجرات

- **الحالة (2026-11-16):** جزئيًا — الجزء الثابت منفّذ: `scripts/migration-safety-guard.mjs`
  في بوابة `guards` يفشل عند أي `DropTable/DropColumn/DropSchema/Rename*/AlterColumn<T>/
  DeleteData` داخل **`Up`** بلا استثناء موثّق في `scripts/migration-safety-allowlist.json`
  (مع سبب)، ويتجاهل `Down` عمدًا، ويفشل عند استثناء قديم. تحقق سلبي مزدوج موثّق (هجرة
  تجريبية فشلت، واستثناء موثّق نجح، ثم نُظِّف). التفاصيل:
  `docs/audit/33-p5-05a-migration-safety-20261116.md`.
  **متبقٍ:** dry-run على staging، وإلزام نسخة احتياطية موثّقة قبل كل تطبيق (`P5-04`).
  - **P5-05b (2026-11-16):** كل تشغيل `migration-guard` يولّد سكربت `PostgreSQL`
    idempotent ويرفعه كأثر `migrations-sql` (30 يومًا)، وأُضيفت قائمة مراجعة SQL في
    `RUN_GUIDE.md §9`. التفاصيل: `docs/audit/34-p5-05b-ddl-artifact-20261116.md`.
- **العمل:** توليد SQL، مراجعة، dry-run، نسخة احتياطية، `expand/contract`، `CONCURRENTLY`، منع الحذف في نفس الإصدار.
- **DoD:** لا هجرة تُطبق يدويًا بلا سجل/مراجعة.
- **الاعتمادية:** `P0-02` (Baseline) و`P5-04` (staging).

#### `P5-06` — نسخ احتياطي وDR

- **العمل:** نسخ يومي + PITR (Supabase)؛ تشفير النسخ؛ استعادة شهرية إلى staging؛ قياس `RTO/RPO`؛ توثيق؛ اختبار استعادة جزئية (جدول واحد) واستعادة كاملة.
- **DoD:** استعادة ناجحة موثّقة خلال آخر 30 يومًا، وRTO/RPO معلنان.

#### `P5-07` — Runbooks/Postmortems

- **الحالة (2026-11-22):** **مغلق** — runbooks التشغيل موجودة ومفهرسة
  (`deploy`, `rollback`, `migrations`, `database-down`, `auth-outage`, `outbox-stuck`,
  `secret-rotation`, `load-testing`)، وكلها تتبع: الأعراض → التشخيص → الإجراء →
  التحقق → التصعيد. أُضيف قالب postmortem بلا لوم
  (`docs/runbooks/postmortem-template.md`) مع الإجراءات التصحيحية والمالك والتاريخ.
  التفاصيل: `docs/audit/101-p5-07-runbooks-postmortem-20261122.md`.
- **العمل:** deploy/rollback، migration، outbox stuck، DB down، auth outage، secret rotation؛ postmortem بلا لوم.
- **`DoD`:** كل حادثة محتملة لها runbook — **متحقق** (8 runbooks + قالب postmortem).

---

### P6 — الواجهة الأمامية

#### `P6-01` — Server state وError boundaries

- **العمل:** TanStack Query بدل `useEffect` اليدوي؛ axios instance موحّد؛ error boundaries؛ retry/backoff للقراءات.
- **DoD:** لا تكرار fetch، ولا شاشة بيضاء عند خطأ.

#### `P6-02` — تفكيك النماذج والعروض

- **العمل:** `DocumentForm.tsx` 1521 → أقسام/hooks؛ `DocumentView.tsx` 947 → بطاقات؛ `ApplicantSideSections` 952؛ schema موحّد (react-hook-form + zod أو المكافئ).
- **DoD:** لا ملف >600 سطر، واختبارات كل قسم.

#### `P6-03` — Design system وإمكانية الوصول

- **العمل:** tokens، مكونات مشتركة، حالات focus، WCAG 2.2 AA، `aria-label`، `prefers-reduced-motion`، تباين، mobile-first (`sm:`/`md:`/`lg:`)، أهداف لمس 44px.
- **DoD:** axe بلا أخطاء حرجة على الشاشات الرئيسية، وفحص 375px بلا تجاوز أفقي.

#### `P6-04` — E2E/Visual/A11y

- **العمل:** Playwright للتدفقات الحرجة (دخول/إنشاء/تعديل/متدخل/حالة/إنابة/استئناف/بوابة/إدارة)؛ لقطات بصرية؛ axe.
- **DoD:** E2E حاجب في CI على staging.

#### `P6-05` — ميزانية الأداء والحزمة

- **الحالة (2026-11-22):** **جزئيًا — حارس الحزمة مثبت، وقياس الويب الحي متبقٍ.**
  - `vite.config.ts` يكتب `dist/bundle-report.json` (Plugin داخلي بلا اعتمادية)،
    و`npm run budget` يفرض: إجمالي ≤2.0MB، أكبر حزمة ≤460kB، الدخول ≤300kB.
  - القياس: `JS=91` · إجمالي **1725.2kB** · أكبر **388.2kB** · دخول **249.3kB**.
  - خطوة `Bundle budget` في `CI` (وظيفة `frontend`) بعد البناء؛ تحقق سلبي موثّق.
  - المسارات lazy والصور بأبعاد/`alt` كانت مكتملة مسبقًا.
  - **متبقٍ:** `LCP/INP/CLS` عبر Lighthouse/staging، و`content-visibility` بعد قياس INP.
  - التفاصيل: `docs/audit/99-p6-05-bundle-budget-20261122.md`.
- **العمل:** `rollup-plugin-visualizer`؛ حد للحزمة؛ lazy routes؛ صور بأبعاد؛ `content-visibility`.
- **`DoD`:** LCP/INP/CLS ضمن الميزانية — **جزئي** (الحجم محروس؛ القياس الحي لاحقًا).

#### `P6-06` — عميل API مولّد

- **الحالة (2026-11-18):** **جزئيًا — التوليد وحارس الانحراف مثبتان، وأول نوع مُحال.**
  - `RequiredByNullabilitySchemaFilter` + `SupportNonNullableReferenceTypes` في
    `AddSwaggerGen`: يشتق `required` من nullability C# (أنواع القيم وسجلات `record`
    كانت تُصدَّر اختيارية فيُضعف النوع المولَّد). أُعيد توليد لقطة `OpenAPI`.
  - `openapi-typescript@7.13.0` (devDependency) + `npm run generate:api` ينتج
    `frontend/src/api/generated.ts` (≈271KB، 239 عملية).
  - **حارس انحراف في `CI`:** خطوة توليد ثم `git diff --exit-code` — أي تغيير API بلا
    إعادة توليد يفشل.
  - **أول إحلال:** `DashboardStatsDto` في `frontend/src/types/management.ts` صار
    `components['schemas']['DashboardStatsDto']` (تطابق حقلًا بحقل).
  - **التثبيت:** `CI`/`Dockerfile` يستخدمان `npm ci --legacy-peer-deps` لأن
    `openapi-typescript` يعلن peer `typescript ^5.x` والمشروع على 6؛ التحقق الحقيقي هو
    `tsc -b` (يمر). أُضيف `@testing-library/dom` صراحةً كي لا يحذفه تثبيت الـpeer legacy.
  - **الجزء الثاني (2026-11-21):** PR
    [`#244`](https://github.com/maarouf003-netizen/docgen/pull/244) — إحالة
    `DocumentAnnexDto` إلى النوع المولَّد
    (`components['schemas']['DocumentAnnexDto']`) وإزالة تعريفه اليدوي؛ `sequence` صار
    مطلوبًا (مطابق `Sequence = 0` الخلفي) وحُدِّثت حمولات النموذج/الاختبارات. `tsc` و
    `oxlint` و`vitest` (1808/1808) و`build` خضراء، وحارس التوليد لا انحراف.
    التفاصيل: `docs/audit/83-p6-06-generated-annex-20261121.md`.
  - **الجزء الثالث (2026-11-21):** PR
    [`#245`](https://github.com/maarouf003-netizen/docgen/pull/245) — إحالة `HeirDto`
    إلى المولَّد وتوسيع مساعدات العرض/
    النماذج لتقبل `string | null` (25 خطأ `tsc` أُصلحت من الجذر بلا `as`/`any`)؛
    `vitest` 1808/1808 و`build` ناجح. التفاصيل:
    `docs/audit/84-p6-06-generated-heir-20261121.md`.
  - **الجزء الرابع (2026-11-21):** PR
    [`#246`](https://github.com/maarouf003-netizen/docgen/pull/246) — إحالة
    `ExecutedHeirDto` إلى المولَّد وتوسيع `heirOption`
    في `appealOptions` ليقبل `string | null` (خطآن `tsc` فقط)؛ `vitest` 1808/1808.
    التفاصيل: `docs/audit/85-p6-06-generated-executed-heir-20261121.md`.
  - **الجزء الخامس (2026-11-21):** PR
    [`#247`](https://github.com/maarouf003-netizen/docgen/pull/247) — إحالة
    `ExecutedNaturalPersonDto` إلى المولَّد —
    **صفر أخطاء `tsc`** بفضل nullability الجذرية في الجزء الثالث؛ `vitest` 1808/1808.
    التفاصيل: `docs/audit/86-p6-06-generated-executed-natural-20261121.md`.
  - **الجزء السادس (2026-11-21):** دفعة واحدة لإحالة `GuarantorDto`/`ExecutedPublicEntityDto`/
    `ApplicantPublicEntityDto`/`BaseNumberEntry` مع تضييق `PartyNature`/`EntityNature`؛
    `vitest` 1808/1808. وبروتوكول تسريع الجولات في `RUN_GUIDE §6` (PR واحد لكل بند،
    ولا مصفوفة خلفية لـ`frontend/` فقط). التفاصيل:
    `docs/audit/88-p6-06-batch-types-20261121.md`.
  - **الجزء السابع (2026-11-21):** إحالة `AssetDto` (المولَّد يطابق `DocumentPartyDtos.cs`
    بما فيه `owners: string[] | null` و`id: number | null`) و`ExecutionApplicantDto`
    (`Omit` + تضييق `nature` إلى `PartyNature | null`)؛ أُصلحت nullability من الجذر
    (39 خطأ `tsc` أوليًا، صفر `as`/`any`)، وأُصلح فحص `id != null` في مساري اختيار الأموال؛
    `vitest` 1808/1808. التفاصيل: `docs/audit/90-p6-06-asset-applicant-20261121.md`.
  - **الجزء الثامن (2026-11-21):** إحالة عقود الطلبات المولَّدة
    (`DocumentUpsertRequest`, `DocumentInterventionUpsertRequest`, `InitialActionRequest`
    كـ`AddExecutionActionRequest`) مع تضييق `PartyNature`/`GeneralEntitySide`/`InterventionNature`؛
    حُذف `applicantRegistryId` الوهمي (غير موجود خلفيًا ولا مستخدم)، وأُصلحت nullability
    (`trimNull`, `NamedInput`, القوائم المطلوبة)؛ `vitest` 1808/1808. التفاصيل:
    `docs/audit/91-p6-06-request-types-20261121.md`.
  - **الجزء التاسع (2026-11-22):** `P4-04b` أظهر `DocumentResponse` و
    `DocumentInterventionDto` في مخطط `OpenAPI` المولَّد — صار إحلالهما نظريًا ممكنًا
    (`components['schemas']['DocumentResponse']`)، لكن الإحلال نفسه مؤجّل: يحمل
    `displayFileNumber`/`needsRotation`/`hasAppeals` المشتقة وتواريخ `DateTime`
    في الاستجابة، فيحتاج قرارًا حول `undefined`/`null` في الواجهة.
  - **الجزء العاشر (2026-11-22):** إحالة `ExecutionActionDto` و`DocumentAssignmentDto`
    و`DocumentInterventionDto` و`DocumentOccurrenceDto` و`SaveBaseNumbersRequest`
    و`CorrectBaseNumberRequest` إلى المولَّد مع تضييق الاتحادات؛ أُصلحت 5 أخطاء
    `tsc` من الجذر (تواريخ `formatDate`/`formatDateTime` تقبل `null`، `TextRow`،
    `version` في حمولة اختبار)؛ `vitest` 1808/1808. التفاصيل:
    `docs/audit/98-p6-06-generated-response-types-20261122.md`.
  - **متبقٍ:** `DocumentResponse` (**مخططه صار متاحًا**، والإحلال يحتاج قرار nullability)،
    وأنواع بلا مخططات (`DocumentFilterOptionsDto`, `RotationDocumentDto`,
    `BaseNumberHistoryDto`) تبقى يدوية موثّقة. ومراجعة أي نوع يستخدم `undefined`
    مقابل `null`، وتثبيت إصدار المولّد عند ترقية كبرى.
  - التفاصيل: `docs/audit/61-p6-06-generated-types-20261118.md`.
- **العمل:** استبدال `types/document.ts` (797 سطرًا) بأنواع مولّدة؛ `openapi-typescript`؛ CI يفشل عند الانحراف.
- **DoD:** عقد واحد بين الخلفية والواجهة — **جزئي** (نوع واحد + الحارس؛ البقية لاحقًا).
- **الاعتمادية:** `P4-04`.

#### `P6-07` — استراتيجية اختبار الواجهة

- **الحالة (2026-11-17):** **مدمج في `P6-04`** — 1808 اختبار `vitest` تغطي الوحدات والتكامل؛
  الفجوة الحقيقية هي `E2E`/`A11y` وهي `P6-04`. لا عمل مستقل.
- **العمل:** RTL للوحدات، MSW للتكامل، Playwright للـE2E؛ تغطية المسارات الحرجة.
- **DoD:** لا انحدار واجهة بلا اختبار.

---

### P7 — التوثيق والحوكمة

#### `P7-01` — فهرس الوثائق وADRs/Runbooks

- **الحالة (2026-11-17):** **مغلق** — PR
  [`#197`](https://github.com/maarouf003-netizen/docgen/pull/197): `docs/README.md` (بداية/ADRs/لقطات/خطط/Runbooks/OpenAPI)،
  `docs/runbooks/` (نشر/تراجع/هجرات/قاعدة/مصادقة/outbox/تدوير أسرار)، `SECURITY.md`،
  `CONTRIBUTING.md`، `.github/CODEOWNERS`، وقالب `PR`. التفاصيل:
  `docs/audit/55-p7-01-governance-20261117.md`.
- **العمل:** `docs/README.md`؛ `docs/adr/`؛ `docs/runbooks/`؛ `SECURITY.md`؛ `CONTRIBUTING.md`؛ `CODEOWNERS`.
- **DoD:** أي قارئ يجد القرار/الإجراء في دقيقة — متحقق.

#### `P7-02` — أتمتة قائمة الهجرات

- **الحالة (2026-11-22):** **مكتمل — التاريخ الفعلي محروس.**
  - `scripts/migration-history-check.mjs` يقارن ملفات `MigrationsPostgres`
    بـ`__EFMigrationsHistory` عبر `dotnet ef migrations list`، ويفشل عند هجرة معلّقة
    أو انحراف (ملف بلا صف/صف بلا ملف/غياب سلسلة).
  - `CI` (`backend-postgres`) يطبّق الهجرات على `docgen_test` ثم يشغّل السكربت.
  - تحقق سلبي موثّق: قاعدة بالـBaseline فقط ⇒ فشل بـ**6** هجرات معلّقة؛ القاعدة الكاملة
    ⇒ **7/7 نظيف**. وصُحّح `RUN_GUIDE §9` («صف واحد» القديم ← **7 صفوف**).
  - التفاصيل: `docs/audit/96-p7-02-migration-history-20261122.md`.
- **العمل:** سكربت يقارن `Migrations` بـ`__EFMigrationsHistory` وينتج قائمة معلّقة؛ CI يفشل عند هجرة غير مطبقة في staging.
- **`DoD`:** لا نسيان نشر — **متحقق لقاعدة جديدة/CI**؛ فحص `staging` الحي يبقى ضمن `P5-04`.

#### `P7-03` — Release checklist/DoD

- **الحالة (2026-11-17):** **مغلق توثيقيًا** — PR
  [`#202`](https://github.com/maarouf003-netizen/docgen/pull/202) — [`docs/RELEASE_CHECKLIST.md`](RELEASE_CHECKLIST.md):
  قبل النافذة (البوابات الثماني، `CHANGELOG`، نسخة احتياطية/استعادة، الهجرات وpreflight،
  متغيّرات `P2-03`، الاعتماديات، خطة التراجع)، أثناءها (سجل الهجرات ثم التطبيق ثم النشر)،
  بعدها (health/ready/outbox، الرؤوس والكوكيز، فحص دخاني، السجلات)، معايير التراجع،
  وحقول التوقيع. `canary` يبقى ضمن `P5-04` (يحتاج staging).
- **العمل:** قائمة قبل كل إصدار: gates، migrations، backup، canary، rollback، changelog.
- **DoD:** لا إصدار بلا توقيع القائمة — متحقق توثيقيًا؛ التنفيذ الفعلي مسؤولية المالك.

#### `P7-04` — سياسة الاعتماديات

- **الحالة (2026-11-17):** **جزئيًا** — PR
  [`#202`](https://github.com/maarouf003-netizen/docgen/pull/202): `.github/dependabot.yml` يفعّل تحديثات أسبوعية
  مجمّعة (NuGet في `/backend`، npm في `/frontend` بالتقسيم prod/dev، وGitHub Actions)،
  مع `open-pull-requests-limit` و`versioning-strategy: increase` للـnpm. بوابات
  `dependency-audit`/`npm-audit-guard`/`dependency-review` تبقى الحكم على الثغرات.
  **متبقٍ (إعداد مستودع/مالك):** تفعيل **Dependabot security updates/alerts** من
  Settings → Code security، ومراجعة أول دفعة PRs يدويًا (خصوصًا `packages.lock.json`).
- **العمل:** Renovate/Dependabot أسبوعيًا؛ lock files؛ مراجعة أمنية؛ حد للإصدارات.
- **DoD:** لا اعتماديات قديمة حرجة.

#### `P7-05` — صيانة CI (تحذيرات Node 20 وUbuntu 26)

- **الحالة (2026-11-22):** **منفّذ** — `ubuntu-24.04` مثبتة في الوظائف التسع،
  والإجراءات رُقّيت إلى `checkout@v7`/`setup-dotnet@v6`/`setup-node@v7`/
  `upload-artifact@v7`/`codeql-action@v4`/`dependency-review@v5` (‏`Node 24`)،
  وصورة `backend-postgres` رُفعت من `15` إلى **`16`** لمطابقة القياس/الإنتاج
  (تصحيح تناقض موثّق في `docs/audit/95`).
- **العمل:** ترقية `actions/checkout@v4` و`actions/setup-dotnet@v4` و`actions/setup-node@v4`
  إلى إصدارات لا تعتمد Node 20 (Node 24)؛ وتثبيت صورة `runs-on` صريحة بدل `ubuntu-latest`
  قبل تحولها إلى Ubuntu 26؛ ومراجعة صورة `postgres` في `backend-postgres`.
- **`DoD`:** لا تحذيرات إهمال في سجلات CI، وبيئة تشغيل/صورة مثبتة الإصدار — **متحقق**
  (التحقق النهائي على بوابات الـ`PR`).

---

## 5. الموجات التنفيذية

### الموجة 0 — أسبوع 1 (تثبيت الأساس)

1. دمج `#138` ثم `#135` ثم `#137` ثم `#136` (بعد إعادة التأسيس على `#138`).
2. `ADR-0001: PostgreSQL-only` + `ADR-0002: transactions/idempotency`.
3. قياس خط الأساس: زمن الاختبارات، زمن الاستعلامات، أخطاء الإنتاج.
4. `P1-01` (منصة Postgres) + `P1-02` (حتمية) — بداية.
5. `P5-01` (OTel skeleton) + `P5-03` (health).

### الموجة 1 — أسابيع 2–4 (المعاملات والبيانات)

`P0-01` → `P0-02` → `P0-03` → `P0-04`، مع `P1-01`/`P1-02`/`P4-06`.

### الموجة 2 — أسابيع 5–8 (الأمان والقراءة)

`P0-05`–`P0-08`، `P1-03`–`P1-07`، `P2-01`–`P2-03`، `P3-01`–`P3-04`.

### الموجة 3 — أسابيع 9–12 (الأداء والواجهة)

`P2-04`–`P2-09`، `P3-05`–`P3-08`، `P4-01`–`P4-05`، `P5-02`/`P5-04`/`P5-05`.

### الموجة 4 — أسابيع 13–16 (التشغيل والصقل)

`P5-06`/`P5-07`، `P6`، `P7`، ومراجعة شاملة ضد `DoD` النهائي.

---

## 6. Definition of Done النهائي (معيار «المثالية»)

1. `PostgreSQL` فقط؛ Baseline واحد؛ صفر هجرات معلّقة؛ نشر expand/contract بلا توقف.
2. كل كتابة idempotent أو versioned؛ اختبار حقن فشل يثبت عدم التكرار/الفقدان.
3. كل نقطة محمية بسياسة موردة؛ مصفوفة صلاحيات 100% مُختبَرة.
4. اختبارات حتمية على Postgres؛ flake < 0.1%؛ وحدات < 5 دقائق؛ mutation score ≥60% للحرج.
5. SLOs معلنة ومُراقبة؛ `p95` للقراءات < 500ms وللكتابات < 1s في staging.
6. لا `Seq Scan` على جدول كبير في استعلام ساخن؛ لا N+1.
7. OpenTelemetry traces/metrics/logs + لوحات + تنبيهات + outbox lag.
8. SAST/DAST/SBOM/secrets/dependency gates خضراء.
9. staging + canary + rollback + backup/PITR + DR drill موثّق.
10. E2E/axe/visual/bundle budgets للواجهة.
11. لا ملف > 600 سطر في التطبيق (باستثناء الهجرات)؛ لا `WithStandardIncludes` للقوائم.
12. ADRs/runbooks/CODEOWNERS/PR template محدّثة؛ لا خطط قديمة متعارضة.

### 6.1 قرار نطاق (2026-11-17) — ما يُغلق «المثالية» وما يُتجاوز

> اعتمده المالك بعد جرد 60 بندًا: **18 منجزة، 10 جزئية، 1 مؤجّل، 31 لم تبدأ** قبل هذه الجولة؛
> وبعد إغلاق `P4-07`/`P7-01`: **20 منجزة، 9 جزئية، 1 مؤجّل، 30 لم تبدأ**.
> الغرض تثبيت معنى «منتهية» ومنع بقاء الخطة مفتوحة بسبب بنود غير حرجة.

**متجاوز نهائيًا (لن يُنفَّذ):**

- `P1-05` **Mutation testing** — التغطية الحالية (وحدات + تكامل + حراس مصدرية) تغطي المخاطر
  العملية؛ كلفته عالية بلا أثر إنتاجي. يُعاد النظر فقط إن انخفضت جودة الاختبارات.
- `Roslynator`/`StyleCop` (تحت `P4-06`) — ضجيج كبير مقابل قيمة تنسيقية؛ `dotnet format` بوابة كافية.
- `P6-07` — **مدمج في `P6-04`** (اختبارات الوحدات قائمة؛ الفجوة هي `E2E`).
- `P4-05` **Modular Monolith** — يبقى مؤجلًا بقرار موثّق؛ لا قيمة تشغيلية قبل `P4-02`.

**مؤجّل حتى القياس (`P3-01`):** `P3-02` · `P3-03` · `P3-04` · `P3-05` · `P3-06` · `P6-05`.
لا تُبنى تحسينات أداء/تخزين مؤقت قبل أرقام `PostgreSQL/Release`.

**لا يُتجاوز (خطر حقيقي):** `P1-01`/`P1-08` (اختبار Postgres حقيقي)، `P2-04` (جلسات)،
`P2-06` (SAST على الأقل)، `P2-07` (PII)، `P5-06` (نسخ/`DR`)، وبقايا `P2-03`/`P3-09`/`P5-05`/`P7-02`
(كلها تعتمد `staging`)، وتطبيق الهجرات الست المعلّقة (منها هجرتا `P3-02`: `pg_trgm` + الفهرس).

### 6.2 بوابات الإغلاق الثلاث

1. **جاهز للإنتاج (إلزامي):** الهجرات الست المعلّقة مطبّقة + بروفة `P2-03` على `staging` +
   `P5-06` (نسخ/استعادة) + `P2-06` SAST + `P2-07` + `P1-01`/`P1-08`.
2. **مُقاس ومُراقب:** `P3-01` ثم فقط ما تثبته الأرقام + `P5-01`/`P5-02`/`P5-04`.
3. **صقل:** `P6-02`/`P6-03`/`P6-04`/`P6-06`، `P7-03`/`P7-04`/`P7-05`، وبقايا `P4-01`/`P4-02` حسب الحاجة.

«المثالية» كما في §6 لا تُعلن منتهية قبل البوابة 1 على الأقل في بيئة إنتاج فعلية؛ البوابة 3 تحسين مستمر.

---

## 7. المخاطر والتراجع

| المخاطرة | التخفيف | التراجع |
|---|---|---|
| سحق الهجرات | نسخة `pg_dump` + قاعدة تجريبية + اختبار استعادة | إعادة الهجرات القديمة من Git |
| إزالة SQLite | إبقاء فرع مؤقت للاختبارات حتى `P1-08` | إعادة الحزمة/الفرع من Git |
| Command pipeline | strangler تدريجي + feature flag | إبقاء المسار القديم حتى اكتمال البند |
| idempotency | TTL + تنظيف + اختبارات تزامن | تعطيل الفلتر عبر إعداد |
| retry | لا تفعيل قبل `P0-03` + حقن فشل | `NonRetrying` الافتراضي |
| تفكيك الخدمات | اختبارات عقد + PRs صغيرة | إعادة الدمج من Git |
| أمان أشد | طرح تدريجي + استثناءات موثقة | إعداد أمني قابل للتراجع |
| تكلفة staging/observability | خطة مجانية/محدودة أولًا | الرجوع لخدمة واحدة |

---

## 8. المراجعة النقدية للـPRs `#135`–`#138`

### 8.1 المنهج

- مراجعة diff كل فرع مقابل `master@de652ff`.
- محاكاة دمج الفروع الأربعة على فرع مؤقت: **دمج نظيف بلا تعارض**.
- بناء `-warnaserror` + اختبارات مستهدفة على الحالة المدموجة: **12 Application + 9 Api خضراء**.
- فحص CI لكل PR: البوابات الست خضراء بعد إعادة تشغيل الفحص العابر في `#135`.

### 8.2 النتائج

| البند | الخطورة | الملاحظة | القرار |
|---|---|---|---|
| `F-02` — `SaveChanges` الداخلي | متوسطة | `TransactionRunner` يحمي الـdelegate، لكن `SaveChanges` يستخدم إستراتيجية السياق العامة؛ `EnableRetryOnFailure` قد يعيد دفعة كتابة داخل المعاملة | موثّق في العقد؛ يُعالج جذريًا في `P0-03` |
| `F-02` — outbox | منخفضة | `HeadAlertOutboxStore` أصبح non-retrying؛ لا تعارض مع الوثائق | مقبول |
| `F-01` — موضع `DocumentAccessInfo` | منخفضة | في `Common` لا `DTOs`؛ إضافة عضو لواجهة `IDocumentService` (لا implementers خارجية) | مقبول |
| `F-01` — مصفوفة الأدوار | منخفضة | نقاط المتدخلين `lawyer-only`؛ المصفوفة الكاملة مغطاة في مسارات GET القائمة | مقبول |
| `F-03` — `EnsureCreated`/SQLite/Debug | متوسطة | 26.8s ليس زمن إنتاج؛ القياس على Postgres/Release في `P3-01` | موثّق |
| `F-03` — اسم الفهرس | منخفضة | الاعتماد على `IX_Guarantors_DocumentId` مقصود كحارس بنيوي | مقبول |
| `#138` — flake | منخفضة | `int.Parse` يفترض أرقامًا؛ كل استدعاءات الملف رقمية | مقبول |

### 8.3 الحكم

**لا توجد عيوب حاجبة** في `#135`–`#138`. الثلاثة الأصلية تحقق أهدافها، والرابع يزيل flake قائمًا. تبقى نقطتان مؤجلتان بقرار معماري واضح: المعاملة/retry الداخلي (`P0-03`)، وقياس Postgres/Release (`P3-01`).

> **الترتيب المقترح للدمج:** `#138` ← `#135` ← `#137` ← `#136`، ثم إعادة تأسيس الفروع الثلاثة على `master` بعد دمج `#138` (أو دمج `#138` أولًا وإعادة تشغيل CI).

---

## 9. أول 10 PRs في المحادثة الجديدة

| # | الفرع المقترح | البند | المخرجات |
|---|---|---|---|
| 1 | `refactor/postgres-only` | `P0-01` | ADR + إزالة مسار SQLite الإنتاجي |
| 2 | `refactor/postgres-baseline` | `P0-02` | Baseline واحد + إعادة ضبط القاعدة التجريبية |
| 3 | `test/postgres-harness` | `P1-01` | Testcontainers/CI + Respawn |
| 4 | `test/determinism-guards` | `P1-02` | حظر `GetHashCode`/`DateTime.Now` + 20 تشغيلًا |
| 5 | `feat/transaction-aware-strategy` | `P0-03` | إستراتيجية واعية + نطاق لكل محاولة |
| 6 | `feat/idempotency-everywhere` | `P0-04` | فلتر + جدول + اختبارات تزامن |
| 7 | `refactor/authz-policies` | `P2-01` | Policy handlers + إزالة فحوص الأدوار |
| 8 | `feat/generic-outbox` | `P0-05` | Outbox عام + dispatcher |
| 9 | `perf/portal-search-postgres` | `P3-01`/`P3-02` | قياس Postgres + `pg_trgm`/`tsvector` |
| 10 | `obs/opentelemetry` | `P5-01` | traces/metrics/logs + `/readyz` |

---

## 10. أوامر التحقق الموحدة

```powershell
dotnet build backend/DocGenerator.sln --nologo -v q -warnaserror
dotnet test backend/tests/DocGenerator.Architecture.Tests/DocGenerator.Architecture.Tests.csproj --nologo -v q
dotnet test backend/tests/DocGenerator.Application.Tests/DocGenerator.Application.Tests.csproj --nologo -v q
dotnet test backend/tests/DocGenerator.Api.Tests/DocGenerator.Api.Tests.csproj --nologo -v q --filter "Category!=Sequential"
dotnet test backend/tests/DocGenerator.Api.Tests/DocGenerator.Api.Tests.csproj --nologo -v q --filter "Category=Sequential"

dotnet ef migrations has-pending-model-changes --context DocGeneratorPostgresDbContext --project backend/src/DocGenerator.Infrastructure --startup-project backend/src/DocGenerator.Api
dotnet ef migrations script --idempotent --context DocGeneratorPostgresDbContext --project backend/src/DocGenerator.Infrastructure --startup-project backend/src/DocGenerator.Api

cd frontend
npx oxlint src scripts
npx tsc -b
npx vitest run
npm run build
npm run guard
```

> لا تُشغّل أوامر الاتصال بقاعدة الإنتاج داخل CI بلا بيئة staging/نسخة احتياطية. لا تُطبع سلاسل الاتصال في السجلات.

---

## 11. المراجعة النقدية للخطة نفسها (قيود وافتراضات)

هذه الخطة طموحة، و«المثالية» لا تُبلغ بضربة واحدة. القيود التالية مقصودة ومعلنة:

| الافتراض/الخطر | لماذا قد يفشل | التخفيف |
|---|---|---|
| سحق الهجرات | وجود بيانات لم تُكتشف، أو اعتماد خارجي على `__EFMigrationsHistory` | نسخة `pg_dump` + جرد أعداد + قاعدة تجريبية + إعادة بناء مرتين |
| `PostgreSQL` فقط | مطور بلا Docker/قاعدة محلية | `docker compose` موحد أو قاعدة تطوير مشتركة؛ لا رجوع إلى SQLite في الإنتاج |
| Testcontainers | Docker غير متاح على بعض الأجهزة/CI | خدمة Postgres في CI + `DOCGEN_TEST_POSTGRES` كبديل محلي |
| Command pipeline | تفكيك واسع قد يجمّد الميزات | strangler تدريجي، feature flag، وإبقاء المسار القديم حتى تكافؤ الاختبارات |
| إضافة `Version` لكل كيان | هجرات/بيانات موجودة | ضمن Baseline + expand/contract مستقبلًا |
| `pg_trgm`/`tsvector` | سلوك بحث عربي/إنجليزي مختلط، أو خطة أسوأ | قياس A/B على Postgres قبل الاعتماد، والاحتفاظ بالمسار القديم خلف flag |
| `OpenTelemetry`/staging مدفوع | تكلفة | بداية مجانية (Render staging صغير + OTLP مجاني)، ثم توسيع |
| MFA/refresh tokens | تعقيد UX ودعم | طرح تدريجي للمدير/المشرف أولًا، مع استثناءات موثقة |
| «لا ملف >600 سطر» | قد يفرض تقسيمًا مصطنعًا | استثناء موثّق للملفات المولدة/المعقدة، والهدف تقليل التعقيد لا الرقم |
| نسخة Render واحدة | لا توزيع/قفل موزع | لا تفعّل cache/outbox متعدد النسخ قبل قفل موزع (Postgres advisory lock أو Redis) |
| لا بيانات حقيقية الآن | قد تتغير لاحقًا | كل هجرة مستقبلية expand/contract + backup + staging |

**ما لا تغطيه الخطة:** تطبيق جوال أصلي، تعدد لغات، أو فصل microservices. هذه ليست أهدافًا قبل استقرار الأساس.

**شرط بدء المحادثة الجديدة:** اقرأ هذا الملف، تحقق من حالة `master` و`#135`–`#138`، ثم ابدأ بالموجة 1 بندًا واحدًا في PR واحد. لا تبدأ بندًا معتمدًا قبل اكتمال اعتماديته.

---

## 12. المراجع

- `docs/CODE_HEALTH_REVIEW.md` — نطاق `C-01..C-11`.
- `docs/CODE_HEALTH_FOLLOWUPS.md` — `F-01..F-03` وحالاتها.
- `RUN_GUIDE.md` §9 — هجرات النشر.
- `docs/audit/00-PROJECT-CONTEXT.md` — السياق والقرارات.
- `AGENTS.md` — قواعد التواريخ/الواجهة/الفرع/الهجرات.

---

**نهاية الخطة — المرجع التنفيذي للرحلة.**
