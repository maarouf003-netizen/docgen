# خطة: طيّ القيود المتطابقة داخل توحيد التسمية (`Unify Fold-on-Collision`)

> الحالة: **نُفّذت كاملة (U1→U5) — وُثّق تالقرار المثبت في سجل التنفيذ أدناه**.
> انحرافات تنفيذية مقصودة عن نص الخطة (مُثبتة أيضًا في الملحق):
> - المساعد سُمّي `RepointEntryLinks` (متزامن) بدل `RepointEntryLinksAsync` (لا await بداخله).
> - `targetKeySet` استُبدل بـ`survivorPool` حصرًا (تطابق مُثبت بين المجموعتين) — الناجي يُبحث عنه مباشرة في القائمة العاملة.
> - كتلة البدائل في طيّ `MoveEntryAsync` بقيت inline (لا تُحوَّل لـ`AddFoldAliases`) تفاديًا لتغيير سلوك إضافة السوابق (مسار النقل لا يضيف سوابق).
> - `UnifyNamesAsync` يملک الآن `survivorPool` = نسخ القيود النشطة للهدف + كل منقول؛ والاستدعاء `MigrateDelegatesAsync(..., null, entryTargetByAbsorbed, token)`.
> - المعاينة استبدلت جملة «تعارض» القديمة بالمحاكاة التجميعية (`F2`/`G2`) بلا تحذير طي.
> سجل التنفيذ: U1/U5 أخضر، U2+اختبارات (1–6) 831/831، U3 أمامي 689/689، U4 أخضر — بتاريخ 06/09/2026.
> القرار المثبت: **الخيار ب البسيط** — طيّ المطابق حرفيًا (`Ordinal`) داخل `UnifyNamesAsync`،
> بلا استثناءات فرعية، وبلا أي fallback من نوع «أول قيد».
> المرجع التشخيصي: الهوية على مستويين (مجموعة/قيد)؛ التوحيد الحالي يحلّ الأول فقط
> (`PublicEntityService.cs:2196` ينقل الصف كما هو) فيرفض عند تصادم المستوى الثاني (`:2187-2188`).
> لا تغيير في مخطط قاعدة البيانات في أي بند → **لا هجرات EF → لا تنبيه نشر** (`RUN_GUIDE.md §9` غير مطلوب).

## البنود

### `U1` — استخراج مساعدَي الطيّ المشتركين (إزالة تكرار، صفر تغيير سلوكي)
- الملف: `backend/src/DocGenerator.Application/Services/PublicEntityService.cs`.
- المصادر الحالية للمنطق (سيمانتك واحدة — يُستهلك ثالثًا في التوحيد):
  1. إعادة توجيه روابط `RegistryId` الثلاث + `Derive` — الدمج (`:1880-1889`)، النقل-طي (`:1423-1432`).
  2. بدائل الطيّ (معياري + كامل `«name — gov / branch»` + سوابق) — الدمج (`:1894-1937`)، النقل-طي (`:1438-1458`، بلا سوابق).
- التغيير — خاصتان جديدتان بنفس الدلالة الحرفية لمنطق الدمج (المرجع الأكمل):
  1. `RepointEntryLinksAsync(Document doc, int fromEntryId, int toEntryId)` — الحلقات الثلاث + `ApplicantRegistryIdDeriver.Derive(doc)`؛ تُستدعى من الدمج والنقل-طي والتوحيد.
  2. `AddFoldAliases(PublicEntity targetEntry, string absorbedGroupName, PublicEntity absorbedEntry, ref int aliasesAdded)` — المعياري + الكامل + ترحيل السوابق مع نفس شروط الاستثناء (`:1923-1929`).
     (مستثنى بوعي: مسار الإلغاء `:2721-2743` — حلقته تستبدل الأسماء مباشرة `a.Name = newCanonical`، سيمانتك مختلفة فلا تدخل المساعد.)
