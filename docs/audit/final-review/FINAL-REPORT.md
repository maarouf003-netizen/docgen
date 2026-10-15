# المراجعة النهائية العدائية — `FINAL-REPORT.md`

> **القرار: `NO-GO` (غير جاهز للنشر) — حاجب واحد فقط، سريع الإصلاح: `FR-001`.**
> لا عجز بنيوي عام: عدا `FR-001`، الشجرة خضراء البوابات ومعزولة البوابة الخارجية.
> يُرفع إلى `GO` بعد إصلاح `FR-001` وإتمام بوابات التحقق وإقرار المستخدم بأن قاعدة الإنتاج لم تُطبِّق الهجرة المعطوبة.

- التاريخ: 2026-10-15
- الفرع: `master` (شجرة العمل غير نظيفة — راجع `FR-014`)
- المنهج: قراءة الكود الفعلي + توليد سكربت هجرات `PostgreSQL` فعلي + تشغيل بوابات البناء والاختبار.
- القاعدة: لا ادّعاء بلا دليل؛ ما لم يُتحقَّق يرد في قسم «لم يُراجَع» صراحةً.

---

## 1. جدول الملاحظات (مرتّب بالخطورة)

| المعرّف | الخطورة | الموقع (`file:line`) | الأثر | الحالة |
|---|---|---|---|---|
| `FR-001` | **`Blocker`** | `backend/src/DocGenerator.Infrastructure/Persistence/MigrationsPostgres/20260928082313_AddPersonalRemindersPg.cs:24,27` | فشل سلسلة هجرات `PostgreSQL` عند الإقلاع على أي قاعدة جديدة/غير مطبَّقة | مؤكَّد |
| `FR-002` | `High (إجرائي)` | `.github/workflows/ci.yml` + `backend/tests/.../DatabaseInitializerTests.cs:430-432` + `.../UtcDateTimeConverterTests.cs:167-169` | مسار هجرات `PostgreSQL` **غير مُختبر في `CI`** (اختبارات البوابة تُتخطّى بصمت) — وهو ما سمح بمرور `FR-001` | مؤكَّد |
| `FR-003` | `Low (كامن)` | `backend/src/.../Persistence/StatisticsRepository.cs:67-70` + `.../DTOs/StatsDtos.cs` (`DashboardStatsDto`) | `totalAmount`/`totalCollectedAmount` يجمع عملات مختلفة في رقم واحد (نقطة `GET /api/dashboard` — غير مستهلكة من الواجهة) | مؤكَّد (نقطة قديمة) |
| `FR-004` | `Low (كامن)` | `StatisticsRepository.cs:47-55` مقابل `DocumentRepository.cs:232-259` | `ExecSubStatus == null` يُحتسب «منفَّذًا كاملًا» في القوائم لكنه **مستبعد من الاثنين** في إحصاء `dashboard` (منطق `SQL` ثلاثي) | مؤكَّد |
| `FR-005` | `Low` | `DocumentService.cs:606-608` + `DigitNormalizer.cs:12-17` + فلترا الفهرسين الجزئيين | صف بلا رقم يُحفَظ `''` (لا `NULL`) فيدخل نطاق الفهرس الفريد ⇒ `409` كاذب محتمل (المحفّز غير مؤكَّد) | مؤكَّد آليًا |
| `FR-006` | `Low` | `backend/src/.../Persistence/AuditLogger.cs:60-78` | حفظان متتاليان (`SaveChangesAsync`) لحدث واحد ⇒ نافذة التزام جزئي إن لم يلفّ المستدعي بمعاملة | مؤكَّد (يخفّف إن لفّ المستدعي) |
| `FR-007` | `Low` | `backend/src/.../Services/AuthService.cs:110-116` | تحرير القفل المنتهي يكتب صف المستخدم **خارج معاملة** و**بلا تدقيق** (تسجيل ناقص لحدث منخفض القيمة) | مؤكَّد |
| `FR-008` | `Low` | `backend/.../MigrationsPostgres/20261002162231_RF019_AuditAppendOnlyPg.cs` | مشغّلات منع `UPDATE`/`DELETE` على مستوى الصف ⇒ `TRUNCATE` يتجاوز إلحاق-فقط (يحتاج صلاحية مالك الجدول) | مؤكَّد |
| `FR-009` | `Low` | `DocumentAppealService.cs:763/806/1158` (تخزين) + `frontend/.../sanitizeRichText` (عرض) | نص غني يُخزَّن غير معقّم ويُعقّم عند العرض فقط (`DOMPurify`) — دفاع بالعمق | مؤكَّد |
| `FR-010` | `Low` | `backend/src/DocGenerator.Api/appsettings.json` (+ `render.yaml` بلا تجاوز لـ`Security:*`) | ترويسات أمنية بقيَم افتراضية ضعيفة: `CspReportOnly=true`, `HstsEnabled=false`, `HttpsRedirectionEnabled=false` — وتسري فعلًا تحت بيان النشر المرجعي | مؤكَّد (المضيف النهائي مجهول) |
| `FR-011` | `Low` | `Repository<T>` العام | يعرض `Update`/`Remove` على `AuditLog`؛ إلحاق-فقط مفروض قاعديًا فقط | مؤكَّد |
| `FR-012` | `Low` | `frontend/src/App.test.tsx:179` | هشاشة (لوحظت في تشغيل كامل واحد): تحت `npx vitest run` الكامل يفشل اختباران، وبالإفراد ينجح 198/198 ⇒ خطر `CI` أحمر متقطّع | مؤكَّد تجريبيًا |
| `FR-013` | `Info` | `DocumentAppealService.cs:805` `CS8601` + `DocumentRepository.cs:98,101` `CS8602` | 4 تحذيرات `nullable` في البناء | مؤكَّد |
| `FR-014` | `Info` | شجرة العمل | تغييرات غير ملتزمة **في `frontend/` فقط** (الخلفية نظيفة): `frontend/e2e/`, `frontend/playwright.config.ts`, `package.json`, `package-lock.json`, `tsconfig.node.json`, `.gitignore` | مؤكَّد |

