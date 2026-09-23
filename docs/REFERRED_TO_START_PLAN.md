# خطة إضافة الحالة «محال الى البداية» — نظام «طالبة تنفيذ»

> **حالة الوثيقة: مسودة بانتظار موافقة المستخدم — يُمنع بدء التنفيذ قبل الموافقة الصريحة.**
> تاريخ الإعداد: 2026-09-22 · المرجع: طلب المستخدم + تقرير المراجعة الخبيرة.
> تنقيح معتمد (2026-09-22): القرارات 9–12 + تصحيحات B1/B3/B4/F2/F3/T — تحرير ملف فقط، بلا تنفيذ كودي.
> تنقيح النقد الذاتي (2026-09-22): مسح الدخول المباشر بلا أداة + توضيح تطهير العودة + توثيق أثر المبالغ + قرار معلق 13.
> حسم القرار 13 (2026-09-22): الخيار (ب) — `referredSplit` على نمط `deferredSplit`.

---

## 1) الطلب الأصلي

إضافة حالة جديدة باسم «محال الى البداية» إلى نافذة «تغيير الحالة»، تُتاح للملفات
بحالات «المتداول» و«التريث» و«منفذ جبريا — منفذ جزئيا» (دون «منفذ كاملا»)، وتُستخدم عند
«عدم وجود أموال في الملف للتنفيذ عليها»، وتحمل الحقول التالية:

| الحقل | الإلزام | المعنى |
|---|---|---|
| رقم كتاب المطالعة بعدم وجود أموال للتنفيذ عليها | إلزامي | `noFundsDemandNumber` |
| تاريخ كتاب المطالعة بعدم وجود أموال للتنفيذ عليها | إلزامي | `noFundsDemandDate` |
| رقم كتاب الإحالة لقسم البداية | اختياري | `startReferralNumber` |
| تاريخ كتاب الإحالة لقسم البداية | اختياري | `startReferralDate` |

---

## 2) القرارات (13 — نهائية)

1. **الدخول**: من «متداول» و«تريث» و«منفذ جبريا — منفذ جزئيا» فقط (يُستبعد «منفذ كاملا» —
   مساره «تراجع» أو «اعتبار منفذًا»).
2. **شرط عدم وجود الأموال**: توجيه فقط دون منع — ملاحظة إرشادية في النافذة عند وجود
   أموال قابلة للتنفيذ، والانتقال مسموح دائمًا.
3. **الخروج**: عودة متاحة دائمًا **بلا حقول إلزامية** — إلى «متداول» للقادم من متداول/تريث،
   وإلى «منفذ جزئيًا» للقادم من جزئيا (قرار 10) — مع حقول اختيارية موحدة في المسارين:
   رقم الملف الجديد (`renewalFileNumber`)، نوع الملف الجديد (`renewalFileType`)، تاريخ
   التجديد (`renewalDate`)، سنة الإعادة (`renewalYear`)، وتاريخ شطب الملف (`struckOffDate`
   — لعودة-متداول فقط، ويُتجاهل في عودة-جزئيا).
   إذا مُلئت حقول التجديد تصبح المعتبَرة: رقم أساس ونوع جديد لسنة جديدة + وقعة تجديد،
   والسرد المعتمد في وقعة العودة: «أعيد السير به بعد موافاتنا بأموال للتنفيذ عليها
   وجدد الملف برقم كذا نوع كذا تاريخ كذا».
4. **العرض**: حالة عرض مستقلة «محال الى البداية» في كل مكان — شارة مستقلة، خيار فلتر
   مستقل في القائمة الرئيسية **وفي بوابة المندوب**، وعدّاد مستقل في إحصاءات المدير
   وإحصاءات البوابة.
5. **تسطير الإنابة**: محظور على هذه الحالة خلفيًا وواجهيًا (مرآة «تريث»).
6. **إعادة الهيكلة** (`StatusTransitionService` المقترحة بتعليق `Status.cs:41-43`): **تُؤجَّل** —
   تُكتب الدوال الجديدة نظيفةً بنفس بنية الملف القائمة مع توثيق الدَّين.
