# خطة إصلاح هوية رقم الملف — القيد والتدوير والشطب والتجديد

> **حالة الخطة:** مثبتة — لا تنفيذ قبل موافقة صاحب القرار  
> **تاريخ التثبيت:** 2026-09-14  
> **آخر دمج لملاحظات المراجعة:** 2026-09-14 — دُمجت نتائج المراجعة التحليلية الشاملة والتحليل الخبير في هذا النص، ثم دُمجت التنقيحات F1–F4، وما زال التنفيذ موقوفًا  
> **قاعدة المراجع:** تُذكر الرموز (الدوال/الفروع) أساسًا، وأرقام الأسطر استرشادية قد تنزاح مع أي تعديل  
> **النطاق:** إصلاح وجودي — أي خطأ يورث مشكلة قانونية خطيرة  
> **وضع التنفيذ:** Build Mode مفعّل لكن التنفيذ موقوف بطلب صريح

---

## 1) المبادئ التشغيلية المثبتة (مرجع المراجعة)

1. `FileNumber + FileYear + FileType` عند `إنشاء / تحت رفع → متداول` هي هوية القيد الأصلي. `FileType` غير إلزامي.
2. كل سنة جديدة = تدوير قضائي يعطي `DocumentBaseNumber(Year, BaseNumber)` جديدا مرتبطا بالسنة الجديدة.
3. أرقام السنوات السابقة توثيقية فقط. الهوية القانونية المعتبرة = **آخر رقم فعال** (ليس بالضرورة `Today.Year` ولا `FileNumber`).
4. التجديد بعد الشطب قد يعطي رقما جديدا **في نفس السنة**. الجديد هو المعتبر والقديم توثيقي.
5. `مشطوب → منفذ` لا يجوز مباشرة. يجب المرور بالتجديد إلى `متداول` أولا.
6. الملف المنيب والمناب في دائرتين مختلفتين — تدويران مستقلان. كذلك الملف والاستئناف — تدويران مستقلان. المطلوب فقط أن **العرض المرجعي** لكل كيان في صفحة الآخر يتحدث تلقائيا عند التدوير.

---

## 2) التشخيص المعماري الحالي

- الأصل ثابت: `backend/src/DocGenerator.Domain/Entities/Document.cs:114-116` (`FileNumber/FileYear/FileType`) + `backend/src/DocGenerator.Domain/Entities/DocumentBaseNumber.cs:8`
- التداول السنوي: `DocumentBaseNumbers` بفهرس فريد `DocumentId+Year` في `backend/src/DocGenerator.Infrastructure/Persistence/Configurations/Configurations.cs:394` (فئة الإعداد عند `:387`) — رقم واحد لكل سنة كحد أقصى.
- التجديد يكتب في نفس الجدول: `ApplyRenewalAsync` في `backend/src/DocGenerator.Application/Services/DocumentService.Status.cs:562` يبحث `FirstOrDefault(Year==year)` فإن وجد يحدثه (`:575-580`) وإلا ينشئه (`:563-573`).
- العرض الحالي: `CurrentBaseNumberOf` في `backend/src/DocGenerator.Application/DTOs/DocumentDtos.cs:980` = `BaseNumbers[Today.Year]` فقط ثم `FileNumber`. كل الواجهات تستخدمه: `DocumentDtos.cs:811` و `DocumentContextBuilder.cs:70` و `DelegationService.cs:898` و `ExcelExportService.cs:159` و `frontend/src/utils/documentDisplay.ts:62` و `frontend/src/components/view/viewFormat.ts:50`.
- المصدر المرجعي للإنابة ديناميكي أصلا لكن بنفس منطق `Today.Year` فقط: `DelegationService.cs:898` + `DelegationRepository.cs:19,41,55`
- رقم الملف في الاستئناف غير ديناميكي: `DocumentAppealService.cs:1013` يقرأ `Document.FileNumber` مباشرة.
- رقم الاستئناف ديناميكي لكن بنفس منطق `Today.Year` فقط: `DocumentAppealService.cs:1048`

**الخلاصة:** الهوية مبعثرة بين ثلاث طبقات بلا محلل مركزي، والتجديد والشطب لا يطبقان مبدأ `الآخر هو المعتبر`.

---

## 3) المراجعة مقابل المبادئ — الانتهاكات المثبتة

