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

### 9.1 — خطة تنفيذ المرحلة 2 التفصيلية (2026-09-14 — القاعدة الحالية تجريبية)

> تُقرأ مع بند المرحلة 2 أعلاه (المثبّت — لا يُعدَّل). هذا التفصيل مبني على فحص الكود الحالي سطرًا بسطر.

**0) المبادئ والحدود (مثبتة لهذه المرحلة):**
- القاعدة الحالية **تجريبية فقط** → لا Backfill ولا تدقيق تاريخي حاجب؛ الهجرتان تُطبَّقان مباشرة على القاعدة التجريبية.
- **لا تغيير في العقود**: نفس الـ`DTOs` ونفس نقاط النهاية ونفس الحقول — التغيير سلوك تخزيني (`append` بدل `upsert`) + جعل قراءتين حتميتين.
- القاعدة الذهبية: سجل رقم الأساس `append-only`؛ المعتبر هو الأحدث (`Year` تنازليًا ثم `CreatedAt` تنازليًا) — المحلل المركزي `EffectiveFileIdentity.Pick` يدعم التعدد أصلًا **دون تعديل**.

**1) تغييرات الخلفية — مسارات الكتابة الأربعة (تحويل `upsert` → إنشاء دائم):**
- أ. `Configurations.cs:394`: حذف `.IsUnique()` من فهرس `(DocumentId, Year)` مع **إبقاء الفهرس غير الفريد** (يلزم الاستعلامات)، وتحديث التعليق `:393` («سجلات متعددة لكل (ملف، سنة): الأحدث `CreatedAt` هو المعتبر»).
- ب. `Configurations.cs:903`: نفس التحويل لفهرس `(AppealId, Year)`، وتحديث التعليق `:902` والتوثيق `XML` في `:892-895`.
- ج. `DocumentService.Status.cs:574-592` (`ApplyRenewalAsync`): حذف فرع `existing` (تحديث رقم سنة الإعادة) — **إنشاء سجل جديد دائمًا**؛ كل تجديد حدث مستقل ووقعة التجديد تُلحق أصلًا (`:599-612` لا تُمس).
- د. `DocumentService.Search.cs:271-294` (حفظ التدوير): إنشاء دائمًا؛ حذف فرع التحديث ورسالة التدقيق «حدّث رقم أساس {year}» (تبقى رسالة «دوّر الملف برقم أساس …»).
- هـ. `DocumentService.Search.cs:256-266` (إلغاء رقم سنة اليوم بتفريغ الحقل): `FirstOrDefault` → حذف **كل** سجلات السنة الحالية — كلها من آثار تدوير هذه السنة — فتعود الهوية الفعّالة للسنة السابقة؛ ورسالة التدقيق تذكر العدد والقيم لا قيمة واحدة. (تنفيذيًا: `Where(...).ToList()` ثم حلقة `Remove` لكل سجل — واجهة `IRepository<T>` لا تملك `RemoveRange` (`IRepository.cs:11`) فلا يُوسَّع العقد، وتُجمَّد القائمة قبل التعديل أثناء التكرار.)
- و. `DocumentAppealService.cs:476-493` (حفظ رقم الأساس الاستئنافي): إنشاء دائمًا؛ رسالة التدقيق `:495-497` تصبح «أضاف سجل رقم أساس استئنافي … (تُحفظ السجلات السابقة)».

**2) تغييرات الخلفية — قراءات تصبح غير حتمية مع التعدد (إصلاح إلزامي):**
- ز. `DocumentService.Search.cs:147` (إسقاط قائمة التدوير): `FirstOrDefault(b => b.Year == currentYear)` ترتيبه غير معرّف مع التعدد → **الأحدث `CreatedAt` لسنة اليوم** (`Where + OrderByDescending(CreatedAt) + FirstOrDefault`).
- ح. `GetBaseNumberHistoryAsync` (`DocumentService.Search.cs:198-201`): إضافة `ThenByDescending(CreatedAt)` — السجل يعرض **كل** الصفوف (التعدد هو التوثيق المطلوب).
- ط. `BuildBaseNumberHistory` (`DocumentAppealService.cs:443-453`): إضافة `ThenByDescending(CreatedAt)` داخل السنة الواحدة.
- **ما لا يُمس — مُتحقق سطرًا بسطر**: `NeedsRotationOf` (`DocumentDtos.cs:990-1003` — `Any`)، `DocumentRepository.cs:521` (`Any`)، `DocumentAppealService.cs:1007` (`Any`)، شروط أهلية الحفظ (`Any(b.Year < year)`)، `EffectiveFileIdentity.Pick` (سنة ثم `CreatedAt` تنازليًا — جاهز)، `DocumentSearchTextBuilder.Build` (مرتب سنة ثم `CreatedAt` تنازليًا — `:20-25`) و`Append` (يمنع التكرار ذاتيًا — `:101-111`)، `UpdateRegistrationAsync` (`:214`/`850` — تمس أعمدة القيد لا جدول السجل).

**3) قرارات سلوكية مثبتة (تمنع الغموض لاحقًا):**
- إعادة حفظ **القيمة نفسها** لنفس السنة تُنشئ سجلًا ثانيًا — قاعدة موحدة بلا استثناءات صامتة (اكتمال أثر التدقيق؛ `Append` يمنع تضخم `SearchText`).
- الإلغاء يحذف كل سجلات السنة الحالية فقط؛ سجلات السنوات السابقة محفوظة دائمًا.
- تعادل `CreatedAt` (حفظان متزامنان) → أيهما يُنتخب مقبول وموثّق؛ لا قفل إضافي (القاعدة تجريبية والتكلفة لا تبرر).
- `Down` للهجرتين يعيد القيد الفريد و**يفشل** بوجود صفوف مكررة — صالح فقط على قاعدة تجريبية بلا تكرار؛ يُوثَّق في ملف الهجرة تعليقًا.