7. **الملفات بهذه الحالة** تبقى ظاهرة في القوائم وقابلة للنقل الكامل بين المحامين
   والتدوير السنوي (لا استثناءات).
8. **`DashboardStatsDto`** (`/stats/dashboard` — سطح غير معروض حاليًا): يُترك دون تغيير.
9. **الدخول محظور على ملفٍ فيه إنابة سارية** (حسب `DelegationActivityPolicy.IsLifecycleActive` —
   نمط حارس الشطب `S1`): رفض برسالة «لا يجوز إحالة ملف فيه إنابة سارية إلى البداية».
   إنابة **مُتممة** (كبيع أدى لجزئيا) لا تمنع — ليست سارية. ويسري الحظر على الدخول من
   جزئيا أيضًا. وبعد العودة إلى «متداول» يصبح الملف قابلًا للتسطير طبيعيًا (الحظر على
   الدخول فقط ولا يُورَّث).
10. **العودة من جزئيا تستعيد «منفذ جزئيًا»** (لا متداول): تُستعاد `ExecStatus=منفذ جبريا`
    والجزئية محفوظة أصلًا من الدخول (قرار 11). التوجيه بلا عمود جديد — لازمة مثبتة:
    `ExecSubStatus == منفذ جزئيا` على ملف «محال» ⟺ دخل من جزئيا (الكاتب الوحيد لها دخول
    جبريا؛ و«تراجع» يمسحها `Status.cs:293`؛ ودخولا متداول/تريث يمسحانها؛ و«محال» بلا
    مخارج أخرى — مجموعة فارغة + `CanRevert` يستثنيها).
11. **الدخول من جزئيا يُبقي عائلة الجبريا ظاهرة** (انحراف مقصود عن عرف «تراجع» الذي يمسح
    `Status.cs:292-299`): لا يُمسح `ExecSubStatus/Collected*/SoldAssetIds/ForcibleTransfer*`،
    ويُلحق ملخص العرض مقطع الجزئية. دخولا متداول/تريث على المسح الأصلي.
12. **التجديد موحّد في مساري العودة** (التجديد هوية ملف لا حالة)؛ `struckOffDate` لعودة-متداول فقط.
13. ✅ **(محسوم — الخيار (ب)) مبالغ داخل-جزئيا تُبقى في إحصاءات المدير** عبر
    `referredSplit` على نمط `deferredSplit` (اتساقًا مع تريث وقرار 11): حقل
    `ManagerStatsDto.ReferredSplit` + تجميعه في `AggregateManagerStats` (نمط `deferredBanking`/
    `deferredOrdinary` + `AccumulateContract` بما فيها `totalBuckets`) + عرضه في بطاقة
    «محال الى البداية» عبر `<ContractSplit split={stats.referredSplit} />` (مرآة سطري تريث
    154-156) + تحديث تركيبات الاختبارات (`MANAGER_STATS`).

---

## 3) التصميم المعتمد (تسميات نهائية)

- ثابت الحالة: `ExecutionStatusCatalog.ReferredToStart = "محال الى البداية"`، وقيمة
  `ExecutionStatus.ReferredToStart = 6` في `Enums.cs`.
- نوع الوقعة: `OccurrenceTypeCatalog.ReferredToStart = "referred-to-start"`، التسمية «محال الى البداية».
- أعمدة الكيان (`Document`): `NoFundsDemandNumber` ‏(`string?` ≤100)، `NoFundsDemandDate`
  ‏(`DateTime?`)، `StartReferralNumber` ‏(`string?` ≤100)، `StartReferralDate` ‏(`DateTime?`).
- هدف النافذة الجديد: `'محال الى البداية'` (دخول) و`'العودة إلى المتداول'` /
  `'العودة إلى منفذ جزئيا'` (خروج — يُختار حسب اللازمة §2-10، ولا يُعاد استخدام `'تراجع'`
  لكيلا يصطدم بمسار `revert-status` وحقوله الإلزامية).
