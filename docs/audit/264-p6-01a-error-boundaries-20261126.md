# `P6-01a` — حدود خطأ مشتركة وإعادة محاولة آمنة وعميل `API` موحّد (بلا ترحيل `TanStack Query`) — 2026-11-26

> **النطاق:** بنية أمامية مشتركة مرنة **دون إعادة كتابة الصفحات**: حدّ خطأ عربي
> (جذر + محتوى) يستبدل الشاشة البيضاء، وتشدّد العميل `axios` الموحّد القائم
> (`frontend/src/api/client.ts`) بلا استبداله: `withCredentials`، `timeout`،
> إعادة محاولة للقراءات فقط، احترام `Retry-After`، إلغاء ينتشر، وتطبيع خطأ مطبَّع.
>
> **المراجع:** `docs/PERFECTION_ROADMAP.md` §`P6-01`، `AGENTS.md` (قواعد الواجهة
> وإرشادات الويب)، `frontend/src/App.tsx`، `frontend/src/components/Layout.tsx`،
> `frontend/src/api/client.ts`، `backend/src/DocGenerator.Api/Middleware/CsrfMiddleware.cs`،
> `docs/audit/49-p4-03-error-contract-20261117.md`، و`docs/audit/261`–`263`.
>
> **الفرع:** `refactor/p6-01a-error-boundaries`.

## 1. المشكلة والقرار

`P6-01` يطلب: `TanStack Query` بدل `useEffect` اليدوي، و`axios instance` موحّدًا،
و`error boundaries`، و`retry/backoff` للقراءات، مع `DoD` «لا تكرار fetch ولا شاشة
بيضاء عند خطأ». الواقع قبل المرحلة:

- `ErrorBoundary` موجود لكنه: بلا إعادة ضبط عند التنقل (يبقى مغلقًا على شاشة الخطأ)،
  ويسجّل `console.error(error, info)` في **كل** البيئات (قد يحمل رموزًا/بيانات
  شخصية من `stack`)، وبلا معرّف خطأ ولا نسخ ولا «عودة للرئيسية».
- حدّ واحد فقط حول التطبيق كله، فلا نجاة للتنقل إذا انهارت صفحة.
- العميل الموحّد موجود (`api`) لكنه بلا `withCredentials`/`timeout`، وإعادة
  المحاولة الوحيدة محصورة في `429` بمهلة ثابتة تتجاهل `Retry-After`، وبلا
  تطبيع خطأ مطبَّع.

**القرار:** مرحلة `P6-01a` تسلّم **البنية المشتركة** فقط: حدود خطأ + تشدّد العميل
القائم + اختبارات. **لا** تُدخل `TanStack Query` في هذه المرحلة (لم تكن في
المستودع أصلًا)، ولا تُغيَّر عقود `API` أو المسارات أو `DTO`، ولا تُعاد كتابة
الصفحات. يبقى ترحيل `useEffect` اليدوي إلى `TanStack Query` بندًا صريحًا متبقيًا
في `P6-01`.

## 2. الملفات المسلَّمة

| الملف | المسؤولية |
|---|---|
| `frontend/src/components/ErrorBoundary.tsx` | حدّ خطأ صنفي: التقاط خطأ العرض، بديل عربي mobile-first، إعادة محاولة/رئيسية/نسخ معرّف، `resetKeys`، تسجيل تطويري منقّى |
| `frontend/src/components/ErrorBoundary.test.tsx` | التقاط + تركيز العنوان + إعادة الضبط (زر و`resetKeys`) + النسخ + تنقية السجل + هرمية العنوان |
| `frontend/src/components/Layout.tsx` | حدّ محتوى حول `Outlet` مع `resetKeys` من `useLocation` — التنقل يبقى حيًّا |
| `frontend/src/components/Layout.errorBoundary.test.tsx` | اختبار تكاملي: صفحة تنهار داخل `Layout` ثم الانتقال من الشريط الجانبي يغادر شاشة الخطأ |
| `frontend/src/components/Layout.test.tsx` | إضافة `useLocation` إلى تمثيل `react-router-dom` في الاختبار القائم |
| `frontend/src/App.tsx` | حدّ الجذر `variant="screen"` مع `resetKeys` من الموقع |
| `frontend/src/utils/redactSensitiveText.ts` | تنقية نصوص السجل/الإبلاغ: كوكي/`CSRF`/`Bearer`/`JWT`/أسرار استعلام/بريد/هاتف/رموز طويلة |
| `frontend/src/utils/redactSensitiveText.test.ts` | مصفوفة التنقية والحالات الفارغة |
| `frontend/src/api/client.ts` | `withCredentials`، `timeout` 60s، سياسة إعادة محاولة قراءة فقط، `Retry-After`، انتظار قابل للإلغاء، `ApiErrorInfo`/`toApiErrorInfo` |
| `frontend/src/api/client.test.ts` | إعداد المثيل، مصفوفة إعادة المحاولة، `Retry-After`، الإلغاء، CSRF للطرق المغيّرة، تطبيع الخطأ |
| `docs/PERFECTION_ROADMAP.md` §`P6-01` | حالة صادقة: البنية المشتركة منجزة، ترحيل `TanStack Query` متبقٍ |
| `docs/README.md` | إضافة اللقطات `264`–`266` |

