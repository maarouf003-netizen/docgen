# خريطة أدلة معايير القبول (§18) — مرساة مراجعة

> **الغرض:** تحويل بنود `§18` في `HEAD_ALERTS_RESTRUCTURE_PLAN.md` من **ادعاءات** إلى
> **ادعاءات + دليل**، لتكون مادة المراجعة النقدية في محادثة مستقلة. هذه **ليست شهادة إنجاز**:
> البنود تبقى `[ ]` في الخطة حتى يقرّر المراجع أنها محقَّقة.
>
> **المرساة:** `master` = `f2e14b8` (الشجرة نظيفة، `CI` 6/6).
>
> **رموز الحالة:**
> - ✅ **دليل مباشر:** ملف/اختبار محدد موجود ويفعل ما يقوله البند.
> - 🟡 **دليل جزئي:** المسار موجود، لكن لم أجد تأكيدًا صريحًا للجزء الأخير من البند.
> - ❓ **لا دليل قاطع:** نقطة تحقيق أولى للمراجع (قد تكون ثغرة توثيق أو ثغرة فعلية).
> - ❌ **ادعاء غير محقَّق في الكود:** بحثتُ عن التنفيذ ولم أجده.
> - ⏸ **مؤجَّل بقرار مالك** موثّق.
>
> **مصدر الترقيم:** أرقام الأسطر هي أسطر البنود في `HEAD_ALERTS_RESTRUCTURE_PLAN.md` عند المرساة.
>
> **التغطية:** الجدول يغطي البنود **المفتوحة** (58 صفًّا = 58 بندًا). البنود المؤشَّرة في الخطة
> (الترقيم `3089`، المؤشر `3091`، الجسر `3105`) غير مكرَّرة هنا؛ دليل المؤشر مُثبَّت في
> `HEAD_ALERTS_DELIVERY_RUNBOOK.md` §11، ودليل إزالة الجسر في `#65`.

---

## 1) الواجهة والأدوار والسلوك الظاهر — §18:3042‑3060

| البند | الدليل | الحالة |
|---|---|---|
| 3042 `/alerts` للأدوار الثلاثة، الإشراف قراءة فقط، «مرسلة» للرئيس/الشعبة | `frontend/src/pages/AlertsPage.tsx` (`isManager`/`isHeadOrSubHead`/`canMarkRead`/`sentEnabled`) + `AlertsPage.test.tsx` | ✅ |
| 3044 وصول المدير من `ManagerIconRow` ورابط `AccountPage`، بلا عدّاد/طلب عدّاد | **مفقود:** `ManagerIconRow.tsx` بلا بطاقة `/alerts` (البطاقات: stats/reviews/correspondence/suggestions/entities/delegates/users/branches)، و`AccountPage.tsx` بلا رابط `/alerts` (بحث شامل: الرابط موجود في `HeadIconRow:160` و`LawyerIconRow:122` فقط). الحالة السلبية للعدّاد صحيحة (`enabled: … && !isManager` في `AccountPage`) | ❌ |
| 3046 كل تنبيه `Kind` صالح، و`legacy` للعرض فقط | `HeadAlertKindCatalog` + `HeadAlertKindCatalogTests` + `HeadAlertDedupe` يرفض `legacy` | ✅ |
| 3048 لا مطابقة نص في منطق يومي (`FindPendingAlertAsync`/`FindByMessageAndBranchAsync`/`PendingAlertPrefix`) | بحث شامل: **صفر** نتائج في `backend/src` (أقوى من المطلوب: حتى مسار الترقية أُزيل مع الجسر) | ✅ |
| 3050 تنبيهات الدوائر مربوطة بـ`ExecutionCircuitId`/`DedupeKey`، وتغيير التسمية لا يطال غيره | `HeadAlertMessages`/`HeadAlertDedupe` + اختبارات الدوائر | 🟡 |
| 3051 الأرشفة بدل الحذف في مواضع §8.6 | `HeadAlertService.ArchiveAsync*` + `ArchiveBy…CoreAsync` | 🟡 (لا اختبار «لا حذف نهائي» صريح) |
| 3052 `archiveKinds` صريحة + `HeadAlertSupersedePlan` عبر `EnqueueSystemAsync` + اختبار بقاء البديل | 11 موضعًا في `DocumentDelegationService*`/`DocumentAppealService` + `HeadAlertOutboxTests`/`HeadAlertOutboxDispatcherTests` | ✅ |
| 3055 المؤرشف مستبعد من القوائم/العدّاد ومتاح بفلتر | `AlertArchiveFilter` + `HeadAlertRepository.ApplyArchiveFilter` + `HeadAlertRepositorySummaryTests.ArchiveFilter_…` | ✅ |
| 3056 الرئيس يعلّم القراءة من الصفحة وشريط الإنابة | `AlertsPage.canMarkRead` + `DelegationActivityStrip` (تأكيد الاختبار) | 🟡 |
| 3057 «مرسلة»: عدّادات + «مقروء من الجميع» + نافذة المستلمين | `AlertsPage.tsx` + `AlertsPage.test.tsx` | ✅ |
| 3058 لا تنبيهات إدخال/اقتراح جهة، وسجل المراجعة يعرض القيود | `PublicEntityService*` بعد إزالة التنبيهين؛ سجل المراجعة | 🟡 |
| 3059 إشعار إعادة التسمية للمنشئ | `HeadAlertMessages.EntityRenameNoticeToCreator` | ✅ |
| 3060 البادئة اليدوية حسب الدور + `ManualKindFor` | `HeadAlertMessages.ManualPrefix` + `HeadAlertService.ManualKindFor` | ✅ |