- نقطة العودة: `POST /api/documents/{id}/return-referred-to-start` بطلب
  `ReturnReferredToStartRequest : RenewalRequest` مضافًا إليه `string? StruckOffDate`،
  ودالة `IDocumentService.ReturnFromReferredToStartAsync`، بصلاحية `CanChangeStatus`
  (كـ«تراجع») ونوع تدقيق `"status"`. نتيجتان: استعادة `منفذ جبريا` (الجزئية محفوظة) عند
  اللازمة، وإلا `متداول`. `struckOffDate` يُطبَّق في نتيجة-متداول فقط ويُتجاهل في نتيجة-جزئيا.
  العودة البسيطة (بلا تجديد) **سلوك جديد خاص بهذه النقطة** — لا «نمط `RestoreStruckOffAsync`»
  (ذاك يُلزم الرقم دائمًا)؛ وعند ملء رقم التجديد تُطبَّق شروط `ApplyRenewalAsync` القائمة كما هي.
- مفاتيح `Details` للوقعة: `noFundsDemandNumber` / `noFundsDemandDate` (بصيغة `yyyy-MM-dd`)
  / `startReferralNumber` / `startReferralDate`.
- شارة الواجهة: بنفسجية `bg-violet-100 text-violet-700` (لون غير مستخدم للحالات).

---

## 4) الجرد الكودي المؤكد (نقاط التماس)

| # | الملف | السطور | الحكم |
|---|---|---|---|
| 1 | `backend/.../Enums/ExecutionStatusCatalog.cs` | 46-49، 56-64، 78-86، 89-99، 106-116، 122-123، 126-137، 140-149 | إضافة الثابت + كل الأذرع + `ExecutedForcibly += ReferredToStart` + تحديث توثيق سطر 102 |
| 2 | `backend/.../Enums/Enums.cs` | 23-31 | إضافة `ReferredToStart = 6` |
| 3 | `backend/.../Enums/OccurrenceTypeCatalog.cs` | 43-46، 48-59، 64-65 | ثابت + `ValidTypes` + `ToLabel` + `IsStatusChange` + تحديث التوثيق |
| 4 | `backend/.../Entities/Document.cs` | ~271-283 | 4 خصائص جديدة بجانب `Tarith*` |
| 5 | `backend/.../Services/DocumentService.Status.cs` | 92-93، 106-111 (نمط)، 115-117، 159-193، 196-204، 236، 248-251، 292-299 (عرف المسح) | فرع `case` جديد + حارس جزئيا + حارس إنابة سارية + ذراع الوقعة |
| 6 | `backend/.../Services/DocumentService.Status.cs` (جديد) | — | `ReturnFromReferredToStartAsync` بنتيجتين (لازمة §2-10) + أداة `ClearReferredToStartFields` (للعودة فقط) + بلا تنبيهات مرآة (عمدًا — قرار 9) |
| 7 | `backend/.../Services/DocumentService.cs` | 55-56 | توقيع الواجهة الجديد |
| 8 | `backend/.../DTOs/DocumentDtos.cs` | 670-673، 845-846 + `ReturnReferredToStartRequest` جديد | 4 حقول استجابة + ربط `FromEntity` |
| 9 | `backend/.../Common/DocumentStatusResolver.cs` | 23-33 | فرع قبل السطر الأخير + توثيق |
| 10 | `backend/.../Common/Audit/DocumentChangeTracker.cs` | 141-144 | 4 تسميات (التتبع تلقائي بالانعكاس) |
| 11 | `backend/.../Services/DocumentDelegationService.cs` | 716-729، 454-459 | منع التسطير + منع التسجيل (نمط `E5`) |
| 12 | `backend/.../Api/Controllers/DocumentsController.cs` | 349-369 (نمط) | نقطة النهاية الجديدة |
| 13 | `backend/.../Persistence/DocumentRepository.cs` | 146-159 | فرع فلتر مستقل |
| 14 | `backend/.../Persistence/PortalRepository.cs` | 152-165 | فرع فلتر مستقل |
| 15 | `backend/.../Persistence/StatisticsRepository.cs` | 490-546، 549-550 | فرع `referredToStartCount` + تجميع `referredSplit` (نمط `deferred`) + الإجمالي |
| 16 | `backend/.../DTOs/StatsDtos.cs` | 88-118 | حقلا `ReferredToStartCount` + `ReferredSplit` + توثيق |
| 17 | `backend/.../Services/PortalService.cs` | 166-182، 210-221 | سلّة + حقل `PortalStatsDto` |
| 18 | `backend/.../DTOs/EntityPortalDtos.cs` | ~82-90 | حقل `ReferredToStartFiles` |
| 19 | `backend/.../Persistence/Configurations/Configurations.cs` | ~181 | `HasMaxLength(100)` للرقمين |
| 20 | هجرتان جديدتان | `Migrations/` + `MigrationsPostgres/` | `AddReferredToStartStatus` |
| 21 | `frontend/src/utils/documentStatus.ts` | 5-30، 44-61، 71-81 | ثابت + مفردة اتحاد + شارة + خيار فلتر + `getDocumentStatus` + `canDelegateSource` |
| 22 | `frontend/src/types/index.ts` | ~346-376، ~760، ~1789 | 4 حقول + `displayStatus؟` توثيق + حقلا الإحصاءات |
| 23 | `frontend/src/components/StatusChangeModal.tsx` | 14-44، 49-100، 117-119 (نمط)، 143-238، 286-406 | حالة/أهداف (جزئيا بفلتر) + عودتان + تجديد موحد/واجهة/ملاحظة التوجيه |
| 24 | `frontend/src/components/view/viewFormat.ts` | 230 (`formatCollectedAmounts`)، ~303-312، 313-320 (نمط)، 377-390 | فرع `buildStatusSummary` + مقطع الجزئية + `case` في `occurrenceLine` |
| 25 | `frontend/src/components/view/OccurrencesModal.tsx` | 180-206 | `case 'referred-to-start'` في التفاصيل |
| 26 | `frontend/src/components/view/StatusCard.tsx` | 31 | يعمل آليًا عبر `buildStatusSummary` |
| 27 | `frontend/src/components/dashboard/ManagerStatsSection.tsx` | 124، 151-156 (نمط) | بطاقة سادسة + `<ContractSplit split={stats.referredSplit} />` (الشبكة إلى 6/7 أعمدة) |
| 28 | `frontend/src/components/dashboard/dashboardIcons.tsx` | 3-59 | أيقونة جديدة (سهم إحالة) |
| 29 | `frontend/src/pages/PortalFiles.tsx` | 15-21، 154-167 | خيار فلتر + بطاقة سادسة |
| 30 | `DocumentsList.tsx` | 521، 708 | يعمل آليًا عبر `STATUS_OPTIONS` |