**4) الهجرتان (بوابة صارمة):**
- من `backend/`: `dotnet tool restore` أولًا (أداة `dotnet-ef 10.0.10` في `dotnet-tools.json` — مثبّت `SDK 10.0.302`)، ثم `dotnet ef migrations add AllowMultipleBaseNumbersPerYear --context DocGeneratorDbContext` (مجلد `Migrations`) ثم `dotnet ef migrations add AllowMultipleBaseNumbersPerYearPg --context DocGeneratorPostgresDbContext` (مجلد `MigrationsPostgres` — لاحقة `Pg` مطابقة للاصطلاح القائم)، مع `--project`/`--startup-project` كما في آخر زوج هجرات.
- **البوابة**: ملف `Up()` في كل هجرة يحوي **فقط** `DropIndex` + `CreateIndex` (غير فريد) على `DocumentBaseNumbers` و`AppealBaseNumbers` — أي عملية إضافية (عمود/جدول/بيانات) → **إيقاف فوري وتحقيق** قبل المتابعة.
- التطبيق على القاعدة التجريبية: `dotnet ef database update --context DocGeneratorDbContext` ثم `--context DocGeneratorPostgresDbContext`.
- تحديث `RUN_GUIDE.md §9`: إضافة سطري الهجرتين لقائمة المعلَّق ثم شطبهما بعد التطبيق الناجح (اصطلاح المستودع).
- التقرير النهائي يتضمن اسمي الهجرتين وتنبيه التطبيق للسياقين (Deploy Reminder — ولو على قاعدة تجريبية، للسجل).

**5) الواجهة (لا تغيير عقود — نسخ ومفاتيح فقط):**
- `frontend/src/components/BaseNumbersModal.tsx:81`: `key={entry.year}` → `` key={`${entry.year}-${i}`} `` (تصادم حتمي مع التعدد).
- `frontend/src/components/appeal/AppealRotationModal.tsx:108`: `key={h.year}` → `` key={`${h.year}-${i}`} `` (نفس السبب).
- `frontend/src/pages/AppealsList.tsx:394`: «تم تحديث رقم الأساس الاستئنافي.» → «أُضيف سجل رقم أساس استئنافي جديد (تُحفظ السجلات السابقة).».
- مسح `Rotation.tsx` وأي `notice` بصيغة استبدال عبر `rg` (النمط: `تم (تحديث|تدوير|حفظ).*أساس`) وإعادة صياغته لدلالة الإلحاق.
- تحقق قراءة فقط: مسار التجديد في `OccurrencesEditor.tsx:215` يمر عبر نقطة النهاية النظامية (لا كتابة مباشرة لأرقام الأساس) — لا مسار خامس.

**6) الاختبارات (لا سلوك بلا اختبار — تُكتب مع التنفيذ):**
- إعادة كتابة `ServiceTests.cs:2554` (`SaveBaseNumbers_CreatesThenUpdatesSameYear` — يؤكد سلوكًا يُلغى) → `SaveBaseNumbers_SecondSaveSameYearAppendsAndNewestWins` (سجلان + الفعّال هو الثاني).
- توسيع `ServiceTests.cs:2575` (`SaveBaseNumbers_EmptyClearsCurrentYearPreservingPrevious`): بذر سجلين لنفس السنة → يُحذفان معًا وتُحفظ السنة السابقة.
- جديد: تجديدان لنفس السنة برقمين مختلفين → سجلان + الفعّال هو الثاني + وقعتا تجديد (الوقوعات لا تُمس).
- جديد: قائمة التدوير مع سجلين لسنة اليوم → تعرض الأحدث.
- توسيع `DocumentAppealServiceTests.cs:408` (`Rotation_OldYearNeedsRotation_ThenCurrentYearClearsIt` — يؤكد `history.Count == 2`): حفظ ثانٍ لنفس السنة → العدد 3 والفعّال هو الأحدث.
- جديد: `EffectiveFileIdentity.LatestFrom` مع سجلين لنفس السنة → الأحدث `CreatedAt` (تثبيت العقد).
- واجهة (`vitest`): `BaseNumbersModal` و`AppealRotationModal` يعرضان صفين لسنة واحدة؛ تحديث أي توقع نسخي متأثر.
- حدّيات محفوظة: إلغاء بلا سجل سنة حالية (no-op)، حفظ قيمة مطابقة (سجل ثانٍ)، ملف بلا أي سجل (الاحتياطي الأصلي)، تكامل التصدير (`DocumentsExportIntegrationTests`) — مسح أي توقع لرقم سنة وحيدة.

**7) التحقق الكامل الإلزامي (قبل إعلان الإنجاز):** `dotnet build` (0/0) → `dotnet test` (السياقان) أخضر → `npx oxlint src` (0/0) → `npx tsc -b` → `npx vitest run` → `npm run build` → مراجعة المسار الكامل (واجهة → `DTO` → خدمة → قاعدة → عرض/`SearchText`/إكسل) → `rg` لبقايا («سجل واحد لكل»، «حدّث رقم أساس»، `key={...year}`، `.IsUnique()`) → مطابقة بنود هذه الخطة بندًا بندًا.

**8) معايير القبول:** كل بنود §9.1 منفذة ومختبرة؛ صفر عمليات هجرة زائدة؛ صفر كسر عقود؛ التقريران (الإنجاز + الهجرتان وتنبيه السياقين) مكتملان؛ `docs/STRUCK_OFF_AUDIT_QUERY.sql` لا يُمس (يخص §7/المرحلة 5)؛ لا إشعار مستخدمين §11 (قاعدة تجريبية بلا بيانات حقيقية).

### 9.2 — خطة تنفيذ المرحلة 3 التفصيلية (2026-09-14)

> تُقرأ مع بند «المرحلة 3» أعلاه (المثبّت — لا يُعدَّل). هذا التفصيل مبني على جرد كامل لمواضع إنشاء
> الوقوعات في الكود الحالي (14 موضعًا آليًا + 3 نقاط يدوية)، ومطابق لمنهجية §9.1.

