# E2E / إمكانية الوصول / الجوال — `P6-04`

> **النطاق:** `Playwright` للتدفقات الحرجة، و`axe-core` للوصول، وفحص تجاوز 375px.
> **الخادم المستهدف محلي فقط** (أو `staging` براية صريحة). **الإنتاج ممنوع منعًا باتًا**
> — ليس توجيهًا أدبيًا فقط: إعداد Playwright يرفض الإقلاع (fail-closed) عند أي مضيف
> غير محلي، وعند أي قاعدة لا يحتوي اسمها على `e2e`.

## 1. المتطلبات المحلية

| المتطلب | ملاحظة |
|---|---|
| Node ≥ 22 | المشروع يثبّت على 24 في CI |
| .NET SDK 10 | لتشغيل الـAPI المحلي |
| PostgreSQL 16 محلي | قاعدة **مؤقتة** باسم يحتوي `e2e` (مثل `docgen_e2e`) |
| متصفح | `Chrome` المثبَّت محليًا (افتراضي)، أو Chromium المثبَّت بـPlaywright |

```bash
cd frontend
npm ci --legacy-peer-deps
# مرة واحدة فقط إذا أردت Chromium المثبَّت بالإصدار بدل Chrome المحلي:
npx playwright install chromium
npm run e2e:browser        # دخان تشغيل المتصفح فقط (بلا قاعدة)
```

## 2. البذر الحتمي والحسابات

`scripts/e2e/seed.sql` يُنشئ **فرعًا واحدًا وأربعة حسابات فقط** بكلمة مرور ثابتة
معلنة (`E2eTest!Pass2026`) وهاش بصيغة التطبيق (`$docgen$v1$...`، PBKDF2-SHA256).
الملح ثابت عمدًا ليكون البذر حتميًا. السكربت idempotent (إعادة تشغيله لا تكرر الصفوف)
ويفشل إن لم تكتمل الحسابات الأربعة.

| المستخدم | الدور | الفرع | الاستخدام في الاختبارات |
|---|---|---|---|
| `e2e.manager` | `Manager` | — | الدخول/اللوحة/المستندات/التنبيهات/axe |
| `e2e.head` | `Head` | `E2E-DAM` | بذر نطاق رئيس القسم (تدفقات لاحقة) |
| `e2e.lawyer` | `Lawyer` | `E2E-DAM` | بذر نطاق المحامي (تدفقات لاحقة) |
| `e2e.portal` | `EntityManager` | — | بوابة القراءة (بلا ربط: نطاق فارغ) |

> كلمة المرور **اختبارية فقط** لقاعدة تُحذف بعد التشغيل، ولا تُستخدم في أي بيئة حقيقية.

## 3. التجهيز المحلي (قاعدة مؤقتة)

```powershell
# 1) قاعدة جديدة (مرة واحدة؛ أو أعد إنشاءها عند التلوث)
$env:PGPASSWORD='<كلمة مرور postgres المحلية>'
createdb -h 127.0.0.1 -U postgres docgen_e2e

# 2) سلسلة الاتصال: احفظها في ملف خارج المستودع، ولا تلصق كلمة المرور في الوثائق/السجلات
#    الملف يحوي سلسلة Npgsql المعتادة بالمفاتيح Host/Port/Database/Username/Password
$env:E2E_DB_CONNECTION = (Get-Content "$HOME\.config\docgen\e2e-db.txt" -Raw).Trim()

# 3) الهجرات (تطبّق على قاعدة E2E فقط عبر --connection)
#    مصنع زمن التصميم يشترط POSTGRES_CONNECTION حتى مع --connection.
$env:POSTGRES_CONNECTION = $env:E2E_DB_CONNECTION
dotnet ef database update --context DocGeneratorPostgresDbContext `
  --project backend/src/DocGenerator.Infrastructure `
  --startup-project backend/src/DocGenerator.Api `
  --connection "$env:E2E_DB_CONNECTION"

# 4) البذر الحتمي
psql -h 127.0.0.1 -p 5432 -U postgres -d docgen_e2e -v ON_ERROR_STOP=1 -f scripts/e2e/seed.sql

# 5) تشغيل الاختبارات (يشغّل API + الواجهة تلقائيًا)
cd frontend
$env:E2E_MANAGED_SERVERS='1'
$env:E2E_BROWSER_CHANNEL='chrome'   # افتراضي محليًا: Chrome المثبَّت
npx playwright test
```

على bash/CI الأوامر نفسها؛ استبدل أسطر PowerShell بـ`export` وقراءة الملف بـ
`export E2E_DB_CONNECTION="$(cat ~/.config/docgen/e2e-db.txt)"`، ولا تنسَ
`export POSTGRES_CONNECTION="$E2E_DB_CONNECTION"` قبل أمر `dotnet ef`.

### ما الذي يُشغَّل تلقائيًا؟

مع `E2E_MANAGED_SERVERS=1` يبدأ Playwright:

- الـAPI: `dotnet run --project ../backend/src/DocGenerator.Api --no-build --no-launch-profile --urls http://127.0.0.1:5080`
  ببيئة `Development` + `Security__AllowDevelopmentEnvironment=true` (كوكي CSRF غير `Secure`
  فوق http المحلي)، وينتظر `/readyz`.