---

## 2. تفاصيل الملاحظات المؤكَّدة

### `FR-001` — `Blocker`: `datetime2` في هجرة `PostgreSQL`

**الدليل المباشر (سكربت الهجرات المُولَّد فعليًا):**
```
CREATE TABLE "PersonalReminders" (
    ...
    "DueDate" datetime2 NOT NULL,     -- سطر 1570
    ...
    "RecurrenceEnd" datetime2,        -- سطر 1573
    ...
);
```
(`dotnet ef migrations script --context DocGeneratorPostgresDbContext ...` — الملف: `%TEMP%\opencode\pg_migrations.sql:1565-1579`.)

**السلسلة:**
1. `AddPersonalRemindersPg.cs:24,27` ينشئ عمودين بنوع `"datetime2"` (نوع `SQL Server/SQLite` غير موجود في `PostgreSQL`).
2. `RF013_ReminderTimestamptzPg.cs:17-32` يحاول لاحقًا `ALTER COLUMN ... TYPE timestamp with time zone`، لكنه **لا يُبلَغ أبدًا** لأن `CREATE TABLE` أعلاه يفشل أولًا (`ERROR: type "datetime2" does not exist`).
3. الإقلاع ينفّذ الهجرات تلقائيًا: `DatabaseInitializer.InitializeAsync` → `_db.Database.MigrateAsync(ct)`.
4. الإنتاج يستخدم `PostgreSQL`: `render.yaml` يضبط `Database__UsePostgres=true`، و`DependencyInjection` يسجّل `DocGeneratorPostgresDbContext` عبر `UseNpgsql`.
5. تعليق `RF013` نفسه يذكر أن الجدول «ما زال معلّق التطبيق في الإنتاج ضمن دفعة `l-main-page`» ⇒ التطبيق القادم سيصل إلى `CREATE TABLE` المعطوب.

**الأثر:** فشل الإقلاع بخطأ `type "datetime2" does not exist` عند `CREATE TABLE` على أي قاعدة `PostgreSQL` جديدة أو غير مطبَّقة، رغم نجاح كل الاختبارات محليًا (لأنها `SQLite`).

**الإصلاح (راجع `FIX-PLAN.md`):** تعديل نوع العمودين في `AddPersonalRemindersPg.cs` إلى `timestamp with time zone` وإعادة توليد `Designer`/snapshot؛ تبقى هجرة `RF013` غير ضارة (تفيد كإعادة تثبيت للنوع).