**0) المبادئ والحدود (مثبتة لهذه المرحلة):**
- الهدف: «تقسية» سجل الوقوعات — الوقعة الآلية حدث حقيقي سُجّل تلقائيًا، لا يجوز للمحرر اليدوي تغييره أو حذفه بصمت.
- **لا كسر عقود — توسعة إضافية آمنة**: نقاط النهاية الثلاث قائمة (`POST/PUT/DELETE /documents/{id}/occurrences`).
  التوسعة الوحيدة إضافية: حقل `Source` في آخر وسائط `DocumentOccurrenceDto` بقيمة افتراضية (`string? Source = null`) تُبقي المتصلين القدامى (واختبارات السيناريوهات الحالية) تُصرَّف كما هي.
- حماية دفاعية بطبقتين: رفض **خدمي** في مساري التعديل/الحذف + إخفاء أدوات التعديل في المحرر.
- **تغيير سلوكي واعٍ يُوثَّق في التقرير**: السماح الموروث من §4.3 بتعديل وقعة تجديد نظامية قائمة على ملف
  مشطوب (`OccurrenceTests.cs:494` — `UpdateOccurrence_EditExistingRenewalOnStruckOffFile_Allowed`) يُلغى؛
  الوقعة النظامية تصبح غير قابلة للتعديل/الحذف. (هذا تعليق مباشر بين §4.3 والمرحلة 3 — يُحسم تحت القرار 2.)

**1) جرد مواضع الإنشاء (المرجع المعتمد — يُوسَم الآلي بالكامل):**
- **آلي (`system`) — `DocumentService.Status.cs`**: `:160` (شطب/جبري/تسوية/تريث)، `:234` (تراجع عن الحالة)،
  `:303` (اعتبار «منفذ جبريا» كاملًا)، `:454` (إعادة «عرض وايداع» إلى المتداول)، `:589` (وقعة التجديد في
  `ApplyRenewalAsync`)، `:616` (`AddStruckOffOccurrenceAsync`).
- **آلي (`system`) — `DocumentDelegationService.cs:500`**: تفعيل «منفذ جبريا — منفذ جزئيا» تلقائيًا مع الإنابة.
- **آلي (`system`) — `PublicEntityService.cs`**: `:2174` و`:2568` و`:2690` و`:3024` و`:3323` و`:3611` و`:3782`
  (وقوعات «تغيير جهة» `entity-change` عند دمج/حل/نقل قيد).
- **يدوي (`manual`) — `DocumentService.Actions.cs`**: `:135` (إضافة)، `:158` (تعديل)، `:188` (حذف) — المسار
  الوحيد عبر `OccurrencesEditor`؛ الإضافة اليدوية تُنتج `manual` دائمًا، والتعديل/الحذف على `manual` يُبقيان الوسط كما هو.
- ملاحظة من الجرد: الوقوعات الآلية تضع `CreatedById = doc.CreatedById` (مالك الملف لا الفاعل) في معظم
  المواضع، فالمصدر الجديد هو وحده ما يميّز «حدث النظام» عن «إدخال المستخدم» — يوثَّق في تعليق الكيان.

**2) الكيان والكشط (Schema):**
- `DocumentOccurrence` (`backend/src/DocGenerator.Domain/Entities/DocumentOccurrence.cs`): إضافة
  `public string Source { get; set; } = OccurrenceSourceCatalog.Manual;`.
- كتالوج جديد `OccurrenceSourceCatalog` في `backend/src/DocGenerator.Domain/Enums/` (بجوار `OccurrenceTypeCatalog`
  تطابقًا لاصطلاحه): `System = "system"`، `Manual = "manual"`، `IsSystem(string?)`.
- `Configurations.cs:653` (`DocumentOccurrenceConfiguration`): `Property(o => o.Source).HasMaxLength(10)
  .IsRequired().HasDefaultValue("manual")` — بلا فهرس جديد (لا استعلامات تصفية بالمصدر).
- `Backfill`: داخل الهجرة (يُحسم تحت القرار 1)؛ `Down` = `DropColumn` فقط.

**3) الخدمة — الحماية الدفاعية (حظر مطلق — القرار 2-أ):**
- `CreateOccurrence` (`Actions.cs:213`): `Source = OccurrenceSourceCatalog.Manual` (يَسْري على `AddOccurrenceAsync`)؛ وإن `request.OccurrenceType == OccurrenceTypeCatalog.EntityChange` → `throw new ArgumentException("نوع الوقعة 'تغيير جهة' نظامي ولا يُنشأ يدوياً")`.
- `UpdateOccurrenceAsync` (`DocumentService.Actions.cs:158`): بعد جلب الوقعة، إن `OccurrenceSourceCatalog.IsSystem(occurrence.Source)` → `throw new ArgumentException("لا يمكن تعديل وقعة نظامية")`؛ وكذلك إن `request.OccurrenceType == OccurrenceTypeCatalog.EntityChange` → `throw` (لا تحويل يدوي إلى نظامي)؛ ثم حارس `§4.3` القائم (تحويل إلى `renewal` على مشطوب).
- `DeleteOccurrenceAsync` (`DocumentService.Actions.cs:188`): إن `IsSystem` → `throw new ArgumentException("لا يمكن حذف وقعة نظامية")`.
- تحويل الـDTO: تمرير `o.Source` في آخر وسيطة في الموضعين — `Actions.cs:341` (ToDto) و`DocumentDtos.cs:950-953` (الموضع الثاني هو إسقاط `DocumentResponse.Occurrences`).
- `DocumentsController.cs:656-712`: إضافة `try/catch (ArgumentException)` في مسار `DELETE` (`:699`) مطابق لمسار `PUT:677` ليُترجم الرفض إلى `400` برسالة عربية؛ لا تغيير آخر في الفرع الأساسي (حظر مطلق — البديل 2-ب مؤجل إلى سطح مشرف لاحق إن لزم).

**4) الهجرتان (بوابة صارمة):**
- من `backend/`: `dotnet tool restore` أولًا، ثم
  `dotnet ef migrations add AddOccurrenceSource --context DocGeneratorDbContext` (مجلد `Migrations`) و
  `dotnet ef migrations add AddOccurrenceSourcePg --context DocGeneratorPostgresDbContext` (مجلد
  `MigrationsPostgres` — لاحقة `Pg` مطابقة للاصطلاح).