- القبول: `dotnet test` أخضر كاملًا دون أي تعديل على الاختبارات (إعادة هيكلة صرفة؛ اختبارات الدمج/النقل القائمة هي الحارس).

### `U2` — الطيّ داخل `UnifyNamesAsync` (السلوك الجديد)
- الملف: `PublicEntityService.cs` (`:2131-2317`).
- التغيير:
  1. استبدال حلقة الرفض (`:2184-2189`) بفرعين لكل قيد ممتصّ نشط، مع قائمة ناجين عاملة
     `survivorPool` تبدأ بنسخة `activeTarget` ويُلحَق بها كل قيد **منقول** (ضروري لحتمية الاختبار 3 أدناه —
     الكود الحالي لا يُلحق المنقول بأي قائمة، فمطابقة الناجي على `activeTarget` وحدها كانت ستفشل خطأً):
     - المفتاح `gov|branch` (باني المفاتيح الحالي `targetKeySet` بـ`Ordinal` في `:2151`) موجود ← **طيّ**:
       الناجي = `survivorPool.First(se => se.Governorate == ae.Governorate && se.BranchName == ae.BranchName)`
       (مطابقة صريحة كالدمج `:1864-1865`؛ غيابه مستحيل ← `throw` دفاعي، **وممنوع أي `?? First()`** —
       ذلك سطر الدمج `:1867` ولا يدخل التوحيد)؛ إعادة التوجيه عبر مساعد `U1`؛ جمع الملفات المرتبطة في
       `affectedDocsById` (كـ`:1873-1878`)؛ `ae.IsActive = false`؛ `entryTargetByAbsorbed[ae.Id] = survivor.Id`؛
       البدائل عبر مساعد `U1`؛ `entriesFolded++`. لا يُضاف إلى `movedEntryIds` (روابطه جُمعت) ولا إلى `targetKeySet` (مفتاحه حاضر).
     - وإلا ← نقل كما اليوم (`:2193-2212`) مع `targetKeySet.Add` القائم **+ إلحاق `ae` بـ`survivorPool`**.
  2. المندوبون (`:2254`): `MigrateDelegatesAsync(absorbedIdsSet, targetGroup.Id, null, entryTargetByAbsorbed, token)` —
     الخريطة + `default: null`: المطوي يرحل لناجيه (`:2441-2444`)، والمنقول (غير المُدرج) يبقى على قيده صحيحًا. يستبدل `null, null`.
  3. التدقيق: الحمولة (`:2257-2270`) += `entriesFolded` + مصفوفة `folds` (`absorbedEntryId/targetEntryId/gov/branch/linkedDocs`)؛
     نص `audit` (`:2304`) يذكر المطوي؛ الوقوعات والاستئنافات بلا تغيير (المطوي ضمن `allAffectedDocs` أصلًا عبر `affectedDocsById`).
   4. الاستجابة: `UnifyNamesResponse` (`EntityRegistryDtos.cs:291-296`) += `int EntriesFolded` (غير كاسرة على مستوى
      JSON/API؛ تُحدَّث الإنشاء الوحيد `:2315` والاختبارات — بند 5 من الاختبارات)؛ الطلب بلا تغيير.
  5. الحرّاس بلا مساس: `NeedsReview` (`:2148`، `:2179`)، غير نشط (`:2137`، `:2175`)، ذاتي (`:2143`) — كلها ترفض قبل أي طيّ.
  6. سياسة المفتاح (قرار صريح `F4`): المطابقة نصّية خام (`==` الافتراضية = `Ordinal`) كسيمانتك الدمج (`:1864-1865`)
     والحرّاس — فرق فراغ زائد يعني «نقلًا» لا «طيًّا»؛ لا تقليم في هذه الخطة (اتساقًا مع `EnsureNoDuplicateEntryAsync:1282-1293`).
- القبول: سيناريو المستخدم الحرفي (فرع واحد تحت تسميتين) يُتوَّج بنجاح بدل `تعارض`.

