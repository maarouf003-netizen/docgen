# ملحق `W0-CSP-followup` — إغلاق فجوات المراجعة النقدية — 2026‑11‑04

> **الغرض:** معالجة ما وصفته المراجعة النقدية لدفعة `W0-CSP` (`PR #91`, `9839498`) من فجوات
> تغطية وتشغيل: `M-1`–`M-4`، و`L-1`، و`L-3`، و`L-4`، و`L-6`، والملاحظات الصغيرة.
> الملحق الأصلي `GOV-SERVER-LAUNCH-DELTA-ANNEX-20261104-W0CSP.md` مجنَّد ولم يُحرَّر.
>
> **النطاق:** كود + اختبارات + توثيق. **لا تغيير في عقود `API`/`DTO`، ولا هجرات `EF`.**
>
> **الفرع:** `feature/security-w0-csp-followup` مبني على `master` عند `9839498`.

---

## 1. `M-1` — `includeSubDomains` راية صريحة بدل `true` مثبّتة

**الخطر السابق:** `HstsSetup.Configure` كان يثبّت `hsts.IncludeSubDomains = true` دائمًا. تفعيل
`HstsEnabled` على نطاق له نطاق فرعي يخدم `HTTP` فقط (بيئة قديمة، خدمة داخلية) يكسره فورًا بلا
إمكانية تراجع سريعة.

**القرار والمعالجة:**

| المفتاح | الافتراضي | السلوك |
|---|---|---|
| `Security:HstsIncludeSubDomains` | `false` | يُضاف `includeSubDomains` صراحةً |
| `Security:HstsPreload` | `false` | **يفرض** `includeSubDomains` (متطلب قوائم المتصفحات) |

- الافتراض `false` هو **التغيير السلوكي الوحيد** في هذه الدفعة: من أراد السلوك السابق يضبط
  `Security__HstsIncludeSubDomains=true` صراحةً.
- اختبارات: `HstsSetupTests.Defaults_AreOneYearWithoutSubDomainsOrPreload`،
  `IncludeSubDomains_IsOptedIntoExplicitly`، `HstsHeaderTests.HttpsResponse_CarriesSubDomainsWhenOptedIn`،
  و`HttpsResponse_CarriesPreloadAndForcedSubDomainsWhenEnabled`.

## 2. `M-2` — `HSTS` خلف وكيل ينهي `TLS`

`UseHsts` لا يرسل الترويسة إلا عند `Request.IsHttps`. خلف وكيل عكسي ينهي `TLS` لا يصبح الطلب
`https` إلا بمعالجة `X-Forwarded-Proto` من وكيل **معلَن** في `Security:KnownProxies` (قرّرت `W0`
إغلاق الثقة افتراضيًا). بدون ذلك: المستخدم على `HTTPS` بينما `HSTS` **غائب صامتًا**.

أُضيف إلى `RUN_GUIDE.md` §9:

- بند صريح تحت `Security:HstsEnabled` يربط الترويسة بشرط الوكيل الموثوق.
- فحص ما بعد النشر:
  `curl -sI https://<host>/ | grep -i '^strict-transport-security'`
  مع تفسير الغياب (وكيل غير موثوق / لا يمرّر `X-Forwarded-Proto`).
- توضيح أن `HttpsRedirection` يخضع للشرط نفسه.

## 3. `M-3` — اختبارات `HSTS` الحقيقية

`TestServer` يثبّت مخطط الطلب على `http` ولا يحاكي إنهاء `TLS` (ولا تحوّله `BaseAddress=https`)،
لذلك فُصلت التغطية إلى طبقتين:

| الملف | التغطية |
|---|---|
| `HstsHeaderTests` | استدعاء `HstsMiddleware` الفعلي على `HttpContext` مخططه `https`: `max-age` الصحيح، غياب `includeSubDomains`/`preload` افتراضيًا، إضافتهما عند الرايات، وغياب الترويسة على `http`. |
| `HstsProductionIntegrationTests` | مضيف `Production` حقيقي: انتقال الرايات إلى `HstsOptions`، وغياب الترويسة على `HTTP`، و**فشل إقلاع** عند `HstsPreload=true` مع `HstsEnabled=false`. |

الاختبار الحارس القديم `NonPositiveMaxAge_IsRejectedAtStartup` أُعيدت تسميته إلى
`NonPositiveMaxAge_IsRejectedByValidation` لأنه اختبار وحدة لا إقلاع.

## 4. `M-4` — توسيع حارس `CSP`

أُضيف إلى `frontend/scripts/csp-guard.mjs`:

- `analyzeCss`: يرفض `url(https://…)` و`url(//…)` و`@import https://…` (مع استثناء
  `data:`/`blob:` المسموحين للصور والخطوط)، ويعالج `@import url(...)` مرة واحدة بلا احتساب مزدوج.
- `analyzeDist` يفحص أوراق `CSS` المبنية ضمن `assets` ويعدّها في `filesChecked`.
- كشف مراجع `/assets/...` عبر محلّل السمات نفسه، فتشمل **علامات التنصيص المفردة والمزدوجة**
  و`srcset` بدل تعبير يطابق المزدوجة فقط.
- `Dockerfile`: `RUN npm run guard` بعد `npm run build` داخل مرحلة الواجهة.
- `ci.yml`: `npx oxlint src scripts` (يشمل الحارس)، وخطوة الحارس صارت `npm run guard`.
- `package.json`: سكربت `guard`.

اختبارات `csp-guard.test.mjs` صارت **19** اختبارًا (كانت 12)، منها: `analyzeCss` (نظيف/خارجي/
`@import` مرة واحدة)، و`analyzeDist` على مجلدات مؤقتة (غياب `dist`، `dist` نظيف، مرجع مفرد
مفقود، `CSS` خارجي).

## 5. `L-1` — سقف `HstsMaxAgeDays`

`HstsSetup.MaximumMaxAgeDays = 3650` (عشر سنوات). القيمة الأكبر تُرفض عند الإقلاع برسالة تسمّي
المفتاح والسقف، بدل فيض `TimeSpan.FromDays` برسالة غامضة. اختباران: الرفض فوق السقف، والقبول
عنده.

## 6. `L-4` — تصحيح تعليق بيئة التطوير

`SecurityHeadersMiddleware` كان يدّعي أن `CspReportOnly=true` إلزامي في التطوير لأن
`connect-src 'self'` يحجب `ws:` الخاص بـ`Vite HMR`. الحقيقة: `Vite` يخدم مستند `SPA` بنفسه،
فلا تصل ترويسة `Kestrel` إلى الصفحة أصلًا. صُحّح التعليق: بقاء وضع المراقبة افتراضيًا **قرار نشر**
لا استثناء تقني.

## 7. `L-6` — توسيع بروفة `Production` الإلزامية

أُضيفت إلى `RUN_GUIDE.md` §9 خطوات سلوكية بعد فرض `CSP`: تنزيل `Word`/`Excel` (`blob:`)،
معاينة الصور (`data:`/`blob:`)، المحرر الغني (`span[style]` + `unsafe-inline`)، الطباعة،
وروابط `SPA` العميقة وتحديثها وصفحة `404`. الاختبار الآلي يثبت وصول الترويسة، لكنه لا يغني عن
الجولة السلوكية.

## 8. ملاحظات المراجعة الصغيرة

- مرجع الأصل في مستند اختبار `Production` صار مكتملًا: أُنشئت `assets/index-test.js` و
  `assets/index-test.css` و`favicon.svg` داخل جذر الويب المؤقت (كانت الصفحة تشير إلى ملفات غير
  موجودة).
- استُخرج مضيف `Production` المشترك إلى `ProductionSpaFactory.cs` بدل تكراره بين بروفة `CSP`
  و`HSTS`.

## 9. التحقق المنفَّذ

```powershell
dotnet build backend/DocGenerator.sln --nologo -v q -warnaserror
dotnet test backend/tests/DocGenerator.Api.Tests --filter "Category!=Sequential"
dotnet test backend/tests/DocGenerator.Api.Tests --filter "Category=Sequential"
dotnet test backend/tests/DocGenerator.Application.Tests
cd frontend
npx oxlint src scripts
npx tsc -b
npx vitest run
npm run build
npm run guard
dotnet ef migrations has-pending-model-changes --context DocGeneratorDbContext
dotnet ef migrations has-pending-model-changes --context DocGeneratorPostgresDbContext
```

- حراس `HSTS` و`CSP` خضراء محليًا، وحارس البناء يفحص `88` ملفًا (86 `JS` + 2 `CSS` + المستند).
- **الهجرات: صفر.** لا `database update` مطلوب، وقائمة `RUN_GUIDE.md` §9 التراكمية لا تتغيّر.

## 10. المتبقّي (دفعة `W1` — لم يُنفَّذ هنا)

- تقييد `CspReportUri` بمسار `/`-نسبي فقط.
- توحيد ردود `404` الخمسة بلا جسم في `DocumentsController`.
- تطهير مسار الاستثناء في `Development` وتشديد حارس مصدر رسائل الخطأ.
- تحويل `OperationCanceledException` إلى `499`.
- `W1-7` (تفرد → `409`) ما زال معلّقًا على اعتماد المالك لأنه يغيّر عقد `500 → 409`.