- بوابة `Up()`: `AddColumn("Source", "DocumentOccurrences", nullable:false, defaultValue:"manual")` ثم `Sql("UPDATE DocumentOccurrences SET Source = 'system'")` — **فقط**؛ أي عملية إضافية → إيقاف فوري وتحقيق.
- `Down()`: `DropColumn` فقط (بلا رسائل).
- قبل الهجرة (للتوثيق): تشغيل استعلام تدقيق `SELECT OccurrenceType, COUNT(*) FROM DocumentOccurrences GROUP BY OccurrenceType` وإرفاق النتيجة بالتقرير كدليل أن التقسية شملت الكل.
- التطبيق على القاعدة التجريبية: `dotnet ef database update` للسياقين؛ تحديث `RUN_GUIDE.md §9` بسطري الهجرتين في المعلَّق؛ والتقرير النهائي يحمل اسمي الهجرتين وتنبيه تطبيق السياقين (Deploy Reminder — للسجل).

**5) الواجهة:**
- `frontend/src/types/index.ts`: `DocumentOccurrenceDto` يُضاف `source?: 'system' | 'manual'` (اختياريًا فلا تُكسر تجهيزات الاختبارات القائمة).
- `frontend/src/components/form/OccurrencesEditor.tsx` + `frontend/src/components/view/OccurrencesModal.tsx`: شارة «نظامي» محايدة (`bg-gray-100 text-gray-700`) بجانب تسمية النوع لكل سجلّ `system`؛ إخفاء زرّي «تعديل/حذف» لها (حظر مطلق).
- `frontend/src/components/view/viewFormat.ts:311-313`: **مراجعة سطر الشطب المختصر** بعد §4.2 — الفرع قائم ومتحقق؛ المتوقع «تم شطب الملف رقم {n} لعام {y} بتاريخ {d}». لا تعديل إلا عند ثبوت خلل عبر اختبار المتغير الموسّع (شطب ثانٍ بعد تدوير: يعرض الرقم الفعّال).
- اختبار واجهة: سطر الشطب بالرقم الفعّال (regression)؛ المحرر والنافذة يخفيان أزرار النظامي ويعرضان الشارة.

**6) القرارات المثبتة (النهائية — بانتظار اعتمادك الصريح قبل التنفيذ وتُسجَّل في التقرير):**
- **القرار 1 — Backfill الصفوف القائمة**: `system` — **معتمد** (قاعدة تجريبية + الهدف تقسية الكل؛ أي `manual`-تاريخي غير قابل للتمييز يُحصّن معها). البديل `manual` الأنعم مرفوض لهذه المرحلة؛ يُعاد النظر فيه فقط بسطح تصحيح أرشيفي إن طُلب لاحقاً.
- **القرار 2 — الحماية**: (أ) **حظر مطلق** في الخدمة لكل الأدوار — **معتمد** (أبسط وأقوى، والتصحيح اليدوي لنظامية لاحقًا عبر قاعدة بيانات أو سطح مشرف مستقبلي). البديل (ب) صلاحية أعلى `Manager/Admin` **مؤجل** إلى سطح مشرف لاحق ولا يُنفذ في هذه المرحلة. قرار الحظر **يلغي** السماح الموروث في §4.3 بتعديل وقعة تجديد نظامية على ملف مشطوب — يُدوَّن صراحةً في تقرير الإنجاز.
- **القرار 3 — ظهور الوسم**: شارة «نظامي» **في المحرر والنافذة التفصيلية** (`OccurrencesEditor.tsx` + `OccurrencesModal.tsx`) لكل سجلّ `system` — **معتمد** لشفافية تدقيقية بلا كلفة (لون محايد `bg-gray-100 text-gray-700`).

**7) الاختبارات (لا سلوك بلا اختبار — تُكتب مع التنفيذ):**
- خلفية (`OccurrenceTests.cs`): كل موضع آلي من الجرد §1 يُنتج `Source == System` (شطب/تجديد/تغيير حالة/تراجع/إعادة ودائع/جبري كامل/إنابة/تغيير جهة)؛ الإضافة اليدوية → `Manual`؛ تعديل/حذف `manual` مسموح ويُبقي المصدر؛ تعديل/حذف `system` مرفوض برسالة صريحة وتبقى الوقعة في القاعدة؛ **إنشاء/تحويل يدوي إلى `entity-change` مرفوض** (`AddOccurrence_EntityChange_Manual_Throws`).
- تحويل سلوكي: `UpdateOccurrence_EditExistingRenewalOnStruckOffFile_Allowed` → تُستبدل باختبار الرفض (`..._SystemRenewal_Throws`) أو يُعاد بناؤها بمتغير `manual`؛ وأي اختبار يؤلف `DocumentOccurrenceDto` بلا `source` يحتاج التحديث إلى `Manual` صريحًا (لا ينكسر الجمع لوجود القيمة الافتراضية).
- واجهة (`vitest`): الشارة في المحرر والنافذة + إخفاء أزرار `system` + سطر الشطب الفعّال.
- حدّيات محفوظة: إضافة يدوية `renewal` على ملف مشطوب (رفض §4.3 قائم لا يُمس)، تعديل `manual` على ملف مشطوب، حذف معرّف وقعة من غيرك (`ReturnsNull`)، إدخالات ما قبل الهجرة بلا `source` (تأخذ الافتراضي عبر الهجرة).

**8) معايير القبول:** كل بنود هذه الخطة منفذة ومختبرة؛ القرارات الثلاثة مُثبتة بموافقتك في التقرير؛ هجرتان فقط باسميهما ومفصّلتان في `RUN_GUIDE.md §9`؛ صفر كسر في نقاط النهاية؛ **جرد §1 مطابق** عبر `rg "new DocumentOccurrence" --glob "*.cs" | rg -v "Source"` (يجب أن يعطي صفراً خارج مواضع الجرد)؛ سطر الشطب المختصر سليم بعد §4.2؛ `docs/STRUCK_OFF_AUDIT_QUERY.sql` لا يُمس؛ نتيجة استعلام التدقيق قبل الهجرة مرفقة بالتقرير.