| المبدأ | الانتهاك | الدليل | الأثر القانوني |
|---|---|---|---|
| الهوية = آخر رقم | العرض يبحث عن `Today.Year` فقط ويسقط إلى `FileNumber` | `DocumentDtos.cs:980` و `Search.cs:147` و `DocumentView.tsx:396` و `FileDataCard.tsx:31` | ملف `2024/55` دوّر `2025/60` ولم يدوّر بعد في `2026` يعرض `55` بدل `60` المعتبر. تصدير وقوالب وإنابات تعرض رقما منتهيا |
| تجديد بنفس السنة | `BaseNumbers` فريد لكل سنة + التجديد يحدث السجل نفسه | `Configurations.cs:394` و `Status.cs:562/575-580` | تجديد `2026/100 → 2026/200` يمحو `100` من تاريخ الأرقام. تفقد التوثيق لنفس السنة |
| تجديد يعطي رقما جديدا هو المعتبر | `ApplyRenewalAsync` لعائلة `منفذ عليها/عرض وايداع` يثبت `Today.Year` ويتجاهل `RenewalYear` المدخلة بصمت، والواجهة تعرض حقل سنة قابلًا للتحرير يُرسَل ثم يُتجاهل (`ExecutedStatusModal:152` يرسل `renewalYear` والخلفية تهمله) | `Status.cs:530-533` + `RenewalFields.tsx:81-93` | مستخدم يدخل `2025` فيحفظ `2026` ويظن أنه جدد لـ `2025`. لا تطابق بين `Year` و `RenewalDate.Year` |
| الشطب يحفظ الرقم الفعال | وقعة الشطب تخزن `doc.FileNumber` الأصلي (والتصميم المعلّق يصرّح بذلك) | `Status.cs:608/615` وفرع طالبة تنفيذ `:164-165` عبر `DocumentService.cs:248-249` | شطب ثان بعد `2026/55` يسجل `55`. سطر الشطب في الواجهة لا يعرض الرقم أصلًا (`viewFormat:311-313`) فيُخفى الخطأ بدل إظهاره |
| الشطب يحفظ سنة الحدث | `UpdateAsync` يعيد استخدام `StruckOffDate` القديم عند شطب جديد بلا تاريخ | `DocumentService.Apply.cs:318` مقابل `Status.cs:386-389` الصحيح | شطب ثان بتاريخ فارغ يحمل `2024` بدل `2026` |
| مشطوب → منفذ عبر التجديد | `targetsOf` تتيح `مشطوب→منفذ` مباشرة بلا تجديد | `ExecutedStatusModal.tsx:23` و `Status.cs:440-458` (فرع «منفذ» لا ينفذ تجديدًا ولا وقعة) | عودة مشطوب إلى منفذ دون `BaseNumber` أو وقعة تجديد |
| الوقوعات تاريخ لا أمر | `OccurrencesEditor.tsx:297` (خيار «تجديد») و`:148` يسمح بإنشاء `renewal` يدويا دون تنفيذ أمر الاستعادة | `DocumentService.Actions.cs:135` (`AddOccurrenceAsync`) لا يمس الملف | ملف مشطوب مع وقعة تجديد يدوية يبقى مشطوبا. بطاقة رقم الملف تبقى قديمة |
| العرض المرجعي للإنابة/الاستئناف | رقم المناب **غير معروض إطلاقًا** في بطاقة المنيب (`DelegationDetails` تعرض الدائرة/الفرع/الأموال فقط) — إضافة جديدة لا إصلاح؛ ورقم الملف في الاستئناف جامد | `DelegationsCard.tsx` + `DelegationDetails.tsx` و `DocumentAppealService.cs:1013` | تدوير المناب لا يظهر في المنيب. تدوير الملف لا يظهر في الاستئناف |
| الهوية = آخر رقم | صفحة التدوير تعرض `FileNumber` الأصلي مرجعًا بينما كل الأسطح ستعرض الفعّال | `Rotation.tsx:11/134/171` و `RotationDocumentDto:474-483` | مدقق التدوير يرى `55` والقوائم ترى `60` — مرجع مضلل لاختيار الرقم التالي |

---

## 4) الحل البرمجي القياسي

### 4.1 ثابت واحد غير قابل للكسر — المحلل المركزي

```
EffectiveFileIdentity(doc, asOf = Today) =
  1. أحدث BaseNumbers حيث Year <= asOf.Year مرتبا بـ Year تنازليا ثم CreatedAt تنازليا
  2. وإلا FileNumber
EffectiveYear = سنة السجل المختار أو FileYear
لا يستخدم رقم مستقبلي قبل حلول سنته. لا يخلط رقم سنة مع سنة قيد.
```

يستبدل كل تكرار `BaseNumbers.FirstOrDefault(Year==Today.Year)` في الخلفية والواجهة بهذا المحلل.
يبقى `FileNumber/FileYear` للتحرير والتوثيق الأصلي.
يغطي: `DocumentResponse.DisplayFileNumber/DisplayFileYear` و `DelegationDto.SourceFileNumber/SourceFileYear` و `TargetDisplay` (حقل جديد لرقم المناب) و `AppealDto.DocumentEffectiveNumber` (من أرقام أساس الملف نفسه لا الاستئناف) و `RotationDocumentDto` (حقلا الرقم/السنة الفعّالين مرجعًا في صفحة التدوير) و `DocumentContextBuilder` (بما فيه `file_number_full` عند `:77-79` وموضعا القالب 004 عند `:298,307`) و `ExcelExport` و `DelegationService` و `ReviewLetterService.cs:510` (سياق كتب المراجعة — عرض حيّ لا لقطة).