### `U3` — المعاينة تُظهر الطيّ بالكم + عرض المودال
- الملفات: `PublicEntityService.cs` (`PreviewUnifyAsync :2075-2128`)، `EntityRegistryDtos.cs:269-280`،
  `frontend/src/types/index.ts:1566-1578`، `frontend/src/components/entity/UnifyNamesModal.tsx:135-137` و`:273-300`.
- التغيير:
  1. DTO جديد `EntryFoldPreviewDto(AbsorbedGroupId, AbsorbedGroupName, Governorate, BranchName, LinkedDocumentCount)` —
     العدد عبر `ListDocumentsLinkedToEntryAsync` (قراءة فقط)؛ حقلا `FoldsToApply` + `TotalEntriesFolded`
     في `UnifyNamesPreviewResponse` (إضافتان غير كاسرتين).
  2. المعاينة — محاكاة مطابقة للتنفيذ (إلزامي، نتيجة المراجعة `F2`): تُبنى مجموعة مفاتيح تجميعية تبدأ بمفاتيح
     الهدف النشط، وتُعالَج الممتصات بنفس ترتيب الطلب (`Distinct()`)؛ مفتاح حاضر = طيّ (سطر «سيُطوى القيد `gov/branch`
     من `absorbed` على `target` — `N` ملفًا» **يستبدل** سطر «تعارض» الحالي `:2116-2121`)، ومفتاح جديد = نقل +
     إلحاقه بالمجموعة التجميعية؛ وعدّاد `totalToMove` يُنقل من الجملة التجميعية على مستوى المجموعة (`:2111`:
     `totalToMove += activeAbsorbed.Count` — تُحذف) إلى داخل حلقة القيد فيُزاد للمنقول فقط. بهذا تطابق المعاينة
     التنفيذ حتى في تصادم «ممتصة↔ممتصة» (سيناريو الاختبار 3)؛ تحذيرات `NeedsReview`/النوع كما هي؛
     `TotalEntriesToMove` = المنقول فقط، `TotalEntriesFolded` = المطوي (تُوثَّق الدلالة في المواصفة — بند `U5`).
  3. المودال: قسم طيّ يسرد `foldsToApply` بالعدّاد (`Intl.NumberFormat('ar-EG')` كاتفاقية `EntityRegistryReviewManagement.tsx:30` + `tabular-nums`)؛
     سطر الملخص (`:276-280`) يذكر المطوي عبر `totalEntriesFolded`؛ رسالة النجاح (`:135-137`) تذكر المنقول والمطوي (توسيع الـ`cast` المضمّن + `UnifyResponse` في `types/index.ts:1588-1594` += `entriesFolded`)؛
     الحقلان مطلوبان في `UnifyPreviewResponse` (`types/index.ts:1573-1578`) فيُحدَّث الـ`mocks` الثلاثة (`UnifyNamesModal.test.tsx:122,143,167`) بإضافة `foldsToApply: []` و`totalEntriesFolded: 0`
     (سيفشل `tsc` بدونهما — وهو الحارس المقصود) مع حارس عرض `?? []`؛
     عدّاد كل مجموعة (`ag.entryCount` في `:293-294`) يبقى «كل القيود النشطة» (منقول+مطوي) — يتصالح مع سطر
     الملخص («سيُنقل 1 ويُطوى 1» مقابل «2 قيد») فلا يُغيَّر؛ التخطيط مكدس أصلًا (mobile-first محفوظ)،
     والقوائم بـ`<ul>/<li>` كما القائم (وصولية محفوظة).
     `SimilarGroupsUnifyTab.tsx:125,276` يستضيف المودال نفسه فلا عرض معاينة آخر للتوحيد يُحدَّث؛ `MergeModal` مستقل بلا مساس؛
     (مُتحقق: `AllEntitiesUnifyTab.tsx` المذكور في المواصفة كـ`NEW` غير موجود أصلًا — المودال هو السطح الوحيد يقينًا).