**لا تغيير** في `package.json`/الأقفال، ولا حزم جديدة، ولا هجرات، ولا `DTO`.

## 3. حدود الخطأ (`ErrorBoundary`)

### 3.1 ما تلتقطه وما لا تلتقطه (حدّ React نفسه)

- تلتقط **أخطاء العرض ودورة الحياة** داخل الشجرة (`render`، `componentDidMount`،
  إلخ). هذا كل ما تسمح به React.
- **لا تلتقط** أخطاء `async` (رفض وعود، `setTimeout`، مستمعو الأحداث، `fetch`)،
  ولا أخطاء مُعالِج الحدث نفسه. هذه تُعالَج في موضعها (`try/catch`/`catch` على
  الوعد) وتُعرض كحالة خطأ داخل الصفحة. **لا يُدّعى** خلاف ذلك في الكود أو الوثيقة.
- خطأ تحميل حزمة `lazy` (شبكة) يصل إلى الحدّ؛ «إعادة المحاولة» تعيد العرض لكن
  `React.lazy` يحتفظ بالرفض، لذا الرابط إلى الرئيسية (انتقال كامل) هو المخرج
  العملي — وهو معروض صراحةً.

### 3.2 المستويان

| المستوى | الموضع | السلوك |
|---|---|---|
| جذر | `App` حول `AuthProvider`/`Routes` | `min-h-dvh` بديل كامل عند انهيار الهيكل؛ لا شاشة بيضاء |
| محتوى | داخل `<main>` في `Layout` حول `<Outlet />` | البديل يحلّ محل الصفحة فقط؛ الشريط الجانبي (مكتبي) والدرج/الشريط السفلي (جوال) يبقيان عاملين |

`resetKeys` تُقارن سطحًا (`Object.is`)؛ في المستويين تُمرَّر `[pathname, search]`،
فتُصفَّر الحدود عند أي تنقل (رابط، زر رجوع، توجيه) بدل حبس المستخدم على شاشة
الخطأ.

### 3.3 البديل (Arabic, mobile-first, a11y)

- `role="alert"` على الحاوية، وعنوان `h1` (جذر) أو `h2` (محتوى، حفاظًا على
  هرمية الصفحة) بـ`tabIndex={-1}` ويُركَّز عند الظهور.
- أزرار/رابط بارتفاع أدنى `min-h-11` (44px)، `flex-col` على الجوال و`sm:flex-row`،
  `max-w-xl`، ومعرّف الخطأ `break-all` — لا أبعاد ثابتة ولا تجاوز أفقي.
- أفعال: **إعادة المحاولة** (تصفير الحالة)، **العودة إلى الرئيسية** (`<a href="/">`)،
  **نسخ معرّف الخطأ** (`navigator.clipboard` مع بديل `execCommand`) وتأكيد
  `role="status"`/`aria-live="polite"`.
- لا حركة/`transition: all`، وحالات تركيز مرئية (`focus-visible:ring-*`) مع بديل
  صريح عند `outline-none`.

### 3.4 السجل وتقليل البيانات

- `console.error` في **التطوير فقط** (`import.meta.env.DEV`)، بسطر واحد:
  `[ErrorBoundary:<id>] <name>: <message منقّى>` — بلا `stack`/`componentStack`
  خامًا.
- التنقية (`redactSensitiveText`) تحجب: `docgen_csrf`/`docgen_session`، `Bearer`،
  `X-CSRF-Token`، `JWT`، أسرار الاستعلام (`token/password/secret/csrf/...`)،
  البريد، أرقامًا طويلة، ورموز `base64/hex` ≥48 حرفًا.
- الإبلاغ الخادمي القائم (`reportClientError`) يبقى صامتًا، لكن بعد التنقية أيضًا،
  ويُضاف معرّف الخطأ إلى الرسالة لربط شكوى المستخدم بالسجل.
