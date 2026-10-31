# خطة إصلاح `PR-4` — نسخة مُنقّحة بعد مراجعة نقدية

> **تاريخ الوثيقة:** 2026-10-28
> **الفرع:** `feature/head-alerts-pr4-api` · **الأساس:** `e0d8384`
> **الحالة:** المراحل 0–7 ✅ (حالة الكود المثبتة `4e0d3ae`؛ 7 توثيقية في 2026-10-31) · التالية: المرحلة 8 (`PR-5`) بعد حسم `D10`
> **القاعدة:** لا هجرات قاعدة بيانات في هذا العمل بالكامل.

---

## 0. للمحادثة الجديدة — ابدأ من هنا

1. اقرأ `AGENTS.md`، ثم `docs/HEAD_ALERTS_RESTRUCTURE_PLAN.md` (§8، §9، §13، §14، §2637–2642)، ثم هذه الوثيقة كاملة.
2. افحص الحالة: `git status --porcelain` و`git branch --show-current`.
   - إن كانت تغييرات `PR-4` (10 معدّلة + 6 جديدة) ما زالت غير مُودعة، نفّذ **المرحلة 0** أولًا.
   - إن كانت مُودعة، تحقّق أن `HEAD` يطابق وصف المرحلة 0.
3. لا تنتقل بين المراحل قبل خضرة البوابات المذكورة في نهاية كل مرحلة.
4. القرارات `D1–D12` في §3 **مثبتة**. أي تغيير فيها يحتاج موافقة المالك الصريحة أولًا.
5. أي انحراف عن هذه الوثيقة يُسجَّل في §12 (سجل الوثيقة) قبل التنفيذ، لا بعده.
6. عند إنهاء كل مرحلة: حدّث §7 (مصفوفة التتبّع) وضع علامة `[x]`.

### 0.1 مرساة التسليم — حالة التنفيذ عند بداية المحادثة الجديدة

> مُثبَّتة في **2026-10-31** عند بداية **المرحلة 7** (بعد إغلاق المرحلة 6 ورقعة مراجعتها). إن خالفتها
> حالة المستودع الفعلية فالحالة الفعلية هي المرجع، ويُحدَّث هذا القسم قبل أي تنفيذ.

| البند | الحالة المثبتة |
|---|---|
| الفرع | `feature/head-alerts-pr4-api` — `origin` = **التزام هذه المرساة** (توثيقي فوق `4e0d3ae`)، وشجرة العمل نظيفة. **حالة الكود المثبتة = `4e0d3ae`** |
| سلسلة الالتزامات | `bd4f8c1` (وسم `pr4-baseline`) → … → `a9bcb7a` (تبعية `follow-redirects`) → `0b3bcf2` (مرساة المرحلة 5) → `902598c` (المرحلة 6) → `a954679` (إغلاق توثيقي) → `f01c74d` (رقعة متانة `CI`/`Postgres`) → `4e0d3ae` (تثبيت أرقام الإغلاق) → التزام هذه المرساة (توثيقي) |
| `PR #54` | مفتوح و`MERGEABLE`، و`CI` **12/12** على `f01c74d` و`4e0d3ae` (ستة فحوص × تشغيلين لكل منهما). **لا يُدمج قبل `PR-5`**: نقاط القائمة تُرجع `PagedResult` بينما الواجهة المنشورة تتوقع مصفوفة |
| البوابات عند تسليم المرحلة 4 (`a9bcb7a`) | `dotnet build` 0/0 · `Application` **2130/2130** · `Api` **718** + **2** تتابعية (رقم **719** في المرساة السابقة كان غير دقيق — أُثبت بـ`--list-tests` على `a9bcb7a`) · `oxlint`/`tsc -b` صفر · `vitest` 152 ملفًا/1599 اختبارًا · `npm run build` ناجح |
| البوابات بعد المرحلة 5 (ورقعة المراجعة) | `dotnet build` 0/0 · `Application` **2147/2147** · `Api` **721** + **2** تتابعية · `oxlint`/`tsc -b` صفر · `vitest` 152/1599 · `npm run build` ناجح (التفصيل في §12) |
| البوابات الحالية (بعد المرحلة 6 ورقعة متانتها) | `dotnet build` 0/0 · `Application` **2149/2149** · `Api` **724** + **2** تتابعية · `oxlint`/`tsc -b` صفر · `vitest` 152/1599 · `npm run build` ناجح · `CI` **12/12** على `f01c74d` و`4e0d3ae`، و`backend-postgres` شغّل الإسقاط الحيّ (`Passed HeadAlertSummaryPostgresTests: 1`، `Skipped: 0`) |
| أمر اختبار `Api` | `--filter "Category!=Sequential"` ثم `--filter "Category=Sequential"` — وإلا يفشل `EnricherMiddlewareTests` فشلًا زائفًا عند التوازي |
| قيد تقني حاكم | `EnableRetryOnFailure` **غير مفعّل** في تسجيل `DbContext` ⇒ `MarkReadAsync` عبر `TryMarkReadAsync` (تحديث ذرّي بلا كيان متتبَّع ولا معاملة) |
| الهجرات | لا هجرات في هذا العمل كله. `RUN_GUIDE.md` §9 كما هو: `SQLite` **4 معلّقة** بالترتيب `Expand` ← `Outbox_Create` ← `Backfill` ← `Contract` (و`Contract` محظورة قبل `G2`)، و`PostgreSQL` **63 / صفر معلّقة** |
| بنود مفتوحة موثقة | `ح‑3`/`ح‑5`/`ح‑8` (كما هي، لا تُعاد بلا سبب)، **`ف‑6`** (منشئ ملف انتقل لفرع آخر — قرار عمل)، **`ف‑7`** (تفاوت `CreateSystemAsync`/`DocumentCreator`)، **`D10`** (اختيار `atomic`/`normalizer` مستحق **قبل المرحلة 8**)، **`D6`** (سطح `received` مؤقت يُحسم في `PR‑6`) |
| نطاق المرحلتين 7 و8 | يبقيان على هذا الفرع، لأن §5 ينص أن `PR‑5` يُبنى على فرع `PR‑4` لا على `master` — لا فرع خلفية جديد |
| المرحلة التالية | **8 (الواجهة `PR-5`)** — يُحسم `D10` (`atomic`/`normalizer`) قبلها. المرحلة 7 أُغلقت في 2026-10-31 دون تغيير كود إنتاج ولا هجرات |

> **تحديث بعد إغلاق المرحلة 7 (2026-10-31):** حالة الكود لم تتغيّر ولا تزال `4e0d3ae`؛ فالالتزامات
> اللاحقة للمرساة توثيقية فقط. أُغلقت `P3-7`/`P3-12`، وأُضيف `§9.4`/`§9.5` وبند `PR-6` في خطة إعادة
> الهيكلة، وقسم اقتران النشر في `RUN_GUIDE.md` §9. **البنود المفتوحة كما هي:** `ف‑6`، `ف‑7`، `D10`
> (يُحسم قبل المرحلة 8)، و`D6` (سطح `received` المؤقت). لا تُعاد المراحل 0–6 ولا الملاحظات `ح‑1…ح‑26`.

---

## 1. السياق والحالة الحالية

### 1.1 ما أُنجز في `PR-4`

- عقود `HeadAlertSummaryDto` و`HeadAlertDto` و`HeadAlertRecipientDto` و`CreateHeadAlertRequest`.
- إسقاط SQL للملخصات بلا نص كامل، وترقيم صارم `1..100`، وترتيب `LastContentChangedAt DESC, Id DESC`.
- صلاحيات `HeadAlertActor` وقرار `D2` للتفاصيل (مستلم → منشئ → إشراف → `404`).
- `AlertsController` بثمانية أسطح، و`201` بجسم `Summary` + `Location`.
- اختبارات عقد/مستودع/صلاحيات/تكامل جديدة.

### 1.2 خط الأساس الأخضر (يُعاد إثباته في المرحلة 0)

| البوابة | النتيجة |
|---|---|
| `dotnet build backend/DocGenerator.sln` | 0 أخطاء / 0 تحذيرات |
| اختبارات التطبيق | 2092 ناجح / 0 فاشل |
| اختبارات API (غير تتابعية) | 705 ناجح |
| اختبارات API (تتابعية) | 2 ناجح |
| `npx oxlint src` / `npx tsc -b` / `npm run build` | خضراء |
| `npx vitest run` | 152 ملفًا / 1599 اختبارًا |

### 1.3 ما لا يفعله هذا العمل

- لا يضيف أو يعدّل هجرات، ولا يلمس `Migrations` أو `MigrationsPostgres`.
- لا ينشر، ولا يغيّر `PagedResult<T>`، ولا يعيد تصميم الواجهة.
- لا يعدّل `docs/audit/` (لقطات مجمّدة).

---

## 2. المبادئ غير القابلة للتفاوض

1. **مصدر صلاحية واحد:** كل قرار دور/فرع/طرف يُتخذ في طبقة الخدمة (`HeadAlertService`)، والمتحكم يترجم HTTP فقط (تحليل استعلام + تحويل استثناءات). **لا استثناء** — بما فيه «رئيس بلا فرع» (`D11`).
2. **مُسقِط واحد:** بناء `HeadAlertSummaryDto` في موضع واحد، وبناء عنوان الملف في موضع واحد.
3. **لا كسر عقود:** لا حقل يُحذف أو يُعاد تسميته. أي إضافة سطح تكون إضافية وقابلة للتجاهل من عميل قديم.
4. **كل سلوك جديد له اختبار:** اختبار وحدة للقاعدة، واختبار تكامل للسلك عندما يتغير العقد.
5. **لا صمت:** لا `catch` فارغ، ولا `null` مُلفَّق، ولا `TODO` بلا مالك وموعد.
6. **التحقق الكامل:** بعد كل مرحلة، البوابات كاملة (وليس اختبارات مستهدفة فقط). أثناء التنفيذ يجوز تشغيل مرشّح ضيق، لكن الإيداع لا يحدث قبل البوابات.
7. **لا حلول اسعافية:** لا `#pragma warning disable`، لا معاملات اختيارية جديدة لتجنّب تحديث المستدعين، لا استثناءات في `Directory.Build.props`.

---

## 3. قرارات المالك (`D1–D12`)