- القبول: فحص بصري 375px بلا تجاوز؛ `preview` بلا طيّ يُخفي القسم.

### `U4` — إصلاح ثغرة مندوبي طيّ `MoveEntryAsync` (نفس الحزمة)
- الملف: `PublicEntityService.cs` (وضع الطيّ `:1405-1461`).
- الثغرة المثبتة: وضع الطيّ يُبطل القيد ولا يمسّ المندوبين أصلًا (لا استدعاء لـ`MigrateDelegatesAsync` في `MoveEntryAsync` كاملًا —
  فحص `grep`: الاستدعاءات في `:2001`، `:2254`، `:2785` فقط) فيبقى مندوب القيد المطوي معلقًا على قيد ميت.
- التغيير (دقيق ومحصور بوضع الطيّ فقط): بعد `:1436`، إعادة توجيه المستخدمين ذوي `PortalEntryId == entryId` إلى
  `(toGroupId, targetEntryId)`؛ **ولا** تُستخدم `MigrateDelegatesAsync` هنا قصدًا — دلالتها على مستوى المجموعة
  (`absorbedIds`) ستهاجر مندوبي المجموعة المصدر كلهم خطأً؛ المطلوب استهداف قيد واحد فقط.
  يتطلب دالة مستودع جديدة (مُتحقق: `IUserRepository` يملك فقط `ListEntityManagers[ByGroupIds]Async` —
  تُضاف e.g. `ListEntityManagersByEntryIdAsync` مع تنفيذها واختبارها ضمنيًا عبر اختبار `U4` أدناه).
  وضع إعادة التعيين (`:1463-1493`) خارج النطاق عمدًا (يدخل في نطاق رئيس القسم `EnsureHeadScopeAsync` ويحتاج تحليلًا مستقلًا).
- القبول: اختبار `U4` أدناه (مندوب المطوي يرحل؛ مندوب مجموعة المصدر بلا مساس).

### `U5` — توثيق المواصفة
- الملف: `docs/ENTITY_UNIFY_SPEC.md` (`:105-116` سلوك `UnifyNamesAsync`، `:130-132` تنبيه النشر).
- التغيير: بند «طيّ المطابق حرفيًا» (القاعدة + بلا fallback + الحرّاس + سياسة المفتاح الخام) + دلالة `TotalEntriesToMove`
  (منقول فقط) و`TotalEntriesFolded` (مطوي)؛ سطر «لا هجرات» يبقى.

## الاختبارات (إلزامية — تُكتب مع السلوك لا بعده)

خلفي (`backend/tests/DocGenerator.Application.Tests/PublicEntityServiceTests.cs` بجانب `:1704`):
1. `Unify_MatchingBranch_FoldsOntoSurvivor` — **يحوّل** `Unify_DuplicateEntryConflict_Throws` (`:1704-1710`) جذريًا (السلوك تغيّر قصدًا):
   بذر هدف (`دمشق/الرئيسي`) + ممتص (`دمشق/الرئيسي` + `حلب/فرع`)؛ التحقق: لا استثناء، `EntriesMoved=1`، `EntriesFolded=1`،
   المطوي `IsActive=false`، المنقول `GroupId=target`، الممتصة معطلة، البدائل الثلاثة (معياري/كامل/سوابق) على الناجي،
   الحمولة تحوي `entriesFolded=1` ومصفوفة `folds` بطول 1.
2. `Unify_Fold_RepointsDocumentsAndDelegates` — سيناريو المستخدم الحرفي: ملف مربوط بالمطوي عبر الأنواع الثلاثة
   (`Applicant/Executed/ExecutionApplicants`) + مندوب قيدي على المطوي + مندوب قيدي على المنقول؛ التحقق:
   `RegistryId` الثلاثة ← الناجي، `Derive` صحيح، النص أُعيد بناؤه بالاسم الموحّد، مندوب المطوي ← (`targetGroup`, `survivorEntry`)،
   مندوب المنقول بلا مساس، وقوع `entity-change` للملف، `totalAffectedDocs` يحسبه.
