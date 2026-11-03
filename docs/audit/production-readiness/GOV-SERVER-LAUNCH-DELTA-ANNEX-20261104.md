# ملحق تصحيحي لملف جاهزية النقل — 2026‑11‑04

> **الغرض:** تصحيح ادعاءات أمنية في `GOV-SERVER-LAUNCH-DELTA-20261103.md` بعد مراجعة أمنية
> نقدية أثبتت مخالفتها للكود الفعلي. الملف الأصلي **مجنَّد ولا يُحرَّر** (سياسة `docs/audit`)،
> فالتصحيح يُودَع هنا، مرجعًا لكل ادعاء بدليله.
>
> **النطاق المنفَّذ في هذه الدفعة (`W0`):** حراس إقلاع، تصفير ثقة الوكلاء، ضبط `AllowedHosts`
> مع عقد فحص الصحة، ومنع تخزين ردود `API`. **ما لم يُنفَّذ بعد:** فرض `CSP`، وحذف
> `upgrade-insecure-requests`، وتقليم `HSTS` — تبقى في الدفعة التالية، والادعاءات المتعلقة بها
> أدناه محدَّثة إلى «مشروط» لا «منجز».

---

## 1. تصحيح: «`KnownProxies: []` مغلق افتراضيًا ضد التزوير»

**الادعاء السابق** (`GOV-SERVER-LAUNCH-DELTA-20261103.md` §2): بلا ضبط `KnownProxies` «تبقى الحالة
مغلقة» ولا تُقرأ `X-Forwarded-For`.

**الواقع المقيس:** إعدادات الإطار الافتراضية تثق بـ`::1` و`127.0.0.0/8` (تحقّق عملي على
`net10.0`: `KnownProxies=[::1]`، `KnownIPNetworks=[127.0.0.0/8]`)، وكان الاختبار
`ForwardedHeadersOptionsTests` نفسه يوثّق «لا تمسّ افتراضيات الإطار (loopback)». الأثر: أي عملية
على المضيف نفسه تستطيع تزوير `X-Forwarded-For` ⇒ تجاوز مفتاح خنق الدخول (`IP:user`) وتلويث السجلات.

**المعالجة (`H-02`):** `ForwardedHeadersSetup.Configure` تُصفّر `KnownProxies` و`KnownIPNetworks`
قبل إضافة ما يُعلَن في الإعدادات، و`ForwardLimit = 1` صريحًا، وبلا وكيل معلَن تُعطّل المعالجة كليًا
(`ForwardedHeaders.None`). القائمة الموثوقة = ما تُصرّح به الإعدادات حصرًا.

> **مصيدة موثَّقة بالاختبار:** «قائمة فارغة» ليست «مغلقة». `ForwardedHeadersMiddleware` يعامل
> `KnownProxies.Count == 0 && KnownIPNetworks.Count == 0` كـ**«ثق بكل المصادر»**، فتصفير القوائم
> وحده كان سيفتح التزوير لكل عميل — أسوأ من الوضع السابق. لذلك يُعطَّل `ForwardedHeaders` نفسه.
> الحارس: `ForwardedHeadersMiddlewareTests.EmptyConfiguration_IgnoresLoopbackSpoofing`
> و`ForwardedHeadersOptionsTests.EmptyKnownProxies_TrustsNothing`.

**إلزامي على الوكيل:** `proxy_set_header X-Forwarded-For $remote_addr;` — **لا**
`$http_x_forwarded_for` (يمرّر قيمة العميل كما هي ⇒ تزوير مباشر).

> **تحذير تشغيلي:** بعد هذا التغيير، وكيل محلي (`nginx` على `127.0.0.1`) **لم يعد موثوقًا تلقائيًا**؛
> يجب إدراج عنوانه في `Security:KnownProxies` صراحةً، وإلا ظهر كل الزوار بعنوان الوكيل.

---

## 2. تصحيح: `AllowedHosts` وعقد فحص الصحة

**الادعاء السابق** (§2): «`AllowedHosts` = النطاق الحكومي — رفض أي `Host` آخر» بلا ذكر فحص الصحة.

**الواقع:** `HostFilteringMiddleware` يردّ `400` لأي `Host` غير مطابق، وفحص الصحة من الوكيل/الموازن
يصل عادةً بـ`Host: 127.0.0.1:8080` أو عنوان داخلي — فتضييق القائمة على النطاق وحده **يُسقط الفحص**
ويُعلن الموازن الخادم ميتًا.

**المعالجة (`H-04`):** قيمة الإنتاج تشمل النطاق **وعنوان الوكيل**:
`"AllowedHosts": "docgen.gov.sy;127.0.0.1;10.0.0.5"`. وقد أثبت اختبار تكامل
(`AllowedHostsIntegrationTests`) ثلاثية العقد:

| الطلب | النتيجة المتحققة |
|---|---|
| `Host: docgen.test` (مُدرج) | `200` |
| `Host: 127.0.0.1:8080` مع إدراج `127.0.0.1` وحده | `200` — **العنوان المجرّد يطابق `IP:port`** |
| `Host: evil.example` | `400` |

**بند إلزامي في نافذة النقل:** شغّل هذه الاختبارات الثلاثة بعد ضبط `AllowedHosts` وقبل تحويل حركة
الوكيل؛ فشل الحالة الثانية = توقّف الخدمة عند الموازن.

---

## 3. جديد: حارس بيئة `Development` (`H-02`)

**الخطر:** متغيّر بيئة واحد (`ASPNETCORE_ENVIRONMENT=Development`) على خادم منشور كان يُسقط خمسة
ضوابط دفعةً واحدة: تطهير رسائل الاستثناء (فيعود `exception.Message` الخام إلى المستخدم)،
والترحيل التلقائي عند الإقلاع، و`Swagger`، و`CORS` لأصول `localhost` مع `AllowCredentials`،
و`Secure` على كوكي `CSRF`.

**المعالجة:** `Program.cs` يرفض الإقلاع في `Development` بلا
`Security__AllowDevelopmentEnvironment=true` (مع استثناء زمن تصميم `EF` لأدوات `dotnet ef`).
الراية تُضبط محليًا من `launchSettings.json`/`user-secrets` فقط — **ولا تُضبط في `appsettings*.json`
ولا في متغيّرات بيئة الخادم**. الاختبار الحارس: `DevelopmentEnvironmentGuardTests`.

---

## 4. جديد: منع تخزين ردود `API` (`H-03`)

ردود `/api/**` تحمل `Cache-Control: no-store` من `SecurityHeadersMiddleware` (قبل `_next`، فيتقدّم
عليها أي متحكم يضبط السياسة صراحةً). الغرض: بيانات قانونية بعد المصادقة لا تُخزَّن في متصفح مشترك
ولا في وسيط تخزين أمامي. الاختبار: `SecurityHeadersMiddlewareTests` + `ApiResponse_IsNotCacheable`.

---

## 5. ادعاءات ما تزال **مشروطة** (لم تُنفَّذ بعد)

| الادعاء في الملحق الأصلي | الحالة الحقيقية اليوم |
|---|---|
| «`CSP` مفروضة على المخدّم الحكومي» | **لم تُفرض**: `Security:CspReportOnly=true` (مراقبة) بلا `CspReportUri` ⇒ لا جمع ولا منع. البروفة الإلزامية قبل الفرض لم تُنفَّذ بعد. |
| «`report-uri` مدعوم للجمع المركزي» | الكود يُلحق الترويسة فقط؛ **لا جامع ولا استثناء `CSRF`** ⇒ أي تقرير يُردّ `403` أو يبتلعه احتياطي `SPA`. |
| «`HSTS` سنة + `includeSubDomains` + `preload`» | لا يزال معطّلًا (`HstsEnabled=false`)، والتفعيل الخاطئ على نطاق مؤقت يعبّئ قائمة `preload` سنويًا. التقليم (فصل `preload`، وإتاحة `max-age`) في الدفعة التالية. |
| «`upgrade-insecure-requests` جزء من السياسة» | موجودة داخل السياسة؛ على خادم `HTTP`-only تُعطّل التطبيق كاملًا. الإزالة في الدفعة التالية (السياسة لا تُفرض قبل ذلك). |

---

## 6. بنود تحقق ما بعد النشر (أُضيفت بهذا الملحق)

```bash
# 1) منع التخزين على ردود API
curl -sI https://<host>/api/meta/current-year | grep -i '^cache-control'    # ⇒ no-store

# 2) فلترة المضيف: مضيف غريب يجب أن يُرفض
curl -s -o /dev/null -w '%{http_code}\n' -H 'Host: evil.example' https://<host>/healthz   # ⇒ 400

# 3) فحص الصحة بصيغة الوكيل يجب أن ينجح (وإلا انكسر الموازن)
curl -s -o /dev/null -w '%{http_code}\n' -H 'Host: 127.0.0.1' https://<host>/healthz       # ⇒ 200

# 4) لا تشغيل ببيئة تطوير: يجب أن ترفض الخدمة الإقلاع بلا الراية
#    (اختبار إقلاع في بيئة معزولة: ASPNETCORE_ENVIRONMENT=Development بلا Security__AllowDevelopmentEnvironment)
```