**أسطح تبيّن أنها لا تحتاج تغييرًا**: `DelegationActivityPolicy` (الحالة غير نهائية —
صحيح)، `ApplyDelegationInheritanceOrRecoveryAsync` (تُتجاهل تلقائيًا)، `DelegationStatusAlertKindFor`
(`_ => null`)، التدوير (`IsExecuted` = false — تبقى مؤهلة)، `NeedsRotationOf`، التصدير
(`DisplayStatus`)، صفحتا المنفذة/المشطوبة، `targetsOf` (وضع «منفذ عليها»).

---

## 5) مراحل التنفيذ (بالترتيب — تُنفَّذ بعد الموافقة فقط)

### المرحلة B1 — الكتالوجات والقيم
1. `ExecutionStatusCatalog`: الثابت + `ValidStatuses` + `Classify` + `ToLabel` +
   فرع `CurrentState` قبل السقوط الأخير «متداول» (سطر 98) + إلحاق `ReferredToStart` بمجموعات
   `StateCirculating` و`Deferred` **و`ExecutedForcibly`** في `AllowedStatusChanges` (في النهاية)،
   ومجموعة فارغة للحالة نفسها + أذرع `ToStateLabel`/`ToStatusLabel` + تحديث توثيق
   `AllowedStatusChanges` (سطر 102: تُذكر «محال» مع الحالات الأربع).
2. `Enums.cs`: `ReferredToStart = 6`.
3. `OccurrenceTypeCatalog`: الثابت + `ValidTypes` + `ToLabel` («محال الى البداية») +
   `IsStatusChange` + تحديث التوثيق.