### المرحلة 3 — تقسية سجل الوقوعات
- إضافة عمود `Source` (`system/manual`) لتمييز الوقوعات النظامية عن اليدوية
- منع تعديل/حذف الوقوعات النظامية من `OccurrencesEditor` أو تقييدها بصلاحية أعلى
- مراجعة سطر وقعة الشطب المختصر بعد تثبيت الرقم الصحيح في §4.2 (البطاقة والنافذة تشتركان في `occurrenceLine` نفسه)
- تحديث توثيق `UpsertOccurrenceRequest` واختباراته

### المرحلة 4 — توحيد الزمن والتدوير حول رأس السنة
- طبقة زمنية موحدة (`IClock`/`IDateProvider`) بدل `Today/Now/UtcNow` المبعثرة
- حقن السنة الحالية من نفس المصدر في الخلفية والواجهة لتجنب اختلاف `getFullYear()` للعميل عن `Today.Year` للخادم
- معالجة `FILE_YEARS` الثابتة في `documentFormConstants.ts:14` لتكون ديناميكية

### 9.3 — خطة تنفيذ المرحلة 4 التفصيلية (2026-09-14 — القاعدة الحالية تجريبية)

> تُقرأ مع بند «المرحلة 4» أعلاه (المثبّت — لا يُعدَّل). هذا التفصيل مبني على جرد سطرًا بسطر لمصادر الزمن
> في الخلفية والواجهة، ومطابق لمنهجية §9.1/§9.2. **نُقّحت (2026-09-14) بمراجعة تحليلية شاملة:**
> ثلاث تصحيحات حاجبة (تسجيل الساعة، حسم المعاملات الثابتة، أفق السنوات) + تلميع نطاق «د3» والواجهة.

**‏0‎) الغاية والهدف (لماذا هذه المرحلة بالضبط):**
رأس السنة ممنطقة حدّية تتقاطع فيها ثلاثة عيوب متراكمة:
1. **اختلاف مصدر «السنة الحالية» بين العميل والخادم**: الواجهة ترسل `renewalYear` محسوبًا من ساعة العميل
   (`new Date().getFullYear()`) بينما يتحقق الخادم منه ضد `DateTime.Today.Year` (ساعته الخاصة) —
   فعند منتصف الليل عبر السنوات (أو فرق منطقة زمنية/انحراف ساعة) يتعرض الملف المشطوب لرفض تجديد
   مشروع أو حفظِ سنة خاطئة بصمت.
2. **مبعثرات زمنية غير موحدة في الخادم نفسه**: `DateTime.Now` (محلي) بجانب `DateTime.UtcNow` و
   `DateTime.Today` — ثلاثة مصادر مختلفة في رسالة تدقيق نقل الملفات وإحصاءات الفترة، يعني ضمنيًا
   نطاقات زمنية مختلفة لنفس المنطق.
3. **`FILE_YEARS` ثابتة مكتوبة يدويًا** (2026..2030): تنتهي صلاحيتها ذاتيًا ولا تعكس السنة الجديدة عند
   رأس السنة — وسنة الملف في النموذج سنة قيدٍ يدخلها المستخدم فيستحيل أن تكفّ عن النمو أو تتبع اليد.

الهدف: **مصدر وحيد قابل للحقن والاختبار لـ«الآن» في الخادم، والسنة الحالية للواجهة تُؤخذ من الخادم نفسه**
لا من ساعة العميل — فيُقضى على اختلاف رأس السنة وعلى الإبقاء اليدوي لقائمة السنوات.

‏**1‎) قرارات هذه المرحلة (تُثبَّت بموافقتك في التقرير):**
- **‏د1 — الأداة**: استخدام **`System.TimeProvider`** (مدمج في .NET، لا طبقة مصطنعة) عبر الحقن في
  الخدمات، بحيث تُشتق «السنة الحالية» من الساعة عبر تحويل منطقة صريح (انظر د5 — لا `GetLocalNow()` مباشر
  لأنه يعتمد على بيئة الحاوية) — بلا تغيير لسلوك الإنتاج القائم. الاختبارات تستعمل `FakeTimeProvider` من
  `Microsoft.Extensions.TimeProvider.Testing` (حزمة اختبارية فقط — تُضاف لمشروع اختبارات التطبيق).
  البديل `IClock` مرفوض لتجنب تكرار ما يقدمه `TimeProvider` أصلًا.
  **التسجيل**: في `Program.cs` (أو `AddApplication`) عبر `services.AddSingleton(TimeProvider.System)`
  — **لا في `AddInfrastructure`**، لأن المستهلكين (الخدمات/المستودعات) في طبقة `Application` التي لا
  تعتمد على `Infrastructure`، والتسجيل هناك يخل بحدود الطبقات.
- **‏د2 — المحلل المركزي**: يبقى `EffectiveFileIdentity` ثابتًا **صافيًا على المدخلات**؛ كل موضع قرار
  سنة يمرر `asOfYear` مستنتجًا من الساعة المحقونة. يُحوَّل `asOfYear` إلى معامل **مطلوب**
  (`int asOfYear` بلا افتراض) في `Latest/LatestFrom/Number/Year/Pick` — فلا يبقى أي استدعاء مباشر لـ
  `DateTime.Today` في الخادم، ويفكك البناء عند أي نداء منسي، وكل مواضع الجرد تمرر السنة المحقونة صراحة
  (المصدر: `TimeZoneInfo.ConvertTime(clock.GetUtcNow(), tz).Year` — د5، لا ساعة النظام الصامتة).
  **يُحذف استثناء `EffectiveFileIdentity.cs`** من معيار القبول §7: `rg "DateTime\.(Today|Now)" backend/src`
  يجب أن يساوي `0` **بلا استثناء** — فلا احتياط صامت خارج الساعة المحقونة.
  **الحسم في `NeedsRotationOf`** (`DocumentDtos.cs:993` — صنف تحويل ثابت لا يُحقن): يُمرَّر `currentYear`
  كمعامل صريح إلى `FromEntity` / `NeedsRotationOf` إلزاميًا (لا افتراض `Today` داخليًا) من الخدمة الحية
  التي تملك الساعة — لا تحويل المنطق لخدمة مستقلة لأنه مبالغة في هذه المرحلة.