| المعرّف | القرار | الحالة |
|---|---|---|
| `D1` | `by-delegation` يعيد `PagedResult<Summary>`، افتراضي `page=1&pageSize=100` | مثبت |
| `D2` | `GET /{id}`: مستلم بأولوية مطلقة (بلا فرع) → منشئ → إدارة/إشراف → `404` | مثبت |
| `D3` | `POST /alerts` → `201` + `Summary` + `Location` | مثبت |
| `D4` | `RepoRootLocator` يصعد حتى `AGENTS.md` ويفشل برسالة صريحة | مثبت |
| `D5` | إبقاء `RecipientCount`/`UnreadCount` في التفاصيل حتى `PR-6` | مثبت |
| `D6` | `GET /alerts/unread-count?scope=received`: مجموع صفوف غير المقروءة **على التنبيهات التي استلمها الرئيس نفسه** في فرعه، بلا كشف تنبيهات لا يستلمها. **سطح مؤقت (`ح‑26`):** وُجد لحفظ رقم شارة `PR-5`؛ ويُحسم في `PR-6` هل تبقى تجميعية أم تصير `user` — وعند التحول يُحذف النطاق مع اختبار يثبت رفضه | مثبت |
| `D7` | عدّاد الرئيس الشخصي (`scope=user`) مقيّد بفرعه، مطابقةً للقائمة. **تغيير عقد صريح (`ح‑20`):** قبل المرحلة 4 كان العدّاد بلا `scope` يجمع كل فروع الرئيس؛ صار فرعه — لا مستهلك للواجهة، ويُوثّق كتغيير مقصود كـ`D11` | مثبت |
| `D8` | نقل كل صلاحيات التنبيهات إلى الخدمة، وحذف الفحوص المكررة من المتحكم | مثبت |
| `D9` | إبقاء عدّادات التفاصيل للمحامي مع اختبار صريح `UntilPR6` | مثبت |
| `D10` | استراتيجية التوافق بين الخلفية والواجهة: `atomic` أو `normalizer` (يُحسم قبل المرحلة 8) | مثبت — يُحسم الاختيار قبل المرحلة 8 |
| `D11` | «رئيس بلا فرع» حالة واحدة موثّقة: `400` في الكتابة (`POST`) كما هو العقد الحالي، و`403` في كل أسطح القراءة/العدّ — ويُتخذ القرار في الخدمة وحدها. **`MarkRead` مستثنى عمدًا:** تعليم القراءة يخصّ صف استلام شخصيًا لا نطاقًا فرعيًا، فلا يشترط فرعًا (وإلا انقطعت تنبيهات الرئيس الشخصية عند أي خلل في ربط الفرع). أي «إصلاح» يضيف `RequireBranch` إلى `MarkRead` يخالف هذا القرار. **الترتيب الملزم:** فحص الدور/الفرع **قبل** التحقق من شكل الترقيم، فالترقيم غير الصالح لا يحوّل `403` إلى `400` | مثبت + مراجعة المرحلة 3 (`ح‑13`، `ح‑16`) |
| `D12` | قراءة النص الكامل في `PR-5`: عبر `GET /alerts/{id}` عند الطلب (صف قابل للتوسيع أو `AlertDetailsSheet`)، لا بالاكتفاء بالمعاينة | مثبت |

> **تثبيت المرحلة 0:** ثُبِّتت `D6–D12` بموافقة المالك في 2026-10-29 (المرحلة 0، البند 3).
> `D10` مثبتٌ **كإطار قرار**، ويبقى اختيار `atomic`/`normalizer` نفسه مستحقًا قبل المرحلة 8 كما ينص القرار.


### 3.1 تفصيل `D6` — لماذا `received` وليس `branch`؟

الواجهة الحالية في `AccountPage.tsx:66-69` تجمع `unreadCount` عبر التنبيهات التي **استلمها الرئيس** (أي `GET /alerts` الخاص به). العدّاد على مستوى الفرع بالكامل سيكشف حجم/وجود تنبيهات لا يستلمها الرئيس (تنبيهات موجهة لمحامين بعينهم)، وهذا يخالف مبدأ «القراءة بالمستلم» في §8.

لذلك `scope=received` يعني حرفيًا:

```sql
COUNT(recipient rows)
WHERE NOT IsRead
  AND NOT HeadAlert.IsArchived
  AND HeadAlert.BranchId = @branchId
  AND EXISTS (HeadAlert.Recipients WHERE UserId = @headUserId)
```

هذا يعيد **نفس رقم** الواجهة القديمة، لكن دون الاعتماد على تحميل صفحة واحدة.

**تمييز لازم بين النطاقين (يمنع التباسًا في الواجهة):**

| النطاق | ما يعنيه | مثال |
|---|---|---|
| `received` | كل صفوف غير المقروءة على التنبيهات التي استلمها الرئيس — أي مجموع `unreadCount` لكل تنبيه في قائمته (السلوك القديم حرفيًا) | تنبيه وصل الرئيس و3 محامين، 2 منهم لم يقرأوا → `2` |
| `user` | صفوف غير المقروءة **العائدة للرئيس نفسه** فقط، مقيّدة بفرعه (`D7`) | نفس المثال → `0` إن كان الرئيس قد قرأ |

⇒ `AccountPage` في `PR-5` يستخدم `received` حفاظًا على مطابقة الشارة، ويُوسم نصًا صريحًا («غير المقروء في تنبيهاتك») لا «غير المقروءة لي». ويُسجَّل في `PR-6` قرار: هل تبقى الشارة تجميعية أم تتحول إلى `user` بعد إزالة عدّادات التفاصيل.

### 3.2 تفصيل `D10` — التوافق والإصدار

| السيناريو | القرار | الأثر |
|---|---|---|
| نشر الواجهة والخلفية ذريًا (نافذة واحدة) | `atomic`: لا طبقة توافق | كود أنظف، لكن تراجع أحد الطرفين منفردًا غير آمن |
| نشر منفصل أو احتمال نافذة زمنية | `normalizer` في الواجهة لمدة إصدار واحد | يجعل الترتيب آمنًا في الاتجاهين، ويُزال في `PR-6` |

**قاعدة الإزالة:** `normalizer` يُحذف في `PR-6` مع اختبار يثبت أن الواجهة لا تقبل الشكل القديم بعد الإزالة. لا يُترك `TODO` مفتوحًا.

### 3.3 تفصيل `D12` — لماذا لا تكفي المعاينة؟

الواجهة الحالية تعرض **النص الكامل** داخل `AlertRow` (`alert.message` في خمسة فروع عرض). بعد تحويل القوائم إلى `HeadAlertSummaryDto` لن يعود النص الكامل موجودًا على السلك أصلًا، و`messagePreview` مقطوعة عند حدّ نقاط Unicode. لا يوجد اليوم أي مستدعٍ لـ`GET /alerts/{id}` في `frontend/src` — الاستدعاءات القائمة هي `/alerts` و`/alerts/sent` و`/alerts/unread-count` و`/alerts/by-delegation/{id}` و`/alerts/{id}/read` فقط.

⇒ إن لم يُضف `PR-5` قراءةً عند الطلب، فسيفقد المستخدم نهاية كل تنبيه طويل **بصمت**. لذلك:

| الخيار | الأثر | الحكم |
|---|---|---|
| صف قابل للتوسيع يجلب `GET /alerts/{id}` عند أول فتح | أقل تغيير، ويستعمل العقد القائم | **موصى به** |
| `AlertDetailsSheet` مستقل (كما في الخطة الأم §2598) | يحتاج مسارًا/حالة إضافية | مقبول إن نُفِّذ ضمن `PR-5` الكامل |
| الاكتفاء بالمعاينة | فقدان نص نهائي | مرفوض |

**قاعدة:** لا يُغلق `PR-5` قبل اختبار يثبت أن فتح تنبيه طويل يعرض نصه الكامل من `GET /alerts/{id}`، وأن القائمة نفسها لا تحمّله.

---

## 4. ما غيّرته المراجعة النقدية للخطة السابقة

| # | الادعاء السابق | الخلل | التصحيح في هذه النسخة |
|---|---|---|---|
| 1 | `scope=branch` يجمع غير المقروء للفرع | تسريب معلومات + تغيير دلالة العدّاد | `scope=received` بدلالة «التنبيهات المستلمة» فقط |
| 2 | إصلاح صلاحية `by-delegation` وحده | نصف إصلاح؛ `CreateAsync` بلا فحص دور في الخدمة أصلًا | `D8`: نقل كل الصلاحيات إلى الخدمة، وحذف فحوص المتحكم المكررة |
| 3 | `RolePermissions.CanCreateAlerts` في الخدمة | `RolePermissions` يسكن `DocGenerator.Api`؛ لا يمكن للتطبيق الاعتماد عليه | استخدام `HeadAlertActor.IsHeadOrSubHead` المكافئ |
| 4 | قارئ مزدوج يفكّ الغلاف فقط | لا يعالج `message` → `messagePreview` عند نشر الواجهة أولًا | `normalizer` يحوّل الغلاف **والحقول**، أو `atomic` |
| 5 | مسار تفاصيل بـ«استعلامين» | الوصف غير دقيق: استعلام واحد بأربعة `Include` (انفجار ديكارتي وتحويل كيان كامل) | إسقاط واحد `HeadAlertDetailRow` بلا كيانات ولا `Include` |
| 6 | `CountUnreadAsync` يتغير في المرحلة 3 ثم 5 | تغيير توقيع مزدوج | نقل توحيد الصلاحيات (المرحلة 3) قبل العدّادات (المرحلة 4) |
| 7 | خريطة أنواع كاملة في بيان العقد | تراكم وصيانة 50 مدخلًا | `forbidden` من البيان + قائمة nullability صغيرة فقط |
| 8 | `Create` بلا فرع → `400` يبقى في المتحكم | يتعارض مع مبدأ «الصلاحية في الخدمة» | `D11`: القرار في الخدمة وحدها (`ArgumentException` → `400`)، وبلا تكرار في المتحكم |
| 9 | قائمة ملفات الواجهة ناقصة | 6 ملفات اختبار ستُنسى | القائمة الكاملة في المرحلة 8 |
| 10 | التراجع PR منفردًا | تراجع الواجهة وحدها يكسر القائمة | وحدة التراجع = الزوج، أو `normalizer` |

### 4.1 ما غيّرته المراجعة الثانية (تكامل الخطة نفسها)

| # | الادعاء السابق | الخلل | التصحيح |
|---|---|---|---|
| 11 | «رئيس بلا فرع» يبقى `400` في المتحكم كاستثناء موثّق | قراران لحالة واحدة: `400` من فحص المتحكم، و`403` من الخدمة في بقية الأسطح ⇒ خرق مبدأ «مصدر صلاحية واحد» | `D11`: القرار في الخدمة وحدها — `ArgumentException` (400) للكتابة، `UnauthorizedAccessException` (403) للقراءة/العدّ |
| 12 | `scope=received` بلا تحليل أداء | استعلام `EXISTS` على جدول ينمو بلا سقف | تحقّق موثّق من الفهارس القائمة `(UserId, IsRead)` و`(BranchId, IsArchived, LastContentChangedAt, Id)` ⇒ لا فهرس جديد ولا هجرة |
| 13 | «`200` أو `400` حسب قرار السقف» | قرار مؤجَّل داخل خطة تُعلن قراراتها مثبتة | تثبيت: `400` عند فيضان `Skip` فقط؛ `page` كبير بـ`pageSize=1` = `200` بعناصر فارغة |
| 14 | اختبار `Postgres` «مبوّب» | خضرة كاذبة: خطوة تنجح دون تنفيذ أي اختبار | `DOCGEN_REQUIRE_POSTGRES=1` في CI ⇒ فشل صريح بدل `Skip`، وإثبات عدد الاختبارات المنفَّذة |

### 4.2 ما كشفته جولة التحقق النهائية (مراسٍ واقعية)

| # | ما تقوله الخطة | الواقع في الكود | التصحيح |
|---|---|---|---|
| 15 | إضافة `PagedResult<T>` إلى `management.ts` | موجود أصلًا في `types/common.ts:28` بنفس الحقول الخمسة | **إعادة استخدامه**؛ يُضاف `HeadAlertSummaryDto` وحده، بلا نوع ثانٍ |
| 16 | `Dashboard` يتحول إلى `PagedResult` فقط | الاستدعاءان بلا `pageSize` ⇒ الافتراضي 50 (`AlertPageRequest.DefaultPageSize`) ⇒ **قصّ صامت** بعد أن كانت القائمة كاملة | تمرير `pageSize=100` ومعالجة `totalCount > items.length` (ترقيم أو مؤشر «المزيد») |
| 17 | `AlertRow` يوجّه تنبيه الدوائر ببادئة النص | `AlertRow.tsx:34` يفحص `alert.message.startsWith(...)`؛ المعاينة قد تُبتر لاحقًا وقد تتغير الصياغة | التوجيه بـ`kind === "circuit_pending_documents"` (الحقل موجود في الملخص) |
| 18 | `AccountPage` يستبدل `reduce` بالعدّاد | `headAlertsQuery` يصبح بلا مستهلك بعد الاستبدال (3 مواضع) | حذف الاستعلام كاملًا وتحديث `summaryError` ومؤشر التحميل |
| 19 | `MarkRead` → «تعديل + `SaveChanges`» | الكتابة الحالية داخل `_tx.RunAsync` (`:375`)، وكل كتابات الخدمة مغلَّفة | **إبقاء `_tx.RunAsync`** — تغييرها ليس جزءًا من الإصلاح |
| 20 | «رتّب فحوص `AlertPageRequest`: `PageSize` أولًا» | لا اختبار يعتمد الترتيب، وإعادة الترتيب تغيير بلا داعٍ | الإبقاء على الترتيب الحالي وإضافة فحص الفيضان فقط |