**قاعدة الفصل (ملزمة للمنفذ):** العرض الحيّ يُحلَّل مركزيًا دائمًا؛ ولقطة اللحظة (نصوص رسائل الإشعارات وقت الإنشاء مثل `DocumentAppealService.cs:876`، وسجلات التدقيق) تُحفظ كما هي ولا تُمس — «إصلاح» اللقطات إفساد للتاريخ.

**تدقيق `Include` الإلزامي قبل أي كود:** كل مستودع يغذي DTO يعرض رقمًا يجب أن يجلب `BaseNumbers` صراحةً: استعلام الاستئناف (`Document.BaseNumbers` لحساب رقم الملف الفعّال في `ToDto`)، `ExportAsync`، `GetRotationCandidatesAsync`، قوائم البوابة (`PortalService` يمر عبر `FromEntity` فلا تعديل مستقل له)، `ListBySourceAsync`/`GetByIdWithDetailsAsync` للإنابات (تشمل `TargetDocument.BaseNumbers` — غير موجودة اليوم). الاحتياطي `?? FileNumber` يتدهور بصمت عند أي `Include` ناقص، لذا يُضاف تسجيل تشخيصي (أو `Debug.Assert`) عندما يملك الملف أرقام أساس ولا يُطابق أيٌّ منها — حتى لا يبقى التدهور صامتًا أبدًا.

### 4.2 إصلاح وقعة الشطب — تخزين الهوية الفعالة بتاريخ الشطب

```
FileNumber في وقعة الشطب = EffectiveFileIdentity(doc, asOf = StruckOffDate.Year)
Year = StruckOffDate.Year
FileType = FileType الحالي
```

يطبق في:
- `backend/src/DocGenerator.Application/Services/DocumentService.Status.cs:606` (`AddStruckOffOccurrenceAsync` — التخزين عند `:608/615`)
- `backend/src/DocGenerator.Application/Services/DocumentService.Status.cs:164` (فرع طالبة تنفيذ — التخزين عند `:164-165`)
- تحديث التعليق التصميمي في `Status.cs:600-604` («الرقم الأصلي») ليعكس السلوك الجديد، وإلا بقي التوثيق كاذبًا.
- **العرض (إلزامي مع التخزين وإلا بقي الإصلاح غير مرئي):** فرع `struck-off` في `occurrenceLine` (`frontend/src/components/view/viewFormat.ts:311-313`) يتضمن `رقم: {fileNumber} — لعام {year}` عند توفرهما، فيظهر في بطاقة الوقوعات والنافذة التفصيلية معًا (كلتاهما تستخدمان `occurrenceLine` — `OccurrencesModal.tsx:3,72`).

> قرار معتمد: الشطب الأول قبل أي تدوير يعطي `Effective == FileNumber` أصلًا، فالتغيير السلوكي محصور في «شطب بعد تجديد/تدوير» وهي الحالة المعطوبة اليوم.

### 4.3 إصلاح مسار التجديد اليدوي — السجل لا يغير الحالة بصمت

**الواجهة:** `frontend/src/components/form/OccurrencesEditor.tsx`
- يستقبل `executedStatus/ExecStatus` و `generalEntitySide`
- عند اختيار `renewal` وكان الملف مشطوبا: لا يرسل إلى `POST /documents/{id}/occurrences` بل يبني `RenewalRequest` من حقول الوقعة ويرسل إلى `POST /documents/{id}/restore-struck-off` ثم يعيد تحميل المستند والوقوعات
- رسالة صريحة بأن التجديد للمشطوب يعني إعادته إلى متداول

**الخلفية دفاعيا:** `backend/src/DocGenerator.Application/Services/DocumentService.Actions.cs:135` (`AddOccurrenceAsync`) و`:152` (`UpdateOccurrenceAsync`)
- رفض **إنشاء** وقعة `renewal` جديدة أو **تحويل** وقعة قائمة إلى `renewal` بينما الملف لا يزال مشطوبا فقط
- **يبقى مسموحًا تعديل** وقعة تجديد نظامية موجودة (المُنشأة من `ApplyRenewalAsync` عند `Status.cs:584-597`) حتى لو أُعيد شطب الملف لاحقًا — إصلاح تاريخي لا يغيّر الحالة

> ملاحظة الصلاحيات: `POST /occurrences` = `CanEdit`، `POST /restore-struck-off` = `CanDelete`، `POST /executed-status` = `CanChangeStatus` في `backend/src/DocGenerator.Api/Controllers/DocumentsController.cs:47`. لا يجوز لنقطة السجل أن تغير الحالة بصمت.

### 4.4 إصلاح تناقض سنة الإعادة (قرار معتمد: إخفاء + تثبيت + رفض دفاعي — لا اشتقاق من تاريخ التجديد)

