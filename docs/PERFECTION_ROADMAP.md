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
  - **`P2-01c‑2`** (2026-11-17): الستة الأخيرة (`Appeals`, `Correspondences`,
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
  - SBOM (CycloneDX 6.3.1 للخلفية، `@cyclonedx/cyclonedx-npm` للواجهة) يُرفع كأثر
    `sbom` في وظيفة `audit` (30 يومًا).
  **المتبقي (البند الكامل):** CodeQL/Semgrep (SAST)، DAST، Trivy للصورة، وبوابة
  `Snyk`/OSS Index إن رغب المالك بتوفير `SNYK_TOKEN`. التفاصيل:
  `docs/audit/28-p2-06-dependency-gate-20261115.md`.
- **العمل:** CodeQL/Semgrep، Snyk/OSS Index، `dotnet list package --vulnerable`، `npm audit`، Trivy للصورة، CycloneDX SBOM؛ بوابات حاجبة للحرج.
- **DoD:** كل PR يفحص الكود والاعتماديات والصورة.

#### `P2-07` — تصنيف PII وتنقيح السجلات

- **العمل:** جرد PII (الاسم/الرقم الوطني/العنوان/الهاتف)؛ تنقيح السجلات؛ صلاحيات وصول؛ احتفاظ؛ تشفير عند الراحة (Supabase) وعبر TLS.
- **DoD:** لا PII في السجلات العامة؛ وصول مدقق.

#### `P2-08` — حدود المعدل ومكافحة التخمين

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

- **العمل:** harness على Postgres/Release مع 10k/50k؛ `pg_stat_statements`، `EXPLAIN (ANALYZE, BUFFERS, FORMAT JSON)`، p50/p95؛ تخزين النتائج.
- **DoD:** قياس موثّق لكل استعلام ساخن؛ لا أرقام من `SQLite/Debug` كحكم إنتاجي.

#### `P3-02` — إعادة كتابة بحث البوابة

- **العمل:** `pg_trgm` GIN على `SearchText`/الأسماء، أو `tsvector` مولّد؛ `UNION ALL` لمجموعات المرشحين بدل `OR`؛ قياس قبل/بعد.
- **DoD:** `p95 < 500ms` على 10k في staging، مع نتيجة صحيحة وعزل تام.

#### `P3-03` — تدقيق الفهارس

- **العمل:** `pg_stat_user_indexes`/`pg_stat_statements`؛ إضافة الناقص، حذف غير المستخدم، `CREATE INDEX CONCURRENTLY`.
- **DoD:** لا `Seq Scan` على جدول كبير في استعلام ساخن.

#### `P3-04` — Read models وإسقاطات

- **العمل:** `DocumentListItemDto`، `DocumentDetailDto`، `PortalFileDetailDto`؛ إزالة `WithStandardIncludes` من القوائم؛ `AsNoTracking` و`Select` فقط.
- **DoD:** 20 `Include` تصبح ≤5، وزمن القائمة يتحسن.
- **الاعتمادية:** `P3-01` (قياس قبل/بعد) و`P1-01` (اختبارات Postgres).

#### `P3-05` — Keyset pagination

- **العمل:** `(CreatedAt, Id)` للقوائم الكبيرة؛ cursor موقّع؛ إبقاء offset للإداريات الصغيرة.
- **DoD:** لا `Skip/Take` على الجداول الكبيرة.

#### `P3-06` — Caching

- **العمل:** `HybridCache` للبيانات المرجعية (الفروع/الشعب/الجهات)؛ invalidation عند الكتابة؛ لا caching لبيانات الصلاحيات بلا مفتاح.
- **DoD:** hit ratio مرصود، ولا بيانات قديمة.

#### `P3-07` — Load tests وSLOs

- **العمل:** k6/NBomber على staging؛ سيناريوهات القراءة/الكتابة/البوابة؛ SLO `p95` و`error budget`.
- **DoD:** تقرير حمل قبل كل إصدار كبير.

#### `P3-08` — حراسة N+1

- **الحالة (2026-11-16):** جزئيًا — PR
  [`#171`](https://github.com/maarouf003-netizen/docgen/pull/171) (squash `fc2224b`): منفّذ:
  - `QueryCounter` (DbCommandInterceptor) في `Api.Tests` يُحقن في `DbContext` الاختباري
    عبر `TestDatabase.UseSqlite`، ويعدّ كل الأوامر لكل طلب.
  - `QueryBudgetTests`: قائمة 15 صفًا بسقف 22 (القياس 18) وتفاصيل صف واحد بسقف 20 —
    العدد لا ينمو مع الصفوف، فأي N+1 يضيف ≥15 استعلامًا ويفشل فورًا.
  - **P3-08b (2026-11-16):** تصدير Excel لـ15 صفًا بسقف **14** (القياس 10) — يغطي مسار
    `WithStandardIncludes` الأثقل. التفاصيل: `docs/audit/35-p3-08b-export-budget-20261116.md`.
  **متبقٍ:** سقوف سجل التغييرات/إحصاءات الدوائر/البوابة، وقياس `PostgreSQL` الحقيقي (`P3-01`). التفاصيل: `docs/audit/31-p3-08-query-budget-20261116.md`.
- **العمل:** `DbCommandInterceptor` في الاختبارات يعدّ الاستعلامات لكل طلب؛ سقف لكل نقطة؛ `AsSplitQuery` عند الحاجة.
- **DoD:** لا انحدار استعلامات.

#### `P3-09` — ضبط الاتصال والـPool

- **العمل:** مراجعة `Npgsql` (`MaxPoolSize`، `Timeout`، `CommandTimeout`، `KeepAlive`، `Maximum Pool Size` مع حدود Render/Supabase)؛ health check بمهلة؛ منع استنزاف الـpool أثناء التقارير/التصدير.
- **DoD:** لا `Timeout` عشوائي تحت الحمل؛ قياس pool utilization.

---

### P4 — المعمارية والصيانة

#### `P4-01` — Command/Query pipeline

- **العمل:** `ICommandHandler<TCommand,TResult>` و`IQueryHandler`؛ Behaviors: Validation, Authorization, Idempotency, Transaction, Audit, Outbox؛ المتحكمات رقيقة.
- **DoD:** كل نقطة كتابة = handler واحد؛ لا `_tx.RunAsync` مكرر في المتحكم.
- **الاعتمادية:** `P0-03`، `P0-04`، `P2-01`.

#### `P4-02` — تفكيك الخدمات الضخمة

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
- **العمل:** `FluentValidation` أو `IValidator<T>` موحّد؛ `ProblemDetails` مع `code`, `field`, `message` عربي؛ ربط أخطاء الواجهة.
- **DoD:** كل 400 بنفس الشكل؛ لا رسائل متفرقة.

#### `P4-04` — OpenAPI + عميل TS مولّد

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

- **الحالة (2026-11-16):** جزئيًا — PR
  [`#170`](https://github.com/maarouf003-netizen/docgen/pull/170) (squash `1884aef`): منفّذ:
  - `backend/Directory.Packages.props` (CPM): 24 حزمة بإصدار مركزي، وأُزيل `Version` من
    35 مرجعًا في 6 مشاريع؛ CPM يفرض نفسه (`NU1008` عند إصدار محلي).
  - `global.json`: SDK `10.0.100` مع `rollForward: latestFeature`.
  - `.editorconfig`: تنسيق موحّد (إرشادي، بلا بوابة `dotnet format` في هذه الجولة).
  - **`P4-06b` (2026-11-16):** `packages.lock.json` لسبعة مشاريع + `--locked-mode` في
    وظائف `backend`/`backend-postgres`/`migration-guard`/`audit`/`flake-sentinel`؛ أي
    انحراف يفشل بـ`NU1004` (تحقق سلبي موثّق). التفاصيل:
    `docs/audit/30-p4-06b-lockfiles-20261116.md`.
  **متبقٍ:** بوابة `dotnet format --verify-no-changes` (ديون تنسيق قائمة ⇒ تجنّب PR
  إعادة تنسيق ضخم)، وRoslynator/StyleCop (تقييم ضجيج أولًا).
- **العمل:** `Directory.Packages.props` (CPM)، `packages.lock.json`، `global.json`، `.editorconfig`، `dotnet format --verify-no-changes`، Roslynator/StyleCop (بعد تقييم الضجيج).
- **DoD:** إصدارات موحدة ونمط تنسيق حاجب.

#### `P4-07` — ADRs/CODEOWNERS/PR template

- **العمل:** `docs/adr/0001-postgres-only.md`، `0002-transactions-idempotency.md`، `0003-authz-policies.md`…؛ `CODEOWNERS`؛ قالب PR؛ حد لحجم PR.
- **DoD:** كل قرار كبير موثّق.

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

- **العمل:** deploy/rollback، migration، outbox stuck، DB down، auth outage، secret rotation؛ postmortem بلا لوم.
- **DoD:** كل حادثة محتملة لها runbook.

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

- **العمل:** `rollup-plugin-visualizer`؛ حد للحزمة؛ lazy routes؛ صور بأبعاد؛ `content-visibility`.
- **DoD:** LCP/INP/CLS ضمن الميزانية.

#### `P6-06` — عميل API مولّد

- **العمل:** استبدال `types/document.ts` (797 سطرًا) بأنواع مولّدة؛ `openapi-typescript`؛ CI يفشل عند الانحراف.
- **DoD:** عقد واحد بين الخلفية والواجهة.
- **الاعتمادية:** `P4-04`.

#### `P6-07` — استراتيجية اختبار الواجهة

- **العمل:** RTL للوحدات، MSW للتكامل، Playwright للـE2E؛ تغطية المسارات الحرجة.
- **DoD:** لا انحدار واجهة بلا اختبار.

---

### P7 — التوثيق والحوكمة

#### `P7-01` — فهرس الوثائق وADRs/Runbooks

- **العمل:** `docs/README.md`؛ `docs/adr/`؛ `docs/runbooks/`؛ `SECURITY.md`؛ `CONTRIBUTING.md`؛ `CODEOWNERS`.
- **DoD:** أي قارئ يجد القرار/الإجراء في دقيقة.

#### `P7-02` — أتمتة قائمة الهجرات

- **الحالة (2026-11-15):** جزئيًا — PR
  [`#164`](https://github.com/maarouf003-netizen/docgen/pull/164) (squash `cf93c6e`): الجزء **الثابت** منفّذ:
  `scripts/migration-watchlist-guard.mjs` يُشغَّل في بوابة `guards`، ويفشل إذا وُجدت
  هجرة في `MigrationsPostgres` غير مذكورة بالاسم في `RUN_GUIDE.md` §9 («هجرات بانتظار
  التطبيق») **و**`AGENTS.md` («تذكير الهجرات المعلّقة») — ويفشل أيضًا إذا اختفى أحد
  القسمين. تحقق سلبي موثّق: هجرة تجريبية غير مسجّلة أفشلت الحارس في الموضعين ثم حُذفت.
  **المتبقي:** مقارنة `__EFMigrationsHistory` الفعلية بقائمة الملفات (يتطلب قاعدة
  staging في CI — يُنفَّذ مع `P1-01`/`P5-04`).
- **العمل:** سكربت يقارن `Migrations` بـ`__EFMigrationsHistory` وينتج قائمة معلّقة؛ CI يفشل عند هجرة غير مطبقة في staging.
- **DoD:** لا نسيان نشر.

#### `P7-03` — Release checklist/DoD

- **العمل:** قائمة قبل كل إصدار: gates، migrations، backup، canary، rollback، changelog.
- **DoD:** لا إصدار بلا توقيع القائمة.

#### `P7-04` — سياسة الاعتماديات

- **العمل:** Renovate/Dependabot أسبوعيًا؛ lock files؛ مراجعة أمنية؛ حد للإصدارات.
- **DoD:** لا اعتماديات قديمة حرجة.

#### `P7-05` — صيانة CI (تحذيرات Node 20 وUbuntu 26)

- **العمل:** ترقية `actions/checkout@v4` و`actions/setup-dotnet@v4` و`actions/setup-node@v4`
  إلى إصدارات لا تعتمد Node 20 (Node 24)؛ وتثبيت صورة `runs-on` صريحة بدل `ubuntu-latest`
  قبل تحولها إلى Ubuntu 26؛ ومراجعة صورة `postgres` في `backend-postgres`.
- **DoD:** لا تحذيرات إهمال في سجلات CI، وبيئة تشغيل/صورة مثبتة الإصدار.

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
