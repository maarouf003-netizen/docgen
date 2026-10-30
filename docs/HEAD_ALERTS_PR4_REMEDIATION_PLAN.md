# خطة إصلاح `PR-4` — نسخة مُنقّحة بعد مراجعة نقدية

> **تاريخ الوثيقة:** 2026-10-28
> **الفرع:** `feature/head-alerts-pr4-api` · **الأساس:** `e0d8384`
> **الحالة:** جاهزة للتنفيذ في محادثة جديدة
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
| `D6` | `GET /alerts/unread-count?scope=received`: مجموع صفوف غير المقروءة **على التنبيهات التي استلمها الرئيس نفسه** في فرعه، بلا كشف تنبيهات لا يستلمها | مثبت |
| `D7` | عدّاد الرئيس الشخصي (`scope=user`) مقيّد بفرعه، مطابقةً للقائمة | مثبت |
| `D8` | نقل كل صلاحيات التنبيهات إلى الخدمة، وحذف الفحوص المكررة من المتحكم | مثبت |
| `D9` | إبقاء عدّادات التفاصيل للمحامي مع اختبار صريح `UntilPR6` | مثبت |
| `D10` | استراتيجية التوافق بين الخلفية والواجهة: `atomic` أو `normalizer` (يُحسم قبل المرحلة 8) | مثبت — يُحسم الاختيار قبل المرحلة 8 |
| `D11` | «رئيس بلا فرع» حالة واحدة موثّقة: `400` في الكتابة (`POST`) كما هو العقد الحالي، و`403` في كل أسطح القراءة/العدّ — ويُتخذ القرار في الخدمة وحدها | مثبت |
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
4. `GetDetailsAsync`:
   - يجلب الصف، يفحص الصلاحية من `CreatedById`، ثم `ToDetailDto(row)`.
5. `MarkReadAsync`:
   - `GetRecipientAsync` → إن `null` فـ`false`؛ إن `IsRead` فـ`true`؛ وإلا تعديل + `SaveChangesAsync` **داخل `_tx.RunAsync`** (اصطلاح كل كتابات الخدمة، `:375`).
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

**التغييرات:**

1. `CreateAsync(CreateHeadAlertRequest request, HeadAlertActor actor, string? actorName, CancellationToken)`:
   - `if (!actor.IsHeadOrSubHead) throw new UnauthorizedAccessException(...)`.
   - `var branchId = RequireBranchForWrite(actor)`: `BranchId is null` → `ArgumentException("رئيس القسم دون فرع لا يمكنه إصدار تنبيهات")` — والمتحكم يحوّل `ArgumentException` إلى `400` أصلًا (`AlertsController.cs:133-136`)، فالعقد الحالي محفوظ بلا تكرار.
   - بقية المنطق كما هو.
   - **مصدر واحد (`D11`):** يُحذف فحص الفرع من `Create` في المتحكم؛ الحالة نفسها تُنتج `400` من الخدمة، و`403` في أسطح القراءة/العدّ عبر `RequireBranchForRead(actor)` → `UnauthorizedAccessException` (`D7`).
2. `MarkReadAsync(int alertId, HeadAlertActor actor, CancellationToken)`:
   - `if (!actor.IsLawyer && !actor.IsHeadOrSubHead) throw ...`.
   - ثم فحص صف الاستلام للمستخدم.
3. `ListByDelegationAsync`:
   - `if (!actor.IsLawyer) throw ...`.
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

**اختبارات:**
- خدمة: `CreateAsync` بمحامٍ/مدير → `UnauthorizedAccessException`.
- خدمة: `MarkReadAsync` بمدير → استثناء.
- خدمة: `ListByDelegationAsync` لغير طرف/إنابة غير موجودة → استثناء.
- متحكم: 403 لكل ما سبق؛ `Create` بلا فرع → 400 (من الخدمة)؛ `unread-count` لرئيس بلا فرع → 403 (`D11`، تغيير موثّق من 200 إلى 403 لحساب في حالة غير صالحة).
- تحديث 19 اختبار صلاحيات قائم.

**القبول:** `rg "IsPartyAsync" backend/src` = صفر؛ `rg "RolePermissions|IsLawyer|IsManagerOrAdmin|IsHeadOrSubHead" AlertsController.cs` = صفر (يبقى فقط بناء `Actor` من `User.GetRoleEnum()` لتمرير الدور إلى الخدمة).
**الحجم:** كبير.

---

### المرحلة 4 — العدّادات والنطاق (`P3-4` + `P2-3`، `D6`/`D7`)

**التغييرات:**

1. `enum AlertUnreadScope { User, Received }` في `Application/Common/`.
2. `IHeadAlertService.CountUnreadAsync(HeadAlertActor actor, AlertUnreadScope scope, CancellationToken)`:
   - `User`: محامٍ → `branchId = null`؛ رئيس/شعبة → `RequireBranchForRead` (403 عند غياب الفرع، `D11`)؛ مدير/مشرف → استثناء.
   - `Received`: رئيس/شعبة فقط + فرع إلزامي.
