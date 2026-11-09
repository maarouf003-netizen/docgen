# دليل التشغيل والتجربة

> ## ⚠️ قبل أي نشر على بيئة حقيقية — اقرأ هذا أولًا
>
> 1. **افتح §9 «تطبيق هجرات قاعدة البيانات عند النشر»** واقرأ **قائمة المهجرات المعلّقة**
>    (قائمة تراكمية في نهاية §9) — كل سطر غير مشطوب **يجب** تطبيقه في نافذة النشر.
> 2. **الهجرات المعلّقة حاليًا (2026-10-26):** سياق `PostgreSQL` = **صفر معلّقة** — طُبِّقت هجرات
>    التنبيهات الأربع (`HeadAlertTaxonomy_Expand_Pg` ثم `HeadAlertOutbox_Create_Pg` ثم
>    `HeadAlertTaxonomy_Backfill_Pg` ثم `HeadAlertTaxonomy_Contract_Pg`) على `Supabase` حيًا،
>    وسجل `__EFMigrationsHistory` = **63** هجرة (`docs/audit/10-head-alerts-pr2b-deploy-20261025.md`
>    و`docs/audit/13-head-alerts-pr3-live-apply-20261026.md`). أما **`SQLite`** فتبقى **4 هجرات**
>    معلّقة: `HeadAlertTaxonomy_Expand` ثم `HeadAlertOutbox_Create` ثم
>    `HeadAlertTaxonomy_Backfill` ثم `HeadAlertTaxonomy_Contract`.
>    **⛔ لا تُطبَّق `HeadAlertTaxonomy_Contract` (SQLite) قبل استيفاء G1/G2 وتأكيد أن كل النسخ تعمل
>    بكود `PR‑2` وصفر قيم `NULL`** — راجع `docs/audit/11-head-alerts-pr3-20261026.md`.
> 3. **⚠️ `Database:AutoMigrate=false` في الإنتاج** ⇒ التطبيق **يرفض الإقلاع** إن كانت الهجرات
>    غير مطبّقة على القاعدة الحية. **طبّق الهجرات قبل نشر النسخة، وإلا انقطاع إقلاع مقصود.**
> 4. **نسخة احتياطية + استعادة مجرَّبة قبل النافذة** — لا «نسخة لم تُجرَّب استعادتها ليست نسخة».
> 5. **بعد النشر**: فحص دخان (دخول + فتح `/alerts` + إنشاء تنبيه + تعليم قراءة + فتح تفاصيل
>    نص طويل 12,000 حرف + عدّاد الشارة + `/alerts/sent`).

## 0) التشغيل بكبسة زر (الأسهل)

