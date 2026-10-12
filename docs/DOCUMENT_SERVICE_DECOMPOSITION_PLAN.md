# خطة تفكيك `DocumentService` (الصنف الإله) — كسر نظيف متعدد المراحل

> **الحالة:** ✅ منفذة بالكامل ومدموجة في `master` بتاريخ 12/10/2026 — المراحل 0–5 عبر `PRs` رقم #12–#18 (كل مرحلة فرع مستقل + بوابات خضراء + مراجعة سطرية). لا تنفيذ إضافي مطلوب.
> **النطاق:** خلفية فقط (`backend/`) — صفر هجرات، صفر تغيير مسارات `API`، صفر مساس بالواجهة.
> **القاعدة الفرعية:** `master` محمي — كل مرحلة فرع `refactor/…` ثم `PR` + اخضرار البوابات.
> **الأمانة المهنية:** هذا التفكيك نظافة بنيوية لا إصلاح عطل — الصنف يعمل ومغطى باختبارات واسعة،
> والتكلفة التقديرية أدناه (~1.5–2 أسبوع متقطع) تُوزن مقابل ذلك قبل الإقرار.
> **سجل القرار:** قرار المالك (11/10/2026): تفكيك كامل + كسر نظيف.

---

## 1) المراجعة النقدية للمسودة الأولى (لماذا عُدّلت)

المسودة الأولى افترضت «نقلًا ميكانيكيًا ملفًا بملف». الفحص الدليلي أثبت خطأ الافتراض:

| الافتراض الخاطئ | الدليل |
|---|---|
| كل جزء مستقل بذاته | نواة `UpdateAsync` (`DocumentService.cs:283-387`) تستدعي ~12 مساعدًا خاصًا عبر 6 ملفات: `ResolveCircuitForWriteAsync` و`EnsureNumberUniqueByCircuitAsync` و`AddCircuitSwitchOccurrencesAsync` (الدوائر)، `EnsureConcurrency` و`WithNumberGuardAsync` و`WithConcurrencyGuardAsync` (التزامن/الترقيم)، `ApplyRenewalAsync` و`AddStruckOffOccurrenceAsync` (الحالات)، `SyncDelegationSnapshotsForDocumentAsync` (التطبيق)، `MirrorSourceEditsToTargetsAsync` و`LogDocumentChangesAsync` و`FireDelegationAlertsAsync` و`FireDelegationReverseAlertAsync` و`ValidateDelegationTargetEdit` و`ValidateDelegationSeizureLockAsync` (الإنابة) |
| دوال الحالات تنتقل مع ملفها فقط | دوال `Status.cs` تستدعي حراس التزامن/الترقيم/الدوائر والتدقيق — نفس النواة المشتركة |
| دوال التدوير (`Rotation`) قرائية خالصة | `SaveBaseNumbersAsync` (`Search.cs:237`) كتابة تستخدم `WithNumberGuardAsync` + `EnsureNumberUniqueByCircuitAsync` |
| لا بيوت ناقصة | `ApplyRegistrationDate` (مثيلية — نسخة `_registrationDates`، `Apply.cs:862`) و`SyncDelegationSnapshotsForDocumentAsync` (نسخة `_delegations`، `Apply.cs:46`) بلا بيت في التقسيم الأول، ونوعا `MirrorAlertIntent`/`TargetEditResult` (`DelegationMirror.cs:17,73`, ‏`private`) يستهلكهما لبّ `UpdateAsync` |
| نواة `internal` بلا واجهة تُحقن في الصنف | باني `DocumentService` **عام**، ولا `InternalsVisibleTo` في `backend/` — بارامتر `internal` في باني عام = `CS0051` (لا يُترجم) |
| «المرحلة 0 وحدها لا تكسر شيئًا» (مع حقن `DI`) | حقن أي تبعية يغيّر **الباني**، والاختبارات تُنشئ **الصنف** مباشرة (14 موقعًا في 13 ملفًا) بحقول مُنمّطة بالواجهة — الكسر يضرب من جهتين (التوقيعات + الباني) |