- **‏د3 — نطاق جرف `DateTime.UtcNow`**: يُنفَّذ في هذه المرحلة **ضمن مسارات قرار السنة/التدوير/رسالة
  التدقيق فقط** (المواضع الـ16 في الجرد + نقاط `Status.cs` حيث يسقط الطابع على سنة الشطب، مثل
  `EventDate = UtcNow` عند وقعة) — يُحوَّل إلى `clock.GetUtcNow()`. **باقي طوابع الإنشاء** العامة
  (`CreatedAt`/`UpdatedAt` لعموم الكيانات) **تُؤجَّل موثقة** لحزمة نظافة لاحقة: توحيدها يحسّن قابلية
  الاختبار لكنه يضاعف حجم التغيير وخطر الانحدار بلا فائدة على هدف المرحلة (اختلاف سنة العميل/الخادم).
  **استثناء موثق وحيد**: دوال التهيئة على خصائص كيانات `Domain` (`User.CreatedAt`…) تبقى افتراضية وقت
  البناء — لا حقن فيها دون تغيير تصميم الكيان، وقيمة الافتراض مطابقة لمنطق «لحظة الإنشاء».
  مفاتيح السياق `TokenService`/`DbLoginRateLimiter` تُحوَّل أيضًا لأنها في نطاق «الآن» الموحد.
- **‏د4 — سنة الواجهة من الخادم**: نقطة نهاية عامة حيادية مصدرُها الساعة المحقونة:
  `GET /api/meta/current-year` ← `{ "currentYear": 2026 }` (بلا مصادقة — قيمة غير حساسة، لكن تبقى
  صادرة من الساعة الموحدة للخادم). تستجلبها الواجهة مرة واحدة (كاش/هوك `useCurrentYear()`)؛ **احتياط
  فشل** موثق واختباري: عند تعذر الجلب تُستعمل `new Date().getFullYear()` — الانحراف الوحيد الباقي
  مقصور على حالة عدم التمكن من الاتصال، ويوثَّق تحته في التقرير.
  **يُصرَّح** بـ `[AllowAnonymous]` وتجاوز `EntityManagerPortalGuard` (الـ middleware يعزل مندوب الجهة)،
  و**لا يتطلب CSRF** (وإلا فشل الجلب قبل المصادقة — `client.ts` يضيف `X-CSRF` تلقائيًا).
  **الكاش**: على مستوى التطبيق عبر `CurrentYearContext` (يُسلَّم في `Layout.tsx`/`App.tsx` — طلب واحد،
  حالة تحميل واحدة) بدل كاش وحدة يُطلق 7 طلبات متوازية؛ **يُعطَّل حفظ/إرسال النموذج أثناء التحميل** حتى
  ينجح الجلب (وإلا يُرسل `renewalYear` منحرفًا فيرفضه الخادم عند `Status.cs:542`).
  **أفق السنوات**: بدل `yearOptions()` بـ `[current..current+4]` (يحذف السنوات الماضية من قائمة سنة قيد
  الملف ويكسر تحرير الملفات القديمة)، الأفق **يشمل الماضي**: `[currentYear-5 .. currentYear+4]` مع
  **إبقاء القيمة المحمَّلة الحالية حتى لو خرجت عن الأفق** (لا تُحذف من `select`).
- **‏د5 — دلالة السنة**: «السنة الحالية» هي **سنة التاريخ المحلي للخادم** (لا UTC) — مطابقة الدلالة
  القائمة؛ الفارق الجوهري هو توحيد المصدر وشهادة الزمن وصيرورة الساعة قابلة للحقن والاختبار.
  **لكن `GetLocalNow()` يعتمد على `TimeZone` الحاوية** (غالبًا UTC في الإنتاج = انحراف ساعتين حول رأس
  السنة عن دمشق). **تُثبَّت المنطقة صراحة**: `TimeZone:Id` في `appsettings.json` (افتراضي
  `Asia/Damascus` / `Syria Standard Time`) ويُشتق القرار عبر
  `TimeZoneInfo.ConvertTime(clock.GetUtcNow(), tz).Year` — حتمي عبر البيئات.

‏**2‎) جرد مصادر الزمن المعنية (خلفية — year-decisions):**
| الموضع | الاستخدام الحالي | المسار بعد التطبيق |
|---|---|---|
| `EffectiveFileIdentity.cs:53` | افتراض `asOfYear` عبر `DateTime.Today.Year` | `asOfYear` معامل مطلوب (د2) — لا افتراض صامت |
| `DocumentService.Status.cs:167` | رقم فعّال لوقعة شطب `..?? DateTime.Today.Year` | `_clock` محقون |
| `DocumentService.Status.cs:542/544` | التحقق من سنة إعادة `executedLike` وحسمها | `_clock` محقون |
| `DocumentService.Status.cs:618` | سنة شطب افتراضية | `_clock` محقون |
| `DocumentService.Search.cs:137` | `currentYear` قائمة التدوير | `_clock` محقون |
| `DocumentService.Search.cs:214` | سنة حفظ أرقام الأساس | `_clock` محقون |
| `DocumentService.cs:413` | `DateTime.Now` (محلي!) في تدقيق نقل الملفات | `clock.GetLocalNow()` — توحيد مع `:418` |
| `DocumentService.Actions.cs:452` | تاريخ افتراضي لملاحظة | `clock` |
| `DocumentContextBuilder.cs:65-66` | `current_date` / `current_date_arabic` | `clock` |
| `DocumentAppealService.cs:473` | سنة تدوير رقم أساس الاستئناف | `_clock` محقون |
| `DocumentAppealService.cs:994-1006` | أهلية تدوير الاستئناف | `_clock` محقون |
| `DocumentDtos.cs:995` | `NeedsRotationOf` | `currentYear` معامل صريح من الخدمة الحية (د2) |
| `DocumentRepository.cs:507` | مؤهلو تدوير الملفات | `TimeProvider` محقون في المستودع |
| `StatisticsRepository.cs:699` | `DateTime.Now` نافذة الإحصاءات | `clock` محقون |
| `DocumentsController.cs:149` / `PortalController.cs:75` | اسم ملف إكسل `DateTime.Now:yyyy-MM-dd` | `clock` |