## 2) العقود وبناء الرسائل — §18:3061‑3070

| البند | الدليل | الحالة |
|---|---|---|
| 3061 `CreateHeadAlertRequest` لا يقبل الحقول الداخلية، و`TargetType=head` مرفوض، و26 موقعًا عبر Outbox، وبلا `new HeadAlert` خارج الخدمة | `AlertsIntegrationTests.CreateAlert_HeadTarget_Rejected` + `HeadAlertArchitectureTests.HeadAlert_NoDirectConstructionOutsideService` | ✅ (عدّ 26 موقعًا يحتاج جردًا) |
| 3065 `CreateSystemHeadAlertRequest` بالحقول الإلزامية، و`DedupeKey` مركزي | `HeadAlertSystemContracts.cs` + `HeadAlertDedupe.Build` | ✅ |
| 3067 مقطع `recipient:` + `RekeyRecipient` عند التعاقب | `HeadAlertDedupe.cs:105‑148` + `HeadAlertSuccessionService.cs:161` | ✅ |
| 3069 لا `new HeadAlert`/`.AddAsync` خارج الخدمة، ولا `CreateSystemAsync` خارجها/المشغّل | نفس اختبار المعمارية أعلاه | ✅ |

## 3) المعاملة و`Outbox` — §18:3071‑3088

| البند | الدليل | الحالة |
|---|---|---|
| 3071 `Enqueue` داخل معاملة العمل، و`EnqueueSystemAsync` خارجها يفشل، وسباق = محاولة ثم `failed` | `HeadAlertOutbox` + `HeadAlertOutboxTests` | ✅ |
| 3073 `CreatedById` إلزامي في الطلب والحمولة، ولا يُستنتج من المستلم | `HeadAlertSystemContracts` + `HeadAlertOutboxPayload` + اختبارات | ✅ |
| 3075 `ITransactionContext` + `TransactionRunner` على نفس `DbContext` + `Application` بلا EF | `DocGenerator.Application.csproj` **بلا** أي مرجع `EntityFramework` (تحقق آلي) + `TransactionRunner` | ✅ |
| 3077 `Enqueue` قبل آخر `SaveChanges` + اختبار حدّ الحفظ | `HeadAlertOutboxTests` (اختبار حدّ الحفظ) | 🟡 |
| 3078 لا `catch` يبتلع، و`head_alert_failed` = 0، والمسارات الأربعة داخل معاملة/`Outbox` | بحث `head_alert_failed` في `backend/src` = **صفر** | ✅ |
| 3081 `MarkRetry`/`MarkFailed` بعد rollback في Scope جديد | `HeadAlertOutboxDispatcher` + اختباراته | 🟡 |
| 3082 مرجع مفقود ⇒ `skipped: missing_reference` | `HeadAlertOutboxSkipReasons.MissingReference` + اختبار | ✅ |
| 3083 `SupersedePlan` عند `Unchanged`، وبلا معاملة متداخلة | `HeadAlertService` + `HeadAlertOutboxDispatcherTests` | 🟡 |
| 3084 أرشفة الإنابة/الاستئناف وفق الخيار المعتمد بلا `best-effort` | الكود موجود؛ **الخيار نفسه غير موقَّع في §20** | 🟡 |
| 3085 `JsonStringEnumConverter` + تطابق `operation`/`Kind` + تدقيق `create_alert`/`update_alert` | `HeadAlertOutboxPayload.cs:107` + `HeadAlertService` | ✅ |
| 3087 صف واحد لكل `(حدث، فرع، جمهور)` وكل المستلمين في `HeadAlertRecipient` | `HeadAlertOutboxStore` + `HeadAlertService` | 🟡 |

## 4) السلوك الفعلي والسباقات — §18:3095‑3108

