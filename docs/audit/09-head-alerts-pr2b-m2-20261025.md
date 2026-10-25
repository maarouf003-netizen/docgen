# PR‑2b — Head alerts: ترحيل مواقع الكتابة + M2 (Backfill) + A3 (المراجع المفقودة)

> **الفرع:** `feature/head-alerts-pr2b-migrate-sites`
> **التاريخ:** 2026‑10‑25
> **الالتزامات:** `fd4747b` (M2) ← `45fe78a` (Outbox) ← `ffdfb1b` (A3) ← توثيق.
> **الهدف:** إكمال المرحلة `M2` وترحيل كل الكتابات النظامية إلى `Outbox`، مع معالجة المراجع
> المفقودة وقت المعالجة (`A3`) وحارس معماري يمنع العودة إلى الإنشاء المباشر.
> **المرجع:** `docs/HEAD_ALERTS_RESTRUCTURE_PLAN.md` (§6.5، §8.1، §14.1، §16) و
> `docs/audit/07-head-alert-migration-matrix-20261020.md`.

---

## ⚠️ قبل الدمج — أطول ثلاثة أسطر في هذا الـPR

1. **6 هجرات معلّقة** (3 لكل سياق) تُطبَّق **قبل** نشر النسخة بالترتيب:
   `HeadAlertTaxonomy_Expand` ← `HeadAlertOutbox_Create` ← `HeadAlertTaxonomy_Backfill`
   (وفي PostgreSQL: `HeadAlertTaxonomy_Expand_Pg` ← `HeadAlertOutbox_Create_Pg` ←
   `HeadAlertTaxonomy_Backfill_Pg`).
   ```powershell
   dotnet ef database update --context DocGeneratorDbContext
   dotnet ef database update --context DocGeneratorPostgresDbContext
   ```
2. **`Database:AutoMigrate=false` في الإنتاج ⇒ التطبيق يرفض الإقلاع** إن لم تُطبَّق الهجرات.
   **طبّق الهجرات قبل نشر النسخة، وإلا انقطاع إقلاع مقصود** (أفضل من `no such column` عند أول طلب).
3. **نسخة احتياطية + استعادة مجرَّبة** قبل النافذة. هجرة `M2` في `SQLite` **تعيد بناء جدول
   `HeadAlerts`** (وحدة `rebuild` في EF) لحذف فهرس قديم وإضافة الفهارس الجديدة؛ والتراجع
   (`Down`) يعيد **المخطط** فقط ولا يفكّ تصنيف الصفوف أو دمج المستلمين.

---

## ما الذي يغيّره هذا الـPR؟

| | قبل | بعد |
|---|---|---|
| **كتابة التنبيهات النظامية** | `IHeadAlertService.CreateAsync` داخل معاملات الخدمات + `catch` يبتلع الفشل | `IHeadAlertOutbox.Enqueue` داخل المعاملة نفسها، والمعالجة في عامل مستقل |
| **معالجة المراجع المحذوفة** | استثناء عام → إعادة حتى `MaxAttempts` ثم `failed` | `skipped: missing_reference` بلا إعادة (فحص مسبق + تصنيف خرق FK) |
| **مستلم التعاقب غير الفعّال** | قد يلتبس بـ`no_recipients` | `recipient_inactive` عند تجاوز تعاقب حقيقي فقط |
| **مخطط `HeadAlerts`** | `Kind`/`DedupeKey` بلا فهرس فريد | تصنيف كامل + مفتاح منع + فهرس فريد جزئي + قيد مستلم فريد |
| **حارس معماري** | لا يوجد | فحص `IL` يمنع `new HeadAlert`/`AddAsync`/`CreateSystemAsync` خارج المالكين |

**⇒ لا تغيير في عقد الـAPI الخارجي ولا في الواجهة.** الكتابة العامة (`POST /api/alerts`)
تبقى كما هي؛ التغيير في المسارات النظامية الداخلية فقط.

---

## 1) M2 — تصنيف البيانات القديمة وفرض القيود

الهجرة `HeadAlertTaxonomy_Backfill` (SQLite) و`HeadAlertTaxonomy_Backfill_Pg` (PostgreSQL):