3. `Unify_TwoAbsorbedSameKey_MovesFirstFoldsSecond` — ممتصتان بنفس المفتاح والهدف يخلو منه: الأول يُنقل (حتمية ترتيب `Distinct()`)
   والثاني يُطوى عليه؛ التحقق من الحتمية عبر عكس ترتيب الطلب في حالة ثانية.
4. `PreviewUnify_ListsFoldsWithCounts` — **يحوّل** `PreviewUnify_WarnsOnGovernorateConflict` (`:1645-1651`):
   `FoldsToApply` بطول 1 مع `LinkedDocumentCount` الصحيح، `TotalEntriesToMove=0`، `TotalEntriesFolded=1`،
   و`Warnings` بلا «تعارض» بل سطر «سيُطوى»؛ + حالة ثانية (ممتصتان بنفس المفتاح والهدف يخلو منه):
   `FoldsToApply` بطول 1 (الثانية تُطوى على الأولى المنقولة)، `TotalEntriesToMove=1` — إثبات تطابق المعاينة مع التنفيذ (اختبار 3).
5. بقاء الأخضر بلا تعديل: `Unify_MovesEntriesAndDeactivatesGroupsAndLogsEvent` (`:1668` + `EntriesFolded=0`)،
   `Unify_KeepsOldNamesAsSearchOnlyAliases` (`:1692`)، `Unify_NeedsReview_Throws` (`:1713`)، `Unify_WithDecree_*` (`:1767-1791`)،
   وكل اختبارات الدمج (حارس إعادة هيكلة `U1`).
6. `MoveEntry_Fold_MigratesEntryDelegates` — مندوب القيد المطوي ← (`toGroupId`, `targetEntryId`)؛ مندوب مجموعة المصدر بلا مساس.

أمامي (`UnifyNamesModal.test.tsx` بجانب `:146`):
7. `preview lists folds with document counts` — معاينة تحوي `foldsToApply` ← قسم الطيّ ظاهر بالعدّاد العربي؛
   ومعاينة بلا طيّ ← القسم غائب؛ رسالة النجاح تذكر المطوي (تحديث الـ`mock` في `:146` بإضافة `entriesFolded`).

## ضوابط الأصول الصارمة (إلزامية قبل إعلان الإنجاز)

1. تدقيق العقود حقلًا بحقل عبر المسار: `UnifyNamesRequest` (ثابت) → `UnifyNamesAsync` → `UnifyNamesResponse` (+`EntriesFolded`) →
   `types/index.ts` → المودال؛ و`UnifyNamesPreviewRequest` (ثابت) → `PreviewUnifyAsync` → `UnifyNamesPreviewResponse`
   (+`FoldsToApply`/`TotalEntriesFolded`/`EntryFoldPreviewDto`) → `types/index.ts` → المودال؛ أنواع `DateTime?`/`decimal?` للمرسوم بلا مساس.
2. فحص البقايا عبر `rg`: `"تعارض: القيد"` تُحذف من `:2120` و`:2188` وتبقى في `MoveAllEntriesAsync:1607` فقط
   (حارس إعادة التعيين المشروع هناك)؛ نص `MoveEntryAsync:1480-1481` مختلف («يوجد قيد مطابق») — بلا مساس؛
   `null, null` في `:2254` يُستبدل؛ `PreviewUnify_WarnsOnGovernorateConflict` و`Unify_DuplicateEntryConflict_Throws` يُحوَّلان ولا يُحذفان عبثًا.
3. قائمة التحقق مقابل هذه الخطة: كل بند `U1`–`U5` وكل اختبار 1–7 يُطابَق مع المنفَّذ؛ أي انحراف يُقاس ويُعلَن.
4. الاصطلاحات: التمثيل النقدي/العددي `Intl.NumberFormat('ar-EG')` + `tabular-nums` (لا صيغ يدوية)؛ التواريخ الحرة للمرسوم كما هي
   (`type="text"` + `placeholder="مثال: 1/8/2026"` + `ParseDateTime` — بلا مساس)؛ `min-h-11` لأي عنصر تفاعلي جديد.
