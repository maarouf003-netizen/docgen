# ملحق `W0-CSP` — تصحيح فرض `CSP` وتقليم `HSTS` — 2026‑11‑04

> **الغرض:** إغلاق البنود الأربعة التي وُصفت في
> `GOV-SERVER-LAUNCH-DELTA-ANNEX-20261104.md` §5 بأنها «في الدفعة التالية»:
> `upgrade-insecure-requests`، وفصل `preload`، وإتاحة `max-age`، وحارس بناء يمنع
> المفاجأة عند الفرض. الملحق السابق مجنَّد (سياسة `docs/audit`) ولم يُحرَّر.
>
> **النطاق:** كود + اختبارات + توثيق. **لا تغيير في عقود `API`/`DTO`، ولا هجرات `EF`.**

---

## 1. `upgrade-insecure-requests` — أُزيلت من السياسة

**الخطر السابق:** السياسة كانت تنتهي بـ`upgrade-insecure-requests`. على خادم يخدم عبر
`HTTP` (حالة النقل الأولى داخل الشبكة الحكومية قبل تركيب `TLS`) تُحوِّل المتصفحات كل
أصول الصفحة النسبية إلى `https` فتفشل ⇒ **صفحة بيضاء بلا رسالة خطأ واضحة**.

**المعالجة:** حُذفت التعليمات نهائيًا من `SecurityHeadersMiddleware.CspValue`، مع تعليق
في الموضع نفسه يشرح لماذا لا تُعاد. تحديث البروتوكول مسؤولية `HSTS` و`HttpsRedirection`
صراحةً، لا سياسة `CSP`.

**الحارس:** `SecurityHeadersMiddlewareTests.Csp_DoesNotUpgradeInsecureRequests`
و`Csp_CoreDirectives_ArePresent`.

## 2. `HSTS` — `preload` مستقل والمدة قابلة للضبط

**الخطر السابق:** `HstsEnabled=true` كان يفعّل `preload` ضمنيًا. إدراج النطاق في قوائم
المتصفحات المدمجة التزام شبه دائم (إزالته تستغرق أشهرًا)، فربطُه بمفتاح التفعيل العادي
يجعل قرارًا تشغيليًا قابلًا للتراجع يُنتج التزامًا لا رجعة فيه.

**المعالجة:**

| المفتاح | الافتراضي | السلوك |
|---|---|---|
| `Security:HstsEnabled` | `false` | تفعيل الترويسة فقط |
| `Security:HstsMaxAgeDays` | `365` | مدة الترويسة؛ قيمة غير موجبة **ترفض الإقلاع** |
| `Security:HstsPreload` | `false` | إدراج `preload` صراحةً؛ مع مدة < 365 يومًا **يرفض الإقلاع** |

الرمز في `HstsSetup.Configure` (نمط `ForwardedHeadersSetup` نفسه)، والمفاتيح موثَّقة في
`RUN_GUIDE.md` §9. الحارس: `HstsSetupTests` (النسخ، الافتراضيات، الرفض، حدّ السنة).

> **أثر نقل:** الخادم الحكومي الذي يريد `preload` **يجب** أن يضبط
> `Security__HstsPreload=true` صراحةً؛ تفعيل `Security__HstsEnabled` وحده لم يعد كافيًا
> (وهذا هو المقصود).

## 3. حارس بناء `CSP` — `frontend/scripts/csp-guard.mjs`

يفحص **مخرجات `Vite`** لا المصدر، ويفشل عند:

1. `<script>` مضمّن بلا `src` (محجوب بـ`script-src 'self'`).
2. معالج حدث مضمّن (`onclick=` ونحوه).
3. أصل خارجي أو `javascript:` في وسوم تحميل الموارد (محجوب بـ`default-src 'self'`).
4. `eval` / `new Function` / `Function(...)` / `WebAssembly` في حزم `dist` (لا
   `'unsafe-eval'` ولا `'wasm-unsafe-eval'` في السياسة).
5. مرجع `/assets/...` غير موجود في `dist` (صفحة بيضاء لأسباب أخرى).

**التشغيل:** `cd frontend && npm run build && node scripts/csp-guard.mjs` — وأُضيف خطوةً في
وظيفة `frontend` بـ`CI` **بعد** `Build`. منطق الحارس مُختبَر في
`frontend/scripts/csp-guard.test.mjs` (12 اختبارًا صلبًا/سلبيًا).

## 4. بروفة `Production` — اختبار تكامل

`CspProductionIntegrationTests` يبني مضيفًا إنتاجيًا حقيقيًا ويخدم `index.html` من جذر ويب
مؤقت، ثم يثبت أن مستند الصفحة يصل معه:

- `Content-Security-Policy` **مفروضة** (لا `Report-Only`) عند `CspReportOnly=false`.
- **بلا** `upgrade-insecure-requests`.
- مع `script-src 'self'` و`style-src 'self' 'unsafe-inline'`.
- مع ترويسات الأساس (`nosniff`، `DENY`، `no-referrer`) وبلا `no-store` على المستند.

هذا يغلق ثغرة البروفة السابقة: في `Development` يخدم `Vite` المستند فلا تصل ترويسة
`Kestrel` إليه أصلًا، وكانت أي بروفة هناك بلا قيمة.

## 5. ما لم يتغيّر — بصراحة

- `Security:CspReportOnly` ما زال `true` افتراضيًا: **الفرض قرار نشر** بعد بروفة على
  بيئة `Production` حقيقية، ولا يُقلب من الكود.
- `CspReportUri` ما زال بلا جامع مركزي، وتحسين التحقق منه (مسار `/`-نسبي فقط) وتوحيد
  ردود `404` وتطهير مسار التطوير = الدفعة `W1`.
- `HSTS` ما زال معطّلًا افتراضيًا على `Render` التجريبي (المنصة تتولى `TLS`).

## 6. تحقق ما بعد النشر

```bash
# 1) CSP مفروضة وبلا upgrade-insecure-requests
curl -sI https://<host>/ | grep -i '^content-security-policy'

# 2) النسخة المراقبة لا تحمل الترويسة المفروضة
curl -sI https://<host>/ | grep -i '^content-security-policy-report-only'

# 3) HSTS بعد تفعيله على المخدّم الخاص (فقط عبر HTTPS)
curl -sI https://<host>/ | grep -i '^strict-transport-security'
#    ⇒ max-age=<HstsMaxAgeDays*86400>; includeSubDomains  (+ preload فقط إن فُعّل صراحةً)

# 4) حارس البناء محليًا
cd frontend && npm run build && node scripts/csp-guard.mjs
```

**التراجع:** `Security__CspReportOnly=true` + إعادة تشغيل (ثوانٍ، بلا نشر)؛ وإرجاع
`Security__HstsPreload=false` لا يزيل النطاق من قوائم `preload` فورًا — لذلك يبقى
افتراضيًا معطّلًا حتى النطاق النهائي.

**الهجرات:** صفر. لا حاجة إلى `dotnet ef database update` ولا إلى تعديل قائمة §9 التراكمية.