**الخلاصة الملزمة:** النواة المشتركة أولًا، **وداخل الصنف نفسه** (`partial` جديد — بلا `DI` ولا تغيير باني)،
وإلا البدائل هي تكرار المنطق أو دورات تبعية أو كسر مبكر للاختبارات — كلها مرفوضة.

---

## 2) حقائق مُتحقق منها (أساس التقسيم)

- الأحجام: `Status.cs` ‏1133، `DelegationMirror.cs` ‏1043، `Apply.cs` ‏952، `DocumentService.cs` ‏637 (الواجهة + الباني + النوى)، `Search.cs` ‏339 (تشمل التدوير ‏163-339)، `Actions.cs` ‏281، `Circuits.cs` ‏201، البقية < 65.
- المستهلكون الخارجيون لـ`IDocumentService`: `DocumentsController` (الكل)، `DelegationsController` و`AppealsController` (يستخدمان `GetAsync` فقط — **لا يتأثران بأي مرحلة**)، ولا خدمة تطبيقية تعتمد عليها (لا دورات قائمة). لا اختبارات متحكمات تذكر الواجهة.
- مواقع بناء `new DocumentService(` في الاختبارات: 14 موقعًا في 13 ملفًا بعشرين وسيطًا موضعيًا (العدّ لحظة كتابة الخطة — يُعاد التأكد بـ`rg` قبل كل مرحلة).
- التسجيل: `DependencyInjection.cs:23` سطر واحد (`AddScoped`) — كل خدمة جديدة سطر مماثل، **ويجب أن تبقى `Scoped` حصرًا** (السبب: `DbContext`/`UoW`/المعاملة مشتركة عبر النطاق — `Transient`/`Singleton` يكسر الذرّية).
- `FIXES_LOG.md` المحال إليه في تعليق `Actions.cs:14-16` **غير موجود** في المستودع — يُصحَّح التعليق ضمن المرحلة 1 (تشير للخطة هذه بدل مرجع ميت).
- `TransactionRunner.RunAsync` (`TransactionRunner.cs:31-48`) يبدأ معاملة ويُثبَّت **بلا فحص تداخل** — التعشيش يرمي `InvalidOperationException`. (انظر الثابت 3 أدناه.)

---

## 3) المراحل المنقحة (0–5)

### المرحلة 0 — النواة داخل الصنف (~ساعات، صفر كسر)

نقل المساعدات العابرة إلى ملفات `partial` جديدة **داخل الصنف نفسه** (`DocumentService.Kernel.Guards.cs` / `Kernel.Circuits.cs` / `Kernel.Audit.cs` — تستبق البيوت النهائية بدل درجٍ واحد) —
**بلا `DI`، بلا تغيير باني، بلا تغيير أي توقيع عام، بلا مساس بالاختبارات**:

- الحراس: `EnsureConcurrency`، `WithConcurrencyGuardAsync`، `WithNumberGuardAsync` (×2)، `ApplyCircuitDerivation`، `EnsureNotPendingRegistration`، `EnsureNumberUniqueByCircuitAsync`/`EnsureNumberUniqueCoreAsync`، `ResolveCircuitForWriteAsync`، `AddCircuitSwitchOccurrencesAsync`، `LogDocumentChangesAsync`، `AuditWithActor`/`ActorFullName` (خالصتان)، `TypeLabel`، `RotationDisplayName`/`FormatName`، وسائر `static` الخالصة.
- **تصنيف مُصحَّح:** `CurrentYear()` و`ApplyRegistrationDate` **مثيليتان** (تعتمدان `_clock`/`_timeZone` و`_registrationDates`) — تبقيان داخل الصنف في هذه المرحلة بلا تغيير، وتُعالَج تبعياتهما صراحة عند الاستخراج اللاحق (كل خدمة تحتاج السنة تُحقن `TimeProvider`/`TimeZone`).
- بوابة الخروج: `rg` بأسماء المساعدات يؤكد زوال النسخ القديمة + `dotnet test` أخضر للمشروعين. هذه المرحلة شبكة أمان خالصة.

