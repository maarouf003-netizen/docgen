# P0-04 — إزالة تكرار عامة لكل عملية كتابة

- **التاريخ:** 2026-11-14.
- **المرجع:** `docs/PERFECTION_ROADMAP.md` §4 `P0-04`، و`docs/adr/0003-idempotency-all-writes.md`.
- **النطاق:** `POST/PUT/PATCH/DELETE` المصادقة في `DocGenerator.Api`.

## 1. ما نُفّذ

- `IdempotencyMiddleware` بعد المصادقة/التفويض/حد المعدل:
  - تحجز المفتاح قبل تنفيذ الإجراء (خارج معاملة العملية).
  - تعيد عند التكرار: `StatusCode` + `ContentType` + `Location` + الجسم حرفيًا.
  - ترفض المتزامن بنفس البصمة `409`.
  - تحرّر الحجز عند الفشل/عدم `2xx`/تعذّر تخزين اللقطة.
- المصدر: ترويسة `X-Idempotency-Key` أو `Idempotency-Key`؛ النطاق
  `(Key, METHOD + route template, UserId)`؛ البصمة `SHA256` لجسم HTTP الخام.
- إضافة `StatusCode` و`ContentType` و`ResponseLocation` إلى `IdempotencyKeys`
  مع الهجرة `20261114140029_AddIdempotencyResponseMetadata`.
- استثناءات `[SkipIdempotency]`:
  - `auth.login` و`auth.logout`.
  - `csp-report` و`client-errors` (تليمترية).
  - العمليات الست ذات الحجز الخدمي القائم: `documents.create`,
    `documents.transfer-all`, `circuits.refer-files`,
    `registry.import-commit`, `registry.unify`, `registry.merge-commit`.
- الواجهة: `api` interceptor يضيف المفتاح تلقائيًا لكل كتابة غير مستثناة،
  ويحترم المفتاح الصريح في النماذج الحساسة.
- وثائق: `ADR-0003` + تحديث `RUN_GUIDE.md` §9 و`AGENTS.md` وroadmap.

## 2. التحقق

| البوابة | النتيجة |
|---|---|
| `dotnet build -warnaserror` | 0 تحذير / 0 خطأ |
| `Api` (متوازية) | 899 ناجحة |
| `Api` (تسلسلية) | 5 ناجحة |
| `Application` | 2392 ناجحة |
| `oxlint` / `tsc -b` / `vitest` | خضراء (1803 اختبار واجهة) |
| `npm run build` / `npm run guard` | خضراء |
| `migrations has-pending-model-changes` | نظيف |
| سكربت الهجرة | لا `datetime2`؛ `StatusCode`/`ContentType`/`ResponseLocation` |

- اختبارات جديدة: `GlobalIdempotencyTests` (إعادة 201 + Location، منع التكرار،
  بصمة مختلفة = نية جديدة، حجز معلّق = 409، مفتاح تالف = 400، تزامن = تنفيذ واحد)،
  `IdempotencyStoreTests` (الحالة/النوع/الموقع + إعادة الضبط)،
  `PostgresBaselineTests` (سلسلة الهجرات على Postgres في CI).

## 3. حدود مقصودة

- المفتاح اختياري؛ الطلب بلا مفتاح يمرّ كما هو (توافق خلفي).
- نافذة الإعادة 72 ساعة (قرار `BQ-032`) لا 24 ساعة.
- لا تُخزَّن استجابات أكبر من `1MiB`؛ تُحرَّر التذكرة عندها.
- التخزين نصي (JSON)؛ لا كتابة تُرجع ملفًا حاليًا (التوليد/التصدير `GET`).
- التنظيف انتهازي داخل `IdempotencyStore`؛ لا مهمة خلفية مستقلة بعد.
- العمليات الست الخدمية تحتفظ بعقدها (إعادة `200` للإنشاء) لحين توحيدها لاحقًا.

## 4. النشر

- هجرة واحدة جديدة: `20261114140029_AddIdempotencyResponseMetadata` (إضافية وغير كاسرة).
- **لم تُطبّق على أي بيئة بعد.** قبل نشر نسخة `P0-04`:
  `dotnet ef database update --context DocGeneratorPostgresDbContext`.
- `Database:AutoMigrate=false` في الإنتاج ⇒ الإقلاع يُرفض إن لم تُطبّق.