- **قبول المرحلة**: `dotnet build` للـ`Domain` أخضر.

### المرحلة B2 — الكيان والإعدادات والهجرتان
1. `Document.cs`: الخصائص الأربع بتوثيق عربي بجانب `Tarith*`.
2. `Configurations.cs`: `HasMaxLength(100)` للحقلين الرقميين.
3. توليد هجرة `AddReferredToStartStatus` لكل سياق (`DocGeneratorDbContext` ثم
   `DocGeneratorPostgresDbContext`) ومراجعة `Up`/`Down` و`ModelSnapshot`.
- **قبول المرحلة**: الهجرتان تُطبَّقان محليًا صعودًا وهبوطًا دون أخطاء.

### المرحلة B3 — مسار الدخول (`UpdateStatusAsync`)
فرع `case ExecutionStatusCatalog.ReferredToStart` قبل `default`:
0. حارس الإنابة السارية (قرار 9 — نمط `S1`): عند هذا الهدف فقط تُجلب
   `ListBySourceAsync`، فإن وُجدت سارية رُفض برسالة «لا يجوز إحالة ملف فيه إنابة سارية
   إلى البداية». (حارس المناب القائم سطر 100 يمنع المناب أصلًا.)
1. `RequireField` للحقلين الإلزاميين برسالتَي: «رقم كتاب المطالعة بعدم وجود أموال
   للتنفيذ عليها» و«تاريخ كتاب المطالعة بعدم وجود أموال للتنفيذ عليها».
2. التاريخان عبر `ParseDateTime` (قاعدة Date Fields Rule)؛ الاختياريان يُحفظان
   فارغين كـ`null`.
3. `CopyDetail` للمفاتيح الأربعة (التواريخ بصيغة `yyyy-MM-dd` على نمط `RecoveryDetails`).
4. تطهير منشطّر حسب المصدر (قرار 11) — **تعيين مباشر بلا أداة مسح في الدخول**
   (الإلزاميان يُكتبان، والاختياريان `= value ?? null` عند الفراغ؛ لا حاجة لمسح ما سيُكتب فوقه):
   - من جزئيا: **يُحفظ** `ExecSubStatus/Collected*/SoldAssetIds/ForcedExecution*` (ظاهرة)؛
     ويُشترط `ExecSubStatus == SubPartiallyExecuted` وإلا رُفض برسالة
     «الإحالة إلى البداية من «منفذ جبريا» متاحة فقط للملف المنفذ جزئيًا» — من «كاملا» مرفوض.
   - من متداول/تريث: تطهير كل العائلات (`ClearBaraetFields`/`ClearTarithFields`/
     `ClearSayerFields`/`ClearForcedExecutionField`/`ClearForcibleTransferFields`/
     `ClearCollectedFields`/`ExecSubStatus=null`/`SoldAssetIds=null`).
   أداة `ClearReferredToStartFields` الجديدة **للعودة فقط** (تمسح الحقول الأربعة عند الخروج).
   **لا تُمسح** حقول `Renewal*`/`StruckOffDate` في الحالين (مرآة `Revert`).
5. ذراع خريطة الوقعة `ExecutionStatus.ReferredToStart => OccurrenceTypeCatalog.ReferredToStart`
   مع `EventDate = UtcNow` و`FileNumber/FileType/Year = null` (مرآة `deferred`).
- **قبول المرحلة**: انتقال من متداول وتريث وجزئيا ينجح؛ من تحت رفع/منفذ-كاملا/مشطوب/مناب
  يُرفض؛ دخول مع إنابة سارية يُرفض؛ نقص الإلزامي يُرفض؛ دخول-جزئيا يُبقي المحصل ظاهرًا؛
  الوقعة بتفاصيلها مسجلة.