### `FR-002` — `High`: هجرات `PostgreSQL` غير مُختبرة في `CI`

- `DatabaseInitializerTests.cs:430-432` و`UtcDateTimeConverterTests.cs:167-169`: تُرجع مبكرًا (`return`) إذا لم يُضبط `DOCGEN_TEST_POSTGRES` ⇒ تُعتبر «ناجحة» بلا تنفيذ.
- `ci.yml` لا يضبط `DOCGEN_TEST_POSTGRES` ولا يشغّل خدمة `PostgreSQL`.
- النتيجة: من `dotnet test` الأخير نجح `645 + 1826 = 2471` اختبارًا، لكن **مسار هجرات `PostgreSQL` لم يُنفَّذ إطلاقًا**، وهو ما يُفسّر مرور `FR-001`.

### `FR-003` / `FR-004` — `Low (كامن)`: إحصاء `dashboard` القديم (غير مستهلك)

- `StatisticsRepository.cs:67-70`:
  ```csharp
  var totalAmount = amounts.Where(d => !d.IsDraft && d.SourceDelegationId == null)
      .Sum(d => d.AmountNumeric + d.InclusionAmountNumeric);
  var totalCollectedAmount = amounts.Where(d => d.SourceDelegationId == null)
      .Sum(d => d.CollectedAmount) ?? 0;
  ```
  جمع عبر عملات مختلفة في `decimal` واحد — يخالف قرار المالك `BQ-029/030` (`RF-015`) المعتمد للوحة `ManagerStats` (التي تستخدم سلات لكل عملة عبر `AddAmount`/`ToCurrencyAmounts`).
- `StatisticsRepository.cs:47-55`: صيغة `d.ExecSubStatus != SubPartiallyExecuted` تُقصي صفوف `NULL` من `Executed` ومن `Active` معًا، بينما `DocumentRepository.cs:232-259` يعالج `NULL` كـ«منفَّذ كامل» صراحةً ⇒ تباعد العدّادات للملفات القديمة ذات `ExecSubStatus` فارغ.
- **تخفيف الخطورة:** الواجهة الحالية لا تستدعي `/api/dashboard` (تُثبت اختبارات `Dashboard.test.tsx:304,620,953` نفيًا `not.toHaveBeenCalledWith('/dashboard')`)، والصفحة تستخدم `/stats/manager`. النقطة تبقى سطح `API` مكشوفًا يحتاج قرارًا: إصلاح أم إزالة.

### `FR-012` — `Low`: هشاشة اختبارات الواجهة

`npx vitest run` الكامل: `1560` ناجح، `2` فاشل في `App.test.tsx:179` (بقاء «جارِ التحميل...»). إعادة تشغيل الملف وحده: `198/198` ناجح. ⇒ هشاشة تحت التوازي، وقد تُسقط `CI` متقطعًا.

---

## 3. متحقَّق وآمن (نتائج إيجابية موثّقة)

- **بناء الخلفية:** `dotnet build backend/DocGenerator.sln` ⇒ `0 Error` (4 تحذيرات فقط — `FR-013`).
- **اختبارات الخلفية:** `645` (`Api`) + `1826` (`Application`) = **`2471/2471`** ناجحة، `0` فشل.
- **الواجهة:** `npx oxlint src` ⇒ `0/0`؛ `npx tsc -b` ⇒ نظيف؛ `npm run build` ⇒ ناجح.
- **عزل بوابة مندوب الجهة:** `PortalController` بلا أي نقطة كتابة عدا المراسلات، والنطاق مفروض خدميًا: `PortalService.ResolveEffectiveIds` يرفض الفرع خارج النطاق (`PortalService.cs:122-130`)، وكل نقطة تُرجع `null`/`404` خارج النطاق (`IsInScopeAsync` عبر `:353-366`).
- **حجب حقول المحامي عن البوابة:** `ScrubForPortal` يصفر صراحةً الحقول الداخلية (`Notes`, `ImmediateActions`, `Lawyer`, معرّفات داخلية…) ويُستدعى في مساري التفاصيل والتصدير (`PortalService.cs:178-218`).
- **عدم تخزين توكن في `localStorage`:** الجلسة `Cookie` فقط؛ الواجهة تقرأ `CSRF` فقط (`frontend/src/api/client.ts:70-74`).
- **التدقيق الذرّي للدخول:** عدّاد الفشل + التدقيق + القفل في معاملة واحدة (`AuthService.cs:149-159`).
- **اتساق وحدانية الترقيم:** النموذج (`DocumentConfiguration.cs:182-187`) وسلسلتا الهجرات (`SQLite`/`PG`: `RF009` ← `PB001` ← سجل الدوائر) والفحص الخدمي (`ExistsActiveWithNumberByCircuitAsync` بفرعيه) متطابقة على مفتاحين جزئيين: `(ExecutionCircuitId, …)` للمربوط و`(CourtNorm, …)` لغير المربوط. (الادّعاء السابق بالتفاوت كان يستند لهجرة `RF009` الانتقالية المستبدَلة — سُحب.)
- **الاعتماديات بلا ثغرات معروفة:** `dotnet list package --vulnerable` (6 مشاريع) و`npm audit --audit-level=high` ⇒ صفر (راجع §7).