- إخفاء حقل `سنة الإعادة` لعائلة `منفذ عليها/عرض وايداع` وعرض `سنة اليوم` كنص ثابت. إبقاؤه إلزاميا لـ `طالبة تنفيذ`. — عبر خاصية صريحة في `RenewalFields` (مثل `hideYear`): إخفاء دائم في `ExecutedStatusModal` و`ExecutedSideSections`، ومشروط بالعائلة في `RenewalModal`.
  - `frontend/src/components/form/RenewalFields.tsx:81-93` + فرع بناء حمولة التجديد في `frontend/src/components/ExecutedStatusModal.tsx:147-156` + `frontend/src/components/RenewalModal.tsx` (ملف جديد غير مرفوع — يُراجَع حقله قبل الاعتماد)
- إبقاء `Today.Year` في فرع `executedLike` (`Status.cs:530-533`) + رفض دفاعي في الخلفية إذا وردت `RenewalYear` مخالفة لسنة اليوم بدل تجاهلها بصمت
- لا اشتقاق للسنة من `RenewalDate.Year`: ذلك سلوك جديد خفيّ (تجديد بتاريخ متأخر يسقط في سنة ماضية) لم يُقرَّر — حُذف هذا البديل بوعي
- لا تغيير لتمرير `executedLike=true` في `DocumentService.cs:246-247`: الفرع لا ينطلق إلا لملف مشطوب، والمشطوب حصرًا من تلك العائلة (`UpdateExecutedStatusAsync:327` تمنع غيره)، فالثابت مكافئ تمامًا للاشتقاق — حُذف هذا البند بوعي
- تطابق `RenewalDate.Year == RenewalYear` عند تقديمهما معا في نظام «طالبة تنفيذ» (وكذلك في `ApplyOccurrence`) — يُقبل اليوم تاريخ 2024 مع سنة 2025 بصمت وهذا خطأ حقيقي

### 4.5 إصلاح إعادة استخدام تاريخ الشطب

حفظ `StruckOffDate` السابق قبل `ApplyRequest` في `UpdateAsync` (`backend/src/DocGenerator.Application/Services/DocumentService.cs:237-239` — الالتقاط قبل التطبيق، وبداية الدالة عند `:222`) ثم:
- إن كان الانتقال جديدا إلى مشطوب وتاريخ الإدخال فارغ → `UtcNow`
- إن كان مشطوبا أصلا → حافظ على السابق ما لم يدخل تاريخ صريح
- يوحد سلوك `DocumentService.Apply.cs:318` مع `Status.cs:386-389`

### 4.6 منع `مشطوب → منفذ` المباشر — واجهة + حارس خلفي (الباب الخلفي عبر نموذج التعديل مغلق هنا)

الخلفية تطبق `r.ExecutedStatus` من نموذج التعديل مباشرة (`Apply.cs:311-317`) والقائمة فيه تعرض الخيارات الثلاثة دون قيد (`ExecutedSideSections.tsx:594-605`)، و`UpdateAsync` لا يعالج إلا `مشطوب→متداول` (تجديد) و`→مشطوب` (وقعة) — فيجب الإغلاق من الجهتين:

**الواجهة:**
- حذف `منفذ` من `targetsOf("مشطوب")` في `frontend/src/components/ExecutedStatusModal.tsx:23` (فرع «منفذ» في `Status.cs:440-458` لا ينفذ تجديدًا أصلًا).
- ترشيح خيارات قائمة «الحالة» في نموذج التعديل (`ExecutedSideSections.tsx:594-605`) حسب الحالة الحالية بمنطق `targetsOf` نفسه: من `مشطوب` تُعرض `متداول` (مع التجديد) و`مشطوب` فقط؛ ومن `منفذ` في عائلة «منفذ عليها» تُقفل القائمة (نهائية)؛ ومن `منفذ` في «عرض وايداع» تُقفل القائمة في النموذج كذلك (خيار «متداول» في النافذة حصرًا — توضيح لا سلوك جديد، فالحارس الخلفي يرفض المباشر أصلًا). (`targetsOf` غير مُصدَّرة اليوم — تُصدَّر من `ExecutedStatusModal` أو تُنقل لأداة مشتركة.)

**الخلفية (حارس انتقالات في `UpdateAsync`/`ApplyRequest` يطابق قواعد النافذة — لا يُعتمد على الواجهة وحدها):**
- رفض `مشطوب → منفذ` المباشر (يجب المرور بالتجديد إلى `متداول` أولًا — المبدأ 5).
- رفض الخروج من `منفذ` في عائلة «منفذ عليها» (نهائية — يطابق `Status.cs:340-343`).
- رفض `منفذ → مشطوب` في «عرض وايداع» (يطابق `Status.cs:345-348`)؛ وإرجاع «عرض وايداع» من `منفذ` إلى `متداول` عبر النافذة حصرًا (كتاب السير + وقعة التراجع) — يُرفض المباشر عبر التعديل — قرار معتمد: رفض وإجبار النافذة (لا قبول مشروط داخل التعديل).
- النطاق: عائلة «منفذ عليها» فقط — `ApplyRequest` لا يسند `ExecStatus` إطلاقًا و«طالبة تنفيذ» محكومة بآلة حالات مغلقة (`StateStruckOff → {}` في `ExecutionStatusCatalog:99`).