#### قرار البيت المشترك (يُحسم هنا — يُطبَّق من المرحلة 1 فصاعدًا)

- الخالصات (`AuditWithActor`، `ActorFullName`، `TypeLabel`، `RotationDisplayName`/`FormatName`، `Trimmed`/`DateText`، `ApplyCircuitDerivation`، `EnsureNotPendingRegistration`) ← صنف `static` مشترك `Common/DocumentText.cs`، بلا حالة ولا تبعيات.
- الحراس ذات الاعتمادات (`EnsureConcurrency`، `WithConcurrencyGuardAsync`، `WithNumberGuardAsync` ×2، `EnsureNumberUniqueByCircuitAsync`/`Core`، `ResolveCircuitForWriteAsync`، `AddCircuitSwitchOccurrencesAsync`، `LogDocumentChangesAsync`) ← متعاون نحيف محقون `IDocumentWriteGuard` / `DocumentWriteGuard` (`Scoped`) يحمل `_documents` و`_dbErrors` و`_audit` و`_occurrences` و`_circuits` فقط.
- `CurrentYear()` تُستبدل في المواقع المنقولة بـ`ServerClock.CurrentYear(_clock, _timeZone)` (نمط الخدمات الأخت) — كل خدمة مستخرجة تُحقن `TimeProvider`/`TimeZone` عند حاجتها للسنة.
- `ApplyRegistrationDate` تبقى بالنواة (مستهلكها الوحيد — `_registrationDates` يبقى في باني النواة).
- كاتبا الوقوعات (`ApplyRenewalAsync`، `AddStruckOffOccurrenceAsync`) يبقيان في خدمة الحالات (ملكية المجال: التجديد/الشطب) وتستهلكهما النواة عبر الواجهة (نواة←حالات — مسموح؛ القرار المُسجَّل لسؤال الحدّ).

### المرحلة 1 — الإجراءات (~يوم شامل المصنع)

- **الخطوة 1أ أولًا:** مصنع اختباري `DocumentServiceFactory` (مشروع الاختبارات فقط) يعزل الـ14 موقع بناء — **قبل** أي تغيير باني، وبمعاملات **مسماة/اختيارية** ليكون تقليص البواني لاحقًا داخل جسم المصنع فقط لا في مواقع الاستدعاء.
- **الخطوة 1ب:** خدمة `IExecutionActionService` / `ExecutionActionService` (واجهة عامة — عرف المستودع): الدوال الست (`Get/Add/Update/Delete/ClearReminder/RescheduleReminder`) + `SeedInitialActionsAsync` + `NormalizeAction`/`NormalizeReminder` (من `Actions.cs` كاملًا).
- الاعتمادات: `_actions` و`_documents` و`_uow` و`_tx` و`_audit` والساعة/المنطقة فقط.
- إعادة السلك: `DocumentsController` (6 نداءات — يكتسب المتحكم +3 تبعيات إجمالًا عبر المراحل (إجراءات/بحث/حالات): مقصود ومُفصَح عنه) + نواتا الإنشاء/التعديل (عبر الواجهة الجديدة — الاتجاه الوحيد المسموح: نواة←إجراءات) + حذف الدوال الست من `IDocumentService` (الكسر النظيف) + تحديث الاختبارات المستدعية عبر المصنع.
- تصحيح التعليق الناقص في `Actions.cs:14-16` (حذف الإحالة الميتة لـ`FIXES_LOG.md`، وتوثيق الانحراف البنيوي المؤجل في وصف `PR`).