---

## 4. لم يُراجَع / مرشّحات غير مؤكَّدة (بطلب المستخدم — مُدرَجة كما هي)

| المعرّف | الموضع | الادّعاء | سبب عدم التحقق |
|---|---|---|---|
| `NR-02` | `DocumentStatusService.cs:1039` | `_documents.Update(target)` + إضافة وقعة عند وراثة/استرداد الإنابة **بلا `_audit.LogAsync`** على وثيقة الهدف | يحتاج تأكيد نيّة التغطية (هل يُدقَّق فعل الإنابة على مستوى الطلب الأم؟) |
| `NR-03` (`FR-015` — سُحب: لا ثغرة) | `AuthService.ChangePasswordAsync:195-214` + `AuthController:119-130` | أخطاء التحقق رسائل عربية ثابتة، وكلمة المرور الخاطئة تُرجع `false` لا استثناءً — ونمط `catch ArgumentException → BadRequest` معمّم ومقصود في كل المتحكمات | حُسم بالقراءة الكاملة |
| `NR-04` | `PortalService.cs:371-641` | بقية منطق الإحصاءات/التصدير للبوابة | لم تُقرأ كاملةً |
| `NR-05` | قاعدة الإنتاج الحية | انحراف المخطط الحي عن الهجرات، وأدوار/صلاحيات `PostgreSQL` | بلا اتصال بقاعدة حية (`NOT VERIFIED`) — قرار المالك `BQ-036` |
| `NR-06` | سلوك `PostgreSQL` الإنتاجي | أحمال/خطط/مناطق زمنية فعليًا، وفكّ التزام `Utc` في `timestamptz` | بلا قاعدة حية |
| `NR-07` | الواجهة على الجوال | فحص بصري فعلي (375px) وتجاوز أفقي/تنقّل | لم يُنفَّذ فحص أجهزة/Playwright (يوجد `frontend/e2e/` غير ملتزم — `FR-014`) |
| `NR-08` | `change-password`, `GlobalExceptionHandler` | تسريب رسائل الاستثناء/التسجيل الكامل للاستثناء في السجلات | لم يُتحقَّق من تفاصيل التسجيل |
| `NR-09` | `TRUNCATE` على جداول التدقيق | صلاحية من يملك `TRUNCATE` في أدوار الإنتاج الفعلية | يعتمد على إعداد الأدوار الحي (`NR-05`) |
| `NR-10` | النسخ/الاستعادة والأسرار | سياسة النسخ الاحتياطي وتمارين الاستعادة (`RPO`/`RTO`) وتدوير `Jwt__Secret` — لم تُفحَص أصلًا | خارج نطاق القراءة؛ تُراجَع إجرائيًا قبل النشر |

---

## 5. قرارات عمل مطلوبة