‏**3‎) جرد مصادر الزمن (واجهة — year-decision / انحراف الساعة):**
| الموضع | الاستخدام الحالي | المسار بعد التطبيق |
|---|---|---|
| `documentFormConstants.ts:14` | `FILE_YEARS` ثابتة | تُزال؛ خيارات السنوات مولّدة من `useCurrentYear()` |
| `DocumentForm.tsx:687` | `renewalYear: new Date().getFullYear()` | سنة الهوك (المصدر الخادم) |
| `DocumentForm.tsx:908` | قائمة `سنة الملف` من `FILE_YEARS` | خيارات الهوك |
| `OccurrencesEditor.tsx:11` | `pinnedRenewalYear() = new Date().getFullYear()` | سنة الهوك |
| `RenewalModal.tsx:72` / `ExecutedStatusModal.tsx:146` | `renewalYear` للعميل | سنة الهوك |
| `AppealRotationModal.tsx:19` (و`:112/:127`) | `currentYear` الترحيل/التسمية | سنة الهوك |
| `Rotation.tsx:29` (و`:102/:141/:165`) | سنــة التدوير للعرض | سنة الهوك |

‏**4‎) خطوات التنفيذ المقترحة (بعد الموافقة — تُنفَّذ ضمن مراجعة المسار الكامل):**
1. **الخلفية**: تسجيل `TimeProvider.System` (Singleton) في `Program.cs` (أو `AddApplication`); حقنه في
   الخدمات/المستودعات المعنية بالجرد أعلاه؛ استبدال المواضع 16 بقرار سنة من `_clock`؛ تحويل
   `DateTime.Now/UtcNow` في مسارات السنة/التدقيق وفق «د3»؛ تحويل `asOfYear` في المحلل إلى معامل مطلوب
   (`EffectiveFileIdentity`) — لا افتراض صامت؛ تثبيت `TimeZone:Id`.
2. **نقطة النهاية**: `MetaController` (Route `api/meta`) يعرّض `GET current-year`: جلب السنة الحالية من
   الساعة المحقونة عبر تحويل المنطقة (`TimeZone:Id`) — استجابة مستقرة `{ currentYear }` (أيضًا تفيد
   مراقبة خطأ أوقات الزحام دون عناء).
3. **الواجهة**: `hooks/useCurrentYear.ts` (جلب + كاش + احتياط موثق)؛ `yearOptions()` خالص للخيارات؛
   حذف `FILE_YEARS` وربط مواضع الجرد السبعة بالهوك.
4. **الاختبارات**: راجع §5 أدناه.
5. **التحقق الإلزامي الكامل** + تقرير الإنجاز بقرارات «د1..د5» مثبتة بموافقتك.

‏**5‎) الاختبارات الإلزامية (تُضاف مع كل تغيير سلوكي — حدّيات رأس السنة):**
- **خلفية** — `FakeTimeProvider` ثابت لحظات حدّية:
  - عند `31/12/2026 23:59` مقابل `01/01/2027 00:01` (بتوقيت الخادم) لمسارين:
    (أ) أهلية التدوير (`NeedsRotationOf` + `GetRotationCandidatesAsync`)، (ب) سنة إعادة
    `executedLike` (رفض/حسم في `Status.cs:542-544`) — يقضي على التعارض العرضي للساعة.
  - وقعة شطب بلا `StruckOffDate` تستنتج سنة شطبها من الساعة؛ ورقم فعّال من ذات المصدر.
  - تدوير رقم أساس استئناف يسجل سنة الساعة لا ساعة النظام الأساسية.
  - رسالة تدقيق نقل الملفات تستعمل تاريخًا واحدًا موحدًا (لا مزيج `Now/UtcNow`).
  - `EffectiveFileIdentityTests`: حالات افتراض `asOfYear` بتقدم/تأخر سياق صريح (صفر اعتماد على `Today`).
  - تعارض سنّي ما زال مرفوضًا حتى لو مرّت لثانية بعد رأس السنة (عبور `DocumentAppealService:473`).
  - نافذة الفترة في `StatisticsRepository:699` عند الحدود (بداية/نهاية فترة رأس السنة).
- **واجهة**:
  - `useCurrentYear` (نجاح/فشل/احتياط؛ والسنة المولّدة مستخدمة حيث خيارات `FILE_YEARS` سابقًا).
  - ربط «منفذ عليها»: النموذج يثبّت `renewalYear` من سنة الهوك حتى لو خالفت سنة العميل.
  - صفارة/تدوير/استئناف: التسميات والترشيح بسنة الهوك (اختبار تجاوب مقاس جوال عبر القوالب القائمة).
- **تكامل API**: `GET /api/meta/current-year` يعيد السنة الحالية بنجاح (200).

‏**6‎) حدّيات محفوظة وحدود النطاق:**
- **لا هجرات** في هذه المرحلة (استبدال كود خالص) — تغيير مستودع/عمود صفري؛ Deploy Reminder = نقطة
  النهاية الجديدة فقط (تُنشر الخلفية والواجهة معًا).
- لا تغيير في العقود القائمة ولا في دلالة «السنة الحالية = سنة تاريخ الخادم المحلي» (د5) —
  التغيير في المصدر والشهادة فقط (مع تثبيت المنطقة `TimeZone:Id` بدل الاعتماد على بيئة الحاوية).
- نطاق «د3» في هذه المرحلة **مقصور على مسارات قرار السنة/التدوير/رسالة التدقيق**؛ طوابع الإنشاء
  العامة تُؤجَّل موثقة لحزمة نظافة لاحقة (حتى لا يتضخم التغيير ويبقى الهدف محددًا).