### المرحلة B4 — مسار العودة (جديد مستقل، بنتيجتين)
1. `DocumentDtos.cs`: `ReturnReferredToStartRequest : RenewalRequest` + `string? StruckOffDate`.
2. `DocumentService.Status.cs`: `ReturnFromReferredToStartAsync`:
   - حراس: موجود، صفة طالبة تنفيذ، ليس منابًا، الحالة الحالية `ReferredToStart`
     (رسائل على نمط `RevertStatusAsync`).
   - التوجيه باللازمة (§2-10 — بلا عمود جديد): إن `ExecSubStatus == SubPartiallyExecuted`
     فالنتيجة استعادة `ExecStatus = ExecutedForcibly` (**دون مسّ عائلة الجبريا إطلاقًا** —
     الجزئية ومحفوظاتها تبقى كما دخلت) وإلا `ExecStatus = None` (متداول) + بقية التطهير
     (مرآة التراجع — وهي عمليًا لا-عملية إذ أُنجز المسح عند الدخول).
   - `ClearReferredToStartFields` في الحالين؛ `struckOffDate` الاختياري → `ParseDateTime` →
     `doc.StruckOffDate` **في نتيجة-متداول فقط** (يُتجاهل في نتيجة-جزئيا).
   - حقول التجديد **موحدة في المسارين** (قرار 12): إن مُلئ `renewalFileNumber` →
     `ApplyRenewalAsync(doc, request, executedLike:false, …)` (يُلزم السنة 1900–2100
     ويطابقها مع سنة التاريخ — السلوك القائم). العودة البسيطة **سلوك جديد خاص بهذه
     النقطة** (لا «نمط `RestoreStruckOffAsync`» — ذاك يُلزم الرقم دائمًا).
   - وقعة `Revert` بتفاصيل: السرد الآلي («أعيد السير به بعد موافاتنا بأموال للتنفيذ عليها…»
     / «…وعاد منفذًا جزئيًا…» حسب النتيجة) + حقول الشطب/التجديد إن وُجدت.
   - `LogDocumentChangesAsync(..., "status", …)` + استدعاء `ApplyDelegationInheritanceOrRecoveryAsync`
     (لاتساق الموروث-تريث `D3`) — و**بلا تنبيهات مرآة عمدًا** (مبرَّر: قرار 9 يجعل الإنابة
     السارية على «محال» مستحيلة؛ يُوثَّق «عمدًا» حتى لا يُقرأ ثغرة).
   - ملاحظة دَين: توثيق أن الاستخراج إلى `StatusTransitionService` مؤجل بقرار معتمد.
3. `IDocumentService` + نقطة `[HttpPost("{id:int}/return-referred-to-start")]` بصلاحية
   `CanChangeStatus` (نمط `RevertStatus`).
- **قبول المرحلة**: عودة بسيطة بنتيجتيها؛ عودة بتجديد موحد (رقم أساس + وقعتان)؛ سنة فاسدة
  تُرفض؛ عودة من غير «محال» تُرفض؛ مناب يُرفض.

### المرحلة B5 — DTO/تدقيق/عرض/إنابة
1. `DocumentResponse`: الحقول الأربعة + ربط `FromEntity` (التواريخ `yyyy-MM-dd`
   `InvariantCulture` على نمط `ForcibleTransferDate`).
2. `FieldLabels`: 4 تسميات عربية.
3. `DocumentStatusResolver`: الفرع + تحديث التوثيق.
4. `ValidateSourceForDelegation`: منع التسطير + منع التسجيل برسالة على نمط `E5`.

### المرحلة B6 — الفلاتر والإحصاءات
1. `DocumentRepository` + `PortalRepository.ScopedQuery`: فرع `ExecStatus == ReferredToStart`.
2. `AggregateManagerStats`: فرع `referredToStartCount` + تجميع `referredBanking/referredOrdinary`
   والسلال عبر `AccumulateContract` (نمط `deferred` حرفيًا — بما فيها `totalBuckets`)؛ ملف جزئيا
   ينتقل بعدّه ومبالغه من `active` إلى الفرع الجديد؛ `TotalFiles` يصبح
   `active + drafts + deferred + referredToStart`؛ حقلا `ManagerStatsDto.ReferredToStartCount`
   + `ReferredSplit` (يظهران تلقائيًا في `/stats/me` أيضًا).