1. **إصلاح أم إزالة `GET /api/dashboard`** (`FR-003`/`FR-004`): النقطة قديمة وغير مستهلكة من الواجهة الحالية؛ تُصلَح (فصل عملات + حارس `NULL`) أم تُحذَف لتقليل السطح؟
2. **معالجة الرقم الفارغ `''`** (`FR-005`): هل يُوحَّد الفراغ إلى `NULL` عند الكتابة (في `ApplyRequest`/`DocumentService`) أم يُوسَّع فلترا الفهرسين بـ`AND "FileNumber" <> ''`؟
3. **إصلاح معطّل القفل بلا تدقيق** (`FR-007`): هل يُضاف حدث تدقيق `lockout_released` (مقابل لا)؟
4. **ترويسات الأمان في الإنتاج** (`FR-010`): هل `CSP` بلا `Report-Only` و`HSTS` مُفعَّلان في بيئة النشر؟ (القيم في `appsettings.json` لا تعكس الإنتاج بالضرورة.)
5. **هشاشة اختبارات الواجهة** (`FR-012`): هل تُثبَّت قبل النشر لتفادي `CI` أحمر متقطع؟

---

## 6. حالة ملاحظات التدقيق السابقة (`docs/audit/05-final-review.md`)

- الوثيقة `05-final-review.md` **لقطة مجمَّدة** قبل إضافة ميزات لاحقة (المنتدى، أقسام الشعبة، سجل الدوائر، ملاحظات التضمين…) — وبحسب `Docs Policy` لا تُحرَّر. أحدث الهجرات في الشجرة تتجاوز ما ورد فيها (مثلًا `AddForumMessages`, `AddInclusionNotes`, `AddSubHeadSections`, `AddExecutionCircuitRegistry`).
- ادّعاء `ARC-007`/`INT-011` («`RF-013` timestamptz — `FIXED`») **لا يصمد لقاعدة جديدة**: `FR-001` يُثبت أن سلسلة `PostgreSQL` تفشل قبل أن تصل `RF-013`. ⇒ يجب إعادة تقييم حالة `INT-011` بعد الإصلاح.
- تصحيح مقابِل: هجرة `RF009` الأحادية (`Court`) انتقالية مستبدَلة بهجرة سجل الدوائر (فهرسان جزئيان) — حالة `INT-001` (`FIXED`) قائمة ولا تتأثر بسحب ادّعاء `FR-005` الأصلي.
- إعادة تقييم كامل «نتيجة-بنتيجة» خارج نطاق هذه الجلسة؛ يلزم ملحق مؤرّخ جديد وفق `Docs Policy` عند معالجة النتائج.

---

## 7. بوابات التحقق (نتائج فعلية — 2026-10-15)

| البوابة | الأمر | النتيجة |
|---|---|---|
| بناء الخلفية | `dotnet build backend/DocGenerator.sln` | نجاح — 0 أخطاء، 4 تحذيرات |
| اختبار الخلفية | `dotnet test backend/DocGenerator.sln` | `2471` ناجح / `0` فاشل على شجرة خلفية نظيفة (يحذّر: `PostgreSQL` مُتخطّى — `FR-002`) |
| `lint` الواجهة | `npx oxlint src` | 0 تحذير / 0 خطأ |
| نوع الواجهة | `npx tsc -b` | نظيف |
| اختبار الواجهة | `npx vitest run` | `1560` ناجح / `2` فاشل هشاشة (`FR-012` — تشغيل كامل واحد) |
| بناء الواجهة | `npm run build` | ناجح |
| سكربت الهجرات `PG` | `dotnet ef migrations script --context DocGeneratorPostgresDbContext` | نجح — وكشف `FR-001` |
| ثغرات حزم الخلفية | `dotnet list backend/DocGenerator.sln package --vulnerable` | نظيف — 6/6 مشاريع بلا حزم معروفة الثغرات |
| ثغرات حزم الواجهة | `npm audit --audit-level=high` | نظيف — `found 0 vulnerabilities` |

---

## 8. ملاحظة النشر (الهجرات)

- المهمة **لا تُعتبر منجزة** قبل إصلاح `FR-001` وإقرار تطبيق الهجرات في نافذة النشر:
  - `dotnet ef database update --context DocGeneratorDbContext`
  - `dotnet ef database update --context DocGeneratorPostgresDbContext`
- المرجع: `RUN_GUIDE.md` §9 «تطبيق هجرات قاعدة البيانات عند النشر».
- تنبيه: أي إصلاح لـ `FR-001` يمسّ هجرة قائمة؛ يُنفَّذ قبل فك حالة `NO-GO` فقط بعد تأكيد أن الإنتاج لم يُطبّق الهجرة المعطوبة.