| البند | الدليل | الحالة |
|---|---|---|
| 3095 `HeadAlertConflictException` بلا رمز HTTP، وإعادة واحدة بعلامة `conflict_retry` | `HeadAlertExceptions` + `HeadAlertOutboxDispatcher.cs:34,403` | ✅ |
| 3098 `TargetUserId` للأحادي، `circuit_renamed` بلا إنشاء، `stale_event`، `no_recipients`، تنظيف `processed/skipped` | ثوابت `HeadAlertOutboxSkipReasons` + `HeadAlertOutboxOptions` (7/30 يومًا) + المشغّل | ✅ |
| 3101 `FileAlertModal` بعد التنقية، واختباراه محدّثان | `FileAlertModal.tsx` + `FileAlertModal.test`/`DocumentView.test` | 🟡 |
| 3103 `Message` بلا سقف 2000 | بحث `2000` في `HeadAlertService` = **صفر** | ✅ |
| 3104 سباق الإنشاء ⇒ صف نشط واحد، والفهرس الجزئي فعّال | `UX_HeadAlerts_DedupeKey_Active` + `HeadAlertSchemaMigrationTests` + اختبار السباق | ✅ |
| 3107 إعادة استخدام `DedupeKey` بعد الأرشفة تُنشئ صفًا جديدًا | يتطلب تأكيد اختبار صريح | 🟡 |
| 3108 `LastContentChangedAt` يتغيّر مع النص فقط، والترتيب حتمي | `HeadAlertService` + `HeadAlertRepositorySummaryTests` (الترتيب) | 🟡 |

## 5) `API` — §18:3109‑3113

| البند | الدليل | الحالة |
|---|---|---|
| 3109 القوائم تعيد معاينة/طول/اقتصاص فقط، بلا `Message` كامل ولا `hasFullMessage` | `HeadAlertDtos` + `readAlertsPage` + حارس البيان | ✅ |
| 3111 `GET /alerts/{id}` نص كامل للمصرح و`404` لغيره | `AlertsController.Get` + `AlertsIntegrationTests` | 🟡 |
| 3112 ترقيم إلزامي وسقف 100، والإشراف بلا `Take(500)` | `AlertPageRequest` + `ListSummariesForOversightAsync` | ✅ |

## 6) الواجهة (تفاصيل) — §18:3114‑3124

| البند | الدليل | الحالة |
|---|---|---|
| 3114 `AccountPage`: «غير المقروء لي» بـ`scope=user`، بلا شارة للمدير، **وله رابط `/alerts`** | العدّاد صحيح (`scope=user` + `!isManager`)؛ **الرابط مفقود** (بحث شامل: لا `/alerts` في `AccountPage.tsx`) — نفس ثغرة `3044` | ❌ |
| 3116 شارة اللوحة بالنطاق نفسه (`user`) | `Dashboard.tsx` (طلب `scope=user`) | ✅ |
| 3118 نص 12,000 حرف كاملًا في التفاصيل على الجوال | `AlertDetails` + اختبار النص الطويل | 🟡 |
| 3119 أهداف لمس `min-h-11` وحوارات وصول (ESC/تركيز/قفل تمرير) | استخدام واسع لـ`min-h-11`؛ الحوارات تحتاج تأكيدًا | 🟡 |
| 3120 تعميمات الجهات الثمانية | `HeadAlertMessages` (Rename/Merge/Unify/Abolish × Lawyers/Heads = 8) | ✅ |
| 3121 التعاقب: نقل غير المقروء النشط فقط، دمج صف المستلم، ومعالجة تصادم المفتاح | `HeadAlertSuccessionService` + اختباراته | 🟡 |

## 7) الهجرات والنشر والبوابات — §18:3125‑3153