3. `PortalService.GetStatsAsync`: سلّة `referredToStartFiles` **تحل محل** عدّه السابق
   (داخل-جزئيا كان في `executedFiles` — السلة التنفيذية تبتلع جبريا بأي فرع) + حقل
   `PortalStatsDto.ReferredToStartFiles`؛ فلتر البوابة `ScopedQuery`: فرع مستقل للحالة
   الجديدة، وفلتر `'منفذ'` لا يشملها.

### المرحلة F1 — أساسيات الواجهة
1. `documentStatus.ts`: الثابت + مفردة `DocumentStatus` + مدخل `STATUS_BADGES`
   (بنفسجي) + إلحاق `STATUS_OPTIONS` (نهايةً) + فرع `getDocumentStatus` +
   `canDelegateSource ⇒ false`.
2. `types/index.ts`: الحقول الأربعة + حقلا الإحصاءات + تحديث توثيق `displayStatus`.

### المرحلة F2 — نافذة تغيير الحالة (`StatusChangeModal.tsx`)
1. `currentStateOf`: فرع `'محال الى البداية'` (يُعرض مع جزئيته إن وُجدت).
2. `allowedTargetsOf`: إلحاق `'محال الى البداية'` لمتداول وتريث + لمنفذ-جبريا مع فلتر
   `execSubStatus === 'منفذ جزئيا'` (نمط سطر 117-119 — يبقى الافتراضي «تراجع») +
   `case 'محال الى البداية'`: اللازمة (§2-10) — جزئيا ⇒ `['العودة إلى منفذ جزئيا']`
   وإلا `['العودة إلى المتداول']` (حارس المناب القائم يكفي).
3. `StatusFields`/`emptyFields`: الحقول الأربعة + `renewalFileNumber`/`renewalFileType`/
   `renewalDate`/`renewalYear` + إعادة استخدام `struckOffDate` القائم (يُعرض في عودة-متداول فقط).
4. `buildPayload`: إلزاميّا الدخول (بعد `normalizeArabicDigits`) + اختياريّاه؛
   العودتان: حقول التجديد موحدة + تحقق «رقم جديد ⇒ رقم وسنة إلزامان».
5. `submit`: فرع `POST …/return-referred-to-start` للعودتين (نفس النقطة — الخادم يوجّه).
6. كتلة حقول الدخول (`grid sm:grid-cols-2` + `placeholder="مثال: 1/8/2026"`) وكتلتا
   حقول العودة + **ملاحظة التوجيه** (كهرمانية، غير مانعة) عند
   `target === 'محال الى البداية'` ووجود أموال `isAuctionableKind`.
7. التزام `min-h-11` و`aria-label` و`focus-visible` القائم.

### المرحلة F3 — العرض والوقوعات واللوحات
1. `viewFormat.ts`: فرع `buildStatusSummary` («محال إلى قسم البداية لعدم وجود أموال
   للتنفيذ عليها بموجب كتاب المطالعة رقم … بتاريخ …» + كتاب الإحالة إن وُجد) +
   إن `execSubStatus == منفذ جزئيا` أُلحق مقطع الجزئية على نمط فرع جبريا (313-320):
   `(منفذ جزئيا)` + `المبلغ المحصل: …` عبر `formatCollectedAmounts` + سطرَي التحويل +
   `case 'referred-to-start'` في `occurrenceLine` و`StatusChangeOccurrenceDetails`.
2. `ManagerStatsSection`: بطاقة «محال الى البداية» بقيمة `stats.referredToStartCount`
   و`<ContractSplit split={stats.referredSplit} />` (مرآة بطاقة تريث 154-156؛ الشبكة
   `xl:grid-cols-6/7`) + أيقونة جديدة في `dashboardIcons.tsx`.
3. `PortalFiles`: خيار الفلتر + بطاقة إحصاء سادسة (الشبكة إلى 6).