- **حدّ معلن:** React نفسها تطبع الخطأ الخام في `console` بوضع التطوير (سلوك
  إطار لا نتحكم به). في بناء الإنتاج لا يوجد هذا السجل، ولا يوجد سجلنا (بوابة
  `DEV`). الاختبار يفحص **سجلنا** تحديدًا.

## 4. العميل الموحّد — الفرق عن القائم (تشدّد لا استبدال)

المثيل `api` القائم بقي كما هو (نفس `baseURL`، `paramsSerializer`، اعتراض
`CSRF`/`Idempotency`، إعادة توجيه `401`، إبلاغ `5xx`). أُضيف:

| البند | قبل | بعد |
|---|---|---|
| `withCredentials` | غير محدد | `true` صراحةً (كوكي الجلسة) |
| `timeout` | بلا سقف | `60_000ms` افتراضيًا؛ تنزيلات `blob` تُمنح `300_000ms` (تصدير/توليد أطول بطبيعته)، ويتجاوز المستدعي بـ`timeout` صريح |
| إعادة المحاولة | `429` لقراءة واحدة بمهلة ثابتة 1000ms | `GET`/`HEAD` فقط: شبكة/مهلة/`5xx`/`429`، **3 محاولات إجمالًا**، تباعد أُسّي مشوّش |
| `Retry-After` | مُهمَل | يُحترم (ثوانٍ أو تاريخ HTTP) بسقف 30s |
| الإلغاء | لا يوقف إعادة المحاولة | `abort` أثناء الانتظار يرفض فورًا بخطأ إلغاء، ولا محاولة تالية |
| تطبيع الخطأ | `getApiErrorMessage` نصيًا | `ApiErrorInfo` (`kind/status/code/message/field/traceId/retryable`) عبر `toApiErrorInfo` مع إبقاء الدالة القديمة |

### 4.1 سياسة إعادة المحاولة (مصفوفة)

| الحالة | `GET`/`HEAD` | `POST`/`PUT`/`PATCH`/`DELETE` |
|---|---|---|
| خطأ شبكة/مهلة | يعيد (حتى 3 محاولات) | **لا** |
| `5xx` | يعيد | **لا** |
| `429` | يعيد مع `Retry-After` | **لا** |
| `400/401/403/404/409` | لا | لا |
| طلب مُلغى (`ERR_CANCELED`/`signal.aborted`) | لا | لا |

- التباعد: `300ms · 2^(n-1)` بسقف 10s، مع تشويش في `[half, full]`؛ عند وجود
  `Retry-After` يحكم الخادم (بسقف 30s).
- العدّاد `_retryAttempt` يُحمَل في `config` (يحفظه دمج axios) ويمنع التتالي.
- الإبلاغ عن `5xx` يقع **مرة واحدة** بعد استنفاد المحاولات فقط، وليس لكل محاولة.

### 4.2 تطابق `CSRF` مع الخادم

`CsrfMiddleware` يفحص `POST/PUT/PATCH/DELETE` ويقارن `X-CSRF-Token` بكوكي
`docgen_csrf` (بلا اعتماد على `Origin`)، ويستثني الدخول/الخروج وتقرير `CSP`.
اعتراض العميل يرفق الترويسة لكل طريقة غير `GET/HEAD/OPTIONS` عند وجود الكوكي —
أي **تغطية أوسع آمنة** (لا ضرر من إرسالها للدخول/الخروج لأن الخادم يتجاهلها)،
ولا تُرسل في القراءات. لا يُخزَّن أي توكن في `localStorage`.

## 5. الاختبارات (دليل)

- `ErrorBoundary.test.tsx` (8): التقاط + `role="alert"` + تركيز العنوان؛ محتوى
  سليم؛ إعادة المحاولة تُصفّر وتعرض المحتوى بعد زوال الخطأ؛ `resetKeys` يعيد
  الضبط؛ نسخ المعرّف (`navigator.clipboard`)؛ لا رموز/بريد/هاتف في سجلنا؛ عدم
  السجل في `MODE=production`؛ `variant="content"` يعرض `h2`.
- `Layout.errorBoundary.test.tsx` (1): صفحة ترمي داخل `Layout` → البديل داخل
  `main`، الشريط الجانبي قائم، والنقر على «لوحة التحكم» يغادر شاشة الخطأ.
- `redactSensitiveText.test.ts` (4): كل أنماط الحجب + النص العربي + المدخلات
  الفارغة/غير القابلة للتحويل + القص.