### المرحلة 2 — البحث (~نصف–يوم)

- خدمة `IDocumentSearchService`: دوال البحث الخمس + `ExportAsync` + `GetFilterOptionsAsync` (ملاحظة: تمريرة للمستودع بالاسم نفسه — يُحافَظ على التسمية القائمة بلا إعادة تسمية) + `EnrichAppealFlagsAsync` (نسخة `_appeals` تنتقل معها).
- الاعتمادات الدقيقة: `_documents` و`_appeals` و`_audit` (التصدير يسجّل تدقيقًا — `Search.cs:157`) و`ExportOptions` (`_maxExportRows` — `Search.cs:139`) والساعة/المنطقة — **وبلا `_tx`/`_uow`/`_users`** (كل استخداماتها في التدوير الذي يبقى بالنواة).
- التدوير (`GetRotationListAsync`/`GetBaseNumberHistoryAsync`/`SaveBaseNumbersAsync` من `Search.cs:163-339`) يبقى مع النواة (ترقيم/دوائر — قريب من `Numbering.cs` و`Circuits.cs`) لا مع البحث.
- توثيق قطيعة الزوج: `GetDeletedAsync` (قراءة مفردة بالمعرف) يبقى بالنواة، و`SearchDeletedAsync` (استعلام ترحّلي) يذهب للبحث — التقسيم حسب شكل العملية لا الاسم.
- قرائية غالبًا — أدنى المراحل خطرًا بعد التجريبية.

### المرحلة 3 — الإنابة (إلزامية، ~يومان)

- النطاق: `DelegationMirror.cs` كاملًا + `ValidateDelegationSeizureLockAsync` + `SyncDelegationSnapshotsForDocumentAsync`.
- القرار الافتراضي: **الدمج في `IDocumentDelegationService` القائمة** (تملك نطاق الإنابة ومستودعها — **العقد يكبر بدوال عامة جديدة**) لا خدمة جديدة — ما لم يُظهر التقييم النصفي تعارض تبعيات، فيُنشأ `IDocumentMirrorService` مستقل.
- الاعتمادات: `_delegations` و`_documents` و`_alertService` و`_audit` و`_uow` (حفظ وسطي — `DelegationMirror.cs:599`) والساعة/المنطقة — **وبلا `_tx` إطلاقًا** (تُستدعى المزامنة داخل معاملة المستدعي، وإطلاق التنبيهات خارجها — تُحفَظ المواضع حرفيًا).
- تفصيل ميكانيكي: استدعاءات `CurrentYear()` داخل المرآة (مثلًا `DelegationMirror.cs:975-996`) تُستبدل بـ`ServerClock.CurrentYear(_clock, _timeZone)` (مكافئ سلوكيًا — هدف الدمج يملكهما أصلًا).
- **عقد صريح بلا توسيع إتاحة:** النواة تستدعي 2–3 دوال عامة بمعاملات عامة (`DTOs`) فقط — النوعان `MirrorAlertIntent`/`TargetEditResult` يبقيان `private` داخل خدمة الإنابة ولا يعبران أي حدّ عام.
- تُنفَّذ **قبل الحالات عمدًا**: الحالة تستدعي المرآة في 5 مواقع (`Status.cs:308` + 310،394،604،826) والمرآة ورقة لا تستدعي حالات — التبعية أحادية والدليل يغلب منطق «النضج».

### المرحلة 4 — الحالات (~2–3 أيام)