### 4.7 البطاقات والعقود (بلا كسر)

- إضافة `DisplayFileYear` إلى `DocumentResponse` بجانب `DisplayFileNumber` (`string?`)
  - `backend/src/DocGenerator.Application/DTOs/DocumentDtos.cs:627`
- تحديث `frontend/src/components/view/viewFormat.ts:50` و `frontend/src/components/view/FileDataCard.tsx:83` لتستخدم السنة المرافقة للرقم الفعال — ومعهما شريحة «السنة» في `frontend/src/pages/DocumentView.tsx:401` و«سنة الملف» في `frontend/src/pages/PortalFileDetail.tsx:77` (البوابة تمر عبر `FromEntity` فيكون رقمها فعالًا) — لئلا تُعرض سنة أصلية بجانب رقم فعال
- توحيد `DisplayFileYear` في `DelegationDto` و `AppealDto.DocumentEffectiveYear` و `DocumentContextBuilder` و `ExcelExport` — قرار معتمد: عمود جديد باسم `لعام` بعد عمود `رقم الملف` مباشرة في `BaseColumns` (`ExcelExportService.cs:29-33`) يحمل سنة الرقم الفعّال (تغيير عقد خارجي موثق — رقم بلا سنة غامض قانونيًا)
- صفحة التدوير تعرض الرقم/السنة الفعّالين مرجعًا: حقلان جديدان في `RotationDocumentDto` يُحسبان في `GetRotationListAsync` (`BaseNumbers` محمّلة أصلًا) ويُعرضان في `Rotation.tsx` بدل `fileNumber` الأصلي
- قيد شكلي: الحقول الجديدة في السجلات الموضعية (`DelegationDto`، `AppealDto`، `RotationDocumentDto`) تُلحق في النهاية بقيم افتراضية (ومواقع البناء وحيدة لكل منها)، والواجهة اختيارية أصلًا — آمن عقديًا بلا كسر

### 4.8 العرض المرجعي للإنابات والاستئنافات (تثبيت طلبك الأخير)

| الموضع | قبل | بعد |
|---|---|---|
| مناب → يعرض رقم المنيب | ديناميكي لكن `Today.Year` فقط | نفس الديناميكية لكن عبر `EffectiveFileIdentity` |
| منيب → يعرض رقم المناب | **غير معروض إطلاقًا** اليوم — إضافة جديدة لا إصلاح | حقل عرض جديد في `DelegationDto` يُبنى من `TargetDocument.EffectiveFileIdentity` مع `Include(d => d.TargetDocument).ThenInclude(t => t!.BaseNumbers)` في `ListBySourceAsync` و`GetByIdWithDetailsAsync`، ويُعرض في `DelegationDetails` |
| استئناف → يعرض رقم الملف | `FileNumber` مباشرة | `EffectiveFileIdentity` للملف **من أرقام أساس الملف نفسه** (لا الاستئناف) — مع التأكد أن استعلام الاستئناف يجلب `Document.BaseNumbers` |
| ملف → يعرض رقم الاستئناف في بطاقة الوقوعات | `Today.Year` فقط | `أحدث ≤ Today` |

كلها تبقى **عرضا مرجعيا فقط** — تدوير كل كيان يبقى مستقلا بدائرته/نظامه.

### 4.9 قابلية البحث بالرقم الجديد (تعارض مغلق هنا)

`SearchText` يضم `FileNumber` الأصلي فقط (`DocumentSearchTextBuilder:18-22`) ولا يُعاد بناؤه عند التدوير/التجديد، وبحث الاستئناف يطابق `AppealBaseNumber` الأصلي فقط (`AppealRepository:58-66`) — فيرى المستخدم الرقم الجديد ولا يجده بالبحث (القوائم والبوابة كلها عبر `SearchText`). الإصلاح:
- تضمين كل أرقام الأساس (`BaseNumbers`) **في مقدمة** `DocumentSearchTextBuilder.Build` مرتبة بالأحدث أولًا — فينجو الرقم المعتبر من `Truncate(1000)` عند اقتراب النص من الحد (البحث `Contains` بلا ترتيب نتائج فلا أثر للترتيب).
- إلحاق الرقم الجديد بالنص القائم (مع `Truncate`) عند التدوير (`SaveBaseNumbersAsync`) والتجديد (`ApplyRenewalAsync`) — لا إعادة بناء كاملة لأن سياق التدوير يجلب `BaseNumbers` فقط (`DocumentRepository:491`) بينما `Build` يحتاج الكفلاء/الورثة/الجهات. وقبول مقصود: إلغاء رقم السنة (`Search.cs:255-263`) أو تصحيحه (`:285-292`) يُبقي المصطلح السابق في النص (فهرس فوقيّ حميد — يجد الملف الصحيح نفسه والواجهة تعرض الهوية الحالية) بلا إعادة بناء ثقيلة في مسار الحفظ الجماعي.
- توسيع بحث الاستئناف: `a.BaseNumbers.Any(b => b.BaseNumber.Contains(term))`.
- تعبئة أثرية لمرة واحدة للأرقام المُدوَّرة منذ إطلاق الميزة: **مؤجلة للمرحلة 5** (قرار معتمد) — إلحاق إضافي منخفض الخطر ضمن حزمة التسوية.