5. التحقق الكامل إلزامي: `dotnet test` (1111 + الجديد)، `dotnet build` (صفر تحذيرات)، `npx oxlint src`، `npx tsc -b`،
   `npx vitest run` (688 + الجديد)، `npm run build`، فحص جوال بصري 375px لمعاينة التوحيد.
6. الترتيب: `U1` (استخراج + أخضر) → `U2` (سلوك + اختبارات 1–3، 5) → `U3` (معاينة + 4 + أمامي 7) → `U4` (إصلاح + 6) → `U5` (توثيق) → التحقق الكامل.
7. لا التزام ولا دفع إلا بطلب صريح بعد عرض التقرير.

## المخاطر المسجلة

- تغيير سلوكي مقصود (لا عقدي): الرفض السابق عند التعارض يصبح طيًّا — يُذكر صراحة في تقرير الإنجاز مع اسمي الاختبارين المحوَّلين.
- دلالة `TotalEntriesToMove` (منقول فقط) — يُعوَّض بعرض المطوي بجانبه نصًا وعددًا فلا لبس.
- الحجم (آلاف الملفات): نفس حلقة الدمج المجرّبة + معاملة `RunAsync` واحدة (الكل-أو-العدم) + إعلان الكم في المعاينة؛ لا مهمة خلفية قبل القياس.
- `U4` محصور بوضع الطيّ عمدًا؛ وضع إعادة التعيين له تحليله المستقل لاحقًا إن طُلب.

## ملحق: نتائج المراجعة التحليلية (مُدمجة أعلاه)

| البند | الحكم | الأثر على الخطة |
|---|---|---|
| `F2` معاينة≠تنفيذ في تصادم «ممتصة↔ممتصة» | ثغرة جوهرية مثبتة بالقراءة (`:2116-2121` مقابل `survivorPool`) | أُضيفت المحاكاة التجميعية في `U3` + الحالة الثانية في الاختبار 4 |
| `F1` ضابط `"تعارض: القيد"` | خطأ واقعي: النص في `MoveAllEntriesAsync:1607` لا `MoveEntryAsync:1480-1481` (نصه مختلف) | صُحح الضابط (2) — البقاء لـ`:1607` فقط |
| `U3` سطح المعاينة | مُتحقق: `AllEntitiesUnifyTab.tsx` غير موجود أصلًا — المودال السطح الوحيد | وُثّق في `U3` |
| `F3` الرقم التجميعي | تحسين معتمد | أُضيف `TotalEntriesFolded` للمعاينة والاختبار 4 |
| `F4` سياسة المفتاح الخام | قرار صريح: إبقاء الخام اتساقًا مع الدمج والحرّاس | وُثّق في `U2` (بند 6) |
| `F5` مسار الإلغاء `:2721-2743` | سيمانتك مختلفة (استبدال أسماء مباشر) — الاستثناء صائب | وُثّق في `U1` |
| `F6` الأداء (`N+1`) | مقبول عند قلة التصادمات؛ لا مهمة خلفية قبل القياس | بلا تغيير (مسجّل في المخاطر) |
| دالة مستودع `U4` | مُتحقق: `IUserRepository` بلا استعلام بالقيد | أُضيفت للدالة في `U4` |
| `G2` عدّاد المعاينة `:2111` | فخ تنفيذي: الجملة التجميعية تضم المطوية | نُقل العدّاد داخل حلقة القيد في `U3` |
| `G1` دلالة `entryCount` | غموض عرضي لا عيب | ثُبّتت «كل النشط» في `U3` بلا تغيير |
| `G4` صياغة «غير كاسرة» | دقة تعبير (JSON/API فقط) | صُححت في `U2` (بند 4) |