---

## 5. استراتيجية الفروع والإصدار والتراجع

1. `PR-4` على `feature/head-alerts-pr4-api` (قائم).
2. `PR-5` على `feature/head-alerts-pr5-ui` مبنيًا على فرع `PR-4` لا على `master`.
3. الدمج: `PR-4` أولًا، ثم `PR-5` فورًا. لا نشر بينهما.
4. **ترتيب النشر:**
   - مع `D10=normalizer`: الواجهة أولًا (تتحمل الشكل القديم والجديد)، ثم الخلفية. التراجع بالعكس.
   - مع `D10=atomic`: نشر واحد متزامن. وحدة التراجع = الزوج.
5. `RUN_GUIDE.md`: سطر صريح «`PR-4`+`PR-5` حزمة واحدة؛ لا نشر منفرد».
6. لا هجرات ⇒ لا `dotnet ef database update`، ولا تحديث لقائمة الهجرات المعلّقة.

---

## 6. المراحل

### المرحلة 0 — خط الأساس والقرارات

**الهدف:** نقطة رجوع نظيفة وقرارات مثبتة.

**الإجراء:**
1. `git status --porcelain` — إن وُجدت التغييرات غير المُودعة، أودعها:
   `feat(head-alerts): PR-4 API contracts (S0-S5)`.
2. سجّل نتائج البوابات في وصف الـPR.
3. ثبّت `D6–D12` في §3 (بموافقة المالك).
4. أنشئ وسمًا محليًا `pr4-baseline` للرجوع.

**القبول:** شجرة نظيفة + وسم موجود + قرارات موثّقة.
**الحجم:** صغير.

---

### المرحلة 1 — التنظيف البنيوي (`P2-2`، `P2-1`، `P3-2` جزئيًا)

**الهدف:** سياسة نص واحدة، مُسقِط ملخص واحد، عنوان ملف واحد، سطح مستودع بلا ميت.

#### 1أ — نقل سياسة النص (`P2-2`)

- `git mv backend/src/DocGenerator.Application/Services/HeadAlertTextLimits.cs`
  → `backend/src/DocGenerator.Application/Common/HeadAlertTextLimits.cs`.
- `namespace` → `DocGenerator.Application.Common`.
- حذف `using DocGenerator.Application.Services;` من `HeadAlertRepository.cs:4`.
- تحديث `using` في `HeadAlertService.cs` والاختبارات التي تستخدمه.

**القبول:** `rg "Application\.Services\.HeadAlertTextLimits"` = صفر.

#### 1ب — مساعد العنوان (`P2-1`)

```csharp
// Application/Common/HeadAlertTitle.cs
public static string Build(
    int? documentId,
    bool documentExists,
    string? borrowerName,
    string? borrowerFather,
    string? borrowerFamily,
    string? documentType);
```

- `HeadAlertRepository.DocumentTitle` (`:238-253`) → حُذف والاستدعاء المباشر في `ToSummaryDto` (انحراف عن الصياغة الأولى موثّق في §12).
- `HeadAlertService.DocumentTitle` (`:991-998`) → تفويض بالحقول المفكوكة + `documentExists` (يبقى الغلاف حتى المرحلة 2 ثم يُحذف).
- `documentExists` يُمرَّر صراحةً من `a.Document != null` في إسقاط SQL ومن `alert.Document is not null` في الخدمة — لأن استنتاج الحذف من فراغ حقول العرض كان يخلط «مستند محذوف» بـ«مستند قائم بلا حقول».
- اختبار `HeadAlertTitleTests` (7 حالات: الترتيب، تجاهل الفراغ، نوع الملف، المحذوف، القائم الفارغ، بلا مرجع ملف، حقول فراغ فقط).

#### 1ج — مُسقِط ملخص واحد

- استخراج `SummaryRows(IQueryable<HeadAlert>, int?, bool)` من `ProjectSummariesAsync` (`:132-179`).
- إضافة إلى الواجهة/المستودع:

```csharp
Task<HeadAlertSummaryDto?> GetSummaryAsync(
    int alertId,
    int? recipientUserId,
    bool includeCounters,
    CancellationToken ct = default);
```

- التنفيذ: `SummaryRows(...).FirstOrDefaultAsync()` ثم `ToSummaryDto(row)` — بلا `COUNT` إضافي.
- `CreateAsync` يستخدمها بدل `GetByIdWithRecipientsAsync` + `ToSummaryDto(HeadAlert,...)`.
- إن أعادت `null` داخل المعاملة → `InvalidOperationException` صريح (تراجع كامل).
- حذف `ToSummaryDto(HeadAlert, ...)` نهائيًا.

**القبول:** `rg "ToSummaryDto"` = تعريف واحد، و`rg "DocumentTitle"` = مساعد واحد + غلاف مؤقت.

#### 1د — الشيفرة الميتة

- تأكيد `rg` ثم حذف:
  - `ListByDelegationWithRecipientsAsync` (`:387`).
  - `ListByDelegationAsync` في المستودع (`:358`).
  - `ListRecipientsAsync` (`:519`).
- تصحيح `IHeadAlertRepository.cs:59-62` و`:96-97` و`:110-111`.

**اختبارات المرحلة:** `HeadAlertTitleTests` (7) + اختبارا تمييز العنوان (مستودع + تكامل) + بقاء 2092/705 بلا تعديل ⇒ الإجمالي 2100 تطبيق + 706 API غير تتابعية.
**الحجم:** متوسط.

---

### المرحلة 2 — إسقاط التفاصيل وتعليم القراءة (`P3-11`)

**الهدف:** استعلام واحد للتفاصيل، وصف واحد لتعليم القراءة، بلا تحميل مستلمين.

**التغييرات:**

1. إضافة `HeadAlertDetailRow` في `Application/Common/` يحمل:
   - حقول العرض كلها + `CreatedById` + `RecipientCount` + `UnreadCount` + `bool? IsRead`.
2. `GetDetailRowAsync(int alertId, int actorUserId, CancellationToken)`:
   - `AsNoTracking()`.
   - `Select` واحد يشمل عدّادات المستلمين وحالة قراءة الفاعل.
3. `GetRecipientAsync(int alertId, int userId, CancellationToken)`:
   - صف `HeadAlertRecipient` متتبَّع (للتعليم فقط).
   - **مُستبدل في المرحلة 3 (مراجعة `ح‑10`):** حُذف لصالح `TryMarkReadAsync` الذرّي — لا كيان متتبَّع ولا معاملة (انظر المرحلة 3، البند 9).
4. `GetDetailsAsync`:
   - يجلب الصف، يفحص الصلاحية من `CreatedById`، ثم `ToDetailDto(row)`.
5. `MarkReadAsync`:
   - `GetRecipientAsync` → إن `null` فـ`false`؛ إن `IsRead` فـ`true`؛ وإلا تعديل + `SaveChangesAsync` **داخل `_tx.RunAsync`** (اصطلاح كل كتابات الخدمة، `:375`).
   - **النسخة النهائية (المرحلة 3):** `TryMarkReadAsync` بتحديث شرطي واحد + فحص وجود عند صفر صفوف.
6. حذف `GetByIdWithRecipientsAsync` و`DocumentTitle(HeadAlert)` بعد `rg`.
7. تعديل `ToDetailDto` ليستقبل `bool? isRead, int recipientCount, int unreadCount` بدل الكيان.

**اختبارات:**
- تفاصيل منشئ مع 3 مستلمين: `IsRead = null`، العدّادات صحيحة.
- تعليم القراءة يعدّل صف المستخدم فقط (`ReadAt` للآخرين `null`).
- مستلم واحد غير الفاعل: `IsRead = null`.
- `CanViewRecipients` للمنشئ/الإشراف فقط.

**اختياري موصى به:** `DbCommandInterceptor` يثبت ≤ استعلامين لمسار التفاصيل و≤ استعلامين لـ`MarkRead`.
**الحجم:** متوسط.

---

### المرحلة 3 — توحيد الصلاحيات في الخدمة (`P3-1` موسّعًا، `D8`)

> **أوسع مرحلة أثرًا. تُنفَّذ وحدها بلا تغييرات أخرى.**

**المبدأ:** `HeadAlertService` هو مصدر الحقيقة للدور/الفرع/الطرف.

**الترتيب المعدّل بعد المراجعة النقدية للمرحلتين 1–2 (2026-10-30):** بندان يُنفَّذان **قبل** البنود المرقّمة، لأن المراجعة أثبتت أنهما أصل العيب لا زينته:

1. **`ح‑1` (إلزامي أولًا):** `ListSummariesByDelegationAsync` لا يقيّد النتيجة بالمستخدم إطلاقًا (`HeadAlertRepository.cs:107-122`)، فالحارس الوحيد هو `AlertsController.cs:172-176` — أي أن عقد `IHeadAlertService.ListByDelegationAsync` غير آمن بذاته، وأي متصل جديد بلا حرس خارجي يتجاوز الفحص. لذلك يُنقل فحص الطرفية وفحص دور `Create`/`MarkRead`/`CountUnread` إلى الخدمة، وتُحذف من المتحكم.
2. **`ح‑4` (ثانيًا):** نقل `RolePermissions` إلى `DocGenerator.Application.Common` ليكون كتالوجًا واحدًا يستهلكه `HeadAlertActor` والمتحكمات معًا؛ الوضع الحالي مصفوفتان (`Api` والخدمة) تنحرفان عند أي قرار جديد.

**التغييرات:**

1. `CreateAsync(CreateHeadAlertRequest request, HeadAlertActor actor, string? actorName, CancellationToken)`:
   - `if (!RolePermissions.CanCreateAlerts(actor.Role)) throw new UnauthorizedAccessException(...)` (تعادل `IsHeadOrSubHead`).
   - `var branchId = RequireBranchForWrite(actor)`: `BranchId is null` → `ArgumentException("رئيس القسم دون فرع لا يمكنه إصدار تنبيهات")` — والمتحكم يحوّل `ArgumentException` إلى `400` أصلًا (`AlertsController.cs:133-136`)، فالعقد الحالي محفوظ بلا تكرار.
   - بقية المنطق كما هو.
   - **مصدر واحد (`D11`):** يُحذف فحص الفرع من `Create` في المتحكم؛ الحالة نفسها تُنتج `400` من الخدمة، و`403` في أسطح القراءة/العدّ عبر `RequireBranch(actor)` → `UnauthorizedAccessException` (`D7`).
2. `MarkReadAsync(int alertId, HeadAlertActor actor, CancellationToken)`:
   - `if (!RolePermissions.CanReceiveAlerts(actor.Role)) throw ...` (تعادل `!IsLawyer && !IsHeadOrSubHead`).
   - ثم فحص صف الاستلام للمستخدم.
3. `ListByDelegationAsync`:
   - `if (!RolePermissions.CanReadDelegationAlerts(actor.Role)) throw ...` (تعادل `!IsLawyer`).
   - `var delegation = await _delegations.GetByIdWithDetailsAsync(...)`؛ إن `null` أو `!DelegationParties.IsParty(delegation, actor.UserId)` → `UnauthorizedAccessException`.
   - حقن `IDelegationRepository` في `HeadAlertService` (لا دورة اعتماد).