| البند | الدليل | الحالة |
|---|---|---|
| 3125 الهجرات الأربع لكل مزوّد + الفهارس + قيد المستلم + صفر مفاتيح مكررة + اختبارات الهجرة | 8 ملفات هجرة (`…Expand`/`…Outbox_Create`/`…Backfill`/`…Contract` + `_Pg`) + `HeadAlertSchemaMigrationTests` + `UX_HeadAlertRecipients_HeadAlertId_UserId` | ✅ |
| 3129 ترتيب `M1`/`M2`/`M3` وقابلية النشر المنفرد | سجل الدليل §11 + حقيقة النشر الحيّ (`PostgreSQL` 63 هجرة) | ✅ |
| 3132 تنبيه أوامر `dotnet ef database update` موثّق | `RUN_GUIDE.md` §9 (قائمة الهجرات التراكمية) + AGENTS.md | ✅ |
| 3135 البوابات خضراء | `CI` 6/6 على `f2e14b8`؛ `dotnet test` 2935/0 · `vitest` 159/1668 | ✅ |
| 3136 تقرير `G0` وقرار نطاق الجسر في §20 | **أُسقط بقرار المالك** (الإطلاق يبدأ فارغًا)؛ الخانات غير مؤشَّرة | ⏸ |
| 3137 Dry-run على نسخة إنتاجية مستعادة | **مؤجَّل بقرار** لبيئات `SQLite` (`G2`)؛ غير منطبق على `PostgreSQL` الحيّ | ⏸ |
| 3139 `AutoMigrate=false` في الإنتاج | `appsettings.json` + `Program.cs:341` (الافتراضي خارج التطوير) | ✅ |
| 3141 `BuildPreview` واعٍ بـUnicode و`MessageLength` بنقاط Unicode والاقتصاص خادمي | `HeadAlertTextLimitsTests` | ✅ |
| 3143 اختبار يثبت اختلاف صياغة `HasFilter`/قيد `Kind` بين المزوّدين | **لم أجد اختبارًا صريحًا**؛ `HeadAlertSchemaMigrationTests` يفحص الفهارس على `SQLite` | ❓ |
| 3144 التنفيذ بـ`PR-1..PR-6` منفصلة | سجل الدمج: `#54`/`#55`/`#58`/`#62`/`#65`/`#66`/`#68` | ✅ |
| 3146 اقتران `PR-4`+`PR-5` بذرّية (`D10`) | `HEAD_ALERTS_DELIVERY_RUNBOOK` §9 + `RUN_GUIDE` §9 | ✅ |
| 3149 قيد النسخة الواحدة موثّق في `RUN_GUIDE.md` | **غير موجود في `RUN_GUIDE`** (موجود في الخطة §16.1 وفي `docs/audit/10-…`) | ❓ |
| 3151 مراجعة المسار كاملًا حقلًا بحقل + لا `Message` في أي قائمة | مراجعة `PR‑6c` (هذه الجلسة) + حارس البيان | 🟡 |
| 3153 `head-alert-contracts.json` + `HeadAlertContractTests` + `contract_sync_alerts.test.ts` | الملفات الثلاثة موجودة وخضراء في `CI` | ✅ |

---

## الملاحظات التي كشفها هذا الجرد (نقاط بدء للمراجع)

1. **❌ `§18:3044` ثغرة وظيفية لا توثيقية:** `/alerts` **غير موصولة للمدير/المشرف**: لا بطاقة في
   `ManagerIconRow.tsx` ولا رابط في `AccountPage.tsx`. الرابط موجود للمحامي (`LawyerIconRow.tsx:122`)
   وللرئيس/الشعبة (`HeadIconRow.tsx:160`) فقط. الأثر: المدير/المشرف لا يصل إلى وضع الإشراف القرائي إلا
   بكتابة المسار يدويًا — أي أن نصف البند (الوصول) غير منفَّذ، ونصفه الآخر (غياب العدّاد) منفَّذ.
2. **`§18:3149` ثغرة توثيق:** قيد «نسخة تطبيق واحدة» مطلوب **في `RUN_GUIDE.md`** وغير موجود فيه
   (موجود في الخطة §16.1 وفي `docs/audit/10-head-alerts-pr2b-deploy-20261025.md` فقط).
3. **`§18:3143` بلا دليل:** لا اختبار صريح يثبت اختلاف صياغة `HasFilter`/قيد `Kind` بين `SQLite`
   و`PostgreSQL`. إن كان الفرق مقصودًا ومهمًا، فغياب الاختبار ثغرة تغطية.
4. **توزيع الحالات على 58 بندًا مفتوحًا:** ✅ 35 · 🟡 17 · ❌ 2 · ❓ 2 · ⏸ 2. بنود 🟡 كلها من نوع
   «المسار موجود، والتأكيد النصي غير مرئي» — أسرع طريق: البحث عن اسم الاختبار المذكور في الصف قبل
   اعتبار البند محقَّقًا.
5. **البنود ⏸ (اثنان)** ليست نقصًا: أُسقطت/أُجّلت بقرار موثّق (الإطلاق الفارغ، و`G2` لبيئات `SQLite`).
6. **`§20` (14 خانة)** توقيعات مالك غير مؤشَّرة — عامّةً، ومنها سياستان أثرهما على الكود قائم:
   `closed_linked` وأرشفة الإنابة/الاستئناف.

> **تنبيه للمراجع:** هذا الجرد أُعدّ بقراءة الكود والبحث النصي، لا بتشغيل كل اختبار منفردًا. أي ✅ يعني
> «الملف/الاختبار موجود ويفعل ما يوصف»، وأي 🟡/❓ يعني «تحقّق قبل التأشير».