- **الصفوف القديمة فقط** (`Kind IS NULL OR TRIM(Kind) = ''`) تصبح `legacy`، ويُضبط
  `LastContentChangedAt = COALESCE(LastContentChangedAt, CreatedAt)`. الصفوف المصنّفة
  مسبقًا لا يُمسّ نوعها ولا نصّها ولا حالتها.
- **تصنيف التدفقات المفتوحة** وفق §6.5:
  - الإنابة: `delegation_pending_approval`، `delegation_sent_to_subhead`،
    `delegation_pending_completion`، وتنبيهات منشئ الملف الثلاثة.
  - الاستئناف: `appeal_referral_to_head` وغيره من الحالات الخمس.
  - كتاب المطالعة: `review_letter_reply`.
  - المعلقات القديمة: تُربط بدائرة التنفيذ من المستند أو من اسم الدائرة في النص، وتُصنَّف
    `circuit_pending_documents`.
  - التنبيه اليدوي، والنص المطابق على `TargetType` خاطئ، والصفوف التي تعذّر بناء مفتاحها:
    تبقى `legacy` (بلا `DedupeKey`) — لا تخمين.
- **حل تصادم `DedupeKey`**: حامل واحد لكل مفتاح (الأحدث `LastContentChangedAt` ثم
  `CreatedAt` ثم `Id`)، ودمج قراءة المستلمين (`MAX(IsRead)` و`MIN(ReadAt)`)، وأرشفة
  الخاسرين (`Kind='legacy'` و`DedupeKey=NULL`).
- **دمج تكرارات `(HeadAlertId, UserId)`** قبل إنشاء القيد الفريد، بالسياسة نفسها.
- **الفهارس والقيود**: `UX_HeadAlerts_DedupeKey_Active` فريد جزئي
  (`DedupeKey IS NOT NULL AND IsArchived = 0`)، و`UX_HeadAlertRecipients_HeadAlertId_UserId`،
  وفهارس القوائم/النوع/الدائرة. فهرس `IX_HeadAlerts_TargetLawyerId` القديم يُستبدل بـ
  `IX_HeadAlerts_TargetLawyerId_Kind_IsArchived`.
- **`Down`**: يعيد فهرس `IX_HeadAlerts_TargetLawyerId` ويحذف الفهارس الجديدة؛ **لا يعكس
  البيانات** (التصنيف والدمج والأرشفة تبقى) — لذلك النسخة الاحتياطية شرط نشر.

**حدود مقصودة:** `Kind` يبقى `nullable` وبلا قيد `CHECK`؛ فرضه غير القابل للفراغ هو
`HeadAlertTaxonomy_Contract` في **PR‑3**، وليس هنا.

---

## 2) ترحيل مواقع الكتابة إلى Outbox

- كل نقاط الكتابة النظامية (23 استدعاء `CreateSystemHeadAlertRequest` في 7 خدمات و8 ملفات
  مصدر) صارت تُسجّل رسالة Outbox **داخل معاملة العمل الأصلية** بدل نداء الخدمة المباشر:
  `DocumentDelegationService` (+`Mirror`)، `DocumentAppealService`، `ReviewLetterService`،
  `PublicEntityService` (+`Branches`/`Moves`/`Groups`/`Merge`/`Registry`/`Unify`)،
  `ExecutionCircuitService`، و`HeadAlertSuccessionService`.
- اختفت مسارات `catch` التي كانت تبتلع فشل التنبيه (`head_alert_failed`) من مسارات الكتابة
  النظامية؛ الفشل أصبح حالة رسالة صريحة (`failed`/`skipped`) يراقبها المشغّل.
- النداءات المباشرة المتبقية على `IHeadAlertService` هي **الأرشفة فقط**
  (`ArchiveByDelegationAsync`/`ArchiveByAppealAsync`)، وهي ليست كتابة تنبيه جديد.
- `HeadAlertSuccessionService` يستهلك `IHeadAlertOutboxStore.UpdateTargetUserForPendingAsync`
  لتحديث `TargetUserId` للرسائل المعلّقة فقط.

---

## 3) A3 — المراجع المفقودة وقت المعالجة