---

## 5) مشاكل مرتبطة ستصلح ضمن نفس الإنجاز

- مصدر رقم المنيب في الإنابات `DelegationService.cs:898` سيستخدم المحلل المركزي فلا يتغير رقم الورق المسطر بتغير السنة إلا بالحل الصحيح. ورسالة الإتمام `targetLabel` (`:549/583-584`) تتحدث تلقائيًا بالمحلل نفسه.
- `occurrenceLine` للشطب في `frontend/src/components/view/viewFormat.ts:311` يتضمن الرقم والسنة (بند 4.2) فتظهر النافذة التفصيلية رقمًا صحيحًا بعد إصلاح التخزين — التخزين وحده لا يكفي.
- تعليق `UpsertOccurrenceRequest` في `DocumentDtos.cs:528` يحدث ليعكس الأنواع الستة الفعلية.
- `DocumentContextBuilder.cs:70` و `ExcelExportService.cs:159` يتوحدان على نفس المحلل؛ `PortalService` يمر عبر `DocumentResponse.FromEntity` (`PortalService.cs:102/133`) فلا تعديل مستقل له.

---

## 6) ما يؤجل بوعي — لا يدخل هذا الإنجاز

1. **تعدد أرقام بنفس السنة مع حفظ كل التوثيق:** يتطلب إزالة القيد الفريد `DocumentId+Year` والسماح بإنشاء سجل جديد دائما مرتبا بـ `CreatedAt`. يحفظ `100` و `200` معا ويختار `200` كفعال. يوجب هجرتين `SQLite + Postgres` وتغيير `SaveBaseNumbersAsync` و `ApplyRenewalAsync`. يبقى للمرحلة التالية لأن الـ occurrence الحالية تحفظ التوثيق المفقود مؤقتا. (كسر التعادل بـ `CreatedAt` في المحلل لا أثر له قبل هذه المرحلة — سجل واحد لكل سنة — وهو تحضير لها فقط.)
2. **وسم مصدر الوقعة `نظامي/يدوي` ومنع تعديل/حذف الوقوعات النظامية:** يتطلب عمودا جديدا وهجرة. يبقى كتقسية لاحقة.
3. **توحيد `Today/Now/UtcNow` وزمن العميل حول رأس السنة:** تقسية لاحقة (خطر منخفض مقارنة بالهوية).

---

## 7) تسوية البيانات التاريخية

إصلاح الكود لا يصحح وقوعات شطب ثانية مخزنة بـ `55` بدل `2026/55`.
يلزم قبل أي تصحيح — كتابة الاستعلام فعليًا لا فقرة نية:
1. استعلام تدقيق مكتوب (EF/`SQL`) بمعيار صريح: وقعة `struck-off` تالية لوقعة `renewal` في نفس الملف حيث `FileNumber !=` آخر `BaseNumber` قبل الشطب — **ويشمل مساري الشطب معًا**: `AddStruckOffOccurrenceAsync` (عائلة منفذ عليها) وفرع طالبة تنفيذ (`UpdateStatusAsync`) — وبترتيب معلن (`EventDate` أم `CreatedAt`) مع معالجة `RenewalDate` الفارغ (`EventDate` قد يكون `null`)
2. تصدير جاف للمراجعة البشرية
3. تصحيح الحتمي فقط — لا هجرة بيانات آلية قبل الاعتماد
4. لا هجرة مخطط في مرحلته الأولى — يعتمد على الأعمدة والجداول القائمة مع حقل عرض مشتق فقط
5. تعبئة `SearchText` الأثرية (بند 4.9): مؤجلة للمرحلة 5 (قرار معتمد) — إلحاق أرقام الأساس القائمة بنصوص البحث ضمن حزمة التسوية

---

## 8) خطة الاختبارات والتحقق الإلزامي

### خلفية `dotnet test`
- الهوية الفعالة: أصل فقط / أساس سنة حالية / أحدث أساس لسنة سابقة مع تجاهل المستقبلي (`Year > asOf` لا يظهر أبدًا)
- شطب ثان بعد تجديد يخزن المجدّد في مساري `منفذ عليها` و `طالبة تنفيذ`؛ وشطب ثانٍ بتاريخ صريح يسجل سنة الشطب الجديدة
- رفض إنشاء/تحويل `renewal` يدوي على مشطوب + السماح بتعديل وقعة التجديد النظامية + رفض `RenewalYear` مخالفة لسنة اليوم في عائلة منفذ عليها + تطابق `RenewalDate.Year == RenewalYear` لطالبة تنفيذ
- العرض المرجعي: منيب في مناب، مناب في منيب (بعد `Include` — بقيمة فعلية لا مجرد عدم فراغ)، ملف في استئناف، استئناف في ملف — كلها تتحدث بعد تدوير الطرف الآخر
- رفض `مشطوب→منفذ` و`منفذ→*` المباشر عبر `UpdateAsync` + رفض إرجاع «عرض وايداع» المباشر عبر التعديل
- البحث برقم أساس مُدوَّر (ملف واستئناف) يجد الملف
- الرقم المعتبر قابل للبحث حتى قرب حد `MaxLength` (ترتيب الأحدث أولًا في المقدمة)
- قائمة التدوير تعرض الرقم/السنة الفعّالين مرجعًا
- **استراتيجية كشف السقوط الصامت:** التأكيد على القيم الفعلية للأرقام بعد تدوير، والتشغيل على `SQLite` (لا `InMemory` وحده — الأخير لا يكشف الـ`Include` الناقص)، واختبار صريح لكل مسار يعرض رقمًا