4. `DelegationParties.IsParty(DocumentDelegation, int)` مساعد نقي في `Application/Common/` يعيد استخدام منطق `IsPartyAsync` نفسه (منشئ المصدر/الهدف، ثم الأطراف)، ويُفحص به الكيان المجلوب في الخطوة 3 — **بلا استعلام إضافي ولا استدعاء خدمة أخرى**.
5. حذف `IsPartyAsync` من `IDocumentDelegationService` + تنفيذه + `DelegationPendingCountBranchGuardTests.cs:30`.
6. حذف من `AlertsController`:
   - `IDocumentDelegationService` من الباني.
   - `CanCreateAlerts` (يُستبدل بفحص الدور في الخدمة).
   - فحوص `IsLawyer`/`IsHeadOrSubHead`/`IsManager` المكررة في `List`/`Sent`/`ByDelegation`/`MarkRead`/`UnreadCount`.
   - **مثبت بالكود:** `ListForActorAsync` يرفض `unreadOnly` للمدير/المشرف بـ`ArgumentException` (`:164-168`)، و`RequireBranch` يرمي `UnauthorizedAccessException` (`:1048`) ⇒ حذف الفحوص المكررة لا يغيّر أي استجابة.
   - **يبقى:** `ParseArchiveFilter`، `BuildPageRequest`، وتحويل الاستثناءات (`ArgumentException` → `400`، `UnauthorizedAccessException` → `403`). لا فحص دور/فرع في المتحكم إطلاقًا.
7. تحديث **22 موضع** `new HeadAlertService(...)` في الاختبارات (ميكانيكي).
8. تحديث التعليقات المتقادمة في `HeadAlertService.cs:232-233` و`IHeadAlertService` XML.
9. **`ح‑2` ثم `ح‑10` (إغلاق نافذة القراءة/الكتابة):** لا تُقرأ صفوف الاستلام ككيانات متتبَّعة أصلًا؛ `MarkReadAsync` تستدعي `IHeadAlertRepository.TryMarkReadAsync` الذي ينفّذ `UPDATE … WHERE IsRead = false` ذرّيًا ثم يفحص الوجود عند صفر صفوف. يُغلق نافذة `TOCTOU` بلا معاملة في الخدمة، ويُبقي إعادة التعليم idempotent (لا يمسّ `ReadAt` المحفوظ)، ويُزيل الاعتماد على كيان متتبَّع كان يُبطل ضمان إعادة المحاولة عند تفعيل `EnableRetryOnFailure`.
10. **`CountUnreadAsync`:** التوقيع يصبح `CountUnreadAsync(HeadAlertActor actor, CancellationToken)` في هذه المرحلة لجانب الصلاحية، وتضيف المرحلة 4 `scope` فقط. البديل (تأجيل الصلاحية إلى المرحلة 4) كان يترك السطح بلا أي حرس بعد حذف فحص المتحكم، ويحوّل `403` المدير/المشرف إلى `200` خلافًا للاختبار القائم `UnreadCount_ManagerAdmin_Forbidden_HeadAllowed`.
11. **قواعد مركزية جديدة في الكتالوج:** `CanReceiveAlerts` (محامٍ/رئيس/شعبة)، `CanReadAllAlerts` (مدير/مشرف)، `CanReadDelegationAlerts` (محامٍ) — ويستهلكها `HeadAlertActor` بدل تكرار الشروط (`HeadAlertService.cs:15-17`).

**اختبارات:**
- خدمة: `CreateAsync` بمحامٍ/مدير → `UnauthorizedAccessException`.
- خدمة: `MarkReadAsync` بمدير → استثناء.
- خدمة: `ListByDelegationAsync` لغير طرف/إنابة غير موجودة → استثناء.
- متحكم: 403 لكل ما سبق؛ `Create` بلا فرع → 400 (من الخدمة)؛ `unread-count` لرئيس بلا فرع → 403 (`D11`، تغيير موثّق من 200 إلى 403 لحساب في حالة غير صالحة).
- إعادة كتابة مصفوفة الصلاحيات (نظرية خماسية `ByDelegation` → نظرية رباعية للدور + اختبار طرفية منفصل) وإضافة 10 حالات: إنشاء/تعليم/عدّاد لمدير ومشرف، رئيس بلا فرع للكتابة والعدّ، وعدم-المستلم بصف استلام. المحصلة `+16` اختبارًا في `Application` (2102 → 2118).

**مراجعة ما بعد التنفيذ (2026-10-30) — `ح‑10`…`ح‑18`:**

| # | الملاحظة | الحالة |
|---|---|---|
| `ح‑10` | نقل قراءة `MarkReadAsync` إلى داخل `_tx.RunAsync` يكسر ضمان إعادة المحاولة (كيان متتبَّع يختصر الحفظ) | **أُغلقت**: `TryMarkReadAsync` تحديث ذرّي بلا كيان متتبَّع ولا معاملة |
| `ح‑11` | «دفاع القاعدة» في `CreateAsync` لم يعد يفحص الدور (اكتفى بدور التوكن) | **أُغلقت**: `CanCreateAlerts(user.Role)` في شرط القاعدة + اختبار تعارض الدور |
| `ح‑12` | رسالة `unreadOnly` صارت تحمل اسم معامل إنجليزيًا | **أُغلقت**: إسقاط `nameof(query)` + اختبار يثبّت النص كاملًا |
| `ح‑13` | التحقق من الترقيم سبق التفويض في `List`/`Sent`/`ByDelegation` ⇒ `403` صارت `400` | **أُغلقت**: الترتيب دور/فرع ← شكل ← طرفية في الأسطح الثلاثة، وإزالة `Validate` المكرر من المتحكم + اختبارات تركيبات |
| `ح‑14` | قاعدة «مدير/مشرف» معرّفة 3 مرات، و`User.HasFullAccess` ميت | **أُغلقت**: `CanReadAllAlerts => HasFullAccess`، `CanReadDelegationAlerts/CanManageDelegations => IsLawyer`، وحذف `User.HasFullAccess` |
| `ح‑15` | عدّاد الرئيس لا يقيّد بالفرع (قائمة مقيّدة) | **أُغلقت في المرحلة 4**: `CountUnreadForUserAsync(userId, branchId)` + «العدّاد = `TotalCount` لقائمة `unreadOnly=true`» (الصياغة الدقيقة في `ح‑19`)، و`received` يعيد مجموع قائمته بلا سقف صفحة |
| `ح‑16` | `MarkRead` لرئيس بلا فرع ينجح بينما `D11` يعتبره غير صالح | **أُغلقت توثيقيًا**: استثناء صريح في `D11` + تعليق على الدالة |
| `ح‑17` | اختبار `D11` التكاملي يُثبت حارس التوكن لا «دفاع القاعدة» | **أُغلقت توثيقيًا**: وصف الاختبار يصرّح بالحد |
| `ح‑18` | عدد الاختبارات في §5 («19») لا يطابق الواقع | **أُغلقت**: الاستبدال أعلاه بالتفصيل الدقيق |

**مراجعة ما بعد تنفيذ المرحلة 4 (2026-10-31) — `ح‑19`…`ح‑26`:**

| # | الملاحظة | الحالة |
|---|---|---|
| `ح‑19` | اختبار «العدّاد = طول القائمة» يثبت النتيجة مصادفةً حين تكون العيّنة كلها غير مقروءة، ولا يصمد أمام الترقيم | **أُغلقت**: الثبات صار «العدّاد = `TotalCount` لقائمة `unreadOnly=true`» (وحدات + تكامل)، وأُضيف اختبار «صف مقروء: القائمة تبقى والعدّاد ينخفض» |
| `ح‑20` | تغيير سلوك الافتراضي للرئيس (كل الفروع ← فرعه) غير مسجّل كتغيير عقد صريح | **أُغلقت**: بند صريح في `D7` + تعليق واختبار تكاملي يثبت استبعاد فرع آخر بالافتراضي |
| `ح‑21` | لا اختبار لـ`SubHead` في `received`، ولا لقراءة الرئيس صفّه (تخفض `user` وحده) | **أُغلقت**: `CountUnread_ReceivedScope_IsAvailableToSubHead_InHisBranch` + `CountUnread_HeadMarkingOwnRowRead_DecrementsUserOnly` + مسار HTTP مماثل |
| `ح‑22` | قيمة `enum` غير معروفة تسقط ضمنيًا إلى `User` (خطأ صامت) | **أُغلقت**: `switch` مع `_ => throw new ArgumentOutOfRangeException` (400) + اختبار `(AlertUnreadScope)42` |
| `ح‑23` | `Received` يفحص الدور مباشرة بينما `User` يفحص الكتالوج ⇒ انحراف محتمل | **أُغلقت**: `RolePermissions.CanUseReceivedScope` + ثابت `received ⊆ user` في اختبار المصفوفة |
| `ح‑24` | توثيق XML ناقص: `403` المحامي في `received`، ودلالة «القائمة القديمة» | **أُغلقت**: تحديث `IHeadAlertService.CountUnreadAsync` و`AlertUnreadScope` |
| `ح‑25` | عدّ اختبارات §5 وأرقام أسطرها قديمة (تكرار `ح‑18`) | **أُغلقت**: العدّ صار 20 حالة، وحُذفت أرقام الأسطر من الخطة |
| `ح‑26` | `received` سطح مؤقت بلا مستهلك بعد `PR-6` | **أُغلقت توثيقيًا**: وُسم صراحةً كسطح مؤقت في `D6`/`AlertUnreadScope` مع بند إزالة محدد |

**القبول:** `rg "IsPartyAsync" backend/src` = صفر؛ `rg "RolePermissions|IsLawyer|IsManagerOrAdmin|IsHeadOrSubHead" AlertsController.cs` = صفر (يبقى فقط بناء `Actor` من `User.GetRoleEnum()` لتمرير الدور إلى الخدمة)؛ `rg "Role (==|is) UserRole" HeadAlertService.cs` لا يطابق إلا تحقّق المستلمين (`ResolveRecipientsAsync` وما شابهه: تحقق بيانات لا صلاحيات) — `HeadAlertActor` بلا أي مقارنة دور مباشرة.
**الحجم:** كبير.

---

### المرحلة 4 — العدّادات والنطاق (`P3-4` + `P2-3`، `D6`/`D7`)

**التغييرات:**

1. `enum AlertUnreadScope { User, Received }` في `Application/Common/`.
2. `IHeadAlertService.CountUnreadAsync(HeadAlertActor actor, AlertUnreadScope scope, CancellationToken)`:
   - `User`: محامٍ → `branchId = null`؛ رئيس/شعبة → `RequireBranch` (403 عند غياب الفرع، `D11`)؛ مدير/مشرف → استثناء.
   - `Received`: رئيس/شعبة فقط + فرع إلزامي.
3. المستودع:
   - `CountUnreadForUserAsync(int userId, int? branchId, CancellationToken)`.
   - `CountUnreadForReceivedAsync(int userId, int branchId, CancellationToken)` — استعلام `COUNT` على صفوف الاستلام بشرط `EXISTS` للمستلم.
   - **تغطية الفهارس (مُتحقَّق منها في `DocGeneratorDbContext`):** `HeadAlerts` يملك `(BranchId, IsArchived, LastContentChangedAt, Id)`، و`HeadAlertRecipients` يملك `(UserId, IsRead)` و`(HeadAlertId, UserId)` ⇒ الاستعلامان يُخدمان بفهارس قائمة، **لا هجرة ولا فهرس جديد**.
4. المتحكم: `[FromQuery] string? scope` مع تحليل صريح:
   - `null/""/"user"` → `User`.
   - `"received"` → `Received`.
   - غير ذلك → `400`.
