# `P2-03` — افتراضات أمنية آمنة في الإنتاج — 2026-11-17

> PR: [#193](https://github.com/maarouf003-netizen/docgen/pull/193)
>
> نطاق هذه الجولة: منع تشغيل **منشور إنتاجي** بوضع أمني ناقص (fail-closed)، وتغطية
> سياسة الكوكيز/CORS بالاختبارات. لا تغيير في السياسات نفسها (`HSTS`/`CSP`/الكوكيز
> كانت مبنية صحيحًا) — التغيير أن **القيم الناقصة تمنع الإقلاع** بدل تحذير يُتجاهل.
>
> ⚠️ **تغيير نشر كاسر مقصود:** أي بيئة `Production` بلا المتغيّرات أدناه **لن تقلع**
> بعد هذه النسخة. هذا هو `DoD` البند، ويُضبط قبل النشر لا بعده.

## 1. القرارات

1. **Fail-closed لا تحذير:** كان `Program.cs` يسجّل تحذيرًا عند `AllowedHosts=*` أو
   `KnownProxies` فارغ، بينما `HSTS`/إعادة التوجيه/`CSP` تبقى معطّلة بصمت. التحذير
   لا يحمي منشورًا. صار الحارس `SecurityProductionPosture` يجمع **كل** الانتهاكات
   ويرفض الإقلاع برسالة واحدة تسمّي المفاتيح.
2. **البروتوكول الأصلي وراء بروكسي:** `HttpsRedirectionEnabled=true` بلا
   `KnownProxies` يسبب **حلقة إعادة توجيه** (الطلب يبدو `HTTP` دائمًا). لذلك يُشترط
   أحد أمرين صراحةً: `Security:KnownProxies` صالح، أو `Security:TlsTerminatedByApp=true`
   (Kestrel ينهي `TLS`). الراية الجديدة تمنع التخمين ولا تفرض `KnownProxies` وهميًا.
3. **رفض الثقة الكاملة:** `0.0.0.0/0`/`::/0` والإدخال غير القابل للتحليل في
   `KnownProxies` يُرفضان: `ForwardedHeadersSetup` يتجاهل غير الصالح بصمت، فيظن
   المشغّل أنه محمي بينما `X-Forwarded-*` غير موثوق.
4. **`CSP` مفروضة:** `CspReportOnly=false` إلزامي في `Production`. السياسة
   (`script-src 'self'` بلا inline، و`style-src 'self' 'unsafe-inline'` لعناصر
   `span[style]` بعد `DOMPurify`) مُثبتة ببروفة `Production` قائمة
   (`CspProductionIntegrationTests`) وبحارس البناء `npm run guard`. **البروفة البصرية
   على `staging` تبقى إجراء مالك قبل الترقية.**
5. **`Swagger` مرفوض في الإنتاج:** `SwaggerUI` لا يحقق `CSP` المفروضة ويكشف سطح
   `API`. (كان مغلقًا افتراضيًا؛ صار إغلاقه شرط إقلاع.)
6. **CORS:** سياسة `Vite` (localhost + credentials) تُطبَّق في `Development` فقط،
   وأُضيف اختبار يثبت أن `Production` لا يُصدر أي ترويسة `Access-Control-*` لأصل محلي.

## 2. المتغيّرات الإلزامية في `Production`

| المفتاح | القيمة |
|---|---|
| `AllowedHosts` | مضيف/مضيفون صريحون بلا `*` (مثال `docgen.gov.sy;127.0.0.1`) |
| `Security__HstsEnabled` | `true` |
| `Security__HttpsRedirectionEnabled` | `true` |
| `Security__CspReportOnly` | `false` |
| `Swagger__Enabled` | `false` |
| `Security__KnownProxies__0` | `IP`/`CIDR` صالح للبروكسي (لا `0.0.0.0/0`) |
| `Security__TlsTerminatedByApp` | بديل عن الوكيل: `true` فقط إذا أنهى Kestrel الـTLS |

`render.yaml` حُدّث بهذه المفاتيح (الثابتة بقيمة، والخاصة بالمضيف/الوكيل بـ`sync: false`)،
و`RUN_GUIDE.md` §9 صار يبدأ بتحذير الرفض هذا.

## 3. التنفيذ

- **جديد:** `Security/SecurityProductionPosture.cs` (يجمع الانتهاكات ويرفض)،
  واختباراته `SecurityProductionPostureTests`، و`ProductionPostureIntegrationTests`.
- **`SecurityOptions.TlsTerminatedByApp`** مع شرح أثر غيابه (حلقة إعادة التوجيه).
- **`Program.cs`:** استدعاء الحارس بعد `HstsSetup.Validate`/`CspSetup.Validate` وقبل البناء.
- **`ProductionSpaFactory`:** إعداد إنتاجي مكتمل (HSTS/إعادة توجيه/`CSP` صارم/مضيفون/وكيل)
  كي تبقى اختبارات `CSP`/`HSTS` على وضع صالح لا ناقص، عبر مساعد واحد
  `ProductionSecurityBaseline` تستخدمه مضيفات الاختبار الإنتاجية كلها
  (`SwaggerDisabledFactory`، `HostFilteringFactory`، `ProductionFactory`، وحارس القاعدة).
- **`SecurityHeadersIntegrationTests`:** كانت تثبت `CSP-Report-Only` في الإنتاج (سلوك ما
  قبل `P2-03`)؛ صارت تثبت الترويسة **المفروضة** في الإنتاج والمراقِبة في التطوير.
- **`appsettings.json`:** `TlsTerminatedByApp=false` موثّقًا.
- **سياسة الكوكيز:** اختبارات تثبت في `Production`: `AuthCookie` = `HttpOnly` +
  `Secure` + `SameSite=Strict`، وكوكي `CSRF` = `Secure` + `Strict` + `HttpOnly=false`
  (مقصود: الواجهة تقرؤه).

## 4. التحقق

- `dotnet build … -warnaserror`: 0/0.
- `dotnet format … --verify-no-changes`: `exit 0`.
- `DocGenerator.Api.Tests` (`Category!=Sequential`): **1507/1507** (+23).
- `DocGenerator.Api.Tests` (`Category=Sequential`): **5/5**.
- `DocGenerator.Application.Tests`: **2486/2486** (بلا تغيير).
- `DocGenerator.Architecture.Tests`: **29/29**.
- الحلقة السريعة: `Application` 707، و`Api` **820** (+16).
- الواجهة (`oxlint`/`tsc`/`vitest` 1808/`build`/`guard`) والحراس الخمسة: نظيفة.

### تحقق سلبي

- **بدون أي إعداد:** رسالة الرفض تسمّي `AllowedHosts`/`HstsEnabled`/`HttpsRedirectionEnabled`/
  `CspReportOnly`/`KnownProxies` معًا (اختبار `Production_WithDefaults_…`).
- **مع كل إعداد صالح:** الإقلاع ينجح (`CompliantProductionSettings_StartHost`).
- **إدخال وكيل غير صالح:** رفض صريح برسالة `no valid IP/CIDR`.
- **`0.0.0.0/0`:** رفض `must not trust every address`.
- **بيئات غير `Production`:** لا تقييد (Development/Staging/Testing).

## 5. الحدود (لا تُخفى)

- **البروفة الفعلية على `staging`** (رؤوس `HSTS`/`CSP`/Cookies/CORS بأمر `curl`) تبقى
  `DoD` غير منفّذ آليًا — تحتاج بيئة منشورة.
- **قيم المضيف/الوكيل الحقيقية** عند المالك؛ لا نخمّن نطاقًا ولا شبكة بروكسي.
- **`HstsPreload`** قرار مالك مستقل (التزام شبه دائم في قوائم المتصفحات) — غير مفروض.
- **`HSTS` لا يُرسَل عبر `HTTP`** (`UseHsts` يشترط `Request.IsHttps`)، ولهذا يشترط
  الحارس وسيلة موثوقة لمعرفة البروتوكول؛ الوكيل يجب أن يرسل `X-Forwarded-Proto`.
