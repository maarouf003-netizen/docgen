# `P2-08` — تقوية حدود المعدل ومكافحة التخمين — 2026-11-17

> PR: [#195](https://github.com/maarouf003-netizen/docgen/pull/195)
>
> نطاق هذه الجولة: إغلاق الثقوب المكتشفة في تغطية `RateLimiting`، تحقق إعدادات صريح
> عند الإقلاع، وترويسة `Retry-After` على كل `429`. لا تغيير في مفاتيح التقسيم
> (IP للمجهول/User للمصادَق) ولا في القفل التدريجي القائم.

## 1. ما كان ناقصًا (مقيس)

| الثغرة | الأثر |
|---|---|
| `EntityRegistryController.import-preview/import-commit` بلا `[EnableRateLimiting]` | استيراد ثقيل يُستدعى بمعدل الحد العام فقط (300/دقيقة للمصادَق) |
| `EntityRegistryController.change-events/export` بلا السياسة | تصدير Excel ثقيل بلا سقف «مكلف» (رغم `[SingleExportPerUser]`) |
| `PermitLimit = Math.Max(1, perMinute)` | `0` أو سالب في الإعدادات يصبح **حدًّا مفعّلًا** بدل خطأ إعداد |
| لا `Retry-After` على 429 | العميل لا يعرف متى يعيد المحاولة (RFC 6585) |

**الموجود والمثبت مسبقًا (لم يُمسّ):** `login-ip` · `password` · `csp-report` ·
`expensive` · الحد العام، والتقسيم حسب `user:{id}`/`ip:{address}`، والقفل التدريجي
في `LockoutOptions` (حد أقصى `MaxLockoutMinutes`)، واختبارات العزل لكل سياسة.

## 2. التغييرات

1. **سياسة `expensive`** على استيراد الجهات (معاينة/اعتماد) وتصدير سجل التغييرات.
2. **`RateLimitOptions.Validate`** تُستدعى عند الإقلاع: كل حد `1..1_000_000`،
   `MaxLoginAttempts 1..100`، `WindowMinutes 1..60`. أي قيمة خارجها تمنع الإقلاع
   برسالة تسمّي المفتاح.
3. **إزالة `Math.Max(1, …)`** من بناء المحدد: القيم صارت محققة، والتسطيح الصامت
   يُخفي خطأ الإعداد.
4. **`Retry-After: 60`** على رفض سياسات النافذة الدقيقة (عام/مكلف/دخول IP/كلمة مرور/
   CSP)، و`Retry-After = WindowMinutes × 60` على رفض محدّد الدخول المعتمد على القاعدة
   (`Login` قبل التحقق وبعد `TryRecordFailure`).
5. **حارس انعكاسي** `RateLimitCoverageTests`: كل إجراء يحمل `[SingleExportPerUser]`
   يجب أن يحمل `expensive` (على الإجراء أو الصنف)، واستيراد الجهات مثبت صراحةً.

## 3. الاختبارات

- `RateLimitOptionsTests` (7 حالات): الافتراضات صالحة، ورفض `MaxLoginAttempts`/`WindowMinutes`
  خارج المدى، وكل حد دقيق غير موجب.
- `RateLimitCoverageTests` (3 حالات): التصديرات المتزامنة + استيراد الجهات.
- `RateLimitingIntegrationTests`: تُثبت الآن `Retry-After: 60` على 429 (دخول IP، عام،
  مكلف)، واختبار إقلاع مرفوض عند `ExpensivePerMinute=0`.

## 4. التحقق

- `dotnet build … -warnaserror`: 0/0.
- `dotnet format … --verify-no-changes`: `exit 0`.
- `DocGenerator.Api.Tests` (`Category!=Sequential`): **1536/1536** (+11).
- `DocGenerator.Api.Tests` (`Category=Sequential`): **5/5**.
- `DocGenerator.Application.Tests`: **2486/2486** (بلا تغيير).
- `DocGenerator.Architecture.Tests`: **29/29**.
- الحلقة السريعة: `Application` 707، و`Api` **848** (+10).
- الواجهة (`oxlint`/`tsc`/`vitest` 1808/`build`/`guard`) والحراس الخمسة: نظيفة.

## 5. الحدود

- **قياس 429 تحت حمل حقيقي/staging** لم يُنفَّذ (لا محاكاة حمل في هذه الجولة).
- **`Retry-After` ثابت 60 ثانية** لسياسات النافذة الدقيقة — صحيح لأن كل السياسات
  نافذة دقيقة واحدة؛ أي سياسة مستقبلية بنافذة مختلفة تحتاج قيمة مشتقة لا ثابتة.
- **القفل التدريجي** بقي كما هو (`LockoutOptions`)، ولم يُضف قفل على مستوى الحساب
  عبر IP واحد (سياسة منتج، لا خلل).
- لا هجرات ولا تغيير في العقود.