5. حذف `CountUnreadAsync(int userId, ...)` القديم من الواجهة/الخدمة/المستودع، وتحديث مستدعيه (المتحكم واختبارات الخدمة).
6. تحديث `AccountPage` لاحقًا في `PR-5` إلى `scope=received` وإعادة تسمية الشارة، مع تمرير `pageSize` ومعالجة `totalCount` في القائمة المعروضة (§4.2 البند 16) — العدّاد تجميعي غير مرقّم، وقائمة العرض مرقّمة، فلا تُقارَن الشارة بطول صفحة.

**اختبارات (20 حالة: 15 وحدة + 5 تكامل):**
- رئيس: تنبيه مستلم فيه 2 غير مقروء + تنبيه غير مستلم فيه 5 غير مقروء → النتيجة 2.
- رئيس: تنبيه مؤرشف لا يُحسب.
- رئيس: تنبيه في فرع آخر لا يُحسب (الافتراضي و`user`)، وهو تغيير العقد المقصود (`ح‑20`).
- رئيس بلا فرع: `scope=user` و`scope=received` → `403` (`D11`).
- محامٍ: عدّه الشخصي فقط، بلا قيد فرع.
- مدير/مشرف: `403` في النطاقين.
- `scope=received` لمحامٍ: `403`.
- `scope=xxx` (و`branch` و`1`): `400` برسالة عربية مثبّتة.
- `scope` بقيمة `enum` غير معروفة: `ArgumentOutOfRangeException` ⇒ `400` (`ح‑22`).
- **`ح‑15`/`ح‑19`:** «العدّاد = `TotalCount` لقائمة `unreadOnly=true`» لرئيس مستلم في فرعين (وحدات + تكامل)، وليس طول الصفحة.
- **`ح‑19`:** صف مقروء: القائمة تبقى بطولها والعدّاد ينخفض.
- **`ح‑21`:** رئيس/شعبة في `received`، وقراءة الرئيس صفّه تخفض `user` ولا تُسقط `received` إلا بمقدار صفّه (وحدات + HTTP).
- **`ح‑23`:** ثابت `received ⊆ user` في مصفوفة الأدوار.

**الحجم:** متوسط.

---

### المرحلة 5 — تحقق المستلمين والترقيم (`P3-5`، `P3-6`) ✅

**التغييرات:**

1. `ResolveRecipientsAsync`:
   - هدف `lawyer`: `lawyer is null || Role != Lawyer || BranchId != branchId || !IsActive` → «المحامي غير موجود ضمن فرعك».
   - هدف `document`: `CreatedBy is null || Role != Lawyer || !IsActive` → «لا يوجد محامٍ مختص نشط لهذا الملف».
   - `doc.BranchId != branchId` → «الملف غير متاح» (نفس رسالة غير الموجود).
2. `AlertPageRequest.Validate`:
   - **بلا إعادة ترتيب** للفحوص القائمة (ترتيبها ليس عقدًا ولا يخدم الفيضان).
   - أضف في النهاية: `if ((long)(Page - 1) * PageSize > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(Page), ...)`.
   - `Skip => (int)((long)(Page - 1) * PageSize)`.
3. `rg` على نص الرسالة القديم «الملف ليس ضمن فرعك» في الواجهة والاختبارات وتحديثه.

**اختبارات:**
- محامٍ معطّل → `400`.
- منشئ ملف معطّل → `400`.
- ملف فرع آخر → `400` بنفس نص «غير موجود».
- `page=2147483647&pageSize=100` → `400` (فيضان `Skip`).
- `page=2147483647&pageSize=1` → `200` بعناصر فارغة: `Skip = 2,147,483,646 ≤ int.MaxValue`؛ السقف يرفض **الفيضان** لا كِبَر رقم الصفحة، فلا يُغيّر السلوك القائم لصفحة بعيدة.

**الحجم:** صغير.

---

### المرحلة 6 — تحصين الاختبارات (`P3-3`، `P3-8`، `P3-9`، `P3-10`)

| الفجوة | الاختبار | الملف |
|---|---|---|
| تعيين الحقول المتميزة | `Detail_MapsDistinctLinkIds_InPositionalOrder` بقيم `11/22/33/44/55` | `HeadAlertServiceTests` |
| عدّادات التفاصيل للمحامي | `Detail_RetainsCounters_ForLawyerRecipient_UntilPR6` | `AlertsIntegrationTests` |
| رئيس نفس الفرع غير مستلم | `Get_SameBranchNonRecipientHead_NotFound` (بذور `CreateSystemAsync` بمنشئ مختلف) | `AlertsIntegrationTests` |
| `forbidden` غير مقروء | قراءة الكتلة من البيان + nullability مختصرة | `HeadAlertContractTests` |
| `StatsScope` صفحة واحدة | `AllAlertIdsAsync` يتكرر حتى `TotalPages` + فحص عدم التكرار | `StatsScopeTests` |
| لا تنفيذ `Postgres` | `HeadAlertSummaryPostgresTests` مبوّب بـ`DOCGEN_TEST_POSTGRES` | `DocGenerator.Api.Tests` |

**تفاصيل اختبار `Postgres`:**
- قاعدة معزولة (نمط `HeadAlertContractPostgresTests`).
- `MigrateAsync()` إلى الأحدث.
- بذور: إيموجي 600، مؤرشف، غير مقروء، تنبيهان بترتيب `LastContentChangedAt` متعاكس مع `Id`.
- `new HeadAlertRepository(postgresDb)` ثم مقارنة النتائج بـ`SQLite`.
- يعمل فعليًا في مهمة `backend-postgres` بالـCI (`.github/workflows/ci.yml:134`).
- **حرس ضد النجاح بالصمت:** محليًا يُتخطّى عند غياب `DOCGEN_TEST_POSTGRES`، وفي CI يُضاف `DOCGEN_REQUIRE_POSTGRES=1`؛ عندها غياب السلسلة أو تعذّر الاتصال = **فشل صريح** لا `Skip`. يُثبت التشغيل الفعلي بفحص عدد الاختبارات المنفَّذة في سجل المهمة (> 0)، لا بالاعتماد على خضرة الخطوة.

**قاعدة اختبار «الرئيس غير المستلم»:** لا يُنشأ التنبيه عبر `POST /api/alerts` (لأن المنشئ سيكون الرئيس نفسه)؛ يُستخدم `HeadAlertTestSeed.SeedSingleHeadAsync` مع `createdById` مختلف.

**اختياري:** `DbCommandInterceptor` لعدّ الاستعلامات (≤2 للتفاصيل، ≤2 لـ`MarkRead`).
**الحجم:** متوسط.

---

### المرحلة 7 — التوثيق والقرارات (`P3-7`، `P3-12`) ✅

1. `docs/HEAD_ALERTS_RESTRUCTURE_PLAN.md`:
   - §9.1: `PATCH` → `204`، إضافة `scope=user|received`، توضيح افتراضي `by-delegation=100`.
   - §9.3: إحالة `GET /{id}` إلى `D2` بدل شرط الفرع (ومثله §12).
   - §8.1/§8.2/§9: أسماء `ListForActorAsync/ListSentAsync` وسطح المستودع المنفَّذ.
   - **§9.4 جديد** لقاعدة الترتيب + خطأ عدّ الحقول، و**§9.5 جديد «قرارات PR-4»** مؤرَّخ: `D1–D12`.
   - توثيق `RecipientCount/UnreadCount` كاحتفاظ مؤقت حتى `PR-6`.
2. `RUN_GUIDE.md` §9: قسم «اقتران نشر `PR-4` + `PR-5`» وترتيبه حسب `D10` + وحدة التراجع + فحص دخاني.
3. وصف الـPR: الانحرافات، وحدة التراجع، **لا هجرات**، وأرقام البوابات الفعلية.
4. بند `PR-6` صريح (منقول إلى `HEAD_ALERTS_RESTRUCTURE_PLAN.md` §15 المرحلة 6):
   - [x] إزالة `RecipientCount`/`UnreadCount` من `HeadAlertDto` + تحديث البيان واختبار العقد.
   - [x] إزالة `normalizer` إن وُجد + اختبار يثبت رفض الشكل القديم.
   - [x] حسم نطاق شارة الرئيس (`received` أم `user`) وحذف النطاق الآخر مع اختبار رفضه.

**الحجم:** صغير.

---

### المرحلة 8 — الواجهة `PR-5` (`P1-1`، `P2-3`، `D10`، `D12`)

**ملفات الإنتاج:**
- `frontend/src/types/management.ts`: إضافة `HeadAlertSummaryDto` **مطابقًا لمفاتيح `headAlertSummary` في البيان**؛ توسيع `HeadAlertDto` بالحقول السلكية الناقصة (`kind`, `lastContentChangedAt`, `isArchived`, `archivedAt`, `publicEntityId`, `executionCircuitId`, `canViewRecipients`) بحيث يكون مرآةً لعقد التفاصيل. **لا تُضف `PagedResult<T>`**: موجود في `types/common.ts:28` ويُستورد منه.
- `frontend/src/lib/alertsNormalize.ts` (إن اختير `normalizer`): يفكّ الغلاف ويحوّل `message → messagePreview` و`previewTruncated` و`messageLength`.
- `frontend/src/pages/Dashboard.tsx`: `PagedResult<HeadAlertSummaryDto>` للاستدعاءين، **`params: { pageSize: 100 }`**، فكّ `r.data.items` بحارس مصفوفة، ومعالجة `totalCount > items.length` (مؤشر «المزيد» أو ترقيم — لا قصّ صامت)، وأنواع `markAlertRead`/`submitAlert`.
- `frontend/src/components/dashboard/AlertsPanel.tsx`: `HeadAlertSummaryDto[]` + `onMarkRead`.
- `frontend/src/components/dashboard/AlertRow.tsx`: `messagePreview` + `previewTruncated` بدل `message`، وتوجيه تنبيه الدوائر بـ`kind === "circuit_pending_documents"` بدل بادئة النص (`:34`).
- `frontend/src/components/dashboard/AlertDetails.tsx` (جديد، `D12`): يفكّ خيار «عرض النص الكامل» بجلب `GET /alerts/{id}` عند الطلب فقط، ويعرض `message` كاملًا؛ القائمة لا تجلبه.
- `frontend/src/pages/AccountPage.tsx`: الشارة من `unread-count?scope=received`، **وحذف `headAlertsQuery` كاملًا** (3 مواضع: البيانات، `summaryError`، مؤشر التحميل) مع `import` النوع، وإعادة تسمية النص.
- `frontend/src/components/delegation/DelegationActivityStrip.tsx`: فكّ `PagedResult` (أو `normalizer`) + `messagePreview`.

**ملفات الاختبار (كلها):**
- `Dashboard.test.tsx`، `AlertsPanel.test.tsx`، `AlertRow.test.tsx`، `AccountPage.test.tsx`، `DocumentView.test.tsx`، `AppealIntegration.test.tsx`.
- جديد: `AlertDetails.test.tsx`، `contract_sync_alerts.test.ts`.

**الاختبارات:**
- الشكل الجديد (`{items}`) والشكل القديم (`array`) — إن وُجد `normalizer`.
- عرض `messagePreview` و`previewTruncated`؛ وتوجيه الدوائر بـ`kind` وليس بالنص.
- `Dashboard` يطلب `pageSize: 100`، ويعرض مؤشر «المزيد» عند `totalCount > items.length`.
- `AccountPage`: استدعاء `unread-count?scope=received` للرئيس، ولا استدعاء لـ`/alerts` من الصفحة.
- `AlertDetails`: لا جلب قبل الفتح، وجلب `/{id}` عند الفتح، وعرض النص الكامل (فشل الاختبار إن عُرِضت المعاينة فقط).
- `contract_sync_alerts.test.ts`: `keyof HeadAlertSummaryDto` = مفاتيح البيان حرفيًا، و`keyof HeadAlertDto ⊇` عقد التفاصيل + المحتفظ، عبر `satisfies`/`expectTypeOf` (لا مقارنة كائنات وقت التشغيل).

