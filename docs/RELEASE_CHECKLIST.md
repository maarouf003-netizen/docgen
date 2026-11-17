# قائمة الإصدار (`P7-03`)

> تُوقَّع قبل كل نشر إلى بيئة مشتركة/إنتاج. لا إصدار بلا توقيع هذه القائمة.
> الإجراءات التفصيلية في [`runbooks/`](runbooks/README.md) و`RUN_GUIDE.md` §9.

## 1. قبل النافذة

- [ ] الـ`PR` مدموج، و**البوابات الثماني** خضراء: `backend` · `frontend` · `guards` ·
      `audit` · `backend-postgres` · `migration-guard` · `CodeQL (csharp)` ·
      `CodeQL (javascript-typescript)`.
- [ ] `CHANGELOG.md` محدّث بما سيُنشر (وإن لم يتغيّر السلوك فاذكر ذلك).
- [ ] **نسخة احتياطية + استعادة تجريبية** موثّقة (التاريخ/الحجم/مكان الحفظ).
- [ ] **الهجرات المعلّقة** من `RUN_GUIDE.md` §9 معروفة، و`p0-07b` شُغّل preflight على
      قاعدة الإنتاج الفعلية إن كانت ضمن الحزمة.
- [ ] **متغيّرات `P2-03`** مضبوطة: `AllowedHosts` صريح، `HstsEnabled=true`،
      `HttpsRedirectionEnabled=true`، `CspReportOnly=false`، `Swagger__Enabled=false`،
      و`KnownProxies` صالح أو `TlsTerminatedByApp=true` (وإلا **لن تقلع** الخدمة).
- [ ] `gitleaks`/مراجعة أسرار البيئة حسب `P2-05` (إن كان الإصدار يمسّ الأسرار).
- [ ] حالة الاعتماديات: `dependency-audit` و`npm-audit-guard` و`dependency-review` خضراء،
      وأي استثناء `npm audit` ما زال ضمن تاريخ انتهائه.
- [ ] خطة التراجع محددة (النسخة السابقة، من يقرر، ومتى).

## 2. أثناء النافذة

- [ ] سجّل الهجرات المطبّقة حيًّا قبل البدء:
      `SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY 1;`
- [ ] طبّق الهجرات **قبل** النسخة:
      `dotnet ef database update --context DocGeneratorPostgresDbContext`
      (بعد preflight عند اللزوم؛ **لا** `--context DocGeneratorDbContext`).
- [ ] تحقّق من `__EFMigrationsHistory` (الصفوف الجديدة ظهرت).
- [ ] شغّل النسخة الجديدة/انشر الصورة.
- [ ] \*هجرات التدقيق (إن انطبقت): `scripts/audit-readonly-privileges.sql` بدور المالك.

## 3. بعد النافذة (فحص دخاني)

- [ ] `GET /healthz` = 200 و`GET /readyz` = 200 و`GET /healthz/outbox` سليم.
- [ ] رؤوس: `Strict-Transport-Security`، `Content-Security-Policy` (لا `Report-Only`)،
      كوكي `docgen_token` (`Secure`/`HttpOnly`/`SameSite=Strict`)، ولا `Access-Control-*`
      لأصل محلي.
- [ ] دخول بحساب اختباري، بحث، فتح تفاصيل، توليد `Word`، تصدير `Excel` صغير.
- [ ] سجل التدقيق يعمل، وصفحة الإحصاءات تُحمَّل.
- [ ] لا أخطاء `5xx` جديدة في السجل خلال 10 دقائق، ولا تراكم Outbox.
- [ ] (إن تغيّر سطح API) لقطة OpenAPI مطابقة (`OpenApiContractTests` في CI) والعميل
      الواجهي يعمل على المسارات المعدّلة.

## 4. التراجع (متى وكيف)

- **معايير التراجع:** `5xx` متصاعدة، تعطّل تدفّق حرج، أو فشل الفحص الدخاني خلال 15 دقيقة.
- **الطريقة:** أعد النسخة السابقة ([`runbooks/rollback.md`](runbooks/rollback.md))؛ لا `Down`
  على القاعدة الحية؛ استعادة النسخة الاحتياطية فقط عند تضرّر البيانات.
- **وثّق:** الوقت، النسخة، السبب، الأثر، الإجراء.

## 5. التوقيع

| الدور | الاسم | القرار (نشر/تأجيل/تراجع) | التاريخ |
|---|---|---|---|
| مالك النظام | | | |
| منفّذ النشر | | | |