3. المستودع:
   - `CountUnreadForUserAsync(int userId, int? branchId, CancellationToken)`.
   - `CountUnreadForReceivedAsync(int userId, int branchId, CancellationToken)` — استعلام `COUNT` على صفوف الاستلام بشرط `EXISTS` للمستلم.
   - **تغطية الفهارس (مُتحقَّق منها في `DocGeneratorDbContext`):** `HeadAlerts` يملك `(BranchId, IsArchived, LastContentChangedAt, Id)`، و`HeadAlertRecipients` يملك `(UserId, IsRead)` و`(HeadAlertId, UserId)` ⇒ الاستعلامان يُخدمان بفهارس قائمة، **لا هجرة ولا فهرس جديد**.
4. المتحكم: `[FromQuery] string? scope` مع تحليل صريح:
   - `null/""/"user"` → `User`.
   - `"received"` → `Received`.
   - غير ذلك → `400`.
5. حذف `CountUnreadAsync(int userId, ...)` القديم من الواجهة/الخدمة/المستودع، وتحديث مستدعيه: `AlertsController.cs:84` و`HeadAlertServiceTests.cs:290,295`.
6. تحديث `AccountPage` لاحقًا في `PR-5` إلى `scope=received` وإعادة تسمية الشارة.

**اختبارات (8):**
- رئيس: تنبيه مستلم فيه 2 غير مقروء + تنبيه غير مستلم فيه 5 غير مقروء → النتيجة 2.
- رئيس: تنبيه مؤرشف لا يُحسب.
- رئيس: تنبيه في فرع آخر لا يُحسب.
- رئيس بلا فرع: `scope=user` و`scope=received` → `403` (`D11`).
- محامٍ: عدّه الشخصي فقط، بلا قيد فرع.
- مدير/مشرف: `403`.
- `scope=received` لمحامٍ: `403`.
- `scope=xxx`: `400`.

**الحجم:** متوسط.

---

### المرحلة 5 — تحقق المستلمين والترقيم (`P3-5`، `P3-6`)

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

### المرحلة 7 — التوثيق والقرارات (`P3-7`، `P3-12`)

1. `docs/HEAD_ALERTS_RESTRUCTURE_PLAN.md`:
   - §9.1: `PATCH` → `204`، إضافة `scope=user|received`، توضيح افتراضي `by-delegation=100`.
   - §9.3: إحالة `GET /{id}` إلى `D2` بدل شرط الفرع.
   - §8.1/§9: أسماء `ListForActorAsync/ListSentAsync`.
   - **§9.5 جديد «قرارات PR-4»** مؤرَّخ: `D1–D12` + قاعدة الترتيب + خطأ عدّ الحقول.
   - توثيق `RecipientCount/UnreadCount` كاحتفاظ مؤقت حتى `PR-6`.
2. `RUN_GUIDE.md`: اقتران النشر وترتيبه (حسب `D10`).
3. وصف الـPR: الانحرافات، وحدة التراجع، **لا هجرات**.
4. بند `PR-6` صريح: إزالة العدّادين من التفاصيل + إزالة `normalizer` (إن وُجد).

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
| `P2-3` شارة الفرع | 4 + 8 | `UnreadCount_ReceivedScope_*` + اختبار واجهة | `[ ]` |
| `P3-1` صلاحية الخدمة | 3 | `CreateAsync/MarkRead/ByDelegation` خدمةً | `[ ]` |
| `P3-2` شيفرة ميتة | 1 + 2 | بناء بلا تحذيرات + `rg` | `[x]` |
| `P3-3` فجوات التغطية | 6 | الحقول المتميزة + `404` + العدّادات | `[ ]` |
| `P3-4` عدّاد غير مقيّد | 4 | `CountUnread_UserScope_HeadBranch` | `[ ]` |
| `P3-5` تحقق المستلمين | 5 | `InactiveLawyer/Creator` + نص موحّد | `[ ]` |
| `P3-6` فيضان `Skip` | 5 | `InvalidPaging_RejectsHugePage` | `[ ]` |
| `P3-7` انحراف التوثيق | 7 | مراجعة يدوية موثّقة | `[ ]` |
| `P3-8` غياب `Postgres` | 6 | `HeadAlertSummaryPostgresTests` في CI | `[ ]` |
| `P3-9` بيان العقد | 6 | `Contract_ForbiddenFromManifest_*` | `[ ]` |
| `P3-10` هشاشة `StatsScope` | 6 | `AllAlertIdsAsync` متعدد الصفحات | `[ ]` |
| `P3-11` تحميل المستلمين | 2 | إسقاط واحد + عدّ استعلامات | `[x]` |
| `P3-12` تعرّض محتفظ | 7 | اختبار `UntilPR6` + بند `PR-6` | `[ ]` |
| `R-1` (`D11`) حالة «رئيس بلا فرع» | 3 | `HeadWithoutBranch_WriteIs400_ReadIs403` | `[ ]` |
| `R-2` فهارس عدّاد `received` | 4 | مراجعة مخطط موثّقة (لا هجرة) | `[ ]` |
| `R-3` سقف الترقيم | 5 | `Paging_OverflowRejected_ExtremePageWithSize1Allowed` | `[ ]` |
| `R-4` حرس `Postgres` | 6 | تنفيذ فعلي > 0 اختبار في وظيفة CI | `[ ]` |
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
| إعادة هيكلة `MarkRead` تفقد التتبع | متوسط | `GetRecipientAsync` متتبَّع + اختبار `ReadAt` للآخرين |
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

---

**نهاية الوثيقة.**