**الفحص البصري:** 375px لـ`Dashboard`/`AccountPage`/شريط الإنابة (Playwright).
**الحجم:** كبير.

---

### المرحلة 9 — الإقفال

1. البوابات كاملة (انظر §11).
2. المراجعة الذاتية الإلزامية (AGENTS): تتبّع عقد كامل، `rg` على الأسماء المستبدلة، مطابقة الخطة بندًا بندًا.
3. الدمج: `PR-4` ← `PR-5`.
4. النشر: حسب `D10` (واجهة أولًا مع `normalizer`، أو متزامن).
5. دخان بعد النشر:
   - `GET /alerts` يعيد `items/totalCount`.
   - `POST /alerts` → `201` بجسم `Summary`.
   - شارة الرئيس تطابق `unread-count?scope=received`، وهي مجموع ما في تنبيهاته المستلمة بلا سقف صفحة (لا مجموع الصفحة المعروضة).
   - `GET /alerts/{id}` يعيد `message` كاملًا فقط.
6. لا هجرات ⇒ لا خطوات `dotnet ef`.

---

## 7. مصفوفة التتبّع

| الإشكال | المرحلة | اختبار الإثبات | الحالة |
|---|---|---|---|
| `P1-1` اقتران النشر | 8 + 5 | اختبارات الشكلين + فحص بصري | `[ ]` |
| `P2-1` مُسقِطان | 1 | `HeadAlertTitleTests` (7) + تمييز المحذوف/القائم الفارغ + 2100/706 | `[x]` |
| `P2-2` انحراف طبقي | 1 | `rg` صفر على `Application.Services` داخل المستودع | `[x]` |
| `P2-3` شارة الفرع | 4 + 8 | `UnreadCount_ReceivedScope_*` (خلفية ✅) + اختبار واجهة (`PR-5` متبقٍ) | `[ ]` |
| `P3-1` صلاحية الخدمة | 3 | `HeadAlertActor` هو مدخل القرار: `CreateAsync`/`MarkReadAsync`/`CountUnreadAsync`/`ListByDelegationAsync` تفحص الدور/الفرع/الطرف داخل `HeadAlertService`، والمتحكم يترجم HTTP فقط؛ `HeadAlertServicePermissionTests` + `DelegationPartiesTests` + بناء بلا تحذيرات | `[x]` |
| `P3-2` شيفرة ميتة | 1 + 2 | بناء بلا تحذيرات + `rg` | `[x]` |
| `P3-3` فجوات التغطية | 6 | `Detail_MapsDistinctLinkIds_InPositionalOrder` (11/22/33/44/55) + `Detail_RetainsCounters_ForLawyerRecipient_UntilPR6` + `Get_SameBranchNonRecipientHead_NotFound` | `[x]` |
| `P3-4` عدّاد غير مقيّد | 4 | `CountUnread_UserScope_EqualsUnreadOnlyListTotal_ForHead` + `CountUnread_ReceivedScope_*` + `CountUnread_HeadMarkingOwnRowRead_DecrementsUserOnly` + `Scopes_UserIsBranchBound_ReceivedCoversReceivedAlerts` (تكامل معزول) | `[x]` |
| `P3-5` تحقق المستلمين | 5 | `Create_InactiveDocumentCreator_Throws` + `Create_InactiveLawyerTarget_Throws` + `Create_MissingDocument_UsesSameUnavailableMessage` + `Create_OutsideBranchDocumentWithInactiveCreator_ReportsUnavailableOnly` (خدمة) و`Head_CreatesLawyerAlert_InactiveTarget_BadRequest` + `Head_CreatesDocumentAlert_InactiveCreator_BadRequest` + `Head_CreatesDocumentAlert_OutsideBranch_BadRequest` (HTTP، نص موحّد) | `[x]` |
| `P3-6` فيضان `Skip` | 5 | `InvalidPaging_RejectsHugePage` + `Validate_RejectsSkipOverflow_WithCleanArabicMessage` + `Validate_RejectsPageBelowOne_WithoutNarrowingArithmetic` + `Skip_ExtremePageWithSizeOne_IsIntMaxValueMinusOne` | `[x]` |
| `P3-7` انحراف التوثيق | 7 | مراجعة يدوية موثّقة — `§9.1` (`204`/`scope`/افتراضي `100`)، `§9.3`+`§12` (`D2`)، `§8.1`/`§8.2`/`§9.4` (الأسماء المنفَّذة)، `§9.4` (ترتيب القرار + عدّ الحقول 22/22)، `RUN_GUIDE` §9 | `[x]` |
| `P3-8` غياب `Postgres` | 6 | `HeadAlertSummaryPostgresTests` في CI (قاعدة معزولة + مقارنة حرفية بـ`SQLite`؛ `backend-postgres` أخضر وخطوة عدّ `trx` أثبتت التنفيذ) | `[x]` |
| `P3-9` بيان العقد | 6 | `SummaryContract_MatchesManifest_WithoutFullMessage` + `DetailContract_MatchesManifest_PlusExplicitlyRetainedFields` + `PagedResultContract_MatchesManifest_WithoutHasNextPage` (تقرأ كتلة `forbidden` من البيان) + `Nullability_MatchesSmallContractList` | `[x]` |
| `P3-10` هشاشة `StatsScope` | 6 | `Alerts_HeadAndSub_SeeOnlyOwn` عبر `AllAlertIdsAsync` (حلقة حتى `totalPages` + عدم تكرار) | `[x]` |
| `P3-11` تحميل المستلمين | 2 | إسقاط واحد (مراجعة كود) + تغطية سلوكية للتفاصيل وتعليم القراءة — **بلا عدّ استعلامات** (لا أداة عدّ في المستودع؛ تصحيح 2026-10-30) | `[x]` |
| `P3-12` تعرّض محتفظ | 7 | اختبار `UntilPR6` + بند `PR-6` صريح بثلاثة مطالب (`HEAD_ALERTS_RESTRUCTURE_PLAN` §15) + توثيق «محتفظ مؤقت» في `§9.2`/`§9.5` | `[x]` |
| `R-1` (`D11`) حالة «رئيس بلا فرع» | 3 | `BranchlessHead_CreateIs400_AndReadsAre403` (تكامل `Api`) + `Create_BranchlessHead_IsArgumentError_NotAuthorization`/`CountUnread_BranchlessHead_IsRejected_LawyerCountsOwnRows` (وحدة) | `[x]` |
| `R-2` فهارس عدّاد `received` | 4 | مراجعة مخطط موثّقة (لا هجرة) | `[ ]` |
| `R-3` سقف الترقيم | 5 | `Paging_OverflowRejected_ExtremePageWithSize1Allowed` (HTTP) + `Paging_ExtremePageWithSize1_IsAllowed` (خدمة) | `[x]` |
| `R-4` حرس `Postgres` | 6 | تنفيذ فعلي > 0 اختبار في وظيفة CI (`DOCGEN_REQUIRE_POSTGRES=1` + خطوة `trx` أثبتت العدّ) | `[x]` |
| `R-5` (`D12`) فقدان النص الكامل | 8 | `AlertDetails.test.tsx` (جلب عند الفتح فقط) | `[ ]` |
| `R-6` قصّ 50 صامت | 8 | اختبار `pageSize: 100` + مؤشر «المزيد» | `[ ]` |
| `R-7` ازدواج `PagedResult` | 8 | استيراد من `types/common.ts` فقط | `[ ]` |
| `R-8` توجيه ببادئة النص | 8 | `AlertRow` بـ`kind` | `[ ]` |

---

## 8. المخاطر والتراجع

| الخطر | الاحتمال | التخفيف |
|---|---|---|
| كسر `201` عند إعادة القراءة داخل المعاملة | منخفض | إبقاء اختبارات `:165-172` و`:375-378` دون تعديل |
| نقل `HeadAlertTextLimits` يكسر `using` | متوسط | كوميت واحد متماسك + بناء كامل |
| `scope=received` يغيّر رقم الشارة | منخفض | مطابق للسلوك القديم؛ اختبار «تنبيه غير مستلم مستبعد» |
| إعادة هيكلة `MarkRead` تفقد التتبع | متوسط | تحديث ذرّي `ExecuteUpdate` (`TryMarkReadAsync`) + اختبار `ReadAt` للآخرين وإعادة التعليم |
| تحديث 22 موضع `new HeadAlertService` | مؤكد | كوميت مستقل يقوده المُصرِّف |
| `Postgres` gated لا يعمل محليًا | مؤكد | يعمل في CI؛ محليًا يُتخطّى صراحةً |
| تراجع `PR-5` وحدها يكسر القائمة | متوسط | `normalizer` أو وحدة تراجع = الزوج |
| `normalizer` يصبح دَينًا دائمًا | متوسط | اختبار إزالة + بند `PR-6` + موعد |
| `P3-12` تعرّض عدّادات التفاصيل | منخفض | قرار `D5` + اختبار صريح + إزالة `PR-6` |

**التراجع:** كل مرحلة كوميت مستقل. المرحلة 3 معزولة. لا تغييرات مخطط ⇒ لا `rollback` قاعدة.

---

## 9. تعريف الإنجاز

- [ ] صفر `P1/P2` مفتوح، وكل `P3` مُصلَح أو موثّق بقرار مؤرَّخ.
- [ ] `rg` على `ToSummaryDto`/`DocumentTitle`/`IsPartyAsync`/`GetByIdWithRecipientsAsync`/`CountUnreadAsync(int` يعطي التعريف الوحيد المتوقع.
- [ ] `head-alert-contracts.json` مقروء فعليًا (أسماء + `forbidden` + nullability).
- [ ] لا `#pragma warning disable`، لا `catch` صامت، لا معامل اختياري جديد، لا `DateTime.Now`.
- [ ] البوابات كاملة خضراء على `PR-4` و`PR-5`.
- [ ] `vitest` يغطي الشكلين (إن وُجد `normalizer`) و`messagePreview`.
- [ ] وثيقة الخطة محدّثة، ووصف الـPR يذكر `D1–D12`، اقتران النشر، **لا هجرات**.
- [ ] `PR-6` يحمل ثلاثة بنود: إزالة العدّادين من التفاصيل + إزالة `normalizer` (إن وُجد) + حسم نطاق شارة الرئيس (`received` أم `user`).
- [ ] `PR-5` يثبت `D12`: القائمة لا تحمل النص الكامل، وفتح التنبيه يجلبه من `GET /alerts/{id}`.

---

## 10. خارج النطاق صراحةً

- لا تغييرات مخطط أو هجرات.
- لا نشر ولا تعديل `docs/audit/`.
- لا إعادة تصميم واجهة أو تغيير `PagedResult<T>`.
- لا `Feature Flag` جديد.
- لا تغيير في `CreateSystemAsync` أو مسارات النظام الأخرى.
- لا إصلاحات لعيوب سابقة خارج قائمة `P1/P2/P3` المعتمدة.

---

## 11. أوامر التحقق