- خدمة `IDocumentStatusService`: الدوال الثمان (`UpdateStatus`/`ConsiderExecutedByDelegation`/`RevertStatus`/`ReturnFromReferredToStart`/`UpdateExecutedStatus` ×2/`RestoreStruckOff` ×2) + `ApplyRenewalAsync` + `AddStruckOffOccurrenceAsync` + `ApplyDelegationInheritanceOrRecoveryAsync` + `CleanupPendingCompletionAlertsAsync` (لا يستدعيها سواها).
- الاعتمادات الدقيقة: `_documents` و`_occurrences` و`_baseNumbers` و`_audit` و`_alertService` (استثناء البنية التحتية — القاعدة 4) و`_delegations` و`_tx` و`_uow` والساعة/المنطقة.
- **قبل النقل:** عيّنة تغطية لمسارات التجديد/الوراثة/الشطب في حزمة الاختبارات — اختبارات توصيف (`characterization`) **للفجوات فقط**، ثم النقل الميكانيكي، ثم إثبات التكافؤ بإعادة تشغيلها.
- تنادي تنبيهات الإنابة عبر `IDocumentDelegationService` (المتاحة من المرحلة 3 — حُلّت تبعية المواقع الخمسة)؛ والنواة تستدعي كاتبي الوقوعات عبر الواجهة الجديدة عند الحاجة (نواة←حالات — مسموح).
- إنهاء تعليق `Status.cs:41-43` وتوحيد الاسم على `IDocumentStatusService` (تقاعُد اسم `StatusTransitionService`) وحذف الإحالة الميتة لـ`FIXES_LOG.md`.

### المرحلة 5 — النواة النحيفة (~نصف يوم، محجوبة خلف 3 و4)

- يبقى في `IDocumentService`: `Get/GetDeleted/Create/Update/Delete/Restore/Transfer/CountFilesByOwner/TransferAll` + التدوير + `IncrementViewCountAsync` + `ApplyRegistrationDate`.
- يُحذف سقال `partial` (تجميلي)، ويصغر الباني للاعتمادات الباقية فقط، وتُحدَّث المواقع الـ14 عبر المصنع.
- يُحسم وضع `docs/audit/01-discovery.md` (أسطر 33، 61، 63 تصف «6 ملفات `partial`»): وثيقة لحظية تُترك، أم حية تُحدَّث هنا.

---

## 4) قواعد ملزمة لكل مرحلة

1. فرع `refactor/…` + `PR` مستقل — لا دمج قبل `dotnet test` للمشروعين أخضر (الواجهة لا تُمس فبواباتها غير مطلوبة).
2. نقل ميكانيكي بلا سلوك: يُمنع أي «تحسين جانبي» داخل `PR` التفكيك.
3. **ثابت ملكية المعاملة:** معاملة `_tx.RunAsync` واحدة لكل نقطة دخول عامة — يُمنع أي مساعد أو متعاون (بما فيها الخدمات الجديدة) من فتح معاملة خاصة. يُفرَض بقائمة فحص كل `PR`: ‏`rg _tx.RunAsync` في الملفات المنقولة يؤكد أن اللفّ في نقاط الدخول فقط.
- تُحفَظ مواضع الاستدعاء **داخل/خارج المعاملة حرفيًا** عند النقل (المزامنة داخل `RunAsync`، وإطلاق التنبيهات خارجه) — أي تبديل هنا تغيير سلوكي.
- يُمنع `Task.WhenAll` (أو أي تزامن) على `DbContext` مشترك داخل النطاق الواحد.
- كل دالة عامة حساسة لسياق المعاملة (`SeedInitialActionsAsync`، كاتبا الوقوعات، دوال المزامنة/إطلاق التنبيهات) تحمل توثيق `XML` صريحًا لعقدها («داخل معاملة المستدعي حصرًا» / «بعد التثبيت حصرًا») — المترجم لن يمنع سوء الاستدعاء مستقبلًا.
4. اتجاه تبعية واحد: الخدمات الجديدة على المستودعات **والمتعاونين البنيويين المشتركين أصلًا** (`IAuditLogger`، `IHeadAlertService`، `ITransactionRunner`، `IUnitOfWork` — بلا دورات: لا أحد منهم ينادي خدمات الملفات)؛ النواة←(إجراءات/حالات/إنابة) فقط؛ أي حاجة عكسية تُحل بنقل المساعد لا بنداء خدمة.
5. **عتبة انقسام النواة (استشارية موثقة):** تبقى النواة واحدة ما لم تتجاوز مجالين جديدين أو تنتفخ بما يستدعي الفصل — التقسيم الرباعي الفوري مرفوض (`YAGNI`).
6. تعريف الإنجاز: منقول + محذوف من `IDocumentService` + مسجَّل في `DI` (‏`Scoped` حصرًا) + متحكم/اختبارات مُعاد سلكها + `rg` بلا شوارد + البوابات خضراء.
7. **شرط الإيقاف:** أي `PR` مرحلي يُظهر فرقًا سلوكيًا (أحمر غير ميكانيكي) → تراجع وتحليل قبل المتابعة — لا تجاوزه بإصلاحات ترقيعية داخل `PR` التفكيك.
8. **التراجع مرتّب عكسيًا:** أي `PR` قابل للتراجع وحده **ما لم تُدمج تابعاته بعده** — عند الحاجة يُتراجع بالترتيب العكسي للدمج.