- `FILE_YEARS` ثابتة سابقة: تاريخيًا لا يمس سوى العرض؛ لا «إعادة بناء نصوص». لا إشعار مستخدمين
  (§11 قائم: القاعدة تجريبية بلا بيانات حقيقية) — لأن العرض يبقى مطابقًا للسنة الحالية الفعلية.
- استثناء الـ`Domain` (د3) موثق فوقه؛ أي نفاذ خارج الجرد يُحتسب خطأ في مراجعة المسار (معيار §7).

‏**7‎) معايير القبول:**
- **جرد §2/§3 مطابق**: `rg "DateTime\.(Today|Now)" backend/src --glob "*.cs"` = **0 بلا استثناء**
  (بعد تحويل افتراض المحلل إلى `asOfYear` مطلوب)، و
  `rg "DateTime\.UtcNow" backend/src --glob "*.cs"` بلا وقوع خارج كيانات `Domain` ودوالها
  (كل موضع في الجرد أو مستثنى موثق)، و`rg "getFullYear" frontend/src --glob "*.{ts,tsx}"` = 0 خارج
  ملف `useCurrentYear.ts` (الاحتياط الموثق) وملفات الاختبارات.
- الاختبارات الحدّية §5 كلها خضراء (خلفية + واجهة + API).
- التحقق الإلزامي كاملًا: `dotnet build` ‏0/0‏، `dotnet test` (Application كامل + Api كامل)،
  `oxlint` ‏0/0‏، `tsc -b`، `vitest` كامل، `npm run build` — كلها خضراء.
- صفر كسر في نقاط النهاية القائمة؛ نقطة النهاية الجديدة **موثقة في `RUN_GUIDE.md`** حيث تُستقى سنة
  الواجهة، ومصرَّح عنها `[AllowAnonymous]` واستثناء CSRF.
- تقرير الإنجاز يثبت قرارات «د1..د5» بموافقتك + جدول الجرد قبل/بعد + نتيجة جرد الاستبدالات
  (المعيار أعلاه) + حدّية رأس السنة موثقة باختبار.

### 9.4 — إضافات قسم «10) الملفات المتأثرة» للمرحلة 4 (الملفات المعنية بالزمن فقط)
> قسم «10)» العام في الخطة يشمل ملفات المراحل 1-3؛ قائمة المرحلة 4 أدناه تُكمّله ولا تُغيّره.

**خلفية (المرحلة 4):**
- `backend/src/DocGenerator.Api/Program.cs` — تسجيل `TimeProvider.System` و`TimeZone:Id`
- `backend/src/DocGenerator.Api/Controllers/MetaController.cs` — `GET /api/meta/current-year`
- `backend/src/DocGenerator.Application/DependencyInjection.cs` — تسجيل الساعة في `AddApplication`
- `backend/src/DocGenerator.Application/Common/EffectiveFileIdentity.cs` — `asOfYear` مطلوب
- `backend/src/DocGenerator.Application/Services/DocumentService.Status.cs` — مواضع 153/165/167/175/397/462/542/544/618
- `backend/src/DocGenerator.Application/Services/DocumentService.Search.cs` — 137/214
- `backend/src/DocGenerator.Application/Services/DocumentService.cs` — 413/418 (رسالة التدقيق)
- `backend/src/DocGenerator.Application/Services/DocumentService.Actions.cs` — 452
- `backend/src/DocGenerator.Application/Services/DocumentContextBuilder.cs` — 65-66
- `backend/src/DocGenerator.Application/Services/DocumentAppealService.cs` — 473/994-1006
- `backend/src/DocGenerator.Application/Services/DocumentDelegationService.cs` — (راجع 292/489/505: تدوينات التدقيق — تُوحَّد)
- `backend/src/DocGenerator.Application/DTOs/DocumentDtos.cs` — 993-1006 (`NeedsRotationOf` ← `currentYear` معامل)
- `backend/src/DocGenerator.Infrastructure/Persistence/DocumentRepository.cs` — 507
- `backend/src/DocGenerator.Infrastructure/Persistence/StatisticsRepository.cs` — 699
- `backend/src/DocGenerator.Api/Controllers/DocumentsController.cs` — 149
- `backend/src/DocGenerator.Api/Controllers/PortalController.cs` — 75
- `backend/src/DocGenerator.Infrastructure/Security/TokenService.cs` — 41-42
- `backend/src/DocGenerator.Infrastructure/Security/DbLoginRateLimiter.cs` — 48/55/73
- `backend/tests/DocGenerator.Application.Tests/ServiceTests.cs` — تحديث مصانع الخدمات (حقن الساعة)
- `backend/tests/DocGenerator.Application.Tests/EffectiveFileIdentityTests.cs` — حالات `asOfYear` صريح
- `backend/tests/DocGenerator.Application.Tests/TimeProviderBoundaryTests.cs` — حدّيات رأس السنة (جديد)

**واجهة (المرحلة 4):**
- `frontend/src/hooks/useCurrentYear.ts` — هوك (جديد)
- `frontend/src/hooks/useCurrentYear.test.ts` — نجاح/فشل/احتياط
- `frontend/src/context/CurrentYearContext.tsx` — كاش التطبيق (جديد)
- `frontend/src/components/Layout.tsx` — تسليم كاش السنة
- `frontend/src/components/form/documentFormConstants.ts` — حذف `FILE_YEARS`
- `frontend/src/utils/yearOptions.ts` — أفق `[currentYear-5 .. currentYear+4]` (جديد)
- `frontend/src/components/form/DocumentForm.tsx` — 687/908
- `frontend/src/components/form/OccurrencesEditor.tsx` — 11
- `frontend/src/components/RenewalModal.tsx` — 72
- `frontend/src/components/ExecutedStatusModal.tsx` — 146
- `frontend/src/components/appeal/AppealRotationModal.tsx` — 19/112/127
- `frontend/src/pages/Rotation.tsx` — 29/102/141/165

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