```powershell
# من جذر المستودع
dotnet build backend/DocGenerator.sln
dotnet test backend/tests/DocGenerator.Application.Tests/DocGenerator.Application.Tests.csproj
dotnet test backend/tests/DocGenerator.Api.Tests/DocGenerator.Api.Tests.csproj --filter "Category!=Sequential"
dotnet test backend/tests/DocGenerator.Api.Tests/DocGenerator.Api.Tests.csproj --filter "Category=Sequential"

# مرشّح ضيق أثناء التنفيذ
dotnet test backend/tests/DocGenerator.Application.Tests/DocGenerator.Application.Tests.csproj --filter "FullyQualifiedName~HeadAlert"

# Postgres محليًا (اختياري)
$env:DOCGEN_TEST_POSTGRES="Host=localhost;Port=5432;Database=docgen_test;Username=postgres;Password=postgres"

# الواجهة
Set-Location frontend
npx oxlint src
npx tsc -b
npx vitest run
npm run build
```

---

## 12. سجل الوثيقة

| التاريخ | التغيير | السبب |
|---|---|---|
| 2026-10-28 | النسخة الأولى من خطة الإصلاح | بعد مراجعة `PR-4` |
| 2026-10-28 | نسخة مُنقّحة: `scope=received`، توحيد الصلاحيات، إسقاط تفاصيل واحد، `normalizer`، ترتيب المراحل | مراجعة نقدية للخطة نفسها |
| 2026-10-28 | `D11` + فهارس العدّاد + تثبيت سقف الترقيم + حرس `Postgres` | مراجعة ثانية تكاملية (تناقض `400/403`، خضرة كاذبة، قرار مؤجَّل) |
| 2026-10-29 | `D12` + منع ازدواج `PagedResult` + `pageSize=100` + توجيه `kind` + حذف `headAlertsQuery` + إبقاء `_tx` | جولة تحقق نهائية على المراسي الواقعية (أسطر/أنواع/مستهلكين) |
| 2026-10-29 | تنفيذ المرحلة 1: نقل `HeadAlertTextLimits` إلى `Common`، مساعد `HeadAlertTitle`، `GetSummaryAsync` بالإسقاط الواحد، وحذف المستودع الميت | البدء بالتنفيذ بعد خط الأساس `pr4-baseline` |
| 2026-10-29 | مراجعة نقدية للمرحلتين 0 و1: تثبيت حالات §3، فصل `documentExists` في عنوان الملف (محذوف ≠ قائم بلا حقول)، حذف `using` زائد من اختبار المستودع، وتصحيح عدّ الاختبارات في §7 | إغلاق قبول المرحلة 0 ومنع انحراف دلالي صامت في التفاصيل/`201` |
| 2026-10-29 | انحراف موثّق عن 1ب: `HeadAlertRepository.DocumentTitle` حُذف واستُدعي المساعد مباشرة في `ToSummaryDto` بدل غلاف تفويض | لا أثر سلوكي؛ مبدأ «مصدر عنوان واحد» محقَّق |
| 2026-10-30 | المرحلة 2: `HeadAlertDetailRow` + `GetDetailRowAsync` بإسقاط واحد، `GetRecipientAsync` متتبَّع، وحذف `GetByIdWithRecipientsAsync` و`DocumentTitle(HeadAlert)` | إزالة تحميل المستلمين من مساري التفاصيل وتعليم القراءة (`P3-11`/`P3-2`) |
| 2026-10-30 | مراجعة نقدية للمرحلتين 1–2 على الكود: تثبيت الملاحظات `ح‑1`…`ح‑9` في §5، تصحيح خلية إثبات `P3-11` (لا أداة عدّ استعلامات)، وتسجيل حقل `IsRecipient` وتوقيع `ToDetailDto(HeadAlertDetailRow)` كانحرافين موثّقين عن نص الخطة | منع «خضرة» توثيقية لا يقابلها دليل، وتحديد ترتيب المرحلة 3 على أساس العيب الفعلي |
| 2026-10-30 | توضيح دلالة `CanViewRecipients` في عقد التفاصيل: يحكم قائمة المستلمين لا `RecipientCount`/`UnreadCount` (محتفظ بهما مؤقتًا حتى `PR-6`) | واجهة `PR-5` كانت ستستنتج من `false` أن العدّادات غائبة، والواقع أنها تُرسَل دائمًا |
| 2026-10-30 | المرحلة 3: `HeadAlertActor` مصدر القرار (دور/فرع/طرف) في `CreateAsync`/`MarkReadAsync`/`ListByDelegationAsync`/`CountUnreadAsync`، `DelegationParties.IsParty` بديل `IDocumentDelegationService.IsPartyAsync` المحذوف، `RequireBranchForWrite` لـ`400` الكتابة، `IDelegationRepository` حقنًا إلزاميًا | تنفيذ `ح‑1`+`D8` قبل `ح‑4`: `ListSummariesByDelegationAsync` لا يقيّد بالمستخدم، فالعقد صار آمنًا بذاته لا بحرس المتحكم؛ و`R-1`/`D11` مغلقان باختبار تكامل صريح |
| 2026-10-30 | مراجعة نقدية لما بعد تنفيذ المرحلة 3: تسجيل `ح‑10`…`ح‑18` (كسر ضمان إعادة المحاولة في `MarkRead`، فقدان فحص دور القاعدة في `Create`، انحراف نص رسالة، انعكاس `403/400`، تكرار تعريف القاعدة) | منع «إغلاق شكلي»: المراجعة أثبتت ثلاث ملاحظات تمسّ عقدًا/ضمانًا مكتوبًا رغم خضرة الاختبارات |
| 2026-10-30 | إغلاق ملاحظات المراجعة: `TryMarkReadAsync` ذرّي، فحص دور القاعدة في `Create`، إسقاط `nameof(query)`، ترتيب «دور/فرع ← شكل ← طرفية» وإزالة `Validate` المكرر من المتحكم، توحيد مسندات الكتالوج وحذف `User.HasFullAccess` الميت | جعل الكود مطابقًا للعقد والضمان المكتوبين لا للاختبارات فقط؛ `ح‑15` تبقى للمرحلة 4 بالتصميم |
| 2026-10-30 | المرحلة 4: `AlertUnreadScope` + `scope` صريح في `GET /alerts/unread-count`، `CountUnreadForUserAsync` (فرع اختياري للرئيس) و`CountUnreadForReceivedAsync` (كل غير المقروء على تنبيهات استلمها الرئيس في فرعه)، وحذف `CountUnreadAsync(int userId)` | تنفيذ `D6`/`D7` وإغلاق `ح‑15`: العدّاد الشخصي صار مطابقًا للقائمة، والافتراضي بلا `scope` يبقى `user` فلا كسر للواجهة المنشورة؛ لا هجرة (الفهارس القائمة تخدم الاستعلامين) |
| 2026-10-31 | مراجعة نقدية للمرحلة 4 على الكود مع فحص SQL فعلي (`ToQueryString`): تثبيت `ح‑19`…`ح‑26` وإغلاقها — ثبات «العدّاد = `TotalCount` لقائمة غير المقروء»، توثيق تغيير عقد الافتراضي (`D7`/`ح‑20`)، تغطية `SubHead` وقراءة الرئيس، `switch` مغلق لقيمة `enum` غير معروفة، `CanUseReceivedScope` وثابت `received ⊆ user`، وتحديث التوثيق والعدّ | رفع جودة الإثبات إلى مستوى الكود: لا اختبار ينجح مصادفةً، ولا سطح مؤقت بلا بند إزالة (`D6`)، ولا قاعدة دور مكررة (`ح‑23`) |
| 2026-10-31 | إغلاق المرحلة 4 نهائيًا وتثبيت **مرساة التسليم §0.1** (الفرع و`HEAD`، حالة `PR #54` و`CI` 12/12، أرقام البوابات، أمر الاختبار التتابعي، قيد `EnableRetryOnFailure`، حالة الهجرات، البنود المفتوحة) | بدء المرحلة 5 في محادثة جديدة دون الاعتماد على ذاكرة الجلسة السابقة، ومنع تسرّب أرقام بوابات قديمة كما حدث في جسم `PR #54` (2126/712 مقابل الواقع 2130/719) |
| 2026-10-31 | **انحرافات المرحلة 5 قبل التنفيذ:** (1) `HEAD` الفعلي `0b3bcf2` لا `a9bcb7a` (تحديث §0.1)؛ (2) نص «الملف ليس ضمن فرعك» لا يظهر في `frontend/src` ولا في الاختبارات — موضع واحد فقط `HeadAlertService.cs:1024`، فالبند الثالث في §5 عمليًا خدمة فقط (الظهور الوحيد «ليست ضمن فرعك» في الواجهة يخص اعتماد الإنابة وخارج النطاق)؛ (3) توحيد رسالة الملف: «الملف غير متاح» للملف غير الموجود **وللملف خارج الفرع** معًا، مع إبقاء فحص الفرع قبل فحص المنشئ (سلوك قائم يمنع تسريب وجود المنشئ، والخطة لا تحدد ترتيب القاعدتين)؛ (4) الفيضان الحالي: `Skip` يُحسب في `int` غير محروس — `(int.MaxValue-1)*100` ينقلب `-200` فيرفضه حارس المستودع 400 عرضًا، و`(int.MaxValue-1)*3` يصبح `2,147,483,642` (موجب خاطئ) فيمرّ 200 بصفحة غير المطلوبة | تثبيت الدليل من الكود قبل الكتابة: لا تغيير واجهة أو عقد غير المنصوص، والحدّ الصريح للفيضان في `AlertPageRequest.Validate` يلغي الاعتماد على حارس المستودع |
| 2026-10-31 | المرحلة 5: `ResolveRecipientsAsync` يرفض المحامي/منشئ الملف المعطّل ويوحّد رسالة الملف، و`AlertPageRequest` يرفض فيضان `Skip` بحساب `long`؛ مع اختبارات خدمة + HTTP. البوابات: `build` 0/0 · `Application` 2145/2145 · `Api` 720 + 2 تتابعية · `oxlint`/`tsc` صفر · `vitest` 152/1599 · `npm run build` ناجح | تنفيذ `P3-5`/`P3-6` وإغلاق `R-3` وتحديث §7 |
| 2026-10-31 | **إصلاح ملاحظات المراجعة النقدية للمرحلة 5 (قبل المرحلة 6):** (ف‑1) `((long)Page - 1) * PageSize` بدل `(long)(Page - 1)` لتفادي التفاف الطرح في `int` (مكافئ للمدخلات الصحيحة، وأسلم لاستدعاء بلا `Validate`)؛ (ف‑2) استثناء صريح `AlertPagingException : ArgumentException` بدل `ArgumentOutOfRangeException(nameof(...))` — السبب أن `.NET` يُلحق «Parameter name: Page» الإنجليزي بـ`Message` فيظهر للمستخدم عبر `{ message }`، والبديل يُبقي `400` (لأن المتحكم يلتقط `ArgumentException`) ورسالة عربية نظيفة؛ (ف‑3) فصل اختبار HTTP المركّب إلى اختبارين؛ (ف‑4) اختبار يجمّد ترتيب «فرع ← منشئ» لملف فرع آخر بمنشئ معطّل؛ (ف‑8) فصل صف بوابات §0.1 إلى «عند التسليم» و«بعد المرحلة 5» | الالتزام بالمعايير: لا رسالة إنجليزية في عقد `400`، ولا أمان بالترتيب، واختبار واحد لكل سلوك، وتجميد قواعد عدم التسريب. **بندان مفتوحان بلا كود (قرار عمل خارج §5):** (ف‑6) منشئ ملف انتقل لفرع آخر بعد إنشائه — التنبيه يبقى في فرع الملف ويُستلم عند منشئ خارج الفرع؛ (ف‑7) مسار `CreateSystemAsync`/`DocumentCreator` لا يفحص فرع الملف (سلوك سابق مقصود جزئيًا) — يُحسمان في `PR‑6` أو وثيقة السياق |
| 2026-10-31 | **بوابات رقعة المراجعة (مُثبتة فعليًا):** `build` 0/0 · `Application` **2147/2147** · `Api` **721** + **2** تتابعية · `oxlint`/`tsc` صفر · `vitest` 152/1599 · `npm run build` ناجح | إثبات أن الإصلاحات (استثناء الترقيم + فصل الاختبارات + اختبار الترتيب) لم تخفض أي بوابة، وأن `400` صار برسالة عربية نظيفة بلا «Parameter name» |
| 2026-10-31 | **المرحلة 6 (تحصين الاختبارات):** (1) `Detail_MapsDistinctLinkIds_InPositionalOrder` بقيم `11/22/33/44/55` لتفكيك أي إزاحة موضعية في `ToDetailDto`؛ (2) `Detail_RetainsCounters_ForLawyerRecipient_UntilPR6` (HTTP: العدّادان يُرسَلان للمحامي قبل وبعد القراءة، مع `CanViewRecipients=false`)؛ (3) `Get_SameBranchNonRecipientHead_NotFound` عبر `HeadAlertTestSeed.SeedSingleHeadAsync` بمنشئ مختلف (لا `POST`)؛ (4) `HeadAlertContractTests` يقرأ كتلة `forbidden` من البيان بدل قائمة مكتوبة، ويضيف قائمة nullability صغيرة عبر `NullabilityInfoContext`؛ (5) `StatsScopeTests` يجلب كل الصفحات عبر `AllAlertIdsAsync` (حلقة حتى `totalPages`) ويثبت عدم التكرار؛ (6) `HeadAlertSummaryPostgresTests` جديد: قاعدة معزولة + `MigrateAsync` + بذور (إيموجي 600، مؤرشف، مقروء/غير مقروء، ترتيب `LastContentChangedAt` معاكس لـ`Id`) ومقارنة حرفية مع `SQLite`، وحرس `DOCGEN_REQUIRE_POSTGRES=1`، وخطوة `CI` تثبت تنفيذ > 0 اختبار. **انحرافات أثناء التنفيذ:** (أ) اختبار المراجع يستخدم صفوفًا مرجعية حقيقية بمعرّفات صريحة `11/22/44/55` و`DelegationId=33` (بلا FK) لأن SQLite في الاختبارات يفعّل المفاتيح الأجنبية؛ `PublicEntityId`/`ExecutionCircuitId` لم يُغطَّيا هنا (يحتاجان تجهيز مجموعة/دائرة، وقيمهما المميزة يغطيها اختبار المستودع)؛ (ب) «رئيس نفس الفرع» لا يمكن إنشاء رئيس ثانٍ مفعّل فيه (فهرس فريد `BranchId` حيث `Role=Head AND IsActive`)، فالفاعل هو رئيس الفرع والمنشئ محامٍ والمستلم شعبة؛ (ج) بذور صفحات `StatsScope` تحتاج أنواعًا **مختلفة** لأن مفتاح `DedupeKey` يدمج تكرار النوع نفسه فتنهار ثلاث بذور إلى واحدة | تنفيذ `P3-3`/`P3-9`/`P3-10` وبناء `P3-8`؛ البوابات المحلية: `build` 0/0 · `Application` 2149/2149 · `Api` 724 + 2 · `oxlint`/`tsc` صفر · `vitest` 152/1599 · `npm run build` ناجح. `Postgres` الحيّ لا خادم له محليًا ⇒ الإثبات النهائي في `CI` |
| 2026-10-31 | **إغلاق المرحلة 6:** `CI` **12/12** على `902598c` — `backend-postgres` pass في التشغيلين (7m19s/7m26s)، وخطوة `Assert Postgres summary projection test actually executed` **pass** (عدّ `trx` > 0)، فـ`P3-8`/`R-4` مغلقان بدليل تنفيذ فعلي لا بخضرة خطوة | إغلاق §7 (`P3-3`/`P3-8`/`P3-9`/`P3-10`/`R-4`) وتثبيت أرقام التسليم للمرحلة 7 |
| 2026-10-31 | **إصلاح ملاحظات المراجعة النقدية للمرحلة 6:** (م‑1) تعطيل احتفاظ المنتدى في `ApiFactory` (`Forum:RetentionMonths=0`) وإضافة `Default Timeout=30` لسلسلة SQLite — بعد فشل متقلب `SQLite database is locked` في `ForbiddenShapeContractTests.InitializeAsync` على التزام توثيقي (نفس الكود نجح في تشغيل موازٍ)؛ (م‑2) تثبيت عقد المعاينة في اختبار `Postgres` (طول 280 نقطة + علامة `…` + غير فارغة)؛ (م‑3) مقارنة `TotalCount` للأرشيف ومنظور المستلم؛ (م‑4) سقف صريح لدورات `AllAlertIdsAsync`؛ (م‑5) قراءة `trx` من تشغيل الـAPI الأساسي بدل إعادة تنفيذ اختبار Postgres؛ (م‑6) نقل حقول `createHeadAlertRequest` الممنوعة إلى كتلة `forbidden` في البيان؛ (م‑7) استخدام `PostgresTestEnvironment.RequireOrSkip` في `DatabaseInitializerTests` (لا تخطّي صامت)؛ (م‑8) تغطية `PublicEntityId`/`ExecutionCircuitId` في اختبار المعرّفات، وإضافة تغطية `GetSummaryAsync`/`Sent`/`ByDelegation` في تكافؤ `Postgres` | إغلاق كل ملاحظات المراجعة بلا تغيير سلوك إنتاجي: التعديلات اختبارات + `ci.yml` + البيان فقط، ومعالجة تقلّب `CI` بجذر بنية الاختبار |
| 2026-10-31 | البوابات بعد رقعة المراجعة: `build` 0/0 · `Application` 2149/2149 · `Api` 724 + 2 تتابعية · `oxlint`/`tsc` صفر · `vitest` 152/1599 · `npm run build` ناجح؛ `CI` **12/12** على `f01c74d` (تشغيلان) وخطوة `trx` الجديدة: `Passed HeadAlertSummaryPostgresTests: 1` — والتقلّب السابق لم يتكرر | إثبات أن إصلاحات المتانة لم تخفض أي بوابة وأغلقت تقلّب SQLite |
| 2026-10-31 | **انحرافات المرحلة 7 قبل التنفيذ:** (1) لا يوجد `§9.4` في خطة إعادة الهيكلة (الترقيم يقفز من `§9.3` إلى `§10`)، فأُنشئ **`§9.4`** لقاعدة الترتيب وخطأ عدّ الحقول، و**`§9.5`** لبطاقة `D1–D12` كما ينص §6 — بلا فجوة ترقيم؛ (2) **خطأ عدّ الحقول المثبت:** عقد التفاصيل الفعلي `HeadAlertDto` = **22** حقلًا (20 معلنًا في `§9.2` + `RecipientCount`/`UnreadCount` المحتفظ بهما)، والبيان يفصل `headAlertDetail` (20) عن `retainedHeadAlertDetail` (2)، والملخص = 22 أيضًا؛ نصّ `§9.2` («قائمة الحقول كاملة») كان يعدّ 20 فقط؛ (3) **توسيع تنظيف الأسماء الميتة** خارج `§8.1`: `§8.2` (أسماء المستودع)، `§14.1`، `§18`، `§12` — الأسماء المنفَّذة هي `ListForActorAsync`/`ListSentAsync`/`ListSummariesForRecipientAsync`/`ListSummariesSentAsync`/`ListSummariesForOversightAsync`/`ListSummariesByDelegationAsync`/`GetDetailRowAsync`، والمذكور سابقًا (`ListForLawyerAsync`/`ListForHeadAsync`/`ListForOversightAsync`/`ListByDelegationSummariesAsync`/`GetByIdForActorAsync`/`GetByIdWithRecipientsAsync`) غير موجود؛ (4) **تناقض `D2` في `§12`** (شرط فرع لغير المستلم) و**تناقض تسمية الشارة مع `D6` في `§10.7`/`§18`** («عدّاد شخصي/تنبيهاتك غير المقروءة») — صُحّحا نصًّا إلى قرار المالك: `received` لشارة الرئيس بنص «غير المقروء في تنبيهاتك»، و`user` هو الافتراضي للسطح؛ (5) `RUN_GUIDE.md`: قسم فرعي جديد داخل `§9` للاقتران، **وقائمة الهجرات التراكمية لم تُمسّ** (لا هجرات)؛ (6) وصف `PR #54` يُحدَّث في مكانه (مسوّدة غير مدموجة) بأرقام البوابات الفعلية `2149`/`724 + 2` | تسجيل الانحراف قبل التنفيذ كما يفرض §0.5، ومنع «توثيق يخالف الكود» بدل إنتاجه: كل تصحيح قابل للتحقق من كود `4e0d3ae` |
| 2026-10-31 | **تثبيت مرساة المرحلة 7 (§0.1):** حالة الكود `4e0d3ae` + التزام هذه المرساة · `CI` 12/12 على `f01c74d` و`4e0d3ae` · `backend-postgres` نفّذ اختبار الإسقاط (`Passed ...: 1`, `Skipped: 0`) · البوابات 2149/724+2 · نقل البنود المفتوحة `ف‑6`/`ف‑7`/`D10`/`D6` إلى المرساة · نطاق المرحلتين 7 و8 على فرع `PR-4` بلا فرع جديد | بدء المرحلة 7 في محادثة جديدة دون الاعتماد على ذاكرة الجلسة الطويلة، ومنع تكرار خطأ أرقام البوابات القديمة |
| 2026-10-31 | **المرحلة 7 منفَّذة (توثيقية):** `HEAD_ALERTS_RESTRUCTURE_PLAN.md` — `§9.1` (`PATCH`→`204`، `scope=user\|received`، افتراضي `by-delegation=100`، قاعدة الترتيب)، `§9.3`+`§12` (`D2` بلا شرط فرع)، `§8.1`/`§8.2` (سطح الخدمة/المستودع المنفَّذ: `ListForActorAsync`/`ListSentAsync`/`ListSummariesForRecipientAsync`/`ListSummariesSentAsync`/`ListSummariesForOversightAsync`/`ListSummariesByDelegationAsync`/`GetDetailRowAsync`)، **`§9.4`** (ترتيب القرار + عدّ الحقول 22/22 + تناقضات `§10.7`/`§14.2`/`§18`)، **`§9.5`** (`D1–D12` بأدلة الكود + احتفاظ مؤقت حتى `PR-6`)، بند `PR-6` الصريح في `§15`؛ `RUN_GUIDE.md` §9 — قسم اقتران النشر وترتيبه حسب `D10` ووحدة التراجع والفحص الدخاني؛ وصف `PR #54` — أرقام البوابات الفعلية والانحرافات ووحدة التراجع ولا هجرات. **البوابات بعد التغيير:** `build` 0/0 · `Application` **2149/2149** · `Api` **724** + **2** تتابعية · `oxlint`/`tsc -b` صفر · `vitest` 152/1599 · `npm run build` ناجح | إغلاق `P3-7`/`P3-12` (§7) وإزالة انحراف التوثيق قبل بدء `PR-5`: لا كود إنتاج ولا اختبارات ولا هجرات، والبنود المفتوحة (`ف‑6`/`ف‑7`/`D10`/`D6`) منقولة كما هي |
| 2026-10-31 | **إثبات `CI` للمرحلة 7:** `CI` **12/12** (ستة فحوص × تشغيلان: `backend`، `backend-postgres`، `frontend`، `guards`، `audit`، `migration-guard`) على التزام توثيق المرحلة 7 — و`backend-postgres` أخضر مع خطوة عدّ `trx`، فلا خضرة كاذبة | إثبات أن التغيير التوثيقي لم يكسر أي بوابة على الفرع، وأن `PR #54` يبقى `MERGEABLE` وغير مدموج (ينتظر `PR-5`) |

---

**نهاية الوثيقة.**