- الواجهة: `npm run dev -- --host 127.0.0.1 --port 5173` مع `VITE_API_TARGET` إلى الـAPI،
  وينتظر `http://127.0.0.1:5173`. (`--strictPort` يمنع Vite من الانتقال الصامت إلى منفذ
  آخر فلا ينتظر Playwright عنوانًا لا يستمع.)

**لا يُعاد استخدام خادم API قائم** (`reuseExistingServer: false`): قد يكون على قاعدة
مختلفة، وهذا يكسر قاعدة «قاعدة مؤقتة للتشغيل». إن كان المنفذ 5080 مشغولًا يفشل التشغيل
صراحةً — أوقف الخادم القديم ثم أعد المحاولة.

> `dotnet run --no-build` يتطلب بناءً مسبقًا: `dotnet build backend/DocGenerator.sln`.

## 4. المتغيّرات

| المتغيّر | الافتراضي | الأثر |
|---|---|---|
| `E2E_BASE_URL` | `http://127.0.0.1:5173` | محلي فقط؛ staging يحتاج راية |
| `E2E_API_URL` | `http://127.0.0.1:5080` | هدف وسيط `Vite` |
| `E2E_MANAGED_SERVERS` | غير مفعّل | `1` لتشغيل الخادمين تلقائيًا |
| `E2E_DB_CONNECTION` | — | إلزامية مع الإدارة؛ اسم القاعدة يحتوي `e2e` |
| `E2E_BROWSER_CHANNEL` | `chrome` محليًا، `chromium` في CI | `chromium` = متصفح Playwright المثبَّت |
| `E2E_ALLOW_STAGING` | — | `1` فقط، ومع مضيف يحتوي `staging` |

## 5. الحارس الأمني (لماذا لا يمكن لمس الإنتاج؟)

`frontend/scripts/e2e-safety-guard.mjs` يُستدعى في أعلى `playwright.config.ts`
(أي **قبل** تشغيل أي خادم أو متصفح) ويفشل عند:

- `E2E_BASE_URL`/`E2E_API_URL` غير محلي (`localhost`/`127.0.0.1`/`::1`) بلا
  `E2E_ALLOW_STAGING=1`، أو مع الراية لكن اسم المضيف لا يحتوي `staging`.
- `E2E_DB_CONNECTION` بمضيف غير محلي (بالشروط نفسها) أو اسم قاعدة **لا يحتوي `e2e`**،
  أو اسم/مضيف يوحي بالإنتاج (`prod`/`production`/`live`).
- غياب `Host`/`Database` في السلسلة: رفض بدل التخمين.

لا تُطبع السلسلة أو كلمة المرور في رسائل الرفض — يُذكر المضيف/اسم القاعدة فقط.

**تحقق سلبي (شغّله محليًا مرة للتأكد):**

```powershell
cd frontend
$env:E2E_BASE_URL='https://docgen.onrender.com'
npx playwright test --list        # يجب أن يفشل فورًا: non-local host
Remove-Item Env:E2E_BASE_URL

$env:E2E_MANAGED_SERVERS='1'
$env:E2E_DB_CONNECTION='Host=127.0.0.1;Database=docgen;Username=postgres'
npx playwright test --list        # يجب أن يفشل: اسم القاعدة لا يحتوي e2e
```

الاختبارات الذاتية:

```bash
cd frontend
node scripts/e2e-safety-guard.mjs --self-test   # 17 حالة
npx vitest run scripts/e2e-safety-guard.test.mjs
```

## 6. الاختبارات وما تغطيه

| الملف | التغطية |
|---|---|
| `e2e/auth.spec.ts` | دخول مدير صحيح → اللوحة؛ خطأ عربي صريح؛ خروج يُبطل الجلسة (المسار المحمي يعيد التوجيه) |
| `e2e/documents.spec.ts` | القائمة تُعرض؛ البحث يصل للـAPI بالاستعلام نفسه؛ نموذج الإنشاء يفتح ويرفض «منفذ عليها» بلا رقم/سنة **دون أي `POST`** |
| `e2e/alerts-portal.spec.ts` | التنبيهات (مدير) وبوابة الملفات (مندوب) تُعرض بلا أخطاء `console` |
| `e2e/mobile-overflow.spec.ts` | 375×812: لا تجاوز أفقي (`scrollWidth <= innerWidth + 1`) على 12 شاشة: الدخول/المستندات/التنبيهات/البوابة/المنتدى/الإحصاءات/إدارة الفروع/التقويم/الاقتراحات/إحصاءات الدوائر/سجل التدقيق/طلبات الإنابة |
| `e2e/a11y.spec.ts` | `axe` على 12 شاشة (نفس القائمة + عرض التنبيهات للمدير)؛ تفشل عند `critical`/`serious` |