### واجهة `npx vitest run` + `npx oxlint src` + `npx tsc -b` + `npm run build`
- لا كسر عقود — `DisplayFileYear` حقل جديد اختياري
- بطاقة `FileDataCard` تظهر سنة الرقم الفعال
- شريحة «السنة» في `DocumentView` و«سنة الملف» في البوابة تعرضان سنة الرقم الفعال
- سطر الشطب في `occurrenceLine` يعرض الرقم والسنة المخزنين
- `OccurrencesEditor` على مشطوب يرسل إلى `restore-struck-off` ويعيد التحميل
- `RenewalFields` تخفي سنة الإعادة لعائلة منفذ عليها
- منع `مشطوب→منفذ` المباشر في الواجهة
- قائمة «الحالة» في نموذج التعديل تُرشَّح حسب الحالة الحالية (لا `منفذ` من `مشطوب`، وقفل القائمة من `منفذ` النهائي وفي «عرض وايداع» المنفذ — الإرجاع عبر النافذة)
- صفحة التدوير تعرض الرقم الفعّال مرجعًا بدل الأصلي

### تدقيق المسار الكامل قبل الإنجاز
1. تدقيق العقود عبر المسار كاملا: واجهة → DTO → Service → قاعدة → عرض/إحصاءات
2. فحص بقايا المصطلحات/الأسماء القديمة عبر `rg`
3. قائمة تحقق مقابل هذه الخطة
4. التأكد من عدم كسر الاصطلاحات (Intl.NumberFormat وغيرها)

---

## 9) المراحل التي يجب تنفيذها بعد الانتهاء من هذه الخطة

### المرحلة 2 — حفظ التوثيق الكامل لنفس السنة (هجرة)
- إزالة القيد الفريد `DocumentId+Year` والسماح بسجلات متعددة لنفس السنة مرتبة بـ `CreatedAt` — **وتماثله في الاستئنافات**: إزالة القيد الفريد `(AppealId, Year)` (`Configurations.cs:903`) بالآلية نفسها
- تعديل `ApplyRenewalAsync` و `SaveBaseNumbersAsync` (وما يقابلهما للاستئناف عند `AppealService:478`) لإنشاء سجل جديد دائما بدل التحديث
- تحديث `BuildBaseNumberHistory` و `BaseNumbersModal` لعرض كل الأرقام لنفس السنة
- هجرات: `SQLite` + `Postgres` تشمل `DocumentBaseNumbers` و`AppealBaseNumbers` + تحديث `Migrations` + تطبيق `dotnet ef database update` للسياقين عند النشر

### المرحلة 3 — تقسية سجل الوقوعات
- إضافة عمود `Source` (`system/manual`) لتمييز الوقوعات النظامية عن اليدوية
- منع تعديل/حذف الوقوعات النظامية من `OccurrencesEditor` أو تقييدها بصلاحية أعلى
- مراجعة سطر وقعة الشطب المختصر بعد تثبيت الرقم الصحيح في §4.2 (البطاقة والنافذة تشتركان في `occurrenceLine` نفسه)
- تحديث توثيق `UpsertOccurrenceRequest` واختباراته

### المرحلة 4 — توحيد الزمن والتدوير حول رأس السنة
- طبقة زمنية موحدة (`IClock`/`IDateProvider`) بدل `Today/Now/UtcNow` المبعثرة
- حقن السنة الحالية من نفس المصدر في الخلفية والواجهة لتجنب اختلاف `getFullYear()` للعميل عن `Today.Year` للخادم
- معالجة `FILE_YEARS` الثابتة في `documentFormConstants.ts:14` لتكون ديناميكية

### المرحلة 5 — تدقيق قانوني وتصدير
- تشغيل استعلامات التدقيق على البيانات التاريخية وتصدير الحالات المشتبه بها
- تعبئة `SearchText` الأثرية المؤجلة من بند 4.9 (إلحاق أرقام الأساس القائمة بنصوص البحث)
- مراجعة بشرية وتصحيح انتقائي
- تحديث `RUN_GUIDE.md §9` بإجراءات تطبيق الهجرات عند النشر

---

## 10) الملفات المتأثرة (للتنفيذ بعد الموافقة)