- `client.test.ts` (الزائد الجديد): إعداد المثيل؛ CSRF للطرق المغيّرة فقط
  (POST/PUT/PATCH/DELETE مقابل GET/HEAD)؛ مصفوفة السياسة (GET/HEAD يعيد،
  الكتابات و4xx لا، الملغى/سقف المحاولات لا)؛ `Retry-After` ثوانٍ/تاريخ/سقف؛
  التشويش؛ الإلغاء (فوري وأثناء الانتظار)؛ تكامل: GET شبكة → 3 نداءات، GET 500
  → نجاح ثانٍ، POST 500 → نداء واحد وبلا مؤقت، 429 مع `Retry-After` لا يبكر؛
  تطبيع `ApiErrorInfo`.

## 6. البوابات

<!-- GATES_RESULT -->

**الواجهة (`frontend/`):** `npx oxlint src scripts` نظيف · `npx tsc -b` نظيف ·
`npx vitest run --maxWorkers=4 --pool=forks` = **173/173 ملفًا، 1837/1837 اختبارًا**
(+ إعادة تشغيل مستهدفة لاختبارات الحدود بعد تنظيف العزل = 8/8) · `npm run build`
ناجح · `npm run budget`: `JS=90`، إجمالي **1730.5kB**، أكبر **388.2kB**، دخول
**259.7kB** (كلها ضمن السقوف) · `npm run guard` (CSP): **93** ملفًا نظيفًا ·
`npm run generate:api` + `git diff --exit-code -- src/api/generated.ts` = **لا انحراف**.

**الخلفية والحراس:** `dotnet restore … --locked-mode` نظيف · بناء
`-warnaserror` = **0 تحذير/0 خطأ** · `dotnet format --verify-no-changes` نظيف ·
`Architecture` **41/41** · `Application` **778 ناجحًا + 1661 متخطّى** (بوابات
`PostgreSQL` تتخطى محليًا بلا خدمة؛ تُنفَّذ في `backend-postgres` بالـ`CI`) ·
`Api` (غير تسلسلي) **916 ناجحًا + 615 متخطّى**، والتسلسلي **0 ناجح + 5 متخطّى**
(فئة `Sequential` تعتمد `Postgres` أيضًا) · الحراس الستة
(`subhead`/`determinism`/`migration-watchlist`/`migration-safety`/`secrets`/
`postgres-suite --self-test`) نظيفة · `observability-config-guard --self-test`
و`backup-scripts-guard --self-test` (6 سكربتات + 8 تعديلات مرفوضة) نظيفان ·
`dependency-audit` (ذاتي + فعلي) **0 High/Critical في 9 مشاريع** ·
`npm-audit-guard` (ذاتي + فعلي) **0 ثغرة** · `ef has-pending-model-changes`
= «No changes … since the last migration».

**الهجرات:** لا هجرة جديدة ولا تغيير نموذج — `has-pending-model-changes` يبقى نظيفًا.

**ملاحظة إفصاح:** لا يوجد متصفح مكتبي متصل في جلسة التنفيذ ولا `Playwright` في
المستودع، فلم تُلتقط لقطة 375px آلية؛ الفحص بنيوي (أصناف مرنة/`min-h-11`/
`break-all`) + اختبارات jsdom. التحقق البصري الحاجب بند `P6-04`.

## 7. الحدود وما لم يُنفَّذ (إفصاح)

- **`TanStack Query` لم يُدخل** (بند `P6-01` المتبقي): لا تكرار-إزالة لاستدعاءات
  `useEffect`، ولا ذاكرة مؤقتة مشتركة/`invalidate` بعد الكتابة. البنية الحالية
  تُهيّئ الأرضية (عميل موحّد + خطأ مطبَّع) لكنها لا تدّعي `DoD` الكامل.
- حدود الخطأ لا تلتقط أخطاء `async` — المعالجة في كل صفحة كما هي.
- `toApiErrorInfo` لا يفكّ جسم خطأ `Blob` (يبقى `getDownloadErrorMessage` للتنزيلات).
- اختبار 375px هنا **بنيوي** (أصناف مرنة/`min-h-11`/`break-all`)، لا لقطة متصفح
  آلية؛ `P6-04` يضيف `Playwright`/`axe` الحاجب.
- زمن الانتظار الافتراضي 60s للنداءات العادية (5 دقائق لتنزيلات `blob`)، وقد
  يحتاج تجاوزًا صريحًا لعملية تصدير ضخمة جدًا على شبكة بطيئة (مذكور كتعليق في
  `client.ts`).