الفحص يتم **قبل الإدراج** في `CreateSystemAsync` عبر
`IHeadAlertRepository.FindMissingReferenceAsync`، ويغطي سبعة أنواع:
الفرع، الملف، الإنابة، الاستئناف، كتاب المطالعة، الجهة العامة، دائرة التنفيذ.
وجود المرجع فقط هو ما يُفحص (لا حالة التفعيل)؛ والملفات المحذوفة حذفًا ناعمًا تُعدّ مفقودة
لأن المرشّحات العامة تُسقطها.

| الحالة | الاستثناء | نتيجة المشغّل | إعادة؟ |
|---|---|---|---|
| مرجع محذوف/غير صالح قبل الإدراج | `HeadAlertMissingReferenceException` | `skipped: missing_reference` | لا |
| مرجع حُذف بين الفحص والإدراج (خرق FK) | `DbUpdateException` يصنّفه `IsForeignKeyViolation` | `skipped: missing_reference` | لا |
| مستلم صريح غير موجود/غير فعّال | `HeadAlertNoRecipientsException` | `skipped: no_recipients` | لا |
| **خلف تعاقب** غير موجود/غير فعّال | `HeadAlertRecipientInactiveException` | `skipped: recipient_inactive` | لا |
| مستخدم غير فعّال في بثّ | لا مستلمون مؤهّلون | `skipped: no_recipients` | لا |

**التمييز الحرج:** المشغّل يمرّر `TargetUserId` دائمًا للجمهور الأحادي، فوجوده وحده لا يعني
«تعاقبًا». لذلك:
- `HeadAlertOutboxStore` يحدّث **`TargetUserId` فقط** ولا يعيد كتابة مستلم الطلب الأصلي.
- الخدمة تعتبر التجاوز حقيقيًا فقط إذا اختلف `TargetUserId` عن
  `request.RecipientUserId`/`request.TargetLawyerId`.
- بهذا يُسجَّل تعذّر الخلف `recipient_inactive`، ويبقى المستلم الصريح المفقود
  `no_recipients` — بلا تخمين وبلا خلط.

`DbExceptionClassifier.IsForeignKeyViolation` يعرف:
`SqliteException` (19/787) و`PostgresException` (`23503`)، عبر سلسلة الاستثناءات كاملة.

---

## 4) الحارس المعماري

`HeadAlertArchitectureTests.HeadAlert_NoDirectConstructionOutsideService` يفحص **الـIL
المترجم** (لا النص المصدري) لتجميعات `Domain`/`Application`/`Infrastructure`/`Api`:

- يمنع `new HeadAlert` و`IHeadAlertRepository.AddAsync` و`IHeadAlertService.CreateSystemAsync`
  خارج `HeadAlertService` و`HeadAlertOutboxDispatcher`.
- لا يعتمد على مسارات الملفات ولا يضيف حزمًا؛ والتجميعات الاختبارية مستثناة عمدًا.
- يثبت أن الفحص رأى المسارات المشروعة فعلًا (إنشاء/إضافة/معالجة)، لا أنه مرّ على IL فارغ.

---

## 5) مصفوفة اختبارات الهجرة (19 اختبارًا)

`HeadAlertSchemaMigrationTests` — كلها تمر:

| الاختبار | ما يثبته |
|---|---|
| `Migration_Expand_OldCodeStillWrites` | الكود القديم يكتب بعد `M1` بلا `Kind`/`LastContentChangedAt` |
| `Migration_Expand_PreservesExistingRowsAndWidensMessage` | `Expand` لا يفقد الصفوف ويوسّع النص |
| `Outbox_Schema_TableIndexesAndConstraintsExist` | جدول Outbox وفهارسه وقيوده الخمسة |
| `Outbox_Constraints_RejectInvalidValues` | رفض الحالات/المحاولات غير الصالحة |
| `Outbox_ValidMessageRoundTrips_WithLowercaseStatus` | حفظ/قراءة الحمولة والحالة بأحرف صغيرة |
| `Outbox_Down_RefusesToDropTableWithUnresolvedMessages` | `Down` يرفض الإسقاط مع رسائل غير محسومة |
| `Migration_Backfill_LegacyOnlyDoesNotOverwriteNewRows` | `M2` لا يمسّ صفوف `PR‑2` المصنّفة |
| `Migration_DuplicateRecipientRows_MergedBeforeUniqueConstraint` | دمج `(HeadAlertId, UserId)` قبل القيد |
| `Migration_DuplicateDedupeKeyCollision_ResolvedBeforeUniqueIndex` | حامل واحد وأرشفة الخاسرين |
| `Migration_PartialUniqueIndex_BlocksDuplicateActiveKey_AllowsAfterArchive` | الفهرس الجزئي يسمح بالتكرار بعد الأرشفة |
| `Migration_Backfill_SetsLastContentChangedAndCircuit` | ضبط زمن المحتوى وربط الدائرة |
| `Migration_LegacyDelegationAppealReview_ClassifiedWithKeys` | تصنيف الإنابة/الاستئناف/كتاب المطالعة ومفاتيحها |
| `Migration_LegacyDocumentCreator_ClassifiedOrExcludedManual` | تصنيف منشئ الملف واستثناء اليدوي/الهدف الخاطئ |
| `Migration_Backfill_Down_RestoresPreBackfillIndexes` | `Down` يعيد فهرس `TargetLawyerId` |
| `Migration_LegacyBackfill_SingleActiveRowPerStream` | صف نشط واحد لكل تيار بعد الحل |
| `Migration_LegacyPendingPrefix_AllClassifiedOrReported` | لا بادئة معلقات نشطة خارج التصنيف أو تقرير §16 |
| `Migration_Sqlite_RebuildPreservesRowsIndexesAndForeignKeys` | إعادة بناء SQLite تحفظ الصفوف/المستلمين/الفهارس/FK |
| `Migration_Sqlite_IntegrityAndFingerprint_PostRebuild` | `integrity_check`/`foreign_key_check` وبصمة دلالية ثابتة |
| `Migration_Backfill_RerunIsIdempotent` | إعادة أوامر البيانات لا تغيّر أي صف أو مستلم |

**إضافة:** 8 اختبارات مرجعية في `HeadAlertRepositoryReferenceTests` لكل نوع مرجع، واختبارات
A3 في `HeadAlertOutboxDispatcherTests`/`HeadAlertSystemWriteTests`، واختبار الحارس المعماري.

---

## 6) البوابات (كلها خضراء)

| البوابة | النتيجة |
|---|---|
| `dotnet test backend/DocGenerator.sln` | ✔ **Application 2055/2055** · **Api 690/690** |
| `npx oxlint src` · `npx tsc -b` | ✔ 0/0 |
| `npx vitest run` | ✔ **1599/1599** (152 ملفًا) |
| `npm run build` | ✔ |
| بناء الالتزامات المعزولة (M2 + Outbox) | ✔ 0 Warning / 0 Error |
| `dotnet ef migrations script` (SQLite + PostgreSQL) | ✔ سكربتان كاملان بلا أخطاء |
| بناء الحل كاملًا | ✔ 0 Warning / 0 Error |

**سكربتا التحقق** (مولّدان محليًا وغير مضمومين إلى Git لأن `backend/build-verify/` مستثنى):
`full-sqlite.sql` (197,072 بايت) و`full-postgres.sql` (101,687 بايت)، وكلاهما يحتوي
هجرات `M2` والفهارس المتوقعة.

---

## 7) الانحرافات والقيود الموثّقة

1. **التحقق الحي على PostgreSQL مؤجَّل إلى بيئة النشر (Render):** لا يوجد PostgreSQL محلي،
   فتم التحقق من SQL عبر `dotnet ef migrations script` باتصال وهمي. يلزم تشغيل الهجرة
   والفحوص على قاعدة حقيقية قبل الاعتماد النهائي.
2. **`HeadAlertTaxonomy_Contract` ليس هنا:** يبقى في PR‑3 لأنه يفرض `Kind` غير فارغ وقيد
   `CHECK` بعد أن تصبح كل الصفوف مصنّفة.
3. **`Down` لا يعكس البيانات:** يعيد المخطط فقط؛ أي تراجع بعد `M2` يتطلب نسخة احتياطية
   صالحة (وهذا مذكور في §16 من الخطة).