### المرحلة T — الاختبارات (تُكتب مع كل مرحلة، وتُجمَع هنا)
- خلفية: `ExecutionStatusCatalogTests` (جدول الانتقالات المحدّث + `ValidStatuses` +
  `Classify`/`ToLabel` + عقود اللازمة: `CanRevert(ReferredToStart)==false` و
  `IsExecuted("محال الى البداية", null)==false` و`IsExecuted("محال الى البداية", "منفذ جزئيا")==false`)، `ServiceTests` (دخول/رفض/تطهير/وقعة/عودة بسيطة بنتيجتيها وبتجديد
  موحد وسنة فاسدة وعودة مرفوضة ومناب + دخول-جزئيا يُبقي المحصل/دخول-كاملا مرفوض/دخول مع
  إنابة سارية مرفوض/عودة-جزئيا تستعيد جبريا)، `DocumentStatusResolverTests`، `StatisticsTests` +
  `ManagerStatsIntegrationTests` (انتقال جزئيا `active`→العداد)، `PortalStatsTests`،
  `DocumentsIntegrationTests` (الجديدة)، منع التسطير/التسجيل.
- واجهة: `StatusChangeModal.test` (الأهداف ومنها جزئيا/العودتان/Payload/النقطة/الملاحظة)،
  `documentStatus.test` (شارة/اتحاد/تفويض)، `viewFormat.test` (ملخص محال بمقطعيه)،
  `OccurrencesModal/Card.test` (وقعة `revert` للعودة تحمل مفاتيح تجديد/شطب بلا حقول سير —
  العرض بالسرد النصي)، `StatusCard.test`، `DocumentsList.test`،
  `Dashboard.test` (تركيبتا `MANAGER_STATS` + `referredSplit`)، `PortalFiles.test`.
- حدّيات: فراغ، أرقام عربية، صيغ التواريخ السبع، سنة خارج 1900–2100.

### المرحلة V — التحقق النهائي الإلزامي
`dotnet test` · `npx oxlint src` · `npx tsc -b` · `npx vitest run` · `npm run build` —
كلها خضراء، + تدقيق العقود (§6) + فحص `rg` لبقايا الأسماء + فحص جوال بصري للنافذة.

---

## 6) تدقيق العقود عبر المسار (يُنفَّذ بعد التنفيذ — حقلًا بحقل)

نافذة → `payload` → خدمة → كيان → DTO → عرض/وقوعات/إحصاءات:
- `noFundsDemandNumber ↔ NoFundsDemandNumber ↔ NoFundsDemandNumber` ‏(`string?`)،
  `noFundsDemandDate ↔ NoFundsDemandDate ↔ NoFundsDemandDate` ‏(`DateTime?` → `"yyyy-MM-dd"`)،
   والمثل للاختياريَّين؛ مفاتيح `Details` مطابقة لمفاتيح `StatusChangeOccurrenceDetails`
   (`d.*`)؛ `displayStatus/record` ↔ مفردة `DocumentStatus` ↔ `STATUS_BADGES`؛
   `ManagerStatsDto/PortalStatsDto` خلفي/أمامي **بما فيها `ReferredSplit`** (عدد + مصرفي/عادي
   + مبالغ — مرآة `deferredSplit`)؛ فلتر القائمة/البوابة ذهابًا وإيابًا؛
   لازمة العودة (`ExecSubStatus==جزئيا` ⟺ دخل من جزئيا) مطابقة في النافذة والخدمة؛
   ملخص محال-جزئيا يعرض المحصل (`formatCollectedAmounts`) مطابقًا لفرع جبريا.

---

## 7) ⚠️ تنبيه النشر (يُدرج في تقرير الإنجاز — إلزامي)

هجرتان جديدتان `AddReferredToStartStatus` (SQLite + Postgres). عند النشر يجب:
`dotnet ef database update --context DocGeneratorDbContext` و
`dotnet ef database update --context DocGeneratorPostgresDbContext`
(المرجع: `RUN_GUIDE.md` §9) — وإلا فشل تشغيلي رغم خضرة الاختبارات.

---

## 8) معايير قبول المهمة

1. كل بنود §5 منفذة واختباراتها خضراء.
2. تدقيق §6 مكتمل بلا انحراف.
3. أدوات §V كلها خضراء.
4. تنبيه §7 مثبّت في تقرير الإنجاز.
5. لا تغييرات كاسرة خارج ما ذُكر (عقود `StatusRequest`/`RenewalRequest`/النقاط القائمة
   محفوظة).