**خلفية:**
- `backend/src/DocGenerator.Application/DTOs/DocumentDtos.cs`
- `backend/src/DocGenerator.Application/DTOs/DelegationDtos.cs`
- `backend/src/DocGenerator.Application/DTOs/AppealDtos.cs`
- `backend/src/DocGenerator.Application/Services/DocumentService.Status.cs`
- `backend/src/DocGenerator.Application/Services/DocumentService.cs`
- `backend/src/DocGenerator.Application/Services/DocumentService.Apply.cs`
- `backend/src/DocGenerator.Application/Services/DocumentService.Actions.cs`
- `backend/src/DocGenerator.Application/Services/DocumentService.Search.cs`
- `backend/src/DocGenerator.Application/Services/DocumentDelegationService.cs`
- `backend/src/DocGenerator.Application/Services/DocumentAppealService.cs`
- `backend/src/DocGenerator.Application/Services/DocumentContextBuilder.cs`
- `backend/src/DocGenerator.Application/Services/ExcelExportService.cs`
- `backend/src/DocGenerator.Application/Common/DocumentSearchTextBuilder.cs` (تضمين أرقام الأساس)
- `backend/src/DocGenerator.Application/Services/ReviewLetterService.cs` (سياق كتب المراجعة — عرض حيّ)
- `backend/src/DocGenerator.Infrastructure/Persistence/DelegationRepository.cs` (جلب `TargetDocument.BaseNumbers`)
- `backend/src/DocGenerator.Infrastructure/Persistence/AppealRepository.cs` (تأكيد جلب `Document.BaseNumbers` لاستعلامات `ToDto`)
- `backend/tests/DocGenerator.Application.Tests/OccurrenceTests.cs`
- `backend/tests/DocGenerator.Application.Tests/ServiceTests.cs`

**واجهة:**
- `frontend/src/components/form/RenewalFields.tsx`
- `frontend/src/components/form/ExecutedSideSections.tsx` (ترشيح خيارات الحالة)
- `frontend/src/components/ExecutedStatusModal.tsx`
- `frontend/src/components/RenewalModal.tsx`
- `frontend/src/components/form/OccurrencesEditor.tsx`
- `frontend/src/components/view/FileDataCard.tsx`
- `frontend/src/components/view/viewFormat.ts`
- `frontend/src/utils/documentDisplay.ts`
- `frontend/src/components/delegation/SourceFileInfoCard.tsx`
- `frontend/src/components/delegation/DelegationsCard.tsx`
- `frontend/src/components/delegation/DelegationDetails.tsx` (موضع عرض رقم المناب الجديد)
- `frontend/src/components/appeal/AppealDetailsBody.tsx`
- `frontend/src/pages/DocumentView.tsx`
- `frontend/src/pages/PortalFileDetail.tsx` (شريحة «سنة الملف» — سنة الرقم الفعال)
- `frontend/src/pages/AppealsList.tsx`
- `frontend/src/pages/Rotation.tsx` (عرض الرقم الفعّال مرجعًا)
- `frontend/src/pages/Rotation.test.tsx`
- `frontend/src/types/index.ts` (حقول العرض الجديدة اختيارية)

---

## 11) تسلسل تنفيذ هذه الخطة (ملزم — لا انفجار كبير)

1. **المرحلة أ — العرض فقط (خطر منخفض):** المحلل المركزي + `DisplayFileNumber/DisplayFileYear` + كل مواضع العرض (`DocumentDtos`، `DocumentContextBuilder`، `ExcelExport` بما فيه عمود `لعام`، `ReviewLetterService`، `DelegationService`، `AppealService`، الواجهة بما فيها صفحة التدوير) + تدقيق `Include` الكامل + قابلية البحث (§4.9 عدا التعبئة الأثرية المؤجلة للمرحلة 5) — تُتحقق بصريًا.
2. **المرحلة ب — مسارات الكتابة:** إصلاح وقعة الشطب (§4.2) + تاريخ الشطب (§4.5) + سنة الإعادة (§4.4).
3. **المرحلة ج — الحواجز:** حارس الوقوعات (§4.3) + منع `مشطوب→منفذ` (§4.6).
4. **المرحلة د — الإضافات المرجعية:** رقم المناب في المنيب + رقم الملف الفعّال في الاستئناف (§4.8).

كل مرحلة خضراء لوحدها بالتحقق الإلزامي قبل التالية.

**تبليغ المستخدمين قبل النشر (إلزامي):** تغيير الدلالة إلى «الأحدث الفعّال» يعني أن أرقام ملفات قائمة ستتغير ظاهريًا بعد النشر (مثال: `2024/55` المُدوَّر `2025/60` سيعرض `60` في القوائم والإكسل والبوابة). هذا هو المطلوب قانونيًا، لكن يجب إبلاغ المستخدمين مسبقًا وإلا استُقبل كحادثة «الأرقام تغيرت لوحدها».

---

> **لا تنفيذ قبل موافقتك الصريحة. هذه الخطة مثبتة كمرجع وحيد للتنفيذ.**