انقر نقراً مزدوجاً على `start-app.bat` في جذر المشروع (أي في نفس المجلد الذي يحتوي `backend\` و`frontend\`):

1. يفتح نافذة «DocGen API» (الخلفية على `http://localhost:5199`) ونافذة «DocGen Frontend» (الواجهة على `http://localhost:5173`).
2. ينتظر ~15 ثانية ثم يفتح المتصفح تلقائياً على `http://localhost:5173`.
3. بعد التجربة انقر `stop-app.bat` لإيقاف الخدمتين.

> يتطلب `.NET SDK 10` و`Node.js 20+` في المسارات الافتراضية (`C:\Program Files\dotnet` و`C:\Program Files\nodejs`).
> يمكنك إيقاف التشغيل يدوياً أيضاً بالطريقة اليدوية في القسم 8.

## 1) المتطلبات

| الأداة | الإصدار | ملاحظة |
|--------|---------|--------|
| .NET SDK | 10.x | مطلوب للخلفية |
| Node.js | 20+ | مطلوب للواجهة |

> ⚠️ في هذا الجهاز الأدوات ليست على `PATH`، لذا في كل نافذة PowerShell جديدة نفّذ أولاً:
> ```powershell
> $env:PATH="C:\Program Files\dotnet;C:\Program Files\nodejs;$env:PATH"
> ```

## 2) تشغيل الخلفية (API)

```powershell
cd "C:\Users\pc\Desktop\document_generator\working 3.8 32b\backend"
dotnet run --project src/DocGenerator.Api --urls http://localhost:5199
```

- عند أول تشغيل تُنشأ قاعدة SQLite `docgen.db` تلقائياً (migrations + بذر المستخدمين والفروع).
- يظهر نص مثل `Now listening on: http://localhost:5199` — اترك النافذة مفتوحة.

## 3) تشغيل الواجهة (Vite) — نافذة جديدة

```powershell
cd "C:\Users\pc\Desktop\document_generator\working 3.8 32b\frontend"
npm run dev
```

- تفتح على `http://localhost:5173` وتُمرر طلبات `/api` تلقائياً إلى الخلفية على المنفذ `5199`.

## 4) فتح المتصفح والتسجيل

افتح `http://localhost:5173` وسجّل الدخول. الحسابات المبذرة في `Development`: `admin`, `manager`, `head1`, `lawyer1`.

> كلمة سر البذر **عشوائية** وتُطبع في نافذة أوامر الخلفية عند إنشاء حسابات البذر أول مرة فقط:
> `[DbSeeder] Development accounts seeded. One-time password: <كلمة عشوائية>` (نتيجة RF-006/SEC-004 — لا كلمة عامة ثابتة).

| المستخدم | الدور | ماذا يفعل |
|----------|-------|-----------|
| `admin` | مشرف نظام | كل الصلاحيات + سجل التدقيق + نشاط المستخدمين |
| `manager` | مدير | قراءة كل المستندات، لا يُدخل ولا يحذف، سجل التدقيق |
| `head1` | رئيس قسم دمشق | يدخل ويعدّل ويحذف مستندات فرعه، يغيّر الحالة |
| `lawyer1` | محامي دمشق | يدخل ويعدّل **مستنداته فقط**، يغيّر حالتها |

## 5) سيناريوهات تجربة جاهزة

### أ) إدارة المستندات (كـ `lawyer1`)
- «مستند جديد» → املأ اسم المقترض ونوع العقد والمبلغ → حفظ.
- المستند يظهر في «المستندات» كمسودة.
- افتحه → «تغيير الحالة» → اختر «منفذ» أو «تريث» وأدخل بيانات البراءة/التريث.

### ب) الصلاحيات (كـ `head1`)
- جرّب الحذف → ينجح. ثم ادخل بنفسك كـ `manager` وجرّب الحذف → مرفوض.
- كـ `manager` حاول «مستند جديد» → رسالة «المدير لا يمكنه إدخال مستندات».

### ج) سجل التدقيق (كـ `admin` أو `manager`)
- من القائمة الجانبية «سجل التدقيق» → تشاهد كل الأحداث (`login`, `create`, `status`, `delete`...).
- استخدم فلتر المستخدم ونوع الحدث، وترقيم الصفحات في الأسفل.

### د) تغيير كلمة المرور
- من القائمة الجانبية «تغيير كلمة المرور» → غيّرها، ثم سجّل خروجاً وأعد الدخول بالجديدة.

### هـ) تحديد المحاولات (Rate Limit)
- من شاشة الدخول أدخل كلمة سر خاطئة 6 مرات → يظهر «محاولات دخول كثيرة جداً» (429) لمدة 5 دقائق.

## 6) تشغيل الاختبارات (اختياري)

الخلفية:

```powershell
cd "C:\Users\pc\Desktop\document_generator\working 3.8 32b\backend"
dotnet test DocGenerator.sln
```

النتيجة المتوقعة: ما يزيد على `2500` اختبار (نحو `1850` وحدة/خدمة + `650` تكامل عبر `WebApplicationFactory` بقاعدة مؤقتة معزولة — لن تلمس `docgen.db`). الأرقام تقريبية بتاريخ 2026-10-16 وتكبر مع كل ميزة.

> بوابة التسلسل: اختبارات وسيط الإثراء (`EnricherMiddlewareTests`) موسومة
> `Category=Sequential` لأنها تستبدل `Log.Logger` العام — عند أي فشل عابر فيها
> أعد التشغيل على خطوتين (الكل متوازيًا عدا الوسم، ثم الوسم وحده تسلسليًا):
>
> ```powershell
> dotnet test backend/DocGenerator.sln --filter "Category!=Sequential"
> dotnet test backend/tests/DocGenerator.Api.Tests --filter "Category=Sequential"
> ```

الواجهة:

```powershell
cd "C:\Users\pc\Desktop\document_generator\working 3.8 32b\frontend"
npm test
```

النتيجة المتوقعة: ما يزيد على `1500` اختبار (Vitest + Testing Library) تغطي النماذج والصفحات والمنطق (الرقم تقريبي بتاريخ 2026-10-16).

## 7) Swagger (توثيق API)

الخلفية شغّالة → افتح `http://localhost:5199/swagger` في المتصفح لاستعراض/تجربة كل نقاط النهاية
(يتطلب زر `Authorize` بإدخال التوكن). مغلق خارج بيئة التطوير ما لم تُفعّل `Swagger:Enabled`.

## 8) الإيقاف

- **الأسهل:** انقر `stop-app.bat` (يوقف الخدمتين على المنفذين `5199` و`5173`).
- أو يدوياً:
  - أوقف الواجهة: اضغط `Ctrl+C` في نافذة Vite.
  - أوقف الخلفية: اضغط `Ctrl+C` في نافذة `dotnet run`.

## 9) ملاحظات الإنتاج

- فعّل `Database:UsePostgres = true` واضبط `ConnectionStrings:DefaultConnection` على PostgreSQL.
  خارج التطوير بلا `Postgres` (ولا `DATABASE_URL`) يرفض التطبيق الإقلاع عمدًا (لا وقوع صامت على `SQLite`) —
  والاستثناء الوحيد عقدة داخلية صغيرة بقرار صريح: `Database:AllowSqliteInProduction = true`.
  `DATABASE_URL` فارغة تُعامَل كغائبة.
- عيّن `Jwt:Secret` قوياً (إلزامي خارج بيئة التطوير).
- الإقلاع المتزامن لنسخ عدة على `Postgres` يعتمد قفل الهجرات المدمج في `EF Core` — يمنع التسابق على
  الهجرات دون قفل استشاري مخصص، لكنه لا يغني عن النسخة الاحتياطية قبل النشر (الهجرة نفسها قد تسقط).
- `Swagger:Enabled` مغلق افتراضياً خارج التطوير؛ فعّله صراحةً عند الحاجة.
- `RateLimiting:MaxLoginAttempts` / `RateLimiting:WindowMinutes` — إعدادات تحديد محاولات الدخول
  (مخزّنة في جدول `LoginAttempts` لتكون مشتركة بين عقد النشر).
- `Logging:File` — مجلد `logs/` (تحت جذر الخلفية `backend/src/DocGenerator.Api/`) **يجب أن يكون قابلًا للكتابة
  على المضيف**، وإلا تعذّر إنشاء ملف السجل اليومي (`logs/logs-YYYYMMDD.txt`) وبقيت الأخطاء على `stdout`
  فقط. في الحاويات ثبّت المجلد عبر `mount`/`volume`، أو غيّر `Logging:File:Path` إلى مسار قابل للكتابة
  (الاحتفاظ 60 يومًا بقرار المالك 2026-10-16، وبحد 50MB للملف عبر `RetainedDays` / `FileSizeLimitBytes`). المجلد مُتجاهَل في `git`
  (`backend/src/DocGenerator.Api/.gitignore`) فلا يُرفع مع الكود — أنشئه بأذونات كتابة عند النشر.
- `Database:AutoMigrate` — **مغلق افتراضيًا** (`false`) في `appsettings.json` (قاعدة الإنتاج) ومفتوح
  (`true`) في `appsettings.Development.json` (تطوير/اختبار). مع الإغلاق **لا يطبّق التطبيق الهجرات عند
  الإقلاع**، بل يتحقق من مطابقة المخطط لنسخة التطبيق: فإن وُجدت هجرات معلّقة **يرفض الإقلاع** برسالة
  تحمل أوامر التطبيق (§ «تطبيق هجرات قاعدة البيانات عند النشر» أدناه). فالتطبيق الصريح يسبق تشغيل النسخة.
  - **الترتيب الإلزامي في النافذة:** (1) نسخة احتياطية + استعادة مجرَّبة، (2) تطبيق الهجرات على القاعدة
    الحية، (3) التحقق من `__EFMigrationsHistory`، (4) **ثم** تشغيل النسخة الجديدة. تطبيق النسخة قبل
    الهجرات = رفض إقلاع مقصود (لا «no such column» عند أول طلب).
  - في `SQLite` تُعطَّل المفاتيح الأجنبية قبل الترحيل وتُفحص بعده (`PRAGMA foreign_keys=OFF` قبل
    `BEGIN`، ثم `foreign_key_check` و`integrity_check`) — يتولاها `DatabaseInitializer` تلقائيًا في
    التطوير/الاختبار؛ وفي الإنتاج يُطبَّق السكربت الصريح:
    ```powershell
    dotnet ef migrations script --context DocGeneratorDbContext --idempotent
    ```
    مع `PRAGMA foreign_keys=OFF` قبل `BEGIN` صراحةً.

### اقتران نشر `PR-4` + `PR-5` (تنبيهات — إلزامي)

- **حالة الزوج (2026-11-02):** `#54` = `97a4a93` و`#55` = `4e421f4` **مدموجان ومنشوران حيًّا** على `Render`
  (`Supabase`/`PostgreSQL`)؛ والنشر تلقائي على `master` بصورة واحدة ⇒ لا نافذة اقتران ولا خطوة `dotnet ef`
  (الحيّ `PG` على 63 هجرة). `PR‑5b` (`#58` = `db7aefd`) منشور كذلك. دخان §د‑4 هو المتبقي.
- **`PR-4` لا يُنشر وحده** (ولا يُدمج قبل جهوزية `PR-5`): القوائم تحوّلت إلى
  `PagedResult<HeadAlertSummaryDto>`، والواجهة المنشورة (`Dashboard`/`AccountPage`/`DelegationActivityStrip`)
  ما تزال تتوقع **مصفوفة** — فالنشر المنفرد يعني قوائم فارغة ونصوصًا مفقودة **بصمت**
  (`docs/HEAD_ALERTS_RESTRUCTURE_PLAN.md` §15).
- **الترتيب المعتمد (`D10=atomic` — قرار المالك 2026-10-31):** نشر واحد متزامن (الخلفية ثم الواجهة
  فورًا داخل النافذة نفسها)؛ **وحدة التراجع = الزوج**، فتراجع أحدهما وحده يكسر القائمة أو النص.
  - (مرجعي فقط إن غُيّر القرار إلى `normalizer`: الواجهة أولًا تقبل الشكلين، ثم الخلفية، والتراجع بالعكس.)
- **لا هجرات في الزوج:** لا خطوة `dotnet ef database update` إضافية، وقائمة «هجرات بانتظار التطبيق» أدناه
  تبقى كما هي (`SQLite` 4 معلّقة، `PostgreSQL` 63/صفر).
- **فحص دخاني بعد النافذة (إلزامي):**
  - `GET /alerts` و`/alerts/sent` و`/alerts/by-delegation/{id}` → `{items,page,perPage,totalCount,totalPages}`.
  - `POST /alerts` → `201` بجسم ملخص + `Location`.
  - `PATCH /alerts/{id}/read` → `204` بلا جسم.
  - شارة غير المقروء: **`?scope=user` لكل الأدوار** («غير المقروء لي») — في لوحة التحكم وصفحة
    الحساب وصفحة `/alerts` **معًا** (رقم واحد)، وليست مجموع الصفحة المعروضة. (حُسم 2026‑11‑02:
    `received` للرئيس/الشعبة كان تجميعيًا فيبقى رقمًا بعد قراءة صاحب الحساب لكل تنبيهاته.)
    **ومنذ `PR‑6a`:** `?scope=received` → `400` برسالة عربية («قيمة scope غير صالحة — استخدم user»)
    — لا 500 ولا سقوط صامت إلى `user`، ولا هجرة مرافقة.
  - قائمة أطول من 100: زر «تحميل المزيد» يجلب الصفحة التالية ويُلحقها بلا تكرار، ويختفي عند
    اكتمال التحميل؛ وفشل الجلب يعرض «إعادة المحاولة» (لا شاشة بلا مخرج) ويُبقي المعروض،
    وإعادة التحميل بعد إصدار تنبيه لا تُفرغ القائمة أثناء الجلب.
  - إزاحة الترقيم (تنبيه جديد يُدرج بين تحميل صفحتين): لا تتكرر الطلبات بلا نهاية — يظهر
    «تغيّرت القائمة أثناء التصفّح فتعذّر تحميل البقية» مع زر «تحديث» يعيد من الصفحة الأولى،
    ويختفي «تحميل المزيد» (لا وعد ترقيم لا يُغلق ولا صفحات فارغة متتالية).
  - رئيس/شعبة بلا فرع: حالة صريحة («حسابك غير مرتبط بفرع…») بلا طلبات تنبيهات ولا `403` متوقع.
  - **تعليم المقروء للرئيس/الشعبة:** زر «تمت القراءة» في تبويب «مستلمة» يُنقص شارة `user`
    وعدّاد الصف بمقدار واحد لكلٍّ (تنبيهات نطاق `head` تُستلَم فعلًا)، ولا يظهر في «مرسلة».
  - **فشل العقد يُعلَن لا يُبتلع:** جسم قائمة لا يطابق `{items,page,perPage,totalCount}` (ومنها
    المصفوفة القديمة) يعطي لافتة خطأ عربية صريحة + «إعادة المحاولة» في اللوحة وشريط الإنابة —
    لا «لا توجد تنبيهات» كاذبة ولا اختفاءً صامتًا (حارس `readAlertsPage`).
  - `GET /alerts/{id}` يعيد `message` كاملًا، والقوائم لا تحمله.
  - أي فشل ⇒ تراجع للزوج، أو للواجهة أولًا في وضع `normalizer`.

### قائمة النشر الملزمة (بوابات — صالحة لأي مضيف)

> المضيف النهائي مجهول بعد؛ `render.yaml` هدف مرجعي لا قيد. أي مضيف يُقبل إن حقق الشروط الأربعة: (1) قاعدة `Postgres` مدارة، (2) نسخ يومي + استعادة مختبَرة (§ النسخ أدناه)، (3) `TLS` كامل للتطبيق والقاعدة، (4) أسرار بيئية (`Jwt__Secret` + `Bootstrap__AdminPassword`) لا في المستودع أبدًا.

- **قبل النافذة**: نسخة احتياطية + تسجيل الهجرات المطبقة حيًا (`SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY 1;`) + فحص `Postgres` قبل النشر (§ أدناه) + استعلامات الكشف المطلوبة للهجرات ذات ⚠️ (§ القائمة التراكمية).
- **أثناء النافذة**: تطبيق السياقين (`database update` مزدوج — § أعلاه) + فحص الانحراف باتصال قراءة فقط (مقارنة سجل `__EFMigrationsHistory` الحي مع هجرات الحزمة — أي فرق = إيقاف وتحقيق قبل المتابعة).
- **بعد النافذة (فحص دخاني)**: دخول + بحث + فتح تفاصيل + توليد + `GET /healthz` — أي فشل = تراجع للنسخة.
- **التراجع**: استعادة النسخة حصرًا (لا `Down` على الحي — § سلامة التراجع).

### إعدادات الحماية للمخدّم الخاص (خطة `docs/SECURITY_PLAN.md`)

- `Security:KnownProxies` — **إلزامي على المخدّم الخاص**: عناوين `IP` البروكسي العكسي (`nginx`/`Traefik`)
  أو نطاقات `CIDR` ضيقة حصرًا (مثال `["10.0.0.5"]` أو `["10.0.0.0/31"]`)؛ **لا تُستخدم نطاقات واسعة**
  كـ`/8` لأن أي مضيف فيها يستطيع تزوير `X-Forwarded-For`. بدونها يُرى كل الزوار بعنوان البروكسي
  فينهار مفتاح تحديد محاولات الدخول (`IP:user`) وتُشوَّه سجلات التدقيق.
  - **مغلقة افتراضيًا فعليًا (`H-02`)**: بلا وكيل معلَن تُصفَّر قوائم الثقة وتُعطَّل معالجة
    `X-Forwarded-*` كليًا (`ForwardedHeaders.None`) — بما فيها افتراضيات الإطار (`::1` و`127.0.0.0/8`)
    التي كانت تجعل أي عملية محلية قادرة على تزوير `X-Forwarded-For`.
    ⚠️ **لا تكتب `[]` وتظنّها مغلقة**: قائمتا ثقة فارغتان بلا تعطيل صريح تعنيان عند الإطار
    «ثق بكل المصادر» — وهذا سبب اعتماد `None` في الكود، فلا تُعِد المنطق يدويًا في وكيل خارجي.
  - **إعداد الوكيل إلزامي أيضًا**: `proxy_set_header X-Forwarded-For $remote_addr;` (لا
    `$http_x_forwarded_for` الذي يمرّر قيمة العميل كما هي ⇒ تزوير مباشر)، مع `X-Forwarded-Proto`.
- `Security:AllowDevelopmentEnvironment` — **`false` افتراضيًا (`H-02`)**: تشغيل `Development` يُعطّل
  تطهير رسائل الاستثناء ويُفعّل الترحيل التلقائي و`Swagger` و`CORS` المحلي ويجعل كوكي `CSRF` بلا
  `Secure`، فترفض الخدمة الإقلاع بلا راية صريحة. تُضبط محليًا من `launchSettings.json` أو
  `dotnet user-secrets` فقط — **ولا تُضبط في `appsettings*.json` ولا في متغيّرات بيئة الخادم**.
  (استثناء مقصود: `dotnet ef` زمن التصميم يبني المضيف بلا راية فلا يحتاجها محليًا.)
- `Security:HstsEnabled` / `Security:HttpsRedirectionEnabled` — `false` افتراضيًا (لـ `Render` التجريبي
  الذي يتولى `TLS`)، و`true` على المخدّم الخاص **بعد التأكد من `HTTPS` الكامل**.
  - **خلف وكيل ينهي `TLS`:** `UseHsts` لا يرسل الترويسة إلا إذا كان `Request.IsHttps=true`، وهذا لا
    يتحقق إلا بـ`X-Forwarded-Proto` من وكيل **معلَن** في `Security:KnownProxies` (انظر بند الوكيل
    أعلاه). بلا ذلك يعمل `HTTPS` للمستخدم بينما **`HSTS` غائب صامتًا** — افحصه بعد النشر بالأمر أدناه.
- `Security:HstsMaxAgeDays` — مدة `HSTS` بالأيام، `365` افتراضيًا. القيمة غير الموجبة أو التي تتجاوز
  السقف `3650` يومًا (عشر سنوات) ترفض الإقلاع برسالة تسمّي المفتاح (بدل ترويسة لا يعمل بها المتصفح
  أو فيض في `TimeSpan`).
- `Security:HstsIncludeSubDomains` — `false` افتراضيًا، و**راية صريحة**: الترويسة تفرض `HTTPS` على
  كل النطاقات الفرعية، فأي نطاق فرعي يخدم عبر `HTTP` فقط ينكسر فورًا. يُفعَّل بعد تأكيد `HTTPS`
  الكامل لكل النطاقات الفرعية. **يُفرَض `true` تلقائيًا عند `HstsPreload=true`** لأن قوائم المتصفحات
  لا تقبل `preload` بدونه.
- `Security:HstsPreload` — `false` افتراضيًا، و**مستقل عن `HstsEnabled` عمدًا**: إدراج النطاق في قوائم
  `preload` المدمجة التزام شبه دائم (إزالته تستغرق أشهرًا)، فلا يُفعَّل إلا على النطاق النهائي بعد سنة
  من `HTTPS` كامل. يرفض الإقلاع إن فُعّل بلا `HstsEnabled` أو مع `HstsMaxAgeDays < 365` (متطلب
  المتصفحات). **`preload` إشارة أهلية لا إدراج تلقائي**: بعد النشر يجب تقديم النطاق يدويًا في
  `https://hstspreload.org` وقبول مراجعته، وإلا بقيت الترويسة بلا أثر في القوائم المدمجة.
  الرمز: `HstsSetup.Validate`/`Configure`، والحراس: `HstsSetupTests` + `HstsHeaderTests`
  + `HstsProductionIntegrationTests`.
- `Security:CspReportOnly` — `true` افتراضيًا (وضع المراقبة على التجريبي)، و`false` على المخدّم الخاص
  بعد جولة اختبار واحدة لفرض `Content-Security-Policy`.
  - **`upgrade-insecure-requests` أُزيلت من السياسة نهائيًا (`W0-CSP`)**: على خادم يخدم عبر `HTTP`
    (حالة النقل الأولى على الشبكة الداخلية) تُحوِّل المتصفحات كل أصول الصفحة إلى `https` فتفشل ⇒
    **صفحة بيضاء**. تحديث البروتوكول مسؤولية `HSTS`/`HttpsRedirection` صراحةً. لا تُعِدها عند تعديل
    `SecurityHeadersMiddleware` (الحارس: `SecurityHeadersMiddlewareTests.Csp_DoesNotUpgradeInsecureRequests`).
  - **حارس بناء `CSP`:** `cd frontend && npm run build && npm run guard` — يفشل عند أي
    سكربت/معالج حدث مضمّن، أو أصل خارجي في وسوم الموارد أو في `CSS` (`url(...)`/`@import`)،
    أو `eval`/`Function`/`WebAssembly` في حزم `dist`، أو مرجع `/assets/...` غير موجود
    (بعلامات تنصيص مفردة أو مزدوجة). يعمل في وظيفة `frontend` بـ`CI` بعد البناء **وفي مرحلة
    بناء `Dockerfile`**، فلا تُكتشف المخالفة أول مرة في متصفح المستخدم.
  - **بروفة إلزامية قبل الفرض** (بيئة `Production` حقيقية لا `Vite`): افتح الصفحات الرئيسية
    وسجّل أي مخالفة في وحدة تحكم المتصفح، ثم مرّ على المسارات التي تعتمد أصولًا ديناميكية:
    1. **تنزيل `Word`/`Excel`**: ملف `.docx` وملف `.xlsx` فعليان (المسار `blob:` + رابط تنزيل) —
       لا حجب `blob:` في `default-src`.
    2. **معاينة الصور**: صورة مرفوعة (`img-src 'self' data: blob:`) وظهورها كاملًا.
    3. **المحرر الغني**: كتابة نص منسّق (عريض/ألوان) ثم حفظه وإعادة عرضه (`span[style]` مع
       `style-src 'unsafe-inline'`).
    4. **الطباعة**: `Ctrl+P` على ملف طويل — بلا عناصر مقطوعة أو أنماط مفقودة.
    5. **روابط `SPA` العميقة**: فتح `/documents/<id>` مباشرة وتحديث الصفحة (`MapFallbackToFile`)
       ثم مسار غير موجود للتأكد من صفحة `404` لا صفحة بيضاء.
    الاختبار `CspProductionIntegrationTests` يثبت أن الترويسة المفروضة تصل مع مستند
    `index.html` فعلًا، لكنه لا يغني عن هذه الجولة السلوكية.
- `Security:CspReportUri` — **المسار المدمج `/api/csp-report` حرفيًا فقط**، أو فارغ لتعطيل
  الجمع. أي قيمة أخرى (رابط مطلق، `//host`, مسار مختلف، مسافة/محارف مكسرة) **تمنع الإقلاع**
  برسالة تسمّي المفتاح (`CspSetup.Validate`)، لأن قبول مسار غير جامع يعني فقدان التقارير
  بصمت أو توجيهها إلى مسار حساس.
  - **جامع جاهز على الأصل نفسه:** `POST /api/csp-report` يستقبل `application/csp-report`
    (القديم) و`application/reports+json` (الحديث)، بحد حجم `16KB` وحد
    `RateLimiting:CspReportPerMinute` (60 افتراضيًا) لكل `IP`، ويُرجع `204`.
    عند ضبطه تُضاف `report-uri` (احتياط) و`report-to csp` مع ترويسة
    `Reporting-Endpoints: csp="/api/csp-report"` **و**`Report-To` القديمة (JSON) للمتصفحات
    الأقدم. النقطة عامة بلا مصادقة/CSRF عمدًا (المتصفح يرسل التقرير تلقائيًا)، فلا تضعها
    خلف بوابة دخول.
  - **السجل:** سطر ملخص واحد لكل طلب (`Count`/`Truncated`/أول 5 توجيهات/أول عيّنة) بدل
    سطر لكل تقرير؛ العناوين تُجرَّد من الاستعلام/المقطع، ولا تُسجَّل الحمولة الخام.
  - `report-to` يتطلب سياقًا آمنًا (`HTTPS`)؛ على `HTTP` الداخلي يبقى `report-uri` هو
    المسار الفعلي (غير مشفّر — البيانات لا تحمل أسرارًا لكنها تحمل مسارات الصفحات).
  - الحارس: `SecurityHeadersMiddlewareTests.ReportUri_*` + `CspSetupTests` +
    `CspReportEndpointTests` + `CspProductionIntegrationTests`.
- `AllowedHosts` — `*` للتطوير فقط؛ على الإنتاج النطاق الحقيقي حصرًا **مع عنوان الوكيل/الموازن**:
  مثال `"docgen.gov.sy;127.0.0.1;10.0.0.5"`. فلتر المضيف يردّ `400` لأي `Host` آخر، وفحص الصحة يصل
  عادةً بـ`Host: 127.0.0.1:8080` — وقد تحقّقنا باختبار أن **إدراج العنوان وحده يطابق `IP:port`**،
  أما إدراج النطاق وحده فيُسقط فحص الصحة ويُعلن الموازن الخادم ميتًا. الاختبارات الحارسة:
  `AllowedHostsIntegrationTests` (مُدرج ⇒ 200، `IP:port` ⇒ 200، غريب ⇒ 400) — شغّلها بعد أي تعديل هنا.
- `Jwt:Secret` — 32 بايت (256 بت) حدًا أدنى، ويُرفض الإقلاع بدونه؛ دوّر `Jwt__Secret` وسلسلة `Postgres`
  دوريًا ولا تُخزَّن في المستودع أبدًا.
- `RateLimiting` — الحدود العامة (`general-anon` لكل `IP` و`general-auth` لكل مستخدم و`expensive`
  للتصدير/الإحصاءات) تُضبط من `appsettings.json` بلا إعادة بناء، وتُراقب الردود `429` بعد النشر
  لضبط القيم على الاستخدام الحقيقي. `csp-report` لكل `IP` حدود مستقلة (`CspReportPerMinute`،
  افتراضي `60`) لحماية جامع تقارير `CSP` العام.
- `Cache-Control` — ردود `/api/**` تحمل `no-store` تلقائيًا (`H-03`) فلا تُخزَّن بيانات ما بعد
  المصادقة في متصفح مشترك ولا في وسيط تخزين أمامي. تحقُّق ما بعد النشر:
  `curl -sI https://<host>/api/meta/current-year | grep -i cache-control` ⇒ `no-store`.
- `CSP` — تحقُّق ما بعد الفرض (`CspReportOnly=false`): `curl -sI https://<host>/ | grep -i '^content-security-policy'`
  ⇒ ترويسة `Content-Security-Policy` (لا `Report-Only`) **بلا** `upgrade-insecure-requests`، ومعها
  `script-src 'self'`. وإن ظهرت صفحة بيضاء بعد الفرض فالسبب المعتاد أصل خارجي أو سكربت مضمّن —
  شغّل `cd frontend && npm run guard` بعد البناء لالتقاطه قبل النشر.
- `HSTS` — تحقُّق ما بعد التفعيل على المخدّم الخاص (بعد التأكد أن `KnownProxies` يمرّر
  `X-Forwarded-Proto`): `curl -sI https://<host>/ | grep -i '^strict-transport-security'`
  ⇒ `max-age=<HstsMaxAgeDays*86400>` مع `includeSubDomains` فقط إن فُعّلت رايته (أو مع `preload`)،
  و`preload` فقط إن فُعّل صراحةً. **غياب الترويسة مع نجاح `HTTPS` يعني أن الوكيل غير موثوق أو لا
  يمرّر `X-Forwarded-Proto`** — راجع `Security:KnownProxies` أولًا، ثم أعد الاختبار.

### تطبيق هجرات قاعدة البيانات عند النشر (إلزامي)

- أي تغيير في الكود يضيف/يعدّل هجرات EF (`Persistence\Migrations\` لـ SQLite و`Persistence\MigrationsPostgres\` لـ PostgreSQL) **لا يصل إلى قواعد البيانات الموجودة تلقائيًا** — الكود الجديد وحده لا يكفي.
- أثناء نافذة نشر خاضعة للتحكم نفّذ على الخادم (من مجلد `backend\`):
  ```powershell
  dotnet ef database update --context DocGeneratorDbContext          # قاعدة SQLite المحلية
  dotnet ef database update --context DocGeneratorPostgresDbContext   # قاعدة PostgreSQL في الإنتاج
  ```
- تتصل الأوامر بسلسلة الاتصال الموجودة في `appsettings.json` لبيئة الخادم (مع `Database:UsePostgres = true` لقاعدة الإنتاج). إن لم يكن `dotnet-ef` مثبتًا عالميًا: `dotnet tool install --global dotnet-ef`.
- **سياسة «صفر تحذيرات» تسري على هذين الأمرين** (وهما يبنيان الخلفية وقت التصميم): أي تحذير مترجم/محلل/NuGet
  أو من محرك MSBuild في مرحلة البناء يُفشِلهما برسالة `Build failed. Use dotnet build to see the errors.` —
  أصلح التحذير أو وثّق `NoWarn` مضيقة في الـ`PR` (المصدر: `backend/Directory.Build.props`؛ وتُغطّى تحذيرات
  تقييم المشروع المبكرة في بوابة CI بمفتاح `-warnaserror`).
- **هجرات معلّقة من دفعة `l-main-page` (2026-09-28) — طبّقها في النشر القادم ثم احذف هذا البند:**
  - SQLite (`Persistence\Migrations\`): `20260928082242_AddPersonalReminders` (جدول `PersonalReminders`)، `20260928085246_AddAppSuggestions` (جدول `AppSuggestions`).
  - PostgreSQL (`Persistence\MigrationsPostgres\`): `20260928082313_AddPersonalRemindersPg`، `20260928085307_AddAppSuggestionsPg`.
  - الأمران أعلاه يطبّقانها معًا؛ إهمالها = فشل تشغيل فعلي (`no such column`) رغم نجاح كل الاختبارات محليًا.
- **تذكير للجلسات القادمة**: بعد كل تعديل يضيف هجرات جديدة، يجب ذكرها بالاسم وعددها في تقرير الإنجاز وتنبيه النشر — انظر `AGENTS.md`.

### سلامة التراجع `Down` (تحذيرات — `RF-001`/`INT-013`)

- القاعدة: **لا تراجع (`MigrateAsync` لنسخة أقدم) على أي قاعدة تحمل بيانات حية** — التراجع
  للطوارئ فقط بعد نسخة احتياطية مُتحقَّق منها (§ النسخ الاحتياطي أعلاه)، ويُختبَر حصرًا على نسخة
  مهملة لا على الإنتاج.
- حالات موثقة في هذا المستودع:
  - `AllowMultipleBaseNumbersPerYear` (وهجرة `Pg` نظيرتها): الـ`Down` يعيد إنشاء فهرسين فريدين —
    **يفشل بوجود أي رقم أساس مكرر** (موثق في تعليق الهجرة). قبل أي تراجع: ابحث عن التكرار ونظّفه.
  - `AddAppSuggestions` (ونظيرتها): الـ`Down` يسقط الجدول `AppSuggestions` — **خسارة بيانات**
    الاقتراحات عند الرجوع.
  - هجرتا التعبئة `BackfillMirrorLawyer` و`BackfillDelegationTargetCourt`: الـ`Down` فارغ عمدًا
    (التعبئة غير قابلة للعكس) — آمنة إعادةً (`idempotent`) لكن لا تراجع لها.
  - `AddExecutionCircuitRegistry` (ونظيرتها): الـ`Down` يسقط جدول `ExecutionCircuits` والأعمدة
    `ExecutionCircuitId`/`DelegatedCircuitId`/`NeedsRegistration` — **خسارة روابط الدوائر نهائيًا**
    (لا ترقية لاحقة تستعيدها)، ويعيد إنشاء فهرسين فريدين عامين على `Court/CourtNorm` —
    **يفشل بوجود دائرتين بالاسم نفسه في فرعين بمحافظة واحدة** (مسموح في النموذج الجديد).
- هجرات التحويل/الحذف مستقبلًا (مثل قيد الترقيم الفريد في `RF-009`) تتطلب: تنظيف البيانات المخالفة
  أولًا، ونسخة احتياطية، ونص `SQL` مولّد محفوظ في `docs/audit/migrations/`، واعتمادًا صريحًا منفصلًا.

### فحص Postgres قبل النشر (إلزامي عند تغيّر المخطط)

- الاختبارات اليومية تعمل على SQLite المتسامح (`datetime2` كنص، بلا فرض `Utc`، بلا فرض دقة عشرية) ولا تُغني عن فحص Postgres الصارم (`timestamptz` يرفض أي `DateTime` بلا `Kind=Utc`) — «أخضر محليًا ≠ أخضر إنتاجًا».
- قبل أي نشر يمسّ المخطط: ارفع Postgres مؤقتًا (حاوية `Docker` تُحذف بعد الفحص، أو ثنائي محمول، أو قاعدة مرحلية على الاستضافة — لا شيء دائم على جهازك ولا مساس بـ `docgen.db`)، طبّق هجرات Postgres، ثم شغّل:
  ```powershell
  $env:DOCGEN_TEST_POSTGRES="Host=...;Port=5432;Database=...;Username=...;Password=..."
  dotnet test backend/tests/DocGenerator.Api.Tests --filter "FullyQualifiedName~DatabaseInitializerTests"
  ```
  (يُفعّل هذا اختبار Postgres الاختياري في `DatabaseInitializerTests` الذي يُتجاهل بدونه).
- لا تنشر إن فشل الفحص — أصلح الانجراف أولًا (سابقة: `AlignPostgresTimestampTypes`).

### النسخ الاحتياطي لقاعدة الإنتاج (إلزامي)

- الاستضافة المُدارة توفر نسخًا آلية (`Postgres` مُدار): فعّل النسخ اليومي + الاسترداد لنقطة زمنية (`PITR`) من لوحة المضيف، واضبط الاحتفاظ (7 أيام حدًا أدنى، 30 للحرجة)، وسجّل `RPO/RTO` المستهدفين هنا.
- **المستهدف المعتمد (بقرار المالك — جلسة `BQ-018/036`)**: `RPO` 24 ساعة و`RTO` 4 ساعات.
- **القاعدة الذهبية: نسخة احتياطية قبل كل نشر** — فأي تغيير في المخطط لا يُتراجَع إلا بالاستعادة من نسخة.
  **ملاحظة دقيقة عن الترحيل التلقائي:** `Database:AutoMigrate` افتراضيه `true` في التطوير فقط؛ وفي الإنتاج
  (`AutoMigrate=false`) **لا تُطبَّق الهجرات عند الإقلاع**، بل يُرفض الإقلاع إن وُجدت هجرة معلّقة — فالتطبيق
  خطوة يدوية **قبل** تشغيل الحزمة الجديدة (انظر «تطبيق هجرات قاعدة البيانات عند النشر» أعلاه).
- اختبر الاستعادة دوريًا (على قاعدة مرحلية، لا على الإنتاج): `pg_restore` أو زر الاستعادة في اللوحة — نسخة لم تُجرَّب استعادتها ليست نسخة.
- لا تُخزَّن النسخ في المستودع أبدًا، وحصر الوصول إليها بمشغّل النشر فقط (تحوي بيانات قانونية حقيقية).
- قاعدة التطوير المحلية (`docgen.db`) لا تحتاج نسخًا — بيانات تجريبية قابلة لإعادة البذر.

> **قبل النقل إلى المخدّم الحكومي (البيئة النهائية):** راجع الملحق
> `docs/audit/production-readiness/GOV-SERVER-LAUNCH-DELTA-20261103.md` — يغطي ما لا تُجرّبه استضافة
> `Render` المؤقتة: الوكيل العكسي و`Security:KnownProxies`، و`HSTS`/`AllowedHosts` على النطاق النهائي،
> وحدود حجم الطلب الطرفية، وفرض `CSP` مع بروفة إلزامية وتراجع، والنشر اليدوي وحزمة الإصدار ومسار الرجوع.

### قيد النسخة الواحدة لمشغّل صندوق الصادر (إلزامي)

- `HeadAlertOutboxDispatcher` (خدمة خلفية واحدة) يعمل على **نسخة تطبيق واحدة**. القفل الحالي محلي داخل
  العملية (`SemaphoreSlim`) مع lease في الجدول؛ فلا تشغيل لنسختين متزامنتين على القاعدة نفسها.
- **التوسّع الأفقي** (أكثر من نسخة تطبيق) يتطلب `PostgreSQL` + **قفلًا استشاريًا** (`pg_advisory_lock`)
  أو تشغيل المشغّل كعملية مستقلة واحدة؛ ولا يُستخدم `SQLite` مع أكثر من نسخة إطلاقًا (قفل الملف + `WAL`).
- المرجع: خطة التنبيهات §6.4 و§16.1؛ وسجل قرار التشغيل في `docs/audit/10-head-alerts-pr2b-deploy-20261025.md`.

### هجرات بانتظار التطبيق (قائمة تراكمية — تُشطب بعد التطبيق)

> ضع هنا أي هجرة جديدة لم تُطبَّق على قواعد البيانات بعد، واشطب السطر بعد `dotnet ef database update` الناجح في النشر.
>
> **حالة 2026‑10‑26:** اكتمل سياق `PostgreSQL` — الهجرات الأربع (`Expand`/`Outbox_Create`/`Backfill`/
> `Contract`) مطبَّقة على `Supabase` (**63** هجرة)؛ سجلها في `docs/audit/10-head-alerts-pr2b-deploy-20261025.md`
> و`docs/audit/13-head-alerts-pr3-live-apply-20261026.md`. كانت تبقى **4 هجرات** في `SQLite` آنذاك:
> `Expand`/`Outbox_Create`/`Backfill` ثم `Contract` — وصارت **6** بعد هجرتي `ConflictRetries`
> و`RemoveDocumentViewCount` أدناه. علامة `[ ]` على زوج الهجرتين تعني أن
> **أحد السياقين** على الأقل لم يُطبَّق بعد؛ والحالة التفصيلية في أول سطر تحت كل عنوان.
>
> **جديد 2026‑11‑03:** هجرتان (`SQLite` + `PostgreSQL`) لعمود `ConflictRetries` في
> `HeadAlertOutboxMessages`. **`PostgreSQL` ✅ مطبَّقة حيًا** (2026‑11‑03 — **64** هجرة، صفر معلّقة،
> `docs/audit/14-head-alerts-conflict-retries-live-apply-20261103.md`)، و**`SQLite` ⏳ معلّقة** (5).
> مستقلتان **دلاليًا** عن هجرات التنبيهات الأربع، لكن `database update` يطبّق كل ما قبل الهدف ضمنًا:
> على `SQLite` (رأسه `Expand`) ستسحب `Outbox_Create`/`Backfill`/`Contract` ⇒ **مرهونة بـ`G2`**.
>
> **جديد 2026‑11‑05:** هجرتان (`SQLite` + `PostgreSQL`) لإزالة عدّاد المشاهدات نهائيًا
> (`RemoveDocumentViewCount` / `RemoveDocumentViewCount_Pg`) — **معلّقتان** حتى التطبيق:
> `PostgreSQL` (65) و`SQLite` (6 مع الخمس المعلّقة). الترتيب: بعد هجرة `ConflictRetries`
> في كل سياق، ولا تُطبَّق `SQLite` وحدها قبل استكمال سلسلة `G2` (نفس منطق السطر أدناه).

> **تحديث 2026‑11‑07:** طُبِّقت هجرتا الملاحق على `PostgreSQL` حيًا (عبر `Database__AutoMigrate=true` مؤقتًا ثم أُعيد إلى `false`)؛ `SQLite` ما زالت معلّقة.

- [ ] **2026-11-08 — `BaseNumberRegistry` / `BaseNumberRegistry_Pg`** (سجل أرقام الأساس v2 — جدول واحد + عمودا الإلغاء + فهرس فريد جزئي):
  - **الحالة:** `PostgreSQL` و`SQLite` — **بانتظار التطبيق** (لم تُطبَّق حيًا في هذه الجلسة بسبب تعذر الوصول المباشر إلى `Supabase` من بيئة التنفيذ).
  - `DocGeneratorDbContext` (SQLite): `Persistence\Migrations\20261108202145_BaseNumberRegistry.cs`.
  - `DocGeneratorPostgresDbContext` (PostgreSQL): `Persistence\MigrationsPostgres\20261108202211_BaseNumberRegistry_Pg.cs`.
  - **التعبئة داخل الهجرة:** تُوقف الهجرة صراحةً عند: صف بلا دائرة، أو صف أقدم من وقعة تبديل دائرة من التعديل (R3)، أو سنة/تكرار غير صالح — لا نسبة صامتة.
  - **التطبيق:** `dotnet ef database update --context DocGeneratorDbContext` ثم `dotnet ef database update --context DocGeneratorPostgresDbContext` قبل نشر النسخة؛ وإلا رفض الإقلاع (`Database:AutoMigrate=false`).
  - **تحقق مسبق (Supabase):** نفّذ تدقيق §8.1 على النسخة/قاعدة الإنتاج قبل التطبيق لأن الهجرة ترفض أي تكرار/صف بلا دائرة.

- [ ] **2026-11-07 — `DocumentAnnexSequence_Unique` / `DocumentAnnexSequence_Unique_Pg`** (فرادة ترتيب الملحق داخل الملف — `DocumentAnnexes`):
  - **الحالة:** `PostgreSQL` ✅ **مطبَّقة حيًا** (69 هجرة) · `SQLite` ⏳ **معلّقة** (10 مع التسع السابقة).
  - `DocGeneratorDbContext` (SQLite): `Persistence\Migrations\20261107090514_DocumentAnnexSequence_Unique.cs` — يسقط الفهرس `IX_DocumentAnnexes_DocumentId_Sequence` غير الفريد ويعيد إنشاءه `UNIQUE` على `(DocumentId, Sequence)`.
  - `DocGeneratorPostgresDbContext` (PostgreSQL): `Persistence\MigrationsPostgres\20261107090617_DocumentAnnexSequence_Unique_Pg.cs` — نفسه.
  - **لا فقدان بيانات:** الفهرس الجديد يمنع تكرار `Sequence` داخل الملف نفسه فقط؛ ويسمح بتكراره بين ملفات مختلفة. لا علاقة له بتكرار اسم الممثل الشرعي (المخزَّن في `Heirs`/`ExecutedHeirs`) عبر الورثة أو الملفات.
  - **فحص قبل التطبيق (يجب أن يعيد صفرًا):**
    ```sql
    SELECT "DocumentId", "Sequence", COUNT(*)
    FROM "DocumentAnnexes"
    GROUP BY "DocumentId", "Sequence"
    HAVING COUNT(*) > 1;
    ```
  - **إجراء النشر:** `dotnet ef database update --context DocGeneratorDbContext` و`dotnet ef database update --context DocGeneratorPostgresDbContext` **قبل** نشر النسخة، وإلا رفض الإقلاع (`Database:AutoMigrate=false`) أو ظهر عدم تطابق مخطط.
  - على `SQLite`: لا تُطبَّق وحدها؛ `database update` يسحب الهجرات المعلّقة السابقة ⇒ التزم بسلسلة §9 (`G2`).

- [ ] **2026-11-07 — `AddDocumentAnnexes` / `AddDocumentAnnexes_Pg`** (تعدد ملاحق السند التنفيذي — جدول `DocumentAnnexes`):
  - **الحالة:** `PostgreSQL` ✅ **مطبَّقة حيًا** (68 هجرة) · `SQLite` ⏳ **معلّقة** (9 مع الثماني السابقة).
  - `DocGeneratorDbContext` (SQLite): `Persistence\Migrations\20261107065601_AddDocumentAnnexes.cs` — ينشئ `DocumentAnnexes` (`Id`, `DocumentId`, `Sequence`, `AnnexType/Number/Date` بأطوال 100/100/50) + `FK` إلى `Documents` (`Cascade`) + فهرس `(DocumentId, Sequence)` + **Backfill** من الأعمدة المفردة القديمة (الصف الأول لكل ملف له ملحق).
  - `DocGeneratorPostgresDbContext` (PostgreSQL): `Persistence\MigrationsPostgres\20261107065740_AddDocumentAnnexes_Pg.cs` — نفسه (`integer` + `character varying(100/100/50)`؛ التاريخ نص حر).
  - **لا حذف للأعمدة القديمة:** `Documents.AnnexType/AnnexNumber/AnnexDate` تبقى **قراءة فقط**؛ الجدول الفرعي هو المصدر الجديد. لا فقدان بيانات (Backfill فقط).
  - **إجراء النشر:** `dotnet ef database update --context DocGeneratorDbContext` و`dotnet ef database update --context DocGeneratorPostgresDbContext` **قبل** نشر النسخة، وإلا رفض الإقلاع (`Database:AutoMigrate=false`) أو ظهر عدم تطابق مخطط (`no such table: DocumentAnnexes`).
  - على `SQLite`: لا تُطبَّق وحدها؛ `database update` يسحب الهجرات المعلّقة السابقة ⇒ التزم بسلسلة §9 (`G2`).

- [ ] **2026-11-06 — `DelegationLocalAdditionSummary` / `DelegationLocalAdditionSummary_Pg`** (سجل الإضافات المحلية على الملف المناب — `DocumentDelegations`):
  - **الحالة:** `PostgreSQL` ⏳ **معلّقة** (67 هجرة) · `SQLite` ⏳ **معلّقة** (8 مع السبع السابقة).
  - `DocGeneratorDbContext` (SQLite): `Persistence\Migrations\20261106080801_DelegationLocalAdditionSummary.cs` — يضيف ثلاثة أعمدة إلى `DocumentDelegations`: `LocalAdditionCount` (`INTEGER NOT NULL DEFAULT 0`)، `LocalAdditionLastMessage` (`TEXT(1000)`)، `LocalAdditionLastAt` (`datetime2`, `NULL`).
  - `DocGeneratorPostgresDbContext` (PostgreSQL): `Persistence\MigrationsPostgres\20261106081025_DelegationLocalAdditionSummary_Pg.cs` — نفسه (`integer NOT NULL DEFAULT 0` / `character varying(1000)` / `timestamp with time zone`).
  - **لا Backfill:** الصفوف القائمة تبدأ `LocalAdditionCount = 0` (قرار المالك: بيانات تجريبية)؛ بطاقة «تشعبات الملف» تبدأ من أول إضافة محلية بعد النشر. لا تُفقد بيانات ملفات: الإضافات نفسها في `Heirs`/`ExecutedHeirs` كما هي.
  - **أثر غير مخطط يفسّر صفوف تدقيق (يُقرأ بعد النشر):** قصّ التوقيع في `DocumentChangeTracker` صار يُلحق بصمة `SHA-256` (16 حرفًا سداسيًا)، فأي توقيع مقصوص **مخزَّن قبل النشر** (مستندات بتوقيع > 2000، مثل ~49 كفيلًا) يختلف عن التوقيع المحسوب بعد النشر ⇒ أول تعديل على ذلك المستند يُنتج صفّ تدقيق لـ`__Col_*` بلا تغيير حقيقي يراه المستخدم. لا إجراء مطلوب: الصفوف اللاحقة صحيحة، وهذه ملاحظة تفسيرية لسجل التدقيق فقط.
  - **إجراء النشر:** `dotnet ef database update --context DocGeneratorDbContext` و`dotnet ef database update --context DocGeneratorPostgresDbContext` **قبل** نشر النسخة، وإلا رفض الإقلاع (`Database:AutoMigrate=false`) أو ظهر عدم تطابق مخطط (`no such column: d.LocalAdditionCount`).
  - على `SQLite`: لا تُطبَّق وحدها؛ `database update` يسحب الهجرات المعلّقة السابقة ⇒ التزم بسلسلة §9 (`G2`).

- [ ] **2026-11-05 — `AddHeirLegalRepresentative` / `AddHeirLegalRepresentative_Pg`** (الممثل الشرعي لكل وريث — `Heirs` و`ExecutedHeirs`):
  - **الحالة:** `PostgreSQL` ⏳ **معلّقة** (66 هجرة) · `SQLite` ⏳ **معلّقة** (7 مع الست السابقة).
  - `DocGeneratorDbContext` (SQLite): `Persistence\Migrations\20261105201644_AddHeirLegalRepresentative.cs` — يضيف ستة أعمدة `nullable` إلى `Heirs` و`ExecutedHeirs`: `RepresentativeName/Father/Family` (100)، `RepresentativeCapacity` (30)، `RepresentativeAddressType` (50)، `RepresentativeAddress` (300).
  - `DocGeneratorPostgresDbContext` (PostgreSQL): `Persistence\MigrationsPostgres\20261105201734_AddHeirLegalRepresentative_Pg.cs` — نفسه (`character varying`).
  - **لا بيانات تُفقد:** أعمدة جديدة `NULL` للصفوف القائمة؛ لا Backfill ولا فهارس.
  - **إجراء النشر:** `dotnet ef database update --context DocGeneratorDbContext` و`dotnet ef database update --context DocGeneratorPostgresDbContext` **قبل** نشر النسخة، وإلا رفض الإقلاع (`Database:AutoMigrate=false`) أو ظهر عدم تطابق مخطط (`no such column: h.RepresentativeName`).
  - على `SQLite`: لا تُطبَّق وحدها؛ `database update` يسحب الهجرات المعلّقة السابقة ⇒ التزم بسلسلة §9 (`G2`).

- [ ] **2026-11-05 — `RemoveDocumentViewCount` / `RemoveDocumentViewCount_Pg`** (إزالة عدّاد المشاهدات نهائيًا — قرار المالك):
  - **الحالة:** `PostgreSQL` ⏳ **معلّقة** (65 هجرة) · `SQLite` ⏳ **معلّقة** (6 مع الخمس السابقة).
  - `DocGeneratorDbContext` (SQLite): `Persistence\Migrations\20261105104202_RemoveDocumentViewCount.cs` — يسقط عمود `ViewCount` من `Documents`.
  - `DocGeneratorPostgresDbContext` (PostgreSQL): `Persistence\MigrationsPostgres\20261105104246_RemoveDocumentViewCount_Pg.cs` — نفسه.
  - **حذف بيانات:** قيم `ViewCount` التاريخية تُفقد نهائيًا (العدّاد كان معطّلًا في الواجهة؛ لا مستهلك بعد الإزالة). `Down` يعيد العمود بقيمة `0`.
  - **إجراء النشر:** `dotnet ef database update --context DocGeneratorDbContext` و`dotnet ef database update --context DocGeneratorPostgresDbContext` **قبل** نشر النسخة، وإلا رفض الإقلاع (`Database:AutoMigrate=false`) أو ظهر عدم تطابق مخطط.
  - على `SQLite`: لا تُطبَّق وحدها؛ `database update` يسحب الهجرات المعلّقة السابقة ⇒ التزم بسلسلة §9 (`G2`).

- [ ] **2026-11-03 — `HeadAlertOutbox_ConflictRetries` / `HeadAlertOutbox_ConflictRetries_Pg`** (حالة سياسة «إعادة واحدة» لتعارض `DedupeKey`):
  - **الحالة:** `PostgreSQL` ✅ مطبَّقة حيًا (`20261103172448`) — 64 هجرة، صفر معلّقة · `SQLite` ⏳ **معلّقة** (5 مع الأربع السابقة).
  - `DocGeneratorDbContext` (SQLite): `Persistence\Migrations\20261103172359_HeadAlertOutbox_ConflictRetries.cs` — يضيف `ConflictRetries` (`INTEGER NOT NULL DEFAULT 0`) + قيد `CK_HeadAlertOutbox_ConflictRetries`، **ويعبّئ** `ConflictRetries = 1` للصفوف التي `LastError = 'conflict_retry'` (كيلا تُمنح إعادة ثانية بعد النشر).
  - `DocGeneratorPostgresDbContext` (PostgreSQL): `Persistence\MigrationsPostgres\20261103172448_HeadAlertOutbox_ConflictRetries_Pg.cs` — نفسه (`integer NOT NULL DEFAULT 0` + القيد + التعبئة).
  - **إجراء `SQLite`:** لا تُطبَّق وحدها. إما استكمال السلسلة (`Contract` بعد `G2`) ثم تطبيقها، أو — على قاعدة عند `Contract` فعلًا — سكربت فرقي:
    `dotnet ef migrations script 20261026151929_HeadAlertTaxonomy_Contract 20261103172359_HeadAlertOutbox_ConflictRetries --context DocGeneratorDbContext --project … --startup-project … -o conflict_retries.sql`
  - **⚠️ إعادة بناء جدول (SQLite):** `AddCheckConstraint` يعيد بناء `HeadAlertOutboxMessages` (نسخ ← إسقاط ← إعادة تسمية) ⇒ **أوقف المشغّل/الكتابة قبل التطبيق**، وإلا فُقدت صفوف كُتبت بين النسخ والإسقاط.
  - **حدّ التعبئة:** تُثبَّت `= 1` فقط للصفوف التي لا تزال علامتها في `LastError`؛ صفٌّ طُمس بخطأ عابر **قبل** النشر يبقى `0` (يُمنح إعادة واحدة إضافية — بلا ضرر بيانات).
  - بدون التطبيق يفشل المشغّل عند الالتقاط/التحديث (`no such column: m.ConflictRetries` في `SQLite`، أو `column "ConflictRetries" does not exist` في `PostgreSQL`)، و`Database:AutoMigrate=false` في الإنتاج يمنع الإقلاع أصلًا.

- [ ] **2026-08-19 — `AddHeadAlertDelegationLink`** (ربط تنبيهات رؤساء الأقسام بالمندوبية):
  - `DocGeneratorDbContext` (SQLite): `Persistence\Migrations\20260819124219_AddHeadAlertDelegationLink.cs` — يضيف عمود `DelegationId` إلى `HeadAlerts` (FK SetNull + فهرس).
  - `DocGeneratorPostgresDbContext` (PostgreSQL): `Persistence\MigrationsPostgres\20260819124309_AddHeadAlertDelegationLink.cs` — نفسه.
  - بدون التطبيق سيفشل ربط تنبيه المراجعة بالمندوبية برسالة `no such column: h.DelegationId`.
- [ ] **2026-08-20 — `AddForcibleTransferAndSnapshotAdjusted`** (ميزة «تاريخ قرار الإحالة القطعية»/«اعتبار الملف منفذًا كاملًا بهذا البيع»):
  - `DocGeneratorDbContext` (SQLite): `Persistence\Migrations\20260820180256_AddForcibleTransferAndSnapshotAdjusted.cs` — يضيف `ForcibleTransferDate` و`ForcibleTransferNoticeNumber` إلى `Documents` و`SnapshotAdjusted` إلى `DelegationAssets`.
  - `DocGeneratorPostgresDbContext` (PostgreSQL): `Persistence\MigrationsPostgres\20260820180319_AddForcibleTransferAndSnapshotAdjusted.cs` — نفسه.
- [ ] **2026-08-20 — `RemoveSendBookColumns`** (حذف أعمدة كتاب الإرسال من المندوبية):
  - `DocGeneratorDbContext` (SQLite): `Persistence\Migrations\20260820131251_RemoveSendBookColumns.cs` — يحذف `SendBookDate` و`SendBookNumber` من `DocumentDelegations`.
  - `DocGeneratorPostgresDbContext` (PostgreSQL): `Persistence\MigrationsPostgres\20260820131305_RemoveSendBookColumns.cs` — نفسه.
- [ ] **2026-08-20 — `AlignPostgresTimestampTypes`** (تنسيق أنواع التوقيت في PostgreSQL فقط):
  - `DocGeneratorPostgresDbContext` (PostgreSQL): `Persistence\MigrationsPostgres\20260820211413_AlignPostgresTimestampTypes.cs` — يحوّل `ForcibleTransferDate` و`ExecutedExecutionDate` في `Documents` إلى `timestamp with time zone`.
  - ⚠️ هذا للـ PostgreSQL فقط — لا يوجد SQLite مقابل.
- [ ] **2026-08-22 — `AddAssetSeizureDate`** (تاريخ صك seizure على الأصول):
  - `DocGeneratorDbContext` (SQLite): `Persistence\Migrations\20260822215648_AddAssetSeizureDate.cs` — يضيف عمود `SeizureDate` إلى `Assets`.
  - `DocGeneratorPostgresDbContext` (PostgreSQL): `Persistence\MigrationsPostgres\20260822215846_AddAssetSeizureDatePg.cs` — نفسه.
  - بدون التطبيق سيفشل عرض/حفظ تاريخ صك seizures برسالة `no such column: a.SeizureDate`.
- [ ] **2026-08-23 — `AddDocumentAppeals` / `AddDocumentAppealsPg`** (ميزة «الاستئنافات» — المرحلة 1):
  - `DocGeneratorDbContext` (SQLite): `Persistence\Migrations\20260823110935_AddDocumentAppeals.cs` — ينشئ جداول `DocumentAppeals` و`AppealActions` و`AppealBaseNumbers` ويضيف عمود `AppealId` إلى `HeadAlerts`.
  - `DocGeneratorPostgresDbContext` (PostgreSQL): `Persistence\MigrationsPostgres\20260823110958_AddDocumentAppealsPg.cs` — نفسه.
  - بدون التطبيق سيفشل الإتمام/الاعتبار فعليًا برسالة `no such column: …` رغم نجاح الاختبارات محليًا.
- [ ] **2026-08-24 — `AddReviewLetters`** (ميزة خطابات المراجعة — المرحلة 1):
  - `DocGeneratorDbContext` (SQLite): `Persistence\Migrations\20260824060821_AddReviewLetters.cs` — ينشئ جداول `ReviewLetters` و`ReviewLetterMessages` مع FKs وفهارس.
  - `DocGeneratorPostgresDbContext` (PostgreSQL): `Persistence\MigrationsPostgres\20260824064804_AddReviewLettersPg.cs` — نفسه.
  - بدون التطبيق سيفشل فتح شاشة خطابات المراجعة برسالة `no such table: ReviewLetters`.
- [ ] **2026-08-24 — `AddReviewLetterNotifications`** (إشعارات خطابات المراجعة):
  - `DocGeneratorDbContext` (SQLite): `Persistence\Migrations\20260824073441_AddReviewLetterNotifications.cs` — يضيف `IsSeenByLawyer` إلى `ReviewLetterMessages` و`ReviewLetterId` إلى `HeadAlerts` (FK SetNull + فهرس).
  - `DocGeneratorPostgresDbContext` (PostgreSQL): `Persistence\MigrationsPostgres\20260824073458_AddReviewLetterNotificationsPg.cs` — نفسه.
  - بدون التطبيق سيفشل إرسال إشعار خطاب مراجعة للمحامي برسالة `no such column: h.ReviewLetterId`.
- [ ] **2026-08-24 — `AddDocumentFieldChanges`** (تغييرات حقول المستند في سجل التدقيق):
  - `DocGeneratorDbContext` (SQLite): `Persistence\Migrations\20260824104831_AddDocumentFieldChanges.cs` — ينشئ جدول `DocumentFieldChanges` مع FK إلى `AuditLogs` + فهارس مركبة.
  - `DocGeneratorPostgresDbContext` (PostgreSQL): `Persistence\MigrationsPostgres\20260824104844_AddDocumentFieldChangesPg.cs` — نفسه.
  - بدون التطبيق سيفشل تسجيل تغييرات الحقول في سجل التدقيق برسالة `no such table: DocumentFieldChanges`.
- [ ] **2026-08-24 — `AddEntityRegistry` / `AddEntityRegistryPg`** (ميزة «بوابة الجهات العامة» — المرحلة 1):
  - `DocGeneratorDbContext` (SQLite): `Persistence\Migrations\20260824173935_AddEntityRegistry.cs` — ينشئ جداول `PublicEntityGroups` و`PublicEntities` و`PublicEntityAliases` و`PublicEntityProposals` ويضيف عمود `Governorate` إلى `Branches`.
  - `DocGeneratorPostgresDbContext` (PostgreSQL): `Persistence\MigrationsPostgres\20260824174004_AddEntityRegistryPg.cs` — نفسه.
  - بدون التطبيق سيفشل فتح شاشة «سجل الجهات العامة» وأي عملية فيها برسالة `no such table: …`.
- [ ] **2026-08-24 — `AddEntityRegistryLinks` / `AddEntityRegistryLinksPg`** (بوابة الجهات العامة — المرحلة 2):
  - `DocGeneratorDbContext` (SQLite): `Persistence\Migrations\20260824190803_AddEntityRegistryLinks.cs` — يضيف `RegistryId` إلى `ApplicantPublicEntities` و`ExecutedPublicEntities` (FK SetNull + فهارس) و`ApplicantRegistryId` إلى `Documents` (فهرس).
  - `DocGeneratorPostgresDbContext` (PostgreSQL): `Persistence\MigrationsPostgres\20260824190820_AddEntityRegistryLinksPg.cs` — نفسه.
  - بدون التطبيق سيفشل حفظ أي ملف برسالة `no such column: r.RegistryId`.
- [ ] **2026-08-24 — `AddEntityPortal` / `AddEntityPortalPg`** (بوابة الجهات العامة — المرحلة 3):
  - `DocGeneratorDbContext` (SQLite): `Persistence\Migrations\20260824202637_AddEntityPortal.cs` — يضيف `PortalGroupId` و`PortalEntryId` إلى `Users` (FK SetNull + فهارس) لنطاق حسابات المندوبين.
  - `DocGeneratorPostgresDbContext` (PostgreSQL): `Persistence\MigrationsPostgres\20260824202706_AddEntityPortalPg.cs` — نفسه.
  - بدون التطبيق سيفشل إنشاء/قراءة أي حساب مندوب برسالة `no such column: u.PortalGroupId`.
- [ ] **2026-08-26 — `AddEntityRegistryReview` / `AddEntityRegistryReviewPg`** (بوابة الجهات — نموذج الحوكمة الجديد):
  - `DocGeneratorDbContext` (SQLite): `Persistence\Migrations\20260826010109_AddEntityRegistryReview.cs` — يضيف `NeedsReview/ReviewedAtUtc/ReviewedById` إلى `PublicEntities` (فهارس + FK SetNull) و**يُسقط جدول `PublicEntityProposals`**.
  - `DocGeneratorPostgresDbContext` (PostgreSQL): `Persistence\MigrationsPostgres\20260826010122_AddEntityRegistryReviewPg.cs` — نفسه.
  - ⚠️ يسقط بيانات الاقتراحات القديمة نهائيًا — تأكد قبل التطبيق، وبعدده فشل شاشة المراجعة (`no such column`).
- [ ] **2026-08-26 — `AddCoverageLabel` / `AddCoverageLabelPg`** (تسمية التغطية + سجل تغييرات الهوية — المرحلة 1):
  - `DocGeneratorDbContext` (SQLite): `Persistence\Migrations\20260826152120_AddCoverageLabel.cs` — يضيف عمود `CoverageLabel` إلى `PublicEntities` (max 150) وينشئ جدول `PublicEntityChangeEvents` مع 3 FKs و5 فهارس.
  - `DocGeneratorPostgresDbContext` (PostgreSQL): `Persistence\MigrationsPostgres\20260826152156_AddCoverageLabelPg.cs` — نفسه بأنواع Postgres (`character varying(150)` + `timestamp with time zone`).
  - بدون التطبيق سيفشل إنشاء قيد جديد بتسمية تغطية وأي عملية نقل/طيّ برسالة `no such column: e.CoverageLabel` أو `no such table: PublicEntityChangeEvents`.
- [ ] **2026-08-26 — `AddEntityEvents` / `AddEntityEventsPg`** (Snapshot فارغ — الجدول أُنشئ أعلاه):
  - `DocGeneratorDbContext` (SQLite): `Persistence\Migrations\20260826152236_AddEntityEvents.cs` — فارغ (لا إجراء).
  - `DocGeneratorPostgresDbContext` (PostgreSQL): `Persistence\MigrationsPostgres\20260826152302_AddEntityEventsPg.cs` — نفسه.
- [ ] **2026-08-26 — `AddIsActiveIndex` / `AddIsActiveIndexPg`** (فهرس `IsActive` على `PublicEntities`):
  - `DocGeneratorDbContext` (SQLite): `Persistence\Migrations\20260826181557_AddIsActiveIndex.cs` — ينشئ `IX_PublicEntities_IsActive`.
  - `DocGeneratorPostgresDbContext` (PostgreSQL): `Persistence\MigrationsPostgres\20260826181623_AddIsActiveIndexPg.cs` — نفسه.
- [ ] **2026-08-27 — `AddDelegationSaleCoversFullDebt`** (ميزة «تغطية بدل المبيع لكامل المديونية» عند إتمام الإنابة):
  - `DocGeneratorDbContext` (SQLite): `Persistence\Migrations\20260827131401_AddDelegationSaleCoversFullDebt.cs` — يضيف عمود `SaleCoversFullDebt` (nullable `bool`) إلى `DocumentDelegations`.
  - `DocGeneratorPostgresDbContext` (PostgreSQL): `Persistence\MigrationsPostgres\20260827131448_AddDelegationSaleCoversFullDebt.cs` — نفسه (`boolean` nullable).
  - بدون التطبيق سيفشل إتمام الإنابة برسالة `no such column: d.SaleCoversFullDebt` رغم نجاح الاختبارات محليًا.
- [ ] **2026-08-29 — `AddParentEntityFlag` (SQLite + Postgres)** (مفهوم «الجهة الأم» بدل «الفرع الرئيسي»):
  - `DocGeneratorDbContext` (SQLite): `Persistence\Migrations\20260829094454_AddParentEntityFlag.cs` — يضيف عمود `IsParentEntity` (`bool`، افتراضي `false`) وفهرس `IX_PublicEntities_IsParentEntity` إلى `PublicEntities`.
  - `DocGeneratorPostgresDbContext` (PostgreSQL): `Persistence\MigrationsPostgres\20260829094517_AddParentEntityFlag.cs` — نفسه (`boolean`).
  - بدون التطبيق سيفشل البحث/اختيار الجهة بـ`no such column: p.IsParentEntity` رغم نجاح الاختبارات محليًا.
- [ ] **2026-08-29 — `AddHeadAlertPublicEntityLink` / `AddHeadAlertPublicEntityLinkPg`** (ربط تنبيهات اقتراح التعديل بالجهة + إصلاح فهرس `ReviewedById` الفريد الخاطئ):
  - `DocGeneratorDbContext` (SQLite): `Persistence\Migrations\20260829000221_AddHeadAlertPublicEntityLink.cs` — يضيف عمود `PublicEntityId` (nullable) وفهرسًا وFK (SetNull) إلى `HeadAlerts`، و**يحذف الفهرس الفريد `IX_PublicEntities_ReviewedById` ويُعيد إنشاءه غير فريد** (دمج لإصلاح `RemoveReviewedByIdUniqueIndex` التي أُزيلت). **مُطبَّق محليًا على `docgen.db`.**
  - `DocGeneratorPostgresDbContext` (PostgreSQL): `Persistence\MigrationsPostgres\20260829000252_AddHeadAlertPublicEntityLinkPg.cs` — نفسه (`integer` nullable).
  - بدون التطبيق سيفشل استعلام تنبيهات الاقتراح/المراجعة بـ`no such column: a.PublicEntityId`، ويبقى خطأ الحفظ `UNIQUE constraint failed: PublicEntities.ReviewedById` عند اعتماد رئيس القسم قيدًا ثانيًا رغم نجاح الاختبارات محليًا.
- [ ] **2026-09-02 — `AddExecutionApplicantRegistryId`** (ربط طالب التنفيذ الاعتباري بالجهة العامة — المرحلة ب):
  - `DocGeneratorDbContext` (SQLite): `Persistence\Migrations\20260902160533_AddExecutionApplicantRegistryId.cs` — يضيف عمود `RegistryId` (nullable) وفهرسًا وFK (SetNull) إلى `ExecutionApplicants` لربط طالب التنفيذ الاعتباري (`legal`) بقيد الجهة العامة في `PublicEntities`.
  - `DocGeneratorPostgresDbContext` (PostgreSQL): `Persistence\MigrationsPostgres\20260902160613_AddExecutionApplicantRegistryId.cs` — نفسه (`integer` nullable).
  - بدون التطبيق سيفشل فتح/حفظ ملفات «منفذ عليه» أو حلول/دمج/إعادة تسمية جهة عامة مربوطة بـ`no such column: a.RegistryId` رغم نجاح الاختبارات محليًا.
- [ ] **2026-09-14 — `AllowMultipleBaseNumbersPerYear` (SQLite + Postgres)** (المرحلة 2 من خطة هوية رقم الملف: توثيق الأرقام الكامل لنفس السنة):
  - `DocGeneratorDbContext` (SQLite): `Persistence\Migrations\20260914161054_AllowMultipleBaseNumbersPerYear.cs` — يحذف القيد الفريد `IX_DocumentBaseNumbers_DocumentId_Year` و`IX_AppealBaseNumbers_AppealId_Year` ويُعيد إنشاءهما غير فريدين (كل تدوير/تجديد يُنشئ سجلًا جديدًا؛ الأحدث `CreatedAt` هو المعتبر).
  - `DocGeneratorPostgresDbContext` (PostgreSQL): `Persistence\MigrationsPostgres\20260914161205_AllowMultipleBaseNumbersPerYearPg.cs` — نفسه.
  - بدون التطبيق سيفشل حفظ تدوير/تجديد ثانٍ لنفس السنة برسالة `UNIQUE constraint failed: DocumentBaseNumbers.DocumentId, DocumentBaseNumbers.Year` رغم نجاح الاختبارات محليًا.
- [ ] **2026-09-14 — `AddOccurrenceSource` (SQLite + Postgres)** (المرحلة 3 من خطة هوية رقم الملف: تقسية سجل الوقوعات):
  - `DocGeneratorDbContext` (SQLite): `Persistence\Migrations\20260914184446_AddOccurrenceSource.cs` — يضيف عمود `Source` (`text`, NOT NULL, default `manual`) ويوسم كل الوقوعات القائمة بـ`system` (`UPDATE DocumentOccurrences SET Source = 'system'`).
  - `DocGeneratorPostgresDbContext` (PostgreSQL): `Persistence\MigrationsPostgres\20260914184530_AddOccurrenceSourcePg.cs` — نفسه (`character varying(10)` + UPDATE).
  - بدون التطبيق سيفشل تشغيل فعلي: محاولة إنقاذ قيد/إدخال وقعة تصطدم بـ`no such column: o.Source` رغم نجاح الاختبارات محليًا.
- [ ] **2026-09-19 — `BackfillMirrorLawyer` / `BackfillMirrorLawyerPg`** (تعبئة «المحامي المختص» للملفات المنابة القائمة — إصلاح ظهور الاسم في جدول الملفات لرئيس القسم/المدير/المشرف):
  - `DocGeneratorDbContext` (SQLite): `Persistence\Migrations\20260919211650_BackfillMirrorLawyer.cs` — تعبئة `Lawyer` للصفوف ذات `SourceDelegationId IS NOT NULL` والاسم المفقود من اسم مالكها (الموكول) وإلا اسم الدخول؛ بلا تغيير مخطط.
  - `DocGeneratorPostgresDbContext` (PostgreSQL): `Persistence\MigrationsPostgres\20260919211723_BackfillMirrorLawyerPg.cs` — نفسه.
  - بدون التطبيق تبقى المنابة القائمة بلا اسم في عمود «المحامي المختص» وفلتره (الجديدة تُضبط عند الاعتماد تلقائيًا).
- [ ] **2026-09-20 — `BackfillDelegationTargetCourt` / `BackfillDelegationTargetCourtPg`** (تعبئة دائرة الملفات المنابة القائمة بالدائرة المنابة — هوية المناب المستقلة: دائرته المسجَّل فيها لا دائرة المنيب):
  - `DocGeneratorDbContext` (SQLite): `Persistence\Migrations\20260920070522_BackfillDelegationTargetCourt.cs` — تعبئة `Court` للصفوف ذات `SourceDelegationId IS NOT NULL` من `DelegatedCourt` لإنابتها (غير الفارغة)؛ بلا تغيير مخطط.
  - `DocGeneratorPostgresDbContext` (PostgreSQL): `Persistence\MigrationsPostgres\20260920070620_BackfillDelegationTargetCourtPg.cs` — نفسه.
  - بدون التطبيق تبقى المنابات القائمة ظاهرة بدائرة المنيب في الشريط والجداول والمستندات والتنبيهات (الجديدة تُضبط عند الاعتماد تلقائيًا).
- [ ] **2026-09-20 — `AddDelegationAssetReservations` (SQLite + Postgres)** (جدول حجوزات الأصول الصريح للإنابات الحاجبة — نقطة التسلسل لسباق التسطير المتزامن، خطة المعالجة `v2`):
  - `DocGeneratorDbContext` (SQLite): `Persistence\Migrations\20260920121618_AddDelegationAssetReservations.cs` — ينشئ جدول `DelegationAssetReservations` (`Id` + `DelegationId` بFK تتالٍ إلى `DocumentDelegations` + `SourceDocumentId` + `AssetId`) مع قيد فريد `IX_DelegationAssetReservations_SourceDocumentId_AssetId` وفهرس `IX_DelegationAssetReservations_DelegationId`.
  - `DocGeneratorPostgresDbContext` (PostgreSQL): `Persistence\MigrationsPostgres\20260920121644_AddDelegationAssetReservations.cs` — نفسه (`integer` + `IdentityByDefaultColumn`).
  - بدون التطبيق يفشل أي تسطير/تعديل إنابة فعليًا برسالة `no such table: DelegationAssetReservations` رغم نجاح الاختبارات محليًا.
- [ ] **2026-09-22 — `AddReferredToStartStatus` (SQLite + Postgres)** (الحالة «محال الى البداية» — كتب المطالعة بعدم وجود الأموال والإحالة إلى قسم البداية):
  - `DocGeneratorDbContext` (SQLite): `Persistence\Migrations\20260922184415_AddReferredToStartStatus.cs` — يضيف `NoFundsDemandNumber` (`TEXT` ≤100) و`NoFundsDemandDate` (`DateTime?`) و`StartReferralNumber` (`TEXT` ≤100) و`StartReferralDate` (`DateTime?`) إلى `Documents`. **مُطبَّق محليًا على `docgen.db` (MigrateAsync عند الإقلاع).**
  - `DocGeneratorPostgresDbContext` (PostgreSQL): `Persistence\MigrationsPostgres\20260922184420_AddReferredToStartStatus.cs` — نفسه (`character varying(100)` + `timestamp with time zone` nullable).
  - بدون التطبيق يفشل الدخول/العودة إلى «محال الى البداية» فعليًا برسالة `no such column: NoFundsDemandNumber` رغم نجاح الاختبارات محليًا.
- [ ] **2026-09-24 — `AddCorrespondences` / `AddCorrespondencesPg`** (ميزة المراسلات: تواصل ثنائي مندوب↔محامٍ/رئيس برقم وتاريخ تلقائيين وأهمية وتوثيق مشاهدة):
  - `DocGeneratorDbContext` (SQLite): `Persistence\Migrations\20260924180352_AddCorrespondences.cs` — ينشئ جداول `Correspondences` (رقم فريد + `BranchId?` + `Governorate` + `TargetUserId` + `Importance` + `DocumentId?`) و`CorrespondenceMessages` (FK تتالٍ) و`CorrespondenceReceipts` (قيد فريد `CorrespondenceId+UserId`) مع الفهارس. **مُطبَّق محليًا على `docgen.db`.**
  - `DocGeneratorPostgresDbContext` (PostgreSQL): `Persistence\MigrationsPostgres\20260924180425_AddCorrespondencesPg.cs` — نفسه (`timestamp with time zone` للتواريخ).
  - بدون التطبيق تفشل كل شاشات المراسلات فعليًا برسالة `no such table: Correspondences` رغم نجاح الاختبارات محليًا.
- [ ] **2026-10-01 — `RequireBranchForBranchRoles` / `RequireBranchForBranchRolesPg`** (قيد «المحامي/الرئيس بلا فرع محرّم» — إغلاق تسريب إحصاءات كل الفروع على مستوى القاعدة):
  - `DocGeneratorDbContext` (SQLite): `Persistence\Migrations\20261001063551_RequireBranchForBranchRoles.cs` — يضيف قيد `CK_Users_BranchRequiredForBranchRoles` (`"BranchId" IS NOT NULL OR "Role" NOT IN ('Lawyer','Head')`). **طُبّق تجريبيًا على قاعدة مؤقتة نظيفة بنجاح (`database update` + فحص السلسلة كاملة).**
  - `DocGeneratorPostgresDbContext` (PostgreSQL): `Persistence\MigrationsPostgres\20261001063633_RequireBranchForBranchRolesPg.cs` — نفسه (نص SQL مولّد ومُراجع: `ALTER TABLE "Users" ADD CONSTRAINT … CHECK (…)`؛ لا خادم Postgres محليًا للتجربة الحية).
  - ⚠️ القيد يتحقق من الصفوف القائمة لحظة التطبيق: أي صف `Lawyer`/`Head` بلا فرع في الإنتاج سيُسقط الهجرة — نفّذ أولًا استعلام التدقيق: `SELECT "Username", "Role" FROM "Users" WHERE "BranchId" IS NULL AND "Role" IN ('Lawyer','Head');` (يجب أن يكون فارغًا)، وإن وُجدت صفوف عالجها إداريًا (تعيين فرع أو تعطيل) قبل التطبيق.
  - بدون التطبيق تبقى الحماية التطبيقية وحدها (البوابة + الحراس + الخدمة — كلها خضراء) لكن الكتابة المباشرة/الاستعادة تبقى قادرة على زرع الصف الشاذ.
- [ ] **2026-10-02 — `RF009_NumberingUniqueIndex` / `RF009_NumberingUniqueIndexPg`** (`RF-009`: وحدانية رقم الأساس الفعّالة — قيد فريد جزئي يمنع ازدواج (الدائرة + الرقم + النوع + السنة) قضائيًا):
  - `DocGeneratorDbContext` (SQLite): `Persistence\Migrations\20261002003430_RF009_NumberingUniqueIndex.cs` — فهرس `IX_Documents_Court_FileNumber_FileType_FileYear` فريد جزئي (`NOT "IsDeleted" AND "FileNumber" IS NOT NULL`). نص SQL مولّد ومُراجَع في `docs/audit/migrations/RF009_sqlite.sql`.
  - `DocGeneratorPostgresDbContext` (PostgreSQL): `Persistence\MigrationsPostgres\20261002003604_RF009_NumberingUniqueIndexPg.cs` — نفسه. نص SQL في `docs/audit/migrations/RF009_postgres.sql`.
  - القاعدة جديدة فارغة حاليًا (لا فحص تكرار لازم) — أول إقلاع يطبقها تلقائيًا مع البقية. بعد الإقلاع: تحقق من سجل `__EFMigrationsHistory` ثم جرّب إدخال رقم مكرر — المتوقع `409` برسالة عربية.
  - ⚠️ مستقبلًا (قاعدة فيها بيانات): أي تكرار فعّال قائم سيُسقط الهجرة — نفّذ أولًا: `SELECT Court, FileNumber, FileType, FileYear, COUNT(*) FROM Documents WHERE IsDeleted = 0 AND FileNumber IS NOT NULL GROUP BY 1,2,3,4 HAVING COUNT(*) > 1;` (يجب أن يكون فارغًا).
- [ ] **2026-10-02 — `RF010_ConcurrencyVersion` / `RF010_ConcurrencyVersionPg`** (`RF-010`: عدّاد التزامن المتفائل — `409` بدل المحو الصامت عند التحرير/الحالة المتزامنين):
  - `DocGeneratorDbContext` (SQLite): `Persistence\Migrations\20261002133853_RF010_ConcurrencyVersion.cs` — عمود `Version` (`INTEGER NOT NULL DEFAULT 0`) على `Documents`. نص SQL مولّد ومُراجَع في `docs/audit/migrations/RF010_sqlite.sql`.
  - `DocGeneratorPostgresDbContext` (PostgreSQL): `Persistence\MigrationsPostgres\20261002134010_RF010_ConcurrencyVersionPg.cs` — نفسه (`bigint`). نص SQL في `docs/audit/migrations/RF010_postgres.sql`.
  - عمود جديد بقيمة افتراضية — بلا تنظيف بيانات لازم؛ الصفوف القائمة تنال `0` تلقائيًا. محليًا يُطبَّق تلقائيًا عند أول إقلاع (`MigrateAsync`). بعد الإقلاع: تحقق من سجل `__EFMigrationsHistory` ثم جرّب تحريرين متزامنين بنفس النسخة — المتوقع `409` للثاني برسالة عربية.
- [ ] **2026-10-02 — `RF013_ReminderTimestamptzPg`** (`RF-013`: طوابع التذكيرات `timestamptz` — بلا إزاحة منطقة):
  - `DocGeneratorPostgresDbContext` (PostgreSQL **فقط** — `SQLite` بلا هجرة لثبات نموذجها): `Persistence\MigrationsPostgres\20261002151805_RF013_ReminderTimestamptzPg.cs` — يحوّل `DueDate` و`RecurrenceEnd` في `PersonalReminders` إلى `timestamp with time zone`. نص SQL مولّد ومُراجَع في `docs/audit/migrations/RF013_postgres.sql`.
  - الجدول نفسه معلق التطبيق في الإنتاج ضمن دفعة `l-main-page` أعلاه — فالتحويل فارغ عمليًا؛ يُطبَّق مع الدفعة في نفس النافذة.
- [ ] **2026-10-02 — `RF019_AuditAppendOnly` / `RF019_AuditAppendOnlyPg`** (`RF-019`: سجل التدقيق إلحاق-فقط قاعديًا — رفض أي تعديل/حذف):
  - `DocGeneratorDbContext` (SQLite): `Persistence\Migrations\20261002162056_RF019_AuditAppendOnly.cs` — 4 مشغّلات `RAISE(ABORT)` على `AuditLogs` و`DocumentFieldChanges`. نص SQL مولّد ومُراجَع في `docs/audit/migrations/RF019_sqlite.sql`.
  - `DocGeneratorPostgresDbContext` (PostgreSQL): `Persistence\MigrationsPostgres\20261002162231_RF019_AuditAppendOnlyPg.cs` — دالة `fn_prevent_audit_mutation()` + 4 مشغّلات. نص SQL في `docs/audit/migrations/RF019_postgres.sql`.
  - مشغّلات بلا مساس بالصفوف — بلا تنظيف لازم. بعد الإقلاع: تحقق من سجل `__EFMigrationsHistory` ثم جرّب تعديل صف تدقيق مباشرةً — المتوقع فشل صريح. (إجراء تنقية القديم — سنتان بيد مشغّل النشر حصرًا — في `docs/audit/units/RF-019.md` § الإجراء.)
- [ ] **2026-10-02 — `RF011_IdempotencyKeys` / `RF011_IdempotencyKeysPg`** (`RF-011`: مفاتيح عدم التكرار — النقرة المزدوجة لا تُكرر الأثر):
  - `DocGeneratorDbContext` (SQLite): `Persistence\Migrations\20261002171957_RF011_IdempotencyKeys.cs` — جدول `IdempotencyKeys` (قيد فريد مركب + فهرس انتهاء). نص SQL مولّد ومُراجَع في `docs/audit/migrations/RF011_sqlite.sql`.
  - `DocGeneratorPostgresDbContext` (PostgreSQL): `Persistence\MigrationsPostgres\20261002172031_RF011_IdempotencyKeysPg.cs` — نفسه. نص SQL في `docs/audit/migrations/RF011_postgres.sql`.
  - جدول جديد فارغ — بلا تنظيف لازم. بعد الإقلاع: تحقق من سجل `__EFMigrationsHistory` ثم جرّب إنشاء ملف مرتين بنفس ترويسة `X-Idempotency-Key` — المتوقع ملف واحد والثانية `200` بنفس الجسم.
- [ ] **2026-10-04 — `PB001_Normalization` / `PB001_NormalizationPg`** (`PB-001`: التطبيع العربي الشامل — الهمزة المختلفة لا تصنع هوية):
  - `DocGeneratorDbContext` (SQLite): `Persistence\Migrations\20261004061212_PB001_Normalization.cs` — أعمدة `CanonicalNameNorm` (فريد) و`CourtNorm` (مفهرس + فريد مركب للترقيم) و`DelegatedCourtNorm`. نص SQL مولّد ومُراجَع في `docs/audit/migrations/PB001_sqlite.sql`.
  - `DocGeneratorPostgresDbContext` (PostgreSQL): `Persistence\MigrationsPostgres\20261004061229_PB001_NormalizationPg.cs` — نفسه. نص SQL في `docs/audit/migrations/PB001_postgres.sql`.
  - القائم تجريبي بقرار المالك (تعبئة نسخ خام). ⚠️ على أي قاعدة فيها بيانات حقيقية: نفّذ أولًا استعلام الكشف في `docs/audit/units/PB-001.md` (يجب فارغًا) ونظّف بالدمج — وإلا أسقط الفريدُ الهجرة. بعد الإقلاع: تحقق من `__EFMigrationsHistory` ثم جرّب هوية بمتغير إملائي — المتوقع `400`.
- [ ] **2026-10-04 — `PB002_StatusChecks` / `PB002_StatusChecksPg`** (`PB-002`: تجميد آلات الحالة — أي إملاء خاطئ مرفوض عند الكتابة):
  - `DocGeneratorDbContext` (SQLite): `Persistence\Migrations\20261004070753_PB002_StatusChecks.cs` — 6 قيود `Check` من الكتالوجات. نص SQL مولّد ومُراجَع في `docs/audit/migrations/PB002_sqlite.sql`.
  - `DocGeneratorPostgresDbContext` (PostgreSQL): `Persistence\MigrationsPostgres\20261004070818_PB002_StatusChecksPg.cs` — نفسه. نص SQL في `docs/audit/migrations/PB002_postgres.sql`.
  - ⚠️ على أي قاعدة فيها بيانات: نفّذ أولًا استعلامات الكشف في `docs/audit/units/PB-002.md` (كلها يجب فارغة) ونظّف الصفوف اليتيمة — وإلا أسقط القيدُ الهجرة. بعد الإقلاع: تحقق من `__EFMigrationsHistory`.
- [ ] **2026-10-06 — `AddSubHeadSections` (SQLite + Postgres)** (المرحلة 1 من خطة رئيس الشعبة `docs/SUBHEAD_PLAN.md`: مخطط الشعب — بلا أي تغيير سلوكي):
  - `DocGeneratorDbContext` (SQLite): `Persistence\Migrations\20261006084513_AddSubHeadSections.cs` — جدولا `Sections` (فريد `BranchId+NameNorm`) و`HeadSuccessions`؛ أعمدة `Users.SectionId/CreatedById` و`ExecutionCircuits.SectionId` و`DocumentAppeals.ForwardState/ForwardedById/ForwardedAt/ForwardReason/Version` و`DocumentDelegations.RedirectedToSectionId/RejectReason/Version` و`RecipientSectionId` على `Correspondences/ReviewLetters`؛ توسيع قيد الفرع ليشمل `'SubHead'`؛ فهرسان فريدان جزئيان (رئيس قسم واحد مفعّل لكل فرع + رئيس شعبة مفعّل واحد لكل شعبة)؛ ترحيل الصفوف القائمة (`ForwardState='Owned'` + `Version=1`). نص SQL مولّد ومُراجَع في `docs/audit/migrations/SUBHEAD_sqlite.sql` (ملاحظة: تحذير `TableRebuildPendingWarning` على `Users` مفهوم ومقبول — `UPDATE` الترحيل يمس جدولين آخرين قبل إعادة بناء `Users`، والحالة النهائية مثبتة باختبار `SubHeadMigrationTests` صعودًا وهبوطًا).
  - `DocGeneratorPostgresDbContext` (PostgreSQL): `Persistence\MigrationsPostgres\20261006084556_AddSubHeadSections.cs` — نفسه (`integer/bigint/character varying` + `timestamp with time zone` لـ`ForwardedAt/At`). نص SQL في `docs/audit/migrations/SUBHEAD_postgres.sql`.
  - ⚠️ القيدان الجديدان يتحققان من الصفوف القائمة لحظة التطبيق — نفّذ أولًا استعلامي التدقيق (يجب أن يكونا فارغين، والصياغة تعمل على المزوّدين): `SELECT "BranchId", COUNT(*) FROM "Users" WHERE "IsActive" AND "Role" = 'Head' GROUP BY "BranchId" HAVING COUNT(*) > 1;` (رئيسا قسم مفعّلان في نفس الفرع — نظّف بالتعطيل قبل التطبيق، قرار §2.26)، واستعلام `RequireBranchForBranchRoles` السابق للصفوف بلا فرع. الصفوف المخالفة تُسقط الهجرة.
  - بدون التطبيق تفشل أي كتابة/قراءة لحقول الشعب الجديدة فعليًا برسائل `no such column: u.SectionId` ونحوها رغم نجاح الاختبارات محليًا.
- [ ] **2026-10-20 — `HeadAlertTaxonomy_Expand` / `HeadAlertTaxonomy_Expand_Pg`** (المرحلة 1 من خطة إعادة هيكلة التنبيهات `docs/HEAD_ALERTS_RESTRUCTURE_PLAN.md` — «توسّع» بلا كسر للتوافق):
  > **الحالة:** `PostgreSQL` ✅ مطبَّقة على Supabase في 2026‑10‑25 (`20261020202452_HeadAlertTaxonomy_Expand_Pg`) · `SQLite` ⏳ معلّقة.
  - `DocGeneratorDbContext` (SQLite): `Persistence\Migrations\20261020202259_HeadAlertTaxonomy_Expand.cs` — يضيف إلى `HeadAlerts` الأعمدة `Kind` (64، nullable)، `DedupeKey` (200، nullable)، `ExecutionCircuitId` (nullable)، `LastContentChangedAt` (nullable)، `ArchivedAt` (nullable)، و`IsArchived` (`NOT NULL DEFAULT 0`).
  - `DocGeneratorPostgresDbContext` (PostgreSQL): `Persistence\MigrationsPostgres\20261020202452_HeadAlertTaxonomy_Expand_Pg.cs` — نفسه + توسيع `HeadAlerts.Message` من `character varying(2000)` إلى `text` (بلا فقد بيانات).
  - **إضافة بحتة**: بلا_backfill وبلا قيد `Kind` وبلا فهرس فريد وبلا تعديل صفوف — الكود القديم يعمل معها، والصفوف القائمة تبقى كما هي (`Kind = NULL` و`LastContentChangedAt = NULL` حتى مرحلة الترحيل).
  - ⚠️ في `PostgreSQL` الـ`Down` لا يُعيد `Message` إلى `character varying(2000)` (التضييق يفقد بيانات) — التراجع التطبيقي فقط.
- [ ] **2026-10-20 — `HeadAlertOutbox_Create` / `HeadAlertOutbox_Create_Pg`** (صندوق الصادر لتسليم التنبيهات النظامية — المرحلة 1 من الخطة نفسها):
  > **الحالة:** `PostgreSQL` ✅ مطبَّقة على Supabase في 2026‑10‑25 (`20261020202700_HeadAlertOutbox_Create_Pg`) · `SQLite` ⏳ معلّقة.
  - `DocGeneratorDbContext` (SQLite): `Persistence\Migrations\20261020202554_HeadAlertOutbox_Create.cs` — جدول `HeadAlertOutboxMessages` + 4 فهارس + 5 قيود `Check`.
  - `DocGeneratorPostgresDbContext` (PostgreSQL): `Persistence\MigrationsPostgres\20261020202700_HeadAlertOutbox_Create_Pg.cs` — نفسه (`bigint identity`).
  - **يجب تطبيقه قبل أي كود يسجّل رسائل Outbox** (المرحلة 2 من الخطة) — وإلا فشل الإدراج بـ `no such table: HeadAlertOutboxMessages`.
  - ⚠️ الـ`Down` **يرفض إسقاط الجدول** إن وُجدت رسائل `pending`/`processing`/`failed` غير محسومة (حارس صريح) — عالجها أو صدّرها أولًا.
- [ ] **2026-10-21 — `HeadAlertTaxonomy_Backfill` / `HeadAlertTaxonomy_Backfill_Pg`** (M2: ترحيل التصنيفات القديمة وبناء المفاتيح — المرحلة 2 من الخطة):
  > **الحالة:** `PostgreSQL` ✅ مطبَّقة على Supabase في 2026‑10‑25 (`20261021232825_HeadAlertTaxonomy_Backfill_Pg`) · `SQLite` ⏳ معلّقة.
  - `DocGeneratorDbContext` (SQLite): `Persistence\Migrations\20261021232824_HeadAlertTaxonomy_Backfill.cs` — تعبئة `legacy` فقط، تصنيف التدفقات المفتوحة، بناء `DedupeKey`، أرشفة المراجعة المتقاعدة، حل تصادم المفاتيح ودمج المستلمين، ثم FK `ExecutionCircuitId` + الفهارس + `UX_HeadAlerts_DedupeKey_Active`.
  - `DocGeneratorPostgresDbContext` (PostgreSQL): `Persistence\MigrationsPostgres\20261021232825_HeadAlertTaxonomy_Backfill_Pg.cs` — نفسه مع `BOOL_OR` و`CURRENT_TIMESTAMP` وفلتر `"IsArchived" = false`.
  - **يجب تطبيقه قبل نشر كود `PR-2`** الذي يعتمد على `DedupeKey` والفهرس الفريد؛ وتعطيل `AutoMigrate` في الإنتاج يعني أن نسخ الكود لا تطبّقه تلقائيًا.
  - ⚠️ الهجرة تعيد بناء جدول `HeadAlerts` على SQLite لإضافة الـFK (سلوك EF متوقع). خذ نسخة احتياطية واستعدها تجريبيًا قبل النافذة. **وهي أول إعادة بناء**، فتُعيد ضبط `sqlite_sequence` على أعلى `Id` موجود: سجّل `SELECT seq FROM sqlite_sequence WHERE name='HeadAlerts';` **قبل هذه الهجرة** (لا قبل `Contract` فقط)، وأعد رفعه بعد `Contract` — القياس والأثر في `docs/audit/12-head-alerts-pr3-g2-rehearsal-20261026.md` §6.
  - الـ`Down` يزيل الـFK والفهارس فقط، **ولا يعكس بيانات الـbackfill** (المفاتيح/التصنيفات/الأرشفة تبقى).
- [ ] **2026-10-26 — `HeadAlertTaxonomy_Contract` / `HeadAlertTaxonomy_Contract_Pg`** (M3: تشديد `Kind` و`LastContentChangedAt` — المرحلة 3 من خطة التنبيهات، PR‑3):
  > **الحالة:** `PostgreSQL` ✅ مطبَّقة على `Supabase` في 2026‑10‑26 (`20261026152238_HeadAlertTaxonomy_Contract_Pg` — **63** هجرة، `docs/audit/13-head-alerts-pr3-live-apply-20261026.md`) · `SQLite` ⏳ معلّقة.
  - `DocGeneratorDbContext` (SQLite): `Persistence\Migrations\20261026151929_HeadAlertTaxonomy_Contract.cs` — حارس صريح يرفض `NULL`/فراغ، ثم إعادة بناء الجدول بـ`Kind TEXT NOT NULL` و`LastContentChangedAt TEXT NOT NULL` **بلا Default دائم**، وقيد `CK_HeadAlerts_Kind_NotEmpty` (`TRIM("Kind") <> ''`)؛ يحافظ على الفهرس الفريد الجزئي `UX_HeadAlerts_DedupeKey_Active` وكل الـFK والفهارس. **⛔ أوقف الخدمة/الكتابة قبل التطبيق** (إعادة البناء تنسخ الصفوف ثم تُسقط الجدول القديم؛ أي كتابة بينهما تُفقد). ⚠️ الهجرة تُنفَّذ في **ثلاث معاملات منفصلة** (حارس+نسخ ← `DROP`/`RENAME` ← فهارس) وسجل الهجرة بعدها؛ انهيار بينها يترك حالة وسيطة — مسار الاستعادة في `docs/audit/11-head-alerts-pr3-20261026.md` §6.3.1. ⚠️ إعادة بناء SQLite تعيد ضبط `sqlite_sequence` على أعلى `Id` موجود — في `Up` و`Down` معًا، **وهي تبدأ فعليًا من `HeadAlertTaxonomy_Backfill` (M2) لا من `Contract`**: قِس `SELECT seq FROM sqlite_sequence WHERE name='HeadAlerts';` **قبل `Backfill`**، ثم أعد الرفع بعد `Contract` (وقبل `Down`/بعد أي إعادة `Up`) إن تراجع. تسجيلها قبل `Contract` فقط يسجّل القيمة المراجعة أصلًا فيضيع الفرق بصمت. القياس المثبت والتصحيح الكامل في `docs/audit/12-head-alerts-pr3-g2-rehearsal-20261026.md` §6 (والتفاصيل الأصلية في `docs/audit/11-head-alerts-pr3-20261026.md` §2.3 و§6.2–§6.3).
  - `DocGeneratorPostgresDbContext` (PostgreSQL): `Persistence\MigrationsPostgres\20261026152238_HeadAlertTaxonomy_Contract_Pg.cs` — حارس `DO $$ … RAISE EXCEPTION` ثم `SET NOT NULL` للعمودين وقيد `CK_HeadAlerts_Kind_NotEmpty`.
  - نص SQL المولّد والمُراجَع (G1): `docs/audit/migrations/HEADALERT_PR3_sqlite.sql` و`docs/audit/migrations/HEADALERT_PR3_postgres.sql`.
  - **⛔ بوابتا ما قبل التطبيق (إلزامي):** (1) **G2** — dry-run على نسخة إنتاجية مستعادة: تطبيق `Expand`/`Outbox_Create`/`Backfill`/`Contract` بالترتيب مع `PRAGMA integrity_check` و`foreign_key_check` وفحص الفهارس وأعداد الصفوف ثم إقلاع التطبيق وفحص الدخان؛ (2) تأكيد أن **كل النسخ** تعمل بكود `PR‑2` و**صفر** صفوف `NULL` في العمودين و**صفر** `Kind` فارغ/أبيض. الهجرة **ترفض** التطبيق برسالة صريحة عند أي مخالفة (لا `IFNULL`/`Default` صامت) — التفاصيل في `docs/audit/11-head-alerts-pr3-20261026.md`.
  - فحص ما قبل النافذة (يجب أن يعيد `0 0 0`):
    ```sql
    -- SQLite (COALESCE: SUM على جدول فارغ يعيد NULL لا 0)
    SELECT COALESCE(SUM("Kind" IS NULL), 0), COALESCE(SUM("LastContentChangedAt" IS NULL), 0),
           COALESCE(SUM("Kind" IS NOT NULL AND TRIM("Kind") = ''), 0) FROM "HeadAlerts";
    -- PostgreSQL
    SELECT COUNT(*) FILTER (WHERE "Kind" IS NULL),
           COUNT(*) FILTER (WHERE "LastContentChangedAt" IS NULL),
           COUNT(*) FILTER (WHERE "Kind" IS NOT NULL AND TRIM("Kind") = '')
    FROM "HeadAlerts";
    ```
  - **ترتيب النشر:** طبّق الهجرة أولًا ثم انشر كود `PR‑3`؛ الكيان يقرأ `LastContentChangedAt` كـ`DateTime` غير قابل للفراغ، وأي صف قديم بـ`NULL` يفشل عند القراءة. لا تنشر الكود قبل تطبيق `Contract` على القاعدة الحية.
  - الـ`Down` تطبيقي فقط: يعيد العمودين nullable ويسقط القيد دون إعادة أي قيمة إلى `NULL` (الخطة §16.2).
  - بدون التطبيق يبقى المخطط أوسع من النموذج: العمودان يقبلان `NULL` ولا قيد يمنع `Kind` الفارغ، فأي مسار كتابة غير محمي يستطيع إدخال نوع فارغ رغم أن كود `PR‑3` يفترض عدم nullable.
  - **تطبيق هجرات التنبيهات بالترتيب** (لا تُطبَّق `database update` على اسم متأخر فتُطبَّق ما قبله ضمنًا): `HeadAlertTaxonomy_Expand` ← `HeadAlertOutbox_Create` ← `HeadAlertTaxonomy_Backfill` ← `HeadAlertTaxonomy_Contract` (وكذلك أزواج `_Pg`، و`Contract` **أخيرًا دائمًا** وبعد G2 وكل النسخ على `PR‑2`).
  - **حالة 2026‑10‑25:** تمّ تطبيق الهجرات الثلاث الأولى كاملة على `PostgreSQL` (Supabase) — 62 هجرة، صفر معلّقة منها (`docs/audit/10-head-alerts-pr2b-deploy-20261025.md`).
  - **حالة 2026‑10‑26 (PR‑3):** `PostgreSQL` **مكتمل حيًا** — طُبِّقت `HeadAlertTaxonomy_Contract_Pg`
    (`63` هجرة، صفر معلّقة) قبل نشر الكود، ثم نُشر `0acdfe3` على `Render` واجتاز الفحص الدخاني
    (`docs/audit/13-head-alerts-pr3-live-apply-20261026.md`). **المتبقّي: `SQLite` فقط** — تطبيق
    `Expand`/`Outbox_Create`/`Backfill`/`Contract` بالترتيب بعد استيفاء G1/G2 على قاعدة `SQLite`
    حقيقية، مع قيد `sqlite_sequence` (يبدأ التراجع من `Backfill` لا من `Contract`)
    — راجع `docs/audit/12-head-alerts-pr3-g2-rehearsal-20261026.md` §6.



---

## 10) تذكير نشر سجل أرقام الأساس v2 (2026-11-09)

> **النطاق:** `DocumentBaseNumbers` وهجرتا `BaseNumberRegistry` (SQLite) و`BaseNumberRegistry_Pg` (PostgreSQL).
> **مهم:** الإنتاج على `PostgreSQL` جديدة؛ لا تعتمد على قواعد `SQLite`/`Supabase` التجريبية.

### قبل النشر
1. خُذ نسخة احتياطية من قاعدة PostgreSQL (حتى لو كانت جديدة): لقطة مزوّد أو `pg_dump` إلى ملف خارج المستودع.
2. شغّل التدقيق المسبق (قراءة فقط):
   - عبر `psql`: `psql "$DATABASE_URL" -f scripts/base-number-registry-preflight.sql`
   - أو عبر محرر SQL في مزوّد القاعدة بنسخ محتوى `scripts/base-number-registry-preflight.sql`.
   - يجب أن تكون نتائج V‑18/السنوات/التكرارات/تبديل الدائرة صفرًا قبل المتابعة.
3. أوقف الخدمة مؤقتًا (Render Suspend) إن أمكن — لا كتابة أثناء الهجرة.

### تطبيق الهجرات (إلزامي قبل تشغيل النسخة)
```bash
dotnet ef database update --context DocGeneratorDbContext
dotnet ef database update --context DocGeneratorPostgresDbContext
```
- `Database:AutoMigrate=false` في الإنتاج؛ **إن لم تُطبّق الهجرات سيرفض التطبيق الإقلاع**.
- الهجرتان تحملان نفس الاسم (`BaseNumberRegistry` / `BaseNumberRegistry_Pg`) لكن محتواهما يشمل
  تعبئة الصفوف، `VoidedAt=document-deleted` للمحذوف، صف القيد، الفهرس الفريد الجزئي، وقيد
  `CK_Documents_FileNumberRequiresCircuit` على PostgreSQL.

### بعد النشر
1. شغّل الخدمة وتحقق من `GET /healthz`.
2. راقب سجل `[schema-check]` عند أول إقلاع: يجب ألا يظهر تحذير/فشل R9.
3. تأكد أن `NumberRegistry:AllowCircuitlessLegacy` **غير موجود** في أي إعداد إنتاجي (لا يوجد مسار تكوين يفعّله).
4. افتح ملفًا تجريبيًا وتحقق من: سجل أرقام الأساس، تصحيح صف، تدوير سنة، ومنع رقم مكرر صلب.

### التراجع
- أوقف النسخة الجديدة أولًا، ثم `dotnet ef database update <PreviousMigration> --context ...` (Down) بحسب الحاجة.
- `Down` يحذف صفوف `Source=registration` التي أنشأتها التعبئة؛ لا تُشغّله والكود الجديد يكتب.
- `render.yaml` غير مُودَع/غير مُعدّل في هذه المهمة.