### قائمة تبييض `axe`

**فارغة حاليًا** (لا استثناءات). أي إضافة مستقبلية يجب أن تكون:

1. في `A11Y_ALLOWLIST` داخل `e2e/a11y.spec.ts` بمعرّف القاعدة وسبب مكتوب.
2. موثّقة في هذا الملف مع تاريخ الإزالة المتوقع.

### ضجيج `console` المسموح

فقط رسائل فشل تحميل موارد شبكية لأسباب جلسة/أصل غير موجود:

- `401` على `/api/auth/me` قبل الدخول (فحص جلسة متوقع في `AuthProvider`).
- `404` على `/favicon.ico` (لا ملف أيقونة في `public/`).

أي `pageerror` أو `console.error` آخر يفشل الاختبار.

## 7. حدود التغطية الحالية (معلنة)

- **حدود الأخطاء (Error Boundary):** لا يمكن إجبار خطأ عرض في E2E بدون مسار اختباري
  في التطبيق (لا نضيف مسارًا للإنتاج). مغطّى باختبارات وحدات:
  `frontend/src/components/ErrorBoundary.test.tsx` و`Layout.errorBoundary.test.tsx`.
- **اللقطات البصرية (Visual):** **لم تُضف بعد**. عند إضافتها لاحقًا: تُولَّد وتُقارن
  في CI فقط باستخدام Chromium المثبَّت بالإصدار (`E2E_BROWSER_CHANNEL=chromium`)،
  لأن Chrome المحلي قد يرسم بفروق طفيفة. لا تُولَّد لقطات من جهاز مطوّر وتُعتمد كمرجع.
  تحديثها المقصود: `npx playwright test --update-snapshots` داخل CI (أو محليًا بـ
  `E2E_BROWSER_CHANNEL=chromium`) ثم مراجعة الفرق قبل الالتزام.
- **التدفقات الكاملة** (إنشاء/تعديل مستند فعلي، متدخل، حالة، إنابة، استئناف، إدارة)
  ما زالت خارج هذه الدفعة — الأساس (دخول/بحث/تحقق/قراءة/axe/جوال) مثبت الآن.
- **staging:** الـ`DoD` الأصلي يطلب E2E على staging؛ هنا يعمل على قاعدة محلية مؤقتة
  في CI. تشغيل staging يحتاج راية `E2E_ALLOW_STAGING=1` ومضيفًا يحتوي `staging` وقاعدة
  مؤقتة هناك — لم يُنفَّذ في هذه الجولة.

## 8. ماذا تشغّل بوابة `e2e` في CI؟

وظيفة `e2e` في `.github/workflows/ci.yml` (فحص إضافي حاجب):

1. `postgres:16` كخدمة + `dotnet restore --locked-mode` + بناء الخلفية.
2. `dotnet ef database update --context DocGeneratorPostgresDbContext` على `docgen_e2e`.
3. تثبيت `postgresql-client` ثم `psql -f scripts/e2e/seed.sql`.
4. `npx playwright install --with-deps chromium` (Chromium المثبَّت بالإصدار — لا Chrome المستضيف).
5. `oxlint` على مصادر E2E + اختبار حارس السلامة الذاتي.
6. `npx playwright test` (31 اختبارًا: 19 سطح مكتب + 12 جوال).
7. رفع `playwright-report` و`test-results` عند الفشل فقط (7 أيام).

## 9. تشخيص الفشل

```bash
cd frontend
npx playwright show-report           # تقرير HTML
npx playwright test --ui             # تشغيل تفاعلي
npx playwright test --debug          # خطوة بخطوة
npx playwright test --project=mobile --grep "375px"
```

- **trace** يُسجَّل عند إعادة المحاولة الأولى (CI يعيد مرتين).
- **screenshot/video** عند الفشل في `frontend/test-results/`.
- فشل `401`/`403` متكرر غالبًا قاعدة غير مبذورة أو مستخدم خاطئ: أعد البذر.
- فشل `net::ERR_CONNECTION_REFUSED` يعني أن `webServer` لم يقلع: راجع سجل الـAPI في
  مخرجات Playwright (`stdout: pipe`)، وتأكد من `dotnet build` ومن `E2E_DB_CONNECTION`.
- فشل `axe` يعرض `id`/`impact`/أول 5 عناصر — أصلح العنصر، ولا تُوسّع القائمة البيضاء
  بلا سبب مكتوب.

## 10. قاعدة البيانات: تنظيف وإعادة

```powershell
# إعادة ضبط كاملة (تفقد كل بيانات E2E فقط)
$env:PGPASSWORD='<كلمة مرور postgres المحلية>'
dropdb -h 127.0.0.1 -U postgres --force docgen_e2e
createdb -h 127.0.0.1 -U postgres docgen_e2e
# ثم أعد الهجرات والبذر (§3)
```

**ممنوع** استخدام قاعدة الإنتاج أو أي قاعدة مشتركة، ولو لـ«قراءة فقط». الحارس يمنعها
قبل التشغيل؛ لا تُعطِّله.