---

## 5) المخاطر والتقدير

- الخطر الأكبر: سلوك دقيق في آلة الحالات/الوراثة لا تغطيه الاختبارات — يُخفَّف بعيّنة التغطية والتوصيف للفجوات (المرحلة 4).
- التقدير الكلي: ~1.5–2 أسبوع متقطع مع البوابات (0: ساعات، 1: يوم شامل المصنع، 2: نصف–يوم، 3: يومان، 4: ‏2–3 أيام، 5: نصف يوم).
- أثر معلن: `DocumentsController` يكتسب +3 تبعيات (إجراءات/بحث/حالات) والاختبارات تتغير ميكانيكيًا كل مرحلة — لا أثر على المسارات أو الواجهة.

---

## 6) قائمة تحقق مقابل المطلوب

- [ ] تفكيك كامل لا تجريبي فقط، مع كسر نظيف لـ`IDocumentService`.
- [ ] نواة **داخل الصنف أولًا** (لا `DI` ولا كسر باني مبكر — تصحيح عيبي الترجمة والترتيب).
- [ ] بيت المساعدات المشتركة محسوم ومطبَّق (خالصات/`static` + حراس/`WriteGuard` + سنة/`ServerClock`).
- [ ] قوائم الاعتمادات الدقيقة مطبَّقة لكل خدمة (بحث: بلا `tx`/`uow`؛ حالات: مع `_alertService`؛ إنابة: مع `_uow` بلا `_tx`).
- [ ] عقود `XML` لسياق المعاملة على كل دالة عامة حساسة (داخل/بعد) + مصنع بمعاملات مسماة.
- [ ] ملفات النواة الثلاثة تستبق البيوت + شرط الإيقاف + حسم وضع وثيقة الاكتشاف.
- [ ] مصنع اختباري **قبل** أول تغيير باني.
- [ ] الإنابة قبل الحالات (تبعية 5 مواقع) مطبَّق.
- [ ] بيت لكل يتيم (`ApplyRegistrationDate` ← النواة، `SyncDelegationSnapshots` ← المرحلة 3، النوعان الخاصان ← يبقيان داخل خدمة الإنابة).
- [ ] تعليقا التأجيل (`Actions` + `Status`) مُنهيان والأسماء موحَّدة على `IDocumentStatusService`.
- [ ] ثابتا المعاملة و`Scoped` منصوصان ومفروضان بقائمة فحص + مواضع داخل/خارج المعاملة محفوظة + منع `WhenAll`.
- [ ] زوج `GetDeleted`/`SearchDeleted` موثَّق التقسيم (مفردة/ترحّلي).
- [ ] عيّنة تغطية قبل فرض اختبارات التوصيف.
- [ ] صفر هجرات، صفر مسارات متغيرة، صفر مساس بالواجهة في كل المراحل.