4. **إعادة بناء SQLite مقصودة:** الجدول يُعاد بناؤه لحذف `IX_HeadAlerts_TargetLawyerId`
   وإضافة الفهرس المركّب؛ الاختبارات تثبت حفظ الصفوف والمستلمين و`foreign_key_check`.
5. **تسمية اختبارين:** فُصل اختبار منشئ الملف عن تدفقات الإنابة/الاستئناف/كتاب المطالعة
   ليبقى الاسم الملزم في الخطة
   (`Migration_LegacyDocumentCreator_ClassifiedOrExcludedManual`) موجودًا، مع اختبار مستقل
   للتدفقات (`Migration_LegacyDelegationAppealReview_ClassifiedWithKeys`).

---

## 8) مراجعة المسار الكامل — ما تم تصحيحه

- **ازدواج دلالة `TargetUserId`:** كان وجوده يُفسَّر تعاقبًا حتى للمستلم الأصلي؛ صُحّح
  بجعله مقارنةً بمستلم الطلب، وبإبقاء مستلم الطلب دون إعادة كتابة في المخزن.
- **سباق حذف المرجع:** الفحص المسبق وحده لا يكفي؛ أُضيف تصنيف خرق المفتاح الأجنبي
  (`SQLite 19/787` و`PostgreSQL 23503`) كشبكة أمان إلى `skipped: missing_reference`.
- **ازدواج XML doc في `IHeadAlertRepository`:** `<summary>` مكرّر فوق
  `ListActivePendingByLawyerAndKindAsync` صُحّح.
- **اختبار إعادة البناء:** كان يحفظ `Document` و`DocumentDelegation` في `SaveChanges`
  واحد فيفشل FK قبل الهجرة؛ فُصل الحفظان ليختبر الهجرة لا ترتيب البذور.
- **تغطية الحالات الحدّية:** مرجع مفقود لكل نوع، مستند محذوف ناعمًا، خلف غير فعّال،
  مستلم صريح مفقود، إعادة تشغيل البيانات، وتكرار المستلمين/المفاتيح.

---

## 9) قائمة النشر (بالترتيب)

1. **نسخة احتياطية** من قاعدة الإنتاج (SQLite: نسخ الملف؛ PostgreSQL: `pg_dump`) مع
   استعادة تجريبية.
2. **تطبيق الهجرات الست** بالأمرين أعلاه، والتأكد من `__EFMigrationsHistory` أن الثلاثة
   لكل سياق مسجّلة.
3. **فحص ما بعد الهجرة:**
   ```sql
   -- SQLite
   PRAGMA integrity_check;
   PRAGMA foreign_key_check;
   ```
   وفي PostgreSQL: التحقق من وجود `UX_HeadAlerts_DedupeKey_Active` و
   `UX_HeadAlertRecipients_HeadAlertId_UserId` وعدم وجود قيم `Kind` فارغة نشطة.
4. **نشر كود PR‑2b** (بعد الهجرات، لا قبلها).
5. **مراقبة المشغّل** أول 24 ساعة: عدّادات `processed`/`skipped`/`failed`، وخصوصًا
   `missing_reference` و`recipient_inactive`.
6. **التحقق الحي على PostgreSQL** (المؤجَّل) قبل إغلاق نافذة النشر.

---

## 10) ما ليس في هذا الـPR

- **PR‑3:** `HeadAlertTaxonomy_Contract(_Pg)` — فرض `Kind` غير فارغ وقيد `CHECK`.
- **PR‑4:** عقد الـAPI النهائي وواجهة العرض.
- **PR‑5:** الواجهة (تُنشر مع PR‑4 في نافذة واحدة).
- **PR‑6:** إزالة الجسر الانتقالي. الجسر ما زال معطّلًا:
  `HeadAlerts:LegacyBridge:Enabled=false`.

---

## المراجع

- `docs/HEAD_ALERTS_RESTRUCTURE_PLAN.md` — §6.3، §6.5، §8.1، §14.1، §16.
- `docs/audit/07-head-alert-migration-matrix-20261020.md` — مصفوفة مواقع الكتابة الـ26.
- `docs/audit/08-head-alerts-pr1-pr2a-description.md` — تقرير PR‑1/PR‑2a السابق.
- `RUN_GUIDE.md` §9 — قائمة الهجرات المعلّقة وأوامر التطبيق.
